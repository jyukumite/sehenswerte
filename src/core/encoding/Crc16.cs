using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography;
using System.Text;

namespace SehensWerte.Utils
{
    public class Crc16 : HashAlgorithm
    {
        public const ushort DefaultSeed = 0;
        public const ushort DefaultPoly = 0x1021;

        ushort m_Result = 0;
        private ushort m_Init;
        private ushort m_Poly;
        private ushort[] m_Table;

        public override int HashSize => 16;


        public Crc16(ushort poly = DefaultPoly, ushort init = DefaultSeed)
        {
            m_Init = init;
            m_Poly = poly;
            m_Table = BuildCrc16Table();
            Initialize();
        }

        public ushort Compute(byte[] bytes)
        {
            HashCore(bytes, 0, bytes.Length);
            return m_Result;
        }

        public override void Initialize()
        {
            m_Result = m_Init;
        }

        private ushort[] BuildCrc16Table()
        {
            ushort[] result;
            result = new ushort[256];
            for (int loop = 0; loop < 256; loop++)
            {
                ushort word = (ushort)(loop << 8);
                for (int bit = 0; bit < 8; bit++)
                {
                    bool set = (word & 0x8000) != 0;
                    word = (ushort)(word << 1);
                    if (set)
                    {
                        word = (ushort)(word ^ m_Poly);
                    }
                }
                result[loop] = word;
            }
            return result;
        }

        protected override void HashCore(byte[] array, int start, int length)
        {
            for (int loop = start; loop < start + length; loop++)
            {
                m_Result = (ushort)((m_Result << 8) ^ m_Table[(byte)(array[loop] ^ (byte)(m_Result >> 8))]);
            }
        }

        protected override byte[] HashFinal()
        {
            return new byte[] { (byte)(m_Result & 0xff), (byte)(m_Result >> 8) }; // low,high
        }
    }

    [TestClass]
    public class Crc16CcittTests
    {
        [TestMethod]
        public void TestCrc16Ccitt()
        {
            Assert.AreEqual(new Crc16().Compute(Encoding.ASCII.GetBytes("Hello")), 0xcbd6);
            CollectionAssert.AreEqual(new Crc16().ComputeHash(Encoding.ASCII.GetBytes("Hello")), new byte[] { 0xd6, 0xcb });
        }
    }

}
