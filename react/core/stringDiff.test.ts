// Ported from C# StringDiffTests (sehenswerte StringDiff.cs).

import { DiffSegment, DiffSide, stringDiff, diffLeftText, diffRightText } from './stringDiff';

function countWhere(diffs: DiffSegment[], side: DiffSide): number {
  return diffs.filter((s) => s.side === side).reduce((sum, s) => sum + s.text.length, 0);
}

describe('stringDiff', () => {
  test('equal strings all common', () => {
    const s = 'allow_auto,debug_upload';
    const diff = stringDiff(s, s);
    expect(diff.length).toBe(1);
    expect(diff[0]).toEqual({ text: s, side: 'both' });
  });

  test('no overlap emits left then right', () => {
    const diff = stringDiff('abc', 'xyz');
    expect(diff.length).toBe(2);
    expect(diff[0]).toEqual({ text: 'abc', side: 'left' });
    expect(diff[1]).toEqual({ text: 'xyz', side: 'right' });
  });

  test('empty left emits right only', () => {
    const diff = stringDiff('', 'abc');
    expect(diff.length).toBe(1);
    expect(diff[0]).toEqual({ text: 'abc', side: 'right' });
  });

  test('empty right emits left only', () => {
    const diff = stringDiff('abc', '');
    expect(diff.length).toBe(1);
    expect(diff[0]).toEqual({ text: 'abc', side: 'left' });
  });

  test('both empty returns empty', () => {
    expect(stringDiff('', '').length).toBe(0);
    expect(stringDiff(null, null).length).toBe(0);
    expect(stringDiff(undefined, undefined).length).toBe(0);
  });

  test('middle insertion', () => {
    // 'foo,bar,baz' vs 'foo,quux,bar,baz' -- 'quux,' is inserted in right.
    const diff = stringDiff('foo,bar,baz', 'foo,quux,bar,baz');
    expect(countWhere(diff, 'both') + countWhere(diff, 'left')).toBe('foo,bar,baz'.length);
    expect(countWhere(diff, 'both') + countWhere(diff, 'right')).toBe('foo,quux,bar,baz'.length);
    expect(countWhere(diff, 'right')).toBeGreaterThanOrEqual('quux'.length);
  });

  test('no adjacent same-side segments', () => {
    // Adjacency merging: consecutive same-side segments must be coalesced.
    const diff = stringDiff('foo bar baz', 'foo qux baz');
    for (let loop = 1; loop < diff.length; loop++) {
      expect(diff[loop - 1].side).not.toBe(diff[loop].side);
    }
  });

  test('whitespace preserved', () => {
    // Whitespace runs are tokens, so they participate in matching.
    const diff = stringDiff('foo bar', 'foo bar');
    expect(diff.length).toBe(1);
    expect(diff[0]).toEqual({ text: 'foo bar', side: 'both' });
  });

  test('case-insensitive matching', () => {
    const diff = stringDiff('Foo,Bar', 'foo,bar');
    // All matched -> single common segment using left's casing.
    expect(diff.length).toBe(1);
    expect(diff[0]).toEqual({ text: 'Foo,Bar', side: 'both' });
  });

  test('leftText and rightText reconstruct inputs', () => {
    const diff = stringDiff('foo,bar,baz', 'foo,quux,bar,baz');
    expect(diffLeftText(diff)).toBe('foo,bar,baz');
    expect(diffRightText(diff)).toBe('foo,quux,bar,baz');
  });

  // The next three mirror the C# string-extension tests; the TS port has one function.

  test('left fully contained in right has no left segments', () => {
    const diff = stringDiff('foo,bar,baz', 'foo,quux,bar,baz');
    expect(diffLeftText(diff)).toBe('foo,bar,baz');
    expect(diff.some((s) => s.side === 'left')).toBe(false);
  });

  test('marks differing tokens', () => {
    const diff = stringDiff('foo,bar,baz', 'foo,baz');
    expect(diffLeftText(diff)).toBe('foo,bar,baz');
    // 'bar' must be flagged left-only.
    expect(countWhere(diff, 'left')).toBeGreaterThanOrEqual('bar'.length);
  });

  test('no overlap reconstructs left', () => {
    const diff = stringDiff('abc', 'xyz');
    expect(diffLeftText(diff)).toBe('abc');
    expect(diff[0]).toEqual({ text: 'abc', side: 'left' });
    expect(diff[1]).toEqual({ text: 'xyz', side: 'right' });
  });

  // Extra coverage (thin in C#).

  test('reconstruction invariant on mixed edits', () => {
    const left = 'alpha beta, gamma-delta 42';
    const right = 'alpha zeta, gamma-delta 43 tail';
    const diff = stringDiff(left, right);
    expect(diffLeftText(diff)).toBe(left);
    expect(diffRightText(diff)).toBe(right);
  });

  test('punctuation is tokenised singly', () => {
    // ',' vs ';' between identical words: only the punctuation differs.
    const diff = stringDiff('a,b', 'a;b');
    expect(diffLeftText(diff)).toBe('a,b');
    expect(diffRightText(diff)).toBe('a;b');
    expect(countWhere(diff, 'left')).toBe(1);
    expect(countWhere(diff, 'right')).toBe(1);
    expect(countWhere(diff, 'both')).toBe(2);
  });
});
