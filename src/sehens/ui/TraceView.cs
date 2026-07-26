using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        [AutoEditor.DisplayOrder(1, "Display Settings")]
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
        [AutoEditor.DisplayOrder(1)]
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
        [AutoEditor.DisplayOrder(1.1)]
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

        [AutoEditor.Hidden]
        public double SelectTime; // HighResTimer.StaticSeconds when trace selected

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
            Spectral,
        }
        private PaintModes m_PaintMode = PaintModes.PolygonDigital;
        [XmlSave]
        [AutoEditor.DisplayOrder(1.1)]
        public PaintModes PaintMode
        {
            get => m_PaintMode;
            set
            {
                if (m_PaintMode == value) return;
                lock (Samples.DataLock)
                {
                    // Spectral forces its own FFT window (see ExecuteFft)
                    bool spectralChanged = (m_PaintMode == PaintModes.Spectral) != (value == PaintModes.Spectral);

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
                        case PaintModes.Spectral:
                        case PaintModes.Points:
                        case PaintModes.PointsIfChanged: Painter = new Paint2dTrace(); break;
                    }
                    if (spectralChanged)
                    {
                        BeforeZoomCalculateRequired();
                    }
                    RecalculateProjectionRequired();
                    Scope.ViewNeedsRepaint(this);
                    GuiUpdateControls?.Invoke(this);
                }
            }
        }

        private string m_TraceFilter = "None";
        [XmlSave]
        [AutoEditor.Values(typeof(FilterChoice))]
        [AutoEditor.DisplayOrder(3.5)]
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

        // XY/XYZ viewport: interpreted only when PaintMode is one of the XY* or XYZ* modes.
        // Zoom is a fraction (0,1] of the source trace's full extent; pan is 0..(1-zoom).
        private double m_XYXZoom = 1.0;
        private double m_XYXPan = 0.0;
        private double m_XYYZoom = 1.0;
        private double m_XYYPan = 0.0;
        private double m_XYZZoom = 1.0;
        private double m_XYZPan = 0.0;

        [XmlSave]
        [AutoEditor.Hidden]
        public double XYXZoom { get => m_XYXZoom; set { double v = Math.Max(1e-6, Math.Min(1.0, value)); if (m_XYXZoom != v) { m_XYXZoom = v; Scope?.ViewNeedsRepaint(this); } } }
        [XmlSave]
        [AutoEditor.Hidden]
        public double XYXPan { get => m_XYXPan; set { double v = Math.Max(0.0, Math.Min(1.0, value)); if (m_XYXPan != v) { m_XYXPan = v; Scope?.ViewNeedsRepaint(this); } } }
        [XmlSave]
        [AutoEditor.Hidden]
        public double XYYZoom { get => m_XYYZoom; set { double v = Math.Max(1e-6, Math.Min(1.0, value)); if (m_XYYZoom != v) { m_XYYZoom = v; Scope?.ViewNeedsRepaint(this); } } }
        [XmlSave]
        [AutoEditor.Hidden]
        public double XYYPan { get => m_XYYPan; set { double v = Math.Max(0.0, Math.Min(1.0, value)); if (m_XYYPan != v) { m_XYYPan = v; Scope?.ViewNeedsRepaint(this); } } }
        [XmlSave]
        [AutoEditor.Hidden]
        public double XYZZoom { get => m_XYZZoom; set { double v = Math.Max(1e-6, Math.Min(1.0, value)); if (m_XYZZoom != v) { m_XYZZoom = v; Scope?.ViewNeedsRepaint(this); } } }
        [XmlSave]
        [AutoEditor.Hidden]
        public double XYZPan { get => m_XYZPan; set { double v = Math.Max(0.0, Math.Min(1.0, value)); if (m_XYZPan != v) { m_XYZPan = v; Scope?.ViewNeedsRepaint(this); } } }

        [AutoEditor.Hidden] // derived from PaintMode - not an editable setting
        public bool IsXYMode =>
            m_PaintMode == PaintModes.XYLine
            || m_PaintMode == PaintModes.XYPoints
            || m_PaintMode == PaintModes.XYCurve
            || m_PaintMode == PaintModes.XYZProjection;

        private bool m_HoldPanZoom = false;
        [XmlSave]
        [AutoEditor.DisplayOrder(6.8)] // horizontal-axis band
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
        [AutoEditor.DisplayOrder(1.1)]
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
        [AutoEditor.DisplayOrder(7.2)] // data-range band
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
        [AutoEditor.DisplayOrder(7, "Data Range")]
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
        [AutoEditor.DisplayOrder(7)]
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
        [AutoEditor.DisplayOrder(2)]
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
        [AutoEditor.DisplayOrder(6.7)] // horizontal-axis band
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
        [AutoEditor.DisplayOrder(6.75)] // horizontal-axis band
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

        public enum LogVerticalMode { Off, Log, dB10, dB20 }
        public enum LogHorizontalMode { Off, Log }

        private LogVerticalMode m_LogVertical;
        [XmlSave]
        [AutoEditor.DisplayName("Log vertical axis")]
        [AutoEditor.DisplayOrder(2)]
        public LogVerticalMode LogVertical
        {
            get => m_LogVertical;
            set
            {
                if (m_LogVertical == value) return;
                bool oldWasDb = m_LogVertical == LogVerticalMode.dB10 || m_LogVertical == LogVerticalMode.dB20;
                bool newIsDb = value == LogVerticalMode.dB10 || value == LogVerticalMode.dB20;
                m_LogVertical = value;
                if (oldWasDb || newIsDb)
                {
                    ClearPeakHold();
                    BeforeZoomCalculateRequired();
                }
                else
                {
                    RecalculateProjectionRequired();
                }
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        private LogHorizontalMode m_LogHorizontal;
        [XmlSave]
        [AutoEditor.DisplayName("Log horizontal axis")]
        [AutoEditor.DisplayOrder(2.1)]
        public LogHorizontalMode LogHorizontal
        {
            get => m_LogHorizontal;
            set
            {
                if (m_LogHorizontal == value) return;
                m_LogHorizontal = value;
                RecalculateProjectionRequired();
                Scope.ViewNeedsRepaint(this);
                GuiUpdateControls?.Invoke(this);
            }
        }

        // Horizontal Axis editor band. The axis terms live on TraceData, which is
        // [AutoEditor.Hidden]; these proxies expose them in the double-click trace editor.
        // There is no enable toggle: the identity map (0, 1, "") IS the sample-number axis, so the
        // values take effect as typed. Composition: sps wins the scale (multiplier ignored while
        // sps > 0), the offset composes with either scale, the unit overrides the "s" default.
        // Persistence stays on the trace (SehensSave.Trace), so no [XmlSave] here.
        [AutoEditor.DisplayOrder(6, "Horizontal Axis")]
        [AutoEditor.DisplayName("Offset")]
        [AutoEditor.Tooltip("Shifts the axis by this many samples\nvalue = (sample + offset) / sps, or (sample + offset) * multiplier.")]
        public double HorizontalAxisOffset
        {
            get => m_Samples.HorizontalOffset;
            set => m_Samples.SetHorizontalAffine(value, m_Samples.HorizontalMultiplier, m_Samples.HorizontalAxisUnit);
        }

        [AutoEditor.DisplayOrder(6.1)]
        [AutoEditor.DisplayName("Multiplier")]
        [AutoEditor.Tooltip("Axis value per sample (value = offset + multiplier * sample).\nIgnored while Samples Per Second > 0 - a rate already sets the scale.")]
        public double HorizontalAxisMultiplier
        {
            get => m_Samples.HorizontalMultiplier;
            set => m_Samples.SetHorizontalAffine(m_Samples.HorizontalOffset, value, m_Samples.HorizontalAxisUnit);
        }

        [AutoEditor.DisplayOrder(6.2)]
        [AutoEditor.DisplayName("Unit")]
        [AutoEditor.Tooltip("Axis unit label. Overrides the \"s\" default when Samples Per Second is set.")]
        public string HorizontalAxisUnit
        {
            get => m_Samples.HorizontalAxisUnit;
            set => m_Samples.SetHorizontalAffine(m_Samples.HorizontalOffset, m_Samples.HorizontalMultiplier, value);
        }

        private bool m_ShowPictureInPicture;
        [XmlSave]
        [AutoEditor.DisplayOrder(1.1)]
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

        private bool m_AudioLoop;
        [XmlSave]
        [AutoEditor.DisplayOrder(1.2)]
        public bool AudioLoop
        {
            get => m_AudioLoop;
            set
            {
                if (m_AudioLoop == value) return;
                m_AudioLoop = value;
                GuiUpdateControls?.Invoke(this);
            }
        }

        private bool m_AudioAfterFilter = true;
        [XmlSave]
        [AutoEditor.DisplayOrder(1.3)]
        public bool AudioAfterFilter
        {
            get => m_AudioAfterFilter;
            set
            {
                if (m_AudioAfterFilter == value) return;
                m_AudioAfterFilter = value;
                if (IsPlaying)
                {
                    StartPlayback();
                }
                GuiUpdateControls?.Invoke(this);
            }
        }

        private TraceViewAudioPlayback? m_AudioPlayback;
        private double[]? m_PlaybackSourceSamples;

        [AutoEditor.Hidden]
        public bool IsPlaying => m_AudioPlayback?.IsPlaying ?? false;

        [AutoEditor.Hidden]
        public int PlaybackSampleNumber => m_AudioPlayback?.CurrentSampleNumber ?? -1;

        public void StartPlayback()
        {
            StopPlayback();
            double[]? samples = m_AudioAfterFilter ? CalculatedBeforeZoom : RawBeforeZoom;
            double sps = Samples.InputSamplesPerSecond;
            if (samples == null || samples.Length <= 1 || sps <= 0) return;
            m_PlaybackSourceSamples = samples;
            var playback = new TraceViewAudioPlayback(
                samples, startSample: 0, samplesPerSecond: sps,
                onTick: () => Scope.ViewNeedsRepaint(this),
                onFinished: () =>
                {
                    Scope.ViewNeedsRepaint(this);
                    if (m_AudioLoop) Scope.BeginInvokeIfRequired(() => { if (m_AudioLoop) StartPlayback(); });
                });
            m_AudioPlayback = playback;
            playback.Play();
            Scope.ViewNeedsRepaint(this);
        }

        public void StopPlayback()
        {
            var playback = m_AudioPlayback;
            m_AudioPlayback = null;
            m_PlaybackSourceSamples = null;
            playback?.Dispose();
            Scope.ViewNeedsRepaint(this);
        }

        internal void RefreshPlaybackBuffer()
        {
            if (!IsPlaying) return;
            double[]? current = m_AudioAfterFilter ? CalculatedBeforeZoom : RawBeforeZoom;
            if (current == null || current == m_PlaybackSourceSamples) return;
            Scope.BeginInvokeIfRequired(() => { if (IsPlaying) StartPlayback(); });
        }

        private TraceView? m_TriggerTrace;
        //fixme make editable using [AutoEditor.Values(m_Display.)]
        [AutoEditor.DisplayName("Trigger Trace")]
        [AutoEditor.DisplayOrder(4)]
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
        [AutoEditor.DisplayOrder(4)]
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
        [AutoEditor.DisplayOrder(4, "Trigger and Timing")]
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
                ViewOverrideChanged();
            }
        }

        private int m_ViewLengthOverride;
        [XmlSave]
        [AutoEditor.DisplayName("View Length")]
        [AutoEditor.DisplayOrder(6.6)] // horizontal-axis band: crops the sample window
        public int ViewLengthOverride
        {
            get => m_ViewLengthOverride;
            set
            {
                if (m_ViewLengthOverride == value) return;
                m_ViewLengthOverride = (value >= 0) ? value : 0;
                m_ViewOverrideEnabled = m_ViewLengthOverride != 0 || m_ViewOffsetOverride != 0;
                ViewOverrideChanged();
            }
        }

        private int m_ViewOffsetOverride;
        [XmlSave]
        [AutoEditor.DisplayName("View Offset")]
        [AutoEditor.DisplayOrder(6.65)] // horizontal-axis band: shifts the sample window
        public int ViewOffsetOverride
        {
            get => m_ViewOffsetOverride;
            set
            {
                if (m_ViewOffsetOverride == value) return;
                m_ViewOffsetOverride = value;
                m_ViewOverrideEnabled = m_ViewLengthOverride != 0 || m_ViewOffsetOverride != 0;
                ViewOverrideChanged();
            }
        }

        // A view length/offset change moves the horizontal extents, so the whole group must
        // reproject (the projection cache holds the OLD extents; without this the axis and curves
        // only caught up on the next zoom nudge).
        private void ViewOverrideChanged()
        {
            BeforeZoomCalculateRequired();
            Scope.GroupedTraces(this).ForEach(x => x.RecalculateProjectionRequired());
            Scope.ViewNeedsRepaint(this);
            GuiUpdateControls?.Invoke(this);
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
        [AutoEditor.DisplayOrder(4)]
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
        [AutoEditor.DisplayOrder(3.1)]
        [AutoEditor.DisplayName("FFT Bandpass HPF 6dB Hz")]
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
        [AutoEditor.DisplayOrder(3.2)]
        [AutoEditor.DisplayName("FFT Bandpass HPF 3dB Hz")]
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
        [AutoEditor.DisplayOrder(3.3)]
        [AutoEditor.DisplayName("FFT Bandpass LPF 3dB Hz")]
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
        [AutoEditor.DisplayOrder(3.4)]
        [AutoEditor.DisplayName("FFT Bandpass LPF 6dB Hz")]
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
            NotchFit,
            WeightedAudioA,
            WeightedAudioC
        }
        private FftFilterTypes m_FftFilterType;
        [XmlSave]
        [AutoEditor.DisplayOrder(3.5)]
        [AutoEditor.DisplayName("FFT Filter Type")]
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
        [AutoEditor.DisplayOrder(3.5)]
        [AutoEditor.DisplayName("FFT Bandpass Window")]
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
        [AutoEditor.DisplayOrder(3.5)]
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
        [AutoEditor.DisplayOrder(3.5)]
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
            public CalculatedTraceData Clone() => (CalculatedTraceData)MemberwiseClone();
        }

        public class CalculatedTraceDataOneDouble : CalculatedTraceData // XML Serialised
        {
            [AutoEditor.DisplayName("Value")]
            public double Param = 1.0;
        }

        public class CalculatedTraceDataQuantise : CalculatedTraceData // XML Serialised
        {
            public double Offset = 1.0;
            public double Scale = 32767.0;
        }

        public class CalculatedTraceDataWindow : CalculatedTraceData // XML Serialised
        {
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
            [AutoEditor.DisplayName("Sample count")]
            public int Count = 100;

            // Resample: if > 0, resample to this rate (Hz) over the source's duration and set the
            // output trace rate, taking precedence over Count. 0 = resample by Count (default).
            [AutoEditor.DisplayName("Sample rate (Hz)")]
            public double SamplesPerSecond = 0.0;
        }

        public class CalculatedTraceDataOrder : CalculatedTraceData // XML Serialised
        {
            public int Order = 5;
        }

        [XmlSave]
        [AutoEditor.DisplayOrder(3, "FFT, Filter and Math")]
        public CalculatedTypes CalculateType;

        [XmlSave(nestedXml: true, nestedDerivedTypes: new Type[]
        {
            typeof(CalculatedTraceDataOneDouble),
            typeof(CalculatedTraceDataQuantise),
            typeof(CalculatedTraceDataWindow),
            typeof(CalculatedTraceDataMinMax),
            typeof(CalculatedTraceDataCount)
        })]
        [AutoEditor.SubEditor]
        [AutoEditor.DisplayOrder(3)]
        public CalculatedTraceData CalculatedParameter = new CalculatedTraceData();
        [AutoEditor.Hidden]
        public List<TraceView> CalculatedSourceViews = new List<TraceView>();        //serialised by ScopeSave

        public enum MathTypes
        {
            Normal,
            FFTMagnitude,
            FFTPhase
        }
        private MathTypes m_MathType;
        [XmlSave]
        [AutoEditor.DisplayOrder(3.5)]
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
        [AutoEditor.DisplayOrder(3.5)]
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
            public int GroupIndex; // index number of this group, counting visible groups only
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
            public bool BeyondDrawnData;


            public MouseInfo ShallowClone() { return (MouseInfo)MemberwiseClone(); }
        }

        internal bool IsFftTrace => m_MathType == MathTypes.FFTMagnitude || m_MathType == MathTypes.FFTPhase;

        internal string TraceHoverStatistics(MouseInfo clickInfo)
        {
            return clickInfo.YRatio is >= 0.0 and <= 1.0
                && !clickInfo.BeyondDrawnData
                && double.IsFinite(clickInfo.SampleAtX)
                ? Painter.GetHoverStatistics(this, clickInfo)
                : "";
        }

        internal bool UseFftFilter => m_Samples.InputSamplesPerSecond != 0.0 && m_FftFilterType != FftFilterTypes.None;
        internal bool IsLogarithmicY => m_LogVertical == LogVerticalMode.dB10 || m_LogVertical == LogVerticalMode.dB20;
        internal bool IsLogY => m_LogVertical == LogVerticalMode.Log;
        internal bool IsLogX => m_LogHorizontal == LogHorizontalMode.Log;
        internal bool IsRebasedResult => IsFftTrace && CalculateAfterZoom;
        internal bool IsRecalculateProjectionRequired => m_RecalculateProjectionRequired != 0 || m_AfterZoomCalculateRequired;
        internal bool ProcessAtInput => m_PaintMode == PaintModes.PeakHold;
        internal bool ProcessAtDisplay => m_PaintMode != PaintModes.PeakHold;
        internal bool CanShowRealYT => CanShowYTInner ? Samples.ViewedUnixTime != null : false;
        internal bool CanShowFakeYT => CanShowYTInner ? (Samples.ViewedSamplesPerSecond == 0.0 ? false : Samples.ViewedLeftmostUnixTime != 0.0) : false;
        internal bool IsYtDisplay => Samples.ViewedIsYTTrace && (CanShowRealYT || CanShowFakeYT);
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
        [AutoEditor.DisplayOrder(1)]
        public string SamplesName
        {
            get => Samples.Name;
            set { Samples.Name = value; }
        }

        [AutoEditor.DisplayName("First Sample UnixTime")]
        [AutoEditor.DisplayOrder(4)]
        public double SamplesLeftmostUnixTime
        {
            get => Samples.InputLeftmostUnixTime;
            set { Samples.InputLeftmostUnixTime = value; }
        }

        [AutoEditor.DisplayOrder(6.4)] // horizontal-axis band, next to the affine settings
        [AutoEditor.Tooltip("Seconds axis when > 0 (offset still applies; multiplier is ignored - a rate already sets the scale).\n0 = sample-number or affine axis. Not editable on YT traces.")]
        public double SamplesPerSecond
        {
            get => Samples.InputSamplesPerSecond;
            set { Samples.InputSamplesPerSecond = value; }
        }

        [AutoEditor.DisplayName("Vertical Unit")]
        [AutoEditor.DisplayOrder(2)]
        public string SamplesVerticalUnit
        {
            get => Samples.VerticalUnit;
            set { Samples.VerticalUnit = value; }
        }

        [AutoEditor.DisplayName("Axis Title Left")]
        [AutoEditor.DisplayOrder(2)]
        public string SamplesAxisTitleLeft
        {
            get => Samples.AxisTitleLeft;
            set { Samples.AxisTitleLeft = value; }
        }

        [AutoEditor.DisplayName("Axis Title Bottom")]
        [AutoEditor.DisplayOrder(2, "Axis and Labels")]
        public string SamplesAxisTitleBottom
        {
            get => Samples.AxisTitleBottom;
            set { Samples.AxisTitleBottom = value; }
        }

        [AutoEditor.DisplayName("Display Sample Offset")]
        [AutoEditor.DisplayOrder(6.5)] // horizontal-axis band: shifts sample numbering / the seconds axis
        [AutoEditor.Tooltip("Added to the displayed sample number (and to the seconds axis when Samples Per Second is set)")]
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
                long generation;

                lock (m_Samples.DataLock)
                {
                    m_CachedStatistics = null;
                    generation = m_Samples.SamplesGeneration;
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

                    bool staleSnapshot = false;
                    if (FindTrigger(triggerSamples, ref sampleCount, ref sampleOffset))
                    {
                        projected = GetDrawnSamples(samples, sampleOffset, sampleCount, out drawnStart);
                        if (projected != null)
                        {
                            (var peakMin, var peakMax) = PeakHoldBeforeZoom(samples, sampleOffset, drawnStart, projected.Length);
                            projected = CalculateAfterZoom ? CalculateFft(projected) : projected;
                            // dB conversion for non-FFT traces (FFT path already applies it inside ExecuteFft).
                            // ApplyDbInPlace early-returns if LogVertical isn't a dB mode.
                            if (!IsFftTrace) ApplyDbInPlace(projected);
                            PeakHoldAfterZoom(projected, ref peakMin, ref peakMax);
                            lock (m_Samples.DataLock)
                            {
                                if (m_Samples.SamplesGeneration == generation)
                                {
                                    m_DrawnSamples = projected;
                                    m_CalculatedBeforeZoom = samples;
                                    m_PeakHoldDrawn = new TraceDataPeakHold(peakMin, peakMax);
                                    m_DrawnStartPosition = drawnStart;
                                }
                                else
                                {
                                    staleSnapshot = true;
                                    BeforeZoomCalculateRequired();
                                }
                            }
                        }
                    }

                    RecalculateProjectionRequired();
                    if (staleSnapshot)
                    {
                        Scope.ViewNeedsRepaint(this);
                    }
                }

                if (before || after)
                {
                    //fixme: don't call if the samples didn't actually change (recursive invalidate)
                    m_Samples.ForEachViewer(viewer =>
                    {
                        viewer.TraceDataCalculatedSamplesChanged(m_Samples);
                    });
                }
                if (before)
                {
                    RefreshPlaybackBuffer();
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
                    return (m_CalculatePhase == CalculatePhases.AfterZoom) ? m_DrawnSamples!.Length : m_CalculatedBeforeZoom!.Length;
                default:
                    return 0;
            }
        }

        private double CalculatedNyquist()
        {
            return m_MathType switch
            {
                MathTypes.FFTMagnitude or MathTypes.FFTPhase => m_Samples.InputSamplesPerSecond / (double)m_FftInputBins * (double)(m_FftResultBins - 1),
                _ => 0.0,
            };
        }

        // How this trace defines its horizontal axis, for grouped-trace alignment (GroupHorizontal).
        internal HorizontalKind HorizontalKind =>
            IsFftTrace ? HorizontalKind.Fft
            : IsYtDisplay ? HorizontalKind.Yt
            : m_Samples.HasExplicitHorizontalAxis ? HorizontalKind.Affine
            : m_Samples.InputSamplesPerSecond != 0.0 ? HorizontalKind.Time
            : HorizontalKind.None;

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
                        int num = IsFftTrace ? 0 : Samples.InputSampleNumberDisplayOffset;
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
                        else if (m_Samples.InputSamplesPerSecond != 0.0 || m_Samples.HasExplicitHorizontalAxis)
                        {
                            // Canonical map (TraceData.HorizontalValueAt): seconds when sps is set
                            // (offset composes, multiplier deferred to the rate), else the affine
                            // map. One code path so the extents can never disagree with
                            // FullHorizontalAffine (a units mismatch put the ValueRect off-pane).
                            // rightSampleNumber is one-past-last; num applies the display/view
                            // offsets to both scales.
                            leftSampleNumberValue = m_Samples.HorizontalValueAt(leftSampleNumber + num);
                            rightSampleNumberValue = m_Samples.HorizontalValueAt(rightSampleNumber + num);
                            sampleValueUnit = m_Samples.HorizontalUnitEffective;
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
            return samples.Length == 0 ? new double[0] : IsFftTrace ? ExecuteFft(samples) : samples;
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
                case FftFilterTypes.WeightedAudioA:
                    result = FftFilter.Arbitrary(input,
                        FftFilter.WeightingA,
                        m_Samples.InputSamplesPerSecond);
                    break;
                case FftFilterTypes.WeightedAudioC:
                    result = FftFilter.Arbitrary(input,
                        FftFilter.WeightingC,
                        m_Samples.InputSamplesPerSecond);
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
            // Tukey window to suppress edge-discontinuity leakage.
            SampleWindow.WindowType window = m_PaintMode == PaintModes.Spectral && FftWindow == SampleWindow.WindowType.Rectangular
                ? SampleWindow.WindowType.FrontBackQuarterRaisedCosine
                : FftWindow;
            if (window != SampleWindow.WindowType.Rectangular)
            {
                input = input.ElementProduct(SampleWindow.GenerateWindow(input.Length, window));
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
            ApplyDbInPlace(result);
            return result;
        }

        // 10*log10(|v|) or 20*log10(|v|) in place. Negative-infinity bins are clamped to the
        // minimum finite dB seen so axes don't blow up. Caller must check LogVertical is a dB mode.
        private void ApplyDbInPlace(double[] data)
        {
            if (m_LogVertical != LogVerticalMode.dB10 && m_LogVertical != LogVerticalMode.dB20) return;
            int length = data.Length;
            double min = 0.0;
            double ratio = m_LogVertical == LogVerticalMode.dB20 ? 20.0 : 10.0;
            for (int loop = 0; loop < length; loop++)
            {
                double dB = ratio * Math.Log10(Math.Abs(data[loop]));
                if (double.IsNegativeInfinity(dB))
                {
                    dB = double.NegativeInfinity;
                }
                else
                {
                    min = min < dB ? min : dB;
                }
                data[loop] = dB;
            }
            for (int loop = 0; loop < length; loop++)
            {
                data[loop] = data[loop] == double.NegativeInfinity ? min : data[loop];
            }
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
                    if (m_PaintMode == PaintModes.PeakHold && !CalculateAfterZoom && m_PeakHoldAll != null)
                    {
                        PeakHold(samples, startTrigger, samples.Length - startTrigger);
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
            if (m_PaintMode == PaintModes.PeakHold && CalculateAfterZoom && m_PeakHoldAll != null)
            {
                lock (m_Samples.DataLock) //locked again inside PeakHold
                {
                    PeakHold(drawnSamples, 0, drawnSamples.Length);
                    peakMax = m_PeakHoldAll.Max;
                    peakMin = m_PeakHoldAll.Min;
                }
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

                // X maps against this trace's value sub-window (== ProjectionArea unless value-aligned),
                // so hover/click on a ragged grouped trace resolves to the correct sample. Y stays full-height.
                result.XRatio = (x - traceDivision.ValueRect.Left) / traceDivision.ValueRect.Width;
                result.YRatio = (y - traceDivision.ProjectionArea.Top) / traceDivision.ProjectionArea.Height;
                result.BeyondDrawnData = result.XRatio < 0.0 || result.XRatio > 1.0; // outside the sub-window
                result.XRatio = Math.Min(result.XRatio, 1.0);
                result.XValue = (m_HighestValue - m_LowestValue) * result.XRatio + m_LowestValue;
                result.YValue = m_HighestValue - (m_HighestValue - m_LowestValue) * result.YRatio;

                if (traceDivision.YTTrace)
                {
                    var fineUnixTimeAtX = (double)YTClickToUnixTime(result.XRatio);
                    (result.SampleAtX, result.IndexBeforeTrim, result.UnixTimeAtX) = Samples.ViewedSampleAtUnixTime(fineUnixTimeAtX);
                    double[]? drawnTimes = Samples.ViewedUnixTime;
                    double firstTime;
                    double lastTime;
                    if (drawnTimes != null && drawnTimes.Length > 0)
                    {
                        firstTime = drawnTimes[0];
                        lastTime = drawnTimes[drawnTimes.Length - 1];
                    }
                    else
                    {
                        double sps = Samples.ViewedSamplesPerSecond;
                        firstTime = Samples.ViewedLeftmostUnixTime;
                        lastTime = firstTime + (sps == 0 ? 0.0 : Math.Max(0, Samples.ViewedSampleCount - 1) / sps);
                    }
                    result.BeyondDrawnData =
                        (fineUnixTimeAtX < firstTime && !PadLeftWithFirstValue)
                        || (fineUnixTimeAtX > lastTime && !PadRightWithLastValue);
                }
                else if (m_DrawnSamples != null && m_DrawnSamples.Length > 0 && result.XRatio >= 0.0 && result.XRatio <= 1.0)
                {
                    bool rebased = IsRebasedResult;
                    int length = m_DrawnSamples!.Length;
                    double indexRatio = result.XRatio;
                    if (m_LogHorizontal == LogHorizontalMode.Log)
                    {
                        var ext = DrawnExtents();
                        double leftVal = ext.leftSampleNumberValue;
                        double rightVal = ext.rightSampleNumberValue;
                        if (rightVal > 0 && rightVal > leftVal)
                        {
                            double effectiveLeft = PaintTraceBase.LogHEffectiveLeft(leftVal, rightVal, length);
                            double val = PaintTraceBase.LogHFractionToValue(result.XRatio, effectiveLeft, rightVal);
                            indexRatio = (val - leftVal) / (rightVal - leftVal);
                            indexRatio = Math.Max(0.0, Math.Min(1.0, indexRatio));
                        }
                    }
                    int index = (int)Math.Floor((double)length * indexRatio);
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
            else if (m_Samples.HasExplicitHorizontalAxis)
            {
                return m_Samples.HorizontalValueAt(click.IndexAfterTrim + m_Samples.InputSampleNumberDisplayOffset)
                    .ToStringRound(5, 3, m_Samples.HorizontalUnitEffective);
            }
            else if (Samples.ViewedIsYTTrace)
            {
                return click.UnixTimeAtX.ToStringRound(3, 3); // use ToHorizontalUnit?
            }
            else
            {
                // seconds axis via the canonical map, so the readout includes the axis offset
                return m_Samples.InputSamplesPerSecond != 0.0
                    ? m_Samples.HorizontalValueAt(click.IndexAfterTrim + m_Samples.InputSampleNumberDisplayOffset)
                        .ToStringRound(5, 3, m_Samples.HorizontalUnitEffective)
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

        public static void ShowGroupControlForm(IReadOnlyList<TraceView> views)
        {
            if (views.Count == 0) return;
            if (views.Count == 1)
            {
                views[0].ShowControlForm();
                return;
            }
            using var form = new AutoEditorGroupForm();
            form.ShowDialog(
                prompt: "View configuration",
                title: $"Group of {views.Count} traces",
                columns: views.Select(x => (x.DecoratedName, (object)x)).ToList());
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
            bool hasHorizontal = Samples.InputSamplesPerSecond != 0.0 || Samples.HasExplicitHorizontalAxis;
            var time = hasHorizontal ? $" ({SampleNumberText(Clicks[0])})" : "";
            text.Append($"{ViewName}[{Clicks[0].IndexBeforeTrim}/{Clicks[0].CountBeforeTrim}{time}]");
            text.Append(@"
value=" + string.Format(VerticalUnitFormat, Clicks[0].SampleAtX.ToStringRound(5, 3)));
            var deltaInfo = Clicks[0].ShallowClone();
            deltaInfo.IndexBeforeTrim = Clicks[0].IndexBeforeTrim - Clicks[1].IndexBeforeTrim;
            string delta = $"{deltaInfo.IndexBeforeTrim}{(hasHorizontal ? (" (" + SampleNumberText(deltaInfo) + ")") : "")}";
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
            else if (TryGroupValueWindow(count, out int vStart, out int vCount))
            {
                // Value-aligned group: zoom/pan select a shared value window of the group's full domain
                int sc = Math.Max(1, Math.Min(vCount, count));
                int st = Math.Clamp(vStart, 0, count - sc);
                array = samples.Skip(st).Take(sc).ToArray();
                drawnStart = st;
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

        // This trace's FULL (pre-zoom) horizontal axis as an affine map value = a + b*sampleNumber, plus
        // its kind/unit/value-range, for group value-domain classification. b > 0 for Time/Affine.
        // Must agree with DrawnExtents / TraceData.HorizontalValueAt (same composition rules and the
        // same display/view offsets), or SubWindow places the ValueRect off-pane.
        internal (HorizontalKind kind, string unit, double left, double right, double a, double b) FullHorizontalAffine()
        {
            int fullCount = m_CalculatedBeforeZoom?.Length ?? m_Samples.ViewedSampleCount;
            int num = m_Samples.InputSampleNumberDisplayOffset; // view offset moved the DATA, not the axis
            HorizontalKind kind = HorizontalKind;
            double a, b;
            string unit;
            double offset = double.IsFinite(m_Samples.HorizontalOffset) ? m_Samples.HorizontalOffset : 0.0;
            switch (kind)
            {
                case HorizontalKind.Affine:
                    // offset is in samples: value = b * (sample + offset + num)
                    b = m_Samples.HorizontalMultiplier;
                    a = b * (offset + num);
                    unit = m_Samples.HorizontalAxisUnit;
                    break;
                case HorizontalKind.Time:
                    double sps = m_Samples.InputSamplesPerSecond;
                    b = 1.0 / sps;
                    a = b * (offset + num);
                    unit = m_Samples.HorizontalUnitEffective;
                    break;
                default: // None / Fft / Yt - value == sample number; not used for value-align
                    a = 0.0;
                    b = 1.0;
                    unit = "";
                    break;
            }
            return (kind, unit, a, a + b * fullCount, a, b);
        }

        // If this trace is in a value-aligned group, resolve the shared zoom/pan value window and return
        // this trace's sample slice [vStart, vStart+vCount) covering it. False for stretch/incompatible
        // groups (keep the legacy count-fraction zoom).
        private bool TryGroupValueWindow(int count, out int vStart, out int vCount)
        {
            vStart = 0;
            vCount = count;
            if (count <= 0) return false;
            TraceView[] group = Scope.GroupedTraces(this);
            var members = new List<GroupHorizontal.Member>(group.Length);
            foreach (TraceView v in group)
            {
                if (!v.Visible) continue;
                var f = v.FullHorizontalAffine();
                members.Add(new GroupHorizontal.Member(f.kind, f.unit, f.left, f.right, v.IsLogX));
            }
            if (members.Count == 0) return false;
            GroupHorizontal.Domain window = GroupHorizontal.Window(members, m_ZoomValue, m_PanValue);
            if (window.Mode != HorizontalMode.ValueAlign) return false;
            var self = FullHorizontalAffine();
            if (self.b == 0.0) return false;
            int s = (int)Math.Round((window.Left - self.a) / self.b);
            int e = (int)Math.Round((window.Right - self.a) / self.b);
            if (e < s) (s, e) = (e, s);
            s = Math.Clamp(s, 0, count);
            e = Math.Clamp(e, 0, count);
            vStart = s;
            vCount = Math.Max(1, e - s);
            return true;
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
            if (sourceTraces.Length == 0)
            {
                return new double[0];
            }
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
                    result = transposedMax.Select(x => x.ToArray()).Select(x => x.Sum() / x.Length).ToArray();
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
                    {
                        var resample = (TraceView.CalculatedTraceDataCount)CalculatedParameter;
                        double srcSps = CalculatedSourceViews[0].Samples.InputSamplesPerSecond;
                        int newLength;
                        if (resample.SamplesPerSecond > 0 && srcSps > 0)
                        {
                            newLength = (int)Math.Round(sourceTraces[0].Length * resample.SamplesPerSecond / srcSps);
                            m_Samples.InputSamplesPerSecond = resample.SamplesPerSecond;
                        }
                        else
                        {
                            newLength = resample.Count;
                        }
                        result = sourceTraces[0].Resample(Math.Max(1, newLength));
                    }
                    break;
            }
            return result;
        }

        ////////////////////////////////////////////////////////////////
        //other

        private void PeakHold(double[] samples, int start, int count)
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
            StopPlayback();
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

    // Tests for the per-kind axis probe and the value-window zoom/pan slice of value-aligned groups.
    [TestClass]
    public class TraceViewHorizontalTests
    {
        [TestMethod]
        public void FullHorizontalAffinePerKind()
        {
            var scope = new SehensControl();

            TraceView affine = SehensTestHarness.AffineTrace(scope, "affine", count: 5, offset: 100, multiplier: 10, unit: "rpm");
            affine.CalculateTrace();
            var fa = affine.FullHorizontalAffine();
            Assert.AreEqual(HorizontalKind.Affine, fa.kind);
            Assert.AreEqual("rpm", fa.unit);
            Assert.AreEqual(1000.0, fa.a, 1e-9); // offset is in samples: 10 * (0 + 100)
            Assert.AreEqual(10.0, fa.b, 1e-9);
            Assert.AreEqual(1000.0, fa.left, 1e-9);
            Assert.AreEqual(1050.0, fa.right, 1e-9); // a + b * count (one past last sample)

            scope["time"].Update(SehensTestHarness.Ramp(50));
            scope["time"].InputSamplesPerSecond = 10.0;
            TraceView time = SehensTestHarness.View(scope, "time");
            time.CalculateTrace();
            var ft = time.FullHorizontalAffine();
            Assert.AreEqual(HorizontalKind.Time, ft.kind);
            Assert.AreEqual("s", ft.unit);
            Assert.AreEqual(0.0, ft.left, 1e-9);
            Assert.AreEqual(5.0, ft.right, 1e-9);
            Assert.AreEqual(0.1, ft.b, 1e-9);

            scope["time"].InputSampleNumberDisplayOffset = 20; // 2 s display offset
            var fo = time.FullHorizontalAffine();
            Assert.AreEqual(2.0, fo.a, 1e-9);
            Assert.AreEqual(2.0, fo.left, 1e-9);
            Assert.AreEqual(7.0, fo.right, 1e-9);

            scope["plain"].Update(SehensTestHarness.Ramp(100));
            TraceView plain = SehensTestHarness.View(scope, "plain");
            plain.CalculateTrace();
            var fp = plain.FullHorizontalAffine();
            Assert.AreEqual(HorizontalKind.None, fp.kind);
            Assert.AreEqual(0.0, fp.left, 1e-9);
            Assert.AreEqual(100.0, fp.right, 1e-9);

            // invalid multiplier -> unusable axis -> classifies as plain index (None), not Affine
            scope["bad"].Update(SehensTestHarness.Ramp(10));
            scope["bad"].SetHorizontalAffine(0.0, 0.0, "rpm");
            Assert.AreEqual(HorizontalKind.None, SehensTestHarness.View(scope, "bad").HorizontalKind);
        }

        [TestMethod]
        public void ValueAlignZoomWindowSlicesByValue()
        {
            var scope = new SehensControl();
            TraceView a = SehensTestHarness.AffineTrace(scope, "A", count: 100, offset: 0, multiplier: 1, unit: "u");  // 0..100
            TraceView b = SehensTestHarness.AffineTrace(scope, "B", count: 50, offset: 50, multiplier: 1, unit: "u");  // 50..100
            scope.GroupViews(new[] { "A", "B" });
            SehensTestHarness.ZoomPan(scope, zoom: 0.5, pan: 0.25); // shared value window [25, 75]
            SehensTestHarness.Layout(scope);

            Assert.AreEqual(25, a.DrawnStartPosition);
            Assert.AreEqual(50, a.DrawnSamples!.Length);
            // B has no data before 50: it draws only its 50..75 slice instead of sliding (legacy
            // count-fraction zoom would show a different value range per member)
            Assert.AreEqual(0, b.DrawnStartPosition);
            Assert.AreEqual(25, b.DrawnSamples!.Length);
            Assert.AreEqual(75.0, b.Samples.HorizontalValueAt(b.DrawnStartPosition + b.DrawnSamples!.Length), 1e-9);
        }

        [TestMethod]
        public void ValueAlignPanKeepsMembersOnTheSameWindow()
        {
            var scope = new SehensControl();
            TraceView a = SehensTestHarness.AffineTrace(scope, "A", count: 100, offset: 0, multiplier: 1, unit: "u");
            TraceView b = SehensTestHarness.AffineTrace(scope, "B", count: 50, offset: 50, multiplier: 1, unit: "u");
            scope.GroupViews(new[] { "A", "B" });
            SehensTestHarness.ZoomPan(scope, zoom: 0.5, pan: 0.5); // window [50, 100]
            SehensTestHarness.Layout(scope);

            // both members show exactly the values 50..100
            Assert.AreEqual(50.0, a.Samples.HorizontalValueAt(a.DrawnStartPosition), 1e-9);
            Assert.AreEqual(100.0, a.Samples.HorizontalValueAt(a.DrawnStartPosition + a.DrawnSamples!.Length), 1e-9);
            Assert.AreEqual(50.0, b.Samples.HorizontalValueAt(b.DrawnStartPosition), 1e-9);
            Assert.AreEqual(100.0, b.Samples.HorizontalValueAt(b.DrawnStartPosition + b.DrawnSamples!.Length), 1e-9);
        }

        [TestMethod]
        public void StretchZoomKeepsLegacyCountFractionBehaviour()
        {
            var scope = new SehensControl();
            scope["plain"].Update(SehensTestHarness.Ramp(100)); // no axis -> Stretch -> legacy branch
            TraceView view = SehensTestHarness.View(scope, "plain");

            SehensTestHarness.ZoomPan(scope, zoom: 0.5, pan: 0.0);
            SehensTestHarness.Layout(scope);
            Assert.AreEqual(0, view.DrawnStartPosition);
            Assert.AreEqual(50, view.DrawnSamples!.Length);

            SehensTestHarness.ZoomPan(scope, zoom: 0.5, pan: 0.5);
            SehensTestHarness.Layout(scope);
            Assert.AreEqual(50, view.DrawnStartPosition);
            Assert.AreEqual(50, view.DrawnSamples!.Length);
        }

        [TestMethod]
        public void GutterWindowMatchesDrawnWindow()
        {
            // The gutter domain comes from TraceGroupDisplay (Painted.Group + scope zoom) while the
            // drawn slice comes from TryGroupValueWindow (Scope.GroupedTraces + view zoom). If those
            // two ever diverge, ticks and curves silently disagree - pin them to each other.
            var scope = new SehensControl();
            TraceView a = SehensTestHarness.AffineTrace(scope, "A", count: 100, offset: 0, multiplier: 1, unit: "u");
            TraceView b = SehensTestHarness.AffineTrace(scope, "B", count: 50, offset: 50, multiplier: 1, unit: "u");
            scope.GroupViews(new[] { "A", "B" });
            SehensTestHarness.ZoomPan(scope, zoom: 0.5, pan: 0.25); // window [25, 75]
            SehensTestHarness.Layout(scope);

            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(a);
            Assert.AreEqual(HorizontalMode.ValueAlign, info.HMode);
            Assert.AreEqual(25.0, info.GroupHLeft, 1e-9);
            Assert.AreEqual(75.0, info.GroupHRight, 1e-9);
            // gutter endpoints == the value range A actually drew
            Assert.AreEqual(info.GroupHLeft, a.Samples.HorizontalValueAt(a.DrawnStartPosition), 1e-9);
            Assert.AreEqual(info.GroupHRight, a.Samples.HorizontalValueAt(a.DrawnStartPosition + a.DrawnSamples!.Length), 1e-9);
        }

        [TestMethod]
        public void AffineEditorProxiesDriveTheTraceAxis()
        {
            // the double-click trace editor binds these TraceView proxies (TraceData is hidden);
            // there is no enable toggle - typed values take effect immediately, identity == off
            var scope = new SehensControl();
            scope["t"].Update(SehensTestHarness.Ramp(10));
            TraceView view = SehensTestHarness.View(scope, "t");
            Assert.IsFalse(view.Samples.HasExplicitHorizontalAxis);

            view.HorizontalAxisOffset = 1000.0; // in samples
            view.HorizontalAxisMultiplier = 7.0;
            view.HorizontalAxisUnit = "f";
            Assert.AreEqual(7021.0, view.Samples.HorizontalValueAt(3), 1e-9); // 7 * (3 + 1000)
            Assert.AreEqual("f", view.Samples.HorizontalAxisUnit);

            view.HorizontalAxisMultiplier = -1.0; // kept as typed, flagged, warning paints
            Assert.AreEqual(-1.0, view.HorizontalAxisMultiplier, 1e-9);
            Assert.IsTrue(view.Samples.HorizontalAffineInvalid);
            Assert.IsFalse(view.Samples.HasExplicitHorizontalAxis);
            view.HorizontalAxisMultiplier = 7.0;

            view.SamplesPerSecond = 10.0; // rate takes the scale; the sample offset still applies
            Assert.AreEqual(100.3, view.Samples.HorizontalValueAt(3), 1e-9); // (3 + 1000) / 10
            Assert.IsFalse(view.Samples.HorizontalAffineInvalid); // ignored multiplier is not an error

            view.SamplesPerSecond = 0.0; // back to the affine scale
            Assert.AreEqual(7021.0, view.Samples.HorizontalValueAt(3), 1e-9);

            view.HorizontalAxisOffset = 0.0; // typing the identity turns the axis off
            view.HorizontalAxisMultiplier = 1.0;
            view.HorizontalAxisUnit = "";
            Assert.IsFalse(view.Samples.HasExplicitHorizontalAxis);
            Assert.AreEqual(3.0, view.Samples.HorizontalValueAt(3), 1e-9);
        }

        [TestMethod]
        public void SpsWinsTheScaleOffsetComposesAndTheTraceStaysVisible()
        {
            // Field report: sps > 0 plus an affine offset made the trace vanish (the group domain
            // and DrawnExtents used different units, pushing the ValueRect off-pane). Composition
            // now: sps supplies the scale, the offset shifts it, the multiplier waits for sps == 0
            // - and extents/FullHorizontalAffine share one canonical map so they cannot diverge.
            var scope = new SehensControl();
            scope["both"].Update(SehensTestHarness.Ramp(100));
            scope["both"].InputSamplesPerSecond = 10.0;
            scope["both"].SetHorizontalAffine(1000.0, 7.0, "f");
            SehensTestHarness.Layout(scope);
            TraceView view = SehensTestHarness.View(scope, "both");

            Assert.AreEqual(HorizontalKind.Time, view.HorizontalKind);
            var ext = view.DrawnExtents();
            Assert.AreEqual(100.0, ext.leftSampleNumberValue, 1e-9);  // (0 + 1000) / 10
            Assert.AreEqual(110.0, ext.rightSampleNumberValue, 1e-9); // (100 + 1000) / 10, mult idle
            Assert.AreEqual("f", ext.sampleValueUnit); // explicit unit beats the "s" default

            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(view);
            Assert.AreEqual(HorizontalMode.ValueAlign, info.HMode);
            Assert.AreEqual(info.ProjectionArea, info.ValueRect); // ON pane - the trace draws
            Assert.AreEqual(100.0, info.GroupHLeft, 1e-9);
            Assert.AreEqual(110.0, info.GroupHRight, 1e-9);

            scope["both"].InputSamplesPerSecond = 0.0; // rate removed: the multiplier takes over
            SehensTestHarness.Layout(scope);
            var affine = view.DrawnExtents();
            Assert.AreEqual(HorizontalKind.Affine, view.HorizontalKind);
            Assert.AreEqual(7700.0, affine.rightSampleNumberValue, 1e-9); // 7 * (100 + 1000)
            Assert.AreEqual("f", affine.sampleValueUnit);
        }

        [TestMethod]
        public void FftAxisDerivesHzFromSps()
        {
            // sps is more than the seconds axis: an FFT trace derives its Hz axis from it
            // (CalculatedNyquist), and the affine terms must not bend that axis.
            const double sps = 8000.0;
            const int n = 1024;
            const int targetBin = 128; // 1000 Hz, bin-aligned so the peak has no leakage
            var tone = new SehensWerte.Generators.ToneGenerator
            {
                SamplesPerSecond = sps,
                FrequencyStart = targetBin * sps / n,
                FrequencyEnd = targetBin * sps / n,
                Amplitude = 1.0,
            };
            var scope = new SehensControl();
            scope["fft"].Update(tone.Generate(n));
            scope["fft"].InputSamplesPerSecond = sps;
            TraceView view = SehensTestHarness.View(scope, "fft");
            view.MathType = TraceView.MathTypes.FFTMagnitude;
            SehensTestHarness.Layout(scope);

            Assert.AreEqual(HorizontalKind.Fft, view.HorizontalKind);
            var ext = view.DrawnExtents();
            Assert.AreEqual("Hz", ext.sampleValueUnit);
            double nyquist = sps / 2.0;
            Assert.AreEqual(0.0, ext.leftSampleNumberValue, 1e-9);
            Assert.AreEqual(nyquist, ext.rightSampleNumberValue, nyquist * 0.01, "right edge ~ Nyquist from sps");

            // the drawn spectrum peaks at the tone's frequency on that Hz axis
            double[] drawn = view.DrawnSamples ?? throw new AssertFailedException("no drawn FFT");
            int peak = 1;
            for (int loop = 1; loop < drawn.Length; loop++)
            {
                if (drawn[loop] > drawn[peak]) peak = loop;
            }
            double binWidth = sps / n;
            double peakHz = peak * nyquist / (drawn.Length - 1);
            Assert.AreEqual(1000.0, peakHz, binWidth * 1.5, $"peak at {peakHz:0.0} Hz");

            // affine terms are ignored on an FFT trace - the Hz axis must not move
            scope["fft"].SetHorizontalAffine(5.0, 2.0, "rpm");
            Assert.AreEqual(HorizontalKind.Fft, view.HorizontalKind);
            var ext2 = view.DrawnExtents();
            Assert.AreEqual("Hz", ext2.sampleValueUnit);
            Assert.AreEqual(ext.rightSampleNumberValue, ext2.rightSampleNumberValue, 1e-9);
        }

        [TestMethod]
        public void FftAxisIsExactForOddAndPrimeWindowSizes()
        {
            // Real traces have arbitrary lengths (the demo noise generator makes ~250k-sample odd
            // sizes) and ExecuteFft runs FFTW at the EXACT input length - no padding. For odd N
            // there is no bin at Nyquist: the axis right edge is bins*sps/N (one-past-last) and
            // bin k must still read k*sps/N Hz, whatever the size.
            const double sps = 8000.0;
            foreach (int n in new[] { 1024, 1000, 999, 997, 513, 4095 })
            {
                int targetBin = Math.Max(1, n / 8);
                double toneHz = targetBin * sps / n; // bin-aligned on THIS size's grid
                var tone = new SehensWerte.Generators.ToneGenerator
                {
                    SamplesPerSecond = sps,
                    FrequencyStart = toneHz,
                    FrequencyEnd = toneHz,
                    Amplitude = 1.0,
                };
                var scope = new SehensControl();
                scope["fft"].Update(tone.Generate(n));
                scope["fft"].InputSamplesPerSecond = sps;
                TraceView view = SehensTestHarness.View(scope, "fft");
                view.MathType = TraceView.MathTypes.FFTMagnitude;
                SehensTestHarness.Layout(scope);

                double[] drawn = view.DrawnSamples ?? throw new AssertFailedException($"no FFT for N={n}");
                int bins = Fftw.SampleCountToBinCount(n);
                Assert.AreEqual(bins, drawn.Length, $"bin count for N={n}");

                var ext = view.DrawnExtents();
                Assert.AreEqual("Hz", ext.sampleValueUnit);
                double binWidth = sps / n;
                Assert.AreEqual(bins * binWidth, ext.rightSampleNumberValue, binWidth * 1e-6,
                    $"right edge for N={n} (== Nyquist only when N is even)");

                int peak = 1;
                for (int loop = 1; loop < drawn.Length; loop++)
                {
                    if (drawn[loop] > drawn[peak]) peak = loop;
                }
                double peakHz = peak * ext.rightSampleNumberValue / drawn.Length;
                Assert.AreEqual(toneHz, peakHz, binWidth * 1.5, $"peak Hz for N={n}");
            }
        }

        [TestMethod]
        public void FakeYtUsesSpsForTheTimeRangeAndSkipsValueAlign()
        {
            // sps + a nonzero start time make a fake-YT trace: the time range comes from sps, and
            // the YT path must bypass the value-align machinery entirely.
            var scope = new SehensControl();
            scope["yt"].Update(SehensTestHarness.Ramp(100));
            scope["yt"].InputSamplesPerSecond = 10.0;
            scope["yt"].InputLeftmostUnixTime = 1000.0;
            TraceView view = SehensTestHarness.View(scope, "yt");
            view.PaintMode = TraceView.PaintModes.PolygonDigital; // fake-YT needs a plain paint mode
            SehensTestHarness.Layout(scope);

            Assert.IsTrue(view.CanShowFakeYT);
            TraceData.TimeRange range = TraceView.GetGroupUnixTimeRange(new[] { view });
            Assert.AreEqual(1000.0, range.Left, 1e-9);
            Assert.AreEqual(1000.0 + 99 / 10.0, range.Right, 1e-9); // (count-1)/sps from the start time

            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(view);
            Assert.IsTrue(info.YTTrace);
            Assert.AreEqual(HorizontalMode.Stretch, info.HMode, "YT traces bypass value alignment");
            Assert.AreEqual(info.ProjectionArea, info.ValueRect);
            var ext = view.DrawnExtents();
            Assert.AreEqual(1000.0, ext.leftUnixTime, 0.1);
            Assert.AreEqual(1009.9, ext.rightUnixTime, 0.1);
        }

        [TestMethod]
        public void PanClampsToTheVisibleWindow()
        {
            // pan is the LEFT edge fraction, ceiling 1 - zoom: the drag path calls SetZoomPan
            // directly, and a [0,1] clamp let a drag over-pan past the data then snap back on
            // release when the scrollbar path re-clamped.
            var scope = new SehensControl();
            scope.SetZoomPan(0.5, 0.9);
            Assert.AreEqual(0.5, scope.PanValue, 1e-9);
            scope.SetZoomPan(1.0, 0.3); // full view: no pan headroom at all
            Assert.AreEqual(0.0, scope.PanValue, 1e-9);
        }

        [TestMethod]
        public void HoverStatsSuppressedOutsideTheDrawnData()
        {
            // Field report: hovering right of a cropped member's data still showed its clamped
            // last sample. A hover outside the trace's value sub-window must produce no stat.
            var scope = new SehensControl();
            SehensTestHarness.AffineTrace(scope, "full", count: 100, offset: 0, multiplier: 1, unit: "u"); // 0..100
            SehensTestHarness.AffineTrace(scope, "half", count: 50, offset: 0, multiplier: 1, unit: "u");  // 0..50, left half
            scope.GroupViews(new[] { "full", "half" });
            SehensTestHarness.Layout(scope);
            TraceView half = SehensTestHarness.View(scope, "half");
            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(half);
            int yMid = info.ProjectionArea.Top + info.ProjectionArea.Height / 2;

            int xInside = info.ValueRect.Left + info.ValueRect.Width / 2;
            TraceView.MouseInfo inside = half.Measure(new MouseEventArgs(MouseButtons.Left, 0, xInside, yMid, 0));
            Assert.IsFalse(inside.BeyondDrawnData);
            Assert.AreNotEqual("", half.TraceHoverStatistics(inside));

            int xBeyond = info.ValueRect.Right + info.ValueRect.Width / 2; // over "full"'s data only
            TraceView.MouseInfo beyond = half.Measure(new MouseEventArgs(MouseButtons.Left, 0, xBeyond, yMid, 0));
            Assert.IsTrue(beyond.BeyondDrawnData);
            Assert.AreEqual("", half.TraceHoverStatistics(beyond));
        }

        [TestMethod]
        public void YtHoverSuppressedBeforeTheDataUnlessPadded()
        {
            // Field report: hovering before a late-starting fake-YT member's data showed index
            // [-302] with value 0. Outside the trace's time extent there is no stat - unless the
            // pad flag covers that side (the pad paints a flat line there, so the hover reads the
            // held edge sample).
            var scope = new SehensControl();
            scope["early"].Update(SehensTestHarness.Ramp(1000), 100.0); // 0..10 s
            scope["early"].InputLeftmostUnixTime = 1_700_000_000;
            scope["late"].Update(SehensTestHarness.Ramp(600), 100.0);   // 5..11 s
            scope["late"].InputLeftmostUnixTime = 1_700_000_005;
            scope.GroupViews(new[] { "early", "late" });
            TraceView late = SehensTestHarness.View(scope, "late");
            late.PaintMode = TraceView.PaintModes.PolygonDigital;
            SehensTestHarness.View(scope, "early").PaintMode = TraceView.PaintModes.PolygonDigital;
            SehensTestHarness.Layout(scope);

            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(late);
            Assert.IsTrue(info.YTTrace);
            int yMid = info.ProjectionArea.Top + info.ProjectionArea.Height / 2;
            int xBeforeLate = info.ProjectionArea.Left + info.ProjectionArea.Width / 10; // ~1 s: early only

            TraceView.MouseInfo beyond = late.Measure(new MouseEventArgs(MouseButtons.Left, 0, xBeforeLate, yMid, 0));
            Assert.IsTrue(beyond.BeyondDrawnData);
            Assert.AreEqual("", late.TraceHoverStatistics(beyond));

            late.PadLeftWithFirstValue = true; // the pad paints there, so the hover follows
            TraceView.MouseInfo padded = late.Measure(new MouseEventArgs(MouseButtons.Left, 0, xBeforeLate, yMid, 0));
            Assert.IsFalse(padded.BeyondDrawnData);
            Assert.AreEqual(0, padded.IndexBeforeTrim); // clamped to the first sample, not [-302]
            Assert.AreEqual(1_700_000_005.0, padded.UnixTimeAtX, 1e-6); // index/sps, not index*sps
            Assert.AreNotEqual("", late.TraceHoverStatistics(padded));
        }

        [TestMethod]
        public void GapSampleHoverIsSuppressed()
        {
            var scope = new SehensControl();
            const int count = 1000;
            double[] samples = new double[count];
            for (int loop = 0; loop < count; loop++)
            {
                samples[loop] = loop < count / 2 ? double.NaN : 1.0;
            }
            scope["gappy"].Update(samples);
            SehensTestHarness.Layout(scope);
            TraceView view = SehensTestHarness.View(scope, "gappy");

            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(view);
            int yMid = info.ProjectionArea.Top + info.ProjectionArea.Height / 2;
            int xGap = info.ProjectionArea.Left + info.ProjectionArea.Width / 4;
            TraceView.MouseInfo gap = view.Measure(new MouseEventArgs(MouseButtons.Left, 0, xGap, yMid, 0));
            Assert.IsFalse(double.IsFinite(gap.SampleAtX));
            Assert.AreEqual("", view.TraceHoverStatistics(gap), "no hover label over a gap");

            int xValid = info.ProjectionArea.Left + info.ProjectionArea.Width * 3 / 4;
            TraceView.MouseInfo valid = view.Measure(new MouseEventArgs(MouseButtons.Left, 0, xValid, yMid, 0));
            Assert.AreNotEqual("", view.TraceHoverStatistics(valid), "real samples still hover");
        }

        [TestMethod]
        public void CalculatedViewWithoutSourcesDoesNotThrow()
        {
            // Field report: mouse-wheeling over the Calculate combo in the group editor set a
            // CalculateType on a view with no source views; ExecuteCalculate's Min() over the
            // empty source list threw InvalidOperationException on the paint thread.
            var scope = new SehensControl();
            scope["calc"].Update(SehensTestHarness.Ramp(100));
            TraceView view = SehensTestHarness.View(scope, "calc");
            view.CalculateType = TraceView.CalculatedTypes.Sum; // no CalculatedSourceViews yet
            SehensTestHarness.Layout(scope); // runs CalculateTrace -> ExecuteCalculate
            Assert.AreEqual(0, view.ExecuteCalculate().Length);
        }

        [TestMethod]
        public void ViewOverridesMoveTrimAndPadTheData()
        {
            // View offset/length reshape the RAW samples and the axis must NOT follow the move:
            // offset N puts source[N] at drawn index 0 (axis still starts at its own origin),
            // negative offset pads left, length trims or extends (pads right).
            var scope = new SehensControl();
            scope["v"].Update(SehensTestHarness.Ramp(5000));
            TraceView view = SehensTestHarness.View(scope, "v");

            view.ViewOffsetOverride = 1000; // move: source[1000] becomes drawn index 0
            SehensTestHarness.Layout(scope);
            Assert.AreEqual(1000.0, view.DrawnSamples![0], 1e-9);
            Assert.AreEqual(0.0, view.DrawnExtents().leftSampleNumberValue, 1e-9); // axis stays put

            view.ViewOffsetOverride = -1000; // negative: pad left
            SehensTestHarness.Layout(scope);
            Assert.AreEqual(5000, view.DrawnSamples!.Length);
            Assert.AreEqual(0.0, view.DrawnSamples![0], 1e-9);      // padding
            Assert.AreEqual(500.0, view.DrawnSamples![1500], 1e-9); // source[500] at index 1500

            view.ViewOffsetOverride = 0;
            view.ViewLengthOverride = 8000; // extend: pad right
            SehensTestHarness.Layout(scope);
            Assert.AreEqual(8000, view.DrawnSamples!.Length);
            Assert.AreEqual(0.0, view.DrawnSamples![7999], 1e-9);
            Assert.AreEqual(8000.0, view.DrawnExtents().rightSampleNumberValue, 1e-9); // domain grows

            view.ViewOffsetOverride = 100; // trim + move on an affine axis: reads from its origin
            view.ViewLengthOverride = 250;
            view.Samples.SetHorizontalAffine(0, 10, "rpm");
            SehensTestHarness.Layout(scope);
            Assert.AreEqual(100.0, view.DrawnSamples![0], 1e-9); // source[100] at index 0
            var ext = view.DrawnExtents();
            Assert.AreEqual(0.0, ext.leftSampleNumberValue, 1e-9);
            Assert.AreEqual(2500.0, ext.rightSampleNumberValue, 1e-9); // 10 * 250 drawn samples
        }

        [TestMethod]
        public void ViewLengthChangeInvalidatesTheGroupProjection()
        {
            // Field report: changing View Length did not recalculate the horizontal axis until a
            // zoom nudge - the setter recalculated the drawn samples but never invalidated the
            // (extent-keyed) projection cache.
            var scope = new SehensControl();
            TraceView a = SehensTestHarness.AffineTrace(scope, "A", count: 100, offset: 0, multiplier: 1, unit: "u");
            TraceView b = SehensTestHarness.AffineTrace(scope, "B", count: 100, offset: 0, multiplier: 1, unit: "u");
            scope.GroupViews(new[] { "A", "B" });
            SehensTestHarness.Layout(scope);
            a.SnapshotProjection(); // consume the initial recalculate flags
            b.SnapshotProjection();

            a.ViewLengthOverride = 50; // crops A: extents move, the whole group must reproject
            Assert.IsTrue(a.SnapshotProjection().recalculate, "changed view must reproject");
            Assert.IsTrue(b.SnapshotProjection().recalculate, "group sibling must reproject too");
        }
    }
}

