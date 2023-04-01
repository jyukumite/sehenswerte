using System.Text;

namespace SehensWerte.Files
{
    public class RiffReader
    {
        private int m_ChannelCount;
        public int ChannelCount => m_ChannelCount;
        public double SamplesPerSecond;
        public double[][]? Buffer;

        public double[] Channel(int channel)
        {
            return (Buffer == null || Buffer.Length <= channel) ? new double[] { } : Buffer[channel];
        }

        public RiffReader(string fileName) : this(new FileStream(fileName, FileMode.Open, FileAccess.Read))
        {
        }

        public RiffReader(Stream waveStream)
        {
            string fileType = ReadString(waveStream, 4);
            ReadInt4(waveStream);
            string blockType = ReadString(waveStream, 4);
            int formatChunkSize = 0;
            string? formatType = FindChunk(ref waveStream, SeekOrigin.Current, "fmt ", out formatChunkSize);
            int format = ReadInt2(waveStream);
            m_ChannelCount = ReadInt2(waveStream);
            SamplesPerSecond = ReadInt4(waveStream);
            ReadBytes(waveStream, 6);
            int bitdepth = ReadInt2(waveStream);
            waveStream.Seek(formatChunkSize - 16, SeekOrigin.Current);
            int dataChunkSize;
            string? dataType = FindChunk(ref waveStream, SeekOrigin.Current, "data", out dataChunkSize);

            if (dataChunkSize == 0)
            {
                dataChunkSize = (int)(waveStream.Length - waveStream.Position);
            }

            if (fileType == "RIFF" && blockType == "WAVE" && formatType == "fmt " && dataType == "data" && format == 1 && bitdepth == 16)
            {
                byte[] array = ReadBytes(waveStream, dataChunkSize);
                int count = dataChunkSize / ChannelCount / (bitdepth / 8);
                Buffer = new double[ChannelCount][];
                for (int i = 0; i < ChannelCount; i++)
                {
                    Buffer[i] = new double[count];
                }
                int index = 0;
                for (int sample = 0; sample < count; sample++)
                {
                    for (int channel = 0; channel < ChannelCount; channel++)
                    {
                        short value = (short)(array[index++] | (array[index++] << 8));
                        Buffer[channel][sample] = (double)value / 32767.0;
                    }
                }
            }
            else
            {
                throw new NotImplementedException("");
            }
        }

        private string? FindChunk(ref Stream waveStream, SeekOrigin seekOrigin, string chunkName, out int chunkSize)
        {
            chunkSize = 0;
            long offset = waveStream.Seek(0L, seekOrigin);
            string? text = null;
            do
            {
                if (waveStream.Seek(chunkSize, SeekOrigin.Current) == waveStream.Length)
                {
                    waveStream.Seek(offset, SeekOrigin.Begin);
                    return null;
                }
                text = ReadString(waveStream, 4);
                chunkSize = ReadInt4(waveStream);
            } while (text != chunkName);
            return text;
        }

        private int ReadInt2(Stream waveStream)
        {
            byte[] array = ReadBytes(waveStream, 2);
            return array[0] | (array[1] << 8);
        }

        private int ReadInt4(Stream waveStream)
        {
            byte[] array = ReadBytes(waveStream, 4);
            return array[0] | (array[1] << 8) | (array[2] << 16) | (array[3] << 24);
        }

        private string ReadString(Stream waveStream, int length)
        {
            byte[] array = ReadBytes(waveStream, length);
            return ASCIIEncoding.ASCII.GetString(array, 0, array.Length);
        }

        private static byte[] ReadBytes(Stream waveStream, int length)
        {
            byte[] array = new byte[length];
            int num = waveStream.Read(array, 0, length);
            if (num != length)
            {
                throw new Exception($"Read {num} of {length} bytes");
            }
            return array;
        }

        public static double[] ToDouble(string fileName, out double samplesPerSecond)
        {
            using (FileStream waveStream = new FileStream(fileName, FileMode.Open))
            {
                RiffReader riffReader = new RiffReader(waveStream);
                samplesPerSecond = riffReader.SamplesPerSecond;
                return riffReader.Channel(0);
            }
        }

        public static Int16[] ToShort(string fileName)
        {
            using (FileStream waveStream = new FileStream(fileName, FileMode.Open))
            {
                RiffReader riffReader = new RiffReader(waveStream);
                double[] array = riffReader.Channel(0);
                short[] shortArray = new short[array.Length];
                for (int loop = 0; loop < array.Length; loop++)
                {
                    int num = (int)(array[loop] * 32767.0);
                    num = (num < -32768) ? -32768 : (num > 32767) ? 32767 : num;
                    shortArray[loop] = (short)num;
                }
                return shortArray;
            }
        }
    }
}
