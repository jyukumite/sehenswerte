using SehensWerte.Files;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace SehensWerte.Controls
{
    public class DataGridViewControl : UserControl
    {
        public DataGridViewCellEventHandler CellDoubleClick = (s, e) => { };
        public DataGridViewCellContextMenuStripNeededEventHandler CellContextMenuStripNeeded = (s, e) => { };
        public DataGridViewDoubleBuffered Grid;
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
        private ToolStripDropDownButton SaveCsv;
        public BoundData? DataGridBind;
        private string RegexInput = ".*";

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
            base.Dispose(disposing);
        }

        public static implicit operator DataGridView(DataGridViewControl d) => d.Grid;

        public DataGridViewControl()
        {
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
            this.SaveCsv = new System.Windows.Forms.ToolStripDropDownButton();

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
            this.Grid.CellContextMenuStripNeeded += (s, e) => CellContextMenuStripNeeded.Invoke(s, e);

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
                    this.SaveCsv});
            this.StatusStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.StatusStrip.Location = new System.Drawing.Point(0, 1037);
            this.StatusStrip.Name = "StatusStrip";
            this.StatusStrip.Padding = new System.Windows.Forms.Padding(18, 0, 1, 0);
            this.StatusStrip.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.StatusStrip.Size = new System.Drawing.Size(1720, 42);
            this.StatusStrip.SizingGrip = false;
            this.StatusStrip.TabIndex = 13;
            this.StatusStrip.Text = "WindowStatusStrip";
            // 
            // StatusFilterText
            // 
            this.StatusFilterText.Name = "StatusFilterText";
            this.StatusFilterText.Size = new System.Drawing.Size(94, 32);
            this.StatusFilterText.Text = "Filtered";
            // 
            // ShowAllStatus
            // 
            this.ShowAllStatus.Name = "ShowAllStatus";
            this.ShowAllStatus.ShowDropDownArrow = false;
            this.ShowAllStatus.Size = new System.Drawing.Size(110, 38);
            this.ShowAllStatus.Text = "&Show All";
            this.ShowAllStatus.Click += new System.EventHandler(this.ShowAllStatus_Click);
            // 
            // UndoFilterStatus
            // 
            this.UndoFilterStatus.Name = "UndoFilterStatus";
            this.UndoFilterStatus.ShowDropDownArrow = false;
            this.UndoFilterStatus.Size = new System.Drawing.Size(76, 38);
            this.UndoFilterStatus.Text = "Undo";
            this.UndoFilterStatus.Click += new System.EventHandler(this.UndoFilterStatus_Click);
            // 
            // HideSelectedStatus
            // 
            this.HideSelectedStatus.Name = "HideSelectedStatus";
            this.HideSelectedStatus.ShowDropDownArrow = false;
            this.HideSelectedStatus.Size = new System.Drawing.Size(166, 38);
            this.HideSelectedStatus.Text = "&Hide Selected";
            this.HideSelectedStatus.Click += new System.EventHandler(this.HideSelectedStatus_Click);
            // 
            // HideUnselectedStatus
            // 
            this.HideUnselectedStatus.Name = "HideUnselectedStatus";
            this.HideUnselectedStatus.ShowDropDownArrow = false;
            this.HideUnselectedStatus.Size = new System.Drawing.Size(193, 38);
            this.HideUnselectedStatus.Text = "Hide &Unselected";
            this.HideUnselectedStatus.Click += new System.EventHandler(this.HideUnselectedStatus_Click);
            // 
            // HideAboveStatus
            // 
            this.HideAboveStatus.Name = "HideAboveStatus";
            this.HideAboveStatus.ShowDropDownArrow = false;
            this.HideAboveStatus.Size = new System.Drawing.Size(143, 38);
            this.HideAboveStatus.Text = "Hide &Above";
            this.HideAboveStatus.Click += new System.EventHandler(this.HideAboveStatus_Click);
            // 
            // HideBelowStatus
            // 
            this.HideBelowStatus.Name = "HideBelowStatus";
            this.HideBelowStatus.ShowDropDownArrow = false;
            this.HideBelowStatus.Size = new System.Drawing.Size(139, 38);
            this.HideBelowStatus.Text = "Hide &Below";
            this.HideBelowStatus.Click += new System.EventHandler(this.HideBelowStatus_Click);
            // 
            // HideByRegexStatus
            // 
            this.HideByRegexStatus.Name = "HideByRegexStatus";
            this.HideByRegexStatus.ShowDropDownArrow = false;
            this.HideByRegexStatus.Size = new System.Drawing.Size(139, 38);
            this.HideByRegexStatus.Text = "Regex Hide";
            this.HideByRegexStatus.Click += new System.EventHandler(this.HideByRegexStatus_Click);
            // 
            // ShowByRegexStatus
            // 
            this.ShowByRegexStatus.Name = "ShowByRegexStatus";
            this.ShowByRegexStatus.ShowDropDownArrow = false;
            this.ShowByRegexStatus.Size = new System.Drawing.Size(147, 38);
            this.ShowByRegexStatus.Text = "Regex Show";
            this.ShowByRegexStatus.Click += new System.EventHandler(this.ShowByRegexStatus_Click);
            // 
            // HideMatchCellStatus
            // 
            this.HideMatchCellStatus.Name = "HideMatchCellStatus";
            this.HideMatchCellStatus.ShowDropDownArrow = false;
            this.HideMatchCellStatus.Size = new System.Drawing.Size(142, 38);
            this.HideMatchCellStatus.Text = "Hide Match";
            this.HideMatchCellStatus.Click += new System.EventHandler(this.HideMatchCellStatus_Click);
            // 
            // HideUnmatchCellStatus
            // 
            this.HideUnmatchCellStatus.Name = "HideUnmatchCellStatus";
            this.HideUnmatchCellStatus.ShowDropDownArrow = false;
            this.HideUnmatchCellStatus.Size = new System.Drawing.Size(171, 38);
            this.HideUnmatchCellStatus.Text = "Hide Unmatch";
            this.HideUnmatchCellStatus.Click += new System.EventHandler(this.HideUnmatchCellStatus_Click);
            // 
            // SaveCsv
            // 
            this.SaveCsv.Name = "SaveCsv";
            this.SaveCsv.ShowDropDownArrow = false;
            this.SaveCsv.Size = new System.Drawing.Size(171, 38);
            this.SaveCsv.Text = "Save";
            this.SaveCsv.Click += new System.EventHandler(this.SaveCsv_Click);
            // 
            // CloudDataQueryTab
            // 
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

                DataGridBind?.HideRowsMatching(
                    Convert.ToString(Grid.CurrentCell.OwningColumn.HeaderText) ?? "",
                    Convert.ToString(Grid.CurrentCell.Value) ?? "");
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

                DataGridBind?.HideRowsNotMatching(
                    Convert.ToString(Grid.CurrentCell.OwningColumn.HeaderText) ?? "",
                    Convert.ToString(Grid.CurrentCell.Value) ?? "");
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

        public void LoadCsv(string fileName)
        {
            DataGridBind = new BoundData(fileName);
            DataGridBind.ListChanged += GridData_ListChanged;
            DataGridBind.Setup(this);
            UpdateStatusStrip();
        }

        public void LoadRows(IEnumerable<IEnumerable<string>> rows, IEnumerable<string> colnames)
        {
            DataGridBind = new BoundData(rows, colnames);
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

        public string[] GetColumn(string header)
        {
            return DataGridBind?.GetColumn(header) ?? new string[] { };
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
            return ((DataGridBind?.FilteredData[rowIndex])?.SourceRow[colIndex]) ?? "";
        }

        public string GetCell(int colIndex, int rowIndex)
        {
            return ((DataGridBind?.FilteredData[rowIndex])?.SourceRow[colIndex]) ?? "";
        }

        public void SetCell(int colIndex, int rowIndex, string to)
        {
            if (DataGridBind == null) return;
            BoundDataRow? row;
            if ((row = DataGridBind.FilteredData[rowIndex]) == null) return;
            if (colIndex >= 0 && colIndex < row.SourceRow.Length)
            {
                row.SourceRow[colIndex] = to;
            }
        }

        public class BoundDataRow
        {
            public string[] SourceRow;
            public bool Visible = true;
            public bool HideNot = false;
            public int Index;
            public int ResortIndex;

            public BoundDataRow(int index, string[] sourceRow)
            {
                Index = index;
                SourceRow = sourceRow;
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
                    string o1 = x.SourceRow[ColIndex].ToLower();
                    string o2 = y.SourceRow[ColIndex].ToLower();
                    int result = o1.NaturalCompare(o2);
                    if (result == 0)
                    {
                        result = x.ResortIndex < y.ResortIndex ? -1 : 1;
                    }
                    return Direction == ListSortDirection.Ascending ? result : -result;
                }
            }

            public String col0 => SourceRow.Length > 0 ? SourceRow[0] : ""; public String col1 => SourceRow.Length > 1 ? SourceRow[1] : ""; public String col2 => SourceRow.Length > 2 ? SourceRow[2] : ""; public String col3 => SourceRow.Length > 3 ? SourceRow[3] : "";
            public String col4 => SourceRow.Length > 4 ? SourceRow[4] : ""; public String col5 => SourceRow.Length > 5 ? SourceRow[5] : ""; public String col6 => SourceRow.Length > 6 ? SourceRow[6] : ""; public String col7 => SourceRow.Length > 7 ? SourceRow[7] : "";
            public String col8 => SourceRow.Length > 8 ? SourceRow[8] : ""; public String col9 => SourceRow.Length > 9 ? SourceRow[9] : ""; public String col10 => SourceRow.Length > 10 ? SourceRow[10] : ""; public String col11 => SourceRow.Length > 11 ? SourceRow[11] : "";
            public String col12 => SourceRow.Length > 12 ? SourceRow[12] : ""; public String col13 => SourceRow.Length > 13 ? SourceRow[13] : ""; public String col14 => SourceRow.Length > 14 ? SourceRow[14] : ""; public String col15 => SourceRow.Length > 15 ? SourceRow[15] : "";
            public String col16 => SourceRow.Length > 16 ? SourceRow[16] : ""; public String col17 => SourceRow.Length > 17 ? SourceRow[17] : ""; public String col18 => SourceRow.Length > 18 ? SourceRow[18] : ""; public String col19 => SourceRow.Length > 19 ? SourceRow[19] : "";
            public String col20 => SourceRow.Length > 20 ? SourceRow[20] : ""; public String col21 => SourceRow.Length > 21 ? SourceRow[21] : ""; public String col22 => SourceRow.Length > 22 ? SourceRow[22] : ""; public String col23 => SourceRow.Length > 23 ? SourceRow[23] : "";
            public String col24 => SourceRow.Length > 24 ? SourceRow[24] : ""; public String col25 => SourceRow.Length > 25 ? SourceRow[25] : ""; public String col26 => SourceRow.Length > 26 ? SourceRow[26] : ""; public String col27 => SourceRow.Length > 27 ? SourceRow[27] : "";
            public String col28 => SourceRow.Length > 28 ? SourceRow[28] : ""; public String col29 => SourceRow.Length > 29 ? SourceRow[29] : ""; public String col30 => SourceRow.Length > 30 ? SourceRow[30] : ""; public String col31 => SourceRow.Length > 31 ? SourceRow[31] : "";
            public String col32 => SourceRow.Length > 32 ? SourceRow[32] : ""; public String col33 => SourceRow.Length > 33 ? SourceRow[33] : ""; public String col34 => SourceRow.Length > 34 ? SourceRow[34] : ""; public String col35 => SourceRow.Length > 35 ? SourceRow[35] : "";
            public String col36 => SourceRow.Length > 36 ? SourceRow[36] : ""; public String col37 => SourceRow.Length > 37 ? SourceRow[37] : ""; public String col38 => SourceRow.Length > 38 ? SourceRow[38] : ""; public String col39 => SourceRow.Length > 39 ? SourceRow[39] : "";
            public String col40 => SourceRow.Length > 40 ? SourceRow[40] : ""; public String col41 => SourceRow.Length > 41 ? SourceRow[41] : ""; public String col42 => SourceRow.Length > 42 ? SourceRow[42] : ""; public String col43 => SourceRow.Length > 43 ? SourceRow[43] : "";
            public String col44 => SourceRow.Length > 44 ? SourceRow[44] : ""; public String col45 => SourceRow.Length > 45 ? SourceRow[45] : ""; public String col46 => SourceRow.Length > 46 ? SourceRow[46] : ""; public String col47 => SourceRow.Length > 47 ? SourceRow[47] : "";
            public String col48 => SourceRow.Length > 48 ? SourceRow[48] : ""; public String col49 => SourceRow.Length > 49 ? SourceRow[49] : ""; public String col50 => SourceRow.Length > 50 ? SourceRow[50] : ""; public String col51 => SourceRow.Length > 51 ? SourceRow[51] : "";
            public String col52 => SourceRow.Length > 52 ? SourceRow[52] : ""; public String col53 => SourceRow.Length > 53 ? SourceRow[53] : ""; public String col54 => SourceRow.Length > 54 ? SourceRow[54] : ""; public String col55 => SourceRow.Length > 55 ? SourceRow[55] : "";
            public String col56 => SourceRow.Length > 56 ? SourceRow[56] : ""; public String col57 => SourceRow.Length > 57 ? SourceRow[57] : ""; public String col58 => SourceRow.Length > 58 ? SourceRow[58] : ""; public String col59 => SourceRow.Length > 59 ? SourceRow[59] : "";
            public String col60 => SourceRow.Length > 60 ? SourceRow[60] : ""; public String col61 => SourceRow.Length > 61 ? SourceRow[61] : ""; public String col62 => SourceRow.Length > 62 ? SourceRow[62] : ""; public String col63 => SourceRow.Length > 63 ? SourceRow[63] : "";
            public String col64 => SourceRow.Length > 64 ? SourceRow[64] : ""; public String col65 => SourceRow.Length > 65 ? SourceRow[65] : ""; public String col66 => SourceRow.Length > 66 ? SourceRow[66] : ""; public String col67 => SourceRow.Length > 67 ? SourceRow[67] : "";
            public String col68 => SourceRow.Length > 68 ? SourceRow[68] : ""; public String col69 => SourceRow.Length > 69 ? SourceRow[69] : ""; public String col70 => SourceRow.Length > 70 ? SourceRow[70] : ""; public String col71 => SourceRow.Length > 71 ? SourceRow[71] : "";
            public String col72 => SourceRow.Length > 72 ? SourceRow[72] : ""; public String col73 => SourceRow.Length > 73 ? SourceRow[73] : ""; public String col74 => SourceRow.Length > 74 ? SourceRow[74] : ""; public String col75 => SourceRow.Length > 75 ? SourceRow[75] : "";
            public String col76 => SourceRow.Length > 76 ? SourceRow[76] : ""; public String col77 => SourceRow.Length > 77 ? SourceRow[77] : ""; public String col78 => SourceRow.Length > 78 ? SourceRow[78] : ""; public String col79 => SourceRow.Length > 79 ? SourceRow[79] : "";
            public String col80 => SourceRow.Length > 80 ? SourceRow[80] : ""; public String col81 => SourceRow.Length > 81 ? SourceRow[81] : ""; public String col82 => SourceRow.Length > 82 ? SourceRow[82] : ""; public String col83 => SourceRow.Length > 83 ? SourceRow[83] : "";
            public String col84 => SourceRow.Length > 84 ? SourceRow[84] : ""; public String col85 => SourceRow.Length > 85 ? SourceRow[85] : ""; public String col86 => SourceRow.Length > 86 ? SourceRow[86] : ""; public String col87 => SourceRow.Length > 87 ? SourceRow[87] : "";
            public String col88 => SourceRow.Length > 88 ? SourceRow[88] : ""; public String col89 => SourceRow.Length > 89 ? SourceRow[89] : ""; public String col90 => SourceRow.Length > 90 ? SourceRow[90] : ""; public String col91 => SourceRow.Length > 91 ? SourceRow[91] : "";
            public String col92 => SourceRow.Length > 92 ? SourceRow[92] : ""; public String col93 => SourceRow.Length > 93 ? SourceRow[93] : ""; public String col94 => SourceRow.Length > 94 ? SourceRow[94] : ""; public String col95 => SourceRow.Length > 95 ? SourceRow[95] : "";
            public String col96 => SourceRow.Length > 96 ? SourceRow[96] : ""; public String col97 => SourceRow.Length > 97 ? SourceRow[97] : ""; public String col98 => SourceRow.Length > 98 ? SourceRow[98] : ""; public String col99 => SourceRow.Length > 99 ? SourceRow[99] : "";
        }

        public class BoundData : IBindingList
        {
            public string CsvFileName { get; private set; }
            public List<BoundDataRow> UnfilteredData;
            public List<BoundDataRow> FilteredData;
            private PropertyDescriptor? CurrentSortProperty;
            private ListSortDirection CurrentSortDirection = ListSortDirection.Ascending;
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
            PropertyDescriptor? IBindingList.SortProperty => CurrentSortProperty;
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


            public BoundData(string csvFileName)
            {
                CsvFileName = csvFileName;
                CSVLoad<string> Source = new CSVLoad<string>(filename: csvFileName,
                    parse: s => s, defaultValue: "", separator: ',', headerRowPrefix: "");
                ColumnNames = Source.ColumnHeadings.ToList();

                var stack = new System.Collections.Concurrent.ConcurrentStack<BoundDataRow>();
                UnfilteredData = new List<BoundDataRow>();
                int index = 0;
                foreach (var v in Source)
                {
                    BoundDataRow item = new BoundDataRow(index, v.Row);
                    IndexToRow.Add(item);
                    UnfilteredData.Add(item);
                    index++;
                }
                FilteredData = UnfilteredData; // keep compiler happy, overridden by ShowAll
                ShowAll();
            }

            public BoundData(IEnumerable<IEnumerable<string>> source, IEnumerable<string> columnNames)
            {
                CsvFileName = "";
                ColumnNames = columnNames.ToList();

                var stack = new System.Collections.Concurrent.ConcurrentStack<BoundDataRow>();
                UnfilteredData = new List<BoundDataRow>();
                int index = 0;
                foreach (var v in source)
                {
                    BoundDataRow item = new BoundDataRow(index, v.ToArray());
                    IndexToRow.Add(item);
                    UnfilteredData.Add(item);
                    index++;
                }
                FilteredData = UnfilteredData; // keep compiler happy, overridden by ShowAll
                ShowAll();
            }

            public void Setup(DataGridView dataGrid)
            {
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
                PushUndo();
                FilteredData = UnfilteredData.Where(x => x.Visible).OrderBy(x=>x.ResortIndex).ToList();
                ReshowFiltered();
            }

            private void ReshowFiltered()
            {
                int count = FilteredData.Count;
                for (int loop = 0; loop < count; loop++)
                {
                    FilteredData[loop].ResortIndex = loop;
                }
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.Reset, 0));
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


            public string[] GetSelectedRowsOfColumn(string column, DataGridView dataGrid)
            {
                int colIndex = ColumnNames.IndexOf(column);
                return colIndex == -1
                    ? new string[] { }
                    : RowsWithSelection(dataGrid).Select(x => IndexToRow[x].SourceRow[colIndex]).ToArray();
            }


            public string[]? GetColumn(string v)
            {
                int colIndex = ColumnNames.IndexOf(v);
                return colIndex == -1 ? null : FilteredData.Select(x => x.SourceRow[colIndex]).ToArray();
            }

            public string[]? GetColumn(int colIndex)
            {
                return FilteredData.Select(x => x.SourceRow[colIndex]).ToArray();
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
            {
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
                foreach (var v in FilteredData.Where(predicate))
                {
                    v.Visible = false;
                }
                Refilter();
            }

            public void HideRowsMatching(string column, string text)
            {
                string lowerText = text.ToLower();
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => x.SourceRow[colIndex].ToLower() == lowerText);
            }

            public void HideRowsNotMatching(string column, string text)
            {
                string lowerText = text.ToLower();
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => x.SourceRow[colIndex].ToLower() != lowerText);
            }

            public void ShowRowsMatchingRegex(string regex, string column)
            {
                Regex match = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => !match.IsMatch(x.SourceRow[colIndex]));
            }

            public void HideRowsMatchingRegex(string regex, string column)
            {
                Regex match = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => match.IsMatch(x.SourceRow[colIndex]));
            }

            object? IList.this[int index]
            {
                get { return FilteredData == null ? null : FilteredData[index]; }
                set { throw new NotImplementedException(); }
            }

            void IBindingList.ApplySort(PropertyDescriptor property, ListSortDirection direction)
            {
                CurrentSortProperty = property;
                CurrentSortDirection = direction;
                var prevCursor = Cursor.Current;
                try
                {
                    int colIndex = int.Parse(property.Name.Replace("col", ""));
                    Cursor.Current = Cursors.WaitCursor;
                    PushUndo();
                    FilteredData?.Sort(new BoundDataRow.SortComparer(colIndex, direction));
                    ReshowFiltered();
                }
                catch
                {
                }
                Cursor.Current = prevCursor;
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
                CSVSave.SaveRows(fileName, ColumnNames, FilteredData.Select(x => x.SourceRow), ",");
            }
        }
    }
}