using SehensWerte.Controls.Sehens;

namespace SehensWerte.Controls
{
    public class TraceGroupDisplay : ICloneable
    {
        [Flags]
        public enum PaintFlags
        {
            None = 0x0,
            Parallel = 0x1,
            Screenshot = 0x2
        }

        public TraceView View0;
        public SehensControl Scope => View0.Scope;

        public Skin Skin => Scope.ActiveSkin;
        public bool YTTrace => View0.Samples.ViewedIsYTTrace;
        public int OverlayIndex => View0.Painted.TraceIndex;

        public PaintBoxMouseInfo MouseInfo;
        public PaintFlags Flags;
        public int PaintVerticalOffset;
        public Rectangle PaintBoxScreenRect;

        public Rectangle GroupArea;
        public Rectangle VerticalAxisArea;
        public Rectangle ProjectionArea;
        public Rectangle BottomGutter;
        public Rectangle RightGutter;
        public Rectangle LeftGutter;
        public Rectangle TopGutter;

        public bool MouseOnEmbed;
        public bool DrawPictureInPicture;
        public bool IsOnScreen;
        public float YOffsetOf0Sample;

        public int LeftSampleNumber;
        public int RightSampleNumber;

        public double LeftSampleNumberValue;
        public double RightSampleNumberValue;

        public double LeftUnixTime;
        public double RightUnixTime;

        public int ViewLengthOverride;
        public int ViewOffsetOverride;

        public string HorizontalUnit;
        public bool ShowHorizontalUnits = true;

        public TraceGroupDisplay(PaintBoxMouseInfo mouse, Rectangle rect, SehensPaintBox paintBox, TraceView view, PaintFlags flag)
        {
            View0 = view;
            MouseInfo = mouse;
            Flags = flag;
            PaintBoxScreenRect = rect;

            bool lastGroup = view.Painted.GroupIndex == paintBox.PaintedTraces.VisibleTraceGroupList.Count - 1;
            int topGutterWidth = 0;
            int bottomGutterHeight;
            int leftGutterWidth = 0;
            int rightGutterWidth;
            int verticalAxisWidth;

            bottomGutterHeight = Skin.AxisTextFont.LineSpacing + 3;
            rightGutterWidth = Skin.TraceStats == Skin.TraceStatistics.VerticalGutter ? Skin.TraceStatsWidth : 0;
            verticalAxisWidth = Skin.VerticalAxisWidth;
            if (Skin.ShowAxisLabels && Skin.VerticalAxisPosition == Skin.VerticalAxisPositions.Right)
            {
                rightGutterWidth += Skin.AxisTitleFont.LineSpacing + 20;
            }

            if (Skin.TraceLabel == Skin.TraceLabels.VerticalGutter)
            {
                leftGutterWidth = Skin.LegendTextFont.LineSpacing * Skin.LeftGutterTextRows + 10;
            }

            int topY;
            int bottomY;
            if (flag.HasFlag(PaintFlags.Screenshot) || view.Painted.HeightAdjustSum == 0)
            {
                topY = paintBox.PaintBoxVirtualHeight * view.Painted.GroupIndex / view.Painted.GroupCount - paintBox.PaintBoxVirtualOffset;
                bottomY = paintBox.PaintBoxVirtualHeight * (view.Painted.GroupIndex + 1) / view.Painted.GroupCount - paintBox.PaintBoxVirtualOffset - 1;
            }
            else
            {
                topY = (int)(paintBox.PaintBoxVirtualHeight * view.Painted.HeightAdjustSumTop / view.Painted.HeightAdjustSum) - paintBox.PaintBoxVirtualOffset;
                bottomY = (int)(paintBox.PaintBoxVirtualHeight * view.Painted.HeightAdjustSumBottom / view.Painted.HeightAdjustSum) - paintBox.PaintBoxVirtualOffset - 1;
            }

            IsOnScreen = flag.HasFlag(PaintFlags.Screenshot) || (bottomY >= 0 && topY < paintBox.PaintBoxRealHeight);

            int projectionHeight = bottomY - topY - bottomGutterHeight - topGutterWidth;
            YOffsetOf0Sample = (float)(topY + (View0.HighestValue - 0.0) * projectionHeight / (View0.HighestValue - View0.LowestValue));

            (LeftSampleNumber, RightSampleNumber, LeftSampleNumberValue, RightSampleNumberValue, LeftUnixTime, RightUnixTime, HorizontalUnit, ViewLengthOverride, ViewOffsetOverride) = view.DrawnExtents();
            int projectionWidth = paintBox.PaintBoxWidth - rightGutterWidth - leftGutterWidth - verticalAxisWidth;

            if (Skin.VerticalAxisPosition == Skin.VerticalAxisPositions.Left)
            {
                ProjectionArea = new Rectangle(leftGutterWidth + verticalAxisWidth, topY + topGutterWidth, projectionWidth, projectionHeight);
                VerticalAxisArea = new Rectangle(leftGutterWidth, topY, verticalAxisWidth, bottomY - topY + 1);
            }
            else
            {
                ProjectionArea = new Rectangle(leftGutterWidth, topY + topGutterWidth, projectionWidth, projectionHeight);
                VerticalAxisArea = new Rectangle(ProjectionArea.Right, topY, Skin.VerticalAxisWidth, bottomY - topY + 1);
            }

            GroupArea = new Rectangle(0, topY, paintBox.PaintBoxWidth, bottomY - topY + 1);
            TopGutter = new Rectangle(leftGutterWidth, topY, ProjectionArea.Width, topY - ProjectionArea.Top);
            BottomGutter = new Rectangle(ProjectionArea.Left, ProjectionArea.Bottom + 1, ProjectionArea.Width, bottomGutterHeight - 1);
            LeftGutter = new Rectangle(0, topY, leftGutterWidth, GroupArea.Height);
            RightGutter = new Rectangle(paintBox.PaintBoxWidth - rightGutterWidth, topY, rightGutterWidth, GroupArea.Height);

            if (flag.HasFlag(PaintFlags.Parallel))
            {
                PaintVerticalOffset = GroupArea.Top;

                GroupArea.Y -= PaintVerticalOffset;
                ProjectionArea.Y -= PaintVerticalOffset;
                VerticalAxisArea.Y -= PaintVerticalOffset;
                TopGutter.Y -= PaintVerticalOffset;
                BottomGutter.Y -= PaintVerticalOffset;
                LeftGutter.Y -= PaintVerticalOffset;
                RightGutter.Y -= PaintVerticalOffset;
            }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
