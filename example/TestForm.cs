﻿using SehensWerte.Filters;
using SehensWerte.Generators;

namespace SehensWerte
{
    public partial class TestForm : Form
    {
        public class Test
        {
            public int Samples = 100000;
            public double SamplesPerSecond = 48000;

            public double NoiseAmplitude = 0;
            public double NoisePan = 0.5;

            public double ToneFrequency = 1000;
            public double ToneAmplitude = 0;
            public double ToneTwist = 0;
            public double TonePan = 0.5;
            public bool ToneUseSin = false;
            public WaveformGenerator.Waveforms ToneWaveform = WaveformGenerator.Waveforms.Sine;

            public double SweepFrequency1 = 1000;
            public double SweepFrequency2 = 2000;
            public double SweepAmplitude = 0;
            public double SweepTwist = 0;
            public double SweepPan = 0.5;
            public bool SweepUseSin = false;
            public WaveformGenerator.Waveforms SweepWaveform = WaveformGenerator.Waveforms.Sine;
        }
        public Test m_Data = new Test();

        public TestForm(string[] argv)
        {
            InitializeComponent();
            if (argv.Length > 0)
            {
                if (argv[0].ToLower().EndsWith(".csv") && Scope != null)
                {
                    SehensWerte.Controls.Sehens.ImportExport.LoadWaveformsCSV(argv[0], Scope, 0);
                }
                else if (argv[0].ToLower().EndsWith(".sehens") && Scope != null)
                {
                    SehensWerte.Controls.Sehens.ImportExport.LoadState(argv[0], Scope);
                }
            }

            Controls.AutoEditor ae = new SehensWerte.Controls.AutoEditor(m_Data, Split.Panel2.Controls);
        }

        private void ButtonGenerate_Click(object sender, EventArgs e)
        {
            try
            {
                Generate();
            }
            catch (Exception ex)
            {
                Log.Add(new Files.CsvLog.Entry(ex.ToString(), Files.CsvLog.Priority.Exception));
            }
        }

        private void Generate()
        {
            Func<double, double, double> PanLeft = (amplitude, pan) => amplitude * ((pan <= 0.5) ? 1 : ((1 - pan) * 2));
            Func<double, double, double> PanRight = (amplitude, pan) => amplitude * ((pan >= 0.5) ? 1 : (pan * 2));

            IChainFilter noiseLeft = new Generators.NoiseGenerator()
            {
                Amplitude = PanLeft(m_Data.NoiseAmplitude, m_Data.NoisePan),
            };

            IChainFilter noiseRight = new Generators.NoiseGenerator()
            {
                Amplitude = PanRight(m_Data.NoiseAmplitude, m_Data.NoisePan),
            };

            IChainFilter toneLeft = new Generators.ToneGenerator()
            {
                Amplitude = PanLeft(m_Data.ToneAmplitude, m_Data.TonePan),
                FrequencyStart = m_Data.ToneFrequency,
                FrequencyEnd = m_Data.ToneFrequency,
                Phase = 0,
                SamplesPerSecond = m_Data.SamplesPerSecond,
                SweepsPerSecond = 0,
                UseMathSin = m_Data.ToneUseSin,
                WaveTable = WaveformGenerator.List[m_Data.ToneWaveform]
            };

            IChainFilter toneRight = new Generators.ToneGenerator()
            {
                Amplitude = PanRight(m_Data.ToneAmplitude, m_Data.TonePan),
                FrequencyStart = m_Data.ToneFrequency,
                FrequencyEnd = m_Data.ToneFrequency,
                Phase = m_Data.ToneTwist,
                SamplesPerSecond = m_Data.SamplesPerSecond,
                SweepsPerSecond = 0,
                UseMathSin = m_Data.ToneUseSin,
                WaveTable = WaveformGenerator.List[m_Data.ToneWaveform]
            };

            ChainFilterMix mixLeft = new ChainFilterMix(new IChainFilter[] { toneLeft, noiseLeft });
            ChainFilterMix mixRight = new ChainFilterMix(new IChainFilter[] { toneRight, noiseLeft });

            ChainFilterOutput outputLeft = new ChainFilterOutput(mixLeft);
            ChainFilterOutput outputRight = new ChainFilterOutput(mixRight);

            Scope["Left"].Update(outputLeft.Get(m_Data.Samples) ?? new double[0]);
            Scope["Right"].Update(outputRight.Get(m_Data.Samples) ?? new double[0]);
        }
    }
}

