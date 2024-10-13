using SehensWerte.Files;
using SehensWerte.Utils;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using static SehensWerte.Files.CsvLog;

namespace SehensWerte.Controls
{
    public class LogControl : UserControl
    {
        private Color DefaultColorFore = Color.FromKnownColor(KnownColor.Black);
        private Color DefaultColorBack = Color.FromKnownColor(KnownColor.ControlLightLight);
        private Pen WarningPen = new Pen(Color.FromArgb(128, Color.Red));
        private Brush WarningBrush;
        private Font TextFont = new Font("Courier New", 8);
        private Font WarningFont = new Font("Courier New", 10);
        private Brush BackgroundBrush;
        private bool m_NewItems;
        private string m_ToolTipString = "";

        private CheckBox CheckPause;
        private Panel PanelView;
        private PictureBox PaintBox;
        private VScrollBar VerticalBar;
        private CheckBox CheckScroll;
        private CheckBox CheckToolTip;
        private System.Windows.Forms.Timer Timer;
        private ContextMenuStrip ContextMenu;
        private ToolStripMenuItem ClearMenuItem;
        private ToolStripMenuItem CopyToClipboardMenuItem;
        private TextBox TextFilter;
        private ToolTip ToolTip;
        private HScrollBar HorizontalBar;
        private Panel panelUI;
        private ComboBox ComboFilter;
        public const int LineHeight = 15;

        private ConcurrentQueue<LogEntry> LogInputQueue = new ConcurrentQueue<LogEntry>();
        private LinkedList<LogEntryRow> LogQueue = new LinkedList<LogEntryRow>();
        private LinkedList<LogEntryRow> FilteredQueue = new LinkedList<LogEntryRow>();
        private LinkedList<LogEntryRow> DisplayedQueue => Filtering ? FilteredQueue : LogQueue;
        private LogEntryRow[]? m_DisplayedEntries;
        private CsvLog? m_LogFile;
        private static string LogFilename = DateTime.Now.ToString("yyyyMMddHHmmsszzz").Replace(":", "");

        private int DisplayedLines => Math.Max(0, PaintBox.Height / LineHeight);
        private bool Filtering => m_FilterRegex != null || m_FilterType != 0;
        private Regex? m_FilterRegex = null;

        private bool m_CompressLogFile;
        public bool CompressLogFile
        {
            get => m_CompressLogFile;
            set { m_LogFile?.Close(); m_CompressLogFile = value; }
        }

        public string FilterString
        {
            get => TextFilter.Text;
            set { TextFilter.Text = value; }
        }

        private CsvLog.Priority m_FilterType = CsvLog.Priority.Info;
        public CsvLog.Priority FilterType
        {
            get => m_FilterType;
            set
            {
                if (m_FilterType != value)
                {
                    m_FilterType = value;
                    ComboFilter.SelectedIndex = ComboFilter.Items.IndexOf(value.ToString());
                    FilterChanged();
                }
            }
        }

        public int ItemLimit { get; set; } = 5000;

        private int m_ScrollIndex = 0;
        public int ScrollIndex
        {
            get => m_ScrollIndex;
            set { m_ScrollIndex = value; UpdateScrollBars(); PaintBox.Invalidate(); }
        }

        private string m_LogFolder = "";
        private string m_LogFolderFullPath = "";
        public string LogFolder
        {
            get
            {
                bool designMode = LicenseManager.UsageMode == LicenseUsageMode.Designtime;
                return !DesignMode && !designMode ? m_LogFolderFullPath : m_LogFolder;
            }
            set
            {
                bool designMode = LicenseManager.UsageMode == LicenseUsageMode.Designtime;
                m_LogFolder = value;
                if (!DesignMode && !designMode && value != "")
                {
                    m_LogFile?.Close();
                    m_LogFolderFullPath = System.IO.Path.GetFullPath(value);
                    Directory.CreateDirectory(m_LogFolderFullPath);
                }
            }
        }

        private Color m_textBackColor = Color.White;
        public Color TextBackColor
        {
            get => m_textBackColor;
            set
            {
                this.m_textBackColor = value;
                PaintBox.BackColor = value;
                TextFilter.BackColor = value;
                PaintBox.Invalidate();
            }
        }

        private void EnsureLogFileWriter()
        {
            if (m_LogFile == null)
            {
                string fileName;
                int index = 0;
                do
                {
                    fileName = System.IO.Path.Combine(LogFolder, LogFilename + ((index == 0) ? "" : "." + index.ToString()) + (m_CompressLogFile ? ".gz" : ".csv"));
                    index++;
                } while (System.IO.File.Exists(fileName));
                m_LogFile = new CsvLog(fileName);
            }
        }

        public class LogEntry
        {
            public CsvLog.Entry? Data;
            public string DisplayedLine = "";
            public Color LineColor;
            public Color BackColor;
            internal string ToDisplayedLine()
            {
                var p1 = Data?.CallPath ?? "";
                var p2 = p1.Trim() + (p1 == "" ? "" : " - ");
                var p3 = ((Data?.Data ?? "") + " " + (Data?.Fields ?? "") + " " + (Data?.Binary == null ? "" : Data.Binary.ToHex())).Trim();
                return ("[" + Data?.Priority.ToString() + "] " + p2 + Data?.Text + " " + p3).Trim();
            }
        }

        private class LogEntryRow
        {
            public LogEntry? Original;
            public string DisplayedLine = "";
            public bool FilterMatchFlag = true;

            public bool MatchesFilter(CsvLog.Priority minimumType, Regex? regex)
            {
                if (Original?.DisplayedLine == null) return false;
                return (Original.Data?.Priority >= minimumType) && (regex == null || regex.IsMatch(Original.DisplayedLine));
            }

            public string ToToolTip()
            {
                string original = "";
                if (Original != null)
                    original = string.Join(Environment.NewLine,
                                    Original.DisplayedLine.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));

                string extraData = "";
                if (Original?.Data != null)
                {
                    extraData += (Original.Data.SourcePath == null || Original.Data.SourcePath == "")
                        ? "" :
                        System.IO.Path.GetFileName(Original.Data.SourcePath) + "[" + Original.Data.SourceLineNumber + "] " + (Original.Data.MemberName ?? "") + Environment.NewLine;
                    extraData += (Original.Data.CallPath == null || Original.Data.CallPath == "")
                        ? ""
                        : "Path: " + Original.Data.CallPath + Environment.NewLine;
                }
                string toolTip = (Original?.DisplayedLine == DisplayedLine)
                                ? DisplayedLine
                                : (DisplayedLine + Environment.NewLine + Environment.NewLine + original);

                toolTip = toolTip + (extraData == "" ? "" : Environment.NewLine + Environment.NewLine + extraData.TrimEnd());
                return toolTip;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_LogFile?.Close();
            }
            base.Dispose(disposing);
        }

        public LogControl()
        {
            WarningBrush = new SolidBrush(WarningPen.Color);

            CheckPause = new CheckBox();
            PaintBox = new PictureBox();
            ContextMenu = new ContextMenuStrip();
            ClearMenuItem = new ToolStripMenuItem();
            CopyToClipboardMenuItem = new ToolStripMenuItem();
            VerticalBar = new VScrollBar();
            PanelView = new Panel();
            HorizontalBar = new HScrollBar();
            CheckScroll = new CheckBox();
            CheckToolTip = new CheckBox();
            Timer = new System.Windows.Forms.Timer();
            TextFilter = new TextBox();
            ToolTip = new ToolTip();
            panelUI = new Panel();
            ComboFilter = new ComboBox();

            ((ISupportInitialize)PaintBox).BeginInit();
            ContextMenu.SuspendLayout();
            PanelView.SuspendLayout();
            panelUI.SuspendLayout();
            SuspendLayout();

            CheckPause.AutoSize = true;
            CheckPause.Dock = DockStyle.Left;
            CheckPause.TextAlign = ContentAlignment.MiddleLeft;
            CheckPause.Margin = new Padding(4, 5, 4, 5);
            CheckPause.Size = new Size(80, 32);
            CheckPause.TabIndex = 11;
            CheckPause.Text = "Pause";
            CheckPause.UseVisualStyleBackColor = true;
            CheckPause.CheckedChanged += (sender, e) => { PaintBox.Invalidate(); };

            CheckScroll.AutoSize = true;
            CheckScroll.Dock = DockStyle.Left;
            CheckScroll.Checked = true;
            CheckScroll.TextAlign = ContentAlignment.MiddleLeft;
            CheckScroll.CheckState = CheckState.Checked;
            CheckScroll.Margin = new Padding(4, 5, 4, 5);
            CheckScroll.Size = new Size(74, 32);
            CheckScroll.TabIndex = 14;
            CheckScroll.Text = "Scroll";
            CheckScroll.UseVisualStyleBackColor = true;

            CheckToolTip.AutoSize = true;
            CheckToolTip.Dock = DockStyle.Left;
            CheckToolTip.Checked = true;
            CheckToolTip.TextAlign = ContentAlignment.MiddleLeft;
            CheckToolTip.CheckState = CheckState.Unchecked;
            CheckToolTip.Margin = new Padding(4, 5, 4, 5);
            CheckToolTip.Size = new Size(74, 32);
            CheckToolTip.TabIndex = 14;
            CheckToolTip.Text = "Mouse-over text";
            CheckToolTip.UseVisualStyleBackColor = true;

            PaintBox.ContextMenuStrip = ContextMenu;
            PaintBox.Dock = DockStyle.Fill;
            PaintBox.Location = new Point(0, 0);
            PaintBox.Margin = new Padding(4, 5, 4, 5);
            PaintBox.Size = new Size(604, 315);
            PaintBox.TabIndex = 14;
            PaintBox.TabStop = false;
            PaintBox.Click += (a, b) => PaintBox.Focus();
            PaintBox.Paint += new PaintEventHandler(PaintBox_Paint);
            PaintBox.MouseMove += new MouseEventHandler(PaintBox_MouseMove);
            PaintBox.MouseWheel += (o, e) => { ScrollIndex -= e.Delta / 120 * SystemInformation.MouseWheelScrollLines; };
            PaintBox.Resize += (o, e) => UpdateScrollBars();

            ContextMenu.ImageScalingSize = new Size(24, 24);
            ContextMenu.Items.AddRange(new ToolStripItem[] { ClearMenuItem, CopyToClipboardMenuItem });
            ContextMenu.Size = new Size(255, 106);

            ClearMenuItem.Text = "&Clear";
            ClearMenuItem.Click += ClearMenuItem_CLick;

            CopyToClipboardMenuItem.Text = "C&opy to clipboard";
            CopyToClipboardMenuItem.Click += CopyToClipboardMenuItem_Click;

            VerticalBar.Dock = DockStyle.Right;
            VerticalBar.Location = new Point(604, 0);
            VerticalBar.Size = new Size(20, 332);
            VerticalBar.TabIndex = 15;
            VerticalBar.Scroll += (o, e) =>
            {
                if (e.Type != ScrollEventType.ThumbPosition && e.Type != ScrollEventType.EndScroll)
                {
                    ScrollIndex = e.NewValue;
                }
            };

            PanelView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            PanelView.AutoScroll = true;
            PanelView.BackColor = Color.Transparent;
            PanelView.BorderStyle = BorderStyle.FixedSingle;
            PanelView.Controls.Add(PaintBox);
            PanelView.Controls.Add(HorizontalBar);
            PanelView.Controls.Add(VerticalBar);
            PanelView.Location = new Point(4, 4);
            PanelView.Margin = new Padding(4, 5, 4, 5);
            PanelView.Size = new Size(623, 363);
            PanelView.TabIndex = 17;

            HorizontalBar.Dock = DockStyle.Bottom;
            HorizontalBar.LargeChange = 50;
            HorizontalBar.Location = new Point(0, 315);
            HorizontalBar.Size = new Size(604, 17);
            HorizontalBar.TabIndex = 16;
            HorizontalBar.Scroll += (sender, e) => { PaintBox.Invalidate(); };

            Timer.Enabled = true;
            Timer.Interval = 1000;
            Timer.Tick += (sender, e) => DrainInputQueue();

            TextFilter.Dock = DockStyle.Fill;
            TextFilter.Location = new Point(267, 0);
            TextFilter.Margin = new Padding(4, 5, 4, 5);
            TextFilter.Size = new Size(238, 26);
            TextFilter.TabIndex = 15;
            TextFilter.TextChanged += (sender, e) => FilterChanged();


            ToolTip.AutoPopDelay = 30000;
            ToolTip.InitialDelay = 500;
            ToolTip.ReshowDelay = 100;

            panelUI.Controls.Add(TextFilter);
            panelUI.Controls.Add(ComboFilter);
            panelUI.Controls.Add(CheckScroll);
            panelUI.Controls.Add(CheckToolTip);
            panelUI.Controls.Add(CheckPause);
            panelUI.Dock = DockStyle.Bottom;
            panelUI.Location = new Point(0, 371);
            panelUI.Margin = new Padding(4, 5, 4, 5);
            panelUI.Size = new Size(632, 32);
            panelUI.TabIndex = 18;

            ComboFilter.Dock = DockStyle.Right;
            ComboFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            ComboFilter.FormattingEnabled = true;
            ComboFilter.Location = new Point(505, 0);
            ComboFilter.Margin = new Padding(4, 5, 4, 5);
            ComboFilter.Size = new Size(118, 28);
            ComboFilter.TabIndex = 19;
            ComboFilter.SelectedIndexChanged += (sender, e) => { FilterType = (CsvLog.Priority)m_FilterType.EnumValue(ComboFilter.Text); };

            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Transparent;
            Controls.Add(PanelView);
            Controls.Add(panelUI);
            Margin = new Padding(4, 5, 4, 5);
            Name = "LogViewControl";
            Size = new Size(632, 403);
            Resize += Log_Resize;
            ((ISupportInitialize)PaintBox).EndInit();
            ContextMenu.ResumeLayout(false);
            PanelView.ResumeLayout(false);
            panelUI.ResumeLayout(false);
            panelUI.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

            PaintBox.BackColor = TextBackColor;
            TextFilter.BackColor = TextBackColor;
            ComboFilter.Items.AddRange(System.Enum.GetNames(typeof(CsvLog.Priority)));
            ComboFilter.SelectedIndex = ComboFilter.Items.IndexOf(FilterType.ToString());

            HorizontalBar.Visible = false;
            UpdateScrollBars();

            BackgroundBrush = new SolidBrush(this.BackColor);

            Add(new CsvLog.Entry("Logging started", CsvLog.Priority.Info));
        }

        static public Action<Entry> AddExtendPath(Action<Entry> prev, string callPath) // same as CsvLog version
        {
            return new Entry.CallPathProxy(prev, callPath).Add;
        }

        public void Add(CsvLog.Entry data)
        {
            Color colour = DefaultColorFore;
            switch (data.Priority)
            {
                case CsvLog.Priority.Debug: colour = Color.Blue; break;
                case CsvLog.Priority.Info: colour = DefaultColorFore; break;
                case CsvLog.Priority.Warn: colour = Color.Green; break;
                case CsvLog.Priority.Error: colour = Color.DarkOrange; break;
                case CsvLog.Priority.Exception: colour = Color.Red; break;
            }
            if (data.Time == null)
            {
                data.Time = HighResTimer.StaticNow;
            }
            var e = new LogEntry()
            {
                Data = data,
                LineColor = colour,
                BackColor = DefaultColorBack,
            };
            e.DisplayedLine = e.ToDisplayedLine();

            LogInputQueue.Enqueue(e);

            if (!m_NewItems && !CheckPause.Checked)
            {
                m_NewItems = true;
                this.BeginInvokeIfRequired((Action)PaintBox.Invalidate);
            }
        }
        private void DrainInputQueue()
        { // UI thread only
            if (CheckPause.Checked) return;

            LogEntry? originalEntry;
            while (LogInputQueue.TryDequeue(out originalEntry))
            {
                if (originalEntry != null && originalEntry.Data != null)
                {
                    try
                    {
                        EnsureLogFileWriter();
                        m_LogFile?.Add(originalEntry.Data);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    string[] split = originalEntry.DisplayedLine.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length > 0 && split[0] == originalEntry.DisplayedLine)
                    {
                        QueueEntry(new LogEntryRow() { Original = originalEntry, DisplayedLine = originalEntry.DisplayedLine });
                    }
                    else
                    {
                        foreach (string line in split)
                        {
                            QueueEntry(new LogEntryRow() { Original = originalEntry, DisplayedLine = line });
                        }
                    }
                }
            }
        }

        private void PaintBox_Paint(object? sender, PaintEventArgs e)
        {
            m_NewItems = false;
            DrainInputQueue();
            int width = PaintBox.Width;
            int height = PaintBox.Height;
            e.Graphics.FillRectangle(BackgroundBrush, new Rectangle(0, 0, width, height));
            bool atEnd;
            m_DisplayedEntries = CopyQueue(m_ScrollIndex, DisplayedLines, out atEnd);
            PaintDrawEntries(e.Graphics, width, m_DisplayedEntries);
            PaintWarning(e.Graphics, width, height, atEnd);
        }

        private void PaintBox_MouseMove(object? sender, MouseEventArgs e)
        {
            LogEntryRow? entry = null;
            LogEntryRow[]? rows = m_DisplayedEntries;
            int row = e.Y / LineHeight;
            if (row >= 0 && rows != null && row < rows.Length)
            {
                entry = rows[row];
            }
            if (string.IsNullOrEmpty(entry?.DisplayedLine) || !CheckToolTip.Checked)
            {
                this.ToolTip.RemoveAll();
                m_ToolTipString = "";
            }
            else if (CheckToolTip.Checked)
            {
                string toolTip = entry.ToToolTip();
                if (m_ToolTipString != toolTip)
                {
                    m_ToolTipString = toolTip;
                    this.ToolTip.SetToolTip(this.PaintBox, m_ToolTipString);
                }
            }
        }

        private void PaintDrawEntries(Graphics graphics, int PaintBoxWidth, LogEntryRow[] entries)
        {
            if (entries.Length == 0) return;

            string longestString = "";
            int y = 0;
            foreach (var entry in entries)
            {
                int textTop = y + ((LineHeight - TextFont.Height) / 2);
                if (entry.DisplayedLine != null && entry.Original != null)
                {
                    Brush brushBack = new SolidBrush(entry.Original.BackColor);
                    graphics.FillRectangle(brushBack, 0, y, PaintBoxWidth, LineHeight);
                    Brush brush = new SolidBrush(entry.Original.LineColor);
                    string fullLine = entry.Original.Data?.Time?.ToString("yyyy/MM/dd HH:mm:ss.fff ") + entry.DisplayedLine;
                    graphics.DrawString(fullLine, TextFont, brush, 5 - HorizontalBar.Value, textTop);
                    if (fullLine.Length > longestString.Length)
                    {
                        longestString = fullLine;
                    }
                }
                y += LineHeight;
            }

            SizeF longestSize = graphics.MeasureString(longestString, TextFont);
            HorizontalBar.Maximum = (int)longestSize.Width + ((longestSize.Width <= PaintBoxWidth) ? HorizontalBar.Value : 0);
            int largeChange = Math.Max(0, (int)(PaintBoxWidth - TextFont.SizeInPoints));
            HorizontalBar.LargeChange = largeChange;
            bool barVisible = (largeChange - HorizontalBar.Value) < HorizontalBar.Maximum;
            if (barVisible && !HorizontalBar.Visible)
            {
                HorizontalBar.Value = 0;
                HorizontalBar.Visible = barVisible;
                PaintBox.Invalidate();
                m_ScrollIndex++;
            }
        }

        private void PaintWarning(Graphics graphics, int width, int height, bool scrolledToEnd)
        {
            string str =
                (scrolledToEnd ? "" : "...") +
                (Filtering ? " filter" : "") +
                (CheckPause.Checked ? " Paused" : "");
            SizeF size = graphics.MeasureString(str, WarningFont);
            graphics.DrawString(str, WarningFont, WarningBrush, width - (int)size.Width - 3, height - (int)size.Height - 3);
        }

        private void QueueEntry(LogEntryRow newEntry)
        {//ui thread only
            int prevIndex = m_ScrollIndex;
            int prevCount = DisplayedQueue.Count;

            if (Filtering)
            {
                AddToQueue(LogQueue, newEntry, scroll: false);
                if (newEntry.MatchesFilter(m_FilterType, m_FilterRegex))
                {
                    AddToQueue(FilteredQueue, newEntry, scroll: true);
                }
            }
            else
            {
                AddToQueue(LogQueue, newEntry, scroll: true);
                FilteredQueue.Clear();
            }
            if (prevIndex != m_ScrollIndex || prevCount != DisplayedQueue.Count)
            {
                UpdateScrollBars();
            }
        }

        private void AddToQueue(LinkedList<LogEntryRow> queue, LogEntryRow entry, bool scroll)
        { //ui thread only
            int prevCount = queue.Count;
            queue.AddLast(entry);

            int displayed = DisplayedLines;
            if (scroll
                && CheckScroll.Checked
                && (m_ScrollIndex + displayed) >= prevCount
                && prevCount >= displayed)
            {
                m_ScrollIndex++;
            }
            while (queue.Count > ItemLimit)
            {
                queue.RemoveFirst();
                if (queue == DisplayedQueue && scroll && m_ScrollIndex != 0)
                {
                    m_ScrollIndex--;
                }
            }
        }

        private void UpdateScrollBars()
        {
            int displayedCount = DisplayedQueue.Count;
            int displayedLines = DisplayedLines;
            int maxScroll = Math.Max(0, displayedCount - displayedLines + 1);
            m_ScrollIndex = m_ScrollIndex > maxScroll ? maxScroll : (m_ScrollIndex < 0) ? 0 : m_ScrollIndex;
            VerticalBar.SuspendLayout();
            if (displayedCount > 0)
            {
                VerticalBar.Maximum = displayedCount - 1;
                VerticalBar.LargeChange = displayedLines;
                VerticalBar.Value = (m_ScrollIndex >= displayedCount) ? (displayedCount - 1) : m_ScrollIndex;
                VerticalBar.Visible = displayedLines < displayedCount;
            }
            else
            {
                VerticalBar.Maximum = 0;
                VerticalBar.LargeChange = 1;
                VerticalBar.Value = 0;
            }
            VerticalBar.ResumeLayout();
        }

        private LogEntryRow[] CopyQueue(int index, int count, out bool atEnd)
        {
            int queueCount = DisplayedQueue.Count;
            atEnd = false;
            if (count >= queueCount)
            {
                atEnd = true;
                return DisplayedQueue.ToArray();
            }
            else if (index + count >= queueCount)
            {
                atEnd = true;
                return DisplayedQueue.Skip(queueCount - count).Take(count).ToArray();
            }
            else
            {
                return DisplayedQueue.Skip(index).Take(count).ToArray();
            }
        }

        private void ClearMenuItem_CLick(object? sender, EventArgs e)
        {
            LogQueue.Clear();
            FilteredQueue.Clear();
            UpdateScrollBars();
            PaintBox.Invalidate();
        }

        private void CopyToClipboardMenuItem_Click(object? sender, EventArgs e)
        {
            StringBuilder text = new StringBuilder();
            foreach (LogEntryRow log in DisplayedQueue)
            {
                string time = log.Original?.Data?.Time?.ToString("yyyy/MM/dd HH:mm:ss.fff ") ?? "";
                text.AppendFormat("{0}\n", (log.DisplayedLine == null) ? "null" : time + log.DisplayedLine);
            }
            try
            {
                Clipboard.SetText(text.ToString());
            }
            catch
            {
                MessageBox.Show("Clipboard error");
            }
        }

        private void FilterChanged()
        {
            FilteredQueue.Clear();

            string filter = TextFilter.Text;
            try
            {
                m_FilterRegex = filter.Length > 0 ? new Regex(filter, RegexOptions.Compiled | RegexOptions.IgnoreCase) : null;
            }
            catch
            {
                m_FilterRegex = null;
            }

            if (Filtering)
            {
                foreach (var l in LogQueue)
                {
                    l.FilterMatchFlag = false;
                }
                Parallel.ForEach(LogQueue, entry =>
                {
                    entry.FilterMatchFlag = entry.MatchesFilter(m_FilterType, m_FilterRegex);
                });
                FilteredQueue = new LinkedList<LogEntryRow>(LogQueue.Where(x => x.FilterMatchFlag));
            }

            UpdateScrollBars();
            PaintBox.Invalidate();
        }

        private void Log_Resize(object? sender, EventArgs e)
        {
            PaintBox.Invalidate();
            if (ScrollIndex >= DisplayedLines)
            {
                m_ScrollIndex++;
            }
        }
    }
}
