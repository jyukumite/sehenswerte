using Microsoft.VisualBasic;
using Microsoft.VisualBasic.Logging;
using SehensWerte.Files;
using SehensWerte.Maths;
using SehensWerte.Utils;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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

    public class DataGridControlCellEventArgs : EventArgs
    {
        public string CellContent = "";
        public string ColumnName = "";
        public Rectangle CellRectangle;
        public bool Shown;
        public int RowIndex;
        public int ColumnIndex;
    }

    public class DataGridControl : UserControl
    {
        public event DataGridViewCellEventHandler CellDoubleClick = (s, e) => { };
        public event DataGridViewCellEventHandler CellClick = (s, e) => { };
        public event DataGridControlCellKeyDownEventHandler CellKeyDown = (s, e) => { };

        public event EventHandler<DataGridControlCellEventArgs> ShowTooltipWindow = (s, e) => { };
        public event EventHandler<DataGridControlCellEventArgs> HideTooltipWindow = (s, e) => { };

        public event DataGridViewCellContextMenuStripNeededEventHandler CellContextMenuStripNeeded = (s, e) => { };
        public DataGridViewCell CurrentCell => Grid.CurrentCell;
        private DataGridView Grid;
        private System.Windows.Forms.Timer HoverTimer;
        private DataGridControlCellEventArgs? HoverArgs;

        Action<CsvLog.Entry> OnLog;

        private StatusStrip StatusStrip;
        private ToolStripStatusLabel StatusFilterText;
        private ToolStripDropDownButton ShowAllStatus;
        private ToolStripDropDownButton UndoFilterStatus;
        private ToolStripDropDownButton HideSelectedStatus;
        private ToolStripDropDownButton HideUnselectedStatus;
        private ToolStripDropDownButton HideAboveStatus;
        private ToolStripDropDownButton HideBelowStatus;
        private ToolStripDropDownButton HideByRegexStatus;
        private ToolStripDropDownButton ShowByRegexStatus;
        private ToolStripDropDownButton HideMatchCellStatus;
        private ToolStripDropDownButton HideUnmatchCellStatus;
        private ToolStripDropDownButton UniqueCellStatus;
        private ToolStripDropDownButton SaveCsvButton;
        private ToolStripDropDownButton LoadCsvButton;
        public BoundData? DataGridBind;
        private string RegexInput = ".*";

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
            base.Dispose(disposing);
        }

        public static implicit operator DataGridView(DataGridControl d) => d.Grid;

        public DataGridControl() : this((s) => { }) { }

        public DataGridControl(Action<CsvLog.Entry> onLog, bool prettyColours = false)
        {
            OnLog += onLog;
            OnLog?.Invoke(new CsvLog.Entry("DataGridControl ctor", CsvLog.Priority.Info));

            this.Grid = new SehensWerte.Controls.DataGridViewDoubleBuffered();
            this.StatusStrip = new System.Windows.Forms.StatusStrip();
            this.StatusFilterText = new System.Windows.Forms.ToolStripStatusLabel();
            this.ShowAllStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.UndoFilterStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideSelectedStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideUnselectedStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideAboveStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideBelowStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideByRegexStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.ShowByRegexStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideMatchCellStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.HideUnmatchCellStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.UniqueCellStatus = new System.Windows.Forms.ToolStripDropDownButton();
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
            this.Grid.RowTemplate.Height = 28;
            this.Grid.Size = new System.Drawing.Size(1720, 1037);
            this.Grid.TabIndex = 12;
            this.Grid.CellMouseDown += this.DataGrid_CellMouseDown;
            this.Grid.RowPostPaint += this.DataGrid_RowPostPaint;
            this.Grid.SelectionChanged += this.Grid_SelectionChanged;
            this.Grid.CellDoubleClick += (s, e) => CellDoubleClick.Invoke(s, e);
            this.Grid.KeyDown += (s, e) =>
            {
                if (Grid.CurrentCell != null)
                {
                    var ee = new DataGridControlCellKeyDownEventArgs() { ColumnIndex = Grid.CurrentCell.ColumnIndex, RowIndex = Grid.CurrentCell.RowIndex, KeyCode = e.KeyCode };
                    CellKeyDown.Invoke(this, ee);
                    e.SuppressKeyPress = ee.SuppressKeyPress;
                }
            };
            this.Grid.CellClick += (s, e) => CellClick.Invoke(s, e);
            this.Grid.CellContextMenuStripNeeded += (s, e) => CellContextMenuStripNeeded.Invoke(s, e);
            this.Grid.CellMouseEnter += Grid_CellMouseEnter;
            this.Grid.CellMouseLeave += Grid_CellMouseLeave;

            this.HoverTimer = new System.Windows.Forms.Timer();
            this.HoverTimer.Interval = 500; // 0.5 second delay for preparation
            this.HoverTimer.Tick += HoverTimer_Tick;

            this.StatusStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.StatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                    this.StatusFilterText,
                    this.ShowAllStatus,
                    this.UndoFilterStatus,
                    this.HideSelectedStatus,
                    this.HideUnselectedStatus,
                    this.HideAboveStatus,
                    this.HideBelowStatus,
                    this.HideByRegexStatus,
                    this.ShowByRegexStatus,
                    this.HideMatchCellStatus,
                    this.HideUnmatchCellStatus,
                    this.UniqueCellStatus,
                    this.SaveCsvButton,
                    this.LoadCsvButton});
            this.StatusStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.StatusStrip.Location = new System.Drawing.Point(0, 1037);
            this.StatusStrip.Name = "StatusStrip";
            this.StatusStrip.Padding = new System.Windows.Forms.Padding(18, 0, 1, 0);
            this.StatusStrip.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.StatusStrip.Size = new System.Drawing.Size(1720, 42);
            this.StatusStrip.SizingGrip = false;
            this.StatusStrip.TabIndex = 13;
            this.StatusStrip.Text = "WindowStatusStrip";

            this.StatusFilterText.Name = "StatusFilterText";
            this.StatusFilterText.Size = new System.Drawing.Size(94, 32);
            this.StatusFilterText.Text = "Filtered";

            this.ShowAllStatus.Name = "ShowAllStatus";
            this.ShowAllStatus.ShowDropDownArrow = false;
            this.ShowAllStatus.Size = new System.Drawing.Size(110, 38);
            this.ShowAllStatus.Text = "&Show All";
            this.ShowAllStatus.Click += new System.EventHandler(this.ShowAllStatus_Click);
            this.ShowAllStatus.BackColor = prettyColours ? Color.FromArgb(216, 208, 242) : SystemColors.Control;

            this.UndoFilterStatus.Name = "UndoFilterStatus";
            this.UndoFilterStatus.ShowDropDownArrow = false;
            this.UndoFilterStatus.Size = new System.Drawing.Size(76, 38);
            this.UndoFilterStatus.Text = "Undo";
            this.UndoFilterStatus.Click += new System.EventHandler(this.UndoFilterStatus_Click);
            this.UndoFilterStatus.BackColor = prettyColours ? Color.FromArgb(216, 208, 242) : SystemColors.Control;

            this.HideSelectedStatus.Name = "HideSelectedStatus";
            this.HideSelectedStatus.ShowDropDownArrow = false;
            this.HideSelectedStatus.Size = new System.Drawing.Size(166, 38);
            this.HideSelectedStatus.Text = "&Hide Selected";
            this.HideSelectedStatus.Click += new System.EventHandler(this.HideSelectedStatus_Click);
            this.HideSelectedStatus.BackColor = prettyColours ? Color.FromArgb(216, 242, 178) : SystemColors.Control;

            this.HideUnselectedStatus.Name = "HideUnselectedStatus";
            this.HideUnselectedStatus.ShowDropDownArrow = false;
            this.HideUnselectedStatus.Size = new System.Drawing.Size(193, 38);
            this.HideUnselectedStatus.Text = "Hide &Unselected";
            this.HideUnselectedStatus.Click += new System.EventHandler(this.HideUnselectedStatus_Click);
            this.HideUnselectedStatus.BackColor = prettyColours ? Color.FromArgb(216, 242, 178) : SystemColors.Control;

            this.HideAboveStatus.Name = "HideAboveStatus";
            this.HideAboveStatus.ShowDropDownArrow = false;
            this.HideAboveStatus.Size = new System.Drawing.Size(143, 38);
            this.HideAboveStatus.Text = "Hide &Above";
            this.HideAboveStatus.Click += new System.EventHandler(this.HideAboveStatus_Click);
            this.HideAboveStatus.BackColor = prettyColours ? Color.FromArgb(178, 242, 216) : SystemColors.Control;

            this.HideBelowStatus.Name = "HideBelowStatus";
            this.HideBelowStatus.ShowDropDownArrow = false;
            this.HideBelowStatus.Size = new System.Drawing.Size(139, 38);
            this.HideBelowStatus.Text = "Hide &Below";
            this.HideBelowStatus.Click += new System.EventHandler(this.HideBelowStatus_Click);
            this.HideBelowStatus.BackColor = prettyColours ? Color.FromArgb(178, 242, 216) : SystemColors.Control;

            this.HideByRegexStatus.Name = "HideByRegexStatus";
            this.HideByRegexStatus.ShowDropDownArrow = false;
            this.HideByRegexStatus.Size = new System.Drawing.Size(139, 38);
            this.HideByRegexStatus.Text = "Regex Hide";
            this.HideByRegexStatus.Click += new System.EventHandler(this.HideByRegexStatus_Click);
            this.HideByRegexStatus.BackColor = prettyColours ? Color.FromArgb(242, 232, 178) : SystemColors.Control;

            this.ShowByRegexStatus.Name = "ShowByRegexStatus";
            this.ShowByRegexStatus.ShowDropDownArrow = false;
            this.ShowByRegexStatus.Size = new System.Drawing.Size(147, 38);
            this.ShowByRegexStatus.Text = "Regex Show";
            this.ShowByRegexStatus.Click += new System.EventHandler(this.ShowByRegexStatus_Click);
            this.ShowByRegexStatus.BackColor = prettyColours ? Color.FromArgb(242, 232, 178) : SystemColors.Control;

            this.HideMatchCellStatus.Name = "HideMatchCellStatus";
            this.HideMatchCellStatus.ShowDropDownArrow = false;
            this.HideMatchCellStatus.Size = new System.Drawing.Size(142, 38);
            this.HideMatchCellStatus.Text = "Hide Match";
            this.HideMatchCellStatus.Click += new System.EventHandler(this.HideMatchCellStatus_Click);
            this.HideMatchCellStatus.BackColor = prettyColours ? Color.FromArgb(242, 216, 178) : SystemColors.Control;

            this.HideUnmatchCellStatus.Name = "HideUnmatchCellStatus";
            this.HideUnmatchCellStatus.ShowDropDownArrow = false;
            this.HideUnmatchCellStatus.Size = new System.Drawing.Size(171, 38);
            this.HideUnmatchCellStatus.Text = "Hide Unmatch";
            this.HideUnmatchCellStatus.Click += new System.EventHandler(this.HideUnmatchCellStatus_Click);
            this.HideUnmatchCellStatus.BackColor = prettyColours ? Color.FromArgb(242, 216, 178) : SystemColors.Control;

            this.UniqueCellStatus.Name = "UniqueCellStatus";
            this.UniqueCellStatus.ShowDropDownArrow = false;
            this.UniqueCellStatus.Size = new System.Drawing.Size(171, 38);
            this.UniqueCellStatus.Text = "Unique";
            this.UniqueCellStatus.Click += new System.EventHandler(this.UniqueCellStatus_Click);
            this.UniqueCellStatus.BackColor = prettyColours ? Color.FromArgb(242, 196, 208) : SystemColors.Control;

            this.SaveCsvButton.Name = "SaveCsv";
            this.SaveCsvButton.ShowDropDownArrow = false;
            this.SaveCsvButton.Size = new System.Drawing.Size(171, 38);
            this.SaveCsvButton.Text = "Save";
            this.SaveCsvButton.Click += new System.EventHandler(this.SaveCsv_Click);
            this.SaveCsvButton.BackColor = prettyColours ? Color.FromArgb(218, 216, 232) : SystemColors.Control;

            this.LoadCsvButton.Name = "LoadCsv";
            this.LoadCsvButton.ShowDropDownArrow = false;
            this.LoadCsvButton.Size = new System.Drawing.Size(171, 38);
            this.LoadCsvButton.Text = "Load";
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

        private void Grid_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                HoverTimer.Stop();
                if (HoverArgs != null)
                {
                    if (HoverArgs.Shown)
                    {
                        HideTooltipWindow?.Invoke(this, HoverArgs);
                        HoverArgs = null;
                    }
                }

                var rectangle = Grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
                Point screenLocation = Grid.PointToScreen(new Point(rectangle.Left, rectangle.Top));

                HoverArgs = new DataGridControlCellEventArgs()
                {
                    CellContent = GetCell(e.ColumnIndex, e.RowIndex),
                    ColumnName = ColumnName(e.ColumnIndex),
                    CellRectangle = new Rectangle(screenLocation.X, screenLocation.Y, rectangle.Width, rectangle.Height),
                    RowIndex = e.RowIndex,
                    ColumnIndex = e.ColumnIndex,
                    Shown = false,
                };
                HoverTimer.Start();
            }
        }

        private void Grid_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            if (HoverArgs != null && HoverArgs.Shown)
            {
                HideTooltipWindow?.Invoke(this, HoverArgs);
            }
            HoverTimer.Stop();
            HoverArgs = null;
        }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            HoverTimer.Stop();
            if (HoverArgs != null && !HoverArgs.Shown)
            {
                ShowTooltipWindow?.Invoke(this, HoverArgs);
                HoverArgs.Shown = true;
            }
        }

        private void Grid_SelectionChanged(object? sender, EventArgs e)
        {
            var rows = DataGridBind?.RowsWithSelection(Grid)?.ToArray();
            this.Grid.ClipboardCopyMode = ((rows?.Length ?? 0) > 1) ?
                        DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText :
                        DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
        }

        private void ShowAllStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.ShowAll();
            });
        }

        private void UndoFilterStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.Undo();
            });
        }

        private void HideSelectedStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.HideRows(DataGridBind.RowsWithSelection(Grid));
            });
        }

        private void HideUnselectedStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.HideRowsOtherThan(DataGridBind.RowsWithSelection(Grid));
            });
        }

        private void HideAboveStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.HideRowsAbove(DataGridBind.RowsWithSelection(Grid).FirstOrDefault());
            });
        }

        private void HideBelowStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.HideRowsBelow(DataGridBind.RowsWithSelection(Grid).FirstOrDefault());
            });
        }

        private void HideByRegexStatus_Click(object? sender, EventArgs e)
        {
            if (Grid.SelectedCells.Count != 1) return;
            string header = Convert.ToString(Grid.CurrentCell.OwningColumn.HeaderText);
            string regex = InputFieldForm.Show($"Hide {header} by regex", "Regex", RegexInput) ?? "";
            if (!string.IsNullOrEmpty(regex))
            {
                RegexInput = regex;
                this.ExceptionToMessagebox(() =>
                {
                    DataGridBind?.HideRowsMatchingRegex(regex, header);
                });
            }
        }

        private void ShowByRegexStatus_Click(object? sender, EventArgs e)
        {
            if (Grid.SelectedCells.Count != 1) return;
            string header = Convert.ToString(Grid.CurrentCell.OwningColumn.HeaderText);
            string regex = InputFieldForm.Show($"Show {header} by regex", "Regex", RegexInput) ?? "";
            if (!string.IsNullOrEmpty(regex))
            {
                RegexInput = regex;
                this.ExceptionToMessagebox(() =>
                {
                    DataGridBind?.ShowRowsMatchingRegex(regex, header);
                });
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
            });
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
            });
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
            });
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
                        DataGridBind?.SaveToCsv(m_SaveFileDialog.FileName, this);
                    }
                }
            });
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
            });
        }

        private void DataGrid_RowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid != null)
            {
                //var index = (e.RowIndex + 1).ToString();
                int index = ((BoundDataRow)grid.Rows[e.RowIndex].DataBoundItem).Index + 1;
                var centerFormat = new StringFormat()
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

        public void UpdateStatusStrip()
        {
            int total = DataGridBind?.UnfilteredData.Count() ?? 0;
            int showing = DataGridBind?.FilteredData.Count() ?? 0;
            StatusFilterText.Text = (showing == total) ? $"{total}rows" : $"{showing}/{total}";
            StatusFilterText.Visible = true;
        }

        public void LoadCsv(string fileName, bool numeric = false)
        {
            DataGridBind = new BoundData(fileName, numeric: numeric, CsvLog.ExtendPath(OnLog, "BoundData"));
            DataGridBind.ListChanged += GridData_ListChanged;
            DataGridBind.Setup(this);
            UpdateStatusStrip();
        }

        public void LoadRows(IEnumerable<IEnumerable<string>> rows, IEnumerable<string> colnames)
        {
            DataGridBind = new BoundData(rows, colnames, CsvLog.ExtendPath(OnLog, "BoundData"));
            DataGridBind.ListChanged += GridData_ListChanged;
            DataGridBind.Setup(this);
            UpdateStatusStrip();
        }

        public void LoadRows(IEnumerable<IEnumerable<double>> rows, IEnumerable<string> colnames)
        {
            DataGridBind = new BoundData(rows, colnames, CsvLog.ExtendPath(OnLog, "BoundData"));
            DataGridBind.ListChanged += GridData_ListChanged;
            DataGridBind.Setup(this);
            UpdateStatusStrip();
        }

        private void GridData_ListChanged(object? sender, ListChangedEventArgs e)
        {
            UpdateStatusStrip();
        }

        public string[] GetSelectedRowsOfColumn(string header)
        {
            return DataGridBind?.GetSelectedRowsOfColumn(header, this) ?? new string[] { };
        }

        public Dictionary<string,string>? GetSelectedRow()
        {
            return DataGridBind?.GetSelectedRow(this);
        }

        public string[] GetColumn(string header)
        {
            return DataGridBind?.GetColumn(header) ?? new string[] { };
        }

        public double[] GetColumnDouble(string header)
        {
            return DataGridBind?.GetColumnDouble(header) ?? new double[] { };
        }

        public string[] GetColumn(int index)
        {
            return DataGridBind?.GetColumn(index) ?? new string[] { };
        }

        public IEnumerable<string> ColumnNames => DataGridBind?.ColumnNames ?? new List<string>();

        public string[] GetSelectedColumnNames()
        {
            return DataGridBind?.GetSelectedColumnNames(this) ?? new string[] { };
        }

        public string ColumnName(int index)
        {
            return index == -1 ? "" : (DataGridBind?.ColumnNames[index] ?? "");
        }

        public IEnumerable<string> GetColumnNames()
        {
            return DataGridBind?.ColumnNames ?? new List<string>();
        }

        public string GetCell(string column, int rowIndex)
        {
            int colIndex = DataGridBind?.ColumnNames.IndexOf(column) ?? -1;
            if (colIndex == -1) return "";
            return ((DataGridBind?.FilteredData[rowIndex])?.Column(colIndex)) ?? "";
        }

        public string GetCell(int colIndex, int rowIndex)
        {
            return ((DataGridBind?.FilteredData[rowIndex])?.Column(colIndex)) ?? "";
        }

        public void SetCell(int colIndex, int rowIndex, string to)
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

        public abstract class BoundDataRow
        {
            public bool Visible = true;
            public bool HideNot = false;
            public int Index;
            public int ResortIndex;

            public BoundDataRow(int index)
            {
                Index = index;
            }

            public abstract string[] Strings { get; }
            public abstract string Column(int index);
            public abstract double ColumnDouble(int colIndex);
            public abstract int Count { get; }
            public abstract void Set(int index, string to);

            public abstract IComparer<BoundDataRow> GetSortComparer(int colIndex, ListSortDirection sortDirection);

            //DataPropertyName = $"col{loop}",
            public String col0 => Column(0); public String col1 => Column(1); public String col2 => Column(2); public String col3 => Column(3);
            public String col4 => Column(4); public String col5 => Column(5); public String col6 => Column(6); public String col7 => Column(7);
            public String col8 => Column(8); public String col9 => Column(9); public String col10 => Column(10); public String col11 => Column(11);
            public String col12 => Column(12); public String col13 => Column(13); public String col14 => Column(14); public String col15 => Column(15);
            public String col16 => Column(16); public String col17 => Column(17); public String col18 => Column(18); public String col19 => Column(19);
            public String col20 => Column(20); public String col21 => Column(21); public String col22 => Column(22); public String col23 => Column(23);
            public String col24 => Column(24); public String col25 => Column(25); public String col26 => Column(26); public String col27 => Column(27);
            public String col28 => Column(28); public String col29 => Column(29); public String col30 => Column(30); public String col31 => Column(31);
            public String col32 => Column(32); public String col33 => Column(33); public String col34 => Column(34); public String col35 => Column(35);
            public String col36 => Column(36); public String col37 => Column(37); public String col38 => Column(38); public String col39 => Column(39);
            public String col40 => Column(40); public String col41 => Column(41); public String col42 => Column(42); public String col43 => Column(43);
            public String col44 => Column(44); public String col45 => Column(45); public String col46 => Column(46); public String col47 => Column(47);
            public String col48 => Column(48); public String col49 => Column(49); public String col50 => Column(50); public String col51 => Column(51);
            public String col52 => Column(52); public String col53 => Column(53); public String col54 => Column(54); public String col55 => Column(55);
            public String col56 => Column(56); public String col57 => Column(57); public String col58 => Column(58); public String col59 => Column(59);
            public String col60 => Column(60); public String col61 => Column(61); public String col62 => Column(62); public String col63 => Column(63);
            public String col64 => Column(64); public String col65 => Column(65); public String col66 => Column(66); public String col67 => Column(67);
            public String col68 => Column(68); public String col69 => Column(69); public String col70 => Column(70); public String col71 => Column(71);
            public String col72 => Column(72); public String col73 => Column(73); public String col74 => Column(74); public String col75 => Column(75);
            public String col76 => Column(76); public String col77 => Column(77); public String col78 => Column(78); public String col79 => Column(79);
            public String col80 => Column(80); public String col81 => Column(81); public String col82 => Column(82); public String col83 => Column(83);
            public String col84 => Column(84); public String col85 => Column(85); public String col86 => Column(86); public String col87 => Column(87);
            public String col88 => Column(88); public String col89 => Column(89); public String col90 => Column(90); public String col91 => Column(91);
            public String col92 => Column(92); public String col93 => Column(93); public String col94 => Column(94); public String col95 => Column(95);
            public String col96 => Column(96); public String col97 => Column(97); public String col98 => Column(98); public String col99 => Column(99);
        }

        public class BoundDataRowString : BoundDataRow
        {
            public string[] Data;

            public override int Count => Data.Length;
            public override string[] Strings => Data;
            public override string Column(int index) => index < Data.Length ? Data[index] : "";
            public override double ColumnDouble(int index) => index < Data.Length ? Data[index].ToDouble(0) : 0;
            public override void Set(int index, string to) { Data[index] = to; }

            public BoundDataRowString(int index, string[] sourceRow) : base(index)
            {
                ResortIndex = index;
                Index = index;
                Data = sourceRow;
            }

            public override IComparer<BoundDataRow> GetSortComparer(int colIndex, ListSortDirection sortDirection)
            {
                return new BoundDataRowString.SortComparer(colIndex, sortDirection);
            }

            public class SortComparer : IComparer<BoundDataRow>
            {
                private int ColIndex;
                private ListSortDirection Direction;

                public SortComparer(int colIndex, ListSortDirection direction)
                {
                    ColIndex = colIndex;
                    Direction = direction;
                }

                public int Compare(BoundDataRow? x, BoundDataRow? y)
                {
                    if (x == null || y == null) return 0;
                    string o1 = (x as BoundDataRowString)?.Data[ColIndex].ToLower() ?? "";
                    string o2 = (y as BoundDataRowString)?.Data[ColIndex].ToLower() ?? "";
                    int result = o1.NaturalCompare(o2);
                    if (result == 0)
                    {
                        result = x.ResortIndex < y.ResortIndex ? -1 : 1; // stable sort
                    }
                    return Direction == ListSortDirection.Ascending ? result : -result;
                }
            }
        }

        public class BoundDataRowDouble : BoundDataRow
        {
            public double[] Data;

            public override int Count => Data.Length;
            public override string[] Strings => Data.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray();
            public override string Column(int index) => index >= Data.Length ? "" : Data[index].ToString(CultureInfo.InvariantCulture);
            public override double ColumnDouble(int index) => index >= Data.Length ? 0 : Data[index];
            public override void Set(int index, string to) { Data[index] = to.ToDouble(0); }

            public BoundDataRowDouble(int index, double[] sourceRow) : base(index)
            {
                Index = index;
                Data = sourceRow;
            }

            public override IComparer<BoundDataRow> GetSortComparer(int colIndex, ListSortDirection sortDirection)
            {
                return new BoundDataRowDouble.SortComparer(colIndex, sortDirection);
            }

            public class SortComparer : IComparer<BoundDataRow>
            {
                private int ColIndex;
                private ListSortDirection Direction;

                public SortComparer(int colIndex, ListSortDirection direction)
                {
                    ColIndex = colIndex;
                    Direction = direction;
                }

                public int Compare(BoundDataRow? x, BoundDataRow? y)
                {
                    if (x == null || y == null) return 0;
                    double o1 = (x as BoundDataRowDouble)!.Data[ColIndex];
                    double o2 = (y as BoundDataRowDouble)!.Data[ColIndex];
                    double result = o1 - o2;
                    if (result == 0)
                    {
                        result = x.ResortIndex < y.ResortIndex ? -1 : 1; // stable sort
                    }
                    return Direction == ListSortDirection.Ascending ? (result > 0 ? 1 : -1) : (result > 0 ? -1 : 1);
                }
            }
        }

        public class BoundData : IBindingList
        {
            private DataGridView DataGrid;

            private Action<CsvLog.Entry> OnLog;
            private CodeProfile Profile = new CodeProfile();

            public string CsvFileName { get; private set; }
            public List<BoundDataRow> UnfilteredData;
            public List<BoundDataRow> FilteredData;
            private Stack<List<BoundDataRow>> UndoList = new Stack<List<BoundDataRow>>();
            private List<BoundDataRow> IndexToRow = new List<BoundDataRow>();
            public List<String> ColumnNames;
            public event ListChangedEventHandler? ListChanged;

            bool IBindingList.AllowNew => false;
            bool IBindingList.AllowEdit => false;
            bool IBindingList.AllowRemove => false;
            bool IBindingList.SupportsChangeNotification => true;
            bool IBindingList.SupportsSearching => false;
            bool IBindingList.SupportsSorting => true;
            bool IBindingList.IsSorted => false;

            private PropertyDescriptor? CurrentSortProperty;
            PropertyDescriptor? IBindingList.SortProperty => CurrentSortProperty;

            private int CurrentSortColIndex = -1;
            private ListSortDirection CurrentSortDirection = ListSortDirection.Ascending;
            ListSortDirection IBindingList.SortDirection => CurrentSortDirection;

            bool IList.IsReadOnly => true;
            bool IList.IsFixedSize => true;
            int ICollection.Count => FilteredData?.Count ?? 0;
            object ICollection.SyncRoot { get { throw new NotImplementedException(); } }
            bool ICollection.IsSynchronized { get { throw new NotImplementedException(); } }

            void IList.Clear() { throw new NotImplementedException(); }
            bool IList.Contains(object? value) { throw new NotImplementedException(); }
            void ICollection.CopyTo(Array? array, int index) { throw new NotImplementedException(); }
            int IBindingList.Find(PropertyDescriptor property, object key) { throw new NotImplementedException(); }
            int IList.IndexOf(object? value) { throw new NotImplementedException(); }
            int IList.Add(object? value) { throw new NotImplementedException(); }
            void IBindingList.AddIndex(PropertyDescriptor property) { throw new NotImplementedException(); }
            object IBindingList.AddNew() { throw new NotImplementedException(); }
            void IList.Insert(int index, object? value) { throw new NotImplementedException(); }
            void IList.Remove(object? value) { throw new NotImplementedException(); }
            void IList.RemoveAt(int index) { throw new NotImplementedException(); }
            void IBindingList.RemoveIndex(PropertyDescriptor property) { throw new NotImplementedException(); }
            public IEnumerator GetEnumerator() { throw new NotImplementedException(); }

            public BoundData(string csvFileName, bool numeric, Action<CsvLog.Entry> onLog)
            {
                OnLog = onLog;
                CsvFileName = csvFileName;
                UnfilteredData = new List<BoundDataRow>();
                int index = 0;

                if (numeric)
                {
                    CSVLoad<string> source = new CSVLoad<string>(filename: csvFileName,
                        parse: s => s, defaultValue: "", separator: ',', headerRowPrefix: "");
                    ColumnNames = source.ColumnHeadings.ToList();
                    foreach (var v in source)
                    {
                        BoundDataRow item = new BoundDataRowString(index, v.Row);
                        IndexToRow.Add(item);
                        UnfilteredData.Add(item);
                        index++;
                    }
                }
                else
                {
                    CSVLoad<double> source = new CSVLoad<double>(filename: csvFileName,
                        parse: s => s.ToDouble(0), defaultValue: 0, separator: ',', headerRowPrefix: "");
                    ColumnNames = source.ColumnHeadings.ToList();

                    foreach (var v in source)
                    {
                        BoundDataRow item = new BoundDataRowDouble(index, v.Row);
                        IndexToRow.Add(item);
                        UnfilteredData.Add(item);
                        index++;
                    }
                }
                FilteredData = UnfilteredData; // keep compiler happy, overridden by ShowAll
                ShowAll();
            }

            public BoundData(IEnumerable<IEnumerable<string>> source, IEnumerable<string> columnNames, Action<CsvLog.Entry> onLog)
            {
                OnLog = onLog;
                InitializeData(source, columnNames, (index, row) => new BoundDataRowString(index, row.ToArray()));
            }

            public BoundData(IEnumerable<IEnumerable<double>> source, IEnumerable<string> columnNames, Action<CsvLog.Entry> onLog)
            {
                OnLog = onLog;
                InitializeData(source, columnNames, (index, row) => new BoundDataRowDouble(index, row.ToArray()));
            }

            private void InitializeData<T>(IEnumerable<IEnumerable<T>> source, IEnumerable<string> columnNames, Func<int, IEnumerable<T>, BoundDataRow> createRowFunc)
            {
                CsvFileName = "";
                ColumnNames = columnNames.ToList();
                UnfilteredData = new List<BoundDataRow>();
                int index = 0;

                foreach (var row in source)
                {
                    BoundDataRow item = createRowFunc(index, row);
                    IndexToRow.Add(item);
                    UnfilteredData.Add(item);
                    index++;
                }
                OnLog?.Invoke(new CsvLog.Entry($"InitializeData {index} rows of {ColumnNames.Count} columns", CsvLog.Priority.Info));

                FilteredData = UnfilteredData; // keep compiler happy, overridden by ShowAll
                ShowAll();
            }

            public void Setup(DataGridView dataGrid)
            {
                DataGrid = dataGrid;

                dataGrid.AutoGenerateColumns = false;
                dataGrid.Columns.Clear();
                UndoList = new Stack<List<BoundDataRow>>();
                for (int loop = 0; loop < ColumnNames.Count; loop++)
                {
                    dataGrid.Columns.Add(new DataGridViewTextBoxColumn
                    {
                        Name = ColumnNames[loop],
                        HeaderText = ColumnNames[loop],
                        DataPropertyName = $"col{loop}",
                        SortMode = DataGridViewColumnSortMode.Automatic
                    });
                }
                dataGrid.DataSource = this;
            }

            public void Undo()
            {
                if (UndoList.Count() > 0)
                {
                    FilteredData = UndoList.Pop();
                    foreach (var v in UnfilteredData)
                    {
                        v.Visible = false;
                    }
                    foreach (var v in FilteredData)
                    {
                        v.Visible = true;
                    }
                    ReshowFiltered();
                }
            }

            private void Refilter()
            {
                Profile.Enter();
                PushUndo();

                var temp = UnfilteredData.Where(x => x.Visible).ToList();
                temp.ParallelSort((x, y) => x.ResortIndex.CompareTo(y.ResortIndex));
                FilteredData = temp;

                OnLog?.Invoke(new CsvLog.Entry(Profile.ToString(), CsvLog.Priority.Debug));
                ReshowFiltered();
                Profile.Exit();
            }

            private void ReshowFiltered()
            {
                Profile.Enter();
                int count = FilteredData.Count;
                for (int loop = 0; loop < count; loop++)
                {
                    FilteredData[loop].ResortIndex = loop;
                }
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.Reset, 0));
                Profile.Exit();
            }

            private void PushUndo()
            {
                if (FilteredData.Count() > 0)
                {
                    UndoList.Push(FilteredData.ToList());
                }
            }

            public void ShowAll()
            {
                foreach (var v in UnfilteredData)
                {
                    v.Visible = true;
                }
                Refilter();
            }

            public IEnumerable<int> RowsWithSelection(DataGridView dataGrid)
            {
                var rows = dataGrid.SelectedRows.Cast<DataGridViewRow>().Select(x => ((BoundDataRow?)(x.DataBoundItem))?.Index);
                var cells = dataGrid.SelectedCells.Cast<DataGridViewCell>().Select(x => ((BoundDataRow?)(x.OwningRow.DataBoundItem))?.Index);
                var union = rows.Union(cells).Where(x => x != null).Select(y => (int)(y ?? 0));
                return union.ToArray();
            }

            public string[]? GetSelectedRowsOfColumn(string column, DataGridView dataGrid)
            {
                int colIndex = ColumnNames.IndexOf(column);
                return colIndex == -1
                    ? null
                    : RowsWithSelection(dataGrid).Select(x => IndexToRow[x].Column(colIndex)).ToArray();
            }

            public string[]? GetColumn(string v)
            {
                int colIndex = ColumnNames.IndexOf(v);
                return colIndex == -1 ? null : FilteredData.Select(x => x.Column(colIndex)).ToArray();
            }

            public Dictionary<string, string>? GetSelectedRow(DataGridView dataGrid)
            {
                int row = RowsWithSelection(dataGrid).FirstOrDefault();
                if (row != -1)
                {
                    string[] rowData = IndexToRow[row].Strings;
                    return (Dictionary<string, string>?)ColumnNames
                        .Select((columnName, index) => new { columnName, value = index < rowData.Length ? rowData[index] : string.Empty })
                        .ToDictionary(item => item.columnName, item => item.value);
                }
                else
                {
                    return new Dictionary<string, string>();
                }
            }

            public double[]? GetColumnDouble(string v)
            {
                int colIndex = ColumnNames.IndexOf(v);
                return colIndex == -1 ? null : FilteredData.Select(x => x.ColumnDouble(colIndex)).ToArray();
            }


            public string[]? GetColumn(int colIndex)
            {
                return FilteredData.Select(x => x.Column(colIndex)).ToArray();
            }

            public string[]? GetSelectedColumnNames(DataGridView dataGrid)
            {
                var cols = dataGrid.SelectedColumns.Cast<DataGridViewColumn>().Select(x => x.Index);
                var cells = dataGrid.SelectedCells.Cast<DataGridViewCell>().Select(x => x.OwningColumn.Index);
                var union = cols.Union(cells).ToArray();
                return (string[]?)union.Select(x => ColumnNames[x]).ToArray();
            }

            public void HideRows(IEnumerable<int> selectedRows)
            {
                foreach (var v in selectedRows)
                {
                    IndexToRow[v].Visible = false;
                }
                Refilter();
            }

            public void HideRowsAbove(int row)
            {
                if (FilteredData == null) return;
                int index = FilteredData.IndexOf(IndexToRow[row]);
                if (index >= 0)
                {
                    for (int loop = 0; loop < index; loop++)
                    {
                        FilteredData[loop].Visible = false;
                    }
                    Refilter();
                }
            }

            public void HideRowsBelow(int row)
            { //here
                if (FilteredData == null) return;
                int index = FilteredData.IndexOf(IndexToRow[row]);
                if (index >= 0)
                {
                    int count = FilteredData.Count;
                    for (int loop = index + 1; loop < count; loop++)
                    {
                        FilteredData[loop].Visible = false;
                    }
                    Refilter();
                }
            }

            public void HideRowsOtherThan(IEnumerable<int> selectedRows)
            {
                if (FilteredData == null) return;
                foreach (var v in selectedRows)
                {
                    IndexToRow[v].HideNot = true;
                }
                foreach (var v in FilteredData)
                {
                    if (!v.HideNot)
                    {
                        v.Visible = false;
                    }
                    v.HideNot = false;
                }
                Refilter();
            }

            private void HideRowsIf(Func<BoundDataRow, bool> predicate)
            {
                if (FilteredData == null) return;
                foreach (var v in FilteredData.Where(predicate)) //here
                {
                    v.Visible = false;
                }
                Refilter();
            }

            public void HideNotFirstUnique(string column)
            {
                // keep the first, hide the rest
                int colIndex = ColumnNames.IndexOf(column);
                List<string> seen = new List<string>();
                HideRowsIf(x =>
                {
                    var xx = x.Column(colIndex);
                    var result = seen.Contains(xx);
                    seen.Add(xx);
                    return result;
                });
            }

            public void HideRowsMatching(string column, IEnumerable<string> rows)
            {
                // case insensitive
                List<string> strings = rows.Select(x => x.ToLower()).ToList();
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => strings.Contains(x.Column(colIndex).ToLower()));
            }

            public void HideRowsNotMatching(string column, IEnumerable<string> rows)
            {
                // case insensitive
                List<string> strings = rows.Select(x => x.ToLower()).ToList();
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => !strings.Contains(x.Column(colIndex).ToLower()));
            }

            public void ShowRowsMatchingRegex(string regex, string column)
            {
                Regex match = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => !match.IsMatch(x.Column(colIndex)));
            }

            public void HideRowsMatchingRegex(string regex, string column)
            {
                Regex match = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => match.IsMatch(x.Column(colIndex)));
            }

            object? IList.this[int index]
            {
                get { return FilteredData == null ? null : FilteredData[index]; }
                set { throw new NotImplementedException(); }
            }


            void IBindingList.ApplySort(PropertyDescriptor property, ListSortDirection direction /*ignored*/)
            {
                Profile.Enter();
                if (UnfilteredData.Count != 0)
                {
                    var prevCursor = Cursor.Current;
                    try
                    {
                        CurrentSortProperty = property;

                        int colIndex = int.Parse(property.Name.Replace("col", ""));
                        if (CurrentSortColIndex == colIndex)
                        {
                            CurrentSortDirection = (CurrentSortDirection == ListSortDirection.Ascending) ? ListSortDirection.Descending : ListSortDirection.Ascending;
                        }
                        else
                        {
                            CurrentSortDirection = ListSortDirection.Ascending;
                        }
                        CurrentSortColIndex = colIndex;
                        Cursor.Current = Cursors.WaitCursor;
                        PushUndo();

                        var temp = FilteredData?.ToList() ?? new List<BoundDataRow>();
                        temp.ParallelSort(UnfilteredData[0].GetSortComparer(colIndex, CurrentSortDirection).Compare);
                        FilteredData = temp;

                        OnLog?.Invoke(new CsvLog.Entry(Profile.ToString(), CsvLog.Priority.Debug));

                        ReshowFiltered();

                        foreach (DataGridViewColumn column in DataGrid.Columns)
                        {
                            column.HeaderCell.SortGlyphDirection = SortOrder.None;
                        }
                        DataGrid.Columns[colIndex].HeaderCell.SortGlyphDirection = CurrentSortDirection == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending;
                    }
                    catch
                    {
                    }
                    Cursor.Current = prevCursor;
                }
                Profile.Exit();
            }

            void IBindingList.RemoveSort()
            {
                CurrentSortProperty = null;
                CurrentSortDirection = ListSortDirection.Ascending;
            }

            internal void SaveToCsv(string fileName, DataGridView dataGrid)
            {
                //var rows = RowsWithSelection(dataGrid);
                //var rows = dataGrid.SelectedRows.Cast<DataGridViewRow>().Select(x => ((BoundDataRow?)(x.DataBoundItem))?.Index);
                //var cells = dataGrid.SelectedCells.Cast<DataGridViewCell>().Select(x => ((BoundDataRow?)(x.OwningRow.DataBoundItem))?.Index);
                //var union = rows.Union(cells).Where(x => x != null).Select(y => (int)(y ?? 0));

                //fixme: save selection
                CSVSave.SaveRows(fileName, ColumnNames, FilteredData.Select(x => x.Strings), ",");
            }
        }
    }

}