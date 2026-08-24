// Tests for the axis partition/format port (C# DoubleExtensions +
// PaintTraceBase.GetPartitions/GetLogPartitions + Paint2dTrace.ToHorizontalUnit).

import {
  formatUnixTime,
  getLogPartitions,
  getPartitions,
  roundSignificant,
  roundSignificantDown,
  roundSignificantUp,
  toStringRound,
  toStringRoundUnit,
} from "./axisFormat";

describe("roundSignificant family", () => {
  test("roundSignificant rounds to significant digits", () => {
    expect(roundSignificant(1234, 2)).toBe(1200);
    expect(roundSignificant(0.01234, 2)).toBeCloseTo(0.012, 10);
    expect(roundSignificant(-1234, 2)).toBe(-1200);
  });

  test("up/down are directional towards +/- infinity (AutoRange usage)", () => {
    expect(roundSignificantUp(1234, 2)).toBe(1300);
    expect(roundSignificantDown(1234, 2)).toBe(1200);
    expect(roundSignificantUp(-1234, 2)).toBe(-1200); // towards +infinity
    expect(roundSignificantDown(-1234, 2)).toBe(-1300); // towards -infinity
  });

  test("significanceOf controls the magnitude, not the value", () => {
    // AutoRange: high.RoundSignificantUp(3, high - low)
    expect(roundSignificantUp(105.0, 3, 10.0)).toBeCloseTo(105.0, 10);
    expect(roundSignificantUp(105.02, 3, 10.0)).toBeCloseTo(105.1, 10);
  });

  test("zero and zero-significance pass through", () => {
    expect(roundSignificant(0, 3)).toBe(0);
    expect(roundSignificantUp(5, 3, 0)).toBe(5);
  });
});

describe("toStringRound", () => {
  test("specials", () => {
    expect(toStringRound(NaN, 5, 3)).toBe("NaN");
    expect(toStringRound(Infinity, 5, 3)).toBe("Inf");
    expect(toStringRound(0, 5, 3)).toBe("0");
  });

  test("rounds to significant digits and trims trailing zeros", () => {
    // minimumDecimalDigits=3 widens precision past the 5 significant digits
    expect(toStringRound(500.005, 5, 3)).toBe("500.005");
    expect(toStringRound(1.5, 5, 3)).toBe("1.5");
    expect(toStringRound(123.456789, 5, 3)).toBe("123.457"); // minDec=3 again
    expect(toStringRound(123.456789, 5, 0)).toBe("123.46"); // pure 5 sig digits
  });

  test("huge magnitudes go exponential", () => {
    expect(toStringRound(1e19, 5, 3)).toContain("E+");
  });
});

describe("toStringRoundUnit", () => {
  test("SI prefixes", () => {
    expect(toStringRoundUnit(1500, 5, 3, "Hz")).toBe("1.5kHz");
    expect(toStringRoundUnit(0.0015, 5, 3, "V")).toBe("1.5mV");
    expect(toStringRoundUnit(2500000, 5, 3, "W")).toBe("2.5MW");
  });

  test("unit s renders durations at or above a minute", () => {
    expect(toStringRoundUnit(90, 5, 3, "s")).toBe("1:30");
    expect(toStringRoundUnit(3660, 5, 3, "s")).toBe("1h01");
    expect(toStringRoundUnit(90061, 5, 3, "s")).toBe("1d01h01");
  });

  test("unit s below a minute falls through to SI prefix", () => {
    expect(toStringRoundUnit(0.5, 5, 3, "s")).toBe("500ms");
  });

  test("no unit falls through to plain rounding", () => {
    expect(toStringRoundUnit(12.25, 5, 3, "")).toBe("12.25");
  });
});

describe("getPartitions", () => {
  test("ticks at multiples of a 1-significant-digit step inside the range", () => {
    const parts = getPartitions(0, 100, 5);
    expect(parts.length).toBeGreaterThan(0);
    expect(parts.length).toBeLessThanOrEqual(5);
    const step = parts.length > 1 ? parts[1] - parts[0] : 0;
    for (let loop = 1; loop < parts.length; loop++) {
      expect(parts[loop] - parts[loop - 1]).toBeCloseTo(step, 9);
    }
    parts.forEach((p) => {
      expect(p).toBeGreaterThanOrEqual(0);
      expect(p).toBeLessThanOrEqual(100);
    });
  });

  test("reversed range still yields ascending ticks", () => {
    const parts = getPartitions(100, 0, 5);
    expect(parts.length).toBeGreaterThan(0);
    for (let loop = 1; loop < parts.length; loop++) {
      expect(parts[loop]).toBeGreaterThan(parts[loop - 1]);
    }
  });

  test("degenerate inputs give no ticks", () => {
    expect(getPartitions(0, 0, 5)).toEqual([]);
    expect(getPartitions(NaN, 1, 5)).toEqual([]);
    expect(getPartitions(0, 1, 0)).toEqual([]);
  });
});

describe("getLogPartitions", () => {
  test("1-2-5 per decade inside the range", () => {
    const parts = getLogPartitions(1, 100);
    expect(parts).toEqual([1, 2, 5, 10, 20, 50, 100]);
  });

  test("non-positive low falls back to two decades below high", () => {
    const parts = getLogPartitions(0, 100);
    expect(parts[0]).toBe(1);
    expect(parts[parts.length - 1]).toBe(100);
  });

  test("empty when unusable", () => {
    expect(getLogPartitions(10, 1)).toEqual([]);
    expect(getLogPartitions(-1, 0)).toEqual([]);
  });
});

describe("formatUnixTime", () => {
  // 2026-08-23 01:02:03.500 UTC
  const t = Date.UTC(2026, 7, 23, 1, 2, 3, 500) / 1000;

  test("formats are keyed on the visible span, in UTC", () => {
    expect(formatUnixTime(t, 700000)).toBe("2026/08/23");
    expect(formatUnixTime(t, 100000)).toBe("2026/08/23 01:02");
    expect(formatUnixTime(t, 7200)).toBe("01:02:03");
    expect(formatUnixTime(t, 120)).toBe("01:02:03.5");
    expect(formatUnixTime(t, 30)).toBe("3.5 s");
  });

  test("full format includes date, time, and trimmed milliseconds", () => {
    expect(formatUnixTime(t, 1, true)).toBe("2026/08/23 01:02:03.5");
    const whole = Date.UTC(2026, 7, 23, 1, 2, 3, 0) / 1000;
    expect(formatUnixTime(whole, 1, true)).toBe("2026/08/23 01:02:03");
  });
});
