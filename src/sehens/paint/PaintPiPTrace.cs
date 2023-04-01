namespace SehensWerte.Controls.Sehens
{
    public class PaintPiPTrace : Paint2dTrace, IPaintTrace, IDisposable
    {
        public PaintPiPTrace()
        {
            SaveTraceVerticalExtents = false;
        }

        public override void PaintProjection(Graphics graphics, TraceGroupDisplay info)
        {
            if (SnapshotReprojectionRequired)
            {
                double high = LastTraceHighestValue;
                double low = LastTraceLowestValue;
                TraceView.AddFactor(ref high, ref low, 0.1);
                double lastTraceHighestValue = LastTraceHighestValue;
                double lastTraceLowestValue = LastTraceLowestValue;
                if (info.View0.CalculatedBeforeZoom != null)
                {
                    Project2dCurves(info.View0.CalculatedBeforeZoom, info.View0, info.ProjectionArea, high, low);
                }
                SnapshotReprojectionRequired = lastTraceHighestValue != LastTraceHighestValue || lastTraceLowestValue != LastTraceLowestValue;
            }
            if (DrawnPolygon != null)
            {
                using Brush brush = new SolidBrush(info.Skin.BackgroundColour);
                graphics.FillPolygon(brush, DrawnPolygon);
            }
            using Pen pen = LinePen(info.View0.Colour, info);
            PaintProjection(DrawnProjection1, graphics, pen);
            PaintProjection(DrawnProjection2, graphics, pen);
        }

        public override void PaintInitial(Graphics graphics, TraceGroupDisplay info)
        {
            if (info.OverlayIndex != 0) return;
            Rectangle rect = new Rectangle(info.ProjectionArea.Left - 1, info.ProjectionArea.Top - 1, info.ProjectionArea.Width + 2, info.ProjectionArea.Height + 2);

            using Pen pen = new Pen(info.Skin.GraduationColour);
            {
                using Brush brush = new SolidBrush(InterpolateColour(info.Skin.BackgroundColour, info.View0.Colour, 1, 5));
                graphics.FillRectangle(brush, rect);
            }

            if (info.View0.ZoomValue != 1.0)
            {
                int left = Math.Max(rect.Left, (int)(((rect.Right - rect.Left) * (double)info.View0.PanValue) + rect.Left));
                int width = Math.Min(rect.Right - left, (int)((double)(rect.Right - rect.Left) * (double)info.View0.ZoomValue));
                using Brush brush = new SolidBrush(InterpolateColour(info.Skin.BackgroundColour, info.View0.Colour, 3, 5));
                graphics.FillRectangle(brush, new Rectangle(left, rect.Top, width, rect.Height));
            }

            graphics.DrawRectangle(pen, rect);
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
