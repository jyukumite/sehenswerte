using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

// Thin numeric-column wrapper over Parquet.Net.
// Public API is ParquetNumeric.SaveCols / ParquetNumeric.LoadCols. Files written
// here are spec-compliant Parquet (Parquet.Net does the heavy lifting). Per-column
// metadata is stored as file-level key/value entries:
//   length.<col> -- original length so short columns can be trimmed back from the
//                   row-group pad
//   sps.<col>    -- samples per second
//   offset.<col> -- sample offset
// Timestamp columns are written as TIMESTAMP_MILLIS (DateTime); LoadedColumn.Values
// always exposes unix seconds as double, regardless of the on-disk encoding.

namespace SehensWerte.Files
{
    public sealed class LoadedColumn
    {
        public string Name = "";
        public double[] Values = Array.Empty<double>();
        public double? SamplesPerSecond;
        public int? SampleOffset;
        public string? Error;
        public bool IsTimestamp;
    }

    public static class ParquetNumeric
    {
        public static void SaveCols(
            string filename,
            IReadOnlyList<string> names,
            IReadOnlyList<double[]> columns,
            IReadOnlyList<double>? samplesPerSeconds = null,
            IReadOnlyList<int>? sampleOffsets = null,
            IReadOnlyList<bool>? isTimestamp = null)
        {
            if (names.Count != columns.Count) throw new ArgumentException("names/columns length mismatch");
            if (columns.Count == 0) throw new ArgumentException("no columns");

            int rows = 0;
            for (int loop = 0; loop < columns.Count; loop++)
            {
                if (columns[loop].Length > rows)
                {
                    rows = columns[loop].Length;
                }
            }

            var fields = new DataField[names.Count];
            var meta = new Dictionary<string, string>();
            for (int loop = 0; loop < names.Count; loop++)
            {
                bool ts = isTimestamp != null && loop < isTimestamp.Count && isTimestamp[loop];
                fields[loop] = ts
                    ? (DataField)new DateTimeDataField(names[loop], DateTimeFormat.DateAndTime)
                    : new DataField<double>(names[loop]);

                meta["length." + names[loop]] = columns[loop].Length.ToString(CultureInfo.InvariantCulture);
                if (samplesPerSeconds != null && loop < samplesPerSeconds.Count)
                {
                    meta["sps." + names[loop]] = samplesPerSeconds[loop].ToString(CultureInfo.InvariantCulture);
                }
                if (sampleOffsets != null && loop < sampleOffsets.Count)
                {
                    meta["offset." + names[loop]] = sampleOffsets[loop].ToString(CultureInfo.InvariantCulture);
                }
            }
            var schema = new ParquetSchema(fields);

            // Task.Run hops off the caller's sync context so async-over-sync from a
            // WinForms thread can't deadlock on Parquet.Net's awaits.
            Task.Run(async () =>
            {
                using var stream = File.Create(filename);
                using var writer = await ParquetWriter.CreateAsync(schema, stream);
                writer.CustomMetadata = meta;
                using var rowGroup = writer.CreateRowGroup();
                for (int loop = 0; loop < names.Count; loop++)
                {
                    var src = columns[loop];
                    if (isTimestamp != null && loop < isTimestamp.Count && isTimestamp[loop])
                    {
                        var arr = new DateTime[rows];
                        for (int row = 0; row < rows; row++)
                        {
                            double value = row < src.Length ? src[row] : double.NaN;
                            arr[row] = double.IsFinite(value)
                                ? DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(value * 1000.0)).UtcDateTime
                                : DateTime.UnixEpoch;
                        }
                        await rowGroup.WriteColumnAsync(new DataColumn(fields[loop], arr));
                    }
                    else
                    {
                        var data = src.Length == rows ? src : PadWithNaN(src, rows);
                        await rowGroup.WriteColumnAsync(new DataColumn(fields[loop], data));
                    }
                }
            }).GetAwaiter().GetResult();
        }

        public static List<LoadedColumn> LoadCols(string filename)
        {
            try
            {
                return Task.Run(LoadColsAsyncImpl).GetAwaiter().GetResult();
            }
            catch (IOException ex) when (LooksLikeNotParquet(ex))
            {
                throw new InvalidDataException($"not a Parquet file: {filename}", ex);
            }

            async Task<List<LoadedColumn>> LoadColsAsyncImpl()
            {
                using var stream = File.OpenRead(filename);
                using var reader = await ParquetReader.CreateAsync(stream);

                var meta = reader.CustomMetadata ?? new Dictionary<string, string>();
                var fields = reader.Schema.GetDataFields();
                var perField = new (List<double>? buf, string? error, bool isTimestamp)[fields.Length];
                for (int field = 0; field < fields.Length; field++)
                {
                    perField[field] = (new List<double>(), null, false);
                }
                for (int rgIdx = 0; rgIdx < reader.RowGroupCount; rgIdx++)
                {
                    using var rgReader = reader.OpenRowGroupReader(rgIdx);
                    for (int field = 0; field < fields.Length; field++)
                    {
                        if (perField[field].error != null) continue;
                        try
                        {
                            var dc = await rgReader.ReadColumnAsync(fields[field]);
                            var (vals, isTs) = ConvertToDoubles(fields[field], dc.Data);
                            perField[field].buf!.AddRange(vals);
                            perField[field] = (perField[field].buf, null, perField[field].isTimestamp || isTs);
                        }
                        catch (Exception ex)
                        {
                            perField[field] = (null, $"{ex.GetType().Name}: {ex.Message}", false);
                        }
                    }
                }

                var result = new List<LoadedColumn>();
                for (int field = 0; field < fields.Length; field++)
                {
                    var (buf, err, isTs) = perField[field];
                    var loadedColumn = new LoadedColumn { Name = fields[field].Name };
                    if (err != null)
                    {
                        loadedColumn.Error = err;
                    }
                    else
                    {
                        loadedColumn.Values = buf!.ToArray();
                        loadedColumn.IsTimestamp = isTs;
                    }

                    if (meta.TryGetValue("sps." + loadedColumn.Name, out var spsStr)
                        && double.TryParse(spsStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sps))
                    {
                        loadedColumn.SamplesPerSecond = sps;
                    }
                    if (meta.TryGetValue("offset." + loadedColumn.Name, out var offStr)
                        && int.TryParse(offStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var off))
                    {
                        loadedColumn.SampleOffset = off;
                    }
                    if (meta.TryGetValue("length." + loadedColumn.Name, out var lenStr)
                        && int.TryParse(lenStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var len)
                        && len >= 0 && len < loadedColumn.Values.Length)
                    {
                        var trimmed = new double[len];
                        Array.Copy(loadedColumn.Values, trimmed, len);
                        loadedColumn.Values = trimmed;
                    }
                    result.Add(loadedColumn);
                }
                return result;
            }
        }

        private static double[] PadWithNaN(double[] src, int rows)
        {
            var result = new double[rows];
            Array.Copy(src, result, src.Length);
            for (int loop = src.Length; loop < rows; loop++)
            {
                result[loop] = double.NaN;
            }
            return result;
        }

        private static (double[] values, bool isTimestamp) ConvertToDoubles(DataField field, Array data)
        {
            switch (data)
            {
                case double[] d:
                    return (d, false);
                case float[] f:
                    {
                        var result = new double[f.Length];
                        for (int loop = 0; loop < f.Length; loop++)
                        {
                            result[loop] = f[loop];
                        }
                        return (result, false);
                    }
                case int[] i32:
                    {
                        var result = new double[i32.Length];
                        for (int loop = 0; loop < i32.Length; loop++)
                        {
                            result[loop] = i32[loop];
                        }
                        return (result, false);
                    }
                case long[] i64:
                    {
                        var result = new double[i64.Length];
                        for (int loop = 0; loop < i64.Length; loop++)
                        {
                            result[loop] = i64[loop];
                        }
                        return (result, false);
                    }
                case bool[] b:
                    {
                        var result = new double[b.Length];
                        for (int loop = 0; loop < b.Length; loop++)
                        {
                            result[loop] = b[loop] ? 1.0 : 0.0;
                        }
                        return (result, false);
                    }
                case DateTime[] dt:
                    {
                        var result = new double[dt.Length];
                        for (int loop = 0; loop < dt.Length; loop++)
                        {
                            result[loop] = (DateTime.SpecifyKind(dt[loop], DateTimeKind.Utc) - DateTime.UnixEpoch).TotalSeconds;
                        }
                        return (result, true);
                    }
                case DateTimeOffset[] dto:
                    {
                        var result = new double[dto.Length];
                        for (int loop = 0; loop < dto.Length; loop++)
                        {
                            result[loop] = dto[loop].ToUnixTimeMilliseconds() / 1000.0;
                        }
                        return (result, true);
                    }
                default:
                    throw new NotSupportedException($"unsupported parquet type {data?.GetType().Name ?? "(null)"} for column '{field.Name}'");
            }
        }

        private static bool LooksLikeNotParquet(IOException ex)
        {
            // Parquet.Net throws IOException with a 'not a Parquet file' message for
            // files missing the magic; preserve the homebrew's InvalidDataException
            // contract for that case while letting genuine I/O errors propagate.
            var msg = ex.Message ?? string.Empty;
            return msg.Contains("parquet", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("magic", StringComparison.OrdinalIgnoreCase);
        }
    }

    [TestClass]
    public class ParquetNumericTests
    {
        [TestMethod]
        public void RoundTripDifferentLengthsAndMetadata()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                var names = new[] { "alpha", "beta" };
                var cols = new[]
                {
                    new[] { 1.0, 2.0, 3.0, 4.0 },
                    new[] { 10.0, 20.0 }
                };
                var sps = new[] { 1000.0, 500.0 };
                var offsets = new[] { 0, 7 };

                ParquetNumeric.SaveCols(path, names, cols, sps, offsets);

                var loaded = ParquetNumeric.LoadCols(path);
                Assert.AreEqual(2, loaded.Count);

                var a = loaded.First(x => x.Name == "alpha");
                Assert.AreEqual(4, a.Values.Length);
                CollectionAssert.AreEqual(cols[0], a.Values);
                Assert.AreEqual(1000.0, a.SamplesPerSecond);
                Assert.AreEqual(0, a.SampleOffset);

                var b = loaded.First(x => x.Name == "beta");
                Assert.AreEqual(2, b.Values.Length);
                CollectionAssert.AreEqual(cols[1], b.Values);
                Assert.AreEqual(500.0, b.SamplesPerSecond);
                Assert.AreEqual(7, b.SampleOffset);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [TestMethod]
        public void RoundTripPreservesNan()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                var names = new[] { "x" };
                var cols = new[] { new[] { 1.0, double.NaN, 3.0 } };

                ParquetNumeric.SaveCols(path, names, cols);
                var loaded = ParquetNumeric.LoadCols(path);

                Assert.AreEqual(1, loaded.Count);
                Assert.AreEqual(3, loaded[0].Values.Length);
                Assert.AreEqual(1.0, loaded[0].Values[0]);
                Assert.IsTrue(double.IsNaN(loaded[0].Values[1]));
                Assert.AreEqual(3.0, loaded[0].Values[2]);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [TestMethod]
        public void SingleRowEdgeCase()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                ParquetNumeric.SaveCols(path, new[] { "only" }, new[] { new[] { 42.0 } });
                var loaded = ParquetNumeric.LoadCols(path);
                Assert.AreEqual(1, loaded.Count);
                Assert.AreEqual(1, loaded[0].Values.Length);
                Assert.AreEqual(42.0, loaded[0].Values[0]);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [TestMethod]
        public void RejectsNonParquetFile()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "this is not a parquet file");
                Assert.ThrowsException<InvalidDataException>(() => ParquetNumeric.LoadCols(path));
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [TestMethod]
        public void TimestampColumnRoundTrip()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                // Mix one timestamp column with one regular value column.
                // Values chosen at millisecond boundaries so TIMESTAMP_MILLIS round-trips exactly.
                var times = new[] { 1700000000.000, 1700000001.500, 1700000003.250 };
                var values = new[] { 1.0, 2.0, 3.0 };
                ParquetNumeric.SaveCols(
                    path,
                    new[] { "trace.Time", "trace.Value" },
                    new[] { times, values },
                    samplesPerSeconds: null,
                    sampleOffsets: null,
                    isTimestamp: new[] { true, false });

                var loaded = ParquetNumeric.LoadCols(path);
                Assert.AreEqual(2, loaded.Count);

                var ts = loaded.First(c => c.Name == "trace.Time");
                Assert.IsTrue(ts.IsTimestamp);
                Assert.AreEqual(3, ts.Values.Length);
                for (int i = 0; i < times.Length; i++)
                {
                    Assert.AreEqual(times[i], ts.Values[i], 1e-3, $"ts[{i}]");
                }

                var vals = loaded.First(c => c.Name == "trace.Value");
                Assert.IsFalse(vals.IsTimestamp);
                CollectionAssert.AreEqual(values, vals.Values);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }
    }
}
