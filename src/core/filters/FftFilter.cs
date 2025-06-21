using MathNet.Numerics;
using MathNet.Numerics.Interpolation;
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

        private double[]? m_Window;
        public double[] Window
        {
            get => m_Window ??= SampleWindow.GenerateWindow(m_Fft.Bins, SampleWindow.WindowType.RaisedCosine);
            set => m_Window = value;
        }

        public int Bins => m_Fft.Bins;
        private double HzToBin(double lowCutoff, double samplesPerSecond) => m_Fft.HzToBin(lowCutoff, samplesPerSecond);


        public static readonly List<(double freqHz, double dB)> WeightingA = new()
        {
            (16, -56.7), (31.5, -39.4), (63, -26.2), (125, -16.1), (250, -8.6), (500, -3.2), (1000, 0.0), (2000, 1.2), (4000, 1.0), (8000, -1.1), (16000, -6.6),
            (20000, -32.0), (22050, -96.0) // added for rolloff
        };

        public static readonly List<(double freqHz, double dB)> WeightingB = new()
        {
            (16, -28.5), (31.5, -17.1), (63, -9.3), (125, -4.2), (250, -1.3), (500, -0.3), (1000, 0.0), (2000, -0.1), (4000, -0.7), (8000, -2.9), (16000, -8.4),
            (20000, -32.0), (22050, -96.0) // added for rolloff
        };

        public static readonly List<(double freqHz, double dB)> WeightingC = new()
        {
            (16, -8.5), (31.5, -3.0), (63, -0.8), (125, -0.2), (250, 0.0), (500, 0.0), (1000, 0.0), (2000, -0.2), (4000, -0.8), (8000, -3.0), (16000, -8.5),
            (20000, -32.0), (22050, -96.0) // added for rolloff
        };


        public FftFilter() : this(256) { }

        public FftFilter(int width) : this(new Fftw(width)) { }

        public FftFilter(Fftw fft)
        {
            m_Fft = fft;
            m_Output1 = new double[fft.Bins];
            m_Output2 = new double[fft.Bins];
            Coefficients = new double[m_Fft.Width].Add(1);
        }

        public double[] Execute(double[] input)
        {
            if (input.Length != m_Fft.Width)
            {
                throw new Exception("Input length doesn't match FFT width");
            }
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

        protected override void Calculate(int count)
        {
            while (count > 0)
            {
                int width = m_Fft.Width;
                int halfWidth = width / 2;
                double[]? array = SourceFilter?.Copy(ref m_SourceFilterTail, width, halfWidth, Ring<double>.Underflow.Null);
                if (array == null)
                {
                    break;
                }
                m_Output1 = m_Output2;
                m_Output2 = SampleWindow.Window(Execute(array), Window);

                double[] result = new double[halfWidth];
                for (int loop = 0; loop < halfWidth; loop++)
                {
                    result[loop] = m_Output1[loop + halfWidth] + m_Output2[loop];
                }
                m_OutputBuffer?.Insert(result);
                count -= result.Length;
            }
        }

        public override double Insert(double value)
        {
            throw new NotImplementedException();
        }

        // simple FFT spectral convolution bandpass coefficients - rectangular, low and high bucket
        public static double[] GenerateBandpassCoefficients(int bins, int lowBucket, int highBucket)
        {
            double[] array = new double[bins];
            for (int loop = lowBucket; loop <= highBucket; loop++)
            {
                if (loop >= 0 && loop < bins)
                {
                    array[loop] = 1.0;
                }
            }
            return array;
        }

        // shaped window FFT spectral convolution bandpass coefficients - 3dB buckets
        public static double[] GenerateBandpassCoefficients(int bins, double low3dBBucket, double high3dBBucket, SampleWindow.WindowType windowType)
        {
            double[] array = new double[bins];
            double windowLength;
            if (windowType == SampleWindow.WindowType.Rectangular)
            {
                windowLength = high3dBBucket - low3dBBucket;
            }
            else
            {
                double low3dB = SampleWindow.Inverse(1.0 / Math.Sqrt(2.0), windowType);
                windowLength = (high3dBBucket - low3dBBucket) * (1.0 / (1.0 - low3dB * 2.0));
            }
            double left = (high3dBBucket + low3dBBucket - windowLength) / 2.0;
            for (int loop = 0; loop < bins; loop++)
            {
                double ratio = (loop - left) / windowLength;
                array[loop] = SampleWindow.Coefficient(ratio <= 0 ? 0 : ratio > 1 ? 1 : ratio, windowType);
            }
            return array;
        }

        // shaped window FFT spectral convolution bandpass coefficients - inflection point buckets
        public static double[] GenerateBandpassCoefficients(int bins, double lowCutValue, double lowCutBucket, double lowPassValue, double lowPassBucket, double highPassValue, double highPassBucket, double highCutValue, double highCutBucket, SampleWindow.WindowType windowType)
        {
            double[] hpf = GenerateHighPassCoefficients(bins, lowCutValue, lowCutBucket, lowPassValue, lowPassBucket, windowType);
            double[] lpf = GenerateLowPassCoefficients(bins, highPassValue, highPassBucket, highCutValue, highCutBucket, windowType);
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
        public static double[] GenerateNotchCoefficients(int bins, double low3dBBucket, double high3dBBucket, SampleWindow.WindowType windowType)
        {
            return GenerateBandpassCoefficients(bins, low3dBBucket, high3dBBucket, windowType).Complement(1);
        }

        // shaped window FFT spectral convolution notch coefficients - inflection point buckets
        public static double[] GenerateNotchCoefficients(int bins, double lowPassValue, double lowPassBucket, double lowCutValue, double lowCutBucket, double highCutValue, double highCutBucket, double highPassValue, double highPassBucket, SampleWindow.WindowType windowType)
        {
            return GenerateBandpassCoefficients(bins,
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


        private static readonly Dictionary<string, double[]> m_ArbitraryCoefficientCache = new();
        public static double[] GenerateArbitraryCoefficients(int bins, IEnumerable<(double freqHz, double dB)> list, double samplesPerSecond)
        {
            string cacheKey = $"{bins}_{samplesPerSecond}_" + string.Join("_", list.Select(p => $"{p.freqHz:F4}:{p.dB:F4}"));
            if (m_ArbitraryCoefficientCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var sorted = list.OrderBy(p => p.freqHz).ToArray();
            var freqHz = sorted.Select(p => p.freqHz).ToArray();
            var dBValues = sorted.Select(p => p.dB).ToArray();

            double minFreq = freqHz.First();
            double maxFreq = freqHz.Last();
            double freqSpan = maxFreq - minFreq;
            var normalizedFreqs = freqHz.Select(f => (f - minFreq) / freqSpan).ToArray();

            var spline = CubicSpline.InterpolatePchipSorted(normalizedFreqs, dBValues);

            var response = new double[bins];
            var dB = new double[bins];
            double hzPerBin = Fftw.HzPerBin(bins, samplesPerSecond);
            for (int bin = 0; bin < response.Length; bin++)
            {
                double freqAtBinHz = bin * hzPerBin;
                double normFreq = (freqAtBinHz - minFreq) / freqSpan;
                normFreq = Math.Clamp(normFreq, 0.0, 1.0);
                dB[bin] = spline.Interpolate(normFreq);
                double linear = Math.Pow(10, dB[bin] / 20.0);
                response[bin] = linear;
            }

            m_ArbitraryCoefficientCache[cacheKey] = response;
            return response;
        }

        public static double[] GenerateArbitraryFir(int width, IEnumerable<(double freqHz, double dB)> list, double samplesPerSecond)
        {
            Fftw fftw = new(width * 2);
            var response = GenerateArbitraryCoefficients(fftw.Bins, list, samplesPerSecond);
            return fftw.GenerateFir(samplesPerSecond, response);
        }

        public static double[] Arbitrary(double[] input, IEnumerable<(double freqHz, double dB)> list, double samplesPerSecond)
        {
            int bins = Fftw.SampleCountToBinCount(input.Length);
            var coeffs = GenerateArbitraryCoefficients(bins, list, samplesPerSecond);
            var fftFilter = new FftFilter(input.Length) { Coefficients = coeffs };
            return fftFilter.Execute(input);
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
        public static double[] GenerateLowPassCoefficients(int bins, double passValue, double passBucket, double cutValue, double cutBucket, SampleWindow.WindowType windowType)
        {
            double[] array = new double[bins];
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
                for (int loop = 0; loop < bins; loop++)
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
        public static double[] GenerateHighPassCoefficients(int bins, double cutValue, double cutBucket, double passValue, double passBucket, SampleWindow.WindowType windowType)
        {
            double[] array = new double[bins];
            if (windowType == SampleWindow.WindowType.Rectangular)
            {
                for (int loop = (int)(passBucket + 0.4999); loop < bins; loop++)
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
                for (int loop = 0; loop < bins; loop++)
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

            coefficients = FftFilter.GenerateBandpassCoefficients(bins: 256, lowBucket: 10, highBucket: 50);
            coefficients = FftFilter.GenerateBandpassCoefficients(bins: 256, low3dBBucket: 10, high3dBBucket: 50, SampleWindow.WindowType.RaisedCosine);
            coefficients = FftFilter.GenerateBandpassCoefficients(bins: 256, lowCutValue: 0.25, lowCutBucket: 20, lowPassValue: .5, lowPassBucket: 40, highPassValue: 0.5, highPassBucket: 150, highCutValue: .25, highCutBucket: 170, SampleWindow.WindowType.RaisedCosine);
            
            CollectionAssert.AreEqual(new double[] { 1, 1, 1, 1, 0, 0, 0, 0 }, FftFilter.GenerateBandpassCoefficients(8, 0, 4, SampleWindow.WindowType.Rectangular)); // bin 0 left must make bin 0 == 1

            fir = FftFilter.GenerateBandPassFir(width: 256, low3dBHz: 10, high3dBHz: 50, samplesPerSecond: 1000, SampleWindow.WindowType.RaisedCosine);
            fir = FftFilter.GenerateBandPassFir(width: 256, low6dBHz: 20, low3dBHz: 40, high3dBHz: 150, high6dBHz: 170, samplesPerSecond: 1000, SampleWindow.WindowType.RaisedCosine);

            // FftFilter.BandPass(double[] input, int lowBucket, int highBucket)
            // FftFilter.BandPass(double[] input, double lowHz, double highHz, double samplesPerSecond)
            // FftFilter.BandPass(double[] input, double low3dBHz, double high3dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)
            // FftFilter.BandPass(double[] input, double low6dBHz, double low3dBHz, double high3dBHz, double high6dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)

            coefficients = FftFilter.GenerateNotchCoefficients(bins: 256, low3dBBucket: 40, high3dBBucket: 200, SampleWindow.WindowType.RaisedCosine);
            coefficients = FftFilter.GenerateNotchCoefficients(bins: 256, lowPassValue: 0.5, lowPassBucket: 30, lowCutValue: 0.25, lowCutBucket: 35, highCutValue: 0.25, highCutBucket: 65, highPassValue: 0.5, highPassBucket: 70, SampleWindow.WindowType.RaisedCosine);

            fir = FftFilter.GenerateNotchFir(width: 256, low3dBHz: 40, high3dBHz: 300, samplesPerSecond: 1000, SampleWindow.WindowType.RaisedCosine);
            fir = FftFilter.GenerateNotchFir(width: 256, low3dB: 30, low6dB: 40, high6dB: 60, high3dB: 70, samplesPerSecond: 1000, SampleWindow.WindowType.RaisedCosine);

            // FftFilter.Notch(double[] input, double lowHz, double highHz, double samplesPerSecond, SampleWindow.WindowType windowType)
            // FftFilter.Notch(double[] input, double low3dBHz, double low6dBHz, double high6dBHz, double high3dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)

            coefficients = FftFilter.GenerateLowPassCoefficients(bins: 256, passValue: 0.5, passBucket: 40, cutValue: 0.25, cutBucket: 50, SampleWindow.WindowType.RaisedCosine);
            fir = FftFilter.GenerateLowPassFir(width: 256, freq3dB: 40, freq6dB: 60, samplesPerSecond: 1000, SampleWindow.WindowType.RaisedCosine);
            // FftFilter.LowPass(double[] input, double high3dBHz, double high6dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)

            coefficients = FftFilter.GenerateHighPassCoefficients(bins: 256, cutValue: 0.25, cutBucket: 40, passValue: 0.5, passBucket: 50, SampleWindow.WindowType.RaisedCosine);
            fir = FftFilter.GenerateHighPassFir(width: 256, freq6dB: 200, freq3dB: 300, samplesPerSecond: 1000, SampleWindow.WindowType.RaisedCosine);
            // FftFilter.HighPass(double[] input, double low6dBHz, double low3dBHz, double samplesPerSecond, SampleWindow.WindowType windowType)

            coefficients = FftFilter.GenerateArbitraryCoefficients(bins: 257, list: FftFilter.WeightingA, 48000);
            fir = FftFilter.GenerateArbitraryFir(width: 256, list: FftFilter.WeightingA, 48000);
            samples = FftFilter.Arbitrary(new double[] { 1, 2, 1, 0, -1, -2, -1, 0 }, FftFilter.WeightingA, 48000);
        }
    }
}