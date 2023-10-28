using System.Xml.Serialization;

namespace SehensWerte.Controls
{
    public class AutoEditorBase
    {
        [AutoEditor.Hidden]
        [XmlIgnore]
        public Action? UpdateControls; // uses BeginInvoke if required

        [AutoEditor.Hidden]
        [XmlIgnore]
        public Action? OnChanged;

        [AutoEditor.Hidden]
        [XmlIgnore]
        public bool Updating;
    }
}
