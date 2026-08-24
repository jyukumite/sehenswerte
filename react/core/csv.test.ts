// Ported from C# CSVLoadTests and CSVSaveTests (sehenswerte CSVLoad.cs / CSVSave.cs).

import { parseCsv, parseCsvRows, rowToCsvText, toCsvText } from './csv';

describe('parseCsv (CSVLoad port)', () => {
  test('basic', () => {
    const csv = parseCsv('a,b,c\n1,2,3\n4,5,6\n');
    expect(csv.columnHeadings).toEqual(['a', 'b', 'c']);
    expect(csv.rows.length).toBe(2);
    expect(csv.rows[0]).toEqual(['1', '2', '3']);
    expect(csv.rows[1]).toEqual(['4', '5', '6']);
  });

  test('null fields', () => {
    // empty fields between commas parse as empty string
    const csv = parseCsv('a,b,c\n1,,3\n,b2,\n');
    expect(csv.rows[0]).toEqual(['1', '', '3']);
    expect(csv.rows[1]).toEqual(['', 'b2', '']);
  });

  test('blank line', () => {
    // blank line in data becomes a row of empty strings
    const csv = parseCsv('a,b,c\n1,2,3\n\n4,5,6\n');
    expect(csv.rows.length).toBe(3);
    expect(csv.rows[1]).toEqual(['', '', '']);
    expect(csv.rows[2]).toEqual(['4', '5', '6']);
  });

  test('quoted comma', () => {
    // comma inside quotes is not a separator
    const csv = parseCsv('a,b,c\n"hello, world",2,3\n');
    expect(csv.rows[0]).toEqual(['hello, world', '2', '3']);
  });

  test('quoted newline', () => {
    // newline inside quotes merges lines into a single field
    const csv = parseCsv('a,b,c\n"line1\nline2",2,3\n');
    expect(csv.rows.length).toBe(1);
    expect(csv.rows[0][0]).toBe('line1\nline2');
    expect(csv.rows[0][1]).toBe('2');
    expect(csv.rows[0][2]).toBe('3');
  });

  test('fewer headers than columns', () => {
    // data row has more columns than header - extras are dropped
    const csv = parseCsv('a,b\n1,2,3,4\n');
    expect(csv.columnHeadings.length).toBe(2);
    expect(csv.rows[0]).toEqual(['1', '2']);
  });

  test('more headers than columns', () => {
    // data row has fewer commas than header - missing columns padded with empty string
    const csv = parseCsv('a,b,c\n1,2\n');
    expect(csv.columnHeadings.length).toBe(3);
    expect(csv.rows[0]).toEqual(['1', '2', '']);
  });

  test('trailing dangling comma in header', () => {
    // trailing comma on header row is stripped
    const csv = parseCsv('a,b,c,\n1,2,3\n');
    expect(csv.columnHeadings.length).toBe(3);
    expect(csv.columnHeadings).toEqual(['a', 'b', 'c']);
  });

  // C# TestColumnIndexer adapted: column lookup by heading name.
  test('column lookup by heading', () => {
    const csv = parseCsv('a,b,c\n1,2,3\n');
    const row = csv.rows[0];
    expect(row[csv.columnHeadings.indexOf('a')]).toBe('1');
    expect(row[csv.columnHeadings.indexOf('b')]).toBe('2');
    expect(row[csv.columnHeadings.indexOf('c')]).toBe('3');
    expect(csv.columnHeadings.indexOf('missing')).toBe(-1);
  });

  // Extra coverage below (thin in C#).

  test('crlf and bare cr line endings', () => {
    const csv = parseCsv('a,b,c\r\n1,2,3\r4,5,6\r\n');
    expect(csv.columnHeadings).toEqual(['a', 'b', 'c']);
    expect(csv.rows).toEqual([['1', '2', '3'], ['4', '5', '6']]);
  });

  test('no trailing newline', () => {
    const csv = parseCsv('a,b\n1,2');
    expect(csv.rows).toEqual([['1', '2']]);
  });

  test('empty text', () => {
    const csv = parseCsv('');
    expect(csv.columnHeadings).toEqual([]);
    expect(csv.rows).toEqual([]);
  });

  test('header row prefix skips leading junk rows', () => {
    // rows before the prefixed header are discarded; the prefix stays in the heading
    const csv = parseCsv('junk1\njunk2\n#a,b\n1,2\n', { headerRowPrefix: '#' });
    expect(csv.columnHeadings).toEqual(['#a', 'b']);
    expect(csv.rows).toEqual([['1', '2']]);
  });

  test('custom separator', () => {
    const csv = parseCsv('a;b\n1;2\n', { separator: ';' });
    expect(csv.columnHeadings).toEqual(['a', 'b']);
    expect(csv.rows).toEqual([['1', '2']]);
  });

  test('doubled quote inside quoted field is dropped (C# parity)', () => {
    // The C# loader toggles quote state on every '"' and never emits one,
    // so "say ""hi""" loads as 'say hi', not 'say "hi"'.
    const csv = parseCsv('a,b\n"say ""hi""",2\n');
    expect(csv.rows[0]).toEqual(['say hi', '2']);
  });
});

describe('parseCsvRows (raw rows, no header semantics)', () => {
  test('returns every logical row unpadded', () => {
    expect(parseCsvRows('a,b,c\n1,2\n')).toEqual([['a', 'b', 'c'], ['1', '2']]);
    expect(parseCsvRows('')).toEqual([]);
    expect(parseCsvRows('"x\ny",z\n')).toEqual([['x\ny', 'z']]);
  });
});

describe('rowToCsvText (CSVSave port)', () => {
  test('basic', () => {
    expect(rowToCsvText(['a', 'b', 'c'])).toBe('a,b,c');
    expect(rowToCsvText([1, 2.5, 3])).toBe('1,2.5,3');
    expect(rowToCsvText(['a', null, 'c'])).toBe('a,null,c');
    expect(rowToCsvText(['a', 'hel,lo', 'c'])).toBe('a,"hel,lo",c');
    expect(rowToCsvText(['a', 'say "hi"', 'c'])).toBe('a,"say ""hi""",c');
    expect(rowToCsvText(['a', 'line1\nline2', 'c'])).toBe('a,"line1\nline2",c');
    expect(rowToCsvText([])).toBe('');
    expect(rowToCsvText(['only'])).toBe('only');
  });

  test('quoteEscape false leaves values raw', () => {
    expect(rowToCsvText(['hel,lo', 'x'], ',', 'null', false)).toBe('hel,lo,x');
  });

  test('custom valueIfNull and separator', () => {
    expect(rowToCsvText(['a', undefined, 'c'], ';', '')).toBe('a;;c');
  });
});

describe('toCsvText', () => {
  test('header plus rows, newline-terminated', () => {
    expect(toCsvText([['1', '2'], ['3', '4']], ['a', 'b'])).toBe('a,b\n1,2\n3,4\n');
    expect(toCsvText([['1', '2']])).toBe('1,2\n');
    expect(toCsvText([])).toBe('');
  });

  test('round-trips through parseCsv', () => {
    const header = ['a', 'b', 'c'];
    const rows = [
      ['plain', 'hel,lo', 'line1\nline2'],
      ['', '2', '3'],
    ];
    const text = toCsvText(rows, header);
    const parsed = parseCsv(text);
    expect(parsed.columnHeadings).toEqual(header);
    expect(parsed.rows).toEqual(rows);
  });
});
