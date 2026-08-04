using System.Drawing;
using System.Windows.Forms;

namespace SehensWerte.Controls
{
    // A small modeless progress window that stays above the work it reports on, without taking focus
    public sealed class ProgressForm : Form
    {
        private const int BarSteps = 1000;

        private readonly ProgressBar m_Bar;
        private readonly Label m_Label;
        private Control? m_Over;

        public ProgressForm()
        {
            m_Label = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
            };
            m_Bar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Minimum = 0,
                Maximum = BarSteps,
            };

            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            ControlBox = false;
            MinimizeBox = false;
            MaximizeBox = false;
            // not TopMost: an owned window already floats above its owner
            Text = "Progress";
            Controls.Add(m_Label);
            Controls.Add(m_Bar);
            ResumeLayout(false);

            // Create the handle on the constructing (UI) thread for InvokeRequired/BeginInvoke before show
            CreateHandle();
        }

        protected override bool ShowWithoutActivation => true;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            int line = Font.Height;
            m_Label.Height = line + line / 2;
            m_Bar.Height = line;
            Padding = new Padding(line / 2);
            ClientSize = new Size(line * 22, Padding.Vertical + m_Label.Height + m_Bar.Height);
            if (m_Over != null)
            {
                PositionOver(m_Over);
            }
        }

        public void ShowOver(Control owner)
        {
            if (Disposing || IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(() => ShowOver(owner));
                return;
            }
            if (Visible) return;
            m_Over = owner;
            Form? ownerForm = owner.FindForm();
            if (ownerForm == null)
            {
                Show();
            }
            else
            {
                Show(ownerForm);
            }
        }

        public void SetProgress(double fraction, string text)
        {
            if (Disposing || IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(() => SetProgress(fraction, text));
                return;
            }
            m_Label.Text = text;
            m_Bar.Value = (int)(Math.Max(0.0, Math.Min(1.0, fraction)) * BarSteps);
        }

        public void HideProgress()
        {
            if (Disposing || IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(HideProgress);
                return;
            }
            if (!Visible) return;
            Hide();
        }

        private void PositionOver(Control owner)
        {
            Rectangle over = owner.RectangleToScreen(owner.ClientRectangle);
            Rectangle work = Screen.FromControl(owner).WorkingArea;
            int x = over.Left + (over.Width - Width) / 2;
            int y = over.Top + Math.Max(8, (over.Height - Height) / 4);
            Location = new Point(
                Math.Max(work.Left, Math.Min(x, work.Right - Width)),
                Math.Max(work.Top, Math.Min(y, work.Bottom - Height)));
        }
    }
}
