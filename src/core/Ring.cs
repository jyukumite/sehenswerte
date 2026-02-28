using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Filters;

namespace SehensWerte.Maths
{
    public class Ring<T>
    {
        public enum Underflow
        {
            Null, // return null if not enough data
            Zeros, // add 0's to the result if not enough data (could be left or right)
            Available // return as many as available if less than requested
        }
        private T[] m_Array;
        private int m_Head; // next input
        private int m_Tail;

        public Ring(int length)
        {
            m_Array = new T[length + 1];
        }

        public T this[int index] => m_Array[(m_Tail + index) % m_Array.Length];
        public int Count => TailCount(m_Tail);
        public int Length => m_Array.Length - 1;
        public T[] ValidSamples() => Copy(m_Tail, Count);
        public T[] AllSamples() => Copy(m_Head + 1, m_Array.Length - 1);

        public void Clear()
        {
            m_Head = m_Tail = 0;
        }

        public T Insert(T value)
        {
            m_Array[m_Head] = value;
            m_Head = (m_Head + 1) % m_Array.Length;
            if (m_Tail == m_Head) // overflow?
            {
                m_Tail = (m_Tail + 1) % m_Array.Length;
            }
            return m_Array[m_Head]; // last out
        }

        public void Insert(T[] values)
        {
            if (values == null) return;

            int count = values.Length;
            int overflow = Count + count - Length;
            int index = 0;
            while (count > 0)
            {
                int copy = Math.Min(m_Array.Length - m_Head, count);
                for (int loop = 0; loop < copy; loop++)
                {
                    m_Array[m_Head + loop] = values[index + loop]; // allow compiler to optimise
                }
                m_Head = (m_Head + copy) % m_Array.Length;
                index += copy;
                count -= copy;
            }

            if (overflow > 0)
            {
                m_Tail = (m_Tail + overflow) % m_Array.Length;
            }
        }

        public void Set(T value)
        {
            for (int loop = 0; loop < m_Array.Length; loop++)
            {
                m_Array[loop] = value;
            }
            m_Head = 0;
            m_Tail = 1;
        }

        public void Fill(T value)
        {
            while (Length != Count)
            {
                Insert(value);
            }
        }

        public T[]? Copy(int start, int length, Underflow mode = Underflow.Zeros)
        {
            int available = TailCount(start);
            int zeros = 0;
            int copy = length;
            if (available < length)
            {
                switch (mode)
                {
                    case Underflow.Null:
                        return null;
                    case Underflow.Available:
                        copy = available;
                        break;
                    case Underflow.Zeros:
                        zeros = copy - available;
                        copy = available;
                        break;
                }
            }

            T[] result = new T[copy];
            int right = Math.Min(copy, m_Array.Length - start);
            int left = copy - right;
            for (int loop = 0; loop < right; loop++)
            {
                result[zeros + loop] = m_Array[start + loop]; // allow compiler to optimise
            }
            for (int loop = 0; loop < left; loop++)
            {
                result[zeros + right + loop] = m_Array[loop]; // allow compiler to optimise
            }
            return result;
        }

        public T[]? TailCopy(ref int tail, int copyCount, int skipCount, Underflow mode)
        {
            int available = TailCount(tail);
            T[]? result = Copy(tail, copyCount, mode);
            switch (mode)
            {
                case Underflow.Null: Skip(ref tail, result == null ? 0 : skipCount); break;
                case Underflow.Available:
                case Underflow.Zeros: Skip(ref tail, Math.Min(available, skipCount)); break;
            }
            return result;
        }

        public void Skip(ref int tail, int skip)
        {
            tail = (tail + skip) % m_Array.Length;
        }

        public int TailCount(int tail) // head-tail
        {
            return (m_Head + m_Array.Length - tail) % m_Array.Length;
        }
    }

    [TestClass]
    public class RingTests
    {
        [TestMethod]
        public void TestInsertReturnWhileFilling()
        {
            var ring = new Ring<double>(3);
            Assert.AreEqual(0.0, ring.Insert(10.0), "t=0: not full, expect 0");
            Assert.AreEqual(0.0, ring.Insert(20.0), "t=1: not full, expect 0");
            Assert.AreEqual(0.0, ring.Insert(30.0), "t=2: not full, expect 0");
        }

        [TestMethod]
        public void TestInsertReturnWhenFull()
        {
            // Once full, Insert displaces the oldest element; the return value should be that displaced element
            var ring = new Ring<double>(3);
            ring.Insert(10.0);
            ring.Insert(20.0);
            ring.Insert(30.0);
            Assert.AreEqual(10.0, ring.Insert(40.0), "t=3: displaced 10");
            Assert.AreEqual(20.0, ring.Insert(50.0), "t=4: displaced 20");
            Assert.AreEqual(30.0, ring.Insert(60.0), "t=5: displaced 30");
        }

        [TestMethod]
        public void TestDelayFilterDelay3()
        {
            // DelayFilter(n) wraps Ring<double>(n)
            var filter = new DelayFilter(3);
            double[] input    = { 1, 2, 3, 4, 5, 6, 7 };
            double[] expected = { 0, 0, 0, 1, 2, 3, 4 };
            for (int i = 0; i < input.Length; i++)
            {
                Assert.AreEqual(expected[i], filter.Insert(input[i]), $"t={i}");
            }
        }

        [TestMethod]
        public void TestDelayFilterDelay1()
        {
            // Edge case: delay of 1.
            var filter = new DelayFilter(1);
            double[] input    = { 5, 10, 15 };
            double[] expected = { 0,  5, 10 };
            for (int i = 0; i < input.Length; i++)
            {
                Assert.AreEqual(expected[i], filter.Insert(input[i]), $"t={i}");
            }
        }
    }
}
