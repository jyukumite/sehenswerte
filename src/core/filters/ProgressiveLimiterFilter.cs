namespace SehensWerte.Filters
{
    public class ProgressiveLimiterFilter : Filter
    {
        public double m_AbsKnee;
        public double m_AbsLimit;
        public double m_ReducedLimit;

        public ProgressiveLimiterFilter()
        {
            m_AbsKnee = 0.5;
            m_AbsLimit = 1;
            m_ReducedLimit = 0.8;
        }

        public ProgressiveLimiterFilter(double knee, double limit, double reducedLimit)
        {
            m_AbsKnee = knee;
            m_AbsLimit = limit;
            m_ReducedLimit = reducedLimit;
        }

        public override double Insert(double value)
        {
            m_LastInput = value;
            double abs = Math.Abs(value);
            if (abs > m_AbsLimit)
            {
                m_LastOutput = m_ReducedLimit;
            }
            else if (abs < m_AbsKnee)
            {
                m_LastOutput = abs;
            }
            else
            {
                double delta = 1.0 - ((abs - m_AbsKnee) / (m_AbsLimit - m_AbsKnee));
                double result = m_AbsKnee + (1.0 - delta * delta) * (m_ReducedLimit - m_AbsKnee);
                m_LastOutput = value < 0 ? -result : result;
            }
            return m_LastOutput;
        }

        public override double[] Insert(double[] values)
        {
            return values.Select(x => Insert(x)).ToArray();
        }
    }
}
