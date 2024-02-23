using SehensWerte.Maths;
using System.Drawing.Drawing2D;

namespace SehensWerte.Controls.Sehens
{
    public partial class PaintTraceBase
    {
        private IPaintTrace? m_PictureInPicture;
        protected bool SnapshotReprojectionRequired = true;

        ////////////////////////////////////////////
        // helpers

        public virtual PaintBoxMouseInfo.GuiSection GetGuiSections(TraceView.PaintedInfo painted, MouseEventArgs e)
        {
            PaintBoxMouseInfo.GuiSection hotPoint = PaintBoxMouseInfo.GuiSection.None;
            foreach (TraceViewClickZone item in painted.ClickZones)
            {
                if (item.Rect.Contains(new Point(e.X, e.Y)))
                {
                    hotPoint |= item.GuiSection;
                }
            }
            return hotPoint;
        }

        private float SampleToRatio(TraceGroupDisplay info, float sampleNumber)
        {
            if (info.YTTrace)
            {
                return (float)((info.View0.Samples.UnixTimeAtSample((int)sampleNumber) - info.LeftUnixTime) / (info.RightUnixTime - info.LeftUnixTime));
            }
            else
            {
                float left = info.LeftSampleNumber + info.ViewOffsetOverride;
                float right = info.RightSampleNumber + info.ViewOffsetOverride;
                return (sampleNumber - left) / (right - left);
            }
        }

        private float SampleToRatioYT(TraceGroupDisplay info, double unixTime)
        {
            if (info.YTTrace)
            {
                return (float)((unixTime - info.LeftUnixTime) / (info.RightUnixTime - info.LeftUnixTime));
            }
            else
            {
                return 0; //fixme?
            }
        }

        public static IEnumerable<double> GetPartitions(double low, double high, int count)
        {
            bool reversed = false;
            List<double> list = new List<double>();
            if (count > 0 && !double.IsNaN(low) && !double.IsInfinity(low) && !double.IsNaN(high) && !double.IsInfinity(high))
            {
                if (high < low)
                {
                    reversed = true;
                    double temp = low;
                    low = high;
                    high = temp;
                }
                double skip = (high - low) / (double)count;
                skip = skip.RoundSignificantUp(1, skip);
                double start = Math.Ceiling(low / skip) * skip;
                list = new List<double>();
                for (int loop = 0; loop < count; loop++)
                {
                    double partition = start + (double)loop * skip;
                    if (partition <= high)
                    {
                        list.Add(partition);
                    }
                }
            }
            if (reversed)
            {
                list.Reverse();
            }
            return list;
        }

        public static void ProjectLog(double maxInput, double input, out double newMax, out double output, int maxStaves = 2, int logBase = 10)
        {
            double pow = Math.Pow(logBase, maxStaves);
            input = input * pow / maxInput;
            output = ((input < 1.0) ? 0.0 : Math.Log(input, logBase).FixToRange(0.0, maxStaves));
            newMax = maxStaves;
        }

        public double[] SnapshotProjection(TraceView trace)
        {
            (var result, var reprocessCurve) = trace.SnapshotProjection();
            if (reprocessCurve)
            {
                ReprojectionRequired();
            }
            return result;
        }

        protected (int leftIndex, int rightIndex, double[] samples, double[] times) SnapshotYTProjection(TraceGroupDisplay info, double overscan)
        {
            bool reprocessCurve;
            var result = info.View0.SnapshotYTProjection(info.LeftUnixTime - overscan, info.RightUnixTime + overscan, out reprocessCurve);
            if (reprocessCurve)
            {
                ReprojectionRequired();
            }
            return result;
        }

        public virtual void CalculateTraceRange(TraceGroupDisplay divisionInfo)
        {
            if (double.IsFinite(divisionInfo.View0.DrawnValueLowest)) return;

            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            double[] samples;
            if (divisionInfo.View0.CanShowRealYT)
            {
                (_, _, samples, _) = SnapshotYTProjection(divisionInfo, 0.0);
            }
            else
            {
                samples = SnapshotProjection(divisionInfo.View0);
            }
            foreach (double sample in samples)
            {
                if (double.IsFinite(sample))
                {
                    min = Math.Min(min, sample);
                    max = Math.Max(max, sample);
                }
            }
            divisionInfo.View0.DrawnValueHighest = max;
            divisionInfo.View0.DrawnValueLowest = min;
        }

        protected static Pen LinePen(Color col, TraceGroupDisplay info)
        {
            Pen pen;
            if (info.Scope.HighQualityRender)
            {
                float width = info.DrawPictureInPicture
                    ? 1f
                    : info.View0.LineWidth == 0 ? info.Skin.TraceLineWidth : info.View0.LineWidth;
                pen = new Pen(col, width);
                if (info.View0.LineWidth != 1.0f && info.Scope.HighQualityRender)
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    pen.MiterLimit = 1f;
                }
            }
            else
            {
                pen = new Pen(col, width: info.Skin.TraceLineWidth);
            }
            return pen;
        }

        ////////////////////////////////////////////
        // setters

        public void ReprojectionRequired()
        {
            SnapshotReprojectionRequired = true;
            m_PictureInPicture?.ReprojectionRequired();
        }

        ////////////////////////////////////////////
        // expected overrides

        public virtual void PaintProjection(Graphics graphics, TraceGroupDisplay info)
        {
        }

        protected virtual string ToHorizontalUnit(TraceGroupDisplay info, double xValue)
        {
            return info.ShowHorizontalUnits ? xValue.ToStringRound(5, 3, info.HorizontalUnit) : xValue.ToStringRound(5, 3);
        }

        public virtual string VerticalAxisFormat(TraceGroupDisplay info, double yValue)
        {
            return string.Format(info.View0.VerticalUnitFormat, yValue.ToStringRound(5, 3));
        }

        protected virtual IEnumerable<double> VerticalAxisPartition(TraceGroupDisplay info, int partitions, out double highestValue, out double lowestValue)
        {
            highestValue = info.View0.HighestValue;
            lowestValue = info.View0.LowestValue;
            return GetPartitions(info.View0.LowestValue, info.View0.HighestValue, partitions);
        }

        public virtual string GetHoverValue(List<TraceView> list, MouseEventArgs e)
        {
            TraceView.MouseInfo measureInfo = list[0].Measure(e);
            string result = "";
            if (measureInfo.YRatio >= 0.0 && measureInfo.YRatio <= 1.0)
            {
                result = measureInfo.YValue.ToStringRound(6, 3);
            }
            return result;
        }

        public virtual string GetHoverStatistics(TraceView trace, TraceView.MouseInfo info)
        {
            string sps = (trace.Samples.InputSamplesPerSecond == 0.0) ? "" : (" (" + trace.SampleNumberText(info) + ")");
            return $"{info.SampleAtX.ToStringRound(5, 3)} {trace.ViewName}[{info.IndexBeforeTrim}]{sps}";
        }

        ////////////////////////////////////////////
        // paint

        public virtual void PaintHorizontalAxis(Graphics graphics, TraceGroupDisplay info)
        {
            if (info.LeftSampleNumber > info.RightSampleNumber) return;
            PaintGutterBottomPartition(
                info,
                graphics,
                left: (double)(info.ShowHorizontalUnits ? info.LeftSampleNumberValue : info.LeftSampleNumber),
                right: (double)(info.ShowHorizontalUnits ? info.RightSampleNumberValue : info.RightSampleNumber));
        }

        public virtual void PaintAxisTitleHorizontal(Graphics graphics, TraceGroupDisplay info)
        {
            string axisTitleBottom = info.View0.Samples.AxisTitleBottom;
            if (axisTitleBottom == null) return;

            using Font font = info.Skin.AxisTitleFont.Font;
            using Brush brush = info.Skin.AxisTitleFont.Brush;
            SizeF sizeF = graphics.MeasureString(axisTitleBottom, font);
            graphics.DrawString(axisTitleBottom, font, brush, (float)info.GroupArea.Right - sizeF.Width - 10f, info.ProjectionArea.Bottom + 1);
        }

        public virtual void PaintAxisTitleVertical(Graphics graphics, TraceGroupDisplay info)
        {
            string axisTitleLeft = info.View0.Samples.AxisTitleLeft;
            if (string.IsNullOrEmpty(axisTitleLeft)) return;

            using Font font = info.Skin.AxisTitleFont.Font;
            using Matrix matrix = new Matrix(1f, 0f, 0f, 1f, 0f, 0f);
            using Brush brush = info.Skin.AxisTitleFont.Brush;
            SizeF sizeF = graphics.MeasureString(axisTitleLeft, font);
            float offsetX = (info.View0.Scope.ActiveSkin.VerticalAxisPosition == Skin.VerticalAxisPositions.Left)
                ? info.VerticalAxisArea.Left + 10
                : info.VerticalAxisArea.Right - sizeF.Height - 10f;
            float offsetY = info.ProjectionArea.Top + info.ProjectionArea.Height / 2;

            matrix.Rotate(-90f, MatrixOrder.Append);
            matrix.Translate(offsetX, offsetY, MatrixOrder.Append);
            try
            {
                graphics.Transform = matrix;
                graphics.DrawString(axisTitleLeft, font, brush, (0f - sizeF.Width) / 2f, 0f);
            }
            finally
            {
                graphics.ResetTransform();
            }
        }

        public virtual void PaintVerticalAxis(Graphics graphics, TraceGroupDisplay info)
        {
            int lineSpacing = info.Skin.AxisTextFont.LineSpacing;

            int lineCount1 = info.ProjectionArea.Height / lineSpacing;
            int lineCount2 = info.ProjectionArea.Height / (lineSpacing * 3 / 2);
            int partitionCount = Math.Min(Math.Max(lineCount2, 5), 16);
            if (partitionCount > lineCount2)
            {
                partitionCount = Math.Min(lineCount1, partitionCount);
            }
            double highestValue;
            double lowestValue;

            foreach (double num in VerticalAxisPartition(info, partitionCount, out highestValue, out lowestValue))
            {
                float y;
                if (info.View0.LogVertical)
                {
                    lowestValue = 0.0;
                    ProjectLog(highestValue, num, out var newMax, out var output);
                    y = (float)(info.ProjectionArea.Top + info.ProjectionArea.Height - output / newMax * info.ProjectionArea.Height);
                }
                else
                {
                    // numbers aren't reprojected, could be 2d trace or FFT result
                    y = (float)(info.ProjectionArea.Top + (highestValue - num) * info.ProjectionArea.Height / (highestValue - lowestValue));
                }

                y = ((y < info.ProjectionArea.Top) ? info.ProjectionArea.Top : ((y > info.ProjectionArea.Bottom) ? info.ProjectionArea.Bottom : y));
                string text = VerticalAxisFormat(info, num);
                using Font font = info.Skin.AxisTextFont.Font;
                using Brush brush = info.Skin.AxisTextFont.Brush;
                using Pen pen = new Pen(info.Skin.GraduationColour);
                graphics.DrawLine(pen, info.ProjectionArea.Left, y, info.ProjectionArea.Right, y);
                y -= lineSpacing / 2;
                if (y >= info.ProjectionArea.Top
                    && y < info.ProjectionArea.Bottom - info.Skin.AxisTextFont.EmSize)
                {
                    float x = (info.Skin.VerticalAxisPosition == Skin.VerticalAxisPositions.Right)
                        ? info.VerticalAxisArea.Left + 2
                        : (info.VerticalAxisArea.Right - graphics.MeasureString(text, font).Width - 2f);
                    graphics.DrawString(text, font, brush, x, y);
                }
            }
        }

        public virtual void PaintFeatures(Graphics graphics, TraceGroupDisplay info, IEnumerable<TraceFeature> features)
        {
            TraceFeature[] array = features.ToArray();
            if (array.Length == 0) return;

            int leftFeatureIndex;
            int rightFeatureIndex;
            if (info.YTTrace)
            {
                leftFeatureIndex = Array.BinarySearch(array, new TraceFeature { UnixTime = info.LeftUnixTime - 1 }, new TraceFeature.FeatureCompare());
                rightFeatureIndex = Array.BinarySearch(array, new TraceFeature { UnixTime = info.RightUnixTime + 1 }, new TraceFeature.FeatureCompare());
            }
            else
            {
                int leftSample = info.LeftSampleNumber + info.ViewOffsetOverride;
                int rightSample = info.RightSampleNumber + info.ViewOffsetOverride;
                leftFeatureIndex = Array.BinarySearch(array, new TraceFeature { SampleNumber = leftSample - 1 }, new TraceFeature.FeatureCompare());
                rightFeatureIndex = Array.BinarySearch(array, new TraceFeature { SampleNumber = rightSample + 1 }, new TraceFeature.FeatureCompare());
            }
            if (leftFeatureIndex < 0) leftFeatureIndex = ~leftFeatureIndex;
            if (leftFeatureIndex > 0) leftFeatureIndex--;
            if (rightFeatureIndex < 0) rightFeatureIndex = ~rightFeatureIndex;

            graphics.SetClip(info.ProjectionArea);
            using SolidBrush solidBrush = new SolidBrush(Color.Empty);
            using Pen pen = new Pen(Color.Empty);
            using Font font = info.Skin.FeatureTextFont.Font;
            float lastX = -50f;

            for (int loop = leftFeatureIndex; loop <= Math.Min(array.Length - 1, rightFeatureIndex); loop++)
            {
                TraceFeature feature = array[loop];
                float xLeft;
                float xRight;
                if (info.YTTrace)
                {
                    xLeft = info.ProjectionArea.Left + (int)(info.ProjectionArea.Width * SampleToRatioYT(info, feature.UnixTime));
                    xRight = info.ProjectionArea.Left + (int)(info.ProjectionArea.Width * SampleToRatioYT(info, feature.RightUnixTime));
                }
                else
                {
                    xLeft = info.ProjectionArea.Left + (int)(info.ProjectionArea.Width * SampleToRatio(info, feature.SampleNumber));
                    xRight = info.ProjectionArea.Left + (int)(info.ProjectionArea.Width * SampleToRatio(info, feature.RightSampleNumber));
                }

                const int handleSize = 24;
                switch (feature.Type)
                {
                    case TraceFeature.Feature.Text:
                        {
                            bool draw = true;
                            if (lastX + info.Skin.FeatureTextFont.LineSpacing <= xLeft)
                            {
                                lastX = xLeft; // put the text where we want it
                            }
                            else// if (lastX > xLeft + info.Skin.FeatureTextFont.LineSpacing * 5)
                            {
                                // too far right of where we want it, bunching up
                                lastX += 2;
                                //draw=false;
                            }
                            //else
                            //{ // fixme: This makes features confusing. Find another method
                            //    xLeft = lastX = lastX + info.Skin.FeatureTextFont.LineSpacing;
                            //}
                            if (draw)
                            {
                                float y = (info.ProjectionArea.Top + info.ProjectionArea.Bottom) / 2;
                                using (Matrix matrix2 = new Matrix(1f, 0f, 0f, 1f, 0f, 0f))
                                {
                                    matrix2.Rotate(feature.Angle, MatrixOrder.Append);
                                    matrix2.Translate(xLeft, y, MatrixOrder.Append);
                                    try
                                    {
                                        graphics.Transform = matrix2;
                                        SizeF sizeF = graphics.MeasureString(feature.Text, font);
                                        solidBrush.Color = feature.Colour ?? info.Skin.FeatureTextFont.Color;
                                        graphics.DrawString(feature.Text, font, solidBrush, (0f - sizeF.Width) / 2f, (0f - sizeF.Height) / 2f);
                                    }
                                    finally
                                    {
                                        graphics.ResetTransform();
                                    }
                                }
                            }
                            break;
                        }

                    case TraceFeature.Feature.GutterText:
                        {
                            float y = info.BottomGutter.Top + info.BottomGutter.Height / 2;
                            SizeF sizeF = graphics.MeasureString(feature.Text, font);
                            solidBrush.Color = feature.Colour ?? info.Skin.FeatureTextFont.Color;
                            graphics.DrawString(feature.Text, font, solidBrush,
                                x: info.ProjectionArea.Left + info.ProjectionArea.Width * SampleToRatio(info, feature.SampleNumber + 0.5f) - sizeF.Width / 2f,
                                y: y - sizeF.Height / 2f);
                            break;
                        }

                    case TraceFeature.Feature.Line:
                        pen.Color = feature.Colour ?? info.Skin.FeatureTextFont.Color;
                        graphics.DrawLine(pen, xLeft, info.ProjectionArea.Top, xLeft, info.ProjectionArea.Bottom);
                        break;

                    case TraceFeature.Feature.Highlight:
                        using (Brush brush = new SolidBrush(feature.Colour ?? info.Skin.HoverLabelColour))
                        {
                            Rectangle rect = Rectangle.Intersect(info.ProjectionArea, new Rectangle((int)xLeft, info.ProjectionArea.Top, (int)(xRight - xLeft), info.ProjectionArea.Height));
                            graphics.FillRectangle(brush, rect);
                            break;
                        }

                    case TraceFeature.Feature.LeftHandle:
                    case TraceFeature.Feature.RightHandle:
                        {
                            pen.Color = feature.Colour ?? info.Skin.FeatureTextFont.Color;
                            graphics.DrawLine(pen, xLeft, info.ProjectionArea.Top, xLeft, info.ProjectionArea.Bottom);
                            solidBrush.Color = pen.Color;
                            int ratio = feature.Type == TraceFeature.Feature.RightHandle ? 1 : 3;

                            feature.PaintedHitBox = new Rectangle(
                                x: (int)(xLeft - handleSize / 2),
                                y: (info.ProjectionArea.Top * ratio + info.ProjectionArea.Bottom * (4 - ratio)) / 4,
                                handleSize - 1,
                                handleSize - 1);
                            graphics.FillEllipse(solidBrush, feature.PaintedHitBox);
                            break;
                        }

                    case TraceFeature.Feature.TriggerHandle:
                        {
                            pen.Color = feature.Colour ?? info.Skin.FeatureTextFont.Color;
                            float y = (float)((double)info.ProjectionArea.Bottom - (double)info.ProjectionArea.Height * (info.View0.TriggerValue - info.View0.LowestValue) / (info.View0.HighestValue - info.View0.LowestValue));
                            graphics.DrawLine(pen, info.ProjectionArea.Left, y, info.ProjectionArea.Left + info.ProjectionArea.Width, y);
                            solidBrush.Color = pen.Color;
                            feature.PaintedHitBox = new Rectangle(info.ProjectionArea.Left + 25, (int)(y - handleSize / 2), handleSize - 1, handleSize - 1);
                            graphics.FillEllipse(solidBrush, feature.PaintedHitBox);
                            break;
                        }
                }
            }
            graphics.ResetClip();
        }

        protected void PaintPiP(TraceGroupDisplay info, Graphics graphics)
        {
            if (info.View0.ShowPictureInPicture && this is not PaintPiPTrace)
            {
                if (m_PictureInPicture == null)
                {
                    m_PictureInPicture = new PaintPiPTrace();
                }

                int x = info.ProjectionArea.Width / 4;
                int y = info.ProjectionArea.Top + 10;
                int width = info.ProjectionArea.Width * 11 / 16;
                int height = Math.Min(info.ProjectionArea.Height / 6, info.PaintBoxScreenRect.Height / 16);

                if (height > 16 && info.View0.ZoomValue != 1.0)
                {
                    var traceDivision = (TraceGroupDisplay)info.Clone();
                    traceDivision.ProjectionArea.X = x;
                    traceDivision.ProjectionArea.Y = y;
                    traceDivision.ProjectionArea.Height = height;
                    traceDivision.ProjectionArea.Width = width;
                    traceDivision.DrawPictureInPicture = true;
                    traceDivision.BottomGutter.Height = 0;
                    m_PictureInPicture.PaintInitial(graphics, traceDivision);
                    m_PictureInPicture.PaintProjection(graphics, traceDivision);
                }
            }
            else if (m_PictureInPicture != null)
            {
                m_PictureInPicture.Dispose();
                m_PictureInPicture = null;
            }
        }

        public void PaintStats(Graphics graphics, TraceGroupDisplay info)
        {
            TraceView view = info.View0;
            if (view.Scope.ShowTraceStatistics == Skin.TraceStatistics.Embedded)
            {
                using Font font = info.Skin.LegendTextFont.Font;
                string decoratedName = view.DecoratedName;
                string traceInfo = view.TraceInfo();
                string decorate = view.Scope.ShowTraceLabels == Skin.TraceLabels.Embedded
                    ? traceInfo
                    : $"{decoratedName}: {traceInfo}";

                float x = info.ProjectionArea.Right - graphics.MeasureString(decorate, font).Width - 5f;
                float y = info.ProjectionArea.Top + font.Height * info.OverlayIndex;

                if (info.OverlayIndex == 0 || y < info.ProjectionArea.Bottom - font.Height)
                {
                    using Brush brush = info.Skin.LegendTextFont.Brush;
                    graphics.DrawString(decorate, font, brush, x, y);
                }
            }
            else if (view.Scope.ShowTraceStatistics == Skin.TraceStatistics.VerticalGutter)
            {
                var statsPairs = view.CalculateStats().AsList();
                using Font font = info.Skin.AxisTextFont.Font;
                using Brush brush = new SolidBrush(view.Colour);

                float xKey = info.RightGutter.Left + 2;
                float xValue = xKey;
                float yStart = info.ProjectionArea.Top + font.Height * 9 * info.OverlayIndex;

                float y = yStart;
                foreach (Tuple<string, double> item in statsPairs)
                {
                    if (y < info.ProjectionArea.Bottom - font.Height)
                    {
                        graphics.DrawString(item.Item1, font, brush, xKey, y);
                        xValue = Math.Max(xValue, xKey + graphics.MeasureString(item.Item1 + " ", font).Width);
                        y += font.Height;
                    }
                }

                y = yStart;
                foreach (Tuple<string, double> item in statsPairs)
                {
                    if (y < info.ProjectionArea.Bottom - font.Height)
                    {
                        graphics.DrawString(item.Item2.ToStringRound(5, 3), font, brush, xValue, y);
                        y += font.Height;
                    }
                }
            }
        }

        public virtual void PaintLabel(Graphics graphics, TraceGroupDisplay info)
        {
            TraceView traceView = info.View0;
            switch (traceView.Scope.ShowTraceLabels)
            {
                case Skin.TraceLabels.VerticalGutter:
                    {
                        float x = info.LeftGutter.Left + info.Skin.LegendTextFont.LineSpacing * info.OverlayIndex;
                        float y = (info.LeftGutter.Top + info.LeftGutter.Bottom) / 2;
                        if (info.OverlayIndex == 0 || x + (float)info.Skin.LegendTextFont.LineSpacing < (float)info.ProjectionArea.Left)
                        {
                            TraceViewEmbedText text = new TraceViewEmbedText((int)x, (int)y, info, PaintBoxMouseInfo.GuiSection.TraceLabel, TraceViewClickZone.Flags.Trace);
                            using (Font font2 = info.Skin.LegendTextFont.Font)
                                text.Paint(traceView.Colour, traceView.DecoratedName, font2, info, graphics, TraceViewEmbedText.Align.TopCenterVertical, TraceViewEmbedText.Style.Normal);
                            traceView.Painted.ClickZones.Add(text);
                        }
                        break;
                    }
                case Skin.TraceLabels.Embedded:
                    {
                        float x = info.ProjectionArea.Left;
                        float y = info.ProjectionArea.Top + info.Skin.LegendTextFont.LineSpacing * info.OverlayIndex;
                        if (info.OverlayIndex == 0 || y + (float)info.Skin.LegendTextFont.LineSpacing < (float)info.ProjectionArea.Bottom)
                        {
                            TraceViewEmbedText scopeTraceViewOverlayText = new TraceViewEmbedText((int)x, (int)y, info, PaintBoxMouseInfo.GuiSection.TraceLabel, TraceViewClickZone.Flags.Trace);
                            using (Font font = info.Skin.LegendTextFont.Font)
                                scopeTraceViewOverlayText.Paint(traceView.Colour, traceView.DecoratedName, font, info, graphics, TraceViewEmbedText.Align.TopLeft, TraceViewEmbedText.Style.Normal);
                            traceView.Painted.ClickZones.Add(scopeTraceViewOverlayText);
                        }
                        break;
                    }
                case Skin.TraceLabels.None:
                    break;
            }
        }

        public virtual void PaintEmbeddedControls(Graphics graphics, TraceGroupDisplay info, ScopeContextMenu scopeContextMenu)
        {
            TraceView view = info.View0;
            Rectangle rectangle = new Rectangle(info.ProjectionArea.Left, info.ProjectionArea.Top + info.PaintVerticalOffset, info.ProjectionArea.Width, info.ProjectionArea.Height);

            if (info.MouseInfo.Click != null
                && !info.MouseInfo.HideCursor
                && rectangle.Contains(new Point(info.MouseInfo.Click!.X, info.MouseInfo.Click.Y)))
            {
                PaintEmbeddedContextMenu(graphics, info, scopeContextMenu);
                scopeContextMenu.AddTrimHandles(info, graphics);
            }
        }

        internal void PaintEmbeddedContextMenu(Graphics graphics, TraceGroupDisplay info, ScopeContextMenu scopeContextMenu)
        {
            if (!info.View0.Scope.ShowTraceContextLabels) return;

            TraceView view = info.View0;
            ScopeContextMenu.MenuArgs a = new ScopeContextMenu.MenuArgs()
            {
                View = view,
            };
            int x = info.ProjectionArea.Right;
            int y = info.ProjectionArea.Bottom;
            foreach (var embeddedContextMenu in scopeContextMenu.EmbeddedContextMenuList)
            {
                a.Menu = embeddedContextMenu;
                if (a.Menu.GetStyle != null)
                {
                    a.Menu.GetStyle(a);
                }
                if (a.Menu.Style != 0)
                {
                    var embed = new ScopeContextMenu.Embed(x, y, info, PaintBoxMouseInfo.GuiSection.ContextOverlay, TraceViewClickZone.Flags.Group, a.Menu);
                    using (Font font = info.Skin.HotPointTextFont.Font)
                    {
                        embed.Paint(view.Colour, a.Menu.Text, font, info, graphics, TraceViewEmbedText.Align.BottomRight, a.Menu.Style);
                    }
                    x -= embed.Rect.Width + 5;
                    view.Painted.ClickZones.Add(embed);
                }
            }
        }

        protected void PaintGutterBottomPartition(TraceGroupDisplay info, Graphics graphics, double left, double right)
        {
            using Font font = info.Skin.AxisTextFont.Font;
            using Pen pen = new Pen(info.Skin.GraduationColour);
            using Brush brush = info.Skin.AxisTextFont.Brush;
            float typicalWidth = graphics.MeasureString("000.000", font).Width;
            float maxWidth = graphics.MeasureString(ToHorizontalUnit(info, info.YTTrace ? info.LeftUnixTime : 500.005), font).Width * 1.2f;
            maxWidth = Math.Max(maxWidth, typicalWidth);
            int partitionCount = (int)Math.Max(5f, (float)info.ProjectionArea.Width / typicalWidth);
            double step = (right - left) / (double)partitionCount;
            step = step.RoundSignificantUp(1, step);
            if (!info.YTTrace && info.View0 != null && info.View0.Samples.ViewedSamplesPerSecond != 0.0)
            {
                step = Math.Max(step, 1.0 / info.View0.Samples.ViewedSamplesPerSecond);
            }
            long index = (long)Math.Floor(left / step);
            double[] partitions = new double[partitionCount + 1];
            for (int loop = 0; loop <= partitionCount; loop++)
            {
                partitions[loop] = (loop + index) * step;
            }
            int skip = (int)Math.Ceiling(maxWidth / typicalWidth);
            skip = ((skip == 0) ? 1 : skip);
            int textIndex = (int)(index % skip);
            graphics.SetClip(new Rectangle(info.ProjectionArea.Left, info.ProjectionArea.Top, info.ProjectionArea.Width, info.BottomGutter.Bottom - info.ProjectionArea.Top));
            foreach (double value in partitions)
            {
                float width = info.ProjectionArea.Width;
                float x = (float)((value - left) * (double)width / (right - left));
                if (x > 0f - width && x < width * 2f)
                {
                    pen.DashStyle = DashStyle.Dash;
                    graphics.DrawLine(pen, x + (float)info.ProjectionArea.Left, info.ProjectionArea.Bottom, x + (float)info.ProjectionArea.Left, info.ProjectionArea.Top);
                    string text = ToHorizontalUnit(info, value);
                    x -= graphics.MeasureString(text, font).Width / 2f;
                    if (textIndex == 0)
                    {
                        graphics.DrawString(text, font, brush, info.ProjectionArea.Left + x, info.BottomGutter.Top + 1);
                    }
                    textIndex = (textIndex + 1) % skip;
                }
            }
            graphics.ResetClip();
        }

        protected void PaintDots(Graphics graphics, Brush brush, PointF[] zipped)
        {
            foreach (PointF point in zipped)
            {
                graphics.FillRectangle(brush, point.X, point.Y, 0.5f, 0.5f);
            }
        }

        protected static void PaintCircles(Graphics graphics, Pen pen, PointF[] zipped, TraceGroupDisplay info)
        {
            float width = pen.Width;
            foreach (PointF point in zipped)
            {
                graphics.DrawEllipse(pen, (float)point.X - width, (float)point.Y - width, width * 2f, width * 2f);
            }
        }

        protected static void PaintFilledCircles(Graphics graphics, Brush brush, PointF[] zipped, float radius)
        {
            foreach (PointF point in zipped)
            {
                graphics.FillEllipse(brush, (float)point.X - radius, (float)point.Y - radius, radius * 2f, radius * 2f);
            }
        }

        ////////////////////////////////////////////
        // other
        public virtual void Dispose()
        {
            if (m_PictureInPicture != null)
            {
                m_PictureInPicture!.Dispose();
                m_PictureInPicture = null;
            }
        }
    }
}
