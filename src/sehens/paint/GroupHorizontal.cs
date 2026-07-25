using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SehensWerte.Controls.Sehens
{
    public enum HorizontalKind
    {
        None,   // no explicit axis, no sample rate - intent is "stretch to fill the pane"
        Time,   // InputSamplesPerSecond != 0; value is seconds (unit "s"). SPS is affine (mult 1/sps).
        Affine, // SetHorizontalAffine(offset, multiplier, unit); value = multiplier*(sample+offset)
        Fft,    // frequency (Hz) - its own painter/axis path
        Yt,     // draws as YT (unix time) - its own group-shared time window path
    }

    public enum HorizontalMode
    {
        Stretch,      // no real axis: each trace fills the pane width independently (legacy behaviour)
        ValueAlign,   // all members share a compatible value axis: position by value in a shared domain
        Incompatible, // members cannot converge (mixed kinds / units): fall back to the leader's axis
    }

    // Pure classification + sub-window math for grouped-trace horizontal alignment. Kept free of
    // TraceView/Graphics so it is unit-testable headlessly; the paint layer supplies Members from each
    // trace's DrawnExtents and applies the resulting SubWindow as the trace's pixel x-range.
    public static class GroupHorizontal
    {
        // One group member's horizontal facts (Left/Right are the axis VALUES at its drawn endpoints).
        public struct Member
        {
            public HorizontalKind Kind;
            public string Unit;
            public double Left;
            public double Right;
            public bool Log; // the view's IsLogX - a display transform, but it changes the value->pixel map
            public Member(HorizontalKind kind, string unit, double left, double right, bool log = false)
            {
                Kind = kind; Unit = unit ?? ""; Left = left; Right = right; Log = log;
            }
        }

        public struct Domain
        {
            public HorizontalMode Mode;
            public double Left;   // shared value-domain left  (ValueAlign only)
            public double Right;  // shared value-domain right (ValueAlign only)
            public string Unit;   // shared axis unit          (ValueAlign only)
        }

        // The effective unit a member contributes to the shared axis (Time is always seconds).
        private static string UnitOf(Member m) => m.Kind == HorizontalKind.Time ? "s" : m.Unit;

        // Classify a group and, when ValueAlign, return the union value-domain [Left, Right] + unit.
        //  - all None                         -> Stretch (legacy per-trace fill)
        //  - all FFT                          -> Stretch (FFT's own Hz path handles alignment)
        //  - all YT                           -> Stretch (YT's own group-shared time window)
        //  - all Time/Affine, one shared unit  -> ValueAlign over the union of member ranges
        //  - anything else (None mixed with a domain, differing units, FFT or YT mixed with
        //    anything else) -> Incompatible
        // Log-X: a lin-X member cannot share a value->pixel map with a log-X member -> Incompatible.
        // All-log members ValueAlign only when their ranges are IDENTICAL (full-pane sub-windows):
        // SubWindow's linear pixel placement cannot compose with the per-trace log projection.
        public static Domain Classify(IReadOnlyList<Member> members)
        {
            if (members == null || members.Count == 0)
            {
                return new Domain { Mode = HorizontalMode.Stretch, Unit = "" };
            }
            bool allNone = true;
            bool allValue = true;
            bool allFft = true;
            bool allYt = true;
            foreach (var m in members)
            {
                if (m.Kind != HorizontalKind.None) allNone = false;
                if (m.Kind != HorizontalKind.Time && m.Kind != HorizontalKind.Affine) allValue = false;
                if (m.Kind != HorizontalKind.Fft) allFft = false;
                if (m.Kind != HorizontalKind.Yt) allYt = false;
            }
            if (allFft || allYt)
            {
                return new Domain { Mode = HorizontalMode.Stretch, Unit = "" };
            }
            if (allNone)
            {
                foreach (var m in members)
                {
                    if (m.Log != members[0].Log) // lin-X + log-X sample-number traces: one gutter cannot serve both
                    {
                        return new Domain { Mode = HorizontalMode.Incompatible, Unit = "" };
                    }
                    if (m.Left != members[0].Left || m.Right != members[0].Right)
                    {
                        return new Domain { Mode = HorizontalMode.Incompatible, Unit = "" };
                    }
                }
                return new Domain { Mode = HorizontalMode.Stretch, Unit = "" };
            }
            if (!allValue)
            {
                return new Domain { Mode = HorizontalMode.Incompatible, Unit = "" };
            }
            string unit = UnitOf(members[0]);
            bool log = members[0].Log;
            double left = double.PositiveInfinity;
            double right = double.NegativeInfinity;
            foreach (var m in members)
            {
                if (UnitOf(m) != unit)
                {
                    return new Domain { Mode = HorizontalMode.Incompatible, Unit = "" };
                }
                if (m.Log != log) // lin-X grouped with log-X: no shared value->pixel map exists
                {
                    return new Domain { Mode = HorizontalMode.Incompatible, Unit = "" };
                }
                if (m.Left < left) left = m.Left;
                if (m.Right > right) right = m.Right;
            }
            if (log)
            {
                foreach (var m in members)
                {
                    if (m.Left != left || m.Right != right) // ragged log group: linear SubWindow placement would lie
                    {
                        return new Domain { Mode = HorizontalMode.Incompatible, Unit = "" };
                    }
                }
            }
            return new Domain { Mode = HorizontalMode.ValueAlign, Left = left, Right = right, Unit = unit };
        }

        // The visible value window after horizontal zoom/pan
        public static Domain Window(IReadOnlyList<Member> fullMembers, double zoom, double pan)
        {
            Domain d = Classify(fullMembers);
            if (d.Mode != HorizontalMode.ValueAlign)
            {
                return d;
            }
            zoom = Math.Clamp(zoom, 0.0, 1.0);
            pan = Math.Clamp(pan, 0.0, 1.0 - zoom);
            double span = d.Right - d.Left;
            double left = d.Left + span * pan;
            double right = left + span * zoom;
            return new Domain { Mode = HorizontalMode.ValueAlign, Left = left, Right = right, Unit = d.Unit };
        }

        // Pixel x-window a member occupies inside the shared value-domain, mapped linearly onto the pane [paneLeft, paneLeft+paneWidth]
        public static (double left, double width) SubWindow(
            double memberLeft, double memberRight, double hLeft, double hRight, double paneLeft, double paneWidth)
        {
            double span = hRight - hLeft;
            if (span <= 0.0)
            {
                return (paneLeft, paneWidth);
            }
            double pxLeft = paneLeft + (memberLeft - hLeft) / span * paneWidth;
            double pxRight = paneLeft + (memberRight - hLeft) / span * paneWidth;
            return (pxLeft, pxRight - pxLeft);
        }
    }

    [TestClass]
    public class GroupHorizontalTests
    {
        private static GroupHorizontal.Member M(HorizontalKind k, string u, double l, double r, bool log = false) => new(k, u, l, r, log);

        [TestMethod]
        public void AllNoneStretches()
        {
            var d = GroupHorizontal.Classify(new[] { M(HorizontalKind.None, "", 0, 100), M(HorizontalKind.None, "", 0, 100) });
            Assert.AreEqual(HorizontalMode.Stretch, d.Mode);
        }

        [TestMethod]
        public void NoneDifferingCountsIncompatible()
        {
            var d = GroupHorizontal.Classify(new[] { M(HorizontalKind.None, "", 0, 100), M(HorizontalKind.None, "", 0, 50) });
            Assert.AreEqual(HorizontalMode.Incompatible, d.Mode);
        }

        [TestMethod]
        public void AllAffineSameUnitValueAlignsOverUnion()
        {
            var d = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.Affine, "rpm", 0, 6000),
                M(HorizontalKind.Affine, "rpm", 1000, 4000),
            });
            Assert.AreEqual(HorizontalMode.ValueAlign, d.Mode);
            Assert.AreEqual(0.0, d.Left, 1e-9);
            Assert.AreEqual(6000.0, d.Right, 1e-9);
            Assert.AreEqual("rpm", d.Unit);
        }

        [TestMethod]
        public void TimeAndAffineSecondsAreCompatible()
        {
            var d = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.Time, "", 0, 10),      // Time contributes unit "s"
                M(HorizontalKind.Affine, "s", 0, 5),
            });
            Assert.AreEqual(HorizontalMode.ValueAlign, d.Mode);
            Assert.AreEqual("s", d.Unit);
            Assert.AreEqual(10.0, d.Right, 1e-9);
        }

        [TestMethod]
        public void DifferentUnitsIncompatible()
        {
            var d = GroupHorizontal.Classify(new[] { M(HorizontalKind.Affine, "rpm", 0, 100), M(HorizontalKind.Affine, "kph", 0, 50) });
            Assert.AreEqual(HorizontalMode.Incompatible, d.Mode);
        }

        [TestMethod]
        public void NoneMixedWithDomainIncompatible()
        {
            var d = GroupHorizontal.Classify(new[] { M(HorizontalKind.None, "", 0, 100), M(HorizontalKind.Affine, "rpm", 0, 100) });
            Assert.AreEqual(HorizontalMode.Incompatible, d.Mode);
        }

        [TestMethod]
        public void SubWindowFullWhenMemberEqualsDomain()
        {
            var (left, width) = GroupHorizontal.SubWindow(0, 100, 0, 100, paneLeft: 10, paneWidth: 200);
            Assert.AreEqual(10.0, left, 1e-9);
            Assert.AreEqual(200.0, width, 1e-9);
        }

        [TestMethod]
        public void SubWindowSubRangeAlignsByValue()
        {
            // A member covering the left half of the domain occupies the left half of the pane
            var (left, width) = GroupHorizontal.SubWindow(0, 50, 0, 100, paneLeft: 0, paneWidth: 200);
            Assert.AreEqual(0.0, left, 1e-9);
            Assert.AreEqual(100.0, width, 1e-9);
            // A member offset into the middle is placed ragged
            var (left2, width2) = GroupHorizontal.SubWindow(25, 75, 0, 100, paneLeft: 0, paneWidth: 200);
            Assert.AreEqual(50.0, left2, 1e-9);
            Assert.AreEqual(100.0, width2, 1e-9);
        }

        [TestMethod]
        public void WindowAppliesZoomPanToFullDomain()
        {
            var members = new[] { M(HorizontalKind.Affine, "rpm", 0, 200), M(HorizontalKind.Affine, "rpm", 0, 100) };
            // full view
            var full = GroupHorizontal.Window(members, zoom: 1.0, pan: 0.0);
            Assert.AreEqual(HorizontalMode.ValueAlign, full.Mode);
            Assert.AreEqual(0.0, full.Left, 1e-9);
            Assert.AreEqual(200.0, full.Right, 1e-9);
            // zoomed to the middle half: window [50,150]
            var zoomed = GroupHorizontal.Window(members, zoom: 0.5, pan: 0.25);
            Assert.AreEqual(50.0, zoomed.Left, 1e-9);
            Assert.AreEqual(150.0, zoomed.Right, 1e-9);
        }

        [TestMethod]
        public void WindowClampsPanInsideTheDomain()
        {
            var members = new[] { M(HorizontalKind.Affine, "u", 0, 200) };
            var d = GroupHorizontal.Window(members, zoom: 0.5, pan: 0.9);
            Assert.AreEqual(100.0, d.Left, 1e-9);  // pan clamped to 0.5
            Assert.AreEqual(200.0, d.Right, 1e-9); // window ends at the domain edge
        }

        [TestMethod]
        public void WindowNonValueAlignUnchanged()
        {
            var members = new[] { M(HorizontalKind.None, "", 0, 100), M(HorizontalKind.None, "", 0, 100) };
            var d = GroupHorizontal.Window(members, zoom: 0.5, pan: 0.1);
            Assert.AreEqual(HorizontalMode.Stretch, d.Mode);
        }

        [TestMethod]
        public void LinAndLogMembersIncompatible()
        {
            // same unit, same range - but one view is log-X: no shared value->pixel map exists,
            // so this must warn instead of silently drawing a linear gutter under a log curve
            var d = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.Affine, "u", 0, 100, log: false),
                M(HorizontalKind.Affine, "u", 0, 100, log: true),
            });
            Assert.AreEqual(HorizontalMode.Incompatible, d.Mode);
        }

        [TestMethod]
        public void AllLogIdenticalRangesValueAlign()
        {
            // identical ranges -> full-pane sub-windows -> each member's own log projection is the
            // same map, so alignment is safe
            var d = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.Affine, "Hz", 0, 12000, log: true),
                M(HorizontalKind.Affine, "Hz", 0, 12000, log: true),
            });
            Assert.AreEqual(HorizontalMode.ValueAlign, d.Mode);
            Assert.AreEqual(0.0, d.Left, 1e-9);
            Assert.AreEqual(12000.0, d.Right, 1e-9);
        }

        [TestMethod]
        public void AllLogRaggedRangesIncompatible()
        {
            // differing ranges would need a log-composed sub-window; SubWindow is linear -> warn
            var d = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.Affine, "Hz", 0, 12000, log: true),
                M(HorizontalKind.Affine, "Hz", 0, 6000, log: true),
            });
            Assert.AreEqual(HorizontalMode.Incompatible, d.Mode);
        }

        [TestMethod]
        public void NoneMixedLogIncompatible()
        {
            // two plain sample-number traces, one switched to log-X: the shared gutter cannot be
            // right for both -> warn (field report: previously classified Stretch, no warning)
            var d = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.None, "", 0, 100, log: false),
                M(HorizontalKind.None, "", 0, 100, log: true),
            });
            Assert.AreEqual(HorizontalMode.Incompatible, d.Mode);
        }

        [TestMethod]
        public void AllNoneAllLogStillStretches()
        {
            var d = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.None, "", 0, 100, log: true),
                M(HorizontalKind.None, "", 0, 100, log: true),
            });
            Assert.AreEqual(HorizontalMode.Stretch, d.Mode);
        }

        [TestMethod]
        public void FftMixedWithNonFftIncompatible()
        {
            // field report: FFT grouped with a plain trace showed no warning when the FFT trace
            // led the group (the paint layer used to skip classification for FFT leaders)
            var d = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.Fft, "", 0, 12000),
                M(HorizontalKind.None, "", 0, 100),
            });
            Assert.AreEqual(HorizontalMode.Incompatible, d.Mode);
        }

        [TestMethod]
        public void AllYtGroupLeftToItsOwnTimeWindow()
        {
            var d = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.Yt, "", 0, 1000),
                M(HorizontalKind.Yt, "", 0, 600),
            });
            Assert.AreEqual(HorizontalMode.Stretch, d.Mode);
        }

        [TestMethod]
        public void YtMixedWithAnythingIncompatible()
        {
            var ytPlusTime = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.Yt, "", 0, 1000),
                M(HorizontalKind.Time, "", 0, 10),
            });
            Assert.AreEqual(HorizontalMode.Incompatible, ytPlusTime.Mode);
            var ytPlusFft = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.Yt, "", 0, 1000),
                M(HorizontalKind.Fft, "", 0, 4000),
            });
            Assert.AreEqual(HorizontalMode.Incompatible, ytPlusFft.Mode);
            var ytPlusNone = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.Yt, "", 0, 1000),
                M(HorizontalKind.None, "", 0, 500),
            });
            Assert.AreEqual(HorizontalMode.Incompatible, ytPlusNone.Mode);
        }

        [TestMethod]
        public void AllFftGroupLeftToItsOwnHzPath()
        {
            var d = GroupHorizontal.Classify(new[]
            {
                M(HorizontalKind.Fft, "", 0, 12000),
                M(HorizontalKind.Fft, "", 0, 12000),
            });
            Assert.AreEqual(HorizontalMode.Stretch, d.Mode); // no warning, no overrides
        }

        [TestMethod]
        public void SubWindowZeroSpanFallsBackToPane()
        {
            var (left, width) = GroupHorizontal.SubWindow(5, 5, 5, 5, paneLeft: 3, paneWidth: 120);
            Assert.AreEqual(3.0, left, 1e-9);
            Assert.AreEqual(120.0, width, 1e-9);
        }
    }
}
