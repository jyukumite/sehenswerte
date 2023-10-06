using System.Xml.Serialization;

namespace SehensWerte.Controls
{
    public class AutoEditorBase
    {
        [AutoEditor.Hidden]
        [XmlIgnore]
        public Action? UpdateControls;

        [AutoEditor.Hidden]
        [XmlIgnore]
        public Action? OnChanged;

        [AutoEditor.Hidden]
        [XmlIgnore]
        public bool Updating;
    }
}
