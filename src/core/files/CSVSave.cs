using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace SehensWerte.Files
{
    public static class CSVSave
    {
        public static void SaveRows(string filename, IEnumerable<string>? header, IEnumerable<object> rowArrays, string separator)
        {
            using (var writer = File.CreateText(filename))
            {
                SaveRows(writer, header, rowArrays, separator);
                writer.Close();
            }
        }

        public static void SaveRows(StreamWriter writer, IEnumerable<string>? header, IEnumerable<object> rowArrays, string separator)
        {
            if (header != null)
            {
                SaveRow(writer, header, separator);
            }
            foreach (object row in rowArrays)
            {
                SaveRow(writer, (IEnumerable)row, separator);
            }
        }

        public static void SaveCols(string filename, IEnumerable<string> header, IEnumerable<object> colArrays, string separator)
        {
            using (StreamWriter writer = File.CreateText(filename))
            {
                SaveCols(writer, header, colArrays, separator);
                writer.Close();
            }
        }

        public static void SaveCols(StreamWriter writer, IEnumerable<string> header, IEnumerable<object> colArrays, string separator)
        {
            if (header != null)
            {
                SaveRow(writer, header, separator);
            }
            List<object> columns = colArrays.ToList();
            int rowCount = columns.Max(x => x is IList ? ((IList)x).Count : 0);
            int colCount = columns.Count;
            for (int row = 0; row < rowCount; row++)
            {
                object[] array = new object[colCount];
                for (int j = 0; j < colCount; j++)
                {
                    IList? column = columns[j] as IList;
                    if (column != null)
                    {
                        array[j] = (row < column.Count && column != null) ? (column[row] ?? "") : "";
                    }
                    else
                    {
                        array[j] = "";
                    }
                }
                int trim = array.Length;
                while (trim > 1 && array[trim - 1] as string == "")
                {
                    trim--;
                }
                SaveRow(writer, array.Take(trim), separator);
            }
        }

        public static void SaveColsGzip(string fileName, IEnumerable<string> header, IEnumerable<object> colArrays, string separator)
        {
            Stream file = File.Open(fileName, FileMode.Create);
            try
            {
                using (GZipStream compressor = new GZipStream(file, CompressionMode.Compress, true))
                using (var writer = new StreamWriter(compressor))
                {
                    CSVSave.SaveCols(writer, header, colArrays, separator);
                    writer.Flush();
                    compressor.Close();
                    file.Close();
                }
            }
            catch
            {
                file.Close();
                File.Delete(fileName);
                throw;
            }
        }

        public static void SaveRow(StreamWriter writer, IEnumerable array, string separator = ",", string valueIfNull = "null")
        {
            if (array != null)
            {
                writer.WriteLine(RowToCsvText(array, separator, valueIfNull));
            }
        }

        public static string RowToCsvText(IEnumerable array, string separator = ",", string valueIfNull = "null", bool quoteEscape = true)
        {
            StringBuilder builder = new StringBuilder();
            foreach (object data in array)
            {
                string value =
                    (data == null) ? valueIfNull
                    : data is double ? ((double)data).ToString(CultureInfo.InvariantCulture)
                    : data is float ? ((float)data).ToString(CultureInfo.InvariantCulture)
                    : (data.ToString() ?? "");

                if (quoteEscape && (value.Contains(separator) || value.Contains("\"") || value.Contains("\n") || value.Contains("\r")))
                {
                    builder.Append("\"" + (object)value.Replace("\"", "\"\"") + "\"");
                }
                else
                {
                    builder.Append(value);
                }
                builder.Append(separator);
            }
            builder.Remove(builder.Length - 1, 1);
            var csvTextString = builder.ToString();
            return csvTextString;
        }
    }
}
