using System.Diagnostics;

namespace SehensWerte.Utils
{
    public class HighResTimer
    {
        private DateTime StartNow = DateTime.Now;
        private double StartSeconds = StaticSeconds;
        private static DateTime StaticStartNow = DateTime.Now;

        private static double SystemClockFrequency = Stopwatch.Frequency;
        private static double StaticStartSeconds = StaticSeconds;

        private static double SystemCounter => Stopwatch.GetTimestamp();
        public static double StaticSeconds => SystemCounter / SystemClockFrequency;
        public static DateTime StaticNow => StaticStartNow.AddSeconds(StaticSeconds - StaticStartSeconds);
        public double ElapsedSeconds => StaticSeconds - StartSeconds;
        public DateTime Now => StartNow.AddSeconds(ElapsedSeconds);
    }
}
