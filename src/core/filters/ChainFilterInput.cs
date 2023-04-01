using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class ChainFilterInput : IChainFilter
    {
        private Ring<double>? m_OutputBuffer;

        public int BufferSize
        {
            get => m_OutputBuffer?.Length ?? 0;
            set { Filter.EnsureBufferSize(ref m_OutputBuffer, value); }
        }

        public double[]? Copy(ref int tail, int count, int stride, Ring<double>.Underflow underflowMode)
        {
            int needed = Math.Max(count, stride);
            Filter.EnsureBufferSize(ref m_OutputBuffer, needed);
            return m_OutputBuffer?.TailCopy(ref tail, count, stride, underflowMode) ?? new double[0];
        }

        public void Insert(double[] samples)
        {
            Filter.EnsureBufferSize(ref m_OutputBuffer, samples.Length);
            m_OutputBuffer?.Insert(samples);
        }

        public void Insert(double result)
        {
            Insert(new double[1] { result });
        }
    }
}
