// The Scope component: a trace-list panel plus a DPR-aware canvas painted by
// paintScope, with the WinForms wheel gestures (Ctrl = horizontal zoom around
// the cursor, Alt = pan) and the hover crosshair + value readout. Repaints
// are rAF-coalesced off the model's change notifications.

import React, {
  useCallback,
  useEffect,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import { formatUnixTime, toStringRoundUnit } from "./axisFormat";
import { PaintedGroup, PaintedLayout, paintScope } from "./ScopePainter";
import { ScopeModel } from "./ScopeModel";
import "./Scope.css";

export interface ScopeProps {
  model: ScopeModel;
  showTraceList?: boolean; // default true
  // Floor for a stacked group's height. Groups share the viewport equally until that
  // would squeeze them below this, at which point the paint area grows taller than
  // its container and scrolls - otherwise a session's 30-odd traces get ~40px each,
  // most of which the axis gutter eats. The C# scrolls for the same reason.
  minGroupHeight?: number; // default 80
  className?: string;
  style?: React.CSSProperties;
}

const DEFAULT_MIN_GROUP_HEIGHT = 80;

interface HoverInfo {
  x: number;
  y: number;
  lines: string[];
}

function groupAtY(layout: PaintedLayout | null, y: number): PaintedGroup | null {
  if (layout === null) return null;
  return layout.groups.find((g) => y >= g.top && y < g.top + g.height) ?? null;
}

export function Scope(props: ScopeProps): JSX.Element {
  const { model } = props;
  const showTraceList = props.showTraceList ?? true;

  useSyncExternalStore(
    useCallback((cb: () => void) => model.subscribe(cb), [model]),
    () => model.version
  );

  const canvasRef = useRef<HTMLCanvasElement>(null);
  const paintRef = useRef<HTMLDivElement>(null);
  const wrapRef = useRef<HTMLDivElement>(null);
  const layoutRef = useRef<PaintedLayout | null>(null);
  const rafRef = useRef<number | null>(null);
  const [hover, setHover] = useState<HoverInfo | null>(null);
  const [listFilter, setListFilter] = useState("");

  const repaint = useCallback((): void => {
    if (rafRef.current !== null) return; // already scheduled
    rafRef.current = window.requestAnimationFrame(() => {
      rafRef.current = null;
      const canvas = canvasRef.current;
      const host = paintRef.current;
      const wrap = wrapRef.current;
      if (!canvas || !host || !wrap) return;
      const dpr = window.devicePixelRatio || 1;

      // Grow the painted content past the viewport rather than squeezing groups. The
      // height is written to the DOM directly, not held in state: repaint runs inside
      // a rAF, and setState there would schedule the render that schedules the next
      // repaint.
      const groupCount = Math.max(1, model.visibleViewGroups().length);
      const minHeight = props.minGroupHeight ?? DEFAULT_MIN_GROUP_HEIGHT;
      const desired = Math.max(host.clientHeight, groupCount * minHeight);
      if (wrap.style.height !== `${desired}px`) {
        wrap.style.height = `${desired}px`;
      }

      // Width from the viewport (unchanged by scrolling, and already excludes the
      // scrollbar); height is the content height computed above rather than measured,
      // because the style assignment has not been laid out yet - and jsdom never
      // lays out at all.
      const w = host.clientWidth;
      const h = desired;
      if (w === 0 || h === 0) return;
      if (canvas.width !== Math.round(w * dpr) || canvas.height !== Math.round(h * dpr)) {
        canvas.width = Math.round(w * dpr);
        canvas.height = Math.round(h * dpr);
      }
      const ctx = canvas.getContext("2d");
      if (!ctx) return;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      layoutRef.current = paintScope(ctx, w, h, model);
    });
  }, [model, props.minGroupHeight]);

  // repaint on model changes and container resizes
  useEffect(() => {
    repaint();
    const unsubscribe = model.subscribe(repaint);
    const host = paintRef.current;
    const observer = new ResizeObserver(repaint);
    if (host) observer.observe(host);
    return () => {
      unsubscribe();
      observer.disconnect();
      if (rafRef.current !== null) {
        window.cancelAnimationFrame(rafRef.current);
        // MUST clear the handle, not just cancel the frame: it doubles as the
        // "already scheduled" guard above, so leaving it set makes every later
        // repaint() a no-op and the canvas never paints again. A cancelled frame
        // never runs the callback that would have cleared it. StrictMode's
        // mount/unmount/remount hits this on the very first paint.
        rafRef.current = null;
      }
    };
  }, [model, repaint]);

  // wheel: Ctrl = horizontal zoom around the cursor, Alt = pan
  // (non-passive so preventDefault beats browser zoom)
  useEffect(() => {
    const host = paintRef.current;
    if (!host) return;
    const onWheel = (e: WheelEvent): void => {
      if (e.ctrlKey) {
        e.preventDefault();
        // the wrap, not the host: it moves with the scroll offset
        const rect = (wrapRef.current ?? host).getBoundingClientRect();
        const frac = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
        const oldZoom = model.zoomValue;
        const factor = e.deltaY < 0 ? 0.8 : 1.25;
        const newZoom = Math.max(1e-7, Math.min(1, oldZoom * factor));
        // keep the value under the cursor stationary
        const cursorPan = model.panValue + frac * oldZoom;
        model.setZoomPan(newZoom, cursorPan - frac * newZoom);
      } else if (e.altKey) {
        e.preventDefault();
        const delta = ((e.deltaY / 120) * model.zoomValue) / 30;
        model.setZoomPan(model.zoomValue, model.panValue + delta);
      }
    };
    host.addEventListener("wheel", onWheel, { passive: false });
    return () => host.removeEventListener("wheel", onWheel);
  }, [model]);

  function onMouseMove(e: React.MouseEvent): void {
    const wrap = wrapRef.current;
    if (!wrap) return;
    // relative to the painted content, so a scrolled view still hits the right group
    const rect = wrap.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    const group = groupAtY(layoutRef.current, y);
    if (group === null || x < group.projLeft || x > group.projLeft + group.projWidth) {
      setHover(null);
      return;
    }
    const frac = (x - group.projLeft) / (group.projWidth || 1);
    const hValue = group.hLeft + frac * (group.hRight - group.hLeft);
    const lines: string[] = [
      group.isYT
        ? formatUnixTime(hValue, group.hRight - group.hLeft, true)
        : toStringRoundUnit(hValue, 5, 3, group.unit),
    ];
    for (const pv of group.views) {
      // invert the pixel back into this view's slice for its value readout
      const localFrac = (x - pv.valueRectLeft) / (pv.valueRectWidth || 1);
      if (localFrac < 0 || localFrac > 1) continue;
      const index = Math.max(
        0,
        Math.min(
          pv.win.count - 1,
          Math.floor(localFrac * pv.win.count)
        )
      );
      const value = pv.view.data.samples[pv.win.first + index];
      lines.push(
        `${pv.view.viewName}[${pv.win.first + index}] ${toStringRoundUnit(value, 6, 3, pv.view.data.verticalUnit)}`
      );
    }
    setHover({ x, y, lines });
  }

  const listFilterLower = listFilter.toLowerCase();
  let listRegex: RegExp | null = null;
  if (listFilter !== "") {
    try {
      listRegex = new RegExp(listFilter, "i");
    } catch {
      listRegex = null;
    }
  }
  const listViews = model.views.filter((v) => {
    if (listFilter === "") return true;
    if (listRegex !== null) return listRegex.test(v.viewName);
    return v.viewName.toLowerCase().includes(listFilterLower);
  });

  return (
    <div className={`sw-scope ${props.className ?? ""}`} style={props.style}>
      {showTraceList && (
        <div className="sw-scope-list">
          <input
            type="text"
            placeholder="Filter traces (regex)..."
            value={listFilter}
            onChange={(e) => setListFilter(e.target.value)}
            spellCheck={false}
          />
          <div className="sw-scope-list-rows">
            {listViews.map((view) => (
              <div
                key={view.viewName}
                className={`sw-scope-list-row${view.visible ? "" : " sw-hidden-trace"}${
                  model.selected.has(view) ? " sw-selected-trace" : ""
                }`}
                onClick={(e) => {
                  if (!e.ctrlKey) model.selected.clear();
                  if (model.selected.has(view)) {
                    model.selected.delete(view);
                  } else {
                    model.selected.add(view);
                  }
                  model.setZoomPan(model.zoomValue, model.panValue); // nudge
                }}
              >
                <input
                  type="checkbox"
                  checked={view.visible}
                  onClick={(e) => e.stopPropagation()}
                  onChange={(e) => model.setViewVisible(view.viewName, e.target.checked)}
                />
                <span className="sw-scope-swatch" style={{ background: view.colour }} />
                {view.viewName}
              </div>
            ))}
          </div>
          <div className="sw-scope-list-buttons">
            <button
              title="Toggle visibility of every trace (Switch Visible)"
              onClick={() => {
                model.views.forEach((v) => model.setViewVisible(v.viewName, !v.visible));
              }}
            >
              Switch
            </button>
            <button title="Auto range all groups (Ctrl+R)" onClick={() => model.autoRangeAll()}>
              Range
            </button>
            <button title="Reset zoom/pan" onClick={() => model.setZoomPan(1, 0)}>
              1:1
            </button>
            <button
              title="Group the selected traces into one pane (Ctrl+G)"
              onClick={() => {
                const names = Array.from(model.selected).map((v) => v.viewName);
                if (names.length > 1) model.groupViews(names, false);
              }}
            >
              Group
            </button>
          </div>
        </div>
      )}

      <div
        className="sw-scope-paint"
        ref={paintRef}
        onMouseMove={onMouseMove}
        onMouseLeave={() => setHover(null)}
        tabIndex={0}
        onKeyDown={(e) => {
          if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "r") {
            e.preventDefault();
            model.autoRangeAll();
          }
        }}
      >
        <div className="sw-scope-canvas-wrap" ref={wrapRef}>
          <canvas ref={canvasRef} />
          <div className="sw-scope-overlay">
          {hover !== null && (
            <>
              <div
                style={{
                  position: "absolute",
                  left: hover.x,
                  top: 0,
                  bottom: 0,
                  width: 1,
                  background: model.skin.crosshairColour,
                }}
              />
              <div
                className="sw-hover-label"
                style={{
                  left: Math.min(hover.x + 10, (wrapRef.current?.clientWidth ?? 300) - 220),
                  top: Math.max(2, hover.y - 10),
                }}
              >
                {hover.lines.map((line, i) => (
                  <div key={i}>{line}</div>
                ))}
              </div>
            </>
          )}
          </div>
        </div>
      </div>
    </div>
  );
}
