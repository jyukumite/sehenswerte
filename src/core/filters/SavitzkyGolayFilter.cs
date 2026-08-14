using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class SavitzkyGolayFilter : StatsFilter
    {
        private int m_PolynomialOrder;
        private double[]? m_Kernel;

        public SavitzkyGolayFilter(int sampleCount, int polynomialOrder) : base(sampleCount)
        {
            m_PolynomialOrder = polynomialOrder;
        }

        public static double[] Window(int sampleCount, int polynomialOrder, double[] samples)
        {
            return new SavitzkyGolayFilter(sampleCount, polynomialOrder).Window(samples);
        }

        public double[] Window(double[] samples)
        {
            return samples.Select((double x) => Insert(x)).ToArray();
        }

        // The window's x values and the evaluation point never change
        private static double[] BuildKernel(int length, int polynomialOrder)
        {
            var kernel = new double[length];
            var impulse = new double[length];
            double centre = (length - 1) / 2.0;
            for (int loop = 0; loop < length; loop++)
            {
                impulse[loop] = 1.0;
                kernel[loop] = impulse.PolyFit(polynomialOrder).PolyVal(centre);
                impulse[loop] = 0.0;
            }
            return kernel;
        }

        public override double Insert(double value)
        {
            m_LastInput = value;
            base.Insert(value);
            double[] history = History;
            m_Kernel ??= BuildKernel(history.Length, m_PolynomialOrder);
            double result = 0.0;
            for (int loop = 0; loop < history.Length; loop++)
            {
                result += m_Kernel[loop] * history[loop];
            }
            return m_LastOutput = result;
        }
    }

    [TestClass]
    public class SavitzkyGolayFilterTests
    {
        [TestMethod]
        public void TestLinearSignalEvaluationPoint()
        {
            var filter = new SavitzkyGolayFilter(sampleCount: 5, polynomialOrder: 2);
            double output = 0;
            for (int i = 0; i <= 4; i++) output = filter.Insert(i);
            Assert.AreEqual(2.0, output, 0.01);
        }
    }
}
