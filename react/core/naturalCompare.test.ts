// Ported from C# NaturalCompareTest (sehenswerte NaturalStringCompare.cs).

import { naturalCompare, naturalCompareNullable } from './naturalCompare';

function testless(a: string, b: string): void {
  expect(naturalCompare(a, b)).toBeLessThan(0);
  expect(naturalCompare(b, a)).toBeGreaterThan(0);
}

function testsame(a: string, b: string): void {
  expect(naturalCompare(a, b)).toBe(0);
  expect(naturalCompare(b, a)).toBe(0);
}

describe('naturalCompare', () => {
  test('equal strings', () => {
    testsame('', '');
    testsame('a2a', 'a2a');
    testsame('test2', 'test2');
    testsame('test2a', 'test2a');
    testsame('a-2a', 'a-2a');
    testsame('test-2', 'test-2');
    testsame('test-2a', 'test-2a');
  });

  test('basic ordering', () => {
    testless('', 'a');
    testless('a', 'aa');
    testless('a', 'b');
    testless('a1', 'a1a');
    testsame('a1.5a', 'a01.5a');
    testless('a1a', 'a2a');
    testless('test1', 'test2');
    testless('test2', 'test12');
    testless('test1a', 'test1b');
  });

  test('decimals compare numerically', () => {
    testsame('test1.50a', 'test1.5a');
    testsame('test-1.50a', 'test-1.5a');
    testsame('test1-1.50a', 'test1-1.5a'); // negative cancels the number but . brings it back
  });

  test('date-like segments', () => {
    testsame('test2022-1-7a', 'test2022-01-07a');
    testless('test2022-1-7a', 'test2022-02-07a');
    testless('test2022-1-7a', 'test2022-01-8a');
    testless('test2022-1-7a', 'test2022-1-8a');
  });

  test('mixed decimal segments', () => {
    testless('test2022-1.4-7a', 'test2022-1.5-8a');
    testless('test2022-1.4-7a', 'test2022-1.51-8a');
    testless('test2022-1.419-7a', 'test2022-1.420-8a');
    testless('test2022-1.419-7a', 'test2022-1.4191-8a');
  });

  test('leading decimal point and negatives', () => {
    testless('a.5b', 'a.6b');
    testless('a.5b', 'a.51b');
    testless('a0.5b', 'a0.51b');
    testless('a-.51b', 'a-.5b');
    testless('a-0.51b', 'a-0.5b');
  });

  test('commas as part of a number', () => {
    testless('1,234,567.89', '1,23,5000.42');
  });

  test('dotted numerics (version-like strings)', () => {
    testless('1.12.3', '1.23.10'); // multiple periods in this form should cancel a decimal
    testless('1.12.3.20', '1.12.3.100');
    testless('1.12.3.20-a-1.2-b', '2.83.10.0-a-1.25-b');
  });

  // C# ExtensionMatchesStaticMethod: the TS port has a single function.
  test('extension parity cases', () => {
    expect(naturalCompare('a2', 'a10')).toBeLessThan(0);
    expect(naturalCompare('a10', 'a2')).toBeGreaterThan(0);
    expect(naturalCompare('test1.5a', 'test1.50a')).toBe(0);
  });
});

describe('naturalCompareNullable', () => {
  // Mirrors the C# IComparer<string>.Compare null handling: nulls sort to top.
  test('nulls sort to top', () => {
    expect(naturalCompareNullable(null, null)).toBe(0);
    expect(naturalCompareNullable(undefined, undefined)).toBe(0);
    expect(naturalCompareNullable(null, 'a')).toBe(-1);
    expect(naturalCompareNullable('a', null)).toBe(1);
    expect(naturalCompareNullable('a2', 'a10')).toBeLessThan(0);
  });

  test('case-insensitive text compare', () => {
    expect(naturalCompareNullable('ABC', 'abc')).toBe(0);
    expect(naturalCompareNullable('ABC2', 'abc10')).toBeLessThan(0);
  });
});
