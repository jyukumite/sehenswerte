namespace SehensWerte.Controls
{
    public class CheckBoxListForm : Form
    {
        private DialogResult ResultButton = System.Windows.Forms.DialogResult.None;

        private Button ButtonOK;
        private Button ButtonCancel;
        private Button ButtonAll;
        private Button ButtonNone;
        private Label LabelText;
        private OwnerDrawCheckedListBox ListBox;

        private readonly Dictionary<string, bool> CheckedState = new();
        private string FilterText = "";
        private bool m_PopulatingItems = false;

        public string Title { set { Text = value; } }
        public string Prompt { set { LabelText.Text = value; } }
        public DialogResult Result => ResultButton;
        public string? TopmostItem => ListBox.Items.Count > 0 ? (string?)ListBox.Items[0] : null;
        public string? SelectedItem => (string?)ListBox.SelectedItem;

        public event Action<string>? FilterChanged;

        public List<string> Selection
        {
            set
            {
                foreach (var v in value)
                {
                    if (!CheckedState.ContainsKey(v))
                    {
                        CheckedState[v] = false;
                    }
                }
                Refilter();
            }
        }

        public List<string> CheckedSelection
        {
            set
            {
                foreach (var k in CheckedState.Keys.ToList())
                {
                    CheckedState[k] = false;
                }
                foreach (var v in value)
                {
                    CheckedState[v] = true;
                }
                Refilter();
            }
            get
            {
                return CheckedState.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
            }
        }

        public string DefaultResponse
        {
            set
            {
                if (CheckedState.ContainsKey(value))
                {
                    int idx = ListBox.Items.IndexOf(value);
                    if (idx >= 0)
                    {
                        ListBox.SelectedIndex = idx;
                    }
                }
            }
        }

        private void Refilter()
        {
            m_PopulatingItems = true;
            ListBox.BeginUpdate();
            try
            {
                ListBox.Items.Clear();
                var starts = CheckedState.Keys
                    .Where(k => k.StartsWith(FilterText, StringComparison.OrdinalIgnoreCase));
                var contains = CheckedState.Keys
                    .Where(k => k.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                             && !k.StartsWith(FilterText, StringComparison.OrdinalIgnoreCase));
                foreach (var k in starts)
                {
                    ListBox.Items.Add(k, CheckedState[k]);
                }
                foreach (var k in contains)
                {
                    ListBox.Items.Add(k, CheckedState[k]);
                }
                if (ListBox.Items.Count > 0)
                {
                    ListBox.SelectedIndex = 0;
                }
            }
            finally
            {
                ListBox.EndUpdate();
                m_PopulatingItems = false;
            }
        }

        public CheckBoxListForm()
        {
            ButtonOK = new Button();
            LabelText = new Label();
            ButtonCancel = new Button();
            ButtonAll = new Button();
            ButtonNone = new Button();
            ListBox = new OwnerDrawCheckedListBox();
            SuspendLayout();

            LabelText.AutoSize = true;
            LabelText.Dock = DockStyle.Fill;
            LabelText.TabIndex = 4;

            ListBox.Dock = DockStyle.Fill;
            ListBox.FormattingEnabled = true;
            ListBox.TabIndex = 0;
            ListBox.CheckOnClick = true;
            ListBox.DrawItemOverride = ListBox_DrawItem;
            ListBox.KeyPress += ListBox_KeyPress;
            ListBox.ItemCheck += (s, e) =>
            {
                if (m_PopulatingItems) return;
                if (e.Index < 0 || e.Index >= ListBox.Items.Count) return;
                var key = (string?)ListBox.Items[e.Index];
                if (key != null)
                {
                    CheckedState[key] = (e.NewValue == CheckState.Checked);
                }
            };

            ButtonAll.Dock = DockStyle.Fill;
            ButtonAll.TabIndex = 1;
            ButtonAll.Text = "Select &All";
            ButtonAll.Click += (s, e) => SetCheckedOnVisible(true);

            ButtonNone.Dock = DockStyle.Fill;
            ButtonNone.TabIndex = 2;
            ButtonNone.Text = "Select &None";
            ButtonNone.Click += (s, e) => SetCheckedOnVisible(false);

            ButtonOK.Dock = DockStyle.Fill;
            ButtonOK.TabIndex = 3;
            ButtonOK.Text = "OK";
            ButtonOK.Click += (sender, e) => { ResultButton = DialogResult.OK; Close(); };

            ButtonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            ButtonCancel.Dock = DockStyle.Fill;
            ButtonCancel.TabIndex = 5;
            ButtonCancel.Text = "Cancel";
            ButtonCancel.Click += (sender, e) => { ResultButton = DialogResult.Cancel; Close(); };

            AcceptButton = this.ButtonOK;
            CancelButton = this.ButtonCancel;

            var allNoneRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                ColumnCount = 2,
                RowCount = 1,
                Padding = Padding.Empty,
            };
            allNoneRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            allNoneRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            allNoneRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            allNoneRow.Controls.Add(ButtonAll, 0, 0);
            allNoneRow.Controls.Add(ButtonNone, 1, 0);

            var okCancelRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                ColumnCount = 2,
                RowCount = 1,
                Padding = Padding.Empty,
            };
            okCancelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            okCancelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            okCancelRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            okCancelRow.Controls.Add(ButtonOK, 0, 0);
            okCancelRow.Controls.Add(ButtonCancel, 1, 0);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // label
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));     // list
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));     // all/none
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));     // ok/cancel
            layout.Controls.Add(LabelText, 0, 0);
            layout.Controls.Add(ListBox, 0, 1);
            layout.Controls.Add(allNoneRow, 0, 2);
            layout.Controls.Add(okCancelRow, 0, 3);
            Controls.Add(layout);

            ClientSize = new Size(400, 450);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            SizeGripStyle = SizeGripStyle.Show;
            Load += (s, e) =>
            {
                var nc = Size - ClientSize;
                MinimumSize = new Size(300 + nc.Width, 250 + nc.Height);
            };
            Shown += (sender, e) => { ActiveControl = ListBox; };
            ResumeLayout(false);
            PerformLayout();
        }

        private void SetCheckedOnVisible(bool check)
        {
            // SetItemChecked fires ItemCheck which updates CheckedState.
            for (int loop = 0; loop < ListBox.Items.Count; loop++)
            {
                ListBox.SetItemChecked(loop, check);
            }
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
                e.Handled = true;
            }
            else if (e.KeyChar == 13)
            {
                ResultButton = DialogResult.OK;
                Close();
                return;
            }
            else if (e.KeyChar == ' ')
            {
                // let CheckedListBox toggle the current item on space
                return;
            }
            else
            {
                var testText = FilterText + e.KeyChar;
                if (CheckedState.Keys.Any(k => k.Contains(testText, StringComparison.OrdinalIgnoreCase)))
                {
                    FilterText = testText;
                }
                e.Handled = true;
            }

            if (FilterText != previousFilter)
            {
                Refilter();
                FilterChanged?.Invoke(FilterText);
            }
        }

        private void ListBox_DrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                e.DrawBackground();
                return;
            }

            string text = (string?)ListBox.Items[e.Index] ?? "";
            bool selected = (e.State & DrawItemState.Selected) != 0;

            // themed checkbox glyph at the left (CheckBoxRenderer matches the native Windows visual style)
            System.Windows.Forms.VisualStyles.CheckBoxState cbState = ListBox.GetItemChecked(e.Index)
                ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
            Size cbSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, cbState);
            var cbLocation = new Point(e.Bounds.Left + 2, e.Bounds.Top + (e.Bounds.Height - cbSize.Height) / 2);
            float x = cbLocation.X + cbSize.Width + 4;

            // modern style: checkbox sits on the normal background; only the text area gets the selection highlight
            e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds);
            if (selected)
            {
                var highlightRect = Rectangle.FromLTRB((int)x - 2, e.Bounds.Top, e.Bounds.Right, e.Bounds.Bottom);
                e.Graphics.FillRectangle(SystemBrushes.Highlight, highlightRect);
            }
            CheckBoxRenderer.DrawCheckBox(e.Graphics, cbLocation, cbState);

            // text with matched-substring highlight
            float y = e.Bounds.Top + (e.Bounds.Height - e.Font!.Height) / 2f;
            Font font = e.Font!;
            Brush normalBrush = selected ? SystemBrushes.HighlightText : Brushes.Black;
            Brush highlightBrush = Brushes.Blue;
            StringFormat format = StringFormat.GenericTypographic;
            format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

            int matchIndex = FilterText.Length > 0
                ? text.IndexOf(FilterText, StringComparison.OrdinalIgnoreCase)
                : -1;

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

            float measure(string t)
            {
                CharacterRange[] ranges = { new CharacterRange(0, t.Length) };
                format.SetMeasurableCharacterRanges(ranges);
                Region[] regions = e.Graphics.MeasureCharacterRanges(t, font, new RectangleF(0, 0, e.Bounds.Width, e.Bounds.Height), format);
                return regions[0].GetBounds(e.Graphics).Width;
            }
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

        // CheckedListBox owner-draws internally and never raises the DrawItem event, so subclass and override
        // OnDrawItem to paint ourselves (checkbox glyph + filter-match highlight). base is not called.
        private sealed class OwnerDrawCheckedListBox : CheckedListBox
        {
            public Action<DrawItemEventArgs>? DrawItemOverride;

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (DrawItemOverride != null)
                {
                    DrawItemOverride(e);
                }
                else
                {
                    base.OnDrawItem(e);
                }
            }
        }
    }
}
