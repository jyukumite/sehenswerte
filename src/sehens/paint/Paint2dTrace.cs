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

        public TraceView View;
        public double[] PaintSamples = new double[0];
        public TraceView.PaintModes PaintMode;
        public RectangleF PaintProjectionArea;
        public double PaintHighestValue;
        public double PaintLowestValue;
        public double PaintHighestY;
        public double PaintLowestY;

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
            if (View.LogVertical)
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

        private PointF[]? ProjectPolygon()
        {
            if (DrawnProjection1 == null || DrawnProjection2 == null) return null;

            int length1 = DrawnProjection1!.Length;
            int length2 = DrawnProjection2!.Length;
            var result = new List<PointF>();

            //fixme: split polygons for embedded NANs
            for (int loop = 0; loop < length1; loop++)
            {
                if (float.IsFinite(DrawnProjection1[loop].Y))
                {
                    result.Add(DrawnProjection1[loop]);
                }
            }
            for (int loop = length2 - 1; loop >= 0; loop--)
            {
                if (float.IsFinite(DrawnProjection2[loop].Y))
                {
                    result.Add(DrawnProjection2[loop]);
                }
            }
            return result.ToArray();
        }

        private void ProjectYT(TraceGroupDisplay info, int leftIndex, int rightIndex)
        {
            if (DrawnYT != null) return;

            TraceData samplesYT = info.View0.Samples;
            double[] samples = samplesYT.ViewedSamplesAsDouble;
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
                if (line.Length > 0)
                {
                    graphics.DrawLines(pen, line.ToArray());
                }
            }
        }

        private void PaintProjectionY(TraceGroupDisplay info, Graphics graphics)
        {
            double[] samples = SnapshotProjection(info.View0);
            if (SnapshotReprojectionRequired)
            {
                LastTraceHighestValue = double.PositiveInfinity;
                LastTraceLowestValue = double.NegativeInfinity;
                SnapshotReprojectionRequired = false;
                DrawnPolygon = null;
                Project2dCurves(samples, info.View0, info.ProjectionArea, info.View0.HighestValue, info.View0.LowestValue);
            }

            bool dots = info.View0.PaintMode == TraceView.PaintModes.Points || info.View0.PaintMode == TraceView.PaintModes.PointsIfChanged;
            if (DrawnPolygon != null && !dots && DrawnPolygon.Count() > 0)
            {
                using Brush brush = new SolidBrush(InterpolateColour(info.Skin.BackgroundColour, info.View0.Colour, 0, 1));
                graphics.FillPolygon(brush, DrawnPolygon);
            }

            Color color = dots ? InterpolateColour(info.View0.Colour, info.Skin.BackgroundColour, 0, 1) : info.View0.Colour;
            if (dots && DotDrawPoints != null)
            {
                using Pen pen = LinePen(color, info);
                using Brush brush = new SolidBrush(info.View0.Colour);
                PaintTraceBase.PaintFilledCircles(graphics, brush, DotDrawPoints!.ToArray(), 4f);
            }
            else if (DrawnProjection1 != null && DrawnProjection2 != null)
            {
                using Pen pen = new Pen(color, width: 1);
                PaintProjection(DrawnProjection1, graphics, pen);
                PaintProjection(DrawnProjection2, graphics, pen);
            }
            else if (DrawnProjection1 != null)
            {
                using Pen pen = LinePen(color, info);
                PaintProjection(DrawnProjection1, graphics, pen);
            }
            PaintPiP(info, graphics);
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

            if (DrawnPolygon != null)
            {
                using Brush brush = new SolidBrush(InterpolateColour(info.Skin.BackgroundColour, info.View0.Colour, 0, 1));
                graphics.FillPolygon(brush, DrawnPolygon);
            }

            if (DrawnProjection1 != null && DrawnProjection2 != null)
            {
                using Pen pen = new Pen(info.View0.Colour, width: 1);
                PaintProjection(DrawnProjection1, graphics, pen);
                PaintProjection(DrawnProjection2, graphics, pen);
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

        public void Project2dCurves(double[] samples, TraceView trace, RectangleF traceDrawArea, double highestValue, double lowestValue)
        {
            trace.Scope.OnLog?.Invoke(new Files.CsvLog.Entry($"Project2dCurves {trace.DecoratedName}", Files.CsvLog.Priority.Info));
            TraceView.PaintModes currentTraceMode = trace.PaintMode;
            float width = traceDrawArea.Width;

            PaintSamples = new double[0];
            PaintMode = TraceView.PaintModes.PolygonDigital;
            View = trace;
            PaintProjectionArea = traceDrawArea;
            PaintHighestValue = highestValue;
            PaintLowestValue = lowestValue;
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
                    DotDrawPoints = ProjectionDots(View.PaintMode == TraceView.PaintModes.PointsIfChanged);
                }
            }
        }

        private List<PointF>? ProjectionDots(bool removeDuplicates)
        {
            if (PaintProjectionArea.Right < PaintProjectionArea.Left) return null;
            var list = new List<PointF>();
            int length = PaintSamples.Length;
            int left = (int)PaintProjectionArea.Left;
            int width = (int)PaintProjectionArea.Width;

            int prevX = -1;
            double prevY = 0.0;

            for (int loop = 0; loop < length; loop++)
            {
                int x = (int)((long)loop * (long)width / length);
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

            for (int loop = 0; loop < width; loop++)
            {
                int index = (int)((long)loop * (long)length / width);
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

            int endIndex;
            for (int loop = 0; loop < width; loop++)
            {
                int startIndex = (int)((long)loop * (long)length / width);
                endIndex = (int)((long)(loop + 1) * (long)length / width);
                double y = PaintSamples[startIndex];
                for (int index = startIndex; index < endIndex; index++)
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

            int prevIndex = -1;
            for (int loop = 0; loop < width; loop++)
            {
                double ratio = (double)loop * (double)length / (double)width;
                int indexLeft = (int)ratio;
                ratio -= (double)indexLeft;
                int indexRight = indexLeft + 1;
                if (prevIndex != indexLeft)
                {
                    ratio = 0.0;
                    prevIndex = indexLeft;
                }
                double leftSample = PaintSamples[indexLeft];
                double rightSample = ((indexRight >= PaintSamples.Length) ? PaintSamples[indexLeft] : PaintSamples[indexRight]);

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

            for (int loop = 0; loop < width; loop++)
            {
                int startIndex = (int)((long)loop * (long)length / width);
                int endIndex = (int)((long)(loop + 1) * (long)length / width);
                double sum = 0.0;
                if (endIndex == startIndex)
                {
                    endIndex++;
                }
                for (int index = startIndex; index < endIndex; index++)
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
}
