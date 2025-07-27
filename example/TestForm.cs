using SehensWerte.Controls;
using SehensWerte.Controls.Sehens;
using SehensWerte.Files;
using SehensWerte.Filters;
using SehensWerte.Generators;
using SehensWerte.Maths;

namespace SehensWerte
{
    public partial class TestForm : Form
    {

        public TestData m_Generator;
        private Action<CsvLog.Entry> OnLog;

        public TestForm(string[] argv)
        {

            InitializeComponent();
            if (argv.Length > 0)
            {
                Scope.Import(argv[0]);
            }
            OnLog = (s) => Log.Add(s);
            m_Generator = new TestData(CsvLog.ExtendPath(OnLog, "Generate"));

            this.AutoEdit.Generate(m_Generator.m_Data);
            m_Generator.Run(Scope);

            SehensWerte.Utils.Process.RunAllTests();
        }

        private void ButtonGenerate_Click(object sender, EventArgs e)
        {
            m_Generator.Run(Scope);
        }

    }

    public class TestData
    {
        private Action<CsvLog.Entry> OnLog;
        public Data m_Data = new Data();

        public TestData(Action<CsvLog.Entry> onLog)
        {
            OnLog = onLog;
        }

        public class Data // : AutoEditorBase
        {
            [AutoEditor.DisplayOrder(0, "Settings")]
            public int Samples = 100000;

            [AutoEditor.DisplayOrder(0)]
            public double SamplesPerSecond = 48000;

            [AutoEditor.DisplayOrder(1, "Noise")]
            public double NoiseAmplitude = 0.4;
            [AutoEditor.DisplayOrder(1)]
            public double NoisePan = 0.3;
            [AutoEditor.DisplayOrder(1)]
            public bool NoiseCryptoRandom = false;

            [AutoEditor.DisplayOrder(2, "Tone")]
            public double ToneFrequency = 1000;
            [AutoEditor.DisplayOrder(2)]
            public double ToneAmplitude = .5;
            [AutoEditor.DisplayOrder(2)]
            public double ToneTwist = 0;
            [AutoEditor.DisplayOrder(2)]
            public double TonePan = 0.7;
            [AutoEditor.DisplayOrder(2)]
            public bool ToneUseSin = false;
            [AutoEditor.DisplayOrder(2)]
            public WaveformGenerator.Waveforms ToneWaveform = WaveformGenerator.Waveforms.Sine;

            [AutoEditor.DisplayOrder(3, "Sweep")]
            public double SweepFrequency1 = 1000;
            [AutoEditor.DisplayOrder(3)]
            public double SweepFrequency2 = 10000;
            [AutoEditor.DisplayOrder(3)]
            public double SweepAmplitude = 0.4;
            [AutoEditor.DisplayOrder(3)]
            public double SweepsPerSecond = 1;
            [AutoEditor.DisplayOrder(3)]
            public double SweepTwist = 0;
            [AutoEditor.DisplayOrder(3)]
            public double SweepPan = 0.3;
            [AutoEditor.DisplayOrder(3)]
            public bool SweepUseSin = false;
            [AutoEditor.DisplayOrder(3)]
            public WaveformGenerator.Waveforms SweepWaveform = WaveformGenerator.Waveforms.Sine;

            [AutoEditor.DisplayOrder(4, "Kalman")]
            public double KalmanVF = 10;
            [AutoEditor.DisplayOrder(4)]
            public double KalmanPF = 0.001;
            [AutoEditor.DisplayOrder(4)]
            public double KalmanTimeStep = 1;
            [AutoEditor.DisplayOrder(4)]
            public int KalmanSamples = 200;
            [AutoEditor.DisplayOrder(4)]
            public int KalmanPredict = 20;
        }

        public void Run(SehensControl scope)
        {
            try
            {
                Generate(scope);
                ExampleKalman(scope);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke(new Files.CsvLog.Entry(ex.ToString(), Files.CsvLog.Priority.Exception));
            }
        }

        private void Generate(SehensControl scope)
        {
            OnLog?.Invoke(new CsvLog.Entry("Generating", CsvLog.Priority.Info));
            Func<double, double, double> PanLeft = (amplitude, pan) => amplitude * ((pan <= 0.5) ? 1 : ((1 - pan) * 2));
            Func<double, double, double> PanRight = (amplitude, pan) => amplitude * ((pan >= 0.5) ? 1 : (pan * 2));

            var noiseLeft = new Generators.NoiseGenerator()
            {
                Amplitude = PanLeft(m_Data.NoiseAmplitude, m_Data.NoisePan),
                UseCryptoRandom = m_Data.NoiseCryptoRandom,
            };

            var noiseRight = new Generators.NoiseGenerator()
            {
                Amplitude = PanRight(m_Data.NoiseAmplitude, m_Data.NoisePan),
                UseCryptoRandom = m_Data.NoiseCryptoRandom,
            };

            var toneLeft = new Generators.ToneGenerator()
            {
                FrequencyStart = m_Data.ToneFrequency,
                FrequencyEnd = m_Data.ToneFrequency,
                SamplesPerSecond = m_Data.SamplesPerSecond,
                UseMathSin = m_Data.ToneUseSin,
                WaveTable = WaveformGenerator.List[m_Data.ToneWaveform],
                Amplitude = PanLeft(m_Data.ToneAmplitude, m_Data.TonePan),
            };

            var toneRight = new Generators.ToneGenerator()
            {
                FrequencyStart = m_Data.ToneFrequency,
                FrequencyEnd = m_Data.ToneFrequency,
                SamplesPerSecond = m_Data.SamplesPerSecond,
                UseMathSin = m_Data.ToneUseSin,
                WaveTable = WaveformGenerator.List[m_Data.ToneWaveform],
                Amplitude = PanRight(m_Data.ToneAmplitude, m_Data.TonePan),
                Phase = m_Data.ToneTwist,
            };

            var sweepLeft = new Generators.ToneGenerator()
            {
                FrequencyStart = m_Data.SweepFrequency1,
                FrequencyEnd = m_Data.SweepFrequency2,
                SamplesPerSecond = m_Data.SamplesPerSecond,
                SweepsPerSecond = m_Data.SweepsPerSecond,
                UseMathSin = m_Data.SweepUseSin,
                WaveTable = WaveformGenerator.List[m_Data.SweepWaveform],
                Amplitude = PanLeft(m_Data.SweepAmplitude, m_Data.SweepPan),
            };

            var sweepRight = new Generators.ToneGenerator()
            {
                FrequencyStart = m_Data.SweepFrequency1,
                FrequencyEnd = m_Data.SweepFrequency2,
                SamplesPerSecond = m_Data.SamplesPerSecond,
                SweepsPerSecond = m_Data.SweepsPerSecond,
                UseMathSin = m_Data.SweepUseSin,
                WaveTable = WaveformGenerator.List[m_Data.SweepWaveform],
                Amplitude = PanRight(m_Data.SweepAmplitude, m_Data.SweepPan),
                Phase = m_Data.SweepTwist,
            };

            FilterMix mixLeft = new FilterMix(new IFilterSource[] { toneLeft, noiseLeft, sweepLeft });
            FilterMix mixRight = new FilterMix(new IFilterSource[] { toneRight, noiseLeft, sweepRight });

            FilterOutput outputLeft = new FilterOutput(mixLeft);
            FilterOutput outputRight = new FilterOutput(mixRight);

            scope["Left"].Update(outputLeft.Get(m_Data.Samples) ?? new double[0], samplesPerSecond: m_Data.SamplesPerSecond);
            scope["Right"].Update(outputRight.Get(m_Data.Samples) ?? new double[0], samplesPerSecond: m_Data.SamplesPerSecond);
        }

        void ExampleKalman(SehensControl scope)
        {
            int totalCount = m_Data.KalmanSamples;
            int predictCount = m_Data.KalmanPredict;
            int predictStart = m_Data.KalmanSamples - predictCount;

            var real = new ToneGenerator() { Amplitude = 0.5, SamplesPerSecond = 20, FrequencyStart = 0.1, FrequencyEnd = 0.1 }.Generate(totalCount);

            var input = new NoiseGenerator().Generate(totalCount).Add(real);
            input.AsSpan(predictStart, predictCount).Fill(double.NaN);

            var kf = new KalmanFilter(velocityFactor: new[] { m_Data.KalmanVF }, positionFactor: new[] { m_Data.KalmanPF });

            var output = new double[m_Data.KalmanSamples];
            output.AsSpan().Fill(double.NaN);
            for (int loop = 0; loop < predictStart; loop++)
            {
                var result = kf.Insert(new[] { input[loop] });
                output[loop] = result[0];
            }

            var predicted = new double[m_Data.KalmanSamples];
            predicted.AsSpan().Fill(double.NaN);
            kf.PredictFuture(predictCount).CopyTo(predicted.AsSpan(predictStart));

            scope["Kalman real"].Update(real, samplesPerSecond: 20);
            scope["Kalman Input"].Update(input, samplesPerSecond: 20);
            scope["Kalman Output"].Update(output, samplesPerSecond: 20);
            scope["Kalman Predicted"].Update(predicted, samplesPerSecond: 20);
        }
    }
}

