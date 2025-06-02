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



        // simple FFT spectral convolution bandpass coefficients - rectangular, low and high bucket
        public static double[] GenerateBandpassCoefficients(int width, int lowBucket, int highBucket)
        {
            double[] array = new double[width];
            for (int loop = lowBucket; loop <= highBucket; loop++)
            {
                if (loop >= 0 && loop < width)
                {
                    array[loop] = 1.0;
                }
            }
            return array;
        }

        // shaped window FFT spectral convolution bandpass coefficients - 3dB buckets
        public static double[] GenerateBandpassCoefficients(int width, double low3dBBucket, double high3dBBucket, SampleWindow.WindowType windowType)
        {
            double[] array = new double[width];
            double low3dB = windowType == SampleWindow.WindowType.Rectangular ? 0.5 : SampleWindow.Inverse(1.0 / Math.Sqrt(2.0), windowType);
            double windowLength = (high3dBBucket - low3dBBucket) * (1.0 / (1.0 - low3dB * 2.0));
            double left = (high3dBBucket + low3dBBucket - windowLength) / 2.0;
            for (int loop = 0; loop < width; loop++)
            {
                double ratio = (loop - left) / windowLength;
                array[loop] = SampleWindow.Coefficient(ratio < 0 ? 0 : ratio > 1 ? 1 : ratio, windowType);
            }
            return array;
        }

        // shaped window FFT spectral convolution bandpass coefficients - inflection point buckets
        public static double[] GenerateBandpassCoefficients(int width, double lowCutValue, double lowCutBucket, double lowPassValue, double lowPassBucket, double highPassValue, double highPassBucket, double highCutValue, double highCutBucket, SampleWindow.WindowType windowType)
        {
            double[] hpf = GenerateHighPassCoefficients(width, lowCutValue, lowCutBucket, lowPassValue, lowPassBucket, windowType);
            double[] lpf = GenerateLowPassCoefficients(width, highPassValue, highPassBucket, highCutValue, highCutBucket, windowType);
            return hpf.ElementProduct(lpf);
        }

        // shaped window FIR bandpass convolution taps - 3dB inflection Hz
        public static double[] GenerateBandPassFir(int width, double low3dBHz, double high3dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            Fftw fftw = new Fftw(width * 2);
            return fftw.GenerateFir(samplesPerSecond,
                GenerateBandpassCoefficients(
                    fftw.Bins,
                    fftw.HzToBin(low3dBHz, samplesPerSecond),
                    fftw.HzToBin(high3dBHz, samplesPerSecond),
                    windowType));
        }

        // shaped window FIR bandpass convolution taps - 3dB and 6dB Hz
        public static double[] GenerateBandPassFir(int width, double low6dBHz, double low3dBHz, double high3dBHz, double high6dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            Fftw fftw = new Fftw(width * 2);
            return fftw.GenerateFir(samplesPerSecond,
                GenerateBandpassCoefficients(fftw.Bins,
                    0.5, fftw.HzToBin(low6dBHz, samplesPerSecond),
                    1.0 / Math.Sqrt(2.0), fftw.HzToBin(low3dBHz, samplesPerSecond),
                    1.0 / Math.Sqrt(2.0), fftw.HzToBin(high3dBHz, samplesPerSecond),
                    0.5, fftw.HzToBin(high6dBHz, samplesPerSecond),
                    windowType));
        }

        // run bandpass on samples, rectangular, low and high bucket
        public static double[] BandPass(double[] input, int lowBucket, int highBucket)
        {
            // simple square bandpass, buckets
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients = GenerateBandpassCoefficients(
                 fft.Bins,
                 lowBucket,
                 highBucket);
            return fft.Execute(input);
        }

        // run bandpass on samples, rectangular, low and high Hz
        public static double[] BandPass(double[] input, double lowHz, double highHz, double samplesPerSecond)
        {
            // simple square bandpass, Hz
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients =
                GenerateBandpassCoefficients(
                    fft.Bins,
                    (int)fft.HzToBin(lowHz, samplesPerSecond),
                    (int)fft.HzToBin(highHz, samplesPerSecond));
            return fft.Execute(input);
        }

        // run bandpass on samples, shaped window, 3dB inflection Hz
        public static double[] BandPass(double[] input, double low3dBHz, double high3dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            // windowed bandpass, 3dB buckets Hz
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients =
                GenerateBandpassCoefficients(
                    fft.Bins,
                    fft.HzToBin(low3dBHz, samplesPerSecond),
                    fft.HzToBin(high3dBHz, samplesPerSecond),
                    windowType);
            return fft.Execute(input);
        }

        // run bandpass on samples, shaped window, 3dB and 6dB inflection Hz
        public static double[] BandPass(double[] input, double low6dBHz, double low3dBHz, double high3dBHz, double high6dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            // windowed bandpass, 6dB and 3dB buckets Hz
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



        // shaped window FFT spectral convolution notch coefficients - 3dB buckets
        public static double[] GenerateNotchCoefficients(int width, double low3dBBucket, double high3dBBucket, SampleWindow.WindowType windowType)
        {
            return GenerateBandpassCoefficients(width, low3dBBucket, high3dBBucket, windowType).Complement(1);
        }

        // shaped window FFT spectral convolution notch coefficients - inflection point buckets
        public static double[] GenerateNotchCoefficients(int width, double lowPassValue, double lowPassBucket, double lowCutValue, double lowCutBucket, double highCutValue, double highCutBucket, double highPassValue, double highPassBucket, SampleWindow.WindowType windowType)
        {
            return GenerateBandpassCoefficients(width,
                1 - lowPassValue, lowPassBucket,
                1 - lowCutValue, lowCutBucket,
                1 - highCutValue, highCutBucket,
                1 - highPassValue, highPassBucket,
                windowType).Complement(1);
        }

        // shaped window FIR notch convolution taps - 3dB inflection Hz
        public static double[] GenerateNotchFir(int width, double low3dBHz, double high3dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            Fftw fftw = new Fftw(width * 2);
            return fftw.GenerateFir(samplesPerSecond,
                GenerateNotchCoefficients(fftw.Bins,
                    fftw.HzToBin(low3dBHz, samplesPerSecond),
                    fftw.HzToBin(high3dBHz, samplesPerSecond),
                    windowType));
        }

        // shaped window FIR notch convolution taps - 3dB and 6dB inflection Hz
        public static double[] GenerateNotchFir(int width, double low3dB, double low6dB, double high6dB, double high3dB, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            Fftw fftw = new Fftw(width * 2);
            return fftw.GenerateFir(samplesPerSecond,
                GenerateNotchCoefficients(fftw.Bins,
                    1.0 / Math.Sqrt(2.0), fftw.HzToBin(low3dB, samplesPerSecond),
                    0.5, fftw.HzToBin(low6dB, samplesPerSecond),
                    0.5, fftw.HzToBin(high6dB, samplesPerSecond),
                    1.0 / Math.Sqrt(2.0), fftw.HzToBin(high3dB, samplesPerSecond),
                    windowType));
        }

        // run notch on samples, shaped window, 3dB inflection Hz
        public static double[] Notch(double[] input, double lowHz, double highHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients =
                GenerateNotchCoefficients(
                    fft.Bins,
                    fft.HzToBin(lowHz, samplesPerSecond),
                    fft.HzToBin(highHz, samplesPerSecond),
                    windowType);
            return fft.Execute(input);
        }

        // run notch on samples, shaped window, 3dB and 6dB inflection Hz
        public static double[] Notch(double[] input, double low3dBHz, double low6dBHz, double high6dBHz, double high3dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients = GenerateNotchCoefficients(
                fft.Bins,
                1.0 / Math.Sqrt(2.0),
                fft.HzToBin(low3dBHz, samplesPerSecond), 0.5,
                fft.HzToBin(low6dBHz, samplesPerSecond), 0.5,
                fft.HzToBin(high6dBHz, samplesPerSecond), 1.0 / Math.Sqrt(2.0),
                fft.HzToBin(high3dBHz, samplesPerSecond), windowType);
            return fft.Execute(input);
        }



        // shaped window FFT spectral convolution lowpass coefficients - 3dB buckets
        public static double[] GenerateLowPassCoefficients(int width, double passValue, double passBucket, double cutValue, double cutBucket, SampleWindow.WindowType windowType)
        {
            double[] array = new double[width];
            if (windowType == SampleWindow.WindowType.Rectangular)
            {
                for (int loop = 0; loop < (int)(passBucket + 0.4999); loop++)
                {
                    array[loop] = 1;
                }
            }
            else
            {
                double passRatio = 1.0 - SampleWindow.Inverse(passValue, windowType);
                double cutRatio = 1.0 - SampleWindow.Inverse(cutValue, windowType);
                double rcenter = (passRatio + cutRatio) / 2.0;
                double icenter = (passBucket + cutBucket) / 2.0;
                for (int loop = 0; loop < width; loop++)
                {
                    double ratio = (loop - icenter) * (double)(cutRatio - passRatio) / (double)(cutBucket - passBucket) + rcenter;
                    array[loop] = SampleWindow.Coefficient((ratio < 0.5) ? 0.5 : (ratio >= 1.0) ? 1.0 : ratio, windowType);
                }
            }
            return array;
        }

        // shaped window FIR lowpass convolution taps - 3dB and 6dB inflection Hz
        public static double[] GenerateLowPassFir(int width, double freq3dB, double freq6dB, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            Fftw fftw = new Fftw(width * 2);
            return fftw.GenerateFir(samplesPerSecond,
                GenerateLowPassCoefficients(fftw.Bins,
                    1.0 / Math.Sqrt(2.0), fftw.HzToBin(freq3dB, samplesPerSecond),
                    0.5, fftw.HzToBin(freq6dB, samplesPerSecond),
                    windowType));
        }

        // run lowpass on samples, shaped window, 3dB and 6dB inflection Hz
        public static double[] LowPass(double[] input, double low3dBHz, double low6dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients =
                GenerateLowPassCoefficients(
                    fft.Bins,
                    1.0 / Math.Sqrt(2.0),
                    fft.HzToBin(low3dBHz, samplesPerSecond), 0.5,
                    fft.HzToBin(low6dBHz, samplesPerSecond), windowType);
            return fft.Execute(input);
        }


        // shaped window FFT spectral convolution highpass coefficients - 3dB buckets
        public static double[] GenerateHighPassCoefficients(int width, double cutValue, double cutBucket, double passValue, double passBucket, SampleWindow.WindowType windowType)
        {
            double[] array = new double[width];
            if (windowType == SampleWindow.WindowType.Rectangular)
            {
                for (int loop = (int)(passBucket + 0.4999); loop < width; loop++)
                {
                    array[loop] = 1;
                }
            }
            else
            {
                double cutRatio = SampleWindow.Inverse(cutValue, windowType);
                double passRatio = SampleWindow.Inverse(passValue, windowType);
                double rcenter = (passRatio + cutRatio) / 2.0;
                double icenter = (passBucket + cutBucket) / 2.0;
                for (int loop = 0; loop < width; loop++)
                {
                    double ratio = (loop - icenter) * (double)(passRatio - cutRatio) / (double)(passBucket - cutBucket) + rcenter;
                    array[loop] = SampleWindow.Coefficient((ratio < 0) ? 0 : (ratio >= 0.5) ? 0.5 : ratio, windowType);
                }
            }
            return array;
        }

        // shaped window FIR highpass convolution taps - 3dB and 6dB inflection Hz
        public static double[] GenerateHighPassFir(int width, double freq6dB, double freq3dB, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            Fftw fftw = new Fftw(width * 2);
            return fftw.GenerateFir(samplesPerSecond,
                GenerateHighPassCoefficients(fftw.Bins,
                    0.5, fftw.HzToBin(freq6dB, samplesPerSecond),
                    1.0 / Math.Sqrt(2.0), fftw.HzToBin(freq3dB, samplesPerSecond),
                    windowType));
        }

        // run highpass on samples, shaped window, 3dB and 6dB inflection Hz
        public static double[] HighPass(double[] input, double high6dBHz, double high3dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
        {
            using FftFilter fft = new FftFilter(input.Length);
            fft.Coefficients =
                GenerateHighPassCoefficients(
                    fft.Bins,
                    0.5,
                    fft.HzToBin(high6dBHz, samplesPerSecond), 1.0 / Math.Sqrt(2.0),
                    fft.HzToBin(high3dBHz, samplesPerSecond), windowType);
            return fft.Execute(input);
        }
    }


    [TestClass]
    public class FftFilterTest
    {
        [TestMethod]
        public void TestFftFilter()
        {
            double[] coefficients;
            double[] fir;
            double[] samples;

            coefficients = FftFilter.GenerateBandpassCoefficients(width: 256, lowBucket: 10, highBucket: 50);
            coefficients = FftFilter.GenerateBandpassCoefficients(width: 256, low3dBBucket: 10, high3dBBucket: 50, SampleWindow.WindowType.RaisedCosine);
            coefficients = FftFilter.GenerateBandpassCoefficients(width: 256, lowCutValue: 0.25, lowCutBucket: 20, lowPassValue: .5, lowPassBucket: 40, highPassValue: 0.5, highPassBucket: 150, highCutValue: .25, highCutBucket: 170, SampleWindow.WindowType.RaisedCosine);

            fir = FftFilter.GenerateBandPassFir(width: 256, low3dBHz: 10, high3dBHz: 50, samplesPerSecond: 1000, SampleWindow.WindowType.RaisedCosine);
            fir = FftFilter.GenerateBandPassFir(width: 256, low6dBHz: 20, low3dBHz: 40, high3dBHz: 150, high6dBHz: 170, samplesPerSecond: 1000, SampleWindow.WindowType.RaisedCosine);

            // FftFilter.BandPass(double[] input, int lowBucket, int highBucket)
            // FftFilter.BandPass(double[] input, double lowHz, double highHz, double samplesPerSecond)
            // FftFilter.BandPass(double[] input, double low3dBHz, double high3dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
            // FftFilter.BandPass(double[] input, double low6dBHz, double low3dBHz, double high3dBHz, double high6dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)

            coefficients = FftFilter.GenerateNotchCoefficients(width: 256, low3dBBucket: 40, high3dBBucket: 200, SampleWindow.WindowType.RaisedCosine);
            coefficients = FftFilter.GenerateNotchCoefficients(width: 256, lowPassValue: 0.5, lowPassBucket: 30, lowCutValue: 0.25, lowCutBucket: 35, highCutValue: 0.25, highCutBucket: 65, highPassValue: 0.5, highPassBucket: 70, SampleWindow.WindowType.RaisedCosine);

            fir = FftFilter.GenerateNotchFir(width: 256, low3dBHz: 40, high3dBHz: 300, samplesPerSecond: 1000, SampleWindow.WindowType.RaisedCosine);
            fir = FftFilter.GenerateNotchFir(width: 256, low3dB: 30, low6dB: 40, high6dB: 60, high3dB: 70, samplesPerSecond: 1000, SampleWindow.WindowType.RaisedCosine);

            // FftFilter.Notch(double[] input, double lowHz, double highHz, double samplesPerSecond, SampleWindow.WindowType windowType)
            // FftFilter.Notch(double[] input, double low3dBHz, double low6dBHz, double high6dBHz, double high3dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)

            coefficients = FftFilter.GenerateLowPassCoefficients(width: 256, passValue: 0.5, passBucket: 40, cutValue: 0.25, cutBucket: 50, SampleWindow.WindowType.RaisedCosine);
            fir = FftFilter.GenerateLowPassFir(width: 256, freq3dB: 40, freq6dB: 60, samplesPerSecond: 1000, SampleWindow.WindowType.RaisedCosine);
            // FftFilter.LowPass(double[] input, double high3dBHz, double high6dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)

            coefficients = FftFilter.GenerateHighPassCoefficients(width: 256, cutValue: 0.25, cutBucket: 40, passValue: 0.5, passBucket: 50, SampleWindow.WindowType.RaisedCosine);
            fir = FftFilter.GenerateHighPassFir(width: 256, freq6dB: 200, freq3dB: 300, samplesPerSecond: 1000, SampleWindow.WindowType.RaisedCosine);
            // FftFilter.HighPass(double[] input, double low6dBHz, double low3dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)


        }
    }
}