using SehensWerte.Filters;
using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class DelayFilter : Filter
    {
        private Ring<double> m_Ring;
        public DelayFilter(int length) : base()
        {
            m_Ring = new Ring<double>(length);
        }

        public override double Insert(double value)
        {
            return m_Ring.Insert(value);
        }

        // public override double[] Insert(double[] values)
        // { //fixme
        // }
    }
}
