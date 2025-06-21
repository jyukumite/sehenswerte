using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class SampleRateChangeFilter : IFilterSource
    {
        // linear interpolation resample

        private Ring<double>? m_OutputBuffer;
        private double m_Step;
        private double m_Accumulator = 0;
        private IFilterSource? m_SourceFilter;
        private int m_SourceFilterTail;

        public int BufferSize { get => m_OutputBuffer?.Length ?? 0; set { Filter.EnsureBufferSize(ref m_OutputBuffer, value); } }
        public SampleRateChangeFilter(IFilterSource source, double from, double to)
        {
            m_Step = from / to;
            Filter.EnsureBufferSize(ref m_OutputBuffer, (int)((source.BufferSize * to / from) + 1));
            m_SourceFilter = source;
        }

        public virtual double[]? Copy(ref int tail, int count, int stride, Ring<double>.Underflow underflowMode)
        {
            int needed = Math.Max(count, stride);
            Filter.EnsureBufferSize(ref m_OutputBuffer, needed * 2);
            int available = m_OutputBuffer?.TailCount(tail) ?? 0;
            if (available < needed)
            {
                Calculate(needed - available);
            }
            return m_OutputBuffer?.TailCopy(ref tail, count, stride, underflowMode);
        }

        public void Skip(ref int tail, int skip)
        {
            m_OutputBuffer?.Skip(ref tail, skip);
        }

        private void Calculate(int count)
        {
            while (count > 0)
            {
                // double m_Accumulator is the position in the input stream
                // while it is >=1, we can skip a sample and decrement the accumulator
                // otherwise walk the accumulator through [0..1] (linear interpolation)
                int skip = (int)m_Accumulator;
                double[]? input = m_SourceFilter?.Copy(ref m_SourceFilterTail, skip + 2, 0, Ring<double>.Underflow.Null);
                if (input != null)
                {
                    m_Accumulator -= skip;
                    m_SourceFilter?.Skip(ref m_SourceFilterTail, skip);

                    double input1 = input[^2];
                    double input2 = input[^1];
                    while (count > 0 && (int)m_Accumulator == 0)
                    {
                        m_OutputBuffer?.Insert((input1 * (1 - m_Accumulator)) + (input2 * m_Accumulator));
                        m_Accumulator += m_Step;
                        count--;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        static public double[]? Resample(double[] vector, double newLength, Filter? filter = null)
        {
            FilterInput ri = new FilterInput();
            ri.BufferSize = vector.Length + 1;
            ri.Insert(vector);
            if (vector.Length > 0)
            {
                ri.Insert(vector[vector.Length - 1]);
            }
            int from = vector.Length;
            int to = (int)newLength;
            if (filter != null)
            {
                filter.SourceFilter = ri;
            }
            SampleRateChangeFilter ra = new SampleRateChangeFilter(
                    (filter == null) ? ri : filter,
                    from,
                    newLength);
            FilterOutput ro = new FilterOutput(ra);
            return ro.Get((int)newLength, Ring<double>.Underflow.Zeros);
        }
    }
}
