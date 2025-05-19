using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Win32; // registry
using SehensWerte.Utils;

namespace SehensWerte.Controls
{
    public class InputFieldForm : Form
    {
        private DialogResult ResultButton = DialogResult.None;

        private Button ButtonOK;
        private Button ButtonCancel;
        private Label LabelText;
        private TextBox EditResult;

        public string Title { set { Text = value; } }
        public string Prompt { set { LabelText.Text = value; } }
        private bool m_MultiLine = false;
        public bool MultiLine
        {
            get => m_MultiLine;
            set
            {
                m_MultiLine = value;
                MinimumSize = GetMinimumSize();
                MaximumSize = MultiLine ? Screen.FromControl(this).WorkingArea.Size : new Size(Screen.FromControl(this).WorkingArea.Width, MinimumSize.Height);
                UpdateControls();
            }
        }
        public DialogResult Result => ResultButton;
        public string ResultString => EditResult.Text;

        public string DefaultResponse { set { EditResult.Text = value; EditResult.SelectAll(); } }
        public bool Password
        {
            get { return EditResult.UseSystemPasswordChar; }
            set { EditResult.UseSystemPasswordChar = value; }
        }
        private static ConcurrentDictionary<string, string> Cache = new ConcurrentDictionary<string, string>();

        public InputFieldForm()
        {
            ButtonOK = new Button();
            EditResult = new TextBox();
            EditResult.MaxLength = 0;
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
            ButtonOK.Click += (sender, e) => { ResultButton = DialogResult.OK; Close(); };
            ButtonCancel.Click += (sender, e) => { ResultButton = DialogResult.Cancel; Close(); };

            AcceptButton = this.ButtonOK;
            CancelButton = this.ButtonCancel;

            Controls.Add(this.ButtonCancel);
            Controls.Add(this.LabelText);
            Controls.Add(this.EditResult);
            Controls.Add(this.ButtonOK);

            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            SizeGripStyle = SizeGripStyle.Show;

            Shown += (sender, e) => { ActiveControl = EditResult; };
            Resize += (s, e) => UpdateControls();
            Load += (s, e) =>
            {
                MinimumSize = GetMinimumSize();
                Size = MinimumSize;
                UpdateControls();
            };

            ResumeLayout(false);
        }

        private Size GetMinimumSize() => new System.Drawing.Size(400, 8 + 24 + 8 + (MultiLine ? 200 : 20) + 8 + 32 + 8) + Size - ClientSize;

        private void UpdateControls()
        {
            LabelText.AutoSize = true;
            LabelText.MaximumSize = new Size(ClientSize.Width - 16, 0);
            LabelText.Location = new System.Drawing.Point(8, 8);

            EditResult.Location = new System.Drawing.Point(8, LabelText.Bottom + 16);
            EditResult.Multiline = MultiLine;
            EditResult.MaxLength = 0;
            EditResult.Size = new System.Drawing.Size(ClientSize.Width - 16, (MultiLine ? 200 : 20) + Size.Height - MinimumSize.Height);
            EditResult.TabIndex = 1;
            EditResult.ScrollBars = MultiLine ? ScrollBars.Vertical : ScrollBars.None;

            ButtonOK.Location = new System.Drawing.Point(8, EditResult.Bottom + 8);
            ButtonOK.Size = new System.Drawing.Size((ClientSize.Width - 24) / 2, 32);
            ButtonOK.TabIndex = 0;
            ButtonOK.Text = "OK";

            ButtonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            ButtonCancel.Location = new System.Drawing.Point(ButtonOK.Right + 8, EditResult.Bottom + 8);
            ButtonCancel.Size = new System.Drawing.Size((ClientSize.Width - 24) / 2, 32);
            ButtonCancel.TabIndex = 3;
            ButtonCancel.Text = "Cancel";
        }

        public static string? Show(string prompt, string title,
                                   object? defaultResponse = null, bool password = false,
                                   bool multiLine = false, bool cache = false, bool save = false,
                                   string? saveKey = null,
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
