using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class EchoCancellationFilter : IFilterSource
    {
        private IFilterSource? m_SourceReal;
        private int m_SourceRealTail;
        private IFilterSource? m_SourceDelayed;
        private int m_SourceDelayedTail;

        private MultiplyAccumulateFilter RealPower;
        private MultiplyAccumulateFilter DelayedPower;

        private Ring<double>? m_OutputBuffer;
        public int BufferSize { get => m_OutputBuffer?.Length ?? 0; set => Filter.EnsureBufferSize(ref m_OutputBuffer, value); }

        private IFilter m_Nlms;
        public IFilter Nlms => m_Nlms;

        public double DelayedLowSignalRmsThreshold = 0.01;
        public double DelayedHighSignalRmsThreshold = 1;
        public double RealLowSignalRmsThreshold = 0.01;
        public double RealHighSignalRmsThreshold = 1;

        private bool m_Hold;
        public bool Hold { get => m_Hold; set => m_Hold = value; }
        private bool m_Enable;
        public bool Enable { get => m_Enable; set => m_Enable = value; }

        public IFilterSource? SourceFilterReal
        {
            get => m_SourceReal;
            set
            {
                if (value != null)
                {
                    m_SourceReal = value;
                    Filter.EnsureBufferSize(ref m_OutputBuffer, m_SourceReal.BufferSize);
                }
            }
        }

        public IFilterSource? SourceFilterDelayed
        {
            get => m_SourceDelayed;
            set
            {
                if (value != null)
                {
                    m_SourceDelayed = value;
                    Filter.EnsureBufferSize(ref m_OutputBuffer, m_SourceDelayed.BufferSize);
                }
            }
        }

        public EchoCancellationFilter(IFilter nlms)
        {
            m_Nlms = nlms;
            RealPower = new MultiplyAccumulateFilter(nlms.History.Length);
            DelayedPower = new MultiplyAccumulateFilter(nlms.History.Length);
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
            double[]? real = m_SourceReal?.Copy(ref m_SourceRealTail, count, 0, Ring<double>.Underflow.Available);
            double[]? delayed = m_SourceDelayed?.Copy(ref m_SourceDelayedTail, count, 0, Ring<double>.Underflow.Available);
            if (real == null || delayed == null) return;
            count = Math.Min(real.Length, delayed.Length);
            m_SourceReal?.Skip(ref m_SourceRealTail, count);
            m_SourceDelayed?.Skip(ref m_SourceDelayedTail, count);

            double[] result = new double[count];
            for (int loop = 0; loop < count; loop++)
            {
                m_Nlms.AdaptiveHold = m_Hold || ThresholdCheck(real, delayed, loop);
                double correction = m_Nlms.Insert(real[loop] - RealPower.HistoryMean, delayed[loop] - DelayedPower.HistoryMean);
                result[loop] = m_Enable ? (delayed[loop] - correction) : delayed[loop];
            }
            m_OutputBuffer?.Insert(result);
        }

        public void Skip(ref int tail, int skip)
        {
            m_OutputBuffer?.Skip(ref tail, skip);
        }

        private bool ThresholdCheck(double[] real, double[] delayed, int loop)
        {
            if (RealLowSignalRmsThreshold == 0 && RealHighSignalRmsThreshold == 0 && DelayedLowSignalRmsThreshold == 0 && DelayedHighSignalRmsThreshold == 0)
            {
                return false;
            }

            RealPower.Insert(real[loop]);
            DelayedPower.Insert(delayed[loop]);
            double realPower = RealPower.HistoryRms;
            double delayedPower = DelayedPower.HistoryRms;
            return realPower < RealLowSignalRmsThreshold
                || realPower > RealHighSignalRmsThreshold
                || delayedPower < DelayedLowSignalRmsThreshold
                || delayedPower > DelayedHighSignalRmsThreshold;
        }
    }
}
