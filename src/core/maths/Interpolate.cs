using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SehensWerte.Maths
{
    public class Interpolate
    {
        public static double Linear(double[] xv, double[] search, double x)
        {
            int l;
            double f;
            Find(x, xv, out l, out f);
            int r = l == xv.Length - 1 ? l : l + 1;
            return search[l] + (search[r] - search[l]) * f;
        }

        public static double Bilinear(double[] xv, double[] yv, double[,] search, double x, double y)
        {
            int l;
            int t;
            double col;
            double row;
            Find(x, xv, out l, out col);
            Find(y, yv, out t, out row);
            int r = (l == xv.Length - 1) ? l : l + 1;
            int b = (t == yv.Length - 1) ? t : t + 1;
            return (1 - row) * (search[t, l] * (1 - col) + search[t, r] * col) + row * (search[b, l] * (1 - col) + search[b, r] * col);
        }

        public static void Find(double needle, double[] haystack, out int i, out double f)
        {
            if (needle <= haystack[0])
            {
                i = 0;
                f = 0;
            }
            else if (needle >= haystack[haystack.Length - 1])
            {
                i = haystack.Length - 1;
                f = 0;
            }
            else
            {
                for (i = 0; i < haystack.Length - 1 && needle >= haystack[i + 1]; i++)
                    ;
                f = ((needle - haystack[i]) / (haystack[i + 1] - haystack[i]));
            }
        }
    }

    [TestClass]
    public class InterpolateTest
    {
        [TestMethod]
        public void TestLinear()
        {
            double[] cols = new double[] { 2, 3, 4 };
            double[] search = new double[] { 10, 20, 30 };
            Assert.IsTrue((Interpolate.Linear(cols, search, -5) - 10) < 0.001);
            Assert.IsTrue((Interpolate.Linear(cols, search, 2.5) - 15) < 0.001);
            Assert.IsTrue((Interpolate.Linear(cols, search, 50) - 30) < 0.001);
        }

        [TestMethod]
        public void TestBilinear()
        {
            double[] xv = new double[] { 1, 2, 3 };
            double[] yv = new double[] { 10, 20, 30 };
            double[,] search = new double[,] { { 10, 20, 30 }, { 20, 30, 40 }, { 30, 40, 50 } };
            Assert.IsTrue((Interpolate.Bilinear(xv, yv, search, 0, 25) - 25) < 0.001);
            Assert.IsTrue((Interpolate.Bilinear(xv, yv, search, 5, 25) - 45) < 0.001);
            Assert.IsTrue((Interpolate.Bilinear(xv, yv, search, 2.5, 0) - 25) < 0.001);
            Assert.IsTrue((Interpolate.Bilinear(xv, yv, search, 2.5, 50) - 55) < 0.001);
            Assert.IsTrue((Interpolate.Bilinear(xv, yv, search, 2.5, 25) - 40) < 0.001);
        }
    }
}
