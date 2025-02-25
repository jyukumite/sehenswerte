using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Policy;

namespace SehensWerte.Maths
{
    public class CodeProfile
    {
        private static CodeProfile m_Static = new CodeProfile();
        protected ConcurrentDictionary<string, Row> m_Rows = new ConcurrentDictionary<string, Row>();

        protected class Row
        {
            private ConcurrentDictionary<int, double> m_InTimes = new ConcurrentDictionary<int, double>();
            public Statistics Stats = new();

            public string SourceFilePath = "";
            public string MemberName = "";
            public int SourceLineNumber; // not part of the key, as exit is called on a different line number
            public string Key = "";
            public int MaxThreads;

            internal void In(string sourceFilePath, string memberName, int sourceLineNumber, string key)
            {
                SourceFilePath = sourceFilePath;
                MemberName = memberName;
                SourceLineNumber = sourceLineNumber;
                Key = key;

                int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                m_InTimes.TryAdd(threadId, HighResTimer.StaticSeconds);

                SetMaxThreads(m_InTimes.Count);
            }

            private void SetMaxThreads(int newMax)
            {
                int prevMax;
                do
                {
                    prevMax = MaxThreads;
                } while (newMax > prevMax && Interlocked.CompareExchange(ref MaxThreads, newMax, prevMax) != prevMax);
            }

            public void Out()
            {
                int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (m_InTimes.TryRemove(threadId, out double startTime))
                {
                    lock (Stats)
                    {
                        Stats.Insert(HighResTimer.StaticSeconds - startTime);
                    }
                }
            }

            public new string ToString()
            {
                Statistics stats = Stats;
                string path = System.IO.Path.GetFileName(SourceFilePath);
                string line = SourceLineNumber == 0 ? "" : $"[{SourceLineNumber.ToString()}]";
                return $"{path}{line} {MemberName} {Key} " +
                    $"({stats.Count} calls, {MaxThreads} simultaneous threads) " +
                    $"min={(stats.Min * 1000.0).ToStringRound(3, 1)} ms, " +
                    $"max={(stats.Max * 1000.0).ToStringRound(3, 1)} ms, " +
                    $"avg={(stats.Average * 1000.0).ToStringRound(3, 1)} ms, " +
                    $"total={((double)stats.Count * stats.Average * 1000.0).ToStringRound(3, 1)} ms, " +
                    $"last={(stats.LastInput * 1000.0).ToStringRound(3, 1)} ms";
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
            string key_ = key == "" ? "" : "_";
            Ensure($"{sourceFilePath}_{memberName}{key_}{key}").In(sourceFilePath, memberName, sourceLineNumber, key);
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
            string key_ = key == "" ? "" : "_";
            Ensure($"{sourceFilePath}_{memberName}{key_}{key}").Out();
        }

        public static void GlobalRun(Action run, string key = "",
                [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
                [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
                [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            string sourceFilePathWithLine = sourceFilePath + $"[{sourceLineNumber}]";
            GlobalEnter(key, sourceFilePathWithLine, memberName, 0);
            run();
            GlobalExit(key, sourceFilePathWithLine, memberName);
        }

        public void Run(Action run, string key = "",
                        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
                        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
                        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            string sourceFilePathWithLine = sourceFilePath + $"[{sourceLineNumber}]";
            Enter(key, sourceFilePathWithLine, memberName, 0);
            run();
            Exit(key, sourceFilePathWithLine, memberName);
        }

        private Row Ensure(string key)
        {
            return m_Rows.GetOrAdd(key, _ => new Row());
        }

        public IEnumerable<string> StaticStringList()
        {
            return m_Static.ToStringList();
        }

        public IEnumerable<string> ToStringList()
        {
            return m_Rows.Select(v => (v.Value.Stats.Sum, v.Value.ToString()))
                .OrderBy(x => -x.Item1)
                .Select(x => x.Item2)
                .ToArray();
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

    [TestClass]
    public class CodeProfileTests
    {
        [TestMethod]
        public void CodeProfileTest()
        {
            CodeProfile profile = new();
            for (int loop = 0; loop < 5; loop++)
            {
                profile.Run(() => { Thread.Sleep(5); });
            }
            profile.Run(() => { Thread.Sleep(5); });
            profile.Run(() => { Thread.Sleep(5); }, "function2");

            Parallel.ForEach(Enumerable.Range(1, 1000), x =>
            {
                profile.Run(() => { Thread.Sleep(5); }, "parfor");
            });

            string test = profile.ToString();

            double a = HighResTimer.StaticSeconds;
            int count = 0;
            for (int loop=0; loop<1000000; loop++)
            {
                profile.Run(() => { count++;  }, "function3");
            }
            double b = HighResTimer.StaticSeconds - a;

            //MessageBox.Show($"{count} took {b}"); // last result: 1000000 took 0.595 (595ns each)
            //fixme: Assert.IsFalse(lhs.IsEqualTo(rhs));
        }
    }
}
