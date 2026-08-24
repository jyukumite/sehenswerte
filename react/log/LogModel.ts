// Port of LogControl's queue/filter core (LogControl.cs), minus the WinForms
// painting and cross-thread marshalling:
// - all-rows list + filtered subset; a multi-line entry splits into one row
//   per line, all sharing the one entry (the tooltip re-joins them)
// - threshold filter (priority rank >=) AND regex filter over the displayed
//   line (WITHOUT the timestamp prefix, matching the C# match target)
// - the control is effectively ALWAYS filtering: filterPriority defaults to
//   "info" (rank 1) while debug is rank 0, so debug entries are excluded from
//   the view by default. The file/export still receives everything. This is
//   the C# design, not an accident - do not "fix" the default.
// - itemLimit trims oldest rows first (C# RemoveFirst)
// - pause: the C# pauses the drain (file writes included) and lets the input
//   queue grow unboundedly; this port buffers paused entries and applies them
//   on unpause instead (deliberate deviation).

import {
  EntrySink,
  LogEntry,
  LogPriority,
  PRIORITY_RANK,
  toDisplayedLine,
} from "./LogEntry";

export interface LogRow {
  line: string; // one display line (no timestamp prefix)
  entry: LogEntry;
  splitRow: boolean; // true when the entry produced more than one row
}

export const DEFAULT_ITEM_LIMIT = 5000;

export class LogModel {
  rows: LogRow[] = [];
  filteredRows: LogRow[] = [];
  itemLimit = DEFAULT_ITEM_LIMIT;
  entryCount = 0; // total entries ever added (survives trimming)

  private filterPriorityValue: LogPriority = "info";
  private filterStringValue = "";
  private filterRegex: RegExp | null = null;
  private filterRegexInvalid = false;
  private pausedValue = false;
  private pendingWhilePaused: LogEntry[] = [];

  version = 0;
  private listeners = new Set<() => void>();

  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private notify(): void {
    this.version++;
    this.listeners.forEach((l) => l());
  }

  // The one-argument sink shape hosts pass around (and extendPath wraps).
  get sink(): EntrySink {
    return (entry) => this.add(entry);
  }

  get filterPriority(): LogPriority {
    return this.filterPriorityValue;
  }

  set filterPriority(value: LogPriority) {
    if (this.filterPriorityValue === value) return;
    this.filterPriorityValue = value;
    this.refilter();
  }

  get filterString(): string {
    return this.filterStringValue;
  }

  // Invalid patterns leave the previous regex cleared and set an error flag
  // (the C# silently swallowed invalid regex; the port surfaces it).
  set filterString(value: string) {
    if (this.filterStringValue === value) return;
    this.filterStringValue = value;
    if (value === "") {
      this.filterRegex = null;
      this.filterRegexInvalid = false;
    } else {
      try {
        this.filterRegex = new RegExp(value, "i");
        this.filterRegexInvalid = false;
      } catch {
        this.filterRegex = null;
        this.filterRegexInvalid = true;
      }
    }
    this.refilter();
  }

  get isFilterRegexInvalid(): boolean {
    return this.filterRegexInvalid;
  }

  // C# Filtering => m_FilterRegex != null || m_FilterType != 0.
  get filtering(): boolean {
    return this.filterRegex !== null || PRIORITY_RANK[this.filterPriorityValue] !== 0;
  }

  get paused(): boolean {
    return this.pausedValue;
  }

  set paused(value: boolean) {
    if (this.pausedValue === value) return;
    this.pausedValue = value;
    if (!value && this.pendingWhilePaused.length > 0) {
      const pending = this.pendingWhilePaused;
      this.pendingWhilePaused = [];
      for (const entry of pending) {
        this.addNow(entry);
      }
    }
    this.notify();
  }

  get displayedRows(): LogRow[] {
    return this.filtering ? this.filteredRows : this.rows;
  }

  add(entry: LogEntry): void {
    if (this.pausedValue) {
      this.pendingWhilePaused.push(entry);
      return;
    }
    this.addNow(entry);
  }

  private addNow(entry: LogEntry): void {
    this.entryCount++;
    // split the displayed line on newlines, removing empties; every row
    // shares the one entry (C# DrainInputQueue)
    const lines = toDisplayedLine(entry)
      .split(/[\r\n]+/)
      .filter((l) => l !== "");
    const splitRow = lines.length > 1;
    for (const line of lines) {
      const row: LogRow = { line, entry, splitRow };
      this.rows.push(row);
      if (this.matches(row)) {
        this.filteredRows.push(row);
      }
    }
    this.trim();
    this.notify();
  }

  private matches(row: LogRow): boolean {
    if (PRIORITY_RANK[row.entry.priority] < PRIORITY_RANK[this.filterPriorityValue]) {
      return false;
    }
    // regex matches the pre-timestamp displayed line, like the C#
    return this.filterRegex === null || this.filterRegex.test(row.line);
  }

  private trim(): void {
    const excess = this.rows.length - this.itemLimit;
    if (excess > 0) {
      const removed = new Set(this.rows.slice(0, excess));
      this.rows = this.rows.slice(excess);
      this.filteredRows = this.filteredRows.filter((r) => !removed.has(r));
    }
  }

  private refilter(): void {
    this.filteredRows = this.rows.filter((r) => this.matches(r));
    this.notify();
  }

  clear(): void {
    this.rows = [];
    this.filteredRows = [];
    this.pendingWhilePaused = [];
    this.notify();
  }

  // Whole displayed view with timestamps, one line per row (C# Copy menu).
  copyText(timestamp: (timeMs: number) => string): string {
    return this.displayedRows.map((r) => timestamp(r.entry.time) + r.line).join("\n");
  }

  // Distinct entries currently held (for CSV export - the C# writes the file
  // per entry, not per row).
  entries(): LogEntry[] {
    const result: LogEntry[] = [];
    let last: LogEntry | null = null;
    for (const row of this.rows) {
      if (row.entry !== last) {
        result.push(row.entry);
        last = row.entry;
      }
    }
    return result;
  }
}
