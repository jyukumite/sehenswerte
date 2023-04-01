using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class ChainFilterMix : IChainFilter
    {
        private IChainFilter[] m_SourceFilter;
        private int[] m_SourceFilterTail;
        private double[] m_Gain;
        private Ring<double>? m_OutputBuffer;

        public int BufferSize
        {
            get => m_OutputBuffer?.Length ?? 0;
            set { Filter.EnsureBufferSize(ref m_OutputBuffer, value); }
        }

        public ChainFilterMix(IChainFilter[] source, double[] gain = null)
        {
            m_SourceFilter = source;
            m_SourceFilterTail = new int[source.Length];
            m_Gain = gain ?? new double[source.Length].Add(1);
            foreach (var filter in source)
            {
                Filter.EnsureBufferSize(ref m_OutputBuffer, filter.BufferSize);
            }
        }

        public double[]? Copy(ref int tail, int count, int stride, Ring<double>.Underflow underflowMode)
        {
            var needed = Math.Max(count, stride);
            Filter.EnsureBufferSize(ref m_OutputBuffer, needed);
            int available = m_OutputBuffer?.TailCount(tail) ?? 0;
            if (available < needed)
            {
                int samples = needed - available;
                double[]? array = null;
                for (int loop = 0; loop < m_SourceFilter.Length; loop++)
                {
                    double[]? array2 =
                        m_SourceFilter[loop]?.Copy(
                            ref m_SourceFilterTail[loop],
                            samples, samples,
                            Ring<double>.Underflow.Empty)
                        ?.ElementProduct(m_Gain[loop]);
                    array = (array == null) ? array2 : array2 == null ? array : array.Add(array2);
                }
                if (array != null)
                {
                    m_OutputBuffer?.Insert(array);
                }
            }
            return m_OutputBuffer?.TailCopy(ref tail, count, stride, underflowMode) ?? new double[0];
        }
    }
}
