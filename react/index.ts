// Public surface of sehenswerte-react. Hosts import from here only.

export { naturalCompare, naturalCompareNullable } from "./core/naturalCompare";
export { parseCsv, parseCsvRows, rowToCsvText, toCsvText } from "./core/csv";
export type { CsvTable, CsvParseOptions, CsvValue } from "./core/csv";
export { stringDiff, diffLeftText, diffRightText } from "./core/stringDiff";
export type { DiffSide, DiffSegment } from "./core/stringDiff";

export {
  GridModel,
  GridRow,
  GridRowString,
  GridRowDouble,
  DEFAULT_COLUMN_WIDTH,
} from "./grid/GridModel";
export type { ColumnWidth, LogSink, LogPriority, CellDiff } from "./grid/GridModel";
export { newSnapshot, newGridHistory } from "./grid/GridHistory";
export type {
  GridHistory,
  Snapshot,
  Operation,
  SortDirection,
  SplitRecipe,
} from "./grid/GridHistory";
export {
  buildSelectedCellsData,
  selectionBoundingCells,
  writeSelectionToClipboard,
} from "./grid/clipboard";
export type { SelectedCellsData, SelectedCell } from "./grid/clipboard";

export {
  newEntry,
  extendPath,
  demoteToDebug,
  fromSimpleSink,
  toDisplayedLine,
  timestampPrefix,
  PRIORITY_RANK,
  PRIORITY_ORDER,
} from "./log/LogEntry";
export type { LogEntry, EntrySink } from "./log/LogEntry";
export { LogModel, DEFAULT_ITEM_LIMIT } from "./log/LogModel";
export type { LogRow } from "./log/LogModel";
export { LogPanel } from "./log/LogPanel";
export type { LogPanelProps } from "./log/LogPanel";
export { entriesToCsv, downloadCsv } from "./log/csvExport";

export { DataGrid, COLLAPSED_WIDTH } from "./grid/DataGrid";
export type {
  DataGridHandle,
  DataGridProps,
  CellInfo,
  TooltipArgs,
} from "./grid/DataGrid";
export { highlightPalette } from "./grid/cells";

export { TraceData, newTraceFeature, computeStatistics } from "./scope/TraceData";
export type {
  TraceFeature,
  TraceFeatureType,
  TraceStatistics,
} from "./scope/TraceData";
export { TraceView, autoRangeGroup } from "./scope/TraceView";
export type { DrawnWindow } from "./scope/TraceView";
export { ScopeModel } from "./scope/ScopeModel";
export { Scope } from "./scope/Scope";
export type { ScopeProps } from "./scope/Scope";
export { paintScope } from "./scope/ScopePainter";
export type { PaintedLayout, PaintedGroup } from "./scope/ScopePainter";
export { classify, valueWindow, subWindow } from "./scope/groupHorizontal";
export type {
  GroupMember,
  HorizontalDomain,
  HorizontalKind,
  HorizontalMode,
} from "./scope/groupHorizontal";
export { projectCurves, projectMinMax, projectLog } from "./scope/projection";
export type { CurvePaintMode, ProjectedCurves } from "./scope/projection";
export {
  getPartitions,
  getLogPartitions,
  toStringRound,
  toStringRoundUnit,
  formatUnixTime,
  roundSignificant,
  roundSignificantUp,
  roundSignificantDown,
} from "./scope/axisFormat";
export { lightSkin, darkSkin } from "./scope/skin";
export type { ScopeSkin } from "./scope/skin";

// Interactive harness for all three controls over synthetic data - mount it at a
// route to exercise the interaction feel the jest suites cannot cover.
export { DevPlayground } from "./DevPlayground";
