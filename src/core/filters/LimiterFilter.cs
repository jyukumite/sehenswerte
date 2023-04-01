namespace SehensWerte.Filters
{
    public class LimiterFilter : Filter
    {
        public double m_PositiveLimiter;
        public double m_NegativeLimiter;

        public LimiterFilter(double limiter) : this(limiter, limiter) { }

        public LimiterFilter(double positiveLimiter, double negativeLimiter)
        {
            m_PositiveLimiter = positiveLimiter;
            m_NegativeLimiter = negativeLimiter;
        }

        public override double Insert(double value)
        {
            m_LastInput = value;
            return m_LastOutput = value < m_NegativeLimiter ? m_NegativeLimiter : value > m_PositiveLimiter ? m_PositiveLimiter : value;
        }
    }
}
