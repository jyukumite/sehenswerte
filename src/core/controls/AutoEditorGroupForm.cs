using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SehensWerte.Controls
{
    // Wide modal editor with an AutoEditorControl column per source object
    public class AutoEditorGroupForm : Form
    {
        private const int ColumnWidth = 340;
        private const int ColumnGap = 8;
        private const int HeaderHeight = 22;

        public Action<AutoEditor>? OnChange;

        private DialogResult m_Result = DialogResult.Cancel;
        private readonly Panel m_Columns;
        private readonly Button m_ButtonOK;
        private readonly Button m_ButtonCancel;
        private readonly Label m_LabelText;
        private readonly Font m_HeaderFont;
        private readonly List<AutoEditorControl> m_Editors = new List<AutoEditorControl>();

        public DialogResult Result => m_Result;
        internal IReadOnlyList<AutoEditorControl> Editors => m_Editors;

        public AutoEditorGroupForm()
        {
            m_HeaderFont = new Font(Font, FontStyle.Bold);
            m_LabelText = new Label
            {
                AutoSize = true,
                Location = new Point(16, 8),
            };
            m_Columns = new Panel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(12, 30),
                AutoScroll = true,
            };
            m_ButtonOK = new Button
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Size = new Size(64, 24),
                Text = "OK",
            };
            m_ButtonOK.Click += (s, e) => { m_Result = DialogResult.OK; Close(); };
            m_ButtonCancel = new Button
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel,
                Size = new Size(64, 24),
                Text = "Cancel",
            };
            m_ButtonCancel.Click += (s, e) =>
            {
                this.ExceptionToMessagebox(() =>
                {
                    m_Result = DialogResult.Cancel;
                    m_Editors.ForEach(x => x.Revert());
                    Close();
                }, "Cancel edits");
            };
            ClientSize = new Size(720, 480);
            m_Columns.Size = new Size(ClientSize.Width - 24, ClientSize.Height - 72);
            m_ButtonOK.Location = new Point(16, ClientSize.Height - 34);
            m_ButtonCancel.Location = new Point(ClientSize.Width - 80, ClientSize.Height - 34);
            Controls.Add(m_Columns);
            Controls.Add(m_LabelText);
            Controls.Add(m_ButtonOK);
            Controls.Add(m_ButtonCancel);
            KeyPreview = true;
            MaximizeBox = true;
            SizeGripStyle = SizeGripStyle.Show;
            KeyPress += (s, e) =>
            {
                switch (e.KeyChar)
                {
                    case '\u001b': m_ButtonCancel.PerformClick(); break;
                    case '\r': m_ButtonOK.PerformClick(); break;
                }
            };
        }

        // Build one header + editor column per source, left to right. Split from ShowDialog so
        // headless tests can assert the layout without showing a modal form.
        internal void Populate(string prompt, string title, IReadOnlyList<(string Title, object SourceData)> columns)
        {
            Text = title;
            m_LabelText.Text = prompt;

            int x = 0;
            int tallest = 0;
            foreach ((string columnTitle, object sourceData) in columns)
            {
                var header = new Label
                {
                    Text = columnTitle,
                    Font = m_HeaderFont,
                    AutoEllipsis = true,
                    Bounds = new Rectangle(x, 0, ColumnWidth - ColumnGap, HeaderHeight),
                };
                var editor = new AutoEditorControl();
                editor.Generate(sourceData);
                editor.Location = new Point(x, HeaderHeight);
                editor.Width = ColumnWidth - ColumnGap;
                editor.Height = editor.PreferredHeight;
                editor.OnChange += (s) => OnChange?.Invoke(s);
                tallest = Math.Max(tallest, editor.Height);
                m_Columns.Controls.Add(header);
                m_Columns.Controls.Add(editor);
                m_Editors.Add(editor);
                x += ColumnWidth;
            }

            Rectangle workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(this);
            Width = Math.Min(workingArea.Width - 80, x + 50);
            Height = Math.Min(workingArea.Height - 80, HeaderHeight + tallest + 140);
        }

        public bool ShowDialog(string prompt, string title, IReadOnlyList<(string Title, object SourceData)> columns)
        {
            Populate(prompt, title, columns);
            this.MoveOnScreen();
            ShowDialog();
            m_Editors.ForEach(x => x.RemoveDelegates());
            return m_Result == DialogResult.OK;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_HeaderFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    [TestClass]
    public class AutoEditorGroupFormTests
    {
        private class Row
        {
            public double Offset = 0;
            public string Unit = "";
        }

        [TestMethod]
        public void OneColumnPerSourceLaidOutLeftToRight()
        {
            using var form = new AutoEditorGroupForm();
            var rows = new[] { new Row(), new Row { Offset = 5 }, new Row { Unit = "rpm" } };
            form.Populate("prompt", "title", rows.Select(r => (r.Unit, (object)r)).ToList());

            Assert.AreEqual(3, form.Editors.Count);
            for (int loop = 0; loop < form.Editors.Count; loop++)
            {
                Assert.AreEqual(loop * 340, form.Editors[loop].Left, $"column {loop} x position");
                Assert.IsTrue(form.Editors[loop].Height > 0, "editor sized to its content");
            }
            // wider than one column: the columns panel scrolls horizontally when clipped
            Assert.IsTrue(form.Editors[2].Right > 680);
        }
    }
}
