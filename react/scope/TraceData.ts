// Port of sehenswerte TraceData (C#, src/sehens/data/TraceData.cs) and TraceFeature
// (src/sehens/data/TraceFeature.cs): the samples and sample-side metadata for one named
// trace channel - input samples (array or ring), optional per-sample unix times (real YT),
// sample rate, horizontal axis terms, features, axis titles/units. It knows nothing about
// painting: display state lives in the view layer, and many views may display one
// TraceData (subscribe() replaces the C# viewer-callback list).
//
// Scope - the DATA layer only:
// - No painting, no WinForms, no viewer/ITraceView registration, no calculated-trace
//   machinery, no FFT, and no stopped-data (StopUpdates) clone - "viewed" data here is
//   always the live input.
// - No locks: JS is single-threaded. The C# threading model (every mutation and snapshot
//   under DataLock, callbacks synchronous on the caller's thread) reduces to synchronous
//   notify(). samplesGeneration keeps its C# job regardless: a consumer that pairs a
//   snapshot with the generation can detect that the samples changed mid-calculation
//   (e.g. across an await) and DISCARD its stale result instead of publishing it over a
//   fresher one. Any new sample-mutation path must bump it.
//
// NaN samples are GAPS by contract: they paint as breaks in the trace, statistics skip
// them, and the hover label is suppressed over them.
//
// Deliberate deviations from the C# (also noted inline):
// - AppendRing's sample-rate guard in the C# tests the CURRENT rate for finiteness
//   instead of the incoming one (a transposed check: calling it without a rate stores
//   NaN as the rate, which then poisons horizontalValueAt and blocks all later rate
//   updates). Ported with Update's intended guard: adopt only a finite incoming rate.
// - The YT appendRing overload throws NotImplementedException in the C#; here it is a
//   real bounded append keeping the most recent ringLength (sample, time) pairs.
// - clear() bumps samplesGeneration (the C# misses it; the repo rule is that EVERY
//   sample-mutation path must bump it under the data lock).
// - Statistics: the C# gates each figure behind a "was set" flag purely for WinForms
//   hover-header display; here every figure is computed, with NaN standing in for "not
//   computable" (no finite samples / no time array) instead of the C# unset default 0.

import type { HorizontalKind } from "./groupHorizontal";

// ---------------------------------------------------------------------------
// TraceFeature (C# src/sehens/data/TraceFeature.cs)

export type TraceFeatureType =
  | "text"
  | "gutterText"
  | "line"
  | "highlight"
  | "leftHandle"
  | "rightHandle"
  | "triggerHandle";

// Where a text feature's anchor Y comes from.
//   centre - pixel-space centre of the plot rectangle. Ignores verticalPosition.
//            Default; reproduces the legacy mid-trace placement.
//   y      - value-space: verticalPosition is a literal Y value, projected through the
//            painter's linear/log Y mapping.
//   sample - value-space: the sample value at sampleNumber, projected through the same
//            Y mapping so the label rides the trace.
export type FeatureVerticalAnchor = "centre" | "y" | "sample";

// Where the text's bounding box sits relative to that anchor Y. For rotated text
// (e.g. angle = -90, reading bottom-to-top), top/bottom refer to the rotated bbox edges
// in screen space, not to the first/last character of the string.
export type FeatureVerticalJustify = "top" | "middle" | "bottom";

export interface TraceFeature {
  type: TraceFeatureType;
  sampleNumber: number; // anchor
  rightSampleNumber: number; // for spans
  unixTime: number; // for YT traces
  rightUnixTime: number;
  text: string;
  colour: string | null; // CSS colour; null = skin default
  angle: number; // -90 is vertical bottom-to-top (the default)
  verticalAnchor: FeatureVerticalAnchor;
  verticalPosition: number;
  verticalJustify: FeatureVerticalJustify;
}

export function newTraceFeature(init?: Partial<TraceFeature>): TraceFeature {
  return {
    type: "text",
    sampleNumber: 0,
    rightSampleNumber: 0,
    unixTime: 0,
    rightUnixTime: 0,
    text: "",
    colour: null,
    angle: -90,
    verticalAnchor: "centre",
    verticalPosition: 0,
    verticalJustify: "middle",
    ...init,
  };
}

// Sort order used by the feature list (C# TraceFeature.FeatureCompare): unix time,
// then sample number, then feature-type declaration order.
const FEATURE_TYPE_ORDER: readonly TraceFeatureType[] = [
  "text",
  "gutterText",
  "line",
  "highlight",
  "leftHandle",
  "rightHandle",
  "triggerHandle",
];

export function compareFeatures(left: TraceFeature, right: TraceFeature): number {
  const byTime = left.unixTime - right.unixTime;
  if (byTime !== 0) return byTime < 0 ? -1 : 1;
  const bySample = left.sampleNumber - right.sampleNumber;
  if (bySample !== 0) return bySample;
  return (
    FEATURE_TYPE_ORDER.indexOf(left.type) - FEATURE_TYPE_ORDER.indexOf(right.type)
  );
}

// ---------------------------------------------------------------------------
// Statistics (C# TraceData.Statistics over SehensWerte.Maths.Statistics)

export interface TraceStatistics {
  min: number;
  max: number;
  range: number;
  average: number;
  stdDev: number;
  sum: number;
  count: number; // TOTAL samples - NaN gaps still count as drawn samples
  lastInput: number; // last FINITE sample
  timeStdDev: number; // std-dev of the unix times (all of them, unfiltered)
}

// C# Maths.Statistics.Variance: population variance with guards - constant data
// (range 0) reports exactly 0 rather than negative rounding noise.
function varianceOf(sum: number, sumSquares: number, count: number, range: number): number {
  let result = 0;
  if (count > 0 && range !== 0 && sumSquares > 0) {
    const mean = sum / count;
    result = sumSquares / count - mean * mean;
  }
  return result;
}

// Gap contract: NaN (and +/-Inf) samples are skipped for the value figures - gaps must
// not poison the stats header to Min=NaN,Max=NaN,... - but count is the TOTAL sample
// count. The C# flag-gating of individual figures is WinForms-display-only (which lines
// the hover header shows), so all figures are computed here; NaN marks "not computable".
export function computeStatistics(
  samples: ArrayLike<number>,
  unixTime?: ArrayLike<number> | null
): TraceStatistics {
  let min = NaN;
  let max = NaN;
  let sum = 0;
  let sumSquares = 0;
  let finiteCount = 0;
  let lastInput = NaN;
  for (let loop = 0; loop < samples.length; loop++) {
    const value = samples[loop];
    if (!Number.isFinite(value)) continue; // NaN gap: skip
    if (finiteCount === 0) {
      min = value;
      max = value;
    } else {
      min = value < min ? value : min;
      max = value > max ? value : max;
    }
    sum += value;
    sumSquares += value * value;
    lastInput = value;
    finiteCount++;
  }

  let timeStdDev = NaN;
  if (unixTime !== undefined && unixTime !== null) {
    let tSum = 0;
    let tSumSquares = 0;
    let tMin = 0;
    let tMax = 0;
    for (let loop = 0; loop < unixTime.length; loop++) {
      const value = unixTime[loop];
      if (loop === 0) {
        tMin = value;
        tMax = value;
      } else {
        tMin = value < tMin ? value : tMin;
        tMax = value > tMax ? value : tMax;
      }
      tSum += value;
      tSumSquares += value * value;
    }
    timeStdDev = Math.sqrt(varianceOf(tSum, tSumSquares, unixTime.length, tMax - tMin));
  }

  const none = finiteCount === 0;
  return {
    min,
    max,
    range: none ? NaN : max - min,
    average: none ? NaN : sum / finiteCount,
    stdDev: none ? NaN : Math.sqrt(varianceOf(sum, sumSquares, finiteCount, max - min)),
    sum: none ? NaN : sum,
    count: samples.length,
    lastInput,
    timeStdDev,
  };
}

// ---------------------------------------------------------------------------
// Ring storage (C# SehensWerte.Maths.Ring<double> as consumed by TraceData: reads go
// through CopyToDoubleArray -> Ring.AllSamples(), which returns the FULL capacity-length
// window ending at the newest sample - never a shorter "valid" prefix).

class DoubleRing {
  readonly capacity: number;
  private buffer: Float64Array;
  private head = 0; // next write position == oldest slot

  constructor(capacity: number) {
    this.capacity = Math.max(0, capacity);
    this.buffer = new Float64Array(this.capacity);
  }

  // C# Ring.Set: fill every slot, so the window is full from the first append.
  set(value: number): void {
    this.buffer.fill(value);
    this.head = 0;
  }

  insert(values: ArrayLike<number>): void {
    if (this.capacity === 0) return;
    for (let loop = 0; loop < values.length; loop++) {
      this.buffer[this.head] = values[loop];
      this.head = (this.head + 1) % this.capacity;
    }
  }

  // C# Ring.AllSamples: the last `capacity` values written, oldest first.
  window(): Float64Array {
    const out = new Float64Array(this.capacity);
    for (let loop = 0; loop < this.capacity; loop++) {
      out[loop] = this.buffer[(this.head + loop) % this.capacity];
    }
    return out;
  }
}

// ---------------------------------------------------------------------------
// helpers

function clamp(value: number, min: number, max: number): number {
  return value < min ? min : value > max ? max : value;
}

// C# Array.BinarySearch semantics: index of a match (any match among duplicates), else
// the bitwise complement of the insertion point.
function binarySearchNumber(array: Float64Array, value: number): number {
  let lo = 0;
  let hi = array.length - 1;
  while (lo <= hi) {
    const mid = (lo + hi) >>> 1;
    if (array[mid] < value) lo = mid + 1;
    else if (array[mid] > value) hi = mid - 1;
    else return mid;
  }
  return ~lo;
}

function binarySearchFeature(list: readonly TraceFeature[], item: TraceFeature): number {
  let lo = 0;
  let hi = list.length - 1;
  while (lo <= hi) {
    const mid = (lo + hi) >>> 1;
    const cmp = compareFeatures(list[mid], item);
    if (cmp < 0) lo = mid + 1;
    else if (cmp > 0) hi = mid - 1;
    else return mid;
  }
  return ~lo;
}

// ---------------------------------------------------------------------------
// TraceData

export class TraceData {
  tag: unknown = null; // C# Tag - opaque host-attached data

  // Change notification for UI layers (e.g. React useSyncExternalStore), same pattern
  // as grid/GridModel.ts: version bumps on ANY change (samples, features, axis terms,
  // titles, name); samplesGeneration bumps ONLY on sample mutations (see header).
  version = 0;
  private listeners = new Set<() => void>();

  private m_Name: string;
  private m_Input: Float64Array | DoubleRing = new Float64Array(0);
  private m_UnixTime: Float64Array | null = null;
  private m_SamplesPerSecond = 0;
  private m_LeftmostUnixTime = 0;
  private m_Features: TraceFeature[] = [];
  private m_SampleNumberDisplayOffset = 0;
  private m_YtRingLength = 0; // > 0 while the input is a YT ring of that length
  private m_SamplesGeneration = 0;
  private m_SampleCache: Float64Array | null = null; // C# InputSampleCache
  private m_InterpCache: Float64Array | null = null; // C# InterpolatedSampleCache

  private m_HorizontalOffset = 0;
  private m_HorizontalMultiplier = 1;
  private m_HorizontalAxisUnit = "";

  private m_AxisTitleBottom = "";
  private m_AxisTitleLeft = "";
  private m_VerticalUnit = "";

  constructor(name: string = "") {
    this.m_Name = name;
  }

  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private notify(): void {
    this.version++;
    this.listeners.forEach((l) => l());
  }

  // ---- identity / labels ----

  get name(): string {
    return this.m_Name;
  }
  set name(value: string) {
    if (this.m_Name !== value) {
      this.m_Name = value;
      this.notify();
    }
  }

  get axisTitleBottom(): string {
    return this.m_AxisTitleBottom;
  }
  set axisTitleBottom(value: string) {
    this.m_AxisTitleBottom = value;
    this.notify();
  }

  get axisTitleLeft(): string {
    return this.m_AxisTitleLeft;
  }
  set axisTitleLeft(value: string) {
    this.m_AxisTitleLeft = value;
    this.notify();
  }

  get verticalUnit(): string {
    return this.m_VerticalUnit;
  }
  set verticalUnit(value: string) {
    this.m_VerticalUnit = value ?? "";
    this.notify();
  }

  // ---- sample access ----

  // Cached materialisation of the input (C# InputSampleCopy/InputSampleCache). Treat as
  // read-only: it may be the trace's own storage or a shared snapshot.
  get samples(): Float64Array {
    if (this.m_SampleCache === null) {
      this.m_SampleCache =
        this.m_Input instanceof DoubleRing ? this.m_Input.window() : this.m_Input;
    }
    return this.m_SampleCache;
  }

  get inputSampleCount(): number {
    return this.samples.length;
  }

  // Read-only view of the per-sample unix times (null unless a real YT trace).
  get unixTime(): Float64Array | null {
    return this.m_UnixTime;
  }

  get inputSamplesPerSecond(): number {
    return this.m_SamplesPerSecond;
  }
  // Rejected while a unix-time array is present: a real-YT trace's rate is CALCULATED
  // from the smallest positive gap, never assigned (C# guard).
  set inputSamplesPerSecond(value: number) {
    if (
      this.m_SamplesPerSecond !== value &&
      Number.isFinite(value) &&
      value >= 0 &&
      this.m_UnixTime === null
    ) {
      this.m_SamplesPerSecond = value;
      this.notify();
    }
  }

  get leftmostUnixTime(): number {
    return this.m_LeftmostUnixTime;
  }
  set leftmostUnixTime(value: number) {
    this.m_LeftmostUnixTime = value;
    this.notify();
  }

  // The pure axis-RELABEL knob: consumers add it to the sample number BEFORE the
  // canonical map (horizontalValueAt), so gutter and hover labels shift without moving
  // the data. Keep it distinct from any view-layer offset: the C# ViewOffsetOverride
  // reshapes the RAW samples and the axis must NOT re-add it (re-adding made the axis
  // follow the move so nothing appeared to shift) - two knobs, two jobs.
  get inputSampleNumberDisplayOffset(): number {
    return this.m_SampleNumberDisplayOffset;
  }
  set inputSampleNumberDisplayOffset(value: number) {
    this.m_SampleNumberDisplayOffset = value;
    this.notify();
  }

  // Bumped on every sample mutation. The C# TraceView.CalculateTrace pairs a snapshot
  // with this generation and discards its projection if the samples changed
  // mid-calculation - concurrent calculations must not clobber a fresher projection
  // with an older one.
  get samplesGeneration(): number {
    return this.m_SamplesGeneration;
  }

  // ---- ingestion ----

  // Replace the samples. samplesPerSecond: omitted (or non-finite) keeps the current
  // rate; a finite value (including 0) is adopted (C# UpdateByRef).
  update(samples: ArrayLike<number>, samplesPerSecond?: number): this {
    this.ingest(Float64Array.from(samples), null, samplesPerSecond);
    return this;
  }

  // Replace with per-sample unix times (real YT). Pairs are sorted by time before
  // storing (C# Update(samples, unixTime) -> Array.Sort(time, data)). A length mismatch
  // drops the time array (C# UpdateByRef stores UnixTime only when lengths agree).
  updateYT(samples: ArrayLike<number>, unixTime: ArrayLike<number>): this {
    let data = Float64Array.from(samples);
    let time = Float64Array.from(unixTime);
    if (data.length === time.length && data.length > 1) {
      const order = Array.from({ length: time.length }, (_, loop) => loop).sort(
        (a, b) => time[a] - time[b]
      );
      const sortedData = new Float64Array(data.length);
      const sortedTime = new Float64Array(time.length);
      for (let loop = 0; loop < order.length; loop++) {
        sortedData[loop] = data[order[loop]];
        sortedTime[loop] = time[order[loop]];
      }
      data = sortedData;
      time = sortedTime;
    }
    this.ingest(data, time, undefined);
    return this;
  }

  private ingest(
    data: Float64Array,
    time: Float64Array | null,
    samplesPerSecond: number | undefined
  ): void {
    this.m_SampleCache = null;
    this.m_InterpCache = null;
    if (
      samplesPerSecond !== undefined &&
      Number.isFinite(samplesPerSecond) &&
      this.m_SamplesPerSecond !== samplesPerSecond
    ) {
      this.m_SamplesPerSecond = samplesPerSecond;
    }
    if (
      time !== null &&
      this.m_SamplesPerSecond !== 0 &&
      (samplesPerSecond === undefined || !Number.isFinite(samplesPerSecond))
    ) {
      this.m_SamplesPerSecond = 0; // per-sample times invalidate the calculated rate
    }
    this.m_UnixTime = time !== null && time.length === data.length ? time : null;
    this.m_Input = data;
    this.m_YtRingLength = 0;
    this.m_SamplesGeneration++;
    this.notify();
  }

  // Bounded streaming append (C# AppendRing). Ring semantics ported exactly:
  // - A new ring (none yet, or ringLength changed) is created and PRE-FILLED with the
  //   first appended sample (C# Ring.Set; 0 for an empty first append), so the visible
  //   window is always exactly ringLength samples - the left side reads as a flat line
  //   until real samples push the prefill out.
  // - Appends discard the oldest samples on overflow; reads always return the full
  //   ringLength window, oldest first (C# reads via Ring.AllSamples).
  // - The plain overload clears any unix-time array (a ring trace is not YT).
  // The YT overload keeps the most recent ringLength (sample, unixTime) pairs - see the
  // header: the C# YT overload is NotImplemented, this one is a functional port of the
  // same bounded-append intent, with NO prefill (fabricated times would corrupt the time
  // axis) and times assumed to arrive in order (they are not re-sorted like updateYT).
  appendRing(samples: ArrayLike<number>, ringLength: number, samplesPerSecond?: number): this;
  appendRing(samples: ArrayLike<number>, unixTime: ArrayLike<number>, ringLength: number): this;
  appendRing(
    samples: ArrayLike<number>,
    ringLengthOrUnixTime: number | ArrayLike<number>,
    samplesPerSecondOrRingLength?: number
  ): this {
    if (typeof ringLengthOrUnixTime === "number") {
      this.appendRingPlain(samples, ringLengthOrUnixTime, samplesPerSecondOrRingLength);
    } else {
      this.appendRingYT(samples, ringLengthOrUnixTime, samplesPerSecondOrRingLength ?? 0);
    }
    return this;
  }

  private appendRingPlain(
    samples: ArrayLike<number>,
    ringLength: number,
    samplesPerSecond: number | undefined
  ): void {
    this.m_SampleCache = null;
    this.m_InterpCache = null;
    // DEVIATION: the C# guard is `IsFinite(InputSamplesPerSecond) && current != incoming`
    // - it tests the CURRENT rate for finiteness, so an omitted rate (NaN) gets stored
    // and then blocks every later change. Ported as Update's intended guard instead.
    if (
      samplesPerSecond !== undefined &&
      Number.isFinite(samplesPerSecond) &&
      this.m_SamplesPerSecond !== samplesPerSecond
    ) {
      this.m_SamplesPerSecond = samplesPerSecond;
    }
    this.m_UnixTime = null;
    let ring = this.m_Input instanceof DoubleRing ? this.m_Input : null;
    if (ring === null || ring.capacity !== ringLength) {
      ring = new DoubleRing(ringLength);
      ring.set(samples.length === 0 ? 0 : samples[0]);
      this.m_Input = ring;
    }
    ring.insert(samples);
    this.m_YtRingLength = 0;
    this.m_SamplesGeneration++;
    this.notify();
  }

  private appendRingYT(
    samples: ArrayLike<number>,
    unixTime: ArrayLike<number>,
    ringLength: number
  ): void {
    this.m_SampleCache = null;
    this.m_InterpCache = null;
    if (this.m_SamplesPerSecond !== 0) {
      this.m_SamplesPerSecond = 0; // per-sample times invalidate the calculated rate
    }
    // Same recreate rule as the plain ring: a ringLength change starts a fresh ring.
    const keepOld =
      this.m_YtRingLength === ringLength &&
      this.m_UnixTime !== null &&
      this.m_Input instanceof Float64Array;
    const oldData = keepOld ? (this.m_Input as Float64Array) : new Float64Array(0);
    const oldTime = keepOld ? (this.m_UnixTime as Float64Array) : new Float64Array(0);
    const pairCount = Math.min(samples.length, unixTime.length); // only complete pairs
    const keep = Math.min(Math.max(0, ringLength), oldData.length + pairCount);
    const data = new Float64Array(keep);
    const time = new Float64Array(keep);
    let index = keep - 1;
    for (let loop = pairCount - 1; loop >= 0 && index >= 0; loop--, index--) {
      data[index] = samples[loop];
      time[index] = unixTime[loop];
    }
    for (let loop = oldData.length - 1; index >= 0; loop--, index--) {
      data[index] = oldData[loop];
      time[index] = oldTime[loop];
    }
    this.m_Input = data;
    this.m_UnixTime = time;
    this.m_YtRingLength = ringLength;
    this.m_SamplesGeneration++;
    this.notify();
  }

  // Resets the data store (samples, times, rate, features, display offset). The affine
  // axis terms live on the trace, not the store, so clear() leaves them alone (the C#
  // Clear replaces only the DataStore).
  clear(): void {
    this.m_SampleCache = null;
    this.m_InterpCache = null;
    this.m_Input = new Float64Array(0);
    this.m_UnixTime = null;
    this.m_SamplesPerSecond = 0;
    this.m_LeftmostUnixTime = 0;
    this.m_Features = [];
    this.m_SampleNumberDisplayOffset = 0;
    this.m_YtRingLength = 0;
    // DEVIATION: the C# forgets this bump; every sample-mutation path must bump.
    this.m_SamplesGeneration++;
    this.notify();
  }

  // ---- horizontal axis ----

  // MINIPLAN (future, carried from the C#): a genuinely NON-uniform horizontal axis
  // (e.g. a nonlinear rpm-vs-speed curve) is not representable affine; today the caller
  // must pre-resample onto a uniform grid. A monotonic per-sample axis is structurally
  // the same as the YT (per-sample unix-time) axis - the intended end-state is to make
  // YT unit-agnostic (carry any unit, not just seconds) so it subsumes the non-uniform
  // case, leaving "affine + unit-agnostic YT" to cover everything.
  //
  // Composition rules:
  // - samplesPerSecond takes precedence over the multiplier (a multiplier cannot compose
  //   with a rate and is silently ignored while sps != 0 - the idle multiplier survives
  //   and takes back over when sps returns to 0).
  // - The OFFSET is always in SAMPLES - it never switches meaning between samples and
  //   axis units, so it composes identically with either scale.
  // - The unit overrides the seconds default when set.
  //     sps != 0: value = (sample + horizontalOffset) / sps    (unit: unit or "s")
  //     sps == 0: value = (sample + horizontalOffset) * horizontalMultiplier
  // - There is NO enable flag: the identity map (0, 1, "") IS the plain sample-number
  //   axis; clearHorizontalAxis() resets to it.
  // - Kind precedence in the C# viewer: FFT > Time > Affine > None. FFT is omitted from
  //   this port (no FFT machinery), leaving Time > Affine > None (horizontalKind).

  get horizontalOffset(): number {
    return this.m_HorizontalOffset;
  }
  get horizontalMultiplier(): number {
    return this.m_HorizontalMultiplier;
  }
  get horizontalAxisUnit(): string {
    return this.m_HorizontalAxisUnit;
  }

  private get horizontalAffineIsIdentity(): boolean {
    return (
      this.m_HorizontalOffset === 0 &&
      this.m_HorizontalMultiplier === 1 &&
      this.m_HorizontalAxisUnit.length === 0
    );
  }

  // True when the AFFINE map positions the samples: no sps (sps takes precedence for
  // positioning; the offset still shifts the seconds axis), a usable map, not the
  // identity.
  get hasExplicitHorizontalAxis(): boolean {
    return (
      this.m_SamplesPerSecond === 0 &&
      !this.horizontalAffineInvalid &&
      !this.horizontalAffineIsIdentity
    );
  }

  // The axis terms are unusable where they matter: a bad multiplier only matters
  // without sps (sps supplies the scale); a non-finite offset always poisons. Invalid
  // terms are stored AS GIVEN, never coerced - every consumer falls back to sample
  // numbers, and the C# trace paints a "(bad horizontal axis)" warning.
  get horizontalAffineInvalid(): boolean {
    return (
      !Number.isFinite(this.m_HorizontalOffset) ||
      (this.m_SamplesPerSecond === 0 &&
        !this.horizontalAffineIsIdentity &&
        !(this.m_HorizontalMultiplier > 0 && Number.isFinite(this.m_HorizontalMultiplier)))
    );
  }

  setHorizontalAffine(offset: number, multiplier: number, unit: string = ""): void {
    this.m_HorizontalOffset = offset;
    this.m_HorizontalMultiplier = multiplier;
    this.m_HorizontalAxisUnit = unit ?? "";
    this.notify();
  }

  clearHorizontalAxis(): void {
    this.m_HorizontalOffset = 0;
    this.m_HorizontalMultiplier = 1;
    this.m_HorizontalAxisUnit = "";
    this.notify();
  }

  // The unit the horizontal axis displays: the explicit unit, or "s" for a rate-based
  // axis (the sps field doubles as the "horizontal axis is time" flag).
  get horizontalUnitEffective(): string {
    return this.m_HorizontalAxisUnit.length !== 0
      ? this.m_HorizontalAxisUnit
      : this.m_SamplesPerSecond !== 0
      ? "s"
      : "";
  }

  // Data-layer axis kind, precedence Time > Affine > None. FFT is a view/paint concept
  // and is omitted from this port; YT display ('yt') is likewise a view decision - the
  // data-side predicate is isYTTrace.
  get horizontalKind(): HorizontalKind {
    return this.m_SamplesPerSecond !== 0
      ? "time"
      : this.hasExplicitHorizontalAxis
      ? "affine"
      : "none";
  }

  // Canonical sample -> axis-value map (see the composition rules above). Everything
  // that positions by value - drawn extents, the group value-domain, hover labels -
  // must go through THIS map: when extents and the affine map disagreed on units in the
  // C#, the group sub-window landed entirely off-pane and a trace with both sps and an
  // offset drew NOTHING. (Consumers add inputSampleNumberDisplayOffset to the sample
  // number first.)
  horizontalValueAt(sampleNumber: number): number {
    const sps = this.m_SamplesPerSecond;
    const offset = Number.isFinite(this.m_HorizontalOffset) ? this.m_HorizontalOffset : 0;
    if (sps !== 0) {
      return (sampleNumber + offset) / sps;
    }
    return this.hasExplicitHorizontalAxis
      ? (sampleNumber + offset) * this.m_HorizontalMultiplier
      : sampleNumber;
  }

  // Inverse of horizontalValueAt, clamped to [0, count-1]. count defaults to the input
  // sample count (the C# always takes it explicitly - views may pass a drawn count).
  sampleAtHorizontalValue(value: number, count: number = this.inputSampleCount): number {
    if (count <= 1) return 0;
    const sps = this.m_SamplesPerSecond;
    const offset = Number.isFinite(this.m_HorizontalOffset) ? this.m_HorizontalOffset : 0;
    if (sps !== 0) {
      return clamp(value * sps - offset, 0, count - 1);
    }
    if (this.hasExplicitHorizontalAxis) {
      return clamp(value / this.m_HorizontalMultiplier - offset, 0, count - 1);
    }
    return clamp(value, 0, count - 1);
  }

  // ---- YT support ----

  // A YT trace positions samples by wall-clock time. Real YT carries a per-sample time
  // array; "fake" YT is a uniform-rate trace pinned to the wall clock by a nonzero
  // leftmostUnixTime (leftmost != 0 && sps != 0) - it draws on the shared time window
  // without per-sample times.
  get isYTTrace(): boolean {
    return (
      this.m_UnixTime !== null ||
      (this.m_LeftmostUnixTime !== 0 && this.m_SamplesPerSecond !== 0)
    );
  }

  get isFakeYT(): boolean {
    return (
      this.m_UnixTime === null &&
      this.m_LeftmostUnixTime !== 0 &&
      this.m_SamplesPerSecond !== 0
    );
  }

  // The sample nearest a unix time (C# ViewedSampleAtUnixTime; no stopped-data clone in
  // this port, so "viewed" is the live input).
  // Fake-YT branch: the index is CLAMPED to the data (the C# used to return negative
  // indices and zero values), and the sample time is index/sps + leftmost (was
  // index*sps - hover labels showed start+68400 for sample 684 at 100 sps).
  // Real-YT branch quirk ported as-is: an EXACT time match at index > 0 returns the
  // sample BEFORE it (the C# decrements found and not-found indices alike).
  viewedSampleAtUnixTime(time: number): { value: number; index: number; time: number } {
    const samples = this.samples;
    const unixTime = this.m_UnixTime;
    let index: number;
    let value: number;
    if (unixTime === null) {
      const sps = this.m_SamplesPerSecond;
      // Math.round ties differ from C# (banker's rounding); only exact .5 midpoints.
      index = Math.round((time - this.m_LeftmostUnixTime) * sps);
      index = clamp(index, 0, Math.max(0, samples.length - 1));
      value = samples.length === 0 ? 0 : samples[index];
      time =
        sps === 0
          ? this.m_LeftmostUnixTime
          : index / sps + this.m_LeftmostUnixTime;
    } else if (unixTime.length === 0) {
      index = 0; // guard: the C# would throw on an empty YT trace
      value = 0;
    } else {
      index = binarySearchNumber(unixTime, time);
      if (index < 0) index = ~index;
      if (index > 0) index--;
      value = samples[index];
      time = unixTime[index];
    }
    return { value, index, time };
  }

  // The calculated rate of a real-YT trace comes from the SMALLEST POSITIVE time gap,
  // so the densest region loses nothing and sparse stretches upsample (C#
  // CalculateSamplesPerSecond). No-op when a rate is already set or there are no times.
  // Quirk ported as-is: delta seeds at 1.0 and only the FIRST gap (when positive)
  // replaces the seed - if the first gap is not positive, later gaps can only lower the
  // seed via min, so a trace whose smallest gap is > 1s ends up rated at 1 Hz.
  private calculateSamplesPerSecond(): void {
    if (this.m_SamplesPerSecond !== 0) return;
    if (this.m_UnixTime === null) return;
    const unixTime = this.m_UnixTime;
    let delta = 1.0;
    for (let loop = 1; loop < unixTime.length; loop++) {
      const diff = unixTime[loop] - unixTime[loop - 1];
      if (diff > 0) {
        delta = loop === 1 ? diff : Math.min(delta, diff);
      }
    }
    this.m_SamplesPerSecond = delta === 0 ? 0 : 1.0 / delta;
  }

  // Resample a real-YT trace's non-uniform samples onto a uniform grid at the
  // calculated rate; returns the plain samples when the trace is not real YT (no time
  // array / length mismatch / empty / rate not computable) - this is what calculations
  // consume (C# ViewedSamplesInterpolatedAsDouble -> DataStore.InterpolateYT).
  // Side effects ported from the C#: leftmostUnixTime is set to the first time, and the
  // calculated rate is STORED into inputSamplesPerSecond. The result is cached until
  // the next sample mutation.
  interpolateYT(): Float64Array {
    if (this.m_InterpCache !== null) return this.m_InterpCache;
    this.m_InterpCache = this.interpolateYTUncached();
    return this.m_InterpCache;
  }

  private interpolateYTUncached(): Float64Array {
    const samples = this.samples;
    const unixTime = this.m_UnixTime;
    if (unixTime === null || samples.length === 0 || samples.length !== unixTime.length) {
      return samples;
    }
    this.m_LeftmostUnixTime = unixTime[0];
    if (samples.length === 1) {
      return samples;
    }
    if (this.m_SamplesPerSecond === 0) {
      this.calculateSamplesPerSecond();
    }
    if (this.m_SamplesPerSecond === 0) {
      return samples;
    }
    return TraceData.interpolate(unixTime, samples, this.m_SamplesPerSecond);
  }

  // C# DataStore.Interpolate. Walks the uniform output grid; when the next input point
  // falls within a quarter-sample overlap of the grid time, advance to it and emit its
  // left value; otherwise linearly interpolate between the bracketing input points.
  // (The C# also tracks low/high envelope values here but never reads them - dropped.)
  private static interpolate(
    unixTime: Float64Array,
    samples: Float64Array,
    samplesPerSecond: number
  ): Float64Array {
    const length = unixTime.length;
    const min = unixTime[0];
    const max = unixTime[length - 1];
    const result = new Float64Array(Math.trunc((max - min) * samplesPerSecond + 1.0));
    let leftSample = samples[0];
    let leftTime = unixTime[0];
    let rightSample = samples[1];
    let rightTime = unixTime[1];
    const overlap = 0.25 / samplesPerSecond;

    let index = 1;
    for (let loop = 0; loop < result.length; loop++) {
      const time = min + loop / samplesPerSecond;
      if (rightTime - time < overlap && index !== length - 1) {
        index++;
        leftTime = rightTime;
        leftSample = rightSample;
        rightTime = unixTime[index];
        rightSample = samples[index];
        result[loop] = leftSample;
      } else {
        result[loop] =
          leftSample + ((rightSample - leftSample) * (time - leftTime)) / (rightTime - leftTime);
      }
    }
    return result;
  }

  // ---- statistics ----

  // Statistics over the input samples (and unix times when present). See
  // computeStatistics for the NaN-gap contract.
  statistics(): TraceStatistics {
    return computeStatistics(this.samples, this.m_UnixTime);
  }

  // ---- features ----

  // Snapshot copy of the feature list (sorted).
  get inputFeatures(): TraceFeature[] {
    return this.m_Features.slice();
  }

  // Replace (clears existing) + sort in one shot, so a feature set derived fresh each
  // run is idempotent on re-runs (C# InputFeatures setter semantics).
  setInputFeatures(features: readonly TraceFeature[]): void {
    this.m_Features = features.slice();
    this.m_Features.sort(compareFeatures);
    this.notify();
  }

  addFeature(sampleNumber: number, text: string): void;
  addFeature(feature: TraceFeature): void;
  addFeature(featureOrSampleNumber: number | TraceFeature, text?: string): void {
    const feature =
      typeof featureOrSampleNumber === "number"
        ? newTraceFeature({ sampleNumber: featureOrSampleNumber, text: text ?? "" })
        : featureOrSampleNumber;
    // Binary insert keeps the list sorted; an equal feature inserts BEFORE the existing
    // one (C# AddFeature inserts at the found index).
    let at = binarySearchFeature(this.m_Features, feature);
    if (at < 0) at = ~at;
    this.m_Features.splice(at, 0, feature);
    this.notify();
  }
}
