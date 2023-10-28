using System.Reflection;

namespace SehensWerte.Controls
{
    public class AutoEditorForm : Form
    {
        private DialogResult ResultButton;

        public Action<AutoEditor>? OnChange;
        private Button ButtonOK;
        private Button ButtonCancel;
        private Label LabelText;
        private AutoEditorControl Panel;
        public string Title { set { Text = value; } }
        public string Prompt { set { LabelText.Text = value; } }
        public DialogResult Result => ResultButton;

        public AutoEditorForm()
        {
            ButtonOK = new Button();
            LabelText = new Label();
            ButtonCancel = new Button();
            Panel = new AutoEditorControl();
            SuspendLayout();
            base.ClientSize = new Size(401, 300);
            ButtonOK.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            ButtonOK.Location = new Point(91, 264);
            ButtonOK.Name = "ButtonOK";
            ButtonOK.Size = new Size(64, 24);
            ButtonOK.TabIndex = 1;
            ButtonOK.Text = "OK";
            ButtonOK.Click += ButtonOK_Click;
            LabelText.AutoSize = true;
            LabelText.Location = new Point(16, 8);
            LabelText.Name = "LabelText";
            LabelText.Size = new Size(54, 13);
            LabelText.TabIndex = 3;
            LabelText.Text = "InputForm";
            ButtonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            ButtonCancel.DialogResult = DialogResult.Cancel;
            ButtonCancel.Location = new Point(252, 264);
            ButtonCancel.Name = "ButtonCancel";
            ButtonCancel.Size = new Size(64, 24);
            ButtonCancel.TabIndex = 2;
            ButtonCancel.Text = "Cancel";
            ButtonCancel.Click += ButtonCancel_Click;
            Panel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Panel.Location = new Point(12, 30);
            Panel.Name = "Panel";
            Panel.Size = new Size(377, 228);
            Panel.TabIndex = 0;
            Panel.OnChange += (s) => OnChange?.Invoke(s);
            base.Controls.Add(Panel);
            base.Controls.Add(ButtonCancel);
            base.Controls.Add(LabelText);
            base.Controls.Add(ButtonOK);
            base.KeyPreview = true;
            base.MaximizeBox = false;
            base.Name = "AutoEditorForm";
            base.SizeGripStyle = SizeGripStyle.Show;
            base.KeyPress += KeyPressed;
            ResumeLayout(performLayout: false);
            PerformLayout();
        }

        public static bool Show(string prompt, string title, object sourceData)
        {
            return new AutoEditorForm().ShowDialog(prompt, title, sourceData);
        }

        public bool ShowDialog(string prompt, string title, object sourceData)
        {
            Title = title;
            Prompt = prompt;
            Panel.Generate(sourceData);

            base.Height = Math.Min(Screen.PrimaryScreen.WorkingArea.Height - 100, Panel.LayoutPanel.GetRowHeights().Sum() + 140);
            if (base.Bottom > Screen.PrimaryScreen.WorkingArea.Height)
            {
                base.Top = Math.Max(20, base.Top - (base.Bottom - Screen.PrimaryScreen.WorkingArea.Height) - 20);
            }

            this.MoveOnScreen();
            ShowDialog();
            Panel.RemoveDelegates();

            return ResultButton == DialogResult.OK;
        }

        private void KeyPressed(object? sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case '\u001b':
                    ButtonCancel_Click(sender, e);
                    break;
                case '\r':
                    ButtonOK_Click(sender, e);
                    break;
            }
        }

        internal void ButtonOK_Click(object? sender, EventArgs e)
        {
            ResultButton = DialogResult.OK;
            Close();
        }

        private void ButtonCancel_Click(object? sender, EventArgs e)
        {
            ResultButton = DialogResult.Cancel;
            Panel.Revert();
            Close();
        }
    }
}
