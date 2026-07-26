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

        // Resample the curve (srcX, srcY) - srcX ascending, same length - onto `count` uniform grid
        // points X_i = offset + multiplier*i, linearly interpolating. Grid points outside
        // [srcX[0], srcX[^1]] return NaN (so a source covering only part of the grid does not draw a
        // flat clamped tail). Empty source => all NaN.
        public static double[] LinearOntoGrid(double[] srcX, double[] srcY, int count, double offset, double multiplier)
        {
            double[] result = new double[count];
            bool empty = srcX.Length == 0;
            double min = empty ? 0.0 : srcX[0];
            double max = empty ? 0.0 : srcX[srcX.Length - 1];
            for (int loop = 0; loop < count; loop++)
            {
                double x = offset + multiplier * loop;
                result[loop] = (empty || x < min || x > max) ? double.NaN : Linear(srcX, srcY, x);
            }
            return result;
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
            Assert.IsTrue(Math.Abs(Interpolate.Linear(cols, search, -5) - 10) < 0.001);
            Assert.IsTrue(Math.Abs(Interpolate.Linear(cols, search, 2.5) - 15) < 0.001);
            Assert.IsTrue(Math.Abs(Interpolate.Linear(cols, search, 50) - 30) < 0.001);
        }

        [TestMethod]
        public void TestBilinear()
        {
            double[] xv = new double[] { 1, 2, 3 };
            double[] yv = new double[] { 10, 20, 30 };
            double[,] search = new double[,] { { 10, 20, 30 }, { 20, 30, 40 }, { 30, 40, 50 } };
            Assert.IsTrue(Math.Abs(Interpolate.Bilinear(xv, yv, search, 0, 25) - 25) < 0.001);
            Assert.IsTrue(Math.Abs(Interpolate.Bilinear(xv, yv, search, 5, 25) - 45) < 0.001);
            Assert.IsTrue(Math.Abs(Interpolate.Bilinear(xv, yv, search, 2.5, 0) - 25) < 0.001);
            Assert.IsTrue(Math.Abs(Interpolate.Bilinear(xv, yv, search, 2.5, 50) - 45) < 0.001);
            Assert.IsTrue(Math.Abs(Interpolate.Bilinear(xv, yv, search, 2.5, 25) - 40) < 0.001);
        }

        [TestMethod]
        public void TestLinearOntoGrid()
        {
            // Non-uniform source (uniform in some other quantity) resampled onto a uniform grid X_i = i.
            double[] srcX = new double[] { 0, 1, 4 };
            double[] srcY = new double[] { 0, 10, 40 };   // slope 10 in [0,1] and [1,4]
            double[] r = Interpolate.LinearOntoGrid(srcX, srcY, count: 6, offset: 0, multiplier: 1);
            Assert.AreEqual(0.0, r[0], 0.001);
            Assert.AreEqual(10.0, r[1], 0.001);
            Assert.AreEqual(20.0, r[2], 0.001);   // interior midpoint interpolates (X=2 -> between (1,10) and (4,40))
            Assert.AreEqual(30.0, r[3], 0.001);
            Assert.AreEqual(40.0, r[4], 0.001);
            Assert.IsTrue(double.IsNaN(r[5]));    // X=5 is past srcX max (4) -> NaN, not flat-clamped
        }

        [TestMethod]
        public void TestLinearOntoGridCrossing()
        {
            // Two curves resampled onto the same grid must cross at the grid index where their values
            // meet (an available-vs-required "operating point"). Rising: y=x; falling: y=10-x.
            double[] xr = new double[] { 0, 10 };
            double[] rising = Interpolate.LinearOntoGrid(xr, new double[] { 0, 10 }, 11, 0, 1);
            double[] falling = Interpolate.LinearOntoGrid(xr, new double[] { 10, 0 }, 11, 0, 1);
            int cross = -1;
            for (int loop = 0; loop < 10; loop++)
            {
                if ((rising[loop] - falling[loop]) == 0.0 ||
                    (rising[loop] - falling[loop]) * (rising[loop + 1] - falling[loop + 1]) < 0.0)
                {
                    cross = loop;
                    break;
                }
            }
            Assert.AreEqual(5, cross);             // x=y=5 at grid index 5
            Assert.AreEqual(5.0, rising[5], 0.001);
            Assert.AreEqual(5.0, falling[5], 0.001);
        }

        [TestMethod]
        public void TestLinearOntoGridEmpty()
        {
            double[] r = Interpolate.LinearOntoGrid(Array.Empty<double>(), Array.Empty<double>(), 3, 0, 1);
            Assert.IsTrue(r.All(double.IsNaN));
        }
    }
}
