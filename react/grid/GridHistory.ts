// Port of sehenswerte DataGridControlHistory.cs.
// A Snapshot records one user operation plus the pre-op visible row set, so the
// history doubles as an undo stack and as a portable, replayable view recipe
// (saveView/restoreView replay it, possibly onto different data).

import type { GridRow } from "./GridModel";

export type SortDirection = "asc" | "desc";

export type Operation =
  | "showAll"
  | "applySort"
  | "hideRowsMatching"
  | "hideRowsNotMatching"
  | "showRowsMatchingRegex"
  | "hideRowsMatchingRegex"
  | "hideNotFirstUnique"
  | "hideRows"
  | "hideRowsOtherThan"
  | "hideRowsAbove"
  | "hideRowsBelow"
  | "columnResize"
  | "decimate"
  | "transpose"
  | "splitColumn"
  | "moveColumn"
  | "highlight";

// Derived columns produced by splitColumn. Functional like the C# delegate;
// same-session save/restore replays it, but it is not JSON-serializable.
export type SplitRecipe = (
  sourceColumn: string
) => { header: string; values: (string | null)[] }[];

export interface Snapshot {
  kind: Operation;
  column: string;
  addedColumns: string[];
  direction: SortDirection; // applySort
  pattern: string; // regex / highlight substring
  anchorValue: string; // hideRowsAbove/Below: cell value at click time
  row: number; // hideRowsAbove/Below positional fallback
  width: number; // columnResize
  stride: number; // decimate: keep every Nth row
  values: string[]; // hideRowsMatching/NotMatching
  // hideRows/hideRowsOtherThan: stable row indices selected at op time.
  // Identity-based, so replay is only meaningful against the same data.
  selectedRows: number[];
  splitRecipe?: SplitRecipe;
  // moveColumn: column is the column that was moved.
  // fromAfterColumn = previous left neighbour (where undo puts it back).
  // toAfterColumn   = new left neighbour. Empty string = leftmost.
  fromAfterColumn: string;
  toAfterColumn: string;

  visibleRows: number[]; // pre-op visible-row stable indices
  visibleRowRefs: GridRow[] | null; // direct row refs for fast undo path
  preTransposeUnfiltered: GridRow[] | null;
  preTransposeColumnNames: string[] | null;
}

export function newSnapshot(kind: Operation): Snapshot {
  return {
    kind,
    column: "",
    addedColumns: [],
    direction: "asc",
    pattern: "",
    anchorValue: "",
    row: -1,
    width: -1,
    stride: 0,
    values: [],
    selectedRows: [],
    fromAfterColumn: "",
    toAfterColumn: "",
    visibleRows: [],
    visibleRowRefs: null,
    preTransposeUnfiltered: null,
    preTransposeColumnNames: null,
  };
}

// Full history oldest-first; last entry is the most recent op.
export interface GridHistory {
  history: Snapshot[];
}

export function newGridHistory(): GridHistory {
  return { history: [] };
}
