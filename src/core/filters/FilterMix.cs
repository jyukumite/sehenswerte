using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class FilterMix : IFilterSource
    {
        private IFilterSource[] m_SourceFilter;
        private int[] m_SourceFilterTail;
        private double[] m_Gain;
        private Ring<double>? m_OutputBuffer;

        public int BufferSize
        {
            get => m_OutputBuffer?.Length ?? 0;
            set { Filter.EnsureBufferSize(ref m_OutputBuffer, value); }
        }

        public FilterMix(IFilterSource[] source, double[]? gain = null)
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
            Filter.EnsureBufferSize(ref m_OutputBuffer, needed * 2);
            int available = m_OutputBuffer?.TailCount(tail) ?? 0;
            if (available < needed)
            {
                Calculate(needed - available);
            }
            return m_OutputBuffer?.TailCopy(ref tail, count, stride, underflowMode);
        }

        private void Calculate(int count)
        {
            List<double[]> arrays = new();
            for (int loop = 0; loop < m_SourceFilter.Length; loop++)
            {
                double[]? array = m_SourceFilter[loop]?.Copy(
                                        ref m_SourceFilterTail[loop],
                                        count, 0,
                                        Ring<double>.Underflow.Available);
                if (array != null)
                {
                    arrays.Add(array.ElementProduct(m_Gain[loop]));
                }
            }
            double[] result;
            if (arrays.Count == 1)
            {
                result = arrays[0];
            }
            else
            {
                result = new double[arrays.Max(a => a.Length)];
                int length = result.Length;
                foreach (var array in arrays)
                {
                    for (int loop = 0; loop < length; loop++)
                    {
                        result[loop] += array[loop];
                    }
                }
            }
            for (int loop = 0; loop < m_SourceFilter.Length; loop++)
            {
                m_SourceFilter[loop]?.Skip(ref m_SourceFilterTail[loop], result.Length);
            }
            m_OutputBuffer?.Insert(result);
        }

        public void Skip(ref int tail, int skip)
        {
            m_OutputBuffer?.Skip(ref tail, skip);
        }
    }
}
