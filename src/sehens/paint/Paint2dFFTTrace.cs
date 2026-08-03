using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Maths;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SehensWerte.Controls.Sehens
{
    public class Paint2dFFTTrace : PaintTraceBase, IPaintTrace, IDisposable
    {
        private Fftw? m_Fft;
        private double HighestFrequency;
        private double LowestFrequency;
        private Bitmap? m_CachedBitmap;
        private IntPtr m_BitmapHGlobal = IntPtr.Zero;

        public Paint2dFFTTrace() { }

        public override void PaintProjection(Graphics graphics, TraceGroupDisplay info)
        {
            if (info.View0.Painted.TraceIndex != 0) return;

            int samplesPerFft;
            (var array, var recalculate) = info.View0.SnapshotProjection();
            int fftsWide = Math.Min(array.Length / (info.View0.IsLogY ? 1024 : 256), 256);
            if (recalculate && fftsWide > 0)
            {
                int overlap = 2;
                samplesPerFft = array.Length * overlap / fftsWide;
                if (samplesPerFft == 0) return;

                LowestFrequency = 0.0;
                HighestFrequency = info.View0.Samples.InputSamplesPerSecond / 2.0;
                double[] fftSamples = new double[samplesPerFft];
                if (m_Fft == null || m_Fft.Width != samplesPerFft)
                {
                    fftSamples = new double[samplesPerFft];
                    m_Fft?.Dispose();
                    m_Fft = new Fftw(samplesPerFft);
                }

                int bins = m_Fft.Bins;
                double hzPerBucket = m_Fft.HighestFrequency(info.View0.Samples.InputSamplesPerSecond) / bins;
                int stride = (fftsWide * 3 + 3) & ~3;
                int bitmapPixelsPerBucket = 1;
                byte[] pixels = new byte[stride * bins * bitmapPixelsPerBucket];
                double[] window = SampleWindow.GenerateWindow(samplesPerFft, info.View0.FftWindow);

                const int MINMAG = -200;
                const int MAXMAG = 100;
                int[] histogram = new int[MAXMAG - MINMAG + 1];

                double[] xv = new double[5] { 0.0, 0.25, 0.5, 0.75, 1.0 };
                Color[] source = new Color[5] { Color.Black, Color.Blue, Color.Pink, Color.Red, Color.Yellow };
                double[] red = source.Select(e => (double)e.R).ToArray();
                double[] green = source.Select(e => (double)e.G).ToArray();
                double[] blue = source.Select(e => (double)e.B).ToArray();

                int startIndex = 0;
                int x = 0;
                while (x < fftsWide && (array.Length + startIndex) >= samplesPerFft)
                {
                    int leftIndex = startIndex + samplesPerFft;
                    int remainder = array.Length - 1 - leftIndex;
                    if (remainder < 0)
                    {
                        startIndex += remainder;
                    }
                    // all-zero bands are excluded from the histogram
                    bool bandIsZero = true;
                    for (int loop = 0; loop < samplesPerFft; loop++)
                    {
                        double sample = array[startIndex + loop];
                        bandIsZero &= sample == 0.0;
                        fftSamples[loop] = sample * window[loop];
                    }

                    m_Fft.ExecuteForward(fftSamples);
                    double[] spectralMagnitude = m_Fft.SpectralMagnitude;

                    double lowestValue = info.View0.LowestValue;
                    double highestValue = info.View0.HighestValue;
                    for (int y = 0; y < bins; y++)
                    {
                        int bin = bins - y - 1;
                        double db = 10.0 * Math.Log10(spectralMagnitude[bin]);
                        db = (double.IsNegativeInfinity(db) || db < MINMAG) ? MINMAG : (db > MAXMAG) ? MAXMAG : db;
                        if (!bandIsZero)
                        {
                            histogram[(int)db - MINMAG]++;
                        }
                        db = (db - lowestValue) / (highestValue - lowestValue);
                        int index = x * 3 + y * stride * bitmapPixelsPerBucket;
                        pixels[index] = (byte)Math.Round(Interpolate.Linear(xv, blue, db));
                        pixels[index + 1] = (byte)Math.Round(Interpolate.Linear(xv, green, db));
                        pixels[index + 2] = (byte)Math.Round(Interpolate.Linear(xv, red, db));
                    }
                    x++;
                    startIndex += samplesPerFft / overlap;
                }
                m_CachedBitmap?.Dispose();
                m_CachedBitmap = null;

                if (info.View0.IsLogY)
                {
                    LogVertical(bins, hzPerBucket, stride, ref bitmapPixelsPerBucket, ref pixels);
                }

                m_CachedBitmap = BytesToBitmap(fftsWide, bins * bitmapPixelsPerBucket, stride, pixels);

                (var mean, var stddev) = HistogramToStddev(histogram);
                if (double.IsNaN(mean))
                {
                    info.View0.DrawnValueLowest = MINMAG;
                    info.View0.DrawnValueHighest = MAXMAG;
                }
                else
                {
                    info.View0.DrawnValueLowest = MINMAG + mean - 2.0 * stddev;
                    info.View0.DrawnValueHighest = MINMAG + mean + 2.0 * stddev;
                }
            }

            if (m_CachedBitmap != null)
            {
                InterpolationMode interpolationMode = graphics.InterpolationMode;
                PixelOffsetMode pixelOffsetMode = graphics.PixelOffsetMode;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(m_CachedBitmap, new RectangleF(info.ProjectionArea.Left, info.ProjectionArea.Top + 1, info.ProjectionArea.Width, info.ProjectionArea.Height));
                graphics.InterpolationMode = interpolationMode;
                graphics.PixelOffsetMode = pixelOffsetMode;
            }

            base.PaintPiP(info, graphics);
        }

        private void LogVertical(int bins, double hzPerBucket, int stride, ref int bitmapPixelsPerBucket, ref byte[] pixels)
        {
            int multiply = 3;
            int newBitmapPixelsPerBucket = bitmapPixelsPerBucket * multiply;
            byte[] newPixels = new byte[stride * bins * newBitmapPixelsPerBucket];
            int y = 0;
            for (int oldy = 0; oldy < bins * bitmapPixelsPerBucket; oldy++)
            {
                ProjectLog(HighestFrequency, (bins - oldy - 1 - 0.5) * hzPerBucket, out var newMax, out var output);
                int newY = (int)((newMax - output) * bins * multiply / newMax);
                newY = newY < 0 ? 0 : ((newY >= bins * multiply) ? (bins * multiply - 1) : newY);
                if (y > newY)
                {
                    //fixme: should sum, not drop
                }
                while (y <= newY)
                {
                    Array.Copy(pixels, oldy * stride, newPixels, y * stride, stride);
                    y++;
                }
            }
            pixels = newPixels;
            bitmapPixelsPerBucket = newBitmapPixelsPerBucket;
        }

        private Bitmap BytesToBitmap(int width, int height, int stride, byte[] pixels)
        {
            if (m_BitmapHGlobal != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(m_BitmapHGlobal);
            }
            m_BitmapHGlobal = Marshal.AllocHGlobal(pixels.Length);
            Marshal.Copy(pixels, 0, m_BitmapHGlobal, pixels.Length);
            Bitmap result = new Bitmap(width, height, stride, PixelFormat.Format24bppRgb, m_BitmapHGlobal);
            return result;
        }

        private static (double mean, double stddev) HistogramToStddev(int[] stats)
        {
            long count = 0L;
            long sum = 0L;
            long sumSquare = 0L;
            for (long loop = 0L; loop < stats.Length; loop++)
            {
                count += stats[loop];
                sum += loop * stats[loop];
                sumSquare += loop * loop * stats[loop];
            }
            if (count == 0L)
            {
                return (double.NaN, double.NaN);
            }
            double mean = sum / (double)count;
            return (mean, Math.Sqrt(Math.Abs(sumSquare / (double)count - (mean * mean))));
        }

        public void PaintInitial(Graphics graphics, TraceGroupDisplay info) { }

        public override void PaintHorizontalAxis(Graphics graphics, TraceGroupDisplay info)
        {
            base.PaintHorizontalAxis(graphics, info);
        }

        public override void PaintAxisTitleHorizontal(Graphics graphics, TraceGroupDisplay info) { }

        public int HoverLabelYFromOffsetX(TraceGroupDisplay info, int x)
        {
            return info.ProjectionArea.Top + info.ProjectionArea.Height / 2;
        }

        protected override double[] VerticalAxisPartition(TraceGroupDisplay info, int partitions, out double topValue, out double bottomValue)
        {
            topValue = 0.0;
            bottomValue = 0.0;

            double[] result = new double[0];
            if (info.View0.Samples.InputSamplesPerSecond != 0.0 && m_Fft != null)
            {
                topValue = info.View0.Samples.InputSamplesPerSecond / 2.0;
                result = GetPartitions(topValue, bottomValue, partitions).ToArray();
            }
            return result;
        }

        public override string VerticalAxisFormat(TraceGroupDisplay info, double yValue)
        {
            return $"{yValue.ToStringRound(5, 3)} Hz";
        }

        public new string GetHoverValue(List<TraceView> list, MouseEventArgs e)
        {
            TraceView.MouseInfo measureInfo = list[0].Measure(e);
            string result = "";
            if (measureInfo.YRatio >= 0.0 && measureInfo.YRatio <= 1.0)
            {
                result = (HighestFrequency - measureInfo.YRatio * (HighestFrequency - LowestFrequency)).ToStringRound(6, 3) + " Hz";
            }
            return result;
        }

        public new string GetHoverStatistics(TraceView trace, TraceView.MouseInfo info)
        {
            string unit = trace.Samples.InputSamplesPerSecond == 0.0 ? "" : $" ({trace.SampleNumberText(info)})";
            return $"{(object)trace.ViewName}[{(object)info.IndexBeforeTrim}]{(object)unit}";
        }

        public override void CalculateTraceRange(TraceGroupDisplay divisionInfo) { }

        public override void Dispose()
        {
            base.Dispose();
            m_Fft?.Dispose();
            m_Fft = null;
            m_CachedBitmap?.Dispose();
            m_CachedBitmap = null;
            if (m_BitmapHGlobal != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(m_BitmapHGlobal);
            }
        }
    }

    [TestClass]
    public class Paint2dFFTTraceTests
    {
        // The all-zero-band gate feeds the auto-ranged colour scale. If the gate is inverted the
        // histogram stays empty, HistogramToStddev divides by a zero count, and the range silently
        // falls back to the MINMAG..MAXMAG clamp for every FFT2D trace - no exception, just a flat
        // spectrogram. Nothing else in the suite paints FFT2D.
        [TestMethod]
        public void Fft2dAutoRangesFromNonZeroBands()
        {
            const double MinMag = -200.0;
            const double MaxMag = 100.0;

            var scope = new SehensControl();
            double[] samples = new double[8192];
            for (int loop = 0; loop < samples.Length / 2; loop++)
            { // tone in the first half, silence in the second - some FFT bands are entirely zero
                samples[loop] = Math.Sin(loop * 0.1);
            }
            scope["fft2d"].Update(samples);
            scope["fft2d"].InputSamplesPerSecond = 1000.0;
            TraceView view = SehensTestHarness.View(scope, "fft2d");
            view.PaintMode = TraceView.PaintModes.FFT2D;
            SehensTestHarness.Layout(scope);

            using Bitmap bitmap = new Bitmap(SehensTestHarness.Width, SehensTestHarness.Height);
            using Graphics graphics = Graphics.FromImage(bitmap);
            view.Painter.PaintProjection(graphics, scope.PaintBox.TraceToGroupDisplayInfo(view));

            Assert.IsFalse(double.IsNaN(view.DrawnValueLowest));
            Assert.IsFalse(double.IsNaN(view.DrawnValueHighest));
            Assert.IsTrue(view.DrawnValueLowest < view.DrawnValueHighest);
            Assert.AreNotEqual(MinMag, view.DrawnValueLowest, 1e-9, "range fell back to the clamp");
            Assert.AreNotEqual(MaxMag, view.DrawnValueHighest, 1e-9, "range fell back to the clamp");
        }
    }
}
