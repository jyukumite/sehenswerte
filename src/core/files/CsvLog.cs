using SehensWerte.Utils;
using System.IO.Compression;

namespace SehensWerte.Files
{
    public class CsvLog
    {
        public enum Priority { Debug = 0, Info, Warn, Error, Exception };
        public string m_ComputerID;
        public string m_ProcessID;
        private string m_FileName;
        public string FileName => m_FileName;
        private StreamWriter? m_StreamWriter = null;
        private GZipStream? m_StreamCompressor = null;
        private Stream? m_StreamFile = null;
        public bool LogTimestamp = true;

        static public Action<Entry> ExtendPath(Action<Entry> prev, string callPath)
        {
            return new Entry.CallPathProxy(prev, callPath).Add;
        }

        public class Entry : ICloneable
        {
            public string Text = "";
            public CsvLog.Priority Priority = CsvLog.Priority.Info;
            public string CallPath = "";
            public string Data = "";
            public string Fields = "";
            public byte[]? Binary = null;
            public string MemberName = "";
            public string SourcePath = "";
            public int SourceLineNumber = 0;
            public int ThreadID = 0;
            public DateTime? Time;

            public Entry() { }
            public Entry(string text,
                         Priority priority,
                         string callPath = "",
                         string data = "",
                         string fields = "",
                         byte[]? binary = null,
                         [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
                         [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
                         [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0,
                         DateTime? logEntryTime = null)
            {
                Text = text;
                Priority = priority;
                CallPath = callPath;
                Data = data;
                Fields = fields;
                Binary = binary;
                MemberName = memberName;
                SourcePath = sourceFilePath;
                SourceLineNumber = sourceLineNumber;
                Time = logEntryTime;
                ThreadID = Thread.CurrentThread.ManagedThreadId;
            }

            public object Clone() => new Entry()
            {
                Text = this.Text,
                Priority = this.Priority,
                CallPath = this.CallPath,
                Data = this.Data,
                Fields = this.Fields,
                Binary = this.Binary,
                MemberName = this.MemberName,
                SourcePath = this.SourcePath,
                SourceLineNumber = this.SourceLineNumber,
                Time = this.Time,
                ThreadID = this.ThreadID
            };

            internal class CallPathProxy
            {
                private Action<CsvLog.Entry> m_Add;
                private string m_CallPath;

                public CallPathProxy(Action<CsvLog.Entry> prev, string callPath)
                {
                    this.m_Add = prev;
                    this.m_CallPath = callPath;
                }

                public void Add(CsvLog.Entry entry)
                {
                    Entry e = (Entry)entry.Clone();
                    e.CallPath = e.CallPath + ":" + m_CallPath;
                    m_Add?.Invoke(e);
                }
            }
        }

        private static string[] Columns =
        {
            "Time", "Priority", "ComputerID", "ProcessID", "ThreadID", "Source", "CallPath", "Text", "Data", "Fields", "Binary"
        };

        public CsvLog(string fileName, string? computerID = null)
        {
            m_ComputerID = computerID ?? Environment.MachineName;
            m_ProcessID = System.Diagnostics.Process.GetCurrentProcess().Id.ToString();
            m_FileName = fileName;

            if (FileName.ToLowerInvariant().EndsWith(".gz"))
            {
                m_StreamFile = File.Open(m_FileName, System.IO.FileMode.Create);
                m_StreamCompressor = new GZipStream(m_StreamFile, CompressionMode.Compress, true);
                m_StreamWriter = new StreamWriter(m_StreamCompressor);
            }
            else
            {
                m_StreamWriter = new StreamWriter(m_FileName, append: true);
                m_StreamWriter.AutoFlush = true;
            }

            CSVSave.SaveRow(m_StreamWriter, Columns, ",");
            Add(new Entry("Logging started - " + m_FileName, Priority.Info));
        }

        public void Add(Entry data)
        {
            if (m_StreamWriter == null) return;
            try
            {
                DateTime logTime = data.Time ?? HighResTimer.StaticNow;
                string[] csv = new string[]
                {
                    logTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    data.Priority.ToString(),
                    m_ComputerID,
                    m_ProcessID.ToString(),
                    data.ThreadID.ToString(),
                    string.Format("{0}[{1}]:{2}", Path.GetFileNameWithoutExtension(data.SourcePath), data.SourceLineNumber, data.MemberName),
                    data.CallPath,
                    data.Text,
                    data.Data,
                    data.Fields,
                    data.Binary == null ? "" : data.Binary.ToHex()
                };
                CSVSave.SaveRow(m_StreamWriter, csv, ",");
            }
            catch { }
        }

        public void Close(string termString = "End of logfile")
        {
            Add(new Entry(termString, Priority.Info));
            m_StreamWriter?.Flush();
            m_StreamCompressor?.Flush();
            m_StreamCompressor?.Close();
            m_StreamCompressor = null;
            try
            {
                if (m_StreamWriter != null && m_StreamWriter.BaseStream != null && m_StreamWriter.BaseStream.CanWrite)
                {
                    m_StreamWriter?.Dispose();
                }
            }
            catch { }
            m_StreamWriter = null;
            m_StreamFile?.Flush();
            m_StreamFile?.Close();
            m_StreamFile = null;
        }

        public void Flush()
        {
            m_StreamCompressor?.Flush();
            m_StreamWriter?.Flush();
            m_StreamFile?.Flush();
        }
    }
}
