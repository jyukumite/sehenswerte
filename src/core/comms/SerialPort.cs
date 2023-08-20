using SehensWerte.Files;
using System.Collections.Concurrent;

namespace SehensWerte.Comms
{
    public abstract class PortBase : IDisposable
    {
        public int ReadBufferSize = 16384;
        public int ThreadPollRate_ms = 50;
        public Action<byte[]> Receive;
        public Action OnPoll;

        protected Action<CsvLog.Entry> OnLog;

        protected PortBase(Action<CsvLog.Entry> onLog)
        {
            OnLog = onLog;
        }

        public virtual string ConfigString { get { return "<null>"; } set { } }

        public abstract void Close();

        public abstract void Open();

        public abstract void Send(byte[] data);

        public void Dispose()
        {
            Close();
        }
    }

    public class SerialPort : PortBase
    {
        public override string ConfigString
        {
            set { throw new NotImplementedException(); } // fixme: interpret config, e.g. com1:9600,n,8,1
            get { return $"{Port}:{BaudRate},{Parity.ToString()[0]},{DataBits},{StopBits.ToString()[0]}"; }
        }

        public string Port = "";
        public int BaudRate = 38400;
        public System.IO.Ports.Parity Parity = System.IO.Ports.Parity.None;
        public int DataBits = 8;
        public System.IO.Ports.StopBits StopBits = System.IO.Ports.StopBits.One;

        private System.IO.Ports.SerialPort? m_SerialPort;
        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread m_Thread;
        private ConcurrentQueue<byte[]> m_TransmitQueue = new ConcurrentQueue<byte[]>();

        private EventWaitHandle m_ReceiveEvent = new System.Threading.EventWaitHandle(false, EventResetMode.AutoReset);
        private EventWaitHandle m_TransmitEvent = new System.Threading.EventWaitHandle(false, EventResetMode.AutoReset);

        public SerialPort(Action<CsvLog.Entry> onLog) : base(onLog)
        {
            m_Thread = new Thread(Run);
        }

        public override void Close()
        {
            try
            {
                if (m_SerialPort == null) return;

                OnLog?.Invoke(new CsvLog.Entry($"Closing {ConfigString}", CsvLog.Priority.Info));
                m_ThreadStop.Set();
                m_Thread.Join();
                if (m_SerialPort != null)
                {
                    m_SerialPort.Close();
                    m_SerialPort = null;
                }
                OnLog?.Invoke(new CsvLog.Entry($"Closed {ConfigString}", CsvLog.Priority.Info));
            }
            catch (Exception e)
            {
                OnLog?.Invoke(new CsvLog.Entry($"Exception closing port {ConfigString} {e.ToString()}", CsvLog.Priority.Exception));
            }
        }

        public override void Open()
        {
            if (m_SerialPort != null) throw new Exception($"Already open {ConfigString}");

            try
            {
                OnLog?.Invoke(new CsvLog.Entry($"Opening {ConfigString}", CsvLog.Priority.Info));
                m_SerialPort = new System.IO.Ports.SerialPort(Port, BaudRate, Parity, DataBits, StopBits);
                m_SerialPort.ReadBufferSize = ReadBufferSize;
                m_SerialPort.Open();

                m_Thread.Start();
                OnLog?.Invoke(new CsvLog.Entry($"Opened {ConfigString}", CsvLog.Priority.Info));
            }
            catch
            {
                Close();
                throw;
            }
        }

        private void Run()
        {
            OnLog?.Invoke(new CsvLog.Entry($"Thread {ConfigString} started", CsvLog.Priority.Debug));

            try
            {
                if (m_SerialPort == null) throw new Exception($"Port {ConfigString} null");

                Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                m_SerialPort.ReadTimeout = 1;
                m_SerialPort.ErrorReceived += (o, e) =>
                {
                    OnLog?.Invoke(new CsvLog.Entry($"Error on {ConfigString}: {e.EventType}", CsvLog.Priority.Exception));
                };

                while (Thread.CurrentThread.ThreadState == ThreadState.Running
                    && !m_ThreadStop.WaitOne(ThreadPollRate_ms))
                {
                    int bytesToRead = m_SerialPort.BytesToRead;
                    if (bytesToRead != 0)
                    {
                        try
                        {
                            byte[] rx = new byte[bytesToRead];
                            try
                            {
                                Receive?.Invoke(rx[0..m_SerialPort.Read(rx, 0, rx.Length)]);
                            }
                            catch (IOException e)
                            {
                                OnLog?.Invoke(new CsvLog.Entry($"{ConfigString} exception " + e.ToString(), CsvLog.Priority.Exception));
                            }
                        }
                        catch (TimeoutException)
                        {
                        }
                    }

                    byte[]? tx;
                    m_TransmitQueue.TryDequeue(out tx);
                    if (tx != null)
                    {
                        m_SerialPort.Write(tx, 0, tx.Length);
                    }

                    OnPoll?.Invoke();
                }
            }
            catch (Exception e)
            {
                OnLog?.Invoke(new CsvLog.Entry($"Thread {ConfigString} exception: " + e.ToString(), CsvLog.Priority.Exception));
            }
            OnLog?.Invoke(new CsvLog.Entry($"Thread {ConfigString} stopped: ", CsvLog.Priority.Debug));
        }


        public override void Send(byte[] data)
        {
            if (m_SerialPort == null) throw new Exception($"Port {ConfigString} not open");
            m_TransmitQueue.Enqueue(data);
            m_TransmitEvent.Set();
        }
    }
}
