namespace SehensWerte.Filters
{
    public class SteadyStateFilter : StatsFilter
    {
        private double m_StdLimit;
        private int m_Count;
        private bool m_IsSteady = false;
        public bool IsSteady => m_IsSteady;

        public SteadyStateFilter(int length, double stdLimit) : base(length)
        {
            m_StdLimit = stdLimit;
        }

        public override double Insert(double value)
        {
            m_LastInput = value;
            base.Insert(value);
            m_Count++;
            double historyMean = base.HistoryMean;
            m_IsSteady = m_Count >= History.Length && HistoryStd < m_StdLimit;
            if (m_IsSteady)
            {
                m_LastOutput = value;
            }
            return m_LastOutput;
        }
    }
}
