using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace SehensWerte
{
    public class NaturalStringCompare : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            return (x == null || y == null) ? -1 : NaturalStringCompare.CompareStrings(x, y);
        }

        //note: only understands invariant culture numbers (period for decimals, numeric comma ignored)
        public static int CompareStrings(string lhs, string rhs)
        {
            int lhsLength = lhs.Length;
            int rhsLength = rhs.Length;
            int idxl = 0;
            int idxr = 0;
            while (idxl < lhsLength && idxr < rhsLength)
            {
                (var isNuml, var textl) = SkipForward(lhs, ref idxl);
                (var isNumr, var textr) = SkipForward(rhs, ref idxr);

                int result;
                if (isNuml && isNumr)
                {
                    // double.TryParse InvariantCulture ignores commas before a period
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
        }

        private static bool IsDottedNumeric(string s, int offset)
        {
            char cm1 = offset != 0 ? s[offset - 1] : '\0';
            if (cm1 != '.') return false;

            char c0 = ((s.Length - offset) > 0) ? s[offset] : '\0';
            if (!char.IsDigit(c0)) return false;

            offset++;
            while (offset < s.Length)
            {
                char c1 = s[offset];
                if (c1 == '.') return true;
                if (!char.IsDigit(c1)) return false;
                offset++;
            }
            return false;
        }

        private static bool IsNumeric(string s, int offset, ref bool dot)
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
            { // comma as part of a number
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

        private static (bool isNum, string text) SkipForward(string str, ref int idx)
        {
            int length = str.Length;
            char[] result = new char[length];
            int idx2 = 0;
            bool dot = false;
            bool isNum = IsNumeric(str, idx, ref dot);
            bool isDottedNum = IsDottedNumeric(str, idx); // middle of a dotted numeric

            do
            {
                if (!isDottedNum && IsDottedNumeric(str, idx)) break;
                result[idx2++] = str[idx];
                if (++idx >= length) break;
                if (isDottedNum && str[idx - 1] == '.') break;
            } while (IsNumeric(str, idx, ref dot) == isNum);
            return (isNum, new string(result, 0, idx2));
        }

        [TestClass]
        public class NaturalCompareTest
        {
            [TestMethod]
            public async void Test()
            {
                Action<string, string> testless = (a, b) =>
                {
                    Assert.IsTrue(CompareStrings(a, b) < 0);
                    Assert.IsTrue(a.NaturalCompare(b) < 0);
                    Assert.IsTrue(CompareStrings(b, a) > 0);
                    Assert.IsTrue(b.NaturalCompare(a) > 0);
                };
                Action<string, string> testsame = (a, b) =>
                {
                    Assert.IsTrue(a.NaturalCompare(b) == 0);
                    Assert.IsTrue(CompareStrings(a, b) == 0);
                    Assert.IsTrue(b.NaturalCompare(a) == 0);
                    Assert.IsTrue(CompareStrings(b, a) == 0);
                };

                //not sure about this - comparing a double with a dotted numeric: testless("2.73", "2.84.1");

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

                testless("1,234,567.89", "1,23,5000.42"); // commas as part of a number

                testless("1.12.3", "1.23.10"); // multiple periods in this form should cancel a decimal
                testless("1.12.3.20", "1.12.3.100");
                testless("1.12.3.20-a-1.2-b", "2.83.10.0-a-1.25-b");
            }
        }
    }
}
