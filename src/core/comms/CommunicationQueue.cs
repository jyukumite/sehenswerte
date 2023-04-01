using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Utils;
using System.Collections.Concurrent;

namespace SehensWerte.Comms
{
    // send (without expecting ack)
    // send (expecting ack, can retry)
    // receive (unsolicited)
    // receive (ack to send)

    public class CommunicationQueue<TSend, TReceive> : IDisposable
    {
        public enum SendResult { Success, Timeout };
        public enum ReceiveResult { Success, Fail };

        public Action<TSend>? OnSent;
        public Action<TSend>? OnSendFail;
        public Action<TSend>? OnSendRetrying;
        public Action<TSend>? OnSendNak;
        public Func<TSend, TReceive, bool>? OnIsAck;

        public Action<TSend>? OnSend; // send a packet

        public int ThreadPollRate_ms = 50;

        private class QueueEntry
        {
            public TSend Sent;
            public bool WaitAck;

            public double SentSeconds;
            public double WaitSeconds;

            public int RetryCount;

            public QueueEntry(TSend sendPacket)
            {
                Sent = sendPacket;
            }
        }

        private ConcurrentQueue<QueueEntry> m_Queue = new ConcurrentQueue<QueueEntry>();
        private QueueEntry? m_Sending;
        public Tuple<TSend, double>? Sending =>
            m_Sending == null
                    ? null
                    : new Tuple<TSend, double>(m_Sending.Sent, m_Sending.SentSeconds); // only use in a callback, won't be threadsafe

        private Object m_SendingLock = new object();

        private EventWaitHandle m_TransmitEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        private EventWaitHandle m_ThreadStop = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread? m_Thread;


        public CommunicationQueue()
        {
        }

        public void Poll()
        {

            lock (m_SendingLock)
            {
                if (m_Sending != null)
                {
                    double now = HighResTimer.StaticSeconds;
                    if ((m_Sending.SentSeconds + m_Sending.WaitSeconds) < now)
                    {
                        if (m_Sending.WaitAck)
                        {
                            if (m_Sending.RetryCount > 0)
                            {
                                m_Sending.RetryCount--;
                                OnSendRetrying?.Invoke(m_Sending.Sent);
                                DoSend();
                            }
                            else
                            {
                                OnSendFail?.Invoke(m_Sending.Sent);
                                m_Sending = null;
                            }
                        }
                        else
                        {
                            OnSent?.Invoke(m_Sending.Sent);
                            m_Sending = null;
                        }
                    }
                }
                if (m_Sending == null)
                {
                    if (m_Queue.TryDequeue(out var tx))
                    {
                        m_Sending = tx;
                        DoSend();
                    }
                }
            }

        }

        private void Run()
        {
            while (!m_ThreadStop.WaitOne(0) && Thread.CurrentThread.ThreadState == ThreadState.Running)
            {
                Poll();
                WaitHandle.WaitAny(new[] { m_ThreadStop, m_TransmitEvent }, ThreadPollRate_ms);
            }
        }

        private void DoSend()
        {
            lock (m_SendingLock)
            {
                if (m_Sending == null) throw new NotImplementedException(); // shouldn't happen
                m_Sending.SentSeconds = HighResTimer.StaticSeconds;
                OnSend?.Invoke(m_Sending.Sent);
            }
        }

        public void Receive(TReceive received)
        {
            lock (m_SendingLock)
            {
                if (m_Sending == null) return;
                if (m_Sending.WaitAck == false) return;
                if (OnIsAck?.Invoke(m_Sending.Sent, received) ?? false)
                {
                    OnSent?.Invoke(m_Sending.Sent);
                    m_Sending = null;
                }
            }
        }

        public void Send(TSend send, double waitSeconds)
        {
            m_Queue.Enqueue(new QueueEntry(send) { WaitSeconds = waitSeconds });
            m_TransmitEvent.Set();
        }

        public void Send(TSend send, double waitSeconds, int retryCount)
        {
            m_Queue.Enqueue(new QueueEntry(send) { WaitAck = true, RetryCount = retryCount, WaitSeconds = waitSeconds });
            m_TransmitEvent.Set();
        }

        public void Dispose()
        {
            Stop();
        }

        public void Clear()
        {
            lock (m_SendingLock)
            {
                m_Sending = null;
                var all = m_Queue.ToList();
                m_Queue.Clear();
                foreach (var v in all)
                {
                    OnSendFail?.Invoke(v.Sent);
                }
            }
        }

        public void Start()
        {
            m_Thread = new Thread(Run);
            m_Thread.Start();
        }

        public void Stop()
        {
            if (m_Thread != null)
            {
                m_ThreadStop.Set();
                m_Thread.Join();
            }
            Clear();
        }
    }

    [TestClass]
    public class CommunicationQueueTest
    {
        [TestMethod]
        public void Test()
        {

        }
    }
}
