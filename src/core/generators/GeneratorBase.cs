using SehensWerte.Filters;
using SehensWerte.Maths;

namespace SehensWerte.Generators
{
    public abstract class GeneratorBase : IFilterSource, IGenerator
    {
        public abstract double[] Generate(int count);
        protected Ring<double>? m_OutputBuffer;

        public int BufferSize
        {
            get { return m_OutputBuffer?.Length ?? 0; }
            set { Filter.EnsureBufferSize(ref m_OutputBuffer, value); }
        }

        public double[]? Copy(ref int tail, int count, int stride, Ring<double>.Underflow underflowMode)
        {
            int needed = Math.Max(count, stride);
            Filter.EnsureBufferSize(ref m_OutputBuffer, needed * 2);

            int available = m_OutputBuffer?.TailCount(tail) ?? 0;
            if (available < needed)
            {
                m_OutputBuffer!.Insert(Generate(needed - available));
            }
            return m_OutputBuffer!.TailCopy(ref tail, count, stride, underflowMode);
        }

        public void Skip(ref int tail, int skip)
        {
            m_OutputBuffer?.Skip(ref tail, skip);
        }
    }
}
