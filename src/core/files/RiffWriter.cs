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
            riffWriter.WriteHeader(samplesPerSecond);
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
}
