namespace SehensWerte.Controls
{
    public class ListSelectForm : Form
    {
        private Button ButtonOK = new Button();
        private Button ButtonCancel = new Button();
        private Label LabelPrompt = new Label();
        private ListBox ListBox = new ListBox();
        private DialogResult ResultButton;
        private Dictionary<string, string> Unfiltered = new Dictionary<string, string>();
        private string FilterText = "";

        public string Title { set { Text = value; } }
        public string Prompt { set { LabelPrompt.Text = value; } }
        public Dictionary<string, string> Selection { set { SetSelection(value); } }
        public DialogResult Result => ResultButton;
        public string ResultString => ListBox.SelectedItem == null ? "" : ((KeyValuePair<string, string>)ListBox.SelectedItem).Value;

        public IEnumerable<string>? ResultStrings
        {
            get
            {
                if (ListBox.SelectedItems != null)
                {
                    List<string> list = new List<string>();
                    foreach (object selectedItem in ListBox.SelectedItems)
                    {
                        list.Add(((KeyValuePair<string, string>)selectedItem).Value);
                    }
                    return list;
                }
                return null;
            }
        }

        public string DefaultResponse
        {
            set
            {
                foreach (KeyValuePair<string, string> item in Unfiltered)
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
            ListBox.DrawMode = DrawMode.OwnerDrawFixed;
            ListBox.DrawItem += ListBox_DrawItem;
            ListBox.KeyPress += ListBox_KeyPress;
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


        private void ListBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0) return;

            string text = ((KeyValuePair<string, string>)ListBox.Items[e.Index]).Key;
            int matchIndex = text.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase);

            float x = e.Bounds.Left;
            float y = e.Bounds.Top;
            Font font = e.Font!;
            Brush normalBrush = Brushes.Black;
            Brush highlightBrush = Brushes.Blue;
            StringFormat format = StringFormat.GenericTypographic;
            format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

            if (matchIndex < 0)
            {
                e.Graphics.DrawString(text, font, normalBrush, x, y, format);
            }
            else
            {
                string before = text.Substring(0, matchIndex);
                if (before.Length > 0)
                {
                    e.Graphics.DrawString(before, font, normalBrush, x, y, format);
                    x += measure(before);
                }
                string match = text.Substring(matchIndex, FilterText.Length);
                if (match.Length > 0)
                {
                    e.Graphics.DrawString(match, font, highlightBrush, x, y, format);
                    x += measure(match);
                }
                string after = text.Substring(matchIndex + FilterText.Length);
                if (after.Length > 0)
                {
                    e.Graphics.DrawString(after, font, normalBrush, x, y, format);
                }
            }

            e.DrawFocusRectangle();

            float measure(string text)
            {
                CharacterRange[] ranges = { new CharacterRange(0, text.Length) };
                format.SetMeasurableCharacterRanges(ranges);
                Region[] regions = e.Graphics.MeasureCharacterRanges(text, font, new RectangleF(0, 0, e.Bounds.Width, e.Bounds.Height), format);
                return regions[0].GetBounds(e.Graphics).Width;
            }
        }


        private void SetSelection(IEnumerable<KeyValuePair<string, string>> list)
        {
            Unfiltered = list.ToDictionary(item => item.Key, item => item.Value);
            FilterText = "";
            Refilter();
        }

        private void ListBox_KeyPress(object? sender, KeyPressEventArgs e)
        {
            var previousFilter = FilterText;
            if (e.KeyChar == '\b')
            {
                if (FilterText.Length > 0)
                {
                    FilterText = FilterText.Substring(0, FilterText.Length - 1);
                }
            }
            else if (e.KeyChar == 13)
            {
                ResultButton = DialogResult.OK;
                Close();
            }
            else
            {
                var testText = FilterText + e.KeyChar;
                if (Unfiltered.Any(x => x.Key.Contains(testText, StringComparison.OrdinalIgnoreCase)))
                {
                    FilterText = testText;
                }
                e.Handled = true;
            }

            if (FilterText != previousFilter)
            {
                Refilter();
            }
        }

        private void Refilter()
        {
            ListBox.BeginUpdate();
            try
            {
                bool reset = false;
                ListBox.Items.Clear();
                foreach (var item in Unfiltered
                    .Where(item => item.Key.StartsWith(FilterText, StringComparison.OrdinalIgnoreCase)))
                {
                    ListBox.Items.Add(item);
                    reset = true;
                }
                foreach (var item in Unfiltered
                    .Where(item => item.Key.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                                && !item.Key.StartsWith(FilterText, StringComparison.OrdinalIgnoreCase)))
                {
                    ListBox.Items.Add(item);
                    reset = true;
                }
                if (reset)
                {
                    ListBox.SelectedIndex = 0;
                }
            }
            finally
            {
                ListBox.EndUpdate();
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
            listSelectForm.SetSelection(selection);
            listSelectForm.DefaultResponse = defaultResponse;
            listSelectForm.ShowDialog();
            return listSelectForm.ResultButton == DialogResult.OK ? listSelectForm.ResultString : null;
        }

        public static IEnumerable<string>? ShowMultiselect(string prompt, string title, IEnumerable<KeyValuePair<string, string>> selection, string defaultResponse)
        {
            ListSelectForm listSelectForm = new ListSelectForm();
            listSelectForm.Title = title;
            listSelectForm.Prompt = prompt;
            listSelectForm.SetSelection(selection);
            listSelectForm.DefaultResponse = defaultResponse;
            listSelectForm.ListBox.SelectionMode = SelectionMode.MultiExtended;
            listSelectForm.ShowDialog();
            return listSelectForm.ResultButton == DialogResult.OK ? listSelectForm.ResultStrings : null;
        }
    }
}
