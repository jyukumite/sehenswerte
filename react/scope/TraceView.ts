// Per-trace display state (a slim port of the TraceView.cs slice the first
// milestone needs): paint mode, colour, visibility, the shared group data
// range (HighestValue/LowestValue), the drawn-window slicing that GetDrawn-
// Samples/DrawnExtents did in the C#, and auto-range. Several views can share
// one TraceData ("new trace (reference)" in the C#).
//
// Not ported yet (backlog): log vertical/horizontal modes, math/FFT/triggers,
// ViewOffsetOverride/ViewLengthOverride trim handles, peak hold, XY modes.

import { roundSignificantDown, roundSignificantUp } from "./axisFormat";
import { GroupMember } from "./groupHorizontal";
import { CurvePaintMode } from "./projection";
import { TraceData } from "./TraceData";

export interface DrawnWindow {
  first: number; // first sample index of the drawn slice
  count: number; // samples in the drawn slice
  leftValue: number; // horizontal value at the slice's left edge
  rightValue: number; // horizontal value one past the slice's right edge
}

export class TraceView {
  data: TraceData;
  viewName: string;
  colour: string;
  visible = true;
  paintMode: CurvePaintMode = "polygonDigital";
  lineWidth = 1;

  // The shared vertical data range; ScopeModel.setGroupHighLow keeps group
  // members in step (SetGroupHighLow in the C#).
  highestValue = 1.0;
  lowestValue = 0.0;

  constructor(data: TraceData, viewName: string, colour: string) {
    this.data = data;
    this.viewName = viewName;
    this.colour = colour;
  }

  // This view's horizontal facts for group classification. left/right are the
  // canonical map's values at the FULL data extent (FullHorizontalAffine):
  // right is one-past-last, matching DrawnExtents.
  groupMember(): GroupMember {
    const count = this.data.inputSampleCount;
    if (this.data.isYTTrace || this.data.isFakeYT) {
      const times = this.data.unixTime;
      const left = times !== null && times.length > 0 ? times[0] : this.data.leftmostUnixTime;
      const right =
        times !== null && times.length > 0
          ? times[times.length - 1]
          : this.data.leftmostUnixTime + count / (this.data.inputSamplesPerSecond || 1);
      return { kind: "yt", unit: "s", left, right };
    }
    return {
      kind: this.data.horizontalKind,
      unit: this.data.horizontalUnitEffective,
      left: this.data.horizontalValueAt(0),
      right: this.data.horizontalValueAt(count),
    };
  }

  // Slice the samples to the visible window.
  // - stretch: plain zoom/pan fractions over the sample count.
  // - valueAlign: the shared [left, right] value window, inverted through the
  //   canonical map (how GetDrawnSamples slices members to the group window).
  drawnWindow(
    mode: "stretch" | "valueAlign",
    windowLeft: number,
    windowRight: number,
    zoom: number,
    pan: number
  ): DrawnWindow {
    const n = this.data.inputSampleCount;
    if (n === 0) {
      return { first: 0, count: 0, leftValue: 0, rightValue: 0 };
    }
    if (mode === "valueAlign") {
      const a = this.data.sampleAtHorizontalValue(windowLeft, n);
      const b = this.data.sampleAtHorizontalValue(windowRight, n);
      const first = Math.max(0, Math.min(n - 1, Math.floor(Math.min(a, b))));
      const last = Math.max(0, Math.min(n - 1, Math.ceil(Math.max(a, b))));
      const count = Math.max(1, last - first + 1);
      return {
        first,
        count,
        leftValue: this.data.horizontalValueAt(first),
        rightValue: this.data.horizontalValueAt(first + count),
      };
    }
    const z = Math.max(0, Math.min(1, zoom));
    const p = Math.max(0, Math.min(1 - z, pan));
    const first = Math.min(n - 1, Math.floor(n * p));
    const count = Math.max(1, Math.min(n - first, Math.ceil(n * z)));
    return {
      first,
      count,
      leftValue: this.data.horizontalValueAt(first),
      rightValue: this.data.horizontalValueAt(first + count),
    };
  }

  // YT slice: the shared unix-time window (the C# YT group path).
  drawnWindowYT(windowLeftTime: number, windowRightTime: number): DrawnWindow {
    const n = this.data.inputSampleCount;
    if (n === 0) {
      return { first: 0, count: 0, leftValue: 0, rightValue: 0 };
    }
    const a = this.data.viewedSampleAtUnixTime(windowLeftTime).index;
    const b = this.data.viewedSampleAtUnixTime(windowRightTime).index;
    const first = Math.max(0, Math.min(n - 1, Math.min(a, b)));
    const last = Math.max(0, Math.min(n - 1, Math.max(a, b) + 1));
    const count = Math.max(1, last - first + 1);
    const times = this.data.unixTime;
    return {
      first,
      count,
      leftValue: times !== null ? times[first] : windowLeftTime,
      rightValue: times !== null ? times[Math.min(n - 1, first + count - 1)] : windowRightTime,
    };
  }

  // Finite extents of a slice (DrawnValueLowest/Highest in the C#).
  sliceExtents(win: DrawnWindow): { lowest: number; highest: number } {
    const samples = this.data.samples;
    let lowest = Infinity;
    let highest = -Infinity;
    const end = Math.min(samples.length, win.first + win.count);
    for (let loop = win.first; loop < end; loop++) {
      const v = samples[loop];
      if (v < lowest) lowest = v;
      if (v > highest) highest = v;
    }
    return lowest > highest ? { lowest: NaN, highest: NaN } : { lowest, highest };
  }
}

// C# TraceView.AutoRange over a group: pad the drawn extents by 10% and round
// outward to 3 significant digits of the range, so the axis lands on tidy
// numbers and never clips data. Returns the shared {high, low} or null when
// no member had finite data.
export function autoRangeGroup(
  views: TraceView[],
  windows: DrawnWindow[]
): { high: number; low: number } | null {
  let high = NaN;
  let low = NaN;
  views.forEach((view, i) => {
    if (!view.visible) return;
    const ext = view.sliceExtents(windows[i]);
    if (!isFinite(ext.lowest)) return;
    let h = ext.highest;
    let l = ext.lowest;
    const pad = (h - l) * 0.1;
    h += pad;
    l -= pad;
    if (isNaN(high) || h > high) high = roundSignificantUp(h, 3, h - l);
    if (isNaN(low) || l < low) low = roundSignificantDown(l, 3, h - l);
  });
  if (isNaN(high) || isNaN(low)) return null;
  if (high === low) {
    // flat trace: give it a visible band
    high = high + (high === 0 ? 1 : Math.abs(high) * 0.1);
    low = low - (low === 0 ? 1 : Math.abs(low) * 0.1);
  }
  return { high, low };
}
