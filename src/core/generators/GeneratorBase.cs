using SehensWerte.Filters;
using SehensWerte.Maths;

namespace SehensWerte.Generators
{
    public abstract class GeneratorBase : IChainFilter, IGenerator
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
            int max = Math.Max(count, stride);
            Filter.EnsureBufferSize(ref m_OutputBuffer, max);
            int tailCount = m_OutputBuffer?.TailCount(tail) ?? 0;
            if (tailCount < max)
            {
                m_OutputBuffer?.Insert(Generate(max - tailCount));
            }
            return m_OutputBuffer?.TailCopy(ref tail, count, stride, underflowMode) ?? new double[0];
        }
    }
}
