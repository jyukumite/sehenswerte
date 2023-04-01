using System.Drawing.Drawing2D;

namespace SehensWerte.Controls.Sehens
{
    public class TraceViewEmbedText : TraceViewClickZone
    {
        public enum Align
        {
            TopLeft,
            BottomLeft,
            BottomRight,
            TopCenterVertical
        }

        public enum Style
        {
            Invisible,
            Normal,
            Selected
        }

        public TraceViewEmbedText(int x, int y, TraceGroupDisplay info, PaintBoxMouseInfo.GuiSection hotPoint, Flags showFlags)
        {
            GuiSection = hotPoint;
            Flag = showFlags;
            Rect = new Rectangle(x, y + info.PaintVerticalOffset, 1, 1);
        }

        public void Paint(Color colour, string text, Font font, TraceGroupDisplay info, Graphics graphics, Align align, Style style)
        {
            Size size = graphics.MeasureString(text, font).ToSize() + new Size(4, 2);
            float num = 0f;
            switch (align)
            {
                case Align.BottomLeft:
                    Rect = new Rectangle(Rect.X, Rect.Y - size.Height, size.Width, size.Height);
                    break;
                case Align.BottomRight:
                    Rect = new Rectangle(Rect.X - size.Width, Rect.Y - size.Height, size.Width, size.Height);
                    break;
                case Align.TopLeft:
                    Rect = new Rectangle(Rect.X, Rect.Y, size.Width, size.Height);
                    break;
                case Align.TopCenterVertical:
                    Rect = new Rectangle(Rect.X, Rect.Y - size.Width / 2, size.Height, size.Width);
                    num = -90f;
                    break;
            }
            Rectangle rect = new Rectangle(Rect.X, Rect.Y - info.PaintVerticalOffset, Rect.Width, Rect.Height);
            if (style == Style.Selected)
            {
                using SolidBrush brush = new SolidBrush(info.Skin.SelectedEmbedColour);
                graphics.FillRectangle(brush, rect);
            }
            HighlightMouseover(info, graphics);
            using SolidBrush brush2 = new SolidBrush(colour);
            if (num != 0f)
            {
                int x = rect.X + rect.Width / 2 + 2;
                int y = rect.Y + rect.Height / 2;
                using Matrix matrix = new Matrix(1f, 0f, 0f, 1f, 0f, 0f);
                matrix.Rotate(num, MatrixOrder.Append);
                matrix.Translate(x, y, MatrixOrder.Append);
                try
                {
                    graphics.Transform = matrix;
                    graphics.DrawString(text, font, brush2, -size.Width / 2, -size.Height / 2);
                }
                finally
                {
                    graphics.ResetTransform();
                }
            }
            else
            {
                int x = rect.X + 2;
                int y = rect.Y + 1;
                using StringFormat format = new StringFormat();
                graphics.DrawString(text, font, brush2, x, y, format);
            }
        }

        protected bool HighlightMouseover(TraceGroupDisplay info, Graphics graphics)
        {
            bool result = false;
            if (!info.Flags.HasFlag(TraceGroupDisplay.PaintFlags.Screenshot)
                && info.MouseInfo.Click != null
                && Rect.Contains(new Point(info.MouseInfo.Click!.X, info.MouseInfo.Click!.Y)))
            {
                info.MouseOnEmbed = true;
                result = true;
                ControlPaint.DrawBorder3D(graphics,
                    new Rectangle(Rect.X, Rect.Y - info.PaintVerticalOffset, Rect.Width, Rect.Height + 2), Border3DStyle.Bump);
            }
            return result;
        }
    }
}
