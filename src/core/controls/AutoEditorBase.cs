namespace SehensWerte.Controls
{
    public class AutoEditorBase
    {
        public Action? UpdateControls;
        public Action? OnChanged;
        [AutoEditorForm.Hidden]
        public bool Updating;
    }
}
