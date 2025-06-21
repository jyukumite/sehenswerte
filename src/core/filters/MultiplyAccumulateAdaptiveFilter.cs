using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public abstract class MultiplyAccumulateAdaptiveFilter : MultiplyAccumulateFilter
    {
        protected int m_SourceDesiredBufferTail;

        private IFilterSource? m_SourceDesired;
        public IFilterSource? SourceDesired { get => m_SourceDesired; set => m_SourceDesired = value; }
        private int m_SourceDesiredTail;

        private IFilterSource? m_SourceValue;
        public IFilterSource? SourceValue { get => m_SourceValue; set => m_SourceValue = value; }
        private int m_SourceValueTail;

        protected bool m_Hold;
        public bool Hold { get => m_Hold; set { m_Hold = value; } }

        protected double m_CoefficientLimit;
        public double CoefficientLimit { get => m_CoefficientLimit; set => m_CoefficientLimit = value; }

        public MultiplyAccumulateAdaptiveFilter(int length) : base(length)
        {
        }

        protected override void Calculate(int count)
        {
            double[]? desired = m_SourceDesired?.Copy(ref m_SourceDesiredTail, count, 0, Ring<double>.Underflow.Available);
            double[]? value = m_SourceValue?.Copy(ref m_SourceValueTail, count, 0, Ring<double>.Underflow.Available);
            if (desired == null || value == null) return;
            count = Math.Min(desired.Length, value.Length);
            m_SourceDesired?.Skip(ref m_SourceDesiredTail, count);
            m_SourceValue?.Skip(ref m_SourceValueTail, count);

            double[] result = new double[count];
            for (int loop = 0; loop < count; loop++)
            {
                result[loop] = Insert(value[loop], desired[loop]);
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
