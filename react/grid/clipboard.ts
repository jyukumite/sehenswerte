// Port of the clipboard-format section of sehenswerte DataGridBoundData.cs
// (SelectedCellsToClipboardFormats / StringArrayToCsv / StringArrayToHtml).
// WrapHtml (the Windows CF_HTML framing for Excel) is intentionally not
// ported; the browser clipboard takes raw text/html.

import { rowToCsvText } from "../core/csv";

export interface SelectedCellsData {
  headers: string[];
  strings: (string | null)[][];
  csv: string;
  tsv: string;
  html: string;
}

export interface SelectedCell {
  row: number;
  col: number;
  value: string | null;
}

// Mirrors System.Net.WebUtility.HtmlEncode for the characters that matter
// in element content; null renders as empty, like the C# `s ?? ""`.
function escapeHtml(s: string | null): string {
  return (s === null ? "" : s)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

function buildTsv(headers: string[], strings: (string | null)[][]): string {
  const lines = strings.map((row) =>
    row.map((cell) => (cell === null ? "" : cell)).join("\t")
  );
  // The C# prepends the header row only when there is more than one data row.
  const header = strings.length > 1 ? headers.join("\t") + "\n" : "";
  return header + lines.join("\n");
}

function buildCsv(headers: string[], strings: (string | null)[][]): string {
  // Unlike TSV, the CSV always carries the header row. Null cells go through
  // rowToCsvText's default null handling, matching CSVSave.RowToCsvText.
  const rows = strings.map((row) => rowToCsvText(row));
  return rowToCsvText(headers) + "\n" + rows.join("\n");
}

function buildHtml(headers: string[], strings: (string | null)[][]): string {
  const parts: string[] = [];
  parts.push('<table border="1">');
  parts.push("<thead><tr>");
  headers.forEach((header) => parts.push("<th>" + escapeHtml(header) + "</th>"));
  parts.push("</tr></thead>");
  parts.push("<tbody>");
  strings.forEach((row) => {
    parts.push("<tr>");
    row.forEach((cell) => parts.push("<td>" + escapeHtml(cell) + "</td>"));
    parts.push("</tr>");
  });
  parts.push("</tbody>");
  parts.push("</table>");
  return parts.join("\n") + "\n";
}

export function buildSelectedCellsData(
  headers: string[],
  strings: (string | null)[][]
): SelectedCellsData {
  return {
    headers,
    strings,
    csv: buildCsv(headers, strings),
    tsv: buildTsv(headers, strings),
    html: buildHtml(headers, strings),
  };
}

// Port of the bounding-set logic at the top of SelectedCellsToClipboardFormats:
// the result spans the distinct selected rows x distinct selected columns
// (ascending), with intersections that were not actually selected left null.
export function selectionBoundingCells(
  cells: SelectedCell[],
  columnNamesAll: string[]
): { headers: string[]; strings: (string | null)[][] } {
  const rowSet = new Set<number>();
  const colSet = new Set<number>();
  cells.forEach((cell) => {
    rowSet.add(cell.row);
    colSet.add(cell.col);
  });
  const rowsSorted = Array.from(rowSet).sort((a, b) => a - b);
  const colsSorted = Array.from(colSet).sort((a, b) => a - b);

  const rowIndex = new Map<number, number>();
  rowsSorted.forEach((row, i) => rowIndex.set(row, i));
  const colIndex = new Map<number, number>();
  colsSorted.forEach((col, i) => colIndex.set(col, i));

  const strings: (string | null)[][] = rowsSorted.map(() =>
    colsSorted.map(() => null as string | null)
  );
  cells.forEach((cell) => {
    strings[rowIndex.get(cell.row)!][colIndex.get(cell.col)!] = cell.value;
  });

  const headers = colsSorted.map((col) => columnNamesAll[col]);
  return { headers, strings };
}

// Writes TSV as text/plain plus a text/html table (what spreadsheets paste
// as cells). Falls back to plain-text TSV when rich write is unavailable.
export async function writeSelectionToClipboard(
  data: SelectedCellsData
): Promise<void> {
  const clipboard = navigator.clipboard;
  const canWriteRich =
    typeof ClipboardItem !== "undefined" &&
    !!clipboard &&
    typeof clipboard.write === "function";
  if (canWriteRich) {
    try {
      const item = new ClipboardItem({
        "text/plain": new Blob([data.tsv], { type: "text/plain" }),
        "text/html": new Blob([data.html], { type: "text/html" }),
      });
      await clipboard.write([item]);
      return;
    } catch {
      // e.g. permissions or unsupported types; fall through to plain text
    }
  }
  await clipboard.writeText(data.tsv);
}
