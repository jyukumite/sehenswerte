using Accessibility;
using Microsoft.VisualBasic.Logging;
using Newtonsoft.Json.Linq;
using SehensWerte.Filters;
using SehensWerte.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace SehensWerte.Maths
{
    public class FFTAnalyse
    {
        /// <summary>
        /// Holds the result of the FFT and derived metrics.
        /// </summary>
        public class Result
        {
            public int Index;
            public double SamplesPerSecond;
            public double[]? OriginalSamples; // time-domain samples (no window applied)
            public double[]? WindowedSamples; // time-domain samples after windowing
            public double[]? AvgMagnitude; // magnitude spectrum
            public double[]? Phase; // phase spectrum
            public float[]? Complex; // real/imag pairs from FFT
            public int Bins; // number of bins (aka Magnitude.Length)
            public double HighestFrequency; // nyquist
            public double HzPerBin;
            public double FirstBinFrequency;
            public double FundamentalBinPower;
            public double FundamentalBinFrequency;
            public double NoiseFloorDB;
            public double SNRdB;
            public double SINADdB;
            public double THDdB;
            public double THDPlusNdB;
            public double SFDRdB; // largest spur vs fundamental
            public double ENOB; // effective number of bits
            public double CrestFactor; // peak to RMS ratio
            public double SampleDCOffset;
            public double SampleRMS;
            public double SamplePeak;
            public List<(double Frequency, double LevelDB)> SpuriousTones = new();

            public void AddToDict(Dictionary<string, List<double>> dict)
            {
                void add(string key, double value)
                {
                    if (!dict.ContainsKey(key))
                    {
                        dict[key] = new List<double>();
                    }
                    dict[key].Add(value);
                }
                //add("HighestFrequency", HighestFrequency);
                //add("HzPerBin", HzPerBin);
                add("FirstBinFrequency", FirstBinFrequency);
                add("FundamentalBinPower", FundamentalBinPower);
                add("FundamentalBinFrequency", FundamentalBinFrequency);
                add("NoiseFloorDB", NoiseFloorDB);
                add("SNRdB", SNRdB);
                add("SINADdB", SINADdB);
                add("THDdB", THDdB);
                add("THDPlusNdB", THDPlusNdB);
                add("SFDRdB", SFDRdB);
                add("ENOB", ENOB);
                add("CrestFactor", CrestFactor);
                add("DCOffset", SampleDCOffset);
                add("RMS", SampleRMS);
                add("Peak", SamplePeak);
                int index = 1;
                foreach (var v in SpuriousTones)
                {
                    add($"Frequency {index}", v.Frequency);
                    add($"Frequency_dB {index}", v.LevelDB);
                    index++;
                }
                if (AvgMagnitude != null)
                {
                    string name = $"Magnitude{Index}";
                    if (!dict.ContainsKey(name))
                    {
                        dict.Add(name, AvgMagnitude.Select(x => 10 * Math.Log10(x)).ToList());
                    }
                }
            }
        }

        private Fftw m_Fft;
        private double[] m_Window;
        private double m_SamplesPerSecond;
        private int m_AvgCount;
        private List<double[]> m_Magnitude = new();
        private int m_Index = 0;

        public int Width => m_Window.Length;

        public FFTAnalyse(int width, double samplesPerSecond = 1.0, int avgCount = 2)
            : this(SampleWindow.GenerateWindow(width, SampleWindow.WindowType.RaisedCosine), samplesPerSecond, avgCount)
        { }

        public FFTAnalyse(double[] window, double samplesPerSecond = 1.0, int avgCount = 2)
        {
            m_Window = window;
            m_Fft = new Fftw(window.Length);
            m_SamplesPerSecond = samplesPerSecond;
            m_AvgCount = avgCount;
        }

        public static IEnumerable<double[]> SliceSamples(double[] samples, double triggerValue, bool risingPhase, int preTriggerSamples, int postTriggerMinimumSamples)
        {
            List<double[]> result = new List<double[]>();

            bool foundFirstTrigger = false;
            int startSliceIndex = 0;
            for (int loop = 1; loop < samples.Length; loop++)
            {
                bool trigger;
                if (risingPhase)
                {
                    trigger = (samples[loop - 1] < triggerValue && samples[loop] >= triggerValue);
                }
                else
                {
                    trigger = (samples[loop - 1] > triggerValue && samples[loop] <= triggerValue);
                }
                if (trigger)
                {
                    if (!foundFirstTrigger)
                    {
                        startSliceIndex = loop;
                        foundFirstTrigger = true;
                    }
                    else
                    {
                        if ((loop - startSliceIndex) >= postTriggerMinimumSamples)
                        {
                            Add(startSliceIndex, loop);
                            startSliceIndex = loop;
                        }
                    }
                }
            }
            if (startSliceIndex != 0)
            {
                Add(startSliceIndex, samples.Length);
            }

            void Add(int start, int end)
            {
                result.Add(samples.Copy(start - preTriggerSamples, end - start + preTriggerSamples));
            }

            return result;
        }

        public static double[] SliceMean(IEnumerable<double[]> slices)
        {
            var maxSlice = slices.Max(x => x.Length);
            slices = slices.Select(x => x.Copy(0, maxSlice)).ToList();
            var meanSlice = new double[maxSlice];
            int count = 0;
            foreach (var slice in slices)
            {
                count++;
                for (int loop = 0; loop < maxSlice; loop++)
                {
                    meanSlice[loop] += slice[loop];
                }
            }
            return meanSlice.ElementProduct(1.0 / count);
        }

        public Result Analyse(double[] samples)
        {
            if (samples.Length != m_Window.Length)
            {
                throw new ArgumentException("Sample length doesn't match window length");
            }

            double[] windowed = SampleWindow.Window(samples, m_Window);
            m_Fft.ExecuteForward(windowed.Subtract(windowed.Mean()));

            // average the historical samples
            m_Magnitude.Add(m_Fft.SpectralMagnitude.ToArray());
            while (m_Magnitude.Count > m_AvgCount)
            {
                m_Magnitude.RemoveAt(0);
            }
            int binCount = m_Fft.Bins;
            var magnitude = new double[binCount];
            foreach (var v in m_Magnitude)
            {
                magnitude = magnitude.Add(v);//.ElementProduct(v));
            }
            magnitude = magnitude.Divide(m_Magnitude.Count);//.Sqrt();

            //
            var result = new Result()
            {
                SamplesPerSecond = m_SamplesPerSecond,
                OriginalSamples = samples,
                WindowedSamples = windowed,
                AvgMagnitude = magnitude,
                Phase = m_Fft.SpectralPhase,
                Complex = m_Fft.SpectralComplex,
                Bins = binCount,
                HighestFrequency = m_Fft.HighestFrequency(m_SamplesPerSecond),
                HzPerBin = m_Fft.HzPerBin(m_SamplesPerSecond),
                Index = m_Index++,
            };

            // get DC offset, crest factor (peak to RMS ratio)
            result.SampleDCOffset = samples.Mean();
            result.SamplePeak = samples.Subtract(result.SampleDCOffset).Abs().Max();
            result.SampleRMS = samples.Subtract(result.SampleDCOffset).Rms();
            result.CrestFactor = (result.SampleRMS == 0.0) ? 0.0 : (result.SamplePeak / result.SampleRMS);

            // min/max to find peaks and troughs in magnitude
            {
                var baseline = magnitude.RollingMean(16);// binCount / 32); // approximate a baseline
                var peak = baseline.Zip(magnitude)
                    .Select(x => (10 * Math.Log10(x.Second)) - (10 * Math.Log10(x.First)))
                    .Select(x => x > 3.01 ? 1.0 : 0.0)
                    .ToArray()
                    .RollingMean(3)
                    .Select(x => x > 0.5)
                    .ToArray();

                // find the highest within each block of peaks
                for (int loop1 = 0; loop1 < peak.Length; loop1++)
                {
                    if (peak[loop1])
                    {
                        int start = loop1;
                        while (loop1 < peak.Length && peak[loop1])
                        {
                            loop1++;
                        }
                        int end = loop1 - 1;

                        double max = 0;
                        int index = start;
                        for (int loop2 = start; loop2 <= end; loop2++)
                        {
                            if (magnitude[loop2] > max)
                            {
                                max = magnitude[loop2];
                                index = loop2;
                            }
                        }

                        double freq = CalcMoment(magnitude, index - 1, index + 1, result.HzPerBin).centreFreq;
                        int bin = (int)(freq / result.HzPerBin + 0.5);
                        double pwr = bin < magnitude.Length ? magnitude[bin] * magnitude[bin] : 0;
                        if (result.FirstBinFrequency == 0)
                        {
                            result.FirstBinFrequency = index == 0 ? 0 : freq;
                        }

                        if (result.SpuriousTones.Count < 10 && freq > 0)
                        {
                            result.SpuriousTones.Add((freq, 10 * Math.Log10(pwr)));
                        }
                    }
                }
            }
            int firstBin = (int)(result.FirstBinFrequency / result.HzPerBin + 0.4999);


            // identify the fundamental frequency bin
            int fundamentalBin = 0;
            {
                double max = double.MinValue;
                for (int loop = 1; loop < binCount; loop++)
                {
                    if (magnitude[loop] > max)
                    {
                        max = magnitude[loop];
                        fundamentalBin = loop;
                    }
                }
                result.FundamentalBinPower = max * max;
                result.FundamentalBinFrequency = CalcMoment(magnitude, fundamentalBin - 2, fundamentalBin + 2, result.HzPerBin).centreFreq; // fundamentalBin * result.HzPerBin;
            }
            if (fundamentalBin > 5)
            {
            }


            // find harmonics (for THD)
            // fixme: too naive, fundamental may be lower power than harmonics
            HashSet<int> toneBins = new();
            toneBins.Add(fundamentalBin);
            double harmonicPowerSum = 0.0;
            const int maxHarmonicOrder = 10;
            for (int loop = 2; loop <= maxHarmonicOrder; loop++)
            {
                int harmonicBin = (int)(result.FundamentalBinFrequency * loop / result.HzPerBin + 0.4999);
                if (harmonicBin >= binCount) break;
                double thisMag = magnitude[harmonicBin];
                harmonicPowerSum += thisMag * thisMag;
                toneBins.Add(harmonicBin);
            }

            // get noise power
            // fixme: too naive, fundamental may be lower power than harmonics
            double noiseAndSpurPower = 0.0;
            for (int loop = 1; loop < binCount; loop++)
            {
                if (!toneBins.Contains(loop))
                {
                    double val = magnitude[loop];
                    noiseAndSpurPower += val * val;
                }
            }

            // THD and THD+N
            // THD = harmonic powers / fundamental power
            // THD+N = (harmonic + noise powers) / fundamental power
            double thd = (result.FundamentalBinPower == 0.0)
                ? 0.0
                : (harmonicPowerSum / result.FundamentalBinPower);
            double thdPlusN = (result.FundamentalBinPower == 0.0)
                ? 0.0
                : ((harmonicPowerSum + noiseAndSpurPower) / result.FundamentalBinPower);
            result.THDdB = 10.0 * Math.Log10(thd);
            result.THDPlusNdB = 10.0 * Math.Log10(thdPlusN);

            // SNR:
            // noise as everything except the fundamental
            double noiseOnlyPower = noiseAndSpurPower;
            double snr = (noiseOnlyPower == 0.0)
                ? 1e9
                : (result.FundamentalBinPower / noiseOnlyPower);
            result.SNRdB = 10.0 * Math.Log10(snr);

            // signal to (noise + distortion) ratio
            // everything other fundamental is considered noise+distortion
            double noisePlusDistortion = noiseAndSpurPower + harmonicPowerSum;
            double sinad = (noisePlusDistortion == 0.0)
                ? 1e9
                : (result.FundamentalBinPower / noisePlusDistortion);
            result.SINADdB = 10.0 * Math.Log10(sinad);

            // effective number of bits that could pass in the channel
            result.ENOB = (result.SINADdB - 1.76) / 6.02;

            // SFDR: largest spur vs fundamental
            double largestSpurMag = double.MinValue;
            int largestSpurBin = -1;
            for (int loop = 0; loop < binCount; loop++)
            {
                if (!toneBins.Contains(loop) && magnitude[loop] > largestSpurMag)
                {
                    largestSpurMag = magnitude[loop];
                    largestSpurBin = loop;
                }
            }
            double largestSpurPower = largestSpurMag * largestSpurMag;
            double sfdr = (largestSpurPower == 0.0)
                ? 1e9
                : (result.FundamentalBinPower / largestSpurPower);
            result.SFDRdB = 10.0 * Math.Log10(sfdr);

            // noise floor (dB) = 10*log10(average noise bin power) - perhaps naive
            double averageNoisePower = 0.0;
            int noiseCount = 0;
            for (int loop = 0; loop < binCount; loop++)
            {
                if (!toneBins.Contains(loop))
                {
                    double pwr = magnitude[loop] * magnitude[loop];
                    averageNoisePower += pwr;
                    noiseCount++;
                }
            }
            averageNoisePower /= (noiseCount == 0 ? 1 : noiseCount);
            double noiseFloorDB = (averageNoisePower <= 0.0)
                ? -200.0
                : (10.0 * Math.Log10(averageNoisePower));
            result.NoiseFloorDB = noiseFloorDB;

            return result;

            (double centreFreq, double weight) CalcMoment(double[] magnitude, int left, int right, double hzPerBin)
            {
                double weight = 0;
                double moment = 0;
                for (int index = Math.Max(1, left); index <= Math.Min(magnitude.Length - 1, right); index++)
                {
                    weight += magnitude[index];
                    moment += index * magnitude[index];
                }
                return (weight == 0 ? 0 : (moment * hzPerBin / weight), weight);
            }

        }

        public static object SliceSamples(double[] original, object sliceTriggerThreshold, bool v1, int v2, int v3)
        {
            throw new NotImplementedException();
        }
    }

    public class MovingFftAnalyseFilter : StatsFilter
    {
        private int m_Stride;
        private int m_StrideIndex;
        private FFTAnalyse m_FftAnalyse;
        private double[] m_Window;
        public FFTAnalyse.Result m_Result = new();
        public FFTAnalyse.Result Result => m_Result;

        public override double LastOutput => m_LastOutput;
        public Dictionary<string, List<double>> Analysis = new();

        public MovingFftAnalyseFilter(int width, int stride = 1,
            double samplesPerSecond = 1.0,
            IFilterSource? source = null,
            double[]? window = null,
            int avgCount = 2) : base(width)
        {
            m_Window = window ?? SampleWindow.GenerateWindow(width, SampleWindow.WindowType.Blackman);
            if (m_Window.Length != width)
            {
                throw new ArgumentException("width != window.Length");
            }
            m_Stride = stride;
            m_FftAnalyse = new FFTAnalyse(m_Window, samplesPerSecond, avgCount);
            base.SourceFilter = source;
        }

        public override double Insert(double value)
        {
            m_LastInput = value;
            Delay.Insert(value);
            m_StrideIndex++;
            if (m_StrideIndex >= m_Stride)
            {
                m_StrideIndex = 0;
                m_Result = m_FftAnalyse.Analyse(History);
            }
            m_Result.AddToDict(Analysis);
            return m_Result.FundamentalBinFrequency;
        }
    }
}
