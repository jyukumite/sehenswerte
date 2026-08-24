// The log viewer component: virtualized monospace rows over a LogModel,
// reproducing the WinForms LogControl's interaction set - priority threshold
// dropdown + regex filter box (red tint on invalid pattern), Pause, Scroll
// (stick-to-bottom, default on), Mouse-over text toggle, the translucent-red
// corner status overlay ("..." + " filter" + " Paused"), and a right-click
// menu with exactly Clear and Copy (plus Save CSV, the browser stand-in for
// the C# control's always-on file writing).

import React, {
  useCallback,
  useEffect,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import { useVirtualizer } from "@tanstack/react-virtual";
import { LogModel, LogRow } from "./LogModel";
import { PRIORITY_ORDER, timestampPrefix, toToolTip } from "./LogEntry";
import { downloadCsv } from "./csvExport";
import "./LogPanel.css";

const ROW_HEIGHT = 17;
const HOVER_DELAY_MS = 500;
const HOVER_AUTOPOP_MS = 30000;

export interface LogPanelProps {
  model: LogModel;
  showTimestamps?: boolean; // default true
  className?: string;
  style?: React.CSSProperties;
}

interface MenuState {
  x: number;
  y: number;
}

interface HoverState {
  x: number;
  y: number;
  text: string;
}

export function LogPanel(props: LogPanelProps): JSX.Element {
  const { model } = props;
  const showTimestamps = props.showTimestamps ?? true;

  useSyncExternalStore(
    useCallback((cb: () => void) => model.subscribe(cb), [model]),
    () => model.version
  );

  const scrollRef = useRef<HTMLDivElement>(null);
  const [stickToBottom, setStickToBottom] = useState(true);
  const [atEnd, setAtEnd] = useState(true);
  const [hoverEnabled, setHoverEnabled] = useState(true);
  const [menu, setMenu] = useState<MenuState | null>(null);
  const [hover, setHover] = useState<HoverState | null>(null);
  const hoverTimers = useRef<{ show?: number; hide?: number }>({});

  const rows = model.displayedRows;

  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => ROW_HEIGHT,
    overscan: 20,
  });

  // stick to bottom on new rows
  const rowCount = rows.length;
  useEffect(() => {
    if (stickToBottom && rowCount > 0) {
      virtualizer.scrollToIndex(rowCount - 1, { align: "end" });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rowCount, stickToBottom]);

  function onScroll(): void {
    const el = scrollRef.current;
    if (!el) return;
    const nowAtEnd = el.scrollTop + el.clientHeight >= el.scrollHeight - ROW_HEIGHT;
    setAtEnd(nowAtEnd);
    // scrolling away yields stick-to-bottom; scrolling back to the end
    // re-engages it (the C# Scroll checkbox stays authoritative either way)
    if (!nowAtEnd && stickToBottom) {
      setStickToBottom(false);
    } else if (nowAtEnd && !stickToBottom) {
      setStickToBottom(true);
    }
  }

  function clearHoverTimers(): void {
    if (hoverTimers.current.show !== undefined) window.clearTimeout(hoverTimers.current.show);
    if (hoverTimers.current.hide !== undefined) window.clearTimeout(hoverTimers.current.hide);
    hoverTimers.current = {};
  }

  function onRowMouseEnter(e: React.MouseEvent, row: LogRow): void {
    if (!hoverEnabled) return;
    clearHoverTimers();
    setHover(null);
    const x = e.clientX;
    const y = e.clientY;
    hoverTimers.current.show = window.setTimeout(() => {
      setHover({ x, y: y + 14, text: toToolTip(row.entry, row.line, row.splitRow) });
      hoverTimers.current.hide = window.setTimeout(() => setHover(null), HOVER_AUTOPOP_MS);
    }, HOVER_DELAY_MS);
  }

  function copyToClipboard(): void {
    const text = model.copyText(showTimestamps ? timestampPrefix : () => "");
    void navigator.clipboard.writeText(text);
  }

  const status =
    (atEnd ? "" : "... ") +
    (model.filtering ? " filter" : "") +
    (model.paused ? " Paused" : "");

  return (
    <div className={`sw-log ${props.className ?? ""}`} style={props.style}>
      <div
        className="sw-log-scroll"
        ref={scrollRef}
        onScroll={onScroll}
        onMouseLeave={() => {
          clearHoverTimers();
          setHover(null);
        }}
        onContextMenu={(e) => {
          e.preventDefault();
          setMenu({ x: e.clientX, y: e.clientY });
        }}
      >
        <div style={{ height: virtualizer.getTotalSize(), position: "relative" }}>
          {virtualizer.getVirtualItems().map((vrow) => {
            const row = rows[vrow.index];
            if (row === undefined) return null;
            return (
              <div
                key={vrow.index}
                className={`sw-log-row sw-log-${row.entry.priority}`}
                style={{ top: vrow.start, height: ROW_HEIGHT }}
                onMouseEnter={(e) => onRowMouseEnter(e, row)}
              >
                {showTimestamps && (
                  <span className="sw-log-timestamp">{timestampPrefix(row.entry.time)}</span>
                )}
                {row.line}
              </div>
            );
          })}
        </div>
      </div>

      {status.trim() !== "" && <div className="sw-log-status">{status}</div>}

      <div className="sw-log-strip">
        <label>
          <input
            type="checkbox"
            checked={model.paused}
            onChange={(e) => {
              model.paused = e.target.checked;
            }}
          />
          Pause
        </label>
        <label>
          <input
            type="checkbox"
            checked={stickToBottom}
            onChange={(e) => {
              setStickToBottom(e.target.checked);
              if (e.target.checked && rows.length > 0) {
                virtualizer.scrollToIndex(rows.length - 1, { align: "end" });
              }
            }}
          />
          Scroll
        </label>
        <label>
          <input
            type="checkbox"
            checked={hoverEnabled}
            onChange={(e) => setHoverEnabled(e.target.checked)}
          />
          Mouse-over text
        </label>
        <select
          value={model.filterPriority}
          onChange={(e) => {
            model.filterPriority = e.target.value as typeof model.filterPriority;
          }}
          title="Minimum priority to display"
        >
          {PRIORITY_ORDER.map((p) => (
            <option key={p} value={p}>
              {p.charAt(0).toUpperCase() + p.slice(1)}
            </option>
          ))}
        </select>
        <input
          type="text"
          className={model.isFilterRegexInvalid ? "sw-invalid" : ""}
          placeholder="Filter regex..."
          value={model.filterString}
          onChange={(e) => {
            model.filterString = e.target.value;
          }}
          spellCheck={false}
          title={model.isFilterRegexInvalid ? "Invalid regular expression" : "Regex filter over displayed lines"}
        />
      </div>

      {menu !== null && (
        <>
          <div
            style={{ position: "fixed", inset: 0, zIndex: 299 }}
            onMouseDown={() => setMenu(null)}
            onContextMenu={(e) => {
              e.preventDefault();
              setMenu(null);
            }}
          />
          <div className="sw-log-menu" style={{ left: menu.x, top: menu.y }}>
            <button
              onClick={() => {
                setMenu(null);
                model.clear();
              }}
            >
              Clear
            </button>
            <button
              onClick={() => {
                setMenu(null);
                copyToClipboard();
              }}
            >
              Copy to clipboard
            </button>
            <button
              onClick={() => {
                setMenu(null);
                void downloadCsv(model.entries(), "log.csv", true);
              }}
            >
              Save CSV...
            </button>
          </div>
        </>
      )}

      {hover !== null && (
        <div
          className="sw-log-hover"
          style={{
            left: Math.min(hover.x, window.innerWidth - 580),
            top: hover.y,
          }}
        >
          {hover.text}
        </div>
      )}
    </div>
  );
}
