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

            LabelText.AutoSize = true;
            LabelText.Dock = DockStyle.Fill;
            LabelText.TabIndex = 2;

            ListBox.Dock = DockStyle.Fill;
            ListBox.DisplayMember = "Key";
            ListBox.ValueMember = "Value";
            ListBox.FormattingEnabled = true;
            ListBox.KeyPress += (sender, e) => { if (e.KeyChar == 13) { ResultButton = DialogResult.OK; Close(); } };
            ListBox.TabIndex = 4;
            ListBox.CheckOnClick = true;

            ButtonOK.Dock = DockStyle.Fill;
            ButtonOK.TabIndex = 0;
            ButtonOK.Text = "OK";
            ButtonOK.Click += (sender, e) => { ResultButton = DialogResult.OK; Close(); };

            ButtonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            ButtonCancel.Dock = DockStyle.Fill;
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

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            layout.Controls.Add(LabelText, 0, 0);
            layout.Controls.Add(ListBox, 0, 1);
            layout.Controls.Add(buttonLayout, 0, 2);
            Controls.Add(layout);

            ClientSize = new Size(400, 370);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            SizeGripStyle = SizeGripStyle.Show;
            Load += (s, e) =>
            {
                var nc = Size - ClientSize;
                MinimumSize = new Size(300 + nc.Width, 200 + nc.Height);
            };
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
