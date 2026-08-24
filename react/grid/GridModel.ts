// Port of sehenswerte DataGridBoundData.cs (BoundData + BoundDataRow*).
//
// Differences from the C# by design:
// - No WinForms binding: the colN reflection properties, IBindingList, and
//   RebuildGridColumns are gone. UI layers subscribe() and re-read state.
// - Selection lives in the UI layer; operations take stable row indices.
// - Colours are CSS colour strings; diffs are opaque objects the renderer
//   interprets (see core/stringDiff).
// - Highlights are model state here (the C# kept them on the control).
//
// Invariant triad preserved from the C#: every undoable op is one Operation
// value + a pushSnapshot at the top of the public method + a case in
// dispatchAction. Missing any of the three silently breaks undo/redo/replay.

import { naturalCompare } from "../core/naturalCompare";
import {
  GridHistory,
  newGridHistory,
  newSnapshot,
  Snapshot,
  SortDirection,
  SplitRecipe,
} from "./GridHistory";

export type CellDiff = object; // opaque to the model; renderer-defined shape

export type LogPriority = "debug" | "info" | "warn" | "error";
export type LogSink = (message: string, priority: LogPriority) => void;

function toDoubleOr0(s: string | null | undefined): number {
  if (s === null || s === undefined || s.trim() === "") return 0;
  const v = Number(s);
  return isNaN(v) ? 0 : v;
}

function arrayMove<T>(arr: T[], from: number, to: number): void {
  const [item] = arr.splice(from, 1);
  arr.splice(to, 0, item);
}

export abstract class GridRow {
  visible = true;
  tempFlag = false;
  index: number; // stable original row index
  resortIndex: number; // current display position (set by reshowFiltered)
  colours: (string | null)[] | null = null;
  diffs: (CellDiff | null)[] | null = null;

  constructor(index: number) {
    this.index = index;
    this.resortIndex = index;
  }

  abstract get strings(): (string | null)[];
  abstract column(index: number): string | null;
  abstract columnDouble(index: number): number;
  abstract get count(): number;
  abstract set(index: number, to: string | null): void;
  abstract insertColumnValue(index: number, value: string | null): void;
  abstract removeColumn(index: number): void;
  abstract moveColumnValue(from: number, to: number): void;
  abstract compareTo(
    other: GridRow,
    colIndex: number,
    direction: SortDirection
  ): number;

  appendColumnValue(value: string | null): void {
    this.insertColumnValue(this.count, value);
  }

  cellColour(col: number, colour: string): void {
    if (this.colours === null) {
      this.colours = new Array(this.count).fill(null);
    }
    this.colours[col] = colour;
  }

  cellDiffs(col: number, diff: CellDiff | null): void {
    if (this.diffs === null) {
      if (diff === null) return;
      this.diffs = new Array(this.count).fill(null);
    }
    if (col >= 0 && col < this.diffs.length) {
      this.diffs[col] = diff;
    }
  }

  protected insertSideArrays(insertAt: number): void {
    if (this.colours !== null) {
      this.colours.splice(insertAt, 0, null);
    }
    if (this.diffs !== null) {
      this.diffs.splice(insertAt, 0, null);
    }
  }

  protected removeSideArrays(index: number): void {
    if (this.colours !== null && index < this.colours.length) {
      this.colours.splice(index, 1);
    }
    if (this.diffs !== null && index < this.diffs.length) {
      this.diffs.splice(index, 1);
    }
  }

  protected moveSideArrays(from: number, to: number): void {
    if (this.colours !== null) arrayMove(this.colours, from, to);
    if (this.diffs !== null) arrayMove(this.diffs, from, to);
  }
}

export class GridRowString extends GridRow {
  data: (string | null)[];

  constructor(index: number, sourceRow: (string | null)[]) {
    super(index);
    this.data = sourceRow;
  }

  get count(): number {
    return this.data.length;
  }
  get strings(): (string | null)[] {
    return this.data;
  }
  column(index: number): string | null {
    return index < this.data.length ? this.data[index] : "";
  }
  columnDouble(index: number): number {
    return index < this.data.length ? toDoubleOr0(this.data[index]) : 0;
  }
  set(index: number, to: string | null): void {
    this.data[index] = to;
    this.cellDiffs(index, null);
  }
  insertColumnValue(index: number, value: string | null): void {
    const insertAt = Math.max(0, Math.min(index, this.data.length));
    this.data.splice(insertAt, 0, value);
    this.insertSideArrays(insertAt);
  }
  removeColumn(index: number): void {
    if (index < 0 || index >= this.data.length) return;
    this.data.splice(index, 1);
    this.removeSideArrays(index);
  }
  moveColumnValue(from: number, to: number): void {
    arrayMove(this.data, from, to);
    this.moveSideArrays(from, to);
  }
  compareTo(other: GridRow, colIndex: number, direction: SortDirection): number {
    const o1 = (this.data[colIndex] ?? "").toLowerCase();
    const o2 = ((other as GridRowString).data[colIndex] ?? "").toLowerCase();
    let result = naturalCompare(o1, o2);
    if (result === 0) {
      result = this.resortIndex < other.resortIndex ? -1 : 1; // stable sort
    }
    return direction === "asc" ? result : -result;
  }
}

export class GridRowDouble extends GridRow {
  data: number[];

  constructor(index: number, sourceRow: number[]) {
    super(index);
    this.data = sourceRow;
  }

  get count(): number {
    return this.data.length;
  }
  get strings(): (string | null)[] {
    return this.data.map((x) => String(x));
  }
  column(index: number): string | null {
    return index >= this.data.length ? "" : String(this.data[index]);
  }
  columnDouble(index: number): number {
    return index >= this.data.length ? 0 : this.data[index];
  }
  set(index: number, to: string | null): void {
    this.data[index] = toDoubleOr0(to);
    this.cellDiffs(index, null);
  }
  insertColumnValue(index: number, value: string | null): void {
    const insertAt = Math.max(0, Math.min(index, this.data.length));
    this.data.splice(insertAt, 0, toDoubleOr0(value));
    this.insertSideArrays(insertAt);
  }
  removeColumn(index: number): void {
    if (index < 0 || index >= this.data.length) return;
    this.data.splice(index, 1);
    this.removeSideArrays(index);
  }
  moveColumnValue(from: number, to: number): void {
    arrayMove(this.data, from, to);
    this.moveSideArrays(from, to);
  }
  compareTo(other: GridRow, colIndex: number, direction: SortDirection): number {
    const o1 = this.data[colIndex];
    const o2 = (other as GridRowDouble).data[colIndex];
    let result = o1 - o2;
    if (result === 0) {
      result = this.resortIndex < other.resortIndex ? -1 : 1; // stable sort
    }
    return direction === "asc" ? (result > 0 ? 1 : -1) : result > 0 ? -1 : 1;
  }
}

export interface ColumnWidth {
  name: string;
  width: number;
}

export const DEFAULT_COLUMN_WIDTH = 100;

export class GridModel {
  unfilteredData: GridRow[] = [];
  filteredData: GridRow[] = [];
  columnNames: string[] = [];
  highlights: string[] = []; // committed highlight substrings, oldest first

  private history: GridHistory = newGridHistory();
  private redoStack: GridHistory = newGridHistory();
  private suppressRedoClear = false;
  private indexToRow: GridRow[] = [];
  private onLog: LogSink;

  // Change notification for UI layers (e.g. React useSyncExternalStore).
  // version bumps on any visible change; columnsVersion additionally bumps
  // when the column set changes (the RebuildGridColumns analogue).
  version = 0;
  columnsVersion = 0;
  private listeners = new Set<() => void>();

  constructor(onLog?: LogSink) {
    this.onLog = onLog ?? (() => undefined);
  }

  static fromStrings(
    source: (string | null)[][],
    columnNames: string[],
    onLog?: LogSink
  ): GridModel {
    const m = new GridModel(onLog);
    m.initializeData(columnNames, source.map(
      (row, index) => new GridRowString(index, row.slice())
    ));
    return m;
  }

  static fromDoubles(
    source: number[][],
    columnNames: string[],
    onLog?: LogSink
  ): GridModel {
    const m = new GridModel(onLog);
    m.initializeData(columnNames, source.map(
      (row, index) => new GridRowDouble(index, row.slice())
    ));
    return m;
  }

  private initializeData(columnNames: string[], rows: GridRow[]): void {
    this.columnNames = columnNames.slice();
    this.unfilteredData = rows;
    this.indexToRow = rows.slice();
    this.onLog(
      `initializeData ${rows.length} rows of ${this.columnNames.length} columns`,
      "info"
    );
    this.filteredData = this.unfilteredData;
    this.showAll();
  }

  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private notify(columnsChanged = false): void {
    this.version++;
    if (columnsChanged) this.columnsVersion++;
    this.listeners.forEach((l) => l());
  }

  get canUndo(): boolean {
    return this.history.history.length > 0;
  }
  get canRedo(): boolean {
    return this.redoStack.history.length > 0;
  }
  get isFiltered(): boolean {
    return this.filteredData.length !== this.unfilteredData.length;
  }

  rowByIndex(index: number): GridRow | undefined {
    return this.indexToRow[index];
  }

  // ---- data mutation (non-undoable, matching the C# public surface) ----

  appendRows(newRows: (string | null)[][]): void {
    for (const row of newRows) {
      const index = this.unfilteredData.length;
      // The C# always appends string rows, even to a numeric grid.
      const item = new GridRowString(index, row.slice());
      this.indexToRow.push(item);
      this.unfilteredData.push(item);
      // filteredData may be a separate list after showAll()/refilter();
      // only add visible rows
      if (item.visible) {
        this.filteredData.push(item);
      }
    }
    this.notify();
  }

  addColumn(header: string, values: (string | null)[]): void {
    this.unfilteredData.forEach((row, i) => {
      row.appendColumnValue(i < values.length ? values[i] : null);
    });
    this.columnNames.push(header);
    this.notify(true);
  }

  insertColumn(header: string, index: number, values: (string | null)[]): void {
    this.unfilteredData.forEach((row, i) => {
      row.insertColumnValue(index, i < values.length ? values[i] : null);
    });
    this.columnNames.splice(index, 0, header);
    this.notify(true);
  }

  removeColumn(header: string): boolean {
    const idx = this.columnNames.indexOf(header);
    if (idx < 0) return false;
    for (const row of this.unfilteredData) {
      row.removeColumn(idx);
    }
    this.columnNames.splice(idx, 1);
    this.notify(true);
    return true;
  }

  setCell(colIndex: number, displayRow: number, value: string | null): void {
    this.filteredData[displayRow].set(colIndex, value);
    this.notify();
  }

  cellColour(col: number, displayRow: number, colour: string): void {
    this.filteredData[displayRow].cellColour(col, colour);
    this.notify();
  }

  cellDiffs(col: number, displayRow: number, diff: CellDiff | null): void {
    this.filteredData[displayRow].cellDiffs(col, diff);
    this.notify();
  }

  // ---- reads ----

  getColumn(nameOrIndex: string | number): (string | null)[] | null {
    const colIndex =
      typeof nameOrIndex === "number"
        ? nameOrIndex
        : this.columnNames.indexOf(nameOrIndex);
    if (typeof nameOrIndex === "string" && colIndex === -1) return null;
    return this.filteredData.map((x) => x.column(colIndex));
  }

  getColumnDouble(name: string): number[] | null {
    const colIndex = this.columnNames.indexOf(name);
    return colIndex === -1
      ? null
      : this.filteredData.map((x) => x.columnDouble(colIndex));
  }

  // stable-index rows -> values of one column (selection helpers live in the UI)
  rowsOfColumn(stableIndices: number[], column: string): (string | null)[] | null {
    const colIndex = this.columnNames.indexOf(column);
    return colIndex === -1
      ? null
      : stableIndices.map((y) => this.indexToRow[y].column(colIndex));
  }

  rowAsRecord(stableIndex: number): Record<string, string | null> {
    const rowData = this.indexToRow[stableIndex]?.strings ?? [];
    const result: Record<string, string | null> = {};
    this.columnNames.forEach((columnName, index) => {
      result[columnName] = index < rowData.length ? rowData[index] : null;
    });
    return result;
  }

  // ---- undoable operations ----

  pushSnapshot(snap: Snapshot): void {
    snap.visibleRows = this.filteredData.map((r) => r.index);
    snap.visibleRowRefs = this.filteredData.slice();
    this.history.history.push(snap);
    if (!this.suppressRedoClear) {
      this.redoStack.history = [];
    }
    this.onLog(
      `pushSnapshot: ${describe(snap)} | buffer: ${this.describeHistory()}`,
      "debug"
    );
    this.notify();
  }

  showAll(): void {
    this.pushSnapshot(newSnapshot("showAll"));
    for (const v of this.unfilteredData) {
      v.visible = true;
    }
    this.refilter();
  }

  hideRows(selectedRows: number[]): void {
    const selected = selectedRows.slice();
    const snap = newSnapshot("hideRows");
    snap.selectedRows = selected;
    this.pushSnapshot(snap);
    for (const v of selected) {
      if (v >= 0 && v < this.indexToRow.length) {
        this.indexToRow[v].visible = false;
      }
    }
    this.refilter();
  }

  // Snapshot stores the selected row indices; replay (redo) re-hides everything
  // else. Index-based, so replay only matches the original against the same data.
  hideRowsOtherThan(selectedRows: number[]): void {
    const selected = selectedRows.slice();
    const snap = newSnapshot("hideRowsOtherThan");
    snap.selectedRows = selected;
    this.pushSnapshot(snap);
    this.unfilteredData.forEach((x) => (x.tempFlag = false));
    for (const v of selected) {
      if (v >= 0 && v < this.indexToRow.length) {
        this.indexToRow[v].tempFlag = true;
      }
    }
    this.filteredData
      .filter((v) => !v.tempFlag)
      .forEach((v) => (v.visible = false));
    this.refilter();
  }

  // Hide rows displayed above the given row (by original stable index)
  hideRowsAbove(row: number): void {
    const displayPos = this.filteredData.indexOf(this.indexToRow[row]);
    if (displayPos < 0) return;
    this.hideRowsAboveAt(displayPos);
  }

  hideRowsBelow(row: number): void {
    const displayPos = this.filteredData.indexOf(this.indexToRow[row]);
    if (displayPos < 0) return;
    this.hideRowsBelowAt(displayPos);
  }

  private hideRowsAboveAt(displayPos: number): void {
    if (displayPos < 0 || displayPos >= this.filteredData.length) return;
    const { column, value } = this.anchorAt(displayPos);
    const snap = newSnapshot("hideRowsAbove");
    snap.column = column;
    snap.anchorValue = value;
    snap.row = displayPos;
    this.pushSnapshot(snap);
    for (let loop = 0; loop < displayPos; loop++) {
      this.filteredData[loop].visible = false;
    }
    this.refilter();
  }

  private hideRowsBelowAt(displayPos: number): void {
    if (displayPos < 0 || displayPos >= this.filteredData.length) return;
    const { column, value } = this.anchorAt(displayPos);
    const snap = newSnapshot("hideRowsBelow");
    snap.column = column;
    snap.anchorValue = value;
    snap.row = displayPos;
    this.pushSnapshot(snap);
    const count = this.filteredData.length;
    for (let loop = displayPos + 1; loop < count; loop++) {
      this.filteredData[loop].visible = false;
    }
    this.refilter();
  }

  private anchorAt(displayPos: number): { column: string; value: string } {
    const keys = this.currentSortKeys();
    if (keys.length === 0) return { column: "", value: "" };
    const col = keys[keys.length - 1].columnName;
    const idx = this.columnNames.indexOf(col);
    if (idx < 0) return { column: "", value: "" };
    return { column: col, value: this.filteredData[displayPos].column(idx) ?? "" };
  }

  hideRowsMatching(column: string, rows: (string | null)[]): void {
    const strings = rows.map((x) => x?.toLowerCase() ?? null);
    const snap = newSnapshot("hideRowsMatching");
    snap.column = column;
    snap.values = strings.map((s) => s ?? "");
    this.pushSnapshot(snap);
    // case insensitive
    const colIndex = this.columnNames.indexOf(column);
    this.hideRowsIf((x) =>
      strings.includes(x.column(colIndex)?.toLowerCase() ?? null)
    );
  }

  hideRowsNotMatching(column: string, rows: (string | null)[]): void {
    const strings = rows.map((x) => x?.toLowerCase() ?? null);
    const snap = newSnapshot("hideRowsNotMatching");
    snap.column = column;
    snap.values = strings.map((s) => s ?? "");
    this.pushSnapshot(snap);
    // case insensitive
    const colIndex = this.columnNames.indexOf(column);
    this.hideRowsIf(
      (x) => !strings.includes(x.column(colIndex)?.toLowerCase() ?? null)
    );
  }

  showRowsMatchingRegex(regex: string, column: string): void {
    const snap = newSnapshot("showRowsMatchingRegex");
    snap.column = column;
    snap.pattern = regex;
    this.pushSnapshot(snap);
    const match = new RegExp(regex, "i");
    const colIndex = this.columnNames.indexOf(column);
    this.hideRowsIf((x) => !match.test(x.column(colIndex) ?? "null"));
  }

  hideRowsMatchingRegex(regex: string, column: string): void {
    const snap = newSnapshot("hideRowsMatchingRegex");
    snap.column = column;
    snap.pattern = regex;
    this.pushSnapshot(snap);
    const match = new RegExp(regex, "i");
    const colIndex = this.columnNames.indexOf(column);
    this.hideRowsIf((x) => match.test(x.column(colIndex) ?? "null"));
  }

  hideNotFirstUnique(column: string): void {
    const snap = newSnapshot("hideNotFirstUnique");
    snap.column = column;
    this.pushSnapshot(snap);
    const colIndex = this.columnNames.indexOf(column);
    if (colIndex < 0) return;

    // count occurrences across currently-visible rows before we hide anything
    const counts = new Map<string, number>();
    for (const row of this.filteredData) {
      const value = row.column(colIndex) ?? "";
      counts.set(value, (counts.get(value) ?? 0) + 1);
    }

    // keep the first, hide the rest
    const seen = new Set<string | null>();
    this.hideRowsIf((x) => {
      const value = x.column(colIndex);
      if (seen.has(value)) {
        return true;
      }
      seen.add(value);
      return false;
    });

    // populate "<column> count" directly to the right of the uniqued column.
    // hidden rows carry their count too, so undo restores a still-meaningful view.
    // If a column with that name already exists (repeated unique on the same
    // column), leave it alone - undo of *this* action shouldn't drop a column
    // an earlier action installed.
    const countHeader = column + " count";
    if (!this.columnNames.includes(countHeader)) {
      const countFor = (r: GridRow): string | null => {
        const c = counts.get(r.column(colIndex) ?? "");
        return c !== undefined ? String(c) : null;
      };
      this.insertColumn(
        countHeader,
        colIndex + 1,
        this.unfilteredData.map(countFor)
      );
      const hist = this.history.history;
      if (hist.length > 0) {
        hist[hist.length - 1].addedColumns.push(countHeader);
      }
    }
  }

  decimate(stride: number): void {
    if (stride < 2) return;
    const snap = newSnapshot("decimate");
    snap.stride = stride;
    this.pushSnapshot(snap);
    let counter = 0;
    this.hideRowsIf(() => counter++ % stride !== 0);
  }

  // First call pivots the visible view: existing column headers become a
  // single "headers" column, and each visible row becomes a "row 1", "row 2"
  // ... column. Second call - detected by the same shape - reverses.
  // Undo restores the pre-transpose unfilteredData/columnNames so that
  // older snapshots' row references stay valid after walking back.
  transpose(): void {
    const preUnfiltered = this.unfilteredData;
    const preColumnNames = this.columnNames;
    this.pushSnapshot(newSnapshot("transpose"));
    const pushed = this.history.history[this.history.history.length - 1];
    pushed.preTransposeUnfiltered = preUnfiltered;
    pushed.preTransposeColumnNames = preColumnNames;
    this.doTransposeInPlace();
  }

  private isTransposedShape(): boolean {
    if (this.columnNames.length < 1) return false;
    if (this.columnNames[0] !== "headers") return false;
    for (let loop = 1; loop < this.columnNames.length; loop++) {
      if (this.columnNames[loop] !== `row ${loop}`) return false;
    }
    return true;
  }

  private doTransposeInPlace(): void {
    const reverse = this.isTransposedShape();
    const oldRows = this.filteredData.length;
    const oldCols = this.columnNames.length;

    const newColumnNames: string[] = [];
    const newRows: GridRow[] = [];

    if (reverse) {
      // "headers" column values become new column names; each "row N"
      // column reflows into a new row.
      for (let loop = 0; loop < oldRows; loop++) {
        newColumnNames.push(this.filteredData[loop].column(0) ?? "");
      }
      const newRowCount = oldCols - 1;
      for (let rowIdx = 0; rowIdx < newRowCount; rowIdx++) {
        const sourceCol = rowIdx + 1;
        const data: (string | null)[] = new Array(oldRows);
        for (let colIdx = 0; colIdx < oldRows; colIdx++) {
          data[colIdx] = this.filteredData[colIdx].column(sourceCol);
        }
        newRows.push(new GridRowString(rowIdx, data));
      }
    } else {
      newColumnNames.push("headers");
      for (let loop = 0; loop < oldRows; loop++) {
        newColumnNames.push(`row ${loop + 1}`);
      }
      for (let colIdx = 0; colIdx < oldCols; colIdx++) {
        const data: (string | null)[] = new Array(oldRows + 1);
        data[0] = this.columnNames[colIdx];
        for (let rowIdx = 0; rowIdx < oldRows; rowIdx++) {
          data[rowIdx + 1] = this.filteredData[rowIdx].column(colIdx);
        }
        newRows.push(new GridRowString(colIdx, data));
      }
    }

    this.columnNames = newColumnNames;
    this.unfilteredData = newRows;
    this.indexToRow = newRows.slice();
    for (const v of this.unfilteredData) {
      v.visible = true;
    }
    this.notify(true);
    this.refilter();
  }

  // Move column so its new left-neighbour is newAfter (or leftmost if empty)
  moveColumn(column: string, newAfter: string): void {
    const from = this.columnNames.indexOf(column);
    if (from < 0) return;
    const to = this.computeMoveTarget(from, newAfter);
    if (to < 0 || from === to) return;
    const oldAfter = from === 0 ? "" : this.columnNames[from - 1];
    const snap = newSnapshot("moveColumn");
    snap.column = column;
    snap.fromAfterColumn = oldAfter;
    snap.toAfterColumn = newAfter;
    this.pushSnapshot(snap);
    this.doMove(from, to);
  }

  // Non-snapshotting reverse-move used by undo.
  private moveColumnAfter(column: string, newAfter: string): void {
    const from = this.columnNames.indexOf(column);
    if (from < 0) return;
    const to = this.computeMoveTarget(from, newAfter);
    if (to < 0 || from === to) return;
    this.doMove(from, to);
  }

  // Translate "after this name" anchor into a post-removal insertion index.
  // Returns -1 if newAfter is non-empty but doesn't exist, or refers to the
  // moved column itself.
  private computeMoveTarget(from: number, newAfter: string): number {
    if (newAfter === "") return 0;
    const afterIdx = this.columnNames.indexOf(newAfter);
    if (afterIdx < 0 || afterIdx === from) return -1;
    return afterIdx < from ? afterIdx + 1 : afterIdx;
  }

  private doMove(from: number, to: number): void {
    if (from === to) return;
    const name = this.columnNames[from];
    this.columnNames.splice(from, 1);
    this.columnNames.splice(to, 0, name);
    for (const row of this.unfilteredData) {
      row.moveColumnValue(from, to);
    }
    this.notify(true);
  }

  // Insert one or more derived columns immediately after the source column.
  splitColumn(sourceColumnName: string, emit: SplitRecipe): number {
    const sourceColIndex = this.columnNames.indexOf(sourceColumnName);
    if (sourceColIndex < 0) {
      this.onLog(`splitColumn: no column '${sourceColumnName}'`, "warn");
      return 0;
    }
    const produced = emit(sourceColumnName);
    const snap = newSnapshot("splitColumn");
    snap.column = sourceColumnName;
    snap.splitRecipe = emit;
    this.pushSnapshot(snap);
    let insertAt = sourceColIndex + 1;
    for (const col of produced) {
      this.insertColumn(col.header, insertAt, col.values);
      snap.addedColumns.push(col.header);
      insertAt++;
    }
    return produced.length;
  }

  sortByColumn(column: string, direction: SortDirection = "asc"): void {
    const colIndex = this.columnNames.indexOf(column);
    if (colIndex < 0) {
      this.onLog(`sortByColumn: no column ${column}`, "warn");
      return;
    }
    const snap = newSnapshot("applySort");
    snap.column = column;
    snap.direction = direction;
    this.pushSnapshot(snap);
    this.applySortDirect();
  }

  // Header-click behaviour: toggle direction relative to our own history
  // (the C# ignores the direction WinForms passes for the same reason).
  toggleSortByColumn(column: string): void {
    const existing = this.currentSortKeys().find((k) => k.columnName === column);
    const newDir: SortDirection =
      existing === undefined ? "asc" : existing.direction === "asc" ? "desc" : "asc";
    this.sortByColumn(column, newDir);
  }

  // Column widths are view state, but resizes join the same history so
  // saveView/restoreView carry layout with filters (matching the C#).
  recordColumnResize(column: string, width: number): void {
    const snap = newSnapshot("columnResize");
    snap.column = column;
    snap.width = width;
    this.pushSnapshot(snap);
  }

  addHighlight(pattern: string): void {
    const snap = newSnapshot("highlight");
    snap.pattern = pattern;
    this.pushSnapshot(snap);
    this.highlights.push(pattern);
    this.notify();
  }

  // ---- sort state ----

  currentSortKeys(): { columnName: string; direction: SortDirection }[] {
    const result: { columnName: string; direction: SortDirection }[] = [];
    for (const snap of this.history.history) {
      if (snap.kind === "applySort") {
        const col = snap.column;
        const existing = result.findIndex((k) => k.columnName === col);
        if (existing >= 0) result.splice(existing, 1);
        result.push({ columnName: col, direction: snap.direction });
      }
    }
    return result;
  }

  // Newest sort key, for painting the header glyph (newest click = primary).
  primarySortKey(): { columnName: string; direction: SortDirection } | null {
    const keys = this.currentSortKeys();
    return keys.length > 0 ? keys[keys.length - 1] : null;
  }

  private applySortDirect(): void {
    const keys = this.currentSortKeys();
    if (keys.length === 0 || this.unfilteredData.length === 0) return;

    const resolved = keys
      .slice()
      .reverse()
      .map((k) => ({ idx: this.columnNames.indexOf(k.columnName), direction: k.direction }))
      .filter((k) => k.idx >= 0);

    const temp = this.filteredData.slice();
    temp.sort((x, y) => {
      for (const key of resolved) {
        const cmp = x.compareTo(y, key.idx, key.direction);
        if (cmp !== 0) return cmp;
      }
      return x.resortIndex - y.resortIndex;
    });
    this.filteredData = temp;
    this.reshowFiltered();
  }

  // ---- filtering internals ----

  // Predicate-based hide. Does NOT push a snapshot.
  private hideRowsIf(predicate: (row: GridRow) => boolean): void {
    this.filteredData.filter(predicate).forEach((v) => (v.visible = false));
    this.refilter();
  }

  private refilter(): void {
    const temp = this.unfilteredData.filter((x) => x.visible);
    temp.sort((x, y) => x.resortIndex - y.resortIndex);
    this.filteredData = temp;
    this.reshowFiltered();
  }

  private reshowFiltered(): void {
    const count = this.filteredData.length;
    for (let loop = 0; loop < count; loop++) {
      this.filteredData[loop].resortIndex = loop;
    }
    this.notify();
  }

  // ---- undo / redo ----

  undo(): ColumnWidth[] | null {
    if (this.history.history.length === 0) {
      this.onLog("undo: history empty, nothing to pop", "debug");
      return null;
    }
    const snap = this.history.history.pop() as Snapshot;
    this.redoStack.history.push(snap);
    this.onLog(
      `undo: popped ${describe(snap)} | buffer: ${this.describeHistory()} | redo depth: ${this.redoStack.history.length}`,
      "debug"
    );
    const wasTranspose = snap.kind === "transpose";
    if (wasTranspose) {
      this.undoTranspose(snap);
    } else {
      this.applyVisible(snap);
    }
    if (snap.kind === "highlight") {
      this.highlights.pop();
    }
    for (let loop = snap.addedColumns.length - 1; loop >= 0; loop--) {
      this.removeColumn(snap.addedColumns[loop]);
    }
    if (snap.kind === "moveColumn") {
      this.moveColumnAfter(snap.column, snap.fromAfterColumn);
    }
    this.reshowFiltered();

    const widths: ColumnWidth[] = [];
    if (snap.kind === "columnResize") {
      const col = snap.column;
      const prior = findLast(
        this.history.history,
        (s) => s.kind === "columnResize" && s.column === col
      );
      widths.push({ name: col, width: prior?.width ?? DEFAULT_COLUMN_WIDTH });
    } else if (wasTranspose) {
      // Restore widths the user set on the now-restored columns from
      // the most recent columnResize entry per column in remaining
      // history. Columns never resized fall back to the default.
      for (const name of this.columnNames) {
        const prior = findLast(
          this.history.history,
          (s) => s.kind === "columnResize" && s.column === name
        );
        widths.push({ name, width: prior?.width ?? DEFAULT_COLUMN_WIDTH });
      }
    }
    return widths;
  }

  redo(): ColumnWidth[] | null {
    if (this.redoStack.history.length === 0) {
      this.onLog("redo: stack empty, nothing to redo", "debug");
      return null;
    }
    const snap = this.redoStack.history.pop() as Snapshot;
    this.onLog(
      `redo: dispatching ${describe(snap)} | redo depth now: ${this.redoStack.history.length}`,
      "debug"
    );

    const widthsByColumn = new Map<string, number>();
    this.suppressRedoClear = true;
    try {
      this.dispatchAction(snap, widthsByColumn);
    } finally {
      this.suppressRedoClear = false;
    }
    return Array.from(widthsByColumn, ([name, width]) => ({ name, width }));
  }

  private undoTranspose(snap: Snapshot): void {
    if (snap.preTransposeUnfiltered !== null && snap.preTransposeColumnNames !== null) {
      this.unfilteredData = snap.preTransposeUnfiltered;
      this.columnNames = snap.preTransposeColumnNames;
      this.indexToRow = this.unfilteredData
        .slice()
        .sort((a, b) => a.index - b.index);
      this.notify(true);
      this.applyVisible(snap);
    } else {
      // No stash (e.g. deserialized snapshot); fall back to re-executing.
      this.doTransposeInPlace();
    }
  }

  private applyVisible(snap: Snapshot): void {
    if (snap.visibleRowRefs === null) {
      const displayOrder = new Map<number, number>();
      snap.visibleRows.forEach((id, pos) => displayOrder.set(id, pos));
      snap.visibleRowRefs = this.unfilteredData
        .filter((r) => displayOrder.has(r.index))
        .sort(
          (a, b) =>
            (displayOrder.get(a.index) ?? Number.MAX_SAFE_INTEGER) -
            (displayOrder.get(b.index) ?? Number.MAX_SAFE_INTEGER)
        );
    }
    this.filteredData = snap.visibleRowRefs;
    for (const v of this.unfilteredData) {
      v.visible = false;
    }
    for (const v of this.filteredData) {
      v.visible = true;
    }
  }

  // ---- save / restore (the portable view recipe) ----

  saveBoundState(): GridHistory {
    return { history: this.history.history.slice() };
  }

  restoreBoundState(state: GridHistory): ColumnWidth[] {
    if (state.history.length === 0) {
      return [];
    }
    this.resetForReplay();
    const widthsByColumn = new Map<string, number>();
    for (const snap of state.history) {
      this.dispatchAction(snap, widthsByColumn);
    }
    return Array.from(widthsByColumn, ([name, width]) => ({ name, width }));
  }

  private resetForReplay(): void {
    this.history.history = [];
    this.redoStack.history = [];
    this.highlights = [];
    for (const v of this.unfilteredData) {
      v.visible = true;
    }
    this.refilter();
  }

  private dispatchAction(snap: Snapshot, widthsByColumn: Map<string, number>): void {
    try {
      switch (snap.kind) {
        case "showAll":
          this.showAll();
          break;
        case "hideRowsMatching":
          this.hideRowsMatching(snap.column, snap.values);
          break;
        case "hideRowsNotMatching":
          this.hideRowsNotMatching(snap.column, snap.values);
          break;
        case "showRowsMatchingRegex":
          this.showRowsMatchingRegex(snap.pattern, snap.column);
          break;
        case "hideRowsMatchingRegex":
          this.hideRowsMatchingRegex(snap.pattern, snap.column);
          break;
        case "hideNotFirstUnique":
          this.hideNotFirstUnique(snap.column);
          break;
        case "hideRows":
          this.hideRows(snap.selectedRows);
          break;
        case "hideRowsOtherThan":
          this.hideRowsOtherThan(snap.selectedRows);
          break;
        case "hideRowsAbove": {
          const pos = this.resolveReplayPosition(snap.column, snap.anchorValue, snap.row);
          if (pos < 0) {
            this.onLog("replay hideRowsAbove: cannot resolve anchor", "warn");
          } else {
            this.hideRowsAboveAt(pos);
          }
          break;
        }
        case "hideRowsBelow": {
          const pos = this.resolveReplayPosition(snap.column, snap.anchorValue, snap.row);
          if (pos < 0) {
            this.onLog("replay hideRowsBelow: cannot resolve anchor", "warn");
          } else {
            this.hideRowsBelowAt(pos);
          }
          break;
        }
        case "applySort":
          this.sortByColumn(snap.column, snap.direction);
          break;
        case "columnResize":
          widthsByColumn.set(snap.column, snap.width);
          this.pushSnapshot(snap);
          break;
        case "decimate":
          this.decimate(snap.stride);
          break;
        case "transpose":
          this.transpose();
          break;
        case "highlight":
          this.highlights.push(snap.pattern);
          this.pushSnapshot(snap);
          this.notify();
          break;
        case "splitColumn":
          if (snap.splitRecipe === undefined) {
            this.onLog(`replay splitColumn: no recipe for '${snap.column}'`, "warn");
          } else {
            this.splitColumn(snap.column, snap.splitRecipe);
          }
          break;
        case "moveColumn":
          this.moveColumn(snap.column, snap.toAfterColumn);
          break;
        default:
          this.onLog(`replay: unknown action ${(snap as Snapshot).kind}`, "warn");
          break;
      }
    } catch (ex) {
      this.onLog(`replay ${snap.kind} failed: ${(ex as Error).message}`, "warn");
    }
  }

  private resolveReplayPosition(
    sortColumn: string,
    anchorValue: string,
    savedRow: number
  ): number {
    if (this.filteredData.length === 0) return -1;
    const colIdx = sortColumn === "" ? -1 : this.columnNames.indexOf(sortColumn);

    if (
      colIdx >= 0 &&
      savedRow >= 0 &&
      savedRow < this.filteredData.length &&
      equalsIgnoreCase(this.filteredData[savedRow].column(colIdx), anchorValue)
    ) {
      return savedRow;
    }
    if (colIdx >= 0) {
      return this.findNearestRowByColumnValue(colIdx, anchorValue);
    }
    if (savedRow >= 0 && savedRow < this.filteredData.length) {
      return savedRow;
    }
    return -1;
  }

  private findNearestRowByColumnValue(colIdx: number, anchorValue: string): number {
    if (this.filteredData.length === 0) return -1;
    if (anchorValue.trim() !== "" && !isNaN(Number(anchorValue))) {
      const anchorNum = Number(anchorValue);
      let bestIdx = 0;
      let bestDist = Number.MAX_VALUE;
      for (let loop = 0; loop < this.filteredData.length; loop++) {
        const dist = Math.abs(this.filteredData[loop].columnDouble(colIdx) - anchorNum);
        if (dist < bestDist) {
          bestDist = dist;
          bestIdx = loop;
        }
      }
      return bestIdx;
    }
    // Exact (case-insensitive) match wins outright
    for (let loop = 0; loop < this.filteredData.length; loop++) {
      if (equalsIgnoreCase(this.filteredData[loop].column(colIdx), anchorValue)) {
        return loop;
      }
    }
    // First row that would slot at-or-after the anchor in ascending order
    const anchorLower = anchorValue.toLowerCase();
    for (let loop = 0; loop < this.filteredData.length; loop++) {
      const v = (this.filteredData[loop].column(colIdx) ?? "").toLowerCase();
      if (v >= anchorLower) {
        return loop;
      }
    }
    return this.filteredData.length - 1;
  }

  private describeHistory(): string {
    if (this.history.history.length === 0) return "<empty>";
    return this.history.history.map((s, i) => `[${i}]${describe(s)}`).join(" | ");
  }
}

function equalsIgnoreCase(a: string | null, b: string): boolean {
  return (a ?? "").toLowerCase() === b.toLowerCase();
}

function findLast<T>(arr: T[], predicate: (item: T) => boolean): T | undefined {
  for (let loop = arr.length - 1; loop >= 0; loop--) {
    if (predicate(arr[loop])) return arr[loop];
  }
  return undefined;
}

function describe(s: Snapshot): string {
  const parts: string[] = [s.kind];
  if (s.column !== "") parts.push(`col=${s.column}`);
  if (s.addedColumns.length > 0) parts.push(`added=[${s.addedColumns.join(",")}]`);
  if (s.kind === "applySort") parts.push(`dir=${s.direction}`);
  if (s.pattern !== "") parts.push(`pat=${s.pattern}`);
  if (s.anchorValue !== "") parts.push(`anchor=${s.anchorValue}`);
  if (s.row >= 0) parts.push(`row=${s.row}`);
  if (s.width >= 0) parts.push(`w=${s.width}`);
  if (s.values.length > 0) parts.push(`vals=[${s.values.join(",")}]`);
  return parts.join(" ");
}
