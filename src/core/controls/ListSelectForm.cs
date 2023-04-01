namespace SehensWerte.Controls
{
    public class ListSelectForm : Form
    {
        private Button ButtonOK = new Button();
        private Button ButtonCancel = new Button();
        private Label LabelPrompt = new Label();
        private ListBox ListBox = new ListBox();
        private DialogResult ResultButton;

        public string Title { set { Text = value; } }
        public string Prompt { set { LabelPrompt.Text = value; } }
        public Dictionary<string, string> Selection { set { Add(value); } }
        public DialogResult Result => ResultButton;
        public string ResultString => ListBox.SelectedItem == null ? "" : ((KeyValuePair<string, string>)ListBox.SelectedItem).Value;

        public IEnumerable<string>? ResultStrings
        {
            get
            {
                if (ListBox.SelectedItems != null)
                {
                    List<string> list = new List<string>();
                    {
                        foreach (object selectedItem in ListBox.SelectedItems)
                        {
                            list.Add(((KeyValuePair<string, string>)selectedItem).Value);
                        }
                        return list;
                    }
                }
                return null;
            }
        }

        public string DefaultResponse
        {
            set
            {
                foreach (KeyValuePair<string, string> item in ListBox.Items)
                {
                    if (item.Key == value)
                    {
                        ListBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        public ListSelectForm()
        {
            SuspendLayout();
            base.ClientSize = new Size(400, 8 + 64 + 8 + 240 + 8 + 32 + 8);
            LabelPrompt.Location = new Point(8, 8);
            LabelPrompt.Size = new Size(base.ClientSize.Width - 16, 64);
            ListBox.DisplayMember = "Key";
            ListBox.FormattingEnabled = true;
            ListBox.ItemHeight = 20;
            ListBox.Location = new Point(8, LabelPrompt.Bottom + 8);
            ListBox.Size = new Size(base.ClientSize.Width - 16, 240);
            ListBox.TabIndex = 0;
            ListBox.ValueMember = "Value";
            ListBox.KeyPress += delegate (object? sender, KeyPressEventArgs e)
            {
                if (e.KeyChar == '\r')
                {
                    ResultButton = DialogResult.OK;
                    Close();
                }
            };
            ListBox.MouseDoubleClick += delegate
            {
                ResultButton = DialogResult.OK;
                Close();
            };
            ButtonOK.Location = new Point(8, ListBox.Bottom + 8);
            ButtonOK.Size = new Size((base.ClientSize.Width - 24) / 2, 32);
            ButtonOK.TabIndex = 0;
            ButtonOK.Text = "OK";
            ButtonOK.Click += delegate
            {
                ResultButton = DialogResult.OK;
                Close();
            };
            ButtonCancel.DialogResult = DialogResult.Cancel;
            ButtonCancel.Location = new Point(ButtonOK.Right + 8, ListBox.Bottom + 8);
            ButtonCancel.Size = new Size((base.ClientSize.Width - 24) / 2, 32);
            ButtonCancel.TabIndex = 3;
            ButtonCancel.Text = "Cancel";
            ButtonCancel.Click += delegate
            {
                ResultButton = DialogResult.Cancel;
                Close();
            };
            base.CancelButton = ButtonCancel;
            base.Controls.Add(ListBox);
            base.Controls.Add(ButtonCancel);
            base.Controls.Add(LabelPrompt);
            base.Controls.Add(ButtonOK);
            base.FormBorderStyle = FormBorderStyle.FixedDialog;
            base.MaximizeBox = false;
            base.SizeGripStyle = SizeGripStyle.Hide;
            base.Shown += delegate
            {
                base.ActiveControl = ListBox;
            };
            ResumeLayout(performLayout: false);
        }

        private void Add(IEnumerable<KeyValuePair<string, string>> list)
        {
            foreach (var item in list)
            {
                ListBox.Items.Add(item);
            }
        }

        public static string? Show(string prompt, string title, Dictionary<string, string> selection)
        {
            return Show(prompt, title, selection, "");
        }

        public static string? Show(string prompt, string title, IEnumerable<string> selection)
        {
            return Show(prompt, title, selection, "");
        }

        public static string? Show(string prompt, string title, Type list, object defaultResponse)
        {
            return Show(prompt, title, Enum.GetNames(list), defaultResponse?.ToString() ?? "");
        }

        public static string? Show(string prompt, string title, IEnumerable<string> selection, string defaultResponse)
        {
            return Show(prompt, title, selection.ToDictionary((x) => x, (x) => x), defaultResponse);
        }

        public static string? Show(string prompt, string title, IEnumerable<KeyValuePair<string, string>> selection, string defaultResponse)
        {
            ListSelectForm listSelectForm = new ListSelectForm();
            listSelectForm.Title = title;
            listSelectForm.Prompt = prompt;
            listSelectForm.Add(selection);
            listSelectForm.DefaultResponse = defaultResponse;
            listSelectForm.ShowDialog();
            return listSelectForm.ResultButton == DialogResult.OK ? listSelectForm.ResultString : null;
        }

        public static IEnumerable<string>? ShowMultiselect(string prompt, string title, IEnumerable<KeyValuePair<string, string>> selection, string defaultResponse)
        {
            ListSelectForm listSelectForm = new ListSelectForm();
            listSelectForm.Title = title;
            listSelectForm.Prompt = prompt;
            listSelectForm.Add(selection);
            listSelectForm.DefaultResponse = defaultResponse;
            listSelectForm.ListBox.SelectionMode = SelectionMode.MultiExtended;
            listSelectForm.ShowDialog();
            return listSelectForm.ResultButton == DialogResult.OK ? listSelectForm.ResultStrings : null;
        }
    }
}
