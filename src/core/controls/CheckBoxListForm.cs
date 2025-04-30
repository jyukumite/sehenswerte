namespace SehensWerte.Controls
{
    public class CheckBoxListForm : Form
    {
        private DialogResult ResultButton = System.Windows.Forms.DialogResult.None;

        private Button ButtonOK;
        private Button ButtonCancel;
        private Label LabelText;
        private CheckedListBox ListBox;

        public string Title { set { Text = value; } }
        public string Prompt { set { LabelText.Text = value; } }
        public DialogResult Result => ResultButton;
        public string ResultString => ((KeyValuePair<string, string>)ListBox.SelectedItem).Value;

        public List<string> Selection
        {
            set
            {
                foreach (string v in value.Where(x => ListBox.Items.Contains(x) == false))
                {
                    ListBox.Items.Add(v);
                }
            }
        }

        public List<string> CheckedSelection
        {
            set
            {
                foreach (string v in value.Where(x => ListBox.Items.Contains(x) == false))
                {
                    ListBox.Items.Add(v);
                }
                foreach (int v in ListBox.CheckedIndices)
                {
                    ListBox.SetItemChecked(v, false);
                }
                foreach (string v in value.Where(x => ListBox.Items.Contains(x)))
                {
                    ListBox.SetItemChecked(ListBox.Items.IndexOf(v), true);
                }
            }
            get
            {
                List<string> result = new List<string>();
                foreach (int v in ListBox.CheckedIndices)
                {
                    result.Add((string)ListBox.Items[v]);
                }
                return result;
            }
        }

        public string DefaultResponse
        {
            set
            {
                foreach (KeyValuePair<string, string> pair in ListBox.Items)
                {
                    if (pair.Key == value)
                    {
                        ListBox.SelectedItem = pair;
                        break;
                    }
                }
            }
        }

        public CheckBoxListForm()
        {
            ButtonOK = new Button();
            LabelText = new Label();
            ButtonCancel = new Button();
            ListBox = new CheckedListBox();
            SuspendLayout();

            ClientSize = new System.Drawing.Size(400, 8 + 64 + 8 + 240 + 8 + 32 + 8);

            LabelText.Location = new System.Drawing.Point(8, 8);
            LabelText.Size = new System.Drawing.Size(ClientSize.Width - 16, 64);
            LabelText.TabIndex = 2;

            ListBox.Location = new System.Drawing.Point(8, LabelText.Bottom + 8);
            ListBox.Size = new System.Drawing.Size(ClientSize.Width - 16, 240);
            ListBox.DisplayMember = "Key";
            ListBox.ValueMember = "Value";
            ListBox.FormattingEnabled = true;
            ListBox.KeyPress += (sender, e) => { if (e.KeyChar == 13) { ResultButton = DialogResult.OK; Close(); } };
            ListBox.TabIndex = 4;
            ListBox.CheckOnClick = true;

            ButtonOK.Location = new System.Drawing.Point(8, ListBox.Bottom + 8);
            ButtonOK.Size = new System.Drawing.Size((ClientSize.Width - 24) / 2, 32);
            ButtonOK.TabIndex = 0;
            ButtonOK.Text = "OK";
            ButtonOK.Click += (sender, e) => { ResultButton = DialogResult.OK; Close(); };

            ButtonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            ButtonCancel.Location = new System.Drawing.Point(ButtonOK.Right + 8, ListBox.Bottom + 8);
            ButtonCancel.Size = new System.Drawing.Size((ClientSize.Width - 24) / 2, 32);
            ButtonCancel.TabIndex = 3;
            ButtonCancel.Text = "Cancel";
            ButtonCancel.Click += (sender, e) => { ResultButton = DialogResult.Cancel; Close(); };

            AcceptButton = this.ButtonOK;
            CancelButton = this.ButtonCancel;

            Controls.Add(this.ListBox);
            Controls.Add(this.ButtonCancel);
            Controls.Add(this.LabelText);
            Controls.Add(this.ButtonOK);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            Shown += (sender, e) => { ActiveControl = ListBox; };
            ResumeLayout(false);
            PerformLayout();
        }

        public static uint Show(string Prompt, string Title, uint binary, Type type)
        {
            List<string> names = new List<string>(Enum.GetNames(type));
            List<string> selection = new List<string>();
            foreach (string s in names)
            {
                if ((binary & (uint)(int)Enum.Parse(type, s)) != 0)
                {
                    selection.Add(s);
                }
            }
            names.Sort();
            selection = Show(Prompt, Title, names, selection) ?? selection;

            uint result = 0;
            foreach (string s in selection)
            {
                result |= (uint)(int)Enum.Parse(type, s);
            }
            return result;
        }

        public static List<string>? Show(string prompt, string title, IEnumerable<string> selection, IEnumerable<string> checkedSelection)
        {
            CheckBoxListForm input = new CheckBoxListForm();
            input.Title = title;
            input.Prompt = prompt;
            input.Selection = new List<string>(selection);
            input.CheckedSelection = new List<string>(checkedSelection);
            input.ShowDialog();
            return (input.ResultButton == DialogResult.OK) ? input.CheckedSelection : null;
        }
    }
}
