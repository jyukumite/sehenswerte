// Port of sehenswerte GroupHorizontal (C#, src/sehens/paint/GroupHorizontal.cs):
// pure classification + sub-window math for grouped-trace horizontal alignment.
// Kept free of view/paint objects so it is unit-testable headlessly; the paint
// layer supplies members from each trace's drawn extents (the canonical
// sample->value map, TraceData.HorizontalValueAt in the C#) and applies the
// resulting subWindow as the trace's pixel x-range (the ValueRect).

export type HorizontalKind =
  | 'none' // no explicit axis, no sample rate - intent is "stretch to fill the pane"
  | 'time' // samples-per-second != 0; value is seconds (unit "s"). SPS is affine (mult 1/sps).
  | 'affine' // horizontal affine (offset, multiplier, unit); value = multiplier*(sample+offset)
  | 'fft' // frequency (Hz) - its own painter/axis path
  | 'yt'; // draws as YT (unix time) - its own group-shared time window path

export type HorizontalMode =
  | 'stretch' // no real axis: each trace fills the pane width independently (legacy behaviour)
  | 'valueAlign' // all members share a compatible value axis: position by value in a shared domain
  | 'incompatible'; // members cannot converge (mixed kinds / units): fall back to the leader's axis

// One group member's horizontal facts (left/right are the axis VALUES at its drawn endpoints).
export interface GroupMember {
  kind: HorizontalKind;
  unit: string;
  left: number;
  right: number;
  // The view's log-X flag - a display transform, but it changes the value->pixel map.
  log?: boolean;
}

export interface HorizontalDomain {
  mode: HorizontalMode;
  left: number; // shared value-domain left  (valueAlign only)
  right: number; // shared value-domain right (valueAlign only)
  unit: string; // shared axis unit          (valueAlign only)
}

export interface SubWindowRect {
  left: number;
  width: number;
}

// The effective unit a member contributes to the shared axis (time is always seconds).
function unitOf(m: GroupMember): string {
  return m.kind === 'time' ? 's' : m.unit || '';
}

function logOf(m: GroupMember): boolean {
  return !!m.log;
}

function clamp(v: number, min: number, max: number): number {
  return v < min ? min : v > max ? max : v;
}

// Classify a group and, when valueAlign, return the union value-domain [left, right] + unit.
//  - all none                          -> stretch (legacy per-trace fill)
//  - all fft                           -> stretch (FFT's own Hz path handles alignment)
//  - all yt                            -> stretch (YT's own group-shared time window)
//  - all time/affine, one shared unit  -> valueAlign over the union of member ranges
//  - anything else (none mixed with a domain, differing units, fft or yt mixed with
//    anything else) -> incompatible
// Log-X: a lin-X member cannot share a value->pixel map with a log-X member -> incompatible.
// All-log members valueAlign only when their ranges are IDENTICAL (full-pane sub-windows):
// subWindow's linear pixel placement cannot compose with the per-trace log projection.
export function classify(members: readonly GroupMember[]): HorizontalDomain {
  if (!members || members.length === 0) {
    return { mode: 'stretch', left: 0, right: 0, unit: '' };
  }
  let allNone = true;
  let allValue = true;
  let allFft = true;
  let allYt = true;
  for (const m of members) {
    if (m.kind !== 'none') allNone = false;
    if (m.kind !== 'time' && m.kind !== 'affine') allValue = false;
    if (m.kind !== 'fft') allFft = false;
    if (m.kind !== 'yt') allYt = false;
  }
  if (allFft || allYt) {
    return { mode: 'stretch', left: 0, right: 0, unit: '' };
  }
  if (allNone) {
    for (const m of members) {
      // lin-X + log-X sample-number traces: one gutter cannot serve both
      if (logOf(m) !== logOf(members[0])) {
        return { mode: 'incompatible', left: 0, right: 0, unit: '' };
      }
      if (m.left !== members[0].left || m.right !== members[0].right) {
        return { mode: 'incompatible', left: 0, right: 0, unit: '' };
      }
    }
    return { mode: 'stretch', left: 0, right: 0, unit: '' };
  }
  if (!allValue) {
    return { mode: 'incompatible', left: 0, right: 0, unit: '' };
  }
  const unit = unitOf(members[0]);
  const log = logOf(members[0]);
  let left = Number.POSITIVE_INFINITY;
  let right = Number.NEGATIVE_INFINITY;
  for (const m of members) {
    if (unitOf(m) !== unit) {
      return { mode: 'incompatible', left: 0, right: 0, unit: '' };
    }
    if (logOf(m) !== log) {
      // lin-X grouped with log-X: no shared value->pixel map exists
      return { mode: 'incompatible', left: 0, right: 0, unit: '' };
    }
    if (m.left < left) left = m.left;
    if (m.right > right) right = m.right;
  }
  if (log) {
    for (const m of members) {
      // ragged log group: linear subWindow placement would lie
      if (m.left !== left || m.right !== right) {
        return { mode: 'incompatible', left: 0, right: 0, unit: '' };
      }
    }
  }
  return { mode: 'valueAlign', left, right, unit };
}

// The visible value window after horizontal zoom/pan. Pan is clamped to
// [0, 1 - zoom] (the LEFT-edge fraction ceiling) - a bare [0, 1] clamp lets a
// drag over-pan past the data and snap back on release.
// (C# GroupHorizontal.Window; renamed so the export cannot shadow the DOM global.)
export function valueWindow(
  fullMembers: readonly GroupMember[],
  zoom: number,
  pan: number
): HorizontalDomain {
  const d = classify(fullMembers);
  if (d.mode !== 'valueAlign') {
    return d;
  }
  const z = clamp(zoom, 0.0, 1.0);
  const p = clamp(pan, 0.0, 1.0 - z);
  const span = d.right - d.left;
  const left = d.left + span * p;
  const right = left + span * z;
  return { mode: 'valueAlign', left, right, unit: d.unit };
}

// Pixel x-window a member occupies inside the shared value-domain, mapped
// linearly onto the pane [paneLeft, paneLeft + paneWidth].
export function subWindow(
  memberLeft: number,
  memberRight: number,
  hLeft: number,
  hRight: number,
  paneLeft: number,
  paneWidth: number
): SubWindowRect {
  const span = hRight - hLeft;
  if (span <= 0.0) {
    return { left: paneLeft, width: paneWidth };
  }
  const pxLeft = paneLeft + ((memberLeft - hLeft) / span) * paneWidth;
  const pxRight = paneLeft + ((memberRight - hLeft) / span) * paneWidth;
  return { left: pxLeft, width: pxRight - pxLeft };
}
