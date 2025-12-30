namespace SehensWerte.Controls
{
    public class EnhancedTextBox : TextBox
    {
        public event EventHandler<PasteEventArgs>? Pasting;

        public class PasteEventArgs : EventArgs
        {
            public bool Handled { get; set; }
        }

        private const int WM_PASTE = 0x0302;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_PASTE)
            {
                PasteEventArgs args = new();
                Pasting?.Invoke(this, args);
                if (args.Handled)
                {
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }
}
