using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SehensWerte.Utils
{
    public partial class FormatBits
    {
        public static readonly string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public static byte[] FromBase32(string input)
        {
            input = input.TrimEnd('=');
            byte[] result = new byte[input.Length * 5 / 8];
            byte bits = 8;
            int idx = 0;
            byte wip = 0;
            string text = input;
            for (int loop = 0; loop < text.Length; loop++)
            {
                int conv = FromBase32(text[loop]);
                if (bits > 5)
                {
                    wip = (byte)(wip | (conv << bits - 5));
                    bits = (byte)(bits - 5);
                    continue;
                }
                wip = (byte)(wip | (conv >> 5 - bits));
                result[idx++] = wip;
                wip = (byte)(conv << 3 + bits);
                bits = (byte)(bits + 3);
            }
            if (idx != result.Length)
            {
                result[idx] = wip;
            }
            return result;
        }

        private static int FromBase32(char c)
        {
            return c >= 'A' && c <= 'Z'
                        ? c - 65
                        : c >= '2' && c <= '7'
                            ? c - 24
                            : (c >= 'a' && c <= 'z'
                                ? c - 97
                                : 0);
        }

        public static uint ReadBits(byte[] data, int bitOffset, int bitCount)
        {
            int byteOffset = bitOffset / 8;
            uint result = 0u;
            bitOffset &= 7;
            int bitsLeft = bitCount;
            while (bitsLeft > 0)
            {
                int inner = Math.Min(8 - bitOffset, bitsLeft);
                result <<= inner;
                result |= (uint)((data[byteOffset] >> 8 - bitOffset - inner) & (255 >> 8 - inner));
                byteOffset++;
                bitOffset = 0;
                bitsLeft -= inner;
            }
            return result;
        }

        public static uint ReadBits(UInt32[] data, int bitOffset, int bitCount)
        {
            int wordOffset = bitOffset / 32;
            uint result = 0u;
            bitOffset &= 0x1F;
            int bitsLeft = bitCount;
            while (bitsLeft > 0)
            {
                int inner = Math.Min(32 - bitOffset, bitsLeft);
                result <<= inner;
                result |= (data[wordOffset] >> 32 - bitOffset - inner) & (uint.MaxValue >> 32 - inner);
                wordOffset++;
                bitOffset = 0;
                bitsLeft -= inner;
            }
            return result;
        }

        public static int ReadBitsSigned(byte[] data, int bitOffset, int bitCount)
        {
            return SignExtend(ReadBits(data, bitOffset, bitCount), bitCount);
        }

        public static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] * 256 + data[offset + 1]);
        }

        public static short ReadInt16(byte[] data, int offset)
        {
            return (short)ReadUInt16(data, offset);
        }

        public static void WriteBits(UInt32[] data, uint value, int bitOffset, int bitCount)
        {
            uint mask = uint.MaxValue >> 32 - bitCount;
            int inner = 32 - bitCount - (bitOffset & 0x1F);
            int wordOffset = bitOffset / 32;
            if (inner < 0)
            {
                data[wordOffset] &= ~(mask >> -inner);
                data[wordOffset] |= (value & mask) >> -inner;
                inner += 32;
                wordOffset++;
            }
            data[wordOffset] &= ~(mask << inner);
            data[wordOffset] |= (value & mask) << inner;
        }

        public static int SignExtend(uint result, int bitCount)
        {
            if ((result & (uint)(1 << bitCount - 1)) != 0)
            {
                result |= (uint)(int)((0xFFFFFFFFuL << bitCount) & 0xFFFFFFFFu);
            }
            return (int)result;
        }
    }

    [TestClass]
    public class FormatBitsTests
    {
        [TestMethod]
        public void TestBits()
        {
        }
    }
}
