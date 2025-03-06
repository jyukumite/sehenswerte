using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public abstract class MultiplyAccumulateAdaptiveFilter : MultiplyAccumulateFilter
    {
        protected int m_SourceDesiredBufferTail;

        private IFilterSource? m_SourceFilterDesired;
        protected IFilterSource? SourceFilterDesired { get => m_SourceFilterDesired; set => m_SourceFilterDesired = value; }

        protected bool m_Hold;
        public bool Hold { get => m_Hold; set { m_Hold = value; } }

        protected double m_CoefficientLimit;
        public double CoefficientLimit { get => m_CoefficientLimit; set => m_CoefficientLimit = value; }

        public MultiplyAccumulateAdaptiveFilter(int length) : base(length)
        {
        }

        public override void OutputBufferUnderflow(int count, Ring<double>.Underflow underflowMode)
        {
            double[]? samples = SourceFilter?.Copy(ref m_SourceFilterTail, count, count, underflowMode);
            double[]? desired = m_SourceFilterDesired?.Copy(ref m_SourceDesiredBufferTail, count, count, underflowMode);
            if (samples == null || desired == null) return;

            int len = Math.Min(samples.Length, desired.Length);
            double[] result = new double[count];
            for (int loop = 0; loop < len; loop++)
            {
                result[loop] = Insert(samples[loop], desired[loop]);
            }
            m_OutputBuffer?.Insert(result);
        }

        protected double ClearCoefficientsOnOverflow(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || double.IsPositiveInfinity(value) || double.IsNegativeInfinity(value))
            {
                value = 0.0;
                int length = History.Length;
                for (int loop = 0; loop < length; loop++)
                {
                    m_Coefficients[loop] = 0.0;
                }
            }
            return value;
        }

        public void CheckLimiter()
        {
            if (m_CoefficientLimit == 0.0) return;

            int length = History.Length;
            double max = m_Coefficients.Abs().Max();
            if (max > m_CoefficientLimit)
            {
                double scale = max / m_CoefficientLimit;
                for (int loop = 0; loop < length; loop++)
                {
                    m_Coefficients[loop] /= scale;
                }
            }
        }
    }
}
