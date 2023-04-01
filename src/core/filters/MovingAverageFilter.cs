namespace SehensWerte.Filters
{
    public class MovingAverageFilter : StatsFilter
    {
        public override double LastOutput => m_LastOutput;

        public MovingAverageFilter(int length, bool integer = false) : base(length, integer)
        {
        }

        public MovingAverageFilter(int length, IChainFilter source, bool integer = false) : base(length, integer)
        {
            base.SourceFilter = source;
        }

        public override double Insert(double value)
        {
            m_LastInput = value;
            Delay.Insert(value);
            return m_LastOutput = HistoryMean;
        }
    }
}
