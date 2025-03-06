namespace SehensWerte.Filters
{
    public class MovingRmsFilter : StatsFilter
    {
        public override double LastOutput => m_LastOutput;

        public MovingRmsFilter(int length, bool integer = false) : base(length, integer)
        {
        }

        public MovingRmsFilter(int length, IFilterSource source, bool integer = false) : base(length, integer)
        {
            base.SourceFilter = source;
        }

        public override double Insert(double value)
        {
            m_LastInput = value;
            Delay.Insert(value);
            return m_LastOutput = HistoryRms;
        }
    }
}
