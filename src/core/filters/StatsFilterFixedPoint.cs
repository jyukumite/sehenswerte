namespace SehensWerte.Filters
{
    public class StatsFilterFixedPoint : StatsFilterBase
    {
        protected int[] m_History;
        protected int m_HistorySumQ;
        protected int m_HistorySumSquareQ;
        protected int Qint;
        protected double Qdouble;
        private bool m_First = true;

        public override double LastOutput => m_History[0] / Qdouble;
        public override double[] History => m_History.Select(x => x / Qdouble).ToArray();
        public override double HistorySum => (double)m_HistorySumQ / Qdouble;
        public override double HistorySumSquare => (double)m_HistorySumSquareQ / Qdouble;
        public override double HistoryMean => HistorySum / (double)m_History.Length;
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

        public StatsFilterFixedPoint(int length, int q = 16)
        {
            m_History = new int[length];
            Qint = 1 << q;
            Qdouble = Qint;
        }

        public override double Insert(double value)
        {
            double num = value * value;

            m_HistorySumQ += (int)(value * Qdouble);
            m_HistorySumSquareQ += (int)(num * Qdouble);

            if (m_First)
            {
                // fill the history buffer with the first value given
                m_First = false;
                for (int loop = 1; loop < m_History.Length; loop++)
                {
                    num = value * value;
                    m_HistorySumQ += (int)(value * Qdouble);
                    m_HistorySumSquareQ += (int)(num * Qdouble);
                    m_History[loop] = (int)(value / Qdouble);
                }
            }

            double oldest = m_History[0];
            double oldestSq = oldest * oldest;
            m_HistorySumQ -= (int)(oldest * Qdouble);
            m_HistorySumSquareQ -= (int)(oldestSq * Qdouble);

            Buffer.BlockCopy(m_History, sizeof(int), m_History, 0, (m_History.Length - 1) * sizeof(int)); //fixme: faster than a Ring for small buffers, maybe not bigger buffers

            m_History[^1] = (int)(value * Qdouble);
            return m_History[0] / Qdouble;
        }
    }
}
