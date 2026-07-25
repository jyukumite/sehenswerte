using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        public bool YTTrace => View0.IsYtDisplay; // same predicate as HorizontalKind.Yt
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

        public HorizontalMode HMode = HorizontalMode.Stretch;
        public Rectangle ValueRect;
        public double GroupHLeft;
        public double GroupHRight;

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

            // Resolve grouped-trace horizontal alignment. Default: Stretch (each trace fills the pane -
            // legacy behaviour), so a plain-index group and a single trace render exactly as before.
            ValueRect = ProjectionArea;
            HMode = HorizontalMode.Stretch;
            GroupHLeft = LeftSampleNumberValue;
            GroupHRight = RightSampleNumberValue;
            // Runs for FFT and YT views too: all-FFT and all-YT groups classify Stretch (their own
            // Hz / group-time-window paths, untouched), but FFT or YT mixed with anything else must
            // classify Incompatible so the warning paints whichever member leads the group.
            {
                double ownLeft = LeftSampleNumberValue;
                double ownRight = RightSampleNumberValue;
                GroupHorizontal.Domain domain = ComputeGroupHorizontal(view);
                HMode = domain.Mode;
                if (domain.Mode == HorizontalMode.ValueAlign)
                {
                    GroupHLeft = domain.Left;
                    GroupHRight = domain.Right;
                    var (xl, xw) = GroupHorizontal.SubWindow(ownLeft, ownRight, domain.Left, domain.Right, ProjectionArea.Left, ProjectionArea.Width);
                    ValueRect = new Rectangle((int)Math.Round(xl), ProjectionArea.Top, Math.Max(1, (int)Math.Round(xw)), ProjectionArea.Height);
                    // The gutter (painted from the leader) spans the shared domain across the full pane;
                    // each trace's samples fill their ValueRect, so ticks and curves use one value->pixel map.
                    LeftSampleNumberValue = domain.Left;
                    RightSampleNumberValue = domain.Right;
                    HorizontalUnit = domain.Unit;
                }
            }
        }

        // The shared horizontal value window this view's group is showing: the group's full value domain
        // (from members' FullHorizontalAffine) narrowed by the current horizontal zoom/pan. Using the
        // full domain + zoom/pan (rather than the post-zoom drawn extents) keeps the gutter exactly on
        // the visible window even when no member's data reaches an edge. Matches the value window
        // GetDrawnSamples slices each member to, so ticks and curves agree. Falls back to the single
        // view when the group isn't painted yet.
        private static GroupHorizontal.Domain ComputeGroupHorizontal(TraceView view)
        {
            var group = view.Painted.Group;
            var members = new List<GroupHorizontal.Member>();
            void Add(TraceView v)
            {
                var f = v.FullHorizontalAffine();
                members.Add(new GroupHorizontal.Member(f.kind, f.unit, f.left, f.right, v.IsLogX));
            }
            if (group == null || group.Count == 0)
            {
                Add(view);
            }
            else
            {
                foreach (TraceView v in group)
                {
                    if (v.Visible) Add(v);
                }
                if (members.Count == 0) Add(view);
            }
            return GroupHorizontal.Window(members, view.Scope.ZoomValue, view.Scope.PanValue);
        }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }

    // Integration tests for the classification -> pixels glue: ValueRect placement, the shared
    // group domain, and the gutter endpoint override. Uses the headless SehensTestHarness geometry.
    [TestClass]
    public class TraceGroupDisplayTests
    {
        [TestMethod]
        public void SinglePlainTraceStretches()
        {
            var scope = new SehensControl();
            scope["plain"].Update(SehensTestHarness.Ramp(100));
            SehensTestHarness.Layout(scope);
            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(SehensTestHarness.View(scope, "plain"));
            Assert.AreEqual(HorizontalMode.Stretch, info.HMode);
            Assert.AreEqual(info.ProjectionArea, info.ValueRect); // legacy no-op invariant
            Assert.AreEqual(0.0, info.LeftSampleNumberValue, 1e-9);
            Assert.AreEqual(100.0, info.RightSampleNumberValue, 1e-9);
        }

        [TestMethod]
        public void SameUnitGroupValueAlignsAndOverridesGutter()
        {
            var scope = new SehensControl();
            SehensTestHarness.AffineTrace(scope, "A", count: 11, offset: 0, multiplier: 10, unit: "rpm"); // 0..110
            SehensTestHarness.AffineTrace(scope, "B", count: 6, offset: 0, multiplier: 10, unit: "rpm");  // 0..60
            scope.GroupViews(new[] { "A", "B" });
            SehensTestHarness.Layout(scope);

            TraceGroupDisplay infoA = scope.PaintBox.TraceToGroupDisplayInfo(SehensTestHarness.View(scope, "A"));
            Assert.AreEqual(HorizontalMode.ValueAlign, infoA.HMode);
            Assert.AreEqual(0.0, infoA.GroupHLeft, 1e-9);
            Assert.AreEqual(110.0, infoA.GroupHRight, 1e-9);
            // the gutter is painted from these endpoints - they must be the SHARED domain + unit
            Assert.AreEqual(0.0, infoA.LeftSampleNumberValue, 1e-9);
            Assert.AreEqual(110.0, infoA.RightSampleNumberValue, 1e-9);
            Assert.AreEqual("rpm", infoA.HorizontalUnit);
            // A spans the full domain so its sub-window is the full pane
            Assert.IsTrue(Math.Abs(infoA.ValueRect.Left - infoA.ProjectionArea.Left) <= 1);
            Assert.IsTrue(Math.Abs(infoA.ValueRect.Width - infoA.ProjectionArea.Width) <= 1);

            TraceGroupDisplay infoB = scope.PaintBox.TraceToGroupDisplayInfo(SehensTestHarness.View(scope, "B"));
            Assert.AreEqual(HorizontalMode.ValueAlign, infoB.HMode);
            // B covers 0..60 of 0..110: left-aligned, ends early at 60/110 of the pane
            Assert.IsTrue(Math.Abs(infoB.ValueRect.Left - infoB.ProjectionArea.Left) <= 1);
            double expectedWidth = infoB.ProjectionArea.Width * 60.0 / 110.0;
            Assert.IsTrue(Math.Abs(infoB.ValueRect.Width - expectedWidth) <= 1.5,
                $"B width {infoB.ValueRect.Width}, expected ~{expectedWidth}");
            Assert.AreEqual(0.0, infoB.LeftSampleNumberValue, 1e-9);   // gutter shows the domain, not B's 0..60
            Assert.AreEqual(110.0, infoB.RightSampleNumberValue, 1e-9);
        }

        [TestMethod]
        public void RaggedMemberIsPlacedByValue()
        {
            var scope = new SehensControl();
            SehensTestHarness.AffineTrace(scope, "A", count: 11, offset: 0, multiplier: 10, unit: "rpm"); // 0..110
            SehensTestHarness.AffineTrace(scope, "B", count: 6, offset: 5, multiplier: 10, unit: "rpm");  // 10*(s+5) = 50..110
            scope.GroupViews(new[] { "A", "B" });
            SehensTestHarness.Layout(scope);

            TraceGroupDisplay infoB = scope.PaintBox.TraceToGroupDisplayInfo(SehensTestHarness.View(scope, "B"));
            double expectedLeft = infoB.ProjectionArea.Left + infoB.ProjectionArea.Width * 50.0 / 110.0;
            Assert.IsTrue(Math.Abs(infoB.ValueRect.Left - expectedLeft) <= 1.5,
                $"B left {infoB.ValueRect.Left}, expected ~{expectedLeft}");
            Assert.IsTrue(Math.Abs(infoB.ValueRect.Right - infoB.ProjectionArea.Right) <= 1.5,
                $"B right {infoB.ValueRect.Right}, expected ~{infoB.ProjectionArea.Right}");
        }

        [TestMethod]
        public void MixedUnitsFallBackToLeaderAxis()
        {
            var scope = new SehensControl();
            SehensTestHarness.AffineTrace(scope, "A", count: 11, offset: 0, multiplier: 10, unit: "rpm"); // 0..110
            SehensTestHarness.AffineTrace(scope, "C", count: 11, offset: 0, multiplier: 5, unit: "kph");  // 0..55
            scope.GroupViews(new[] { "A", "C" });
            SehensTestHarness.Layout(scope);

            TraceGroupDisplay infoA = scope.PaintBox.TraceToGroupDisplayInfo(SehensTestHarness.View(scope, "A"));
            Assert.AreEqual(HorizontalMode.Incompatible, infoA.HMode); // -> "mixed horizontal axes" warning
            Assert.AreEqual(infoA.ProjectionArea, infoA.ValueRect);    // legacy index-stretch fallback
            Assert.AreEqual(0.0, infoA.LeftSampleNumberValue, 1e-9);   // leader keeps its own axis
            Assert.AreEqual(110.0, infoA.RightSampleNumberValue, 1e-9);
            Assert.AreEqual("rpm", infoA.HorizontalUnit);
        }

        [TestMethod]
        public void InvalidAffineMemberClassifiesAsPlainIndex()
        {
            var scope = new SehensControl();
            // invalid multiplier: axis unusable -> trace behaves as plain-index (kind None)
            SehensTestHarness.AffineTrace(scope, "bad", count: 10, offset: 0, multiplier: -1, unit: "rpm");
            SehensTestHarness.Layout(scope);
            Assert.IsTrue(SehensTestHarness.View(scope, "bad").Samples.HorizontalAffineInvalid);
            TraceGroupDisplay solo = scope.PaintBox.TraceToGroupDisplayInfo(SehensTestHarness.View(scope, "bad"));
            Assert.AreEqual(HorizontalMode.Stretch, solo.HMode); // single None member stretches

            // grouped with a REAL axis it cannot reconcile -> Incompatible, not silent misalignment
            SehensTestHarness.AffineTrace(scope, "good", count: 10, offset: 0, multiplier: 10, unit: "rpm");
            scope.GroupViews(new[] { "good", "bad" });
            SehensTestHarness.Layout(scope);
            TraceGroupDisplay grouped = scope.PaintBox.TraceToGroupDisplayInfo(SehensTestHarness.View(scope, "good"));
            Assert.AreEqual(HorizontalMode.Incompatible, grouped.HMode);
        }

        [TestMethod]
        public void SingleTimeTraceAlignsToItsOwnExtents()
        {
            var scope = new SehensControl();
            scope["T"].Update(SehensTestHarness.Ramp(50));
            scope["T"].InputSamplesPerSecond = 10.0; // Time kind, 0..5 s
            SehensTestHarness.Layout(scope);
            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(SehensTestHarness.View(scope, "T"));
            Assert.AreEqual(HorizontalMode.ValueAlign, info.HMode);
            Assert.AreEqual(0.0, info.LeftSampleNumberValue, 1e-9);
            Assert.AreEqual(5.0, info.RightSampleNumberValue, 1e-9);
            Assert.AreEqual(info.ProjectionArea, info.ValueRect); // member == domain -> full pane
        }

        [TestMethod]
        public void LinAndLogGroupFallsBackWithWarning()
        {
            // Field report: an affine lin-X trace grouped with an affine log-X trace showed no
            // "mixed horizontal axes" warning while the shared gutter could only be right for one
            // of them. Log-ness is part of axis compatibility - the pair must fall back + warn.
            var scope = new SehensControl();
            SehensTestHarness.AffineTrace(scope, "lin", count: 100, offset: 0, multiplier: 1, unit: "u");
            TraceView logView = SehensTestHarness.AffineTrace(scope, "log", count: 100, offset: 0, multiplier: 1, unit: "u");
            logView.LogHorizontal = TraceView.LogHorizontalMode.Log;
            scope.GroupViews(new[] { "lin", "log" });
            SehensTestHarness.Layout(scope);

            TraceGroupDisplay infoLin = scope.PaintBox.TraceToGroupDisplayInfo(SehensTestHarness.View(scope, "lin"));
            Assert.AreEqual(HorizontalMode.Incompatible, infoLin.HMode); // -> "mixed horizontal axes" warning
            Assert.AreEqual(infoLin.ProjectionArea, infoLin.ValueRect);  // legacy index-stretch fallback
            TraceGroupDisplay infoLog = scope.PaintBox.TraceToGroupDisplayInfo(logView);
            Assert.AreEqual(HorizontalMode.Incompatible, infoLog.HMode);
            Assert.AreEqual(0.0, infoLog.LeftSampleNumberValue, 1e-9);   // own axis, not a shared domain
            Assert.AreEqual(100.0, infoLog.RightSampleNumberValue, 1e-9);
        }

        [TestMethod]
        public void NoneLinAndNoneLogGroupWarns()
        {
            // two plain sample-number traces, one log-X: must warn instead of silently stretching
            var scope = new SehensControl();
            scope["nlin"].Update(SehensTestHarness.Ramp(100));
            scope["nlog"].Update(SehensTestHarness.Ramp(100));
            SehensTestHarness.View(scope, "nlog").LogHorizontal = TraceView.LogHorizontalMode.Log;
            scope.GroupViews(new[] { "nlin", "nlog" });
            SehensTestHarness.Layout(scope);
            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(SehensTestHarness.View(scope, "nlin"));
            Assert.AreEqual(HorizontalMode.Incompatible, info.HMode); // -> "mixed horizontal axes" warning
            Assert.AreEqual(info.ProjectionArea, info.ValueRect);
        }

        [TestMethod]
        public void FftGroupedWithPlainTraceWarnsEvenWhenFftLeads()
        {
            var scope = new SehensControl();
            scope["fft"].Update(SehensTestHarness.Ramp(256));
            scope["plain"].Update(SehensTestHarness.Ramp(256));
            TraceView fft = SehensTestHarness.View(scope, "fft");
            fft.MathType = TraceView.MathTypes.FFTMagnitude;
            scope.GroupViews(new[] { "fft", "plain" }); // FFT trace is the LEADER
            SehensTestHarness.Layout(scope);

            // the warning paints from the leader's info - it must classify even for an FFT view
            TraceGroupDisplay infoFft = scope.PaintBox.TraceToGroupDisplayInfo(fft);
            Assert.AreEqual(HorizontalMode.Incompatible, infoFft.HMode);
            Assert.AreEqual(infoFft.ProjectionArea, infoFft.ValueRect); // FFT axis untouched
            TraceGroupDisplay infoPlain = scope.PaintBox.TraceToGroupDisplayInfo(SehensTestHarness.View(scope, "plain"));
            Assert.AreEqual(HorizontalMode.Incompatible, infoPlain.HMode);
        }

        [TestMethod]
        public void FftPairKeepsItsOwnPathWithoutWarning()
        {
            var scope = new SehensControl();
            scope["f1"].Update(SehensTestHarness.Ramp(256));
            scope["f2"].Update(SehensTestHarness.Ramp(256));
            SehensTestHarness.View(scope, "f1").MathType = TraceView.MathTypes.FFTMagnitude;
            SehensTestHarness.View(scope, "f2").MathType = TraceView.MathTypes.FFTMagnitude;
            scope.GroupViews(new[] { "f1", "f2" });
            SehensTestHarness.Layout(scope);
            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(SehensTestHarness.View(scope, "f1"));
            Assert.AreEqual(HorizontalMode.Stretch, info.HMode); // Hz-align stays in the FFT path
            Assert.AreEqual(info.ProjectionArea, info.ValueRect);
        }

        [TestMethod]
        public void SingleLogAffineTraceKeepsItsOwnLogAxis()
        {
            var scope = new SehensControl();
            TraceView view = SehensTestHarness.AffineTrace(scope, "logsolo", count: 100, offset: 0, multiplier: 120, unit: "Hz");
            view.LogHorizontal = TraceView.LogHorizontalMode.Log;
            SehensTestHarness.Layout(scope);
            TraceGroupDisplay info = scope.PaintBox.TraceToGroupDisplayInfo(view);
            Assert.AreEqual(HorizontalMode.ValueAlign, info.HMode); // single member: domain == own
            Assert.AreEqual(info.ProjectionArea, info.ValueRect);   // full pane, log map untouched
            Assert.AreEqual(0.0, info.LeftSampleNumberValue, 1e-9);
            Assert.AreEqual(12000.0, info.RightSampleNumberValue, 1e-9);
        }
    }
}
