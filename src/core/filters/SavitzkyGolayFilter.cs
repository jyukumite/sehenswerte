using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class SavitzkyGolayFilter : StatsFilter
    {
        private int m_PolynomialOrder;

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

        public override double Insert(double value)
        {
            m_LastInput = value;
            base.Insert(value);
            double[] coeff = History.PolyFit(m_PolynomialOrder);
            return m_LastOutput = coeff.PolyVal((History.Length - 1) / 2.0);
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
