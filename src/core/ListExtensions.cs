using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace SehensWerte
{
    public static class ListExtensions
    {
        public static T[] Dequeue<T>(this Queue<T> queue, int count)
        {
            T[] result = new T[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = queue.Dequeue();
            }
            return result;
        }

        public static T[] GetColumn<T>(this T[,] array, int columnNumber)
        {
            // var test_data = new[,]
            // {
            //     {'row0 col0', 'row0 col1' },
            //     {'row1 col0', 'row1 col1' }
            // };
            return Enumerable.Range(0, array.GetLength(0))
                    .Select(x => array[x, columnNumber])
                    .ToArray();
        }

        public static T[] GetRow<T>(this T[,] array, int rowNumber)
        {
            return Enumerable.Range(0, array.GetLength(1))
                    .Select(x => array[rowNumber, x])
                    .ToArray();
        }

        public static void Add<T1, T2>(this IList<Tuple<T1, T2>> list, T1 item1, T2 item2)
        {
            list.Add(Tuple.Create(item1, item2));
        }

        public static void Add<T1, T2, T3>(this IList<Tuple<T1, T2, T3>> list, T1 item1, T2 item2, T3 item3)
        {
            list.Add(Tuple.Create(item1, item2, item3));
        }

        public static void Add<T1, T2, T3, T4>(this IList<Tuple<T1, T2, T3, T4>> list, T1 item1, T2 item2, T3 item3, T4 item4)
        {
            list.Add(Tuple.Create(item1, item2, item3, item4));
        }

        public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
        {
            foreach (T item in enumeration)
            {
                action(item);
            }
        }

        public static void ParallelSort<T>(this List<T> array, Comparison<T> comparer, int maxCores = 0)
        {
            const int fallbackCount = 200;
            const int medianSampleSize = 50;
            const int medianSelectCount = 10000;
            int maxDegreeOfParallelism = maxCores > 0 ? maxCores : Environment.ProcessorCount;
            int taskCount = 0;
            var partitions = new ConcurrentStack<(int left, int right)>();
            var innerExceptions = new ConcurrentBag<Exception>();

            T[] sorted = array.ToArray();
            int count = array.Count;

            var complete = new ManualResetEvent(false);

            if (count <= 1)
            {
                return;
            }
            else if (count < fallbackCount)
            {
                Array.Sort(sorted, Comparer<T>.Create(comparer));
            }
            else
            {
                partitions.Push((0, count - 1));
                startTask();
                complete.WaitOne();
            }
            array.Clear();
            array.AddRange(sorted);
            if (innerExceptions.Count > 0)
            {
                throw new AggregateException("Inner exceptions in ParallelSort", innerExceptions);
            }

            void startTask()
            {
                Interlocked.Increment(ref taskCount);
                Task.Run(() =>
                {
                    try
                    {
                        bool again = true;
                        while (again)
                        {
                            (int left, int right) partition;
                            again = partitions.TryPop(out partition);
                            if (again)
                            {
                                quickSort(partition.left, partition.right);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        innerExceptions.Add(ex);
                    }
                    if (Interlocked.Decrement(ref taskCount) == 0)
                    {
                        complete.Set();
                    }
                });
            }

            void quickSort(int left, int right)
            { //http://en.wikipedia.org/wiki/Quicksort#Hoare_partition_scheme
                if (left < right)
                {
                    int l = left;
                    int p = (left + right) / 2;
                    int r = right;
                    if ((right - left + 1) >= medianSelectCount)
                    {
                        p = medianIndex(left, right);
                    }

                    if (comparer(sorted[r], sorted[l]) < 0)
                    {
                        swapEntry(r, l);
                    }
                    if (comparer(sorted[p], sorted[l]) < 0)
                    {
                        swapEntry(p, l);
                    }
                    if (comparer(sorted[r], sorted[p]) < 0)
                    {
                        swapEntry(r, p);
                    }
                    var pivot = sorted[p];
                    while (l <= r)
                    {
                        while (l <= right && comparer(sorted[l], pivot) < 0)
                        {
                            l++;
                        }
                        while (r >= left && comparer(sorted[r], pivot) > 0)
                        {
                            r--;
                        }
                        if (l <= r)
                        {
                            swapEntry(l, r);
                            l++;
                            r--;
                        }
                    }
                    if (left < r)
                    {
                        subSort(left, r);
                    }
                    if (l < right)
                    {
                        subSort(l, right);
                    }
                }
            }

            int medianIndex(int left, int right)
            {
                int stride = (right - left + 1) / medianSampleSize;
                (T value, int index)[] temp = new (T value, int index)[medianSampleSize];
                for (int i = 0; i < medianSampleSize; i++)
                {
                    int idx = left + i * stride;
                    temp[i] = (sorted[idx], idx);
                }
                Array.Sort(temp, (x, y) => comparer(x.value, y.value));
                return temp[medianSampleSize / 2].index;
            }

            void swapEntry(int a, int b)
            {
                T temp = sorted[a];
                sorted[a] = sorted[b];
                sorted[b] = temp;
            }

            void subSort(int left, int right)
            {
                if (right > left)
                {
                    if (right - left <= fallbackCount)
                    {
                        Array.Sort(sorted, left, right - left + 1, Comparer<T>.Create(comparer));
                    }
                    else
                    {
                        partitions.Push((left, right));
                        if (taskCount < maxDegreeOfParallelism)
                        {
                            startTask();
                        }
                    }
                }
            }
        }
    }

    [TestClass]
    public class ListExtensionTest
    {
        [TestMethod]
        public void ParallelSortTest()
        {
            {
                var list = new List<int> { 5, 3, 1, 4, 2 }; // below fallback
                var expected = list.OrderBy(x => x).ToList();
                list.ParallelSort((x, y) => x.CompareTo(y), maxCores: 1);
                CollectionAssert.AreEqual(list, expected);
            }
            //for (int loop = 0; loop < 10000; loop++)
            {
                var random = new Random();
                int count = 5000; // above fallback point
                var expected = Enumerable.Range(0, count).Select(x => (a: x, b: random.Next(count), c: "hello world")).ToList();
                var shuffled = expected.OrderBy(x => x.b).ToList();
                shuffled.ParallelSort((x, y) => x.CompareTo(y), maxCores: 2);
                CollectionAssert.AreEqual(shuffled, expected);
            }
        }

        //[TestMethod]
        public void ParallelSortLargeTest()
        {
            string results = "";
            void time(Func<List<(int a, int b, string c)>> a, List<(int a, int b, string c)> expected, string prefix)
            {
                var seconds1 = HighResTimer.StaticSeconds;
                var result = a();
                var seconds2 = HighResTimer.StaticSeconds;
                results = results + $"{prefix}={seconds2 - seconds1} ";
                CollectionAssert.AreEqual(result, expected);
            }

            var random = new Random();
            const int count = 10 * 1000 * 1000 - 50;
            var expected = Enumerable.Range(0, count).Select(x => (a: x, b: random.Next(count), c: "hello world")).ToList();
            var shuffled = expected.OrderBy(x => x.b).ToList();

            time(() => { var list = shuffled.ToArray(); return list.OrderBy(x => x.a).ToList(); }, expected, "list.Orderby");
            time(() => { return shuffled.AsParallel().OrderBy(x => x.a).ToList(); }, expected, "AsParallel.OrderBy"); // this deadlocks in some cases with 100% CPU (34M row dataset)

            time(() => { var copy = shuffled.ToList(); copy.ParallelSort((x, y) => x.a.CompareTo(y.a)); return copy; }, expected, "List.ParallelSort");
            time(() => { var copy = shuffled.ToArray(); Array.Sort(copy, (x, y) => x.a.CompareTo(y.a)); return copy.ToList(); }, expected, "Array.Sort");

            MessageBox.Show(results);
        }
    }
}