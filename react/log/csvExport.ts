// The CsvLog file-format half of CsvLog.cs: the fixed 11-column CSV
// projection. The C# streams to a (optionally gzipped) file as entries
// arrive; a browser can only hand out a download, so this builds the whole
// CSV in memory from the entries currently held.

import { rowToCsvText } from "../core/csv";
import { LogEntry } from "./LogEntry";

export const CSV_HEADER = [
  "Time",
  "Priority",
  "ComputerID",
  "ProcessID",
  "ThreadID",
  "Source",
  "CallPath",
  "Text",
  "Data",
  "Fields",
  "Binary",
];

const PRIORITY_NAME: Record<string, string> = {
  debug: "Debug",
  info: "Info",
  warn: "Warn",
  error: "Error",
  exception: "Exception",
};

// C# Source column: {filenameWithoutExt}[{line}]:{memberName}.
function sourceOf(entry: LogEntry): string {
  if (entry.sourcePath === undefined && entry.memberName === undefined) return "";
  const file = (entry.sourcePath ?? "").replace(/^.*[\\/]/, "").replace(/\.[^.]*$/, "");
  const line = entry.sourceLineNumber !== undefined ? `[${entry.sourceLineNumber}]` : "";
  return `${file}${line}:${entry.memberName ?? ""}`;
}

export function entryToCsvRow(entry: LogEntry): string {
  return rowToCsvText(
    [
      new Date(entry.time).toISOString(), // C# uses ToString("o")
      PRIORITY_NAME[entry.priority],
      "", // ComputerID - no browser equivalent
      "", // ProcessID
      entry.threadId ?? "",
      sourceOf(entry),
      entry.callPath,
      entry.text,
      entry.data ?? "",
      entry.fields ?? "",
      entry.binaryHex ?? "",
    ],
    ",",
    "" // nulls never occur; keep empty-not-"null" for the log format
  );
}

export function entriesToCsv(entries: LogEntry[]): string {
  const lines = [rowToCsvText(CSV_HEADER)];
  for (const entry of entries) {
    lines.push(entryToCsvRow(entry));
  }
  return lines.join("\n") + "\n";
}

// Download the CSV, gzipped when the browser supports CompressionStream and
// gzip is requested (matching the C# LogControl.CompressLogFile = true habit).
export async function downloadCsv(
  entries: LogEntry[],
  filename: string,
  gzip: boolean = false
): Promise<void> {
  const csv = entriesToCsv(entries);
  let blob: Blob;
  let name = filename;
  const CompressionStreamCtor = (window as unknown as {
    CompressionStream?: new (format: string) => GenericTransformStream;
  }).CompressionStream;
  if (gzip && CompressionStreamCtor !== undefined) {
    const stream = new Blob([csv]).stream().pipeThrough(new CompressionStreamCtor("gzip"));
    blob = await new Response(stream).blob();
    if (!name.endsWith(".gz")) name += ".gz";
  } else {
    blob = new Blob([csv], { type: "text/csv" });
  }
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
}
