using SehensWerte.Files;
using SehensWerte.Filters;
using SehensWerte.Maths;
using SehensWerte.Utils;
using System;
using System.Text;
using System.Xml.Serialization;

namespace SehensWerte.Controls.Sehens
{
    public partial class TraceView : ITraceView, IDisposable
    {
        [AutoEditor.Hidden]
        public SehensControl Scope;

        [AutoEditor.Hidden]
        public IPaintTrace Painter = new Paint2dTrace();

        [AutoEditor.Hidden]
        public PaintedInfo Painted = new PaintedInfo();
        internal List<TraceView> Group = new List<TraceView>(); //needs lock - get using Scope.GroupedTraces(this)

        public event Action<TraceView>? GuiUpdateControls;

        private TraceData.Statistics? m_CachedStatistics;
        private TraceDataPeakHold? m_PeakHoldAll;
        private TraceDataPeakHold? m_PeakHoldDrawn;

        internal int CalculateOrder;
        internal bool m_AfterZoomCalculateRequired = true;
        internal bool m_BeforeZoomCalculateRequired = true;
        internal double DrawnValueHighest = double.PositiveInfinity;
        internal double DrawnValueLowest = double.NegativeInfinity;

        private int m_DrawnStartPosition;
        private int m_FftInputBins;
        private int m_FftResultBins;
        private int m_RecalculateProjectionRequired = 1;

        private double[]? m_DrawnSamples;
        private double[]? m_CalculatedBeforeZoom;
        private double[]? m_RawBeforeZoom;
        private double[]? m_TriggerSamples;
        private Fftw? m_Fft;
        private MouseInfo[] Clicks = new MouseInfo[5];

        [AutoEditor.Hidden]
        internal double[]? RawBeforeZoom { get { lock (m_Samples.DataLock) { return m_RawBeforeZoom; } } }
        [AutoEditor.Hidden]
        internal double[]? CalculatedBeforeZoom { get { lock (m_Samples.DataLock) { return m_CalculatedBeforeZoom; } } }
        [AutoEditor.Hidden]
        public double[]? DrawnSamples { get { lock (m_Samples.DataLock) { return m_DrawnSamples; } } } //fixme: calculate if not on screen (see DrawnSamplesYT?)
        [AutoEditor.Hidden]
        public SnapshotYT? DrawnSamplesYT
        {
            get
            {
                var extents = DrawnExtents();
                return m_Samples.SnapshotYTProjection(extents.leftUnixTime, extents.rightUnixTime);
            }
        }
        [AutoEditor.Hidden]
        internal double[]? PeakHoldMinDrawn { get { lock (m_Samples.DataLock) { return m_PeakHoldDrawn?.Min; } } }
        [AutoEditor.Hidden]
        internal double[]? PeakHoldMaxDrawn { get { lock (m_Samples.DataLock) { return m_PeakHoldDrawn?.Max; } } }
        [AutoEditor.Hidden]
        internal double[]? PeakHoldMinAll { get { lock (m_Samples.DataLock) { return m_PeakHoldAll?.Min; } } }
        [AutoEditor.Hidden]
        internal double[]? PeakHoldMaxAll { get { lock (m_Samples.DataLock) { return m_PeakHoldAll?.Max; } } }

        public record struct SnapshotYT(int leftIndex, int rightIndex, double[] samples, double[] time)
        {
        }

        [AutoEditor.Hidden]
        public int DrawnStartPosition => m_DrawnStartPosition;

        internal int ViewOriginalSampleCount;

        [AutoEditor.Hidden]
        public bool IsViewer => true;

        public TraceView(SehensControl scope, TraceData samples, string viewName)
        {
            Scope = scope;
            ViewName = viewName;
            m_Samples = samples;
            m_Samples.AddViewer(this);
            Scope.AddView(this);
            Scope.ViewNeedsRepaint(this);
        }

        public TraceView(SehensControl scope, TraceView trace)
        {
            Scope = scope;
            ViewName = trace.m_Samples.Name;
            m_Samples = trace.m_Samples;
            TriggerView = trace.TriggerView;
            m_Samples.AddViewer(this);
            Scope.AddView(this);
            Scope.ViewNeedsRepaint(this);
        }

        ////////////////////////////////////////////////////////////////
        //Properties

        private TraceData m_Samples;
        [AutoEditor.Hidden]
        public TraceData Samples
        {
            get => m_Samples;
            set
            {
                if (m_Samples == value) return;
                m_Samples.RemoveViewer(this);
                m_Samples = value;
                m_Samples.AddViewer(this);
                Scope.AddView(this);
                Scope.ViewNeedsRepaint(this);
            }
        }

        private string m_ViewName = ""; //serialsied by SehensSave
        public string ViewName
        {
            get => m_ViewName;
            set
            {
                if (m_ViewName == value) return;
                Scope.RenameView(this, value, ref m_ViewName);
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
                Scope.GroupWithViewChanged(this);
            }
        }

        private Color m_Colour = Color.Black;
        [XmlSave]
        public Color Colour
        {
            get => m_Colour;
            set
            {
                if (m_Colour == value) return;
                m_Colour = value;
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private string m_GroupWithView = "";
        [XmlSave]
        [AutoEditor.DisplayName("Group With Trace")] //fixme: AutoEditorForm.Values
        public string GroupWithView
        {
            get => m_GroupWithView;
            set
            {
                if (m_GroupWithView == value) return;
                m_GroupWithView = value;
                Scope.GroupWithViewChanged(this);
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        public double SelectTime;// HighResTimer.StaticSeconds;

        private bool m_Selected;
        [AutoEditor.Hidden]
        [XmlSave]
        public bool Selected
        {
            get => m_Selected;
            set
            {
                if (m_Selected == value) return;
                if (value)
                {
                    SelectTime = HighResTimer.StaticSeconds;
                }
                m_Selected = value;
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        public enum PaintModes
        {
            PolygonDigital,
            PolygonContinuous,
            Min,
            Max,
            Average,
            PeakHold,
            Points,
            PointsIfChanged,
            XYLine,
            XYPoints,
            XYCurve,
            XYZProjection,
            FFT2D,
        }
        private PaintModes m_PaintMode = PaintModes.PolygonDigital;
        [XmlSave]
        [AutoEditor.DisplayName("Paint Mode")]
        public PaintModes PaintMode
        {
            get => m_PaintMode;
            set
            {
                if (m_PaintMode == value) return;
                lock (Samples.DataLock)
                {
                    m_PaintMode = value;
                    ClearPeakHold();
                    switch (m_PaintMode)
                    {
                        case PaintModes.XYLine: Painter = new PaintXYTrace(PaintXYTrace.DrawModes.Line); break;
                        case PaintModes.XYPoints: Painter = new PaintXYTrace(PaintXYTrace.DrawModes.Dot); break;
                        case PaintModes.XYCurve: Painter = new PaintXYTrace(PaintXYTrace.DrawModes.Curve); break;
                        case PaintModes.XYZProjection: Painter = new PaintXYZTrace(PaintXYZTrace.DrawModes.RectangularLine); break;
                        case PaintModes.FFT2D: Painter = new Paint2dFFTTrace(); break;
                        case PaintModes.Min:
                        case PaintModes.Max:
                        case PaintModes.Average:
                        case PaintModes.PolygonDigital:
                        case PaintModes.PolygonContinuous:
                        case PaintModes.PeakHold:
                        case PaintModes.Points:
                        case PaintModes.PointsIfChanged: Painter = new Paint2dTrace(); break;
                    }
                    RecalculateProjectionRequired();
                    Scope.ViewNeedsRepaint(this);
                    GuiUpdateControls?.Invoke(this);
                }
            }
        }

        private string m_TraceFilter = "None";
        [XmlSave]
        public string TraceFilter
        {
            get => m_TraceFilter;
            set
            {
                if (m_TraceFilter == value) return;
                m_TraceFilter = value;
                ClearPeakHold();
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private double m_ZoomValue = 1.0;
        [XmlSave]
        [AutoEditor.Hidden]
        public double ZoomValue
        {
            get => m_ZoomValue;
            set
            {
                if (m_ZoomValue != value && !m_HoldPanZoom)
                {
                    m_ZoomValue = value;
                    ZoomPanChanged();
                }
            }
        }

        private double m_PanValue = 0.0;
        [XmlSave]
        [AutoEditor.Hidden]
        public double PanValue
        {
            get => m_PanValue;
            set
            {
                if (m_PanValue != value && !m_HoldPanZoom)
                {
                    m_PanValue = value;
                    ZoomPanChanged();
                }
            }
        }

        private bool m_HoldPanZoom = false;
        [XmlSave]
        public bool HoldPanZoom
        {
            get => m_HoldPanZoom;
            set
            {
                if (m_HoldPanZoom == value) return;
                m_HoldPanZoom = value;
                ZoomPanChanged();
                Scope?.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private int m_LineWidth = 0; // 0 = use from skin
        [XmlSave]
        public int LineWidth
        {
            get => m_LineWidth;
            set
            {
                m_LineWidth = value;
                Scope?.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private bool m_AutoReduceRange;
        [XmlSave]
        public bool AutoReduceRange
        {
            get => m_AutoReduceRange;
            set
            {
                if (m_AutoReduceRange == value) return;
                m_AutoReduceRange = value;
                RecalculateProjectionRequired();
                Scope?.ViewNeedsRepaint(this);
                this.GuiUpdateControls?.Invoke(this);
            }
        }


        private double m_HighestValue = 1.0;
        [XmlSave]
        [AutoEditor.DisplayOrder(-2)]
        [AutoEditor.DisplayName("Highest Value")]
        public double HighestValue
        {
            get => m_HighestValue;
            set
            {
                if (m_HighestValue == value) return;
                SetGroupHighLow(value, m_LowestValue);
            }
        }

        private double m_LowestValue = 0;
        [XmlSave]
        [AutoEditor.DisplayOrder(-2)]
        [AutoEditor.DisplayName("Lowest Value")]
        public double LowestValue
        {
            get => m_LowestValue;
            set
            {
                if (m_LowestValue == value) return;
                SetGroupHighLow(m_HighestValue, value);
            }
        }


        private double m_TraceHeightFactor = 1;
        [XmlSave]
        public double HeightFactor
        {
            get => m_TraceHeightFactor;
            set
            {
                if (m_TraceHeightFactor == value) return;
                m_TraceHeightFactor = value;
                Scope.VisibleViews.ForEach(x => x.RecalculateProjectionRequired());
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private bool m_PadLeftWithFirstValue;
        [XmlSave]
        [AutoEditor.DisplayName("Pad Left With First Value")]
        public bool PadLeftWithFirstValue
        {
            get => m_PadLeftWithFirstValue;
            set
            {
                if (m_PadLeftWithFirstValue == value) return;
                m_PadLeftWithFirstValue = value;
                Scope.ViewNeedsRepaint(this);
            }
        }

        private bool m_PadRightWithLastValue;
        [XmlSave]
        [AutoEditor.DisplayName("Pad Right With Last Value")]
        public bool PadRightWithLastValue
        {
            get
            {
                return m_PadRightWithLastValue;
            }
            set
            {
                if (m_PadRightWithLastValue == value) return;
                m_PadRightWithLastValue = value;
                Scope.ViewNeedsRepaint(this);
            }
        }

        private bool m_Visible = true;
        [XmlSave]
        [AutoEditor.Hidden]
        public bool Visible
        {
            get => m_Visible;
            set
            {
                if (m_Visible == value) return;
                m_Visible = value;
                Scope.ViewVisibleChanged(this);
            }
        }

        private bool m_LogVertical;
        [XmlSave]
        [AutoEditor.DisplayName("Log vertical axis")]
        public bool LogVertical
        {
            get => m_LogVertical;
            set
            {
                if (m_LogVertical == value) return;
                m_LogVertical = value;
                RecalculateProjectionRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private bool m_ShowPictureInPicture;
        [XmlSave]
        public bool ShowPictureInPicture
        {
            get => m_ShowPictureInPicture;
            set
            {
                if (m_ShowPictureInPicture == value) return;
                m_ShowPictureInPicture = value;
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private TraceView? m_TriggerTrace;
        //fixme [AutoEditor.Values(m_Display.)]
        [AutoEditor.DisplayName("Trigger Trace")]
        public TraceView? TriggerView
        {
            get => m_TriggerTrace;
            set
            {
                if (m_TriggerTrace == value) return;
                m_TriggerTrace = value;
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private double m_TriggerValue;
        [XmlSave]
        [AutoEditor.DisplayName("Trigger Value")]
        public double TriggerValue
        {
            get => m_TriggerValue;
            set
            {
                if (m_TriggerValue == value) return;
                m_TriggerValue = value;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        public enum TriggerModes
        {
            None,
            RisingAuto,
            FallingAuto,
            Rising,
            Falling
        }
        private TriggerModes m_TriggerMode;
        [XmlSave]
        [AutoEditor.DisplayName("Trigger Mode")]
        public TriggerModes TriggerMode
        {
            get => m_TriggerMode;
            set
            {
                if (m_TriggerMode == value) return;
                m_TriggerMode = value;
                AfterZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private bool m_ViewOverrideEnabled;
        [XmlSave]
        [AutoEditor.Hidden]
        public bool ViewOverrideEnabled
        {
            get => m_ViewOverrideEnabled;
            set
            {
                if (m_ViewOverrideEnabled == value) return;
                m_ViewOverrideEnabled = value;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private int m_ViewLengthOverride;
        [XmlSave]
        [AutoEditor.DisplayName("View Length")]
        public int ViewLengthOverride
        {
            get => m_ViewLengthOverride;
            set
            {
                if (m_ViewLengthOverride == value) return;
                m_ViewLengthOverride = (value >= 0) ? value : 0;
                m_ViewOverrideEnabled = m_ViewLengthOverride != 0 || m_ViewOffsetOverride != 0;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private int m_ViewOffsetOverride;
        [XmlSave]
        [AutoEditor.DisplayName("View Offset")]
        public int ViewOffsetOverride
        {
            get => m_ViewOffsetOverride;
            set
            {
                if (m_ViewOffsetOverride == value) return;
                m_ViewOffsetOverride = value;
                m_ViewOverrideEnabled = m_ViewLengthOverride != 0 || m_ViewOffsetOverride != 0;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        bool m_OverrideSamplesUnixTime = false;
        [AutoEditor.Hidden]
        public bool OverrideSamplesUnixTime => m_OverrideSamplesUnixTime;

        private TraceData.TimeRange m_UnixTimeRange = new TraceData.TimeRange(0.0, 0.0);

        [XmlSave(nestedXml: true)]
        [AutoEditor.Hidden]
        public TraceData.TimeRange UnixTimeRange
        {
            get => m_UnixTimeRange;
            set
            {
                if (value.Equals(m_UnixTimeRange)) return;
                m_OverrideSamplesUnixTime = true;
                m_UnixTimeRange = value;
                UnixTimesChanged();
            }
        }

        double UnixTimeRangeLeft { get => m_UnixTimeRange.Left; set { UnixTimeRange = new TraceData.TimeRange(value, m_UnixTimeRange.Right); } }
        double UnixTimeRangeRight { get => m_UnixTimeRange.Right; set { UnixTimeRange = new TraceData.TimeRange(m_UnixTimeRange.Right, value); } }


        [AutoEditor.Hidden]
        public TraceData.TimeRange DrawnUnixTimeRange
        {
            get
            {
                if (Painted.UnixTimes == null)
                {
                    return Painted.UnixTimes = GetGroupUnixTimeRange(Painted.Group);
                }
                else
                {
                    return Painted.UnixTimes;
                }
            }
        }

        [AutoEditor.Hidden]
        public TraceData.TimeRange GroupUnixTimeRange => GetGroupUnixTimeRange(Group);

        internal static TraceData.TimeRange GetGroupUnixTimeRange(IEnumerable<TraceView> group)
        {
            TraceData.TimeRange range = new TraceData.TimeRange(0.0, 0.0);

            bool first = true;
            void expand(TraceData.TimeRange traceTime)
            {
                if (first)
                {
                    range = traceTime;
                    first = false;
                }
                else
                {
                    range.Expand(traceTime);
                }
            }

            group.ForEach(item =>
            {
                lock (item.Samples.DataLock)
                {
                    if (item.CanShowRealYT)
                    {
                        TraceData.TimeRange traceTime = item.OverrideSamplesUnixTime ? item.m_UnixTimeRange : item.Samples.UnixTimeRange;
                        expand(traceTime);
                    }
                    if (item.CanShowFakeYT)
                    {
                        double sps = item.Samples.InputSamplesPerSecond;
                        int num = item.m_ViewOverrideEnabled ? item.m_ViewOffsetOverride : 0;
                        double left = item.Samples.ViewedLeftmostUnixTime + (double)num / sps;
                        int num3 = ((item.m_ViewOverrideEnabled && item.m_ViewLengthOverride != 0) ? item.m_ViewLengthOverride : item.Samples.InputSampleCount);
                        double right = left + (double)(num3 - 1) / sps;
                        expand(new TraceData.TimeRange(left, right));
                    }
                }
            });

            if (first || range.Left == range.Right)
            {
                range.Left -= 1.0;
                range.Right += 1.0;
            }

            return range;
        }

        private void UnixTimesChanged()
        {
            Scope.GroupedTraces(this).ForEach(x => x.RecalculateProjectionRequired());
            Scope.ViewNeedsRepaint(this);
            GuiUpdateControls?.Invoke(this);
        }


        private int m_PreTriggerSampleCount;
        [XmlSave]
        [AutoEditor.DisplayName("PreTrigger Sample Count")]
        public int PreTriggerSampleCount
        {
            get => m_PreTriggerSampleCount;
            set
            {
                if (m_PreTriggerSampleCount == value) return;
                m_PreTriggerSampleCount = value;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }


        private double m_FftBandpassHPF6dB;
        [XmlSave]
        public double FftBandpassHPF6dB
        {
            get => m_FftBandpassHPF6dB;
            set
            {
                if (m_FftBandpassHPF6dB == value) return;
                m_FftBandpassHPF6dB = value;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private double m_FftBandpassHPF3dB;
        [XmlSave]
        public double FftBandpassHPF3dB
        {
            get => m_FftBandpassHPF3dB;
            set
            {
                if (m_FftBandpassHPF3dB == value) return;
                m_FftBandpassHPF3dB = value;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private double m_FftBandpassLPF3dB;
        [XmlSave]
        public double FftBandpassLPF3dB
        {
            get => m_FftBandpassLPF3dB;
            set
            {
                if (m_FftBandpassLPF3dB == value) return;
                m_FftBandpassLPF3dB = value;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private double m_FftBandpassLPF6dB;
        [XmlSave]
        public double FftBandpassLPF6dB
        {
            get => m_FftBandpassLPF6dB;
            set
            {
                if (m_FftBandpassLPF6dB == value) return;
                m_FftBandpassLPF6dB = value;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        public enum FftFilterTypes
        {
            None,
            BandPassFit,
            BandPass,
            LowPass,
            LowPass3dBPerOctave,
            HighPass,
            HighPass3dBPerOctave,
            Notch,
            NotchFit
        }
        private FftFilterTypes m_FftFilterType;
        [XmlSave]
        public FftFilterTypes FftFilterType
        {
            get => m_FftFilterType;
            set
            {
                if (m_FftFilterType == value) return;
                m_FftFilterType = value;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private SampleWindow.WindowType m_FftBandpassWindow = SampleWindow.WindowType.Rectangular;
        [XmlSave]
        public SampleWindow.WindowType FftBandpassWindow
        {
            get => m_FftBandpassWindow;
            set
            {
                if (m_FftBandpassWindow == value) return;
                m_FftBandpassWindow = value;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private SampleWindow.WindowType m_FftWindow = SampleWindow.WindowType.Rectangular;
        [XmlSave]
        [AutoEditor.DisplayOrder(-3)]
        [AutoEditor.DisplayName("FFT Display Window")]
        public SampleWindow.WindowType FftWindow
        {
            get => m_FftWindow;
            set
            {
                if (m_FftWindow == value) return;
                m_FftWindow = value;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        public enum CalculatePhases
        {
            BeforeZoom,
            AfterZoom
        }
        private CalculatePhases m_CalculatePhase = CalculatePhases.AfterZoom;
        [XmlSave]
        public CalculatePhases MathPhase
        {
            get => m_CalculatePhase;
            set
            {
                if (m_CalculatePhase == value) return;
                m_CalculatePhase = value;
                ClearPeakHold();
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        public enum CalculatedTypes
        {
            None,
            PythonScript,
            Magnitude,
            Atan2,
            Difference,
            Product,
            ProductSimple,
            SubtractOffset,
            FIR,
            Rescale,
            Normalised,
            Differentiate,
            Integrate,
            Quantize,
            RollingRMS,
            RollingMean,
            ProjectYTtoY,
            RescaledError,
            NormalisedError,
            Resample,
            Abs,
            Sum,
            Subtract,
            Mean,
            PolyFilter
        }

        public class CalculatedTraceData // XML Serialised
        {
        }

        public class CalculatedTraceDataOneDouble : CalculatedTraceData // XML Serialised
        {
            [AutoEditor.DisplayName("Value")]
            public double Param = 1.0;
        }

        public class CalculatedTraceDataQuantise : CalculatedTraceData // XML Serialised
        {
            [AutoEditor.DisplayName("Offset")]
            public double Offset = 1.0;

            [AutoEditor.DisplayName("Scale")]
            public double Scale = 32767.0;
        }

        public class CalculatedTraceDataWindow : CalculatedTraceData // XML Serialised
        {
            [AutoEditor.DisplayName("Window")]
            public int Window = 100;
        }

        public class CalculatedTraceDataMinMax : CalculatedTraceData // XML Serialised
        {
            [AutoEditor.DisplayName("Minimum Value")]
            public int Min = 0;

            [AutoEditor.DisplayName("Maximum Value")]
            public int Max = 1;
        }

        public class CalculatedTraceDataCount : CalculatedTraceData // XML Serialised
        {
            [AutoEditor.DisplayName("Count")]
            public int Count = 100;
        }

        public class CalculatedTraceDataOrder : CalculatedTraceData // XML Serialised
        {
            [AutoEditor.DisplayName("Order")]
            public int Order = 5;
        }

        [XmlSave]
        public CalculatedTypes CalculateType;

        [XmlSave(nestedXml: true, nestedDerivedTypes: new Type[]
        {
            typeof(CalculatedTraceDataOneDouble),
            typeof(CalculatedTraceDataQuantise),
            typeof(CalculatedTraceDataWindow),
            typeof(CalculatedTraceDataMinMax),
            typeof(CalculatedTraceDataCount)
        })]
        public CalculatedTraceData CalculatedParameter = new CalculatedTraceData();
        public List<TraceView> CalculatedSourceViews = new List<TraceView>();        //serialised by ScopeSave

        public enum MathTypes
        {
            Normal,
            FFTMagnitude,
            FFTPhase,
            FFT10Log10,
            FFT20Log10
        }
        private MathTypes m_MathType;
        [XmlSave]
        [AutoEditor.DisplayOrder(-3)]
        [AutoEditor.DisplayName("Math Type")]
        public MathTypes MathType
        {
            get => m_MathType;
            set
            {
                if (m_MathType == value) return;
                m_MathType = value;
                ClearPeakHold();
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        public enum FilterTransforms
        {
            None,
            DifferentiateIntegrate
        }
        private FilterTransforms m_FilterTransform;

        [XmlSave]
        public FilterTransforms FilterTransform
        {
            get => m_FilterTransform;
            set
            {
                if (m_FilterTransform == value) return;
                m_FilterTransform = value;
                BeforeZoomCalculateRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        ////////////////////////////////////////////////////////////////
        //paint helpers

        public class PaintedInfo
        {
            public int TraceIndex; // index of this trace within the group
            public int GroupIndex; // index number of this group
            public int GroupCount = 1; // number of groups

            public List<TraceView> Group = new List<TraceView>();
            public List<TraceViewClickZone> ClickZones = new List<TraceViewClickZone>();

            public double HeightAdjustSumTop;
            public double HeightAdjustSumBottom;
            public double HeightAdjustSum;

            public TraceData.TimeRange? UnixTimes;
        }

        public class MouseInfo
        {
            public double SampleAtX;
            public double UnixTimeAtX;
            public int IndexAfterTrim;
            public int IndexBeforeTrim;
            public int CountAfterTrim;
            public int CountBeforeTrim;
            public string ExtraInfo = "";
            public double YRatio;
            public double XRatio;
            public double YValue;
            public double XValue;


            public MouseInfo ShallowClone() { return (MouseInfo)MemberwiseClone(); }
        }

        internal bool IsFftTrace => m_MathType == MathTypes.FFTMagnitude || m_MathType == MathTypes.FFTPhase || m_MathType == MathTypes.FFT10Log10 || m_MathType == MathTypes.FFT20Log10;

        internal string TraceHoverStatistics(MouseInfo clickInfo)
        {
            return clickInfo.YRatio is >= 0.0 and <= 1.0 ? Painter.GetHoverStatistics(this, clickInfo) : "";
        }

        internal bool UseFftFilter => m_Samples.InputSamplesPerSecond != 0.0 && m_FftFilterType != 0
                        && (m_FftBandpassHPF6dB != 0.0 || m_FftBandpassHPF3dB != 0.0 || m_FftBandpassLPF3dB != 0.0 || m_FftBandpassLPF6dB != 0.0);
        internal bool IsLogarithmicY => m_MathType == MathTypes.FFT10Log10 || m_MathType == MathTypes.FFT20Log10;
        internal bool IsRebasedResult => IsFftTrace && CalculateAfterZoom;
        internal bool IsRecalculateProjectionRequired => m_RecalculateProjectionRequired != 0 || m_AfterZoomCalculateRequired;
        internal bool ProcessAtInput => m_PaintMode == PaintModes.PeakHold;
        internal bool ProcessAtDisplay => m_PaintMode != PaintModes.PeakHold;
        internal bool CanShowRealYT => CanShowYTInner ? Samples.ViewedUnixTime != null : false;
        internal bool CanShowFakeYT => CanShowYTInner ? (Samples.ViewedSamplesPerSecond == 0.0 ? false : Samples.ViewedLeftmostUnixTime != 0.0) : false;
        private bool CalculateAfterZoom => m_MathType != 0 && m_CalculatePhase == CalculatePhases.AfterZoom;
        private bool CalculateBeforeZoom => m_MathType != 0 && m_CalculatePhase == CalculatePhases.BeforeZoom;

        private bool CanShowYTInner
        {
            get
            {
                if (MathType != 0) return false;
                if (Samples.ViewedSampleCount == 0) return false;
                if (TraceFilter != "None") return false;
                if (FilterTransform != 0) return false;
                if (UseFftFilter) return false;
                if (TriggerMode != 0) return false;
                if (PaintMode != PaintModes.Points && PaintMode != PaintModes.PolygonDigital) return false;
                return true;
            }
        }

        ////////////////////////////////////////////////////////////////
        //edit form helpers
        [AutoEditor.DisplayName("Sample Name")]
        public string SamplesName
        {
            get => Samples.Name;
            set { Samples.Name = value; }
        }

        [AutoEditor.DisplayOrder(-1)]
        [AutoEditor.DisplayName("First Sample UnixTime")]
        public double SamplesLeftmostUnixTime
        {
            get => Samples.InputLeftmostUnixTime;
            set { Samples.InputLeftmostUnixTime = value; }
        }

        [AutoEditor.DisplayName("Samples Per Second")]
        public double SamplesPerSecond
        {
            get => Samples.InputSamplesPerSecond;
            set { Samples.InputSamplesPerSecond = value; }
        }

        [AutoEditor.DisplayName("Vertical Unit")]
        public string SamplesVerticalUnit
        {
            get => Samples.VerticalUnit;
            set { Samples.VerticalUnit = value; }
        }

        [AutoEditor.DisplayName("Axis Title Left")]
        public string SamplesAxisTitleLeft
        {
            get => Samples.AxisTitleLeft;
            set { Samples.AxisTitleLeft = value; }
        }

        [AutoEditor.DisplayName("Axis Title Bottom")]
        public string SamplesAxisTitleBottom
        {
            get => Samples.AxisTitleBottom;
            set { Samples.AxisTitleBottom = value; }
        }

        [AutoEditor.DisplayName("Sample Number Display Offset")]
        public int SamplesNumberDisplayOffset
        {
            get => Samples.InputSampleNumberDisplayOffset;
            set { Samples.InputSampleNumberDisplayOffset = value; }
        }

        ////////////////////////////////////////////////////////////////
        //TraceData callbacks
        internal void BeforeZoomCalculateRequired()
        {
            m_BeforeZoomCalculateRequired = true;
            DrawnValueHighest = double.PositiveInfinity;
            DrawnValueLowest = double.NegativeInfinity;
        }

        private void AfterZoomCalculateRequired()
        {
            m_AfterZoomCalculateRequired = true;
            DrawnValueHighest = double.PositiveInfinity;
            DrawnValueLowest = double.NegativeInfinity;
        }

        public void TraceDataCalculatedSamplesChanged(TraceData sender)
        {
            if (CalculateType != CalculatedTypes.None)
            {
                TraceDataSamplesChanged(sender);
            }
        }


        public void TraceDataSettingsChanged(TraceData sender)
        {
            TraceDataSamplesChanged(sender);
            GuiUpdateControls?.Invoke(this);
        }

        public void TraceDataClosed(TraceData sender)
        {
            Scope.RemoveView(this);
            Dispose();
        }

        public void TraceDataRename(TraceData sender, string oldName, string newName)
        {
            GuiUpdateControls?.Invoke(this);
            Scope.ViewNeedsRepaint(this);
            Scope.GroupWithViewChanged(this);
        }

        public void TraceDataSamplesChanged(TraceData sender)
        {
            UnixTimesChanged();

            if (m_Samples.ViewedIsYTTrace)
            {
                Scope.GroupedTraces(this).ForEach(x => x.RecalculateProjectionRequired());
            }

            lock (m_Samples.DataLock)
            {
                BeforeZoomCalculateRequired();
                if (ProcessAtInput)
                {
                    CalculateTrace();
                }
            }
            Scope.ViewNeedsRepaint(this);
        }

        ////////////////////////////////////////////////////////////////
        //Calculate

        public void CalculateTrace()
        {
            Scope.OnLog?.Invoke(new CsvLog.Entry(
                $"{ViewName} CalculateTrace {(m_BeforeZoomCalculateRequired ? "before " : "")} {(m_AfterZoomCalculateRequired ? "after " : "")} {((m_RecalculateProjectionRequired == 1) ? "projection " : "")})",
                CsvLog.Priority.Debug));

            if (CanShowRealYT || CanShowFakeYT)
            {
                ClearCachedSamples();
                if (m_AfterZoomCalculateRequired || m_BeforeZoomCalculateRequired)
                {
                    RecalculateProjectionRequired();
                }
                m_AfterZoomCalculateRequired = false;
                m_BeforeZoomCalculateRequired = false;
            }
            else
            {
                bool before = false;
                bool after = false;
                double[]? samples = null;
                double[]? view;

                lock (m_Samples.DataLock)
                {
                    m_CachedStatistics = null;
                    view = m_Samples.ViewedSamplesInterpolatedAsDouble;
                    before = m_BeforeZoomCalculateRequired;
                    if (!before)
                    {
                        samples = m_CalculatedBeforeZoom;
                    }
                    m_BeforeZoomCalculateRequired = false;
                    after = m_AfterZoomCalculateRequired;
                    m_AfterZoomCalculateRequired = false;
                    if (CalculateType != CalculatedTypes.None && before)
                    {
                        view = ExecuteCalculate();
                    }
                    if (view == null)
                    {
                        ClearCachedSamples();
                    }
                }

                if (samples == null)
                {
                    before = true;
                }
                double[] projected;
                int drawnStart = 0;

                if (before && view != null)
                {
                    ViewOriginalSampleCount = view.Length;
                    m_RawBeforeZoom = ApplyOffsetAndLength(view);
                    samples = CalculateFilters(m_RawBeforeZoom);
                    samples = CalculateBeforeZoom ? CalculateFft(samples) : samples;
                    after = true;
                }

                if (after && samples != null)
                {
                    int sampleCount = samples.Length;
                    int sampleOffset = 0;
                    double[] triggerSamples;
                    lock (m_Samples.DataLock)
                    {
                        if (m_TriggerTrace == null || m_TriggerSamples == null)
                        {
                            triggerSamples = samples;
                            m_TriggerSamples = samples;
                        }
                        else
                        {
                            triggerSamples = m_TriggerSamples;
                        }
                    }

                    if (FindTrigger(triggerSamples, ref sampleCount, ref sampleOffset))
                    {
                        projected = GetDrawnSamples(samples, sampleOffset, sampleCount, out drawnStart);
                        if (projected != null)
                        {
                            (var peakMin, var peakMax) = PeakHoldBeforeZoom(samples, sampleOffset, drawnStart, projected.Length);
                            projected = CalculateAfterZoom ? CalculateFft(projected) : projected;
                            PeakHoldAfterZoom(projected, ref peakMin, ref peakMax);
                            lock (m_Samples.DataLock)
                            {
                                m_DrawnSamples = projected;
                                m_CalculatedBeforeZoom = samples;
                                m_PeakHoldDrawn = new TraceDataPeakHold(peakMin, peakMax);
                                m_DrawnStartPosition = drawnStart;
                            }
                        }
                    }

                    RecalculateProjectionRequired();
                }

                if (before || after)
                {
                    //fixme: don't call if the samples didn't actually change (recursive invalidate)
                    m_Samples.ForEachViewer(viewer =>
                    {
                        viewer.TraceDataCalculatedSamplesChanged(m_Samples);
                    });
                }
            }
        }

        private double[] ApplyOffsetAndLength(double[] input)
        {
            double[] result = input;

            if (m_ViewOverrideEnabled && (m_ViewLengthOverride != 0 || m_ViewOffsetOverride != 0))
            {
                result = new double[m_ViewLengthOverride > 0 ? m_ViewLengthOverride : input.Length];

                int inputOffset = m_ViewOffsetOverride > 0 ? m_ViewOffsetOverride : 0;
                int resultOffset = m_ViewOffsetOverride < 0 ? -m_ViewOffsetOverride : 0;
                int copyCount = Math.Min(result.Length - resultOffset, input.Length - inputOffset);
                if (resultOffset >= 0 && resultOffset < result.Length && inputOffset < input.Length)
                {
                    if (m_PadLeftWithFirstValue)
                    {
                        for (int loop = 0; loop < resultOffset; loop++)
                        {
                            result[loop] = input[0];
                        }
                    }
                    Array.Copy(input, inputOffset, result, resultOffset, copyCount);
                    if (m_PadRightWithLastValue && input[copyCount - 1] != 0.0)
                    {
                        for (int loop = resultOffset + copyCount; loop < result.Length; loop++)
                        {
                            result[loop] = input[copyCount - 1];
                        }
                    }
                }
            }
            return result;
        }

        public void UpdateLinkedRanges(IEnumerable<TraceView> list)
        {
            HighestValue = list.Max(x => x.HighestValue);
            LowestValue = list.Min(x => x.LowestValue);
        }

        public bool AutoRange(bool requestShrink = true)
        {
            double oldLow = m_LowestValue;
            double oldHigh = m_HighestValue;
            TraceView[] group = Scope.GroupedTraces(this);
            bool shrink = requestShrink || group.Any(x => x.m_AutoReduceRange);

            Scope.OnLog?.Invoke(new CsvLog.Entry($"AutoRange {DecoratedName} shrink={shrink}", CsvLog.Priority.Debug));

            foreach (TraceView traceView in group)
            {
                if (traceView.IsRecalculateProjectionRequired)
                {
                    traceView.CalculateTrace();
                }
                if (!double.IsFinite(traceView.DrawnValueLowest))
                {
                    Painter.CalculateTraceRange(Scope.PaintBox.TraceToGroupDisplayInfo(this));
                }
                if (traceView.Visible && double.IsFinite(traceView.DrawnValueLowest))
                {
                    double high = traceView.DrawnValueHighest;
                    double low = traceView.DrawnValueLowest;
                    AddFactor(ref high, ref low, 0.1);
                    if (shrink || oldHigh < high)
                    {
                        oldHigh = high.RoundSignificantUp(3, high - low);
                    }
                    if (shrink || oldLow > low)
                    {
                        oldLow = low.RoundSignificantDown(3, high - low);
                    }
                    shrink = false;
                }
            }
            return SetGroupHighLow(oldHigh, oldLow);
        }

        private int CalculatedFftBins()
        {
            switch (m_MathType)
            {
                case MathTypes.FFTMagnitude:
                case MathTypes.FFTPhase:
                case MathTypes.FFT10Log10:
                case MathTypes.FFT20Log10:
                    return (m_CalculatePhase == CalculatePhases.AfterZoom) ? m_DrawnSamples!.Length : m_CalculatedBeforeZoom!.Length;
                default:
                    return 0;
            }
        }

        private double CalculatedNyquist()
        {
            return m_MathType switch
            {
                MathTypes.FFTMagnitude or MathTypes.FFTPhase or MathTypes.FFT10Log10 or MathTypes.FFT20Log10 => m_Samples.InputSamplesPerSecond / (double)m_FftInputBins * (double)(m_FftResultBins - 1),
                _ => 0.0,
            };
        }

        internal (int leftSampleNumber, int rightSampleNumber, double leftSampleNumberValue, double rightSampleNumberValue, double leftUnixTime, double rightUnixTime, string sampleValueUnit, int viewLengthOverride, int viewOffsetOverride) DrawnExtents()
        {
            int leftSampleNumber;
            int rightSampleNumber;
            double leftSampleNumberValue;
            double rightSampleNumberValue;
            double leftUnixTime;
            double rightUnixTime;
            string sampleValueUnit;
            int viewLengthOverride;
            int viewOffsetOverride;

            leftSampleNumber = 0;
            rightSampleNumber = 0;
            leftSampleNumberValue = 0.0;
            rightSampleNumberValue = 0.0;
            if (CanShowRealYT || CanShowFakeYT)
            {
                TraceData.TimeRange timeRange = YTTimeRange();
                leftUnixTime = timeRange.Left;
                rightUnixTime = timeRange.Right;
            }
            else
            {
                leftUnixTime = 0.0;
                rightUnixTime = 0.0;
            }
            sampleValueUnit = "";
            viewLengthOverride = (ViewOverrideEnabled ? ViewLengthOverride : 0);
            viewOffsetOverride = (ViewOverrideEnabled ? ViewOffsetOverride : 0);
            if (m_Samples != null)
            {
                lock (m_Samples.DataLock)
                {
                    if (m_DrawnSamples != null && m_DrawnSamples!.Length != 0)
                    {
                        bool rebased = IsRebasedResult;
                        leftSampleNumber = rebased ? 0 : m_DrawnStartPosition;
                        rightSampleNumber = leftSampleNumber + m_DrawnSamples!.Length;
                        int num = IsFftTrace ? 0 : (Samples.InputSampleNumberDisplayOffset + viewOffsetOverride);
                        if (IsFftTrace)
                        {
                            int sampleCount;
                            if (!rebased)
                            {
                                double[]? processedSamplesBeforeZoom = m_CalculatedBeforeZoom;
                                sampleCount = processedSamplesBeforeZoom?.Length ?? 0;
                            }
                            else
                            {
                                sampleCount = m_DrawnSamples!.Length;
                            }
                            double ratio = ((m_Samples.InputSamplesPerSecond == 0.0) ? 1.0 : CalculatedNyquist()) / (sampleCount - 1);
                            leftSampleNumberValue = leftSampleNumber * ratio;
                            rightSampleNumberValue = rightSampleNumber * ratio;
                            sampleValueUnit = ((m_Samples.InputSamplesPerSecond != 0.0) ? "Hz" : "");
                        }
                        else if (m_Samples.InputSamplesPerSecond != 0.0)
                        {
                            leftSampleNumberValue = (leftSampleNumber + num) / m_Samples.InputSamplesPerSecond;
                            rightSampleNumberValue = (rightSampleNumber + num) / m_Samples.InputSamplesPerSecond;
                            sampleValueUnit = "s";
                        }
                        else
                        {
                            leftSampleNumberValue = leftSampleNumber + num;
                            rightSampleNumberValue = rightSampleNumber + num;
                        }
                    }
                    else if (CanShowRealYT || CanShowFakeYT)
                    {
                        (leftSampleNumberValue, leftSampleNumber, _) = m_Samples.ViewedSampleAtUnixTime(YTClickToUnixTime(0.0));
                        (rightSampleNumberValue, rightSampleNumber, _) = m_Samples.ViewedSampleAtUnixTime(YTClickToUnixTime(1.0));
                    }
                }
            }
            return (leftSampleNumber, rightSampleNumber, leftSampleNumberValue, rightSampleNumberValue, leftUnixTime, rightUnixTime, sampleValueUnit, viewLengthOverride, viewOffsetOverride);
        }

        public (double[], bool recalculate) SnapshotProjection()
        {
            lock (m_Samples.DataLock)
            {
                bool recalculate = Interlocked.Exchange(ref m_RecalculateProjectionRequired, 0) > 0;
                if (recalculate)
                {
                    Scope.OnLog?.Invoke(new CsvLog.Entry($"{ViewName} requires calculation", CsvLog.Priority.Debug));
                }
                return (m_DrawnSamples ?? new double[0], recalculate);
            }
        }

        internal void RecalculateProjectionRequired()
        {
            Scope.OnLog?.Invoke(new CsvLog.Entry(ViewName + " SetReprocessDisplayRequired", CsvLog.Priority.Debug));
            m_RecalculateProjectionRequired = 1;
            DrawnValueHighest = double.PositiveInfinity;
            DrawnValueLowest = double.NegativeInfinity;
        }

        private double[] CalculateFilters(double[] samples)
        {
            samples = m_FilterTransform == FilterTransforms.DifferentiateIntegrate ? samples.Differentiated() : samples;
            samples = ExecuteFilter(samples);
            samples = CalculateFftFilter(samples);
            samples = m_FilterTransform == FilterTransforms.DifferentiateIntegrate ? samples.Integrated() : samples;
            return samples;
        }

        private double[] CalculateFft(double[] samples)
        {
            return IsFftTrace ? ExecuteFft(samples) : samples;
        }

        private double[] CalculateFftFilter(double[] input)
        {
            if (!UseFftFilter) return input;

            double[] result;
            switch (m_FftFilterType)
            {
                case FftFilterTypes.BandPass:
                    result = FftFilter.BandPass(input,
                        m_FftBandpassHPF6dB, m_FftBandpassHPF3dB,
                        m_FftBandpassLPF3dB, m_FftBandpassLPF6dB,
                        m_Samples.InputSamplesPerSecond, m_FftBandpassWindow);
                    break;
                case FftFilterTypes.BandPassFit:
                    result = FftFilter.BandPass(input,
                        m_FftBandpassHPF3dB,
                        m_FftBandpassLPF3dB,
                        m_Samples.InputSamplesPerSecond, m_FftBandpassWindow);
                    break;
                case FftFilterTypes.Notch:
                    result = FftFilter.Notch(input,
                        m_FftBandpassLPF3dB, m_FftBandpassLPF6dB,
                        m_FftBandpassHPF6dB, m_FftBandpassHPF3dB,
                        m_Samples.InputSamplesPerSecond, m_FftBandpassWindow);
                    break;
                case FftFilterTypes.NotchFit:
                    result = FftFilter.Notch(input, 
                        m_FftBandpassLPF3dB, 
                        m_FftBandpassHPF3dB, 
                        m_Samples.InputSamplesPerSecond, m_FftBandpassWindow);
                    break;
                case FftFilterTypes.HighPass:
                    result = FftFilter.HighPass(input, 
                        m_FftBandpassHPF6dB, 
                        m_FftBandpassHPF3dB, 
                        m_Samples.InputSamplesPerSecond, m_FftBandpassWindow);
                    break;
                case FftFilterTypes.HighPass3dBPerOctave:
                    result = FftFilter.HighPass(input, 
                        m_FftBandpassHPF3dB / 2.0, 
                        m_FftBandpassHPF3dB, 
                        m_Samples.InputSamplesPerSecond, m_FftBandpassWindow);
                    break;
                case FftFilterTypes.LowPass:
                    result = FftFilter.LowPass(input, 
                        m_FftBandpassLPF3dB, 
                        m_FftBandpassLPF6dB, 
                        m_Samples.InputSamplesPerSecond, m_FftBandpassWindow);
                    break;
                case FftFilterTypes.LowPass3dBPerOctave:
                    result = FftFilter.LowPass(input, 
                        m_FftBandpassLPF3dB, 
                        m_FftBandpassLPF3dB * 2.0, 
                        m_Samples.InputSamplesPerSecond, m_FftBandpassWindow);
                    break;
                case FftFilterTypes.None:
                default:
                    result = input;
                    break;
            }
            return result;
        }

        private double[] ExecuteFilter(double[] input)
        {
            try
            {
                Filter filter = FilterChoice.Create(m_TraceFilter);
                return filter is FirFilter ? ((FirFilter)filter).CenterWindowFir(input) : filter.Insert(input);
            }
            catch
            {
                return input;
            }
        }

        private double[] ExecuteFft(double[] input)
        {
            if (m_Fft == null || m_Fft.Width != input.Length)
            {
                m_Fft?.Dispose();
                m_Fft = new Fftw(input.Length);
            }
            m_FftInputBins = input.Length;
            if (FftWindow != SampleWindow.WindowType.Rectangular)
            {
                input = input.ElementProduct(SampleWindow.GenerateWindow(input.Length, FftWindow));
            }
            m_Fft.ExecuteForward(input);
            double[] result;
            if (m_MathType == MathTypes.FFTPhase)
            {
                result = m_Fft.SpectralPhase;
            }
            else
            {
                result = m_Fft.SpectralMagnitude;
            }
            m_FftResultBins = result.Length;
            if (m_MathType == MathTypes.FFT10Log10 || m_MathType == MathTypes.FFT20Log10)
            {
                int length = result.Length;
                double min = 0.0;
                double ratio = m_MathType == MathTypes.FFT20Log10 ? 20.0 : 10.0;
                for (int loop = 0; loop < length; loop++)
                {
                    double dB = ratio * Math.Log10(result[loop]);
                    if (double.IsNegativeInfinity(dB))
                    {
                        dB = double.NegativeInfinity;
                    }
                    else
                    {
                        min = min < dB ? min : dB;
                    }
                    result[loop] = dB;
                }
                for (int loop = 0; loop < length; loop++)
                {
                    result[loop] = result[loop] == double.NegativeInfinity ? min : result[loop];
                }
            }
            return result;
        }

        private bool FindTrigger(double[] samples, ref int sampleCount, ref int sampleOffset)
        {
            if (samples.Length == 0) return true;
            if (m_TriggerMode == TriggerModes.None) return true;

            bool rising = m_TriggerMode == TriggerModes.Rising || m_TriggerMode == TriggerModes.RisingAuto;
            bool auto = m_TriggerMode == TriggerModes.RisingAuto || m_TriggerMode == TriggerModes.FallingAuto;
            int index = sampleOffset;
            double previous = samples[sampleOffset];

            int offset = sampleOffset;
            while (index == sampleOffset && offset < sampleOffset + sampleCount)
            {
                if ((rising && previous < m_TriggerValue && samples[offset] >= m_TriggerValue) || (!rising && previous > m_TriggerValue && samples[offset] <= m_TriggerValue))
                {
                    index = offset;
                    auto = true;
                }
                previous = samples[offset];
                offset++;
            }
            index -= m_PreTriggerSampleCount;
            index = Math.Max(index, 0);
            sampleCount -= index - sampleOffset;
            sampleOffset = index;
            return auto;
        }

        internal TraceData.Statistics CalculateStats()
        {
            lock (m_Samples.DataLock)
            {
                if (m_CachedStatistics == null)
                {
                    if (CanShowRealYT || CanShowFakeYT)
                    {
                        TraceData.TimeRange timeRange = YTTimeRange();
                        m_CachedStatistics = Samples.ViewedSampleStatisticsBetweenUnixTimes(timeRange.Left, timeRange.Right);
                    }
                    else
                    {
                        m_CachedStatistics = m_DrawnSamples == null ? new TraceData.Statistics() : new TraceData.Statistics(m_DrawnSamples);
                    }
                }
                return m_CachedStatistics;
            }
        }

        private TraceData.TimeRange YTTimeRange()
        {
            TraceData.TimeRange paintedTraceGroupUnixTimeRange = DrawnUnixTimeRange;
            double delta = (paintedTraceGroupUnixTimeRange.Right - paintedTraceGroupUnixTimeRange.Left) * ZoomValue;
            double left = paintedTraceGroupUnixTimeRange.Left + (paintedTraceGroupUnixTimeRange.Right - paintedTraceGroupUnixTimeRange.Left) * PanValue;
            double right = left + delta;
            return new TraceData.TimeRange(left, right);
        }

        private (double[] peakMin, double[] peakMax) PeakHoldBeforeZoom(double[] samples, int startTrigger, int start, int sampleCount)
        {
            lock (m_Samples.DataLock)
            {
                try
                {
                    if (m_PaintMode == PaintModes.PeakHold && !CalculateAfterZoom)
                    {
                        PeakHold(samples, startTrigger, samples.Length - startTrigger);
                        if (m_PeakHoldAll == null) throw new NullReferenceException();
                        return (m_PeakHoldAll.Min.Copy(), m_PeakHoldAll.Max.Copy());
                    }
                }
                catch (Exception e)
                {
                    Scope.OnLog?.Invoke(new CsvLog.Entry(e.ToString(), CsvLog.Priority.Exception));
                }
            }

            return (samples, samples);
        }

        private void PeakHoldAfterZoom(double[] drawnSamples, ref double[] peakMin, ref double[] peakMax)
        {
            if (m_PaintMode == PaintModes.PeakHold && CalculateAfterZoom)
            {
                PeakHold(drawnSamples, 0, drawnSamples.Length);
                peakMax = m_PeakHoldAll.Max;
                peakMin = m_PeakHoldAll.Min;
            }
        }

        ////////////////////////////////////////////////////////////////
        //UI
        internal string DecoratedName => m_ViewName + (Samples.Name == ViewName ? "" : "->" + Samples.Name);

        internal void AutoRangeTime()
        {
            m_OverrideSamplesUnixTime = false;
            UnixTimesChanged();
        }

        private double YTClickToUnixTime(double xRatio)
        {
            TraceData.TimeRange timeRange = YTTimeRange();
            return timeRange.Left + (timeRange.Right - timeRange.Left) * xRatio;
        }

        internal static void AddFactor(ref double high, ref double low, double factor)
        {
            double delta = high - low;
            if (delta == 0.0)
            {
                delta = 1.0;
            }
            high += delta * factor;
            low -= delta * factor;
        }

        internal string VerticalUnitFormat
        {
            get
            {
                return m_Samples.VerticalUnit.Contains("{")
                    ? (IsLogarithmicY
                        ? string.Format(m_Samples.VerticalUnit, "{0} dB")
                        : m_Samples.VerticalUnit)
                    : (IsLogarithmicY
                        ? "{0} dB" + m_Samples.VerticalUnit
                        : "{0} " + m_Samples.VerticalUnit);
            }
        }

        internal MouseInfo Measure(MouseEventArgs? e)
        {
            MouseInfo result = new MouseInfo();
            double x = e?.X ?? 0;
            double y = e?.Y ?? 0;

            lock (m_Samples.DataLock)
            {
                TraceGroupDisplay traceDivision = Scope.PaintBox.TraceToGroupDisplayInfo(this);

                result.XRatio = (x - traceDivision.ProjectionArea.Left) / traceDivision.ProjectionArea.Width;
                result.YRatio = (y - traceDivision.ProjectionArea.Top) / traceDivision.ProjectionArea.Height;
                result.XRatio = Math.Min(result.XRatio, 1.0);
                result.XValue = (m_HighestValue - m_LowestValue) * result.XRatio + m_LowestValue;
                result.YValue = m_HighestValue - (m_HighestValue - m_LowestValue) * result.YRatio;

                if (traceDivision.YTTrace)
                {
                    var fineUnixTimeAtX = (double)YTClickToUnixTime(result.XRatio);
                    (result.SampleAtX, result.IndexBeforeTrim, result.UnixTimeAtX) = Samples.ViewedSampleAtUnixTime(fineUnixTimeAtX);
                }
                else if (m_DrawnSamples != null && m_DrawnSamples.Length > 0 && result.XRatio >= 0.0 && result.XRatio <= 1.0)
                {
                    bool rebased = IsRebasedResult;
                    int length = m_DrawnSamples!.Length;
                    int index = (int)Math.Floor((double)length * result.XRatio);
                    index = index < 0 ? 0 : ((index >= length) ? (length - 1) : index);
                    result.SampleAtX = m_DrawnSamples[index];

                    int offset = ((ViewOverrideEnabled && !IsFftTrace) ? ViewOffsetOverride : 0);
                    result.IndexAfterTrim = index + ((!rebased) ? m_DrawnStartPosition : 0);
                    result.IndexBeforeTrim = index + ((!rebased) ? (m_DrawnStartPosition + offset) : 0);
                    result.CountAfterTrim = (rebased ? m_DrawnSamples!.Length : m_CalculatedBeforeZoom!.Length);
                    result.CountBeforeTrim = (rebased ? m_DrawnSamples!.Length : m_Samples.ViewedSampleCount);
                    result.UnixTimeAtX = m_Samples.InputSamplesPerSecond == 0 ? 0 : result.IndexBeforeTrim / m_Samples.InputSamplesPerSecond;

                    if (IsFftTrace && m_Fft != null)
                    {
                        result.ExtraInfo = @$"FFT[{SampleNumberText(result)}]"; //fixme: analyse fft
                    }
                }
            }
            return result;
        }

        public string SampleNumberText(MouseInfo click)
        {
            if (IsFftTrace)
            {
                double value = click.IndexBeforeTrim * ((m_Samples.InputSamplesPerSecond == 0.0) ? 1.0 : CalculatedNyquist()) / (double)(click.CountAfterTrim - 1);
                return m_Samples.InputSamplesPerSecond == 0.0
                    ? $"{value.ToStringRound(5, 3)} of N"
                    : value.ToStringRound(5, 3, "Hz");
            }
            else if (Samples.ViewedIsYTTrace)
            {
                return click.UnixTimeAtX.ToStringRound(3, 3); // use ToHorizontalUnit?
            }
            else
            {
                return m_Samples.InputSamplesPerSecond != 0.0
                    ? click.UnixTimeAtX.ToStringRound(5, 3, "s")
                    : m_Samples.InputSamplesPerSecond.ToString();
            }
        }

        private void ZoomPanChanged()
        {
            m_CachedStatistics = null;
            if (CalculateAfterZoom)
            {
                ClearPeakHold();
            }
            AfterZoomCalculateRequired();
        }

        public void ShowControlForm()
        {
            new AutoEditorForm()
                .ShowDialog(
                sourceData: this,
                prompt: "View configuration",
                title: DecoratedName);
        }

        public string TraceInfo()
        {
            lock (m_Samples.DataLock)
            {
                StringBuilder text = new StringBuilder();
                if (CanShowRealYT || CanShowFakeYT)
                {
                    TraceData.TimeRange timeRange = YTTimeRange();
                    text.Append($"{(timeRange.Right - timeRange.Left).ToStringRound(4, 3)} s, {CalculateStats()}");
                }
                else if (m_DrawnSamples == null)
                {
                    text.Append(m_Samples.ViewedSampleCount != 0 ? "Samples changed after calculation" : "No trace");
                }
                else if (IsFftTrace)
                {
                    text.Append($"({CalculatedFftBins()} bin FFT), ");
                    double rhs = CalculatedNyquist();
                    if (rhs != 0.0)
                    {
                        text.Append($"{rhs.ToStringRound(4, 3)} Hz ny, ");
                    }
                }
                else
                {
                    if (m_Samples.InputSamplesPerSecond > 0.0)
                    {
                        double seconds = m_DrawnSamples.Length / m_Samples.InputSamplesPerSecond;
                        text.Append($"{seconds.ToStringRound(4, 3)} s, {m_Samples.InputSamplesPerSecond.ToStringRound(5, 3)} sps, ");
                    }
                    text.Append(" " + CalculateStats().ToString());
                }
                return text.ToString();
            }
        }

        internal void TraceClicked(MouseEventArgs e)
        {
            for (int loop = Clicks.Length - 1; loop > 0; loop--)
            {
                Clicks[loop] = Clicks[loop - 1];
            }
            Clicks[0] = Measure(e);
        }

        internal string ClickString()
        {
            double delta01 = Clicks[0].YValue - Clicks[1].YValue;
            double delta23 = Clicks[2].YValue - Clicks[3].YValue;
            StringBuilder text = new StringBuilder();
            var time = Samples.InputSamplesPerSecond == 0.0 ? "" : $" ({SampleNumberText(Clicks[0])})";
            text.Append($"{ViewName}[{Clicks[0].IndexBeforeTrim}/{Clicks[0].CountBeforeTrim}{time}]");
            text.Append(@"
value=" + string.Format(VerticalUnitFormat, Clicks[0].SampleAtX.ToStringRound(5, 3)));
            var deltaInfo = Clicks[0].ShallowClone();
            deltaInfo.IndexBeforeTrim = Clicks[0].IndexBeforeTrim - Clicks[1].IndexBeforeTrim;
            string delta = $"{deltaInfo.IndexBeforeTrim}{((Samples.InputSamplesPerSecond == 0.0) ? "" : (" (" + SampleNumberText(deltaInfo) + ")"))}";
            var c12 = Math.Abs(delta01).ToStringRound(5, 3);
            var c23 = Math.Abs(delta23).ToStringRound(5, 3);
            var ratio = ((delta01 + delta23 != 0.0) ? (delta23 / delta01) : 0.0).ToStringRound(5, 3);
            text.Append(@$"
[C1]-[C2]={c12} [C3]-[C4]={c23} Ratio={ratio}, last clicks delta={delta}");

            if (Clicks[0].ExtraInfo.Length > 0)
            {
                text.Append($@"
{Clicks[0].ExtraInfo}");
            }
            return text.ToString();
        }

        public bool SetHighLow(double top, double bottom)
        {
            bool changed = (m_HighestValue != top || m_LowestValue != bottom) && !double.IsNaN(top) && !double.IsNaN(bottom);
            if (changed)
            {
                m_HighestValue = top;
                m_LowestValue = bottom;
                RecalculateProjectionRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
            return changed;
        }

        public bool SetGroupHighLow(double high, double low)
        {
            bool changed = SetHighLow(high, low);
            if (m_PaintMode != PaintModes.FFT2D)
            {
                TraceView[] array = Scope.GroupedTraces(this);
                array.Where(x => x.Visible && x != this).ForEach(x => changed |= x.SetHighLow(high, low));
            }
            return changed;
        }

        private double[] GetDrawnSamples(double[] samples, int start, int sampleCount, out int drawnStart)
        {
            drawnStart = 0;
            int count = samples.Length;
            double[] array;
            if (count == 0)
            {
                array = new double[0];
            }
            else
            {
                sampleCount = Math.Min(sampleCount, (int)(count * m_ZoomValue));
                sampleCount = Math.Min(sampleCount, count);
                sampleCount = Math.Max(1, sampleCount);
                start += samples.Length - (int)(count * (1.0 - m_PanValue));
                start = Math.Min(start, count - sampleCount);
                array = samples.Skip(start).Take(sampleCount).ToArray();
                drawnStart = start;
            }
            return array;
        }

        internal SnapshotYT SnapshotYTProjection(double leftTime, double rightTime, out bool recalculateProjectionRequired)
        {
            recalculateProjectionRequired = Interlocked.Exchange(ref m_RecalculateProjectionRequired, 0) > 0;
            return m_Samples.SnapshotYTProjection(leftTime, rightTime);
        }

        public double[] ExecuteCalculate()
        {
            //fixme: recursive invalidate with YT traces

            double[][] sourceTraces = CalculatedSourceViews.Select(x => (x.CanShowRealYT ? x.Samples.ViewedSamplesInterpolatedAsDouble : x.CalculatedBeforeZoom) ?? new double[0]).ToArray();
            int minLength = sourceTraces.Min(x => x.Length);
            int maxLength = sourceTraces.Max(x => x.Length);
            int traceCount = sourceTraces.Length;

            void exact(int count) { if (traceCount != count) throw new Exception($"Type {CalculateType} expects {count} traces"); }
            void minimum(int count) { if (traceCount < count) throw new Exception($"Type {CalculateType} expects {count} or more traces"); }

            double[] result = new double[0];

            var transposedMax = Enumerable.Range(0, maxLength).Select(index => sourceTraces.Where(arr => index < arr.Length).Select(arr => arr[index]));
            var transposedMin = Enumerable.Range(0, minLength).Select(index => sourceTraces.Select(arr => arr[index]));

            //todo: check performance - linq might be too painful

            switch (CalculateType)
            {
                case CalculatedTypes.PythonScript:
                    break; // not implemented

                case CalculatedTypes.Magnitude:
                    minimum(2);
                    result = transposedMax.Select(x => x.ToArray().Aggregate(0.0, (prod, arr) => prod + arr * arr)).ToArray().Sqrt();
                    break;

                case CalculatedTypes.Atan2:
                    exact(2);
                    result = transposedMin.Select(x => x.ToArray()).Select(x => Math.Atan2(x[0], x[1])).ToArray();
                    break;

                case CalculatedTypes.Difference:
                    exact(2);
                    result = transposedMin.Select(x => x.ToArray()).Select(x => Math.Abs(x[0] - x[1])).ToArray();
                    break;

                case CalculatedTypes.Subtract:
                    exact(2);
                    result = transposedMin.Select(x => x.ToArray()).Select(x => x[0] - x[1]).ToArray();
                    break;

                case CalculatedTypes.Abs:
                    exact(1);
                    result = sourceTraces[0].Select(x => Math.Abs(x)).ToArray();
                    break;

                case CalculatedTypes.Sum:
                    minimum(2);
                    result = transposedMax.Select(x => x.Sum()).ToArray();
                    break;

                case CalculatedTypes.SubtractOffset:
                    exact(1);
                    double simpleOffset = ((TraceView.CalculatedTraceDataOneDouble)CalculatedParameter).Param;
                    result = sourceTraces[0].Select(x => x - simpleOffset).ToArray();
                    break;

                case CalculatedTypes.Mean:
                    minimum(2);
                    result = transposedMax.Select(x => x.ToArray()).Select(x => x.Sum() / x.Count()).ToArray();
                    break;

                case CalculatedTypes.Product:
                    minimum(2);
                    result = transposedMax.Select(x => x.ToArray().Product()).ToArray();
                    break;

                case CalculatedTypes.ProductSimple:
                    exact(1);
                    double simpleProduct = ((TraceView.CalculatedTraceDataOneDouble)CalculatedParameter).Param;
                    result = sourceTraces[0].Select(x => x * simpleProduct).ToArray();
                    break;

                case CalculatedTypes.PolyFilter:
                    int order = ((TraceView.CalculatedTraceDataOrder)CalculatedParameter).Order;
                    exact(1);
                    result = sourceTraces[0].PolyFilter(order);
                    break;

                case CalculatedTypes.FIR:
                    exact(2);
                    result = new Filters.FirFilter(sourceTraces[1]).CenterWindowFir(sourceTraces[0]);
                    break;

                case CalculatedTypes.Rescale:
                    exact(1);
                    var rescale = (TraceView.CalculatedTraceDataMinMax)CalculatedParameter;
                    result = sourceTraces[0].Rescale(rescale.Min, rescale.Max);
                    break;

                case CalculatedTypes.Normalised:
                    exact(1);
                    result = sourceTraces[0].Normalised();
                    break;

                case CalculatedTypes.Differentiate:
                    exact(1);
                    result = sourceTraces[0].Differentiated();
                    break;

                case CalculatedTypes.Integrate:
                    exact(1);
                    result = sourceTraces[0].Integrated();
                    break;

                case CalculatedTypes.Quantize:
                    exact(1);
                    var quantize = (TraceView.CalculatedTraceDataQuantise)CalculatedParameter;
                    result = sourceTraces[0].Subtract(quantize.Offset).Quantize(quantize.Scale);
                    break;

                case CalculatedTypes.RollingRMS:
                    exact(1);
                    int rmsWindow = ((TraceView.CalculatedTraceDataWindow)CalculatedParameter).Window;
                    result = sourceTraces[0].RollingRms(rmsWindow);
                    break;

                case CalculatedTypes.RollingMean:
                    exact(1);
                    int meanWindow = ((TraceView.CalculatedTraceDataWindow)CalculatedParameter).Window;
                    result = sourceTraces[0].RollingMean(meanWindow);
                    break;

                case CalculatedTypes.ProjectYTtoY:
                    exact(1);
                    result = sourceTraces[0];
                    break;

                case CalculatedTypes.RescaledError:
                    exact(2);
                    result = sourceTraces[0].Rescale(0, 1).Copy(0, minLength).Subtract(sourceTraces[1].Rescale(0, 1).Copy(0, minLength));
                    break;

                case CalculatedTypes.NormalisedError:
                    exact(2);
                    result = sourceTraces[0].Normalised().Copy(0, minLength).Subtract(sourceTraces[1].Normalised().Copy(0, minLength));
                    break;

                case CalculatedTypes.Resample:
                    exact(1);
                    result = sourceTraces[0].Resample(((TraceView.CalculatedTraceDataCount)CalculatedParameter).Count);
                    break;
            }
            return result;
        }

        ////////////////////////////////////////////////////////////////
        //other

        private void PeakHold(double[] samples, int start, int count)
        {
            lock (m_Samples.DataLock)
            {
                if (m_PeakHoldAll == null)
                {
                    m_PeakHoldAll = new TraceDataPeakHold(samples, start, count);
                }
                else
                {
                    m_PeakHoldAll.Peak(samples, start, count);
                }
            }
        }

        private void ClearPeakHold()
        {
            m_PeakHoldAll?.Dispose();
            m_PeakHoldAll = null;
            m_PeakHoldDrawn?.Dispose();
            m_PeakHoldDrawn = null;
        }

        private void ClearCachedSamples()
        {
            m_CachedStatistics = null;
            lock (m_Samples.DataLock)
            {
                m_DrawnSamples = null;
                m_CalculatedBeforeZoom = null;
                ClearPeakHold();
                m_RawBeforeZoom = null;
                m_DrawnStartPosition = 0;
                ViewOriginalSampleCount = 0;
            }
        }

        public void Close()
        {
            TraceData samples = m_Samples;
            m_Samples.RemoveViewer(this);
            Scope.RemoveView(this);
            Scope.ViewNeedsRepaint(this);
            if (samples.VisibleViewerCount == 0)
            {
                samples.Close();
            }
            Dispose();
        }

        public void Dispose()
        {
            GroupWithView = "";
            Scope.RemoveView(this);
            m_Samples.RemoveViewer(this);
            Scope.BeginInvokeIfRequired(() =>
            {
                CalculatedSourceViews.Clear();

                m_Samples.Dispose();
                Painter.Dispose();
                m_CachedStatistics = null;
                m_TriggerTrace = null;
                ClearPeakHold();
                m_RawBeforeZoom = null;
                m_CalculatedBeforeZoom = null;
                m_DrawnSamples = null;
                m_TriggerSamples = null;

                m_Fft?.Dispose();
                m_Fft = null;

            });
        }
    }
}

