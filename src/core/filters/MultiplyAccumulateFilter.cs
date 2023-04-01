namespace SehensWerte.Filters
{
    public class MultiplyAccumulateFilter : StatsFilter
    {
        protected double[] m_Coefficients;
        public override double[] Coefficients
        {
            get => m_Coefficients;
            set { m_Coefficients = value; }
        }

        public override double LastOutput => m_LastOutput;

        public MultiplyAccumulateFilter(double[] coefficients, bool integer = false) : base(coefficients.Length, integer)
        {
            m_Coefficients = coefficients;
        }

        public MultiplyAccumulateFilter(int length) : base(length)
        {
            m_Coefficients = new double[length];
        }

        public override double Insert(double value)
        {
            m_LastInput = value;
            base.Insert(value);
            return m_LastOutput = MultiplyAccumulate(m_Coefficients);
        }
    }
}
