using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SehensWerte.Filters
{
    // like Statistics, but holds actual history and drops off old values
    // output is the oldest entry in history
    public class StatsFilter : Filter
    {
        protected StatsFilterBase Delay;
        public override double LastOutput => Delay.LastOutput;
        public override double[] History => Delay.History;
        public double HistoryAcRms => Delay.HistoryAcRms;
        public double HistoryMean => Delay.HistoryMean;
        public double HistoryRms => Delay.HistoryRms;
        public double HistoryStd => Delay.HistoryStd;
        public double HistorySum => Delay.HistorySum;
        public double HistorySumSquare => Delay.HistorySumSquare;

        public StatsFilter(int length, bool integer = false)
        {
            Delay = integer ? new StatsFilterFixedPoint(length) : new StatsFilterFloatingPoint(length);
        }

        public StatsFilter(int length, IFilterSource source) : this(length)
        {
            base.SourceFilter = source;
        }

        public override double Insert(double value)
        {
            m_LastInput = value;
            return m_LastOutput = Delay.Insert(value);
        }
    }

    [TestClass]
    public class StatsFilterTests
    {
        [TestMethod]
        public void TestStats()
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
        }
    }
}
