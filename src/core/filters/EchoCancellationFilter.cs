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
            int needed = Math.Max(count, stride);
            Filter.EnsureBufferSize(ref m_OutputBuffer, needed);
            if (m_OutputBuffer == null || m_OutputBuffer!.Length < needed * 2)
            {
                throw new Exception("Buffer too short");
            }

            int available = m_OutputBuffer!.TailCount(tail);
            if (available < needed)
            {
                int need = needed - available;
                double[] real = m_SourceReal?.Copy(ref m_SourceRealTail, need, need, underflowMode) ?? new double[0];
                double[] delayed = m_SourceDelayed?.Copy(ref m_SourceDelayedTail, need, need, underflowMode) ?? new double[0];

                if (real != null && delayed != null)
                {
                    int pairCount = Math.Min(real.Length, delayed.Length);
                    double[] result = new double[pairCount];

                    for (int loop = 0; loop < pairCount; loop++)
                    {
                        m_Nlms.AdaptiveHold = m_Hold || ThresholdCheck(real, delayed, loop);

                        double correction = m_Nlms.Insert(real[loop] - RealPower.HistoryMean, delayed[loop] - DelayedPower.HistoryMean);
                        result[loop] = m_Enable ? (delayed[loop] - correction) : delayed[loop];
                    }
                    m_OutputBuffer.Insert(result);
                }
            }
            return m_OutputBuffer.TailCopy(ref tail, count, stride, underflowMode);
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
