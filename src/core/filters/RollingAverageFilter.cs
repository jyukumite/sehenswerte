namespace SehensWerte.Filters
{
    public class RollingAverageFilter : Filter
    {
        private int m_RollFactor;
        private bool m_InitFirst;

        public RollingAverageFilter(int rollFactor, bool initWithFirstInsert = false)
        {
            m_RollFactor = rollFactor;
            m_InitFirst = initWithFirstInsert;
        }

        public override double Insert(double value)
        {
            if (!double.IsFinite(m_LastOutput) || m_InitFirst)
            {
                m_InitFirst = false;
                m_LastOutput = value;
            }
            return m_LastOutput = (m_LastOutput * (double)(m_RollFactor - 1) + value) / (double)m_RollFactor;
        }
    }
}
