// The DataGrid component: renders a GridModel with virtualized rows and
// reproduces the sehenswerte DataGridControl interaction model:
// - header click = sort toggle; header drag = reorder with drop indicator;
//   right-edge drag = live resize; divider double-click = autofit
// - cell/row selection with ctrl/shift; Ctrl+C copies TSV + HTML
// - Ctrl+Z/Y undo/redo, Ctrl+F regex show, Ctrl+wheel font zoom
// - status strip with the hide/regex/unique/decimate/transpose/highlight/
//   columns/save/load operations
// - host-owned context menu and tooltip render slot

import React, {
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
} from "react";
import { useVirtualizer } from "@tanstack/react-virtual";
import { useSyncExternalStore } from "react";
import { DiffSegment } from "../core/stringDiff";
import { toCsvText, parseCsv } from "../core/csv";
import { GridHistory } from "./GridHistory";
import { ColumnWidth, DEFAULT_COLUMN_WIDTH, GridModel } from "./GridModel";
import { cellBackground, renderCellText } from "./cells";
import {
  buildSelectedCellsData,
  SelectedCellsData,
  selectionBoundingCells,
  writeSelectionToClipboard,
} from "./clipboard";
import { StatusStrip, StripAction } from "./StatusStrip";
import { RegexPromptDialog } from "./dialogs/RegexPromptDialog";
import { ColumnPickerDialog } from "./dialogs/ColumnPickerDialog";
import "./DataGrid.css";

export const COLLAPSED_WIDTH = 14;
const ROW_HEADER_WIDTH = 52;
const DRAG_THRESHOLD_PX = 5;
const RESIZE_EDGE_PX = 5;
const EDGE_SCROLL_ZONE_PX = 28;
const EDGE_SCROLL_STEP_PX = 20;
const EDGE_SCROLL_INTERVAL_MS = 50;
const TOOLTIP_PAUSE_MS = 1500;
const TOOLTIP_SHOW_MS = 10000;
const TOOLTIP_MAX_LENGTH = 1000;
const MIN_FONT_PT = 6;
const MAX_FONT_PT = 72;

export interface CellInfo {
  displayRow: number;
  stableRow: number;
  colIndex: number;
  columnName: string;
  value: string | null;
}

export interface TooltipArgs {
  cell: CellInfo;
  rect: DOMRect; // cell rectangle in viewport coordinates
  displayText: string; // truncated to TOOLTIP_MAX_LENGTH
}

export interface DataGridHandle {
  model: GridModel;
  getSelectedRowIndices: () => number[]; // stable indices
  getSelectedRowsOfColumn: (column: string) => (string | null)[] | null;
  getSelectedColumnNames: () => string[];
  getSelectedRow: () => Record<string, string | null> | null;
  getSelectedCell: () => CellInfo | null;
  getSelectedCellsData: () => SelectedCellsData | null;
  copySelection: () => Promise<void>;
  saveView: () => GridHistory;
  restoreView: (state: GridHistory) => void;
  collapseColumn: (name: string) => void;
  expandColumn: (name: string, width?: number) => void;
  isColumnCollapsed: (name: string) => boolean;
  // display-row coords, like the C# filteredRowIndex
  scrollRowToTop: (displayRow: number) => void;
  firstVisibleRow: () => number; // -1 when nothing is displayed
  visibleRowCount: () => number; // counts partial rows at both ends
}

export interface DataGridProps {
  model: GridModel;
  maskColumns?: string[];
  showCellHints?: boolean; // default true
  onCellClick?: (cell: CellInfo) => void;
  onCellDoubleClick?: (cell: CellInfo) => void;
  // return true to suppress the grid's default handling of the key
  onCellKeyDown?: (e: React.KeyboardEvent, cell: CellInfo | null) => boolean | void;
  onCellContextMenu?: (e: React.MouseEvent, cell: CellInfo | null) => void;
  // vertical scroll only - horizontal scroll never fires it (C# filters on
  // ScrollOrientation.VerticalScroll)
  onVerticalScroll?: () => void;
  renderTooltip?: (args: TooltipArgs) => React.ReactNode;
  columnColours?: Record<string, string>;
  // Load button appears only when the host accepts replacement models
  onLoadCsv?: (model: GridModel) => void;
  className?: string;
  style?: React.CSSProperties;
}

interface SelRect {
  r1: number;
  c1: number;
  r2: number;
  c2: number;
}

function normRect(r: SelRect): SelRect {
  return {
    r1: Math.min(r.r1, r.r2),
    r2: Math.max(r.r1, r.r2),
    c1: Math.min(r.c1, r.c2),
    c2: Math.max(r.c1, r.c2),
  };
}

type HeaderDrag =
  | { kind: "maybe"; name: string; startX: number }
  | { kind: "reorder"; name: string; dropDisplayIndex: number; indicatorX: number }
  | { kind: "resize"; name: string; startX: number; startWidth: number };

type DialogKind = "regexShow" | "regexHide" | "highlight" | "columns" | null;

export const DataGrid = forwardRef<DataGridHandle, DataGridProps>(
  function DataGrid(props, ref) {
    const { model } = props;
    const showCellHints = props.showCellHints ?? true;

    useSyncExternalStore(
      useCallback((cb: () => void) => model.subscribe(cb), [model]),
      () => model.version
    );

    const scrollRef = useRef<HTMLDivElement>(null);
    const containerRef = useRef<HTMLDivElement>(null);
    const fileInputRef = useRef<HTMLInputElement>(null);

    const [widths, setWidths] = useState<Map<string, number>>(new Map());
    const [fontSize, setFontSize] = useState(13);
    const [rects, setRects] = useState<SelRect[]>([]);
    const [anchor, setAnchor] = useState<{ r: number; c: number } | null>(null);
    const [currentCell, setCurrentCell] = useState<{ r: number; c: number } | null>(null);
    const [headerDrag, setHeaderDrag] = useState<HeaderDrag | null>(null);
    const [dialog, setDialog] = useState<DialogKind>(null);
    const [previewHighlight, setPreviewHighlight] = useState<string | null>(null);
    const [headerFilter, setHeaderFilter] = useState("");
    const [tooltip, setTooltip] = useState<TooltipArgs | null>(null);

    const cellDragRef = useRef(false);
    const tooltipTimers = useRef<{ show?: number; hide?: number }>({});
    const edgeScrollTimer = useRef<number | null>(null);

    const rowHeight = Math.max(16, Math.round(fontSize * 1.7));
    const headerHeight = rowHeight + 2;
    const columnNames = model.columnNames;
    const filtered = model.filteredData;

    const widthOf = useCallback(
      (name: string): number => widths.get(name) ?? DEFAULT_COLUMN_WIDTH,
      [widths]
    );

    const totalWidth = useMemo(
      () => ROW_HEADER_WIDTH + columnNames.reduce((acc, n) => acc + widthOf(n), 0),
      [columnNames, widthOf]
    );

    const virtualizer = useVirtualizer({
      count: filtered.length,
      getScrollElement: () => scrollRef.current,
      estimateSize: () => rowHeight,
      overscan: 12,
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
    useEffect(() => virtualizer.measure(), [rowHeight]);

    const maskSet = useMemo(
      () => new Set(props.maskColumns ?? []),
      [props.maskColumns]
    );

    // ---- selection helpers ----

    const clampRect = useCallback(
      (r: SelRect): SelRect => ({
        r1: Math.max(0, Math.min(r.r1, filtered.length - 1)),
        r2: Math.max(0, Math.min(r.r2, filtered.length - 1)),
        c1: Math.max(0, Math.min(r.c1, columnNames.length - 1)),
        c2: Math.max(0, Math.min(r.c2, columnNames.length - 1)),
      }),
      [filtered.length, columnNames.length]
    );

    const isSelected = useCallback(
      (r: number, c: number): boolean =>
        rects.some((rect) => {
          const n = normRect(rect);
          return r >= n.r1 && r <= n.r2 && c >= n.c1 && c <= n.c2;
        }),
      [rects]
    );

    const selectedRowSet = useMemo(() => {
      const set = new Set<number>();
      for (const rect of rects) {
        const n = clampRect(normRect(rect));
        for (let r = n.r1; r <= n.r2 && r < filtered.length; r++) {
          set.add(r);
        }
      }
      return set;
    }, [rects, filtered.length, clampRect]);

    const selectedStableIndices = useCallback((): number[] => {
      return Array.from(selectedRowSet)
        .filter((r) => r < filtered.length)
        .map((r) => filtered[r].index);
    }, [selectedRowSet, filtered]);

    const selectedColIndices = useMemo(() => {
      const set = new Set<number>();
      for (const rect of rects) {
        const n = clampRect(normRect(rect));
        for (let c = n.c1; c <= n.c2 && c < columnNames.length; c++) {
          set.add(c);
        }
      }
      return Array.from(set).sort((a, b) => a - b);
    }, [rects, columnNames.length, clampRect]);

    const cellInfoAt = useCallback(
      (r: number, c: number): CellInfo | null => {
        if (r < 0 || r >= filtered.length || c < 0 || c >= columnNames.length) {
          return null;
        }
        return {
          displayRow: r,
          stableRow: filtered[r].index,
          colIndex: c,
          columnName: columnNames[c],
          value: filtered[r].column(c),
        };
      },
      [filtered, columnNames]
    );

    const currentCellInfo = currentCell
      ? cellInfoAt(currentCell.r, currentCell.c)
      : null;

    // ---- clipboard ----

    const getSelectedCellsData = useCallback((): SelectedCellsData | null => {
      const cells: { row: number; col: number; value: string | null }[] = [];
      const seen = new Set<string>();
      for (const rect of rects) {
        const n = clampRect(normRect(rect));
        for (let r = n.r1; r <= n.r2; r++) {
          for (let c = n.c1; c <= n.c2; c++) {
            const key = `${r}:${c}`;
            if (!seen.has(key)) {
              seen.add(key);
              cells.push({ row: r, col: c, value: filtered[r].column(c) });
            }
          }
        }
      }
      if (cells.length === 0) return null;
      const bounded = selectionBoundingCells(cells, columnNames);
      return buildSelectedCellsData(bounded.headers, bounded.strings);
    }, [rects, filtered, columnNames, clampRect]);

    const copySelection = useCallback(async (): Promise<void> => {
      const data = getSelectedCellsData();
      if (data !== null) {
        await writeSelectionToClipboard(data);
      }
    }, [getSelectedCellsData]);

    // ---- width application from undo/redo/restore ----

    function applyWidths(list: ColumnWidth[] | null): void {
      if (list === null || list.length === 0) return;
      setWidths((prev) => {
        const next = new Map(prev);
        for (const w of list) {
          next.set(w.name, w.width);
        }
        return next;
      });
    }

    const collapseColumn = useCallback(
      (name: string): void => {
        model.recordColumnResize(name, COLLAPSED_WIDTH);
        setWidths((prev) => new Map(prev).set(name, COLLAPSED_WIDTH));
      },
      [model]
    );

    const expandColumn = useCallback(
      (name: string, width = DEFAULT_COLUMN_WIDTH): void => {
        model.recordColumnResize(name, width);
        setWidths((prev) => new Map(prev).set(name, width));
      },
      [model]
    );

    const isColumnCollapsed = useCallback(
      (name: string): boolean => widthOf(name) <= COLLAPSED_WIDTH,
      [widthOf]
    );

    // ---- scroll accessors (C# ScrollRowToTop / FirstVisibleRow / VisibleRowCount) ----
    // The virtualizer has no scrollMargin, so it places row r at scroll offset
    // r*rowHeight while the DOM draws it at headerHeight+r*rowHeight. The sticky
    // header covers exactly that much viewport, so the two cancel: scrollTop /
    // rowHeight is the row sitting immediately below the header. Do not add a
    // scrollMargin to "correct" it - that breaks both this and scrollRowToTop.

    const scrollRowToTop = useCallback(
      (displayRow: number): void => {
        const el = scrollRef.current;
        if (el === null || displayRow < 0 || displayRow >= filtered.length) return;
        // near the end the browser clamps, as the C# swallows the WinForms throw
        el.scrollTop = displayRow * rowHeight;
      },
      [filtered.length, rowHeight]
    );

    const firstVisibleRow = useCallback((): number => {
      const el = scrollRef.current;
      if (el === null || filtered.length === 0) return -1;
      const row = Math.floor(el.scrollTop / rowHeight);
      return Math.max(0, Math.min(row, filtered.length - 1));
    }, [filtered.length, rowHeight]);

    // includePartialRow: true - a row clipped at either end still counts
    const visibleRowCount = useCallback((): number => {
      const el = scrollRef.current;
      if (el === null || filtered.length === 0) return 0;
      const viewport = el.clientHeight - headerHeight;
      if (viewport <= 0) return 0;
      const first = Math.max(0, Math.floor(el.scrollTop / rowHeight));
      const last = Math.min(
        filtered.length - 1,
        Math.ceil((el.scrollTop + viewport) / rowHeight) - 1
      );
      return Math.max(0, last - first + 1);
    }, [filtered.length, rowHeight, headerHeight]);

    // scrollLeft changes (header drag edge-scroll included) must not fire this
    const lastScrollTop = useRef(0);
    const onScroll = useCallback((): void => {
      const el = scrollRef.current;
      if (el === null || el.scrollTop === lastScrollTop.current) return;
      lastScrollTop.current = el.scrollTop;
      props.onVerticalScroll?.();
    }, [props.onVerticalScroll]);

    // ---- imperative API for hosts (the tier-1 host surface) ----

    useImperativeHandle(
      ref,
      (): DataGridHandle => ({
        model,
        getSelectedRowIndices: selectedStableIndices,
        getSelectedRowsOfColumn: (column) =>
          model.rowsOfColumn(selectedStableIndices(), column),
        getSelectedColumnNames: () =>
          selectedColIndices.map((c) => columnNames[c]),
        getSelectedRow: () => {
          const stable = selectedStableIndices();
          return stable.length > 0 ? model.rowAsRecord(stable[0]) : null;
        },
        getSelectedCell: () => {
          // only when exactly one cell is selected, like the C#
          const data = getSelectedCellsData();
          if (
            data !== null &&
            data.strings.length === 1 &&
            data.strings[0].length === 1 &&
            currentCellInfo !== null
          ) {
            return currentCellInfo;
          }
          return null;
        },
        getSelectedCellsData,
        copySelection,
        saveView: () => model.saveBoundState(),
        restoreView: (state) => applyWidths(model.restoreBoundState(state)),
        collapseColumn,
        expandColumn,
        isColumnCollapsed,
        scrollRowToTop,
        firstVisibleRow,
        visibleRowCount,
      })
    );

    // ---- header interactions ----

    function displayIndexFromX(clientX: number): number {
      const scroll = scrollRef.current;
      if (!scroll) return 0;
      const originX =
        scroll.getBoundingClientRect().left - scroll.scrollLeft + ROW_HEADER_WIDTH;
      let x = originX;
      for (let loop = 0; loop < columnNames.length; loop++) {
        const w = widthOf(columnNames[loop]);
        if (clientX < x + w / 2) return loop;
        x += w;
      }
      return columnNames.length;
    }

    function indicatorXFor(displayIndex: number): number {
      let x = ROW_HEADER_WIDTH;
      for (let loop = 0; loop < displayIndex; loop++) {
        x += widthOf(columnNames[loop]);
      }
      return x;
    }

    function stopEdgeScroll(): void {
      if (edgeScrollTimer.current !== null) {
        window.clearInterval(edgeScrollTimer.current);
        edgeScrollTimer.current = null;
      }
    }

    function maybeEdgeScroll(clientX: number): void {
      const scroll = scrollRef.current;
      if (!scroll) return;
      const rect = scroll.getBoundingClientRect();
      let dir = 0;
      if (clientX < rect.left + EDGE_SCROLL_ZONE_PX) dir = -1;
      else if (clientX > rect.right - EDGE_SCROLL_ZONE_PX) dir = 1;
      if (dir === 0) {
        stopEdgeScroll();
      } else if (edgeScrollTimer.current === null) {
        edgeScrollTimer.current = window.setInterval(() => {
          scroll.scrollLeft += dir * EDGE_SCROLL_STEP_PX;
        }, EDGE_SCROLL_INTERVAL_MS);
      }
    }

    function onHeaderMouseDown(e: React.MouseEvent, name: string): void {
      if (e.button !== 0) return;
      const target = e.currentTarget as HTMLElement;
      const rect = target.getBoundingClientRect();
      const nearRightEdge = rect.right - e.clientX <= RESIZE_EDGE_PX;
      const startX = e.clientX;
      const startWidth = widthOf(name);
      e.preventDefault();

      let drag: HeaderDrag = nearRightEdge
        ? { kind: "resize", name, startX, startWidth }
        : { kind: "maybe", name, startX };
      setHeaderDrag(drag);

      const onMove = (ev: MouseEvent): void => {
        if (drag.kind === "resize") {
          const w = Math.max(COLLAPSED_WIDTH, startWidth + (ev.clientX - startX));
          setWidths((prev) => new Map(prev).set(name, w));
        } else {
          if (
            drag.kind === "maybe" &&
            Math.abs(ev.clientX - startX) > DRAG_THRESHOLD_PX
          ) {
            drag = { kind: "reorder", name, dropDisplayIndex: 0, indicatorX: 0 };
          }
          if (drag.kind === "reorder") {
            const idx = displayIndexFromX(ev.clientX);
            drag = { ...drag, dropDisplayIndex: idx, indicatorX: indicatorXFor(idx) };
            setHeaderDrag(drag);
            maybeEdgeScroll(ev.clientX);
          }
        }
      };

      const onUp = (ev: MouseEvent): void => {
        document.removeEventListener("mousemove", onMove);
        document.removeEventListener("mouseup", onUp);
        stopEdgeScroll();
        setHeaderDrag(null);
        if (drag.kind === "resize") {
          const w = Math.max(COLLAPSED_WIDTH, startWidth + (ev.clientX - startX));
          model.recordColumnResize(name, w);
        } else if (drag.kind === "reorder") {
          const from = columnNames.indexOf(name);
          let drop = drag.dropDisplayIndex;
          if (drop !== from && drop !== from + 1) {
            const newAfter = drop === 0 ? "" : columnNames[drop - 1];
            model.moveColumn(name, newAfter);
          }
        } else {
          // movement under threshold = sort click
          model.toggleSortByColumn(name);
        }
      };

      document.addEventListener("mousemove", onMove);
      document.addEventListener("mouseup", onUp);
    }

    function autofitColumn(name: string): void {
      const colIndex = columnNames.indexOf(name);
      if (colIndex < 0) return;
      const canvas = document.createElement("canvas");
      const ctx = canvas.getContext("2d");
      if (!ctx) return;
      ctx.font = `${fontSize}px "Segoe UI", system-ui, sans-serif`;
      let max = ctx.measureText(name).width + 14; // header + sort glyph room
      const italic = `italic ${fontSize}px "Segoe UI", system-ui, sans-serif`;
      for (const row of filtered) {
        const text = row.column(colIndex);
        if (text === null) {
          ctx.font = italic;
          max = Math.max(max, ctx.measureText("null").width);
          ctx.font = `${fontSize}px "Segoe UI", system-ui, sans-serif`;
        } else {
          max = Math.max(max, ctx.measureText(text).width);
        }
      }
      const container = scrollRef.current;
      const clamped = Math.max(
        10,
        Math.min(Math.ceil(max) + 14, (container?.clientWidth ?? 800) - 20)
      );
      setWidths((prev) => new Map(prev).set(name, clamped));
      model.recordColumnResize(name, clamped);
    }

    // ---- cell interactions ----

    function onCellMouseDown(e: React.MouseEvent, r: number, c: number): void {
      if (e.button === 2) {
        // right-click: if outside the selection, select the cell first
        if (!isSelected(r, c)) {
          setRects([{ r1: r, c1: c, r2: r, c2: c }]);
          setAnchor({ r, c });
          setCurrentCell({ r, c });
        }
        return;
      }
      if (e.button !== 0) return;
      containerRef.current?.focus();
      e.preventDefault();
      if (e.ctrlKey || e.metaKey) {
        setRects((prev) => [...prev, { r1: r, c1: c, r2: r, c2: c }]);
        setAnchor({ r, c });
      } else if (e.shiftKey && anchor) {
        setRects((prev) => {
          const next = prev.slice(0, Math.max(0, prev.length - 1));
          next.push({ r1: anchor.r, c1: anchor.c, r2: r, c2: c });
          return next;
        });
      } else {
        setRects([{ r1: r, c1: c, r2: r, c2: c }]);
        setAnchor({ r, c });
      }
      setCurrentCell({ r, c });
      cellDragRef.current = true;
      const onUp = (): void => {
        cellDragRef.current = false;
        document.removeEventListener("mouseup", onUp);
      };
      document.addEventListener("mouseup", onUp);
      props.onCellClick?.(cellInfoAt(r, c) as CellInfo);
    }

    function onCellMouseEnter(r: number, c: number): void {
      if (cellDragRef.current) {
        setRects((prev) => {
          if (prev.length === 0) return prev;
          const next = prev.slice(0, prev.length - 1);
          const last = prev[prev.length - 1];
          next.push({ ...last, r2: r, c2: c });
          return next;
        });
        setCurrentCell({ r, c });
      } else if (showCellHints) {
        scheduleTooltip(r, c);
      }
    }

    function onRowHeaderMouseDown(e: React.MouseEvent, r: number): void {
      if (e.button !== 0) return;
      containerRef.current?.focus();
      e.preventDefault();
      const full = { r1: r, c1: 0, r2: r, c2: columnNames.length - 1 };
      if (e.ctrlKey || e.metaKey) {
        setRects((prev) => [...prev, full]);
      } else if (e.shiftKey && anchor) {
        setRects([{ r1: anchor.r, c1: 0, r2: r, c2: columnNames.length - 1 }]);
      } else {
        setRects([full]);
        setAnchor({ r, c: 0 });
      }
      setCurrentCell({ r, c: 0 });
    }

    // ---- tooltip ----

    function clearTooltipTimers(): void {
      if (tooltipTimers.current.show !== undefined) {
        window.clearTimeout(tooltipTimers.current.show);
      }
      if (tooltipTimers.current.hide !== undefined) {
        window.clearTimeout(tooltipTimers.current.hide);
      }
      tooltipTimers.current = {};
    }

    function scheduleTooltip(r: number, c: number): void {
      clearTooltipTimers();
      setTooltip(null);
      const info = cellInfoAt(r, c);
      if (info === null || info.value === null || maskSet.has(info.columnName)) {
        return; // masked and null cells suppress the hover, like the C#
      }
      tooltipTimers.current.show = window.setTimeout(() => {
        const el = scrollRef.current?.querySelector(
          `[data-cell="${r}:${c}"]`
        ) as HTMLElement | null;
        if (!el) return;
        const displayText =
          (info.value ?? "").length > TOOLTIP_MAX_LENGTH
            ? (info.value ?? "").slice(0, TOOLTIP_MAX_LENGTH)
            : info.value ?? "";
        setTooltip({ cell: info, rect: el.getBoundingClientRect(), displayText });
        tooltipTimers.current.hide = window.setTimeout(
          () => setTooltip(null),
          TOOLTIP_SHOW_MS
        );
      }, TOOLTIP_PAUSE_MS);
    }

    // ---- keyboard ----

    function currentColumnName(): string {
      return columnNames[currentCell?.c ?? 0] ?? columnNames[0] ?? "";
    }

    function doStripAction(action: StripAction): void {
      switch (action) {
        case "showAll":
          model.showAll();
          break;
        case "undo":
          applyWidths(model.undo());
          break;
        case "redo":
          applyWidths(model.redo());
          break;
        case "hideSelected":
          model.hideRows(selectedStableIndices());
          setRects([]);
          break;
        case "hideUnselected":
          model.hideRowsOtherThan(selectedStableIndices());
          setRects([]);
          break;
        case "hideAbove":
          if (currentCellInfo) {
            model.hideRowsAbove(currentCellInfo.stableRow);
            setRects([]);
          }
          break;
        case "hideBelow":
          if (currentCellInfo) {
            model.hideRowsBelow(currentCellInfo.stableRow);
            setRects([]);
          }
          break;
        case "regexShow":
          setDialog("regexShow");
          break;
        case "regexHide":
          setDialog("regexHide");
          break;
        case "hideMatch": {
          const values = selectedCellValuesInCurrentColumn();
          if (values !== null) {
            model.hideRowsMatching(currentColumnName(), values);
            setRects([]);
          }
          break;
        }
        case "hideUnmatch": {
          const values = selectedCellValuesInCurrentColumn();
          if (values !== null) {
            model.hideRowsNotMatching(currentColumnName(), values);
            setRects([]);
          }
          break;
        }
        case "unique":
          model.hideNotFirstUnique(currentColumnName());
          break;
        case "decimate":
          model.decimate(10);
          break;
        case "transpose":
          model.transpose();
          setRects([]);
          setCurrentCell(null);
          break;
        case "highlight":
          setDialog("highlight");
          break;
        case "columns":
          setDialog("columns");
          break;
        case "save":
          saveCsv();
          break;
        case "load":
          fileInputRef.current?.click();
          break;
        default:
          break;
      }
    }

    function selectedCellValuesInCurrentColumn(): (string | null)[] | null {
      const col = currentCell?.c ?? -1;
      if (col < 0) return null;
      const values = new Set<string | null>();
      for (const r of Array.from(selectedRowSet)) {
        if (r < filtered.length) {
          values.add(filtered[r].column(col));
        }
      }
      return values.size > 0 ? Array.from(values) : null;
    }

    function saveCsv(): void {
      // all columns, visible rows - matching the C# SaveToCsv
      const text = toCsvText(
        model.filteredData.map((r) => r.strings),
        model.columnNames
      );
      const blob = new Blob([text], { type: "text/csv" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = "grid.csv";
      a.click();
      URL.revokeObjectURL(url);
    }

    function onLoadFile(e: React.ChangeEvent<HTMLInputElement>): void {
      const file = e.target.files?.[0];
      e.target.value = "";
      if (!file || !props.onLoadCsv) return;
      const numeric = window.confirm(
        "Load as numbers? (Cancel loads as strings)"
      );
      file.text().then((text) => {
        const parsed = parseCsv(text);
        const newModel = numeric
          ? GridModel.fromDoubles(
              parsed.rows.map((row) => row.map((v) => Number(v) || 0)),
              parsed.columnHeadings
            )
          : GridModel.fromStrings(parsed.rows, parsed.columnHeadings);
        props.onLoadCsv?.(newModel);
      });
    }

    function onKeyDown(e: React.KeyboardEvent): void {
      const ctrl = e.ctrlKey || e.metaKey;
      if (ctrl && e.key.toLowerCase() === "c") {
        e.preventDefault();
        void copySelection();
        return;
      }
      if (ctrl && !e.shiftKey && e.key.toLowerCase() === "z") {
        e.preventDefault();
        doStripAction("undo");
        return;
      }
      if (ctrl && (e.key.toLowerCase() === "y" || (e.shiftKey && e.key.toLowerCase() === "z"))) {
        e.preventDefault();
        doStripAction("redo");
        return;
      }
      if (ctrl && e.key.toLowerCase() === "f") {
        e.preventDefault();
        setDialog("regexShow");
        return;
      }
      if (ctrl && e.key.toLowerCase() === "s") {
        e.preventDefault();
        saveCsv();
        return;
      }
      if (ctrl && e.key.toLowerCase() === "o" && props.onLoadCsv) {
        e.preventDefault();
        fileInputRef.current?.click();
        return;
      }
      if (ctrl && (e.key === "+" || e.key === "=")) {
        e.preventDefault();
        setFontSize((f) => Math.min(MAX_FONT_PT, f + 1));
        return;
      }
      if (ctrl && e.key === "-") {
        e.preventDefault();
        setFontSize((f) => Math.max(MIN_FONT_PT, f - 1));
        return;
      }
      if (e.altKey && !ctrl) {
        const mnemonics: Record<string, StripAction> = {
          m: "hideMatch",
          u: "hideUnmatch",
          q: "unique",
          d: "decimate",
          t: "transpose",
          h: "highlight",
          c: "columns",
          f: "columns", // Alt+F is a hardcoded alias for Columns in the C#
        };
        const action = mnemonics[e.key.toLowerCase()];
        if (action !== undefined) {
          e.preventDefault();
          doStripAction(action);
          return;
        }
      }
      // anything else is the host's (Enter = drill down etc.)
      const suppress = props.onCellKeyDown?.(e, currentCellInfo);
      if (suppress === true) {
        e.preventDefault();
      }
    }

    // ---- ctrl+wheel font zoom (non-passive so preventDefault works) ----

    useEffect(() => {
      const el = scrollRef.current;
      if (!el) return;
      const onWheel = (e: WheelEvent): void => {
        if (e.ctrlKey) {
          e.preventDefault();
          setFontSize((f) =>
            Math.max(MIN_FONT_PT, Math.min(MAX_FONT_PT, f + (e.deltaY < 0 ? 1 : -1)))
          );
        }
      };
      el.addEventListener("wheel", onWheel, { passive: false });
      return () => el.removeEventListener("wheel", onWheel);
    }, []);

    // clear selection when the row set changes shape under us
    const filteredLength = filtered.length;
    useEffect(() => {
      setRects((prev) => prev.map(clampRect));
      setCurrentCell((prev) =>
        prev && prev.r >= filteredLength ? null : prev
      );
    }, [filteredLength, clampRect]);

    // ---- render ----

    const primarySort = model.primarySortKey();
    const headerFilterLower = headerFilter.toLowerCase();
    const virtualRows = virtualizer.getVirtualItems();

    return (
      <div
        className={`sw-datagrid ${props.className ?? ""}`}
        style={props.style}
        ref={containerRef}
        tabIndex={0}
        onKeyDown={onKeyDown}
      >
        <div
          className="sw-grid-scroll"
          ref={scrollRef}
          onScroll={onScroll}
          onMouseLeave={() => {
            clearTooltipTimers();
            setTooltip(null);
          }}
          onContextMenu={(e) => {
            if (props.onCellContextMenu) {
              e.preventDefault();
              const cellEl = (e.target as HTMLElement).closest("[data-cell]");
              let cell: CellInfo | null = null;
              if (cellEl) {
                const [r, c] = (cellEl.getAttribute("data-cell") ?? "0:0")
                  .split(":")
                  .map(Number);
                cell = cellInfoAt(r, c);
              }
              props.onCellContextMenu(e, cell);
            }
          }}
        >
          <div
            className="sw-grid-canvas"
            style={{
              width: totalWidth,
              height: headerHeight + virtualizer.getTotalSize(),
              fontSize,
            }}
          >
            <div className="sw-grid-header" style={{ height: headerHeight }}>
              <div
                className="sw-row-header"
                style={{ width: ROW_HEADER_WIDTH, height: headerHeight }}
              />
              {columnNames.map((name) => (
                <div
                  key={name}
                  className={`sw-header-cell ${
                    headerFilter !== "" &&
                    name.toLowerCase().includes(headerFilterLower)
                      ? "sw-filter-match"
                      : ""
                  }`}
                  style={{ width: widthOf(name), height: headerHeight }}
                  title={name}
                  onMouseDown={(e) => onHeaderMouseDown(e, name)}
                >
                  {name}
                  {primarySort?.columnName === name && (
                    <span className="sw-sort-glyph">
                      {primarySort.direction === "asc" ? "▲" : "▼"}
                    </span>
                  )}
                  <div
                    className="sw-resize-grip"
                    onDoubleClick={(e) => {
                      e.stopPropagation();
                      autofitColumn(name);
                    }}
                  />
                </div>
              ))}
              {headerDrag?.kind === "reorder" && (
                <div
                  className="sw-drop-indicator"
                  style={{ left: headerDrag.indicatorX }}
                />
              )}
            </div>

            {virtualRows.map((vrow) => {
              const row = filtered[vrow.index];
              if (row === undefined) return null;
              return (
                <div
                  key={vrow.index}
                  className={`sw-grid-row ${vrow.index % 2 === 1 ? "sw-row-alt" : ""}`}
                  style={{
                    top: headerHeight + vrow.start,
                    height: rowHeight,
                  }}
                >
                  <div
                    className="sw-row-header"
                    style={{ width: ROW_HEADER_WIDTH, height: rowHeight }}
                    onMouseDown={(e) => onRowHeaderMouseDown(e, vrow.index)}
                  >
                    {row.index + 1}
                  </div>
                  {columnNames.map((name, c) => {
                    const value = row.column(c);
                    const masked = maskSet.has(name);
                    const bg = cellBackground(
                      row,
                      c,
                      value,
                      props.columnColours?.[name],
                      model.highlights,
                      previewHighlight
                    );
                    const selected = isSelected(vrow.index, c);
                    const isCurrent =
                      currentCell?.r === vrow.index && currentCell?.c === c;
                    return (
                      <div
                        key={name}
                        data-cell={`${vrow.index}:${c}`}
                        className={`sw-cell${selected ? " sw-selected" : ""}${
                          isCurrent ? " sw-current" : ""
                        }`}
                        style={{
                          width: widthOf(name),
                          height: rowHeight,
                          ...(bg !== undefined && !selected
                            ? { background: bg }
                            : {}),
                        }}
                        onMouseDown={(e) => onCellMouseDown(e, vrow.index, c)}
                        onMouseEnter={() => onCellMouseEnter(vrow.index, c)}
                        onDoubleClick={() =>
                          props.onCellDoubleClick?.(
                            cellInfoAt(vrow.index, c) as CellInfo
                          )
                        }
                      >
                        {renderCellText(
                          value,
                          row.diffs?.[c] as DiffSegment[] | null | undefined,
                          masked
                        )}
                      </div>
                    );
                  })}
                </div>
              );
            })}
          </div>
        </div>

        <StatusStrip
          rowsShown={filtered.length}
          rowsTotal={model.unfilteredData.length}
          colsTotal={columnNames.length}
          isFiltered={model.isFiltered}
          canUndo={model.canUndo}
          canRedo={model.canRedo}
          hasSelection={selectedRowSet.size > 0}
          hasCurrentCell={currentCell !== null}
          canLoad={props.onLoadCsv !== undefined}
          onAction={doStripAction}
        />

        <input
          ref={fileInputRef}
          type="file"
          accept=".csv,text/csv"
          style={{ display: "none" }}
          onChange={onLoadFile}
        />

        {tooltip !== null &&
          (props.renderTooltip ? (
            props.renderTooltip(tooltip)
          ) : (
            <div
              className="sw-tooltip"
              style={{
                left: Math.min(tooltip.rect.left, window.innerWidth - 500),
                top: tooltip.rect.bottom + 4,
              }}
            >
              {tooltip.displayText}
            </div>
          ))}

        {dialog === "regexShow" && (
          <RegexPromptDialog
            title={`Show rows matching regex in "${currentColumnName()}"`}
            mruKey="sw-grid-regex-mru"
            isRegex={true}
            onAccept={(pattern) => {
              setDialog(null);
              model.showRowsMatchingRegex(pattern, currentColumnName());
            }}
            onClose={() => setDialog(null)}
          />
        )}
        {dialog === "regexHide" && (
          <RegexPromptDialog
            title={`Hide rows matching regex in "${currentColumnName()}"`}
            mruKey="sw-grid-regex-mru"
            isRegex={true}
            onAccept={(pattern) => {
              setDialog(null);
              model.hideRowsMatchingRegex(pattern, currentColumnName());
            }}
            onClose={() => setDialog(null)}
          />
        )}
        {dialog === "highlight" && (
          <RegexPromptDialog
            title="Highlight cells containing"
            hint="Substring match, case-insensitive; each highlight gets the next palette colour"
            mruKey="sw-grid-highlight-mru"
            isRegex={false}
            onPreview={(value) => setPreviewHighlight(value === "" ? null : value)}
            onAccept={(value) => {
              setDialog(null);
              setPreviewHighlight(null);
              model.addHighlight(value);
            }}
            onClose={() => {
              setDialog(null);
              setPreviewHighlight(null);
            }}
          />
        )}
        {dialog === "columns" && (
          <ColumnPickerDialog
            columns={columnNames.map((name) => ({
              name,
              checked: !isColumnCollapsed(name),
            }))}
            onFilterChanged={setHeaderFilter}
            onAccept={(checkedNames, focusColumn) => {
              setDialog(null);
              const checkedSet = new Set(checkedNames);
              for (const name of columnNames) {
                const collapsed = isColumnCollapsed(name);
                if (checkedSet.has(name) && collapsed) {
                  expandColumn(name);
                } else if (!checkedSet.has(name) && !collapsed) {
                  collapseColumn(name);
                }
              }
              if (focusColumn !== null) {
                const c = columnNames.indexOf(focusColumn);
                if (c >= 0) {
                  setCurrentCell({ r: currentCell?.r ?? 0, c });
                }
              }
              setHeaderFilter("");
            }}
            onClose={() => {
              setDialog(null);
              setHeaderFilter("");
            }}
          />
        )}
      </div>
    );
  }
);
