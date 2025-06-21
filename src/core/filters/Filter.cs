using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public abstract class Filter : IFilter, IFilterSource
    {
        // IFilter - simple filter
        public abstract double Insert(double value);

        public virtual double[] Insert(double[] values)
        {
            int length = values.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = Insert(values[loop]);
            }
            return result;
        }

        // IFilter - delay filter
        protected double m_LastOutput = 0.0;
        protected double m_LastInput = 0.0;
        public virtual double LastOutput => m_LastOutput;
        public virtual double LastInput => m_LastInput;

        public virtual double[] History => new double[] { m_LastInput };
        private double[] m_Coefficients = new double[0];
        public virtual double[] Coefficients { get => m_Coefficients; set { m_Coefficients = value; } }

        // IFilter - adaptive filter
        public virtual double Insert(double value, double desired)
        {
            return Insert(value);
        }

        public virtual double[] Insert(double[] values, double[] desired)
        {
            if (values.Length != desired.Length) throw new Exception("Length mismatch");
            int length = values.Length;
            double[] result = new double[length];
            for (int loop = 0; loop < length; loop++)
            {
                result[loop] = Insert(values[loop], desired[loop]);
            }
            return result;
        }

        protected bool m_AdaptiveHold = false;
        public bool AdaptiveHold { get => m_AdaptiveHold; set => m_AdaptiveHold = value; }

        protected double m_AdaptiveOutputLimit = double.MaxValue;
        public double AdaptiveOutputLimit { get => m_AdaptiveOutputLimit; set => m_AdaptiveOutputLimit = value; }

        // IFilterSource - chain filter

        protected int m_SourceFilterTail;

        protected IFilterSource? m_SourceFilter;
        public IFilterSource? SourceFilter
        {
            get { return m_SourceFilter; }
            set { m_SourceFilter = value; EnsureBufferSize(value?.BufferSize ?? 0); }
        }

        protected Ring<double>? m_OutputBuffer;
        public int BufferSize { get => m_OutputBuffer?.Length ?? 0; set { EnsureBufferSize(value); } }

        public virtual double[]? Copy(ref int tail, int count, int stride, Ring<double>.Underflow underflowMode)
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

        protected virtual void Calculate(int count)
        {
            double[]? value = m_SourceFilter?.Copy(ref m_SourceFilterTail, count, count, Ring<double>.Underflow.Available);
            if (value != null)
            {
                count = value.Length;
                double[] result = new double[count];
                for (int loop = 0; loop < count; loop++)
                {
                    result[loop] = Insert(value[loop]);
                }
                m_OutputBuffer?.Insert(result);
            }
        }

        public void Skip(ref int tail, int skip)
        {
            m_OutputBuffer?.Skip(ref tail, skip);
        }

        public void EnsureBufferSize(int count)
        {
            Filter.EnsureBufferSize(ref m_OutputBuffer, count);
        }

        public static void EnsureBufferSize(ref Ring<double>? buffer, int count)
        {
            if (buffer == null || buffer.Length < count)
            {
                if (buffer != null && buffer.Count != 0)
                {
                    throw new Exception("Output ring buffer is too short but contains data");
                }
                buffer = new Ring<double>(Math.Max(1000, count)); //fixme: assumes a buffer size. Maybe have callers provide a hint.
            }
        }

        protected double MultiplyAccumulate()
        {
            return MultiplyAccumulate(Coefficients);
        }

        public double MultiplyAccumulate(double[] rhs)
        {
            return rhs.DotProduct(History);
        }
    }
}
