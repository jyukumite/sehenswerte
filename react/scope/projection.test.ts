// Tests for the scope projection (decimation) pipeline ported from the C# sehenswerte
// painters (Paint2dTrace.Project2dCurves + Projection2dMinMax/Normal/Interpolate/Average/
// Dots + ProjectPolygon, PaintTraceBase.ProjectLog + the log-X axis maps).
//
// The C# repo has no [TestClass] coverage for these functions, so the cases below are
// derived from the invariants in projection.ts's header comment, with expected values
// computed by hand or by an independent brute-force reference in the test.

import {
  CurvePaintMode,
  drawnExtents,
  envelopePolygon,
  LOG_HORIZONTAL_MAX_DECADES,
  logHEffectiveLeft,
  logHFractionToValue,
  logHValueToFraction,
  logPixelToFracSampleIndex,
  logPixelToSampleIndex,
  logSampleIndexToXOffset,
  projectAverage,
  projectCurves,
  projectDots,
  projectInterpolate,
  projectLog,
  projectLogInverse,
  projectMinMax,
  projectNearest,
} from './projection';

function ramp(n: number, scale = 1, offset = 0): Float64Array {
  const out = new Float64Array(n);
  for (let i = 0; i < n; i++) out[i] = offset + i * scale;
  return out;
}

function sine(n: number, cycles: number): Float64Array {
  const out = new Float64Array(n);
  for (let i = 0; i < n; i++) out[i] = Math.sin((2 * Math.PI * i * cycles) / n);
  return out;
}

// Independent per-bin reference: bin i covers samples [floor(i*count/width),
// floor((i+1)*count/width)), an empty bin is forced to one sample.
function binBounds(pixel: number, count: number, width: number): [number, number] {
  const start = Math.floor((pixel * count) / width);
  let end = Math.floor(((pixel + 1) * count) / width);
  if (end <= start) end = start + 1;
  return [start, end];
}

describe('projectMinMax', () => {
  test('DenseSineEnvelopeBoundsEverySample', () => {
    const count = 10000;
    const width = 100;
    const samples = sine(count, 13);
    const mins = projectMinMax(samples, 0, count, width, true);
    const maxs = projectMinMax(samples, 0, count, width, false);
    expect(mins.length).toBe(width);
    expect(maxs.length).toBe(width);
    for (let pixel = 0; pixel < width; pixel++) {
      expect(mins[pixel]).toBeLessThanOrEqual(maxs[pixel]);
      const [start, end] = binBounds(pixel, count, width);
      let lo = Infinity;
      let hi = -Infinity;
      for (let i = start; i < end && i < count; i++) {
        if (samples[i] < lo) lo = samples[i];
        if (samples[i] > hi) hi = samples[i];
        // the envelope bounds every sample in the pixel's bin
        expect(samples[i]).toBeGreaterThanOrEqual(mins[pixel]);
        expect(samples[i]).toBeLessThanOrEqual(maxs[pixel]);
      }
      // and is tight: exactly the bin's min/max
      expect(mins[pixel]).toBe(lo);
      expect(maxs[pixel]).toBe(hi);
    }
  });

  test('BinBoundaryMathMatchesFloorFormula', () => {
    // ragged bins (prime count) and a mid-sparse case with empty bins forced to one sample
    const cases: Array<[number, number]> = [
      [997, 100], // dense, ragged
      [7, 10], // count < width: empty-bin guard active
      [100, 100], // one sample per bin
    ];
    for (const [count, width] of cases) {
      const samples = ramp(count, 3, -5);
      const mins = projectMinMax(samples, 0, count, width, true);
      const maxs = projectMinMax(samples, 0, count, width, false);
      for (let pixel = 0; pixel < width; pixel++) {
        const [start, end] = binBounds(pixel, count, width);
        // ramp is increasing: min is the first sample of the bin, max the last (clipped)
        expect(mins[pixel]).toBe(samples[start]);
        expect(maxs[pixel]).toBe(samples[Math.min(end, count) - 1]);
      }
    }
  });

  test('NaNFirstSampleInBinProjectsNaN', () => {
    const count = 100;
    const width = 10; // bins of 10 samples
    const samples = ramp(count);
    samples[0] = NaN; // first sample of bin 0
    const mins = projectMinMax(samples, 0, count, width, true);
    const maxs = projectMinMax(samples, 0, count, width, false);
    expect(Number.isNaN(mins[0])).toBe(true);
    expect(Number.isNaN(maxs[0])).toBe(true);
    // other bins untouched
    expect(mins[1]).toBe(10);
    expect(maxs[1]).toBe(19);
  });

  test('NaNLaterInBinDoesNotPoisonFiniteStart', () => {
    const count = 100;
    const width = 10;
    const samples = ramp(count);
    samples[35] = NaN; // bin 3 covers [30, 40); its first sample (30) is finite
    const mins = projectMinMax(samples, 0, count, width, true);
    const maxs = projectMinMax(samples, 0, count, width, false);
    // NaN never wins a </> comparison against the finite running value
    expect(mins[3]).toBe(30);
    expect(maxs[3]).toBe(39);
  });

  test('WindowFirstOffsetRespected', () => {
    const samples = ramp(100);
    const mins = projectMinMax(samples, 50, 20, 10, true);
    const maxs = projectMinMax(samples, 50, 20, 10, false);
    for (let pixel = 0; pixel < 10; pixel++) {
      expect(mins[pixel]).toBe(50 + 2 * pixel);
      expect(maxs[pixel]).toBe(51 + 2 * pixel);
    }
  });

  test('EmptyWindowAndZeroWidth', () => {
    const samples = ramp(10);
    const empty = projectMinMax(samples, 0, 0, 5, true);
    expect(empty.length).toBe(5);
    for (let i = 0; i < empty.length; i++) {
      expect(Number.isNaN(empty[i])).toBe(true);
    }
    expect(projectMinMax(samples, 0, 10, 0, true).length).toBe(0);
  });
});

describe('projectNearest', () => {
  test('SparseRampIsStaircase', () => {
    // 10 samples into 100 pixels: index = floor(pixel*10/100) = floor(pixel/10)
    const samples = ramp(10);
    const out = projectNearest(samples, 0, 10, 100);
    expect(out.length).toBe(100);
    for (let pixel = 0; pixel < 100; pixel++) {
      expect(out[pixel]).toBe(Math.floor(pixel / 10));
    }
  });

  test('WindowFirstOffsetRespected', () => {
    const samples = ramp(20);
    const out = projectNearest(samples, 15, 5, 10);
    for (let pixel = 0; pixel < 10; pixel++) {
      expect(out[pixel]).toBe(15 + Math.floor(pixel / 2));
    }
  });
});

describe('projectInterpolate', () => {
  test('SparseRampMonotonicWithExactHitsAndRightEdgeClamp', () => {
    // 10 samples into 100 pixels; samples[i] = 10*i
    const samples = ramp(10, 10);
    const out = projectInterpolate(samples, 0, 10, 100);
    expect(out.length).toBe(100);
    // the first pixel that lands on a new sample index projects that sample exactly
    for (let k = 0; k < 10; k++) {
      expect(out[10 * k]).toBe(samples[k]);
    }
    // monotonic non-decreasing on an increasing ramp
    for (let pixel = 1; pixel < 100; pixel++) {
      expect(out[pixel]).toBeGreaterThanOrEqual(out[pixel - 1]);
    }
    // right edge: indexRight >= count clamps to the last sample, so the last
    // sample-index run is flat at samples[9] (closeTo: 90*(1-r)+90*r can round)
    expect(out[95]).toBeCloseTo(90, 9);
    expect(out[99]).toBeCloseTo(90, 9);
  });

  test('RatioResetGuardProjectsExactSampleOnIndexChange', () => {
    // count=3, width=7: pixel 3 has frac = 9/7 (indexLeft 1, fractional part 2/7),
    // but the prevIndex guard forces ratio=0 -> exactly samples[1], not an
    // interpolation 2/7 of the way to samples[2].
    const samples = Float64Array.from([10, 20, 40]);
    const out = projectInterpolate(samples, 0, 3, 7);
    expect(out[0]).toBe(10);
    expect(out[1]).toBeCloseTo(10 + (10 * 3) / 7, 9); // ratio 3/7 within index 0
    expect(out[2]).toBeCloseTo(10 + (10 * 6) / 7, 9);
    expect(out[3]).toBe(20); // guard: exact hit
    expect(out[4]).toBeCloseTo((20 * 2) / 7 + (40 * 5) / 7, 9); // ratio 5/7 within index 1
    expect(out[5]).toBe(40); // guard again: ratio reset -> exact
    expect(out[6]).toBeCloseTo(40, 9); // both neighbours clamp to samples[2]
  });
});

describe('projectAverage', () => {
  test('PerBinMean', () => {
    const samples = Float64Array.from([1, 2, 3, 4, 5, 6, 7, 8]);
    const out = projectAverage(samples, 0, 8, 4);
    expect(out.length).toBe(4);
    expect(out[0]).toBeCloseTo(1.5, 9);
    expect(out[1]).toBeCloseTo(3.5, 9);
    expect(out[2]).toBeCloseTo(5.5, 9);
    expect(out[3]).toBeCloseTo(7.5, 9);
  });

  test('RaggedBins', () => {
    // count=6, width=4: bins [0,1) [1,3) [3,4) [4,6)
    const samples = Float64Array.from([2, 4, 6, 8, 10, 12]);
    const out = projectAverage(samples, 0, 6, 4);
    expect(out[0]).toBeCloseTo(2, 9);
    expect(out[1]).toBeCloseTo(5, 9);
    expect(out[2]).toBeCloseTo(8, 9);
    expect(out[3]).toBeCloseTo(11, 9);
  });

  test('NaNAnywhereInBinMakesMeanNaN', () => {
    const samples = ramp(8);
    samples[5] = NaN; // bin 2 covers [4, 6)
    const out = projectAverage(samples, 0, 8, 4);
    expect(out[1]).toBeCloseTo(2.5, 9);
    expect(Number.isNaN(out[2])).toBe(true);
    expect(out[3]).toBeCloseTo(6.5, 9);
  });
});

describe('projectDots', () => {
  test('SampleZeroAlwaysSkipped', () => {
    // count=4 into width=100: x = 0, 25, 50, 75; sample 0 never becomes a dot
    const samples = Float64Array.from([1, 2, 3, 4]);
    const dots = projectDots(samples, 0, 4, 100, false, -Infinity, Infinity);
    expect(Array.from(dots.x)).toEqual([25, 50, 75]);
    expect(Array.from(dots.y)).toEqual([2, 3, 4]);
  });

  test('DuplicateXSuppressed', () => {
    // count=10 into width=5: x = floor(loop/2) = 0,0,1,1,2,2,3,3,4,4 - only the
    // first sample landing on each new pixel is kept (sample 0 skipped as always)
    const samples = ramp(10);
    const dots = projectDots(samples, 0, 10, 5, false, -Infinity, Infinity);
    expect(Array.from(dots.x)).toEqual([0, 1, 2, 3, 4]);
    expect(Array.from(dots.y)).toEqual([1, 2, 4, 6, 8]);
  });

  test('PointsIfChangedSkipsUnchangedY', () => {
    const samples = Float64Array.from([0, 1, 1, 2]);
    const dots = projectDots(samples, 0, 4, 100, true, -Infinity, Infinity);
    expect(Array.from(dots.x)).toEqual([25, 75]); // the repeated 1 at x=50 is dropped
    expect(Array.from(dots.y)).toEqual([1, 2]);
  });

  test('OutOfRangeValuesFiltered', () => {
    const samples = Float64Array.from([0, 10, 999, 20]);
    const dots = projectDots(samples, 0, 4, 100, false, 0, 100);
    expect(Array.from(dots.x)).toEqual([25, 75]);
    expect(Array.from(dots.y)).toEqual([10, 20]);
  });

  test('EmptyWindowGivesNoDots', () => {
    const dots = projectDots(new Float64Array(0), 0, 0, 100, false, -Infinity, Infinity);
    expect(dots.x.length).toBe(0);
    expect(dots.y.length).toBe(0);
  });
});

describe('envelopePolygon', () => {
  test('MinForwardThenMaxReversed', () => {
    const mins = Float64Array.from([1, 2, 3]);
    const maxs = Float64Array.from([4, 5, 6]);
    const poly = envelopePolygon(mins, maxs);
    expect(Array.from(poly.x)).toEqual([0, 1, 2, 2, 1, 0]);
    expect(Array.from(poly.y)).toEqual([1, 2, 3, 6, 5, 4]);
  });

  test('NonFiniteValuesSkipped', () => {
    const mins = Float64Array.from([1, NaN, 3]);
    const maxs = Float64Array.from([4, 5, Infinity]);
    const poly = envelopePolygon(mins, maxs);
    expect(Array.from(poly.x)).toEqual([0, 2, 1, 0]);
    expect(Array.from(poly.y)).toEqual([1, 3, 5, 4]);
  });
});

describe('projectLog', () => {
  test('DefaultTwoStavesOutputRangeAndClamp', () => {
    // staves defaults to 2: two decades below maxInput map to [0, 2]
    const top = projectLog(100, 100);
    expect(top.newMax).toBe(2);
    expect(top.output).toBeCloseTo(2, 9);
    const mid = projectLog(100, 10);
    expect(mid.newMax).toBe(2);
    expect(mid.output).toBeCloseTo(1, 9);
    // the clamp threshold is maxInput / base^staves = 1
    expect(projectLog(100, 1).output).toBeCloseTo(0, 9);
    expect(projectLog(100, 0.999).output).toBe(0); // below threshold -> 0
    expect(projectLog(100, 1e-9).output).toBe(0);
    // above maxInput clamps to the top of the range
    expect(projectLog(100, 1000).output).toBe(2);
  });

  test('CustomStavesAndBase', () => {
    const r = projectLog(1000, 1000, 3);
    expect(r.newMax).toBe(3);
    expect(r.output).toBeCloseTo(3, 9);
    expect(projectLog(1000, 1, 3).output).toBeCloseTo(0, 9); // threshold 1000/10^3
    expect(projectLog(1000, 0.5, 3).output).toBe(0);
    // base 2, 2 staves: threshold maxInput/4
    expect(projectLog(8, 4, 2, 2).output).toBeCloseTo(1, 9);
  });

  test('InverseRoundTripsAboveClampThreshold', () => {
    const maxInput = 100;
    for (const input of [1.5, 2, 5, 50, 99, 100]) {
      const { output } = projectLog(maxInput, input);
      expect(projectLogInverse(maxInput, output)).toBeCloseTo(input, 9);
    }
    // at/below the threshold the forward map collapsed to 0, so the inverse is a
    // floor at the threshold value, not an exact round trip
    expect(projectLogInverse(maxInput, 0)).toBeCloseTo(1, 9);
    expect(projectLogInverse(maxInput, projectLog(maxInput, 0.001).output)).toBeCloseTo(1, 9);
  });
});

describe('logXAxisMaps', () => {
  test('EffectiveLeftCapsAtThreeDecades', () => {
    expect(LOG_HORIZONTAL_MAX_DECADES).toBe(3);
    // data left far below the cap: capped to right * 10^-3
    expect(logHEffectiveLeft(0.05, 1000, 100)).toBeCloseTo(1, 9);
    // data left inside the cap: used as-is
    expect(logHEffectiveLeft(10, 1000, 100)).toBeCloseTo(10, 9);
    // non-positive left falls back to the first sample's value right/(length-1)
    expect(logHEffectiveLeft(0, 1000, 101)).toBeCloseTo(10, 9);
    // ... which is itself capped at three decades
    expect(logHEffectiveLeft(0, 1000, 100001)).toBeCloseTo(1, 9);
    // single-sample fallback right*0.01
    expect(logHEffectiveLeft(0, 1000, 1)).toBeCloseTo(10, 9);
    // non-positive right passes through
    expect(logHEffectiveLeft(0, 0, 100)).toBe(0);
    expect(logHEffectiveLeft(0, -5, 100)).toBe(-5);
  });

  test('ValueFractionRoundTrip', () => {
    const effLeft = 1;
    const right = 1000;
    expect(logHValueToFraction(10, effLeft, right)).toBeCloseTo(1 / 3, 9);
    expect(logHFractionToValue(0, effLeft, right)).toBeCloseTo(effLeft, 9);
    expect(logHFractionToValue(1, effLeft, right)).toBeCloseTo(right, 9);
    for (const value of [1.5, 10, 500, 1000]) {
      const frac = logHValueToFraction(value, effLeft, right);
      expect(logHFractionToValue(frac, effLeft, right)).toBeCloseTo(value, 9);
    }
    for (const frac of [0, 0.25, 0.5, 0.75, 1]) {
      const value = logHFractionToValue(frac, effLeft, right);
      expect(logHValueToFraction(value, effLeft, right)).toBeCloseTo(frac, 9);
    }
    // guards: at/below the effective left edge the fraction is 0
    expect(logHValueToFraction(1, effLeft, right)).toBe(0);
    expect(logHValueToFraction(0.5, effLeft, right)).toBe(0);
    expect(logHValueToFraction(10, 0, right)).toBe(0);
  });

  test('PixelToSampleIndexClampsAndIsMonotonic', () => {
    const axis = { leftValue: 1, rightValue: 1000 };
    const count = 1000;
    const width = 100;
    expect(logPixelToSampleIndex(0, width, count, axis)).toBe(0);
    expect(logPixelToSampleIndex(-50, width, count, axis)).toBe(0);
    expect(logPixelToSampleIndex(width, width, count, axis)).toBe(count - 1);
    expect(logPixelToSampleIndex(width * 100, width, count, axis)).toBe(count - 1);
    let prev = 0;
    for (let pixel = 0; pixel < width; pixel++) {
      const index = logPixelToSampleIndex(pixel, width, count, axis);
      expect(index).toBeGreaterThanOrEqual(0);
      expect(index).toBeLessThanOrEqual(count - 1);
      expect(index).toBeGreaterThanOrEqual(prev);
      prev = index;
    }
    // degenerate window (range <= 0) collapses to index 0
    expect(logPixelToSampleIndex(50, width, count, { leftValue: 5, rightValue: 5 })).toBe(0);
  });

  test('FracIndexAndXOffsetAgree', () => {
    // pixel -> fractional index -> back to a pixel offset lands on the same column
    const axis = { leftValue: 1, rightValue: 1000 };
    const count = 1000;
    const width = 200;
    for (const pixel of [1, 10, 50, 100, 150, 199]) {
      const frac = logPixelToFracSampleIndex(pixel, width, count, axis);
      const x = logSampleIndexToXOffset(frac, count, width, axis);
      expect(Math.abs(x - pixel)).toBeLessThanOrEqual(1);
    }
  });
});

describe('projectCurves', () => {
  test('DensePolygonDigitalDispatchesToMinMax', () => {
    const samples = ramp(1000);
    const result = projectCurves(samples, 0, 1000, 100, 'polygonDigital');
    expect(result.mode).toBe('minMax');
    expect(result.min).toBeDefined();
    expect(result.max).toBeDefined();
    expect(result.line).toBeUndefined();
    expect(result.dots).toBeUndefined();
    expect(result.min!.length).toBe(100);
    expect(result.max!.length).toBe(100);
    for (let pixel = 0; pixel < 100; pixel++) {
      expect(result.min![pixel]).toBe(10 * pixel);
      expect(result.max![pixel]).toBe(10 * pixel + 9);
    }
  });

  test('SparsePolygonContinuousDispatchesToInterpolate', () => {
    const samples = ramp(10, 10);
    const result = projectCurves(samples, 0, 10, 100, 'polygonContinuous');
    expect(result.mode).toBe('interpolate');
    expect(result.line).toBeDefined();
    expect(result.min).toBeUndefined();
    expect(result.line!.length).toBe(100);
    for (let k = 0; k < 10; k++) {
      expect(result.line![10 * k]).toBe(10 * k); // exact hits on sample columns
    }
  });

  test('SparsePolygonDigitalDispatchesToNearest', () => {
    const samples = ramp(10);
    const result = projectCurves(samples, 0, 10, 100, 'polygonDigital');
    expect(result.mode).toBe('nearest');
    expect(result.line).toBeDefined();
    for (let pixel = 0; pixel < 100; pixel++) {
      expect(result.line![pixel]).toBe(Math.floor(pixel / 10)); // staircase
    }
  });

  test('MinAndMaxModesProduceSingleLineViaMinMax', () => {
    const samples = sine(100, 5);
    const lo = projectCurves(samples, 0, 100, 10, 'min');
    expect(lo.mode).toBe('minMax');
    expect(lo.line).toBeDefined();
    expect(lo.min).toBeUndefined();
    expect(lo.max).toBeUndefined();
    const hi = projectCurves(samples, 0, 100, 10, 'max');
    expect(hi.mode).toBe('minMax');
    const refLo = projectMinMax(samples, 0, 100, 10, true);
    const refHi = projectMinMax(samples, 0, 100, 10, false);
    for (let pixel = 0; pixel < 10; pixel++) {
      expect(lo.line![pixel]).toBe(refLo[pixel]);
      expect(hi.line![pixel]).toBe(refHi[pixel]);
    }
  });

  test('AverageFallback', () => {
    const samples = Float64Array.from([1, 2, 3, 4, 5, 6, 7, 8]);
    const dense = projectCurves(samples, 0, 8, 4, 'average');
    expect(dense.mode).toBe('average');
    expect(Array.from(dense.line!)).toEqual([1.5, 3.5, 5.5, 7.5]);
    // pixelWidth == count is neither dense nor sparse: even polygonDigital falls
    // through to average, which with one-sample bins is the identity
    const equal = projectCurves(samples, 0, 8, 8, 'polygonDigital');
    expect(equal.mode).toBe('average');
    expect(Array.from(equal.line!)).toEqual([1, 2, 3, 4, 5, 6, 7, 8]);
  });

  test('PointsModesAlsoProduceDots', () => {
    // sparse points: interpolate line plus dots
    const sparse = projectCurves(ramp(10), 0, 10, 100, 'points');
    expect(sparse.mode).toBe('interpolate');
    expect(sparse.line).toBeDefined();
    expect(sparse.dots).toBeDefined();
    expect(Array.from(sparse.dots!.x)).toEqual([10, 20, 30, 40, 50, 60, 70, 80, 90]);
    expect(Array.from(sparse.dots!.y)).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]);
    // dense pointsIfChanged: min/max envelope plus deduplicated dots
    const dense = projectCurves(ramp(1000), 0, 1000, 100, 'pointsIfChanged');
    expect(dense.mode).toBe('minMax');
    expect(dense.min).toBeDefined();
    expect(dense.max).toBeDefined();
    expect(dense.dots).toBeDefined();
    // dotsRange filters dot values
    const filtered = projectCurves(ramp(10), 0, 10, 100, 'points', undefined, {
      lowestValue: 0,
      highestValue: 5,
    });
    expect(Array.from(filtered.dots!.x)).toEqual([10, 20, 30, 40, 50]);
    expect(Array.from(filtered.dots!.y)).toEqual([1, 2, 3, 4, 5]);
  });

  test('EmptyInputsReturnEmptyResult', () => {
    expect(projectCurves(new Float64Array(0), 0, 0, 100, 'polygonDigital')).toEqual({});
    expect(projectCurves(ramp(10), 0, 10, 0, 'polygonDigital')).toEqual({});
    const modes: CurvePaintMode[] = [
      'polygonDigital',
      'polygonContinuous',
      'points',
      'pointsIfChanged',
      'average',
      'min',
      'max',
    ];
    for (const mode of modes) {
      expect(projectCurves(ramp(10), 0, 10, -1, mode).mode).toBeUndefined();
    }
  });
});

describe('drawnExtents', () => {
  test('IgnoresNaNAndReportsFiniteRange', () => {
    const { lowest, highest } = drawnExtents(Float64Array.from([NaN, 2, -1, NaN, 0.5]));
    expect(lowest).toBe(-1);
    expect(highest).toBe(2);
  });

  test('AllNaNOrEmptyReportsNaN', () => {
    const allNaN = drawnExtents(Float64Array.from([NaN, NaN]));
    expect(Number.isNaN(allNaN.lowest)).toBe(true);
    expect(Number.isNaN(allNaN.highest)).toBe(true);
    const empty = drawnExtents(new Float64Array(0));
    expect(Number.isNaN(empty.lowest)).toBe(true);
    expect(Number.isNaN(empty.highest)).toBe(true);
  });
});

describe('largeArray', () => {
  test('MillionSampleSineEnvelopeIsSane', () => {
    // 1e6 samples into 500 px: each 2000-sample bin spans 5 full cycles, so every
    // bin's envelope must reach close to -1/+1 and everything stays finite
    const count = 1000000;
    const width = 500;
    const samples = sine(count, 2500);
    const result = projectCurves(samples, 0, count, width, 'polygonDigital');
    expect(result.mode).toBe('minMax');
    expect(result.min!.length).toBe(width);
    expect(result.max!.length).toBe(width);
    for (let pixel = 0; pixel < width; pixel++) {
      expect(Number.isFinite(result.min![pixel])).toBe(true);
      expect(Number.isFinite(result.max![pixel])).toBe(true);
      expect(result.min![pixel]).toBeLessThanOrEqual(result.max![pixel]);
      expect(result.min![pixel]).toBeLessThan(-0.99);
      expect(result.max![pixel]).toBeGreaterThan(0.99);
    }
    const lo = drawnExtents(result.min!);
    const hi = drawnExtents(result.max!);
    expect(lo.lowest).toBeGreaterThanOrEqual(-1);
    expect(hi.highest).toBeLessThanOrEqual(1);
    const poly = envelopePolygon(result.min!, result.max!);
    expect(poly.x.length).toBe(2 * width);
    expect(poly.y.length).toBe(2 * width);
    expect(poly.x[0]).toBe(0);
    expect(poly.x[width - 1]).toBe(width - 1);
    expect(poly.x[width]).toBe(width - 1); // max envelope starts reversed
    expect(poly.x[2 * width - 1]).toBe(0);
  });
});
