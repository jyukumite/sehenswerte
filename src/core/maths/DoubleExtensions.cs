using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SehensWerte.Maths
{
    public static class DoubleExtensions
    {
        public static double DegreesToRadians(this double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        public static double RadiansToDegrees(this double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        public static double FixToRange(this double num, double low, double high)
        {
            return num < low ? low : num > high ? high : num;
        }

        public static double FindIn(this double needle, Func<double, double> haystack, double left, double right, int steps = 20)
        {
            while (steps > 0)
            {
                double median = (left + right) / 2;
                double diff = haystack(median) - needle;
                if (diff < 0)
                {
                    left = median;
                }
                else
                {
                    right = median;
                }
                steps--;
            }
            return (left + right) / 2;
        }

        private static double RoundSignificant(
                        this double value,
                        int significantDigits,
                        double significanceOf,
                        Func<double, double> ifNegative,
                        Func<double, double> ifPositive)
        {
            bool negative = value < 0;
            double abs = negative ? -value : value;
            significanceOf = Math.Abs(significanceOf);
            if (significanceOf != 0.0 && value != 0.0)
            {
                double prefix = Math.Floor(Math.Log10(significanceOf));
                double mag = Math.Pow(10.0, prefix - (double)significantDigits + 1.0);
                abs = (negative ? ifNegative : ifPositive)(abs / mag) * mag;
            }
            return negative ? -abs : abs;
        }

        public static double RoundSignificant(this double value, int significantDigits)
        {
            return value.RoundSignificant(significantDigits, value, new Func<double, double>(Math.Round), new Func<double, double>(Math.Round));
        }

        public static double RoundSignificant(this double value, int significantDigits, double significanceOf)
        {
            return value.RoundSignificant(significantDigits, significanceOf, new Func<double, double>(Math.Round), new Func<double, double>(Math.Round));
        }

        public static double RoundSignificantDown(this double value, int significantDigits)
        {
            return value.RoundSignificant(significantDigits, value, new Func<double, double>(Math.Ceiling), new Func<double, double>(Math.Floor));
        }

        public static double RoundSignificantDown(this double value, int significantDigits, double significanceOf)
        {
            return value.RoundSignificant(significantDigits, significanceOf, new Func<double, double>(Math.Ceiling), new Func<double, double>(Math.Floor));
        }

        public static double RoundSignificantUp(this double value, int significantDigits)
        {
            return value.RoundSignificant(significantDigits, value, new Func<double, double>(Math.Floor), new Func<double, double>(Math.Ceiling));
        }

        public static double RoundSignificantUp(this double value, int significantDigits, double significanceOf)
        {
            return value.RoundSignificant(significantDigits, significanceOf, new Func<double, double>(Math.Floor), new Func<double, double>(Math.Ceiling));
        }

        public static string ToStringRound(this double value, int significantDigits, int minimumDecimalDigits, string unit)
        {
            if (double.IsNaN(value)) return "NaN";
            if (double.IsInfinity(value)) return "Infinity";
            if (value == 0.0) return "0";

            string[] prefixes = { "f", "p", "n", "µ", "m", "", "k", "M", "G", "T" };
            string result = "";

            if (unit == "s")
            {
                result = ToStringRoundTime(value, significantDigits, result);
            }
            if (result == "")
            {
                if (unit == "")
                {
                    result = ToStringRound(value, significantDigits, minimumDecimalDigits);
                }
                else
                {
                    int prefixIndex = (int)Math.Floor(Math.Log10(Math.Abs(value)) / 3);
                    prefixIndex = Math.Max(-5, Math.Min(4, prefixIndex));
                    double scaledValue = value / Math.Pow(10, prefixIndex * 3);
                    result = ToStringRound(scaledValue, significantDigits, minimumDecimalDigits) + prefixes[prefixIndex + 5] + unit;
                }
            }
            return result;
        }

        private static string ToStringRoundTime(double value, int significantDigits, string result)
        {
            TimeSpan time = new TimeSpan(0, 0, 0, (int)value, (int)(1000.0 * (value - (double)(int)value)));
            string format = "";
            if (time.Days != 0)
            {
                format = "d\\dhh\\hmm";
            }
            else if (time.Hours != 0)
            {
                format = ((time.Seconds == 0) ? "h\\hmm" : "h\\hmm\\:ss");
            }
            else if (time.Minutes != 0)
            {
                format = "m\\:ss";
                if (time.Milliseconds != 0)
                {
                    if (significantDigits >= 3)
                    {
                        format += "\\.";
                    }
                    for (int loop = 3; loop <= significantDigits; loop++)
                    {
                        format += "f";
                    }
                }
            }
            if (format != "")
            {
                if (value < 0.0)
                {
                    format = "\\-" + format;
                }
                result = time.ToString(format);
            }

            return result;
        }

        public static string ToStringRound(this double value, int significantDigits, int minimumDecimalDigits)
        {
            if (double.IsNaN(value)) return "NaN";
            if (double.IsInfinity(value)) return "Inf";
            if (value == 0.0) return "0";

            double absValue = Math.Abs(value);
            int wholeDigits = (int)Math.Floor(Math.Log10(absValue)) + 1;
            if (wholeDigits < -18 || wholeDigits > 18)
            {
                return $"{value:#.##E+0}";
            }

            wholeDigits -= significantDigits;
            if (wholeDigits > -minimumDecimalDigits)
            {
                wholeDigits = -minimumDecimalDigits;
            }
            try
            {
                decimal d = (decimal)absValue;
                wholeDigits = Math.Min(25, -wholeDigits);
                d = decimal.Round(d, wholeDigits, MidpointRounding.AwayFromZero);
                d /= 1.0000000000000000000000000000m;
                return (value < 0.0 ? (-d) : d).ToString();
            }
            catch (OverflowException)
            {
                return "Overflow";
            }
        }

        public static double WrapAngle(this double angle, double maxFromZero)
        {
            double num = angle;
            if (num < -maxFromZero)
            {
                num += maxFromZero * 2.0 * Math.Ceiling(Math.Truncate(-num / maxFromZero) / 2.0);
            }
            else if (num > maxFromZero)
            {
                num -= maxFromZero * 2.0 * Math.Ceiling(Math.Truncate(num / maxFromZero) / 2.0);
            }
            return num;
        }

        public static double WrapRadians(this double angle)
        {
            return angle.WrapAngle(Math.PI);
        }

        public static double WrapDegrees(this double angle)
        {
            return angle.WrapAngle(180.0);
        }
    }

    [TestClass]
    public class DoubleTests
    {
        [TestMethod]
        public void TestDegreesRadians()
        {
            Assert.IsTrue(Math.Abs((45.0).DegreesToRadians() - (Math.PI / 4)) < 0.0001);
            Assert.IsTrue(Math.Abs((Math.PI / 4).RadiansToDegrees() - 45) < 0.0001);
        }

        [TestMethod]
        public void TestRange()
        {
            Assert.AreEqual((2.0).FixToRange(1, 4), 2);
            Assert.AreEqual((-2.0).FixToRange(1, 4), 1);
            Assert.AreEqual((5.0).FixToRange(1, 4), 4);
        }

        [TestMethod]
        public void TestFind()
        {
            Assert.IsTrue(Math.Abs((1.7).FindIn(x => x - 3, 1, 10) - 4.7) < 0.001);
        }

        [TestMethod]
        public void TestRound()
        {
            Action<double, double> test = (a, b) => Assert.IsTrue(Math.Abs(a - b) < 0.001);

            test((12.345678).RoundSignificant(4), 12.35);
            test((-12.345678).RoundSignificant(4), -12.35);
            test((12.345678).RoundSignificant(4, 1.2345678), 12.346);
            test((12.345678).RoundSignificant(4, -1.2345678), 12.346);

            test((12.345678).RoundSignificantDown(5), 12.345);
            test((-12.345678).RoundSignificantDown(5), -12.346);
            test((12.345678).RoundSignificantDown(5, 1.2345678), 12.345);
            test((-12.345678).RoundSignificantDown(5, -1.2345678), -12.3457);

            test((12.345678).RoundSignificantUp(5), 12.346);
            test((-12.345678).RoundSignificantUp(5), -12.345);
            test((12.345678).RoundSignificantUp(4, 1.2345678), 12.346);
            test((-12.345678).RoundSignificantUp(4, 1.2345678), -12.345);


        }

        [TestMethod]
        public void TestToStringRound()
        {
            Assert.AreEqual((12.3456789).ToStringRound(3, 2), "12.35");
            Assert.AreEqual((12.3).ToStringRound(3, 2), "12.3");
            Assert.AreEqual((12345.3).ToStringRound(3, 0), "12345");
            Assert.AreEqual((12.345).ToStringRound(3, 0), "12.3");
            Assert.AreEqual((12.345).ToStringRound(3, 2), "12.35");
            Assert.AreEqual((12.3456).ToStringRound(3, 3), "12.346");

            Assert.AreEqual((-12.3456789).ToStringRound(3, 2), "-12.35");
            Assert.AreEqual((-12.3).ToStringRound(3, 2), "-12.3");
            Assert.AreEqual((-12345.3).ToStringRound(3, 0), "-12345");
            Assert.AreEqual((-12.345).ToStringRound(3, 0), "-12.3");
            Assert.AreEqual((-12.345).ToStringRound(3, 2), "-12.35");
            Assert.AreEqual((-12.3456).ToStringRound(3, 3), "-12.346");
            Assert.AreEqual((1e-40).ToStringRound(3, 3), "1E-40");

            Assert.AreEqual((-12.3456).ToStringRound(3, 3, "Hz"), "-12.346Hz");
            Assert.AreEqual((-1234.56789).ToStringRound(3, 3, "Hz"), "-1.235kHz");
            Assert.AreEqual((-0.00123456789).ToStringRound(3, 3, "Hz"), "-1.235mHz");

            Assert.AreEqual((1e-40).ToStringRound(3, 3, "Hz"), "1E-25fHz");

            Assert.AreEqual((1e-40).ToStringRound(3, 3, "s"), "1E-25fs");
            Assert.AreEqual((0.000123456).ToStringRound(3, 3, "s"), "123.456µs");
            Assert.AreEqual((-0.000123456).ToStringRound(3, 3, "s"), "-123.456µs");

            Assert.AreEqual((1234.56).ToStringRound(3, 3, "s"), "20:34.5");
            Assert.AreEqual((1234567.89).ToStringRound(3, 3, "s"), "14d06h56");
            Assert.AreEqual((-1234567.89).ToStringRound(3, 3, "s"), "-14d06h56");
        }

        [TestMethod]
        public void TestWrapAngle()
        {
            Action<double, double> test = (a, b) => Assert.IsTrue(Math.Abs(a - b) < 0.001);
            test((100.0).WrapDegrees(), 100.0);
            test((200.0).WrapDegrees(), 200 - 360.0);
            test((-200.0).WrapDegrees(), -200 + 360.0);

            test((1.0).WrapRadians(), 1.0);
            test((2.0).WrapRadians(), 2.0);
            test((3.0).WrapRadians(), 3.0);
            test((-3.0).WrapRadians(), -3.0);
            test((4.0).WrapRadians(), 4.0 - Math.PI * 2);
            test((-4.0).WrapRadians(), -4.0 + Math.PI * 2);

            //public static double WrapAngle(this double angle, double maxFromZero) (tested by WrapRadians and WrapDegrees)
        }
    }
}
