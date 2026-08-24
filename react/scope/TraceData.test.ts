// Port of the C# HorizontalAffineTests ([TestClass] in sehenswerte
// src/sehens/data/TraceData.cs) plus coverage for the ring append, YT support,
// interpolation, statistics, features, and change notification of the TraceData port.

import {
  compareFeatures,
  computeStatistics,
  newTraceFeature,
  TraceData,
  TraceFeature,
} from "./TraceData";

describe("horizontal affine axis (C# HorizontalAffineTests port)", () => {
  test("affineValueAndInverse", () => {
    const td = new TraceData("t");
    td.setHorizontalAffine(5.0, 2.0, "rpm"); // offset is in samples
    expect(td.hasExplicitHorizontalAxis).toBe(true);
    expect(td.horizontalAxisUnit).toBe("rpm");
    expect(td.horizontalValueAt(0)).toBeCloseTo(10.0, 9); // 2 * (0 + 5)
    expect(td.horizontalValueAt(3)).toBeCloseTo(16.0, 9);
    expect(td.horizontalValueAt(5)).toBeCloseTo(20.0, 9);
    // inverse round-trips within the sample range
    expect(td.sampleAtHorizontalValue(16.0, 10)).toBeCloseTo(3.0, 9);
    expect(td.sampleAtHorizontalValue(10.0, 10)).toBeCloseTo(0.0, 9);
  });

  test("affineInverseClampsToRange", () => {
    const td = new TraceData("t");
    td.setHorizontalAffine(0.0, 1.0, "s");
    expect(td.sampleAtHorizontalValue(-100.0, 8)).toBeCloseTo(0.0, 9); // below -> 0
    expect(td.sampleAtHorizontalValue(1000.0, 8)).toBeCloseTo(7.0, 9); // above -> count-1
    expect(td.sampleAtHorizontalValue(42.0, 1)).toBeCloseTo(0.0, 9); // degenerate count
  });

  test("noExplicitAxisIsSampleNumber", () => {
    const td = new TraceData("t");
    expect(td.hasExplicitHorizontalAxis).toBe(false);
    expect(td.horizontalValueAt(4)).toBeCloseTo(4.0, 9); // identity
    expect(td.sampleAtHorizontalValue(4.0, 10)).toBeCloseTo(4.0, 9); // identity, in range
    expect(td.sampleAtHorizontalValue(42.0, 10)).toBeCloseTo(9.0, 9); // clamps to count-1
  });

  test("spsWinsTheScaleOffsetComposes", () => {
    // sps > 0: value = (sample + offset)/sps - the multiplier cannot compose with a rate
    // and is ignored; the offset is always in samples so it means the same thing under
    // either scale; the unit overrides the "s" default.
    const td = new TraceData("t");
    td.update(new Array(100).fill(0));
    td.setHorizontalAffine(1000.0, 7.0, "f");
    td.inputSamplesPerSecond = 10.0;
    expect(td.horizontalAffineInvalid).toBe(false); // multiplier is ignored, not an error
    expect(td.hasExplicitHorizontalAxis).toBe(false); // sps positions; affine does not
    expect(td.horizontalValueAt(3)).toBeCloseTo(100.3, 9); // (3 + 1000) / 10, multiplier unused
    expect(td.sampleAtHorizontalValue(100.5, 100)).toBeCloseTo(5.0, 9);
    expect(td.horizontalUnitEffective).toBe("f"); // explicit unit beats the "s" default
    expect(td.horizontalKind).toBe("time"); // precedence: Time > Affine

    td.inputSamplesPerSecond = 0.0; // rate removed: the affine scale takes over
    expect(td.hasExplicitHorizontalAxis).toBe(true);
    expect(td.horizontalKind).toBe("affine");
    expect(td.horizontalValueAt(3)).toBeCloseTo(7021.0, 9); // 7 * (3 + 1000)
  });

  test("identityAffineIsNoAxisAndUnitAloneIsExplicit", () => {
    const td = new TraceData("t");
    td.setHorizontalAffine(0.0, 1.0, ""); // identity == the plain sample-number axis
    expect(td.hasExplicitHorizontalAxis).toBe(false);
    expect(td.horizontalKind).toBe("none");
    expect(td.horizontalValueAt(4)).toBeCloseTo(4.0, 9);

    td.setHorizontalAffine(0.0, 1.0, "km/h"); // a bare unit alone still labels the sample axis
    expect(td.hasExplicitHorizontalAxis).toBe(true);
    expect(td.horizontalValueAt(4)).toBeCloseTo(4.0, 9);
    expect(td.horizontalUnitEffective).toBe("km/h");
  });

  test("samplesGenerationBumpsOnEveryMutation", () => {
    // The C# TraceView.CalculateTrace pairs a snapshot with this generation and discards
    // its projection if the samples changed mid-calculation (concurrent calculations
    // must not clobber a fresher projection with an older one).
    const td = new TraceData("t");
    const g0 = td.samplesGeneration;
    td.update([1, 2, 3]);
    expect(td.samplesGeneration).toBeGreaterThan(g0);
    const g1 = td.samplesGeneration;
    td.update([4, 5]);
    expect(td.samplesGeneration).toBeGreaterThan(g1);
    const g2 = td.samplesGeneration;
    td.appendRing([6], 8);
    expect(td.samplesGeneration).toBeGreaterThan(g2);
    const g3 = td.samplesGeneration;
    td.updateYT([1, 2], [10, 20]);
    expect(td.samplesGeneration).toBeGreaterThan(g3);
    const g4 = td.samplesGeneration;
    td.appendRing([3], [30], 8); // YT ring overload
    expect(td.samplesGeneration).toBeGreaterThan(g4);
    const g5 = td.samplesGeneration;
    td.clear(); // deviation from the C#, which forgets this bump
    expect(td.samplesGeneration).toBeGreaterThan(g5);
  });

  test("invalidMultiplierFlagsErrorAndFallsBack", () => {
    const td = new TraceData("t");
    td.setHorizontalAffine(5.0, 0.0, "rpm"); // zero multiplier: invalid, stored as given
    expect(td.horizontalAffineInvalid).toBe(true);
    expect(td.hasExplicitHorizontalAxis).toBe(false);
    expect(td.horizontalMultiplier).toBeCloseTo(0.0, 9); // no silent =1 coercion
    expect(td.horizontalValueAt(7)).toBeCloseTo(7.0, 9); // sample-number fallback
    expect(td.sampleAtHorizontalValue(4.0, 10)).toBeCloseTo(4.0, 9);

    td.setHorizontalAffine(5.0, -3.0, "rpm"); // negative multiplier
    expect(td.horizontalAffineInvalid).toBe(true);
    expect(td.horizontalMultiplier).toBeCloseTo(-3.0, 9);

    td.setHorizontalAffine(NaN, 2.0, "rpm"); // non-finite offset
    expect(td.horizontalAffineInvalid).toBe(true);

    td.setHorizontalAffine(0.0, 2.0, "rpm"); // valid map clears the error
    expect(td.horizontalAffineInvalid).toBe(false);
    expect(td.hasExplicitHorizontalAxis).toBe(true);
  });

  test("nonFiniteOffsetFallsBackToZeroUnderSps", () => {
    // A non-finite offset always flags invalid, but with sps set the map still works as
    // a time axis with the offset treated as 0 (C# HorizontalValueAt).
    const td = new TraceData("t");
    td.update(new Array(100).fill(0), 10);
    td.setHorizontalAffine(NaN, 2.0, "");
    expect(td.horizontalAffineInvalid).toBe(true);
    expect(td.horizontalValueAt(3)).toBeCloseTo(0.3, 9); // (3 + 0) / 10
    expect(td.sampleAtHorizontalValue(0.3, 100)).toBeCloseTo(3.0, 9);
  });

  test("clearRevertsToImplicit", () => {
    const td = new TraceData("t");
    td.setHorizontalAffine(10.0, 3.0, "kph");
    expect(td.hasExplicitHorizontalAxis).toBe(true);
    td.clearHorizontalAxis();
    expect(td.hasExplicitHorizontalAxis).toBe(false);
    expect(td.horizontalAxisUnit).toBe("");
    expect(td.horizontalValueAt(6)).toBeCloseTo(6.0, 9); // back to sample number
  });

  test("sampleAtHorizontalValueDefaultsToInputCount", () => {
    const td = new TraceData("t");
    td.update([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);
    expect(td.sampleAtHorizontalValue(42.0)).toBeCloseTo(9.0, 9); // clamps to count-1
  });
});

describe("update / updateYT", () => {
  test("updateStoresACopy", () => {
    const source = [1, 2, 3];
    const td = new TraceData("t").update(source);
    source[0] = 99;
    expect(Array.from(td.samples)).toEqual([1, 2, 3]);
    expect(td.inputSampleCount).toBe(3);
  });

  test("updateRateHandling", () => {
    const td = new TraceData("t");
    td.update([1, 2], 100);
    expect(td.inputSamplesPerSecond).toBe(100);
    td.update([3, 4]); // omitted rate keeps the current one
    expect(td.inputSamplesPerSecond).toBe(100);
    td.update([5, 6], 0); // an explicit finite 0 clears the rate
    expect(td.inputSamplesPerSecond).toBe(0);
  });

  test("updateYTSortsPairsByTime", () => {
    const td = new TraceData("t").updateYT([3, 1, 2], [30, 10, 20]);
    expect(Array.from(td.samples)).toEqual([1, 2, 3]);
    expect(Array.from(td.unixTime as Float64Array)).toEqual([10, 20, 30]);
    expect(td.isYTTrace).toBe(true);
    expect(td.isFakeYT).toBe(false);
  });

  test("updateYTInvalidatesACalculatedRate", () => {
    const td = new TraceData("t");
    td.update([1, 2, 3], 100);
    td.updateYT([1, 2, 3], [10, 20, 30]);
    expect(td.inputSamplesPerSecond).toBe(0); // per-sample times invalidate the rate
  });

  test("updateYTLengthMismatchDropsTimes", () => {
    const td = new TraceData("t").updateYT([1, 2, 3], [10, 20]);
    expect(td.unixTime).toBeNull();
    expect(td.isYTTrace).toBe(false);
    expect(Array.from(td.samples)).toEqual([1, 2, 3]);
  });

  test("samplesPerSecondSetterGuards", () => {
    const td = new TraceData("t");
    td.updateYT([1, 2], [10, 20]);
    td.inputSamplesPerSecond = 50; // refused: real-YT rate is calculated, never assigned
    expect(td.inputSamplesPerSecond).toBe(0);

    td.update([1, 2]);
    td.inputSamplesPerSecond = -5; // refused: negative
    expect(td.inputSamplesPerSecond).toBe(0);
    td.inputSamplesPerSecond = NaN; // refused: non-finite
    expect(td.inputSamplesPerSecond).toBe(0);
    td.inputSamplesPerSecond = 50;
    expect(td.inputSamplesPerSecond).toBe(50);
  });
});

describe("appendRing (C# Ring.Set prefill + AllSamples window semantics)", () => {
  test("newRingPrefillsWithFirstSample", () => {
    // The window is always exactly ringLength long: the left side is the prefill (first
    // appended sample) until real samples push it out.
    const td = new TraceData("t").appendRing([1, 2, 3], 8);
    expect(Array.from(td.samples)).toEqual([1, 1, 1, 1, 1, 1, 2, 3]);
    expect(td.inputSampleCount).toBe(8);
  });

  test("streamingWraparoundDiscardsOldest", () => {
    const td = new TraceData("t");
    td.appendRing([1, 2, 3], 8);
    td.appendRing([4, 5, 6, 7, 8, 9], 8);
    expect(Array.from(td.samples)).toEqual([2, 3, 4, 5, 6, 7, 8, 9]);
  });

  test("appendLargerThanRingKeepsTail", () => {
    const td = new TraceData("t").appendRing([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], 4);
    expect(Array.from(td.samples)).toEqual([7, 8, 9, 10]);
  });

  test("emptyFirstAppendPrefillsZero", () => {
    const td = new TraceData("t").appendRing([], 3);
    expect(Array.from(td.samples)).toEqual([0, 0, 0]);
    td.appendRing([5], 3);
    expect(Array.from(td.samples)).toEqual([0, 0, 5]);
  });

  test("ringLengthChangeRecreatesTheRing", () => {
    const td = new TraceData("t");
    td.appendRing([1, 2], 4);
    td.appendRing([9], 2); // different length: fresh ring, prefilled with 9
    expect(Array.from(td.samples)).toEqual([9, 9]);
  });

  test("updateResetsTheRing", () => {
    const td = new TraceData("t");
    td.appendRing([1, 2, 3], 4);
    td.update([9, 9]);
    expect(Array.from(td.samples)).toEqual([9, 9]);
    td.appendRing([5], 4); // array input replaced the ring: recreate + prefill
    expect(Array.from(td.samples)).toEqual([5, 5, 5, 5]);
  });

  test("appendRingRateHandling", () => {
    const td = new TraceData("t");
    td.appendRing([1], 4, 50);
    expect(td.inputSamplesPerSecond).toBe(50);
    // DEVIATION pin: the C#'s transposed IsFinite check would store NaN here; the port
    // keeps the current rate when none is given (Update's intended guard).
    td.appendRing([2], 4);
    expect(td.inputSamplesPerSecond).toBe(50);
  });

  test("plainRingClearsUnixTime", () => {
    const td = new TraceData("t").updateYT([1, 2], [10, 20]);
    expect(td.isYTTrace).toBe(true);
    td.appendRing([3], 4);
    expect(td.unixTime).toBeNull();
    expect(td.isYTTrace).toBe(false);
  });
});

describe("appendRing YT overload (bounded pair append; C# throws NotImplemented)", () => {
  test("accumulatesPairsUpToRingLength", () => {
    const td = new TraceData("t");
    td.appendRing([1, 2], [10, 11], 4);
    td.appendRing([3], [12], 4);
    expect(Array.from(td.samples)).toEqual([1, 2, 3]);
    expect(Array.from(td.unixTime as Float64Array)).toEqual([10, 11, 12]);
    expect(td.isYTTrace).toBe(true);
  });

  test("boundedWraparoundKeepsNewestPairs", () => {
    const td = new TraceData("t");
    td.appendRing([1, 2, 3], [10, 11, 12], 4);
    td.appendRing([4, 5, 6], [13, 14, 15], 4);
    expect(Array.from(td.samples)).toEqual([3, 4, 5, 6]);
    expect(Array.from(td.unixTime as Float64Array)).toEqual([12, 13, 14, 15]);
  });

  test("ringLengthChangeRestartsPairs", () => {
    const td = new TraceData("t");
    td.appendRing([1, 2, 3], [10, 11, 12], 4);
    td.appendRing([9], [99], 2);
    expect(Array.from(td.samples)).toEqual([9]);
    expect(Array.from(td.unixTime as Float64Array)).toEqual([99]);
  });

  test("ytRingInvalidatesAStoredRate", () => {
    const td = new TraceData("t");
    td.update([1, 2], 100);
    td.appendRing([3], [30], 4);
    expect(td.inputSamplesPerSecond).toBe(0);
  });
});

describe("YT support", () => {
  test("fakeYtNotion", () => {
    // fake YT = a uniform-rate trace pinned to the wall clock: leftmost != 0 && sps != 0
    const td = new TraceData("t").update([1, 2, 3], 100);
    expect(td.isYTTrace).toBe(false);
    td.leftmostUnixTime = 1000;
    expect(td.isYTTrace).toBe(true);
    expect(td.isFakeYT).toBe(true);

    const real = new TraceData("r").updateYT([1, 2], [10, 20]);
    expect(real.isYTTrace).toBe(true);
    expect(real.isFakeYT).toBe(false);
  });

  test("viewedSampleAtUnixTimeRealYt", () => {
    const td = new TraceData("t").updateYT([1, 2, 3], [10, 20, 30]);
    // between two samples: the sample at-or-before the query time
    expect(td.viewedSampleAtUnixTime(25)).toEqual({ value: 2, index: 1, time: 20 });
    // before the first: clamps to sample 0
    expect(td.viewedSampleAtUnixTime(5)).toEqual({ value: 1, index: 0, time: 10 });
    // after the last: clamps to the last sample
    expect(td.viewedSampleAtUnixTime(100)).toEqual({ value: 3, index: 2, time: 30 });
    // ported C# quirk: an EXACT match at index > 0 returns the sample BEFORE it (the
    // C# decrements found and not-found binary-search indices alike)
    expect(td.viewedSampleAtUnixTime(20)).toEqual({ value: 1, index: 0, time: 10 });
  });

  test("viewedSampleAtUnixTimeFakeYt", () => {
    const td = new TraceData("t");
    td.update(Array.from({ length: 100 }, (_, loop) => loop), 100);
    td.leftmostUnixTime = 1000;
    // index = round((time - leftmost) * sps); time back-computed as index/sps + leftmost
    // (the fixed C# math - index*sps once made sample 684 at 100 sps label start+68400)
    expect(td.viewedSampleAtUnixTime(1000.5)).toEqual({ value: 50, index: 50, time: 1000.5 });
    // clamps to the data (the C# used to return negative indices and zero values)
    expect(td.viewedSampleAtUnixTime(999)).toEqual({ value: 0, index: 0, time: 1000 });
    const last = td.viewedSampleAtUnixTime(2000);
    expect(last.value).toBe(99);
    expect(last.index).toBe(99);
    expect(last.time).toBeCloseTo(1000.99, 9);
  });

  test("viewedSampleAtUnixTimeNoRate", () => {
    // sps == 0: index 0, and the returned time is leftmostUnixTime
    const td = new TraceData("t").update([7, 8, 9]);
    td.leftmostUnixTime = 5;
    expect(td.viewedSampleAtUnixTime(123)).toEqual({ value: 7, index: 0, time: 5 });
  });

  test("interpolateYtRateFromSmallestPositiveGap", () => {
    // gaps 0.25, 1.0, 1.0 -> the SMALLEST positive gap supplies the rate (4 sps): the
    // densest region loses nothing and the sparse stretches upsample
    const td = new TraceData("t").updateYT([0, 1, 2, 3], [100, 100.25, 101.25, 102.25]);
    const result = td.interpolateYT();
    expect(td.inputSamplesPerSecond).toBe(4); // calculated rate is STORED (C# side effect)
    expect(td.leftmostUnixTime).toBe(100); // leftmost pinned to the first time
    expect(Array.from(result)).toEqual([0, 1, 1.25, 1.5, 1.75, 2, 2.25, 2.5, 2.75, 3]);
  });

  test("interpolateYtNonYtReturnsPlainSamples", () => {
    const td = new TraceData("t").update([1, 2, 3]);
    expect(Array.from(td.interpolateYT())).toEqual([1, 2, 3]);
  });

  test("interpolateYtSingleSample", () => {
    const td = new TraceData("t").updateYT([5], [100]);
    expect(Array.from(td.interpolateYT())).toEqual([5]);
    expect(td.leftmostUnixTime).toBe(100);
  });

  test("interpolateYtCacheInvalidatedByMutation", () => {
    const td = new TraceData("t").updateYT([0, 1], [0, 0.5]);
    expect(Array.from(td.interpolateYT())).toEqual([0, 1]);
    td.updateYT([0, 2], [0, 0.5]);
    expect(Array.from(td.interpolateYT())).toEqual([0, 2]);
  });

  test("calculateSamplesPerSecondSeedQuirk", () => {
    // Ported C# quirk: delta seeds at 1.0 and only a positive FIRST gap replaces the
    // seed; with a non-positive first gap, later (larger) gaps cannot raise it, so the
    // rate lands at 1 Hz even though the smallest real gap is 2 s.
    const td = new TraceData("t").updateYT([1, 2, 3, 4], [0, 0, 2, 4]);
    const result = td.interpolateYT();
    expect(td.inputSamplesPerSecond).toBe(1);
    expect(result.length).toBe(5); // (4 - 0) * 1 + 1
  });
});

describe("statistics", () => {
  test("statisticsSkipGapSamples", () => {
    // gaps (NaN) must not poison the stats header to Min=NaN,Max=NaN,...
    const stats = computeStatistics([NaN, 1.0, 3.0, NaN, 2.0]);
    expect(stats.min).toBeCloseTo(1.0, 9);
    expect(stats.max).toBeCloseTo(3.0, 9);
    expect(stats.range).toBeCloseTo(2.0, 9);
    expect(stats.sum).toBeCloseTo(6.0, 9);
    expect(stats.average).toBeCloseTo(2.0, 9);
    expect(stats.count).toBe(5); // gaps still count as drawn samples
    expect(stats.lastInput).toBeCloseTo(2.0, 9); // last FINITE sample
    // population std-dev of [1,3,2]: sqrt(14/3 - 4)
    expect(stats.stdDev).toBeCloseTo(0.816496580927726, 9);

    const empty = computeStatistics([NaN, NaN]);
    expect(empty.count).toBe(2);
    // figures that cannot be computed are NaN (the C# leaves them flag-unset)
    expect(empty.min).toBeNaN();
    expect(empty.max).toBeNaN();
    expect(empty.average).toBeNaN();
    expect(empty.sum).toBeNaN();
    expect(empty.lastInput).toBeNaN();
  });

  test("constantDataHasZeroStdDev", () => {
    // the C# variance guard (range == 0 -> 0) avoids negative rounding noise
    const stats = computeStatistics([5, 5, 5]);
    expect(stats.min).toBe(5);
    expect(stats.max).toBe(5);
    expect(stats.range).toBe(0);
    expect(stats.stdDev).toBe(0);
  });

  test("timeStdDevFromUnixTimes", () => {
    const stats = computeStatistics([1, 2, 3], [10, 20, 30]);
    // population std-dev of [10,20,30]: sqrt(1400/3 - 400)
    expect(stats.timeStdDev).toBeCloseTo(8.16496580927726, 9);
    expect(computeStatistics([1, 2, 3]).timeStdDev).toBeNaN(); // no time array
  });

  test("traceStatisticsMethodUsesInputAndTimes", () => {
    const td = new TraceData("t").updateYT([1, NaN, 3], [10, 20, 30]);
    const stats = td.statistics();
    expect(stats.count).toBe(3);
    expect(stats.min).toBe(1);
    expect(stats.max).toBe(3);
    expect(stats.timeStdDev).toBeCloseTo(8.16496580927726, 9);
  });
});

describe("features", () => {
  test("newTraceFeatureDefaults", () => {
    const f = newTraceFeature();
    expect(f.type).toBe("text");
    expect(f.colour).toBeNull();
    expect(f.angle).toBe(-90); // vertical, bottom to top
    expect(f.verticalAnchor).toBe("centre");
    expect(f.verticalJustify).toBe("middle");
    expect(f.text).toBe("");
  });

  test("addFeatureKeepsSortedOrder", () => {
    const td = new TraceData("t");
    td.addFeature(5, "five");
    td.addFeature(1, "one");
    td.addFeature(3, "three");
    expect(td.inputFeatures.map((f) => f.sampleNumber)).toEqual([1, 3, 5]);
  });

  test("addFeatureInsertsEqualBeforeExisting", () => {
    // C# AddFeature inserts at the binary-search hit, so an equal feature lands BEFORE
    // the existing one
    const td = new TraceData("t");
    td.addFeature(3, "first");
    td.addFeature(3, "second");
    expect(td.inputFeatures.map((f) => f.text)).toEqual(["second", "first"]);
  });

  test("setInputFeaturesClearsSortsAndIsIdempotent", () => {
    const td = new TraceData("t");
    td.addFeature(99, "stale"); // replaced by the set below
    const derived: TraceFeature[] = [
      newTraceFeature({ sampleNumber: 5, text: "five" }),
      newTraceFeature({ sampleNumber: 1, text: "one" }),
      newTraceFeature({ sampleNumber: 3, text: "three" }),
    ];
    td.setInputFeatures(derived);
    expect(td.inputFeatures.map((f) => f.text)).toEqual(["one", "three", "five"]);
    // a feature set derived fresh each run must be idempotent on re-runs
    td.setInputFeatures(derived);
    expect(td.inputFeatures.map((f) => f.text)).toEqual(["one", "three", "five"]);
    expect(td.inputFeatures.length).toBe(3);
  });

  test("sortOrderIsTimeThenSampleThenType", () => {
    // unix time beats sample number; feature-type declaration order breaks ties
    const byTime = compareFeatures(
      newTraceFeature({ sampleNumber: 10, unixTime: 1 }),
      newTraceFeature({ sampleNumber: 1, unixTime: 2 })
    );
    expect(byTime).toBeLessThan(0);
    const byType = compareFeatures(
      newTraceFeature({ sampleNumber: 3, type: "line" }),
      newTraceFeature({ sampleNumber: 3, type: "text" })
    );
    expect(byType).toBeGreaterThan(0); // text sorts before line
  });

  test("inputFeaturesReturnsASnapshot", () => {
    const td = new TraceData("t");
    td.addFeature(1, "one");
    const snapshot = td.inputFeatures;
    snapshot.pop();
    expect(td.inputFeatures.length).toBe(1);
  });
});

describe("change notification (GridModel subscribe pattern)", () => {
  test("notifiesOnSampleAxisAndFeatureChanges", () => {
    const td = new TraceData("t");
    let calls = 0;
    const unsubscribe = td.subscribe(() => calls++);

    const v0 = td.version;
    td.update([1, 2, 3]);
    expect(calls).toBe(1);
    expect(td.version).toBeGreaterThan(v0);

    const generation = td.samplesGeneration;
    td.setHorizontalAffine(1, 2, "u"); // axis change notifies...
    expect(calls).toBe(2);
    expect(td.samplesGeneration).toBe(generation); // ...but is not a sample mutation

    td.addFeature(0, "x");
    expect(calls).toBe(3);

    td.verticalUnit = "V";
    td.axisTitleBottom = "b";
    expect(calls).toBe(5);

    unsubscribe();
    td.update([4]);
    expect(calls).toBe(5);
  });

  test("nameNotifiesOnlyOnChange", () => {
    const td = new TraceData("t");
    let calls = 0;
    td.subscribe(() => calls++);
    td.name = "t"; // unchanged: no notify (C# fires the rename callback only on change)
    expect(calls).toBe(0);
    td.name = "u";
    expect(calls).toBe(1);
    expect(td.name).toBe("u");
  });
});

describe("accessors and clear", () => {
  test("accessorsRoundTrip", () => {
    const td = new TraceData("trace1");
    td.verticalUnit = "V";
    td.axisTitleBottom = "bottom";
    td.axisTitleLeft = "left";
    td.inputSampleNumberDisplayOffset = 42; // pure axis-relabel knob
    expect(td.name).toBe("trace1");
    expect(td.verticalUnit).toBe("V");
    expect(td.axisTitleBottom).toBe("bottom");
    expect(td.axisTitleLeft).toBe("left");
    expect(td.inputSampleNumberDisplayOffset).toBe(42);
    // the display offset relabels only: consumers add it BEFORE the canonical map, so
    // the map itself is unchanged (two independent horizontal knobs)
    expect(td.horizontalValueAt(3)).toBe(3);
  });

  test("clearResetsDataButKeepsAffineTerms", () => {
    const td = new TraceData("t");
    td.update([1, 2, 3], 100);
    td.leftmostUnixTime = 5;
    td.addFeature(1, "x");
    td.inputSampleNumberDisplayOffset = 7;
    td.setHorizontalAffine(10, 3, "kph");
    td.clear();
    expect(td.inputSampleCount).toBe(0);
    expect(td.inputSamplesPerSecond).toBe(0);
    expect(td.leftmostUnixTime).toBe(0);
    expect(td.inputFeatures.length).toBe(0);
    expect(td.inputSampleNumberDisplayOffset).toBe(0);
    // the affine terms live on the trace, not the data store (C# Clear semantics)
    expect(td.horizontalOffset).toBe(10);
    expect(td.horizontalMultiplier).toBe(3);
    expect(td.horizontalAxisUnit).toBe("kph");
    expect(td.name).toBe("t");
  });
});
