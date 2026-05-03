using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using SehensWerte.Files;

namespace ParquetTest
{
    [TestClass]
    public class ParquetNetReadBack
    {
        [TestMethod]
        public async Task ParquetNetReadsFileWrittenByParquetSimple()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                var names = new[] { "alpha", "beta", "gamma" };
                var cols = new[]
                {
                    new[] { 1.0, 2.0, 3.0, 4.0 },
                    new[] { 10.0, 20.0 },
                    new[] { 0.5, double.NaN, 1.5, 2.5 }
                };
                var sps = new[] { 1000.0, 500.0, 250.0 };
                var offsets = new[] { 0, 7, 13 };

                ParquetSimple.SaveCols(path, names, cols, sps, offsets);

                using var stream = File.OpenRead(path);
                using var reader = await ParquetReader.CreateAsync(stream);

                var meta = reader.CustomMetadata;
                Assert.IsNotNull(meta);
                Assert.AreEqual("4", meta!["length.alpha"]);
                Assert.AreEqual("2", meta["length.beta"]);
                Assert.AreEqual("4", meta["length.gamma"]);
                Assert.AreEqual("1000", meta["sps.alpha"]);
                Assert.AreEqual("500", meta["sps.beta"]);
                Assert.AreEqual("250", meta["sps.gamma"]);
                Assert.AreEqual("0", meta["offset.alpha"]);
                Assert.AreEqual("7", meta["offset.beta"]);
                Assert.AreEqual("13", meta["offset.gamma"]);

                var fields = reader.Schema.GetDataFields();
                Assert.AreEqual(3, fields.Length);
                Assert.AreEqual("alpha", fields[0].Name);
                Assert.AreEqual("beta", fields[1].Name);
                Assert.AreEqual("gamma", fields[2].Name);

                Assert.AreEqual(1, reader.RowGroupCount);
                using var rg = reader.OpenRowGroupReader(0);

                var alphaCol = await rg.ReadColumnAsync(fields[0]);
                CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0, 4.0 }, (Array)alphaCol.Data);

                // beta is padded to 4 with NaN by the writer
                var betaCol = await rg.ReadColumnAsync(fields[1]);
                var beta = (double[])betaCol.Data;
                Assert.AreEqual(4, beta.Length);
                Assert.AreEqual(10.0, beta[0]);
                Assert.AreEqual(20.0, beta[1]);
                Assert.IsTrue(double.IsNaN(beta[2]));
                Assert.IsTrue(double.IsNaN(beta[3]));

                var gammaCol = await rg.ReadColumnAsync(fields[2]);
                var gamma = (double[])gammaCol.Data;
                Assert.AreEqual(4, gamma.Length);
                Assert.AreEqual(0.5, gamma[0]);
                Assert.IsTrue(double.IsNaN(gamma[1]));
                Assert.AreEqual(1.5, gamma[2]);
                Assert.AreEqual(2.5, gamma[3]);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [DataTestMethod]
        [DataRow(CompressionMethod.None)]
        [DataRow(CompressionMethod.Gzip)]
        [DataRow(CompressionMethod.Snappy)]
        public async Task ParquetSimpleReadsRequiredFileWrittenByParquetNet(CompressionMethod compression)
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                var values = Enumerable.Range(0, 500).Select(i => Math.Cos(i * 0.05)).ToArray();
                var field = new DataField<double>("signal");
                var schema = new ParquetSchema(field);

                using (var fs = File.Create(path))
                using (var writer = await ParquetWriter.CreateAsync(schema, fs))
                {
                    writer.CompressionMethod = compression;
                    writer.CustomMetadata = new Dictionary<string, string>
                    {
                        ["sps.signal"] = "200",
                        ["length.signal"] = "500"
                    };
                    using var rg = writer.CreateRowGroup();
                    await rg.WriteColumnAsync(new DataColumn(field, values));
                }

                var loaded = ParquetSimple.LoadCols(path);
                Assert.AreEqual(1, loaded.Count);
                Assert.AreEqual("signal", loaded[0].Name);
                Assert.AreEqual(500, loaded[0].Values.Length);
                CollectionAssert.AreEqual(values, loaded[0].Values);
                Assert.AreEqual(200.0, loaded[0].SamplesPerSecond);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [TestMethod]
        public async Task ParquetSimpleReadsOptionalColumnWrittenByParquetNet()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                // Optional double[] column with one null in the middle.
                var nullable = new double?[] { 1.5, 2.5, null, 4.5, 5.5 };
                var field = new DataField<double?>("optsig");
                var schema = new ParquetSchema(field);

                using (var fs = File.Create(path))
                using (var writer = await ParquetWriter.CreateAsync(schema, fs))
                {
                    using var rg = writer.CreateRowGroup();
                    await rg.WriteColumnAsync(new DataColumn(field, nullable));
                }

                var loaded = ParquetSimple.LoadCols(path);
                Assert.AreEqual(1, loaded.Count);
                Assert.AreEqual("optsig", loaded[0].Name);
                var v = loaded[0].Values;
                Assert.AreEqual(5, v.Length);
                Assert.AreEqual(1.5, v[0]);
                Assert.AreEqual(2.5, v[1]);
                Assert.IsTrue(double.IsNaN(v[2]));
                Assert.AreEqual(4.5, v[3]);
                Assert.AreEqual(5.5, v[4]);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        // -------- Dictionary encoding cross-checks --------

        [DataTestMethod]
        [DataRow(CompressionMethod.None)]
        [DataRow(CompressionMethod.Gzip)]
        [DataRow(CompressionMethod.Snappy)]
        public async Task ParquetNetReadsDictionaryFileWrittenByParquetSimple(CompressionMethod compression)
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                // Low-cardinality column - ParquetSimple's auto-detect will pick dict.
                var rng = new Random(99);
                var palette = new[] { -1.0, 0.0, 1.0, 2.5 };
                var values = Enumerable.Range(0, 800).Select(_ => palette[rng.Next(palette.Length)]).ToArray();

                var pcodec = compression == CompressionMethod.None ? ParquetCompression.Uncompressed
                            : compression == CompressionMethod.Gzip ? ParquetCompression.Gzip
                            : ParquetCompression.Snappy;

                var meta = new List<ParquetSimple.KeyValue>
                {
                    new ParquetSimple.KeyValue("length.signal", "800")
                };
                using (var fs = File.Create(path))
                {
                    ParquetSimple.ParquetWriter.Write(fs, new[] { "signal" }, new[] { values }, 800, meta, pcodec, forceDictionary: true);
                }

                using var stream = File.OpenRead(path);
                using var reader = await ParquetReader.CreateAsync(stream);
                var fields = reader.Schema.GetDataFields();
                Assert.AreEqual(1, fields.Length);
                using var rg = reader.OpenRowGroupReader(0);
                var col = await rg.ReadColumnAsync(fields[0]);
                CollectionAssert.AreEqual(values, (Array)col.Data);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        // -------- Timestamp encoding cross-checks --------

        [TestMethod]
        public async Task ParquetNetReadsTimestampWrittenByParquetSimple()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                // Two-column file: a timestamp .Time column and a paired .Value column.
                // ParquetSimple writes .Time as INT64 + TIMESTAMP_MICROS (sub-ms precision).
                // Parquet.Net 4.x's CLR mapping for TIMESTAMP_MICROS surfaces as INT64
                // rather than DateTime; pyarrow / DuckDB / spark all handle it correctly
                // as a TimestampType. The data on disk is the canonical thing the test
                // verifies: microseconds-since-epoch.
                var times = new[] { 1700000000.0, 1700000001.500, 1700000003.250 };
                var values = new[] { 10.0, 20.0, 30.0 };
                ParquetSimple.SaveCols(
                    path,
                    new[] { "sig.Time", "sig.Value" },
                    new[] { times, values },
                    samplesPerSeconds: null,
                    sampleOffsets: null,
                    isTimestamp: new[] { true, false });

                using var stream = File.OpenRead(path);
                using var reader = await ParquetReader.CreateAsync(stream);
                var fields = reader.Schema.GetDataFields();
                Assert.AreEqual(2, fields.Length);

                var timeField = fields.First(f => f.Name == "sig.Time");
                using var rg = reader.OpenRowGroupReader(0);
                var timeCol = await rg.ReadColumnAsync(timeField);

                // Tolerate either CLR mapping; what we care about is that the value
                // bytes decode to micros-since-epoch.
                long[] micros;
                if (timeCol.Data is long[] longs) micros = longs;
                else if (timeCol.Data is DateTime[] dts)
                {
                    micros = dts.Select(d =>
                        (long)((d - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Ticks / 10)).ToArray();
                }
                else throw new InvalidOperationException($"Unexpected CLR type: {timeCol.Data?.GetType()}");

                Assert.AreEqual(3, micros.Length);
                for (int i = 0; i < times.Length; i++)
                    Assert.AreEqual(times[i], micros[i] / 1_000_000.0, 1e-6, $"ts[{i}]");
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [TestMethod]
        public async Task ParquetSimpleReadsTimestampWrittenByParquetNet()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                // Parquet.Net writes DateTime[] columns as INT64 + TIMESTAMP_MILLIS by default.
                var dts = new[]
                {
                    new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2024, 1, 1, 0, 0, 1, DateTimeKind.Utc).AddMilliseconds(500),
                    new DateTime(2024, 1, 1, 0, 0, 3, DateTimeKind.Utc).AddMilliseconds(250)
                };
                var field = new DataField<DateTime>("when");
                var schema = new ParquetSchema(field);
                using (var fs = File.Create(path))
                using (var writer = await ParquetWriter.CreateAsync(schema, fs))
                {
                    using var rg = writer.CreateRowGroup();
                    await rg.WriteColumnAsync(new DataColumn(field, dts));
                }

                var loaded = ParquetSimple.LoadCols(path);
                Assert.AreEqual(1, loaded.Count);
                var c = loaded[0];
                Assert.AreEqual("when", c.Name);
                Assert.IsTrue(c.IsTimestamp);
                Assert.IsNull(c.Error);
                Assert.AreEqual(3, c.Values.Length);
                for (int i = 0; i < dts.Length; i++)
                {
                    double expected = (dts[i] - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                    Assert.AreEqual(expected, c.Values[i], 1e-3, $"ts[{i}]");
                }
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [TestMethod]
        public async Task ParquetSimpleReadsDictionaryFileWrittenByParquetNet()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                // Parquet.Net 4.x defaults to dictionary encoding for low-cardinality columns.
                // This file should land on RLE_DICTIONARY for the data page and PLAIN for the
                // dictionary page - exactly the format ParquetSimple has to handle for
                // foreign files.
                var rng = new Random(11);
                var palette = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
                var values = Enumerable.Range(0, 600).Select(_ => palette[rng.Next(palette.Length)]).ToArray();

                var field = new DataField<double>("signal");
                var schema = new ParquetSchema(field);
                using (var fs = File.Create(path))
                using (var writer = await ParquetWriter.CreateAsync(schema, fs))
                {
                    writer.CustomMetadata = new Dictionary<string, string> { ["length.signal"] = "600" };
                    using var rg = writer.CreateRowGroup();
                    await rg.WriteColumnAsync(new DataColumn(field, values));
                }

                var loaded = ParquetSimple.LoadCols(path);
                Assert.AreEqual(1, loaded.Count);
                Assert.AreEqual("signal", loaded[0].Name);
                CollectionAssert.AreEqual(values, loaded[0].Values);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }
    }
}
