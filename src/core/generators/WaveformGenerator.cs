namespace SehensWerte.Generators
{
    public class WaveformGenerator
    {
        public enum Waveforms
        {
            Sine,
            Sine8Entry,
            Square,
            SquareF1to9,
            RisingSawtooth,
            FallingSawtooth,
            Triangle,
            Sinc10
        }

        public static double[] Sine;
        public static double[] Sine8Entry;
        public static double[] Square;
        public static double[] SquareF1to9;
        public static double[] RisingSawtooth;
        public static double[] FallingSawtooth;
        public static double[] Triangle;
        public static double[] Sinc10;

        public static Dictionary<Waveforms, double[]> List;

        static WaveformGenerator()
        {
            Sine = new double[65536];
            Sine8Entry = new double[65536];
            Square = new double[65536];
            SquareF1to9 = new double[65536];
            RisingSawtooth = new double[65536];
            FallingSawtooth = new double[65536];
            Triangle = new double[65536];
            Sinc10 = SinCardinal(65536);

            for (int loop = 0; loop < 65536; loop++)
            {
                Sine[loop] = Math.Sin(loop * 2 * Math.PI / 65536);
                Sine8Entry[loop] = Sine[loop & 0xE000];
            }
            for (int loop = 0; loop < 65536; loop++)
            {
                Square[loop] = loop < 32768 ? 1 : -1;
                SquareF1to9[loop] = Sine[loop] + Sine[(loop * 3) & 0xFFFF] / 3 + Sine[(loop * 5) & 0xFFFF] / 5 + Sine[(loop * 7) & 0xFFFF] / 7 + Sine[(loop * 9) & 0xFFFF] / 9;
            }
            for (int loop = 0; loop < 65536; loop++)
            {
                FallingSawtooth[loop] = (Sine[loop] + Sine[(loop * 2) & 0xFFFF] / 2 + Sine[(loop * 3) & 0xFFFF] / 3 + Sine[(loop * 4) & 0xFFFF] / 4 + Sine[(loop * 5) & 0xFFFF] / 5) / 2;
                RisingSawtooth[loop] = -FallingSawtooth[loop];
            }
            for (int loop = 0; loop < 65536; loop++)
            {
                double num = 0;
                for (int f = 0; f < 10; f++)
                {
                    num += ((f & 1) == 1 ? -1 : 1) * Sine[((2 * f + 1) * loop) & 0xFFFF] / ((2 * f + 1) * (2 * f + 1));
                }
                Triangle[loop] = num * 8 / (Math.PI * Math.PI);
            }

            List = new Dictionary<Waveforms, double[]>
            {
                [Waveforms.Sine] = Sine,
                [Waveforms.Sine8Entry] = Sine8Entry,
                [Waveforms.Square] = Square,
                [Waveforms.SquareF1to9] = SquareF1to9,
                [Waveforms.RisingSawtooth] = RisingSawtooth,
                [Waveforms.FallingSawtooth] = FallingSawtooth,
                [Waveforms.Triangle] = Triangle,
                [Waveforms.Sinc10] = Sinc10
            };
        }

        public static double[] SinCardinal(int samples, double amplitude = 1, double leftTime = -10, double rightTime = 10, double halfWidthTime = 1, double delayTime = 0, double baseline = 0)
        {
            double[] array = new double[samples];
            for (int loop = 0; loop < samples; loop++)
            {
                double t = leftTime + (rightTime - leftTime) * loop / samples;
                double trad = Math.PI * (t - delayTime);
                if (trad == 0)
                {
                    array[loop] = baseline + amplitude;
                }
                else
                {
                    array[loop] = baseline + amplitude * Math.Sin(trad / halfWidthTime) * halfWidthTime / trad;
                }
            }
            return array;
        }
    }
}
