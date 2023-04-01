namespace SehensWerte.Generators
{
    public class ToneGenerator : GeneratorBase
    {
        private const int SineTableLength = 65536;

        public double Amplitude = 1.0;
        public double SamplesPerSecond = 10000.0;
        public double FrequencyStart = 1000.0;
        public double FrequencyEnd = 1000.0;
        public double SweepsPerSecond;
        public double[] WaveTable = WaveformGenerator.Sine;
        public bool UseMathSin;

        private double Accumulator;
        private double AccumulatorStep;

        private double m_Phase;
        public double Phase
        {
            get => m_Phase;
            set // allow phase to be changed during calculation (e.g. psk)
            {
                Func<double, double> func = (double phase) => phase < 0 ? (1 - ((-phase) % 1)) : (phase % 1);
                Accumulator -= SineTableLength * func(m_Phase);
                m_Phase = value;
                Accumulator += SineTableLength * func(m_Phase);
            }
        }

        public override double[] Generate(int count)
        {
            double[] array = new double[count];
            double startstep = SineTableLength * FrequencyStart / SamplesPerSecond;
            double stopstep = SineTableLength * FrequencyEnd / SamplesPerSecond;
            double stepstep = (stopstep - startstep) * SweepsPerSecond / SamplesPerSecond;
            int length = array.Length;
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
                                (UseMathSin
                                     ? Math.Sin(Accumulator * Math.PI * 2.0 / SineTableLength)
                                     : WaveTable[(int)(Accumulator % SineTableLength)]);
                Accumulator = (Accumulator + AccumulatorStep) % SineTableLength;
                AccumulatorStep += stepstep;
            }
            return array;
        }
    }
}
