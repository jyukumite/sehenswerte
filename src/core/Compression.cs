using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using K4os.Compression.LZ4.Streams;
using K4os.Compression.LZ4;

namespace SehensWerte.Utils
{
    public class Compression
    {
        static public byte[] GZipCompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream())
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                zipStream.Write(data, 0, data.Length);
                zipStream.Close();
                return compressedStream.ToArray();
            }
        }

        static public byte[] GZipDecompress(byte[] data)
        {
            using (var decompressedStream = new MemoryStream(data))
            using (var unzipStream = new GZipStream(decompressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                unzipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }

        static public void GZipCompress(byte[] inputData, string filename)
        {
            using (var inputStream = new MemoryStream(inputData))
            using (var compressedStream = File.OpenWrite(filename))
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                inputStream.CopyTo(zipStream);
                zipStream.Write(new byte[0], 0, 0); // ensure 0-byte file is written too
                zipStream.Close();
                compressedStream.Close();
            }
        }

        static public byte[] GZipDecompress(string filename)
        {
            using (var decompressedStream = File.OpenRead(filename))
            using (var unzipStream = new GZipStream(decompressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                unzipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }

        static public byte[] LZ4Compress(byte[] data)
        {
            using (var compressedStream = new MemoryStream())
            using (var lz4Stream = LZ4Stream.Encode(compressedStream, LZ4Level.L12_MAX))
            {
                lz4Stream.Write(data, 0, data.Length);
                lz4Stream.Flush();
                lz4Stream.Dispose();
                return compressedStream.ToArray();
            }
        }

        static public byte[] LZ4Decompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream(data))
            using (var lz4Stream = LZ4Stream.Decode(compressedStream))
            using (var resultStream = new MemoryStream())
            {
                lz4Stream.CopyTo(resultStream);
                lz4Stream.Dispose();
                return resultStream.ToArray();
            }
        }

        static public void LZ4Compress(byte[] inputData, string filename)
        {
            using (var inputStream = new MemoryStream(inputData))
            using (var compressedStream = File.OpenWrite(filename))
            using (var lz4Stream = LZ4Stream.Encode(compressedStream, LZ4Level.L12_MAX))
            {
                inputStream.CopyTo(lz4Stream);
                lz4Stream.Write(new byte[0], 0, 0); // ensure 0-byte file is written too
                lz4Stream.Flush();
                lz4Stream.Dispose();
                compressedStream.Close();
            }
        }

        static public byte[] LZ4Decompress(string filename)
        {
            using (var compressedStream = File.OpenRead(filename))
            using (var lz4Stream = LZ4Stream.Decode(compressedStream))
            using (var resultStream = new MemoryStream())
            {
                lz4Stream.CopyTo(resultStream);
                lz4Stream.Dispose();
                return resultStream.ToArray();
            }
        }

    }

    [TestClass]
    public class CompressionTests
    {
        [TestMethod]
        public void TestGZip()
        {
            foreach (var test in new[] {
                RandomNumberGenerator.GetBytes(10000),
                RandomNumberGenerator.GetBytes(10),
                Encoding.UTF8.GetBytes("Test of gzip compression"),
                new byte[0]})
            {
                var compressed = Compression.GZipCompress(test);
                CollectionAssert.AreNotEqual(compressed, test);
                byte[] decompressed = Compression.GZipDecompress(compressed);
                CollectionAssert.AreEqual(decompressed, test);

                string fn = Path.GetTempFileName();
                Compression.GZipCompress(test, fn);
                CollectionAssert.AreNotEqual(System.IO.File.ReadAllBytes(fn), test);
                decompressed = Compression.GZipDecompress(fn);
                CollectionAssert.AreEqual(decompressed, test);
                File.Delete(fn);
            }
        }

        [TestMethod]
        public void TestLZ4()
        {
            foreach (var test in new[] {
                RandomNumberGenerator.GetBytes(10000),
                RandomNumberGenerator.GetBytes(10),
                Encoding.UTF8.GetBytes("Test of LZ4 compression"),
                new byte[0]})
            {
                var compressed = Compression.LZ4Compress(test);
                CollectionAssert.AreNotEqual(compressed, test);
                byte[] decompressed = Compression.LZ4Decompress(compressed);
                CollectionAssert.AreEqual(decompressed, test);

                string fn = Path.GetTempFileName();
                Compression.LZ4Compress(test, fn);
                CollectionAssert.AreNotEqual(System.IO.File.ReadAllBytes(fn), test);
                decompressed = Compression.LZ4Decompress(fn);
                CollectionAssert.AreEqual(decompressed, test);
                File.Delete(fn);
            }
        }
    }
}
