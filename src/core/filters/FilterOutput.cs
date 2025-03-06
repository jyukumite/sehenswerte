using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class FilterOutput
    {
        private IFilterSource m_Source;
        private int m_Tail;

        public FilterOutput(IFilterSource source)
        {
            m_Source = source;
        }

        public double[]? Get(int samples, Ring<double>.Underflow mode = Ring<double>.Underflow.Zero)
        {
            return m_Source?.Copy(ref m_Tail, samples, samples, mode);
        }
    }
}
