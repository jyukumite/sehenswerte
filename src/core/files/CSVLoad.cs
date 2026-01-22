using System.Collections;
using System.IO.Compression;
using System.Text;

namespace SehensWerte.Files
{
    public class CSVRow<T>
    {
        public T[] Row;
        public List<string> Columns;
        public T Default;

        public CSVRow(List<string> columns, T[] rows, T defaultValue)
        {
            Columns = columns;
            Row = rows;
            Default = defaultValue;
        }

        public T this[string column]
        {
            get
            {
                int index = Columns.IndexOf(column);
                return ((index < 0) || (index >= Row.Length)) ? Default : Row[index];
            }
            set => Row[Columns.IndexOf(column)] = value;
        }

        public T this[int column]
        {
            get => ((column < 0) || (column >= Row.Length)) ? Default : Row[column];
            set => Row[column] = value;
        }
    }

    public class CSVEnumerator<T> : IEnumerator<CSVRow<T>>
    {
        public CSVLoad<T> Source;
        int RowNumber = int.MinValue;

        public CSVEnumerator(CSVLoad<T> source)
        {
            Source = source;
        }

        public bool MoveNext()
        {
            RowNumber = (RowNumber < 0 ? 0 : ++RowNumber);
            return RowNumber < Source.Rows.Count;
        }

        public void Reset()
        {
            RowNumber = int.MinValue;
        }

        private CSVRow<T> ThisRow()
        {
            try
            {
                return new CSVRow<T>(Source.ColumnHeadings, Source.Rows[RowNumber], Source.Default);
            }
            catch (IndexOutOfRangeException)
            {
                throw new InvalidOperationException();
            }
        }

        CSVRow<T> IEnumerator<CSVRow<T>>.Current => ThisRow();
        public object Current => ThisRow();

        public void Dispose() { }
    }

    public class CSVLoad<T> : IEnumerable<CSVRow<T>>, IDisposable
    {
        public char Separator = ',';
        public List<string> ColumnHeadings;
        public int ColCount => ColumnHeadings.Count;
        public List<T[]> Rows;
        public int RowCount { get { return CountedRows; } }
        private int CountedRows = 0;

        public T Default;
        public string HeaderRowPrefix;
        private string? NextFileLine;

        public StreamReader? File;
        public Func<string, T> OnParse;
        private bool QuotedText = false;

        public CSVLoad(string filename, Func<String, T> parse, T defaultValue, char separator = ',', string headerRowPrefix = "") :
            this(NormalOrGZipFileStream(filename), parse, defaultValue, separator, headerRowPrefix)
        {
        }

        public CSVLoad(StreamReader file, Func<String, T> parse, T defaultValue, char separator = ',', string headerRowPrefix = "")
        {
            File = file;
            Separator = separator;
            HeaderRowPrefix = headerRowPrefix;
            OnParse = parse;
            Default = defaultValue;
            ReadHeader();
            CompleteLoad();
            ColumnHeadings = ColumnHeadings ?? new List<string>();
            Rows = Rows ?? new List<T[]>();
        }

        public static StreamReader NormalOrGZipFileStream(string filename)
        {
            FileStream? fileStream = null;
            GZipStream? gzipStream = null;
            fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            if (filename.ToLowerInvariant().EndsWith(".gz"))
            {
                gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                return new StreamReader(gzipStream, Encoding.UTF8);
            }
            else
            {
                return new StreamReader(fileStream, Encoding.UTF8);
            }
        }

        public void Dispose()
        {
            Rows?.Clear();
            ColumnHeadings?.Clear();
            File?.Close();
            File = null;
        }

        public CSVRow<T> this[int index] => new CSVRow<T>(ColumnHeadings, Rows[index], Default);

        public T[] Column(int index)
        {
            T[] result = new T[Rows.Count];
            for (int loop = 0; loop < Rows.Count; loop++)
            {
                result[loop] = ((index == int.MinValue) || (index >= Rows[loop].Length)) ? Default : Rows[loop][index];
            }
            return result;
        }

        public T[] Column(string column)
        {
            return Column(ColumnHeadings.IndexOf(column));
        }

        public T[] ColumnOrDefault(string column, T def)
        {
            int index = ColumnHeadings.IndexOf(column);
            if (index == -1)
            {
                return Rows.Select(x => def).ToArray();
            }
            else
            {
                return Column(index);
            }
        }

        private void ReadHeader()
        {
            bool found = false;
            PreloadNext();
            while (File != null && !found)
            {
                string? asString = NextFileLine;
                List<StringBuilder>? headings = ReadNextMergedRow();
                if (headings != null && headings.Count != 0 && (HeaderRowPrefix == "" || (asString?.StartsWith(HeaderRowPrefix) ?? false)))
                {
                    while (headings.Count > 0 && headings[headings.Count - 1].Length == 0) // dangling ,
                    {
                        headings.RemoveAt(headings.Count - 1);
                    }
                    ColumnHeadings = headings.Select(x => x.ToString()).ToList();
                    found = true;
                }
            }
        }

        public List<StringBuilder>? ReadNextMergedRow()
        {
            if (NextFileLine == null && File != null)
            {
                File.Close();
                File = null;
            }

            if (NextFileLine != null)
            {
                List<StringBuilder> row = new List<StringBuilder>();
                do
                {
                    if (this.QuotedText || NextFileLine.Contains('"') || row.Count > 0)
                    {
                        if (row.Count == 0)
                        {
                            row.Add(new StringBuilder());
                        }
                        else
                        {
                            row[row.Count - 1].Append('\n');
                        }
                        foreach (char c in NextFileLine.ToCharArray())
                        {
                            if (c == Separator && !this.QuotedText)
                            {
                                row.Add(new StringBuilder());
                            }
                            else if (c == '"')
                            {
                                this.QuotedText = !this.QuotedText;
                            }
                            else
                            {
                                row[row.Count - 1].Append(c);
                            }
                        }
                    }
                    else
                    {
                        row.AddRange(NextFileLine.Split(Separator).Select(x => new StringBuilder(x)));
                    }
                    PreloadNext();
                } while (QuotedText == true && NextFileLine != null);
                return row;
            }

            return null;
        }

        private void PreloadNext() // one string
        {
            NextFileLine = null;
            while (File != null)
            {
                if (File.EndOfStream)
                {
                    File.Close();
                    File = null;
                }
                else
                {
                    NextFileLine = File.ReadLine();
                    break;
                }
            }
        }

        private void CompleteLoad()
        {
            if (Rows == null)
            {
                Rows = new List<T[]>();
            }
            while (File != null || NextFileLine != null)
            {
                var row = ReadNextMergedRow();
                if (row != null)
                {
                    while (row.Count < ColCount)
                    {
                        row.Add(new StringBuilder()); // pad to correct length
                    }
                    Rows.Add(row.Select(x => OnParse(x.ToString())).Take(ColCount).ToArray());
                }
                CountedRows++;
            }
        }

        public IEnumerator<CSVRow<T>> GetEnumerator()
        {
            return new CSVEnumerator<T>(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new CSVEnumerator<T>(this);
        }

        private object ArrayToObject(T[] toConvert)
        {
            return toConvert;
        }
    }
}
