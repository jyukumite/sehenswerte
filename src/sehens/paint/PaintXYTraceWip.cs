using SehensWerte.Maths;

namespace SehensWerte.Controls.Sehens
{
    internal class PaintXYTrace : PaintTraceBase, IPaintTrace, IDisposable
    {
        public enum DrawModes
        {
            Line,
            Curve,
            Dot
        }

        private struct TraceLimitsStruct
        {
            public RectangleF TraceDrawArea;
            public double TopValue;
            public double BottomValue;
            public double LeftValue;
            public double RightValue;
        }

        private const int ErrorTransparency = 3;

        public DrawModes DrawMode;

        private TraceLimitsStruct TraceLimits;

        public PaintXYTrace(DrawModes mode)
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
            (var xSamples, var ySamples) = GetXY(ref info, out var xTrace, out var yTrace, out TraceLimits);

            float x = (float)((double)TraceLimits.TraceDrawArea.Top + TraceLimits.TopValue * (double)TraceLimits.TraceDrawArea.Height / (TraceLimits.TopValue - TraceLimits.BottomValue));
            x = (x < TraceLimits.TraceDrawArea.Top)
                ? TraceLimits.TraceDrawArea.Top
                : ((x > TraceLimits.TraceDrawArea.Bottom) ? TraceLimits.TraceDrawArea.Bottom : x);
            float y = (float)((double)TraceLimits.TraceDrawArea.Left + TraceLimits.LeftValue * (double)TraceLimits.TraceDrawArea.Width / (TraceLimits.LeftValue - TraceLimits.RightValue));
            y = (y < TraceLimits.TraceDrawArea.Left)
                ? TraceLimits.TraceDrawArea.Left
                : ((y > TraceLimits.TraceDrawArea.Right) ? TraceLimits.TraceDrawArea.Right : y);

            using Pen gradPen = new Pen(info.Skin.GraduationColour);
            graphics.DrawLine(gradPen, info.ProjectionArea.Left, x, info.ProjectionArea.Right, x);
            graphics.DrawLine(gradPen, y, TraceLimits.TraceDrawArea.Top, y, TraceLimits.TraceDrawArea.Bottom);

            xTrace.DrawnValueLowest = xSamples.Min();
            xTrace.DrawnValueHighest = xSamples.Max();
            yTrace.DrawnValueLowest = ySamples.Min();
            yTrace.DrawnValueHighest = ySamples.Max();
            PointF[] projection = Project(xSamples, ySamples, TraceLimits);

            using Pen pen = LinePen(info.View0.Colour, info);
            switch (DrawMode)
            {
                case DrawModes.Curve:
                    graphics.DrawCurve(pen, projection);
                    break;
                case DrawModes.Dot:
                    {
                        using SolidBrush brush = new SolidBrush(Color.FromArgb(128, pen.Color));
                        PaintDots(graphics, brush, projection);
                        break;
                    }
                case DrawModes.Line:
                    graphics.DrawLines(pen, projection);
                    break;
            }

            PaintPiP(info, graphics);
        }

        public override void PaintFeatures(Graphics graphics, TraceGroupDisplay info, IEnumerable<TraceFeature> features)
        {
            (var xSamples, var ySamples) = GetXY(ref info, out var xTrace, out var yTrace, out TraceLimits);

            PointF[] projection = Project(xSamples, ySamples, TraceLimits);
            using Font font = info.Skin.FeatureTextFont.Font;
            using SolidBrush solidBrush = new SolidBrush(Color.Empty);
            foreach (TraceFeature traceFeature in features)
            {
                int sampleNumber = traceFeature.SampleNumber;
                if (sampleNumber >= 0 && sampleNumber < projection.Length && traceFeature.Type == TraceFeature.Feature.Text)
                {
                    string text = traceFeature.Text;
                    solidBrush.Color = traceFeature.Colour ?? info.Skin.ForegroundColour;
                    graphics.DrawString(text, font, solidBrush, (float)projection[sampleNumber].X, (float)projection[sampleNumber].Y - font.Height);
                }
            }
        }

        private static (double[] xSamples, double[] ySamples) GetXY(ref TraceGroupDisplay info, out TraceView xTrace, out TraceView yTrace, out TraceLimitsStruct traceLimits)
        {
            if (info.View0.Painted.Group.Count != 2)
            {
                throw new PainterException("XY needs two traces");
            }
            xTrace = info.View0.Painted.Group[0];
            yTrace = info.View0.Painted.Group[1];
            (var xSamples, _) = xTrace!.SnapshotProjection();
            (var ySamples, _) = yTrace!.SnapshotProjection();
            traceLimits.TraceDrawArea = info.ProjectionArea;
            traceLimits.TopValue = yTrace!.HighestValue;
            traceLimits.BottomValue = yTrace!.LowestValue;
            traceLimits.LeftValue = xTrace!.LowestValue;
            traceLimits.RightValue = xTrace!.HighestValue;
            return (xSamples, ySamples);
        }

        private PointF[] Project(double[] xSamples, double[] ySamples, TraceLimitsStruct TraceLimits)
        {
            int num = Math.Min(xSamples.Length, ySamples.Length);
            PointF[] array = new PointF[num];
            for (int index = 0; index < num; index++)
            {
                float y = (float)((double)TraceLimits.TraceDrawArea.Top + (TraceLimits.TopValue - ySamples[index]) * (double)TraceLimits.TraceDrawArea.Height / (TraceLimits.TopValue - TraceLimits.BottomValue));
                y = (y < TraceLimits.TraceDrawArea.Top)
                    ? TraceLimits.TraceDrawArea.Top
                    : ((y > TraceLimits.TraceDrawArea.Bottom) ? TraceLimits.TraceDrawArea.Bottom : y);
                float x = (float)((double)TraceLimits.TraceDrawArea.Left + (TraceLimits.LeftValue - xSamples[index]) * (double)(TraceLimits.TraceDrawArea.Right - TraceLimits.TraceDrawArea.Left) / (TraceLimits.LeftValue - TraceLimits.RightValue));
                x = (x < TraceLimits.TraceDrawArea.Left)
                    ? TraceLimits.TraceDrawArea.Left
                    : ((x > TraceLimits.TraceDrawArea.Right) ? TraceLimits.TraceDrawArea.Right : x);
                array[index].X = Math.Max(TraceLimits.TraceDrawArea.Left, Math.Min(TraceLimits.TraceDrawArea.Right, x));
                array[index].Y = Math.Max(TraceLimits.TraceDrawArea.Top, Math.Min(TraceLimits.TraceDrawArea.Bottom, y));
            }
            return array;
        }

        public int HoverLabelYFromOffsetX(TraceGroupDisplay info, int x)
        {
            return 0;
        }

        public new string GetHoverValue(List<TraceView> list, MouseEventArgs e)
        {
            string result = "";
            if (list.Count >= 2)
            {
                TraceView.MouseInfo measureInfo = list[0].Measure(e);
                result = string.Concat(str2: list[1].Measure(e).YValue.ToStringRound(3, 0), str0: measureInfo.XValue.ToStringRound(3, 0), str1: ",");
            }
            return result;
        }

        public new string GetHoverStatistics(TraceView trace, TraceView.MouseInfo info)
        {
            return "";
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
