using SehensWerte.Files;
using SehensWerte.Maths;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SehensWerte.Controls
{
    public partial class DataGridControl
    {
        public abstract class BoundDataRow
        {
            public bool Visible = true;
            public bool HideNot = false;
            public int Index;
            public int ResortIndex;
            public Color?[]? Colours = null;
            public StringDiff.Diffs?[]? Diffs = null;

            public BoundDataRow(int index)
            {
                Index = index;
            }

            public abstract string?[] Strings { get; }
            public abstract string? Column(int index);
            public abstract double ColumnDouble(int colIndex);
            public abstract int Count { get; }
            public abstract void Set(int index, string? to);

            public abstract IComparer<BoundDataRow> GetSortComparer(int colIndex, ListSortDirection sortDirection);

            //DataPropertyName = $"col{loop}",
            public String? col0 => Column(0); public String? col1 => Column(1); public String? col2 => Column(2); public String? col3 => Column(3);
            public String? col4 => Column(4); public String? col5 => Column(5); public String? col6 => Column(6); public String? col7 => Column(7);
            public String? col8 => Column(8); public String? col9 => Column(9); public String? col10 => Column(10); public String? col11 => Column(11);
            public String? col12 => Column(12); public String? col13 => Column(13); public String? col14 => Column(14); public String? col15 => Column(15);
            public String? col16 => Column(16); public String? col17 => Column(17); public String? col18 => Column(18); public String? col19 => Column(19);
            public String? col20 => Column(20); public String? col21 => Column(21); public String? col22 => Column(22); public String? col23 => Column(23);
            public String? col24 => Column(24); public String? col25 => Column(25); public String? col26 => Column(26); public String? col27 => Column(27);
            public String? col28 => Column(28); public String? col29 => Column(29); public String? col30 => Column(30); public String? col31 => Column(31);
            public String? col32 => Column(32); public String? col33 => Column(33); public String? col34 => Column(34); public String? col35 => Column(35);
            public String? col36 => Column(36); public String? col37 => Column(37); public String? col38 => Column(38); public String? col39 => Column(39);
            public String? col40 => Column(40); public String? col41 => Column(41); public String? col42 => Column(42); public String? col43 => Column(43);
            public String? col44 => Column(44); public String? col45 => Column(45); public String? col46 => Column(46); public String? col47 => Column(47);
            public String? col48 => Column(48); public String? col49 => Column(49); public String? col50 => Column(50); public String? col51 => Column(51);
            public String? col52 => Column(52); public String? col53 => Column(53); public String? col54 => Column(54); public String? col55 => Column(55);
            public String? col56 => Column(56); public String? col57 => Column(57); public String? col58 => Column(58); public String? col59 => Column(59);
            public String? col60 => Column(60); public String? col61 => Column(61); public String? col62 => Column(62); public String? col63 => Column(63);
            public String? col64 => Column(64); public String? col65 => Column(65); public String? col66 => Column(66); public String? col67 => Column(67);
            public String? col68 => Column(68); public String? col69 => Column(69); public String? col70 => Column(70); public String? col71 => Column(71);
            public String? col72 => Column(72); public String? col73 => Column(73); public String? col74 => Column(74); public String? col75 => Column(75);
            public String? col76 => Column(76); public String? col77 => Column(77); public String? col78 => Column(78); public String? col79 => Column(79);
            public String? col80 => Column(80); public String? col81 => Column(81); public String? col82 => Column(82); public String? col83 => Column(83);
            public String? col84 => Column(84); public String? col85 => Column(85); public String? col86 => Column(86); public String? col87 => Column(87);
            public String? col88 => Column(88); public String? col89 => Column(89); public String? col90 => Column(90); public String? col91 => Column(91);
            public String? col92 => Column(92); public String? col93 => Column(93); public String? col94 => Column(94); public String? col95 => Column(95);
            public String? col96 => Column(96); public String? col97 => Column(97); public String? col98 => Column(98); public String? col99 => Column(99);

            public void CellColour(int col, Color colour)
            {
                if (Colours == null)
                {
                    Colours = new Color?[Count];
                }
                Colours[col] = colour;
            }

            public void CellDiffs(int col, StringDiff.Diffs? diff)
            {
                if (Diffs == null)
                {
                    if (diff == null)
                    {
                        return;
                    }
                    Diffs = new StringDiff.Diffs?[Count];
                }
                if (col >= 0 && col < Diffs.Length)
                {
                    Diffs[col] = diff;
                }
            }
        }

        public class BoundDataRowString : BoundDataRow
        {
            public string?[] Data;

            public override int Count => Data.Length;
            public override string?[] Strings => Data;
            public override string? Column(int index) => index < Data.Length ? Data[index] : "";
            public override double ColumnDouble(int index) => index < Data.Length ? (Data[index]?.ToDouble(0) ?? 0) : 0;
            public override void Set(int index, string? to)
            {
                Data[index] = to;
                CellDiffs(index, null);
            }

            public BoundDataRowString(int index, string?[] sourceRow) : base(index)
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
                    string o1 = (x as BoundDataRowString)?.Data[ColIndex]?.ToLower() ?? "";
                    string o2 = (y as BoundDataRowString)?.Data[ColIndex]?.ToLower() ?? "";
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
            public override void Set(int index, string? to)
            {
                Data[index] = to?.ToDouble(0) ?? 0;
                CellDiffs(index, null);
            }

            public BoundDataRowDouble(int index, double[] sourceRow) : base(index)
            {
                ResortIndex = index;
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

        public record struct SelectedCellsData(string[] headers, string?[,] strings, string csv, string tsv, string html, string wrappedHtml);

        public class BoundData : IBindingList
        {
            private DataGridControl? DataGrid;

            private Action<CsvLog.Entry> OnLog;
            public CodeProfile Profile => m_Profile;
            private CodeProfile m_Profile = new CodeProfile();

            public string CsvFileName { get; private set; }
            public List<BoundDataRow> UnfilteredData;
            public List<BoundDataRow> FilteredData;
            private DataGridControlHistory m_History = new DataGridControlHistory();
            private DataGridControlHistory m_RedoStack = new DataGridControlHistory();
            private bool m_SuppressRedoClear = false;
            private List<BoundDataRow> IndexToRow = new List<BoundDataRow>();
            public List<String> ColumnNames;
            public event ListChangedEventHandler? ListChanged;

            public bool CanUndo => m_History.History.Count > 0;
            public bool CanRedo => m_RedoStack.History.Count > 0;
            public bool IsFiltered => FilteredData.Count != UnfilteredData.Count;

            bool IBindingList.AllowNew => false;
            bool IBindingList.AllowEdit => false;
            bool IBindingList.AllowRemove => false;
            bool IBindingList.SupportsChangeNotification => true;
            bool IBindingList.SupportsSearching => false;
            bool IBindingList.SupportsSorting => true;

            private PropertyDescriptor? CurrentSortProperty;
            PropertyDescriptor? IBindingList.SortProperty => CurrentSortProperty;

            private List<(string columnName, ListSortDirection direction)> CurrentSortKeys()
            {
                var result = new List<(string columnName, ListSortDirection direction)>();
                foreach (var snap in m_History.History)
                {
                    if (snap.Action?.Kind == DataGridControlHistory.FilterAction.Operation.ApplySort)
                    {
                        string col = snap.Action.Column;
                        result.RemoveAll(k => k.columnName == col);
                        result.Add((col, snap.Action.Direction));
                    }
                }
                return result;
            }

            ListSortDirection IBindingList.SortDirection
            {
                get
                {
                    var keys = CurrentSortKeys();
                    return keys.Count > 0 ? keys[^1].direction : ListSortDirection.Ascending;
                }
            }
            bool IBindingList.IsSorted => CurrentSortKeys().Count > 0;


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
                else
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
                FilteredData = UnfilteredData; // keep compiler happy, overridden by ShowAll
                ShowAll();
            }

            public BoundData(IEnumerable<IEnumerable<string?>> source, IEnumerable<string> columnNames, Action<CsvLog.Entry> onLog)
            {
                OnLog = onLog;
                InitializeData(source, columnNames, (index, row) => new BoundDataRowString(index, row.ToArray()));
            }

            public BoundData(IEnumerable<IEnumerable<double>> source, IEnumerable<string> columnNames, Action<CsvLog.Entry> onLog)
            {
                OnLog = onLog;
                InitializeData(source, columnNames, (index, row) => new BoundDataRowDouble(index, row.ToArray()));
            }

            public void AppendRows(IEnumerable<IEnumerable<string?>> newRows)
            {
                foreach (var row in newRows)
                {
                    int index = UnfilteredData.Count;
                    var item = new BoundDataRowString(index, row.ToArray());
                    IndexToRow.Add(item);
                    UnfilteredData.Add(item);
                    // FilteredData may be a separate list after ShowAll()/Refilter(); only add visible rows
                    if (item.Visible)
                    {
                        FilteredData.Add(item);
                    }
                }
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.Reset, 0));
            }

            [MemberNotNull(nameof(CsvFileName), nameof(ColumnNames), nameof(UnfilteredData), nameof(FilteredData))]
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

            public void Setup(DataGridControl dataGrid)
            {
                DataGrid = dataGrid;
                m_History = new DataGridControlHistory();
                RebuildGridColumns();
            }

            internal void RebuildGridColumns()
            {
                if (DataGrid == null) return;
                DataGridView grid = DataGrid.Grid;
                grid.DataSource = null;
                grid.AutoGenerateColumns = false;
                grid.Columns.Clear();
                for (int loop = 0; loop < ColumnNames.Count; loop++)
                {
                    grid.Columns.Add(new DataGridViewTextBoxColumn
                    {
                        Name = ColumnNames[loop],
                        HeaderText = ColumnNames[loop],
                        DataPropertyName = $"col{loop}",
                        SortMode = DataGridViewColumnSortMode.Automatic
                    });
                }
                grid.DataSource = this;
            }

            public void Unbind()
            {
                // could release resources, but the GC should clean up anyway
            }

            public void CellColour(int col, int row, Color colour)
            {
                FilteredData[row].CellColour(col, colour);
            }

            public void CellDiffs(int col, int row, StringDiff.Diffs? diff)
            {
                FilteredData[row].CellDiffs(col, diff);
            }

            public IEnumerable<(string Name, int Width)>? Undo()
            {
                if (m_History.History.Count == 0)
                {
                    OnLog?.Invoke(new CsvLog.Entry("Undo: history empty, nothing to pop", CsvLog.Priority.Debug));
                    return null;
                }
                int last = m_History.History.Count - 1;
                var snap = m_History.History[last];
                m_History.History.RemoveAt(last);
                m_RedoStack.History.Add(snap);
                OnLog?.Invoke(new CsvLog.Entry(
                    $"Undo: popped {Describe(snap.Action)} | buffer: {DescribeHistory()} | redo depth: {m_RedoStack.History.Count}",
                    CsvLog.Priority.Debug));
                bool wasTranspose = snap.Action?.Kind == DataGridControlHistory.FilterAction.Operation.Transpose;
                if (wasTranspose)
                {
                    UndoTranspose(snap);
                }
                else
                {
                    ApplyVisible(snap);
                }
                ReshowFiltered();
                UpdateSortGlyphs();

                const int defaultColumnWidth = 100;
                var widths = new List<(string Name, int Width)>();
                if (snap.Action?.Kind == DataGridControlHistory.FilterAction.Operation.ColumnResize)
                {
                    string col = snap.Action.Column;
                    var prior = m_History.History
                        .Select(s => s.Action)
                        .LastOrDefault(a => a != null
                            && a.Kind == DataGridControlHistory.FilterAction.Operation.ColumnResize
                            && a.Column == col);
                    widths.Add((col, prior?.Width ?? defaultColumnWidth));
                }
                else if (wasTranspose)
                {
                    // Restore widths the user set on the now-restored columns from
                    // the most recent ColumnResize entry per column in remaining
                    // history. Columns never resized fall back to default 100.
                    foreach (var name in ColumnNames)
                    {
                        var prior = m_History.History
                            .Select(s => s.Action)
                            .LastOrDefault(a => a != null
                                && a.Kind == DataGridControlHistory.FilterAction.Operation.ColumnResize
                                && a.Column == name);
                        widths.Add((name, prior?.Width ?? defaultColumnWidth));
                    }
                }
                DataGrid?.UpdateButtons(this, EventArgs.Empty);
                return widths;
            }

            public IEnumerable<(string Name, int Width)>? Redo()
            {
                if (m_RedoStack.History.Count == 0)
                {
                    OnLog?.Invoke(new CsvLog.Entry("Redo: stack empty, nothing to redo", CsvLog.Priority.Debug));
                    return null;
                }
                int top = m_RedoStack.History.Count - 1;
                var snap = m_RedoStack.History[top];
                m_RedoStack.History.RemoveAt(top);
                if (snap.Action == null)
                {
                    OnLog?.Invoke(new CsvLog.Entry("Redo: popped snapshot with null action, skipping", CsvLog.Priority.Debug));
                    DataGrid?.UpdateButtons(this, EventArgs.Empty);
                    return new List<(string, int)>();
                }
                OnLog?.Invoke(new CsvLog.Entry(
                    $"Redo: dispatching {Describe(snap.Action)} | redo depth now: {m_RedoStack.History.Count}",
                    CsvLog.Priority.Debug));

                var widthsByColumn = new Dictionary<string, int>();
                m_SuppressRedoClear = true;
                try
                {
                    DispatchAction(snap.Action, widthsByColumn);
                }
                finally
                {
                    m_SuppressRedoClear = false;
                }
                DataGrid?.UpdateButtons(this, EventArgs.Empty);
                return widthsByColumn.Select(kv => (kv.Key, kv.Value)).ToList();
            }

            private void Refilter()
            {
                Profile.Enter();

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

            public void PushSnapshot(DataGridControlHistory.FilterAction action)
            {
                var snap = MakeSnapshot(cacheRefs: true);
                snap.Action = action;
                m_History.History.Add(snap);
                if (!m_SuppressRedoClear)
                {
                    m_RedoStack.History.Clear();
                }
                OnLog?.Invoke(new CsvLog.Entry(
                    $"PushSnapshot: {Describe(action)} | buffer: {DescribeHistory()}",
                    CsvLog.Priority.Debug));
                DataGrid?.UpdateButtons(this, EventArgs.Empty);
            }

            private static string Describe(DataGridControlHistory.FilterAction? a)
            {
                if (a == null) return "<null>";
                var parts = new List<string> { a.Kind.ToString() };
                if (!string.IsNullOrEmpty(a.Column)) parts.Add($"col={a.Column}");
                if (a.Kind == DataGridControlHistory.FilterAction.Operation.ApplySort)
                {
                    parts.Add($"dir={(a.Direction == ListSortDirection.Ascending ? "asc" : "desc")}");
                }
                if (!string.IsNullOrEmpty(a.Pattern)) parts.Add($"pat={a.Pattern}");
                if (!string.IsNullOrEmpty(a.AnchorValue)) parts.Add($"anchor={a.AnchorValue}");
                if (a.Row >= 0) parts.Add($"row={a.Row}");
                if (a.Width >= 0) parts.Add($"w={a.Width}");
                if (a.Values.Count > 0) parts.Add($"vals=[{string.Join(",", a.Values)}]");
                return string.Join(" ", parts);
            }

            private string DescribeHistory()
            {
                if (m_History.History.Count == 0) return "<empty>";
                return string.Join(" | ", m_History.History
                    .Select((s, i) => $"[{i}]{Describe(s.Action)}"));
            }

            private DataGridControlHistory.Snapshot MakeSnapshot(bool cacheRefs)
            {
                return new DataGridControlHistory.Snapshot
                {
                    VisibleRows = FilteredData.Select(r => r.Index).ToList(),
                    VisibleRowRefs = cacheRefs ? FilteredData.ToList() : null
                };
            }

            public void ShowAll()
            {
                PushSnapshot(new DataGridControlHistory.FilterAction { Kind = DataGridControlHistory.FilterAction.Operation.ShowAll });
                foreach (var v in UnfilteredData)
                {
                    v.Visible = true;
                }
                Refilter();
            }

            public IEnumerable<int> RowsWithSelection()
            {
                if (DataGrid == null) return Array.Empty<int>();
                var rows = DataGrid.Grid.SelectedRows.Cast<DataGridViewRow>().Select(x => ((BoundDataRow?)(x.DataBoundItem))?.Index);
                var cells = DataGrid.Grid.SelectedCells.Cast<DataGridViewCell>().Select(x => ((BoundDataRow?)(x.OwningRow.DataBoundItem))?.Index);
                var union = rows.Union(cells).Where(x => x != null).Select(y => (int)(y ?? 0));
                return union.ToArray();
            }

            public IEnumerable<int> ColsWithSelection()
            {
                if (DataGrid == null) return Array.Empty<int>();
                var cols = DataGrid.Grid.SelectedColumns.Cast<DataGridViewColumn>().Select(x => x.Index);
                var cells = DataGrid.Grid.SelectedCells.Cast<DataGridViewCell>().Select(x => x.OwningColumn.Index);
                return cols.Union(cells).ToArray();
            }

            public string?[]? GetSelectedRowsOfColumn(string column)
            {
                int colIndex = ColumnNames.IndexOf(column);
                return colIndex == -1
                    ? null
                    : RowsWithSelection().Select(y => IndexToRow[y].Column(colIndex)).ToArray();
            }

            public string?[]? GetColumn(string v)
            {
                m_Profile.Enter(v);
                try
                {
                    int colIndex = ColumnNames.IndexOf(v);
                    return colIndex == -1 ? null : FilteredData.Select(x => x.Column(colIndex)).ToArray();
                }
                finally
                {
                    m_Profile.Exit(v);
                }
            }

            public Dictionary<string, string?>? GetSelectedRow()
            {
                int row = RowsWithSelection().FirstOrDefault();
                if (row != -1)
                {
                    string?[] rowData = IndexToRow[row].Strings;
                    return (Dictionary<string, string?>?)ColumnNames
                        .Select((columnName, index) => new { columnName, value = index < rowData.Length ? rowData[index] : null })
                        .ToDictionary(item => item.columnName, item => item.value);
                }
                else
                {
                    return new Dictionary<string, string?>();
                }
            }

            public double[]? GetColumnDouble(string v)
            {
                int colIndex = ColumnNames.IndexOf(v);
                return colIndex == -1 ? null : FilteredData.Select(x => x.ColumnDouble(colIndex)).ToArray();
            }


            public string?[]? GetColumn(int colIndex)
            {
                return FilteredData.Select(x => x.Column(colIndex)).ToArray();
            }

            public string[]? GetSelectedColumnNames()
            {
                return ColsWithSelection().Select(x => ColumnNames[x]).ToArray();
            }

            public void HideRows(IEnumerable<int> selectedRows)
            {
                foreach (var v in selectedRows)
                {
                    IndexToRow[v].Visible = false;
                }
                Refilter();
            }

            // Hide rows displayed above the given row (by original Index)
            public void HideRowsAbove(int row)
            {
                if (FilteredData == null) return;
                int displayPos = FilteredData.IndexOf(IndexToRow[row]);
                if (displayPos < 0) return;
                HideRowsAboveAt(displayPos);
            }

            public void HideRowsBelow(int row)
            {
                if (FilteredData == null) return;
                int displayPos = FilteredData.IndexOf(IndexToRow[row]);
                if (displayPos < 0) return;
                HideRowsBelowAt(displayPos);
            }

            private void HideRowsAboveAt(int displayPos)
            {
                if (FilteredData == null || displayPos < 0 || displayPos >= FilteredData.Count) return;
                var (column, anchorValue) = AnchorAt(displayPos);
                PushSnapshot(new DataGridControlHistory.FilterAction
                {
                    Kind = DataGridControlHistory.FilterAction.Operation.HideRowsAbove,
                    Column = column,
                    AnchorValue = anchorValue,
                    Row = displayPos
                });
                for (int loop = 0; loop < displayPos; loop++)
                {
                    FilteredData[loop].Visible = false;
                }
                Refilter();
            }

            private void HideRowsBelowAt(int displayPos)
            {
                if (FilteredData == null || displayPos < 0 || displayPos >= FilteredData.Count) return;
                var (column, anchorValue) = AnchorAt(displayPos);
                PushSnapshot(new DataGridControlHistory.FilterAction
                {
                    Kind = DataGridControlHistory.FilterAction.Operation.HideRowsBelow,
                    Column = column,
                    AnchorValue = anchorValue,
                    Row = displayPos
                });
                int count = FilteredData.Count;
                for (int loop = displayPos + 1; loop < count; loop++)
                {
                    FilteredData[loop].Visible = false;
                }
                Refilter();
            }

            private (string column, string value) AnchorAt(int displayPos)
            {
                var keys = CurrentSortKeys();
                if (keys.Count == 0) return ("", "");
                string col = keys[^1].columnName;
                int idx = ColumnNames.IndexOf(col);
                if (idx < 0) return ("", "");
                return (col, FilteredData[displayPos].Column(idx) ?? "");
            }

            private int ResolveReplayPosition(string sortColumn, string anchorValue, int savedRow)
            {
                if (FilteredData == null || FilteredData.Count == 0) return -1;
                int colIdx = string.IsNullOrEmpty(sortColumn) ? -1 : ColumnNames.IndexOf(sortColumn);

                if (colIdx >= 0
                    && savedRow >= 0 && savedRow < FilteredData.Count
                    && string.Equals(FilteredData[savedRow].Column(colIdx), anchorValue, StringComparison.OrdinalIgnoreCase))
                {
                    return savedRow;
                }
                if (colIdx >= 0)
                {
                    return FindNearestRowByColumnValue(colIdx, anchorValue);
                }
                if (savedRow >= 0 && savedRow < FilteredData.Count)
                {
                    return savedRow;
                }
                return -1;
            }

            private int FindNearestRowByColumnValue(int colIdx, string anchorValue)
            {
                if (FilteredData == null || FilteredData.Count == 0) return -1;
                if (double.TryParse(anchorValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double anchorNum))
                {
                    int bestIdx = 0;
                    double bestDist = double.MaxValue;
                    for (int loop = 0; loop < FilteredData.Count; loop++)
                    {
                        double dist = Math.Abs(FilteredData[loop].ColumnDouble(colIdx) - anchorNum);
                        if (dist < bestDist) { bestDist = dist; bestIdx = loop; }
                    }
                    return bestIdx;
                }
                // Exact (case-insensitive) match wins outright
                for (int loop = 0; loop < FilteredData.Count; loop++)
                {
                    if (string.Equals(FilteredData[loop].Column(colIdx), anchorValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return loop;
                    }
                }
                // First row that would slot at-or-after the anchor in ascending order
                for (int loop = 0; loop < FilteredData.Count; loop++)
                {
                    if (string.Compare(FilteredData[loop].Column(colIdx) ?? "", anchorValue, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return loop;
                    }
                }
                return FilteredData.Count - 1;
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

            // Predicate-based hide. Does NOT push a snapshot
            private void HideRowsIf(Func<BoundDataRow, bool> predicate)
            {
                if (FilteredData == null) return;
                foreach (var v in FilteredData.Where(predicate))
                {
                    v.Visible = false;
                }
                Refilter();
            }

            public void Decimate(int stride)
            {
                if (stride < 2) return;
                PushSnapshot(new DataGridControlHistory.FilterAction
                {
                    Kind = DataGridControlHistory.FilterAction.Operation.Decimate,
                    Stride = stride
                });
                int counter = 0;
                HideRowsIf(_ => counter++ % stride != 0);
            }

            // First click pivots the visible view: existing column headers become a
            // single "headers" column, and each visible row becomes a "row 1", "row 2"
            // ... column. Second click - detected by the same shape - reverses.
            // Undo restores the pre-transpose UnfilteredData/ColumnNames so that
            // older snapshots' row references stay valid after walking back.
            public void Transpose()
            {
                var preUnfiltered = UnfilteredData;
                var preColumnNames = ColumnNames;
                PushSnapshot(new DataGridControlHistory.FilterAction
                {
                    Kind = DataGridControlHistory.FilterAction.Operation.Transpose
                });
                var pushed = m_History.History[^1];
                pushed.PreTransposeUnfiltered = preUnfiltered;
                pushed.PreTransposeColumnNames = preColumnNames;
                DoTransposeInPlace();
            }

            private void UndoTranspose(DataGridControlHistory.Snapshot snap)
            {
                if (snap.PreTransposeUnfiltered != null && snap.PreTransposeColumnNames != null)
                {
                    UnfilteredData = snap.PreTransposeUnfiltered;
                    ColumnNames = snap.PreTransposeColumnNames;
                    IndexToRow = UnfilteredData.OrderBy(r => r.Index).ToList();
                    RebuildGridColumns();
                    ApplyVisible(snap);
                }
                else
                {
                    // No stash (e.g. pre-stash snapshot); fall back to re-executing.
                    DoTransposeInPlace();
                }
            }

            private bool IsTransposedShape()
            {
                if (ColumnNames.Count < 1) return false;
                if (ColumnNames[0] != "headers") return false;
                for (int loop = 1; loop < ColumnNames.Count; loop++)
                {
                    if (ColumnNames[loop] != $"row {loop}") return false;
                }
                return true;
            }

            private void DoTransposeInPlace()
            {
                bool reverse = IsTransposedShape();
                int oldRows = FilteredData.Count;
                int oldCols = ColumnNames.Count;

                var newColumnNames = new List<string>();
                var newRows = new List<BoundDataRow>();

                if (reverse)
                {
                    // "headers" column values become new column names; each "row N"
                    // column reflows into a new row.
                    for (int loop = 0; loop < oldRows; loop++)
                    {
                        newColumnNames.Add(FilteredData[loop].Column(0) ?? "");
                    }
                    int newRowCount = oldCols - 1;
                    for (int rowIdx = 0; rowIdx < newRowCount; rowIdx++)
                    {
                        int sourceCol = rowIdx + 1;
                        string?[] data = new string?[oldRows];
                        for (int colIdx = 0; colIdx < oldRows; colIdx++)
                        {
                            data[colIdx] = FilteredData[colIdx].Column(sourceCol);
                        }
                        newRows.Add(new BoundDataRowString(rowIdx, data));
                    }
                }
                else
                {
                    newColumnNames.Add("headers");
                    for (int loop = 0; loop < oldRows; loop++)
                    {
                        newColumnNames.Add($"row {loop + 1}");
                    }
                    for (int colIdx = 0; colIdx < oldCols; colIdx++)
                    {
                        string?[] data = new string?[oldRows + 1];
                        data[0] = ColumnNames[colIdx];
                        for (int rowIdx = 0; rowIdx < oldRows; rowIdx++)
                        {
                            data[rowIdx + 1] = FilteredData[rowIdx].Column(colIdx);
                        }
                        newRows.Add(new BoundDataRowString(colIdx, data));
                    }
                }

                ColumnNames = newColumnNames;
                UnfilteredData = newRows;
                IndexToRow = new List<BoundDataRow>(newRows);
                foreach (var v in UnfilteredData)
                {
                    v.Visible = true;
                }

                RebuildGridColumns();
                Refilter();
            }

            public void HideNotFirstUnique(string column)
            {
                PushSnapshot(new DataGridControlHistory.FilterAction
                {
                    Kind = DataGridControlHistory.FilterAction.Operation.HideNotFirstUnique,
                    Column = column
                });
                // keep the first, hide the rest
                int colIndex = ColumnNames.IndexOf(column);
                var seen = new HashSet<string?>();
                HideRowsIf(x =>
                {
                    var value = x.Column(colIndex);
                    if (seen.Contains(value))
                    {
                        return true;
                    }
                    seen.Add(value);
                    return false;
                });
            }

            public void HideRowsMatching(string column, IEnumerable<string?> rows)
            {
                List<string?> strings = rows.Select(x => x?.ToLower()).ToList();
                PushSnapshot(new DataGridControlHistory.FilterAction
                {
                    Kind = DataGridControlHistory.FilterAction.Operation.HideRowsMatching,
                    Column = column,
                    Values = strings.Select(s => s ?? "").ToList()
                });
                // case insensitive
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => strings.Contains(x.Column(colIndex)?.ToLower()));
            }

            public void HideRowsNotMatching(string column, IEnumerable<string?> rows)
            {
                List<string?> strings = rows.Select(x => x?.ToLower()).ToList();
                PushSnapshot(new DataGridControlHistory.FilterAction
                {
                    Kind = DataGridControlHistory.FilterAction.Operation.HideRowsNotMatching,
                    Column = column,
                    Values = strings.Select(s => s ?? "").ToList()
                });
                // case insensitive
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => !strings.Contains(x.Column(colIndex)?.ToLower()));
            }

            public void ShowRowsMatchingRegex(string regex, string column)
            {
                PushSnapshot(new DataGridControlHistory.FilterAction
                {
                    Kind = DataGridControlHistory.FilterAction.Operation.ShowRowsMatchingRegex,
                    Column = column,
                    Pattern = regex
                });
                Regex match = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => !match.IsMatch(x.Column(colIndex) ?? "null"));
            }

            public void HideRowsMatchingRegex(string regex, string column)
            {
                PushSnapshot(new DataGridControlHistory.FilterAction
                {
                    Kind = DataGridControlHistory.FilterAction.Operation.HideRowsMatchingRegex,
                    Column = column,
                    Pattern = regex
                });
                Regex match = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => match.IsMatch(x.Column(colIndex) ?? "null"));
            }

            object? IList.this[int index]
            {
                get { return FilteredData == null ? null : FilteredData[index]; }
                set { throw new NotImplementedException(); }
            }


            void IBindingList.ApplySort(PropertyDescriptor property, ListSortDirection direction)
            {
                // The `direction` argument is intentionally ignored here. DataGridView appears to
                // always pass Ascending on header clicks against this custom IBindingList (its
                // sortedColumn/sortOrder tracking does not interact with us as expected), so we
                // toggle relative to our own m_History to give the user alternating click UX.
                // Programmatic callers that need an explicit direction should call SortByColumn
                // directly instead of going through DataGridView.Sort / IBindingList.ApplySort.
                Profile.Enter();
                if (UnfilteredData.Count != 0)
                {
                    var prevCursor = Cursor.Current;
                    try
                    {
                        int colIndex = int.Parse(property.Name.Replace("col", ""));
                        string colName = ColumnNames[colIndex];
                        var existing = CurrentSortKeys().FirstOrDefault(k => k.columnName == colName);
                        var newDir = existing == default
                            ? ListSortDirection.Ascending
                            : (existing.direction == ListSortDirection.Ascending
                                ? ListSortDirection.Descending : ListSortDirection.Ascending);
                        Cursor.Current = Cursors.WaitCursor;
                        CurrentSortProperty = property;
                        SortByColumn(colName, newDir);
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
            }

            private void ApplySortDirect()
            {
                var keys = CurrentSortKeys();
                if (keys.Count == 0 || UnfilteredData.Count == 0) return;

                var resolved = keys
                    .AsEnumerable()
                    .Reverse()
                    .Select(k => (idx: ColumnNames.IndexOf(k.columnName), k.direction))
                    .Where(k => k.idx >= 0)
                    .ToList();

                var temp = FilteredData?.ToList() ?? new List<BoundDataRow>();
                temp.ParallelSort((x, y) =>
                {
                    foreach (var key in resolved)
                    {
                        int cmp = x.GetSortComparer(key.idx, key.direction).Compare(x, y);
                        if (cmp != 0) return cmp;
                    }
                    return x.ResortIndex.CompareTo(y.ResortIndex);
                });
                FilteredData = temp;

                OnLog?.Invoke(new CsvLog.Entry(Profile.ToString(), CsvLog.Priority.Debug));
                ReshowFiltered();
            }

            private void UpdateSortGlyphs()
            {
                if (DataGrid == null) return;
                DataGridView grid = DataGrid.Grid;
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    col.HeaderCell.SortGlyphDirection = SortOrder.None;
                }
                var keys = CurrentSortKeys();
                if (keys.Count > 0)
                {
                    // Newest click is primary, so the glyph goes on keys[^1].
                    var primary = keys[^1];
                    int idx = ColumnNames.IndexOf(primary.columnName);
                    if (idx >= 0 && idx < grid.Columns.Count)
                    {
                        grid.Columns[idx].HeaderCell.SortGlyphDirection =
                            primary.direction == ListSortDirection.Ascending
                                ? SortOrder.Ascending : SortOrder.Descending;
                    }
                }
            }

            public DataGridControlHistory SaveBoundState()
            {
                var state = new DataGridControlHistory();
                state.History.AddRange(m_History.History);
                state.History.Add(MakeSnapshot(cacheRefs: false));
                return state;
            }

            public IEnumerable<(string Name, int Width)> RestoreBoundState(DataGridControlHistory state)
            {
                if (state.History.Count == 0)
                {
                    return Enumerable.Empty<(string, int)>();
                }

                ResetForReplay();

                var widthsByColumn = new Dictionary<string, int>();
                foreach (var snap in state.History)
                {
                    if (snap.Action == null) continue; // synthetic "current" tail entry
                    DispatchAction(snap.Action, widthsByColumn);
                }
                return widthsByColumn.Select(kv => (kv.Key, kv.Value)).ToList();
            }

            private void ResetForReplay()
            {
                m_History.History.Clear();
                m_RedoStack.History.Clear();
                foreach (var v in UnfilteredData)
                {
                    v.Visible = true;
                }
                Refilter();
                UpdateSortGlyphs();
                DataGrid?.UpdateButtons(this, EventArgs.Empty);
            }

            private void ApplyVisible(DataGridControlHistory.Snapshot snap)
            {
                if (snap.VisibleRowRefs == null)
                {
                    var visibleSet = new HashSet<int>(snap.VisibleRows);
                    var displayOrder = snap.VisibleRows
                        .Select((id, pos) => (id, pos))
                        .ToDictionary(t => t.id, t => t.pos);
                    snap.VisibleRowRefs = UnfilteredData
                        .Where(r => visibleSet.Contains(r.Index))
                        .OrderBy(r => displayOrder.TryGetValue(r.Index, out int p) ? p : int.MaxValue)
                        .ToList();
                }
                FilteredData = snap.VisibleRowRefs;
                foreach (var v in UnfilteredData)
                {
                    v.Visible = false;
                }
                foreach (var v in FilteredData)
                {
                    v.Visible = true;
                }
            }

            private void DispatchAction(
                DataGridControlHistory.FilterAction action,
                Dictionary<string, int> widthsByColumn)
            {
                try
                {
                    switch (action.Kind)
                    {
                        case DataGridControlHistory.FilterAction.Operation.ShowAll:
                            ShowAll();
                            break;
                        case DataGridControlHistory.FilterAction.Operation.HideRowsMatching:
                            HideRowsMatching(action.Column, action.Values);
                            break;
                        case DataGridControlHistory.FilterAction.Operation.HideRowsNotMatching:
                            HideRowsNotMatching(action.Column, action.Values);
                            break;
                        case DataGridControlHistory.FilterAction.Operation.ShowRowsMatchingRegex:
                            ShowRowsMatchingRegex(action.Pattern, action.Column);
                            break;
                        case DataGridControlHistory.FilterAction.Operation.HideRowsMatchingRegex:
                            HideRowsMatchingRegex(action.Pattern, action.Column);
                            break;
                        case DataGridControlHistory.FilterAction.Operation.HideNotFirstUnique:
                            HideNotFirstUnique(action.Column);
                            break;
                        case DataGridControlHistory.FilterAction.Operation.HideRowsAbove:
                            {
                                int pos = ResolveReplayPosition(action.Column, action.AnchorValue, action.Row);
                                if (pos < 0)
                                {
                                    OnLog?.Invoke(new CsvLog.Entry($"replay HideRowsAbove: cannot resolve anchor", CsvLog.Priority.Warn));
                                }
                                else
                                {
                                    HideRowsAboveAt(pos);
                                }
                                break;
                            }
                        case DataGridControlHistory.FilterAction.Operation.HideRowsBelow:
                            {
                                int pos = ResolveReplayPosition(action.Column, action.AnchorValue, action.Row);
                                if (pos < 0)
                                {
                                    OnLog?.Invoke(new CsvLog.Entry($"replay HideRowsBelow: cannot resolve anchor", CsvLog.Priority.Warn));
                                }
                                else
                                {
                                    HideRowsBelowAt(pos);
                                }
                                break;
                            }
                        case DataGridControlHistory.FilterAction.Operation.ApplySort:
                            SortByColumn(action.Column, action.Direction);
                            break;
                        case DataGridControlHistory.FilterAction.Operation.ColumnResize:
                            widthsByColumn[action.Column] = action.Width;
                            PushSnapshot(action);
                            break;
                        case DataGridControlHistory.FilterAction.Operation.Decimate:
                            Decimate(action.Stride);
                            break;
                        case DataGridControlHistory.FilterAction.Operation.Transpose:
                            Transpose();
                            break;
                        default:
                            OnLog?.Invoke(new CsvLog.Entry($"replay: unknown action {action.Kind}", CsvLog.Priority.Warn));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke(new CsvLog.Entry($"replay {action.Kind} failed: {ex.Message}", CsvLog.Priority.Warn));
                }
            }

            public void SortByColumn(string column, ListSortDirection direction = ListSortDirection.Ascending)
            {
                int colIndex = ColumnNames.IndexOf(column);
                if (colIndex < 0)
                {
                    OnLog?.Invoke(new CsvLog.Entry($"SortByColumn: no column {column}", CsvLog.Priority.Warn));
                    return;
                }
                PushSnapshot(new DataGridControlHistory.FilterAction
                {
                    Kind = DataGridControlHistory.FilterAction.Operation.ApplySort,
                    Column = column,
                    Direction = direction
                });
                ApplySortDirect();
                UpdateSortGlyphs();
            }

            internal void SaveToCsv(string fileName)
            {
                //fixme: save selection?
                CSVSave.SaveRows(fileName, ColumnNames, FilteredData.Select(x => x.Strings), ",");
            }

            internal SelectedCellsData SelectedCellsToClipboardFormats(bool numericGrid)
            {
                if (DataGrid == null) return default;
                DataGridView grid = DataGrid.Grid;
                var cells = grid.SelectedCells.Cast<DataGridViewCell>().ToArray();

                var colFlags = new bool[grid.ColumnCount];
                var rowFlags = new bool[grid.RowCount];
                foreach (var cell in cells)
                {
                    colFlags[cell.ColumnIndex] = true;
                    rowFlags[cell.RowIndex] = true;
                }

                var colIndex = new int[grid.ColumnCount];
                int resultCols = 0;
                for (int loop = 0; loop < grid.ColumnCount; loop++)
                {
                    if (colFlags[loop])
                    {
                        colIndex[loop] = resultCols++;
                    }
                }

                var rowIndex = new int[grid.RowCount];
                int resultRows = 0;
                for (int loop = 0; loop < grid.RowCount; loop++)
                {
                    if (rowFlags[loop])
                    {
                        rowIndex[loop] = resultRows++;
                    }
                }

                string[] headers = colFlags.Select((x, i) => new { x, i }).Where(x => x.x).Select(x => ColumnNames[x.i]).ToArray();

                StringBuilder sb = new StringBuilder();
                if (resultRows > 1)
                {
                    sb.AppendLine(string.Join("\t", headers));
                }

                string?[,] strings = new string[resultRows, resultCols];
                foreach (var v in cells)
                {
                    strings[rowIndex[v.RowIndex], colIndex[v.ColumnIndex]] =
                        FilteredData[v.RowIndex].Strings[v.ColumnIndex];
                }

                for (int row = 0; row < resultRows; row++)
                {
                    if (row != 0)
                    {
                        sb.AppendLine();
                    }
                    for (int col = 0; col < resultCols; col++)
                    {
                        if (col != 0)
                        {
                            sb.Append("\t");
                        }
                        sb.Append(strings[row, col]);
                    }
                }
                string tsv = sb.ToString();

                string csv = StringArrayToCsv(headers, strings);
                string html = StringArrayToHtml(headers, strings);

                return new SelectedCellsData(headers, strings, csv, tsv, html, WrapHtml(html));
            }

            private string WrapHtml(string html)
            { // excel requires the html section to be wrapped like this
                const string header = // {0000:D10} is 10 chars long
@"Version:1.0
StartHTML:{0000:D10}
EndHTML:{0001:D10}
StartFragment:{0002:D10}
EndFragment:{0003:D10}
";
                string pre = "<html><body>\r\n<!--StartFragment-->";
                string post = "<!--EndFragment-->\r\n</body></html>";

                string htmlFragment = pre + html + post;

                int startHTML = header.Length;
                int startFragment = startHTML + pre.Length;
                int endFragment = startFragment + html.Length;
                int endHTML = startFragment + htmlFragment.Length;

                return string.Format(
                    header,
                    startHTML,
                    startHTML + htmlFragment.Length,
                    startFragment,
                    endFragment
                ) + htmlFragment;
            }

            private static string StringArrayToCsv(string[] headers, string?[,] strings)
            {
                int rows = strings.GetLength(0);
                int cols = strings.GetLength(1);
                IEnumerable<string?> getRow(int row) => Enumerable.Range(0, cols).Select(col => strings[row, col]);
                var csvRows = Enumerable.Range(0, rows).Select(row => CSVSave.RowToCsvText(getRow(row)));
                string csv = @$"{CSVSave.RowToCsvText(headers)}
{string.Join(@"
", csvRows)}";
                return csv;
            }

            private static string StringArrayToHtml(string[] headers, string?[,] strings)
            {
                int rows = strings.GetLength(0);
                int cols = strings.GetLength(1);

                string Escape(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
                IEnumerable<string?> getRow(int row) => Enumerable.Range(0, cols).Select(col => strings[row, col]);

                var result = new StringBuilder();
                result.AppendLine("<table border=\"1\">");
                result.AppendLine("<thead><tr>");
                headers.ForEach(header => result.Append("<th>").Append(Escape(header)).AppendLine("</th>"));
                result.AppendLine("</tr></thead>");
                result.AppendLine("<tbody>");
                for (int row = 0; row < rows; row++)
                {
                    result.AppendLine("<tr>");
                    getRow(row).ForEach(cell => result.Append("<td>").Append(Escape(cell)).AppendLine("</td>"));
                    result.AppendLine("</tr>");
                }
                result.AppendLine("</tbody>");
                result.AppendLine("</table>");
                return result.ToString();
            }
        }
    }
}
