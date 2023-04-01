using SehensWerte.Maths;

namespace SehensWerte.Filters
{
    public class FirFilter : MultiplyAccumulateFilter
    {
        // use Fftw to generate coefficients

        private double m_Gain;

        public FirFilter(double[] coefficients) : base(coefficients)
        {
            m_Gain = 1.0;
        }

        public FirFilter(double[] coefficients, double gain) : base(coefficients)
        {
            m_Gain = gain;
        }

        public override double Insert(double value)
        {
            return m_LastOutput = (double)base.Insert(value) * m_Gain;
        }

        public double[] CenterWindowFir(double[] samples)
        {
            int num = samples.Length;
            double[] array = new double[num];
            int halfLength = m_Coefficients.Length / 2;
            if (num >= halfLength)
            {
                int frontLoad = m_Coefficients.Length + 1;
                for (int loop = 0; loop < frontLoad; loop++)
                {
                    Insert(samples[0]);
                }
                for (int loop = 0; loop < num + halfLength; loop++)
                {
                    double num4 = Insert((loop >= num) ? samples[num - 1] : samples[loop]);
                    if (loop >= halfLength)
                    {
                        array[loop - halfLength] = num4;
                    }
                }
            }
            return array;
        }

        public static double[] ConvolveCoefficients(double[] filterCoefficients, double[] originalCoeffients)
        {
            Fftw fftw = new Fftw(originalCoeffients.Length);
            fftw.TemporalReal = originalCoeffients;
            fftw.ExecuteForward();
            double[] spectralPhase = fftw.SpectralPhase;
            double[] spectralMagnitude = fftw.SpectralMagnitude;
            spectralMagnitude = new FirFilter(filterCoefficients).CenterWindowFir(spectralMagnitude);
            double[] samples = spectralPhase.UnwrapRadians();
            spectralPhase = new FirFilter(filterCoefficients).CenterWindowFir(samples);
            spectralPhase = spectralPhase.WrapRadians();
            fftw.SetSpectral(spectralMagnitude, spectralPhase);
            fftw.ExecuteReverse();
            return fftw.TemporalReal;
        }
    }
}
