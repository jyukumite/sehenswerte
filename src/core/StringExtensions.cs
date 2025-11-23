using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Utils;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

        public static T? FromJson<T>(this string data, T? defaultValue = default)
        {
            try
            {
                var options = new JsonSerializerOptions()
                {
                    IncludeFields = true
                };
                options.Converters.Add(new JsonStringEnumConverter());
                return JsonSerializer.Deserialize<T>(data, options) ?? defaultValue;
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

        public static int NaturalCompare(this string lhs, string rhs)
        {
            return NaturalStringCompare.CompareStrings(lhs, rhs);
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

        public static string ToJson<T>(this T source, bool compact = false)
        {
            var options = new JsonSerializerOptions
            {
                IncludeFields = true,
                WriteIndented = !compact,
                //DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                IgnoreReadOnlyFields = true,
                IgnoreReadOnlyProperties = true,
            };
            options.Converters.Add(new JsonStringEnumConverter());

            return source == null ? "" : JsonSerializer.Serialize(source, options);
        }

        public class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => System.Text.Encoding.UTF8;
        }

        static public Guid? ToGuid(this string uuid, bool uuid4 = true)
        {
            try
            {
                string trimmed = uuid.ToLower().Trim();
                if (trimmed.StartsWith("uuid(") && trimmed.EndsWith(")"))
                {
                    trimmed = trimmed.Substring(5, trimmed.Length - 6);
                }
                if (trimmed.StartsWith("'") && trimmed.EndsWith("'"))
                {
                    trimmed = trimmed.Substring(1, trimmed.Length - 2);
                }
                if (trimmed == "")
                {
                    return null;
                }
                var pattern = uuid4
                    ? @"^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$"
                    : @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";
                if (!Regex.IsMatch(trimmed, pattern, RegexOptions.IgnoreCase))
                {
                    return null;
                }
                return Guid.Parse(trimmed);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    [TestClass]
    public class StringTests
    {
        [TestMethod]
        public void TestConstantTimeCompare()
        {
            Assert.IsTrue("test".CompareConstantTime("test"));
            Assert.IsFalse("test".CompareConstantTime("test2"));
            Assert.IsFalse("test2".CompareConstantTime("test"));
            Assert.IsFalse("aaa".CompareConstantTime("aba"));
            Assert.IsFalse("aba".CompareConstantTime("aaa"));
            //fixme? test that it IS constant time?
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

        [TestMethod]
        public void ToGuid_MultipleTests()
        {
            var testCases = new[]
            {
                new { Uuid4 = true,  Input =      "e02fa0e4-01ad-4a0a-8130-0d05a0008ba0",    Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-4a0a-8130-0d05a0008ba0") },
                new { Uuid4 = true,  Input = "UUID(e02fa0e4-01ad-4a0a-8130-0d05a0008ba0)",   Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-4a0a-8130-0d05a0008ba0") },
                new { Uuid4 = true,  Input =     "'e02fa0e4-01ad-4a0a-8130-0d05a0008ba0'",   Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-4a0a-8130-0d05a0008ba0") },
                new { Uuid4 = true,  Input =   "   e02fa0e4-01ad-4a0a-8130-0d05a0008ba0   ", Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-4a0a-8130-0d05a0008ba0") },
                new { Uuid4 = true,  Input =      "e02fa0e4-01ad-090A-8130-0d05a0008ba0",    Expected = (Guid?)null },
                new { Uuid4 = true,  Input = "UUID(e02fa0e4-01ad-090A-8130-0d05a0008ba0)",   Expected = (Guid?)null },
                new { Uuid4 = true,  Input =     "'e02fa0e4-01ad-090A-8130-0d05a0008ba0'",   Expected = (Guid?)null },
                new { Uuid4 = true,  Input =      "e02fa0e4-01ad-490A-c130-0d05a0008ba0",    Expected = (Guid?)null },
                new { Uuid4 = true,  Input = "UUID(e02fa0e4-01ad-490A-c130-0d05a0008ba0)",   Expected = (Guid?)null },
                new { Uuid4 = true,  Input =     "'e02fa0e4-01ad-490A-c130-0d05a0008ba0'",   Expected = (Guid?)null },
                new { Uuid4 = true,  Input = "invalid-guid", Expected = (Guid?)null },
                new { Uuid4 = true,  Input = "", Expected = (Guid?)null },
                new { Uuid4 = true,  Input = "UUID(e02fa0e4-01ad)", Expected = (Guid?)null },
                new { Uuid4 = false, Input =      "e02fa0e4-01ad-4a0a-8130-0d05a0008ba0",    Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-4a0a-8130-0d05a0008ba0") },
                new { Uuid4 = false, Input = "UUID(e02fa0e4-01ad-4a0a-8130-0d05a0008ba0)",   Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-4a0a-8130-0d05a0008ba0") },
                new { Uuid4 = false, Input =     "'e02fa0e4-01ad-4a0a-8130-0d05a0008ba0'",   Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-4a0a-8130-0d05a0008ba0") },
                new { Uuid4 = false, Input =   "   e02fa0e4-01ad-4a0a-8130-0d05a0008ba0   ", Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-4a0a-8130-0d05a0008ba0") },
                new { Uuid4 = false, Input =      "e02fa0e4-01ad-090A-8130-0d05a0008ba0",    Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-090A-8130-0d05a0008ba0") },
                new { Uuid4 = false, Input = "UUID(e02fa0e4-01ad-090A-8130-0d05a0008ba0)",   Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-090A-8130-0d05a0008ba0") },
                new { Uuid4 = false, Input =     "'e02fa0e4-01ad-090A-8130-0d05a0008ba0'",   Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-090A-8130-0d05a0008ba0") },
                new { Uuid4 = false, Input =      "e02fa0e4-01ad-490A-c130-0d05a0008ba0",    Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-490A-c130-0d05a0008ba0") },
                new { Uuid4 = false, Input = "UUID(e02fa0e4-01ad-490A-c130-0d05a0008ba0)",   Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-490A-c130-0d05a0008ba0") },
                new { Uuid4 = false, Input =     "'e02fa0e4-01ad-490A-c130-0d05a0008ba0'",   Expected = (Guid?)Guid.Parse("e02fa0e4-01ad-490A-c130-0d05a0008ba0") },
                new { Uuid4 = false, Input = "invalid-guid", Expected = (Guid?)null },
                new { Uuid4 = false, Input = "", Expected = (Guid?)null },
                new { Uuid4 = false, Input = "UUID(e02fa0e4-01ad)", Expected = (Guid?)null },
            };

            foreach (var testCase in testCases)
            {
                var result = testCase.Uuid4 ? testCase.Input.ToGuid() : testCase.Input.ToGuid(uuid4: false);
                Assert.AreEqual(testCase.Expected, result, $"Failed for input: {testCase.Input}");
            }
        }
    }
}
