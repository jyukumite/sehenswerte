using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Maths;
using System.Reflection;

namespace SehensWerte.Utils
{
    public static class ObjectExtension
    {
        public static void CopyMembersFrom(this object target, object source)
        {
            Type sourceType = source.GetType();
            Type targetType = target.GetType();
            FieldInfo[] sourceFields = sourceType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo[] sourceProperties = sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo field in sourceFields)
            {
                FieldInfo? targetField = targetType.GetField(field.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                targetField?.SetValue(target, field.GetValue(source));
            }

            foreach (PropertyInfo property in sourceProperties)
            {
                PropertyInfo? targetProperty = targetType.GetProperty(property.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (targetProperty != null && targetProperty.CanWrite)
                {
                    targetProperty.SetValue(target, property.GetValue(source));
                }
            }
        }

        public static double[] CopyToDoubleArray(this object input)
        { //fixme: unit test

            if (input == null) return new double[0];

            if (input is Array array)
            {
                if (input is double[] doublearray) return doublearray.Copy();
                else if (input is byte[] bytearray) return Array.ConvertAll(bytearray, x => (double)x);
                else if (input is short[] shortarray) return Array.ConvertAll(shortarray, x => (double)x);
                else if (input is ushort[] ushortarray) return Array.ConvertAll(ushortarray, x => (double)x);
                else if (input is int[] intarray) return Array.ConvertAll(intarray, x => (double)x);
                else if (input is uint[] uintarray) return Array.ConvertAll(uintarray, x => (double)x);
                else if (input is float[] floatarray) return Array.ConvertAll(floatarray, x => (double)x);
                else return new double[array.Length];
            }
            else if (input is List<double> doublelist) return doublelist.Select(x => (double)x).ToArray();
            else if (input is List<byte> bytelist) return bytelist.Select(x => (double)x).ToArray();
            else if (input is List<short> shortlist) return shortlist.Select(x => (double)x).ToArray();
            else if (input is List<ushort> ushortlist) return ushortlist.Select(x => (double)x).ToArray();
            else if (input is List<int> intlist) return intlist.Select(x => (double)x).ToArray();
            else if (input is List<uint> uintlist) return uintlist.Select(x => (double)x).ToArray();
            else if (input is List<float> floatlist) return floatlist.Select(x => (double)x).ToArray();
            else if (input is Ring<double> ring) return ring.AllSamples();
            else return new double[0];
        }

        public static IEnumerable<IEnumerable<T>> Transpose<T>(this IEnumerable<IEnumerable<T>> source)
        { //fixme: unit test
            if (source == null || !source.Any())
            {
                return Enumerable.Empty<IEnumerable<T>>();
            }
            var rows = source.Select(row => row.ToList()).ToList();
            int maxLength = rows.Max(r => r.Count);

            return Enumerable.Range(0, maxLength)
                             .Select(i => rows.Select(row => i < row.Count ? row[i] : default!).ToArray())
                             .ToArray();
        }
    }

    [TestClass]
    public class ObjectExtensionTest
    {
        class SourceClass
        {
            public int IntField;
            public string StringField { get; set; } = "";
            public int Other1;
        }

        class TargetClass
        {
            public int IntField;
            public string StringField { get; set; } = "";
            public int Other2;
        }

        [TestMethod]
        public void TestCopyMembersFrom()
        {
            SourceClass source = new SourceClass
            {
                IntField = 42,
                StringField = "Test"
            };
            TargetClass target = new TargetClass();
            target.CopyMembersFrom(source);
            Assert.AreEqual(source.IntField, target.IntField);
            Assert.AreEqual(source.StringField, target.StringField);
        }
    }
}
