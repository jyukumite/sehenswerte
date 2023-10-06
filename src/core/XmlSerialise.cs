using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SehensWerte
{
    // helper to extract and insert any fields or properties marked with [XmlSave] to/from an object
    //
    // put this in the target class
    //
    // [XmlAnyElement]
    // public List<XmlElement> OtherElements = new List<XmlElement>();
    //
    // and make a helper class for serialising, e.g.
    // 
    // public class Skin
    // {
    //     [XmlAnyElement]
    //     public List<XmlElement> OtherElements = new List<XmlElement>();
    //     public Skin() { }
    //     public Skin(Controls.Sehens.Skin obj) // build from real object
    //     {
    //         OtherElements = XmlSaveAttribute.Extract(obj);
    //     }
    //     public void SaveTo(Controls.Sehens.Skin obj) // save back to real object
    //     {
    //         XmlSaveAttribute.Inject(obj, OtherElements);
    //     }
    // }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    sealed public class XmlSaveAttribute : Attribute
    {
        public string? Name;
        public bool NestedXml; // saved item is serialised as xml
        public Type[]? NestedDerivedTypes; // passed to serialise to know about derived types

        public XmlSaveAttribute(string? name = null, bool nestedXml = false, Type[]? nestedDerivedTypes = null)
        {
            Name = name;
            NestedDerivedTypes = nestedDerivedTypes;
            NestedXml = nestedXml;
        }

        public static List<XmlElement> Extract(object obj)
        {
            var elements = new List<XmlElement>();

            if (obj == null)
            {
                return elements;
            }

            var objectType = obj.GetType();
            var fields = objectType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var properties = objectType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var field in fields)
            {
                var attribute = field.GetCustomAttribute<XmlSaveAttribute>();
                if (attribute != null)
                {
                    var fieldName = attribute.Name == null ? field.Name : attribute.Name;
                    var value = field.GetValue(obj);
                    elements.Add(EncodeXmlElement(fieldName, value, attribute));
                }
            }

            foreach (var property in properties)
            {
                var attribute = property.GetCustomAttribute<XmlSaveAttribute>();
                if (attribute != null)
                {
                    var propertyName = attribute.Name == null ? property.Name : attribute.Name;
                    var value = property.GetValue(obj);
                    elements.Add(EncodeXmlElement(propertyName, value, attribute));
                }
            }

            return elements;
        }

        public static void Inject(object obj, List<XmlElement> otherElements)
        {
            if (obj == null)
            {
                return;
            }

            if (otherElements == null || otherElements.Count == 0)
            {
                return;
            }

            var objectType = obj.GetType();
            var fields = objectType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var properties = objectType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var element in otherElements)
            {
                try
                {
                    var elementName = element.Name;
                    var field = fields.FirstOrDefault(f => f.Name == elementName);
                    var property = properties.FirstOrDefault(p => p.Name == elementName);
                    if (field != null)
                    {
                        var attribute = field.GetCustomAttribute<XmlSaveAttribute>();
                        if (attribute != null)
                        {
                            field.SetValue(obj, DecodeXmlElement(element, field.FieldType, attribute, field.FieldType));
                        }
                    }
                    else if (property != null)
                    {
                        var attribute = property.GetCustomAttribute<XmlSaveAttribute>();
                        if (attribute != null)
                        {
                            property.SetValue(obj, DecodeXmlElement(element, property.PropertyType, attribute, property.PropertyType));
                        }
                    }
                }
                catch { } //quietly drop errors
            }
        }

        private static XmlElement EncodeXmlElement(string fieldName, object? value, XmlSaveAttribute attribute)
        {
            var doc = new XmlDocument();
            var xmlElement = doc.CreateElement(fieldName);
            if (value != null)
            {
                if (attribute.NestedXml)
                {
                    xmlElement.InnerXml = value.ToXml(attribute.NestedDerivedTypes, compact:true);
                }
                else if (value.GetType() == typeof(Color))
                {
                    xmlElement.InnerText = ((Color)value).ToArgb().ToString();
                }
                else if (value.GetType() == typeof(double))
                {
                    xmlElement.InnerXml = ((double)value).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    xmlElement.InnerXml = value.ToString() ?? "";
                }
            }
            return xmlElement;
        }

        private static string GetXmlFirstElement(string text)
        {
            using (StringReader sr = new StringReader(text))
            using (XmlTextReader xmlReader = new XmlTextReader(sr))
            {
                xmlReader.WhitespaceHandling = WhitespaceHandling.Significant;
                xmlReader.Normalization = true;
                xmlReader.XmlResolver = null;
                while (xmlReader.Read())
                {
                    if (xmlReader.NodeType == XmlNodeType.Element)
                    {
                        return xmlReader.Name;
                    }
                }
            }
            return "";
        }

        private static object? DecodeXmlElement(XmlElement element, Type fieldType, XmlSaveAttribute attribute, Type type)
        {
            object? result = null;
            if (attribute.NestedXml)
            {
                List<Type> types = new List<Type>() { type };
                if (attribute.NestedDerivedTypes != null)
                {
                    types.AddRange(attribute.NestedDerivedTypes);
                }
                try
                {
                    if (element.InnerXml != "")
                    {
                        // change the root type to the derived type, work around XmlSerializer.Deserialize not finding derived types
                        var newType = types.FirstOrDefault(t => t.Name == GetXmlFirstElement(element.InnerXml), (Type?)null);
                        using (StringReader sr = new StringReader(element.InnerXml))
                        {
                            result = new XmlSerializer(newType ?? fieldType, types.ToArray()).Deserialize((TextReader)sr);
                        }
                    }
                }
                catch
                {
                    return null;
                }
            }
            else if (fieldType.IsEnum)
            {
                result = (object?)Enum.Parse(fieldType, element.InnerText);
            }
            else if (fieldType == typeof(Color))
            {
                result = Color.FromArgb((int)Convert.ChangeType(element.InnerText, typeof(int)));
            }
            else if (fieldType.GetConstructor(new[] { typeof(string) }) != null)
            {
                result = Activator.CreateInstance(fieldType, element.InnerText);
            }
            else
            {
                result = Convert.ChangeType(element.InnerText, fieldType, CultureInfo.InvariantCulture);
            }
            return result;
        }
    }

    // Based on https://weblogs.asp.net/pwelter34/444961
    [XmlRoot("dictionary")]
    public class XmlSerialisableDictionary<TKey, TValue>
        : Dictionary<TKey, TValue>, IXmlSerializable
        where TValue : notnull
        where TKey : notnull
    {
        private const string ItemTag = "item";

        public void ReadXml(XmlReader reader)
        {
            if (reader.IsEmptyElement) return;

            var keySerializer = new XmlSerializer(typeof(TKey));
            var valueSerializer = new XmlSerializer(typeof(TValue));

            reader.ReadStartElement();

            while (reader.IsStartElement(ItemTag))
            {
                reader.ReadStartElement(ItemTag);
                TKey? key = (TKey?)keySerializer.Deserialize(reader);
                TValue? value = (TValue?)valueSerializer.Deserialize(reader);
                reader.ReadEndElement();
                if (key != null && value != null)
                {
                    this.Add(key, value);
                }
            }
            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            var keySerializer = new XmlSerializer(typeof(TKey));
            var valueSerializer = new XmlSerializer(typeof(TValue));

            foreach (var kvp in this)
            {
                writer.WriteStartElement(ItemTag);
                keySerializer.Serialize(writer, kvp.Key);
                valueSerializer.Serialize(writer, kvp.Value);
                writer.WriteEndElement();
            }
        }

        public XmlSchema? GetSchema()
        {
            return null;
        }

        //unit tested as part of StringExtensions
    }

    [TestClass]
    public class XmlSaveAttributeTest
    {
        //fixme: unit test XmlSaveAttribute

        public class XmlNestTest
        {
            public string a;
            [XmlAttribute]
            public string b;
        }

        public class XmlTest
        {
            public double a;
            public double b;
            public string c;

            public XmlSerialisableDictionary<string, double> d;

            public XmlNestTest e;
            private double f = 2;
            public double fget => f; // getter only
            public void fset(double to) { f = to; } // not a property
            public string[] g = new string[0];
            public string[] h = new string[0];
        }

        [TestMethod]
        public void TestXml()
        {
            XmlTest xmlTest = new XmlTest()
            {
                a = 1,
                b = 2,
                c = "§",
                d = new XmlSerialisableDictionary<string, double>() { { "a", 1 }, { "b", 2 } },
                e = new XmlNestTest() { a = "5", b = "6" },
                g = new string[] { "hello", "world" },
                h = new string[] { },
            };
            xmlTest.fset(42);

            string xml = xmlTest.ToXml();
            //Clipboard.SetText(xml);
            string expected = @"
<?xml version=""1.0"" encoding=""utf-8""?>
<XmlTest xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <a>1</a>
  <b>2</b>
  <c>§</c>
  <d>
    <item>
      <string>a</string>
      <double>1</double>
    </item>
    <item>
      <string>b</string>
      <double>2</double>
    </item>
  </d>
  <e b=""6"">
    <a>5</a>
  </e>
  <g>
    <string>hello</string>
    <string>world</string>
  </g>
  <h />
</XmlTest>
";

            Assert.AreEqual(
                xml.Replace(" ", "").Replace("\r", "").Replace("\n", ""),
                expected.Replace(" ", "").Replace("\r", "").Replace("\n", ""));

            XmlTest? result = xml.FromXml<XmlTest>();
            Assert.IsNotNull(result);

            Assert.AreEqual(xmlTest.a, result!.a);
            Assert.AreEqual(xmlTest.b, result!.b);
            Assert.AreEqual(xmlTest.c, result!.c);
            CollectionAssert.AreEqual(xmlTest.d, result!.d);
            Assert.AreEqual(xmlTest.e.a, result!.e.a);
            Assert.AreEqual(xmlTest.e.b, result!.e.b);
            Assert.AreEqual(2, result!.fget);
            CollectionAssert.AreEqual(xmlTest.g, result!.g);
            CollectionAssert.AreEqual(xmlTest.h, result!.h);
        }
    }

}

