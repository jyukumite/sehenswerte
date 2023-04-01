namespace SehensWerte.Controls.Sehens
{
    internal class PaintXYZTrace : PaintTraceBase, IPaintTrace, IDisposable
    {
        public enum DrawModes
        {
            RectangularLine,
            RectangularCurve,
            RectangularDot,
            PolarLine,
            PolarCurve,
            PolarDot
        }

        private DrawModes DrawMode;

        public PaintXYZTrace(DrawModes mode)
        {
            DrawMode = mode;
        }

        public void PaintInitial(Graphics graphics, TraceGroupDisplay info) { }
        public override void PaintHorizontalAxis(Graphics graphics, TraceGroupDisplay info) { }
        public override void PaintVerticalAxis(Graphics graphics, TraceGroupDisplay info) { }
        public override void PaintAxisTitleHorizontal(Graphics graphics, TraceGroupDisplay info) { }
        public override void PaintAxisTitleVertical(Graphics graphics, TraceGroupDisplay info) { }

        public override void PaintProjection(Graphics graphics, TraceGroupDisplay info)
        {
            if (info.View0.Painted.TraceIndex != 0) return;
            (var xSamples, var ySamples, var zSamples) = GetXYZ(ref info, out var xTrace, out var yTrace, out var zTrace);

            PointF[] array = new PointF[(Math.Min(Math.Min(xSamples.Length, ySamples.Length), zSamples.Length))];
            RectangleF rect = info.ProjectionArea;
            double lowX = xTrace.LowestValue;
            double lowY = yTrace.LowestValue;
            double highX = xTrace.HighestValue;
            double highY = yTrace.HighestValue;
            double dz = 2.0;

            double midY = (highY + lowY) / 2.0;
            double rangeY = highY - lowY;

            float y1 = (float)((double)rect.Top + highY * (double)rect.Height / (highY - lowY));
            y1 = (y1 < rect.Top)
                ? rect.Top
                : ((y1 > rect.Bottom) ? rect.Bottom : y1);
            float x1 = (float)((double)rect.Left + lowX * (double)rect.Width / (lowX - highX));
            x1 = (x1 < rect.Left)
                ? rect.Left
                : ((x1 > rect.Right) ? rect.Right : x1);
            using Pen gradPen = new Pen(info.Skin.GraduationColour);
            graphics.DrawLine(gradPen, rect.Left, y1, rect.Right, y1);
            graphics.DrawLine(gradPen, x1, rect.Top, x1, rect.Bottom);

            for (int loop = 0; loop < Math.Min(Math.Min(xSamples.Length, ySamples.Length), zSamples.Length); loop++)
            {
                double x = xSamples[loop];
                double y = ySamples[loop];
                double z = (zSamples[loop] - midY) / rangeY;

                float mz = (float)(dz / (z + dz));
                float projectedY = (float)(rect.Top + (highY - y) * rect.Height / (highY - lowY)) * mz;
                float projectedX = (float)(rect.Left + (lowX - x) * rect.Width / (lowX - highX)) * mz;
                projectedY = (projectedY < rect.Top) ? rect.Top : ((projectedY > rect.Bottom) ? rect.Bottom : projectedY);
                projectedX = (projectedX < rect.Left) ? rect.Left : ((projectedX > rect.Right) ? rect.Right : projectedX);
                array[loop].X = Math.Max((int)rect.Left, Math.Min((int)rect.Right, (int)projectedX));
                array[loop].Y = Math.Max((int)rect.Top, Math.Min((int)rect.Bottom, (int)projectedY));
            }

            xTrace.DrawnValueLowest = xSamples.Min();
            xTrace.DrawnValueHighest = xSamples.Max();
            yTrace.DrawnValueLowest = ySamples.Min();
            yTrace.DrawnValueHighest = ySamples.Max();
            zTrace.DrawnValueLowest = zSamples.Min();
            zTrace.DrawnValueHighest = zSamples.Max();
            using Pen pen = new Pen(info.View0.Colour);
            switch (DrawMode)
            {
                case DrawModes.RectangularCurve:
                    graphics.DrawCurve(pen, array);
                    break;
                case DrawModes.RectangularDot:
                    {
                        using SolidBrush brush = new SolidBrush(Color.FromArgb(128, pen.Color));
                        PaintDots(graphics, brush, array);
                        break;
                    }
                case DrawModes.RectangularLine:
                    graphics.DrawLines(pen, array);
                    break;
                default:
                    throw new PainterException("Unknown draw mode");
            }

            PaintPiP(info, graphics);
        }

        private static (double[] xSamples, double[] ySamples, double[] zSamples) GetXYZ(ref TraceGroupDisplay info, out TraceView xTrace, out TraceView yTrace, out TraceView zTrace)
        {
            if (info.View0.Painted.Group.Count != 3)
            {
                throw new PainterException("XYZ needs three traces");
            }
            xTrace = info.View0.Painted.Group[0];
            yTrace = info.View0.Painted.Group[1];
            zTrace = info.View0.Painted.Group[2];
            (var xSamples, _) = xTrace.SnapshotProjection();
            (var ySamples, _) = yTrace.SnapshotProjection();
            (var zSamples, _) = zTrace.SnapshotProjection();
            return (xSamples, ySamples, zSamples);
        }

        public int HoverLabelYFromOffsetX(TraceGroupDisplay info, int x)
        {
            return 0;
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
