using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            internal bool Visible = true;
            internal bool TempFlag = false;
            internal int Index;
            internal int ResortIndex;
            internal Color?[]? Colours = null;
            internal StringDiff.Diffs?[]? Diffs = null;

            public BoundDataRow(int index)
            {
                Index = index;
            }

            public abstract string?[] Strings { get; }
            public abstract string? Column(int index);
            public abstract double ColumnDouble(int colIndex);
            public abstract int Count { get; }
            public abstract void Set(int index, string? to);
            public abstract void InsertColumnValue(int index, string? value);
            public abstract void RemoveColumn(int index);
            public abstract void MoveColumnValue(int from, int to);

            public void AppendColumnValue(string? value)
            {
                InsertColumnValue(Count, value);
            }

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

            public override void InsertColumnValue(int index, string? value)
            {
                int insertAt = Math.Clamp(index, 0, Data.Length);
                var grown = new string?[Data.Length + 1];
                Array.Copy(Data, 0, grown, 0, insertAt);
                grown[insertAt] = value;
                Array.Copy(Data, insertAt, grown, insertAt + 1, Data.Length - insertAt);
                Data = grown;
                if (Colours != null)
                {
                    var grownColours = new Color?[Colours.Length + 1];
                    Array.Copy(Colours, 0, grownColours, 0, insertAt);
                    Array.Copy(Colours, insertAt, grownColours, insertAt + 1, Colours.Length - insertAt);
                    Colours = grownColours;
                }
                if (Diffs != null)
                {
                    var grownDiffs = new StringDiff.Diffs?[Diffs.Length + 1];
                    Array.Copy(Diffs, 0, grownDiffs, 0, insertAt);
                    Array.Copy(Diffs, insertAt, grownDiffs, insertAt + 1, Diffs.Length - insertAt);
                    Diffs = grownDiffs;
                }
            }

            public override void RemoveColumn(int index)
            {
                if (index < 0 || index >= Data.Length) return;
                var shrunk = new string?[Data.Length - 1];
                Array.Copy(Data, 0, shrunk, 0, index);
                Array.Copy(Data, index + 1, shrunk, index, Data.Length - index - 1);
                Data = shrunk;
                if (Colours != null && index < Colours.Length)
                {
                    var shrunkColours = new Color?[Colours.Length - 1];
                    Array.Copy(Colours, 0, shrunkColours, 0, index);
                    Array.Copy(Colours, index + 1, shrunkColours, index, Colours.Length - index - 1);
                    Colours = shrunkColours;
                }
                if (Diffs != null && index < Diffs.Length)
                {
                    var shrunkDiffs = new StringDiff.Diffs?[Diffs.Length - 1];
                    Array.Copy(Diffs, 0, shrunkDiffs, 0, index);
                    Array.Copy(Diffs, index + 1, shrunkDiffs, index, Diffs.Length - index - 1);
                    Diffs = shrunkDiffs;
                }
            }

            public override void MoveColumnValue(int from, int to)
            {
                Data.Move(from, to);
                Colours?.Move(from, to);
                Diffs?.Move(from, to);
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

            public override void InsertColumnValue(int index, string? value)
            {
                int insertAt = Math.Clamp(index, 0, Data.Length);
                var grown = new double[Data.Length + 1];
                Array.Copy(Data, 0, grown, 0, insertAt);
                grown[insertAt] = value?.ToDouble(0) ?? 0;
                Array.Copy(Data, insertAt, grown, insertAt + 1, Data.Length - insertAt);
                Data = grown;
                if (Colours != null)
                {
                    var grownColours = new Color?[Colours.Length + 1];
                    Array.Copy(Colours, 0, grownColours, 0, insertAt);
                    Array.Copy(Colours, insertAt, grownColours, insertAt + 1, Colours.Length - insertAt);
                    Colours = grownColours;
                }
                if (Diffs != null)
                {
                    var grownDiffs = new StringDiff.Diffs?[Diffs.Length + 1];
                    Array.Copy(Diffs, 0, grownDiffs, 0, insertAt);
                    Array.Copy(Diffs, insertAt, grownDiffs, insertAt + 1, Diffs.Length - insertAt);
                    Diffs = grownDiffs;
                }
            }

            public override void RemoveColumn(int index)
            {
                if (index < 0 || index >= Data.Length) return;
                var shrunk = new double[Data.Length - 1];
                Array.Copy(Data, 0, shrunk, 0, index);
                Array.Copy(Data, index + 1, shrunk, index, Data.Length - index - 1);
                Data = shrunk;
                if (Colours != null && index < Colours.Length)
                {
                    var shrunkColours = new Color?[Colours.Length - 1];
                    Array.Copy(Colours, 0, shrunkColours, 0, index);
                    Array.Copy(Colours, index + 1, shrunkColours, index, Colours.Length - index - 1);
                    Colours = shrunkColours;
                }
                if (Diffs != null && index < Diffs.Length)
                {
                    var shrunkDiffs = new StringDiff.Diffs?[Diffs.Length - 1];
                    Array.Copy(Diffs, 0, shrunkDiffs, 0, index);
                    Array.Copy(Diffs, index + 1, shrunkDiffs, index, Diffs.Length - index - 1);
                    Diffs = shrunkDiffs;
                }
            }

            public override void MoveColumnValue(int from, int to)
            {
                Data.Move(from, to);
                Colours?.Move(from, to);
                Diffs?.Move(from, to);
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
                    if (snap.Kind == DataGridControlHistory.Snapshot.Operation.ApplySort)
                    {
                        string col = snap.Column;
                        result.RemoveAll(k => k.columnName == col);
                        result.Add((col, snap.Direction));
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

            public void AddColumn(string header, IEnumerable<string?> values)
            {
                using var rowIter = UnfilteredData.GetEnumerator();
                using var valIter = values.GetEnumerator();
                while (rowIter.MoveNext())
                {
                    string? value = valIter.MoveNext() ? valIter.Current : null;
                    rowIter.Current.AppendColumnValue(value);
                }
                ColumnNames.Add(header);
                RebuildGridColumns();
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.Reset, 0));
            }

            public void InsertColumn(string header, int index, IEnumerable<string?> values)
            {
                using var rowIter = UnfilteredData.GetEnumerator();
                using var valIter = values.GetEnumerator();
                while (rowIter.MoveNext())
                {
                    string? value = valIter.MoveNext() ? valIter.Current : null;
                    rowIter.Current.InsertColumnValue(index, value);
                }
                ColumnNames.Insert(index, header);
                RebuildGridColumns();
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.Reset, 0));
            }

            public bool RemoveColumn(string header)
            {
                int idx = ColumnNames.IndexOf(header);
                if (idx < 0) return false;
                foreach (var row in UnfilteredData)
                {
                    row.RemoveColumn(idx);
                }
                ColumnNames.RemoveAt(idx);
                RebuildGridColumns();
                ListChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.Reset, 0));
                return true;
            }

            // Move column so its new left-neighbour is newAfter (or leftmost if newAfter is empty)
            public void MoveColumn(string column, string newAfter)
            {
                int from = ColumnNames.IndexOf(column);
                if (from < 0) return;
                int to = ComputeMoveTarget(from, newAfter);
                if (to < 0 || from == to) return;
                string oldAfter = from == 0 ? "" : ColumnNames[from - 1];
                PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.MoveColumn)
                {
                    Column = column,
                    FromAfterColumn = oldAfter,
                    ToAfterColumn = newAfter
                });
                DoMove(from, to);
            }

            // Non-snapshotting reverse-move used by Undo.
            private void MoveColumnAfter(string column, string newAfter)
            {
                int from = ColumnNames.IndexOf(column);
                if (from < 0) return;
                int to = ComputeMoveTarget(from, newAfter);
                if (to < 0 || from == to) return;
                DoMove(from, to);
            }

            // Translate "after this name" anchor into a post-removal insertion index.
            // Returns -1 if newAfter is non-empty but doesn't exist, or refers to the
            // moved column itself.
            private int ComputeMoveTarget(int from, string newAfter)
            {
                if (newAfter == "") return 0;
                int afterIdx = ColumnNames.IndexOf(newAfter);
                if (afterIdx < 0 || afterIdx == from) return -1;
                return afterIdx < from ? afterIdx + 1 : afterIdx;
            }

            private void DoMove(int from, int to)
            {
                if (from == to) return;
                var name = ColumnNames[from];
                ColumnNames.RemoveAt(from);
                ColumnNames.Insert(to, name);
                foreach (var row in UnfilteredData)
                {
                    row.MoveColumnValue(from, to);
                }
                RebuildGridColumns();
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

                var savedBackColor = new Dictionary<string, Color>();
                var savedWidth = new Dictionary<string, int>();
                foreach (DataGridViewColumn c in grid.Columns)
                {
                    if (!c.DefaultCellStyle.BackColor.IsEmpty)
                    {
                        savedBackColor[c.Name] = c.DefaultCellStyle.BackColor;
                    }
                    savedWidth[c.Name] = c.Width;
                }

                grid.DataSource = null;
                grid.AutoGenerateColumns = false;
                grid.Columns.Clear();
                for (int loop = 0; loop < ColumnNames.Count; loop++)
                {
                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = ColumnNames[loop],
                        HeaderText = ColumnNames[loop],
                        DataPropertyName = $"col{loop}",
                        SortMode = DataGridViewColumnSortMode.Automatic
                    };
                    if (savedBackColor.TryGetValue(col.Name, out var bg))
                    {
                        col.DefaultCellStyle.BackColor = bg;
                    }
                    if (savedWidth.TryGetValue(col.Name, out var w))
                    {
                        col.Width = w;
                    }
                    grid.Columns.Add(col);
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
                    $"Undo: popped {Describe(snap)} | buffer: {DescribeHistory()} | redo depth: {m_RedoStack.History.Count}",
                    CsvLog.Priority.Debug));
                bool wasTranspose = snap.Kind == DataGridControlHistory.Snapshot.Operation.Transpose;
                if (wasTranspose)
                {
                    UndoTranspose(snap);
                }
                else
                {
                    ApplyVisible(snap);
                }
                for (int loop = snap.AddedColumns.Count - 1; loop >= 0; loop--)
                {
                    RemoveColumn(snap.AddedColumns[loop]);
                }
                if (snap.Kind == DataGridControlHistory.Snapshot.Operation.MoveColumn)
                {
                    MoveColumnAfter(snap.Column, snap.FromAfterColumn);
                }
                ReshowFiltered();
                UpdateSortGlyphs();

                const int defaultColumnWidth = 100;
                var widths = new List<(string Name, int Width)>();
                if (snap.Kind == DataGridControlHistory.Snapshot.Operation.ColumnResize)
                {
                    string col = snap.Column;
                    var prior = m_History.History
                        .LastOrDefault(s => s.Kind == DataGridControlHistory.Snapshot.Operation.ColumnResize
                            && s.Column == col);
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
                            .LastOrDefault(s => s.Kind == DataGridControlHistory.Snapshot.Operation.ColumnResize
                                && s.Column == name);
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
                OnLog?.Invoke(new CsvLog.Entry(
                    $"Redo: dispatching {Describe(snap)} | redo depth now: {m_RedoStack.History.Count}",
                    CsvLog.Priority.Debug));

                var widthsByColumn = new Dictionary<string, int>();
                m_SuppressRedoClear = true;
                try
                {
                    DispatchAction(snap, widthsByColumn);
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

            public void PushSnapshot(DataGridControlHistory.Snapshot snap)
            {
                snap.VisibleRows = FilteredData.Select(r => r.Index).ToList();
                snap.VisibleRowRefs = FilteredData.ToList();
                m_History.History.Add(snap);
                if (!m_SuppressRedoClear)
                {
                    m_RedoStack.History.Clear();
                }
                OnLog?.Invoke(new CsvLog.Entry(
                    $"PushSnapshot: {Describe(snap)} | buffer: {DescribeHistory()}",
                    CsvLog.Priority.Debug));
                DataGrid?.UpdateButtons(this, EventArgs.Empty);
            }

            private static string Describe(DataGridControlHistory.Snapshot s)
            {
                var parts = new List<string> { s.Kind.ToString() };
                if (!string.IsNullOrEmpty(s.Column)) parts.Add($"col={s.Column}");
                if (s.AddedColumns.Count > 0) parts.Add($"added=[{string.Join(",", s.AddedColumns)}]");
                if (s.Kind == DataGridControlHistory.Snapshot.Operation.ApplySort)
                {
                    parts.Add($"dir={(s.Direction == ListSortDirection.Ascending ? "asc" : "desc")}");
                }
                if (!string.IsNullOrEmpty(s.Pattern)) parts.Add($"pat={s.Pattern}");
                if (!string.IsNullOrEmpty(s.AnchorValue)) parts.Add($"anchor={s.AnchorValue}");
                if (s.Row >= 0) parts.Add($"row={s.Row}");
                if (s.Width >= 0) parts.Add($"w={s.Width}");
                if (s.Values.Count > 0) parts.Add($"vals=[{string.Join(",", s.Values)}]");
                return string.Join(" ", parts);
            }

            private string DescribeHistory()
            {
                if (m_History.History.Count == 0) return "<empty>";
                return string.Join(" | ", m_History.History
                    .Select((s, i) => $"[{i}]{Describe(s)}"));
            }

            public void ShowAll()
            {
                PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.ShowAll));
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
                PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.HideRowsAbove)
                {
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
                PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.HideRowsBelow)
                {
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

            // Does NOT push a snapshot
            public void HideRowsOtherThan(IEnumerable<int> selectedRows)
            {
                if (FilteredData == null) return;
                UnfilteredData.ForEach(x => x.TempFlag = false);
                selectedRows.ForEach(v => IndexToRow[v].TempFlag = true);
                FilteredData.Where(v => !v.TempFlag).ForEach(v => v.Visible = false);
                Refilter();
            }

            // Predicate-based hide. Does NOT push a snapshot
            private void HideRowsIf(Func<BoundDataRow, bool> predicate)
            {
                if (FilteredData == null) return;
                FilteredData.Where(predicate).ForEach(v => v.Visible = false);
                Refilter();
            }

            public void Decimate(int stride)
            {
                if (stride < 2) return;
                PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.Decimate)
                {
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
                PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.Transpose));
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
                PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.HideNotFirstUnique)
                {
                    Column = column
                });
                int colIndex = ColumnNames.IndexOf(column);
                if (colIndex < 0) return;

                // count occurrences across currently-visible rows before we hide anything
                var counts = new Dictionary<string, int>();
                foreach (var row in FilteredData)
                {
                    var value = row.Column(colIndex) ?? "";
                    counts.TryGetValue(value, out int c);
                    counts[value] = c + 1;
                }

                // keep the first, hide the rest
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

                // populate "<column> count" directly to the right of the uniqued column.
                // hidden rows carry their count too, so Undo restores a still-meaningful view.
                // If a column with that name already exists (repeated unique on the same
                // column), leave it alone - Undo of *this* action shouldn't drop a column
                // an earlier action installed.
                string countHeader = column + " count";
                if (!ColumnNames.Contains(countHeader))
                {
                    string? CountFor(BoundDataRow r)
                    {
                        return counts.TryGetValue(r.Column(colIndex) ?? "", out int c)
                            ? c.ToString(CultureInfo.InvariantCulture)
                            : null;
                    }
                    InsertColumn(countHeader, colIndex + 1, UnfilteredData.Select(CountFor));
                    if (m_History.History.Count > 0)
                    {
                        m_History.History[^1].AddedColumns.Add(countHeader);
                    }
                }
            }

            public void HideRowsMatching(string column, IEnumerable<string?> rows)
            {
                List<string?> strings = rows.Select(x => x?.ToLower()).ToList();
                PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.HideRowsMatching)
                {
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
                PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.HideRowsNotMatching)
                {
                    Column = column,
                    Values = strings.Select(s => s ?? "").ToList()
                });
                // case insensitive
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => !strings.Contains(x.Column(colIndex)?.ToLower()));
            }

            public void ShowRowsMatchingRegex(string regex, string column)
            {
                PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.ShowRowsMatchingRegex)
                {
                    Column = column,
                    Pattern = regex
                });
                Regex match = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => !match.IsMatch(x.Column(colIndex) ?? "null"));
            }

            public void HideRowsMatchingRegex(string regex, string column)
            {
                PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.HideRowsMatchingRegex)
                {
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
                    DispatchAction(snap, widthsByColumn);
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
                DataGridControlHistory.Snapshot snap,
                Dictionary<string, int> widthsByColumn)
            {
                try
                {
                    switch (snap.Kind)
                    {
                        case DataGridControlHistory.Snapshot.Operation.ShowAll:
                            ShowAll();
                            break;
                        case DataGridControlHistory.Snapshot.Operation.HideRowsMatching:
                            HideRowsMatching(snap.Column, snap.Values);
                            break;
                        case DataGridControlHistory.Snapshot.Operation.HideRowsNotMatching:
                            HideRowsNotMatching(snap.Column, snap.Values);
                            break;
                        case DataGridControlHistory.Snapshot.Operation.ShowRowsMatchingRegex:
                            ShowRowsMatchingRegex(snap.Pattern, snap.Column);
                            break;
                        case DataGridControlHistory.Snapshot.Operation.HideRowsMatchingRegex:
                            HideRowsMatchingRegex(snap.Pattern, snap.Column);
                            break;
                        case DataGridControlHistory.Snapshot.Operation.HideNotFirstUnique:
                            HideNotFirstUnique(snap.Column);
                            break;
                        case DataGridControlHistory.Snapshot.Operation.HideRowsAbove:
                            {
                                int pos = ResolveReplayPosition(snap.Column, snap.AnchorValue, snap.Row);
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
                        case DataGridControlHistory.Snapshot.Operation.HideRowsBelow:
                            {
                                int pos = ResolveReplayPosition(snap.Column, snap.AnchorValue, snap.Row);
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
                        case DataGridControlHistory.Snapshot.Operation.ApplySort:
                            SortByColumn(snap.Column, snap.Direction);
                            break;
                        case DataGridControlHistory.Snapshot.Operation.ColumnResize:
                            widthsByColumn[snap.Column] = snap.Width;
                            PushSnapshot(snap);
                            break;
                        case DataGridControlHistory.Snapshot.Operation.Decimate:
                            Decimate(snap.Stride);
                            break;
                        case DataGridControlHistory.Snapshot.Operation.Transpose:
                            Transpose();
                            break;
                        case DataGridControlHistory.Snapshot.Operation.SplitColumn:
                            if (snap.SplitRecipe == null)
                            {
                                OnLog?.Invoke(new CsvLog.Entry($"replay SplitColumn: no recipe for '{snap.Column}'", CsvLog.Priority.Warn));
                            }
                            else
                            {
                                SplitColumn(snap.Column, snap.SplitRecipe);
                            }
                            break;
                        case DataGridControlHistory.Snapshot.Operation.MoveColumn:
                            MoveColumn(snap.Column, snap.ToAfterColumn);
                            break;
                        default:
                            OnLog?.Invoke(new CsvLog.Entry($"replay: unknown action {snap.Kind}", CsvLog.Priority.Warn));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke(new CsvLog.Entry($"replay {snap.Kind} failed: {ex.Message}", CsvLog.Priority.Warn));
                }
            }

            // Insert one or more derived columns immediately after the source column.
            public int SplitColumn(string sourceColumnName, Func<string, IEnumerable<(string Header, string?[] Values)>> emit)
            {
                int sourceColIndex = ColumnNames.IndexOf(sourceColumnName);
                if (sourceColIndex < 0)
                {
                    OnLog?.Invoke(new CsvLog.Entry($"SplitColumn: no column '{sourceColumnName}'", CsvLog.Priority.Warn));
                    return 0;
                }
                var produced = emit(sourceColumnName).ToList();
                var snap = new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.SplitColumn)
                {
                    Column = sourceColumnName,
                    SplitRecipe = emit
                };
                PushSnapshot(snap);
                int insertAt = sourceColIndex + 1;
                foreach (var col in produced)
                {
                    InsertColumn(col.Header, insertAt, col.Values);
                    snap.AddedColumns.Add(col.Header);
                    insertAt++;
                }
                return produced.Count;
            }

            public void SortByColumn(string column, ListSortDirection direction = ListSortDirection.Ascending)
            {
                int colIndex = ColumnNames.IndexOf(column);
                if (colIndex < 0)
                {
                    OnLog?.Invoke(new CsvLog.Entry($"SortByColumn: no column {column}", CsvLog.Priority.Warn));
                    return;
                }
                PushSnapshot(new DataGridControlHistory.Snapshot(DataGridControlHistory.Snapshot.Operation.ApplySort)
                {
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

    [TestClass]
    public class DataGridBoundDataTest
    {
        private static DataGridControl.BoundData Make(List<string> cols, params string?[][] rows)
        {
            var data = rows.Select(r => (IEnumerable<string?>)r.ToList()).ToList();
            return new DataGridControl.BoundData(data, cols, _ => { });
        }

        [TestMethod]
        public void InsertColumnPlacesHeaderAndShiftsValues()
        {
            var bd = Make(new() { "key", "val" },
                new[] { "a", "1" },
                new[] { "b", "2" });
            bd.InsertColumn("mid", index: 1, values: new[] { "x", "y" });

            CollectionAssert.AreEqual(new List<string> { "key", "mid", "val" }, bd.ColumnNames);
            Assert.AreEqual("a", bd.UnfilteredData[0].Column(0));
            Assert.AreEqual("x", bd.UnfilteredData[0].Column(1));
            Assert.AreEqual("1", bd.UnfilteredData[0].Column(2));
            Assert.AreEqual("y", bd.UnfilteredData[1].Column(1));
        }

        [TestMethod]
        public void InsertColumnAtCountAppends()
        {
            var bd = Make(new() { "key", "val" }, new[] { "a", "1" });
            bd.InsertColumn("tail", index: bd.ColumnNames.Count, values: new[] { "z" });

            CollectionAssert.AreEqual(new List<string> { "key", "val", "tail" }, bd.ColumnNames);
            Assert.AreEqual("z", bd.UnfilteredData[0].Column(2));
        }

        [TestMethod]
        public void RemoveColumnDropsHeaderAndShiftsDataLeft()
        {
            var bd = Make(new() { "key", "val" },
                new[] { "a", "1" },
                new[] { "b", "2" });
            bd.InsertColumn("mid", index: 1, values: new[] { "x", "y" });

            bool removed = bd.RemoveColumn("mid");

            Assert.IsTrue(removed);
            CollectionAssert.AreEqual(new List<string> { "key", "val" }, bd.ColumnNames);
            Assert.AreEqual("a", bd.UnfilteredData[0].Column(0));
            Assert.AreEqual("1", bd.UnfilteredData[0].Column(1));
        }

        [TestMethod]
        public void RemoveColumnReturnsFalseWhenMissing()
        {
            var bd = Make(new() { "key", "val" }, new[] { "a", "1" });
            Assert.IsFalse(bd.RemoveColumn("nope"));
            Assert.AreEqual(2, bd.ColumnNames.Count);
        }

        [TestMethod]
        public void AppendColumnValueDelegatesToInsertAtEnd()
        {
            var bd = Make(new() { "key", "val" }, new[] { "a", "1" });
            var row = bd.UnfilteredData[0];
            int prev = row.Count;

            row.AppendColumnValue("appended");

            Assert.AreEqual(prev + 1, row.Count);
            Assert.AreEqual("appended", row.Column(prev));
            // existing values untouched
            Assert.AreEqual("a", row.Column(0));
            Assert.AreEqual("1", row.Column(1));
        }

        [TestMethod]
        public void HideNotFirstUniqueInsertsCountColumnAndCollapsesDuplicates()
        {
            var bd = Make(new() { "key", "val" },
                new[] { "a", "1" },
                new[] { "b", "2" },
                new[] { "a", "3" },
                new[] { "a", "4" },
                new[] { "b", "5" });

            bd.HideNotFirstUnique("key");

            CollectionAssert.AreEqual(new List<string> { "key", "key count", "val" }, bd.ColumnNames);
            Assert.AreEqual(2, bd.FilteredData.Count);

            Assert.AreEqual("a", bd.FilteredData[0].Column(0));
            Assert.AreEqual("3", bd.FilteredData[0].Column(1));
            Assert.AreEqual("1", bd.FilteredData[0].Column(2));

            Assert.AreEqual("b", bd.FilteredData[1].Column(0));
            Assert.AreEqual("2", bd.FilteredData[1].Column(1));
            Assert.AreEqual("2", bd.FilteredData[1].Column(2));
        }

        [TestMethod]
        public void HideNotFirstUniqueLeavesExistingCountColumnAlone()
        {
            var bd = Make(new() { "key", "val" },
                new[] { "a", "1" },
                new[] { "a", "2" },
                new[] { "b", "3" });

            bd.HideNotFirstUnique("key");
            int afterFirst = bd.ColumnNames.Count;

            bd.HideNotFirstUnique("key"); // already unique - no fresh column should appear

            Assert.AreEqual(afterFirst, bd.ColumnNames.Count);
            Assert.AreEqual(1, bd.ColumnNames.Count(c => c == "key count"));
        }

        [TestMethod]
        public void UndoHideNotFirstUniqueRemovesCountColumn()
        {
            var bd = Make(new() { "key", "val" },
                new[] { "a", "1" },
                new[] { "a", "2" },
                new[] { "b", "3" });

            bd.HideNotFirstUnique("key");
            Assert.IsTrue(bd.ColumnNames.Contains("key count"));

            bd.Undo();

            CollectionAssert.AreEqual(new List<string> { "key", "val" }, bd.ColumnNames);
            Assert.AreEqual(3, bd.FilteredData.Count);
            // row data shape restored - col(1) should be the original val again
            Assert.AreEqual("1", bd.FilteredData[0].Column(1));
            Assert.AreEqual("2", bd.FilteredData[1].Column(1));
            Assert.AreEqual("3", bd.FilteredData[2].Column(1));
        }

        [TestMethod]
        public void RedoHideNotFirstUniqueRestoresCountColumn()
        {
            var bd = Make(new() { "key", "val" },
                new[] { "a", "1" },
                new[] { "a", "2" },
                new[] { "b", "3" });

            bd.HideNotFirstUnique("key");
            bd.Undo();
            bd.Redo();

            CollectionAssert.AreEqual(new List<string> { "key", "key count", "val" }, bd.ColumnNames);
            Assert.AreEqual(2, bd.FilteredData.Count);
            Assert.AreEqual("2", bd.FilteredData[0].Column(1)); // a appears 2x
            Assert.AreEqual("1", bd.FilteredData[1].Column(1)); // b appears 1x
        }

        [TestMethod]
        public void ColoursShiftWithInsertAndRemove()
        {
            var bd = Make(new() { "key", "val" }, new[] { "a", "1" });
            var row = bd.UnfilteredData[0];
            row.CellColour(1, Color.Red);

            bd.InsertColumn("mid", index: 1, values: new[] { "x" });
            // val moved from col 1 to col 2 - red should follow it
            Assert.IsNotNull(row.Colours);
            Assert.AreEqual(Color.Red, row.Colours![2]);
            Assert.IsNull(row.Colours[1]);

            bd.RemoveColumn("mid");
            Assert.AreEqual(Color.Red, row.Colours![1]);
        }

        [TestMethod]
        public void DiffsShiftWithInsertAndRemove()
        {
            var bd = Make(new() { "key", "val" }, new[] { "a", "1" });
            var row = bd.UnfilteredData[0];
            var marker = new StringDiff.Diffs { ("1", StringDiff.Diffs.Side.Left) };
            row.CellDiffs(1, marker);

            bd.InsertColumn("mid", index: 1, values: new[] { "x" });
            Assert.IsNotNull(row.Diffs);
            Assert.AreSame(marker, row.Diffs![2]);
            Assert.IsNull(row.Diffs[1]);

            bd.RemoveColumn("mid");
            Assert.AreSame(marker, row.Diffs![1]);
        }
    }
}
