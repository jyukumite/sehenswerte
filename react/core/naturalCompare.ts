// Port of sehenswerte NaturalStringCompare (C#): natural/numeric-aware string compare.
// Only understands invariant-culture numbers (period for decimals, numeric commas ignored).

function isDigit(c: string): boolean {
  return c >= '0' && c <= '9';
}

// True when offset sits in the middle of a dotted-numeric run like "1.12.3":
// previous char is '.', current is a digit, and the digit run ends at another '.'.
function isDottedNumeric(s: string, offset: number): boolean {
  const cm1 = offset !== 0 ? s[offset - 1] : '\0';
  if (cm1 !== '.') return false;

  const c0 = s.length - offset > 0 ? s[offset] : '\0';
  if (!isDigit(c0)) return false;

  offset++;
  while (offset < s.length) {
    const c1 = s[offset];
    if (c1 === '.') return true;
    if (!isDigit(c1)) return false;
    offset++;
  }
  return false;
}

// state.dot mirrors the C# "ref bool dot": once a decimal point is consumed in a
// token, later '.', ',' and '-' no longer extend the numeric run.
function isNumeric(s: string, offset: number, state: { dot: boolean }): boolean {
  const cm1 = offset !== 0 ? s[offset - 1] : '\0';
  const c0 = s.length - offset > 0 ? s[offset] : '\0';
  const c1 = s.length - offset > 1 ? s[offset + 1] : '\0';
  const c2 = s.length - offset > 2 ? s[offset + 2] : '\0';
  if (isDigit(c0)) {
    return true;
  }
  if (!state.dot && c0 === '.' && isDigit(c1)) {
    state.dot = true;
    return true;
  }
  if (!state.dot && c0 === ',' && isDigit(c1)) {
    // comma as part of a number
    return true;
  }
  if (!state.dot && !isDigit(cm1) && c0 === '-' && isDigit(c1)) {
    return true;
  }
  if (!state.dot && !isDigit(cm1) && c0 === '-' && c1 === '.' && isDigit(c2)) {
    return true;
  }
  return false;
}

function skipForward(str: string, pos: { idx: number }): { isNum: boolean; text: string } {
  const length = str.length;
  let text = '';
  const state = { dot: false };
  const isNum = isNumeric(str, pos.idx, state);
  const startedDottedNum = isDottedNumeric(str, pos.idx); // middle of a dotted numeric

  do {
    if (!startedDottedNum && isDottedNumeric(str, pos.idx)) break;
    text += str[pos.idx];
    if (++pos.idx >= length) break;
    if (startedDottedNum && str[pos.idx - 1] === '.') break;
  } while (isNumeric(str, pos.idx, state) === isNum);
  return { isNum, text };
}

// Mirrors C# double.TryParse(NumberStyles.Any, InvariantCulture): commas are
// thousands separators (any placement), failure yields 0.
function parseInvariantDouble(text: string): number {
  const value = parseFloat(text.replace(/,/g, ''));
  return isNaN(value) ? 0 : value;
}

export function naturalCompare(lhs: string, rhs: string): number {
  const lhsLength = lhs.length;
  const rhsLength = rhs.length;
  const posl = { idx: 0 };
  const posr = { idx: 0 };
  while (posl.idx < lhsLength && posr.idx < rhsLength) {
    const left = skipForward(lhs, posl);
    const right = skipForward(rhs, posr);

    let result: number;
    if (left.isNum && right.isNum) {
      const parsel = parseInvariantDouble(left.text);
      const parser = parseInvariantDouble(right.text);
      result = parsel < parser ? -1 : parsel > parser ? 1 : 0;
    } else {
      const textl = left.text.toLowerCase();
      const textr = right.text.toLowerCase();
      result = textl < textr ? -1 : textl > textr ? 1 : 0;
    }
    if (result !== 0) {
      return result;
    }
  }
  return posl.idx === lhsLength && posr.idx === rhsLength ? 0 : lhsLength > rhsLength ? 1 : -1;
}

// Mirrors the C# IComparer<string>.Compare: nulls sort to top.
export function naturalCompareNullable(
  x: string | null | undefined,
  y: string | null | undefined
): number {
  const xNull = x === null || x === undefined;
  const yNull = y === null || y === undefined;
  if (xNull && yNull) return 0;
  if (xNull) return -1;
  if (yNull) return 1;
  return naturalCompare(x as string, y as string);
}
