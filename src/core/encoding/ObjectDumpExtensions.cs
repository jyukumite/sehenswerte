using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Reflection;
using System.Text;

namespace SehensWerte.Utils
{
    public static class ObjectDumpExtension
    {
        private const string NullName = "(null)";
        private const string CNullName = "null";

        public enum DumpMode { Verbose, CSharp };

        public static string DumpObject(this object obj, DumpMode mode = DumpMode.Verbose)
        {
            try
            {
                return Object(obj, 0, mode);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        private static string Object(object obj, int indent, DumpMode mode)
        {
            return obj == null ?
                mode switch
                {
                    DumpMode.Verbose => NullName,
                    DumpMode.CSharp => CNullName,
                }
                : Object(obj, obj.GetType(), NullName, indent, mode);
        }

        private static string Object(object obj, Type type, string name, int indent, DumpMode mode)
        {
            string text = "";
            if ((type.IsPrimitive || type.IsSealed || obj is string) && obj is not Array)
            {
                return text + Primitive(obj, type, name, indent, mode);
            }
            else
            {
                return text + Composite(obj, type, name, indent, mode);
            }
        }

        private static string Primitive(object obj, Type type, string name, int indent, DumpMode mode)
        {
            return mode switch
            {
                DumpMode.Verbose => Pad(indent, "{0} ({1}): {2}\r\n", name, type.Name, obj),
                DumpMode.CSharp => Pad(indent, (obj is string) ? "{0} = \"{2}\",\r\n" : "{0} = {2},\r\n", name, type.Name, obj)
            };
        }

        private static string Composite(object obj, Type type, string name, int indent, DumpMode mode)
        {
            string text = mode switch
            {
                DumpMode.Verbose => Pad(indent, "{0} ({1}):\r\n", name == NullName ? "" : name, type.Name),
                DumpMode.CSharp => Pad(indent, "new {0}() {{\r\n", name == CNullName ? "" : name)
            };

            if (obj is byte[])
            {
                text += ((byte[])obj).HexDump() + "\r\n";
            }
            else if (obj is IDictionary)
            {
                text += Dictionary((IDictionary)obj, indent, mode);
            }
            else if (obj is ICollection)
            {
                text += Collection((ICollection)obj, indent, mode);
            }
            else if (obj.GetType().IsCOMObject)
            {
                text += ComObject(obj, indent, mode);
            }
            else
            {
                foreach (MemberInfo member in obj.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    try
                    {
                        text += DumpMember(obj, member, indent, mode);
                    }
                    catch (SystemException ex)
                    {
                        text = text + ex.Message + "\r\n";
                    }
                }
            }
            return text +
                mode switch
                {
                    DumpMode.Verbose => "",
                    DumpMode.CSharp => "\r\n" + Pad(indent, "}}\r\n")
                };
            ;
        }

        private static string ComObject(object obj, int indent, DumpMode mode)
        {
            return Pad(indent + 1, "(COM) {0}\r\n", obj.GetType().ToString());
        }

        private static string Pad(int level, string msg, params object[] args)
        {
            return string.Format(msg, args).PadLeft(level * 4 + string.Format(msg, args).Length);
        }

        private static string Collection(ICollection collection, int indent, DumpMode mode)
        {
            string text = "";
            foreach (object item in collection)
            {
                text += Object(item, indent + 1, mode);
            }
            return text;
        }

        private static string Dictionary(IDictionary dictionary, int indent, DumpMode mode)
        {
            string text = "";
            foreach (object key in dictionary.Keys)
            {
                text += mode switch
                {
                    DumpMode.Verbose => Pad(indent + 1, "[{0}] ({1}):\r\n", key, key.GetType().Name),
                    DumpMode.CSharp => Pad(indent + 1, "{{ {0}.{1}, ", GetFullName(key), key)
                };
                text += Object(dictionary[key] ?? ((mode == DumpMode.CSharp) ? CNullName : NullName), indent + 2, mode);
                text += mode switch
                {
                    DumpMode.Verbose => "",
                    DumpMode.CSharp => Pad(indent + 1, "}},\r\n")
                };
            }
            return text;
        }

        private static string GetFullName(object key)
        {
            return (key.GetType().FullName ?? "").Split("+").Reverse().Take(1).FirstOrDefault("");
        }

        private static string DumpMember(object obj, MemberInfo member, int indent, DumpMode mode)
        {
            if (member is not MethodInfo && member is not ConstructorInfo && member is not EventInfo)
            {
                if (member is FieldInfo)
                {
                    FieldInfo fieldInfo = (FieldInfo)member;
                    string text = member.Name;
                    if ((fieldInfo.Attributes & FieldAttributes.Public) == 0)
                    {
                        text = "#" + text;
                    }
                    return Object(fieldInfo.GetValue(obj) ?? NullName, fieldInfo.FieldType, text, indent + 1, mode);
                }

                if (member is PropertyInfo)
                {
                    PropertyInfo propertyInfo = (PropertyInfo)member;
                    if (propertyInfo.GetIndexParameters().Length == 0 && propertyInfo.CanRead)
                    {
                        string text = member.Name;
                        MethodInfo? getMethod = propertyInfo.GetGetMethod();
                        if (getMethod != null && (getMethod.Attributes & MethodAttributes.Public) == 0)
                        {
                            text = "#" + text;
                        }
                        return Object(propertyInfo.GetValue(obj, null) ?? NullName, propertyInfo.PropertyType, text, indent + 1, mode);
                    }
                }
            }

            return "";
        }

        public static string ToHex(this byte[] bytes, string padding = "")
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("X2"));
                sb.Append(padding);
            }
            return sb.ToString();
        }

        public static string HexDump(this byte[] data)
        {
            int length = data.Length;
            StringBuilder sb = new StringBuilder();
            int addressLength = ((length >= 16) ? ((length < 65536) ? 4 : 8) : 0);
            int idx = 0;
            while (idx < length)
            {
                if (idx != 0)
                {
                    sb.Append("\r\n");
                }
                if (addressLength > 0)
                {
                    sb.Append(idx.ToString("x" + addressLength) + " - ");
                }
                int loop;
                for (loop = 0; loop < 16; loop++)
                {
                    if ((idx + loop) < length)
                    {
                        sb.Append(data[idx + loop].ToString("x2") + " ");
                    }
                    else if (length > 16)
                    {
                        sb.Append("   ");
                    }
                    if (length > 16 && loop == 7)
                    {
                        sb.Append("- ");
                    }
                }
                sb.Append(": ");
                for (loop = 0; loop < 16 && loop < length; loop++)
                {
                    byte b = (idx + loop) < length ? data[idx + loop] : (byte)32;
                    sb.Append((char)((b >= 32 && b <= 126) ? b : 46));
                }
                idx += loop;
            }
            return sb.ToString();
        }
    }

    [TestClass]
    public class ObjectDumperTest
    {
        [TestMethod]
        public void TestObjectDumper()
        {
        }
    }
}
