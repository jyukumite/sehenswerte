using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace SehensWerte
{
    public static class StringExtensions
    {
        public static bool CompareConstantTime(this string a, string b)
        {
            int result = (char)(a.Length ^ b.Length);
            for (int loop = 0; loop < a.Length && loop < b.Length; loop++)
            {
                result |= (int)(a[loop] ^ b[loop]);
            }
            return result == 0;
        }

        public static T? FromXml<T>(this string data, T? defaultValue = default(T?), Type[]? derivedTypes = null)
        {
            try
            {
                using (StringReader sr = new StringReader(data))
                {
                    object? obj = new XmlSerializer(typeof(T), derivedTypes).Deserialize((TextReader)sr);
                    return obj == null ? defaultValue : (T)obj;
                }
            }
            catch
            {
                return defaultValue;
            }
        }

        public static uint HexToUInt32(this string value)
        {
            try
            {
                return (value == "" || value.StartsWith("-"))
                    ? 0
                    : Convert.ToUInt32(value.ToLower().StartsWith("0x") ? value.Substring(2, value.Length - 2) : value, 16);
            }
            catch
            {
                return 0;
            }
        }

        //note: only understands invariant culture numbers (period for decimals, numberic comma ignored)
        public static int NaturalCompare(this string lhs, string rhs)
        {
            int lhsLength = lhs.Length;
            int rhsLength = rhs.Length;
            int idxl = 0;
            int idxr = 0;
            while (idxl < lhsLength && idxr < rhsLength)
            {
                bool isNuml;
                string textl = SkipForward(lhs, ref idxl, out isNuml);
                bool isNumr;
                string textr = SkipForward(rhs, ref idxr, out isNumr);

                int result;
                if (isNuml && isNumr)
                {
                    double.TryParse(textl, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsel);
                    double.TryParse(textr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parser);
                    result = parsel.CompareTo(parser);
                }
                else
                {
                    result = string.Compare(textl, textr, ignoreCase: true);
                }
                if (result != 0)
                {
                    return result;
                }
            }
            return (idxl == lhsLength && idxr == rhsLength) ? 0 : (lhsLength > rhsLength ? 1 : -1);

            bool IsNumeric(string s, int offset, ref bool dot)
            {
                char cm1 = offset != 0 ? s[offset - 1] : '\0';
                char c0 = ((s.Length - offset) > 0) ? s[offset] : '\0';
                char c1 = ((s.Length - offset) > 1) ? s[offset + 1] : '\0';
                char c2 = ((s.Length - offset) > 2) ? s[offset + 2] : '\0';
                if (char.IsDigit(c0))
                {
                    return true;
                }
                if (!dot && c0 == '.' && char.IsDigit(c1))
                {
                    dot = true;
                    return true;
                }
                if (!dot && c0 == ',' && char.IsDigit(c1))
                {
                    return true;
                }
                if (!dot && !char.IsDigit(cm1) && c0 == '-' && char.IsDigit(c1))
                {
                    return true;
                }
                if (!dot && !char.IsDigit(cm1) && c0 == '-' && c1 == '.' && char.IsDigit(c2))
                {
                    return true;
                }
                return false;
            }

            string SkipForward(string str, ref int idx, out bool isNum)
            {
                int length = str.Length;
                char[] result = new char[length];
                int idx2 = 0;
                bool dot = false;
                isNum = IsNumeric(str, idx, ref dot);
                do
                {
                    result[idx2++] = str[idx];
                    if (++idx >= length) break;
                } while (IsNumeric(str, idx, ref dot) == isNum);
                return new string(result, 0, idx2);
            }
        }

        private static string RtfEncode(char c)
        {
            switch (c)
            {
                case '\n': return "\r\n\\line ";
                case '\\': return "\\\\";
                case '{': return "\\{";
                case '}': return "\\}";
                default:
                    int ch = Convert.ToInt32(c);
                    return char.IsLetter(c) && ch < 128 ? c.ToString() : ("\\u" + ch + "?");
            }
        }

        public static string RtfEncode(this string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s.Replace("\r\n", "\n").Replace("\r", "\n"))
            {
                sb.Append(RtfEncode(c));
            }
            return sb.ToString();
        }

        public static bool ToBool(this string str)
        {
            str = str.Trim();
            if (str.Length == 0) return false;
            if (bool.TryParse(str, out var result)) return result;
            return str[0] == 'y' || str[0] == 'Y' || str[0] == 't' || str[0] == 'T' || str[0] == '1';
        }

        public static double ToDouble(this string input, double defaultValue)
        {
            return double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
                ? result
                : defaultValue;
        }

        public static int ToInt(this string input, int defaultValue)
        {
            return int.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
                ? result
                : defaultValue;
        }

        public static string ToXml<T>(this T source, Type[]? derivedTypes = null, bool compact = false)
        {
            if (source == null) return "";

            XmlSerializer xs = new XmlSerializer(source.GetType(), derivedTypes);
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = !compact,
                OmitXmlDeclaration = compact
            };
            using (StringWriter sw = new Utf8StringWriter())
            using (XmlWriter writer = XmlWriter.Create(sw, settings))
            {
                if (compact)
                {
                    XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
                    namespaces.Add(string.Empty, string.Empty);
                    xs.Serialize(writer, source, namespaces);
                }
                else
                {
                    xs.Serialize(writer, source);
                }
                return sw.ToString();
            }
        }

        public class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => System.Text.Encoding.UTF8;
        }
    }

    [TestClass]
    public class StringTests
    {
        [TestMethod]
        public async void TestConstantTimeCompare()
        {
            Assert.IsTrue("test".CompareConstantTime("test"));
            Assert.IsFalse("test".CompareConstantTime("test2"));
            Assert.IsFalse("test2".CompareConstantTime("test"));
            Assert.IsFalse("aaa".CompareConstantTime("aba"));
            Assert.IsFalse("aba".CompareConstantTime("aaa"));
            //fixme? test that it IS constant time?
        }

        [TestMethod]
        public async void TestNaturalCompare()
        {
            Action<string, string> testless = (a, b) =>
            {
                Assert.IsTrue(a.NaturalCompare(b) < 0);
                Assert.IsTrue(b.NaturalCompare(a) > 0);
            };
            Action<string, string> testsame = (a, b) =>
            {
                Assert.IsTrue(a.NaturalCompare(b) == 0);
            };

            testsame("", "");
            testsame("a2a", "a2a");
            testsame("test2", "test2");
            testsame("test2a", "test2a");
            testsame("a-2a", "a-2a");
            testsame("test-2", "test-2");
            testsame("test-2a", "test-2a");

            testless("", "a");
            testless("a", "aa");
            testless("a", "b");
            testless("a1", "a1a");
            testsame("a1.5a", "a01.5a");
            testless("a1a", "a2a");
            testless("test1", "test2");
            testless("test2", "test12");
            testless("test1a", "test1b");

            testsame("test1.50a", "test1.5a");
            testsame("test-1.50a", "test-1.5a");
            testsame("test1-1.50a", "test1-1.5a"); // negative cancels the number but . brings it back

            testsame("test2022-1-7a", "test2022-01-07a");
            testless("test2022-1-7a", "test2022-02-07a");
            testless("test2022-1-7a", "test2022-01-8a");
            testless("test2022-1-7a", "test2022-1-8a");

            testless("test2022-1.4-7a", "test2022-1.5-8a");
            testless("test2022-1.4-7a", "test2022-1.51-8a");
            testless("test2022-1.419-7a", "test2022-1.420-8a");
            testless("test2022-1.419-7a", "test2022-1.4191-8a");

            testless("a.5b", "a.6b");
            testless("a.5b", "a.51b");
            testless("a0.5b", "a0.51b");
            testless("a-.51b", "a-.5b");
            testless("a-0.51b", "a-0.5b");

            testless("1,234,567.89", "1,23,5000.42");
        }

        [TestMethod]
        public void TestHex()
        {
            Assert.AreEqual("".HexToUInt32(), 0U);
            Assert.AreEqual("1".HexToUInt32(), 1U);
            Assert.AreEqual("-1".HexToUInt32(), 0U);
            Assert.AreEqual("1A".HexToUInt32(), 26U);
            Assert.AreEqual("CafeF00d".HexToUInt32(), 0xcafef00dU);
        }

        [TestMethod]
        public void TestToBool()
        {
            Assert.IsFalse("".ToBool());
            Assert.IsFalse("0".ToBool());
            Assert.IsFalse("F".ToBool());
            Assert.IsFalse("f".ToBool());
            Assert.IsFalse("N".ToBool());
            Assert.IsFalse("n".ToBool());
            Assert.IsFalse("No".ToBool());
            Assert.IsFalse("False".ToBool());
            Assert.IsFalse("false".ToBool());

            Assert.IsTrue("1".ToBool());
            Assert.IsTrue("T".ToBool());
            Assert.IsTrue("t".ToBool());
            Assert.IsTrue("Y".ToBool());
            Assert.IsTrue("y".ToBool());
            Assert.IsTrue("Yes".ToBool());
            Assert.IsTrue("True".ToBool());
            Assert.IsTrue("true".ToBool());
        }

        [TestMethod]
        public void TestToDouble()
        {
            Assert.AreEqual("".ToDouble(0), 0);
            Assert.AreEqual("".ToDouble(1), 1);
            Assert.AreEqual("0".ToDouble(1), 0);
            Assert.AreEqual("0".ToDouble(1), 0);
            Assert.AreEqual("0.0".ToDouble(1), 0);
            Assert.AreEqual("test".ToDouble(1), 1);
            Assert.AreEqual("42.125".ToDouble(1), 42.125);
            Assert.AreEqual("-42.125".ToDouble(1), -42.125);
            Assert.AreEqual("-42.125E+10".ToDouble(1), -42.125E+10);
            Assert.AreEqual("42bob".ToDouble(1), 1);
        }

        [TestMethod]
        public void TestRtf()
        {
            // private static string RtfEncode(char c)
            // public static string RtfEncode(this string s)
        }

    }
}
