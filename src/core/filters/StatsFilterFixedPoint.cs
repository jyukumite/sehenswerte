using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SehensWerte.Filters
{
    public class StatsFilterFixedPoint : StatsFilterBase
    {
        protected int[] m_History;
        protected int m_HistorySumQ;
        protected Int64 m_HistorySumSquareQQ;
        protected int Qint; // e.g. 65536 for 16.16 fixed point
        protected double Qdouble; // e.g. 65536.0 for 16.16 fixed point
        private bool m_First = true;

        public override double LastOutput => m_History[0] / Qdouble;
        public override double[] History => m_History.Select(x => x / Qdouble).ToArray();
        public override double HistorySum => (double)m_HistorySumQ / Qdouble;
        public override double HistorySumSquare => (double)m_HistorySumSquareQQ / Qdouble / Qdouble;
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
            int valQ = (int)(value * Qdouble);
            Int64 squareQQ = (Int64)valQ * (Int64)valQ;

            if (m_First)
            {
                // fill the history buffer with the first value given
                m_First = false;
                for (int loop = 1; loop < m_History.Length; loop++)
                {
                    m_HistorySumQ += valQ;
                    m_HistorySumSquareQQ += squareQQ;
                    m_History[loop] = valQ;
                }
            }

            m_HistorySumQ += valQ;
            m_HistorySumSquareQQ += squareQQ;

            int oldest = m_History[0];
            Int64 oldestSq = (Int64)oldest * (Int64)oldest;
            m_HistorySumQ -= (int)oldest;
            m_HistorySumSquareQQ -= (Int64)oldestSq;

            // BlockCopy is faster than a Ring for small buffers, maybe not bigger buffers
            Buffer.BlockCopy(m_History, sizeof(int), m_History, 0, (m_History.Length - 1) * sizeof(int));

            m_History[^1] = valQ;
            return m_History[0] / Qdouble;
        }
    }
    [TestClass]
    public class StatsFilterFixedPointTests
    {
        [TestMethod]
        public void TestStatsFixedPoint()
        {
            var filter = new StatsFilterFixedPoint(length: 4, q: 16);
            filter.Insert(1.0);
            Assert.AreEqual(1.0, filter.HistoryMean, 0.01);
            filter.Insert(2.0);
            Assert.AreEqual(1.25, filter.HistoryMean, 0.01);
            Assert.AreEqual(1.0, filter.LastOutput, 0.01);
            Assert.AreEqual(5.0, filter.HistorySum, 0.01);
            Assert.AreEqual(7.0, filter.HistorySumSquare, 0.01);
            Assert.AreEqual(Math.Sqrt(7 / 4.0), filter.HistoryRms, 0.0001);
            Assert.AreEqual(Math.Sqrt(0.75 / 4), filter.HistoryStd, 0.0001);
            Assert.AreEqual(filter.HistoryStd, filter.HistoryAcRms, 0.0001);

            double[] hist = filter.History;
            Assert.AreEqual(4, hist.Length);
            Assert.AreEqual(1.0, hist[0]);
            Assert.AreEqual(1.0, hist[1]);
            Assert.AreEqual(1.0, hist[2]);
            Assert.AreEqual(2.0, hist[3]);
        }

        [TestMethod]
        public void TestStatsFixedPointQuantisation()
        {
            // Q=4 gives resolution of 1/16 = 0.0625
            // 0.1 -> (int)(0.1 * 16) = 1 -> 1/16.0 = 0.0625 (not 0.1)
            // 0.3 -> (int)(0.3 * 16) = 4 -> 4/16.0 = 0.25  (not 0.3)
            var filter = new StatsFilterFixedPoint(length: 2, q: 4);
            double quantised01 = (int)(0.1 * 16) / 16.0; // 0.0625
            double quantised03 = (int)(0.3 * 16) / 16.0; // 0.25

            filter.Insert(0.1);
            Assert.AreEqual(quantised01, filter.LastOutput, 1e-10);
            Assert.AreNotEqual(0.1, filter.LastOutput);

            filter.Insert(0.3);
            Assert.AreEqual(quantised01, filter.LastOutput, 1e-10);
            Assert.AreEqual((quantised01 + quantised03) / 2.0, filter.HistoryMean, 1e-10);
            Assert.AreNotEqual((0.1 + 0.3) / 2.0, filter.HistoryMean);
        }
    }
}