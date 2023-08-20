namespace SehensWerte.Controls
{
    public class AutoEditorBase
    {
        [AutoEditor.Hidden]
        public Action? UpdateControls;
        [AutoEditor.Hidden]
        public Action? OnChanged;
        [AutoEditor.Hidden]
        public bool Updating;
    }
}
