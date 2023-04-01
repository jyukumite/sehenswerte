namespace SehensWerte.Maths
{
    public class Ring<T>
    {
        public enum Underflow { Empty, Zero, Available }
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

        public void Insert(T value)
        {
            m_Array[m_Head] = value;
            m_Head = (m_Head + 1) % m_Array.Length;
            if (m_Tail == m_Head) // overflow?
            {
                m_Tail = (m_Tail + 1) % m_Array.Length;
            }
        }

        public void Insert(T[] values)
        {
            if (values == null) return;

            int remaining = values.Length;
            int result = Math.Min(Length, Count + remaining);
            int index = 0;
            while (remaining > 0)
            {
                int copy = Math.Min(m_Array.Length - m_Head, remaining);
                for (int loop = 0; loop < copy; loop++)
                {
                    m_Array[m_Head + loop] = values[index + loop]; // allow compiler to optimise
                }
                m_Head = (m_Head + copy) % m_Array.Length;
                index += copy;
                remaining -= copy;
            }
            m_Tail = (m_Head - result + m_Array.Length) % m_Array.Length;
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

        public T[] Copy(int start, int length, Underflow mode = Underflow.Zero)
        {
            int available = TailCount(start);
            T[] temp;
            if (mode == Underflow.Empty && available < length)
            {
                temp = new T[] { };
            }
            else
            {
                int copy = (mode == Underflow.Available && available < length) ? available : length;
                temp = new T[copy];
                int zeroPad = (available < copy) ? (copy - available) : 0;
                int right = Math.Min(copy - zeroPad, m_Array.Length - start);
                int left = copy - right - zeroPad;

                for (int loop = 0; loop < right; loop++)
                {
                    temp[zeroPad + loop] = m_Array[start + loop]; // allow compiler to optimise
                }
                for (int loop = 0; loop < left; loop++)
                {
                    temp[zeroPad + right + loop] = m_Array[loop]; // allow compiler to optimise
                }
            }
            return temp;
        }

        public T[] TailCopy(ref int tail, int copyCount, int skipCount, Underflow mode)
        {
            T[] result = Copy(tail, copyCount, mode);
            tail = (tail + Math.Min(TailCount(tail), skipCount)) % m_Array.Length;
            return result;
        }

        public int TailCount(int tail) // head-tail
        {
            return (m_Head + m_Array.Length - tail) % m_Array.Length;
        }
    }
}
