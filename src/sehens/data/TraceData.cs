using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        // MINIPLAN (future): a genuinely NON-uniform horizontal axis (e.g. a nonlinear rpm-vs-speed curve) is
        // not representable affine; today the caller must pre-resample onto a uniform grid. A monotonic
        // per-sample axis is structurally the same as the YT (per-sample unix-time) axis - the intended
        // end-state is to make YT unit-agnostic (carry any unit, not just seconds) so it subsumes the
        // non-uniform case, leaving "affine + unit-agnostic YT" to cover everything with no separate
        // per-sample value array.
        
        // SamplesPerSecond takes precidence over Multiplier
        // The offset is always samples
        // Unit overrides the seconds default when set.
        //   sps > 0:  value = (sample + HorizontalOffset) / sps   (unit: HorizontalAxisUnit or "s")
        //   sps == 0: value = (sample + HorizontalOffset) * HorizontalMultiplier
        public double HorizontalOffset { get; private set; }
        public double HorizontalMultiplier { get; private set; } = 1.0;
        public string HorizontalAxisUnit { get; private set; } = "";

        private bool HorizontalAffineIsIdentity =>
            HorizontalOffset == 0.0 && HorizontalMultiplier == 1.0 && HorizontalAxisUnit.Length == 0;

        // True when the AFFINE map positions the samples: no sps (sps takes precedence for
        // positioning; the offset still shifts the seconds axis), a usable map, not the identity.
        public bool HasExplicitHorizontalAxis =>
            InputSamplesPerSecond == 0.0 && !HorizontalAffineInvalid && !HorizontalAffineIsIdentity;

        // The axis terms are unusable where they matter: a bad multiplier only matters without sps
        // (sps supplies the scale); a non-finite offset always poisons. The trace paints a
        // "(bad horizontal axis)" warning and falls back to sample numbers.
        public bool HorizontalAffineInvalid =>
            !double.IsFinite(HorizontalOffset)
            || (InputSamplesPerSecond == 0.0
                && !(HorizontalOffset == 0.0 && HorizontalMultiplier == 1.0 && HorizontalAxisUnit.Length == 0)
                && !(HorizontalMultiplier > 0.0 && double.IsFinite(HorizontalMultiplier)));

        public void SetHorizontalAffine(double offset, double multiplier, string unit = "")
        {
            lock (DataLock)
            {
                HorizontalOffset = offset;
                HorizontalMultiplier = multiplier;
                HorizontalAxisUnit = unit ?? "";
            }
            ForEachViewer(x => x.TraceDataSettingsChanged(this));
        }

        public void ClearHorizontalAxis()
        {
            lock (DataLock)
            {
                HorizontalOffset = 0.0;
                HorizontalMultiplier = 1.0;
                HorizontalAxisUnit = "";
            }
            ForEachViewer(x => x.TraceDataSettingsChanged(this));
        }

        // The unit the horizontal axis displays: the explicit unit, or "s" for a rate-based axis.
        public string HorizontalUnitEffective =>
            HorizontalAxisUnit.Length != 0 ? HorizontalAxisUnit
            : InputSamplesPerSecond != 0.0 ? "s"
            : "";

        // Canonical sample -> axis-value map (see the composition rules above the fields).
        public double HorizontalValueAt(double sampleNumber)
        {
            double sps = InputSamplesPerSecond;
            double offset = double.IsFinite(HorizontalOffset) ? HorizontalOffset : 0.0;
            if (sps != 0.0)
            {
                return (sampleNumber + offset) / sps;
            }
            else
            {
                return HasExplicitHorizontalAxis ? (sampleNumber + offset) * HorizontalMultiplier : sampleNumber;
            }
        }

        // Inverse of HorizontalValueAt, clamped to [0, count-1].
        public double SampleAtHorizontalValue(double value, int count)
        {
            if (count <= 1) return 0.0;
            double sps = InputSamplesPerSecond;
            double offset = double.IsFinite(HorizontalOffset) ? HorizontalOffset : 0.0;
            if (sps != 0.0)
            {
                return Math.Clamp(value * sps - offset, 0.0, count - 1);
            }
            else if (HasExplicitHorizontalAxis)
            {
                return Math.Clamp(value / HorizontalMultiplier - offset, 0.0, count - 1);
            }
            else
            {
                return Math.Clamp(value, 0.0, count - 1);
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
                throw new AggregateException(exceptions);
            }
            if (exceptions.Count == 1)
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
                throw new AggregateException(exceptions);
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
                var array = m_ViewedData.InputSamples.CopyToDoubleArray();
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

        internal TraceView.SnapshotYT SnapshotYTProjection(double leftTime, double rightTime)
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
                return new TraceView.SnapshotYT(left, right, samples, time);
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

        internal (double value, int index, double time) ViewedSampleAtUnixTime(double time)
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
                    time = (index * m_ViewedData.SamplesPerSecond) + m_ViewedData.LeftmostUnixTime;
                }
                else
                {
                    index = Array.BinarySearch(unixTime, time);
                    if (index < 0) index = ~index;
                    if (index > 0) index--;
                    value = samples[index];
                    time = unixTime[index];
                }
                return (value, index, time);
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
                InputSampleCache = InputSamples.CopyToDoubleArray();
                return InputSampleCache;
            }

            public double[] InterpolatedCopy()
            {
                if (InterpolatedSampleCache != null) return InterpolatedSampleCache;
                InterpolatedSampleCache = InterpolateYT();
                return InterpolatedSampleCache;
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
                double[] samples = InputSamples.CopyToDoubleArray();
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
                    InputSamples = InputSamples.CopyToDoubleArray(),
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
                return string.Join(",", AsList().Select(x => x.Item1 + "=" + x.Item2.ToStringRound(5, 3, trimRight: false)));
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

    [TestClass]
    public class HorizontalAffineTests
    {
        [TestMethod]
        public void AffineValueAndInverse()
        {
            var td = new TraceData("t");
            td.SetHorizontalAffine(offset: 5.0, multiplier: 2.0, unit: "rpm"); // offset is in samples
            Assert.IsTrue(td.HasExplicitHorizontalAxis);
            Assert.AreEqual("rpm", td.HorizontalAxisUnit);
            Assert.AreEqual(10.0, td.HorizontalValueAt(0), 1e-9); // 2 * (0 + 5)
            Assert.AreEqual(16.0, td.HorizontalValueAt(3), 1e-9);
            Assert.AreEqual(20.0, td.HorizontalValueAt(5), 1e-9);
            // inverse round-trips within the sample range
            Assert.AreEqual(3.0, td.SampleAtHorizontalValue(16.0, count: 10), 1e-9);
            Assert.AreEqual(0.0, td.SampleAtHorizontalValue(10.0, count: 10), 1e-9);
        }

        [TestMethod]
        public void AffineInverseClampsToRange()
        {
            var td = new TraceData("t");
            td.SetHorizontalAffine(0.0, 1.0, "s");
            Assert.AreEqual(0.0, td.SampleAtHorizontalValue(-100.0, count: 8), 1e-9); // below -> 0
            Assert.AreEqual(7.0, td.SampleAtHorizontalValue(1000.0, count: 8), 1e-9); // above -> count-1
            Assert.AreEqual(0.0, td.SampleAtHorizontalValue(42.0, count: 1), 1e-9);   // degenerate count
        }

        [TestMethod]
        public void NoExplicitAxisIsSampleNumber()
        {
            var td = new TraceData("t");
            Assert.IsFalse(td.HasExplicitHorizontalAxis);
            Assert.AreEqual(4.0, td.HorizontalValueAt(4), 1e-9);              // identity
            Assert.AreEqual(4.0, td.SampleAtHorizontalValue(4.0, 10), 1e-9);  // identity, in range
            Assert.AreEqual(9.0, td.SampleAtHorizontalValue(42.0, 10), 1e-9); // clamps to count-1
        }

        [TestMethod]
        public void SpsWinsTheScaleOffsetComposes()
        {
            // sps > 0: value = (sample + offset)/sps - the multiplier cannot compose with a rate
            // and is ignored; the offset is always in samples so it means the same thing under
            // either scale; the unit overrides the "s" default.
            var td = new TraceData("t");
            td.Update(new double[100]);
            td.SetHorizontalAffine(1000.0, 7.0, "f");
            td.InputSamplesPerSecond = 10.0;
            Assert.IsFalse(td.HorizontalAffineInvalid); // multiplier is ignored, not an error
            Assert.IsFalse(td.HasExplicitHorizontalAxis); // sps positions; affine does not
            Assert.AreEqual(100.3, td.HorizontalValueAt(3), 1e-9); // (3 + 1000) / 10, multiplier unused
            Assert.AreEqual(5.0, td.SampleAtHorizontalValue(100.5, 100), 1e-9);
            Assert.AreEqual("f", td.HorizontalUnitEffective); // explicit unit beats the "s" default

            td.InputSamplesPerSecond = 0.0; // rate removed: the affine scale takes over
            Assert.IsTrue(td.HasExplicitHorizontalAxis);
            Assert.AreEqual(7021.0, td.HorizontalValueAt(3), 1e-9); // 7 * (3 + 1000)
        }

        [TestMethod]
        public void IdentityAffineIsNoAxisAndUnitAloneIsExplicit()
        {
            var td = new TraceData("t");
            td.SetHorizontalAffine(0.0, 1.0, ""); // identity == the plain sample-number axis
            Assert.IsFalse(td.HasExplicitHorizontalAxis);
            Assert.AreEqual(4.0, td.HorizontalValueAt(4), 1e-9);

            td.SetHorizontalAffine(0.0, 1.0, "km/h"); // a bare unit labels the sample axis (RideTime)
            Assert.IsTrue(td.HasExplicitHorizontalAxis);
            Assert.AreEqual(4.0, td.HorizontalValueAt(4), 1e-9);
            Assert.AreEqual("km/h", td.HorizontalUnitEffective);
        }

        [TestMethod]
        public void InvalidMultiplierFlagsErrorAndFallsBack()
        {
            var td = new TraceData("t");
            td.SetHorizontalAffine(5.0, 0.0, "rpm"); // zero multiplier: invalid, stored as given
            Assert.IsTrue(td.HorizontalAffineInvalid);
            Assert.IsFalse(td.HasExplicitHorizontalAxis);
            Assert.AreEqual(0.0, td.HorizontalMultiplier, 1e-9); // no silent =1 coercion
            Assert.AreEqual(7.0, td.HorizontalValueAt(7), 1e-9); // sample-number fallback
            Assert.AreEqual(4.0, td.SampleAtHorizontalValue(4.0, 10), 1e-9);

            td.SetHorizontalAffine(5.0, -3.0, "rpm"); // negative multiplier
            Assert.IsTrue(td.HorizontalAffineInvalid);
            Assert.AreEqual(-3.0, td.HorizontalMultiplier, 1e-9);

            td.SetHorizontalAffine(double.NaN, 2.0, "rpm"); // non-finite offset
            Assert.IsTrue(td.HorizontalAffineInvalid);

            td.SetHorizontalAffine(0.0, 2.0, "rpm"); // valid map clears the error
            Assert.IsFalse(td.HorizontalAffineInvalid);
            Assert.IsTrue(td.HasExplicitHorizontalAxis);
        }

        [TestMethod]
        public void ClearRevertsToImplicit()
        {
            var td = new TraceData("t");
            td.SetHorizontalAffine(10.0, 3.0, "kph");
            Assert.IsTrue(td.HasExplicitHorizontalAxis);
            td.ClearHorizontalAxis();
            Assert.IsFalse(td.HasExplicitHorizontalAxis);
            Assert.AreEqual("", td.HorizontalAxisUnit);
            Assert.AreEqual(6.0, td.HorizontalValueAt(6), 1e-9); // back to sample number
        }
    }
}
