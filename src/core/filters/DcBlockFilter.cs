using SehensWerte.Filters;

namespace Core.filters
{
    public class DcBlockFilter : StatsFilter
    {
        public DcBlockFilter(int length, bool integer = false) : base(length, integer)
        {
        }

        public override double Insert(double value)
        {
            m_LastInput = value;
            base.Insert(value);
            return m_LastOutput = value - HistoryMean;
        }
    }
}
