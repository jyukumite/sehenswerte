// Port of the DataGridControl status strip: right-aligned flow of operation
// buttons with tooltips and the row/column count label. Button visibility
// rules match the WinForms control (Show All only when filtered, Undo/Redo
// only when available, Transpose disabled over 200 visible rows).

import React from "react";

export type StripAction =
  | "showAll"
  | "undo"
  | "redo"
  | "hideSelected"
  | "hideUnselected"
  | "hideAbove"
  | "hideBelow"
  | "regexHide"
  | "regexShow"
  | "hideMatch"
  | "hideUnmatch"
  | "unique"
  | "decimate"
  | "transpose"
  | "highlight"
  | "columns"
  | "save"
  | "load";

export interface StatusStripProps {
  rowsShown: number;
  rowsTotal: number;
  colsTotal: number;
  isFiltered: boolean;
  canUndo: boolean;
  canRedo: boolean;
  hasSelection: boolean;
  hasCurrentCell: boolean;
  canLoad: boolean;
  onAction: (action: StripAction) => void;
}

const TRANSPOSE_MAX_ROWS = 200;

interface ButtonSpec {
  action: StripAction;
  label: string;
  tooltip: string;
  group: string;
  visible?: (p: StatusStripProps) => boolean;
  enabled?: (p: StatusStripProps) => boolean;
}

// Alt mnemonics from the WinForms strip are marked with & in labels and
// handled by the grid's keydown (Alt+M/U/Q/D/T/H/C).
const BUTTONS: ButtonSpec[] = [
  { action: "showAll", label: "Show All", tooltip: "Clear all filters", group: "view", visible: (p) => p.isFiltered },
  { action: "undo", label: "Undo", tooltip: "Undo (Ctrl+Z)", group: "view", visible: (p) => p.canUndo },
  { action: "redo", label: "Redo", tooltip: "Redo (Ctrl+Y)", group: "view", visible: (p) => p.canRedo },
  { action: "hideSelected", label: "Hide Selected", tooltip: "Hide the selected rows", group: "hide", enabled: (p) => p.hasSelection },
  { action: "hideUnselected", label: "Hide Unselected", tooltip: "Keep only the selected rows", group: "hide", enabled: (p) => p.hasSelection },
  { action: "hideAbove", label: "Hide Above", tooltip: "Hide rows above the current row", group: "hide", enabled: (p) => p.hasCurrentCell },
  { action: "hideBelow", label: "Hide Below", tooltip: "Hide rows below the current row", group: "hide", enabled: (p) => p.hasCurrentCell },
  { action: "regexHide", label: "Regex Hide", tooltip: "Hide rows matching a regex in the current column", group: "regex", enabled: (p) => p.hasCurrentCell },
  { action: "regexShow", label: "Regex Show", tooltip: "Keep only rows matching a regex in the current column (Ctrl+F)", group: "regex", enabled: (p) => p.hasCurrentCell },
  { action: "hideMatch", label: "Hide Match", tooltip: "Hide rows whose cell matches any selected value (Alt+M)", group: "regex", enabled: (p) => p.hasSelection },
  { action: "hideUnmatch", label: "Hide Unmatch", tooltip: "Keep only rows whose cell matches any selected value (Alt+U)", group: "regex", enabled: (p) => p.hasSelection },
  { action: "unique", label: "Unique", tooltip: "Keep the first row per distinct value and add a count column (Alt+Q)", group: "shape", enabled: (p) => p.hasCurrentCell },
  { action: "decimate", label: "Decimate", tooltip: "Keep every 10th visible row (Alt+D)", group: "shape" },
  {
    action: "transpose",
    label: "Transpose",
    tooltip: "Pivot the visible view (Alt+T); disabled over 200 visible rows",
    group: "shape",
    enabled: (p) => p.rowsShown <= TRANSPOSE_MAX_ROWS,
  },
  { action: "highlight", label: "Highlight", tooltip: "Highlight cells containing a substring (Alt+H)", group: "view" },
  { action: "columns", label: "Columns", tooltip: "Show, hide, and find columns (Alt+C)", group: "view" },
  { action: "save", label: "Save", tooltip: "Save visible rows to CSV (Ctrl+S)", group: "file" },
  { action: "load", label: "Load", tooltip: "Load a CSV (Ctrl+O)", group: "file", visible: (p) => p.canLoad },
];

export function StatusStrip(props: StatusStripProps): JSX.Element {
  const rowsLabel =
    props.rowsShown === props.rowsTotal
      ? `${props.rowsTotal} rows`
      : `${props.rowsShown}/${props.rowsTotal} rows`;

  return (
    <div className="sw-status-strip">
      <span className="sw-strip-label">
        {rowsLabel} · {props.colsTotal} cols
      </span>
      <span style={{ flex: 1 }} />
      {BUTTONS.filter((b) => b.visible?.(props) ?? true).map((b) => (
        <button
          key={b.action}
          className={`sw-strip-button sw-group-${b.group}`}
          title={b.tooltip}
          disabled={!(b.enabled?.(props) ?? true)}
          onClick={() => props.onAction(b.action)}
        >
          {b.label}
        </button>
      ))}
    </div>
  );
}
