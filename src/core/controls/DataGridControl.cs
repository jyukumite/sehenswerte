using SehensWerte.Files;
using SehensWerte.Maths;
using SehensWerte.Utils;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Drawing;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;

namespace SehensWerte.Controls
{
    public class DataGridControlCellKeyDownEventArgs
    {
        public int ColumnIndex;
        public int RowIndex;
        public Keys KeyCode;
        public bool SuppressKeyPress;
    }
    public delegate void DataGridControlCellKeyDownEventHandler(object sender, DataGridControlCellKeyDownEventArgs e);

    public class DataGridControlToolTipArgs : EventArgs
    {
        public string CellContent = "";
        public string DisplayText = "";
        public string ColumnName = "";
        public Rectangle CellRectangle;
        public Point MouseLocation;
        public bool Shown;
        public int RowIndex;
        public int ColumnIndex;
    }

    public partial class DataGridControl : UserControl
    {
        // call like this to load and bind data:
        //  public void LoadCsv(string fileName, bool numeric = false)
        //  public void LoadRows(IEnumerable<IEnumerable<string?>> rows, IEnumerable<string> colnames)
        //  public void LoadRows(IEnumerable<IEnumerable<double>> rows, IEnumerable<string> colnames)

        public event DataGridViewCellEventHandler CellDoubleClick = (s, e) => { };
        public event DataGridViewCellEventHandler CellClick = (s, e) => { };
        public event DataGridControlCellKeyDownEventHandler CellKeyDown = (s, e) => { };
        public event EventHandler SelectionChanged = (s, e) => { };

        public event EventHandler<DataGridControlToolTipArgs> ShowTooltipWindow = (s, e) => { };
        public event EventHandler<DataGridControlToolTipArgs> HideTooltipWindow = (s, e) => { };

        public event DataGridViewCellContextMenuStripNeededEventHandler CellContextMenuStripNeeded = (s, e) => { };
        public DataGridViewCell CurrentCell => Grid.CurrentCell;

        public Color NullForeColor;
        public bool NumericGrid { get; private set; }

        private DataGridView Grid;
        private System.Windows.Forms.Timer HoverShowTimer;
        private System.Windows.Forms.Timer HoverHideTimer;
        private DataGridControlToolTipArgs? HoverArgs;

        public int ToolTipShowMilliseconds { get; set; } = 10000;
        public int ToolTipPauseMilliseconds { get; set; } = 1500;
        public int ToolTipMaxLength { get; set; } = 1000;
        public bool ShowCellHints { get; set; } = true;
        public bool AllowUserToOrderColumns { get; set; } = true;

        public string[]? MaskColumns { get; set; }
        public string MaskString { get; set; } = new string('\u2022', 5); // small dot

        Action<CsvLog.Entry>? OnLog;

        private StatusStrip StatusStrip;
        private ToolStripStatusLabel StatusFilterText;
        private ToolStripDropDownButton ShowAllStatus;
        private ToolStripDropDownButton UndoFilterStatus;
        private ToolStripDropDownButton RedoFilterStatus;
        private ToolStripDropDownButton HideSelectedStatus;
        private ToolStripDropDownButton HideUnselectedStatus;
        private ToolStripDropDownButton HideAboveStatus;
        private ToolStripDropDownButton HideBelowStatus;
        private ToolStripDropDownButton HideByRegexStatus;
        private ToolStripDropDownButton ShowByRegexStatus;
        private ToolStripDropDownButton HideMatchCellStatus;
        private ToolStripDropDownButton HideUnmatchCellStatus;
        private ToolStripDropDownButton UniqueCellStatus;
        private ToolStripDropDownButton DecimateCellStatus;
        private ToolStripDropDownButton TransposeGrid;
        private ToolStripDropDownButton HighlightButton;
        private ToolStripDropDownButton ColumnsButton;
        private ToolStripDropDownButton SaveCsvButton;
        private ToolStripDropDownButton LoadCsvButton;
        private System.Windows.Forms.ToolTip HoverTip;


        public BoundData? DataGridBind;
        private string RegexInput = ".*";
        private bool m_ResizingColumn = false;
        private string? m_ResizingColumnName;
        private bool m_ApplyingProgrammaticWidths = false;

        // cells containing one of these substrings get a coloured background
        private readonly List<(string Text, Color Color)> m_Highlights = new();
        private (string Text, Color Color)? m_PreviewHighlight = null;
        private static readonly Color[] HighlightPalette = new[]
        {
            Color.FromArgb(255, 245, 157),  // yellow
            Color.FromArgb(178, 235, 178),  // green
            Color.FromArgb(178, 218, 245),  // blue
            Color.FromArgb(245, 196, 224),  // pink
            Color.FromArgb(245, 196, 178),  // salmon
            Color.FromArgb(218, 198, 245),  // lavender
            Color.FromArgb(178, 245, 232),  // cyan
            Color.FromArgb(245, 232, 178),  // khaki
        };

        private Color NextHighlightColor()
        {
            return HighlightPalette[m_Highlights.Count % HighlightPalette.Length];
        }

        // Apply a highlight (used both by user OK and by redo). Does NOT push history.
        internal void AddHighlightInternal(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            m_Highlights.Add((text, NextHighlightColor()));
            Grid.Invalidate();
        }

        // Drop all highlights (used when replay rebuilds the set from history).
        internal void ClearHighlightsInternal()
        {
            m_Highlights.Clear();
            Grid.Invalidate();
        }

        // Pop the most recently added highlight (used by undo). Undo is LIFO, so the
        // last entry is always the one being undone - no need to search by text.
        internal void RemoveLastHighlightInternal()
        {
            if (m_Highlights.Count == 0) return;
            m_Highlights.RemoveAt(m_Highlights.Count - 1);
            Grid.Invalidate();
        }

        private Color? MatchHighlight(string? cellText)
        {
            if (string.IsNullOrEmpty(cellText)) return null;
            // Most recent committed highlight wins on overlap, then preview.
            for (int loop = m_Highlights.Count - 1; loop >= 0; loop--)
            {
                if (cellText.Contains(m_Highlights[loop].Text, StringComparison.OrdinalIgnoreCase))
                {
                    return m_Highlights[loop].Color;
                }
            }
            if (m_PreviewHighlight is { } prev && cellText.Contains(prev.Text, StringComparison.OrdinalIgnoreCase))
            {
                return prev.Color;
            }
            return null;
        }

        // Scroll the first cell (top-to-bottom, left-to-right) containing the substring
        // into view, so the live highlight preview jumps to the first match as you type.
        private void ScrollFirstHighlightIntoView(string text)
        {
            if (string.IsNullOrEmpty(text) || DataGridBind == null) return;
            var rows = DataGridBind.FilteredData;
            int colCount = DataGridBind.ColumnNames.Count;
            for (int r = 0; r < rows.Count; r++)
            {
                for (int c = 0; c < colCount; c++)
                {
                    var value = rows[r].Column(c);
                    if (string.IsNullOrEmpty(value)
                        || !value.Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        string colName = DataGridBind.ColumnNames[c];
                        int colIndex = Grid.Columns.Contains(colName) ? Grid.Columns[colName].Index : -1;
                        if (Grid.GetRowDisplayRectangle(r, false).IsEmpty)
                        {
                            Grid.FirstDisplayedScrollingRowIndex = r;
                        }
                        if (colIndex >= 0 && Grid.GetColumnDisplayRectangle(colIndex, false).IsEmpty)
                        {
                            Grid.FirstDisplayedScrollingColumnIndex = colIndex;
                        }
                    }
                    catch { }
                    return;
                }
            }
        }
        private string? m_DraggingColumnName;
        private int m_DraggingStartX = -1;
        private int m_DraggingDropX = -1; // x of drop indicator, -1 = hidden
        private string? m_LiveResizeColumn;
        private int m_LiveResizeStartX = -1;
        private int m_LiveResizeStartWidth = -1;
        private readonly Dictionary<(string FamilyName, float Size, FontStyle Style), Font> m_CellFontCache = new();
        private readonly Dictionary<int, SolidBrush> m_SolidBrushCache = new();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearPaintCaches();
            }
            base.Dispose(disposing);
        }

        // Make Alt+<mnemonic> on any status-strip button (e.g. &Columns, &Show All) work when the grid has focus
        protected override bool ProcessMnemonic(char charCode)
        {
            foreach (ToolStripItem item in StatusStrip.Items)
            {
                if (!item.Enabled || !item.Visible) continue;
                if (Control.IsMnemonic(charCode, item.Text ?? ""))
                {
                    item.PerformClick();
                    return true;
                }
            }
            return base.ProcessMnemonic(charCode);
        }

        public static implicit operator DataGridView(DataGridControl d) => d.Grid; // helper, perhaps confusing and undiscoverable

        public DataGridControl() : this((s) => { }) { }

        public DataGridControl(Action<CsvLog.Entry> onLog, bool prettyColours = false)
        {
            OnLog += onLog;
            OnLog?.Invoke(new CsvLog.Entry("DataGridControl ctor", CsvLog.Priority.Info));

            this.Grid = new SehensWerte.Controls.DataGridViewDoubleBuffered();
            this.HoverTip = new System.Windows.Forms.ToolTip();
            ShowTooltipWindow += (s, e) =>
            {
                if (!ShowCellHints) return;
                int safetyTimer = ToolTipShowMilliseconds + 1000;
                HoverTip.Show(e.DisplayText, Grid, Grid.PointToClient(e.MouseLocation), safetyTimer);
            };
            HideTooltipWindow += (s, e) =>
            {
                if (Grid != null && !Grid.IsDisposed)
                {
                    HoverTip.Hide(Grid);
                }
            };
            this.StatusStrip = new System.Windows.Forms.StatusStrip();
            this.StatusFilterText = new System.Windows.Forms.ToolStripStatusLabel();
            this.ShowAllStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.UndoFilterStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.RedoFilterStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideSelectedStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideUnselectedStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideAboveStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideBelowStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideByRegexStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.ShowByRegexStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideMatchCellStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideUnmatchCellStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.UniqueCellStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.DecimateCellStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.TransposeGrid = new System.Windows.Forms.ToolStripDropDownButton();
            this.HighlightButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.ColumnsButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.SaveCsvButton = new System.Windows.Forms.ToolStripDropDownButton();
            this.LoadCsvButton = new System.Windows.Forms.ToolStripDropDownButton();

            ((System.ComponentModel.ISupportInitialize)(this.Grid)).BeginInit();
            this.StatusStrip.SuspendLayout();
            this.SuspendLayout();

            this.Grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.Grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Grid.Location = new System.Drawing.Point(0, 0);
            this.Grid.Margin = new System.Windows.Forms.Padding(4);
            this.Grid.Name = "DataGrid";
            this.Grid.RowHeadersWidth = 78;
            this.Grid.RowTemplate.Height = (int)(28 * DeviceDpi / 96f);
            this.Grid.Size = new System.Drawing.Size(1720, 1037);
            this.Grid.TabIndex = 12;
            this.Grid.CellMouseDown += this.DataGrid_CellMouseDown;
            this.Grid.RowPostPaint += this.DataGrid_RowPostPaint;
            this.Grid.SelectionChanged += this.Grid_SelectionChanged;
            this.Grid.CellDoubleClick += (s, e) => CellDoubleClick.Invoke(s, e);
            this.Grid.ColumnDividerDoubleClick += Grid_ColumnDividerDoubleClick;
            this.Grid.CellPainting += Grid_CellPainting;
            this.Grid.KeyDown += Grid_KeyDown;
            this.Grid.CellClick += (s, e) => CellClick.Invoke(s, e);
            this.Grid.CellContextMenuStripNeeded += (s, e) => CellContextMenuStripNeeded.Invoke(s, e);
            this.Grid.CellMouseEnter += Grid_CellMouseEnter;
            this.Grid.CellMouseLeave += (s, e) => { HoverHide(); };
            this.Grid.ShowCellToolTips = false;
            this.Grid.CellToolTipTextNeeded += (s, e) => { e.ToolTipText = ""; };
            // AllowUserToOrderColumns intentionally left off: WinForms' built-in
            // column drag traps the mouse inside the header band. We do our own
            // drop logic in MouseUp using the release X coordinate instead, so the
            // user can hold the column header and move the mouse anywhere.
            this.Grid.ColumnWidthChanged += (s, e) =>
            {
                if (m_ApplyingProgrammaticWidths) return;
                m_ResizingColumn = true;
                m_ResizingColumnName = e.Column.Name;
            };
            this.Grid.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;

                const int resizeEdgeTolerance = 5;
                for (int loop = 0; loop < Grid.Columns.Count; loop++)
                {
                    var rect = Grid.GetColumnDisplayRectangle(loop, false);
                    if (rect.Width > 0 && Math.Abs(e.X - rect.Right) <= resizeEdgeTolerance)
                    {
                        var c = Grid.Columns[loop];
                        if (c.Resizable != DataGridViewTriState.False)
                        {
                            m_LiveResizeColumn = c.Name;
                            m_LiveResizeStartX = e.X;
                            m_LiveResizeStartWidth = c.Width;
                        }
                        return;
                    }
                }
                if (!AllowUserToOrderColumns) return;
                var hit = Grid.HitTest(e.X, e.Y);
                if (hit.Type != DataGridViewHitTestType.ColumnHeader) return;
                if (hit.ColumnIndex < 0 || hit.ColumnIndex >= Grid.Columns.Count) return;
                m_DraggingColumnName = Grid.Columns[hit.ColumnIndex].Name;
                m_DraggingStartX = e.X;
            };
            this.Grid.MouseMove += (s, e) =>
            {
                if (m_LiveResizeColumn != null)
                {
                    if (Grid.Columns.Contains(m_LiveResizeColumn))
                    {
                        var c = Grid.Columns[m_LiveResizeColumn];
                        int newWidth = Math.Max(c.MinimumWidth, m_LiveResizeStartWidth + (e.X - m_LiveResizeStartX));
                        if (c.Width != newWidth)
                        {
                            m_ApplyingProgrammaticWidths = true;
                            try { c.Width = newWidth; }
                            finally { m_ApplyingProgrammaticWidths = false; }
                        }
                    }
                    return;
                }
                if (m_DraggingColumnName == null) return;
                if (m_ResizingColumn)
                {
                    // A resize has started under our drag — cancel the reorder.
                    m_DraggingColumnName = null;
                    m_DraggingStartX = -1;
                    UpdateDropIndicator(-1);
                    Grid.Cursor = Cursors.Default;
                    return;
                }
                if (Math.Abs(e.X - m_DraggingStartX) < 5)
                {
                    UpdateDropIndicator(-1);
                    return;
                }
                if (Grid.Cursor != Cursors.SizeWE) Grid.Cursor = Cursors.SizeWE;
                if (!Grid.Columns.Contains(m_DraggingColumnName))
                {
                    UpdateDropIndicator(-1);
                    return;
                }
                int srcDisplay = Grid.Columns[m_DraggingColumnName].DisplayIndex;
                int dropDisplay = ColumnDisplayAtX(e.X);
                if (dropDisplay < 0 || dropDisplay == srcDisplay)
                {
                    UpdateDropIndicator(-1);
                    return;
                }
                var targetCol = ColumnAtDisplay(dropDisplay);
                if (targetCol == null)
                {
                    UpdateDropIndicator(-1);
                    return;
                }
                var rect = Grid.GetColumnDisplayRectangle(targetCol.Index, false);
                UpdateDropIndicator(dropDisplay > srcDisplay ? rect.Right - 1 : rect.Left);
            };
            this.Grid.MouseUp += (s, e) =>
            {
                if (m_LiveResizeColumn != null)
                {
                    string name = m_LiveResizeColumn;
                    m_LiveResizeColumn = null;
                    m_LiveResizeStartX = -1;
                    m_LiveResizeStartWidth = -1;
                    m_ResizingColumn = false;
                    m_ResizingColumnName = null;
                    if (Grid.Columns.Contains(name))
                    {
                        DataGridBind?.PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.ColumnResize)
                        {
                            Column = name,
                            Width = Grid.Columns[name].Width
                        });
                    }
                    return;
                }
                if (m_ResizingColumn)
                {
                    m_ResizingColumn = false;
                    var col = m_ResizingColumnName != null && Grid.Columns.Contains(m_ResizingColumnName)
                        ? Grid.Columns[m_ResizingColumnName] : null;
                    DataGridBind?.PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.ColumnResize)
                    {
                        Column = m_ResizingColumnName ?? "",
                        Width = col?.Width ?? 0
                    });
                    m_ResizingColumnName = null;
                }
                if (m_DraggingColumnName != null)
                {
                    string movedName = m_DraggingColumnName;
                    int startX = m_DraggingStartX;
                    m_DraggingColumnName = null;
                    m_DraggingStartX = -1;
                    UpdateDropIndicator(-1);
                    Grid.Cursor = Cursors.Default;

                    // Small movement → treat as a sort click (handled by
                    // IBindingList.ApplySort); push no move snapshot.
                    if (Math.Abs(e.X - startX) < 5) return;
                    if (!Grid.Columns.Contains(movedName)) return;

                    int srcDisplay = Grid.Columns[movedName].DisplayIndex;
                    int dropDisplay = ColumnDisplayAtX(e.X);
                    if (dropDisplay < 0 || dropDisplay == srcDisplay) return;

                    string newAfter;
                    if (dropDisplay > srcDisplay)
                    {
                        // Dropping further right: the moved column ends up immediately
                        // after the column at dropDisplay.
                        newAfter = ColumnAtDisplay(dropDisplay)?.Name ?? "";
                    }
                    else
                    {
                        // Dropping further left: the moved column ends up immediately
                        // before the column at dropDisplay (so its new left-neighbour
                        // is the column at dropDisplay - 1, or "" if leftmost).
                        newAfter = dropDisplay == 0 ? "" : ColumnAtDisplay(dropDisplay - 1)?.Name ?? "";
                    }
                    DataGridBind?.MoveColumn(movedName, newAfter);
                }
            };
            this.Grid.Paint += (s, e) =>
            {
                if (m_DraggingDropX < 0) return;
                using var pen = new Pen(Color.OrangeRed, 2);
                e.Graphics.DrawLine(pen, m_DraggingDropX, 0, m_DraggingDropX, Grid.ClientSize.Height);
            };
            this.NullForeColor = this.Grid.ForeColor;

            this.HoverShowTimer = new System.Windows.Forms.Timer();
            this.HoverShowTimer.Tick += (s, e) => { HoverShow(); };
            this.HoverHideTimer = new System.Windows.Forms.Timer();
            this.HoverHideTimer.Tick += (s, e) => { HoverHide(); };

            this.StatusStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.StatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { // right to left
                    this.StatusFilterText,
                    this.ShowAllStatus,
                    this.UndoFilterStatus,
                    this.RedoFilterStatus,
                    this.HideSelectedStatus,
                    this.HideUnselectedStatus,
                    this.HideAboveStatus,
                    this.HideBelowStatus,
                    this.HideByRegexStatus,
                    this.ShowByRegexStatus,
                    this.HideMatchCellStatus,
                    this.HideUnmatchCellStatus,
                    this.UniqueCellStatus,
                    this.DecimateCellStatus,
                    this.TransposeGrid,
                    this.HighlightButton,
                    this.ColumnsButton,
                    this.SaveCsvButton,
                    this.LoadCsvButton});
            this.StatusStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.Flow;
            this.StatusStrip.Location = new System.Drawing.Point(0, 1037);
            this.StatusStrip.Name = "StatusStrip";
            this.StatusStrip.Padding = new System.Windows.Forms.Padding(18, 0, 1, 0);
            this.StatusStrip.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.StatusStrip.Size = new System.Drawing.Size(1720, 42);
            this.StatusStrip.SizingGrip = false;
            // StatusStrip defaults this to false (unlike ToolStrip), so button ToolTipText never shows.
            this.StatusStrip.ShowItemToolTips = true;
            this.StatusStrip.TabIndex = 13;
            this.StatusStrip.Text = "WindowStatusStrip";

            this.StatusFilterText.Name = "StatusFilterText";
            this.StatusFilterText.Size = new System.Drawing.Size(94, 32);
            this.StatusFilterText.Text = "Filtered";

            this.ShowAllStatus.Name = "ShowAllStatus";
            this.ShowAllStatus.ShowDropDownArrow = false;
            this.ShowAllStatus.Size = new System.Drawing.Size(110, 38);
            this.ShowAllStatus.Text = "Show All";
            this.ShowAllStatus.ToolTipText = "Clear all filters and bring every hidden row back into view";
            this.ShowAllStatus.Click += new System.EventHandler(this.ShowAllStatus_Click);
            this.ShowAllStatus.BackColor = prettyColours ? Color.FromArgb(216, 208, 242) : SystemColors.Control;

            this.UndoFilterStatus.Name = "UndoFilterStatus";
            this.UndoFilterStatus.ShowDropDownArrow = false;
            this.UndoFilterStatus.Size = new System.Drawing.Size(76, 38);
            this.UndoFilterStatus.Text = "Undo";
            this.UndoFilterStatus.ToolTipText = "Undo the last filter, sort, or layout change (Ctrl+Z)";
            this.UndoFilterStatus.Click += new System.EventHandler(this.UndoFilterStatus_Click);
            this.UndoFilterStatus.BackColor = prettyColours ? Color.FromArgb(216, 208, 242) : SystemColors.Control;

            this.RedoFilterStatus.Name = "RedoFilterStatus";
            this.RedoFilterStatus.ShowDropDownArrow = false;
            this.RedoFilterStatus.Size = new System.Drawing.Size(76, 38);
            this.RedoFilterStatus.Text = "Redo";
            this.RedoFilterStatus.ToolTipText = "Reapply the change you just undid (Ctrl+Y / Ctrl+Shift+Z)";
            this.RedoFilterStatus.Visible = false;
            this.RedoFilterStatus.Click += new System.EventHandler(this.RedoFilterStatus_Click);
            this.RedoFilterStatus.BackColor = prettyColours ? Color.FromArgb(216, 208, 242) : SystemColors.Control;

            this.HideSelectedStatus.Name = "HideSelectedStatus";
            this.HideSelectedStatus.ShowDropDownArrow = false;
            this.HideSelectedStatus.Size = new System.Drawing.Size(166, 38);
            this.HideSelectedStatus.Text = "Hide Selected";
            this.HideSelectedStatus.ToolTipText = "Hide the rows you have selected";
            this.HideSelectedStatus.Click += new System.EventHandler(this.HideSelectedStatus_Click);
            this.HideSelectedStatus.BackColor = prettyColours ? Color.FromArgb(216, 242, 178) : SystemColors.Control;

            this.HideUnselectedStatus.Name = "HideUnselectedStatus";
            this.HideUnselectedStatus.ShowDropDownArrow = false;
            this.HideUnselectedStatus.Size = new System.Drawing.Size(193, 38);
            this.HideUnselectedStatus.Text = "Hide Unselected";
            this.HideUnselectedStatus.ToolTipText = "Hide every row except the ones you have selected";
            this.HideUnselectedStatus.Click += new System.EventHandler(this.HideUnselectedStatus_Click);
            this.HideUnselectedStatus.BackColor = prettyColours ? Color.FromArgb(216, 242, 178) : SystemColors.Control;

            this.HideAboveStatus.Name = "HideAboveStatus";
            this.HideAboveStatus.ShowDropDownArrow = false;
            this.HideAboveStatus.Size = new System.Drawing.Size(143, 38);
            this.HideAboveStatus.Text = "Hide Above";
            this.HideAboveStatus.ToolTipText = "Hide all rows above the selected row";
            this.HideAboveStatus.Click += new System.EventHandler(this.HideAboveStatus_Click);
            this.HideAboveStatus.BackColor = prettyColours ? Color.FromArgb(178, 242, 216) : SystemColors.Control;

            this.HideBelowStatus.Name = "HideBelowStatus";
            this.HideBelowStatus.ShowDropDownArrow = false;
            this.HideBelowStatus.Size = new System.Drawing.Size(139, 38);
            this.HideBelowStatus.Text = "Hide Below";
            this.HideBelowStatus.ToolTipText = "Hide all rows below the selected row";
            this.HideBelowStatus.Click += new System.EventHandler(this.HideBelowStatus_Click);
            this.HideBelowStatus.BackColor = prettyColours ? Color.FromArgb(178, 242, 216) : SystemColors.Control;

            this.HideByRegexStatus.Name = "HideByRegexStatus";
            this.HideByRegexStatus.ShowDropDownArrow = false;
            this.HideByRegexStatus.Size = new System.Drawing.Size(139, 38);
            this.HideByRegexStatus.Text = "Regex Hide";
            this.HideByRegexStatus.ToolTipText = "Hide rows whose cell in the selected column matches a regex";
            this.HideByRegexStatus.Click += new System.EventHandler(this.HideByRegexStatus_Click);
            this.HideByRegexStatus.BackColor = prettyColours ? Color.FromArgb(242, 232, 178) : SystemColors.Control;

            this.ShowByRegexStatus.Name = "ShowByRegexStatus";
            this.ShowByRegexStatus.ShowDropDownArrow = false;
            this.ShowByRegexStatus.Size = new System.Drawing.Size(147, 38);
            this.ShowByRegexStatus.Text = "Regex Show";
            this.ShowByRegexStatus.ToolTipText = "Show only rows whose cell in the selected column matches a regex (Ctrl+F)";
            this.ShowByRegexStatus.Click += new System.EventHandler(this.ShowByRegexStatus_Click);
            this.ShowByRegexStatus.BackColor = prettyColours ? Color.FromArgb(242, 232, 178) : SystemColors.Control;

            this.HideMatchCellStatus.Name = "HideMatchCellStatus";
            this.HideMatchCellStatus.ShowDropDownArrow = false;
            this.HideMatchCellStatus.Size = new System.Drawing.Size(142, 38);
            this.HideMatchCellStatus.Text = "Hide &Match";
            this.HideMatchCellStatus.ToolTipText = "Hide rows whose value in this column matches the selected cell(s) (Alt+M)";
            this.HideMatchCellStatus.Click += new System.EventHandler(this.HideMatchCellStatus_Click);
            this.HideMatchCellStatus.BackColor = prettyColours ? Color.FromArgb(242, 216, 178) : SystemColors.Control;

            this.HideUnmatchCellStatus.Name = "HideUnmatchCellStatus";
            this.HideUnmatchCellStatus.ShowDropDownArrow = false;
            this.HideUnmatchCellStatus.Size = new System.Drawing.Size(171, 38);
            this.HideUnmatchCellStatus.Text = "Hide &Unmatch";
            this.HideUnmatchCellStatus.ToolTipText = "Hide rows whose value in this column differs from the selected cell(s) (Alt+U)";
            this.HideUnmatchCellStatus.Click += new System.EventHandler(this.HideUnmatchCellStatus_Click);
            this.HideUnmatchCellStatus.BackColor = prettyColours ? Color.FromArgb(242, 216, 178) : SystemColors.Control;

            this.UniqueCellStatus.Name = "UniqueCellStatus";
            this.UniqueCellStatus.ShowDropDownArrow = false;
            this.UniqueCellStatus.Size = new System.Drawing.Size(171, 38);
            this.UniqueCellStatus.Text = "Uni&que";
            this.UniqueCellStatus.ToolTipText = "Hide duplicate rows, keeping the first of each value in this column (Alt+Q)";
            this.UniqueCellStatus.Click += new System.EventHandler(this.UniqueCellStatus_Click);
            this.UniqueCellStatus.BackColor = prettyColours ? Color.FromArgb(242, 196, 208) : SystemColors.Control;

            this.DecimateCellStatus.Name = "DecimateCellStatus";
            this.DecimateCellStatus.ShowDropDownArrow = false;
            this.DecimateCellStatus.Size = new System.Drawing.Size(171, 38);
            this.DecimateCellStatus.Text = "&Decimate";
            this.DecimateCellStatus.ToolTipText = "Thin the view to every 10th visible row, hiding the rest (Alt+D)";
            this.DecimateCellStatus.Click += new System.EventHandler(this.DecimateCellStatus_Click);
            this.DecimateCellStatus.BackColor = prettyColours ? Color.FromArgb(242, 196, 208) : SystemColors.Control;

            this.TransposeGrid.Name = "TransposeGrid";
            this.TransposeGrid.ShowDropDownArrow = false;
            this.TransposeGrid.Size = new System.Drawing.Size(171, 38);
            this.TransposeGrid.Text = "&Transpose";
            this.TransposeGrid.ToolTipText = "Swap rows and columns; click again to switch back (Alt+T)";
            this.TransposeGrid.Click += new System.EventHandler(this.TransposeGrid_Click);
            this.TransposeGrid.BackColor = prettyColours ? Color.FromArgb(242, 196, 208) : SystemColors.Control;

            this.HighlightButton.Name = "HighlightButton";
            this.HighlightButton.ShowDropDownArrow = false;
            this.HighlightButton.Size = new System.Drawing.Size(140, 38);
            this.HighlightButton.Text = "&Highlight";
            this.HighlightButton.ToolTipText = "Colour any cell containing a substring you type; repeat to stack colours (Alt+H)";
            this.HighlightButton.Click += new System.EventHandler(this.HighlightButton_Click);
            this.HighlightButton.BackColor = prettyColours ? Color.FromArgb(255, 240, 180) : SystemColors.Control;

            this.ColumnsButton.Name = "ColumnsButton";
            this.ColumnsButton.ShowDropDownArrow = false;
            this.ColumnsButton.Size = new System.Drawing.Size(140, 38);
            this.ColumnsButton.Text = "&Columns";
            this.ColumnsButton.ToolTipText = "Pick which columns are shown or hidden (Alt+C)";
            this.ColumnsButton.Click += new System.EventHandler(this.ColumnsButton_Click);
            this.ColumnsButton.BackColor = prettyColours ? Color.FromArgb(216, 232, 218) : SystemColors.Control;

            this.SaveCsvButton.Name = "SaveCsv";
            this.SaveCsvButton.ShowDropDownArrow = false;
            this.SaveCsvButton.Size = new System.Drawing.Size(171, 38);
            this.SaveCsvButton.Text = "Save";
            this.SaveCsvButton.ToolTipText = "Save the currently visible rows and columns to a CSV file (Ctrl+S)";
            this.SaveCsvButton.Click += new System.EventHandler(this.SaveCsv_Click);
            this.SaveCsvButton.BackColor = prettyColours ? Color.FromArgb(218, 216, 232) : SystemColors.Control;

            this.LoadCsvButton.Name = "LoadCsv";
            this.LoadCsvButton.ShowDropDownArrow = false;
            this.LoadCsvButton.Size = new System.Drawing.Size(171, 38);
            this.LoadCsvButton.Text = "Load";
            this.LoadCsvButton.ToolTipText = "Load a CSV file into the grid (Ctrl+O)";
            this.LoadCsvButton.Click += new System.EventHandler(this.LoadCsv_Click);
            this.LoadCsvButton.BackColor = prettyColours ? Color.FromArgb(218, 216, 232) : SystemColors.Control;

            this.AutoScaleDimensions = new System.Drawing.SizeF(13F, 32F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.Grid);
            this.Controls.Add(this.StatusStrip);
            this.Size = new System.Drawing.Size(2032, 1312);
            ((System.ComponentModel.ISupportInitialize)(this.Grid)).EndInit();
            this.StatusStrip.ResumeLayout(false);
            this.StatusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }


        public SelectedCellsData SelectedCellsToClipboardFormats(bool numeric = false)
        {
            if (DataGridBind == null)
            {
                throw new NullReferenceException("DataGridBind is null, cannot copy to clipboard");
            }
            return DataGridBind!.SelectedCellsToClipboardFormats(numeric);
        }

        private void Grid_KeyDown(object? sender, KeyEventArgs e)
        {
            bool inEdit = Grid.IsCurrentCellInEditMode; // don't steal selected hotkeys from a cell editor

            if (e.Control && e.KeyCode == Keys.C)
            {
                string[] formats = new string[] { DataFormats.Html, DataFormats.Text, DataFormats.UnicodeText, DataFormats.CommaSeparatedValue };

                if (DataGridBind != null)
                {
                    this.ExceptionToMessagebox(() =>
                    {
                        //var temp = Grid.GetClipboardContent();
                        var data = DataGridBind.SelectedCellsToClipboardFormats(NumericGrid);
                        var dataObj = new DataObject();
                        dataObj.SetData(DataFormats.Text, data.tsv);
                        dataObj.SetData(DataFormats.UnicodeText, data.tsv);
                        dataObj.SetData(DataFormats.Html, data.wrappedHtml);
                        dataObj.SetData(DataFormats.CommaSeparatedValue, data.csv);
                        Clipboard.SetDataObject(dataObj, true);
                    }, "Copy to clipboard");
                    e.Handled = true;
                }
            }
            else if (!inEdit && e.Control && e.KeyCode == Keys.Z && !e.Shift)
            {
                UndoFilterStatus_Click(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (!inEdit && e.Control && (e.KeyCode == Keys.Y || (e.KeyCode == Keys.Z && e.Shift)))
            {
                RedoFilterStatus_Click(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (!inEdit && e.Control && e.KeyCode == Keys.F)
            {
                ShowByRegexStatus_Click(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (!inEdit && e.Control && e.KeyCode == Keys.S && !e.Shift)
            {
                SaveCsv_Click(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (!inEdit && e.Control && e.KeyCode == Keys.O && !e.Shift)
            {
                LoadCsv_Click(this, EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (!inEdit && e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add))
            {
                AdjustGridFontSize(+1f);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (!inEdit && e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract))
            {
                AdjustGridFontSize(-1f);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (Grid.CurrentCell != null)
            {
                var ee = new DataGridControlCellKeyDownEventArgs()
                {
                    ColumnIndex = Grid.CurrentCell.ColumnIndex,
                    RowIndex = Grid.CurrentCell.RowIndex,
                    KeyCode = e.KeyCode
                };
                CellKeyDown.Invoke(this, ee);
                e.SuppressKeyPress = ee.SuppressKeyPress;
            }
        }

        private float m_CellFontDelta = 0f; // Ctrl+'+' / Ctrl+'-' zoom the cell text

        private void AdjustGridFontSize(float delta)
        {
            float baseSize = (Grid.DefaultCellStyle.Font ?? Grid.Font).Size;
            float next = Math.Clamp(m_CellFontDelta + delta, 6f - baseSize, 72f - baseSize);
            if (next == m_CellFontDelta) return;
            m_CellFontDelta = next;
            ClearCellFontCache();
            Grid.Invalidate();
        }

        private Font GetCellFont(Font baseFont, bool italic)
        {
            // immeasurable speedup, but improved cleanup/dispose
            Font font = baseFont;
            if (m_CellFontDelta == 0f && !italic)
            {
                return font;
            }
            else
            {
                float size = Math.Clamp(baseFont.Size + m_CellFontDelta, 6f, 72f);
                FontStyle style = italic ? baseFont.Style | FontStyle.Italic : baseFont.Style;
                var key = (baseFont.FontFamily.Name, size, style);
                if (m_CellFontCache.TryGetValue(key, out Font? cachedFont) && cachedFont != null)
                {
                    font = cachedFont;
                }
                else
                {
                    font = new Font(baseFont.FontFamily, size, style);
                    m_CellFontCache[key] = font;
                }
            }
            return font;
        }

        private SolidBrush GetSolidBrush(Color color)
        {
            // immeasurable speedup, but improved cleanup/dispose
            int key = color.ToArgb();
            if (!m_SolidBrushCache.TryGetValue(key, out SolidBrush? brush))
            {
                brush = new SolidBrush(color);
                m_SolidBrushCache[key] = brush;
            }
            return brush;
        }

        private void ClearCellFontCache()
        {
            foreach (Font font in m_CellFontCache.Values)
            {
                font.Dispose();
            }
            m_CellFontCache.Clear();
        }

        private void ClearPaintCaches()
        {
            ClearCellFontCache();
            foreach (SolidBrush brush in m_SolidBrushCache.Values)
            {
                brush.Dispose();
            }
            m_SolidBrushCache.Clear();
        }


        private void Grid_ColumnDividerDoubleClick(object? sender, DataGridViewColumnDividerDoubleClickEventArgs e)
        {
            try
            {
                if (sender == null) return;
                DataGridView grid = (DataGridView)sender;
                DataGridViewColumn column = grid.Columns[e.ColumnIndex];
                var strings = GetColumn(e.ColumnIndex);
                int padding = grid.ColumnHeadersDefaultCellStyle.Padding.Left
                                  + grid.ColumnHeadersDefaultCellStyle.Padding.Right;

                Font headerFont = grid.ColumnHeadersDefaultCellStyle.Font;
                SizeF headerTextSize = TextRenderer.MeasureText(column.HeaderText, headerFont);
                int headerWidth = (int)Math.Ceiling(headerTextSize.Width) + padding;

                Font baseCellFont = column.InheritedStyle.Font;
                float cellFontSize = Math.Clamp(baseCellFont.Size + m_CellFontDelta, 6f, 72f);
                using Font cellFont = new Font(baseCellFont.FontFamily, cellFontSize, baseCellFont.Style);

                ThreadLocal<(Graphics g, int m)> widths = new ThreadLocal<(Graphics g, int m)>(() =>
                    (Graphics.FromImage(new Bitmap(1, 1)), headerWidth), trackAllValues: true
                );
                Parallel.ForEach(strings, str =>
                {
                    if (str != null)
                    {
                        Graphics g = widths.Value.g;
                        SizeF textSize = g.MeasureString(str, cellFont);
                        int width = (int)Math.Ceiling(textSize.Width) + padding;
                        widths.Value = (g, Math.Max(widths.Value.m, width));
                    }
                });
                foreach (var v in widths.Values)
                {
                    v.g.Dispose();
                }

                int maxWidth = widths.Values.Max(value => value.m);
                int newWidth = Math.Max(10, Math.Min(grid.Parent.Width - 20, maxWidth + 10));
                widths.Dispose();

                DataGridBind?.PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.ColumnResize)
                {
                    Column = column.Name,
                    Width = newWidth
                });

                m_ApplyingProgrammaticWidths = true;
                try
                {
                    column.Width = newWidth;
                }
                finally
                {
                    m_ApplyingProgrammaticWidths = false;
                }

                e.Handled = true;
            }
            catch
            {
            }
        }

        public new void Focus()
        {
            if (Grid != null)
            {
                if (Grid.SelectedCells.Count > 0)
                {
                    Grid.CurrentCell = Grid.SelectedCells[0];
                }
                Grid.BeginInvoke(new Action(() => Grid.Focus()));
            }
        }

        private void Grid_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            string colName = ColumnName(e.ColumnIndex);
            bool masked = MaskColumns?.Contains(colName) ?? false;
            string? cellContent = GetCell(e.ColumnIndex, e.RowIndex);
            string displayText = cellContent ?? "null";

            if (displayText.Length > ToolTipMaxLength)
            {
                displayText = displayText.Substring(0, ToolTipMaxLength) + "...";
            }

            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && !masked && cellContent != null)
            {
                HoverHide();

                var rectangle = Grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
                Point screenLocation = Grid.PointToScreen(new Point(rectangle.Left, rectangle.Top));

                HoverArgs = new DataGridControlToolTipArgs()
                {
                    CellContent = cellContent,
                    DisplayText = displayText,
                    ColumnName = colName,
                    CellRectangle = new Rectangle(screenLocation.X, screenLocation.Y, rectangle.Width, rectangle.Height),
                    MouseLocation = Cursor.Position,
                    RowIndex = e.RowIndex,
                    ColumnIndex = e.ColumnIndex,
                    Shown = false,
                };
                HoverShowTimer.Interval = ToolTipPauseMilliseconds;
                HoverShowTimer.Start();
            }
        }

        private void HoverShow()
        {
            HoverShowTimer.Stop();
            HoverHideTimer.Stop();
            if (HoverArgs != null && !HoverArgs.Shown)
            {
                HoverHideTimer.Interval = ToolTipShowMilliseconds;
                HoverHideTimer.Start();
                ShowTooltipWindow?.Invoke(this, HoverArgs);
                HoverArgs.Shown = true;
            }
        }

        private void HoverHide()
        {
            HoverShowTimer.Stop();
            HoverHideTimer.Stop();
            if (HoverArgs != null && HoverArgs.Shown)
            {
                HideTooltipWindow?.Invoke(this, HoverArgs);
            }
            HoverArgs = null;
        }

        private void Grid_SelectionChanged(object? sender, EventArgs e)
        {
            SelectionChanged.Invoke(this, e);
        }

        private void ShowAllStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.ShowAll();
            }, "Show all");
        }

        private void UndoFilterStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                ApplyColumnWidths(DataGridBind?.Undo());
            }, "Undo filter");
        }

        private void RedoFilterStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                ApplyColumnWidths(DataGridBind?.Redo());
            }, "Redo filter");
        }

        private void UpdateButtons(object? sender, EventArgs e)
        {
            UndoFilterStatus.Visible = DataGridBind?.CanUndo ?? false;
            RedoFilterStatus.Visible = DataGridBind?.CanRedo ?? false;
            ShowAllStatus.Visible = DataGridBind?.IsFiltered ?? false;
        }

        private void ApplyColumnWidths(IEnumerable<(string Name, int Width)>? widths)
        {
            if (widths == null) return;
            m_ApplyingProgrammaticWidths = true;
            try
            {
                foreach (var w in widths)
                {
                    if (Grid.Columns.Contains(w.Name))
                    {
                        int wasWidth = Grid.Columns[w.Name].Width;
                        Grid.Columns[w.Name].Width = w.Width;
                        int isWidth = Grid.Columns[w.Name].Width;
                        OnLog?.Invoke(new CsvLog.Entry(
                            $"ApplyColumnWidths: {w.Name} {wasWidth} -> requested {w.Width} (now {isWidth})",
                            CsvLog.Priority.Debug));
                    }
                    else
                    {
                        OnLog?.Invoke(new CsvLog.Entry(
                            $"ApplyColumnWidths: column {w.Name} not in Grid.Columns",
                            CsvLog.Priority.Debug));
                    }
                }
            }
            finally
            {
                m_ApplyingProgrammaticWidths = false;
            }
        }

        private void HideSelectedStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.HideRows(DataGridBind.RowsWithSelection());
            }, "Hide selected rows");
        }

        private void HideUnselectedStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.HideRowsOtherThan(DataGridBind.RowsWithSelection());
            }, "Hide unselected rows");
        }

        private void HideAboveStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.HideRowsAbove(DataGridBind.RowsWithSelection().FirstOrDefault());
            }, "Hide rows above");
        }

        private void HideBelowStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.HideRowsBelow(DataGridBind.RowsWithSelection().FirstOrDefault());
            }, "Hide rows below");
        }

        private void HideByRegexStatus_Click(object? sender, EventArgs e)
        {
            if (Grid.SelectedCells.Count != 1) return;
            string header = Convert.ToString(Grid.CurrentCell.OwningColumn.HeaderText);
            string regex = InputFieldForm.Show($"Hide {header} by regex", "Regex", RegexInput, regex: true) ?? "";
            if (!string.IsNullOrEmpty(regex))
            {
                RegexInput = regex;
                this.ExceptionToMessagebox(() =>
                {
                    DataGridBind?.HideRowsMatchingRegex(regex, header);
                }, "Hide by regex");
            }
        }

        private void ShowByRegexStatus_Click(object? sender, EventArgs e)
        {
            if (Grid.CurrentCell == null) return;
            string header = Convert.ToString(Grid.CurrentCell.OwningColumn.HeaderText);
            string regex = InputFieldForm.Show($"Show {header} by regex", "Regex", RegexInput, regex: true) ?? "";
            if (!string.IsNullOrEmpty(regex))
            {
                RegexInput = regex;
                this.ExceptionToMessagebox(() =>
                {
                    DataGridBind?.ShowRowsMatchingRegex(regex, header);
                }, "Show by regex");
            }
        }

        private void HideMatchCellStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                if (Grid.SelectedCells.Count == 0)
                {
                    return;
                }

                string header = Grid.CurrentCell.OwningColumn.HeaderText;
                DataGridBind?.HideRowsMatching(
                    Convert.ToString(header) ?? "",
                    GetSelectedRowsOfColumn(header));
            }, "Hide matching cells");
        }

        private void HideUnmatchCellStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                if (Grid.SelectedCells.Count == 0)
                {
                    return;
                }

                string header = Grid.CurrentCell.OwningColumn.HeaderText;
                DataGridBind?.HideRowsNotMatching(
                    Convert.ToString(header) ?? "",
                    GetSelectedRowsOfColumn(header));
            }, "Hide unmatching cells");
        }

        private void UniqueCellStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                if (Grid.SelectedCells.Count == 0)
                {
                    return;
                }

                string header = Grid.CurrentCell.OwningColumn.HeaderText;
                DataGridBind?.HideNotFirstUnique(Convert.ToString(header) ?? "");
            }, "Unique cells");
        }

        private void DecimateCellStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                if (Grid.SelectedCells.Count == 0)
                {
                    return;
                }
                DataGridBind?.Decimate(10);
            }, "Decimate");
        }

        private void TransposeGrid_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                if (DataGridBind == null) return;
                DataGridBind.Transpose();
            }, "Transpose grid");
        }

        private void HighlightButton_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                if (DataGridBind == null) return;

                var color = NextHighlightColor();
                var form = new InputFieldForm
                {
                    Title = "Highlight",
                    Prompt = "Substring to highlight (any cell that contains it):",
                };
                form.PreviewChanged += (text) =>
                {
                    m_PreviewHighlight = string.IsNullOrEmpty(text) ? null : (text, color);
                    ScrollFirstHighlightIntoView(text);
                    Grid.Invalidate();
                };
                form.ShowDialog(this);

                m_PreviewHighlight = null;
                if (form.Result == DialogResult.OK && !string.IsNullOrEmpty(form.ResultString))
                {
                    DataGridBind.PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.Highlight)
                    {
                        Pattern = form.ResultString,
                    });
                    AddHighlightInternal(form.ResultString);
                }
                Grid.Invalidate();
            }, "Highlight");
        }

        private void ColumnsButton_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                if (DataGridBind == null) return;

                var allCols = DataGridBind.ColumnNames.ToList();
                if (allCols.Count == 0) return;

                var visible = allCols.Where(c => !IsColumnCollapsed(c)).ToList();

                var form = new CheckBoxListForm();
                form.Title = "Columns";
                form.Prompt = "Tick columns to show; Enter or OK scrolls to the topmost match.";
                form.Selection = allCols;
                form.CheckedSelection = visible;
                form.ShowDialog();
                if (form.Result != DialogResult.OK) return;

                var nowChecked = new HashSet<string>(form.CheckedSelection);
                foreach (var col in allCols)
                {
                    bool wantVisible = nowChecked.Contains(col);
                    bool isCollapsed = IsColumnCollapsed(col);
                    if (wantVisible && isCollapsed)
                    {
                        ExpandColumn(col);
                    }
                    else if (!wantVisible && !isCollapsed)
                    {
                        CollapseColumn(col);
                    }
                }

                var topmost = form.TopmostItem;
                if (topmost != null)
                {
                    ScrollColumnIntoView(topmost);
                }
            }, "Columns");
        }

        private SaveFileDialog m_SaveFileDialog = new SaveFileDialog();
        private void SaveCsv_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                if (Grid.SelectedCells.Count != 0)
                {
                    m_SaveFileDialog.Title = "Save as CSV";
                    m_SaveFileDialog.Filter = "CSV files (*.csv)|*.csv";
                    m_SaveFileDialog.RestoreDirectory = true;
                    if (m_SaveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        DataGridBind?.SaveToCsv(m_SaveFileDialog.FileName);
                    }
                }
            }, "Save CSV");
        }

        private OpenFileDialog m_LoadFileDialog = new OpenFileDialog();
        private void LoadCsv_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                m_LoadFileDialog.Title = "Load CSV";
                m_LoadFileDialog.Filter = "CSV files (*.csv)|*.csv";
                m_LoadFileDialog.RestoreDirectory = true;
                if (m_LoadFileDialog.ShowDialog() == DialogResult.OK)
                {
                    LoadCsv(m_LoadFileDialog.FileName,
                        MessageBox.Show("Load as strings?", "Load as strings?", MessageBoxButtons.YesNo) == DialogResult.No);
                }
            }, "Load CSV");
        }

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            //Profile?.Enter("Grid_CellPainting");
            try
            {
                PaintGridCell(e);
            }
            finally
            {
                //Profile?.Exit("Grid_CellPainting");
            }
        }

        private void PaintGridCell(DataGridViewCellPaintingEventArgs e)
        {
            bool selected = e.State.HasFlag(DataGridViewElementStates.Selected);
            e.PaintBackground(e.CellBounds, selected);
            e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);

            if (!selected && DataGridBind?.FilteredData.Count > e.RowIndex)
            {
                var a = DataGridBind.FilteredData[e.RowIndex];
                var b = a?.Colours?[e.ColumnIndex];
                if (b != null)
                {
                    e.Graphics.FillRectangle(GetSolidBrush(b.Value), e.CellBounds);
                }
                Color? hl = MatchHighlight(e.Value?.ToString());
                if (hl != null)
                {
                    e.Graphics.FillRectangle(GetSolidBrush(hl.Value), e.CellBounds);
                }
            }

            string colName = Grid.Columns[e.ColumnIndex].Name;
            bool isNull = e.Value == null;
            string realText = e.Value?.ToString() ?? "null";
            bool masked = !isNull && realText != "" && (MaskColumns?.Contains(colName) ?? false);
            string displayText = masked ? MaskString : realText;

            Font cellFont = GetCellFont(e.CellStyle.Font, isNull);
            Color textColor = isNull ? NullForeColor : e.CellStyle.ForeColor;
            using StringFormat stringFormat = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = NumericGrid ? StringFormatFlags.NoWrap : 0
            };

            stringFormat.Alignment = e.CellStyle.Alignment switch
            {
                DataGridViewContentAlignment.MiddleCenter or DataGridViewContentAlignment.TopCenter or DataGridViewContentAlignment.BottomCenter => StringAlignment.Center,
                DataGridViewContentAlignment.MiddleLeft or DataGridViewContentAlignment.TopLeft or DataGridViewContentAlignment.BottomLeft => StringAlignment.Near,
                DataGridViewContentAlignment.MiddleRight or DataGridViewContentAlignment.TopRight or DataGridViewContentAlignment.BottomRight => StringAlignment.Far,
                _ => StringAlignment.Near
            };

            float cellTextWidth = e.CellBounds.Width - e.CellStyle.Padding.Left - e.CellStyle.Padding.Right;
            bool overflows = displayText.Contains('\n')
                || (!NumericGrid && e.Graphics.MeasureString(displayText, cellFont).Width > cellTextWidth);
            stringFormat.LineAlignment = overflows ? StringAlignment.Near : e.CellStyle.Alignment switch
            {
                DataGridViewContentAlignment.MiddleCenter or DataGridViewContentAlignment.MiddleLeft or DataGridViewContentAlignment.MiddleRight => StringAlignment.Center,
                DataGridViewContentAlignment.TopCenter or DataGridViewContentAlignment.TopLeft or DataGridViewContentAlignment.TopRight => StringAlignment.Near,
                DataGridViewContentAlignment.BottomCenter or DataGridViewContentAlignment.BottomLeft or DataGridViewContentAlignment.BottomRight => StringAlignment.Far,
                _ => StringAlignment.Near
            };

            RectangleF textBounds = new RectangleF(
                e.CellBounds.X + e.CellStyle.Padding.Left,
                e.CellBounds.Y + e.CellStyle.Padding.Top,
                e.CellBounds.Width - e.CellStyle.Padding.Left - e.CellStyle.Padding.Right,
                e.CellBounds.Height - e.CellStyle.Padding.Top - e.CellStyle.Padding.Bottom
            );

            if (textBounds.Width <= 0 || textBounds.Height <= 0 || e.CellBounds.Width <= 0 || e.CellBounds.Height <= 0)
            {
                e.Handled = true;
                return;
            }
            using Region originalClip = e.Graphics.Clip.Clone();
            try
            {
                e.Graphics.SetClip(e.CellBounds, System.Drawing.Drawing2D.CombineMode.Intersect);

                StringDiff.Diffs? diff = null;
                if (!selected && !masked && DataGridBind?.FilteredData.Count > e.RowIndex)
                {
                    diff = DataGridBind.FilteredData[e.RowIndex]?.Diffs?[e.ColumnIndex];
                    if (diff != null && diff.LeftText != displayText)
                    {
                        diff = null;
                    }
                }

                SolidBrush textBrush = GetSolidBrush(textColor);
                try
                {
                    if (diff == null)
                    {
                        e.Graphics.DrawString(displayText, cellFont, textBrush, textBounds, stringFormat);
                    }
                    else
                    {
                        DrawDiffString(e.Graphics, diff, cellFont, textBrush, textBounds, stringFormat);
                    }
                }
                catch
                {
                    try
                    {
                        e.Graphics.DrawString("Cell content too large", GetCellFont(e.CellStyle.Font, true), textBrush, textBounds, stringFormat);
                    }
                    catch { }
                }
            }
            finally
            {
                e.Graphics.Clip = originalClip;
            }
            e.Handled = true;
        }

        private static readonly Color DiffChangedForeColor = Color.Red;

        private void DrawDiffString(Graphics g, StringDiff.Diffs diff, Font font, SolidBrush textBrush, RectangleF textBounds, StringFormat baseFormat)
        {
            string leftText = diff.LeftText;
            g.DrawString(leftText, font, textBrush, textBounds, baseFormat);

            int cursor = 0;
            List<(int Start, int Length)> changed = new();
            foreach (var (text, side) in diff)
            {
                if (side == StringDiff.Diffs.Side.Right)
                {
                    continue;
                }
                if (side == StringDiff.Diffs.Side.Left && text.Length > 0)
                {
                    changed.Add((cursor, text.Length));
                }
                cursor += text.Length;
            }
            if (changed.Count == 0 || cursor == 0)
            {
                return;
            }

            using StringFormat measureFormat = (StringFormat)baseFormat.Clone();
            measureFormat.FormatFlags = baseFormat.FormatFlags | StringFormatFlags.MeasureTrailingSpaces;

            using Region union = new Region();
            union.MakeEmpty();

            // Win32 limit: 32 measurable ranges per call. Chunk if needed.
            const int maxRangesPerCall = 32;
            for (int chunkStart = 0; chunkStart < changed.Count; chunkStart += maxRangesPerCall)
            {
                int chunkLen = Math.Min(maxRangesPerCall, changed.Count - chunkStart);
                CharacterRange[] ranges = new CharacterRange[chunkLen];
                for (int loop = 0; loop < chunkLen; loop++)
                {
                    var s = changed[chunkStart + loop];
                    ranges[loop] = new CharacterRange(s.Start, s.Length);
                }
                measureFormat.SetMeasurableCharacterRanges(ranges);
                Region[] regions;
                try
                {
                    regions = g.MeasureCharacterRanges(leftText, font, textBounds, measureFormat);
                }
                catch
                {
                    return;
                }
                foreach (Region r in regions)
                {
                    union.Union(r);
                    r.Dispose();
                }
            }

            SolidBrush brush = GetSolidBrush(DiffChangedForeColor);
            using Region savedClip = g.Clip;
            try
            {
                g.IntersectClip(union);
                g.DrawString(leftText, font, brush, textBounds, baseFormat);
            }
            finally
            {
                g.Clip = savedClip;
            }
        }

        private void DataGrid_RowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid != null)
            {
                //var index = (e.RowIndex + 1).ToString();
                int index = ((BoundDataRow)grid.Rows[e.RowIndex].DataBoundItem).Index + 1;
                using StringFormat centerFormat = new StringFormat()
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
                e.Graphics.DrawString(index.ToString(), this.Font, SystemBrushes.ControlText, headerBounds, centerFormat);
            }
        }

        private void DataGrid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
        }

        public void SortByColumn(string heading, ListSortDirection direction = ListSortDirection.Ascending)
        {
            DataGridBind?.SortByColumn(heading, direction);
        }

        public void UpdateStatusStrip()
        {
            int total = DataGridBind?.UnfilteredData.Count ?? 0;
            int showing = DataGridBind?.FilteredData.Count ?? 0;
            StatusFilterText.Text = (showing == total) ? $"{total}rows" : $"{showing}/{total}";
            StatusFilterText.Visible = true;

            // Transpose turns rows into columns; the col0..col99 binding caps display at 100.
            TransposeGrid.Enabled = showing <= 100;
            TransposeGrid.ToolTipText = TransposeGrid.Enabled
                ? "Swap rows and columns; click again to switch back (Alt+T)"
                : "Disabled: transpose would create more than 100 columns";
        }


        public void Clear()
        {
            DataGridBind?.Unbind();
            NumericGrid = false;
            DataGridBind = null;
            Grid.Columns.Clear();
            UpdateButtons(this, EventArgs.Empty);
            UpdateStatusStrip();
        }

        public void LoadCsv(string fileName, bool numeric = false)
        {
            DataGridBind?.Unbind();
            NumericGrid = numeric;
            DataGridBind = new BoundData(fileName, numeric: numeric, CsvLog.ExtendPath(OnLog, "BoundData"));
            DataGridBind.ListChanged += GridData_ListChanged;
            DataGridBind.Setup(this);
            UpdateStatusStrip();
            UpdateButtons(this, EventArgs.Empty);
        }

        public void LoadRows(IEnumerable<IEnumerable<string?>> rows, IEnumerable<string> colnames)
        {
            DataGridBind?.Unbind();
            NumericGrid = false;
            DataGridBind = new BoundData(rows, colnames, CsvLog.ExtendPath(OnLog, "BoundData"));
            DataGridBind.ListChanged += GridData_ListChanged;
            DataGridBind.Setup(this);
            UpdateStatusStrip();
            UpdateButtons(this, EventArgs.Empty);
        }

        public void LoadRows(IEnumerable<IEnumerable<double>> rows, IEnumerable<string> colnames)
        {
            DataGridBind?.Unbind();
            NumericGrid = true;
            DataGridBind = new BoundData(rows, colnames, CsvLog.ExtendPath(OnLog, "BoundData"));
            DataGridBind.ListChanged += GridData_ListChanged;
            DataGridBind.Setup(this);
            UpdateStatusStrip();
            UpdateButtons(this, EventArgs.Empty);
        }

        public void LoadJson(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            List<string> cols;
            List<List<string?>> rows;

            if (root.ValueKind == JsonValueKind.Array)
            {
                cols = new List<string>();
                var elements = root.EnumerateArray().ToList();
                foreach (var el in elements)
                {
                    if (el.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in el.EnumerateObject())
                        {
                            if (!cols.Contains(prop.Name))
                            {
                                cols.Add(prop.Name);
                            }
                        }
                    }
                }
                if (cols.Count == 0)
                {
                    cols.Add("value");
                }

                rows = new List<List<string?>>();
                foreach (var el in elements)
                {
                    var row = new List<string?>();
                    if (el.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var col in cols)
                        {
                            row.Add(el.TryGetProperty(col, out var val) ? JsonElementToString(val) : null);
                        }
                    }
                    else
                    {
                        row.Add(JsonElementToString(el));
                        for (int loop = 1; loop < cols.Count; loop++)
                        {
                            row.Add(null);
                        }
                    }
                    rows.Add(row);
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                cols = new List<string> { "Key", "Value" };
                rows = root.EnumerateObject()
                    .Select(p => new List<string?> { p.Name, JsonElementToString(p.Value) })
                    .ToList();
            }
            else
            {
                cols = new List<string> { "Value" };
                rows = new List<List<string?>> { new List<string?> { root.ToString() } };
            }

            LoadRows(rows, cols);
        }

        public static string JsonElementToString(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? "",
            JsonValueKind.Null => "",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => el.GetRawText(),
        };

        public void AppendRows(IEnumerable<IEnumerable<string?>> rows)
        {
            int scrollRow = Grid.FirstDisplayedScrollingRowIndex;
            DataGridBind?.AppendRows(rows);
            if (scrollRow >= 0)
            {
                Grid.FirstDisplayedScrollingRowIndex = scrollRow;
            }
            UpdateStatusStrip();
        }

        // Append a new column on the right. values aligned with UnfilteredData.
        public void AddColumn(string header, IEnumerable<string?> values)
        {
            DataGridBind?.AddColumn(header, values);
            UpdateStatusStrip();
        }

        // Insert a new column at the given position. values aligned with UnfilteredData.
        public void InsertColumn(string header, int index, IEnumerable<string?> values)
        {
            DataGridBind?.InsertColumn(header, index, values);
            UpdateStatusStrip();
        }

        // Undoable "derive N columns from one source column" operation
        public int SplitColumn(string sourceColumnName, Func<string, IEnumerable<(string Header, string?[] Values)>> emit)
        {
            int produced = DataGridBind?.SplitColumn(sourceColumnName, emit) ?? 0;
            UpdateStatusStrip();
            return produced;
        }

        private void GridData_ListChanged(object? sender, ListChangedEventArgs e)
        {
            UpdateStatusStrip();
            UpdateButtons(this, EventArgs.Empty);
        }

        public DataGridViewCellEventArgs? GetSelectedCell
        {
            get
            {
                var cell = Grid.SelectedCells.Count == 1 ? Grid.SelectedCells[0] : null;
                return cell == null ? null : new DataGridViewCellEventArgs(cell.ColumnIndex, cell.RowIndex);
            }
        }

        public int GetSelectedRowCount()
        {
            return DataGridBind?.RowsWithSelection()?.Count() ?? 0;
        }

        public string?[] GetSelectedRowsOfColumn(string header)
        {
            return DataGridBind?.GetSelectedRowsOfColumn(header) ?? new string?[] { };
        }

        public Dictionary<string, string?>? GetSelectedRow()
        {
            return DataGridBind?.GetSelectedRow();
        }

        public CodeProfile? Profile => DataGridBind?.Profile;

        public string?[] GetColumn(string header)
        {
            return DataGridBind?.GetColumn(header) ?? new string?[] { };
        }

        public double[] GetColumnDouble(string header)
        {
            return DataGridBind?.GetColumnDouble(header) ?? new double[] { };
        }

        public string?[] GetColumn(int index)
        {
            return DataGridBind?.GetColumn(index) ?? new string?[] { };
        }

        public string?[] GetRow(int index)
        {
            return DataGridBind?.FilteredData[index].Strings ?? new string?[] { };
        }

        public int RowCount => DataGridBind?.FilteredData.Count ?? 0;

        public IEnumerable<string> ColumnNames => DataGridBind?.ColumnNames ?? new List<string>();

        public string[] GetSelectedColumnNames()
        {
            return DataGridBind?.GetSelectedColumnNames() ?? new string[] { };
        }

        public string ColumnName(int index)
        {
            return index == -1 ? "" : (DataGridBind?.ColumnNames[index] ?? "");
        }

        public IEnumerable<string> GetColumnNames()
        {
            return DataGridBind?.ColumnNames ?? new List<string>();
        }

        private void UpdateDropIndicator(int newX)
        {
            if (newX == m_DraggingDropX) return;
            if (m_DraggingDropX >= 0)
            {
                Grid.Invalidate(new Rectangle(m_DraggingDropX - 2, 0, 5, Grid.ClientSize.Height));
            }
            m_DraggingDropX = newX;
            if (m_DraggingDropX >= 0)
            {
                Grid.Invalidate(new Rectangle(m_DraggingDropX - 2, 0, 5, Grid.ClientSize.Height));
            }
        }

        // Find the DataGridViewColumn whose DisplayIndex == idx, or null if missing.
        private DataGridViewColumn? ColumnAtDisplay(int idx)
        {
            foreach (DataGridViewColumn c in Grid.Columns)
            {
                if (c.DisplayIndex == idx) return c;
            }
            return null;
        }

        // Map a client X coordinate to a column display index. Drops past the
        // rightmost column snap to the rightmost; drops left of the leftmost
        // snap to the leftmost. -1 only if the grid has no columns.
        private int ColumnDisplayAtX(int x)
        {
            int count = Grid.Columns.Count;
            if (count == 0) return -1;
            int leftDisplay = 0;
            int rightDisplay = count - 1;
            int leftX = int.MaxValue;
            int rightX = int.MinValue;
            for (int loop = 0; loop < count; loop++)
            {
                var rect = Grid.GetColumnDisplayRectangle(loop, false);
                if (rect.Width == 0) continue;
                if (rect.Left < leftX) { leftX = rect.Left; leftDisplay = Grid.Columns[loop].DisplayIndex; }
                if (rect.Right > rightX) { rightX = rect.Right; rightDisplay = Grid.Columns[loop].DisplayIndex; }
                if (x >= rect.Left && x < rect.Right) return Grid.Columns[loop].DisplayIndex;
            }
            return x < leftX ? leftDisplay : rightDisplay;
        }

        public void CollapseColumn(string column)
        {
            if (!Grid.Columns.Contains(column)) return;
            int newWidth = Grid.Columns[column].MinimumWidth;
            DataGridBind?.PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.ColumnResize)
            {
                Column = column,
                Width = newWidth
            });
            m_ApplyingProgrammaticWidths = true;
            try
            {
                Grid.Columns[column].Width = newWidth;
            }
            finally
            {
                m_ApplyingProgrammaticWidths = false;
            }
        }

        public void ExpandColumn(string column, int width = 100)
        {
            if (!Grid.Columns.Contains(column)) return;
            DataGridBind?.PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.ColumnResize)
            {
                Column = column,
                Width = width
            });
            m_ApplyingProgrammaticWidths = true;
            try
            {
                Grid.Columns[column].Width = width;
            }
            finally
            {
                m_ApplyingProgrammaticWidths = false;
            }
        }

        public bool IsColumnCollapsed(string column)
        {
            if (!Grid.Columns.Contains(column)) return false;
            var c = Grid.Columns[column];
            return c.Width <= c.MinimumWidth;
        }

        public void ScrollColumnIntoView(string column)
        {
            if (!Grid.Columns.Contains(column)) return;
            try
            {
                Grid.FirstDisplayedScrollingColumnIndex = Grid.Columns[column].Index;
            }
            catch
            {
                // Index can be invalid mid-rebind; ignore.
            }
        }

        public string? GetCell(string column, int rowIndex)
        {
            int colIndex = DataGridBind?.ColumnNames.IndexOf(column) ?? -1;
            if (colIndex == -1) return "";
            return ((DataGridBind?.FilteredData[rowIndex])?.Column(colIndex));
        }

        public string? GetCell(int colIndex, int rowIndex)
        {
            bool valid = rowIndex >= 0
                && colIndex >= 0
                && rowIndex < (DataGridBind?.FilteredData.Count ?? 0)
                && colIndex < (DataGridBind?.ColumnNames.Count ?? 0);
            return valid ? ((DataGridBind?.FilteredData[rowIndex])?.Column(colIndex)) : null;
        }

        public void SetCell(int colIndex, int rowIndex, string? to)
        {
            if (DataGridBind == null) return;
            BoundDataRow? row;
            if ((row = DataGridBind.FilteredData[rowIndex]) == null) return;
            if (colIndex >= 0 && colIndex < row.Count)
            {
                row.Set(colIndex, to);
            }
        }

        public void ShowRowsOfColumn(string columnName, string value)
        {
            DataGridBind?.ShowAll();
            DataGridBind?.HideRowsNotMatching(columnName, new string[] { value });
        }

        public void ApplyColumnColour(string v, Color color)
        {
            if (Grid.Columns.Contains(v))
            {
                Grid.Columns[v].DefaultCellStyle.BackColor = color;
            }
        }

        public void CellColour(int col, int row, Color colour)
        {
            DataGridBind?.CellColour(col, row, colour);
        }

        public void CellDiffs(int col, int row, StringDiff.Diffs? diff)
        {
            DataGridBind?.CellDiffs(col, row, diff);
        }

        public DataGridControlHistory SaveView()
        {
            return DataGridBind?.SaveBoundState() ?? new DataGridControlHistory();
        }

        public void RestoreView(DataGridControlHistory state)
        {
            ApplyColumnWidths(DataGridBind?.RestoreBoundState(state));
        }
    }
}
