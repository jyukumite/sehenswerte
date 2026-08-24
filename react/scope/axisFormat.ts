// Axis partitioning and label formatting, ported from the C# sehenswerte:
//   DoubleExtensions.RoundSignificant/Up/Down, ToStringRound (SI prefixes + time)
//   PaintTraceBase.GetPartitions / GetLogPartitions
//   Paint2dTrace.ToHorizontalUnit (unix-time formats keyed on the visible span)
// These feed gutter ticks, hover readouts, and auto-range rounding; hover and
// gutter must use the same functions so they agree.

// ---------------------------------------------------------------------------
// significant rounding (C# RoundSignificant family)

function roundSignificantCore(
  value: number,
  significantDigits: number,
  significanceOf: number,
  ifNegative: (v: number) => number,
  ifPositive: (v: number) => number
): number {
  const negative = value < 0;
  let abs = negative ? -value : value;
  const sig = Math.abs(significanceOf);
  if (sig !== 0.0 && value !== 0.0) {
    const prefix = Math.floor(Math.log10(sig));
    const mag = Math.pow(10.0, prefix - significantDigits + 1.0);
    abs = (negative ? ifNegative : ifPositive)(abs / mag) * mag;
  }
  return negative ? -abs : abs;
}

export function roundSignificant(
  value: number,
  significantDigits: number,
  significanceOf: number = value
): number {
  return roundSignificantCore(value, significantDigits, significanceOf, Math.round, Math.round);
}

// Directional: "Up" rounds towards +infinity, "Down" towards -infinity (for
// negatives, Up(-1234,2) = -1200 and Down(-1234,2) = -1300) - matching the C#
// pairs AutoRange uses so [low rounded down, high rounded up] never clips data.
export function roundSignificantDown(
  value: number,
  significantDigits: number,
  significanceOf: number = value
): number {
  return roundSignificantCore(value, significantDigits, significanceOf, Math.ceil, Math.floor);
}

export function roundSignificantUp(
  value: number,
  significantDigits: number,
  significanceOf: number = value
): number {
  return roundSignificantCore(value, significantDigits, significanceOf, Math.floor, Math.ceil);
}

// ---------------------------------------------------------------------------
// number -> label (C# ToStringRound)

// Away-from-zero rounding at N decimal places (C# decimal.Round MidpointRounding.AwayFromZero).
function roundAwayFromZero(value: number, decimals: number): number {
  const mag = Math.pow(10, decimals);
  const abs = Math.round(Math.abs(value) * mag) / mag;
  return value < 0 ? -abs : abs;
}

export function toStringRound(
  value: number,
  significantDigits: number,
  minimumDecimalDigits: number,
  trimRight: boolean = true
): string {
  if (isNaN(value)) return "NaN";
  if (!isFinite(value)) return "Inf";
  if (value === 0.0) return "0";

  const absValue = Math.abs(value);
  let wholeDigits = Math.floor(Math.log10(absValue)) + 1;
  if (wholeDigits < -18 || wholeDigits > 18) {
    // C# {value:#.##E+0}
    const exp = value.toExponential(2).toUpperCase().replace("E+", "E+").replace("E-", "E-");
    return exp.replace(/\.?0+E/, "E");
  }

  wholeDigits -= significantDigits;
  if (wholeDigits > -minimumDecimalDigits) {
    wholeDigits = -minimumDecimalDigits;
  }
  const decimals = Math.min(25, Math.max(0, -wholeDigits));
  const rounded = roundAwayFromZero(value, decimals);
  if (trimRight) {
    // decimal /1.0m trick: shortest representation without trailing zeros
    return String(parseFloat(rounded.toFixed(Math.min(20, decimals))));
  }
  return rounded.toFixed(Math.max(minimumDecimalDigits, decimals));
}

const SI_PREFIXES = ["f", "p", "n", "u", "m", "", "k", "M", "G", "T"];

// Duration rendering for unit "s" at or above one minute (C# ToStringRoundTime);
// returns "" below one minute so the SI-prefix path takes over (0.5 -> "500ms").
function toStringRoundTime(value: number, significantDigits: number): string {
  const totalSeconds = Math.trunc(Math.abs(value));
  const ms = Math.trunc(1000.0 * (Math.abs(value) - totalSeconds));
  const days = Math.trunc(totalSeconds / 86400);
  const hours = Math.trunc((totalSeconds % 86400) / 3600);
  const minutes = Math.trunc((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  const two = (n: number): string => String(n).padStart(2, "0");
  let result = "";
  if (days !== 0) {
    result = `${days}d${two(hours)}h${two(minutes)}`;
  } else if (hours !== 0) {
    result = seconds === 0 ? `${hours}h${two(minutes)}` : `${hours}h${two(minutes)}:${two(seconds)}`;
  } else if (minutes !== 0) {
    result = `${minutes}:${two(seconds)}`;
    if (ms !== 0 && significantDigits >= 3) {
      result += "." + String(ms).padStart(3, "0").slice(0, Math.max(1, significantDigits - 2));
    }
  }
  return result === "" ? "" : (value < 0 ? "-" : "") + result;
}

// C# ToStringRound(value, sig, minDec, unit): unit "s" tries the duration form
// first; otherwise SI-prefix the value (femto..tera) and append the unit.
export function toStringRoundUnit(
  value: number,
  significantDigits: number,
  minimumDecimalDigits: number,
  unit: string,
  trimRight: boolean = true
): string {
  if (isNaN(value)) return "NaN";
  if (!isFinite(value)) return "Infinity";
  if (value === 0.0) return "0";

  if (unit === "s") {
    const time = toStringRoundTime(value, significantDigits);
    if (time !== "") return time;
  }
  if (unit === "") {
    return toStringRound(value, significantDigits, minimumDecimalDigits, trimRight);
  }
  let prefixIndex = Math.floor(Math.log10(Math.abs(value)) / 3);
  prefixIndex = Math.max(-5, Math.min(4, prefixIndex));
  const scaledValue = value / Math.pow(10, prefixIndex * 3);
  return (
    toStringRound(scaledValue, significantDigits, minimumDecimalDigits, trimRight) +
    SI_PREFIXES[prefixIndex + 5] +
    unit
  );
}

// ---------------------------------------------------------------------------
// tick positions

// Linear ticks at multiples of a 1-significant-digit step (C# GetPartitions).
// Ascending; at most `count` ticks inside [low, high].
export function getPartitions(low: number, high: number, count: number): number[] {
  const list: number[] = [];
  if (count <= 0 || !isFinite(low) || !isFinite(high)) return list;
  if (high < low) {
    const temp = low;
    low = high;
    high = temp;
  }
  let skip = (high - low) / count;
  skip = roundSignificantUp(skip, 1, skip);
  if (skip <= 0 || !isFinite(skip)) return list;
  const start = Math.ceil(low / skip) * skip;
  for (let loop = 0; loop < count; loop++) {
    const partition = start + loop * skip;
    if (partition <= high) {
      list.push(partition);
    }
  }
  return list;
}

// 1-2-5 ticks per decade on a log axis (C# GetLogPartitions).
export function getLogPartitions(low: number, high: number): number[] {
  if (low <= 0) low = high * 0.01;
  if (low <= 0 || high <= 0 || low >= high) return [];
  const result: number[] = [];
  const decade = Math.pow(10.0, Math.floor(Math.log10(low)));
  for (let d = decade / 10.0; d <= high * 10.0; d *= 10.0) {
    for (const m of [1.0, 2.0, 5.0]) {
      const tick = d * m;
      if (tick >= low && tick <= high) {
        result.push(tick);
      }
    }
  }
  return result;
}

// ---------------------------------------------------------------------------
// unix-time labels (C# Paint2dTrace.ToHorizontalUnit YT path). Formats are
// keyed on the visible span and rendered in UTC - NOT local time - matching
// the C# (DateTimeKind.Utc); datcli and the scope agree because of this.

const pad2 = (n: number): string => String(n).padStart(2, "0");
const pad3 = (n: number): string => String(n).padStart(3, "0");

function utcParts(unixTime: number) {
  const d = new Date(unixTime * 1000);
  return {
    year: d.getUTCFullYear(),
    month: pad2(d.getUTCMonth() + 1),
    day: pad2(d.getUTCDate()),
    hours: pad2(d.getUTCHours()),
    minutes: pad2(d.getUTCMinutes()),
    seconds: pad2(d.getUTCSeconds()),
    ms: d.getUTCMilliseconds(),
  };
}

// Millisecond suffix like .NET "FFF": omitted when zero, trailing zeros trimmed.
function msSuffix(ms: number): string {
  if (ms === 0) return "";
  return "." + pad3(ms).replace(/0+$/, "");
}

export function formatUnixTime(unixTime: number, visibleSpanSeconds: number, full: boolean = false): string {
  const t = utcParts(unixTime);
  const span = Math.abs(visibleSpanSeconds);
  if (full) {
    return `${t.year}/${t.month}/${t.day} ${t.hours}:${t.minutes}:${t.seconds}${msSuffix(t.ms)}`;
  }
  if (span >= 604800.0) {
    return `${t.year}/${t.month}/${t.day}`;
  }
  if (span >= 86400.0) {
    return `${t.year}/${t.month}/${t.day} ${t.hours}:${t.minutes}`;
  }
  if (span >= 3600.0) {
    return `${t.hours}:${t.minutes}:${t.seconds}`;
  }
  if (span >= 60.0) {
    return `${t.hours}:${t.minutes}:${t.seconds}${msSuffix(t.ms)}`;
  }
  // sub-minute window: seconds.fff "s"
  const seconds = Number(t.seconds) + t.ms / 1000;
  return `${seconds.toFixed(3).replace(/\.?0+$/, "")} s`;
}
