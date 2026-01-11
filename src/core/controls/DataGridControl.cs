using SehensWerte.Files;
using SehensWerte.Maths;
using SehensWerte.Utils;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Drawing;
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
        public int ToolTipPauseMilliseconds { get; set; } = 500;
        public int ToolTipMaxLength { get; set; } = 1000;

        public string[]? MaskColumns { get; set; }
        public string MaskString { get; set; } = new string('\u2022', 5); // small dot

        Action<CsvLog.Entry>? OnLog;

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
        private ToolStripDropDownButton DecimateCellStatus;
        private ToolStripDropDownButton TransposeGrid;
        private ToolStripDropDownButton SaveCsvButton;
        private ToolStripDropDownButton LoadCsvButton;
        private System.Windows.Forms.ToolTip HoverTip;


        public BoundData? DataGridBind;
        private string RegexInput = ".*";

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
            base.Dispose(disposing);
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
                int safetyTimer = ToolTipShowMilliseconds + 1000;
                HoverTip.Show(e.DisplayText, Grid, Grid.PointToClient(e.MouseLocation), safetyTimer);
            };
            HideTooltipWindow += (s, e) =>
            {
                HoverTip.Hide(Grid);
            };
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
            this.DecimateCellStatus = new System.Windows.Forms.ToolStripDropDownButton();
            this.TransposeGrid = new System.Windows.Forms.ToolStripDropDownButton();
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
            this.Grid.ColumnDividerDoubleClick += Grid_ColumnDividerDoubleClick;
            this.Grid.CellPainting += Grid_CellPainting;
            this.Grid.KeyDown += Grid_KeyDown;
            this.Grid.CellClick += (s, e) => CellClick.Invoke(s, e);
            this.Grid.CellContextMenuStripNeeded += (s, e) => CellContextMenuStripNeeded.Invoke(s, e);
            this.Grid.CellMouseEnter += Grid_CellMouseEnter;
            this.Grid.CellMouseLeave += (s, e) => { HoverHide(); };
            this.Grid.ShowCellToolTips = false;
            this.Grid.CellToolTipTextNeeded += (s, e) => { e.ToolTipText = ""; };
            this.NullForeColor = this.Grid.ForeColor;

            this.HoverShowTimer = new System.Windows.Forms.Timer();
            this.HoverShowTimer.Tick += (s, e) => { HoverShow(); };
            this.HoverHideTimer = new System.Windows.Forms.Timer();
            this.HoverHideTimer.Tick += (s, e) => { HoverHide(); };

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
                    this.DecimateCellStatus,
                    //this.TransposeGrid,
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

            this.DecimateCellStatus.Name = "DecimateCellStatus";
            this.DecimateCellStatus.ShowDropDownArrow = false;
            this.DecimateCellStatus.Size = new System.Drawing.Size(171, 38);
            this.DecimateCellStatus.Text = "Decimate";
            this.DecimateCellStatus.Click += new System.EventHandler(this.DecimateCellStatus_Click);
            this.DecimateCellStatus.BackColor = prettyColours ? Color.FromArgb(242, 196, 208) : SystemColors.Control;

            this.TransposeGrid.Name = "TransposeGrid";
            this.TransposeGrid.ShowDropDownArrow = false;
            this.TransposeGrid.Size = new System.Drawing.Size(171, 38);
            this.TransposeGrid.Text = "Transpose";
            this.TransposeGrid.Click += new System.EventHandler(this.TransposeGrid_Click);
            this.TransposeGrid.BackColor = prettyColours ? Color.FromArgb(242, 196, 208) : SystemColors.Control;

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
            if (e.Control && e.KeyCode == Keys.C)
            {
                string[] formats = new string[] { DataFormats.Html, DataFormats.Text, DataFormats.UnicodeText, DataFormats.CommaSeparatedValue };


                if (DataGridBind != null)
                {
                    //var temp = Grid.GetClipboardContent();
                    var data = DataGridBind.SelectedCellsToClipboardFormats(NumericGrid);
                    var dataObj = new DataObject();
                    dataObj.SetData(DataFormats.Text, data.tsv);
                    dataObj.SetData(DataFormats.UnicodeText, data.tsv);
                    dataObj.SetData(DataFormats.Html, data.wrappedHtml);
                    dataObj.SetData(DataFormats.CommaSeparatedValue, data.csv);
                    Clipboard.SetDataObject(dataObj, true);
                }
                e.Handled = true;
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

                Font font = grid.ColumnHeadersDefaultCellStyle.Font;
                SizeF headerTextSize = TextRenderer.MeasureText(column.HeaderText, font);
                int headerWidth = (int)Math.Ceiling(headerTextSize.Width) + padding;

                ThreadLocal<(Graphics g, int m)> widths = new ThreadLocal<(Graphics g, int m)>(() =>
                    (Graphics.FromImage(new Bitmap(1, 1)), headerWidth), trackAllValues: true
                );
                Parallel.ForEach(strings, str =>
                {
                    if (str != null)
                    {
                        Graphics g = widths.Value.g;
                        SizeF textSize = g.MeasureString(str, font);
                        int width = (int)Math.Ceiling(textSize.Width) + padding;
                        widths.Value = (g, Math.Max(widths.Value.m, width));
                    }
                });
                foreach (var v in widths.Values)
                {
                    v.g.Dispose();
                }

                int maxWidth = widths.Values.Max(value => value.m);
                column.Width = Math.Max(10, Math.Min(grid.Parent.Width - 20, maxWidth + 10));
                widths.Dispose();

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
                DataGridBind?.HideRows(DataGridBind.RowsWithSelection());
            });
        }

        private void HideUnselectedStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.HideRowsOtherThan(DataGridBind.RowsWithSelection());
            });
        }

        private void HideAboveStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.HideRowsAbove(DataGridBind.RowsWithSelection().FirstOrDefault());
            });
        }

        private void HideBelowStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                DataGridBind?.HideRowsBelow(DataGridBind.RowsWithSelection().FirstOrDefault());
            });
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
                });
            }
        }

        private void ShowByRegexStatus_Click(object? sender, EventArgs e)
        {
            if (Grid.SelectedCells.Count != 1) return;
            string header = Convert.ToString(Grid.CurrentCell.OwningColumn.HeaderText);
            string regex = InputFieldForm.Show($"Show {header} by regex", "Regex", RegexInput, regex: true) ?? "";
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

        private void DecimateCellStatus_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
                if (Grid.SelectedCells.Count == 0)
                {
                    return;
                }

                string header = Grid.CurrentCell.OwningColumn.HeaderText;
                int counter = 0;
                DataGridBind?.HideRowsIf(_ => counter++ % 10 != 0);
            });
        }

        private void TransposeGrid_Click(object? sender, EventArgs e)
        {
            this.ExceptionToMessagebox(() =>
            {
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
                        DataGridBind?.SaveToCsv(m_SaveFileDialog.FileName);
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

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            bool selected = e.State.HasFlag(DataGridViewElementStates.Selected);
            e.PaintBackground(e.ClipBounds, selected);
            e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);

            if (!selected && DataGridBind?.FilteredData.Count > e.RowIndex)
            {
                var a = DataGridBind.FilteredData[e.RowIndex];
                var b = a?.Colours?[e.ColumnIndex];
                if (b != null)
                {
                    e.Graphics.FillRectangle(new SolidBrush(b.Value), e.CellBounds);
                }
            }

            string colName = Grid.Columns[e.ColumnIndex].Name;
            bool isNull = e.Value == null;
            string realText = e.Value?.ToString() ?? "null";
            bool masked = !isNull && realText != "" && (MaskColumns?.Contains(colName) ?? false);
            string displayText = masked ? MaskString : realText;

            Font cellFont = isNull ? new Font(e.CellStyle.Font, FontStyle.Italic) : e.CellStyle.Font;
            Color textColor = isNull ? NullForeColor : e.CellStyle.ForeColor;
            StringFormat stringFormat = new StringFormat
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

            stringFormat.LineAlignment = e.CellStyle.Alignment switch
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

            Region originalClip = e.Graphics.Clip.Clone();
            e.Graphics.SetClip(e.CellBounds);
            using (SolidBrush textBrush = new SolidBrush(textColor))
            {
                e.Graphics.DrawString(displayText, cellFont, textBrush, textBounds, stringFormat);
            }
            e.Graphics.Clip = originalClip;
            e.Handled = true;
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

        public void SortByColumn(string heading)
        {
            Grid.Sort(Grid.Columns[heading], ListSortDirection.Ascending);
        }

        public void UpdateStatusStrip()
        {
            int total = DataGridBind?.UnfilteredData.Count ?? 0;
            int showing = DataGridBind?.FilteredData.Count ?? 0;
            StatusFilterText.Text = (showing == total) ? $"{total}rows" : $"{showing}/{total}";
            StatusFilterText.Visible = true;
        }


        public void Clear()
        {
            DataGridBind?.Unbind();
            NumericGrid = false;
            DataGridBind = null;
            Grid.Columns.Clear();
        }

        public void LoadCsv(string fileName, bool numeric = false)
        {
            DataGridBind?.Unbind();
            NumericGrid = numeric;
            DataGridBind = new BoundData(fileName, numeric: numeric, CsvLog.ExtendPath(OnLog, "BoundData"));
            DataGridBind.ListChanged += GridData_ListChanged;
            DataGridBind.Setup(Grid);
            UpdateStatusStrip();
        }

        public void LoadRows(IEnumerable<IEnumerable<string?>> rows, IEnumerable<string> colnames)
        {
            DataGridBind?.Unbind();
            NumericGrid = false;
            DataGridBind = new BoundData(rows, colnames, CsvLog.ExtendPath(OnLog, "BoundData"));
            DataGridBind.ListChanged += GridData_ListChanged;
            DataGridBind.Setup(Grid);
            UpdateStatusStrip();
        }

        public void LoadRows(IEnumerable<IEnumerable<double>> rows, IEnumerable<string> colnames)
        {
            DataGridBind?.Unbind();
            NumericGrid = true;
            DataGridBind = new BoundData(rows, colnames, CsvLog.ExtendPath(OnLog, "BoundData"));
            DataGridBind.ListChanged += GridData_ListChanged;
            DataGridBind.Setup(Grid);
            UpdateStatusStrip();
        }

        private void GridData_ListChanged(object? sender, ListChangedEventArgs e)
        {
            UpdateStatusStrip();
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

        public void CollapseColumn(string column)
        {
            if (Grid.Columns.Contains(column))
            {
                Grid.Columns[column].Width = Grid.Columns[column].MinimumWidth;
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

        public class UndoEntry
        {
            // shouldn't mutate caller's data, or callbacks may lose caller's context
            // consider: save context for caller, or adjust for viewer?
            public List<BoundDataRow>? VisibleRows;
        }
    }
}