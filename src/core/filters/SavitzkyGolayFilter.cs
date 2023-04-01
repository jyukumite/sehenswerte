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
            return m_LastOutput = coeff.PolyVal(m_PolynomialOrder / 2);
        }
    }
}
