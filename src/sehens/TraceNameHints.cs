using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Controls;

namespace SehensWerte
{
    // Describes the decorations a host application adds to trace names, e.g. a per-source-file suffix appended to every trace loaded from that file
    public class TraceNameHints
    {
        public List<string> IdentifyingPrefixes = new List<string>();
        public List<string> IdentifyingSuffixes = new List<string>();

        public bool IsEmpty => IdentifyingPrefixes.Count == 0 && IdentifyingSuffixes.Count == 0;

        public string Strip(string name)
        {
            return Strip(name, out _, out _);
        }

        // strip at most one matching prefix and one matching suffix (longest match wins)
        public string Strip(string name, out string strippedPrefix, out string strippedSuffix)
        {
            strippedPrefix = "";
            strippedSuffix = "";
            string result = name;
            string? prefix = IdentifyingPrefixes
                .Where(x => x.Length > 0 && result.Length > x.Length && result.StartsWith(x, StringComparison.Ordinal))
                .OrderByDescending(x => x.Length)
                .FirstOrDefault();
            if (prefix != null)
            {
                strippedPrefix = prefix;
                result = result.Substring(prefix.Length);
            }
            string? suffix = IdentifyingSuffixes
                .Where(x => x.Length > 0 && result.Length > x.Length && result.EndsWith(x, StringComparison.Ordinal))
                .OrderByDescending(x => x.Length)
                .FirstOrDefault();
            if (suffix != null)
            {
                strippedSuffix = suffix;
                result = result.Substring(0, result.Length - suffix.Length);
            }
            return result;
        }

        public TraceNameHints MergedWith(TraceNameHints other)
        {
            return new TraceNameHints
            {
                IdentifyingPrefixes = IdentifyingPrefixes.Union(other.IdentifyingPrefixes).ToList(),
                IdentifyingSuffixes = IdentifyingSuffixes.Union(other.IdentifyingSuffixes).ToList()
            };
        }
    }

    [TestClass]
    public class TraceNameHintsTest
    {
        [TestMethod]
        public void TestStrip()
        {
            var hints = new TraceNameHints
            {
                IdentifyingSuffixes = new List<string> { "_f1", @"_MCE23589\S109-6EF492B5-sensor.bin" },
                IdentifyingPrefixes = new List<string> { "Csv." }
            };
            Assert.AreEqual("Mcu_ms", hints.Strip(@"Mcu_ms_MCE23589\S109-6EF492B5-sensor.bin"));
            Assert.AreEqual("UTC_seconds", hints.Strip("UTC_seconds_f1", out string prefix, out string suffix));
            Assert.AreEqual("", prefix);
            Assert.AreEqual("_f1", suffix);
            Assert.AreEqual("Speed", hints.Strip("Csv.Speed_f1", out prefix, out suffix));
            Assert.AreEqual("Csv.", prefix);
            Assert.AreEqual("_f1", suffix);
            Assert.AreEqual("NoDecoration", hints.Strip("NoDecoration"));
            Assert.AreEqual("_f1", hints.Strip("_f1")); // never strip to empty
            Assert.AreEqual("x", new TraceNameHints().Strip("x"));

            // longest suffix wins
            var longest = new TraceNameHints { IdentifyingSuffixes = new List<string> { "_f", "_a_f" } };
            Assert.AreEqual("name", longest.Strip("name_a_f", out _, out suffix));
            Assert.AreEqual("_a_f", suffix);
        }

        [TestMethod]
        public void TestMergedWith()
        {
            var a = new TraceNameHints { IdentifyingSuffixes = new List<string> { "_f1", "_f2" } };
            var b = new TraceNameHints
            {
                IdentifyingSuffixes = new List<string> { "_f2", "_f3" },
                IdentifyingPrefixes = new List<string> { "Csv." }
            };
            var merged = a.MergedWith(b);
            CollectionAssert.AreEquivalent(new[] { "_f1", "_f2", "_f3" }, merged.IdentifyingSuffixes);
            CollectionAssert.AreEquivalent(new[] { "Csv." }, merged.IdentifyingPrefixes);
            Assert.IsTrue(new TraceNameHints().IsEmpty);
            Assert.IsFalse(merged.IsEmpty);
        }

        [TestMethod]
        public void TestClearResetsHints()
        {
            var scope = new SehensControl();
            scope.TraceNameHints = new TraceNameHints { IdentifyingSuffixes = new List<string> { "_f1" } };
            Assert.IsFalse(scope.TraceNameHints.IsEmpty);
            scope.Clear();
            Assert.IsTrue(scope.TraceNameHints.IsEmpty);
        }
    }
}
