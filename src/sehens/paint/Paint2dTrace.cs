using Microsoft.VisualStudio.TestTools.UnitTesting;
using SehensWerte.Generators;
using SehensWerte.Maths;
using System.Drawing.Drawing2D;

namespace SehensWerte.Controls.Sehens
{
    public partial class Paint2dTrace : PaintTraceBase, IPaintTrace, IDisposable
    {
        public PointF[]? DrawnPolygon;
        public PointF[]? DrawnProjection1;
        public PointF[]? DrawnProjection2;
        public PointF[]? DrawnYT;

        protected List<PointF>? DotDrawPoints;
        protected bool SaveTraceVerticalExtents = true;
        protected double LastTraceLowestValue = -1.0;
        protected double LastTraceHighestValue = 1.0;

        public TraceView? View;
        public double[] PaintSamples = new double[0];
        public TraceView.PaintModes PaintMode;
        public RectangleF PaintProjectionArea;
        public double PaintHighestValue;
        public double PaintLowestValue;
        public double PaintHighestY;
        public double PaintLowestY;
        public double PaintLeftHValue;
        public double PaintRightHValue;

        private static List<Tuple<double, string>> HorizontalUnits = new List<Tuple<double, string>>
        {
            { 604800.0, "yyyy/MM/dd" },
            { 86400.0, "yyyy/MM/dd HH:mm" },
            { 3600.0, "HH:mm:ss" },
            { 60.0, "HH:mm:ss.FFF" },
            { 0.0, "s.fff \"s\"" }
        };

        public class PointCompareX : IComparer<PointF>
        {
            public int Compare(PointF left, PointF right) => left.X < right.X ? -1 : left.X > right.X ? 1 : 0;
        }

        public override void PaintProjection(Graphics graphics, TraceGroupDisplay info)
        {
            //fixme: last drawn sample is horizontal instead of pointing at the real next location

            var bounds = graphics.ClipBounds;
            try
            {
                graphics.SetClip((RectangleF)info.ProjectionArea);

                if (info.YTTrace)
                {
                    PaintProjectionYT(info, graphics);
                }
                else
                {
                    DrawnYT = null;
                    PaintProjectionY(info, graphics);
                }
                if (SaveTraceVerticalExtents)
                {
                    info.View0.DrawnValueLowest = LastTraceLowestValue;
                    info.View0.DrawnValueHighest = LastTraceHighestValue;
                }
            }
            finally
            {
                graphics.SetClip(bounds);
            }
        }

        protected Color InterpolateColour(Color c1, Color c2, int col, int max)
        {
            int num = (int)(Math.Pow(1.0 / (max - col), 1.5) * 750.0);
            return Color.FromArgb(
                c1.A + (c2.B - c1.A) * num / 1000,
                c1.R + (c2.R - c1.R) * num / 1000,
                c1.G + (c2.G - c1.G) * num / 1000,
                c1.B + (c2.B - c1.B) * num / 1000);
        }

        private void AdjustLastTraceValue(TraceGroupDisplay info, double value, PointF projected)
        {
            if (double.IsFinite(LastTraceHighestValue))
            {
                LastTraceHighestValue = Math.Max(value, LastTraceHighestValue);
                LastTraceLowestValue = Math.Min(value, LastTraceLowestValue);
            }
            else
            {
                LastTraceHighestValue = value;
                LastTraceLowestValue = value;
            }
        }

        protected override string ToHorizontalUnit(TraceGroupDisplay info, double xValue)
        {
            if (info.YTTrace)
            {
                DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(xValue);
                double span = Math.Abs(info.RightUnixTime - info.LeftUnixTime);
                string text = "yyyy/MM/dd HH:mm:ss.FFF";
                string format = info.View0.ZoomValue > 0.999
                    ? text
                    : HorizontalUnits.Where(x => span >= x.Item1).First().Item2;
                return dateTime.ToString(format);
            }
            else
            {
                return base.ToHorizontalUnit(info, xValue);
            }
        }

        public int HoverLabelYFromOffsetX(TraceGroupDisplay info, int x)
        {
            int y = 0;
            if (DrawnYT != null)
            {
                int v = FindIndexOfX(DrawnYT, x);
                if (v != -1)
                {
                    y = (int)(DrawnYT[v].Y + 0.5f);
                }
            }
            else if (DrawnProjection1 != null && DrawnProjection1!.Length != 0)
            {
                int index = FindIndexOfX(DrawnProjection1, x);
                if (index != -1)
                {
                    y = (int)(DrawnProjection1[index].Y + 0.5f);
                    if (DrawnProjection2 != null && DrawnProjection2!.Length != 0)
                    {
                        y = (int)((y + DrawnProjection2[index].Y) / 2);
                    }
                }
            }
            return y;
        }

        private static int FindIndexOfX(PointF[] array, int x)
        {
            int index = Array.BinarySearch(array, new PointF(x, 0), new PointCompareX());
            if (index < 0)
            {
                index = ~index;
            }
            if (index > 0)
            {
                index--;
            }
            if (index >= array.Length)
            {
                index = array.Length - 1;
            }
            return index;
        }

        private float Project(double y)
        {
            if (y < PaintLowestY)
            {
                PaintLowestY = y;
            }
            if (y > PaintHighestY)
            {
                PaintHighestY = y;
            }
            float num;
            if (View?.IsLogY == true)
            {
                ProjectLog(View.HighestValue, y, out var newMax, out var output);
                num = (float)(PaintProjectionArea.Top + (newMax - output) * PaintProjectionArea.Height / newMax);
            }
            else
            {
                num = (float)(PaintProjectionArea.Top + (PaintHighestValue - y) * PaintProjectionArea.Height / (PaintHighestValue - PaintLowestValue));
            }

            return Math.Max(PaintProjectionArea.Top - 1, Math.Min(PaintProjectionArea.Bottom + 1, num));
        }

        private PointF[]? ProjectPolygon() => ProjectPolygon(DrawnProjection1, DrawnProjection2);

        private static PointF[]? ProjectPolygon(PointF[]? projection1, PointF[]? projection2)
        {
            if (projection1 == null || projection2 == null) return null;

            var result = new List<PointF>();

            //fixme: split polygons for embedded NANs
            for (int loop = 0; loop < projection1.Length; loop++)
            {
                if (float.IsFinite(projection1[loop].Y))
                {
                    result.Add(projection1[loop]);
                }
            }
            for (int loop = projection2.Length - 1; loop >= 0; loop--)
            {
                if (float.IsFinite(projection2[loop].Y))
                {
                    result.Add(projection2[loop]);
                }
            }
            return result.ToArray();
        }

        private static (PointF[], PointF[]) WidenEnvelope(PointF[] projection1, PointF[] projection2, float lineWidth)
        {
            PointF[] widened1 = (PointF[])projection1.Clone();
            PointF[] widened2 = (PointF[])projection2.Clone();
            int count = Math.Min(widened1.Length, widened2.Length);
            for (int loop = 0; loop < count; loop++)
            {
                float y1 = widened1[loop].Y;
                float y2 = widened2[loop].Y;
                float pad = (lineWidth - (Math.Abs(y1 - y2) + 1f)) / 2f;
                if (pad > 0 && float.IsFinite(pad))
                {
                    widened1[loop].Y = y1 >= y2 ? y1 + pad : y1 - pad;
                    widened2[loop].Y = y1 >= y2 ? y2 - pad : y2 + pad;
                }
            }
            return (widened1, widened2);
        }

        private void ProjectYT(TraceGroupDisplay info, int leftIndex, int rightIndex)
        {
            if (DrawnYT != null) return;

            TraceData samplesYT = info.View0.Samples;
            double[] samples = samplesYT.ViewedSamplesAsDouble;
            if (samples.Length == 0)
            {
                DrawnYT = new PointF[0];
                return;
            }
            leftIndex = Math.Clamp(leftIndex, 0, samples.Length - 1);
            rightIndex = Math.Clamp(rightIndex, leftIndex, samples.Length - 1);
            int length = rightIndex - leftIndex + 1;

            PointF[] path = new PointF[length];
            double resampleRate = samplesYT.ViewedSamplesPerSecond;

            double leftTime = double.NegativeInfinity;
            double rightTime = double.PositiveInfinity;

            double leftValue = 0.0;
            double rightValue = 0.0;

            double viewedLeftmostUnixTime = samplesYT.ViewedLeftmostUnixTime;
            double[]? unixTime = samplesYT.ViewedUnixTime;

            for (int index = leftIndex; index <= rightIndex; index++)
            {
                double sample = samples[index];
                double time = (unixTime == null) ? viewedLeftmostUnixTime + index / resampleRate : unixTime[index];
                PointF projected = path[index - leftIndex] = Project(info, sample, time);
                if (projected.X < info.ProjectionArea.Left)
                {
                    if (leftTime == double.NegativeInfinity || time > leftTime)
                    {
                        leftTime = time;
                        leftValue = sample;
                    }
                }
                else if (projected.X >= info.ProjectionArea.Right)
                {
                    if (rightTime == double.PositiveInfinity || time < rightTime)
                    {
                        rightTime = time;
                        rightValue = sample;
                    }
                }
                else
                {
                    AdjustLastTraceValue(info, sample, projected);
                }
            }
            if (rightTime != double.PositiveInfinity)
            {
                LastTraceHighestValue = Math.Max(rightValue, LastTraceHighestValue);
                LastTraceLowestValue = Math.Min(rightValue, LastTraceLowestValue);
            }
            if (leftTime != double.NegativeInfinity)
            {
                LastTraceHighestValue = Math.Max(leftValue, LastTraceHighestValue);
                LastTraceLowestValue = Math.Min(leftValue, LastTraceLowestValue);
            }

            bool padLeft = info.View0.PadLeftWithFirstValue;
            bool padRight = info.View0.PadRightWithLastValue;
            if ((padLeft || padRight) && length > 0)
            {
                double firstTime = unixTime == null ? viewedLeftmostUnixTime + leftIndex / resampleRate : unixTime[leftIndex];
                double lastTime = unixTime == null ? viewedLeftmostUnixTime + rightIndex / resampleRate : unixTime[rightIndex];
                var padded = new List<PointF>(path.Length + 2);
                if (padLeft && firstTime > info.LeftUnixTime)
                {
                    padded.Add(Project(info, samples[leftIndex], info.LeftUnixTime));
                }
                padded.AddRange(path);
                if (padRight && lastTime < info.RightUnixTime)
                {
                    padded.Add(Project(info, samples[rightIndex], info.RightUnixTime));
                }
                path = padded.ToArray();
            }

            DrawnProjection1 = null;
            DrawnProjection2 = null;
            DrawnYT = path.ToArray();
        }

        private static PointF Project(TraceGroupDisplay info, double sample, double unixTime)
        {
            double x = info.ProjectionArea.Left + (unixTime - info.LeftUnixTime) * info.ProjectionArea.Width / (info.RightUnixTime - info.LeftUnixTime);
            double y = info.ProjectionArea.Top + (info.View0.HighestValue - sample) * info.ProjectionArea.Height / (info.View0.HighestValue - info.View0.LowestValue);
            double leftX = info.ProjectionArea.Left - 10000;
            double rightX = info.ProjectionArea.Right + 10000;
            return new Point((int)(x <= leftX ? leftX : (x > rightX ? rightX : x)), (int)y);
        }

        public virtual void PaintInitial(Graphics graphics, TraceGroupDisplay info)
        {
            if (info.OverlayIndex != 0) return;

            bool grads = !info.DrawPictureInPicture;
            bool stats = info.Scope.ShowTraceStatistics == Skin.TraceStatistics.Embedded && !info.DrawPictureInPicture;

            if (!grads && info.View0.LowestValue < 0.0 && info.View0.HighestValue > 0.0)
            {
                using Pen pen = new Pen(info.Skin.GraduationColour);
                pen.DashStyle = DashStyle.Dash;
                graphics.DrawLine(pen, info.ProjectionArea.Left, info.YOffsetOf0Sample, info.ProjectionArea.Right, info.YOffsetOf0Sample);
            }
            if (!grads && !stats && info.ProjectionArea.Height > info.Skin.AxisTextFont.LineSpacing * 2)
            {
                PaintEmbeddedTraceExtents(info, graphics);
            }
        }

        protected void PaintProjection(PointF[]? projection, Graphics graphics, Pen pen)
        {
            if (projection == null || projection.Length == 0) return;

            if (projection.Length <= 1)
            {
                using SolidBrush brush = new SolidBrush(pen.Color);
                PaintTraceBase.PaintFilledCircles(graphics, brush, projection, pen.Width + 1);
            }
            else
            {
                var line = projection.Where(point => float.IsFinite(point.Y) && point.Y > -100000 && point.Y < 100000).ToArray();
                if (line.Length > 1)
                {
                    graphics.DrawLines(pen, line.ToArray());
                }
            }
        }

        private void PaintProjectionY(TraceGroupDisplay info, Graphics graphics)
        {
            double[] samples = SnapshotProjection(info.View0);

            // Value-aligned traces draw into a narrower pixel sub-window (ValueRect); == ProjectionArea
            // for the legacy stretch/incompatible path. The projection cache is keyed on data + log-H
            // values, NOT on the rect, so a change in the sub-window (sibling added/hidden/rescaled,
            // group range shift) must force a reprojection or the trace would draw at the stale width.
            Rectangle valueRect = info.ValueRect;
            if (info.View0.IsLogX
                && (PaintLeftHValue != info.LeftSampleNumberValue || PaintRightHValue != info.RightSampleNumberValue))
            {
                SnapshotReprojectionRequired = true;
            }
            if (PaintProjectionArea != valueRect)
            {
                SnapshotReprojectionRequired = true;
            }
            if (SnapshotReprojectionRequired)
            {
                LastTraceHighestValue = double.PositiveInfinity;
                LastTraceLowestValue = double.NegativeInfinity;
                SnapshotReprojectionRequired = false;
                DrawnPolygon = null;
                Project2dCurves(samples, info.View0, valueRect, info.View0.HighestValue, info.View0.LowestValue, info.LeftSampleNumberValue, info.RightSampleNumberValue);
            }

            if (info.View0.PaintMode == TraceView.PaintModes.Spectral)
            {
                PaintSpectral(info, graphics);
                PaintPiP(info, graphics);
            }
            else
            {
                bool dots = info.View0.PaintMode == TraceView.PaintModes.Points || info.View0.PaintMode == TraceView.PaintModes.PointsIfChanged;
                PointF[]? projection1 = DrawnProjection1;
                PointF[]? projection2 = DrawnProjection2;
                PointF[]? polygon = DrawnPolygon;
                if (!dots && polygon != null && projection1 != null && projection2 != null)
                {
                    float lineWidth = EffectiveLineWidth(info);
                    if (lineWidth > 1f)
                    {
                        (projection1, projection2) = WidenEnvelope(projection1, projection2, lineWidth);
                        polygon = ProjectPolygon(projection1, projection2);
                    }
                }
                if (polygon != null && !dots && polygon.Length > 0)
                {
                    using Brush brush = new SolidBrush(InterpolateColour(info.Skin.BackgroundColour, info.View0.Colour, 0, 1));
                    graphics.FillPolygon(brush, polygon);
                }

                Color color = dots ? InterpolateColour(info.View0.Colour, info.Skin.BackgroundColour, 0, 1) : info.View0.Colour;
                if (dots && DotDrawPoints != null)
                {
                    using Pen pen = LinePen(color, info);
                    using Brush brush = new SolidBrush(info.View0.Colour);
                    PaintTraceBase.PaintFilledCircles(graphics, brush, DotDrawPoints!.ToArray(), 4f);
                }
                else if (projection1 != null && projection2 != null)
                {
                    using Pen pen = new Pen(color, width: 1);
                    PaintProjection(projection1, graphics, pen);
                    PaintProjection(projection2, graphics, pen);
                }
                else if (projection1 != null)
                {
                    using Pen pen = LinePen(color, info);
                    PaintProjection(projection1, graphics, pen);
                }
                PaintPiP(info, graphics);
            }
        }

        private void PaintSpectral(TraceGroupDisplay info, Graphics graphics)
        {
            if (DrawnPolygon == null || DrawnPolygon.Length < 3) return;

            // vertical gradient in the projection rectangle - green at the bottom, blue at the top.
            var rect = new RectangleF(
                info.ProjectionArea.Left, info.ProjectionArea.Top,
                Math.Max(1, info.ProjectionArea.Width), Math.Max(1, info.ProjectionArea.Height));
            using (var brush = new LinearGradientBrush(rect, SpectralFillTop, SpectralFillBottom, LinearGradientMode.Vertical))
            {
                brush.WrapMode = WrapMode.TileFlipXY; // avoid the edge sliver
                graphics.FillPolygon(brush, DrawnPolygon);
            }

            if (DrawnProjection1 != null && DrawnProjection1.Length > 1)
            {
                var prevSmoothing = graphics.SmoothingMode;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(SpectralCapColour, 1f);
                PaintProjection(DrawnProjection1, graphics, pen);
                graphics.SmoothingMode = prevSmoothing;
            }
        }

        private void PaintProjectionYT(TraceGroupDisplay info, Graphics graphics)
        {
            PaintPiP(info, graphics);

            double overscan = (info.RightUnixTime - info.LeftUnixTime) / 20.0;
            var indices = SnapshotYTProjection(info, overscan);
            if (SnapshotReprojectionRequired)
            {
                LastTraceHighestValue = double.PositiveInfinity;
                LastTraceLowestValue = double.NegativeInfinity;
                SnapshotReprojectionRequired = false;
                DrawnPolygon = null;
                DrawnYT = null;
            }
            RectangleF clipBounds = graphics.ClipBounds;
            graphics.SetClip(Rectangle.Inflate(info.ProjectionArea, 0, 1));
            if (info.View0.PaintMode == TraceView.PaintModes.Points)
            {
                PaintProjectionYTDots(info, graphics, indices.leftIndex, indices.rightIndex);
            }
            else if ((indices.rightIndex - indices.leftIndex + 1) > info.ProjectionArea.Width * 10)
            {
                PaintProjectionYTPolygon(info, graphics, indices.leftIndex, indices.rightIndex);
            }
            else
            {
                PaintProjectionYTLine(info, graphics, indices.leftIndex, indices.rightIndex);
            }
            graphics.SetClip(clipBounds);
        }

        private void PaintProjectionYTDots(TraceGroupDisplay info, Graphics graphics, int leftIndex, int rightIndex)
        {
            ProjectYT(info, leftIndex, rightIndex);

            using Pen pen = new Pen(InterpolateColour(info.View0.Colour, info.Skin.BackgroundColour, 0, 1));
            using Brush brush = new SolidBrush(info.View0.Colour);
            if (DrawnYT?.Length > 1)
            {
                graphics.DrawLines(pen, DrawnYT);
            }
            if (DrawnYT != null)
            {
                PaintFilledCircles(graphics, brush, DrawnYT, 4f);
            }
        }

        private void PaintProjectionYTLine(TraceGroupDisplay info, Graphics graphics, int leftIndex, int rightIndex)
        {
            ProjectYT(info, leftIndex, rightIndex);

            using Pen pen = LinePen(info.View0.Colour, info);
            PaintProjection(DrawnYT, graphics, pen);
        }

        private void PaintProjectionYTPolygon(TraceGroupDisplay info, Graphics graphics, int leftIndex, int rightIndex)
        {
            if (DrawnPolygon == null || DrawnProjection1 == null || DrawnProjection2 == null)
            {
                TraceData samplesYT = info.View0.Samples;
                double[] samples = samplesYT.ViewedSamplesAsDouble;
                if (samples.Length == 0) return;
                leftIndex = Math.Clamp(leftIndex, 0, samples.Length - 1);
                rightIndex = Math.Clamp(rightIndex, leftIndex, samples.Length - 1);
                int width = info.ProjectionArea.Width;
                PointF[] path1 = new PointF[width + 2];
                PointF[] path2 = new PointF[width + 2];
                int index = 0;

                double resampleRate = samplesYT.ViewedSamplesPerSecond;

                bool first = true;
                PointF point = default(PointF);
                PointF right1 = default(PointF);
                PointF right2 = default(PointF);

                double leftHigh = double.NegativeInfinity;
                double leftLow = double.PositiveInfinity;
                double rightHigh = double.NegativeInfinity;
                double rightLow = double.PositiveInfinity;

                double viewedLeftmostUnixTime = samplesYT.ViewedLeftmostUnixTime;
                double[]? unixTime = samplesYT.ViewedUnixTime;
                for (int loop = leftIndex; loop <= rightIndex; loop++)
                {
                    double value = samples[loop];
                    double time = unixTime == null ? viewedLeftmostUnixTime + loop / resampleRate : unixTime[loop];
                    PointF projection = Project(info, value, time);

                    float x = projection.X - info.ProjectionArea.Left;
                    if (x <= 0)
                    {
                        if (leftHigh == double.NegativeInfinity || projection.X > path1[0].X)
                        {
                            path1[0] = projection;
                            path2[0] = projection;
                            leftHigh = value;
                            leftLow = value;
                        }
                        else
                        {
                            path1[0].Y = Math.Max(path1[0].Y, projection.Y);
                            path2[0].Y = Math.Min(path2[0].Y, projection.Y);
                            leftHigh = Math.Max(leftHigh, value);
                            leftLow = Math.Min(leftLow, value);
                        }
                    }
                    else if (x >= width)
                    {
                        if (rightHigh == double.NegativeInfinity || projection.X < right1.X)
                        {
                            right1 = projection;
                            right2 = projection;
                            rightHigh = value;
                            rightLow = value;
                        }
                        else if (projection.X == right1.X)
                        {
                            right1.Y = Math.Max(right1.Y, projection.Y);
                            right2.Y = Math.Min(right2.Y, projection.Y);
                            rightHigh = Math.Max(rightHigh, value);
                            rightLow = Math.Min(rightLow, value);
                        }
                    }
                    else
                    {
                        AdjustLastTraceValue(info, value, projection);
                        if (path1[index].X == projection.X)
                        {
                            path1[index].Y = Math.Max(path1[index].Y, projection.Y);
                            path2[index].Y = Math.Min(path2[index].Y, projection.Y);
                        }
                        else if (!first && (projection.X - point.X) > 2)
                        {
                            float x1 = point.X + 1;
                            index++;
                            path1[index] = new PointF(x1, point.Y);
                            path2[index] = new PointF(x1, point.Y);

                            float x2 = projection.X - 1;
                            index++;
                            path1[index] = new PointF(x2, projection.Y);
                            path2[index] = new PointF(x2, projection.Y);
                        }
                        else
                        {
                            index++;
                            path1[index] = projection;
                            path2[index] = projection;
                        }
                    }
                    point = projection;
                    first = false;
                }
                if (rightHigh != double.PositiveInfinity)
                {
                    index++;
                    path1[index] = right1;
                    path2[index] = right2;
                    LastTraceHighestValue = Math.Max(rightHigh, LastTraceHighestValue);
                    LastTraceLowestValue = Math.Min(rightLow, LastTraceLowestValue);
                }
                index++;
                if (leftHigh != double.PositiveInfinity)
                {
                    LastTraceHighestValue = Math.Max(leftHigh, LastTraceHighestValue);
                    LastTraceLowestValue = Math.Min(leftLow, LastTraceLowestValue);
                }
                int sourceIndex = leftHigh == double.PositiveInfinity ? 1 : 0;
                int count = leftHigh == double.PositiveInfinity ? index : index - 1;

                DrawnProjection1 = new PointF[count];
                DrawnProjection2 = new PointF[count];
                Array.Copy(path1, sourceIndex, DrawnProjection1, 0, count);
                Array.Copy(path2, sourceIndex, DrawnProjection2, 0, count);
                DrawnYT = null;
                DrawnPolygon = ProjectPolygon();
            }

            PointF[]? projection1 = DrawnProjection1;
            PointF[]? projection2 = DrawnProjection2;
            PointF[]? polygon = DrawnPolygon;
            if (projection1 != null && projection2 != null)
            {
                float lineWidth = EffectiveLineWidth(info);
                if (lineWidth > 1f)
                {
                    (projection1, projection2) = WidenEnvelope(projection1, projection2, lineWidth);
                    polygon = ProjectPolygon(projection1, projection2);
                }
            }

            if (polygon != null)
            {
                using Brush brush = new SolidBrush(InterpolateColour(info.Skin.BackgroundColour, info.View0.Colour, 0, 1));
                graphics.FillPolygon(brush, polygon);
            }

            if (projection1 != null && projection2 != null)
            {
                using Pen pen = new Pen(info.View0.Colour, width: 1);
                PaintProjection(projection1, graphics, pen);
                PaintProjection(projection2, graphics, pen);
            }
        }

        public override void PaintHorizontalAxis(Graphics graphics, TraceGroupDisplay info)
        {
            if (info.YTTrace)
            {
                double leftUnixTime = info.LeftUnixTime;
                double rightUnixTime = info.RightUnixTime;
                PaintGutterBottomPartition(info, graphics, leftUnixTime, rightUnixTime);
            }
            else
            {
                base.PaintHorizontalAxis(graphics, info);
            }
        }

        private static void PaintEmbeddedTraceExtents(TraceGroupDisplay info, Graphics graphics)
        {
            string highText = string.Format(info.View0.Samples.VerticalUnit, info.View0.HighestValue.ToStringRound(5, 3));
            string lowText = string.Format(info.View0.Samples.VerticalUnit, info.View0.LowestValue.ToStringRound(5, 3));
            using Font font = info.Skin.AxisTextFont.Font;
            using Brush brush = info.Skin.AxisTextFont.Brush;
            SizeF highSize = graphics.MeasureString(highText, font);
            SizeF lowSize = graphics.MeasureString(lowText, font);

            graphics.DrawString(highText, font, brush, info.GroupArea.Right - highSize.Width - 5f, info.ProjectionArea.Top);
            graphics.DrawString(lowText, font, brush, info.GroupArea.Right - lowSize.Width - 5f, info.ProjectionArea.Bottom - lowSize.Height);
        }

        public void Project2dCurves(double[] samples, TraceView trace, RectangleF traceDrawArea, double highestValue, double lowestValue, double leftHValue = 0.0, double rightHValue = 0.0)
        {
            trace.Scope.OnLog?.Invoke(new Files.CsvLog.Entry($"Project2dCurves {trace.DecoratedName} logH={trace.LogHorizontal} left={leftHValue} right={rightHValue} len={samples.Length} fft={trace.IsFftTrace} rebased={trace.IsRebasedResult} width={traceDrawArea.Width}", Files.CsvLog.Priority.Debug));
            TraceView.PaintModes currentTraceMode = trace.PaintMode;
            float width = traceDrawArea.Width;

            PaintSamples = new double[0];
            PaintMode = TraceView.PaintModes.PolygonDigital;
            View = trace;
            PaintProjectionArea = traceDrawArea;
            PaintHighestValue = highestValue;
            PaintLowestValue = lowestValue;
            PaintLeftHValue = leftHValue;
            PaintRightHValue = rightHValue;
            PaintLowestY = samples.Length == 0 ? 0 : samples[0];
            PaintHighestY = samples.Length == 0 ? 0 : samples[0];

            DotDrawPoints = null;
            bool dots = currentTraceMode == TraceView.PaintModes.PointsIfChanged || currentTraceMode == TraceView.PaintModes.Points;
            bool interpolate = currentTraceMode == TraceView.PaintModes.PolygonContinuous || dots;
            if (samples.Length == 0 || PaintHighestValue == PaintLowestValue)
            {
                DrawnProjection1 = null;
                DrawnProjection2 = null;
            }
            else if (currentTraceMode == TraceView.PaintModes.Spectral)
            {
                // Audacity-style filled spectral display
                // a slightly smoothed max envelope along the top, filled down to the bottom of the window.
                PaintSamples = samples;
                PaintMode = (width < samples.Length) ? TraceView.PaintModes.Max : currentTraceMode;
                PointF[] envelope = (width < samples.Length) ? Projection2d() : Projection2dNormal();
                SmoothEnvelope(envelope, SpectralSmoothPixels);
                DrawnProjection1 = envelope;
                DrawnProjection2 = null;
                DrawnPolygon = SpectralPolygon(envelope);
            }
            else if (currentTraceMode == TraceView.PaintModes.PeakHold)
            {
                PaintSamples = ((trace.PeakHoldMinDrawn == null) ? samples : trace.PeakHoldMinDrawn);
                PaintMode = TraceView.PaintModes.Min;
                DrawnProjection1 = Projection2d();
                PaintSamples = ((trace.PeakHoldMaxDrawn == null) ? samples : trace.PeakHoldMaxDrawn);
                PaintMode = TraceView.PaintModes.Max;
                DrawnProjection2 = Projection2d();
            }
            else if ((currentTraceMode == TraceView.PaintModes.PolygonDigital || interpolate) && width < samples.Length)
            {
                PaintSamples = samples;
                PaintMode = TraceView.PaintModes.Min;
                DrawnProjection1 = Projection2d();
                PaintMode = TraceView.PaintModes.Max;
                DrawnProjection2 = Projection2d();
                DrawnPolygon = ProjectPolygon();
            }
            else
            {
                if (width > samples.Length)
                {
                    PaintSamples = samples;
                    PaintMode = currentTraceMode;
                    DrawnProjection1 = interpolate ? Projection2dInterpolate() : Projection2dNormal();
                    DrawnProjection2 = null;
                }
                else
                {
                    PaintSamples = samples;
                    PaintMode = currentTraceMode;
                    DrawnProjection1 = Projection2d();
                    DrawnProjection2 = null;
                }
            }

            if (samples.Length != 0)
            {
                LastTraceHighestValue = PaintHighestY;
                LastTraceLowestValue = PaintLowestY;
                if (dots)
                {
                    DotDrawPoints = ProjectionDots(View?.PaintMode == TraceView.PaintModes.PointsIfChanged);
                }
            }
        }

        private static readonly Color SpectralFillBottom = Color.FromArgb(0, 200, 0); // green at the window bottom
        private static readonly Color SpectralFillTop = Color.FromArgb(0, 60, 220); // blue at the window top
        private static readonly Color SpectralCapColour = Color.FromArgb(150, 225, 90, 90); // semi-transparent red cap line
        private const int SpectralSmoothPixels = 2; // half-width of the envelope smoothing window, in pixels

        // Box smoothing of the envelope's Y values (pixel space) while preserving max peaks.
        private static void SmoothEnvelope(PointF[] points, int halfWidth)
        {
            if (halfWidth <= 0 || points.Length < 3) return;
            float[] smoothed = new float[points.Length];
            for (int loop = 0; loop < points.Length; loop++)
            {
                int from = Math.Max(0, loop - halfWidth);
                int to = Math.Min(points.Length - 1, loop + halfWidth);
                float sum = 0f;
                for (int j = from; j <= to; j++) sum += points[j].Y;
                smoothed[loop] = sum / (to - from + 1);
            }
            for (int loop = 0; loop < points.Length; loop++)
            {
                points[loop] = new PointF(points[loop].X, smoothed[loop]);
            }
        }

        private PointF[]? SpectralPolygon(PointF[] envelope)
        {
            if (envelope.Length < 2) return null;
            float bottom = PaintProjectionArea.Bottom;
            var result = new List<PointF>(envelope.Length + 2);
            result.AddRange(envelope);
            result.Add(new PointF(envelope[envelope.Length - 1].X, bottom));
            result.Add(new PointF(envelope[0].X, bottom));
            return result.ToArray();
        }

        private List<PointF>? ProjectionDots(bool removeDuplicates)
        {
            if (PaintProjectionArea.Right < PaintProjectionArea.Left) return null;
            var list = new List<PointF>();
            int length = PaintSamples.Length;
            int left = (int)PaintProjectionArea.Left;
            int width = (int)PaintProjectionArea.Width;
            bool logH = UseLogH;

            int prevX = -1;
            double prevY = 0.0;

            for (int loop = 0; loop < length; loop++)
            {
                int x = logH
                    ? LogSampleIndexToXOffset(loop, length)
                    : (int)((long)loop * (long)width / length);
                double y = PaintSamples[loop];
                bool add = loop != 0 && (y != prevY || !removeDuplicates) && x != prevX;
                prevY = y;

                if (add && y >= PaintLowestValue && y <= PaintHighestValue)
                {
                    list.Add(new PointF(x + left, Project(y)));
                    prevX = x;
                }
            }
            return list;
        }

        private bool UseLogH => View?.IsLogX == true && PaintRightHValue > 0;

        // Pixel (0..width-1) -> fractional sample index via the inverse log-H mapping. The axis
        // spans the full data extent [effectiveLeft, right]; see PaintTraceBase.LogHEffectiveLeft.
        private double LogPixelToFracSampleIndex(int loop, int width, int length)
        {
            double range = PaintRightHValue - PaintLeftHValue;
            if (range <= 0) return 0.0;
            double effectiveLeft = LogHEffectiveLeft(PaintLeftHValue, PaintRightHValue, length);
            double val = LogHFractionToValue((double)loop / width, effectiveLeft, PaintRightHValue);
            return (val - PaintLeftHValue) / range * (length - 1);
        }

        // Pixel -> integer sample index, clamped.
        private int LogPixelToSampleIndex(int loop, int width, int length)
        {
            double frac = LogPixelToFracSampleIndex(loop, width, length);
            return Math.Max(0, Math.Min(length - 1, (int)frac));
        }

        // Sample index -> pixel-offset-from-left in log X mode.
        private int LogSampleIndexToXOffset(int sampleIndex, int length)
        {
            double range = PaintRightHValue - PaintLeftHValue;
            if (range <= 0) return 0;
            double val = PaintLeftHValue + (double)sampleIndex / Math.Max(1, length - 1) * range;
            double effectiveLeft = LogHEffectiveLeft(PaintLeftHValue, PaintRightHValue, length);
            return (int)(LogHValueToFraction(val, effectiveLeft, PaintRightHValue) * PaintProjectionArea.Width);
        }

        private PointF[] Projection2d()
        {
            return PaintMode switch
            {
                TraceView.PaintModes.Min => Projection2dMinMax(min: true),
                TraceView.PaintModes.Max => Projection2dMinMax(min: false),
                _ => Projection2dAverage(),
            };
        }

        private PointF[] Projection2dNormal()
        {
            if (PaintProjectionArea.Right < PaintProjectionArea.Left) return new PointF[0];

            int length = PaintSamples.Length;
            int left = (int)PaintProjectionArea.Left;
            int width = (int)PaintProjectionArea.Width;
            PointF[] array = new PointF[width];
            bool logH = UseLogH;

            for (int loop = 0; loop < width; loop++)
            {
                int index = logH
                    ? LogPixelToSampleIndex(loop, width, length)
                    : (int)((long)loop * (long)length / width);
                double y = PaintSamples[index];
                array[loop] = new PointF(left + loop, Project(y));
            }
            return array;
        }

        private PointF[] Projection2dMinMax(bool min)
        {
            if (PaintProjectionArea.Right < PaintProjectionArea.Left) return new PointF[0];

            int length = PaintSamples.Length;
            int left = (int)PaintProjectionArea.Left;
            int width = (int)PaintProjectionArea.Width;
            PointF[] array = new PointF[width];
            bool logH = UseLogH;

            for (int loop = 0; loop < width; loop++)
            {
                int startIndex = logH ? LogPixelToSampleIndex(loop, width, length) : (int)((long)loop * (long)length / width);
                int endIndex = logH ? LogPixelToSampleIndex(loop + 1, width, length) : (int)((long)(loop + 1) * (long)length / width);
                if (endIndex <= startIndex) endIndex = startIndex + 1;
                double y = PaintSamples[startIndex];
                for (int index = startIndex; index < endIndex && index < length; index++)
                {
                    double sample = PaintSamples[index];
                    y = min ? (sample < y ? sample : y) : (sample > y ? sample : y);
                }
                array[loop] = new PointF(left + loop, Project(y));
            }
            return array;
        }

        private PointF[] Projection2dInterpolate()
        {
            if (PaintProjectionArea.Right < PaintProjectionArea.Left) return new PointF[0];

            int length = PaintSamples.Length;
            int left = (int)PaintProjectionArea.Left;
            int width = (int)PaintProjectionArea.Width;
            PointF[] array = new PointF[width];
            bool logH = UseLogH;

            int prevIndex = -1;
            for (int loop = 0; loop < width; loop++)
            {
                double frac = logH
                    ? LogPixelToFracSampleIndex(loop, width, length)
                    : (double)loop * (double)length / (double)width;
                int indexLeft = (int)frac;
                double ratio = frac - indexLeft;
                int indexRight = indexLeft + 1;
                if (prevIndex != indexLeft)
                {
                    ratio = 0.0;
                    prevIndex = indexLeft;
                }
                double leftSample = PaintSamples[Math.Max(0, Math.Min(length - 1, indexLeft))];
                double rightSample = indexRight >= length ? PaintSamples[length - 1] : PaintSamples[indexRight];
                array[loop] = new PointF(left + loop, Project(leftSample * (1.0 - ratio) + rightSample * ratio));
            }

            return array;
        }

        private PointF[] Projection2dAverage()
        {
            if (PaintProjectionArea.Right < PaintProjectionArea.Left) return new PointF[0];

            int length = PaintSamples.Length;
            int left = (int)PaintProjectionArea.Left;
            int width = (int)PaintProjectionArea.Width;
            PointF[] array = new PointF[width];
            bool logH = UseLogH;

            for (int loop = 0; loop < width; loop++)
            {
                int startIndex = logH ? LogPixelToSampleIndex(loop, width, length) : (int)((long)loop * (long)length / width);
                int endIndex = logH ? LogPixelToSampleIndex(loop + 1, width, length) : (int)((long)(loop + 1) * (long)length / width);
                double sum = 0.0;
                if (endIndex <= startIndex)
                {
                    endIndex = startIndex + 1;
                }
                for (int index = startIndex; index < endIndex && index < length; index++)
                {
                    double y = PaintSamples[index];
                    if (y < PaintLowestY)
                    {
                        PaintLowestY = y;
                    }
                    if (y > PaintHighestY)
                    {
                        PaintHighestY = y;
                    }
                    sum += y;
                }
                array[loop] = new PointF(left + loop, Project(sum / (double)(endIndex - startIndex)));
            }

            return array;
        }

        public override void Dispose()
        {
            base.Dispose();
            DrawnPolygon = null;
            DrawnYT = null;
            DrawnProjection1 = null;
            DrawnProjection2 = null;
        }
    }

    [TestClass]
    public class LogHorizontalLeftEdgeTests
    {
        private struct Case
        {
            public double SamplesPerSecond;
            public int FftSize;     // N
            public int TargetBin;   // bin we steer the generated tone onto (bin-aligned, no leakage)
            public string Note;
        }

        // Drives the real Project2dCurves log-H path for one generated tone and reports whether the
        // peak made it into the drawn polygon, at which pixel, and what Hz the axis assigns there.
        private static (bool drawnHasPeak, int peakBin, int peakPixel, int expectedPixel, int bins, double peakHz, double axisHzAtPeak)
            RunCase(SehensControl scope, Case c)
        {
            double nyquist = c.SamplesPerSecond / 2.0;
            double peakHz = (double)c.TargetBin * c.SamplesPerSecond / c.FftSize; // bin-aligned

            // "use generator": a real tone -> real FFT magnitude, same as the scope's FFT trace.
            var tone = new ToneGenerator
            {
                SamplesPerSecond = c.SamplesPerSecond,
                FrequencyStart = peakHz,
                FrequencyEnd = peakHz,
                Amplitude = 1.0,
            };
            double[] signal = tone.Generate(c.FftSize);

            double[] spectrum;
            using (var fft = new Fftw(c.FftSize))
            {
                fft.ExecuteForward(signal);
                spectrum = fft.SpectralMagnitude; // length = bins, bin k -> k*sps/N Hz
            }
            int bins = spectrum.Length;

            int peakBin = 0;
            double peakValue = double.NegativeInfinity;
            for (int i = 0; i < bins; i++)
            {
                if (spectrum[i] > peakValue) { peakValue = spectrum[i]; peakBin = i; }
            }

            // Configure a real view exactly as an un-zoomed FFT trace: log horizontal, left H = 0.
            var data = new TraceData($"loghtest_{c.SamplesPerSecond}_{c.FftSize}_{c.TargetBin}");
            var view = new TraceView(scope, data, data.Name)
            {
                LogHorizontal = TraceView.LogHorizontalMode.Log,
                PaintMode = TraceView.PaintModes.PolygonDigital,
            };

            // width < bins forces the Min/Max bucketing path used for dense FFT traces.
            const int width = 100;
            const int height = 100;
            var area = new RectangleF(0f, 0f, width, height);
            Assert.IsTrue(width < bins, "test must exercise the down-sampling (min/max) projection path");

            var paint = new Paint2dTrace();
            paint.Project2dCurves(spectrum, view, area, highestValue: peakValue, lowestValue: 0.0,
                leftHValue: 0.0, rightHValue: nyquist);

            // The peak value projects to the top of the area (y == 0). DrawnProjection2 is the Max
            // envelope, one point per pixel with X == left + loop; the pixel holding the peak is the
            // one with minimum y. If the peak bin were dropped, the tallest thing drawn would be a
            // near-zero bin and the min y would sit near the bottom (height).
            var maxEnv = paint.DrawnProjection2 ?? paint.DrawnProjection1 ?? new PointF[0];
            int peakPixel = -1;
            float minY = float.PositiveInfinity;
            for (int i = 0; i < maxEnv.Length; i++)
            {
                if (maxEnv[i].Y < minY) { minY = maxEnv[i].Y; peakPixel = i; }
            }
            double drawnMaxValue = peakValue * (1.0 - minY / height);
            bool drawnHasPeak = drawnMaxValue >= 0.5 * peakValue;

            // Expected pixel from a closed-form log mapping computed by hand, independent of the
            // LogHValueToFraction/LogHFractionToValue maps under test. Uses the same left-edge rule
            // (first bin, capped to the decade limit) so it stays correct whether or not the cap
            // engages: frac = log10(peakHz / effLeft) / log10(right / effLeft).
            double effLeft = Math.Max(nyquist / (bins - 1),
                nyquist * Math.Pow(10.0, -PaintTraceBase.LogHorizontalMaxDecades));
            int expectedPixel = peakHz <= effLeft ? 0
                : (int)((Math.Log10(peakHz) - Math.Log10(effLeft)) / (Math.Log10(nyquist) - Math.Log10(effLeft)) * width);

            // The Hz the axis (gutter labels + hover readout, same shared helper) assigns to the
            // peak's pixel.
            double axisHzAtPeak = peakPixel < 0 ? 0.0
                : PaintTraceBase.LogHFractionToValue((peakPixel + 0.5) / width, effLeft, nyquist);

            data.Close();
            return (drawnHasPeak, peakBin, peakPixel, expectedPixel, bins, peakHz, axisHzAtPeak);
        }

        [TestMethod]
        public void LowPeakStaysVisibleOnLogHorizontalAxis()
        {
            var cases = new List<Case>
            {
                // The exact field report: 100 Hz @ 24 kHz. Used to drop at large N; must now show.
                new Case { SamplesPerSecond = 24000, FftSize = 512,  TargetBin = 2,  Note = "~100Hz@24k N=512" },
                new Case { SamplesPerSecond = 24000, FftSize = 8192, TargetBin = 34, Note = "~100Hz@24k N=8192 (was DROPPED)" },

                // Old floor-bin straddles at several FFT sizes - all must show now.
                new Case { SamplesPerSecond = 24000, FftSize = 1024, TargetBin = 4,  Note = "N=1024 (old floor 5)" },
                new Case { SamplesPerSecond = 24000, FftSize = 1024, TargetBin = 5,  Note = "N=1024" },
                new Case { SamplesPerSecond = 24000, FftSize = 2048, TargetBin = 9,  Note = "N=2048 (old floor 10)" },
                new Case { SamplesPerSecond = 24000, FftSize = 2048, TargetBin = 10, Note = "N=2048" },
                new Case { SamplesPerSecond = 24000, FftSize = 4096, TargetBin = 19, Note = "N=4096 (old floor 20)" },
                new Case { SamplesPerSecond = 24000, FftSize = 4096, TargetBin = 20, Note = "N=4096" },
                new Case { SamplesPerSecond = 24000, FftSize = 8192, TargetBin = 39, Note = "N=8192 (old floor 40)" },
                new Case { SamplesPerSecond = 24000, FftSize = 8192, TargetBin = 40, Note = "N=8192" },

                // Different sample rates, same N.
                new Case { SamplesPerSecond = 8000,  FftSize = 4096, TargetBin = 19, Note = "8k  N=4096" },
                new Case { SamplesPerSecond = 8000,  FftSize = 4096, TargetBin = 25, Note = "8k  N=4096" },
                new Case { SamplesPerSecond = 44100, FftSize = 4096, TargetBin = 19, Note = "44k1 N=4096" },
                new Case { SamplesPerSecond = 44100, FftSize = 4096, TargetBin = 21, Note = "44k1 N=4096" },
                new Case { SamplesPerSecond = 48000, FftSize = 2048, TargetBin = 3,  Note = "48k N=2048" },
                new Case { SamplesPerSecond = 48000, FftSize = 2048, TargetBin = 200,Note = "48k N=2048 (mid band)" },

                // Smallest representable bins.
                new Case { SamplesPerSecond = 24000, FftSize = 256,  TargetBin = 1,  Note = "N=256 bin 1" },
                new Case { SamplesPerSecond = 24000, FftSize = 512,  TargetBin = 1,  Note = "N=512 bin 1 (was DROPPED)" },

                // High peaks.
                new Case { SamplesPerSecond = 24000, FftSize = 8192, TargetBin = 4000, Note = "near nyquist" },
                new Case { SamplesPerSecond = 16000, FftSize = 1024, TargetBin = 400,  Note = "mid band" },
            };

            var scope = new SehensControl();
            var report = new System.Text.StringBuilder();
            report.AppendLine();
            report.AppendLine($"  sps     N     peakBin  bins   peakHz   drawn  pixel  expect  axisHz  note");

            int missing = 0, mispositioned = 0, wrongHz = 0;
            foreach (var c in cases)
            {
                var r = RunCase(scope, c);
                if (!r.drawnHasPeak) missing++;
                if (r.drawnHasPeak && Math.Abs(r.peakPixel - r.expectedPixel) > 2) mispositioned++;
                // axis frequency under the peak should match the peak's frequency (within ~1 pixel,
                // which on this log axis is a small ratio).
                if (r.drawnHasPeak && (r.axisHzAtPeak / r.peakHz > 1.25 || r.peakHz / r.axisHzAtPeak > 1.25)) wrongHz++;
                report.AppendLine(
                    $"  {c.SamplesPerSecond,-6} {c.FftSize,-5} {r.peakBin,6} {r.bins,6} {r.peakHz,8:0.0}   " +
                    $"{(r.drawnHasPeak ? "yes" : "NO "),-5} {r.peakPixel,5} {r.expectedPixel,6} {r.axisHzAtPeak,7:0.0}  {c.Note}");
            }

            System.Diagnostics.Trace.WriteLine(report.ToString());

            // Every generated peak (bin >= 1) is on screen now - nothing dropped off the left.
            Assert.AreEqual(0, missing, "peaks dropped off the left edge:" + report);
            // ...each lands at the expected log-axis pixel (forward/inverse maps agree)...
            Assert.AreEqual(0, mispositioned, "peak drawn at the wrong log-axis pixel:" + report);
            // ...and the frequency the axis prints under the peak matches the peak's frequency.
            Assert.AreEqual(0, wrongHz, "axis frequency under the peak disagrees with the peak frequency:" + report);
        }

        [TestMethod]
        public void FieldCase100HzAt24kHzN8192IsVisible()
        {
            // Explicit regression guard for the reported case, kept separate from the table.
            var scope = new SehensControl();
            var r = RunCase(scope, new Case { SamplesPerSecond = 24000, FftSize = 8192, TargetBin = 34, Note = "field" });
            Assert.IsTrue(r.drawnHasPeak, $"100 Hz @ 24 kHz (N=8192) must be visible; peakBin={r.peakBin}, bins={r.bins}");
            Assert.IsTrue(Math.Abs(r.peakPixel - r.expectedPixel) <= 2,
                $"100 Hz @ 24 kHz drawn at pixel {r.peakPixel}, expected ~{r.expectedPixel}");
            // The axis must print ~the peak's frequency under the peak (not a different number).
            Assert.IsTrue(r.axisHzAtPeak / r.peakHz <= 1.25 && r.peakHz / r.axisHzAtPeak <= 1.25,
                $"axis shows {r.axisHzAtPeak:0.0} Hz under a {r.peakHz:0.0} Hz peak");
        }

        [TestMethod]
        public void LogHorizontalCapsTheVisibleSpan()
        {
            // A wide spectrum (here ~4.5 decades full extent) is capped to LogHorizontalMaxDecades
            // down from the right edge so the useful band isn't squashed; content further left is
            // off-screen, exactly like a log axis that starts at its left edge.
            const double sps = 20000.0;   // right edge = nyquist = 10 kHz
            const int n = 65536;
            double nyquist = sps / 2.0;
            double binWidth = sps / n;
            int bins = Fftw.SampleCountToBinCount(n);

            double effLeft = PaintTraceBase.LogHEffectiveLeft(0.0, nyquist, bins);
            double expectedCap = nyquist * Math.Pow(10.0, -PaintTraceBase.LogHorizontalMaxDecades);
            Assert.IsTrue(Math.Abs(effLeft - expectedCap) < 1e-6,
                $"left edge {effLeft:0.###} Hz should be the {PaintTraceBase.LogHorizontalMaxDecades}-decade cap {expectedCap:0.###} Hz, not the {nyquist / (bins - 1):0.###} Hz first bin");

            var scope = new SehensControl();
            // A peak comfortably inside the window (100 Hz) is shown; one below the capped left
            // edge (a third of the edge frequency) is off-screen.
            int binInside = (int)Math.Round(100.0 / binWidth);
            int binBelow = Math.Max(1, (int)(effLeft / binWidth / 3));
            var inside = RunCase(scope, new Case { SamplesPerSecond = sps, FftSize = n, TargetBin = binInside, Note = "inside window" });
            var below = RunCase(scope, new Case { SamplesPerSecond = sps, FftSize = n, TargetBin = binBelow, Note = "below left edge" });

            Assert.IsTrue(inside.drawnHasPeak,
                $"peak at {inside.peakHz:0.0} Hz (inside the {PaintTraceBase.LogHorizontalMaxDecades}-decade window) should be drawn");
            Assert.IsFalse(below.drawnHasPeak,
                $"peak at {below.peakHz:0.00} Hz (below the {effLeft:0.##} Hz left edge) should be off-screen");
        }
    }

    // Projection-cache and stretch-equivalence tests for the full paint path (real Graphics on a
    // bitmap; assertions on the public DrawnProjection arrays, same introspection surface as
    // LogHorizontalLeftEdgeTests).
    [TestClass]
    public class Paint2dProjectionTests
    {
        [TestMethod]
        public void StretchProjectionMatchesLegacyIndexMath()
        {
            // A lone plain trace (Stretch) must project exactly as the old index math did - one column per pixel,
            // X == ProjectionArea.Left + column, column i covering samples [i*len/W, (i+1)*len/W).
            var scope = new SehensControl();
            const int count = 20000; // > 10x pane width -> the min/max decimation path
            scope["ramp"].Update(SehensTestHarness.Ramp(count));
            SehensTestHarness.Layout(scope);
            TraceView view = SehensTestHarness.View(scope, "ramp");
            view.PaintMode = TraceView.PaintModes.PolygonDigital;
            view.SetHighLow(count, 0);
            SehensTestHarness.Layout(scope);

            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(view);
            Assert.AreEqual(HorizontalMode.Stretch, info.HMode);
            Assert.AreEqual(info.ProjectionArea, info.ValueRect);

            using var bmp = new Bitmap(SehensTestHarness.Width, SehensTestHarness.Height);
            using var graphics = Graphics.FromImage(bmp);
            view.Painter.PaintProjection(graphics, info);

            var painter = (Paint2dTrace)view.Painter;
            PointF[] minEnv = painter.DrawnProjection1 ?? throw new AssertFailedException("no min envelope");
            PointF[] maxEnv = painter.DrawnProjection2 ?? throw new AssertFailedException("no max envelope");
            int width = info.ProjectionArea.Width;
            Assert.AreEqual(width, minEnv.Length, "one column per pixel");
            Assert.AreEqual(width, maxEnv.Length);

            double binSize = (double)count / width;
            double height = info.ProjectionArea.Height;
            double DecodeValue(float y) => count * (1.0 - (y - info.ProjectionArea.Top) / height);
            for (int loop = 0; loop < width; loop++)
            {
                Assert.AreEqual(info.ProjectionArea.Left + loop, minEnv[loop].X, 0.01, "legacy X grid");
                // ramp: column min ~= first index in the bucket, column max ~= last
                Assert.AreEqual(loop * binSize, DecodeValue(minEnv[loop].Y), binSize * 2 + 2,
                    $"column {loop} min envelope value");
                Assert.AreEqual(Math.Min((loop + 1) * binSize, count - 1), DecodeValue(maxEnv[loop].Y), binSize * 2 + 2,
                    $"column {loop} max envelope value");
            }
        }

        [TestMethod]
        public void ValueRectChangeForcesReprojection()
        {
            // The projection cache is keyed on data + log-H values, not the target rect; the rect
            // comparison in PaintProjectionY is what forces a reproject when the sub-window moves
            // (sibling added/hidden/rescaled). Remove that guard and this test fails with a
            // stale half-width projection.
            var scope = new SehensControl();
            SehensTestHarness.AffineTrace(scope, "A", count: 8000, offset: 0, multiplier: 1, unit: "u");    // 0..8000
            TraceView b = SehensTestHarness.AffineTrace(scope, "B", count: 4000, offset: 4000, multiplier: 1, unit: "u"); // right half
            scope.GroupViews(new[] { "A", "B" });
            SehensTestHarness.Layout(scope);
            b.PaintMode = TraceView.PaintModes.PolygonDigital;
            b.SetHighLow(4000, 0);
            SehensTestHarness.Layout(scope);

            TraceGroupDisplay infoHalf = scope.PaintBox.TraceToGroupDisplayInfo(b);
            Assert.AreEqual(HorizontalMode.ValueAlign, infoHalf.HMode);
            Assert.IsTrue(infoHalf.ValueRect.Width < infoHalf.ProjectionArea.Width * 0.6, "B should occupy ~half the pane");

            using var bmp = new Bitmap(SehensTestHarness.Width, SehensTestHarness.Height);
            using var graphics = Graphics.FromImage(bmp);
            var painter = (Paint2dTrace)b.Painter;

            b.Painter.PaintProjection(graphics, infoHalf);
            PointF[] half = painter.DrawnProjection1 ?? throw new AssertFailedException("no projection");
            Assert.AreEqual(infoHalf.ValueRect.Width, half.Length, "projection fills the sub-window");
            Assert.AreEqual(infoHalf.ValueRect.Left, half[0].X, 0.01);

            // unchanged rect -> the cache holds (no rebuild)
            b.Painter.PaintProjection(graphics, infoHalf);
            Assert.AreSame(half, painter.DrawnProjection1, "same rect must not reproject");

            // widen the sub-window (what a hidden/removed sibling does) -> must reproject at once
            var infoFull = (TraceGroupDisplay)infoHalf.Clone();
            infoFull.ValueRect = infoFull.ProjectionArea;
            b.Painter.PaintProjection(graphics, infoFull);
            PointF[] full = painter.DrawnProjection1 ?? throw new AssertFailedException("no projection");
            Assert.AreEqual(infoFull.ProjectionArea.Width, full.Length, "projection must re-span the new rect");
            Assert.AreEqual(infoFull.ProjectionArea.Left, full[0].X, 0.01);
        }

        [TestMethod]
        public void FakeYtGroupPaintsPastTheDataEnd()
        {
            var scope = new SehensControl();
            scope["ytA"].Update(SehensTestHarness.Ramp(1000), 100.0); // 0..10 s
            scope["ytA"].InputLeftmostUnixTime = 1_700_000_000;
            scope["ytB"].Update(SehensTestHarness.Ramp(600), 100.0);  // +5..+11 s
            scope["ytB"].InputLeftmostUnixTime = 1_700_000_005;
            scope.GroupViews(new[] { "ytA", "ytB" });
            TraceView a = SehensTestHarness.View(scope, "ytA");
            TraceView b = SehensTestHarness.View(scope, "ytB");
            a.PaintMode = TraceView.PaintModes.PolygonDigital;
            b.PaintMode = TraceView.PaintModes.PolygonDigital;
            a.SetHighLow(1000, 0);
            b.SetHighLow(600, 0);
            SehensTestHarness.Layout(scope);

            using var bmp = new Bitmap(SehensTestHarness.Width, SehensTestHarness.Height);
            using var graphics = Graphics.FromImage(bmp);
            TraceGroupDisplay infoA = scope.PaintBox.TraceToGroupDisplayInfo(a);
            Assert.IsTrue(infoA.YTTrace, "pair must classify as YT");
            a.Painter.PaintProjection(graphics, infoA); // threw IndexOutOfRange before the clamp fix
            TraceGroupDisplay infoB = scope.PaintBox.TraceToGroupDisplayInfo(b);
            b.Painter.PaintProjection(graphics, infoB);

            PointF[] drawnA = ((Paint2dTrace)a.Painter).DrawnYT ?? throw new AssertFailedException("A drew nothing");
            Assert.IsTrue(drawnA.Length > 0);
            PointF[] drawnB = ((Paint2dTrace)b.Painter).DrawnYT ?? throw new AssertFailedException("B drew nothing");
            Assert.IsTrue(drawnB.Length > 0);
        }

        [TestMethod]
        public void FakeYtPadsExtendToTheWindowEdges()
        {
            var scope = new SehensControl();
            scope["padA"].Update(SehensTestHarness.Ramp(1000), 100.0); // 0..10 s of the 0..11 s window
            scope["padA"].InputLeftmostUnixTime = 1_700_000_000;
            scope["padB"].Update(SehensTestHarness.Ramp(600), 100.0);  // 5..11 s
            scope["padB"].InputLeftmostUnixTime = 1_700_000_005;
            scope.GroupViews(new[] { "padA", "padB" });
            TraceView a = SehensTestHarness.View(scope, "padA");
            TraceView b = SehensTestHarness.View(scope, "padB");
            a.PaintMode = TraceView.PaintModes.PolygonDigital;
            b.PaintMode = TraceView.PaintModes.PolygonDigital;
            a.SetHighLow(1000, 0);
            b.SetHighLow(600, 0);
            a.PadRightWithLastValue = true; // A's data ends 1 s before the group window's right edge
            b.PadLeftWithFirstValue = true; // B's data starts 5 s after the window's left edge
            SehensTestHarness.Layout(scope);

            using var bmp = new Bitmap(SehensTestHarness.Width, SehensTestHarness.Height);
            using var graphics = Graphics.FromImage(bmp);
            TraceGroupDisplay infoA = scope.PaintBox.TraceToGroupDisplayInfo(a);
            a.Painter.PaintProjection(graphics, infoA);
            PointF[] drawnA = ((Paint2dTrace)a.Painter).DrawnYT ?? throw new AssertFailedException("A drew nothing");
            Assert.IsTrue(drawnA[drawnA.Length - 1].X >= infoA.ProjectionArea.Right - 2,
                $"pad-right must reach the window edge (last X {drawnA[drawnA.Length - 1].X}, right {infoA.ProjectionArea.Right})");

            TraceGroupDisplay infoB = scope.PaintBox.TraceToGroupDisplayInfo(b);
            b.Painter.PaintProjection(graphics, infoB);
            PointF[] drawnB = ((Paint2dTrace)b.Painter).DrawnYT ?? throw new AssertFailedException("B drew nothing");
            Assert.IsTrue(drawnB[0].X <= infoB.ProjectionArea.Left + 2,
                $"pad-left must reach the window edge (first X {drawnB[0].X}, left {infoB.ProjectionArea.Left})");
            // and without the pad flag the other edge stays where the data ends (mid-pane)
            Assert.IsTrue(drawnA[0].X <= infoA.ProjectionArea.Left + 2, "A starts at the window left anyway");
            Assert.IsTrue(drawnB[drawnB.Length - 1].X >= infoB.ProjectionArea.Right - 2, "B ends at the window right anyway");
        }

        [TestMethod]
        public void LogAffineSpikeLandsAtTheLogAxisPixel()
        {
            // A lone affine trace with LogHorizontal=Log, through the FULL pipeline (group
            // machinery, ValueRect, PaintProjection): a spike at a known axis value must land at
            // the closed-form log pixel and the axis inverse must read the same value back.
            var scope = new SehensControl();
            const int count = 8000;
            const double mult = 1.5; // domain 0..12000 "Hz"
            double[] samples = new double[count];
            const int spikeIndex = 800; // 1200 Hz
            samples[spikeIndex] = 100.0;
            scope["logaffine"].Update(samples);
            scope["logaffine"].SetHorizontalAffine(0.0, mult, "Hz");
            TraceView view = SehensTestHarness.View(scope, "logaffine");
            view.LogHorizontal = TraceView.LogHorizontalMode.Log;
            view.PaintMode = TraceView.PaintModes.PolygonDigital;
            view.SetHighLow(100.0, 0.0);
            SehensTestHarness.Layout(scope);

            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(view);
            Assert.AreEqual(0.0, info.LeftSampleNumberValue, 1e-9);
            Assert.AreEqual(count * mult, info.RightSampleNumberValue, 1e-9);

            using var bmp = new Bitmap(SehensTestHarness.Width, SehensTestHarness.Height);
            using var graphics = Graphics.FromImage(bmp);
            view.Painter.PaintProjection(graphics, info);

            var painter = (Paint2dTrace)view.Painter;
            PointF[] maxEnv = painter.DrawnProjection2 ?? throw new AssertFailedException("no max envelope");
            int width = info.ProjectionArea.Width;
            Assert.AreEqual(width, maxEnv.Length);
            int peakPixel = -1;
            float minY = float.PositiveInfinity;
            for (int loop = 0; loop < maxEnv.Length; loop++)
            {
                if (maxEnv[loop].Y < minY) { minY = maxEnv[loop].Y; peakPixel = loop; }
            }
            double drawnValue = 100.0 * (1.0 - (minY - info.ProjectionArea.Top) / info.ProjectionArea.Height);
            Assert.IsTrue(drawnValue >= 50.0, $"spike must be drawn (drawn value {drawnValue:0.0})");

            // closed-form expected pixel, independent of the maps under test (the painter models
            // the sample value as left + i/(len-1)*range)
            double right = count * mult;
            double spikeHz = (double)spikeIndex / (count - 1) * right;
            double effLeft = PaintTraceBase.LogHEffectiveLeft(0.0, right, count);
            int expectedPixel = (int)(PaintTraceBase.LogHValueToFraction(spikeHz, effLeft, right) * width);
            Assert.IsTrue(Math.Abs(peakPixel - expectedPixel) <= 2,
                $"spike at {spikeHz:0.0} Hz drawn at pixel {peakPixel}, expected ~{expectedPixel}");

            // the value the gutter/hover assigns to that pixel matches the spike's value
            double axisHz = PaintTraceBase.LogHFractionToValue((peakPixel + 0.5) / width, effLeft, right);
            Assert.IsTrue(axisHz / spikeHz <= 1.1 && spikeHz / axisHz <= 1.1,
                $"axis shows {axisHz:0.0} Hz under a {spikeHz:0.0} Hz spike");
        }
    }
}
