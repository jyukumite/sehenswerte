using SehensWerte.Maths;

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

        private struct Limits
        {
            public RectangleF DrawArea;
            public double XLow, XHigh;
            public double YLow, YHigh;
            public double ZLow, ZHigh;
        }

        // Pinhole-camera distance in normalised Z units (the normalised Z range is -0.5..+0.5).
        // Larger values flatten the perspective; smaller values exaggerate it.
        private const double CameraDistance = 2.5;

        private DrawModes DrawMode;
        private Limits m_Limits;
        private TraceView? m_XTrace;
        private TraceView? m_YTrace;
        private TraceView? m_ZTrace;

        public PaintXYZTrace(DrawModes mode)
        {
            DrawMode = mode;
        }

        public void PaintInitial(Graphics graphics, TraceGroupDisplay info)
        {
            // cache per-paint state so axis painters (which run before PaintProjection) can see it
            GetXYZ(ref info, out m_XTrace, out m_YTrace, out m_ZTrace, out m_Limits);
        }

        public override void PaintProjection(Graphics graphics, TraceGroupDisplay info)
        {
            if (info.View0.Painted.TraceIndex != 0) return;
            var samples = GetXYZ(ref info, out var xTrace, out var yTrace, out var zTrace, out m_Limits);
            double[] xSamples = samples.x;
            double[] ySamples = samples.y;
            double[] zSamples = samples.z;

            graphics.SetClip(m_Limits.DrawArea);
            try
            {
                PaintCube(graphics, info);

                if (xSamples.Length > 0 && ySamples.Length > 0 && zSamples.Length > 0)
                {
                    xTrace.DrawnValueLowest = xSamples.Min();
                    xTrace.DrawnValueHighest = xSamples.Max();
                    yTrace.DrawnValueLowest = ySamples.Min();
                    yTrace.DrawnValueHighest = ySamples.Max();
                    zTrace.DrawnValueLowest = zSamples.Min();
                    zTrace.DrawnValueHighest = zSamples.Max();
                }

                PointF[] projection = Project(xSamples, ySamples, zSamples, m_Limits);
                if (projection.Length >= 2)
                {
                    using Pen pen = LinePen(info.View0.Colour, info);
                    switch (DrawMode)
                    {
                        case DrawModes.RectangularCurve:
                            graphics.DrawCurve(pen, projection);
                            break;
                        case DrawModes.RectangularDot:
                            using (SolidBrush brush = new SolidBrush(Color.FromArgb(128, pen.Color)))
                            {
                                PaintDots(graphics, brush, projection);
                            }
                            break;
                        case DrawModes.RectangularLine:
                            graphics.DrawLines(pen, projection);
                            break;
                        default:
                            // Polar* not implemented yet; fall back to line
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
            var samples = GetXYZ(ref info, out _, out _, out _, out m_Limits);
            PointF[] projection = Project(samples.x, samples.y, samples.z, m_Limits);
            using Font font = info.Skin.FeatureTextFont.Font;
            using SolidBrush brush = new SolidBrush(Color.Empty);
            foreach (TraceFeature f in features)
            {
                int n = f.SampleNumber;
                if (n >= 0 && n < projection.Length && f.Type == TraceFeature.Feature.Text)
                {
                    brush.Color = f.Colour ?? info.Skin.ForegroundColour;
                    graphics.DrawString(f.Text, font, brush, projection[n].X, projection[n].Y - font.Height);
                }
            }
        }

        public override void PaintHorizontalAxis(Graphics graphics, TraceGroupDisplay info)
        {
            if (m_XTrace == null) return;
            PaintGutterBottomPartition(info, graphics, m_Limits.XLow, m_Limits.XHigh);
        }

        public override void PaintVerticalAxis(Graphics graphics, TraceGroupDisplay info)
        {
            if (m_YTrace == null) return;
            base.PaintVerticalAxis(graphics, info);
        }

        protected override IEnumerable<double> VerticalAxisPartition(TraceGroupDisplay info, int partitions, out double highestValue, out double lowestValue)
        {
            highestValue = m_Limits.YHigh;
            lowestValue = m_Limits.YLow;
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

        private static (double[] x, double[] y, double[] z) GetXYZ(
            ref TraceGroupDisplay info,
            out TraceView xTrace,
            out TraceView yTrace,
            out TraceView zTrace,
            out Limits limits)
        {
            if (info.View0.Painted.Group.Count != 3)
            {
                throw new PainterException("XYZ needs three traces");
            }
            xTrace = info.View0.Painted.Group[0];
            yTrace = info.View0.Painted.Group[1];
            zTrace = info.View0.Painted.Group[2];

            // Acknowledge each trace's snapshot so m_RecalculateProjectionRequired is cleared.
            // We read CalculatedBeforeZoom directly below because XYZ needs the full sample set.
            xTrace.SnapshotProjection();
            yTrace.SnapshotProjection();
            zTrace.SnapshotProjection();

            double[] xs = (xTrace.CanShowRealYT ? xTrace.Samples.ViewedSamplesInterpolatedAsDouble : xTrace.CalculatedBeforeZoom) ?? new double[0];
            double[] ys = (yTrace.CanShowRealYT ? yTrace.Samples.ViewedSamplesInterpolatedAsDouble : yTrace.CalculatedBeforeZoom) ?? new double[0];
            double[] zs = (zTrace.CanShowRealYT ? zTrace.Samples.ViewedSamplesInterpolatedAsDouble : zTrace.CalculatedBeforeZoom) ?? new double[0];

            limits.DrawArea = info.ProjectionArea;
            limits.XLow = WindowLow(xTrace.LowestValue, xTrace.HighestValue, info.View0.XYXZoom, info.View0.XYXPan, out double xWin);
            limits.XHigh = limits.XLow + xWin;
            limits.YLow = WindowLow(yTrace.LowestValue, yTrace.HighestValue, info.View0.XYYZoom, info.View0.XYYPan, out double yWin);
            limits.YHigh = limits.YLow + yWin;
            limits.ZLow = WindowLow(zTrace.LowestValue, zTrace.HighestValue, info.View0.XYZZoom, info.View0.XYZPan, out double zWin);
            limits.ZHigh = limits.ZLow + zWin;
            return (xs, ys, zs);
        }

        private static double WindowLow(double fullLow, double fullHigh, double zoom, double pan, out double winSize)
        {
            double full = fullHigh - fullLow;
            winSize = full * zoom;
            return fullLow + (full - winSize) * pan;
        }

        private PointF[] Project(double[] xs, double[] ys, double[] zs, Limits limits)
        {
            int count = Math.Min(xs.Length, Math.Min(ys.Length, zs.Length));
            PointF[] result = new PointF[count];
            double xRange = limits.XHigh - limits.XLow;
            double yRange = limits.YHigh - limits.YLow;
            double zRange = limits.ZHigh - limits.ZLow;
            if (xRange == 0 || yRange == 0 || zRange == 0) return result;

            float cx = limits.DrawArea.Left + limits.DrawArea.Width / 2f;
            float cy = limits.DrawArea.Top + limits.DrawArea.Height / 2f;
            float halfW = limits.DrawArea.Width / 2f;
            float halfH = limits.DrawArea.Height / 2f;

            for (int i = 0; i < count; i++)
            {
                double nx = (xs[i] - limits.XLow) / xRange - 0.5;       // -0.5..+0.5
                double ny = (ys[i] - limits.YLow) / yRange - 0.5;
                double nz = (zs[i] - limits.ZLow) / zRange - 0.5;
                double scale = CameraDistance / (CameraDistance - nz);  // high z = closer to viewer = larger
                result[i].X = (float)(cx + nx * halfW * 2 * scale);
                result[i].Y = (float)(cy - ny * halfH * 2 * scale);
            }
            return result;
        }

        private void PaintCube(Graphics graphics, TraceGroupDisplay info)
        {
            // Project the eight corners of the viewport box through the same perspective so the
            // wireframe reads as a 3D reference frame for the data.
            double xRange = m_Limits.XHigh - m_Limits.XLow;
            double yRange = m_Limits.YHigh - m_Limits.YLow;
            double zRange = m_Limits.ZHigh - m_Limits.ZLow;
            if (xRange == 0 || yRange == 0 || zRange == 0) return;
            float cx = m_Limits.DrawArea.Left + m_Limits.DrawArea.Width / 2f;
            float cy = m_Limits.DrawArea.Top + m_Limits.DrawArea.Height / 2f;
            float halfW = m_Limits.DrawArea.Width / 2f;
            float halfH = m_Limits.DrawArea.Height / 2f;
            PointF[] corners = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                double nx = ((i & 1) == 0 ? -0.5 : 0.5);
                double ny = ((i & 2) == 0 ? -0.5 : 0.5);
                double nz = ((i & 4) == 0 ? -0.5 : 0.5);
                double scale = CameraDistance / (CameraDistance - nz);
                corners[i].X = (float)(cx + nx * halfW * 2 * scale);
                corners[i].Y = (float)(cy - ny * halfH * 2 * scale);
            }
            int[][] edges =
            {
                new[] { 0, 1 }, new[] { 2, 3 }, new[] { 4, 5 }, new[] { 6, 7 }, // x-edges
                new[] { 0, 2 }, new[] { 1, 3 }, new[] { 4, 6 }, new[] { 5, 7 }, // y-edges
                new[] { 0, 4 }, new[] { 1, 5 }, new[] { 2, 6 }, new[] { 3, 7 }, // z-edges
            };
            using Pen pen = new Pen(info.Skin.GraduationColour);
            foreach (int[] e in edges)
            {
                graphics.DrawLine(pen, corners[e[0]], corners[e[1]]);
            }
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
