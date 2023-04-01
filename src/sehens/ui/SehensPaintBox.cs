using SehensWerte.Files;
using SehensWerte.Maths;
using SehensWerte.Utils;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

namespace SehensWerte.Controls.Sehens
{
    public partial class SehensPaintBox : Control
    {
        private Action<CsvLog.Entry> OnLog;
        private SehensControl Scope;

        private int m_UpdateInProgressCounter;
        public bool UpdateInProgress => m_UpdateInProgressCounter > 0;
        public void BeginUpdate() { Interlocked.Increment(ref m_UpdateInProgressCounter); }
        public bool EndUpdate() { return Interlocked.Decrement(ref m_UpdateInProgressCounter) == 0; }

        public int PaintBoxWidth;
        public int PaintBoxRealHeight;
        public int PaintBoxVirtualHeight;
        public int PaintBoxVirtualOffset;

        private float StatsPaintWidth = 350f;

        private Thread m_PaintThread;
        private EventWaitHandle m_PaintThreadSemaphore;

        private double m_LastPaintFinishSeconds;

        private bool m_RateLimitedPaint = true;
        private bool m_Painting;
        private int m_PaintRequired;
        private bool m_PaintThreadStop;
        internal PaintedTraceList PaintedTraces = new PaintedTraceList();

        private class Stats
        {
            public RollingAverage PaintSeconds = new RollingAverage(10.0);
            public int PaintCount;
            public int OverTimeCount;
            public int InvalidateDuringPaintCount;
            public int PaintDuringPaintCount;
            public int DisplayedGroupCount;
            public int TotalGroupCount;
        }
        Stats m_Stats = new Stats();

        public Rectangle PaintBoxScreenRect;

        public CodeProfile Profile = new CodeProfile();

        public class PaintedTraceList
        {
            public List<List<TraceView>> VisibleTraceGroupList = new List<List<TraceView>>();
            public List<TraceView> VisibleTraceList = new List<TraceView>();
            public List<TraceView> AllTraceList = new List<TraceView>();
            public List<TraceView> SelectedTraceList => VisibleTraceGroupList.SelectMany(x => x).Where(x => x.Selected).ToList();
            public bool MouseOnEmbed;
        }

        public SehensPaintBox(SehensControl scope, Action<CsvLog.Entry> onLog)
        {
            OnLog = onLog;
            Scope = scope;
            SetStyle(ControlStyles.ResizeRedraw, value: true);
            SetStyle(ControlStyles.Selectable, value: false);
            SetStyle(ControlStyles.Opaque, value: false);
            SetStyle(ControlStyles.UserMouse, value: true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, value: false);
            SetStyle(ControlStyles.CacheText, value: true);
            SetStyle(ControlStyles.DoubleBuffer, value: true);
            SetStyle(ControlStyles.UserPaint, value: true);
            TabStop = false;
            AllowDrop = false;
            BackColor = SystemColors.Window;
            m_PaintThreadSemaphore = new EventWaitHandle(initialState: false, EventResetMode.AutoReset);
            m_PaintThread = new Thread(new ThreadStart(PaintRun));
            Paint += PaintBoxPaint;
        }

        public new void Invalidate()
        {
            if (m_Painting && Scope.PaintBoxShowStats)
            {
                m_Stats.InvalidateDuringPaintCount++;
            }
            Interlocked.Increment(ref m_PaintRequired);
            m_PaintThreadSemaphore.Set();
        }

        public void InvalidateDelayed()
        {
            Invalidate();
        }

        private List<string> StatStrings()
        {
            List<string> list = new List<string>();
            list.Add($"Paint average {(m_Stats.PaintSeconds.LastOutput * 1000.0).ToStringRound(3, 1)} ms");
            list.Add($"Paint {m_Stats.PaintCount}");
            list.Add($"Overtime {m_Stats.OverTimeCount}");
            list.Add($"Invalidate while in invalidate {m_Stats.InvalidateDuringPaintCount}");
            list.Add($"Paint while in paint {m_Stats.PaintDuringPaintCount}");
            list.Add($"Groups onscreen {m_Stats.DisplayedGroupCount}");
            list.Add($"Groups total {m_Stats.TotalGroupCount}");
            list.Add("");
            list.AddRange(Profile.ToStringList());
            return list;
        }


        private bool MouseOnGutter(TraceGroupDisplay info)
        {
            return Scope.PaintBoxMouse.Click != null
                    && info.BottomGutter.Contains(Scope.PaintBoxMouse.Click.X, Scope.PaintBoxMouse.Click.Y);
        }

        public override Cursor Cursor
        {
            get => base.Cursor;
            set
            {
                if (base.Cursor == value) return;
                base.Cursor = value;
                InvalidateDelayed();
            }
        }

        internal int MinimumGroupHeight =>
            Math.Max(
                50,
                Height / Math.Max(1, PaintedTraces.VisibleTraceGroupList.Count())
            );

        internal int DefaultGroupHeight =>
            Math.Max(
                MinimumGroupHeight,
                Math.Min(Height, (int)((Height - MinimumGroupHeight) * Scope.m_TraceHeightRatio) + MinimumGroupHeight)
            );



        ////////////////////////////////////////////////////////////////
        //paint
        protected override void OnPaint(PaintEventArgs pe)
        {
            if (m_PaintThread.ThreadState == System.Threading.ThreadState.Unstarted)
            {
                m_PaintThread.Start();
            }

            try
            {
                double seconds = HighResTimer.StaticSeconds;
                double betweenPaintSeconds = seconds - m_LastPaintFinishSeconds;
                m_Stats.PaintCount++;
                if (m_Painting)
                {
                    m_Stats.PaintDuringPaintCount++;
                    m_PaintRequired = 1;
                    Point point = PointToScreen(new Point(0, 0));
                    pe.Graphics.CopyFromScreen(point, new Point(0, 0), new Size(Width, Height), CopyPixelOperation.SourceCopy);
                    PaintWarningText(pe.Graphics, "Paint recursion");
                }
                else
                {
                    m_Painting = true;
                    base.OnPaint(pe);
                    m_Painting = false;
                }
                m_LastPaintFinishSeconds = HighResTimer.StaticSeconds;
                var thisPaintSeconds = m_LastPaintFinishSeconds - seconds;
                if (Scope.PaintBoxShowStats && (thisPaintSeconds / (thisPaintSeconds + betweenPaintSeconds)) > 0.5)
                {
                    m_Stats.OverTimeCount++;
                }

                double value = Math.Min(5.0, thisPaintSeconds);
                m_Stats.PaintSeconds.Insert(value);
                if (m_Stats.PaintSeconds.LastOutput > thisPaintSeconds * 50.0)
                {
                    m_Stats.PaintSeconds = new RollingAverage(m_Stats.PaintSeconds.Distance);
                }
                if (Scope.PaintBoxShowStats)
                {
                    using var font = Scope.ActiveSkin.StatsFont.Font;
                    using var backBrush = new SolidBrush(Scope.ActiveSkin.BackgroundColour);
                    using var brush = new SolidBrush(Scope.ActiveSkin.StatsFont.Color);
                    var list = StatStrings();

                    StatsPaintWidth = Math.Max(StatsPaintWidth, list.Max(x => pe.Graphics.MeasureString(x, font).Width));
                    float y = 10f;
                    foreach (string item in list)
                    {
                        RectangleF rectangleF = new RectangleF((float)(Width - StatsPaintWidth), y, StatsPaintWidth, font.Height);
                        pe.Graphics.FillRectangle(backBrush, rectangleF);
                        pe.Graphics.DrawString(item, font, brush, rectangleF);
                        y += font.Height;
                    }
                }
            }
            catch (Exception ex)
            {
                using var font = Scope.ActiveSkin.WarningFont.Font;
                using var brush = new SolidBrush(Scope.ActiveSkin.BackgroundColour);
                pe.Graphics.DrawString(ex.ToString(), font, brush, 5f, 5f);
                m_Painting = false;
            }
        }

        private void PaintRun()
        {
            while (m_PaintThread.IsAlive && !m_PaintThreadStop)
            {
                if (Interlocked.Exchange(ref m_PaintRequired, 0) > 0)
                {
                    m_PaintThreadSemaphore.Reset();
                    if (m_RateLimitedPaint)
                    {
                        double until = HighResTimer.StaticSeconds + Math.Min(1.0, m_Stats.PaintSeconds.LastOutput * 2.0);
                        while (HighResTimer.StaticSeconds < until && !m_PaintThreadStop)
                        {
                            int ms = (int)((until - HighResTimer.StaticSeconds) * 1000);
                            Scope.OnLog?.Invoke(new CsvLog.Entry($"PaintRun() delaying {ms} {Thread.CurrentThread.ManagedThreadId}", CsvLog.Priority.Info));
                            Thread.Sleep(Math.Min(100, Math.Max(1, ms)));
                        }
                    }
                    if (!m_PaintThreadStop && !UpdateInProgress)
                    {
                        try
                        {
                            if (IsHandleCreated)
                            {
                                Scope.OnLog?.Invoke(new CsvLog.Entry($"PaintRun() invalidating", CsvLog.Priority.Info));
                                BeginInvoke(base.Invalidate);
                            }
                        }
                        catch { }
                    }
                }
                m_PaintThreadSemaphore.WaitOne(1000 / 60);
            }
        }

        private void PaintBoxPaint(object? sender, PaintEventArgs e)
        {
            Profile.Enter();
            try
            {
                BackColor = Scope.ActiveSkin.BackgroundColour;
                PaintBoxScreenRect = Screen.FromControl(this).Bounds;
                Scope.RecalculateProjectionIfRequired();
                PaintedTraces = Scope.GetPaintedTraces();
                Scope.UpdateVerticalScrollbar();
                PaintGetRectangle();
                CalculateBefore();

                if (Scope.HighQualityRender)
                {
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                }
                PaintTraces(e.Graphics, TraceGroupDisplay.PaintFlags.Parallel);
                PaintCursor(e.Graphics);
                PaintMouseOverStats(e.Graphics);
                PaintMouseOver(e.Graphics);
                PaintStoppedWarning(e.Graphics);
                PaintDragSelect(e.Graphics);
                if (Scope.RecalculateProjectionIfRequired())
                {
                    Invalidate(); // something changed during paint
                }
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void CalculateBefore()
        {
            Profile.Enter();
            try
            {
                OnLog?.Invoke(new CsvLog.Entry("CalculateBefore", CsvLog.Priority.Debug));

                PaintedTraces.VisibleTraceList.ForEach(item =>
                {
                    item.CalculateOrder =
                        item.CalculateType == TraceView.CalculatedTypes.None
                            ? (item.TriggerTrace == null ? 0 : 1)
                            : (item.Samples.InputSampleCount == 0 ? 3 : 2);
                });

                List<TraceView> list = PaintedTraces.VisibleTraceList.OrderBy(x => x.CalculateOrder).ToList();

                for (int order = 0; order < 4; order++)
                { // finish the parallel for each order
                    Parallel.ForEach<TraceView>(
                            list.Where(x => x.CalculateOrder == order && x != null && x.ProcessAtDisplay),
                            trace => { trace.CalculateTrace(); });
                }
            }
            finally
            {
                Profile.Exit();
            }
        }


        private void PaintDragSelect(Graphics graphics)
        {
            Profile.Enter();
            try
            {
                if (Scope.PaintBoxMouse.ClickType != PaintBoxMouseInfo.Type.WipeSelect) return;

                using (Pen pen = new Pen(Scope.ActiveSkin.DefaultPenColour))
                {
                    var mouse = Scope.PaintBoxMouse;
                    int x1 = Math.Min(mouse.Click?.X ?? 0, mouse.WipeStart?.X ?? 0);
                    int x2 = Math.Max(mouse.Click?.X ?? 0, mouse.WipeStart?.X ?? 0);
                    int y1 = Math.Min(mouse.Click?.Y ?? 0, mouse.WipeStart?.Y ?? 0);
                    int y2 = Math.Max(mouse.Click?.Y ?? 0, mouse.WipeStart?.Y ?? 0);
                    graphics.DrawRectangle(pen, new Rectangle(x1, y1, x2 - x1, y2 - y1));
                }
                if (Scope.PaintBoxMouse.WipeStart != null && Scope.PaintBoxMouse.Click != null)
                {
                    List<TraceView> list = PaintedTraces.VisibleTraceGroupList[Scope.PaintBoxMouse.MouseDownGroupIndex];
                    MouseEventArgs topLeft;
                    string text = WipeSelectMouseOver(list, out topLeft);
                    if (text.Length != 0)
                    {
                        PaintMouseOverValue(graphics, text, topLeft);
                    }
                }
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void PaintMouseOver(Graphics graphics)
        {
            Profile.Enter();
            try
            {
                if (!PaintedTraces.MouseOnEmbed
                    && Scope.CursorMode != 0
                    && Scope.ShowHoverValue
                    && Scope.PaintBoxMouse.Click != null
                    && !Scope.PaintBoxMouse.HideCursor
                    && PaintedTraces.VisibleTraceGroupList.Count != 0)
                {
                    int index = Scope.MouseToGroupIndex(Scope.PaintBoxMouse.Click.Y);
                    if (index >= 0 && index < PaintedTraces.VisibleTraceGroupList.Count)
                    {
                        string text = "";
                        var list = PaintedTraces.VisibleTraceGroupList[index];
                        if (list.Count > 0)
                        {
                            text = list[0].Painter.GetHoverValue(list, Scope.PaintBoxMouse.Click);
                        }
                        if (text.Length > 0)
                        {
                            PaintMouseOverValue(graphics, text, Scope.PaintBoxMouse.Click);
                        }
                    }
                }
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void PaintMouseOverValue(Graphics graphics, string text, MouseEventArgs desired)
        {
            Profile.Enter();
            try
            {
                using Font font = Scope.ActiveSkin.HoverTextFont.Font;

                using Brush brush2 = Scope.ActiveSkin.HoverTextFont.Brush;
                SizeF sizeF = graphics.MeasureString(text, font);
                int y = desired.Y + 3;
                int x = desired.X + 3;
                if (y > Height - (int)sizeF.Height - 2)
                {
                    y -= (int)(sizeF.Height + 6f);
                }
                if (x > PaintBoxWidth - (int)sizeF.Width - 2)
                {
                    x -= (int)(sizeF.Width + 6f);
                }
                if (x < 0)
                {
                    x = 0;
                }
                if (y < 0)
                {
                    y = 0;
                }
                Rectangle rect = new Rectangle(x, y, (int)sizeF.Width + 3, (int)sizeF.Height + 3);
                if (Scope.ActiveSkin.HoverLabelColour != Color.Transparent)
                {
                    using Brush brush = new SolidBrush(Scope.ActiveSkin.HoverLabelColour);
                    using Pen pen = Scope.ActiveSkin.HoverTextFont.Pen;
                    graphics.FillRectangle(brush, rect);
                    graphics.DrawRectangle(pen, rect);
                }
                graphics.DrawString(text, font, brush2, rect.Left + 1, rect.Top + 1);
            }
            finally
            {
                Profile.Exit();
            }
        }

        private static void PaintException(Graphics graphics, TraceGroupDisplay group, Exception e)
        {
            string text = e.ToString();
            using Font font = group.Skin.HoverTextFont.Font;
            using Brush brush = group.Skin.HoverTextFont.Brush;
            SizeF sizeF = graphics.MeasureString(text, font);
            graphics.DrawString(text, font, brush, 10f, group.ProjectionArea.Bottom - (int)sizeF.Height - 3);
        }

        private static void PaintWarning(Graphics graphics, TraceGroupDisplay group, string warn)
        {
            using Font font = group.Skin.WarningFont.Font;
            using Brush brush = group.Skin.WarningFont.Brush;
            SizeF sizeF = graphics.MeasureString(warn, font);
            graphics.DrawString(warn, font, brush, group.ProjectionArea.Right - (int)sizeF.Width - 3, group.ProjectionArea.Bottom - (int)sizeF.Height - 3);
        }

        private void PaintStoppedWarning(Graphics graphics)
        {
            string text = (Scope.StopUpdates ? " (Stopped)" : "") + ((PaintBoxVirtualHeight > PaintBoxRealHeight) ? " (scroll)" : "");
            if (text.Length > 0)
            {
                PaintWarningText(graphics, text);
            }
        }

        private void PaintWarningText(Graphics graphics, string text)
        {
            using Font font = Scope.ActiveSkin.WarningFont.Font;
            using Brush brush = Scope.ActiveSkin.WarningFont.Brush;
            SizeF sizeF = graphics.MeasureString(text, font);
            graphics.DrawString(text, font, brush, Width - (int)sizeF.Width - 3, Height - (int)sizeF.Height - 3);
        }

        internal Bitmap ScreenshotToBitmap(Skin skin, int? singleGroup)
        {
            Skin prevActiveSkin = Scope.ActiveSkin;
            Scope.ActiveSkin = skin;
            try
            {
                Scope.PaintBoxMouse.Click = null;
                Scope.PaintBoxMouse.MouseGuiSection = PaintBoxMouseInfo.GuiSection.None;
                var copyPaintedGroup = PaintedTraces;
                PaintedTraces = Scope.GetPaintedTraces(
                    selectedOnly: skin!.ExportTraces == Skin.TraceSelections.SelectedTraces,
                    groupStart: singleGroup ?? 0,
                    groupMax: singleGroup == null ? int.MaxValue : 1);

                Rectangle rectangle = new Rectangle(0, 0, Scope.ActiveSkin.TraceWidth, PaintedTraces.VisibleTraceGroupList.Count * Scope.ActiveSkin.TraceHeight);
                Bitmap? result = null;
                if (PaintedTraces.VisibleTraceGroupList.Count > 0)
                {
                    result = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format24bppRgb);
                    using Graphics graphics = Graphics.FromImage(result);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    PaintBoxWidth = rectangle.Width;
                    PaintBoxVirtualHeight = (PaintBoxRealHeight = rectangle.Height);
                    PaintBoxVirtualOffset = 0;
                    Scope.RecalculateProjection();
                    Scope.RecalculateProjectionIfRequired();
                    CalculateBefore();
                    PaintTraces(graphics, TraceGroupDisplay.PaintFlags.Screenshot);
                    Scope.RecalculateProjection();
                }
                PaintedTraces = copyPaintedGroup;
                return result ?? new Bitmap(1, 1);
            }
            finally
            {
                Scope.ActiveSkin = prevActiveSkin;
            }
        }

        private void PaintGetRectangle()
        {
            Profile.Enter();
            try
            {
                int totalY = Math.Max(Height, Math.Max(Height, PaintedTraces.VisibleTraceGroupList.Count * DefaultGroupHeight));
                int scrollY = Math.Min(totalY - Height, Math.Max(0, Scope.VerticalScrollBar.Value));

                if (totalY != PaintBoxVirtualHeight
                    || scrollY != PaintBoxVirtualOffset
                    || Height != PaintBoxRealHeight
                    || Width != PaintBoxWidth)
                {
                    PaintBoxVirtualHeight = totalY;
                    PaintBoxVirtualOffset = scrollY;
                    PaintBoxRealHeight = Height;
                    PaintBoxWidth = Width;
                    Scope.RecalculateProjection();
                }
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void PaintTraces(Graphics graphics, TraceGroupDisplay.PaintFlags flags)
        {
            Profile.Enter();
            try
            {
                PaintedTraces.MouseOnEmbed = false;
                if (PaintedTraces.VisibleTraceGroupList.Count == 0)
                {
                    using Brush brush = new SolidBrush(Scope.ActiveSkin.BackgroundColour);
                    graphics.FillRectangle(brush, 0, 0, Width, Height);
                }
                else
                {
                    object graphicsLock = new object();

                    int displayedGroupCount = 0;
                    int totalGroupCount = 0;
                    var action = delegate (List<TraceView> views)
                    {
                        if (views.Count > 0)
                        {
                            TraceGroupDisplay info = TraceToGroupDisplayInfo(views[0], flags);
                            if (info.IsOnScreen)
                            {
                                if (flags.HasFlag(TraceGroupDisplay.PaintFlags.Parallel))
                                { // paint to a temporary bitmap
                                    Rectangle rect = info.GroupArea;
                                    using Bitmap bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
                                    using Graphics bitmapGraphics = Graphics.FromImage(bitmap);
                                    PaintTraceGroup(bitmapGraphics, flags, views);
                                    lock (graphicsLock)
                                    {
                                        graphics.DrawImage(bitmap, 0, info.PaintVerticalOffset);
                                    }
                                }
                                else
                                {
                                    PaintTraceGroup(graphics, flags, views);
                                }
                                Interlocked.Increment(ref displayedGroupCount);
                            }
                            Interlocked.Increment(ref totalGroupCount);
                        }
                    };

                    if (flags.HasFlag(TraceGroupDisplay.PaintFlags.Parallel))
                    {
                        Parallel.ForEach(PaintedTraces.VisibleTraceGroupList, action);
                    }
                    else
                    {
                        PaintedTraces.VisibleTraceGroupList.ForEach(action);
                    }

                    m_Stats.DisplayedGroupCount = displayedGroupCount;
                    m_Stats.TotalGroupCount = totalGroupCount;
                }
                if (Scope.m_BackgroundThreadCount > 0)
                {
                    PaintWarningText(graphics, "Background operation...");
                }
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void PaintTraceGroup(Graphics graphics, TraceGroupDisplay.PaintFlags flags, List<TraceView> views)
        {
            Profile.Enter();
            try
            {
                TraceView view = views[0];
                TraceGroupDisplay info0 = TraceToGroupDisplayInfo(view, flags);

                PaintSelection(graphics, info0, views);
                view.Painter.PaintInitial(graphics, info0);
                PaintGutters(info0, graphics);
                PaintHorizontalAxis(graphics, info0);
                PaintAxesTitles(graphics, info0);
                PaintVerticalAxis(graphics, info0);
                foreach (TraceView item in views)
                {
                    TraceGroupDisplay info = TraceToGroupDisplayInfo(item, flags);
                    if (Scope.ShowTraceFeatures)
                    {
                        item.Painter.PaintFeatures(graphics, info,
                            info.View0.Samples.ViewedFeatures.Where(x => x.Type == TraceFeature.Feature.Highlight));
                    }
                    PaintTraceSamples(graphics, info);
                    item.Painter.PaintStats(graphics, info);
                    item.Painter.PaintLabel(graphics, info);
                    if (Scope.ShowTraceFeatures)
                    {
                        item.Painter.PaintFeatures(graphics, info,
                            info.View0.Samples.ViewedFeatures.Where(x => x.Type != TraceFeature.Feature.Highlight));
                    }
                    item.Painter.PaintEmbeddedControls(graphics, info, Scope.ContextMenu);
                    if (info.MouseOnEmbed)
                    {
                        PaintedTraces.MouseOnEmbed = true;
                    }
                }
            }
            catch (Exception ex)
            {
                using Font font = Scope.ActiveSkin.HoverTextFont.Font;
                using Brush brush = Scope.ActiveSkin.HoverTextFont.Brush;
                graphics.DrawString(ex.ToString(), font, brush, 5f, 5f);
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void PaintSelection(Graphics graphics, TraceGroupDisplay info, List<TraceView> views)
        {
            Profile.Enter();
            try
            {
                using Brush brush = new SolidBrush(
                        !info.Flags.HasFlag(TraceGroupDisplay.PaintFlags.Screenshot) && views.Any(x => x.Selected)
                            ? Scope.ActiveSkin.SelectedContextColour
                            : Scope.ActiveSkin.BackgroundColour);
                graphics.FillRectangle(brush, info.GroupArea);
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void PaintGutters(TraceGroupDisplay info, Graphics graphics)
        {
            Profile.Enter();
            try
            {
                using (Brush brush = new SolidBrush(info.Skin.GutterColour))
                {
                    graphics.FillRectangle(brush, info.RightGutter);
                    graphics.FillRectangle(brush, info.VerticalAxisArea);
                    graphics.FillRectangle(brush, info.LeftGutter);
                    graphics.FillRectangle(brush, info.TopGutter);
                    Color gutterColour = info.Skin.GutterColour;
                    using Pen pen2 = new Pen(Skin.ChangeLightness(gutterColour, 1.05f));
                    using Pen pen = new Pen(Skin.ChangeLightness(gutterColour, 1.1f));
                    using Pen pen3 = new Pen(Skin.ChangeLightness(gutterColour, 0.9f));
                    using Pen pen4 = new Pen(Skin.ChangeLightness(gutterColour, 0.7f));
                    graphics.FillRectangle(brush, info.BottomGutter);
                    graphics.DrawLine(pen, info.BottomGutter.Left, info.BottomGutter.Top, info.BottomGutter.Right, info.BottomGutter.Top);
                    graphics.DrawLine(pen2, info.BottomGutter.Left, info.BottomGutter.Top + 1, info.BottomGutter.Right, info.BottomGutter.Top + 1);
                    graphics.DrawLine(pen3, info.BottomGutter.Left, info.BottomGutter.Bottom - 1, info.BottomGutter.Right, info.BottomGutter.Bottom - 1);
                    graphics.DrawLine(pen4, info.BottomGutter.Left, info.BottomGutter.Bottom, info.BottomGutter.Right, info.BottomGutter.Bottom);
                }
                using Pen pen5 = new Pen(info.Skin.AxisLineColour);
                switch (info.Skin.AxisLineStyle)
                {
                    case Skin.AxisLines.All:
                        graphics.DrawRectangle(pen5, info.ProjectionArea.Left - 1, info.ProjectionArea.Top - 1, info.ProjectionArea.Width + 2, info.ProjectionArea.Height + 2);
                        break;
                    case Skin.AxisLines.BottomLeft:
                        graphics.DrawLine(pen5, info.ProjectionArea.Left - 1, info.ProjectionArea.Top - 1, info.ProjectionArea.Left - 1, info.ProjectionArea.Bottom + 1);
                        graphics.DrawLine(pen5, info.ProjectionArea.Left - 1, info.ProjectionArea.Bottom + 1, info.ProjectionArea.Right + 1, info.ProjectionArea.Bottom + 1);
                        break;
                    case Skin.AxisLines.None:
                        break;
                }
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void PaintHorizontalAxis(Graphics graphics, TraceGroupDisplay info)
        {
            Profile.Enter();
            try
            {
                info.View0.Painter.PaintHorizontalAxis(graphics, info);
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void PaintVerticalAxis(Graphics graphics, TraceGroupDisplay divisionInfo)
        {
            Profile.Enter();
            try
            {
                if (divisionInfo.ProjectionArea.Height > Scope.ActiveSkin.AxisTextFont.LineSpacing * 2 && divisionInfo.VerticalAxisArea.Width > 0)
                {
                    divisionInfo.View0.Painter.PaintVerticalAxis(graphics, divisionInfo);
                }
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void PaintAxesTitles(Graphics graphics, TraceGroupDisplay info)
        {
            Profile.Enter();
            try
            {
                if (Scope.ActiveSkin.ShowAxisLabels)
                {
                    info.View0.Painter.PaintAxisTitleHorizontal(graphics, info);
                    if (info.ProjectionArea.Height > Scope.ActiveSkin.AxisTitleFont.LineSpacing * 2 && info.VerticalAxisArea.Width > 0)
                    {
                        info.View0.Painter.PaintAxisTitleVertical(graphics, info);
                    }
                }
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void PaintTraceSamples(Graphics graphics, TraceGroupDisplay info)
        {
            Profile.Enter();
            try
            {
                TraceView view = info.View0;
                string text =
                    (view.HoldPanZoom ? " (Hold)" : "")
                    + ((info.ViewLengthOverride != 0 || info.ViewOffsetOverride != 0) ? " (Offset)" : "")
                    + (view.CalculateType == TraceView.CalculatedTypes.None ? "" : " (Calc)");
                if (text.Length > 0)
                {
                    PaintWarning(graphics, info, text);
                }
                Profile.Enter(view.Painter.ToString() ?? "null painter tostring");
                try
                {
                    view.Painter.PaintProjection(graphics, info);
                }
                catch (PainterException e)
                {
                    PaintException(graphics, info, e);
                }
                finally
                {
                    Profile.Exit(view.Painter.ToString() ?? "null painter tostring");
                }
                if (Scope.TraceAutoRange && view.AutoRange(requestShrink: false))
                {
                    InvalidateDelayed();
                }
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void PaintCursor(Graphics graphics)
        {
            var mouse = Scope.PaintBoxMouse;
            if (mouse.Click == null || mouse.HideCursor || Scope.CursorMode == Skin.Cursors.Pointer) return;

            Profile.Enter();
            try
            {
                int x = mouse.Click.X;
                int y = mouse.Click.Y;
                using Pen pen = new Pen(Scope.ActiveSkin.CrossHairColour);
                switch (Scope.CursorMode)
                {
                    case Skin.Cursors.CrossHair:
                        graphics.DrawLine(pen, new Point(0, y), new Point(x - 15, y));
                        graphics.DrawLine(pen, new Point(x + 15, y), new Point(PaintBoxWidth, y));
                        graphics.DrawLine(pen, new Point(x, 0), new Point(x, y - 15));
                        graphics.DrawLine(pen, new Point(x, y + 15), new Point(x, PaintBoxRealHeight));
                        break;
                    case Skin.Cursors.VerticalLine:
                        graphics.DrawLine(pen, new Point(x, 0), new Point(x, PaintBoxRealHeight));
                        break;
                }
            }
            finally
            {
                Profile.Exit();
            }
        }

        private void PaintMouseOverStats(Graphics graphics)
        {
            var mouse = Scope.PaintBoxMouse;
            if (PaintedTraces.MouseOnEmbed || !Scope.ShowHoverInfo || Scope.CursorMode == Skin.Cursors.Pointer || mouse.Click == null || mouse.HideCursor) return;

            Profile.Enter();
            try
            {
                int mouseX = mouse.Click.X;
                int mouseY = mouse.Click.Y;
                using Brush labelBrush = new SolidBrush(Scope.ActiveSkin.HoverLabelColour);
                using Brush fontBrush = Scope.ActiveSkin.HoverTextFont.Brush;
                using Pen pen = Scope.ActiveSkin.HoverTextFont.Pen;
                using Font font = Scope.ActiveSkin.HoverTextFont.Font;

                bool[] rows = new bool[PaintBoxVirtualHeight];
                foreach (TraceView item in PaintedTraces.VisibleTraceGroupList.SelectMany(x => x))
                {
                    TraceGroupDisplay info = TraceToGroupDisplayInfo(item);
                    if (info.ProjectionArea.Contains(mouseX, mouseY) && info.IsOnScreen)
                    {
                        TraceView.MouseInfo clickInfo = item.Measure(mouse.Click);
                        string text = item.TraceHoverStatistics(clickInfo);

                        int y = item.Painter.HoverLabelYFromOffsetX(info, mouseX) + info.GroupArea.Top;
                        SizeF sizeF = graphics.MeasureString(text, font);

                        Rectangle rect = new Rectangle(mouseX, y, (int)sizeF.Width + 3, (int)sizeF.Height + 3);
                        for (int loop = rect.Top; loop < rect.Bottom && loop + PaintBoxVirtualOffset < PaintBoxVirtualHeight; loop++)
                        {
                            int row = loop + PaintBoxVirtualOffset;
                            if (row >= 0 && row < rows.Length && rows[row])
                            {
                                loop = rect.Top;
                                rect.Y++;
                            }
                        }

                        if (rect.Bottom >= PaintBoxVirtualHeight - 1)
                        {
                            rect.Y -= rect.Bottom - PaintBoxVirtualHeight + 1;
                        }
                        if (rect.Right >= PaintBoxWidth - 4)
                        {
                            rect.X -= rect.Right - PaintBoxWidth + 4;
                        }
                        if (rect.X < 0)
                        {
                            rect.X = 0;
                        }
                        if (rect.Y < 0)
                        {
                            rect.Y = 0;
                        }

                        for (int row = rect.Top + PaintBoxVirtualOffset; row < rect.Bottom + PaintBoxVirtualOffset && row < PaintBoxVirtualHeight; row++)
                        {
                            rows[row] = true;
                        }

                        graphics.FillRectangle(labelBrush, rect);
                        graphics.DrawRectangle(pen, rect);
                        graphics.DrawString(text, font, fontBrush, rect.Left + 1, rect.Top + 1);
                    }
                }
            }
            finally
            {
                Profile.Exit();
            }
        }

        ////////////////////////////////////////////////////////////////
        //Strings

        private string WipeSelectMouseOver(List<TraceView> list, out MouseEventArgs topLeft)
        {
            StringBuilder stringBuilder = new StringBuilder();
            topLeft = Scope.PaintBoxMouse?.WipeTopLeft ?? new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0);
            var bottomRight = Scope.PaintBoxMouse?.WipeBottomRight ?? new MouseEventArgs(MouseButtons.Left, 0, 0, 0, 0);

            foreach (TraceView item in list)
            {
                TraceView.MouseInfo wipeTopLeft = item.Measure(topLeft);
                TraceView.MouseInfo wipeBottomRight = item.Measure(bottomRight);

                void line(string label, double[] data)
                {
                    double[] array = data.Copy(wipeTopLeft.IndexAfterTrim, wipeBottomRight.IndexAfterTrim - wipeTopLeft.IndexAfterTrim);
                    if (array.Length > 0)
                    {
                        Statistics statistics = new Statistics(array);
                        double area = array.Sum() - array.Length * ((array[0] + array[^1]) / 2.0);
                        stringBuilder.AppendLine($"{label} {item.DecoratedName} {statistics} area={area}");
                    }
                }

                double[]? samples;

                samples = item.RawBeforeZoom;
                if (samples != null && samples.Length != 0)
                {
                    line("raw", samples);
                }
                samples = item.CalculatedBeforeZoom;
                if (samples != null && samples.Length != 0)
                {
                    line("calculated", samples);
                }
            }
            return stringBuilder.ToString();
        }



        ////////////////////////////////////////////////////////////////
        //Other

        public TraceGroupDisplay TraceToGroupDisplayInfo(TraceView trace, TraceGroupDisplay.PaintFlags flags = TraceGroupDisplay.PaintFlags.None)
        {
            var info = new TraceGroupDisplay(Scope.PaintBoxMouse, PaintBoxScreenRect, this, trace, flags);
            info.ShowHorizontalUnits = info.View0.Samples.InputSamplesPerSecond != 0.0 && !MouseOnGutter(info);
            return info;
        }

        protected override void Dispose(bool disposing)
        {
            if (m_PaintThread.ThreadState != System.Threading.ThreadState.Unstarted)
            {
                m_PaintThreadStop = true;
                m_PaintThreadSemaphore.Set();
                m_PaintThread.Join();
            }
            base.Dispose(disposing);
        }

    }
}
