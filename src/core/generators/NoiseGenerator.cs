namespace SehensWerte.Generators
{
    public class NoiseGenerator : GeneratorBase
    {
        public double Amplitude = 1.0;

        private Random RandomGenerator = new Random();

        public override double[] Generate(int count)
        {
            double[] array = new double[count];
            for (int loop = 0; loop < array.Length; loop++)
            {
                array[loop] += (RandomGenerator.NextDouble() * 2.0 - 1.0) * Amplitude;
            }
            return array;
        }
    }
}
