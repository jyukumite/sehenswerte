using SehensWerte.Utils;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SehensWerte.Controls
{
    public class InputFieldForm : Form
    {
        private DialogResult ResultButton = DialogResult.None;

        private Button ButtonOK;
        private Button ButtonCancel;
        private Label LabelText;
        private EnhancedTextBox EditResult;

        public string Title { set => Text = value; }
        public string Prompt { set => LabelText.Text = value; }
        private bool m_MultiLine = false;
        public bool MultiLine
        {
            get => m_MultiLine;
            set
            {
                m_MultiLine = value;
                m_Layout.RowStyles[1] = value
                    ? new RowStyle(SizeType.Percent, 100f)
                    : new RowStyle(SizeType.AutoSize);
                EditResult.Multiline = value;
                EditResult.ScrollBars = value ? ScrollBars.Vertical : ScrollBars.None;
                MinimumSize = GetMinimumSize();
                MaximumSize = MultiLine ? Screen.FromControl(this).WorkingArea.Size : new Size(Screen.FromControl(this).WorkingArea.Width, MinimumSize.Height);
            }
        }
        public DialogResult Result => ResultButton;
        public string ResultString => EditResult.Text;

        public string DefaultResponse { set { EditResult.Text = value; EditResult.SelectAll(); } }
        public bool Password
        {
            get => EditResult.UseSystemPasswordChar; 
            set => EditResult.UseSystemPasswordChar = value;
        }

        private Func<string, string>? PasteHook;
        private TableLayoutPanel m_Layout;

        private static ConcurrentDictionary<string, string> Cache = new();

        public InputFieldForm()
        {
            ButtonOK = new Button();
            EditResult = new EnhancedTextBox();
            LabelText = new Label();
            ButtonCancel = new Button();
            SuspendLayout();

            EditResult.PreviewKeyDown += (sender, e) =>
            {
                if (sender != null && e.KeyCode == Keys.Enter)
                {
                    if (MultiLine)
                    {
                        e.IsInputKey = true;
                    }
                    else
                    {
                        ResultButton = DialogResult.OK;
                        Close();
                    }
                }
            };

            EditResult.Pasting += (sender, e) =>
            {
                try
                {
                    string clip = Clipboard.GetText() ?? "";
                    string processed = PasteHook?.Invoke(clip) ?? clip;
                    int start = EditResult.SelectionStart;
                    int len = EditResult.SelectionLength;
                    EditResult.Text = EditResult.Text.Remove(start, len).Insert(start, processed);
                    EditResult.SelectionStart = start + processed.Length;
                    e.Handled = true;
                }
                catch { }
            };

            LabelText.AutoSize = true;
            LabelText.Dock = DockStyle.Fill;

            EditResult.Dock = DockStyle.Fill;
            EditResult.MaxLength = 0;
            EditResult.TabIndex = 1;

            ButtonOK.Dock = DockStyle.Fill;
            ButtonOK.TabIndex = 0;
            ButtonOK.Text = "OK";
            ButtonOK.Click += (sender, e) => { ResultButton = DialogResult.OK; Close(); };

            ButtonCancel.Dock = DockStyle.Fill;
            ButtonCancel.DialogResult = DialogResult.Cancel;
            ButtonCancel.TabIndex = 3;
            ButtonCancel.Text = "Cancel";
            ButtonCancel.Click += (sender, e) => { ResultButton = DialogResult.Cancel; Close(); };

            AcceptButton = this.ButtonOK;
            CancelButton = this.ButtonCancel;

            var buttonLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                ColumnCount = 2,
                RowCount = 1,
                Padding = Padding.Empty,
            };
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            buttonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            buttonLayout.Controls.Add(ButtonOK, 0, 0);
            buttonLayout.Controls.Add(ButtonCancel, 1, 0);

            m_Layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8),
            };
            m_Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            m_Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            m_Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            m_Layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            m_Layout.Controls.Add(LabelText, 0, 0);
            m_Layout.Controls.Add(EditResult, 0, 1);
            m_Layout.Controls.Add(buttonLayout, 0, 2);
            Controls.Add(m_Layout);

            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            SizeGripStyle = SizeGripStyle.Show;
            ClientSize = new Size(400, 150);

            Shown += (sender, e) => { ActiveControl = EditResult; };
            Load += (s, e) =>
            {
                var nc = Size - ClientSize;
                int minClientH = LabelText.Height + 16 + EditResult.PreferredHeight + 8 + 40;
                MinimumSize = new Size(400 + nc.Width, minClientH + nc.Height);
                if (!MultiLine) MaximumSize = new Size(Screen.FromControl(this).WorkingArea.Width, MinimumSize.Height);
                ClientSize = new Size(ClientSize.Width, MultiLine ? LabelText.Height + 16 + EditResult.PreferredHeight * 8 + 8 + 40 : minClientH);
            };

            ResumeLayout(false);
        }

        private Size GetMinimumSize() => new System.Drawing.Size(400, 8 + 24 + 8 + (MultiLine ? 200 : EditResult.PreferredHeight) + 8 + 32 + 8) + Size - ClientSize;


        public static string? Show(string prompt, string title,
                                   object? defaultResponse = null, bool password = false,
                                   bool multiLine = false, bool cache = false, bool save = false,
                                   string? saveKey = null, bool regex = false,
                         [System.Runtime.CompilerServices.CallerFilePath] string cacheFilePath = "",
                         [System.Runtime.CompilerServices.CallerLineNumber] int cacheLineNumber = 0)
        {
            string key = saveKey ?? $"{prompt}:{title}:{cacheFilePath}[{cacheLineNumber}]";
            bool persistInRegistry = save || saveKey != null;
            if (cache)
            {
                string? cached;
                if (Cache.TryGetValue(key, out cached))
                {
                    defaultResponse = cached;
                }
            }

            if (persistInRegistry)
            {
                if (WindowsRegistry.Read(key, out string? savedValue) && savedValue != null)
                {
                    defaultResponse = savedValue;
                }
            }

            InputFieldForm form = new InputFieldForm();
            form.Title = title;
            form.Prompt = prompt;
            form.DefaultResponse = defaultResponse?.ToString() ?? "";
            form.Password = password;
            form.MultiLine = multiLine;
            if (regex)
            {
                form.PasteHook = (s) =>
                    ((ModifierKeys & Keys.Shift) == Keys.Shift) ? s :
                    String.Join("|",
                       (s?.ToString() ?? "")
                        .Trim()
                        .Replace("\r\n", "\r").Replace("\n", "\r")
                        .Split("\r")
                        .Select(x => $"^{Regex.Escape(x)}$"));
            }
            form.ShowDialog();
            form.TopMost = true;

            if (form.ResultButton == DialogResult.OK)
            {
                if (cache && !password)
                {
                    Cache[key] = form.ResultString;
                }
                if (persistInRegistry && !password)
                {
                    WindowsRegistry.Write(key, form.ResultString);
                }
                return form.ResultString;
            }
            else
            {
                return null;
            }
        }
    }
}
