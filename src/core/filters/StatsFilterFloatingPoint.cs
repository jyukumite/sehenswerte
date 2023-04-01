namespace SehensWerte.Filters
{
    public class StatsFilterFloatingPoint : StatsFilterBase
    {
        public double[] m_History;
        private double m_HistorySum;
        private double m_HistorySumSquare;
        private bool m_HistoryRecalculate = true;
        private bool m_First = true;

        public override double LastOutput => m_History[0];
        public override double[] History => m_History;
        public override double HistorySum { get { HistoryCalculate(); return m_HistorySum; } }
        public override double HistorySumSquare { get { HistoryCalculate(); return m_HistorySumSquare; } }
        public override double HistoryMean => HistorySum / m_History.Length;
        public override double HistoryAcRms => HistoryStd;
        public override double HistoryRms => Math.Sqrt(HistorySumSquare / (double)m_History.Length);

        public override double HistoryStd
        {
            get
            {
                double mean = HistorySum / (double)m_History.Length;
                double meanSq = HistorySumSquare / (double)m_History.Length - mean * mean;
                return (meanSq <= 0.0) ? 0.0 : Math.Sqrt(meanSq);
            }
        }

        public StatsFilterFloatingPoint(int length)
        {
            m_History = new double[length];
        }


        public override double Insert(double value)
        {
            if (m_First)
            {
                m_First = false;
                for (int loop = 1; loop < m_History.Length; loop++)
                {
                    m_History[loop] = value;
                }
            }
            Buffer.BlockCopy(m_History, sizeof(double), m_History, 0, (m_History.Length - 1) * sizeof(double)); //fixme: faster than a Ring for small buffers, maybe not bigger buffers
            m_History[^1] = value;
            m_HistoryRecalculate = true;
            return m_History[0];
        }

        private void HistoryCalculate()
        {
            if (!m_HistoryRecalculate) return;

            m_HistorySum = 0;
            m_HistorySumSquare = 0;

            int length = m_History.Length;
            for (int loop = 0; loop < length; loop++)
            {
                double val = m_History[loop];
                m_HistorySum += val;
                m_HistorySumSquare += val * val;
            }

            m_HistoryRecalculate = false;
        }
    }
}
