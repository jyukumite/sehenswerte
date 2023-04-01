using System.Collections.Concurrent;

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
        public bool MultiLine { get => m_MultiLine; set { m_MultiLine = value; UpdateControls(); } }
        public DialogResult Result => ResultButton;
        public string ResultString => EditResult.Text;
        public string DefaultResponse { set { EditResult.Text = value; EditResult.SelectAll(); } }
        public bool Password
        {
            get { return EditResult.PasswordChar == '*'; }
            set { EditResult.PasswordChar = value ? '*' : (char)0; }
        }
        private static ConcurrentDictionary<string, string> Cache = new ConcurrentDictionary<string, string>();

        public InputFieldForm()
        {
            ButtonOK = new Button();
            EditResult = new TextBox();
            LabelText = new Label();
            ButtonCancel = new Button();
            SuspendLayout();

            UpdateControls();
            EditResult.KeyPress += (sender, e) => { if (e.KeyChar == 13 && MultiLine) { e.Handled = true; ResultButton = DialogResult.OK; Close(); } };
            ButtonOK.Click += (sender, e) => { ResultButton = DialogResult.OK; Close(); };
            ButtonCancel.Click += (sender, e) => { ResultButton = DialogResult.Cancel; Close(); };

            AcceptButton = this.ButtonOK;
            CancelButton = this.ButtonCancel;

            Controls.Add(this.ButtonCancel);
            Controls.Add(this.LabelText);
            Controls.Add(this.EditResult);
            Controls.Add(this.ButtonOK);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            SizeGripStyle = SizeGripStyle.Hide;
            Shown += (sender, e) => { ActiveControl = EditResult; };
            ResumeLayout(false);
        }

        private void UpdateControls()
        {
            ClientSize = new System.Drawing.Size(400, 8 + 64 + 8 + (MultiLine ? 200 : 20) + 8 + 32 + 8);

            LabelText.Location = new System.Drawing.Point(8, 8);
            LabelText.Size = new System.Drawing.Size(ClientSize.Width - 16, 64);

            EditResult.Location = new System.Drawing.Point(8, LabelText.Bottom + 8);
            EditResult.Multiline = MultiLine;
            EditResult.Size = new System.Drawing.Size(ClientSize.Width - 16, MultiLine ? 200 : 20);
            EditResult.TabIndex = 1;

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

        public static string? Show(string prompt, string title, object? defaultResponse = null, bool password = false, bool multiLine = false, bool cache = false,
                         [System.Runtime.CompilerServices.CallerFilePath] string cacheFilePath = "",
                         [System.Runtime.CompilerServices.CallerLineNumber] int cacheLineNumber = 0)
        {
            string key = $"{prompt}:{title}:{cacheFilePath}[{cacheLineNumber}]";
            if (cache)
            {
                string? cached;
                if (Cache.TryGetValue(key, out cached))
                {
                    defaultResponse = cached;
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
                return form.ResultString;
            }
            else
                return null;
        }
    }
}
