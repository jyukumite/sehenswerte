using System;
using System.Drawing;
using System.Windows.Forms;

namespace SehensWerte.Controls
{
    public class SplitContainer : System.Windows.Forms.SplitContainer
    {
        public enum ControlledPanel { Panel1, Panel2 }
        public ControlledPanel CollapsingPanel { get; set; } = ControlledPanel.Panel1;

        private int MinUncollapsedSize { get; set; }

        private bool IsCollapsed =>
            CollapsingPanel == ControlledPanel.Panel1
            ? (SplitterDistance == 0)
            : (SplitterDistance == ((Orientation == Orientation.Horizontal ? Height : Width) - SplitterWidth));

        public bool Collapsed
        {
            get { return IsCollapsed; }
            set { SetCollapse(value, resizePanel: true); }
        }

        private int PreviousSplitterDistance = 100;
        private Rectangle TriangleBounds;
        private bool Draw = true;

        private bool CollapsedBeforeDrag;
        private bool SplitterHold;

        public SplitContainer()
        {
            Paint += DrawSplitter;
            MouseMove += OnMouseMove;
            MouseLeave += OnMouseLeave;
            SplitterMoving += OnSplitterMoving;
            SplitterMoved += OnSplitterMoved;
        }

        private void OnMouseLeave(object? sender, EventArgs e)
        {
            Cursor = Cursors.Default;
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_LBUTTONUP = 0x0202;

            if (m.Msg == WM_LBUTTONDOWN)
            {
                Point clickPoint = PointToClient(Cursor.Position);
                if (TriangleBounds.Contains(clickPoint))
                {
                    return;
                }
            }
            if (m.Msg == WM_LBUTTONUP)
            {
                Point clickPoint = PointToClient(Cursor.Position);
                if (TriangleBounds.Contains(clickPoint))
                {
                    SetCollapse(!Collapsed, resizePanel: true);
                    return;
                }
            }

            base.WndProc(ref m);
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (TriangleBounds.Contains(e.Location))
            {
                Cursor = Cursors.Hand;
            }
            else
            {
                Cursor = Orientation == Orientation.Vertical ? Cursors.VSplit : Cursors.HSplit;
            }
        }

        private void OnSplitterMoving(object? sender, SplitterCancelEventArgs e)
        {
            CaptureMinimumSize();
            if (Draw)
            {
                CollapsedBeforeDrag = IsCollapsed;
            }
            Draw = false;
            if (IsCollapsed)
            {
                SetCollapse(collapsed: false, resizePanel: false);
            }
        }

        private void CaptureMinimumSize()
        {
            FixedPanel = (CollapsingPanel == ControlledPanel.Panel1) ? FixedPanel.Panel1 : FixedPanel.Panel2;
            if (MinUncollapsedSize == 0)
            {
                MinUncollapsedSize = CollapsingPanel == ControlledPanel.Panel1 ? Panel1MinSize : Panel2MinSize;
                if (MinUncollapsedSize == 0)
                {
                    MinUncollapsedSize = 150;
                }
            }
            if (CollapsingPanel == ControlledPanel.Panel1)
            {
                Panel1MinSize = 0;
            }
            else
            {
                Panel2MinSize = 0;
            }
        }

        private void OnSplitterMoved(object? sender, SplitterEventArgs e)
        {
            int splitterDistance = SplitterDistance;
            if (CollapsingPanel == ControlledPanel.Panel2)
            {
                splitterDistance = (Orientation == Orientation.Horizontal ? Height : Width) - splitterDistance - SplitterWidth;
            }
            if (splitterDistance > 0 && splitterDistance < MinUncollapsedSize && !SplitterHold)
            {
                SetCollapse(!CollapsedBeforeDrag, resizePanel: true);
            }
            Draw = true;
        }

        private void DrawSplitter(object? sender, PaintEventArgs e)
        {
            if (!Draw) return;
            CaptureMinimumSize();

            Graphics g = e.Graphics;
            Rectangle rect = SplitterRectangle;

            using (Pen pen = new Pen(Color.Gray, 1))
            {
                if (Orientation == Orientation.Vertical)
                {
                    g.DrawLine(pen, rect.Left + rect.Width / 2 - 1, rect.Top, rect.Left + rect.Width / 2 - 1, rect.Bottom);
                    g.DrawLine(pen, rect.Left + rect.Width / 2 + 1, rect.Top, rect.Left + rect.Width / 2 + 1, rect.Bottom);
                }
                else
                {
                    g.DrawLine(pen, rect.Left, rect.Top + rect.Height / 2 - 1, rect.Right, rect.Top + rect.Height / 2 - 1);
                    g.DrawLine(pen, rect.Left, rect.Top + rect.Height / 2 + 1, rect.Right, rect.Top + rect.Height / 2 + 1);
                }
            }

            Point[] triangle = new Point[3];
            bool reverse = IsCollapsed ^ CollapsingPanel == ControlledPanel.Panel1;
            ArrowDirection direction;
            int x;
            int y;
            int toggleTriangleSize = Math.Min(8, SplitterWidth);
            if (Orientation == Orientation.Vertical)
            {
                x = rect.Left + rect.Width / 2;
                y = rect.Top + 15;
                direction = reverse ? ArrowDirection.Left : ArrowDirection.Right;
            }
            else
            {
                x = rect.Right - 15;
                y = rect.Top + rect.Height / 2;
                direction = reverse ? ArrowDirection.Up : ArrowDirection.Down;
            }

            switch (direction)
            {
                case ArrowDirection.Up:
                    triangle[0] = new Point(x - toggleTriangleSize, y + toggleTriangleSize / 2);
                    triangle[1] = new Point(x + toggleTriangleSize, y + toggleTriangleSize / 2);
                    triangle[2] = new Point(x, y - toggleTriangleSize / 2);
                    break;
                case ArrowDirection.Down:
                    triangle[0] = new Point(x - toggleTriangleSize, y - toggleTriangleSize / 2);
                    triangle[1] = new Point(x + toggleTriangleSize, y - toggleTriangleSize / 2);
                    triangle[2] = new Point(x, y + toggleTriangleSize / 2);
                    break;
                case ArrowDirection.Left:
                    triangle[0] = new Point(x + toggleTriangleSize / 2, y - toggleTriangleSize);
                    triangle[1] = new Point(x + toggleTriangleSize / 2, y + toggleTriangleSize);
                    triangle[2] = new Point(x - toggleTriangleSize / 2, y);
                    break;
                case ArrowDirection.Right:
                    triangle[0] = new Point(x - toggleTriangleSize / 2, y - toggleTriangleSize);
                    triangle[1] = new Point(x - toggleTriangleSize / 2, y + toggleTriangleSize);
                    triangle[2] = new Point(x + toggleTriangleSize / 2, y);
                    break;
            }

            TriangleBounds = new Rectangle(x - toggleTriangleSize-1, y - toggleTriangleSize-1, toggleTriangleSize * 2+1, toggleTriangleSize * 2+1);

            g.FillPolygon(Brushes.Black, triangle);
        }

        private void SetCollapse(bool collapsed, bool resizePanel)
        {
            CaptureMinimumSize();
            SplitterHold = true;
            try
            {
                int collapsedDistance = CollapsingPanel == ControlledPanel.Panel1
                    ? 0
                    : (Orientation == Orientation.Vertical ? Width : Height) - SplitterWidth;
                if (!IsCollapsed)
                {
                    PreviousSplitterDistance = SplitterDistance;
                }
                if (resizePanel)
                {
                    int newDistance = collapsed ? collapsedDistance : PreviousSplitterDistance;
                    if (CollapsingPanel == ControlledPanel.Panel1)
                    {
                        SplitterDistance = newDistance == collapsedDistance
                            ? collapsedDistance
                            : ((newDistance < MinUncollapsedSize) ? MinUncollapsedSize : newDistance);
                    }
                    else
                    {
                        SplitterDistance = newDistance == collapsedDistance
                            ? collapsedDistance
                            : ((newDistance > (collapsedDistance - MinUncollapsedSize)) ? (collapsedDistance - MinUncollapsedSize) : newDistance);
                    }
                }
                Invalidate();
            }
            finally
            {
                SplitterHold = false;
            }
        }
    }
}
