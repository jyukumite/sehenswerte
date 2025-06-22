using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Filters;

namespace SehensWerte.Maths
{
    public class SampleWindow
    {
        public enum WindowType
        {
            Blackman,
            FrontBackQuarterRaisedCosine,
            Hamming,
            Log60dB,
            RaisedCosine, // also Hann
            RaisedCosineSquared,
            Rectangular,
            // fixme: add Rectangular6dBPerBin - soft edge,
        }

        public static double[] GenerateWindow(int length, WindowType type)
        {
            Func<double, double>? fn;
            double[] array = new double[length];
            switch (type)
            {
                case WindowType.Blackman: fn = Blackman; break;
                case WindowType.FrontBackQuarterRaisedCosine: fn = FrontBackQuarterRaisedCosine; break;
                case WindowType.Hamming: fn = Hamming; break;
                case WindowType.Log60dB: fn = Log60dB; break;
                case WindowType.RaisedCosine: fn = RaisedCosine; break;
                case WindowType.RaisedCosineSquared: fn = RaisedCosineSquared; break;
                case WindowType.Rectangular: fn = Rectangular; break;
                default: throw new NotImplementedException();
            }
            int mid = (length + length % 2) / 2;
            for (int loop = 0; loop <= mid; loop++)
            {
                array[loop] = fn((double)loop / (double)(length - 1));
            }
            return array.Reflect();
        }

        internal static double Coefficient(double ratio, WindowType type)
        {
            switch (type)
            {
                case WindowType.Blackman: return Blackman(ratio);
                case WindowType.FrontBackQuarterRaisedCosine: return FrontBackQuarterRaisedCosine(ratio);
                case WindowType.Hamming: return Hamming(ratio);
                case WindowType.Log60dB: return Log60dB(ratio);
                case WindowType.RaisedCosine: return RaisedCosine(ratio);
                case WindowType.RaisedCosineSquared: return RaisedCosineSquared(ratio);
                case WindowType.Rectangular: return Rectangular(ratio);
                default: throw new NotImplementedException();
            }
        }

        public static double Inverse(double value, WindowType windowType)
        {
            switch (windowType)
            {
                case WindowType.Blackman: return InverseBlackman(value);
                case WindowType.RaisedCosine: return InverseRaisedCosine(value);
                case WindowType.Log60dB: return InverseLog60dB(value);
                case WindowType.FrontBackQuarterRaisedCosine: return InverseFrontBackQuarterRaisedCosine(value);
                case WindowType.RaisedCosineSquared: return InverseRaisedCosineSquared(value);
                case WindowType.Rectangular: return InverseRectangular(value);
                case WindowType.Hamming: return InverseHamming(value);
                default: throw new NotImplementedException();
            }
        }

        private static double Blackman(double ratio) => 0.42 - 0.5 * Math.Cos(ratio * Math.PI * 2.0) + 0.08 * Math.Cos(ratio * Math.PI * 4.0);
        private static double FrontBackQuarterRaisedCosine(double ratio) => ratio < 0.25 || ratio > 0.75 ? 0.5 * (1.0 - Math.Cos(ratio * Math.PI * 4.0)) : 1.0;
        private static double Hamming(double ratio) => 0.54 - 0.46 * Math.Cos(ratio * Math.PI * 2.0);
        private static double Log60dB(double ratio) => Math.Pow(10.0, (((ratio > 0.5) ? (1.0 - ratio) : ratio) * 2.0 - 1.0) * 6.0);
        private static double RaisedCosine(double ratio) => 0.5 * (1.0 - Math.Cos(ratio * Math.PI * 2.0));
        private static double Rectangular(double ratio) => ratio >= 0 && ratio < 1.0 ? 1.0 : 0.0; // rectangular BETWEEN 0 and 1, so inverse has a chance and we don't need to use epsilon or 1.0+epsilon(?) or [double]0x3FF0000000000001 or 1.00000000000000015
        private static double RaisedCosineSquared(double ratio) => Math.Pow(0.5 * (1.0 - Math.Cos(ratio * Math.PI * 2.0)), 2.0);

        private static double InverseBlackman(double value) => value.FindIn(Blackman, left: 0.0, right: 0.5, steps: 20); //fixme!
        private static double InverseRectangular(double value) => value < 0.5 ? 0 : 0.5;
        private static double InverseFrontBackQuarterRaisedCosine(double value) => (Math.PI - Math.Acos(2.0 * value - 1.0)) / (Math.PI * 4.0);
        private static double InverseHamming(double value) => Math.Acos((0.54 - value) / 0.46) / (Math.PI * 2.0);
        private static double InverseLog60dB(double value) => ((Math.Log10(value) / (60 / 10)) + 1.0) / 2.0;
        private static double InverseRaisedCosine(double value) => (Math.PI - Math.Acos(2.0 * value - 1.0)) / (Math.PI * 2.0);
        private static double InverseRaisedCosineSquared(double value) => (Math.PI - Math.Acos(2.0 * Math.Sqrt(value) - 1.0)) * 0.5 / Math.PI;

        public static double[] Window(double[] array, WindowType type)
        {
            return Window(array, GenerateWindow(array.Length, type));
        }

        public static double[] Window(double[] data, double[] window, int start = 0)
        {
            int num = window.Length;
            double[] result = new double[num];
            for (int loop = 0; loop < num; loop++)
            {
                result[loop] = data[loop + start] * window[loop];
            }
            return result;
        }

        public static void Scope(Action<string, double[]> onScope)
        {
            foreach (WindowType window in Enum.GetValues(typeof(WindowType)))
            {
                try { onScope(window.ToString(), GenerateWindow(1000000, window)); } catch { }
                try { onScope($"{window}_1000_2000_6000_7000_0.25_0.75_0.75_0.25", FftFilter.GenerateBandpassCoefficients(10000, 0.25, 1000.0, 0.75, 2000.0, 0.75, 6000.0, 0.25, 7000.0, window)); } catch { }
            }
        }
    }

    [TestClass]
    public class SampleWindowTest
    {

        [TestMethod]
        public void TestBlackman()
        {
            const double r = 0.3;
            double a = SampleWindow.Coefficient(r, SampleWindow.WindowType.Blackman);
            double b = SampleWindow.Inverse(a, SampleWindow.WindowType.Blackman);
            Assert.IsTrue(Math.Abs(b - r) < 0.0001);
        }

        //fixme - more unit tests
    }
}
