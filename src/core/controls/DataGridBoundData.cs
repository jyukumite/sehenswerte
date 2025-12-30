using SehensWerte.Files;
using SehensWerte.Maths;
using System.Collections;
using System.ComponentModel;
using System.Data;
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
        }

        public class BoundDataRowString : BoundDataRow
        {
            public string?[] Data;

            public override int Count => Data.Length;
            public override string?[] Strings => Data;
            public override string? Column(int index) => index < Data.Length ? Data[index] : "";
            public override double ColumnDouble(int index) => index < Data.Length ? (Data[index]?.ToDouble(0) ?? 0) : 0;
            public override void Set(int index, string? to) { Data[index] = to; }

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
            public override void Set(int index, string? to) { Data[index] = to?.ToDouble(0) ?? 0; }

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
            private DataGridView DataGrid;

            private Action<CsvLog.Entry> OnLog;
            private CodeProfile Profile = new CodeProfile();

            public string CsvFileName { get; private set; }
            public List<BoundDataRow> UnfilteredData;
            public List<BoundDataRow> FilteredData;
            private Stack<UndoEntry> UndoList = new Stack<UndoEntry>();
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
                UndoList = new Stack<UndoEntry>();
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

            public void Unbind()
            {
                // could release resources, but the GC should clean up anyway
            }

            public void CellColour(int col, int row, Color colour)
            {
                FilteredData[row].CellColour(col, colour);
            }

            public void Undo()
            {
                if (UndoList.Count() > 0)
                {
                    var undo = UndoList.Pop();
                    if (undo.VisibleRows != null)
                    {
                        FilteredData = undo.VisibleRows;
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
            }

            private void Refilter()
            {
                Profile.Enter();

                PushUndoVisibleRows();

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

            private void PushUndoVisibleRows()
            {
                if (FilteredData.Count() > 0)
                {
                    UndoList.Push(new UndoEntry() { VisibleRows = FilteredData.ToList() });
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

            public IEnumerable<int> RowsWithSelection()
            {
                var rows = DataGrid.SelectedRows.Cast<DataGridViewRow>().Select(x => ((BoundDataRow?)(x.DataBoundItem))?.Index);
                var cells = DataGrid.SelectedCells.Cast<DataGridViewCell>().Select(x => ((BoundDataRow?)(x.OwningRow.DataBoundItem))?.Index);
                var union = rows.Union(cells).Where(x => x != null).Select(y => (int)(y ?? 0));
                return union.ToArray();
            }

            public IEnumerable<int> ColsWithSelection()
            {
                var cols = DataGrid.SelectedColumns.Cast<DataGridViewColumn>().Select(x => x.Index);
                var cells = DataGrid.SelectedCells.Cast<DataGridViewCell>().Select(x => x.OwningColumn.Index);
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
                int colIndex = ColumnNames.IndexOf(v);
                return colIndex == -1 ? null : FilteredData.Select(x => x.Column(colIndex)).ToArray();
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

            public void HideRowsIf(Func<BoundDataRow, bool> predicate)
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
                // case insensitive
                List<string?> strings = rows.Select(x => x?.ToLower()).ToList();
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => strings.Contains(x.Column(colIndex)?.ToLower()));
            }

            public void HideRowsNotMatching(string column, IEnumerable<string?> rows)
            {
                // case insensitive
                List<string?> strings = rows.Select(x => x?.ToLower()).ToList();
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => !strings.Contains(x.Column(colIndex)?.ToLower()));
            }

            public void ShowRowsMatchingRegex(string regex, string column)
            {
                Regex match = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => !match.IsMatch(x.Column(colIndex) ?? "null"));
            }

            public void HideRowsMatchingRegex(string regex, string column)
            {
                Regex match = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                int colIndex = ColumnNames.IndexOf(column);
                HideRowsIf(x => match.IsMatch(x.Column(colIndex) ?? "null"));
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
                        PushUndoVisibleRows();

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

            internal void SaveToCsv(string fileName)
            {
                //fixme: save selection?
                CSVSave.SaveRows(fileName, ColumnNames, FilteredData.Select(x => x.Strings), ",");
            }

            internal SelectedCellsData SelectedCellsToClipboardFormats(bool numericGrid)
            {
                var cells = DataGrid.SelectedCells.Cast<DataGridViewCell>().ToArray();

                var colFlags = new bool[DataGrid.ColumnCount];
                var rowFlags = new bool[DataGrid.RowCount];
                foreach (var cell in cells)
                {
                    colFlags[cell.ColumnIndex] = true;
                    rowFlags[cell.RowIndex] = true;
                }

                var colIndex = new int[DataGrid.ColumnCount];
                int resultCols = 0;
                for (int loop = 0; loop < DataGrid.ColumnCount; loop++)
                {
                    if (colFlags[loop])
                    {
                        colIndex[loop] = resultCols++;
                    }
                }

                var rowIndex = new int[DataGrid.RowCount];
                int resultRows = 0;
                for (int loop = 0; loop < DataGrid.RowCount; loop++)
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
