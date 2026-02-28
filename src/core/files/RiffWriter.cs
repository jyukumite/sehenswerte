using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace SehensWerte.Files
{
    public class RiffWriter
    {
        private Stream m_Stream;
        private int m_Channels = 1;
        private int m_BitDepth = 16;

        public RiffWriter(string filename, int samplesPerSecond, int channels = 1)
        {
            m_Stream = File.Open(filename, FileMode.Create);
            m_Channels = channels;
            WriteHeader(samplesPerSecond);
        }

        public RiffWriter(Stream stream, int samplesPerSecond, int channels = 1)
        {
            m_Stream = stream;
            m_Channels = channels;
            WriteHeader(samplesPerSecond);
        }

        public static void Write(string filename, short[] samples, int samplesPerSecond)
        {
            RiffWriter riffWriter = new RiffWriter(filename, samplesPerSecond);
            riffWriter.Add(samples);
            riffWriter.Close();
        }

        public static Stream ToStream(short[] sample, int samplesPerSecond)
        {
            MemoryStream memoryStream = new MemoryStream();
            RiffWriter riffWriter = new RiffWriter(memoryStream, samplesPerSecond);
            riffWriter.Add(sample);
            riffWriter.Finalise();
            memoryStream.Seek(0L, SeekOrigin.Begin);
            return memoryStream;
        }

        private byte[] Bytes(int v)
        {
            return new byte[4]
            {
                (byte)((uint)v & 0xFFu),
                (byte)((uint)(v >> 8) & 0xFFu),
                (byte)((uint)(v >> 16) & 0xFFu),
                (byte)((uint)(v >> 24) & 0xFFu)
            };
        }

        private byte[] Bytes(short v)
        {
            return new byte[2]
            {
                (byte)((uint)v & 0xFFu),
                (byte)((uint)(v >> 8) & 0xFFu)
            };
        }

        private byte[] Bytes(string v)
        {
            return new ASCIIEncoding().GetBytes(v);
        }

        public void WriteHeader(int samplesPerSecond)
        {
            m_Stream.Write(Bytes("RIFF"), 0, 4);
            m_Stream.Write(Bytes(0), 0, 4);
            m_Stream.Write(Bytes("WAVE"), 0, 4);
            m_Stream.Write(Bytes("fmt "), 0, 4);
            m_Stream.Write(Bytes(16), 0, 4);
            m_Stream.Write(Bytes((short)1), 0, 2);
            m_Stream.Write(Bytes((short)m_Channels), 0, 2);
            m_Stream.Write(Bytes(samplesPerSecond), 0, 4);
            m_Stream.Write(Bytes(samplesPerSecond * m_BitDepth / 8 * m_Channels), 0, 4);
            m_Stream.Write(Bytes((short)(m_Channels * m_BitDepth / 8)), 0, 2);
            m_Stream.Write(Bytes((short)m_BitDepth), 0, 2);
            m_Stream.Write(Bytes("data"), 0, 4);
            m_Stream.Write(Bytes(0), 0, 4);
        }

        public void Close()
        {
            Finalise();
            m_Stream.Close();
            m_Stream.Dispose();
        }

        private void Finalise()
        {
            int length = (int)m_Stream.Length;
            m_Stream.Seek(4L, SeekOrigin.Begin);
            m_Stream.Write(Bytes(length - 8), 0, 4);
            m_Stream.Seek(40L, SeekOrigin.Begin);
            m_Stream.Write(Bytes(length - 44), 0, 4);
        }

        public void Add(double[] samples)
        {
            if (m_BitDepth != 16) throw new NotImplementedException();
            int length = samples.Length;
            byte[] array = new byte[length * (m_BitDepth / 8)];
            int index = 0;
            for (int loop = 0; loop < length; loop++)
            {
                int sample = (int)(samples[loop] * 32767.0);
                sample = (sample < -32768) ? -32768 : (sample > 32767) ? 32767 : sample;
                array[index++] = (byte)((uint)sample & 0xFFu);
                array[index++] = (byte)(sample >> 8);
            }
            m_Stream.Write(array, 0, array.Length);
        }

        public void Add(short[] samples)
        {
            if (m_BitDepth != 16) throw new NotImplementedException();
            int length = samples.Length;
            byte[] array = new byte[length * (m_BitDepth / 8)];
            int index = 0;
            for (int loop = 0; loop < length; loop++)
            {
                int sample = samples[loop];
                array[index++] = (byte)((uint)sample & 0xFFu);
                array[index++] = (byte)(sample >> 8);
            }
            m_Stream.Write(array, 0, array.Length);
        }
    }

    [TestClass]
    public class RiffWriterTests
    {
        [TestMethod]
        public void TestToStreamSingleHeader()
        {
            short[] samples = { 0, 1000, -1000, 32767 };
            using var stream = RiffWriter.ToStream(samples, 44100);
            Assert.AreEqual(52, stream.Length);
        }

        [TestMethod]
        public void TestHeaderFields()
        {
            using var stream = RiffWriter.ToStream(new short[0], 44100);
            byte[] b = ((MemoryStream)stream).ToArray();

            Assert.AreEqual("RIFF", Encoding.ASCII.GetString(b, 0, 4));
            Assert.AreEqual("WAVE", Encoding.ASCII.GetString(b, 8, 4));
            Assert.AreEqual("fmt ", Encoding.ASCII.GetString(b, 12, 4));
            Assert.AreEqual(16, BitConverter.ToInt32(b, 16));    // fmt chunk size
            Assert.AreEqual(1, BitConverter.ToInt16(b, 20));     // PCM format
            Assert.AreEqual(1, BitConverter.ToInt16(b, 22));     // mono
            Assert.AreEqual(44100, BitConverter.ToInt32(b, 24)); // sample rate
            Assert.AreEqual(88200, BitConverter.ToInt32(b, 28)); // byte rate = 44100*1*2
            Assert.AreEqual(2, BitConverter.ToInt16(b, 32));     // block align = 1*2
            Assert.AreEqual(16, BitConverter.ToInt16(b, 34));    // bit depth
            Assert.AreEqual("data", Encoding.ASCII.GetString(b, 36, 4));
        }

        [TestMethod]
        public void TestChunkSizesAfterFinalise()
        {
            short[] samples = { 1, 2, 3 }; // 3 samples × 2 bytes = 6 data bytes
            using var stream = RiffWriter.ToStream(samples, 44100);
            byte[] b = ((MemoryStream)stream).ToArray();

            Assert.AreEqual(44 + 6, b.Length);
            Assert.AreEqual(44 + 6 - 8, BitConverter.ToInt32(b, 4)); // RIFF chunk size
            Assert.AreEqual(6, BitConverter.ToInt32(b, 40));          // data chunk size
        }

        [TestMethod]
        public void TestSampleBytesLittleEndian()
        {
            short[] samples = { 0x0102, -1 }; // -1 = 0xFFFF
            using var stream = RiffWriter.ToStream(samples, 44100);
            byte[] b = ((MemoryStream)stream).ToArray();

            Assert.AreEqual(0x02, b[44]); // 0x0102 low byte first
            Assert.AreEqual(0x01, b[45]);
            Assert.AreEqual(0xFF, b[46]); // -1 = 0xFFFF
            Assert.AreEqual(0xFF, b[47]);
        }

        [TestMethod]
        public void TestDoubleScaleAndClamp()
        {
            var ms = new MemoryStream();
            var writer = new RiffWriter(ms, 44100);
            writer.Add(new double[] { 0.0, 1.0, -1.0, 2.0, -2.0 });
            writer.Close();
            byte[] b = ms.ToArray();

            Assert.AreEqual(0, BitConverter.ToInt16(b, 44));      // 0.0 -> 0
            Assert.AreEqual(32767, BitConverter.ToInt16(b, 46));  // 1.0 -> 32767
            Assert.AreEqual(-32767, BitConverter.ToInt16(b, 48)); // -1.0 -> -32767
            Assert.AreEqual(32767, BitConverter.ToInt16(b, 50));  // 2.0 clamped to 32767
            Assert.AreEqual(-32768, BitConverter.ToInt16(b, 52)); // -2.0 clamped to -32768
        }

        [TestMethod]
        public void TestStereoHeader()
        {
            var ms = new MemoryStream();
            new RiffWriter(ms, 44100, channels: 2).Close();
            byte[] b = ms.ToArray();

            Assert.AreEqual(2, BitConverter.ToInt16(b, 22));        // channels = 2
            Assert.AreEqual(176400, BitConverter.ToInt32(b, 28));   // byte rate = 44100*2*2
            Assert.AreEqual(4, BitConverter.ToInt16(b, 32));        // block align = 2*2
        }
    }
}
