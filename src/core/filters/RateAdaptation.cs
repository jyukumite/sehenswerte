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
            Filter.EnsureBufferSize(ref m_OutputBuffer, needed);
            int available = m_OutputBuffer?.TailCount(tail) ?? 0;
            if (available < needed)
            {
                int samples = needed - available;
                OutputBufferUnderflow(samples, underflowMode);
            }
            return m_OutputBuffer?.TailCopy(ref tail, count, stride, underflowMode) ?? new double[0];
        }

        private void OutputBufferUnderflow(int count, Ring<double>.Underflow underflowMode)
        {
            bool stop = false;
            while (count > 0 && !stop)
            {
                double[]? input = null;
                int skip = (int)m_Accumulator;
                if (skip != 0)
                {
                    input = m_SourceFilter?.Copy(ref m_SourceFilterTail, 0, skip, Ring<double>.Underflow.Empty);
                    if (input == null)
                    {
                        stop = true;
                    }
                    else
                    {
                        m_Accumulator -= skip;
                    }
                }
                if (!stop)
                {
                    input = m_SourceFilter?.Copy(ref m_SourceFilterTail, 2, 0, Ring<double>.Underflow.Empty);
                    stop = input == null;
                }
                if (!stop)
                {
                    double input1 = input == null ? 0 : input[0];
                    double input2 = input == null ? 0 : input[1];
                    while (count > 0 && (int)m_Accumulator == 0)
                    {
                        m_OutputBuffer?.Insert((input1 * (1 - m_Accumulator)) + (input2 * m_Accumulator));
                        m_Accumulator += m_Step;
                        count--;
                    }
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
                    (filter == null) ? ri : (IFilterSource)filter,
                    from,
                    newLength);
            FilterOutput ro = new FilterOutput(ra);
            return ro.Get((int)newLength, Ring<double>.Underflow.Zero);
        }
    }
}
