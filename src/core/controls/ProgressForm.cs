using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace SehensWerte.Controls
{
    // A small modeless progress window that stays above the work it reports on, without taking focus
    public sealed class ProgressForm : Form
    {
        private const int BarSteps = 1000;

        private readonly ProgressBar m_Bar;
        private readonly Label m_Label;
        private Control? m_Owner;

        // Bumped by every HideProgress before it marshals. A ShowOver captures the epoch at call
        // time; if a hide was requested after that (queued shows can arrive arbitrarily late), the
        // show is stale and must not run, otherwise a cancelled task's last show strands the window
        private int m_HideEpoch;

        // Only show once the work has been running this long, so short operations never flash a
        // popup the user can half-see; 0 shows immediately
        public int ShowDelayMs { get; set; } = 500;
        private readonly System.Windows.Forms.Timer m_ShowTimer;
        private int m_ArmedEpoch;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
        private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;

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

            m_ShowTimer = new System.Windows.Forms.Timer();
            m_ShowTimer.Tick += ShowTimerTick;
            Disposed += (s, e) => m_ShowTimer.Dispose();

            // Create the handle on the constructing (UI) thread for InvokeRequired/BeginInvoke before show
            CreateHandle();
        }

        protected override bool ShowWithoutActivation => true;

        public void ShowOver(Control owner)
        {
            ShowOver(owner, Volatile.Read(ref m_HideEpoch));
        }

        private void ShowOver(Control owner, int epoch)
        {
            if (Disposing || IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(() => ShowOver(owner, epoch));
                return;
            }
            if (epoch != Volatile.Read(ref m_HideEpoch))
            {
                return; // a hide was requested after this show was issued
            }
            if (Visible)
            {
                return;
            }
            m_Owner = owner;
            if (ShowDelayMs <= 0)
            {
                ShowNow();
                return;
            }
            if (!m_ShowTimer.Enabled)
            {
                m_ArmedEpoch = epoch;
                m_ShowTimer.Interval = ShowDelayMs;
                m_ShowTimer.Start();
            }
        }

        private void ShowTimerTick(object? sender, EventArgs e)
        {
            m_ShowTimer.Stop();
            if (Disposing || IsDisposed || Visible || m_Owner == null) return;
            if (m_ArmedEpoch != Volatile.Read(ref m_HideEpoch))
            {
                return; // a hide was requested while the show was pending
            }
            ShowNow();
        }

        private void ShowNow()
        {
            if (m_Owner == null) return;
            // Size and position while still hidden, on EVERY show: the form is reused, so the
            // owner may be on a different monitor than last time, and mapping the window at a
            // stale location then moving it across a DPI boundary can strand it off-screen
            SizeAndPosition();
            Form? ownerForm = m_Owner.FindForm();
            if (ownerForm == null)
            {
                Show();
            }
            else
            {
                Show(ownerForm);
            }
            // becoming visible on a different-DPI monitor rescales the form; re-center at final size
            SizeAndPosition();
            // The handle was created before the owner was assigned (ctor CreateHandle), and Windows
            // does not enforce owned-above-owner z-order for a post-creation GWL_HWNDPARENT until
            // the next activation change - the form shows BEHIND its active owner. Raise it
            // explicitly, without activating, so it is visible immediately
            SetWindowPos(Handle, IntPtr.Zero /*HWND_TOP*/, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
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
            // invalidate every show issued before this hide, on whatever thread it is queued
            Interlocked.Increment(ref m_HideEpoch);
            if (Disposing || IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(HideProgressOnUiThread);
                return;
            }
            HideProgressOnUiThread();
        }

        private void HideProgressOnUiThread()
        {
            if (Disposing || IsDisposed) return;
            m_ShowTimer.Stop();
            if (!Visible) return;
            Hide();
        }

        private void SizeAndPosition()
        {
            if (m_Owner == null) return;

            int line = Font.Height;
            m_Label.Height = line + line / 2;
            m_Bar.Height = line;
            Padding = new Padding(line / 2);
            ClientSize = new Size(line * 22, Padding.Vertical + m_Label.Height + m_Bar.Height);

            Rectangle over = m_Owner.RectangleToScreen(m_Owner.ClientRectangle);
            Rectangle work = Screen.FromControl(m_Owner).WorkingArea;
            int x = over.Left + (over.Width - Width) / 2;
            int y = over.Top + Math.Max(8, (over.Height - Height) / 4);
            Location = new Point(
                Math.Max(work.Left, Math.Min(x, work.Right - Width)),
                Math.Max(work.Top, Math.Min(y, work.Bottom - Height)));
        }
    }
}
