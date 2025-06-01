using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Utils;
using System.Diagnostics;

namespace SehensWerte.Generators
{
    public class ToneGenerator : GeneratorBase
    {
        public double Amplitude = 1.0;
        public double SamplesPerSecond = 10000.0;
        public double FrequencyStart = 1000.0;
        public double FrequencyEnd = 1000.0;
        public double SweepsPerSecond;
        public double[] WaveTable = WaveformGenerator.Sine;
        public bool UseMathSin = true; // at least on I9-13900K, sin is 10% faster than wavetable

        private double Accumulator;
        private double AccumulatorStep;

        private double m_Phase;
        public double Phase
        {
            get => m_Phase;
            set // allow phase to be changed during calculation (e.g. psk)
            {
                Func<double, double> func = (double phase) => phase < 0 ? (1 - ((-phase) % 1)) : (phase % 1);
                Accumulator -= WaveTable.Length * func(m_Phase);
                m_Phase = value;
                Accumulator += WaveTable.Length * func(m_Phase);
            }
        }

        public override double[] Generate(int count)
        {
            int waveTableLength = WaveTable.Length; // immeasurable difference from const int = 65536
            double[] array = new double[count];
            double startstep = waveTableLength * FrequencyStart / SamplesPerSecond;
            double stopstep = waveTableLength * FrequencyEnd / SamplesPerSecond;
            double stepstep = (stopstep - startstep) * SweepsPerSecond / SamplesPerSecond;
            int length = array.Length;
            bool useMathSin = UseMathSin && WaveTable == WaveformGenerator.Sine;

            for (int loop = 0; loop < length; loop++)
            {
                if (startstep == stopstep || FrequencyStart == FrequencyEnd || SweepsPerSecond == 0.0)
                {
                    AccumulatorStep = startstep;
                }
                else if ((startstep < stopstep && (AccumulatorStep > stopstep || AccumulatorStep < startstep))
                        || (startstep > stopstep && (AccumulatorStep < stopstep || AccumulatorStep > startstep)))
                {
                    AccumulatorStep = startstep;
                }

                array[loop] += Amplitude *
                                (useMathSin
                                        ? Math.Sin(Accumulator * Math.PI * 2.0 / waveTableLength)
                                        : WaveTable[(int)(Accumulator % waveTableLength)]);
                Accumulator = (Accumulator + AccumulatorStep) % waveTableLength;
                AccumulatorStep += stepstep;
            }
            return array;
        }
    }

    [TestClass]
    public class ToneGeneratorTest
    {
        [TestMethod]
        public void TestSpeed()
        {
            bool testSpeed = false;
            if (testSpeed)
            {
                int n = 100000000;
                double a = HighResTimer.StaticSeconds;
                ToneGenerator a1 = new ToneGenerator() { UseMathSin = false };
                a1.Generate(n);
                double b = HighResTimer.StaticSeconds;
                ToneGenerator a2 = new ToneGenerator() { UseMathSin = true };
                a2.Generate(n);
                double c = HighResTimer.StaticSeconds;
                ToneGenerator a3 = new ToneGenerator() { UseMathSin = false, FrequencyStart = 1000, FrequencyEnd = 2000 };
                a3.Generate(n);
                double d = HighResTimer.StaticSeconds;
                ToneGenerator a4 = new ToneGenerator() { UseMathSin = true, FrequencyStart = 1000, FrequencyEnd = 2000 };
                a4.Generate(n);
                double e = HighResTimer.StaticSeconds;

                // example on i9-13900K
                // 1.0523086 seconds for 100000000 samples sin=false
                // 0.9688325 seconds for 100000000 samples sin=true
                // 1.0904067 seconds for 100000000 samples sweep sin=false
                // 0.9693191 seconds for 100000000 samples sweep sin=true

                MessageBox.Show(@$"Generators:
{b - a} seconds for {n} samples sin=false
{c - b} seconds for {n} samples sin=true
{d - c} seconds for {n} samples sweep sin=false
{e - d} seconds for {n} samples sweep sin=true");
            }
        }
    }
}
