using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("ParquetTest")]

// Single-file, dependency-free but partial (numeric-only) Apache Parquet reader/writer
// use ParquetSimple.SaveCols/LoadCols for the main API.
// This is not a full Parquet implementation; it can read/write simple numeric columns with some metadata

namespace SehensWerte.Files
{
    public enum ParquetPrimitiveType
    {
        Boolean = 0,
        Int32 = 1,
        Int64 = 2,
        Int96 = 3,
        Float = 4,
        Double = 5,
        ByteArray = 6,
        FixedLenByteArray = 7
    }

    public enum ParquetEncoding
    {
        Plain = 0,
        PlainDictionary = 2,
        Rle = 3,
        BitPacked = 4,
        DeltaBinaryPacked = 5,
        DeltaLengthByteArray = 6,
        DeltaByteArray = 7,
        RleDictionary = 8,
        ByteStreamSplit = 9
    }

    public enum ParquetCompression
    {
        Uncompressed = 0,
        Snappy = 1,
        Gzip = 2,
        Lzo = 3,
        Brotli = 4,
        Lz4 = 5,
        Zstd = 6,
        Lz4Raw = 7
    }

    public enum ParquetRepetition
    {
        Required = 0,
        Optional = 1,
        Repeated = 2
    }

    public sealed class LoadedColumn
    {
        public string Name = "";
        public double[] Values = Array.Empty<double>();
        public double? SamplesPerSecond;
        public int? SampleOffset;
        public string? Error; // Non-null if ParquetSimple could not decode
        public bool IsTimestamp; // values are unix seconds (decoded from INT64/INT96...)
    }

    public static class ParquetSimple
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

            var meta = new List<KeyValue>();
            for (int loop = 0; loop < columns.Count; loop++)
            {
                meta.Add(new KeyValue("length." + names[loop], columns[loop].Length.ToString(CultureInfo.InvariantCulture)));
                if (samplesPerSeconds != null && loop < samplesPerSeconds.Count)
                {
                    meta.Add(new KeyValue("sps." + names[loop], samplesPerSeconds[loop].ToString(CultureInfo.InvariantCulture)));
                }
                if (sampleOffsets != null && loop < sampleOffsets.Count)
                {
                    meta.Add(new KeyValue("offset." + names[loop], sampleOffsets[loop].ToString(CultureInfo.InvariantCulture)));
                }
            }

            using var stream = File.Create(filename);
            ParquetWriter.Write(stream, names, columns, rows, meta, ParquetCompression.Uncompressed, isTimestamp: isTimestamp);
        }

        public static List<LoadedColumn> LoadCols(string filename)
        {
            using var stream = File.OpenRead(filename);
            var (cols, meta) = ParquetReader.Read(stream);

            var metaDict = new Dictionary<string, string>();
            foreach (var kv in meta)
            {
                metaDict[kv.Key] = kv.Value ?? "";
            }
            var result = new List<LoadedColumn>();
            foreach (var col in cols)
            {
                var lc = new LoadedColumn { Name = col.Name, Values = col.Values, Error = col.Error, IsTimestamp = col.IsTimestamp };
                if (metaDict.TryGetValue("sps." + col.Name, out var sps)
                    && double.TryParse(sps, NumberStyles.Any, CultureInfo.InvariantCulture, out var spsVal))
                {
                    lc.SamplesPerSecond = spsVal;
                }
                if (metaDict.TryGetValue("offset." + col.Name, out var off)
                                    && int.TryParse(off, NumberStyles.Any, CultureInfo.InvariantCulture, out var offVal))
                {
                    lc.SampleOffset = offVal;
                }
                if (metaDict.TryGetValue("length." + col.Name, out var lenStr)
                                    && int.TryParse(lenStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var len)
                                    && len >= 0 && len < lc.Values.Length)
                {
                    var trimmed = new double[len];
                    Array.Copy(lc.Values, trimmed, len);
                    lc.Values = trimmed;
                }
                result.Add(lc);
            }
            return result;
        }

        // ---------------------------------------------------------------------------
        // Thrift compact protocol codec.
        // Spec: https://github.com/apache/thrift/blob/master/doc/specs/thrift-compact-protocol.md

        internal const byte TC_STOP = 0;
        internal const byte TC_TRUE = 1;
        internal const byte TC_FALSE = 2;
        internal const byte TC_BYTE = 3;
        internal const byte TC_I16 = 4;
        internal const byte TC_I32 = 5;
        internal const byte TC_I64 = 6;
        internal const byte TC_DOUBLE = 7;
        internal const byte TC_BINARY = 8;
        internal const byte TC_LIST = 9;
        internal const byte TC_SET = 10;
        internal const byte TC_MAP = 11;
        internal const byte TC_STRUCT = 12;

        internal sealed class ThriftWriter
        {
            private readonly Stream m_Stream;
            private readonly Stack<short> m_LastFieldStack = new Stack<short>();
            private short m_LastField = 0;

            public ThriftWriter(Stream stream)
            {
                m_Stream = stream;
            }

            public long Position => m_Stream.Position;

            public void StructBegin()
            {
                m_LastFieldStack.Push(m_LastField);
                m_LastField = 0;
            }

            public void StructEnd()
            {
                m_Stream.WriteByte(TC_STOP);
                m_LastField = m_LastFieldStack.Pop();
            }

            public void FieldBegin(byte type, short id)
            {
                int delta = id - m_LastField;
                if (delta > 0 && delta <= 15)
                {
                    m_Stream.WriteByte((byte)((delta << 4) | type));
                }
                else
                {
                    m_Stream.WriteByte(type);
                    WriteVarint(ZigZag32(id));
                }
                m_LastField = id;
            }

            public void WriteBoolField(short id, bool value)
            {
                int delta = id - m_LastField;
                byte type = value ? TC_TRUE : TC_FALSE;
                if (delta > 0 && delta <= 15)
                {
                    m_Stream.WriteByte((byte)((delta << 4) | type));
                }
                else
                {
                    m_Stream.WriteByte(type);
                    WriteVarint(ZigZag32(id));
                }
                m_LastField = id;
            }

            public void WriteByte(sbyte value) { m_Stream.WriteByte((byte)value); }
            public void WriteByteRaw(byte value) { m_Stream.WriteByte(value); }
            public void WriteI16(short value) { WriteVarint(ZigZag32(value)); }
            public void WriteI32(int value) { WriteVarint(ZigZag32(value)); }
            public void WriteI64(long value) { WriteVarint(ZigZag64(value)); }

            public void WriteDouble(double value)
            {
                Span<byte> buf = stackalloc byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(buf, BitConverter.DoubleToInt64Bits(value));
                m_Stream.Write(buf);
            }

            public void WriteBinary(byte[] bytes)
            {
                WriteVarint((uint)bytes.Length);
                m_Stream.Write(bytes, 0, bytes.Length);
            }

            public void WriteString(string s)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                WriteVarint((uint)bytes.Length);
                m_Stream.Write(bytes, 0, bytes.Length);
            }

            public void ListBegin(byte elementType, int size)
            {
                if (size <= 14)
                {
                    m_Stream.WriteByte((byte)((size << 4) | elementType));
                }
                else
                {
                    m_Stream.WriteByte((byte)(0xF0 | elementType));
                    WriteVarint((uint)size);
                }
            }

            public void WriteVarint(uint value)
            {
                while ((value & ~0x7Fu) != 0)
                {
                    m_Stream.WriteByte((byte)((value & 0x7F) | 0x80));
                    value >>= 7;
                }
                m_Stream.WriteByte((byte)value);
            }

            public void WriteVarint(ulong value)
            {
                while ((value & ~0x7FuL) != 0)
                {
                    m_Stream.WriteByte((byte)((value & 0x7F) | 0x80));
                    value >>= 7;
                }
                m_Stream.WriteByte((byte)value);
            }

            public static uint ZigZag32(int n) => (uint)((n << 1) ^ (n >> 31));
            public static ulong ZigZag64(long n) => (ulong)((n << 1) ^ (n >> 63));
        }

        internal sealed class TReader
        {
            private readonly Stream m_Stream;
            private readonly Stack<short> m_LastFieldStack = new Stack<short>();
            private short m_LastField = 0;

            // pending boolean field value (if last field header was TC_TRUE/TC_FALSE)
            public bool BoolPending;
            public bool BoolValue;

            public TReader(Stream stream)
            {
                m_Stream = stream;
            }

            public long Position => m_Stream.Position;

            public void StructBegin()
            {
                m_LastFieldStack.Push(m_LastField);
                m_LastField = 0;
            }

            public void StructEnd()
            {
                m_LastField = m_LastFieldStack.Pop();
            }

            public (byte type, short id) FieldBegin()
            {
                int b = m_Stream.ReadByte();
                if (b < 0) throw new EndOfStreamException();
                if (b == TC_STOP) return (TC_STOP, 0);
                byte type = (byte)(b & 0x0F);
                int delta = (b & 0xF0) >> 4;
                short id;
                if (delta == 0)
                {
                    id = (short)ReadVarint32();
                }
                else
                {
                    id = (short)(m_LastField + delta);
                }
                m_LastField = id;
                if (type == TC_TRUE)
                {
                    BoolPending = true;
                    BoolValue = true;
                }
                else if (type == TC_FALSE)
                {
                    BoolPending = true;
                    BoolValue = false;
                }
                else
                {
                    BoolPending = false;
                }
                return (type, id);
            }

            public byte ReadByteRaw()
            {
                int b = m_Stream.ReadByte();
                if (b < 0) throw new EndOfStreamException();
                return (byte)b;
            }

            public sbyte ReadByte() => (sbyte)ReadByteRaw();

            public short ReadI16() => (short)ReadVarint32();
            public int ReadI32() => ReadVarint32();
            public long ReadI64() => UnZigZag64(ReadVarint64());

            public double ReadDouble()
            {
                Span<byte> buf = stackalloc byte[8];
                if (m_Stream.Read(buf) != 8) throw new EndOfStreamException();
                return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(buf));
            }

            public byte[] ReadBinary()
            {
                uint len = ReadVarint32U();
                var buf = new byte[len];
                int got = 0;
                while (got < buf.Length)
                {
                    int n = m_Stream.Read(buf, got, buf.Length - got);
                    if (n <= 0) throw new EndOfStreamException();
                    got += n;
                }
                return buf;
            }

            public string ReadString() => Encoding.UTF8.GetString(ReadBinary());

            public (byte type, int size) ListBegin()
            {
                int b = m_Stream.ReadByte();
                if (b < 0) throw new EndOfStreamException();
                byte type = (byte)(b & 0x0F);
                int size = (b & 0xF0) >> 4;
                if (size == 15)
                {
                    size = (int)ReadVarint32U();
                }
                return (type, size);
            }

            // Skip the value of a field given its compact-protocol type code.
            public void Skip(byte type)
            {
                switch (type)
                {
                    case TC_TRUE:
                    case TC_FALSE:
                        break;
                    case TC_BYTE:
                        ReadByteRaw();
                        break;
                    case TC_I16:
                    case TC_I32:
                        ReadVarint32U();
                        break;
                    case TC_I64:
                        ReadVarint64();
                        break;
                    case TC_DOUBLE:
                        Span<byte> tmp = stackalloc byte[8];
                        m_Stream.Read(tmp);
                        break;
                    case TC_BINARY:
                        ReadBinary();
                        break;
                    case TC_LIST:
                    case TC_SET:
                        {
                            var (et, sz) = ListBegin();
                            for (int loop = 0; loop < sz; loop++)
                            {
                                Skip(et);
                            }
                        }
                        break;
                    case TC_MAP:
                        {
                            uint sz = ReadVarint32U();
                            if (sz != 0)
                            {
                                int b = m_Stream.ReadByte();
                                if (b < 0) throw new EndOfStreamException();
                                byte kt = (byte)((b & 0xF0) >> 4);
                                byte vt = (byte)(b & 0x0F);
                                for (uint loop = 0; loop < sz; loop++)
                                {
                                    Skip(kt);
                                    Skip(vt);
                                }
                            }
                        }
                        break;
                    case TC_STRUCT:
                        {
                            StructBegin();
                            while (true)
                            {
                                var (ft, _) = FieldBegin();
                                if (ft == TC_STOP) break;
                                Skip(ft);
                            }
                            StructEnd();
                        }
                        break;
                    default:
                        throw new InvalidDataException($"Thrift skip: unknown type {type}");
                }
            }

            public uint ReadVarint32U()
            {
                uint result = 0;
                int shift = 0;
                while (true)
                {
                    int b = m_Stream.ReadByte();
                    if (b < 0)
                    {
                        throw new EndOfStreamException();
                    }
                    result |= (uint)(b & 0x7F) << shift;
                    if ((b & 0x80) == 0)
                    {
                        return result;
                    }
                    shift += 7;
                    if (shift > 35)
                    {
                        throw new InvalidDataException("varint too long");
                    }
                }
            }

            public int ReadVarint32() => UnZigZag32(ReadVarint32U());

            public ulong ReadVarint64()
            {
                ulong result = 0;
                int shift = 0;
                while (true)
                {
                    int b = m_Stream.ReadByte();
                    if (b < 0)
                    {
                        throw new EndOfStreamException();
                    }
                    result |= (ulong)(b & 0x7F) << shift;
                    if ((b & 0x80) == 0)
                    {
                        return result;
                    }
                    shift += 7;
                    if (shift > 70)
                    {
                        throw new InvalidDataException("varint too long");
                    }
                }
            }

            public static int UnZigZag32(uint n) => (int)(n >> 1) ^ -(int)(n & 1);
            public static long UnZigZag64(ulong n) => (long)(n >> 1) ^ -(long)(n & 1);
        }

        internal sealed class KeyValue
        {
            public string Key = "";
            public string? Value;
            public KeyValue() { }
            public KeyValue(string key, string? value) { Key = key; Value = value; }

            public void Write(ThriftWriter w)
            {
                w.StructBegin();
                w.FieldBegin(TC_BINARY, 1);
                w.WriteString(Key);
                if (Value != null)
                {
                    w.FieldBegin(TC_BINARY, 2);
                    w.WriteString(Value);
                }
                w.StructEnd();
            }

            public static KeyValue Read(TReader r)
            {
                var kv = new KeyValue();
                r.StructBegin();
                while (true)
                {
                    var (t, id) = r.FieldBegin();
                    if (t == TC_STOP) break;
                    switch (id)
                    {
                        case 1: kv.Key = r.ReadString(); break;
                        case 2: kv.Value = r.ReadString(); break;
                        default: r.Skip(t); break;
                    }
                }
                r.StructEnd();
                return kv;
            }
        }

        internal sealed class SchemaElement
        {
            public ParquetPrimitiveType? Type; // 1
            public int? TypeLength; // 2
            public ParquetRepetition? Repetition; // 3
            public string Name = ""; // 4
            public int? NumChildren; // 5
            public int? ConvertedType; // 6 (UTF8 = 0 for strings)
            // 7=scale
            // 8=precision
            // 9=field_id
            public bool IsTimestampLogicalType; // 10 (TIMESTAMP MICROS) When true, also emit a LogicalType.TIMESTAMP{utc=true, unit=MICROS} on field 10

            public void Write(ThriftWriter w)
            {
                w.StructBegin();
                if (Type.HasValue)
                {
                    w.FieldBegin(TC_I32, 1);
                    w.WriteI32((int)Type.Value);
                }
                if (TypeLength.HasValue)
                {
                    w.FieldBegin(TC_I32, 2);
                    w.WriteI32(TypeLength.Value);
                }
                if (Repetition.HasValue)
                {
                    w.FieldBegin(TC_I32, 3);
                    w.WriteI32((int)Repetition.Value);
                }
                w.FieldBegin(TC_BINARY, 4);
                w.WriteString(Name);
                if (NumChildren.HasValue)
                {
                    w.FieldBegin(TC_I32, 5);
                    w.WriteI32(NumChildren.Value);
                }
                if (ConvertedType.HasValue)
                {
                    w.FieldBegin(TC_I32, 6);
                    w.WriteI32(ConvertedType.Value);
                }
                if (IsTimestampLogicalType)
                {
                    // LogicalType union { 8: TimestampType { 1: bool isAdjustedToUTC, 2: TimeUnit { 2: MICROS } } }
                    w.FieldBegin(TC_STRUCT, 10);
                    w.StructBegin();
                    w.FieldBegin(TC_STRUCT, 8); // TIMESTAMP variant of LogicalType
                    w.StructBegin();
                    w.WriteBoolField(1, true); // isAdjustedToUTC
                    w.FieldBegin(TC_STRUCT, 2); // unit
                    w.StructBegin();
                    w.FieldBegin(TC_STRUCT, 2); // MICROS variant
                    w.StructBegin(); // empty MicroSeconds struct
                    w.StructEnd();
                    w.StructEnd();
                    w.StructEnd();
                    w.StructEnd();
                }
                w.StructEnd();
            }

            public static SchemaElement Read(TReader r)
            {
                var s = new SchemaElement();
                r.StructBegin();
                while (true)
                {
                    var (t, id) = r.FieldBegin();
                    if (t == TC_STOP) break;
                    switch (id)
                    {
                        case 1: s.Type = (ParquetPrimitiveType)r.ReadI32(); break;
                        case 2: s.TypeLength = r.ReadI32(); break;
                        case 3: s.Repetition = (ParquetRepetition)r.ReadI32(); break;
                        case 4: s.Name = r.ReadString(); break;
                        case 5: s.NumChildren = r.ReadI32(); break;
                        case 6: s.ConvertedType = r.ReadI32(); break;
                        default: r.Skip(t); break;
                    }
                }
                r.StructEnd();
                return s;
            }
        }

        internal enum PageType { DataPage = 0, IndexPage = 1, DictionaryPage = 2, DataPageV2 = 3 }

        internal sealed class DataPageHeader
        {
            public int NumValues; // 1
            public ParquetEncoding Encoding; // 2
            public ParquetEncoding DefLevelEnc; // 3
            public ParquetEncoding RepLevelEnc; // 4
            // 5 = Statistics (we skip)

            public void Write(ThriftWriter w)
            {
                w.StructBegin();
                w.FieldBegin(TC_I32, 1);
                w.WriteI32(NumValues);
                w.FieldBegin(TC_I32, 2);
                w.WriteI32((int)Encoding);
                w.FieldBegin(TC_I32, 3);
                w.WriteI32((int)DefLevelEnc);
                w.FieldBegin(TC_I32, 4);
                w.WriteI32((int)RepLevelEnc);
                w.StructEnd();
            }

            public static DataPageHeader Read(TReader r)
            {
                var d = new DataPageHeader();
                r.StructBegin();
                while (true)
                {
                    var (t, id) = r.FieldBegin();
                    if (t == TC_STOP) break;
                    switch (id)
                    {
                        case 1: d.NumValues = r.ReadI32(); break;
                        case 2: d.Encoding = (ParquetEncoding)r.ReadI32(); break;
                        case 3: d.DefLevelEnc = (ParquetEncoding)r.ReadI32(); break;
                        case 4: d.RepLevelEnc = (ParquetEncoding)r.ReadI32(); break;
                        default: r.Skip(t); break;
                    }
                }
                r.StructEnd();
                return d;
            }
        }

        internal sealed class DataPageHeaderV2
        {
            public int NumValues; // 1
            public int NumNulls; // 2
            public int NumRows; // 3
            public ParquetEncoding Enc; // 4
            public int DefLevelByteLen; // 5
            public int RepLevelByteLen; // 6
            public bool IsCompressed = true; // 7

            public static DataPageHeaderV2 Read(TReader r)
            {
                var d = new DataPageHeaderV2();
                r.StructBegin();
                while (true)
                {
                    var (t, id) = r.FieldBegin();
                    if (t == TC_STOP) break;
                    switch (id)
                    {
                        case 1: d.NumValues = r.ReadI32(); break;
                        case 2: d.NumNulls = r.ReadI32(); break;
                        case 3: d.NumRows = r.ReadI32(); break;
                        case 4: d.Enc = (ParquetEncoding)r.ReadI32(); break;
                        case 5: d.DefLevelByteLen = r.ReadI32(); break;
                        case 6: d.RepLevelByteLen = r.ReadI32(); break;
                        case 7: d.IsCompressed = r.BoolValue; break;
                        default: r.Skip(t); break;
                    }
                }
                r.StructEnd();
                return d;
            }
        }

        internal sealed class DictionaryPageHeader
        {
            public int NumValues; // 1
            public ParquetEncoding Encoding; // 2 (PLAIN or PLAIN_DICTIONARY)
            // 3 = is_sorted, skipped

            public void Write(ThriftWriter w)
            {
                w.StructBegin();
                w.FieldBegin(TC_I32, 1);
                w.WriteI32(NumValues);
                w.FieldBegin(TC_I32, 2);
                w.WriteI32((int)Encoding);
                w.StructEnd();
            }

            public static DictionaryPageHeader Read(TReader r)
            {
                var d = new DictionaryPageHeader();
                r.StructBegin();
                while (true)
                {
                    var (t, id) = r.FieldBegin();
                    if (t == TC_STOP) break;
                    switch (id)
                    {
                        case 1: d.NumValues = r.ReadI32(); break;
                        case 2: d.Encoding = (ParquetEncoding)r.ReadI32(); break;
                        default: r.Skip(t); break;
                    }
                }
                r.StructEnd();
                return d;
            }
        }

        internal sealed class PageHeader
        {
            public PageType Type; // 1
            public int UncompressedPageSize; // 2
            public int CompressedPageSize; // 3
            // 4 = crc, skipped
            public DataPageHeader? DataHeader; // 5
            public DictionaryPageHeader? DictHeader; // 7
            public DataPageHeaderV2? DataHeaderV2; // 8

            public void Write(ThriftWriter w)
            {
                w.StructBegin();
                w.FieldBegin(TC_I32, 1);
                w.WriteI32((int)Type);
                w.FieldBegin(TC_I32, 2);
                w.WriteI32(UncompressedPageSize);
                w.FieldBegin(TC_I32, 3);
                w.WriteI32(CompressedPageSize);
                if (DataHeader != null)
                {
                    w.FieldBegin(TC_STRUCT, 5);
                    DataHeader.Write(w);
                }
                if (DictHeader != null)
                {
                    w.FieldBegin(TC_STRUCT, 7);
                    DictHeader.Write(w);
                }
                w.StructEnd();
            }

            public static PageHeader Read(TReader r)
            {
                var p = new PageHeader();
                r.StructBegin();
                while (true)
                {
                    var (t, id) = r.FieldBegin();
                    if (t == TC_STOP) break;
                    switch (id)
                    {
                        case 1: p.Type = (PageType)r.ReadI32(); break;
                        case 2: p.UncompressedPageSize = r.ReadI32(); break;
                        case 3: p.CompressedPageSize = r.ReadI32(); break;
                        case 5: p.DataHeader = DataPageHeader.Read(r); break;
                        case 7: p.DictHeader = DictionaryPageHeader.Read(r); break;
                        case 8: p.DataHeaderV2 = DataPageHeaderV2.Read(r); break;
                        default: r.Skip(t); break;
                    }
                }
                r.StructEnd();
                return p;
            }
        }

        internal sealed class ColumnMetaData
        {
            public ParquetPrimitiveType Type; // 1
            public List<ParquetEncoding> Encodings = new(); // 2
            public List<string> PathInSchema = new(); // 3
            public ParquetCompression Codec; // 4
            public long NumValues; // 5
            public long TotalUncompressedSize; // 6
            public long TotalCompressedSize; // 7
            // 8 = key_value_metadata
            public long DataPageOffset; // 9
            // 10 = index_page_offset, 12 = statistics
            public long DictionaryPageOffset; // 11 (0 == not present)

            public void Write(ThriftWriter w)
            {
                w.StructBegin();
                w.FieldBegin(TC_I32, 1);
                w.WriteI32((int)Type);
                w.FieldBegin(TC_LIST, 2);
                w.ListBegin(TC_I32, Encodings.Count);
                foreach (var e in Encodings)
                {
                    w.WriteI32((int)e);
                }
                w.FieldBegin(TC_LIST, 3);
                w.ListBegin(TC_BINARY, PathInSchema.Count);
                foreach (var p in PathInSchema)
                {
                    w.WriteString(p);
                }
                w.FieldBegin(TC_I32, 4);
                w.WriteI32((int)Codec);
                w.FieldBegin(TC_I64, 5);
                w.WriteI64(NumValues);
                w.FieldBegin(TC_I64, 6);
                w.WriteI64(TotalUncompressedSize);
                w.FieldBegin(TC_I64, 7);
                w.WriteI64(TotalCompressedSize);
                w.FieldBegin(TC_I64, 9);
                w.WriteI64(DataPageOffset);
                if (DictionaryPageOffset > 0)
                {
                    w.FieldBegin(TC_I64, 11);
                    w.WriteI64(DictionaryPageOffset);
                }
                w.StructEnd();
            }

            public static ColumnMetaData Read(TReader r)
            {
                var c = new ColumnMetaData();
                r.StructBegin();
                while (true)
                {
                    var (t, id) = r.FieldBegin();
                    if (t == TC_STOP) break;
                    switch (id)
                    {
                        case 1: c.Type = (ParquetPrimitiveType)r.ReadI32(); break;
                        case 2:
                            {
                                var (type, size) = r.ListBegin();
                                for (int loop = 0; loop < size; loop++)
                                {
                                    c.Encodings.Add((ParquetEncoding)r.ReadI32());
                                }
                            }
                            break;
                        case 3:
                            {
                                var (type, size) = r.ListBegin();
                                for (int loop = 0; loop < size; loop++)
                                {
                                    c.PathInSchema.Add(r.ReadString());
                                }
                            }
                            break;
                        case 4: c.Codec = (ParquetCompression)r.ReadI32(); break;
                        case 5: c.NumValues = r.ReadI64(); break;
                        case 6: c.TotalUncompressedSize = r.ReadI64(); break;
                        case 7: c.TotalCompressedSize = r.ReadI64(); break;
                        case 9: c.DataPageOffset = r.ReadI64(); break;
                        case 11: c.DictionaryPageOffset = r.ReadI64(); break;
                        default: r.Skip(t); break;
                    }
                }
                r.StructEnd();
                return c;
            }
        }

        internal sealed class ColumnChunk
        {
            // 1 = file_path (optional, we omit)
            public long FileOffset; // 2
            public ColumnMetaData? MetaData; // 3

            public void Write(ThriftWriter w)
            {
                w.StructBegin();
                w.FieldBegin(TC_I64, 2);
                w.WriteI64(FileOffset);
                if (MetaData != null)
                {
                    w.FieldBegin(TC_STRUCT, 3);
                    MetaData.Write(w);
                }
                w.StructEnd();
            }

            public static ColumnChunk Read(TReader r)
            {
                var c = new ColumnChunk();
                r.StructBegin();
                while (true)
                {
                    var (type, id) = r.FieldBegin();
                    if (type == TC_STOP) break;
                    switch (id)
                    {
                        case 2: c.FileOffset = r.ReadI64(); break;
                        case 3: c.MetaData = ColumnMetaData.Read(r); break;
                        default: r.Skip(type); break;
                    }
                }
                r.StructEnd();
                return c;
            }
        }

        internal sealed class RowGroup
        {
            public List<ColumnChunk> Columns = new(); // 1
            public long TotalByteSize; // 2
            public long NumRows; // 3

            public void Write(ThriftWriter w)
            {
                w.StructBegin();
                w.FieldBegin(TC_LIST, 1);
                w.ListBegin(TC_STRUCT, Columns.Count);
                foreach (var col in Columns)
                {
                    col.Write(w);
                }
                w.FieldBegin(TC_I64, 2);
                w.WriteI64(TotalByteSize);
                w.FieldBegin(TC_I64, 3);
                w.WriteI64(NumRows);
                w.StructEnd();
            }

            public static RowGroup Read(TReader r)
            {
                var g = new RowGroup();
                r.StructBegin();
                while (true)
                {
                    var (type, id) = r.FieldBegin();
                    if (type == TC_STOP) break;
                    switch (id)
                    {
                        case 1:
                            {
                                var (_, size) = r.ListBegin();
                                for (int loop = 0; loop < size; loop++)
                                {
                                    g.Columns.Add(ColumnChunk.Read(r));
                                }
                            }
                            break;
                        case 2: g.TotalByteSize = r.ReadI64(); break;
                        case 3: g.NumRows = r.ReadI64(); break;
                        default: r.Skip(type); break;
                    }
                }
                r.StructEnd();
                return g;
            }
        }

        internal sealed class FileMetaData
        {
            public int Version = 1; // 1
            public List<SchemaElement> Schema = new(); // 2
            public long NumRows; // 3
            public List<RowGroup> RowGroups = new(); // 4
            public List<KeyValue> KeyValueMetadata = new(); // 5
            public string? CreatedBy; // 6

            public void Write(ThriftWriter w)
            {
                w.StructBegin();
                w.FieldBegin(TC_I32, 1);
                w.WriteI32(Version);
                w.FieldBegin(TC_LIST, 2);
                w.ListBegin(TC_STRUCT, Schema.Count);
                foreach (var s in Schema)
                {
                    s.Write(w);
                }
                w.FieldBegin(TC_I64, 3);
                w.WriteI64(NumRows);
                w.FieldBegin(TC_LIST, 4);
                w.ListBegin(TC_STRUCT, RowGroups.Count);
                foreach (var g in RowGroups)
                {
                    g.Write(w);
                }
                if (KeyValueMetadata.Count > 0)
                {
                    w.FieldBegin(TC_LIST, 5);
                    w.ListBegin(TC_STRUCT, KeyValueMetadata.Count);
                    foreach (var kv in KeyValueMetadata)
                    {
                        kv.Write(w);
                    }
                }
                if (CreatedBy != null)
                {
                    w.FieldBegin(TC_BINARY, 6);
                    w.WriteString(CreatedBy);
                }
                w.StructEnd();
            }

            public static FileMetaData Read(TReader r)
            {
                var m = new FileMetaData();
                r.StructBegin();
                while (true)
                {
                    var (type, id) = r.FieldBegin();
                    if (type == TC_STOP) break;
                    switch (id)
                    {
                        case 1: m.Version = r.ReadI32(); break;
                        case 2:
                            {
                                var (_, size) = r.ListBegin();
                                for (int loop = 0; loop < size; loop++)
                                {
                                    m.Schema.Add(SchemaElement.Read(r));
                                }
                            }
                            break;
                        case 3: m.NumRows = r.ReadI64(); break;
                        case 4:
                            {
                                var (_, size) = r.ListBegin();
                                for (int loop = 0; loop < size; loop++)
                                {
                                    m.RowGroups.Add(RowGroup.Read(r));
                                }
                            }
                            break;
                        case 5:
                            {
                                var (_, sz) = r.ListBegin();
                                for (int loop = 0; loop < sz; loop++)
                                {
                                    m.KeyValueMetadata.Add(KeyValue.Read(r));
                                }
                            }
                            break;
                        case 6: m.CreatedBy = r.ReadString(); break;
                        default: r.Skip(type); break;
                    }
                }
                r.StructEnd();
                return m;
            }
        }

        /// xxxxxxxxxxxxxxxxxxx
        internal static class Codec
        {
            public static byte[] Compress(byte[] data, ParquetCompression codec)
            {
                switch (codec)
                {
                    case ParquetCompression.Uncompressed:
                        return data;
                    case ParquetCompression.Gzip:
                        {
                            using var ms = new MemoryStream();
                            using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                            {
                                gz.Write(data, 0, data.Length);
                            }
                            return ms.ToArray();
                        }
                    case ParquetCompression.Snappy:
                        return SnappyCompressLiteral(data);
                    case ParquetCompression.Zstd:
                        {
                            // Default level (3) - what parquet-mr / pyarrow / DuckDB use
                            // unless overridden. Good speed vs ratio balance.
                            using var compressor = new ZstdSharp.Compressor();
                            return compressor.Wrap(data).ToArray();
                        }
                    default:
                        throw new NotSupportedException($"ParquetSimple does not write codec {codec}; use Uncompressed, Gzip, Snappy, or Zstd");
                }
            }

            public static byte[] Decompress(byte[] data, ParquetCompression codec, int uncompressedSize)
            {
                switch (codec)
                {
                    case ParquetCompression.Uncompressed:
                        return data;
                    case ParquetCompression.Gzip:
                        {
                            using var ms = new MemoryStream(data);
                            using var gz = new GZipStream(ms, CompressionMode.Decompress);
                            using var outMs = new MemoryStream(uncompressedSize > 0 ? uncompressedSize : 4096);
                            gz.CopyTo(outMs);
                            return outMs.ToArray();
                        }
                    case ParquetCompression.Snappy:
                        return SnappyDecompress(data);
                    case ParquetCompression.Zstd:
                        {
                            using var decompressor = new ZstdSharp.Decompressor();
                            return decompressor.Unwrap(data).ToArray();
                        }
                    default:
                        throw new NotSupportedException($"ParquetSimple does not read codec {codec}; re-export with Uncompressed, Gzip, Snappy, or Zstd");
                }
            }

            // Spec-legal Snappy: a single literal covering all input. Slightly larger than
            // an LZ77-driven encoder but trivially correct and decodable by any reader.
            internal static byte[] SnappyCompressLiteral(byte[] input)
            {
                using var ms = new MemoryStream();
                // varint of uncompressed length
                uint len = (uint)input.Length;
                while ((len & ~0x7Fu) != 0)
                {
                    ms.WriteByte((byte)((len & 0x7F) | 0x80)); len >>= 7;
                }
                ms.WriteByte((byte)len);
                // literal tag
                int litLen = input.Length;
                if (litLen == 0)
                {
                    // no literal bytes to emit
                }
                else if (litLen <= 60)
                {
                    ms.WriteByte((byte)(((litLen - 1) << 2) | 0x00));
                    ms.Write(input, 0, input.Length);
                }
                else
                {
                    int lenMinus1 = litLen - 1;
                    int extra = lenMinus1 < 0x100 ? 1 : lenMinus1 < 0x10000 ? 2 : lenMinus1 < 0x1000000 ? 3 : 4;
                    byte tag = (byte)(((59 + extra) << 2) | 0x00);
                    ms.WriteByte(tag);
                    for (int loop = 0; loop < extra; loop++)
                    {
                        ms.WriteByte((byte)((lenMinus1 >> (8 * loop)) & 0xFF));
                    }
                    ms.Write(input, 0, input.Length);
                }
                return ms.ToArray();
            }

            internal static byte[] SnappyDecompress(byte[] input)
            {
                int pos = 0;
                // varint uncompressed length
                int outLen = 0;
                int shift = 0;
                while (true)
                {
                    if (pos >= input.Length) throw new InvalidDataException("Snappy: truncated length");
                    int b = input[pos++];
                    outLen |= (b & 0x7F) << shift;
                    if ((b & 0x80) == 0) break;
                    shift += 7;
                    if (shift > 35) throw new InvalidDataException("Snappy: length too long");
                }
                var output = new byte[outLen];
                int outPos = 0;
                while (pos < input.Length)
                {
                    byte tag = input[pos++];
                    int kind = tag & 0x03;
                    if (kind == 0) // literal
                    {
                        int litLen = tag >> 2;
                        if (litLen < 60)
                        {
                            litLen++;
                        }
                        else
                        {
                            int extra = litLen - 59;
                            int lenMinus1 = 0;
                            for (int loop = 0; loop < extra; loop++)
                            {
                                lenMinus1 |= input[pos++] << (8 * loop);
                            }
                            litLen = lenMinus1 + 1;
                        }
                        Buffer.BlockCopy(input, pos, output, outPos, litLen);
                        pos += litLen;
                        outPos += litLen;
                    }
                    else if (kind == 1) // copy 1-byte offset
                    {
                        int copyLen = ((tag >> 2) & 0x07) + 4;
                        int offset = ((tag >> 5) << 8) | input[pos++];
                        SnappyCopy(output, outPos, offset, copyLen);
                        outPos += copyLen;
                    }
                    else if (kind == 2) // copy 2-byte offset
                    {
                        int copyLen = (tag >> 2) + 1;
                        int offset = input[pos] | (input[pos + 1] << 8);
                        pos += 2;
                        SnappyCopy(output, outPos, offset, copyLen);
                        outPos += copyLen;
                    }
                    else // copy 4-byte offset
                    {
                        int copyLen = (tag >> 2) + 1;
                        int offset = input[pos] | (input[pos + 1] << 8) | (input[pos + 2] << 16) | (input[pos + 3] << 24);
                        pos += 4;
                        SnappyCopy(output, outPos, offset, copyLen);
                        outPos += copyLen;
                    }
                }
                if (outPos != outLen) throw new InvalidDataException("Snappy: short output");
                return output;
            }

            private static void SnappyCopy(byte[] output, int outPos, int offset, int copyLen)
            {
                // overlapping copies: byte-by-byte to support back-references shorter than length
                int src = outPos - offset;
                if (src < 0) throw new InvalidDataException("Snappy: bad offset");
                for (int loop = 0; loop < copyLen; loop++)
                {
                    output[outPos + loop] = output[src + loop];
                }
            }
        }

        internal static class LevelReader
        {
            // bitWidth: number of bits per level value. count: total number of values.
            // Returns the decoded levels array.
            public static int[] ReadHybrid(byte[] buffer, int offset, int byteLen, int bitWidth, int count)
            {
                var result = new int[count];
                if (bitWidth == 0) return result; // all zeros

                int pos = offset;
                int endPos = offset + byteLen;
                int written = 0;

                while (written < count && pos < endPos)
                {
                    // read run header (unsigned varint)
                    int header = 0;
                    int shift = 0;
                    while (true)
                    {
                        if (pos >= endPos) throw new InvalidDataException("levels: truncated header");
                        int b = buffer[pos++];
                        header |= (b & 0x7F) << shift;
                        if ((b & 0x80) == 0) break;
                        shift += 7;
                    }
                    bool bitPacked = (header & 1) != 0;
                    int runLen = header >> 1;

                    if (bitPacked)
                    {
                        // bit-packed run: runLen groups of 8 values
                        int values = runLen * 8;
                        int bitsTotal = values * bitWidth;
                        int bytesNeeded = (bitsTotal + 7) / 8;
                        if (pos + bytesNeeded > endPos) throw new InvalidDataException("levels: truncated bitpacked");
                        int bitOffset = 0;
                        for (int v = 0; v < values && written < count; v++)
                        {
                            int byteIdx = pos + (bitOffset / 8);
                            int bitIdx = bitOffset & 7;
                            int val = 0;
                            int bitsTaken = 0;
                            while (bitsTaken < bitWidth)
                            {
                                int avail = 8 - bitIdx;
                                int take = Math.Min(avail, bitWidth - bitsTaken);
                                int chunk = (buffer[byteIdx] >> bitIdx) & ((1 << take) - 1);
                                val |= chunk << bitsTaken;
                                bitsTaken += take;
                                bitOffset += take;
                                byteIdx = pos + (bitOffset / 8);
                                bitIdx = bitOffset & 7;
                            }
                            result[written++] = val;
                        }
                        pos += bytesNeeded;
                    }
                    else
                    {
                        // RLE run: value packed in ceil(bitWidth/8) bytes little-endian
                        int valBytes = (bitWidth + 7) / 8;
                        if (pos + valBytes > endPos) throw new InvalidDataException("levels: truncated rle");
                        int rleVal = 0;
                        for (int loop = 0; loop < valBytes; loop++)
                        {
                            rleVal |= buffer[pos + loop] << (8 * loop);
                        }
                        pos += valBytes;
                        for (int loop = 0; loop < runLen && written < count; loop++)
                        {
                            result[written++] = rleVal;
                        }
                    }
                }
                return result;
            }

            public static int BitWidth(int maxLevel)
            {
                int w = 0;
                while (maxLevel != 0)
                {
                    w++;
                    maxLevel >>= 1;
                }
                return w;
            }

            // Emit a single bit-packed-hybrid run covering all values. Used for the
            // dictionary-encoded-data-page indices. ReadHybrid consumes whatever this
            // emits, so the two are tested against each other in self round-trips.
            public static byte[] WriteSingleBitPackedRun(int[] values, int bitWidth)
            {
                if (bitWidth == 0)
                {
                    // RLE run of zeros, no value bytes follow
                    using var ms0 = new MemoryStream();
                    WriteVarint(ms0, (uint)values.Length << 1);
                    return ms0.ToArray();
                }
                int numGroups = (values.Length + 7) / 8;
                int totalValues = numGroups * 8;
                int bytesNeeded = (totalValues * bitWidth + 7) / 8;
                using var ms = new MemoryStream();
                WriteVarint(ms, (uint)((numGroups << 1) | 1));
                var packed = new byte[bytesNeeded];
                int bitOffset = 0;
                for (int v = 0; v < values.Length; v++)
                {
                    int val = values[v] & (int)((1L << bitWidth) - 1);
                    int remaining = bitWidth;
                    while (remaining > 0)
                    {
                        int byteIdx = bitOffset / 8;
                        int bitIdx = bitOffset & 7;
                        int avail = 8 - bitIdx;
                        int take = Math.Min(avail, remaining);
                        packed[byteIdx] |= (byte)((val & ((1 << take) - 1)) << bitIdx);
                        val >>= take;
                        remaining -= take;
                        bitOffset += take;
                    }
                }
                ms.Write(packed, 0, packed.Length);
                return ms.ToArray();
            }

            private static void WriteVarint(Stream stream, uint value)
            {
                while ((value & ~0x7Fu) != 0)
                {
                    stream.WriteByte((byte)((value & 0x7F) | 0x80));
                    value >>= 7;
                }
                stream.WriteByte((byte)value);
            }
        }

        internal static class Plain
        {
            public static byte[] EncodeDouble(double[] values)
            {
                var buf = new byte[values.Length * 8];
                for (int loop = 0; loop < values.Length; loop++)
                {
                    BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(loop * 8, 8), BitConverter.DoubleToInt64Bits(values[loop]));
                }
                return buf;
            }

            public static byte[] EncodeInt64(long[] values)
            {
                var buf = new byte[values.Length * 8];
                for (int loop = 0; loop < values.Length; loop++)
                {
                    BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(loop * 8, 8), values[loop]);
                }
                return buf;
            }

            // INT96 (deprecated parquet timestamp): 8 bytes nanos-of-day little-endian
            // followed by 4 bytes Julian-day little-endian. Returns unix seconds (double).
            public static double[] DecodeInt96AsTimestamp(byte[] buf, int offset, int byteLen, int count)
            {
                if (byteLen < count * 12) throw new InvalidDataException("PLAIN int96: short buffer");
                var values = new double[count];
                for (int loop = 0; loop < count; loop++)
                {
                    int p = offset + loop * 12;
                    long nanosOfDay = BinaryPrimitives.ReadInt64LittleEndian(buf.AsSpan(p, 8));
                    int julianDay = BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(p + 8, 4));
                    long daysSinceEpoch = julianDay - 2440588L; // Julian day for 1970-01-01
                    values[loop] = daysSinceEpoch * 86400.0 + nanosOfDay / 1e9;
                }
                return values;
            }

            public static double[] DecodeDouble(byte[] buf, int offset, int byteLen, int count)
            {
                if (byteLen < count * 8) throw new InvalidDataException("PLAIN double: short buffer");
                var values = new double[count];
                for (int loop = 0; loop < count; loop++)
                {
                    long bits = BinaryPrimitives.ReadInt64LittleEndian(buf.AsSpan(offset + loop * 8, 8));
                    values[loop] = BitConverter.Int64BitsToDouble(bits);
                }
                return values;
            }

            public static double[] DecodeFloat(byte[] buf, int offset, int byteLen, int count)
            {
                if (byteLen < count * 4) throw new InvalidDataException("PLAIN float: short buffer");
                var values = new double[count];
                for (int loop = 0; loop < count; loop++)
                {
                    int bits = BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(offset + loop * 4, 4));
                    values[loop] = BitConverter.Int32BitsToSingle(bits);
                }
                return values;
            }

            public static double[] DecodeInt32(byte[] buf, int offset, int byteLen, int count)
            {
                if (byteLen < count * 4) throw new InvalidDataException("PLAIN int32: short buffer");
                var values = new double[count];
                for (int loop = 0; loop < count; loop++)
                {
                    values[loop] = BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(offset + loop * 4, 4));
                }
                return values;
            }

            public static double[] DecodeInt64(byte[] buf, int offset, int byteLen, int count)
            {
                if (byteLen < count * 8) throw new InvalidDataException("PLAIN int64: short buffer");
                var values = new double[count];
                for (int loop = 0; loop < count; loop++)
                {
                    values[loop] = BinaryPrimitives.ReadInt64LittleEndian(buf.AsSpan(offset + loop * 8, 8));
                }
                return values;
            }

            public static double[] DecodeBoolean(byte[] buf, int offset, int byteLen, int count)
            {
                var values = new double[count];
                for (int loop = 0; loop < count; loop++)
                {
                    int byteIdx = offset + (loop / 8);
                    int bitIdx = loop & 7;
                    values[loop] = ((buf[byteIdx] >> bitIdx) & 1) != 0 ? 1.0 : 0.0;
                }
                return values;
            }
        }

        internal static class ParquetWriter
        {
            private static readonly byte[] s_Magic = Encoding.ASCII.GetBytes("PAR1");

            public static void Write(
                Stream stream,
                IReadOnlyList<string> names,
                IReadOnlyList<double[]> columns,
                int rows,
                List<KeyValue> fileMetadata,
                ParquetCompression codec,
                bool? forceDictionary = null,
                IReadOnlyList<bool>? isTimestamp = null)
            {
                stream.Write(s_Magic, 0, 4);

                var chunkInfos = new List<ChunkInfo>();

                for (int loop = 0; loop < columns.Count; loop++)
                {
                    bool ts = isTimestamp != null && loop < isTimestamp.Count && isTimestamp[loop];
                    double[] col = columns[loop];
                    double[] padded;
                    if (col.Length == rows)
                    {
                        padded = col;
                    }
                    else
                    {
                        padded = new double[rows];
                        Array.Copy(col, padded, col.Length);
                        for (int k = col.Length; k < rows; k++)
                        {
                            padded[k] = double.NaN;
                        }
                    }

                    if (ts)
                    {
                        // Timestamps written as INT64 PLAIN with TIMESTAMP_MILLIS converted_type.
                        // Auto-dictionary skipped: timestamps are usually high-cardinality,
                        // and dict encoding would just bloat the file.
                        chunkInfos.Add(WriteTimestampColumn(stream, padded, codec));
                        continue;
                    }

                    bool useDict = forceDictionary ?? ShouldUseDictionary(padded);
                    if (useDict)
                    {
                        chunkInfos.Add(WriteDictColumn(stream, padded, codec));
                    }
                    else
                    {
                        chunkInfos.Add(WritePlainColumn(stream, padded, codec));
                    }
                }

                var fileMd = new FileMetaData
                {
                    Version = 1,
                    NumRows = rows,
                    CreatedBy = "ParquetSimple",
                    KeyValueMetadata = fileMetadata
                };
                fileMd.Schema.Add(new SchemaElement
                {
                    Name = "schema",
                    NumChildren = columns.Count
                });
                for (int loop = 0; loop < columns.Count; loop++)
                {
                    var info = chunkInfos[loop];
                    var schemaLeaf = new SchemaElement
                    {
                        Name = names[loop],
                        Type = info.PrimitiveType,
                        Repetition = ParquetRepetition.Required
                    };
                    if (info.IsTimestamp)
                    {
                        schemaLeaf.ConvertedType = ParquetReader.CT_TIMESTAMP_MICROS;
                        schemaLeaf.IsTimestampLogicalType = true;
                    }
                    fileMd.Schema.Add(schemaLeaf);
                }

                var rg = new RowGroup
                {
                    NumRows = rows,
                    TotalByteSize = chunkInfos.Sum(c => c.UncompSize)
                };
                for (int loop = 0; loop < columns.Count; loop++)
                {
                    var info = chunkInfos[loop];
                    var cm = new ColumnMetaData
                    {
                        Type = info.PrimitiveType,
                        Codec = info.Codec,
                        NumValues = info.NumValues,
                        TotalUncompressedSize = info.UncompSize,
                        TotalCompressedSize = info.CompSize,
                        DataPageOffset = info.DataPageOffset,
                        DictionaryPageOffset = info.DictPageOffset
                    };
                    if (info.DictPageOffset > 0)
                    {
                        // Modern layout: dictionary page encoded as PLAIN, data page as RLE_DICTIONARY.
                        cm.Encodings.Add(ParquetEncoding.Plain);
                        cm.Encodings.Add(ParquetEncoding.RleDictionary);
                    }
                    else
                    {
                        cm.Encodings.Add(ParquetEncoding.Plain);
                    }
                    cm.PathInSchema.Add(names[loop]);
                    rg.Columns.Add(new ColumnChunk
                    {
                        FileOffset = info.DictPageOffset > 0 ? info.DictPageOffset : info.DataPageOffset,
                        MetaData = cm
                    });
                }
                fileMd.RowGroups.Add(rg);

                long footerStart = stream.Position;
                fileMd.Write(new ThriftWriter(stream));
                long footerLen = stream.Position - footerStart;

                Span<byte> lenBuf = stackalloc byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(lenBuf, (int)footerLen);
                stream.Write(lenBuf);
                stream.Write(s_Magic, 0, 4);
            }

            private struct ChunkInfo
            {
                public long DataPageOffset;
                public long DictPageOffset; // 0 if no dictionary
                public long UncompSize;
                public long CompSize;
                public int NumValues;
                public ParquetCompression Codec;
                public ParquetPrimitiveType PrimitiveType; // Double or Int64
                public bool IsTimestamp; // sets schema converted_type = TIMESTAMP_MILLIS
            }

            // dictionary-encode when at least half the values are duplicates
            // and we have at least 8 rows (below that, page-header + dict-page overhead
            // dwarfs any savings). NaNs are bit-equal to themselves under bit-key lookup,
            // so a column of all NaN compresses cleanly through this path.
            private static bool ShouldUseDictionary(double[] padded)
            {
                if (padded.Length < 8) return false;
                var seen = new HashSet<long>(padded.Length);
                int threshold = padded.Length / 2;
                for (int loop = 0; loop < padded.Length; loop++)
                {
                    seen.Add(BitConverter.DoubleToInt64Bits(padded[loop]));
                    if (seen.Count > threshold) return false;
                }
                return seen.Count > 0;
            }

            private static ChunkInfo WritePlainColumn(Stream stream, double[] padded, ParquetCompression codec)
            {
                byte[] uncompressed = Plain.EncodeDouble(padded);
                byte[] compressed = Codec.Compress(uncompressed, codec);

                var page = new PageHeader
                {
                    Type = PageType.DataPage,
                    UncompressedPageSize = uncompressed.Length,
                    CompressedPageSize = compressed.Length,
                    DataHeader = new DataPageHeader
                    {
                        NumValues = padded.Length,
                        Encoding = ParquetEncoding.Plain,
                        DefLevelEnc = ParquetEncoding.Rle,
                        RepLevelEnc = ParquetEncoding.Rle
                    }
                };

                using var headerMs = new MemoryStream();
                page.Write(new ThriftWriter(headerMs));
                byte[] headerBytes = headerMs.ToArray();

                long pageOffset = stream.Position;
                stream.Write(headerBytes, 0, headerBytes.Length);
                stream.Write(compressed, 0, compressed.Length);

                return new ChunkInfo
                {
                    DataPageOffset = pageOffset,
                    DictPageOffset = 0,
                    UncompSize = headerBytes.Length + (long)uncompressed.Length,
                    CompSize = headerBytes.Length + (long)compressed.Length,
                    NumValues = padded.Length,
                    Codec = codec,
                    PrimitiveType = ParquetPrimitiveType.Double
                };
            }

            private static ChunkInfo WriteTimestampColumn(Stream stream, double[] padded, ParquetCompression codec)
            {
                var int64s = new long[padded.Length];
                for (int loop = 0; loop < padded.Length; loop++)
                {
                    int64s[loop] = (long)Math.Round(padded[loop] * 1_000_000.0);
                }
                byte[] uncompressed = Plain.EncodeInt64(int64s);
                byte[] compressed = Codec.Compress(uncompressed, codec);

                var page = new PageHeader
                {
                    Type = PageType.DataPage,
                    UncompressedPageSize = uncompressed.Length,
                    CompressedPageSize = compressed.Length,
                    DataHeader = new DataPageHeader
                    {
                        NumValues = padded.Length,
                        Encoding = ParquetEncoding.Plain,
                        DefLevelEnc = ParquetEncoding.Rle,
                        RepLevelEnc = ParquetEncoding.Rle
                    }
                };
                using var headerMs = new MemoryStream();
                page.Write(new ThriftWriter(headerMs));
                byte[] headerBytes = headerMs.ToArray();
                long pageOffset = stream.Position;
                stream.Write(headerBytes, 0, headerBytes.Length);
                stream.Write(compressed, 0, compressed.Length);

                return new ChunkInfo
                {
                    DataPageOffset = pageOffset,
                    DictPageOffset = 0,
                    UncompSize = headerBytes.Length + (long)uncompressed.Length,
                    CompSize = headerBytes.Length + (long)compressed.Length,
                    NumValues = padded.Length,
                    Codec = codec,
                    PrimitiveType = ParquetPrimitiveType.Int64,
                    IsTimestamp = true
                };
            }

            private static ChunkInfo WriteDictColumn(Stream stream, double[] padded, ParquetCompression codec)
            {
                // Build dictionary keyed on the canonical bit pattern of each double, so
                // NaN clusters into a single entry instead of N (since NaN != NaN).
                var map = new Dictionary<long, int>();
                var dictValues = new List<double>();
                var indices = new int[padded.Length];
                for (int loop = 0; loop < padded.Length; loop++)
                {
                    long bits = BitConverter.DoubleToInt64Bits(padded[loop]);
                    if (!map.TryGetValue(bits, out int idx))
                    {
                        idx = dictValues.Count;
                        dictValues.Add(padded[loop]);
                        map[bits] = idx;
                    }
                    indices[loop] = idx;
                }

                // -- Dictionary page (PLAIN-encoded values).
                byte[] dictUncompressed = Plain.EncodeDouble(dictValues.ToArray());
                byte[] dictCompressed = Codec.Compress(dictUncompressed, codec);
                var dictPageHeader = new PageHeader
                {
                    Type = PageType.DictionaryPage,
                    UncompressedPageSize = dictUncompressed.Length,
                    CompressedPageSize = dictCompressed.Length,
                    DictHeader = new DictionaryPageHeader
                    {
                        NumValues = dictValues.Count,
                        Encoding = ParquetEncoding.Plain
                    }
                };
                using var dictHeaderMs = new MemoryStream();
                dictPageHeader.Write(new ThriftWriter(dictHeaderMs));
                byte[] dictHeaderBytes = dictHeaderMs.ToArray();

                long dictOffset = stream.Position;
                stream.Write(dictHeaderBytes, 0, dictHeaderBytes.Length);
                stream.Write(dictCompressed, 0, dictCompressed.Length);

                // -- Data page (byte0 = bit width, then a single bit-packed-hybrid run).
                int bitWidth = LevelReader.BitWidth(dictValues.Count - 1);
                byte[] hybrid = LevelReader.WriteSingleBitPackedRun(indices, bitWidth);
                byte[] dataUncompressed = new byte[1 + hybrid.Length];
                dataUncompressed[0] = (byte)bitWidth;
                Buffer.BlockCopy(hybrid, 0, dataUncompressed, 1, hybrid.Length);
                byte[] dataCompressed = Codec.Compress(dataUncompressed, codec);

                var dataPageHeader = new PageHeader
                {
                    Type = PageType.DataPage,
                    UncompressedPageSize = dataUncompressed.Length,
                    CompressedPageSize = dataCompressed.Length,
                    DataHeader = new DataPageHeader
                    {
                        NumValues = padded.Length,
                        Encoding = ParquetEncoding.RleDictionary,
                        DefLevelEnc = ParquetEncoding.Rle,
                        RepLevelEnc = ParquetEncoding.Rle
                    }
                };
                using var dataHeaderMs = new MemoryStream();
                dataPageHeader.Write(new ThriftWriter(dataHeaderMs));
                byte[] dataHeaderBytes = dataHeaderMs.ToArray();

                long dataOffset = stream.Position;
                stream.Write(dataHeaderBytes, 0, dataHeaderBytes.Length);
                stream.Write(dataCompressed, 0, dataCompressed.Length);

                long uncompTotal = dictHeaderBytes.Length + (long)dictUncompressed.Length
                                 + dataHeaderBytes.Length + (long)dataUncompressed.Length;
                long compTotal = dictHeaderBytes.Length + (long)dictCompressed.Length
                               + dataHeaderBytes.Length + (long)dataCompressed.Length;

                return new ChunkInfo
                {
                    DataPageOffset = dataOffset,
                    DictPageOffset = dictOffset,
                    UncompSize = uncompTotal,
                    CompSize = compTotal,
                    NumValues = padded.Length,
                    Codec = codec,
                    PrimitiveType = ParquetPrimitiveType.Double
                };
            }
        }

        internal sealed class ReadColumn
        {
            public string Name = "";
            public double[] Values = Array.Empty<double>();
            public string? Error;
            public bool IsTimestamp;
        }

        internal static class ParquetReader
        {
            public static (List<ReadColumn> cols, List<KeyValue> meta) Read(Stream stream)
            {
                if (!stream.CanSeek)
                {
                    throw new ArgumentException("ParquetSimple requires a seekable stream");
                }
                long fileLen = stream.Length;
                if (fileLen < 12)
                {
                    throw new InvalidDataException("File too small to be parquet");
                }
                Span<byte> magicBuf = stackalloc byte[4];
                stream.Position = 0;
                stream.Read(magicBuf);
                if (magicBuf[0] != 'P' || magicBuf[1] != 'A' || magicBuf[2] != 'R' || magicBuf[3] != '1')
                {
                    throw new InvalidDataException("Not a parquet file (missing PAR1 header)");
                }
                stream.Position = fileLen - 4;
                stream.Read(magicBuf);
                if (magicBuf[0] != 'P' || magicBuf[1] != 'A' || magicBuf[2] != 'R' || magicBuf[3] != '1')
                {
                    throw new InvalidDataException("Not a parquet file (missing PAR1 footer)");
                }
                Span<byte> lenBuf = stackalloc byte[4];
                stream.Position = fileLen - 8;
                stream.Read(lenBuf);
                int footerLen = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
                if (footerLen <= 0 || footerLen > fileLen - 8)
                {
                    throw new InvalidDataException("Bad footer length");
                }
                stream.Position = fileLen - 8 - footerLen;
                var footerBuf = new byte[footerLen];
                int got = 0;
                while (got < footerLen)
                {
                    int n = stream.Read(footerBuf, got, footerLen - got);
                    if (n <= 0) throw new EndOfStreamException();
                    got += n;
                }
                using var footerMs = new MemoryStream(footerBuf, writable: false);
                var fileMd = FileMetaData.Read(new TReader(footerMs));

                // Identify leaf schema elements (depth-first, skipping groups). We keep
                // each leaf's SchemaElement so the decoder can dispatch on logical type
                // (TIMESTAMP_MILLIS / TIMESTAMP_MICROS / INT96-as-timestamp).
                var leafPaths = new List<List<string>>();
                var leafElements = new List<SchemaElement>();
                BuildLeafPaths(fileMd.Schema, leafPaths, leafElements);

                var cols = new List<ReadColumn>();
                for (int li = 0; li < leafPaths.Count; li++)
                {
                    var path = leafPaths[li];
                    var name = string.Join(".", StripWrapperSegments(path));
                    var leaf = leafElements[li];
                    var col = new ReadColumn { Name = name, IsTimestamp = IsTimestampLeaf(leaf) };
                    try
                    {
                        col.Values = ReadLeafFromAllRowGroups(stream, fileMd, leafIndex: li, leaf: leaf);
                    }
                    catch (NotSupportedException ex)
                    {
                        col.Error = ex.Message;
                    }
                    catch (InvalidDataException ex)
                    {
                        col.Error = ex.Message;
                    }
                    cols.Add(col);
                }

                return (cols, fileMd.KeyValueMetadata);
            }

            // Walks fileMd.Schema in depth-first parquet order. Root is index 0 with
            // num_children = N; children follow recursively. We collect each leaf's
            // path (sequence of names from root's first child down to the leaf).
            private static void BuildLeafPaths(List<SchemaElement> schema, List<List<string>> result, List<SchemaElement> resultLeaves)
            {
                if (schema.Count == 0) return;
                int idx = 1;
                int rootChildren = schema[0].NumChildren ?? (schema.Count - 1);
                for (int c = 0; c < rootChildren && idx < schema.Count; c++)
                {
                    Walk(schema, ref idx, new List<string>(), result, resultLeaves);
                }
            }

            private static void Walk(List<SchemaElement> schema, ref int idx, List<string> path, List<List<string>> leaves, List<SchemaElement> leafElements)
            {
                var node = schema[idx++];
                path.Add(node.Name);
                int kids = node.NumChildren ?? 0;
                if (kids == 0)
                {
                    leaves.Add(new List<string>(path));
                    leafElements.Add(node);
                }
                else
                {
                    for (int c = 0; c < kids && idx < schema.Count; c++)
                    {
                        Walk(schema, ref idx, path, leaves, leafElements);
                    }
                }
                path.RemoveAt(path.Count - 1);
            }

            // ConvertedType values from parquet.thrift: 10 = TIMESTAMP_MILLIS, 11 = TIMESTAMP_MICROS.
            internal const int CT_TIMESTAMP_MILLIS = 10;
            internal const int CT_TIMESTAMP_MICROS = 11;

            private static bool IsTimestampLeaf(SchemaElement leaf)
            {
                if (leaf.Type == ParquetPrimitiveType.Int96)
                {
                    return true;
                }
                if (leaf.Type == ParquetPrimitiveType.Int64
                    && (leaf.ConvertedType == CT_TIMESTAMP_MILLIS || leaf.ConvertedType == CT_TIMESTAMP_MICROS))
                {
                    return true;
                }
                return false;
            }

            private static IEnumerable<string> StripWrapperSegments(List<string> path)
            {
                // pyarrow / arrow nested types insert wrapper segments. Strip the most common.
                foreach (var seg in path)
                {
                    if (seg == "list" || seg == "element" || seg == "bag" || seg == "array" || seg == "key_value") continue;
                    yield return seg;
                }
            }

            private static double[] ReadLeafFromAllRowGroups(Stream stream, FileMetaData fileMd, int leafIndex, SchemaElement leaf)
            {
                var all = new List<double>();
                foreach (var rg in fileMd.RowGroups)
                {
                    if (leafIndex >= rg.Columns.Count) continue;
                    var chunk = rg.Columns[leafIndex];
                    var cm = chunk.MetaData;
                    if (cm == null) continue;

                    // Seek to dictionary page first if it exists, otherwise the first data page.
                    long startOffset = cm.DictionaryPageOffset > 0 ? cm.DictionaryPageOffset : cm.DataPageOffset;
                    long valuesRemaining = cm.NumValues;

                    int maxDef = ComputeMaxLevel(fileMd.Schema, leafIndex, repetitionMode: false);
                    int maxRep = ComputeMaxLevel(fileMd.Schema, leafIndex, repetitionMode: true);

                    double[]? dictionary = null; // populated when a DictionaryPage is encountered

                    stream.Position = startOffset;
                    while (valuesRemaining > 0)
                    {
                        var ph = PageHeader.Read(new TReader(stream));
                        var page = new byte[ph.CompressedPageSize];
                        int got = 0;
                        while (got < page.Length)
                        {
                            int n = stream.Read(page, got, page.Length - got);
                            if (n <= 0) throw new EndOfStreamException();
                            got += n;
                        }

                        if (ph.Type == PageType.DictionaryPage)
                        {
                            if (ph.DictHeader == null) throw new InvalidDataException("DictionaryPage missing header");
                            byte[] decompressed = Codec.Decompress(page, cm.Codec, ph.UncompressedPageSize);
                            dictionary = DecodePlainAsDouble(decompressed, 0, decompressed.Length, ph.DictHeader.NumValues, cm.Type, leaf);
                            // dictionary page does not advance valuesRemaining
                        }
                        else if (ph.Type == PageType.DataPage)
                        {
                            byte[] decompressed = Codec.Decompress(page, cm.Codec, ph.UncompressedPageSize);
                            if (ph.DataHeader == null) throw new InvalidDataException("DataPage missing header");
                            var values = DecodeDataPageV1(decompressed, ph.DataHeader, cm.Type, maxDef, maxRep, dictionary, leaf);
                            all.AddRange(values);
                            valuesRemaining -= ph.DataHeader.NumValues;
                        }
                        else if (ph.Type == PageType.DataPageV2)
                        {
                            throw new NotSupportedException("ParquetSimple does not yet read DataPageV2");
                        }
                        else
                        {
                            // Unknown page type; advance per the (already-consumed) header bytes + page data
                            valuesRemaining = 0;
                        }
                    }
                }
                return all.ToArray();
            }

            private static double[] DecodePlainAsDouble(byte[] buf, int offset, int byteLen, int count, ParquetPrimitiveType type, SchemaElement leaf)
            {
                // Timestamp columns: convert raw int64 millis/micros or int96 julian-day
                // form into unix seconds (double, sub-second precision preserved).
                if (type == ParquetPrimitiveType.Int96)
                {
                    return Plain.DecodeInt96AsTimestamp(buf, offset, byteLen, count);
                }
                if (type == ParquetPrimitiveType.Int64
                    && (leaf.ConvertedType == CT_TIMESTAMP_MILLIS || leaf.ConvertedType == CT_TIMESTAMP_MICROS))
                {
                    var raw = Plain.DecodeInt64(buf, offset, byteLen, count);
                    double scale = leaf.ConvertedType == CT_TIMESTAMP_MILLIS ? 1e-3 : 1e-6;
                    for (int loop = 0; loop < raw.Length; loop++)
                    {
                        raw[loop] *= scale;
                    }
                    return raw;
                }
                return type switch
                {
                    ParquetPrimitiveType.Double => Plain.DecodeDouble(buf, offset, byteLen, count),
                    ParquetPrimitiveType.Float => Plain.DecodeFloat(buf, offset, byteLen, count),
                    ParquetPrimitiveType.Int32 => Plain.DecodeInt32(buf, offset, byteLen, count),
                    ParquetPrimitiveType.Int64 => Plain.DecodeInt64(buf, offset, byteLen, count),
                    ParquetPrimitiveType.Boolean => Plain.DecodeBoolean(buf, offset, byteLen, count),
                    _ => throw new NotSupportedException($"ParquetSimple does not decode primitive type {type}")
                };
            }

            // Walks the schema and returns the max definition or repetition level for
            // the targeted leaf. For def-levels every non-Required ancestor adds 1; for
            // rep-levels only Repeated ancestors do.
            private static int ComputeMaxLevel(List<SchemaElement> schema, int leafIndex, bool repetitionMode)
            {
                int idx = 1;
                int rootChildren = schema[0].NumChildren ?? (schema.Count - 1);
                int seen = 0;
                int max = 0;
                bool found = false;
                for (int loop = 0; loop < rootChildren && idx < schema.Count && !found; loop++)
                {
                    WalkForLevel(schema, ref idx, 0, ref seen, leafIndex, ref max, ref found, repetitionMode);
                }
                return max;
            }

            private static void WalkForLevel(List<SchemaElement> schema, ref int idx, int curLevel, ref int seenLeaves, int targetLeaf, ref int maxLevel, ref bool found, bool repetitionMode)
            {
                var node = schema[idx++];
                int contributes = repetitionMode
                    ? (node.Repetition == ParquetRepetition.Repeated ? 1 : 0)
                    : (node.Repetition == ParquetRepetition.Required ? 0 : 1);
                int nodeLevel = curLevel + contributes;
                int kids = node.NumChildren ?? 0;
                if (kids == 0)
                {
                    if (seenLeaves == targetLeaf)
                    {
                        maxLevel = nodeLevel;
                        found = true;
                    }
                    seenLeaves++;
                }
                else
                {
                    for (int c = 0; c < kids && idx < schema.Count && !found; c++)
                    {
                        WalkForLevel(schema, ref idx, nodeLevel, ref seenLeaves, targetLeaf, ref maxLevel, ref found, repetitionMode);
                    }
                }
            }

            private static double[] DecodeDataPageV1(byte[] buf, DataPageHeader hdr, ParquetPrimitiveType type, int maxDef, int maxRep, double[]? dictionary, SchemaElement leaf)
            {
                int pos = 0;
                int[]? defLevels = null;
                int[]? repLevels = null;

                if (maxRep > 0)
                {
                    int len = BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(pos, 4));
                    pos += 4;
                    repLevels = LevelReader.ReadHybrid(buf, pos, len, LevelReader.BitWidth(maxRep), hdr.NumValues);
                    pos += len;
                }
                if (maxDef > 0)
                {
                    int len = BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(pos, 4));
                    pos += 4;
                    defLevels = LevelReader.ReadHybrid(buf, pos, len, LevelReader.BitWidth(maxDef), hdr.NumValues);
                    pos += len;
                }

                int nonNull = hdr.NumValues;
                if (defLevels != null)
                {
                    nonNull = 0;
                    for (int loop = 0; loop < defLevels.Length; loop++)
                    {
                        if (defLevels[loop] == maxDef)
                        {
                            nonNull++;
                        }
                    }
                }

                int valuesByteLen = buf.Length - pos;
                double[] decoded;
                if (hdr.Encoding == ParquetEncoding.PlainDictionary || hdr.Encoding == ParquetEncoding.RleDictionary)
                {
                    if (dictionary == null) throw new InvalidDataException("Dictionary-encoded data page with no preceding dictionary page");
                    if (valuesByteLen < 1) throw new InvalidDataException("Dict-encoded data page truncated");
                    int bitWidth = buf[pos];
                    int[] indices = LevelReader.ReadHybrid(buf, pos + 1, valuesByteLen - 1, bitWidth, nonNull);
                    decoded = new double[nonNull];
                    for (int loop = 0; loop < nonNull; loop++)
                    {
                        int idx = indices[loop];
                        if (idx < 0 || idx >= dictionary.Length) throw new InvalidDataException($"Dict index {idx} out of range (size {dictionary.Length})");
                        decoded[loop] = dictionary[idx];
                    }
                }
                else if (hdr.Encoding == ParquetEncoding.Plain)
                {
                    decoded = DecodePlainAsDouble(buf, pos, valuesByteLen, nonNull, type, leaf);
                }
                else
                {
                    throw new NotSupportedException($"ParquetSimple does not read encoding {hdr.Encoding}; re-export with PLAIN");
                }

                if (defLevels == null) return decoded;

                // expand to NumValues, inserting NaN where def < maxDef
                var full = new double[hdr.NumValues];
                int read = 0;
                for (int loop = 0; loop < hdr.NumValues; loop++)
                {
                    if (defLevels[loop] == maxDef)
                    {
                        full[loop] = decoded[read++];
                    }
                    else
                    {
                        full[loop] = double.NaN;
                    }
                }
                return full;
            }
        }
    }

    [TestClass]
    public class ParquetSimpleTests
    {
        [TestMethod]
        public void ThriftCompactRoundTripsPrimitives()
        {
            using var ms = new MemoryStream();
            var w = new ParquetSimple.ThriftWriter(ms);
            w.WriteI32(0);
            w.WriteI32(1);
            w.WriteI32(-1);
            w.WriteI32(127);
            w.WriteI32(int.MinValue);
            w.WriteI32(int.MaxValue);
            w.WriteI64(long.MaxValue);
            w.WriteI64(long.MinValue);
            w.WriteDouble(3.14159);
            w.WriteString("hello");
            ms.Position = 0;
            var r = new ParquetSimple.TReader(ms);
            Assert.AreEqual(0, r.ReadI32());
            Assert.AreEqual(1, r.ReadI32());
            Assert.AreEqual(-1, r.ReadI32());
            Assert.AreEqual(127, r.ReadI32());
            Assert.AreEqual(int.MinValue, r.ReadI32());
            Assert.AreEqual(int.MaxValue, r.ReadI32());
            Assert.AreEqual(long.MaxValue, r.ReadI64());
            Assert.AreEqual(long.MinValue, r.ReadI64());
            Assert.AreEqual(3.14159, r.ReadDouble());
            Assert.AreEqual("hello", r.ReadString());
        }

        [TestMethod]
        public void ThriftCompactRoundTripsKeyValueStruct()
        {
            using var ms = new MemoryStream();
            new ParquetSimple.KeyValue("foo", "bar").Write(new ParquetSimple.ThriftWriter(ms));
            ms.Position = 0;
            var kv = ParquetSimple.KeyValue.Read(new ParquetSimple.TReader(ms));
            Assert.AreEqual("foo", kv.Key);
            Assert.AreEqual("bar", kv.Value);
        }

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

                ParquetSimple.SaveCols(path, names, cols, sps, offsets);

                var loaded = ParquetSimple.LoadCols(path);
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
                try
                {
                    File.Delete(path);
                }
                catch { }
            }
        }

        [TestMethod]
        public void RoundTripPreservesNanWithoutMetadataTrim()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                var names = new[] { "x" };
                var cols = new[] { new[] { 1.0, double.NaN, 3.0 } };

                ParquetSimple.SaveCols(path, names, cols);
                var loaded = ParquetSimple.LoadCols(path);

                Assert.AreEqual(1, loaded.Count);
                Assert.AreEqual(3, loaded[0].Values.Length);
                Assert.AreEqual(1.0, loaded[0].Values[0]);
                Assert.IsTrue(double.IsNaN(loaded[0].Values[1]));
                Assert.AreEqual(3.0, loaded[0].Values[2]);
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch { }
            }
        }

        [TestMethod]
        public void DeterministicByteOutput()
        {
            string p1 = Path.GetTempFileName() + ".a.parquet";
            string p2 = Path.GetTempFileName() + ".b.parquet";
            try
            {
                var names = new[] { "a", "b" };
                var cols = new[] { new[] { 1.0, 2.0 }, new[] { 3.0, 4.0, 5.0 } };
                var sps = new[] { 100.0, 200.0 };
                ParquetSimple.SaveCols(p1, names, cols, sps);
                ParquetSimple.SaveCols(p2, names, cols, sps);
                var b1 = File.ReadAllBytes(p1);
                var b2 = File.ReadAllBytes(p2);
                CollectionAssert.AreEqual(b1, b2);
            }
            finally
            {
                try
                {
                    File.Delete(p1);
                }
                catch { }
                try
                {
                    File.Delete(p2);
                }
                catch { }
            }
        }

        [TestMethod]
        public void SnappyLiteralRoundTrip()
        {
            var rng = new Random(42);
            var input = new byte[2000];
            rng.NextBytes(input);
            var compressed = ParquetSimple.Codec.SnappyCompressLiteral(input);
            var decompressed = ParquetSimple.Codec.SnappyDecompress(compressed);
            CollectionAssert.AreEqual(input, decompressed);
        }

        [TestMethod]
        public void GzipRoundTripDecompresses()
        {
            var rng = new Random(43);
            var input = new byte[4000];
            rng.NextBytes(input);
            var compressed = ParquetSimple.Codec.Compress(input, ParquetCompression.Gzip);
            var decompressed = ParquetSimple.Codec.Decompress(compressed, ParquetCompression.Gzip, input.Length);
            CollectionAssert.AreEqual(input, decompressed);
        }

        [TestMethod]
        public void ZstdRoundTripDecompresses()
        {
            var rng = new Random(44);
            var input = new byte[8000];
            rng.NextBytes(input);
            var compressed = ParquetSimple.Codec.Compress(input, ParquetCompression.Zstd);
            var decompressed = ParquetSimple.Codec.Decompress(compressed, ParquetCompression.Zstd, input.Length);
            CollectionAssert.AreEqual(input, decompressed);
        }

        [DataTestMethod]
        [DataRow(ParquetCompression.Uncompressed)]
        [DataRow(ParquetCompression.Gzip)]
        [DataRow(ParquetCompression.Snappy)]
        [DataRow(ParquetCompression.Zstd)]
        public void RoundTripWithCodec(ParquetCompression codec)
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                var names = new[] { "x", "y" };
                var cols = new[]
                {
                    Enumerable.Range(0, 1000).Select(i => Math.Sin(i * 0.01)).ToArray(),
                    Enumerable.Range(0, 1000).Select(i => (double)i).ToArray()
                };
                var meta = new List<ParquetSimple.KeyValue>
                {
                    new ParquetSimple.KeyValue("length.x", "1000"),
                    new ParquetSimple.KeyValue("length.y", "1000")
                };
                using (var fs = File.Create(path))
                {
                    ParquetSimple.ParquetWriter.Write(fs, names, cols, 1000, meta, codec);
                }

                var loaded = ParquetSimple.LoadCols(path);
                Assert.AreEqual(2, loaded.Count);
                CollectionAssert.AreEqual(cols[0], loaded.First(c => c.Name == "x").Values);
                CollectionAssert.AreEqual(cols[1], loaded.First(c => c.Name == "y").Values);
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch { }
            }
        }

        [TestMethod]
        public void SingleRowEdgeCase()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                ParquetSimple.SaveCols(path, new[] { "only" }, new[] { new[] { 42.0 } });
                var loaded = ParquetSimple.LoadCols(path);
                Assert.AreEqual(1, loaded.Count);
                Assert.AreEqual(1, loaded[0].Values.Length);
                Assert.AreEqual(42.0, loaded[0].Values[0]);
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch { }
            }
        }

        [TestMethod]
        public void RejectsNonParquetFile()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, "this is not a parquet file");
                Assert.ThrowsException<InvalidDataException>(() => ParquetSimple.LoadCols(path));
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch { }
            }
        }

        [DataTestMethod]
        [DataRow(ParquetCompression.Uncompressed)]
        [DataRow(ParquetCompression.Gzip)]
        [DataRow(ParquetCompression.Snappy)]
        public void DictionaryRoundTripWithCodec(ParquetCompression codec)
        {
            // 1000 rows, 5 distinct values - triggers dictionary path under both auto
            // detect and forceDictionary=true.
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                var rng = new Random(7);
                var palette = new[] { -1.5, 0.0, 1.5, 3.14, 42.0 };
                var col = Enumerable.Range(0, 1000).Select(_ => palette[rng.Next(palette.Length)]).ToArray();

                var meta = new List<ParquetSimple.KeyValue>
                {
                    new ParquetSimple.KeyValue("length.signal", "1000")
                };
                using (var fs = File.Create(path))
                {
                    ParquetSimple.ParquetWriter.Write(fs, new[] { "signal" }, new[] { col }, 1000, meta, codec, forceDictionary: true);
                }

                var loaded = ParquetSimple.LoadCols(path);
                Assert.AreEqual(1, loaded.Count);
                CollectionAssert.AreEqual(col, loaded[0].Values);
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch { }
            }
        }

        [TestMethod]
        public void DictionaryAutoDetectChoosesDictForLowCardinality()
        {
            string pathDict = Path.GetTempFileName() + ".dict.parquet";
            string pathPlain = Path.GetTempFileName() + ".plain.parquet";
            try
            {
                // 1000 rows of 3 distinct values. Auto-detect should pick dict and produce
                // a meaningfully smaller file than forced-PLAIN.
                var col = Enumerable.Range(0, 1000).Select(i => (double)(i % 3)).ToArray();
                var meta = new List<ParquetSimple.KeyValue> { new ParquetSimple.KeyValue("length.x", "1000") };

                using (var fs = File.Create(pathDict))
                {
                    ParquetSimple.ParquetWriter.Write(fs, new[] { "x" }, new[] { col }, 1000, meta, ParquetCompression.Uncompressed);
                }
                using (var fs = File.Create(pathPlain))
                {
                    ParquetSimple.ParquetWriter.Write(fs, new[] { "x" }, new[] { col }, 1000, meta, ParquetCompression.Uncompressed, forceDictionary: false);
                }
                long dictSize = new FileInfo(pathDict).Length;
                long plainSize = new FileInfo(pathPlain).Length;
                Assert.IsTrue(dictSize < plainSize, $"Dict ({dictSize}) should be smaller than plain ({plainSize})");

                CollectionAssert.AreEqual(col, ParquetSimple.LoadCols(pathDict)[0].Values);
                CollectionAssert.AreEqual(col, ParquetSimple.LoadCols(pathPlain)[0].Values);
            }
            finally
            {
                try
                {
                    File.Delete(pathDict);
                }
                catch { }
                try
                {
                    File.Delete(pathPlain);
                }
                catch { }
            }
        }

        [TestMethod]
        public void DictionarySingleValueColumnUsesBitWidthZero()
        {
            // Edge case: dictionary with one entry -> bit width = 0, so the bit-packed
            // hybrid run is a single varint header with no value bytes. ReadHybrid
            // handles bit_width == 0 specially.
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                var col = Enumerable.Repeat(7.5, 100).ToArray();
                var meta = new List<ParquetSimple.KeyValue> { new ParquetSimple.KeyValue("length.k", "100") };
                using (var fs = File.Create(path))
                {
                    ParquetSimple.ParquetWriter.Write(fs, new[] { "k" }, new[] { col }, 100, meta, ParquetCompression.Uncompressed, forceDictionary: true);
                }
                var loaded = ParquetSimple.LoadCols(path);
                CollectionAssert.AreEqual(col, loaded[0].Values);
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch { }
            }
        }

        [TestMethod]
        public void TimestampColumnRoundTrip()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                // Mix one timestamp column with one regular value column.
                var times = new[] { 1700000000.000, 1700000001.500, 1700000003.250 }; // unix seconds
                var values = new[] { 1.0, 2.0, 3.0 };
                ParquetSimple.SaveCols(
                    path,
                    new[] { "trace.Time", "trace.Value" },
                    new[] { times, values },
                    samplesPerSeconds: null,
                    sampleOffsets: null,
                    isTimestamp: new[] { true, false });

                var loaded = ParquetSimple.LoadCols(path);
                Assert.AreEqual(2, loaded.Count);

                var ts = loaded.First(c => c.Name == "trace.Time");
                Assert.IsTrue(ts.IsTimestamp);
                Assert.AreEqual(3, ts.Values.Length);
                // TIMESTAMP_MICROS rounds to us; precision check is microsecond.
                for (int loop = 0; loop < times.Length; loop++)
                {
                    Assert.AreEqual(times[loop], ts.Values[loop], 1e-6, $"ts[{loop}]");
                }

                var vals = loaded.First(c => c.Name == "trace.Value");
                Assert.IsFalse(vals.IsTimestamp);
                CollectionAssert.AreEqual(values, vals.Values);
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch { }
            }
        }

        [TestMethod]
        public void BitPackedHybridSelfRoundTrip()
        {
            // Direct test of LevelReader.ReadHybrid against LevelReader.WriteSingleBitPackedRun.
            // bitWidth, count pairs that exercise typical and tight cases.
            foreach (var (bitWidth, count) in new[] { (1, 8), (1, 9), (3, 100), (5, 33), (7, 200), (32, 16) })
            {
                var rng = new Random(bitWidth * 31 + count);
                int max = bitWidth == 32 ? int.MaxValue : (1 << bitWidth) - 1;
                var input = Enumerable.Range(0, count).Select(_ => rng.Next(0, max == 0 ? 1 : max)).ToArray();
                var encoded = ParquetSimple.LevelReader.WriteSingleBitPackedRun(input, bitWidth);
                var decoded = ParquetSimple.LevelReader.ReadHybrid(encoded, 0, encoded.Length, bitWidth, count);
                CollectionAssert.AreEqual(input, decoded, $"bitWidth={bitWidth} count={count}");
            }
        }
    }
}
