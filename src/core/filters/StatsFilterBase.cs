namespace SehensWerte.Filters
{
    public abstract class StatsFilterBase
    {
        public abstract double HistoryAcRms { get; }
        public abstract double HistoryMean { get; }
        public abstract double HistoryRms { get; }
        public abstract double HistoryStd { get; }
        public abstract double HistorySum { get; }
        public abstract double HistorySumSquare { get; }
        public abstract double LastOutput { get; }
        public abstract double[] History { get; }
        public abstract double Insert(double value);
    }
}
