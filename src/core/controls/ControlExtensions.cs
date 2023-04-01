namespace SehensWerte.Controls
{
    public static class ControlExtensions
    {
        public static void InvokeIfRequired(this Control control, Action action)
        {
            Exception? innerException = null;
            Action invoke = () =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    innerException = e;
                };
            };
            if (control.InvokeRequired)
            {
                control.Invoke(invoke);
            }
            else
            {
                invoke();
            }
            if (innerException != null)
            {
                throw innerException;
            }
        }

        public static void BeginInvokeIfRequired(this Control control, Action action, Action<Exception>? exception = null)
        {
            Action invoke = () =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    exception?.Invoke(e);
                }
            };
            if (!control.Disposing && !control.IsDisposed && control.IsHandleCreated && control.InvokeRequired)
            {
                control.BeginInvoke(invoke);
            }
            else
            {
                invoke();
            }
        }

        public static void InvokeIfRequired(this Control control, Action action, Action<Exception>? exception = null)
        {
            Action invoke = () =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    exception?.Invoke(e);
                }
            };
            if (!control.Disposing && !control.IsDisposed && control.IsHandleCreated && control.InvokeRequired)
            {
                control.Invoke(invoke);
            }
            else
            {
                invoke();
            }
        }

        public static void ExceptionToMessagebox(this Control control, Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        public static void MoveOnScreen(this Form form, bool shrinkToFit = true)
        {
            var screen = Screen.FromPoint(form.PointToScreen(new System.Drawing.Point(0, 0)));
            var left = screen.WorkingArea.X;
            var top = screen.WorkingArea.Y;
            var right = screen.WorkingArea.Right;
            var bottom = screen.WorkingArea.Bottom;

            if (form.Top < top)
            {
                form.Top = top;
            }
            if (form.Left < left)
            {
                form.Left = left;
            }
            if (shrinkToFit)
            {
                if (form.Bottom > bottom)
                {
                    form.Height = form.Height - (form.Bottom - bottom);
                }
                if (form.Right > right)
                {
                    form.Width = form.Width - (form.Right - right);
                }
            }
        }
    }
}
