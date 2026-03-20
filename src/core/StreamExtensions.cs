using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace SehensWerte
{
    public sealed class AppendableStream : Stream
    {
        private readonly MemoryStream _ms = new();
        private long _readPos;

        public void Append(byte[] data, int offset, int count)
        {
            _ms.Position = _ms.Length;
            _ms.Write(data, offset, count);
            _ms.Position = _readPos;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _ms.Position = _readPos;
            int n = _ms.Read(buffer, offset, count);
            _readPos = _ms.Position;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _readPos; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [TestClass]
    public class AppendableStreamTests
    {
        [TestMethod]
        public void ReadReturnsZeroWhenEmpty()
        {
            var stream = new AppendableStream();
            Assert.AreEqual(0, stream.Read(new byte[10], 0, 10));
        }

        [TestMethod]
        public void AppendThenRead()
        {
            var stream = new AppendableStream();
            byte[] data = Encoding.UTF8.GetBytes("hello world");
            stream.Append(data, 0, data.Length);
            byte[] buf = new byte[data.Length];
            int count = stream.Read(buf, 0, buf.Length);
            Assert.AreEqual(data.Length, count);
            CollectionAssert.AreEqual(data, buf);
        }

        [TestMethod]
        public void IncrementalAppendAndRead()
        {
            var stream = new AppendableStream();
            byte[] part1 = Encoding.UTF8.GetBytes("hello ");
            byte[] part2 = Encoding.UTF8.GetBytes("world");
            stream.Append(part1, 0, part1.Length);
            byte[] buf = new byte[100];
            int count1 = stream.Read(buf, 0, buf.Length);
            Assert.AreEqual(part1.Length, count1);
            Assert.AreEqual(0, stream.Read(buf, 0, buf.Length));
            stream.Append(part2, 0, part2.Length);
            int count2 = stream.Read(buf, count1, buf.Length - count1);
            Assert.AreEqual(part2.Length, count2);
            Assert.AreEqual("hello world", Encoding.UTF8.GetString(buf, 0, count1 + count2));
        }
    }
}
