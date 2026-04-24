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
        private TraceView? m_XTrace;
        private TraceView? m_YTrace;

        public PaintXYTrace(DrawModes mode)
        {
            DrawMode = mode;
        }

        public void PaintInitial(Graphics graphics, TraceGroupDisplay info)
        {
            // cache trace refs and viewport so axis painters (which run before PaintProjection) can see them
            GetXY(ref info, out m_XTrace, out m_YTrace, out TraceLimits);
        }

        public override void PaintProjection(Graphics graphics, TraceGroupDisplay info)
        {
            if (info.View0.Painted.TraceIndex != 0) return;
            (var xSamples, var ySamples) = GetXY(ref info, out var xTrace, out var yTrace, out TraceLimits);

            // axis crosshairs (origin lines if visible within the viewport)
            using Pen gradPen = new Pen(info.Skin.GraduationColour);
            if (TraceLimits.TopValue >= 0 && TraceLimits.BottomValue <= 0 && TraceLimits.TopValue != TraceLimits.BottomValue)
            {
                float yOrigin = (float)(TraceLimits.TraceDrawArea.Top + TraceLimits.TopValue * TraceLimits.TraceDrawArea.Height / (TraceLimits.TopValue - TraceLimits.BottomValue));
                graphics.DrawLine(gradPen, TraceLimits.TraceDrawArea.Left, yOrigin, TraceLimits.TraceDrawArea.Right, yOrigin);
            }
            if (TraceLimits.LeftValue <= 0 && TraceLimits.RightValue >= 0 && TraceLimits.LeftValue != TraceLimits.RightValue)
            {
                float xOrigin = (float)(TraceLimits.TraceDrawArea.Left + (0 - TraceLimits.LeftValue) * TraceLimits.TraceDrawArea.Width / (TraceLimits.RightValue - TraceLimits.LeftValue));
                graphics.DrawLine(gradPen, xOrigin, TraceLimits.TraceDrawArea.Top, xOrigin, TraceLimits.TraceDrawArea.Bottom);
            }

            if (xSamples.Length > 0 && ySamples.Length > 0)
            {
                xTrace.DrawnValueLowest = xSamples.Min();
                xTrace.DrawnValueHighest = xSamples.Max();
                yTrace.DrawnValueLowest = ySamples.Min();
                yTrace.DrawnValueHighest = ySamples.Max();
            }
            PointF[] projection = Project(xSamples, ySamples, TraceLimits);

            graphics.SetClip(TraceLimits.TraceDrawArea);
            try
            {
                using Pen pen = LinePen(info.View0.Colour, info);
                if (projection.Length >= 2)
                {
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
                }
            }
            finally
            {
                graphics.ResetClip();
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

        public override void PaintHorizontalAxis(Graphics graphics, TraceGroupDisplay info)
        {
            if (m_XTrace == null) return;
            PaintGutterBottomPartition(info, graphics, TraceLimits.LeftValue, TraceLimits.RightValue);
        }

        public override void PaintVerticalAxis(Graphics graphics, TraceGroupDisplay info)
        {
            if (m_YTrace == null) return;
            base.PaintVerticalAxis(graphics, info);
        }

        protected override IEnumerable<double> VerticalAxisPartition(TraceGroupDisplay info, int partitions, out double highestValue, out double lowestValue)
        {
            highestValue = TraceLimits.TopValue;
            lowestValue = TraceLimits.BottomValue;
            return GetPartitions(lowestValue, highestValue, partitions);
        }

        public override string VerticalAxisFormat(TraceGroupDisplay info, double yValue)
        {
            return (m_YTrace != null)
                ? string.Format(m_YTrace.VerticalUnitFormat, yValue.ToStringRound(5, 3))
                : yValue.ToStringRound(5, 3);
        }

        protected override string ToHorizontalUnit(TraceGroupDisplay info, double xValue)
        {
            return (m_XTrace != null)
                ? string.Format(m_XTrace.VerticalUnitFormat, xValue.ToStringRound(5, 3))
                : xValue.ToStringRound(5, 3);
        }

        public override void PaintAxisTitleHorizontal(Graphics graphics, TraceGroupDisplay info)
        {
            // For XY the "horizontal axis" is the X source trace. Prefer its own AxisTitleLeft
            // (what it would label its own vertical axis with), falling back to the view name.
            string? title = m_XTrace?.Samples.AxisTitleLeft;
            if (string.IsNullOrEmpty(title)) title = m_XTrace?.ViewName;
            PaintAxisTitle(graphics, info, title, horizontal: true);
        }

        public override void PaintAxisTitleVertical(Graphics graphics, TraceGroupDisplay info)
        {
            string? title = m_YTrace?.Samples.AxisTitleLeft;
            if (string.IsNullOrEmpty(title)) title = m_YTrace?.ViewName;
            PaintAxisTitle(graphics, info, title, horizontal: false);
        }

        private void PaintAxisTitle(Graphics graphics, TraceGroupDisplay info, string? title, bool horizontal)
        {
            if (string.IsNullOrEmpty(title)) return;
            using Font font = info.Skin.AxisTitleFont.Font;
            using Brush brush = info.Skin.AxisTitleFont.Brush;
            SizeF sz = graphics.MeasureString(title, font);
            if (horizontal)
            {
                graphics.DrawString(title, font, brush, info.GroupArea.Right - sz.Width - 10f, info.ProjectionArea.Bottom + 1);
            }
            else
            {
                using System.Drawing.Drawing2D.Matrix matrix = new System.Drawing.Drawing2D.Matrix(1f, 0f, 0f, 1f, 0f, 0f);
                float offsetX = (info.View0.Scope.ActiveSkin.VerticalAxisPosition == Skin.VerticalAxisPositions.Left)
                    ? info.VerticalAxisArea.Left + 10
                    : info.VerticalAxisArea.Right - sz.Height - 10f;
                float offsetY = info.ProjectionArea.Top + info.ProjectionArea.Height / 2;
                matrix.Rotate(-90f, System.Drawing.Drawing2D.MatrixOrder.Append);
                matrix.Translate(offsetX, offsetY, System.Drawing.Drawing2D.MatrixOrder.Append);
                try
                {
                    graphics.Transform = matrix;
                    graphics.DrawString(title, font, brush, -sz.Width / 2f, 0f);
                }
                finally
                {
                    graphics.ResetTransform();
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

            // Acknowledge the per-trace snapshot to clear m_RecalculateProjectionRequired;
            // the zoom-truncated buffer returned is discarded because XY needs the full sample set.
            xTrace.SnapshotProjection();
            yTrace.SnapshotProjection();

            double[] xSamples = (xTrace.CanShowRealYT ? xTrace.Samples.ViewedSamplesInterpolatedAsDouble : xTrace.CalculatedBeforeZoom) ?? new double[0];
            double[] ySamples = (yTrace.CanShowRealYT ? yTrace.Samples.ViewedSamplesInterpolatedAsDouble : yTrace.CalculatedBeforeZoom) ?? new double[0];

            traceLimits.TraceDrawArea = info.ProjectionArea;

            // XY viewport: zoom/pan on View0 (the XY trace host) drive the porthole over the full data extents.
            double fullXLow = xTrace.LowestValue;
            double fullXHigh = xTrace.HighestValue;
            double fullYLow = yTrace.LowestValue;
            double fullYHigh = yTrace.HighestValue;
            double fullX = fullXHigh - fullXLow;
            double fullY = fullYHigh - fullYLow;

            double winX = fullX * info.View0.XYXZoom;
            double leftX = fullXLow + (fullX - winX) * info.View0.XYXPan;
            double winY = fullY * info.View0.XYYZoom;
            double botY = fullYLow + (fullY - winY) * info.View0.XYYPan;

            traceLimits.LeftValue = leftX;
            traceLimits.RightValue = leftX + winX;
            traceLimits.BottomValue = botY;
            traceLimits.TopValue = botY + winY;
            return (xSamples, ySamples);
        }

        private PointF[] Project(double[] xSamples, double[] ySamples, TraceLimitsStruct limits)
        {
            int num = Math.Min(xSamples.Length, ySamples.Length);
            PointF[] array = new PointF[num];
            double xRange = limits.RightValue - limits.LeftValue;
            double yRange = limits.TopValue - limits.BottomValue;
            if (xRange == 0 || yRange == 0) return array;
            for (int index = 0; index < num; index++)
            {
                float x = (float)(limits.TraceDrawArea.Left + (xSamples[index] - limits.LeftValue) * limits.TraceDrawArea.Width / xRange);
                float y = (float)(limits.TraceDrawArea.Top + (limits.TopValue - ySamples[index]) * limits.TraceDrawArea.Height / yRange);
                array[index].X = x;
                array[index].Y = y;
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
