using SehensWerte.Maths;

namespace SehensWerte.Controls.Sehens
{
    public class TraceDataPeakHold : IDisposable
    {
        public double[] Max;
        public double[] Min;
        public int Length => Math.Min(Max.Length, Min.Length);

        public TraceDataPeakHold(double[] min, double[] max)
        {
            Min = min;
            Max = max;
        }

        public TraceDataPeakHold(double[] data, int start, int count)
        {
            Min = data.Copy();
            Max = data.Copy();
        }

        public void Peak(double[] data, int start, int count)
        {
            for (int loop = 0; loop < count; loop++)
            {
                double num = data[loop + start];
                if (Min[loop] > num)
                {
                    Min[loop] = num;
                }
                if (Max[loop] < num)
                {
                    Max[loop] = num;
                }
            }
        }

        public virtual void Dispose()
        {
        }
    }
}
