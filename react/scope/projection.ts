// Sample-to-pixel projection (decimation) pipeline for the scope, ported from the C#
// sehenswerte painters:
//
//   Paint2dTrace.Project2dCurves           -> projectCurves (mode dispatch)
//   Paint2dTrace.Projection2dMinMax        -> projectMinMax
//   Paint2dTrace.Projection2dNormal        -> projectNearest
//   Paint2dTrace.Projection2dInterpolate   -> projectInterpolate
//   Paint2dTrace.Projection2dAverage       -> projectAverage
//   Paint2dTrace.ProjectionDots            -> projectDots
//   Paint2dTrace.ProjectPolygon            -> envelopePolygon
//   Paint2dTrace.LogPixelToFracSampleIndex -> logPixelToFracSampleIndex
//   Paint2dTrace.LogPixelToSampleIndex     -> logPixelToSampleIndex
//   Paint2dTrace.LogSampleIndexToXOffset   -> logSampleIndexToXOffset
//   PaintTraceBase.ProjectLog              -> projectLog (+ projectLogInverse)
//   PaintTraceBase.LogHEffectiveLeft       -> logHEffectiveLeft
//   PaintTraceBase.LogHValueToFraction     -> logHValueToFraction
//   PaintTraceBase.LogHFractionToValue     -> logHFractionToValue
//
// Invariants carried over from the C# (see sehenswerte agents.md, "Painter Pipeline" and
// "Axis log scaling"):
//
// - One output value per horizontal pixel column. Column i of a dense projection covers the
//   sample bin [floor(i*count/width), floor((i+1)*count/width)); an empty bin is forced to one
//   sample (endIndex = startIndex + 1). The C# does the bin math in 64-bit integers to avoid
//   32-bit overflow; JS doubles are exact for the products involved (pixel * count stays far
//   below 2^53), so plain Math.floor reproduces the truncating integer division.
// - This is the expensive resample/decimate step. The C# runs it only when
//   SnapshotReprojectionRequired and caches the result; the cache is keyed on data + log-H
//   values, NOT on the target rect, so a pixel-width (ValueRect) change MUST force a
//   reprojection or the trace draws at a stale width. Callers here must key their cache on
//   (data generation, first, count, pixelWidth, log-H window) the same way.
// - Log-X: the pixel<->sample-index<->value maps below are the ONE shared implementation.
//   Gutter tick positions, the drawn curve, and the hover readout must all go through
//   logHEffectiveLeft / logHValueToFraction / logHFractionToValue (and the pixel<->index
//   wrappers) or they disagree about where a frequency sits. Tick POSITIONS always follow the
//   log mapping when the view is log-X, even when the labels are bare sample numbers.
// - The log axis spans at most LOG_HORIZONTAL_MAX_DECADES decades down from the right edge;
//   content further left is off-screen (logHEffectiveLeft caps the left edge).
// - projectLog compresses `staves` decades (default 2); values below maxInput / base^staves
//   clamp to output 0. The inverse is input = maxInput * base^(output - newMax).
// - NaN gap samples pass through: a dense min/max bin whose FIRST sample is NaN projects NaN
//   (NaN never wins a `<`/`>` comparison against a finite starting value, so a later NaN in
//   the bin does not poison it - exact C# comparison semantics). envelopePolygon skips
//   non-finite values (C# fixme carried over: gaps are skipped, not split into polygons).
//
// Deliberate deviations from the C#:
// - Outputs are in the sample VALUE domain (Float64Array, one Y value per pixel). The C#
//   projects through Project() to screen-space Y as it goes; value -> pixel-Y mapping (linear
//   or projectLog) is the caller's job here.
// - The C# guard "PaintHighestValue == PaintLowestValue -> no projection" exists only because
//   the screen-space Y mapping would divide by zero; it is not reproduced.
// - The C# tracks drawn extents (PaintLowestY/PaintHighestY) as a side effect; scan the
//   returned arrays instead (drawnExtents helper).

export type ProjectionMode = "minMax" | "nearest" | "interpolate" | "average";

// Trace paint modes that reach the Project2dCurves dispatch (the pure subset; the C#
// Spectral/PeakHold variants reuse projectMinMax/projectNearest over prepared arrays).
export type CurvePaintMode =
  | "polygonDigital"
  | "polygonContinuous"
  | "points"
  | "pointsIfChanged"
  | "average"
  | "min"
  | "max";

// The horizontal-axis value window of the projected samples (PaintLeftHValue /
// PaintRightHValue in the C#): sample 0 of the window sits at leftValue, sample count-1 at
// rightValue. Log-X mapping is active only when rightValue > 0 (C# UseLogH).
export interface LogAxisWindow {
  leftValue: number;
  rightValue: number;
}

export interface DotProjection {
  x: Int32Array; // pixel offset from the left edge, ascending, no duplicates
  y: Float64Array; // sample value at that dot
}

export interface EnvelopePolygon {
  x: Int32Array; // pixel offsets: min envelope forward, then max envelope reversed
  y: Float64Array;
}

export interface ProjectedCurves {
  mode?: ProjectionMode; // which projection the dense/sparse dispatch selected
  min?: Float64Array; // dense envelope (mode "minMax")
  max?: Float64Array;
  line?: Float64Array; // single-curve projection (other modes)
  dots?: DotProjection; // points / pointsIfChanged modes only
}

// Most decades shown on the log horizontal axis (C# PaintTraceBase.LogHorizontalMaxDecades).
export const LOG_HORIZONTAL_MAX_DECADES = 3;

function fixToRange(num: number, low: number, high: number): number {
  // NaN passes through (both comparisons false), matching the C# DoubleExtensions.FixToRange.
  return num < low ? low : num > high ? high : num;
}

// ---------------------------------------------------------------------------
// Log-Y compression (C# PaintTraceBase.ProjectLog)

// Compresses `staves` decades (default 2) into output range [0, staves]. Values below
// maxInput / base^staves clamp to 0. newMax is always `staves`.
export function projectLog(
  maxInput: number,
  input: number,
  staves: number = 2,
  logBase: number = 10
): { newMax: number; output: number } {
  const pow = Math.pow(logBase, staves);
  const scaled = (input * pow) / maxInput;
  const output = scaled < 1.0 ? 0.0 : fixToRange(Math.log(scaled) / Math.log(logBase), 0.0, staves);
  return { newMax: staves, output };
}

// Inverse of projectLog for outputs in (0, staves]: input = maxInput * base^(output - staves).
// output 0 maps to the clamp threshold maxInput / base^staves (everything at or below the
// threshold collapsed to 0, so the inverse there is a floor, not an exact round trip).
export function projectLogInverse(
  maxInput: number,
  output: number,
  staves: number = 2,
  logBase: number = 10
): number {
  return maxInput * Math.pow(logBase, output - staves);
}

// ---------------------------------------------------------------------------
// Log-X axis maps (C# PaintTraceBase + Paint2dTrace). These are the canonical shared maps:
// gutter, projection, and hover must all use them so they agree.

// Left edge of the visible log axis. Uses the data's left value when positive, else the first
// sample's value (right / (length - 1)), capped to at most LOG_HORIZONTAL_MAX_DECADES decades
// below the right edge.
export function logHEffectiveLeft(left: number, right: number, length: number): number {
  if (right <= 0) return right;
  const dataLeft = left > 0 ? left : length > 1 ? right / (length - 1) : right * 0.01;
  const cappedLeft = right * Math.pow(10.0, -LOG_HORIZONTAL_MAX_DECADES);
  return Math.max(dataLeft, cappedLeft);
}

// value on [effectiveLeft, right] -> fraction 0..1 across the plot width.
export function logHValueToFraction(value: number, effectiveLeft: number, right: number): number {
  if (effectiveLeft <= 0 || right <= effectiveLeft || value <= effectiveLeft) return 0.0;
  return (Math.log10(value) - Math.log10(effectiveLeft)) / (Math.log10(right) - Math.log10(effectiveLeft));
}

// Inverse of logHValueToFraction: fraction 0..1 -> value on [effectiveLeft, right].
export function logHFractionToValue(fraction: number, effectiveLeft: number, right: number): number {
  if (effectiveLeft <= 0 || right <= effectiveLeft) return effectiveLeft;
  return effectiveLeft * Math.pow(10.0, fraction * (Math.log10(right) - Math.log10(effectiveLeft)));
}

// Pixel (0..pixelWidth-1) -> fractional sample index via the inverse log-H mapping. The axis
// spans the full data extent [effectiveLeft, right].
export function logPixelToFracSampleIndex(
  pixel: number,
  pixelWidth: number,
  count: number,
  axis: LogAxisWindow
): number {
  const range = axis.rightValue - axis.leftValue;
  if (range <= 0) return 0.0;
  const effectiveLeft = logHEffectiveLeft(axis.leftValue, axis.rightValue, count);
  const val = logHFractionToValue(pixel / pixelWidth, effectiveLeft, axis.rightValue);
  return ((val - axis.leftValue) / range) * (count - 1);
}

// Pixel -> integer sample index, clamped to [0, count-1].
export function logPixelToSampleIndex(
  pixel: number,
  pixelWidth: number,
  count: number,
  axis: LogAxisWindow
): number {
  const frac = logPixelToFracSampleIndex(pixel, pixelWidth, count, axis);
  return Math.max(0, Math.min(count - 1, Math.trunc(frac)));
}

// Sample index -> pixel offset from the left edge in log-X mode.
export function logSampleIndexToXOffset(
  sampleIndex: number,
  count: number,
  pixelWidth: number,
  axis: LogAxisWindow
): number {
  const range = axis.rightValue - axis.leftValue;
  if (range <= 0) return 0;
  const val = axis.leftValue + (sampleIndex / Math.max(1, count - 1)) * range;
  const effectiveLeft = logHEffectiveLeft(axis.leftValue, axis.rightValue, count);
  return Math.trunc(logHValueToFraction(val, effectiveLeft, axis.rightValue) * pixelWidth);
}

// ---------------------------------------------------------------------------
// Projections. All take the samples array plus a [first, first+count) window (the zoomed-in
// view) and produce one value per pixel column. Empty windows fill with NaN.

function isLogHActive(axis: LogAxisWindow | undefined): axis is LogAxisWindow {
  // C# UseLogH: log only when the view is log-X AND the right H value is positive.
  return axis !== undefined && axis.rightValue > 0;
}

// Dense min/max envelope (C# Projection2dMinMax): reduce each per-pixel bin to its min (or
// max). Bin [floor(i*count/width), floor((i+1)*count/width)), empty bin -> one sample. A bin
// whose first sample is NaN projects NaN (gap preserved).
export function projectMinMax(
  samples: Float64Array,
  first: number,
  count: number,
  pixelWidth: number,
  min: boolean,
  logAxis?: LogAxisWindow
): Float64Array {
  if (pixelWidth <= 0) return new Float64Array(0);
  const out = new Float64Array(pixelWidth);
  if (count <= 0) {
    out.fill(NaN);
    return out;
  }
  const logH = isLogHActive(logAxis);
  for (let pixel = 0; pixel < pixelWidth; pixel++) {
    const startIndex = logH
      ? logPixelToSampleIndex(pixel, pixelWidth, count, logAxis)
      : Math.floor((pixel * count) / pixelWidth);
    let endIndex = logH
      ? logPixelToSampleIndex(pixel + 1, pixelWidth, count, logAxis)
      : Math.floor(((pixel + 1) * count) / pixelWidth);
    if (endIndex <= startIndex) endIndex = startIndex + 1;
    let y = samples[first + startIndex];
    for (let index = startIndex; index < endIndex && index < count; index++) {
      const sample = samples[first + index];
      y = min ? (sample < y ? sample : y) : sample > y ? sample : y;
    }
    out[pixel] = y;
  }
  return out;
}

// Sparse nearest-sample projection (C# Projection2dNormal): one sample picked per pixel by
// truncating index math (or the log pixel->index map).
export function projectNearest(
  samples: Float64Array,
  first: number,
  count: number,
  pixelWidth: number,
  logAxis?: LogAxisWindow
): Float64Array {
  if (pixelWidth <= 0) return new Float64Array(0);
  const out = new Float64Array(pixelWidth);
  if (count <= 0) {
    out.fill(NaN);
    return out;
  }
  const logH = isLogHActive(logAxis);
  for (let pixel = 0; pixel < pixelWidth; pixel++) {
    const index = logH
      ? logPixelToSampleIndex(pixel, pixelWidth, count, logAxis)
      : Math.floor((pixel * count) / pixelWidth);
    out[pixel] = samples[first + index];
  }
  return out;
}

// Sparse linear interpolation (C# Projection2dInterpolate), for continuous/points modes.
// The prevIndex/ratio-reset guard makes the FIRST pixel that lands on a new sample index
// project that sample's exact value (ratio forced to 0); later pixels on the same index
// interpolate towards the next sample. The right neighbour clamps to the last sample.
export function projectInterpolate(
  samples: Float64Array,
  first: number,
  count: number,
  pixelWidth: number,
  logAxis?: LogAxisWindow
): Float64Array {
  if (pixelWidth <= 0) return new Float64Array(0);
  const out = new Float64Array(pixelWidth);
  if (count <= 0) {
    out.fill(NaN);
    return out;
  }
  const logH = isLogHActive(logAxis);
  let prevIndex = -1;
  for (let pixel = 0; pixel < pixelWidth; pixel++) {
    const frac = logH
      ? logPixelToFracSampleIndex(pixel, pixelWidth, count, logAxis)
      : (pixel * count) / pixelWidth;
    const indexLeft = Math.trunc(frac);
    let ratio = frac - indexLeft;
    const indexRight = indexLeft + 1;
    if (prevIndex !== indexLeft) {
      ratio = 0.0;
      prevIndex = indexLeft;
    }
    const leftSample = samples[first + Math.max(0, Math.min(count - 1, indexLeft))];
    const rightSample = indexRight >= count ? samples[first + count - 1] : samples[first + indexRight];
    out[pixel] = leftSample * (1.0 - ratio) + rightSample * ratio;
  }
  return out;
}

// Per-pixel-bin mean (C# Projection2dAverage). Same bin math as projectMinMax; note the C#
// divides by the UNCLIPPED bin size (endIndex - startIndex) even though the sum loop clips at
// the window end - preserved here (only reachable through the empty-bin guard at the last
// sample, where the two agree). A NaN anywhere in the bin makes the mean NaN.
export function projectAverage(
  samples: Float64Array,
  first: number,
  count: number,
  pixelWidth: number,
  logAxis?: LogAxisWindow
): Float64Array {
  if (pixelWidth <= 0) return new Float64Array(0);
  const out = new Float64Array(pixelWidth);
  if (count <= 0) {
    out.fill(NaN);
    return out;
  }
  const logH = isLogHActive(logAxis);
  for (let pixel = 0; pixel < pixelWidth; pixel++) {
    const startIndex = logH
      ? logPixelToSampleIndex(pixel, pixelWidth, count, logAxis)
      : Math.floor((pixel * count) / pixelWidth);
    let endIndex = logH
      ? logPixelToSampleIndex(pixel + 1, pixelWidth, count, logAxis)
      : Math.floor(((pixel + 1) * count) / pixelWidth);
    if (endIndex <= startIndex) endIndex = startIndex + 1;
    let sum = 0.0;
    for (let index = startIndex; index < endIndex && index < count; index++) {
      sum += samples[first + index];
    }
    out[pixel] = sum / (endIndex - startIndex);
  }
  return out;
}

// Dot positions for Points / PointsIfChanged (C# ProjectionDots). Walks the samples (not the
// pixels): sample 0 is always skipped; a dot is added only when its pixel X differs from the
// last ADDED dot's X, its value is inside [lowestValue, highestValue], and - for
// points-if-changed - its value differs from the previous SAMPLE's value.
export function projectDots(
  samples: Float64Array,
  first: number,
  count: number,
  pixelWidth: number,
  skipUnchangedY: boolean,
  lowestValue: number,
  highestValue: number,
  logAxis?: LogAxisWindow
): DotProjection {
  const xs: number[] = [];
  const ys: number[] = [];
  if (pixelWidth >= 0 && count > 0) {
    const logH = isLogHActive(logAxis);
    let prevX = -1;
    let prevY = 0.0;
    for (let loop = 0; loop < count; loop++) {
      const x = logH
        ? logSampleIndexToXOffset(loop, count, pixelWidth, logAxis)
        : Math.floor((loop * pixelWidth) / count);
      const y = samples[first + loop];
      const add = loop !== 0 && (y !== prevY || !skipUnchangedY) && x !== prevX;
      prevY = y;
      if (add && y >= lowestValue && y <= highestValue) {
        xs.push(x);
        ys.push(y);
        prevX = x;
      }
    }
  }
  return { x: Int32Array.from(xs), y: Float64Array.from(ys) };
}

// Assemble the filled band between the min and max envelopes (C# ProjectPolygon): min
// envelope forward, then max envelope reversed, skipping non-finite values. X values are
// pixel offsets (array indices). C# fixme carried over: embedded NaN gaps are skipped, not
// split into separate polygons.
export function envelopePolygon(minValues: Float64Array, maxValues: Float64Array): EnvelopePolygon {
  const xs: number[] = [];
  const ys: number[] = [];
  for (let loop = 0; loop < minValues.length; loop++) {
    if (Number.isFinite(minValues[loop])) {
      xs.push(loop);
      ys.push(minValues[loop]);
    }
  }
  for (let loop = maxValues.length - 1; loop >= 0; loop--) {
    if (Number.isFinite(maxValues[loop])) {
      xs.push(loop);
      ys.push(maxValues[loop]);
    }
  }
  return { x: Int32Array.from(xs), y: Float64Array.from(ys) };
}

// Convenience: finite extents of a projected array (the C# tracks PaintLowestY/PaintHighestY
// as a projection side effect; here scan the output). Returns NaN/NaN when nothing is finite.
export function drawnExtents(values: Float64Array): { lowest: number; highest: number } {
  let lowest = Infinity;
  let highest = -Infinity;
  for (let loop = 0; loop < values.length; loop++) {
    const y = values[loop];
    if (y < lowest) lowest = y;
    if (y > highest) highest = y;
  }
  return lowest > highest ? { lowest: NaN, highest: NaN } : { lowest, highest };
}

// Mode dispatch (C# Project2dCurves, minus the screen-space/paint parts):
// - dense (pixelWidth < count) digital/continuous/points -> min + max envelope ("minMax");
// - sparse (pixelWidth > count) -> interpolate for continuous/points modes, else nearest;
// - otherwise (pixelWidth == count, or dense min/max/average) -> minMax for "min"/"max",
//   average for everything else (the C# Projection2d default; with one-sample bins at
//   pixelWidth == count this is the identity).
// Points modes ALSO build the dot list (the C# keeps the envelope/line for the cache and
// draws the dots). PeakHold is not a mode here: the C# projects min over the peak-hold-min
// array and max over the peak-hold-max array - call projectMinMax twice.
// dotsRange is the visible value range filter for dots (C# PaintLowestValue/PaintHighestValue).
export function projectCurves(
  samples: Float64Array,
  first: number,
  count: number,
  pixelWidth: number,
  mode: CurvePaintMode,
  logAxis?: LogAxisWindow,
  dotsRange?: { lowestValue: number; highestValue: number }
): ProjectedCurves {
  if (count <= 0 || pixelWidth <= 0) return {};

  const dots = mode === "points" || mode === "pointsIfChanged";
  const interpolate = mode === "polygonContinuous" || dots;
  const result: ProjectedCurves = {};

  if ((mode === "polygonDigital" || interpolate) && pixelWidth < count) {
    result.mode = "minMax";
    result.min = projectMinMax(samples, first, count, pixelWidth, true, logAxis);
    result.max = projectMinMax(samples, first, count, pixelWidth, false, logAxis);
  } else if (pixelWidth > count) {
    if (interpolate) {
      result.mode = "interpolate";
      result.line = projectInterpolate(samples, first, count, pixelWidth, logAxis);
    } else {
      result.mode = "nearest";
      result.line = projectNearest(samples, first, count, pixelWidth, logAxis);
    }
  } else if (mode === "min" || mode === "max") {
    result.mode = "minMax";
    result.line = projectMinMax(samples, first, count, pixelWidth, mode === "min", logAxis);
  } else {
    result.mode = "average";
    result.line = projectAverage(samples, first, count, pixelWidth, logAxis);
  }

  if (dots) {
    const lowest = dotsRange ? dotsRange.lowestValue : -Infinity;
    const highest = dotsRange ? dotsRange.highestValue : Infinity;
    result.dots = projectDots(
      samples,
      first,
      count,
      pixelWidth,
      mode === "pointsIfChanged",
      lowest,
      highest,
      logAxis
    );
  }
  return result;
}
