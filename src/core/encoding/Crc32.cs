using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography;
using System.Text;

namespace SehensWerte.Utils
{
    public class Crc32 : HashAlgorithm
    {
        public const uint DefaultSeed = 0xffffffff;
        public const uint DefaultPoly = 0xedb88320;

        private uint m_Result;
        private uint[] m_Table;

        public override int HashSize => 32;

        public Crc32(uint polynomial = DefaultPoly)
        {
            m_Table = BuildCrc32Table(polynomial);
            Initialize();
        }

        public uint Compute(byte[] buffer)
        {
            Initialize();
            HashCore(buffer, 0, buffer.Length);
            return m_Result;
        }

        public override void Initialize()
        {
            m_Result = 0u;
        }

        protected void HashCore(byte[] buffer, int start, int inc, int length)
        {
            m_Result ^= DefaultSeed;
            while (--length >= 0)
            {
                m_Result = m_Table[(m_Result ^ buffer[start]) & 0xFF] ^ (m_Result >> 8);
                start += inc;
            }
            m_Result ^= DefaultSeed;
        }

        protected void HashCore(byte value, int repeated_length)
        {
            byte[] buffer = new byte[1] { value };
            HashCore(buffer, 0, 0, repeated_length);
        }

        protected override void HashCore(byte[] buffer, int start, int length)
        {
            HashCore(buffer, start, 1, length);
        }

        protected override byte[] HashFinal()
        {
            HashValue = new byte[4]
            {
                (byte)((m_Result >> 24) & 0xFFu),
                (byte)((m_Result >> 16) & 0xFFu),
                (byte)((m_Result >> 8) & 0xFFu),
                (byte)(m_Result & 0xFFu)
            };
            return HashValue;
        }

        private static uint[] BuildCrc32Table(uint polynomial)
        {
            uint[] result = new uint[256];
            for (int loop = 0; loop < 256; loop++)
            {
                uint word = (uint)loop;
                for (int bit = 8; bit > 0; bit--)
                {
                    word = (((word & 1) != 1) ? (word >> 1) : ((word >> 1) ^ polynomial));
                }
                result[loop] = word;
            }
            return result;
        }
    }

    [TestClass]
    public class Crc32Tests
    {
        [TestMethod]
        public void TestCrc32()
        {
            Assert.AreEqual(new Crc32().Compute(Encoding.ASCII.GetBytes("Hello")), 0xF7D18982U);
            CollectionAssert.AreEqual(new Crc32().ComputeHash(Encoding.ASCII.GetBytes("Hello")), new byte[] { 0xf7, 0xd1, 0x89, 0x82 });
        }
    }

}
