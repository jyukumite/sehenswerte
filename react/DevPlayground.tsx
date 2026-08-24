// Interactive harness for the three controls: a tab each for DataGrid, Log and
// Scope, over synthetic data. This is how the interaction feel gets exercised in a
// real browser - header drag/resize/sort, scope zoom/pan/hover, log scrolling -
// which the jest suites cannot cover.
//
// It needs nothing from any host: no data source, no auth, no configuration. Mount
// it at a route in whatever app embeds this library, e.g.
//
//   import { DevPlayground } from "<...>/sehenswerte/react";
//   if (window.location.pathname.startsWith("/dev")) return <DevPlayground />;
//
// placed ahead of that app's own routing/session gate so it stays reachable when
// the rest of the app cannot start.

import React, { useMemo, useRef, useState } from "react";

import { DataGrid } from "./grid/DataGrid";
import type { DataGridHandle } from "./grid/DataGrid";
import { GridModel } from "./grid/GridModel";
import { LogModel } from "./log/LogModel";
import { LogPanel } from "./log/LogPanel";
import { extendPath, fromSimpleSink, newEntry } from "./log/LogEntry";
import type { EntrySink, LogEntry, LogPriority } from "./log/LogEntry";
import { Scope } from "./scope/Scope";
import { ScopeModel } from "./scope/ScopeModel";
import { darkSkin } from "./scope/skin";

/* ───────────────────────── shared log ─────────────────────────── */
// One LogModel for the whole harness, wired the way a host wires it (an
// extendPath-scoped sink per consumer), so the Log tab shows the grid's and
// scope's own entries rather than only synthetic ones.

const harnessLog = new LogModel();
harnessLog.itemLimit = 20000;

const rootSink: EntrySink = (entry) => harnessLog.add(entry);
const scopedSink = (name: string): EntrySink => extendPath(rootSink, name);
// grid and scope still take the (message, priority) signature
const simpleSink = (name: string) => fromSimpleSink(scopedSink(name));

/* ───────────────────────── grid fixture ───────────────────────── */

// Columns chosen to exercise the model, not just to fill space: embedded digits
// for natural sort (item2 before item10), nulls, heavy duplicates for the
// hide/show ops, a wide text column for autofit, and numerics for value ops.
const NOTES = [
  "ok",
  "retry after transient timeout",
  "value clamped to the configured maximum before persisting",
  "",
  "checksum mismatch - payload discarded and re-requested from the device",
];
const REGIONS = ["au-syd", "us-west", "eu-central", "ap-tokyo"];

const GRID_COLUMNS = ["id", "name", "region", "reading", "count", "notes", "timestamp"];

function makeGridRows(count: number): (string | null)[][] {
  const rows: (string | null)[][] = [];
  for (let i = 0; i < count; i++) {
    rows.push([
      String(i + 1),
      `item${i % 250}`, // natural-sort bait: item2 vs item10 vs item100
      REGIONS[i % REGIONS.length], // duplicates to hide/filter on
      i % 17 === 0 ? null : (Math.sin(i / 40) * 1000).toFixed(3),
      i % 23 === 0 ? null : String(20 + (i % 60)),
      NOTES[i % NOTES.length], // wide column for autofit
      new Date(Date.UTC(2026, 7, 1 + (i % 24), i % 24, i % 60)).toISOString(),
    ]);
  }
  return rows;
}

/* ───────────────────────── scope fixtures ───────────────────────── */

function sine(n: number, cycles: number, amplitude = 1): Float64Array {
  const out = new Float64Array(n);
  for (let i = 0; i < n; i++) {
    out[i] = amplitude * Math.sin((2 * Math.PI * cycles * i) / n);
  }
  return out;
}

function noisy(n: number): Float64Array {
  const out = new Float64Array(n);
  let v = 0;
  for (let i = 0; i < n; i++) {
    v += Math.random() - 0.5;
    out[i] = v;
  }
  return out;
}

function steps(n: number): Float64Array {
  const out = new Float64Array(n);
  for (let i = 0; i < n; i++) {
    out[i] = Math.floor((i / n) * 8) % 4;
  }
  return out;
}

/* ───────────────────────── panels ───────────────────────── */

const ROW_COUNTS = [100, 5000, 100000];

function GridTab(): JSX.Element {
  const [rowCount, setRowCount] = useState(5000);
  const [model, setModel] = useState(() =>
    GridModel.fromStrings(makeGridRows(5000), GRID_COLUMNS, simpleSink("Grid"))
  );
  const [note, setNote] = useState<string | null>(null);
  const gridRef = useRef<DataGridHandle | null>(null);

  function reload(count: number) {
    setRowCount(count);
    setModel(GridModel.fromStrings(makeGridRows(count), GRID_COLUMNS, simpleSink("Grid")));
  }

  function readSelection() {
    const handle = gridRef.current;
    setNote(
      handle
        ? `getSelectedRowsOfColumn("name") -> ${JSON.stringify(
            handle.getSelectedRowsOfColumn("name")
          )}`
        : "no grid ref"
    );
  }

  return (
    <Tab
      controls={
        <>
          {ROW_COUNTS.map((n) => (
            <button key={n} onClick={() => reload(n)} disabled={n === rowCount}>
              {n.toLocaleString()} rows
            </button>
          ))}
          <button onClick={readSelection}>Read selection (imperative handle)</button>
          <Hint>
            drag/resize/double-click headers, click-drag cells, Ctrl+C, Ctrl+Z/Y,
            Ctrl+F, Ctrl+wheel zooms the font. Autofit (double-click a header edge)
            measures every row, so expect a pause on the 100k fixture.
          </Hint>
        </>
      }
      note={note}
    >
      <DataGrid ref={gridRef} model={model} onLoadCsv={setModel} />
    </Tab>
  );
}

function LogTab(): JSX.Element {
  const counter = useRef(0);
  const emitScoped = useMemo(() => scopedSink("Playground"), []);

  function emit(
    priority: LogPriority,
    text: string,
    extra?: Partial<Omit<LogEntry, "text" | "priority" | "time">>
  ) {
    emitScoped(newEntry(text, priority, extra));
  }

  function seed() {
    emit("debug", `debug ${++counter.current} - hidden until you lower the threshold`);
    emit("info", `info ${++counter.current} - steady state`);
    emit("warn", `warn ${++counter.current} - warnings render green, deliberately`);
    emit("error", `error ${++counter.current} - request failed`, {
      memberName: "readSelection",
      sourcePath: "react/DevPlayground.tsx",
      sourceLineNumber: 42,
    });
    emit(
      "exception",
      `exception ${++counter.current}\n  at outer()\n  at middle()\n  at inner()`
    );
  }

  function flood() {
    for (let i = 0; i < 2000; i++) {
      emit(
        i % 50 === 0 ? "warn" : "info",
        `flood ${++counter.current} region=${REGIONS[i % REGIONS.length]} value=${(
          Math.sin(i / 7) * 100
        ).toFixed(2)}`
      );
    }
  }

  return (
    <Tab
      controls={
        <>
          <button onClick={seed}>One of each priority</button>
          <button onClick={flood}>Flood 2000</button>
          <button onClick={() => harnessLog.clear()}>Clear</button>
          <Hint>
            the Grid and Scope tabs log into this same model (":Grid" / ":Scope"), so
            switching tabs after using them shows real entries. Debug rows are hidden
            at the default threshold (info) - that is the C# behaviour, not a bug.
            Right-click for Clear/Copy/Save CSV; hover a row for the entry card.
          </Hint>
        </>
      }
    >
      <LogPanel model={harnessLog} />
    </Tab>
  );
}

function ScopeTab(): JSX.Element {
  const modelRef = useRef<ScopeModel | null>(null);
  if (modelRef.current === null) {
    modelRef.current = new ScopeModel(darkSkin, scopedSink("Scope"));
  }
  const model = modelRef.current;

  function addDemoTraces() {
    const n = 1000000;
    model.ensure("sine 1M").update(sine(n, 2500));
    model.ensure("random walk").update(noisy(n));
    model.ensure("steps").update(steps(5000));
    // polygonDigital is already the TraceView default, so the staircase "steps"
    // trace needs no setting; switch the sine to the continuous mode instead so
    // both paint paths are actually on screen.
    const sineView = model.viewByName("sine 1M");
    if (sineView) sineView.paintMode = "polygonContinuous";
    model.autoRangeAll();
  }

  function addAffinePair() {
    const a = model.ensure("power (rpm axis)");
    a.update(sine(1100, 3, 500));
    a.setHorizontalAffine(0, 10, "rpm"); // 0..11000 rpm
    a.verticalUnit = "W";
    const b = model.ensure("torque (rpm axis)");
    b.update(sine(600, 2, 40));
    b.setHorizontalAffine(0, 10, "rpm"); // 0..6000 rpm - ragged, aligns by value
    b.verticalUnit = "Nm";
    model.groupViews(["power (rpm axis)", "torque (rpm axis)"]);
    model.autoRangeAll();
  }

  function addYtPair() {
    const now = Date.now() / 1000;
    const n = 2000;
    const t1 = new Float64Array(n);
    const t2 = new Float64Array(n);
    for (let i = 0; i < n; i++) {
      t1[i] = now - 600 + (600 * i) / n; // last 10 minutes
      t2[i] = now - 300 + (300 * i) / n; // last 5 minutes
    }
    model.ensure("speed (YT)").updateYT(sine(n, 6, 20), t1);
    model.ensure("current (YT)").updateYT(sine(n, 3, 80), t2);
    model.groupViews(["speed (YT)", "current (YT)"]);
    model.autoRangeAll();
  }

  return (
    <Tab
      controls={
        <>
          <button onClick={addDemoTraces}>1M-sample demo</button>
          <button onClick={addAffinePair}>Affine group demo</button>
          <button onClick={addYtPair}>YT group demo</button>
          <button onClick={() => model.clear()}>Clear</button>
          <Hint>Ctrl+wheel zooms, Alt+wheel pans, Ctrl+R auto-ranges</Hint>
        </>
      }
    >
      <Scope model={model} />
    </Tab>
  );
}

/* ───────────────────────── chrome ───────────────────────── */

function Hint({ children }: { children: React.ReactNode }): JSX.Element {
  return <span style={{ color: "#7a828c", fontSize: 12, marginLeft: 4 }}>{children}</span>;
}

function Tab(props: {
  controls: React.ReactNode;
  note?: string | null;
  children: React.ReactNode;
}): JSX.Element {
  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%", minHeight: 0 }}>
      <div
        style={{
          display: "flex",
          gap: 8,
          alignItems: "center",
          flexWrap: "wrap",
          padding: "8px 16px",
          flex: "none",
        }}
      >
        {props.controls}
      </div>
      {props.note && (
        <div style={{ padding: "0 16px 8px", color: "#e3b341", fontSize: 12 }}>
          {props.note}
        </div>
      )}
      <div style={{ flex: 1, minHeight: 0, padding: "0 16px 16px" }}>{props.children}</div>
    </div>
  );
}

const TABS: { id: string; label: string; render: () => JSX.Element }[] = [
  { id: "grid", label: "DataGrid", render: () => <GridTab /> },
  { id: "log", label: "Log", render: () => <LogTab /> },
  { id: "scope", label: "Scope", render: () => <ScopeTab /> },
];

export function DevPlayground(): JSX.Element {
  const [tab, setTab] = useState("grid");
  const active = TABS.find((t) => t.id === tab) ?? TABS[0];

  return (
    <div
      style={{
        height: "100vh",
        display: "flex",
        flexDirection: "column",
        background: "#0e1116",
        color: "#e6e8eb",
        font: "13px system-ui, sans-serif",
      }}
    >
      <header
        style={{
          display: "flex",
          alignItems: "center",
          gap: 8,
          padding: "10px 16px",
          borderBottom: "1px solid #2b313a",
          flex: "none",
        }}
      >
        <strong>sehenswerte-react playground</strong>
        <span style={{ color: "#7a828c", fontSize: 12, marginRight: 8 }}>
          synthetic data - no host wiring
        </span>
        {TABS.map((t) => (
          <button
            key={t.id}
            onClick={() => setTab(t.id)}
            style={{
              background: t.id === tab ? "#1f6feb" : "transparent",
              color: t.id === tab ? "#fff" : "#8a93a0",
              border: "1px solid #2b313a",
              padding: "4px 12px",
              borderRadius: 4,
              cursor: "pointer",
            }}
          >
            {t.label}
          </button>
        ))}
      </header>
      {/* keyed so switching tabs rebuilds the control - fresh model per visit */}
      <main style={{ flex: 1, minHeight: 0 }} key={active.id}>
        {active.render()}
      </main>
    </div>
  );
}

export default DevPlayground;
