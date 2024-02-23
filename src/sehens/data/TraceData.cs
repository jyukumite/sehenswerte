using SehensWerte.Maths;
using SehensWerte.Utils;
using System.Collections;

namespace SehensWerte.Controls.Sehens
{
    public class TraceData : IDisposable
    {
        public object? Tag;

        [XmlSave]
        public double InputLeftmostUnixTime
        {
            get
            {
                lock (DataLock)
                {
                    return m_InputData.LeftmostUnixTime;
                }
            }
            set
            {
                lock (DataLock)
                {
                    m_InputData.LeftmostUnixTime = value;
                }
                ForEachViewer(x => x.TraceDataSamplesChanged(this));
            }
        }

        public string Name // serialised by SehensSave
        {
            get => m_Name;
            set
            {
                if (m_Name != value)
                {
                    string name = m_Name;
                    m_Name = value;
                    ForEachViewer(x => x.TraceDataRename(this, name, m_Name));
                }
            }
        }

        [XmlSave]
        public bool StopUpdates
        {
            get => m_StoppedData != null;
            set
            {
                lock (DataLock)
                {
                    if ((value && m_StoppedData == null) || (!value && m_StoppedData != null))
                    {
                        m_StoppedData = value ? m_InputData.DeepClone() : null;
                    }
                }
                NotifyChanges();

            }
        }

        internal void NotifyChanges()
        {
            ForEachViewer(x => x.TraceDataSamplesChanged(this));
            ForEachViewer(x => x.TraceDataSettingsChanged(this));
        }

        public bool HasVisibleViewer
        {
            get
            {
                lock (m_ViewerLock)
                {
                    return m_ViewerList.Any(x => x.Visible);
                }
            }
        }

        public TraceView? FirstView
        {
            //note: better to use ForEachViewer
            //fixme: most callers assume only one viewer
            get
            {
                lock (m_ViewerLock)
                {
                    return m_ViewerList.Where(x => x is TraceView).Select(x => x as TraceView).FirstOrDefault();
                }
            }
        }

        public IEnumerable<TraceFeature> InputFeatures
        {
            get
            {
                lock (DataLock)
                {
                    return m_InputData.Features.ToArray();
                }
            }
            set
            {
                lock (DataLock)
                {
                    m_InputData.Features = value.ToList();
                    m_InputData.Features.Sort(new TraceFeature.FeatureCompare());
                }
                ForEachViewer(x => x.TraceDataSamplesChanged(this));
            }
        }

        [XmlSave]
        public string AxisTitleBottom
        {
            get => m_AxisTitleBottom;
            set
            {
                m_AxisTitleBottom = value;
                ForEachViewer(x => x.TraceDataSettingsChanged(this));
            }
        }

        [XmlSave]
        public string AxisTitleLeft
        {
            get => m_AxisTitleLeft;
            set
            {
                m_AxisTitleLeft = value;
                ForEachViewer(x => x.TraceDataSettingsChanged(this));
            }
        }

        [XmlSave]
        public string VerticalUnit
        {
            get => m_VerticalUnit;
            set
            {
                m_VerticalUnit = value ?? "";
                ForEachViewer(x => x.TraceDataSettingsChanged(this));
            }
        }

        public IEnumerable<TraceFeature> ViewedFeatures
        {
            get
            {
                lock (DataLock)
                {
                    return m_ViewedData.Features.ToArray();
                }
            }
        }

        public double[] ViewedSamplesAsDouble
        {
            get
            {
                lock (DataLock)
                {
                    return m_ViewedData.InputSampleCopy();
                }
            }
        }

        public double[] ViewedSamplesInterpolatedAsDouble
        {//returns normal samples if not real yt trace
            get
            {
                lock (DataLock)
                {
                    return m_ViewedData.InterpolatedCopy();
                }
            }
        }

        public int ViewedSampleCount
        {
            get
            {
                lock (DataLock)
                {
                    return m_ViewedData.InputSampleCopy().Length;
                }
            }
        }

        public double ViewedSamplesPerSecond => m_ViewedData.SamplesPerSecond;

        public double[]? ViewedUnixTime => m_ViewedData.UnixTime;

        public double ViewedLeftmostUnixTime => m_ViewedData.LeftmostUnixTime;

        public double[] InputSamplesAsDouble
        {
            get
            {
                lock (DataLock)
                {
                    return m_InputData.InputSampleCopy();
                }
            }
        }

        public int InputSampleCount
        {
            get
            {
                lock (DataLock)
                {
                    return m_InputData.InputSampleCopy().Length;
                }
            }
        }

        public double InputSamplesPerSecond
        {
            get => m_InputData.SamplesPerSecond;
            set
            {
                if (m_InputData.SamplesPerSecond != value
                    && double.IsFinite(value)
                    && value >= 0
                    && m_InputData.UnixTime == null)
                {
                    lock (DataLock)
                    {
                        m_InputData.SamplesPerSecond = value;
                    }
                    NotifyChanges();
                }
            }
        }

        public int InputSampleNumberDisplayOffset
        {
            get => m_InputData.SampleNumberDisplayOffset;
            set
            {
                lock (DataLock)
                {
                    m_InputData.SampleNumberDisplayOffset = value;
                }
                ForEachViewer(x => x.TraceDataSettingsChanged(this));
            }
        }

        public int VisibleViewerCount
        {
            get
            {
                lock (m_ViewerLock)
                {
                    return m_ViewerList.Count(x => x.IsViewer);
                }
            }
        }

        private object m_ViewerLock = new object();
        private List<ITraceView> m_ViewerList = new List<ITraceView>();

        internal object DataLock = new object();
        private DataStore m_InputData = new DataStore();
        private DataStore m_ViewedData => m_StoppedData ?? m_InputData;

        internal DataStore SaveInputData { get => m_InputData; set { m_InputData = value; } }
        internal DataStore? SaveViewedData { get => m_StoppedData; set { m_StoppedData = value; } }

        public TimeRange UnixTimeRange { get { lock (DataLock) { return m_ViewedData.UnixTimeRange; } } }
        public bool ViewedIsYTTrace { get { lock (DataLock) { return m_ViewedData.UnixTime != null || (m_ViewedData.LeftmostUnixTime != 0 && m_ViewedData.SamplesPerSecond != 0); } } }

        private DataStore? m_StoppedData = null;

        private string m_Name = "";
        private string m_AxisTitleBottom = "";
        private string m_AxisTitleLeft = "";
        private string m_VerticalUnit = "";

        private bool m_Closing;

        internal TraceData()
        {
        }

        public TraceData(string name)
        {
            m_Name = name;
        }

        public TraceData(string name, TraceData from, bool viewedData)
        {
            m_Name = name;
            m_InputData = (viewedData ? from.m_ViewedData : from.m_InputData).DeepClone();
        }

        public void AddViewer(ITraceView viewer)
        {
            lock (m_ViewerLock)
            {
                if (!m_Closing && !m_ViewerList.Contains(viewer))
                {
                    m_ViewerList.Add(viewer);
                }
            }
        }

        public void RemoveViewer(ITraceView viewer)
        {
            lock (m_ViewerLock)
            {
                m_ViewerList.Remove(viewer);
            }
        }

        public void ForEachViewer(Action<ITraceView> action)
        {
            ITraceView[] copy;
            lock (m_ViewerLock)
            {
                copy = m_ViewerList.ToArray();
            }
            List<Exception> exceptions = new List<Exception>();
            foreach (ITraceView viewer in copy)
            {
                try
                {
                    action(viewer);
                }
                catch (Exception item)
                {
                    exceptions.Add(item);
                }
            }
            if (exceptions.Count > 1)
            {
                throw new NestedException(exceptions);
            }
            if (exceptions.Count > 0)
            {
                throw exceptions[0];
            }
        }

        public virtual void Dispose()
        {
            m_ViewerList = new List<ITraceView>();
            m_InputData = new DataStore();
            m_StoppedData = null;
        }

        public virtual void Close()
        {
            ITraceView[] viewerListCopy;
            lock (m_ViewerLock)
            {
                m_Closing = true;
                viewerListCopy = m_ViewerList.ToArray();
                m_ViewerList.Clear();
            }

            List<Exception> exceptions = new List<Exception>();
            foreach (var traceDataCallback in viewerListCopy)
            {
                try
                {
                    traceDataCallback.TraceDataClosed(this);
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
            if (exceptions.Count > 1)
            {
                throw new NestedException(exceptions);
            }
            if (exceptions.Count > 0)
            {
                throw exceptions[0];
            }
        }

        public void Clear()
        {
            lock (DataLock)
            {
                m_InputData = new DataStore();
            }
            NotifyChanges();
        }


        public void AddFeature(int sampleNumber, string text)
        {
            AddFeature(new TraceFeature
            {
                SampleNumber = sampleNumber,
                Text = text
            });
        }

        public void AddFeature(TraceFeature feature)
        {
            lock (DataLock)
            {
                int num = m_InputData.Features.BinarySearch(feature, new TraceFeature.FeatureCompare());
                if (num < 0)
                {
                    m_InputData.Features.Insert(~num, feature);
                }
                else
                {
                    m_InputData.Features.Insert(num, feature);
                }
            }
            ForEachViewer(x => x.TraceDataSamplesChanged(this));
        }

        public bool InputValuesAllIdentical()
        {
            lock (DataLock)
            {
                double[] values = InputSamplesAsDouble;
                if (values.Length < 2) return true;
                double firstValue = values[0];
                for (int loop = 1; loop < values.Length; loop++)
                {
                    if (values[loop] != firstValue)
                    {
                        return false;
                    }
                }
                return true;
            }
        }


        public TraceData Update(IEnumerable<double> samples, IEnumerable<double> unixTime)
        {
            double[] data = samples.ToArray();
            double[] time = unixTime.ToArray();
            Array.Sort(time, data);
            return UpdateByRef(data, time, double.NaN);
        }

        public TraceData Update<T>(IEnumerable<T> samples, double samplesPerSecond = double.NaN)
        {
            return UpdateByRef(samples.ToArray(), samplesPerSecond);
        }

        public TraceData Update<T>(T[] samples, double samplesPerSecond = double.NaN)
        {
            return UpdateByRef(samples[..], samplesPerSecond);
        }

        public TraceData UpdateByRef(object samples, double samplesPerSecond = double.NaN)
        {
            return UpdateByRef(samples, null, samplesPerSecond);
        }

        public TraceData UpdateByRef(object samples, double[]? unixTime = null, double samplesPerSecond = double.NaN)
        {
            bool guiChange = false;
            lock (DataLock)
            {
                m_InputData.InputSampleCache = null;
                m_InputData.InterpolatedSampleCache = null;
                if (double.IsFinite(samplesPerSecond) && m_InputData.SamplesPerSecond != samplesPerSecond)
                {
                    m_InputData.SamplesPerSecond = samplesPerSecond;
                    guiChange = true;
                }
                if (unixTime != null && m_InputData.SamplesPerSecond != 0 && !double.IsFinite(samplesPerSecond))
                {
                    m_InputData.SamplesPerSecond = 0; // invalidate calculated sample rate
                    guiChange = true;
                }

                m_InputData.UnixTime = unixTime != null && unixTime.Length == DataStore.Count(samples) ? unixTime : null;
                m_InputData.InputSamples = samples;
            }
            if (guiChange)
            {
                ForEachViewer(x => x.TraceDataSettingsChanged(this));
            }
            ForEachViewer(x => x.TraceDataSamplesChanged(this));
            return this;
        }

        public TraceData AppendRing(double[] samples, int ringLength, double samplesPerSecond = double.NaN)
        {
            bool guiChange = false;
            lock (DataLock)
            {
                m_InputData.InputSampleCache = null;
                m_InputData.InterpolatedSampleCache = null;
                if (double.IsFinite(InputSamplesPerSecond) && m_InputData.SamplesPerSecond != samplesPerSecond)
                {
                    m_InputData.SamplesPerSecond = samplesPerSecond;
                    guiChange = true;
                }
                m_InputData.UnixTime = null;

                Ring<double>? ring = m_InputData.InputSamples as Ring<double>;
                if (ring == null || ring.Length != ringLength)
                {
                    m_InputData.InputSamples = ring = new Ring<double>(ringLength);
                    ring.Set(samples.Length == 0 ? 0 : samples[0]);
                }
                ring.Insert(samples);
            }
            if (guiChange)
            {
                ForEachViewer(x => x.TraceDataSettingsChanged(this));
            }
            ForEachViewer(x => x.TraceDataSamplesChanged(this));
            return this;
        }

        public TraceData AppendRing(double[] samples, double[] unixTime, int ringLength, double samplesPerSecond = double.NaN)
        {
            throw new NotImplementedException();
        }


        public void SetSelectedSamples(int leftSampleNumber, int rightSampleNumber, double to)
        {
            lock (DataLock)
            {
                if (m_ViewedData.UnixTime == null) return;
                var array = DataStore.CopyToDouble(m_ViewedData.InputSamples);
                for (int loop = leftSampleNumber; loop < rightSampleNumber; loop++)
                {
                    if (loop >= 0 && loop < array.Length)
                    {
                        array[loop] = 0.0;
                    }
                }
                m_ViewedData.InputSamples = to;
            }
            ForEachViewer(x => x.TraceDataSamplesChanged(this));
        }

        internal (int leftIndex, int rightIndex, double[] samples, double[] time) SnapshotYTProjection(double leftTime, double rightTime)
        {
            lock (DataLock)
            {
                int left;
                int right;
                double[] samples = m_ViewedData.InputSampleCopy();
                double[] time;
                m_InputData.CalculateSamplesPerSecond();

                if (m_ViewedData.UnixTime == null)
                {
                    time = DoubleVectorExtensions.Range(m_InputData.LeftmostUnixTime, samples.Length, 1.0 / m_InputData.SamplesPerSecond).ToArray();
                    left = (int)Math.Round((leftTime - m_InputData.LeftmostUnixTime) * m_InputData.SamplesPerSecond);
                    left = (left < 0) ? 0 : (left > samples.Length) ? samples.Length : left;
                    right = (int)Math.Round((rightTime - m_InputData.LeftmostUnixTime) * m_InputData.SamplesPerSecond);
                    right = (right < left) ? left : (right > samples.Length) ? samples.Length : right;
                }
                else
                {
                    time = m_ViewedData.UnixTime.Copy();
                    left = Array.BinarySearch(time, leftTime);
                    right = Array.BinarySearch(time, rightTime);
                    if (left < 0) left = ~left;
                    if (right < 0) right = ~right - 1;
                    if (left > 0) left--;
                    if (right < time.Length - 1) right++;
                }
                return (left, right, samples, time);
            }
        }

        internal Statistics ViewedSampleStatisticsBetweenUnixTimes(double leftTime, double rightTime)
        {
            lock (DataLock)
            {
                var indices = SnapshotYTProjection(leftTime, rightTime);
                return new TraceData.Statistics(
                    m_ViewedData.InputSampleCopy()[indices.leftIndex..indices.rightIndex],
                    m_ViewedData.UnixTime == null ? null : m_ViewedData.UnixTime[indices.leftIndex..(indices.rightIndex + 1)]);
            }
        }

        internal (double value, int index) ViewedSampleAtUnixTime(double time)
        {
            lock (DataLock)
            {
                var samples = m_ViewedData.InputSampleCopy();
                var unixTime = m_ViewedData.UnixTime;

                int index;
                double value;

                if (unixTime == null)
                {
                    m_ViewedData.CalculateSamplesPerSecond();
                    index = (int)Math.Round((time - m_ViewedData.LeftmostUnixTime) * m_ViewedData.SamplesPerSecond);
                    value = time >= m_ViewedData.LeftmostUnixTime && index < samples.Length ? samples[index] : 0.0;
                }
                else
                {
                    index = Array.BinarySearch(unixTime, time);
                    if (index < 0) index = ~index;
                    if (index > 0) index--;
                    value = samples[index];
                }
                return (value, index);
            }
        }

        internal double UnixTimeAtSample(int sampleNumber)
        {
            lock (DataLock)
            {
                var samples = m_ViewedData.InputSampleCopy();
                var unixTime = m_ViewedData.UnixTime;
                if (unixTime == null)
                {
                    m_ViewedData.CalculateSamplesPerSecond();
                    return ViewedLeftmostUnixTime + sampleNumber / m_ViewedData.SamplesPerSecond;
                }
                else
                {
                    return unixTime[sampleNumber];
                }
            }
        }

        internal class DataStore
        {
            public double[]? InputSampleCache;
            public double[]? InterpolatedSampleCache;
            public object InputSamples = new double[0];
            public double[]? UnixTime;
            [XmlSave]
            public double SamplesPerSecond;
            [XmlSave]
            public double LeftmostUnixTime;
            public List<TraceFeature> Features = new List<TraceFeature>();
            [XmlSave]
            public int SampleNumberDisplayOffset;

            public TimeRange UnixTimeRange =>
                UnixTime == null
                ? new TimeRange(LeftmostUnixTime, InputSampleCopy().Length / SamplesPerSecond)
                : new TimeRange(UnixTime[0], UnixTime.Last());

            public DataStore()
            {
                InputSamples = new double[0];
                Features = new List<TraceFeature>();
            }

            public double[] InputSampleCopy()
            {
                if (InputSampleCache != null) return InputSampleCache;
                InputSampleCache = CopyToDouble(InputSamples);
                return InputSampleCache;
            }

            public double[] InterpolatedCopy()
            {
                if (InterpolatedSampleCache != null) return InterpolatedSampleCache;
                InterpolatedSampleCache = InterpolateYT();
                return InterpolatedSampleCache;
            }

            public static double[] CopyToDouble(object input)
            {
                if (input == null) return new double[0];

                if (input is Array array)
                {
                    if (input is double[] doublearray) return doublearray.Copy();
                    else if (input is byte[] bytearray) return Array.ConvertAll(bytearray, x => (double)x);
                    else if (input is short[] shortarray) return Array.ConvertAll(shortarray, x => (double)x);
                    else if (input is ushort[] ushortarray) return Array.ConvertAll(ushortarray, x => (double)x);
                    else if (input is int[] intarray) return Array.ConvertAll(intarray, x => (double)x);
                    else if (input is uint[] uintarray) return Array.ConvertAll(uintarray, x => (double)x);
                    else if (input is float[] floatarray) return Array.ConvertAll(floatarray, x => (double)x);
                    else return new double[array.Length];
                }
                else if (input is List<double> doublelist) return doublelist.Select(x => (double)x).ToArray();
                else if (input is List<byte> bytelist) return bytelist.Select(x => (double)x).ToArray();
                else if (input is List<short> shortlist) return shortlist.Select(x => (double)x).ToArray();
                else if (input is List<ushort> ushortlist) return ushortlist.Select(x => (double)x).ToArray();
                else if (input is List<int> intlist) return intlist.Select(x => (double)x).ToArray();
                else if (input is List<uint> uintlist) return uintlist.Select(x => (double)x).ToArray();
                else if (input is List<float> floatlist) return floatlist.Select(x => (double)x).ToArray();
                else if (input is Ring<double> ring) return ring.AllSamples();
                else return new double[0];
            }

            public static int Count(object input)
            {
                if (input == null) return 0;
                else if (input is Array) return ((Array)input).Length;
                else if (input is Ring<double>) return ((Ring<double>)input).Length;
                else if (input is ICollection) return ((ICollection)input).Count;
                else return 0;
            }

            private double[] InterpolateYT()
            {
                double[] samples = CopyToDouble(InputSamples);
                if (UnixTime == null || samples.Length == 0 || samples.Length != UnixTime.Length)
                {
                    return samples;
                }
                LeftmostUnixTime = UnixTime[0];
                if (Count(InputSamples) == 1)
                {
                    return samples;
                }
                if (SamplesPerSecond == 0)
                {
                    CalculateSamplesPerSecond();
                }
                if (SamplesPerSecond == 0)
                {
                    return samples;
                }
                return Interpolate(UnixTime, samples);
            }

            internal void CalculateSamplesPerSecond()
            {
                if (SamplesPerSecond != 0) return;
                if (UnixTime == null) return;

                int length = UnixTime.Length;
                double delta = 1.0;
                for (int loop = 1; loop < length; loop++)
                {
                    double diff = UnixTime[loop] - UnixTime[loop - 1];
                    if (diff > 0.0)
                    {
                        delta = loop == 1 ? diff : Math.Min(delta, diff);
                    }
                }
                SamplesPerSecond = delta == 0.0 ? 0.0 : (1.0 / delta);
            }

            private double[] Interpolate(double[] unixTime, double[] samples)
            {
                int length = unixTime.Length;
                double min = unixTime[0];
                double max = unixTime.Last();
                var result = new double[(int)((max - min) * SamplesPerSecond + 1.0)];
                double leftSample = samples[0];
                double leftTime = unixTime[0];
                double rightSample = samples[1];
                double rightTime = unixTime[1];
                double lowValue = samples[0];
                double highValue = samples[0];
                double overlap = 0.25 / SamplesPerSecond;

                int index = 1;
                for (int loop = 0; loop < result.Length; loop++)
                {
                    double time = min + (double)loop / SamplesPerSecond;

                    lowValue = Math.Min(rightSample, lowValue);
                    highValue = Math.Max(rightSample, highValue);
                    if ((rightTime - time) < overlap && index != length - 1)
                    {
                        index++;
                        leftTime = rightTime;
                        leftSample = rightSample;
                        rightTime = unixTime[index];
                        rightSample = samples[index];
                        result[loop] = leftSample;
                    }
                    else
                    {
                        result[loop] = leftSample + (rightSample - leftSample) * (time - leftTime) / (rightTime - leftTime);
                    }
                }

                return result;
            }

            public DataStore DeepClone()
            {
                return new DataStore()
                {
                    InputSamples = CopyToDouble(InputSamples),
                    UnixTime = UnixTime == null ? null : UnixTime.Copy(),
                    SamplesPerSecond = SamplesPerSecond,
                    LeftmostUnixTime = LeftmostUnixTime,
                    Features = Features.Select(x => (TraceFeature)x.Clone()).ToList(),
                    SampleNumberDisplayOffset = SampleNumberDisplayOffset
                };
            }
        }

        internal class Statistics
        {
            [Flags]
            private enum SetFlags
            {
                Min = 0x01, Max = 0x02, Average = 0x04, StdDev = 0x08, Sum = 0x10, Count = 0x20, LastInput = 0x40, TimeStdDev = 0x80
            }

            private double m_Min;
            private double m_Max;
            private double m_Average;
            private double m_StdDev;
            private double m_Sum;
            private int m_Count;
            private double m_LastInput;
            private double m_TimeStdDev;
            private SetFlags m_Set;

            public double Min { get => m_Min; set { m_Min = value; m_Set |= SetFlags.Min; } }
            public double Max { get => m_Max; set { m_Max = value; m_Set |= SetFlags.Max; } }
            public double Average { get => m_Average; set { m_Average = value; m_Set |= SetFlags.Average; } }
            public double StdDev { get => m_StdDev; set { m_StdDev = value; m_Set |= SetFlags.StdDev; } }
            public double Sum { get => m_Sum; set { m_Sum = value; m_Set |= SetFlags.Sum; } }
            public int Count { get => m_Count; set { m_Count = value; m_Set |= SetFlags.Count; } }
            public double LastInput { get => m_LastInput; set { m_LastInput = value; m_Set |= SetFlags.LastInput; } }
            public double TimeStdDev { get => m_TimeStdDev; set { m_TimeStdDev = value; m_Set |= SetFlags.TimeStdDev; } }

            public IEnumerable<Tuple<string, double>> AsList()
            {
                if (m_Set.HasFlag(SetFlags.Min)) yield return new Tuple<string, double>("Min", Min);
                if (m_Set.HasFlag(SetFlags.Max)) yield return new Tuple<string, double>("Max", Max);
                if (m_Set.HasFlag(SetFlags.Max) && m_Set.HasFlag(SetFlags.Min)) yield return new Tuple<string, double>("Range", Max - Min);
                if (m_Set.HasFlag(SetFlags.Average)) yield return new Tuple<string, double>("Average", Average);
                if (m_Set.HasFlag(SetFlags.StdDev)) yield return new Tuple<string, double>("StdDev", StdDev);
                if (m_Set.HasFlag(SetFlags.Sum)) yield return new Tuple<string, double>("Sum", Sum);
                if (m_Set.HasFlag(SetFlags.Count)) yield return new Tuple<string, double>("Count", Count);
                if (m_Set.HasFlag(SetFlags.LastInput)) yield return new Tuple<string, double>("LastInput", LastInput);
                if (m_Set.HasFlag(SetFlags.TimeStdDev)) yield return new Tuple<string, double>("TimeStdDev", TimeStdDev);
            }

            public override string ToString()
            {
                return string.Join(",", AsList().Select(x => x.Item1 + "=" + x.Item2.ToStringRound(5, 3)));
            }

            public Statistics(double[] samples, double[]? unixTime = null)
            {
                var temp = new SehensWerte.Maths.Statistics(samples);
                Min = temp.Min;
                Max = temp.Max;
                Average = temp.Average;
                StdDev = temp.StdDev;
                Sum = temp.Sum;
                Count = temp.Count;
                LastInput = temp.LastInput;
                if (unixTime != null)
                {
                    temp = new Maths.Statistics(unixTime);
                    TimeStdDev = temp.StdDev;
                }
            }

            public Statistics()
            {
            }
        }

        public class TimeRange // xml serialised
        {
            public double Left = 0;
            public double Right = 0;

            public TimeRange() { }

            public TimeRange(double left, double right)
            {
                Left = left;
                Right = right;
            }

            public void Expand(TimeRange to)
            {
                if (to.Left < Left) Left = to.Left;
                if (to.Right > Right) Right = to.Right;
            }

            public override bool Equals(object? obj)
            {
                if (obj?.GetType() != GetType())
                {
                    return false;
                }
                TimeRange other = (TimeRange)obj;
                return this.Left == other.Left && this.Right == other.Right;
            }

            public override int GetHashCode()
            {
                return (Left.GetHashCode() ^ Right.GetHashCode());
            }
        }
    }
}
