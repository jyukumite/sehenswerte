using System;
using System.IO;
using System.Text;

namespace SehensWerte.Files
{
    public class RiffReader
    {
        public int ChannelCount { get; private set; }
        public double SamplesPerSecond { get; private set; }
        public int BitDepth { get; private set; }
        public double[][]? Buffer { get; private set; }

        private ushort AudioFormat;

        public double[] Channel(int channel)
        {
            return (Buffer == null || Buffer.Length <= channel) ? new double[] { } : Buffer[channel];
        }


        public RiffReader(string fileName) : this(new FileStream(fileName, FileMode.Open, FileAccess.Read))
        {
        }

        public RiffReader(Stream waveStream)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(waveStream, Encoding.UTF8, leaveOpen: true))
                {
                    string fileType = new string(reader.ReadChars(4));
                    if (fileType != "RIFF")
                    {
                        throw new NotSupportedException("This is not a valid RIFF file.");
                    }
                    int fileSize = reader.ReadInt32();
                    string riffType = new string(reader.ReadChars(4));
                    if (riffType != "WAVE")
                    {
                        throw new NotSupportedException("This is not a valid WAVE file.");
                    }
                    string formatChunkId = new string(reader.ReadChars(4));
                    if (formatChunkId != "fmt ")
                    {
                        throw new NotSupportedException("Invalid format chunk ID in WAVE file.");
                    }
                    int formatChunkSize = reader.ReadInt32();
                    AudioFormat = reader.ReadUInt16();
                    ChannelCount = reader.ReadUInt16();
                    SamplesPerSecond = reader.ReadInt32();
                    int byteRate = reader.ReadInt32();
                    ushort blockAlign = reader.ReadUInt16();
                    BitDepth = reader.ReadUInt16();

                    if (AudioFormat != 1 && AudioFormat != 3) // 1 = PCM, 3 = IEEE float
                    {
                        throw new NotSupportedException("WAVE file encoding not supported.");
                    }

                    reader.BaseStream.Seek(formatChunkSize - 16, SeekOrigin.Current); // Skip the rest of the fmt chunk

                    // Find the data chunk
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        string chunkId = new string(reader.ReadChars(4));
                        int chunkSize = reader.ReadInt32();
                        if (chunkId.ToLower() == "data")
                        {
                            ReadDataChunk(reader, chunkSize);
                            break;
                        }
                        else
                        {
                            reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Error reading WAVE file.", ex);
            }
        }

        private void ReadDataChunk(BinaryReader reader, int chunkSize)
        {
            int bytesPerSample = BitDepth / 8;
            int totalSamples = chunkSize / bytesPerSample / ChannelCount;

            Buffer = new double[ChannelCount][];
            for (int i = 0; i < ChannelCount; i++)
            {
                Buffer[i] = new double[totalSamples];
            }

            for (int sample = 0; sample < totalSamples; sample++)
            {
                for (int channel = 0; channel < ChannelCount; channel++)
                {
                    double sampleValue;
                    if (BitDepth == 16)
                    {
                        // ushort
                        sampleValue = reader.ReadInt16() / (double)short.MaxValue;
                    }
                    else if (BitDepth == 24)
                    {
                        // 24-bit sample
                        byte[] sampleBytes = reader.ReadBytes(3);
                        int sampleInt = sampleBytes[0] | (sampleBytes[1] << 8) | (sampleBytes[2] << 16);
                        sampleInt = (sampleInt & 0x800000) > 0 ? (sampleInt | ~0xFFFFFF) : sampleInt; // sign extend
                        sampleValue = sampleInt / (double)(1 << 23);
                    }
                    else if (BitDepth == 32 && AudioFormat == 3) // 32-bit float
                    {
                        sampleValue = reader.ReadSingle();
                    }
                    else
                    {
                        throw new NotSupportedException($"Unsupported bit depth: {BitDepth}.");
                    }

                    Buffer[channel][sample] = sampleValue;
                }
            }
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
