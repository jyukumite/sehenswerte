// Port of the sehenswerte CsvLog entry model (CsvLog.cs): the Entry shape,
// the numerically-ranked Priority (the viewer filters with >=), the sink
// decorators extendPath/demoteToDebug, and the display-line/tooltip
// composition from LogControl.LogEntry.

export type LogPriority = "debug" | "info" | "warn" | "error" | "exception";

// Numeric rank is load-bearing: the viewer's threshold compares >=.
export const PRIORITY_RANK: Record<LogPriority, number> = {
  debug: 0,
  info: 1,
  warn: 2,
  error: 3,
  exception: 4,
};

export const PRIORITY_ORDER: LogPriority[] = [
  "debug",
  "info",
  "warn",
  "error",
  "exception",
];

// C# enum names, used in the displayed line ("[Info]").
const PRIORITY_DISPLAY: Record<LogPriority, string> = {
  debug: "Debug",
  info: "Info",
  warn: "Warn",
  error: "Error",
  exception: "Exception",
};

export interface LogEntry {
  text: string;
  priority: LogPriority;
  callPath: string; // host-supplied scope chain, built by extendPath
  data?: string; // free-form secondary payload
  fields?: string; // free-form tertiary payload
  binaryHex?: string; // pre-rendered hex of any binary payload
  // Source location: C# fills these via CallerMemberName/FilePath/LineNumber,
  // which have no TS equivalent - optional, host-supplied when available.
  memberName?: string;
  sourcePath?: string;
  sourceLineNumber?: number;
  threadId?: string; // kept for CSV shape compatibility (tab/session id)
  time: number; // unix epoch milliseconds
}

export type EntrySink = (entry: LogEntry) => void;

export function newEntry(
  text: string,
  priority: LogPriority = "info",
  extra?: Partial<Omit<LogEntry, "text" | "priority" | "time">> & { time?: number }
): LogEntry {
  return {
    text,
    priority,
    callPath: "",
    time: Date.now(),
    ...extra,
  };
}

// C# CsvLog.ExtendPath: clone the entry and append ":" + callPath. Note the
// deliberate LEADING colon - nesting from "" produces ":BoundData:GridTab"
// (innermost segment first), matching the C# output byte for byte.
export function extendPath(prev: EntrySink, callPath: string): EntrySink {
  return (entry) => {
    prev({ ...entry, callPath: entry.callPath + ":" + callPath });
  };
}

// C# CsvLog.DemoteToDebug: force priority to debug and prefix the text, but
// ONLY when actually demoting - entries already at debug pass through with no
// prefix. (Hosts use it to fold noisy warmup traffic down to debug.)
export function demoteToDebug(prev: EntrySink, textPrefix: string): EntrySink {
  return (entry) => {
    if (entry.priority === "debug") {
      prev(entry);
    } else {
      prev({ ...entry, priority: "debug", text: textPrefix + entry.text });
    }
  };
}

// Adapter for the simple (message, priority) sinks GridModel/DataGrid already
// use, until those migrate to the entry-shaped sink.
export function fromSimpleSink(
  sink: EntrySink
): (message: string, priority: LogPriority) => void {
  return (message, priority) => sink(newEntry(message, priority));
}

// C# LogEntry.ToDisplayedLine: "[Priority] {CallPath} - {Text} {Data} {Fields}
// {Hex}", trimmed, with the " - " omitted when the call path is empty.
export function toDisplayedLine(entry: LogEntry): string {
  const head = `[${PRIORITY_DISPLAY[entry.priority]}]`;
  const path = entry.callPath !== "" ? ` ${entry.callPath} -` : "";
  const tail = [entry.text, entry.data, entry.fields, entry.binaryHex]
    .filter((x) => x !== undefined && x !== "")
    .join(" ");
  return `${head}${path} ${tail}`.trim();
}

const pad2 = (n: number): string => String(n).padStart(2, "0");
const pad3 = (n: number): string => String(n).padStart(3, "0");

// Paint-time timestamp prefix (LOCAL time, like the C# paint path):
// "yyyy/MM/dd HH:mm:ss.fff ". Not part of the stored line and not part of the
// regex match target.
export function timestampPrefix(timeMs: number): string {
  const d = new Date(timeMs);
  return (
    `${d.getFullYear()}/${pad2(d.getMonth() + 1)}/${pad2(d.getDate())} ` +
    `${pad2(d.getHours())}:${pad2(d.getMinutes())}:${pad2(d.getSeconds())}.${pad3(d.getMilliseconds())} `
  );
}

// C# LogEntryRow.ToToolTip: the row's line; plus the full original entry text
// when the row is one of several split lines; plus source location and path.
export function toToolTip(entry: LogEntry, rowLine: string, isSplitRow: boolean): string {
  const parts: string[] = [rowLine];
  if (isSplitRow) {
    parts.push("", toDisplayedLine(entry));
  }
  if (entry.sourcePath !== undefined || entry.memberName !== undefined) {
    const file = entry.sourcePath ?? "";
    const line = entry.sourceLineNumber !== undefined ? `[${entry.sourceLineNumber}]` : "";
    parts.push(`${file}${line} ${entry.memberName ?? ""}`.trim());
  }
  if (entry.callPath !== "") {
    parts.push(`Path: ${entry.callPath}`);
  }
  return parts.join("\n");
}
