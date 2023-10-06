using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Controls.Sehens;
using SehensWerte.Files;
using SehensWerte.Maths;
using SehensWerte.Utils;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Text;

namespace SehensWerte.Controls
{
    public partial class SehensControl : UserControl
    {
        ////////////////////////////////////////////////////////////////
        //UI

        internal Action<CsvLog.Entry>? OnLog;
        public Action<SehensControl>? OnViewListChanged;
        internal PaintBoxMouseInfo PaintBoxMouse = new PaintBoxMouseInfo();
        private Control? FocusRestore;
        private ViewProxy ViewProxyCallback;

        private HScrollBar HorizontalScrollZoomBar;
        private HScrollBar HorizontalScrollPanBar;
        private SplitContainer PaintBoxScrollBarContainer;
        private Panel PanelLeft;
        internal SehensPaintBox PaintBox;
        internal VScrollBar VerticalScrollBar;
        private SplitContainer LeftRightSplit;
        private TraceListControl TraceListView;
        private ToolTip m_ToolTip;

        private bool m_SplitterHold;
        private bool m_SplitterWasVisible;

        internal bool m_HoldZoomPan;
        internal double m_ZoomBarValue;
        internal double m_PanBarValue;
        internal int m_VerticalScrollbarChanged = 0;
        internal double m_TraceHeightRatio;
        internal double m_ZoomValue = 1.0;
        internal double m_PanValue;
        internal const double ZoomExp = 200.0;

        internal int m_BackgroundThreadCount;

        public double ZoomValue { get => m_ZoomValue; set { m_ZoomValue = value; Invalidate(); } }

        public double PanValue { get => m_PanValue; set { m_PanValue = value; Invalidate(); } }

        private bool m_ShowPaintStats;
        public bool PaintBoxRateLimitedRefresh { get => m_RateLimitedRefresh; set => m_RateLimitedRefresh = value; }

        private bool m_RateLimitedRefresh;
        public bool PaintBoxShowStats { get => m_ShowPaintStats; set => m_ShowPaintStats = value; }

        private Skin m_ScreenshotSkin = new Skin(Skin.CannedSkins.ScreenShot);
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Skin ScreenshotSkin { get => m_ScreenshotSkin; set => m_ScreenshotSkin = value; }

        //serialisd by SehensSave
        private Skin m_ActiveSkin = new Skin(Skin.CannedSkins.Clean);
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Skin ActiveSkin { get => m_ActiveSkin; set { m_ActiveSkin = value; RecalculateProjection(); } }

        public bool HighQualityRender
        {
            get => ActiveSkin.HighQualityRender;
            set { ActiveSkin.HighQualityRender = value; Invalidate(); }
        }

        private bool m_SimpleUi;
        [XmlSave]
        public bool SimpleUi
        {
            get => m_SimpleUi;
            set { m_SimpleUi = value; PaintBox.ContextMenuStrip = m_SimpleUi ? null : ContextMenu.ScopeMenuStrip; }
        }

        public Skin.TraceStatistics ShowTraceStatistics
        {
            get => ActiveSkin.TraceStats;
            set { ActiveSkin.TraceStats = value; RecalculateProjection(); }
        }

        public bool ShowTraceFeatures
        {
            get => ActiveSkin.ShowTraceFeatures;
            set { ActiveSkin.ShowTraceFeatures = value; PaintBox.InvalidateDelayed(); }
        }

        private static bool m_ShowTraceContextLabels = true;
        [XmlSave]
        public bool ShowTraceContextLabels
        {
            get => m_ShowTraceContextLabels;
            set { m_ShowTraceContextLabels = value; PaintBox.InvalidateDelayed(); }
        }

        public Skin.TraceLabels ShowTraceLabels
        {
            get => ActiveSkin.TraceLabel;
            set { ActiveSkin.TraceLabel = value; RecalculateProjection(); }
        }

        public bool ShowAxisLabels
        {
            get => ActiveSkin.ShowAxisLabels;
            set { ActiveSkin.ShowAxisLabels = value; RecalculateProjection(); }
        }

        private bool m_ShowHoverInfo = true;
        [XmlSave]
        public bool ShowHoverInfo
        {
            get => m_ShowHoverInfo;
            set { m_ShowHoverInfo = value; PaintBox.InvalidateDelayed(); }
        }

        private bool m_ShowHoverValue = true;
        [XmlSave]
        public bool ShowHoverValue
        {
            get { return m_ShowHoverValue; }
            set { m_ShowHoverValue = value; PaintBox.InvalidateDelayed(); }
        }

        public List<TraceView[]> VisibleViewGroups
        {
            get
            {
                lock (m_ViewGroupsLock)
                {
                    return m_ViewGroups
                        .Select(x => x.Where(y => y.Visible).ToArray())
                        .Where(x => x.Length > 0)
                        .ToList();
                }
            }
        }

        //serialisd by SehensSave
        public List<String[]> AllViewGroupNames
        {
            get
            {
                lock (m_ViewGroupsLock)
                {
                    return m_ViewGroups
                        .Select(x => x.Select(x => x.ViewName).ToArray())
                        .Where(x => x.Length > 0)
                        .ToList();
                }
            }
        }

        public TraceView[] AllViews
        {
            get
            {
                lock (m_ViewGroupsLock)
                {
                    return m_ViewGroups.SelectMany(x => x).ToArray();
                }
            }
        }

        public TraceView[] VisibleViews => AllViews.Where((TraceView x) => x.Visible).ToArray();

        public TraceView[] InvisibleViews => AllViews.Where((TraceView x) => !x.Visible).ToArray();

        public TraceData[] AllTraces
        {
            get
            {
                lock (m_TraceDataLock)
                {
                    return m_Traces.Select(x => x.Value).ToArray();
                }
            }
        }

        public TraceData this[string s] => EnsureTrace(s);

        public Color ForegroundColour
        {
            get => ActiveSkin.ForegroundColour;
            set { ActiveSkin.ForegroundColour = value; Invalidate(); }
        }

        public Color BackgroundColour
        {
            get => ActiveSkin.BackgroundColour;
            set { ActiveSkin.BackgroundColour = value; Invalidate(); }
        }

        public Skin.Cursors CursorMode
        {
            get => ActiveSkin.CursorMode;
            set { ActiveSkin.CursorMode = value; UpdateMouseCursor(); }
        }

        [XmlSave]
        public bool ScopeBoxZoomPanBarsVisible
        {
            get => PaintBoxScrollBarContainer.Visible;
            set => PaintBoxScrollBarContainer.Visible = value;
        }

        public Color HoverLabelColour { get => ActiveSkin.HoverLabelColour; set => ActiveSkin.HoverLabelColour = value; }

        private bool m_TraceAutoRange = true;
        [XmlSave]
        public bool TraceAutoRange { get => m_TraceAutoRange; set => m_TraceAutoRange = value; }

        public bool StopUpdates
        {
            get => AllTraces.Any(x => x.StopUpdates);
            set
            {
                TraceData[] traceList = AllTraces;
                for (int i = 0; i < traceList.Length; i++)
                {
                    traceList[i].StopUpdates = value;
                }
            }
        }

        [XmlSave]
        public bool TraceListVisible
        {
            get => !LeftRightSplit.Panel1Collapsed;
            set
            {
                if (value)
                {
                    LeftRightSplit.Panel1Collapsed = false;
                    LeftRightSplit.IsSplitterFixed = false;
                    LeftRightSplit.Panel1.Show();
                    LeftRightSplit.Panel1MinSize = 0;
                    LeftRightSplit.SplitterDistance = 150;
                }
                else
                {
                    LeftRightSplit.Panel1Collapsed = true;
                    LeftRightSplit.IsSplitterFixed = true;
                    LeftRightSplit.Panel1.Hide();
                    LeftRightSplit.Panel1MinSize = 0;
                    LeftRightSplit.SplitterDistance = 150;
                }
            }
        }

        internal void SetVerticalZoom(int anchorDivision, double ratio)
        {
            int before = PaintBox.DefaultGroupHeight;
            m_TraceHeightRatio = Math.Min(1.0, Math.Max(0.0, ratio));
            int after = PaintBox.DefaultGroupHeight;

            m_VerticalScrollbarChanged = 1;
            UpdateVerticalScrollbar();

            VerticalScrollBar.Value =
                Math.Min(
                    Math.Max(0, VerticalScrollBar.Maximum - PaintBox.Height),
                    Math.Max(VerticalScrollBar.Minimum, VerticalScrollBar.Value + (after - before) * anchorDivision));
            PaintBox.InvalidateDelayed();
        }

        internal void UpdateVerticalScrollbar()
        {
            if (base.InvokeRequired) return;
            if (Interlocked.Exchange(ref m_VerticalScrollbarChanged, 0) == 0) return;

            int count;
            if (PaintBox.PaintedTraces.VisibleTraceGroupList != null
                && m_ViewGroups.Count != 0
                && PaintBox.PaintedTraces.VisibleTraceGroupList.Count != 0)
            {
                count = PaintBox.PaintedTraces.VisibleTraceGroupList.Count;
            }
            else
            {
                count = 1;
                m_VerticalScrollbarChanged = 1;
            }
            VerticalScrollBar.Maximum = count * PaintBox.DefaultGroupHeight;
            VerticalScrollBar.LargeChange = PaintBox.Height * 9 / 10;
        }

        public SehensControl()
        {
            ViewProxyCallback = new ViewProxy(this);
            SuspendLayout();
            base.Size = new Size(800, 450);
            base.AutoScaleDimensions = new SizeF(6f, 13f);
            base.AutoScaleMode = AutoScaleMode.Font;
            LeftRightSplit = new SplitContainer();
            PanelLeft = new Panel();

            TraceListView = new TraceListControl(this, CsvLog.ExtendPath((a) => OnLog?.Invoke(a), "TraceListView"));
            LeftRightSplit.Panel1MinSize = 0;
            LeftRightSplit.FixedPanel = FixedPanel.Panel1;
            LeftRightSplit.SplitterMoved += (o, e) =>
            {
                int distance = LeftRightSplit.SplitterDistance;
                if (distance > 20 && distance < 150 && !m_SplitterHold)
                {
                    m_SplitterHold = true;
                    TraceListVisible = distance >= 150 && !m_SplitterWasVisible;
                    m_SplitterHold = false;
                }
                m_SplitterWasVisible = TraceListVisible;
            };
            LeftRightSplit.SplitterDistance = LeftRightSplit.Width;
            base.Location = new Point(0, 0);

            PaintBoxScrollBarContainer = new SplitContainer();
            HorizontalScrollPanBar = new HScrollBar();
            HorizontalScrollZoomBar = new HScrollBar();
            VerticalScrollBar = new VScrollBar();
            PaintBox = new SehensPaintBox(this, CsvLog.ExtendPath((a) => OnLog?.Invoke(a), "PaintBox"));
            ((ISupportInitialize)PaintBoxScrollBarContainer).BeginInit();
            PaintBoxScrollBarContainer.Panel1.SuspendLayout();
            PaintBoxScrollBarContainer.Panel2.SuspendLayout();
            PaintBoxScrollBarContainer.SuspendLayout();

            SuspendLayout();
            PaintBoxScrollBarContainer.Dock = DockStyle.Bottom;
            PaintBoxScrollBarContainer.Location = new Point(0, 472);
            PaintBoxScrollBarContainer.PreviewKeyDown += PaintBox_PreviewKeyDown;
            PaintBoxScrollBarContainer.Panel1.Controls.Add(HorizontalScrollPanBar);
            PaintBoxScrollBarContainer.Panel2.Controls.Add(HorizontalScrollZoomBar);
            PaintBoxScrollBarContainer.Size = new Size(380, 18);
            PaintBoxScrollBarContainer.SplitterDistance = 181;
            PaintBoxScrollBarContainer.TabIndex = 4;
            PaintBoxScrollBarContainer.IsSplitterFixed = true;

            HorizontalScrollPanBar.Dock = DockStyle.Bottom;
            HorizontalScrollPanBar.LargeChange = 5000;
            HorizontalScrollPanBar.Location = new Point(0, 0);
            HorizontalScrollPanBar.Maximum = 1000;
            HorizontalScrollPanBar.Size = new Size(181, 18);
            HorizontalScrollPanBar.TabIndex = 39;
            HorizontalScrollPanBar.ValueChanged += HorizontalScrollPanBar_ValueChanged;

            HorizontalScrollZoomBar.Dock = DockStyle.Bottom;
            HorizontalScrollZoomBar.LargeChange = 5000;
            HorizontalScrollZoomBar.Location = new Point(0, 0);
            HorizontalScrollZoomBar.Maximum = 1000000;
            HorizontalScrollZoomBar.Size = new Size(195, 18);
            HorizontalScrollZoomBar.TabIndex = 39;
            HorizontalScrollZoomBar.ValueChanged += HorizontalScrollZoomBar_ValueChanged;

            VerticalScrollBar.Dock = DockStyle.Right;
            VerticalScrollBar.LargeChange = 500;
            VerticalScrollBar.Location = new Point(0, 0);
            VerticalScrollBar.Maximum = 10000;
            VerticalScrollBar.Size = new Size(18, 360);
            VerticalScrollBar.TabIndex = 39;
            VerticalScrollBar.ValueChanged += (s, e) =>
            {
                UpdateVerticalScrollbar();
                PaintBox.InvalidateDelayed();
            };

            PaintBox.BackColor = SystemColors.Window;
            PaintBox.Dock = DockStyle.Fill;
            PaintBox.Location = new Point(0, 0);
            PaintBox.Size = new Size(380, 472);
            PaintBox.TabIndex = 3;
            PaintBox.TabStop = false;
            PaintBox.MouseDoubleClick += PaintBox_MouseDoubleClick;
            PaintBox.MouseDown += PaintBox_MouseDown;
            PaintBox.MouseEnter += (s, e) => { };
            PaintBox.MouseLeave += PaintBox_MouseLeave;
            PaintBox.MouseMove += PaintBox_MouseMove;
            PaintBox.MouseUp += PaintBox_MouseUp;
            PaintBox.MouseWheel += PaintBox_MouseWheel;
            PaintBox.PreviewKeyDown += PaintBox_PreviewKeyDown;

            PanelLeft.Controls.Add(PaintBox);
            PanelLeft.Controls.Add(VerticalScrollBar);
            PanelLeft.Controls.Add(PaintBoxScrollBarContainer);

            base.Name = "SehensControl";
            base.Size = new Size(398, 490);
            base.AutoScaleMode = AutoScaleMode.None;
            base.PreviewKeyDown += PaintBox_PreviewKeyDown;
            base.Resize += (s, e) =>
            {
                m_VerticalScrollbarChanged = 1;
                UpdateVerticalScrollbar();
                RecalculateProjection();
            };
            PaintBoxScrollBarContainer.Panel1.ResumeLayout(performLayout: false);
            PaintBoxScrollBarContainer.Panel2.ResumeLayout(performLayout: false);
            ((ISupportInitialize)PaintBoxScrollBarContainer).EndInit();
            PaintBoxScrollBarContainer.ResumeLayout(performLayout: false);
            ResumeLayout(performLayout: false);
            UpdateVerticalScrollbar();
            m_ToolTip = new ToolTip();
            ContextMenu = new ScopeContextMenu(PaintBox, this);
            LeftRightSplit.Panel2.Controls.Add(PanelLeft);
            LeftRightSplit.Panel1.Controls.Add(TraceListView);
            base.Controls.Add(LeftRightSplit);
            LeftRightSplit.Dock = DockStyle.Fill;
            PanelLeft.Dock = DockStyle.Fill;
            TraceListView.Dock = DockStyle.Fill;
            LeftRightSplit.SplitterDistance = 100;
            Dock = DockStyle.None;
            ResumeLayout(performLayout: false);
            ReprocessMathAfterZoom();
        }




        ////////////////////////////////////////////////////////////////
        //Internal

        internal double m_TimebaseLineupLeftX;
        internal double m_TimebaseLineupRightX;

        internal ScopeContextMenu ContextMenu;

        private bool m_Closing;

        ////////////////////////////////////////////////////////////////
        //Traces

        private int m_RecalculateProjectionRequired;

        private object m_ViewGroupsLock = new object();
        private object m_TraceDataLock = new object();
        private Dictionary<string, TraceData> m_Traces = new Dictionary<string, TraceData>();
        private List<List<TraceView>> m_ViewGroups = new List<List<TraceView>>(); //includes invisible groups!

        public class ViewGroupList : List<TraceView>
        { // helper to set tracegroup
            public ViewGroupList(IEnumerable<TraceView> list)
            {
                AddRange(list);
                this.ForEach(x => x.Group = this);
            }
        }

        public TraceView? TryGetView(string name)
        {
            lock (m_ViewGroupsLock)
            {
                foreach (var traceGroup in m_ViewGroups)
                {
                    foreach (TraceView item in traceGroup)
                    {
                        if (item.ViewName == name)
                        {
                            return item;
                        }
                    }
                }
                return null;
            }
        }

        internal TraceData? TryGetTrace(string name)
        {
            TraceData? value = null;
            lock (m_TraceDataLock)
            {
                m_Traces.TryGetValue(name, out value);
                return value;
            }
        }

        public TraceData EnsureTrace(string name)
        {
            lock (m_ViewGroupsLock)
            {
                TraceData? traceData = TryGetTrace(name);
                if (traceData == null)
                {
                    traceData = new TraceData(name);
                    AddTrace(traceData);
                    return traceData;
                }
                return traceData;
            }
        }

        public string EnsureUnique(string text, Func<string, bool> check)
        {
            int index = 0;
            string newText;
            do
            {
                newText = (index == 0) ? text : $"{text} ({index})";
                index++;
            } while (check(newText));
            return newText;
        }

        public TraceView EnsureView(string viewName)
        {
            lock (m_ViewGroupsLock)
            {
                TraceView? traceView = TryGetView(viewName);
                if (traceView == null)
                {
                    TraceData traceData = TryGetTrace(viewName) ?? new TraceData(viewName);
                    traceView = new TraceView(this, traceData, viewName);
                    AddView(traceView);
                }
                return traceView;
            }
        }

        private bool ViewExists(TraceView view)
        {
            lock (m_ViewGroupsLock)
            {
                return m_ViewGroups.Any(x => x.Contains(view));
            }
        }

        public TraceView? ViewByTrace(TraceData trace)
        {
            lock (m_ViewGroupsLock)
            {
                foreach (ViewGroupList traceGroup in m_ViewGroups)
                {
                    foreach (var item in traceGroup)
                    {
                        if (item.Samples == trace)
                        {
                            return item;
                        }
                    }
                }
                return null;
            }
        }

        public void AddView(TraceView view)
        {
            if (m_Closing || view == null) return;

            OnLog?.Invoke(new CsvLog.Entry("Adding view " + view.ViewName, CsvLog.Priority.Debug));
            view.ZoomValue = ZoomValue;
            view.PanValue = PanValue;
            lock (m_ViewGroupsLock)
            {
                if (!ViewExists(view))
                {
                    m_ViewGroups.Add(new ViewGroupList(new[] { view }));
                }
            }
            AddTrace(view.Samples);
            GroupWithViewChanged(view);
        }

        public void RemoveView(TraceView view)
        {
            lock (m_ViewGroupsLock)
            {
                m_ViewGroups.ForEach(x => x.Remove(view));
                m_ViewGroups = m_ViewGroups.Where(x => x.Count > 0).ToList();
            }
            ViewListChanged();
            RecalculateProjection();
        }

        public void RenameView(TraceView view, string newName, ref string oldName)
        {
            lock (m_ViewGroupsLock)
            {
                string old = oldName;
                newName = EnsureUnique(newName, x => TryGetView(x) != null);
                var list = view.Group.Where(item => item.GroupWithView == old && item != view).ToList();
                list.ForEach(x => x.GroupWithView = "");
                oldName = newName; // change the view name
                list.ForEach(x => x.GroupWithView = newName);
            }
        }

        public TraceView DuplicateTraceView(TraceView view)
        {
            lock (m_ViewGroupsLock)
            {
                TraceView result = new TraceView(this, view);
                result.ViewName = EnsureUnique(view.ViewName, x => TryGetTrace(x) != null || TryGetView(x) != null);
                AddView(result);
                result.GroupWithView = view.ViewName;
                return result;
            }
        }

        public TraceView DuplicateTraceData(TraceView view)
        {
            TraceView traceView = DuplicateTraceView(view);
            traceView.Samples = new TraceData(traceView.ViewName, view.Samples, viewedData: true);
            return traceView;
        }


        public void AddTrace(TraceData trace)
        {
            if (m_Closing || trace == null) return;

            OnLog?.Invoke(new CsvLog.Entry("Adding trace " + trace.Name, CsvLog.Priority.Debug));
            bool addTrace = true;
            lock (m_TraceDataLock)
            {
                if (m_Traces.TryGetValue(trace.Name, out var value))
                {
                    if (value != trace)
                    {
                        throw new ArgumentException("Samples name already used");
                    }
                    addTrace = false;
                }
                m_Traces[trace.Name] = trace;
            }

            trace.StopUpdates = StopUpdates;
            if (addTrace)
            {
                trace.AddViewer(ViewProxyCallback);
            }
            lock (m_ViewGroupsLock)
            {
                string viewName = EnsureUnique(trace.Name, x => TryGetView(x) != null);
                TraceView? traceView = ViewByTrace(trace);
                if (traceView == null)
                {
                    traceView = new TraceView(this, trace, viewName);
                    AddView(traceView);
                }
            }
        }

        public TraceView[] GroupedTraces(TraceView view)
        {
            lock (m_ViewGroupsLock)
            {
                return view.Group.ToArray();
            }
        }

        public void Clear()
        {
            AllTraces.ForEach(x => x.Close());
        }

        public void AutoRangeAll()
        {

            VisibleViews.ForEach(view => view.AutoRange());
            RecalculateProjection();
        }

        internal void AutoRangeTimeAll()
        {
            VisibleViews.ForEach(item => item.AutoRangeTime());
            RecalculateProjection();
        }

        private void UpdateLinkedRanges()
        {
            VisibleViewGroups.ForEach(
                group => group.ForEach(
                    view => view.UpdateLinkedRanges(group)));
        }

        public TraceView? ViewByName(string viewName)
        {
            return TryGetView(viewName);
        }

        public TraceData? TraceByName(string traceName)
        {
            return TryGetTrace(traceName);
        }

        public void CloseVisible()
        {
            AllViews.Where(x => x.Visible).ToList().ForEach(x => x.Close());
        }

        public void CloseFlatTraces()
        {
            AllTraces.Where(x => x.InputValuesAllIdentical()).ForEach(x => x.Close());
        }

        public void CloseEmptyTraces()
        {
            AllTraces.Where(x => x.InputSampleCount == 0).ForEach(x => x.Close());
        }

        public void CloseInvisibleTraces()
        {
            AllTraces.Where(x => x.HasVisibleViewer == false).ForEach(x => x.Close());
        }

        private void ProxyRemoveTrace(string name)
        {
            lock (m_TraceDataLock)
            {
                m_Traces.Remove(name);
                ViewListChanged();
            }
        }

        private void ProxyRenameTrace(string oldName, string newName, TraceData trace)
        {
            lock (m_TraceDataLock)
            {
                if (m_Traces.ContainsKey(oldName))
                {
                    m_Traces.Remove(oldName);
                }
                m_Traces.Add(newName, trace);
            }
        }

        public void RecalculateProjection()
        {
            OnLog?.Invoke(new CsvLog.Entry("Setting flag for recalculating project", CsvLog.Priority.Debug));
            m_RecalculateProjectionRequired = 1;
            PaintBox.InvalidateDelayed();
        }

        internal void CalculateBeforeZoomRequired()
        {
            OnLog?.Invoke(new CsvLog.Entry("Setting flag for calculate before zoom", CsvLog.Priority.Debug));
            AllViews.Where(x => x.Visible).ForEach(view =>
            {
                view.ZoomValue = m_ZoomValue;
                view.PanValue = m_PanValue;
                view.m_BeforeZoomCalculateRequired = true;
            });
            PaintBox.InvalidateDelayed();
        }

        public void ReprocessMathAfterZoom()
        {
            OnLog?.Invoke(new CsvLog.Entry("Setting flag for calculate after zoom", CsvLog.Priority.Debug));
            AllViews.ForEach(view =>
            {
                view.ZoomValue = m_ZoomValue;
                view.PanValue = m_PanValue;
                view.m_AfterZoomCalculateRequired = true;
            });
            PaintBox.InvalidateDelayed();
        }

        internal bool RecalculateProjectionIfRequired()
        {
            bool required = Interlocked.Exchange(ref m_RecalculateProjectionRequired, 0) != 0;
            if (required)
            {
                AllViews.ForEach(view => view.RecalculateProjectionRequired());
                return required;
            }
            else
            {
                lock (m_ViewGroupsLock)
                {
                    return m_ViewGroups.Any(
                        x => x.Any(
                            item => item.IsRecalculateProjectionRequired
                            && item.Visible
                            && PaintBox.TraceToGroupDisplayInfo(item).IsOnScreen));
                }
            }
        }

        ////////////////////////////////////////////////////////////////
        //UI

        internal void IncrementBackgroundThreadCount()
        {
            Interlocked.Increment(ref m_BackgroundThreadCount);
            PaintBox.InvalidateDelayed();
        }

        internal void DecrementBackgroundThreadCount()
        {
            Interlocked.Decrement(ref m_BackgroundThreadCount);
            PaintBox.InvalidateDelayed();
        }

        public new void Invalidate()
        {
            if (!base.InvokeRequired && !PaintBox.UpdateInProgress)
            {
                base.Invalidate();
            }
            PaintBox.InvalidateDelayed();
        }

        public void BeginUpdate()
        {
            PaintBox.BeginUpdate();
        }

        public void EndUpdate()
        {
            if (PaintBox.EndUpdate())
            {
                RecalculateProjection();
                ViewListChanged();
                PaintBox.InvalidateDelayed();
            }
        }

        public void DeselectAll()
        {
            AllViews.ForEach(x => x.Selected = false);
        }

        public void SelectAllVisible()
        {
            AllViews.Where(x => x.Visible).ToList().ForEach(x => x.Selected = true);
        }

        public void SortViewGroups(bool byColour = true)
        {
            lock (m_ViewGroupsLock)
            {
                m_ViewGroups.Sort(new TraceViewComparer(ActiveSkin, byColour));
            }
            ViewListChanged();
            RecalculateProjection();
        }

        public void GroupViews(IEnumerable<string> viewNames, bool colour = false)
        {
            if (viewNames != null && viewNames.Count() != 0)
            {
                GroupViews(viewNames
                    .Select(x => TryGetView(x))
                    .Where(x => x != null)
                    .Select(y => y!), colour);
            }
        }

        public void GroupViews(IEnumerable<TraceView> views, bool colour = false)
        {
            if (views == null) return;

            BeginUpdate();
            try
            {
                TraceView firstView = views.First();
                double highestValue = views.Max(x => x.HighestValue);
                double lowestValue = views.Min(x => x.LowestValue);
                int num = 0;
                foreach (TraceView view in views)
                {
                    view.GroupWithView = (view == firstView) ? "" : firstView.ViewName;
                    if (colour)
                    {
                        view.Colour = ActiveSkin.ColourByIndex(num++);
                    }
                }
                firstView.HighestValue = highestValue;
                firstView.LowestValue = lowestValue;
            }
            catch (Exception e)
            {
                OnLog?.Invoke(new CsvLog.Entry(e.ToString(), CsvLog.Priority.Exception));
                throw;
            }
            finally
            {
                EndUpdate();
            }
        }

        public void MatchUnixTimes(IEnumerable<TraceView> views)
        {
            TraceData.TimeRange range = TraceView.GetGroupUnixTimeRange(views);
            views.ForEach(view => view.UnixTimeRange = range);
        }

        public void MatchUnixTimes(IEnumerable<string> views)
        {
            MatchUnixTimes(views.Select(x => TryGetView(Name)).Where(x => x != null).Select(x => x!).ToList());
        }

        public class TraceViewComparer : IComparer<List<TraceView>>
        {
            private Skin m_Skin;
            private bool m_ByColour;

            public TraceViewComparer(Skin skin) { m_Skin = skin; }
            public TraceViewComparer(Skin skin, bool byColour) { m_Skin = skin; m_ByColour = byColour; }

            public int Compare(List<TraceView>? listA, List<TraceView>? listB)
            {
                TraceView? viewA = (listA == null || listA.Count == 0) ? null : listA[0];
                TraceView? viewB = (listB == null || listB.Count == 0) ? null : listB[0];
                return viewA == null || viewB == null ? 0 : Compare(viewA, viewB);
            }

            public int Compare(TraceView viewA, TraceView viewB)
            {
                int result = 0;
                if (viewA.Visible && !viewB.Visible)
                {
                    result = -1;
                }
                else if (!viewA.Visible && viewB.Visible)
                {
                    result = 1;
                }
                else if (viewA.Samples != null && viewB.Samples != null)
                {
                    if (m_ByColour)
                    {
                        result = CompareColour(viewA, viewB);
                    }
                    if (result == 0)
                    {
                        result = viewA.ViewName.NaturalCompare(viewB.ViewName);
                    }
                }
                return result;
            }

            public int CompareColour(TraceView a, TraceView b)
            {
                int result = 0;
                int cola = m_Skin.ColourIndex(a.Colour);
                int colb = m_Skin.ColourIndex(b.Colour);
                if (cola != -1 && colb != -1 && cola < colb)
                {
                    result = -1;
                }
                else if (cola != -1 && colb != -1 && cola > colb)
                {
                    result = 1;
                }
                else if (a.Colour.ToArgb() < b.Colour.ToArgb())
                {
                    result = -1;
                }
                else if (a.Colour.ToArgb() > b.Colour.ToArgb())
                {
                    result = 1;
                }
                return result;
            }
        }

        private void PaintBox_PreviewKeyDown(object? s, PreviewKeyDownEventArgs e)
        {
            if (m_ViewGroups != null && PaintBox.PaintedTraces.VisibleTraceGroupList != null)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DeselectAll();
                }
                ContextMenu_PreviewKeyDown(s, e);
            }
        }

        ////////////////////////////////////////////////////////////////
        //events

        private void ViewListChanged()
        {
            if (!PaintBox.UpdateInProgress)
            {
                UpdateLinkedRanges();
                OnViewListChanged?.Invoke(this);
            }
        }

        public void ViewVisibleChanged(TraceView sender)
        {
            m_VerticalScrollbarChanged = 1;
            ViewNeedsRepaint(sender);
            OnViewListChanged?.Invoke(this);
            RecalculateProjection();
        }

        public void ViewNeedsRepaint(TraceView sender)
        {
            if (!sender.Visible) return;

            if (PaintBox.UpdateInProgress)
            {
                PaintBox.InvalidateDelayed();
            }
            else
            {
                Invalidate();
            }
        }

        internal void GroupWithViewChanged(TraceView view)
        {
            OnLog?.Invoke(new CsvLog.Entry($"Group {view.ViewName} changed", CsvLog.Priority.Debug));
            m_VerticalScrollbarChanged = 1;

            lock (m_ViewGroupsLock)
            {
                if (view.GroupWithView == "" || view.GroupWithView == view.ViewName)
                {
                    List<TraceView> traceGroup = view.Group;
                    RemoveViewFromGroup(view);
                    CleanGroupWithView(traceGroup);
                }
                else
                {
                    TraceView? groupView = TryGetView(view.GroupWithView);
                    if (groupView != null && groupView.Group != view.Group)
                    {
                        List<TraceView> oldGroup = view.Group;
                        List<TraceView> newGroup = groupView.Group;
                        oldGroup.Remove(view);
                        view.Selected = groupView.Selected;
                        newGroup.Add(view);
                        view.Group = groupView.Group;
                        CleanGroupWithView(oldGroup);
                        CleanGroupWithView(newGroup);
                    }
                    m_ViewGroups = m_ViewGroups.Where(x => x.Count > 0).ToList();
                }
            }
            ViewListChanged();
            RecalculateProjection();
        }

        private void RemoveViewFromGroup(TraceView view)
        {
            if (view.Group.Count != 0
                && (view.GroupWithView == "" || view.GroupWithView == view.ViewName)
                && (view.Group.Count > 1 || view.Group[0] != view))
            {
                foreach (TraceView item in view.Group)
                {
                    if (item != view && item.GroupWithView == view.ViewName)
                    {
                        return; // can't remove view from group when view is the group target
                    }
                }

                var oldGroup = view.Group;
                view.Group.Remove(view);
                int newIndex = m_ViewGroups.IndexOf(oldGroup);
                ViewGroupList newGroup = new ViewGroupList(new[] { view });
                if (newIndex == -1)
                {
                    m_ViewGroups.Add(newGroup); // probably shouldn't happen
                }
                else
                {
                    m_ViewGroups.Insert(newIndex + 1, newGroup);
                }
            }
        }

        private void CleanGroupWithView(List<TraceView> group)
        {
            var list = group.Where(item => item.GroupWithView == "" || item.GroupWithView == item.ViewName).ToArray();
            foreach (TraceView item in list)
            {
                if (item.Group.Count != 1)
                {
                    RemoveViewFromGroup(item);
                }
            }
        }

        private void PaintBox_MouseDown(object? sender, MouseEventArgs e)
        {
            try
            {
                PaintBoxMouse.WipeStart = e;
                PaintBoxMouse.MouseDownVisibleGroupIndex = MouseToGroupIndex(e.Y);
                PaintBoxMouse.MouseDownGroupDisplay = MouseToGroupDisplayInfo(PaintBoxMouse.MouseDownVisibleGroupIndex);
                PaintBoxMouse.DownSeconds = HighResTimer.StaticSeconds;
                PaintBoxMouse.XDragPixels = 0;

                if ((PaintBoxMouse.MouseGuiSection & PaintBoxMouseInfo.GuiSection.BottomGutter) != 0)
                {
                    PaintBoxMouse.ClickType = PaintBoxMouseInfo.Type.TraceHeight;
                }
                else if (e.Button == MouseButtons.Right && (PaintBoxMouse.MouseDownGroupDisplay?.IsOnScreen ?? false) && !SimpleUi)
                {
                    PaintBoxMouse.ClickType = PaintBoxMouseInfo.Type.WipeSelectStart;
                }
                else if (e.Button == MouseButtons.Left)
                {
                    PaintBoxMouse.ClickType = PaintBoxMouseInfo.Type.DragTrace;
                    PaintBoxMouse.MouseDownGroupDisplay?.View0.Group.ForEach(x => x.TraceClicked(e));
                }
                else
                {
                    PaintBoxMouse.ClickType = PaintBoxMouseInfo.Type.None;
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke(new CsvLog.Entry(ex.ToString(), CsvLog.Priority.Exception));
            }
        }

        private void PaintBox_MouseUp(object? sender, MouseEventArgs e)
        {
            try
            {
                PaintBoxMouse.RightClickGroup = null;
                bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
                bool control = (Control.ModifierKeys & Keys.Control) == Keys.Control;

                int index = MouseToGroupIndex(e.Y);
                TraceView? view = MouseToSingleTraceClickZone(e);
                if (view != null)
                {
                    DeselectAll();
                }

                if (index >= 0 && index < PaintBox.PaintedTraces.VisibleTraceGroupList.Count)
                {
                    var list = PaintBox.PaintedTraces.VisibleTraceGroupList[index];
                    switch (e.Button)
                    {
                        case MouseButtons.Left:
                            if (PaintBoxMouse.MouseDownVisibleGroupIndex == index)
                            {
                                PaintBox_MouseUp_LeftClick(control, shift, index, list, e);
                            }
                            break;
                        case MouseButtons.Right:
                            {
                                PaintBoxMouse.RightClickGroup = (view == null) ? list : new List<TraceView> { view };
                                if (PaintBoxMouse.RightClickGroup.Any(x => x.Selected == false))
                                {
                                    DeselectAll();
                                }
                                break;
                            }
                    }
                    if (!shift)
                    {
                        PaintBoxMouse.ClickGroupIndex = index;
                    }
                }

                if (e.Button == MouseButtons.Right && !SimpleUi)
                {
                    ContextMenu.ContextMenuShow(this, PaintBoxMouse, e,
                        traces: view == null
                            ? PaintBox.PaintedTraces.VisibleTraceGroupList
                            : new List<List<TraceView>> { new List<TraceView> { view } });
                }
                if (e.Button == MouseButtons.Left && PaintBoxMouse.WipeStart != null)
                {
                    PaintBoxMouse.WipeStart = null;
                    SetZoomPan(ZoomValue, Math.Min(1.0 - ZoomValue, Math.Max(0.0, PanValue)));
                }
                PaintBoxMouse.ClickType = PaintBoxMouseInfo.Type.None;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke(new CsvLog.Entry(ex.ToString(), CsvLog.Priority.Exception));
            }
            Invalidate();
        }

        private void PaintBox_MouseUp_LeftClick(bool control, bool shift, int clickDivision, List<TraceView> list, MouseEventArgs e)
        {
            foreach (TraceView view in list)
            {
                foreach (var zone in view.Painted.ClickZones)
                {
                    if (zone.Rect.Contains(new Point(e.X, e.Y)))
                    {
                        zone.Click(view, list, e);
                    }
                }
            }

            if (!control)
            {
                DeselectAll();
            }

            if (shift)
            {
                SelectVisibleViewGroupRange(PaintBoxMouse.ClickGroupIndex, clickDivision);
            }
            else
            {
                bool selected = list[0].Selected;
                list.ForEach(x => x.Selected = !selected);
            }
        }

        private void PaintBox_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (SimpleUi) return;

            FocusRestore = null;
            TraceView? view = MouseToSingleTraceClickZone(e);
            if (view != null)
            {
                view.ShowControlForm();
            }
            else
            {
                int index = MouseToGroupIndex(e.Y);
                if (index >= 0 && index < PaintBox.PaintedTraces.VisibleTraceGroupList.Count)
                {
                    PaintBox.PaintedTraces.VisibleTraceGroupList[index].ForEach(item => item.ShowControlForm());
                }
            }
        }

        private void PaintBox_MouseLeave(object? sender, EventArgs e)
        {
            if (PaintBox.PaintedTraces.VisibleTraceGroupList == null) return;
            if (FocusRestore != null && FocusRestore.FindForm() == base.ParentForm)
            {
                try
                {
                    if (!FocusRestore.IsDisposed)
                    {
                        FocusRestore.Focus();
                    }
                }
                catch { }
                FocusRestore = null;
            }
            PaintBoxMouse.MouseGuiSection = PaintBoxMouseInfo.GuiSection.None;
            m_ToolTip.RemoveAll();
            PaintBoxMouse.HideCursor = true;
            PaintBox.InvalidateDelayed();
        }

        private void PaintBox_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (PaintBox.PaintedTraces.VisibleTraceGroupList == null) return;
            PaintBoxMouseEvent(e);
            m_HoldZoomPan = true;
            if (m_ViewGroups != null && m_ViewGroups.Count > 0 && PaintBox.PaintedTraces.VisibleTraceGroupList != null && PaintBox.PaintedTraces.VisibleTraceGroupList.Count > 0)
            {
                Keys modifierKeys = Control.ModifierKeys;
                if ((modifierKeys & Keys.Control) == Keys.Control)
                {
                    PaintBoxMouseWheelHorizontalZoom(e);
                }
                else if ((modifierKeys & Keys.Shift) == Keys.Shift)
                {
                    PaintBoxMouseWheelVerticalZoom(e);
                }
                else if ((modifierKeys & Keys.Alt) == Keys.Alt)
                {
                    SetZoomPan(m_ZoomValue, m_PanValue + (e.Delta / 120.0 * m_ZoomValue / 30.0));
                }
                else
                {
                    VerticalScrollBar.Value = Math.Min(VerticalScrollBar.Maximum, Math.Max(VerticalScrollBar.Minimum, VerticalScrollBar.Value - e.Delta));
                }
            }
            m_HoldZoomPan = false;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            const int WM_MOUSEHWHEEL = 0x020E; // horizontal wheel
            if (m.Msg == WM_MOUSEHWHEEL)
            {
                int delta = (int)(short)(m.WParam.ToInt64() >> 16);
                m_HoldZoomPan = true;
                SetZoomPan(m_ZoomValue, m_PanValue + (delta / 120.0 * m_ZoomValue / 30.0));
                m_HoldZoomPan = false;
            }
        }

        private void PaintBoxMouseWheelHorizontalZoom(MouseEventArgs e)
        {
            int index = MouseToGroupIndex(e.Y);
            if (index < 0 || index >= PaintBox.PaintedTraces.VisibleTraceGroupList.Count) return;

            var list = PaintBox.PaintedTraces.VisibleTraceGroupList[index];
            var group = PaintBox.TraceToGroupDisplayInfo(list[0]);
            if (!group.ProjectionArea.Contains(e.X, e.Y)) return;

            int max = list.Max(x => x.CalculatedBeforeZoom?.Length ?? 0);
            int min = Math.Min(500, Math.Max(100, max / 2000));
            int delta = e.Delta / 120 * (HorizontalScrollZoomBar.Maximum / min);

            double newZoom = Math.Min(HorizontalScrollZoomBar.Maximum, Math.Max(0, m_ZoomBarValue + delta));
            var zoomPan = ConvertBarToZoomPanValues(newZoom, m_PanBarValue);

            double panChange = (e.X - group.ProjectionArea.Left) / (double)group.ProjectionArea.Width;
            SetZoomPan(zoomPan.zoomValue, Math.Min(1.0 - zoomPan.zoomValue, Math.Max(0.0, m_PanValue + (m_ZoomValue - zoomPan.zoomValue) * panChange)));
        }

        private void PaintBoxMouseWheelVerticalZoom(MouseEventArgs e)
        {
            SetVerticalZoom(PaintBoxMouse.MouseMoveGroupIndex, m_TraceHeightRatio + e.Delta / 2000.0);
        }

        private void PaintBoxMouseEvent(MouseEventArgs e)
        {
            PaintBoxMouse.Click = e;
            PaintBoxMouse.MouseMoveGroupIndex = MouseToGroupIndex(e.Y);
            PaintBoxMouse.MouseMoveGroupDisplay = MouseToGroupDisplayInfo(PaintBoxMouse.MouseMoveGroupIndex);
            PaintBoxMouse.MouseGuiSection = PaintBoxMouseInfo.GuiSection.None;
            if (PaintBox.PaintedTraces.VisibleTraceGroupList.Count() == 0)
            {
                PaintBoxMouse.MouseGuiSection |= PaintBoxMouseInfo.GuiSection.EmptyScope;
            }
            (MouseToTraceGroup(e) ?? Enumerable.Empty<TraceView>())
                .ForEach(x => PaintBoxMouse.MouseGuiSection |= x.Painter.GetGuiSections(x.Painted, e));

            TraceGroupDisplay? info = PaintBoxMouse.MouseMoveGroupDisplay;
            if (info != null)
            {
                if (info.ProjectionArea.Contains(e.X, e.Y))
                {
                    PaintBoxMouse.MouseGuiSection |= PaintBoxMouseInfo.GuiSection.TraceArea;
                }
                if (info.BottomGutter.Contains(e.X, e.Y))
                {
                    PaintBoxMouse.MouseGuiSection |= PaintBoxMouseInfo.GuiSection.BottomGutter;
                }
                if (info.RightGutter.Contains(e.X, e.Y))
                {
                    PaintBoxMouse.MouseGuiSection |= PaintBoxMouseInfo.GuiSection.RightGutter;
                }
                if (info.VerticalAxisArea.Contains(e.X, e.Y))
                {
                    PaintBoxMouse.MouseGuiSection |= PaintBoxMouseInfo.GuiSection.VerticalAxis;
                }
            }
        }

        private void PaintBox_MouseMove(object? sender, MouseEventArgs e)
        {
            Keys modifierKeys = Control.ModifierKeys;
            if (PaintBox.PaintedTraces.VisibleTraceGroupList == null) return;
            if (Form.ActiveForm != base.ParentForm) return;

            if (PaintBoxMouse.ClickType != PaintBoxMouse.PreviousClickType)
            {
                PaintBox.InvalidateDelayed();
                PaintBoxMouse.PreviousClickType = PaintBoxMouse.ClickType;
            }

            Control? focusedControl = GetFocusedControl();
            if (focusedControl != PaintBox)
            {
                FocusRestore = focusedControl;
            }

            PaintBox.Focus();
            PaintBoxMouseEvent(e);
            PaintBoxMouse.HideCursor = false;
            UpdateMouseCursor();

            if (PaintBoxMouse.WipeStart != null && PaintBoxMouse.Click != null && e.Button == MouseButtons.Right)
            {
                switch (PaintBoxMouse.ClickType)
                {
                    case PaintBoxMouseInfo.Type.WipeSelectStart:
                        if (Math.Abs(PaintBoxMouse.WipeStart.X - PaintBoxMouse.Click.X) + Math.Abs(PaintBoxMouse.WipeStart.Y - PaintBoxMouse.Click.Y) >= 6)
                        {
                            PaintBoxMouse.ClickType = PaintBoxMouseInfo.Type.WipeSelect;
                        }
                        break;
                    case PaintBoxMouseInfo.Type.WipeSelect:
                        PaintBox.InvalidateDelayed();
                        break;
                }
            }

            if (PaintBoxMouse.WipeStart != null && e.Button == MouseButtons.Left)
            {
                switch (PaintBoxMouse.ClickType)
                {
                    case PaintBoxMouseInfo.Type.TraceHeight:
                        {
                            int mouseDownDivision = PaintBoxMouse.MouseDownVisibleGroupIndex;
                            double y = e.Y - PaintBoxMouse.MouseDownGroupDisplay.ProjectionArea.Top;
                            if (modifierKeys.HasFlag(Keys.Shift)) // just this trace
                            {
                                TraceView view = PaintBox.PaintedTraces.VisibleTraceGroupList[mouseDownDivision][0];
                                double sum = view.Painted.HeightAdjustSum;
                                double val = y * sum / (PaintBox.PaintedTraces.VisibleTraceGroupList.Count * PaintBox.DefaultGroupHeight);
                                view.HeightFactor = Math.Max(1.0, Math.Min(2.5, val));
                                PaintBox.InvalidateDelayed();
                            }
                            else // all traces
                            {
                                double ratio = (y - PaintBox.MinimumGroupHeight) / (PaintBox.Height - PaintBox.MinimumGroupHeight);
                                OnLog?.Invoke(new CsvLog.Entry($"new ratio {ratio} y={y} pbh={PaintBox.Height} mgh={PaintBox.MinimumGroupHeight}", CsvLog.Priority.Info));
                                SetVerticalZoom(mouseDownDivision, ratio);
                            }
                            break;
                        }

                    case PaintBoxMouseInfo.Type.DragTrace:
                        {
                            if (PaintBox.PaintedTraces.VisibleTraceGroupList.Count > 0)
                            {
                                int startDivision = MouseToGroupIndex(PaintBoxMouse.WipeStart.Y);
                                if (startDivision >= 0 && startDivision < PaintBox.PaintedTraces.VisibleTraceGroupList.Count)
                                {
                                    foreach (var view in PaintBox.PaintedTraces.VisibleTraceGroupList[startDivision])
                                    {
                                        foreach (var zone in view.Painted.ClickZones)
                                        {
                                            if (zone.Rect.Contains(new Point(e.X, e.Y)) && view.Painted.Group.Count == 1)
                                            {
                                                PaintBoxMouse.DragGuiSection = zone.GuiSection;
                                                PaintBoxMouse.ClickType = PaintBoxMouseInfo.Type.DragOverlayHitbox;
                                            }
                                        }
                                    }
                                }
                            }
                            if (PaintBoxMouse.ClickType == PaintBoxMouseInfo.Type.DragOverlayHitbox)
                            {
                                PaintBox_MouseMove_DragOverlayHitbox(e);
                            }
                            else
                            {
                                PaintBox_MouseMove_DragTrace(e);
                            }
                            break;
                        }

                    case PaintBoxMouseInfo.Type.DragOverlayHitbox:
                        PaintBox_MouseMove_DragOverlayHitbox(e);
                        break;
                }
            }

            if (CursorMode != 0 || ShowHoverInfo || ShowHoverValue)
            {
                PaintBox.InvalidateDelayed();
            }
        }

        private (double zoomBarValue, double panBarValue) CalculateZoomPanScrollbar()
        {
            HorizontalScrollPanBar.LargeChange = Math.Max(1, (int)(m_ZoomValue * HorizontalScrollPanBar.Maximum));

            return (Math.Max(0.0, Math.Min(HorizontalScrollZoomBar.Maximum, (1.0 - Math.Log(m_ZoomValue * 199.0 + 1.0, 200.0)) * HorizontalScrollZoomBar.Maximum)),
                    Math.Max(0.0, Math.Min(HorizontalScrollPanBar.Maximum - HorizontalScrollPanBar.LargeChange, m_PanValue * HorizontalScrollPanBar.Maximum))
                    );
        }

        private (double zoomValue, double panValue) ConvertBarToZoomPanValues(double zoomBarValue, double panBarValue)
        {
            double zoomValue = (Math.Pow(200.0, Math.Max(0.0, 1.0 - (zoomBarValue / HorizontalScrollZoomBar.Maximum))) - 1.0) / 199.0;
            zoomValue = Math.Max(0, Math.Min(1, zoomValue));
            return (zoomValue, Math.Min(1.0 - zoomValue, panBarValue / HorizontalScrollPanBar.Maximum));
        }

        public void SetZoomPan(double zoom, double pan)
        {
            m_HoldZoomPan = true;
            m_ZoomValue = Math.Max(0.0, Math.Min(1.0, zoom));
            m_PanValue = Math.Max(0.0, Math.Min(1.0, pan));
            var result = CalculateZoomPanScrollbar();
            if (result.zoomBarValue != m_ZoomBarValue || result.panBarValue != m_PanBarValue)
            {
                m_ZoomBarValue = result.zoomBarValue;
                m_PanBarValue = result.panBarValue;
                HorizontalScrollPanBar.Value = Math.Max(0, Math.Min(HorizontalScrollPanBar.Maximum - HorizontalScrollPanBar.LargeChange, (int)m_PanBarValue));
                HorizontalScrollZoomBar.Value = Math.Max(0, Math.Min(HorizontalScrollZoomBar.Maximum, (int)m_ZoomBarValue));
                ReprocessMathAfterZoom();
            }
            m_HoldZoomPan = false;
        }

        private void HorizontalScrollPanBar_ValueChanged(object sender, EventArgs e)
        {
            if (m_HoldZoomPan) return;

            m_HoldZoomPan = true;
            (m_ZoomValue, m_PanValue) = ConvertBarToZoomPanValues(HorizontalScrollZoomBar.Value, HorizontalScrollPanBar.Value);
            (m_ZoomBarValue, m_PanBarValue) = CalculateZoomPanScrollbar();
            ReprocessMathAfterZoom();
            m_HoldZoomPan = false;
        }

        private void HorizontalScrollZoomBar_ValueChanged(object sender, EventArgs e)
        {
            if (m_HoldZoomPan) return;

            m_HoldZoomPan = true;
            double zoomValue = m_ZoomValue;
            (m_ZoomValue, _) = ConvertBarToZoomPanValues(HorizontalScrollZoomBar.Value, HorizontalScrollPanBar.Value);
            m_PanValue = Math.Max(0.0, Math.Min(1.0, m_PanValue + (zoomValue - m_ZoomValue) * 0.5));
            (m_ZoomBarValue, m_PanBarValue) = CalculateZoomPanScrollbar();
            HorizontalScrollPanBar.Value = (int)m_PanBarValue;
            ReprocessMathAfterZoom();
            m_HoldZoomPan = false;
        }

        private static Control? GetFocusedControl(Control? control = null)
        {
            for (IContainerControl? container = (control ?? Form.ActiveForm) as IContainerControl;
                container != null;
                container = control as IContainerControl)
            {
                control = container?.ActiveControl;
            }
            return control;
        }

        private void PaintBox_MouseMove_DragTrace(MouseEventArgs e)
        {
            int newVisibleDivision = MouseToGroupIndex(e.Y);
            OnLog?.Invoke(new CsvLog.Entry($"drag {PaintBoxMouse.MouseDownVisibleGroupIndex} to {newVisibleDivision}", CsvLog.Priority.Info));
            if (newVisibleDivision < 0 || newVisibleDivision >= PaintBox.PaintedTraces.VisibleTraceGroupList.Count) return;

            TraceGroupDisplay traceDivision = PaintBox.TraceToGroupDisplayInfo(PaintBox.PaintedTraces.VisibleTraceGroupList[newVisibleDivision][0]);
            double num = (e.X - PaintBoxMouse.WipeStart?.X) * m_ZoomValue / traceDivision.ProjectionArea.Width - PaintBoxMouse.XDragPixels ?? 0;

            PaintBoxMouse.XDragPixels += num;
            SetZoomPan(m_ZoomValue, m_PanValue - num);

            if (newVisibleDivision != PaintBoxMouse.MouseDownVisibleGroupIndex)
            {
                VisibleViewGroupTranspose(PaintBoxMouse.MouseDownVisibleGroupIndex, newVisibleDivision);
                PaintBoxMouse.MouseDownVisibleGroupIndex = newVisibleDivision;
            }
        }

        private void SelectVisibleViewGroupRange(int start, int end)
        {
            for (int loop = Math.Min(start, end); loop <= Math.Max(start, end); loop++)
            {
                if (loop >= 0 && loop < PaintBox.PaintedTraces.VisibleTraceGroupList.Count)
                {
                    PaintBox.PaintedTraces.VisibleTraceGroupList[loop].ForEach(x => x.Selected = true);
                }
            }
        }

        private void VisibleViewGroupTranspose(int group1, int group2)
        {
            try
            {
                lock (m_ViewGroupsLock)
                {
                    int g1 = VisibleGroupToGroup(group1);
                    int g2 = VisibleGroupToGroup(group2);
                    (m_ViewGroups[g1], m_ViewGroups[g2]) = (m_ViewGroups[g2], m_ViewGroups[g1]);
                }
                ViewListChanged();
                RecalculateProjection();
            }
            catch (ArgumentOutOfRangeException) { }

            int VisibleGroupToGroup(int visibleIndex)
            {
                return m_ViewGroups
                    .Select((group, i) => new { Group = group, RealIndex = i })
                    .FirstOrDefault(x => x.Group.Any(y => y.Visible) && visibleIndex-- == 0)?.RealIndex
                    ?? throw new ArgumentOutOfRangeException();
            }
        }

        private void PaintBox_MouseMove_DragOverlayHitbox(MouseEventArgs e)
        {
            if (PaintBoxMouse.WipeStart == null) return;
            int index = MouseToGroupIndex(PaintBoxMouse.WipeStart.Y);
            if (index < 0 || index >= PaintBox.PaintedTraces.VisibleTraceGroupList.Count) return;

            foreach (var view in PaintBox.PaintedTraces.VisibleTraceGroupList[index])
            {
                foreach (var zone in view.Painted.ClickZones)
                {
                    if (PaintBoxMouse.DragGuiSection == zone.GuiSection && view.Painted.Group.Count == 1)
                    {
                        TraceView.MouseInfo down = view.Measure(PaintBoxMouse.WipeStart);
                        TraceView.MouseInfo now = view.Measure(e);
                        zone.Drag(view, down, now);
                    }
                }
            }
        }

        public int MouseToGroupIndex(int y)
        {
            foreach (List<TraceView> group in PaintBox.PaintedTraces.VisibleTraceGroupList)
            {
                if (group.Count > 0)
                {
                    TraceGroupDisplay div = PaintBox.TraceToGroupDisplayInfo(group[0]);
                    if (div.GroupArea.Top <= y && div.GroupArea.Bottom >= y)
                    {
                        return group[0].Painted.GroupIndex;
                    }
                }
            }
            return PaintBox.PaintedTraces.VisibleTraceGroupList.Count;
        }

        private TraceGroupDisplay? MouseToGroupDisplayInfo(int division)
        {
            TraceGroupDisplay? result = null;
            List<TraceView>? list = null;

            if (division >= 0 && division < PaintBox.PaintedTraces.VisibleTraceGroupList.Count)
            {
                list = PaintBox.PaintedTraces.VisibleTraceGroupList[division];
            }
            if (list != null && list.Count > 0)
            {
                result = PaintBox.TraceToGroupDisplayInfo(list[0]);
            }
            return result;
        }

        private TraceView? MouseToSingleTraceClickZone(MouseEventArgs e)
        {
            IEnumerable<TraceView>? views = MouseToTraceGroup(e);
            if (views != null)
            {
                foreach (var view in views)
                {
                    foreach (var zone in view.Painted.ClickZones)
                    {
                        if (zone.Rect.Contains(new Point(e.X, e.Y))
                            && zone.Flag.HasFlag(TraceViewClickZone.Flags.Trace))
                        {
                            return view;
                        }
                    }
                }
            }
            return null;
        }

        private IEnumerable<TraceView>? MouseToTraceGroup(MouseEventArgs e)
        {
            int index = MouseToGroupIndex(e.Y);
            return index >= 0 && index < PaintBox.PaintedTraces.VisibleTraceGroupList.Count
                ? PaintBox.PaintedTraces.VisibleTraceGroupList[index]
                : null;
        }

        internal void UpdateMouseCursor()
        {
            if (PaintBoxMouse.MouseGuiSection == PaintBoxMouseInfo.GuiSection.BottomGutter
                || PaintBoxMouse.ClickType == PaintBoxMouseInfo.Type.TraceHeight)
            {
                PaintBox.Cursor = Cursors.HSplit;
            }
            else
            {
                switch (CursorMode)
                {
                    case Skin.Cursors.VerticalLine: PaintBox.Cursor = Cursors.VSplit; break;
                    case Skin.Cursors.Pointer: PaintBox.Cursor = Cursors.Default; break;
                    case Skin.Cursors.CrossHair: PaintBox.Cursor = Cursors.Cross; break;
                }
                PaintBox.InvalidateDelayed();
            }
        }

        ////////////////////////////////////////////////////////////////
        //context menus

        internal void ShowContextMenu()
        {
            PaintBoxMouse.Click = null;
            ContextMenu.ContextMenuShow(
                this,
                PaintBoxMouse,
                new MouseEventArgs(MouseButtons.Left, 1, PaintBox.Width - 12, 40, 0),
                PaintBox.PaintedTraces.VisibleTraceGroupList);
        }

        private void ContextMenu_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            PaintBoxMouse.RightClickGroup = null;
            ContextMenu.ContextMenuList
                .Where(x =>
                        x.Valid(PaintBox.PaintedTraces.VisibleTraceGroupList, PaintBox.PaintedTraces.SelectedTraceList, PaintBoxMouseInfo.GuiSection.Anywhere)
                        && x.HotKey(e))
                .ForEach(menu =>
                {
                    var list = PaintBoxMouse.CombinedSelectedTraces(PaintBox.PaintedTraces.VisibleTraceGroupList);
                    switch (menu.Call)
                    {
                        case ScopeContextMenu.MenuItem.CallWhen.Once:
                            ContextClick(menu: menu, traces: list);
                            break;

                        case ScopeContextMenu.MenuItem.CallWhen.PerTrace:
                            list.ForEach(x => ContextClick(menu: menu, traces: new List<TraceView> { x }));
                            break;

                        case ScopeContextMenu.MenuItem.CallWhen.PerTraceGroup:
                            PaintBox.PaintedTraces.VisibleTraceGroupList
                                .Where(x => x.Count() > 0)
                                .ForEach(x => ContextClick(menu: menu, traces: x));
                            break;
                    }
                });
        }

        internal void ContextMenuClick(ScopeContextMenu.MenuItem menu)
        {
            var list = PaintBoxMouse.CombinedSelectedTraces(PaintBox.PaintedTraces.VisibleTraceGroupList);
            if (list.Count == 0 && menu.ShownWhenTrace == ScopeContextMenu.MenuItem.ShowWhen.Always)
            {
                ContextClick(menu: menu, traces: list);
            }
            else if (PaintBoxMouse.Click != null && list.Count > 0 && menu.Call == ScopeContextMenu.MenuItem.CallWhen.Once)
            {
                ContextClick(menu: menu, traces: list);
            }
            else if (PaintBoxMouse.Click != null)
            {
                ContextClick(menu: menu, traces: list);
            }
        }

        private void ContextClick(ScopeContextMenu.MenuItem menu, List<TraceView> traces)
        {
            ScopeContextMenu.DropDownArgs a = new ScopeContextMenu.DropDownArgs()
            {
                Scope = this,
                Menu = menu,
                Views = traces,
                Mouse = PaintBoxMouse,
            };

            try
            {
                menu.Clicked?.Invoke(a);
            }
            catch (Exception e)
            {
                OnLog?.Invoke(new CsvLog.Entry(e.ToString(), CsvLog.Priority.Exception));
                MessageBox.Show(e.Message);
            }
        }

        ////////////////////////////////////////////////////////////////
        //screenshot helpers

        public Bitmap ScreenshotToBitmap(Skin skin)
        {
            return PaintBox.ScreenshotToBitmap(skin, null);
        }

        public void ScreenshotToClipboard()
        {
            using AutoEditorForm autoEditorForm = new AutoEditorForm();
            if (autoEditorForm.ShowDialog("Screenshot", "Screenshot", ScreenshotSkin))
            {
                try
                {
                    Clipboard.SetImage(ScreenshotToBitmap(ScreenshotSkin));
                }
                catch (Exception e)
                {
                    OnLog?.Invoke(new CsvLog.Entry(e.ToString(), CsvLog.Priority.Exception));
                }
            }
        }

        public void ScreenshotToClipboard(int traceWidth, int traceHeight)
        {
            try
            {
                Skin skin = new Skin(Skin.CannedSkins.ScreenShot);
                skin.TraceWidth = traceWidth;
                skin.TraceHeight = traceHeight;
                Clipboard.SetImage(ScreenshotToBitmap(skin));
            }
            catch (Exception e)
            {
                OnLog?.Invoke(new CsvLog.Entry(e.ToString(), CsvLog.Priority.Exception));
            }
        }

        public string ScreenshotToRtf(Skin? skin = null)
        {
            skin = skin ?? ScreenshotSkin;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("{\\rtf1");
            Bitmap bitmap;
            bool notFirst = false;
            int index = 0;
            int figureIndex = 1;
            foreach (var item in PaintBox.PaintedTraces.VisibleTraceGroupList)
            {
                if (skin.ExportTraces == Skin.TraceSelections.VisibleTraces
                    || (item.FirstOrDefault()?.Selected ?? false))
                {
                    bitmap = PaintBox.ScreenshotToBitmap(skin, item.FirstOrDefault()?.Painted.GroupIndex);
                    if (notFirst)
                    {
                        stringBuilder.AppendLine("\\line");
                    }
                    notFirst = true;
                    stringBuilder.AppendLine("{");
                    stringBuilder.AppendLine($@"\pict \pic{bitmap.Width} \pich{bitmap.Height} \picwgoal{bitmap.Width * 15} \pichgoal{bitmap.Height * 15} \dpix96 \dpiy96 \picscalex100 \picscaley100 \pngblip");
                    stringBuilder.AppendLine(BitmapToHexPng(bitmap));
                    stringBuilder.AppendLine("}");
                    stringBuilder.AppendLine("{");
                    var captions = item.Select(x => x.DecoratedName);
                    stringBuilder.AppendLine($@"\line\ltrch Figure {figureIndex} - {string.Join(", ", captions).RtfEncode()} \par");
                    stringBuilder.AppendLine("}");
                    figureIndex++;
                }
                index++;
            }
            stringBuilder.AppendLine("}");
            return stringBuilder.ToString();
        }

        private string BitmapToHexPng(Bitmap image)
        {
            byte[] png;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                image.Save(memoryStream, ImageFormat.Png);
                png = memoryStream.ToArray();
            }
            return String.Join(@"
", png.Select(x => $"{x:x2}").Chunk(64).Select(x => String.Join("", x)));
        }


        ////////////////////////////////////////////////////////////////
        //paint

        internal SehensPaintBox.PaintedTraceList GetPaintedTraces(bool selectedOnly = false, int groupStart = 0, int groupMax = int.MaxValue)
        {
            SehensPaintBox.PaintedTraceList result = new SehensPaintBox.PaintedTraceList();
            lock (m_ViewGroupsLock)
            {
                result.VisibleTraceGroupList = new List<List<TraceView>>();
                result.VisibleTraceList = new List<TraceView>();
                List<TraceView> list = new List<TraceView>();

                double heightFactorSum = 0;
                double traceHeightFactorSumTop = 0;
                foreach (var traceGroup in m_ViewGroups.Skip(groupStart).Take(groupMax))
                {
                    List<TraceView> viewsInGroup = new List<TraceView>();
                    foreach (TraceView item in traceGroup)
                    {
                        if (item.Visible && (!selectedOnly || item.Selected))
                        {
                            if (viewsInGroup.Count == 0)
                            {
                                traceHeightFactorSumTop = heightFactorSum;
                                heightFactorSum += traceGroup.Count == 0 ? 0 : item.HeightFactor;
                            }
                            item.Painted = new TraceView.PaintedInfo()
                            {
                                HeightAdjustSumTop = traceHeightFactorSumTop,
                                HeightAdjustSumBottom = heightFactorSum,
                                TraceIndex = viewsInGroup.Count,
                                GroupIndex = result.VisibleTraceGroupList.Count,
                                Group = viewsInGroup,
                                ClickZones = new List<TraceViewClickZone>(),
                            };
                            viewsInGroup.Add(item);
                            result.VisibleTraceList.Add(item);
                        }
                        list.Add(item);
                    }
                    if (viewsInGroup.Count > 0)
                    {
                        result.VisibleTraceGroupList.Add(viewsInGroup);
                    }
                }
                result.AllTraceList = list;
                int groupCount = result.VisibleTraceGroupList.Count;
                foreach (var x in result.VisibleTraceList)
                {
                    x.Painted.GroupCount = groupCount;
                    x.Painted.HeightAdjustSum = heightFactorSum;
                };
            }

            return result;
        }

        ////////////////////////////////////////////////////////////////
        //other functions

        private class ViewProxy : ITraceView, IDisposable
        {
            private SehensControl Owner;
            public bool Visible => false;
            public bool IsViewer => false;
            public Color Colour { set { } }

            public ViewProxy(SehensControl owner) { Owner = owner; }

            public void TraceDataRename(TraceData sender, string oldName, string newName)
            {
                Owner.ProxyRenameTrace(oldName, newName, sender);
            }

            public void TraceDataClosed(TraceData sender)
            {
                sender.RemoveViewer(this);
                Owner.ProxyRemoveTrace(sender.Name);
            }

            public void TraceDataSamplesChanged(TraceData sender) { }
            public void TraceDataCalculatedSamplesChanged(TraceData sender) { }
            public void TraceDataSettingsChanged(TraceData sender) { }
            public void Dispose() { }
        }

        public new virtual void Dispose()
        {
            m_Closing = true;

            AllViews.ForEach(delegate (TraceView view)
            {
                view.Dispose();
            });

            m_ViewGroups.Clear();
            m_ToolTip.Dispose();
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            Clear();
            base.Dispose(disposing);
        }

        public void UpdateBitwise(string name, byte[] samples, double samplesPerSecond)
        {
            if (StopUpdates) return;
            for (int bit = 0; bit < 8; bit++)
            {
                this[$"{name}.{bit}"].Update(samples.Select(x => (x & 1 << bit) == 0 ? 0.0 : 1.0), samplesPerSecond);
            }
        }

        public void Import(string filename)
        {
            ImportExport.LoadWaveformsUsingExtension(this, new string[] { filename });
            /*
            if (argv[0].ToLower().EndsWith(".csv") && Scope != null)
            {
                Scope.LoadWaveformsCSV(argv[0], 0);
            }
            else if (argv[0].ToLower().EndsWith(".sehens") && Scope != null)
            {
                Scope.LoadStateBinary(argv[0]);
            }
            SehensWerte.Controls.Sehens.ImportExport.LoadWaveformsCSV(argv[0], Scope, 0);
            SehensWerte.Controls.Sehens.ImportExport.LoadStateBinary(argv[0], Scope);
            */
        }

        [TestClass]
        public class ScopeControlTests
        {
            [TestMethod]
            public void TestImportExport()
            {
                var scope1 = new SehensControl();
                string filename = System.IO.Path.GetTempFileName();
                SehensSave.SaveStateXml(filename, scope1);
                var scope2 = new SehensControl();
                SehensSave.LoadStateXml(filename, scope2);
            }
        }
    }
}
