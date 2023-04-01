using SehensWerte.Utils;

namespace SehensWerte.Maths
{
    public class CodeProfile
    {
        private static CodeProfile m_Static = new CodeProfile();

        private object m_LockObject = new object();
        protected Dictionary<string, Row> m_Rows = new Dictionary<string, Row>();

        protected class Row
        {
            private bool m_In;
            private double m_InTime;
            public Statistics Stats;

            public string SourceFilePath = "";
            public string MemberName = "";
            public int SourceLineNumber;
            public string Key = "";

            internal void In(string sourceFilePath, string memberName, int sourceLineNumber, string key)
            {
                SourceFilePath = sourceFilePath;
                MemberName = memberName;
                SourceLineNumber = sourceLineNumber;
                Key = key;

                if (!m_In)
                {
                    m_In = true;
                    m_InTime = HighResTimer.StaticSeconds;
                }
            }
            public void Out()
            {
                if (m_In)
                {
                    m_In = false;
                    Stats.Insert(HighResTimer.StaticSeconds - m_InTime);
                }
            }
            public Row()
            {
                Stats = new Statistics();
            }
        }

        public static void GlobalEnter(string key = "",
                        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
                        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
                        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            m_Static.Enter(key, sourceFilePath, memberName, sourceLineNumber);
        }

        public void Enter(string key = "",
                        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
                        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
                        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            lock (m_LockObject)
            {
                string key_ = key == "" ? "" : "_";
                Ensure($"{sourceFilePath}_{memberName}{key_}{key}").In(sourceFilePath, memberName, sourceLineNumber, key);
            }
        }

        public static void GlobalExit(string key = "",
                        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
                        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            m_Static.Exit(key, sourceFilePath, memberName);
        }

        public void Exit(string key = "",
                        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
                        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            lock (m_LockObject)
            {
                string key_ = key == "" ? "" : "_";
                Ensure($"{sourceFilePath}_{memberName}{key_}{key}").Out();
            }
        }

        private Row Ensure(string key)
        {
            if (!m_Rows.TryGetValue(key, out var value))
            {
                value = new Row();
                m_Rows.Add(key, value);
            }
            return value;
        }

        public IEnumerable<string> StaticStringList()
        {
            return m_Static.ToStringList();
        }

        public IEnumerable<string> ToStringList()
        {
            lock (m_LockObject)
            {
                return m_Rows.Select(v =>
                        {
                            Statistics stats = v.Value.Stats;
                            string path = System.IO.Path.GetFileName(v.Value.SourceFilePath);
                            return new Tuple<double, string>(stats.Sum,
                                $"{path}[{v.Value.SourceLineNumber}] {v.Value.MemberName} {v.Value.Key} " +
                                $"({stats.Count} calls) " +
                                $"min={(stats.Min * 1000.0).ToStringRound(3, 1)} ms, " +
                                $"max={(stats.Max * 1000.0).ToStringRound(3, 1)} ms, " +
                                $"avg={(stats.Average * 1000.0).ToStringRound(3, 1)} ms, " +
                                $"total={((double)stats.Count * stats.Average * 1000.0).ToStringRound(3, 1)} ms, " +
                                $"last={(stats.LastInput * 1000.0).ToStringRound(3, 1)} ms");
                        })
                    .OrderBy(x => -x.Item1)
                    .Select(x => x.Item2)
                    .ToArray();
            }
        }

        public static string StaticToString()
        {
            return m_Static.ToString();
        }

        public new string ToString()
        {
            return string.Join("\n", ToStringList());
        }
    }
}
