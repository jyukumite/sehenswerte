using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SehensWerte
{
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
}