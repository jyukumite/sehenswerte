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
            m_Generator.m_Data.OnChanged += () => { m_Generator.Run(Scope); };

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

        public class Data : AutoEditorBase // derived only for OnChange event
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
            public double KalmanPN = 0.00001;
            [AutoEditor.DisplayOrder(4)]
            public double KalmanMN = 10;
            [AutoEditor.DisplayOrder(4)]
            public double KalmanTimeStep = 1;
            [AutoEditor.DisplayOrder(4)]
            public int KalmanSamples = 200;
            [AutoEditor.DisplayOrder(4)]
            public int KalmanPredict = 20;

            [AutoEditor.DisplayOrder(5.1, "Control")]
            public double PidP = 1;
            [AutoEditor.DisplayOrder(5.2)]
            public double PidI = 0.1;
            [AutoEditor.DisplayOrder(5.3)]
            public double PidD = 10;

            [AutoEditor.DisplayOrder(5.4)]
            public double LqrQ = 0.28;
            [AutoEditor.DisplayOrder(5.5)]
            public double LqrR = 0.2;
            [AutoEditor.DisplayOrder(5.51)]
            public double LqrA = 1;

            [AutoEditor.DisplayOrder(5.6)]
            public double LqrGainSmooth = 0.5;
            [AutoEditor.DisplayOrder(5.6)]
            public int LqrGainPeriod = 1;

            [AutoEditor.DisplayOrder(5.6)]
            public int ControlDelayLen = 5;
        }

        public void Run(SehensControl scope)
        {
            try
            {
                Generate(scope);
                ExampleKalman(scope);
                ExampleControllers(scope);
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

            var kf = new KalmanFilterA(processNoise: new[] { m_Data.KalmanPN }, measurementNoise: new[] { m_Data.KalmanMN });

            var output = new double[m_Data.KalmanSamples];
            output.AsSpan().Fill(double.NaN);
            for (int loop = 0; loop < predictStart; loop++)
            {
                var result = kf.Insert(new[] { input[loop] });
                output[loop] = result[0];
            }

            var predicted = new double[m_Data.KalmanSamples];
            predicted.AsSpan().Fill(double.NaN);
            kf.PredictFuture1d(predictCount).CopyTo(predicted.AsSpan(predictStart));

            scope["Kalman real"].Update(real, samplesPerSecond: 20);
            scope["Kalman Input"].Update(input, samplesPerSecond: 20);
            scope["Kalman Output"].Update(output, samplesPerSecond: 20);
            scope["Kalman Predicted"].Update(predicted, samplesPerSecond: 20);
        }

        void ExampleControllers(SehensControl scope)
        {
            int totalCount = 1000;
            double sps = 20;

            //var target = new double[totalCount].Add(-0.5);
            //target.AsSpan(500, 300).Fill(1.0);
            var target = new Generators.ToneGenerator() { WaveTable = WaveformGenerator.Sine8Entry, SamplesPerSecond = sps, FrequencyStart = 0.05, FrequencyEnd = 0.1 }
                            .Generate(totalCount)
                            .ElementProduct(1.5)
                            .Add(0.5);
            target = target.Add(
               new Generators.ToneGenerator() { WaveTable = WaveformGenerator.Square, SamplesPerSecond = sps, FrequencyStart = 0.1, FrequencyEnd = 0.12 }
                           .Generate(totalCount)
                           .ElementProduct(2.0)
                           .Add(0.0));

            PID pid = new PID { P = m_Data.PidP, I = m_Data.PidI, D = m_Data.PidD, MaxOut = 5.0, MinOut = -5.0 };

            var lqr1 = new SimpleLQR()
            {
                Q = m_Data.LqrQ,
                R = m_Data.LqrR,
                A = m_Data.LqrA,
                B = 1 / sps,
                MaxOut = 5.0,
                MinOut = -5.0,
                GainSmoothing = m_Data.LqrGainSmooth,
                GainUpdatePeriod = m_Data.LqrGainPeriod
            };
            var lqr2 = new LQR()
            {
                Q = new double[,] { { m_Data.LqrQ } },
                R = new double[,] { { m_Data.LqrR } },
                A = new double[,] { { m_Data.LqrA } },
                B = new double[,] { { 1 / sps } },
                MaxOut = 5.0,
                MinOut = -5.0,
                GainSmoothing = m_Data.LqrGainSmooth,
                GainUpdatePeriod = m_Data.LqrGainPeriod
            };

            var sensor_pid = new double[totalCount];
            var sensor_lqr1 = new double[totalCount];
            var sensor_lqr2 = new double[totalCount];
            var output_pid = new double[totalCount];
            var output_lqr1 = new double[totalCount];
            var output_lqr2 = new double[totalCount];

            double test_pid = 0.0;
            double test_lqr1 = 0.0;
            double test_lqr2 = 0.0;

            int delayLen = m_Data.ControlDelayLen;
            var delayPid = new Ring<double>(delayLen);
            var delayLqr1 = new Ring<double>(delayLen);
            var delayLqr2 = new Ring<double>(delayLen);

            for (int loop = 0; loop < totalCount; loop++)
            {
                // PID
                double u_pid = delayPid.Insert(pid.Calc(test_pid, target[loop]));
                test_pid += u_pid * (1.0 / sps);
                sensor_pid[loop] = test_pid;
                output_pid[loop] = u_pid;

                // Simple LQR
                double u_lqr1 = delayLqr1.Insert(lqr1.Calc(test_lqr1, target[loop]));
                test_lqr1 += u_lqr1 * (1.0 / sps);
                sensor_lqr1[loop] = test_lqr1;
                output_lqr1[loop] = u_lqr1;

                // Better LQR
                double u_lqr2 = delayLqr2.Insert(lqr2.Calc(new double[] { test_lqr2 }, new double[] { target[loop] }));
                test_lqr2 += u_lqr2 * (1.0 / sps);
                sensor_lqr2[loop] = test_lqr2;
                output_lqr2[loop] = u_lqr2;

                //scope["LQR1 K0"].AppendRing(new double[] { lqr1.K[0] }, target.Length, samplesPerSecond: sps);
                //scope["LQR1 K1"].AppendRing(new double[] { lqr1.K[1] }, target.Length, samplesPerSecond: sps);
                scope["LQR2 K0"].AppendRing(new double[] { lqr2.K[0] }, target.Length, samplesPerSecond: sps);
                //scope["LQR2 K1"].AppendRing(new double[] { lqr2.K[1] }, target.Length, samplesPerSecond: sps);
            }

            scope["Step Target"].Update(target, samplesPerSecond: sps);
            scope["PID Sensor"].Update(sensor_pid, samplesPerSecond: sps);
            scope["PID Output"].Update(output_pid, samplesPerSecond: sps);
            scope["LQR1 Sensor"].Update(sensor_lqr1, samplesPerSecond: sps);
            scope["LQR1 Output"].Update(output_lqr1, samplesPerSecond: sps);
            scope["LQR2 Sensor"].Update(sensor_lqr2, samplesPerSecond: sps);
            scope["LQR2 Output"].Update(output_lqr2, samplesPerSecond: sps);
        }
    }
}

