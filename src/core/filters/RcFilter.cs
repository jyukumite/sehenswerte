namespace SehensWerte.Filters
{
    public class RcFilter : Filter
    {
        protected double m_Charge;
        private double m_Coefficient;
        private bool m_First = true;

        public RcFilter(double sampleRate, double r, double c) : this(sampleRate, r * c) { }
        public RcFilter(double sampleRate, double tau) : this(1.0 - Math.Pow(Math.E, (0.0 - (double)(1.0 / sampleRate)) / tau)) { }
        public RcFilter(double coefficient) { m_Coefficient = coefficient; }

        public override double Insert(double value)
        {
            if (m_First)
            {
                m_First = false;
                m_Charge = value;
            }
            double num = value - m_Charge;
            if (AdaptiveOutputLimit > 0.0)
            {
                num = ((num > AdaptiveOutputLimit) ? AdaptiveOutputLimit : ((num < AdaptiveOutputLimit) ? (0.0 - AdaptiveOutputLimit) : num));
            }
            m_Charge += num * m_Coefficient;
            return m_LastOutput = m_Charge;
        }
    }
}
