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
}
