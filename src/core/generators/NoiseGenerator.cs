using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SehensWerte.Generators
{
    public class NoiseGenerator : GeneratorBase
    {
        public double Amplitude = 1.0;
        public bool UseCryptoRandom = false;

        private Random? FastGen;
        private RandomNumberGenerator? CryptoGen;

        public override double[] Generate(int count)
        {
            double[] array = new double[count];
            if (UseCryptoRandom)
            {
                byte[] buffer = new byte[count * sizeof(UInt64)];
                CryptoGen ??= RandomNumberGenerator.Create();
                CryptoGen.GetBytes(buffer);
                var uintSpan = MemoryMarshal.Cast<byte, UInt64>(buffer);
                for (int loop = 0; loop < count; loop++)
                {
                    array[loop] = (double)((uintSpan[loop] / (double)UInt64.MaxValue) * 2.0 - 1.0) * Amplitude;
                }
            }
            else
            {
                FastGen ??= new Random();
                for (int loop = 0; loop < array.Length; loop++)
                {
                    array[loop] = (FastGen.NextDouble() * 2.0 - 1.0) * Amplitude;
                }
            }
            return array;
        }
    }
}
