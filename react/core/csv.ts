// Port of the string-matrix core of sehenswerte CSVLoad/CSVSave (C#).
// File/stream/gzip plumbing and the typed CSVRow/enumerator wrappers are not ported;
// the TS API takes and returns strings.

export interface CsvTable {
  columnHeadings: string[];
  rows: string[][];
}

export interface CsvParseOptions {
  separator?: string; // single character
  headerRowPrefix?: string;
}

// Mirrors StreamReader.ReadLine: lines split on \r\n, \r, or \n; a trailing
// newline does not produce a final empty line.
function splitLines(text: string): string[] {
  if (text.length === 0) return [];
  const lines = text.split(/\r\n|\r|\n/);
  if (lines.length > 0 && lines[lines.length - 1] === '') {
    lines.pop();
  }
  return lines;
}

// Stateful merged-row reader; quote state deliberately persists across rows,
// exactly like the C# QuotedText member.
function makeRowReader(lines: string[], separator: string) {
  let index = 0;
  let quoted = false;
  return {
    hasMore: () => index < lines.length,
    peekLine: () => (index < lines.length ? lines[index] : null),
    // Reads one logical row, merging physical lines while inside quotes.
    // Note: '"' toggles quote state and is never emitted, so a doubled quote
    // ("") inside a quoted field is dropped, not unescaped - C# parity.
    readMergedRow(): string[] | null {
      if (index >= lines.length) return null;
      const row: string[] = [];
      do {
        const line = lines[index++];
        if (row.length === 0) {
          row.push('');
        } else {
          row[row.length - 1] += '\n'; // newline inside quotes joins with \n regardless of source line ending
        }
        for (let i = 0; i < line.length; i++) {
          const c = line[i];
          if (c === separator && !quoted) {
            row.push('');
          } else if (c === '"') {
            quoted = !quoted;
          } else {
            row[row.length - 1] += c;
          }
        }
      } while (quoted && index < lines.length);
      return row;
    },
  };
}

// Raw parse: every logical row as-is, no header handling, no padding.
export function parseCsvRows(text: string, separator: string = ','): string[][] {
  const reader = makeRowReader(splitLines(text), separator);
  const rows: string[][] = [];
  let row: string[] | null;
  while ((row = reader.readMergedRow()) !== null) {
    rows.push(row);
  }
  return rows;
}

// Full CSVLoad semantics: first (prefix-matching) row is the header, trailing
// empty header cells are stripped, and every data row is padded/truncated to
// the header width.
export function parseCsv(text: string, options: CsvParseOptions = {}): CsvTable {
  const separator = options.separator !== undefined ? options.separator : ',';
  const headerRowPrefix = options.headerRowPrefix !== undefined ? options.headerRowPrefix : '';
  const reader = makeRowReader(splitLines(text), separator);

  // ReadHeader
  let columnHeadings: string[] = [];
  let found = false;
  while (reader.hasMore() && !found) {
    const firstLine = reader.peekLine();
    const headings = reader.readMergedRow();
    if (
      headings !== null &&
      headings.length !== 0 &&
      (headerRowPrefix === '' || (firstLine !== null && firstLine.indexOf(headerRowPrefix) === 0))
    ) {
      while (headings.length > 0 && headings[headings.length - 1] === '') {
        headings.pop(); // dangling ,
      }
      columnHeadings = headings;
      found = true;
    }
  }

  // CompleteLoad
  const colCount = columnHeadings.length;
  const rows: string[][] = [];
  let row: string[] | null;
  while ((row = reader.readMergedRow()) !== null) {
    while (row.length < colCount) {
      row.push(''); // pad to correct length
    }
    rows.push(row.slice(0, colCount));
  }

  return { columnHeadings, rows };
}

export type CsvValue = string | number | null | undefined;

// Port of CSVSave.RowToCsvText. Numbers serialize like C# invariant culture
// for ordinary values. Assumes a single-character separator.
export function rowToCsvText(
  row: ReadonlyArray<CsvValue>,
  separator: string = ',',
  valueIfNull: string = 'null',
  quoteEscape: boolean = true
): string {
  const parts: string[] = [];
  for (let i = 0; i < row.length; i++) {
    const data = row[i];
    const value = data === null || data === undefined ? valueIfNull : String(data);
    if (
      quoteEscape &&
      (value.indexOf(separator) >= 0 ||
        value.indexOf('"') >= 0 ||
        value.indexOf('\n') >= 0 ||
        value.indexOf('\r') >= 0)
    ) {
      parts.push('"' + value.replace(/"/g, '""') + '"');
    } else {
      parts.push(value);
    }
  }
  return parts.join(separator);
}

// Port of CSVSave.SaveRows to a string: optional header line, one line per
// row, newline after every line. Uses \n (the C# used the platform newline).
export function toCsvText(
  rows: ReadonlyArray<ReadonlyArray<CsvValue>>,
  header?: ReadonlyArray<string>,
  separator: string = ','
): string {
  let out = '';
  if (header !== undefined && header !== null) {
    out += rowToCsvText(header, separator) + '\n';
  }
  for (let i = 0; i < rows.length; i++) {
    out += rowToCsvText(rows[i], separator) + '\n';
  }
  return out;
}
