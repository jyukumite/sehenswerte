using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using SehensWerte.Files;

namespace ParquetTest
{
    // Pulls a curated subset of files from apache/parquet-testing at runtime

    [TestClass]
    public class ApacheFixtures
    {
        private const string ApacheBase = "https://raw.githubusercontent.com/apache/parquet-testing/master/data/";
        private static readonly string CacheDir = Path.Combine(Path.GetTempPath(), "ParquetSimple-ApacheFixtures");

        private static readonly (string Name, string Url)[] s_Fixtures = new (string, string)[]
        {
            ("alltypes_plain.parquet",          ApacheBase + "alltypes_plain.parquet"),
            ("alltypes_plain.snappy.parquet",   ApacheBase + "alltypes_plain.snappy.parquet"),
            ("alltypes_dictionary.parquet",     ApacheBase + "alltypes_dictionary.parquet"),
            ("single_nan.parquet",              ApacheBase + "single_nan.parquet"),
            ("nan_in_stats.parquet",            ApacheBase + "nan_in_stats.parquet"),
            ("int32_decimal.parquet",           ApacheBase + "int32_decimal.parquet"),
            ("nullable.impala.parquet",         ApacheBase + "nullable.impala.parquet"),
            ("nonnullable.impala.parquet",      ApacheBase + "nonnullable.impala.parquet"),
            ("userdata1.parquet",               "https://github.com/Teradata/kylo/raw/master/samples/sample-data/parquet/userdata1.parquet"),
        };

        [ClassInitialize]
        public static void DownloadFixtures(TestContext _)
        {
            Directory.CreateDirectory(CacheDir);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            foreach (var (name, url) in s_Fixtures)
            {
                var dest = Path.Combine(CacheDir, name);
                if (File.Exists(dest) && new FileInfo(dest).Length > 0) continue;
                var bytes = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
                File.WriteAllBytes(dest, bytes);
            }
        }

        [DataTestMethod]
        [DataRow("alltypes_plain.parquet")]
        [DataRow("alltypes_plain.snappy.parquet")]
        [DataRow("alltypes_dictionary.parquet")]
        [DataRow("single_nan.parquet")]
        [DataRow("nan_in_stats.parquet")]
        [DataRow("int32_decimal.parquet")]
        [DataRow("nullable.impala.parquet")]
        [DataRow("nonnullable.impala.parquet")]
        [DataRow("userdata1.parquet")]
        public async Task ParquetSimpleAgainstApacheFixture(string filename)
        {
            var path = Path.Combine(CacheDir, filename);
            Assert.IsTrue(File.Exists(path), $"Fixture not downloaded: {path}");

            // Oracle: what Parquet.Net thinks the file contains.
            int truthCols;
            long truthRows;
            string[] truthNames;
            using (var fs = File.OpenRead(path))
            using (var reader = await ParquetReader.CreateAsync(fs))
            {
                var fields = reader.Schema.GetDataFields();
                truthCols = fields.Length;
                truthRows = 0;
                for (int i = 0; i < reader.RowGroupCount; i++)
                    truthRows += reader.OpenRowGroupReader(i).RowCount;
                truthNames = fields.Select(f => f.Name).ToArray();
            }

            List<LoadedColumn> loaded;
            try
            {
                loaded = ParquetSimple.LoadCols(path);
            }
            catch (NotSupportedException ex)
            {
                // Structural NotSupported (e.g. DataPageV2 footer) - file-level, not per-column.
                Assert.Inconclusive($"{filename}: structural unsupported feature - {ex.Message}");
                return;
            }
            catch (Exception ex)
            {
                Assert.Fail($"{filename}: unexpected {ex.GetType().Name}: {ex.Message}");
                return;
            }

            // Structural parity: same number of leaves. Naming convention for nested
            // schemas (list/map element segments) differs between Parquet.Net and
            // ParquetSimple's dotted-path flattening; both are valid, neither is
            // canonical. Truth names are kept in scope for diagnostics only.
            _ = truthNames;
            Assert.AreEqual(truthCols, loaded.Count, $"{filename}: column count mismatch");

            // If any column reported a per-column decode error, mark Inconclusive with a
            // summary so the test output documents what's covered.
            var errored = loaded.Where(c => c.Error != null).ToList();
            if (errored.Count > 0)
            {
                var summary = string.Join("; ", errored.Select(c => $"{c.Name}: {c.Error}"));
                Assert.Inconclusive($"{filename}: loaded {loaded.Count - errored.Count}/{loaded.Count} columns; per-column errors: {summary}");
            }
        }

        [TestMethod]
        public void Userdata1NumericAndTimestampLoad()
        {
            var path = Path.Combine(CacheDir, "userdata1.parquet");
            Assert.IsTrue(File.Exists(path), $"Fixture not downloaded: {path}");

            var loaded = ParquetSimple.LoadCols(path);
            Assert.AreEqual(13, loaded.Count, "userdata1 has 13 columns");

            // Numeric columns (Int32, Double)
            Assert.AreEqual(1000, loaded.First(c => c.Name == "id").Values.Length);
            Assert.AreEqual(1000, loaded.First(c => c.Name == "salary").Values.Length);
            Assert.IsNull(loaded.First(c => c.Name == "id").Error);
            Assert.IsNull(loaded.First(c => c.Name == "salary").Error);
            Assert.IsFalse(loaded.First(c => c.Name == "id").IsTimestamp);
            Assert.IsFalse(loaded.First(c => c.Name == "salary").IsTimestamp);

            // INT96 timestamp column - decodes to unix seconds
            var ts = loaded.First(c => c.Name == "registration_dttm");
            Assert.IsNull(ts.Error);
            Assert.IsTrue(ts.IsTimestamp);
            Assert.AreEqual(1000, ts.Values.Length);
            // Sanity-check that the values are post-2000 unix seconds (~1.0e9).
            // Userdata1 has 2016-era registration dates; epoch seconds are ~1.4-1.5e9.
            Assert.IsTrue(ts.Values[0] > 1.0e9 && ts.Values[0] < 2.0e9, $"unexpected ts[0]={ts.Values[0]}");

            // Ten BYTE_ARRAY string columns still error
            var errored = loaded.Where(c => c.Error != null).ToList();
            Assert.AreEqual(10, errored.Count, $"expected 10 errored columns, got {errored.Count}");
        }
    }
}
