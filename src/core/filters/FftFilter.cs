using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class FftFilter : Filter, IDisposable
    {
        private Fftw m_Fft;
        private double[] m_Output1;
        private double[] m_Output2;
        public Fftw? Fft => m_Fft;

        private double m_PhaseShift;
        public double PhaseShift { get => m_PhaseShift; set => m_PhaseShift = value; }

        private double[] m_Window;
        public double[] Window { get => m_Window; set => m_Window = value; }

        public int Bins => m_Fft.Bins;
        private double HzToBin(double lowCutoff, double samplesPerSecond) => m_Fft.HzToBin(lowCutoff, samplesPerSecond);

        public FftFilter() : this(256) { }

        public FftFilter(int width)
        {
            if (width == 0)
            {
                throw new Exception("Can't create FFT of width 0");
            }
            m_Fft = new Fftw(width);
            m_Output1 = new double[width];
            m_Output2 = new double[width];
            Coefficients = new double[m_Fft.Width].Add(1);
            m_Window = SampleWindow.GenerateWindow(width, SampleWindow.WindowType.RaisedCosine);
        }

        public double[] Execute(double[] input)
        {
            if (input.Length != m_Fft.Width) throw new Exception("Input length doesn't match FFT width");
            m_Fft.TemporalReal = input;
            m_Fft.ExecuteForward();
            m_Fft.ApplySpectralGain(Coefficients);
            m_Fft.SpectralPhaseShift(m_PhaseShift);
            m_Fft.ExecuteReverse();
            return m_Fft.TemporalReal;
        }

        public void Dispose()
        {
            m_Fft.Dispose();
        }

        public override void OutputBufferUnderflow(int samples, Ring<double>.Underflow underflowMode)
        {
            int width = m_Fft.Width;
            int halfWidth = width / 2;
            int needed = samples;

            while (needed > 0)
            {
                double[]? array = SourceFilter?.Copy(ref m_SourceFilterTail, width, halfWidth, Ring<double>.Underflow.Empty);
                if (array == null)
                {
                    break;
                }
                m_Output1 = m_Output2;
                m_Output2 = SampleWindow.Window(Execute(array), m_Window);

                double[] result = new double[halfWidth];
                for (int loop = 0; loop < halfWidth; loop++)
                {
                    result[loop] = m_Output1[loop + halfWidth] + m_Output2[loop];
                }
                m_OutputBuffer?.Insert(result);
                needed -= halfWidth;
            }
        }

        public override double Insert(double value)
        {
            throw new NotImplementedException();
        }

        public static double[] BandPass(double[] input, int lowBucket, int highBucket)
        {
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients = GenerateBandpassCoefficients(
                 fft.Bins,
                 lowBucket,
                 highBucket);
            return fft.Execute(input);
        }

        public static double[] BandPass(double[] input, double lowHz, double highHz, double samplesPerSecond)
        {
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients =
                GenerateBandpassCoefficients(
                    fft.Bins,
                    (int)fft.HzToBin(lowHz, samplesPerSecond),
                    (int)fft.HzToBin(highHz, samplesPerSecond));
            return fft.Execute(input);
        }

        public static double[] BandPass(double[] input, double lowHz, double highHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients =
                GenerateBandpassCoefficient(
                    fft.Bins,
                    fft.HzToBin(lowHz, samplesPerSecond),
                    fft.HzToBin(highHz, samplesPerSecond),
                    windowType);
            return fft.Execute(input);
        }

        public static double[] BandPass(double[] input, double low6dBHz, double low3dBHz, double high3dBHz, double high6dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients = GenerateBandpassCoefficients(
                fft.Bins,
                0.5,
                fft.HzToBin(low6dBHz, samplesPerSecond), 1.0 / Math.Sqrt(2.0),
                fft.HzToBin(low3dBHz, samplesPerSecond), 1.0 / Math.Sqrt(2.0),
                fft.HzToBin(high3dBHz, samplesPerSecond), 0.5,
                fft.HzToBin(high6dBHz, samplesPerSecond), windowType);
            return fft.Execute(input);
        }

        public static double[] LowPass(double[] input, double high3dBHz, double high6dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients =
                GenerateLowPassCoefficients(
                    fft.Bins,
                    1.0 / Math.Sqrt(2.0),
                    fft.HzToBin(high3dBHz, samplesPerSecond), 0.5,
                    fft.HzToBin(high6dBHz, samplesPerSecond), windowType);
            return fft.Execute(input);
        }

        public static double[] HighPass(double[] input, double low6dBHz, double low3dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients =
                GenerateHighPassCoefficients(
                    fft.Bins,
                    0.5,
                    fft.HzToBin(low6dBHz, samplesPerSecond), 1.0 / Math.Sqrt(2.0),
                    fft.HzToBin(low3dBHz, samplesPerSecond), windowType);
            return fft.Execute(input);
        }

        public static double[] GenerateBandpassCoefficients(int width, int lowCutoffIndex, int highCutoffIndex)
        {
            double[] array = new double[width];
            for (int loop = lowCutoffIndex; loop <= highCutoffIndex; loop++)
            {
                if (loop >= 0 && loop < width)
                {
                    array[loop] = 1.0;
                }
            }
            return array;
        }

        public static double[] GenerateBandpassCoefficient(int width, double low3dBIndex, double high3dBIndex, SampleWindow.WindowType windowType)
        {
            double[] array = new double[width];
            double low3dB = windowType == SampleWindow.WindowType.Rectangular ? 0.5 : SampleWindow.Inverse(1.0 / Math.Sqrt(2.0), windowType);
            double windowLength = (high3dBIndex - low3dBIndex) * (1.0 / (1.0 - low3dB * 2.0));
            double left = (high3dBIndex + low3dBIndex - windowLength) / 2.0;
            for (int loop = 0; loop < width; loop++)
            {
                double ratio = (loop - left) / windowLength;
                array[loop] = SampleWindow.Coefficient(ratio < 0 ? 0 : ratio > 1 ? 1 : ratio, windowType);
            }
            return array;
        }

        public static double[] GenerateBandpassCoefficients(int width,
            double lowCutValue, double lowCutIndex,
            double lowPassValue, double lowPassIndex,
            double highPassValue, double highPassIndex,
            double highCutValue, double highCutIndex,
            SampleWindow.WindowType windowType)
        {
            double[] hpf = GenerateHighPassCoefficients(width, lowCutValue, lowCutIndex, lowPassValue, lowPassIndex, windowType);
            double[] lpf = GenerateLowPassCoefficients(width, highPassValue, highPassIndex, highCutValue, highCutIndex, windowType);
            return hpf.ElementProduct(lpf);
        }

        public static double[] GenerateLowPassCoefficients(int width, double passValue, double passIndex, double cutValue, double cutIndex, SampleWindow.WindowType windowType)
        {
            double[] array = new double[width];
            if (windowType == SampleWindow.WindowType.Rectangular)
            {
                for (int loop = 0; loop < (int)(passIndex + 0.4999); loop++)
                {
                    array[loop] = 1;
                }
            }
            else
            {
                double passRatio = 1.0 - SampleWindow.Inverse(passValue, windowType);
                double cutRatio = 1.0 - SampleWindow.Inverse(cutValue, windowType);
                double rcenter = (passRatio + cutRatio) / 2.0;
                double icenter = (passIndex + cutIndex) / 2.0;
                for (int loop = 0; loop < width; loop++)
                {
                    double ratio = (loop - icenter) * (double)(cutRatio - passRatio) / (double)(cutIndex - passIndex) + rcenter;
                    array[loop] = SampleWindow.Coefficient((ratio < 0.5) ? 0.5 : (ratio >= 1.0) ? 1.0 : ratio, windowType);
                }
            }
            return array;
        }

        public static double[] GenerateHighPassCoefficients(int width, double cutValue, double cutIndex, double passValue, double passIndex, SampleWindow.WindowType windowType)
        {
            double[] array = new double[width];
            if (windowType == SampleWindow.WindowType.Rectangular)
            {
                for (int loop = (int)(passIndex + 0.4999); loop < width; loop++)
                {
                    array[loop] = 1;
                }
            }
            else
            {
                double cutRatio = SampleWindow.Inverse(cutValue, windowType);
                double passRatio = SampleWindow.Inverse(passValue, windowType);
                double rcenter = (passRatio + cutRatio) / 2.0;
                double icenter = (passIndex + cutIndex) / 2.0;
                for (int loop = 0; loop < width; loop++)
                {
                    double ratio = (loop - icenter) * (double)(passRatio - cutRatio) / (double)(passIndex - cutIndex) + rcenter;
                    array[loop] = SampleWindow.Coefficient((ratio < 0) ? 0 : (ratio >= 0.5) ? 0.5 : ratio, windowType);
                }
            }
            return array;
        }

        public static double[] GenerateBandPassFir(int count, double lowCutoff, double highCutoff, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            Fftw fftw = new Fftw(count * 2);
            return fftw.GenerateFir(samplesPerSecond,
                GenerateBandpassCoefficient(fftw.Bins, fftw.HzToBin(lowCutoff, samplesPerSecond),
                    fftw.HzToBin(highCutoff, samplesPerSecond),
                    windowType));
        }

        public static double[] GenerateBandPassFir(int count, double low6dB, double low3dB, double high3dB, double high6dB, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            Fftw fftw = new Fftw(count * 2);
            return fftw.GenerateFir(samplesPerSecond,
                GenerateBandpassCoefficients(fftw.Bins, 0.5,
                    fftw.HzToBin(low6dB, samplesPerSecond), 1.0 / Math.Sqrt(2.0),
                    fftw.HzToBin(low3dB, samplesPerSecond), 1.0 / Math.Sqrt(2.0),
                    fftw.HzToBin(high3dB, samplesPerSecond), 0.5,
                    fftw.HzToBin(high6dB, samplesPerSecond), windowType));
        }

        public static double[] GenerateLowPassFir(int count, double freq3dB, double freq6dB, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            Fftw fftw = new Fftw(count * 2);
            return fftw.GenerateFir(samplesPerSecond,
                GenerateLowPassCoefficients(fftw.Bins, 1.0 / Math.Sqrt(2.0),
                    fftw.HzToBin(freq3dB, samplesPerSecond), 0.5,
                    fftw.HzToBin(freq6dB, samplesPerSecond), windowType));
        }

        public static double[] GenerateHighPassFir(int count, double freq6dB, double freq3dB, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            Fftw fftw = new Fftw(count * 2);
            return fftw.GenerateFir(samplesPerSecond,
                GenerateHighPassCoefficients(fftw.Bins, 0.5,
                    fftw.HzToBin(freq6dB, samplesPerSecond), 1.0 / Math.Sqrt(2.0),
                    fftw.HzToBin(freq3dB, samplesPerSecond), windowType));
        }

        public static double[] GenerateFir(int count, double lowCutoff, double highCutoff, double samplesPerSecond)
        {
            Fftw fftw = new Fftw(count * 2);
            return fftw.GenerateFir(samplesPerSecond,
                GenerateBandpassCoefficients(fftw.Bins,
                    (int)fftw.HzToBin(lowCutoff, samplesPerSecond),
                    (int)fftw.HzToBin(highCutoff, samplesPerSecond)));
        }

        public static double[] GenerateFir(int count, double samplesPerSecond, double[] fftCoefficients)
        {
            Fftw fftw = new Fftw(count * 2);
            return fftw.GenerateFir(samplesPerSecond, fftCoefficients);
        }
    }

    [TestClass]
    public class FftFilterTest
    {

        [TestMethod]
        public void TestFftFilter()
        {

        }
    }
}