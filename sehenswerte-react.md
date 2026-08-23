# sehenswerte-react: port the scope + data grid to React for the app2-cloud local admin tool

## Context

The WinForms Flitescope tool is built on the sehenswerte control library (public MIT repo,
submoduled into flitescope). Its two flagship controls are `SehensControl` (the oscilloscope,
~19k lines) and `DataGridControl` (the grid, ~4.5k lines). `app2-cloud/webapps/admin_site` is the
in-progress React replacement for Flitescope's cloud-facing tabs (React 18, CRA 5, plain JS,
never deployed, local-first: login defaults to `http://127.0.0.1:8000` / `localdev.py`, CORS wide
open, localhost dbexecute cap is 1 GB). It already has working auth (OAuth2+TOTP+rolling refresh),
permission helpers, and a dbexecute call - but renders results as a raw `<table>`, and has
commented-out `/scope` and `/map` routes waiting for exactly this work.

Goal: reimplement the scope and grid in React with **similar look, feel, and interaction** to the
WinForms originals, consumed by admin_site.

### Decisions (confirmed with Mike)

| Decision | Choice |
|---|---|
| Scope rendering | Custom canvas port - port the pure C# math, render on per-group `<canvas>`, React chrome |
| Grid | Headless TanStack Table + TanStack Virtual; port BoundData semantics as a plain TS engine class |
| Language | TypeScript for all new library code; existing admin_site pages stay JS (CRA 5 mixes per-file) |
| Code home | **REVISED 2026-08-23:** a `react/` folder inside the existing sehenswerte repo (the sehenswerte-react GitHub repo is dropped - Mike deletes it). The co-location IS the reminder to keep C# and TS in step, and the two ports can share golden test vectors. app2-cloud submodules sehenswerte at `webapps/admin_site/src/sehenswerte` (done; the C# tree rides along harmlessly - CRA only compiles what is imported); flitescope already submodules sehenswerte, so it needs no new wiring. Active TS development happens in the app2-cloud submodule checkout (where yarn start/test run); commit/push from inside that checkout and pull in flitescope's copy (or vice versa). |

The library-boundary discipline stands: the `react/` tree has **zero imports from admin_site
code**, its own `README.md`, no Fliteboard-proprietary strings, and self-contained tests.
admin_site imports from `src/sehenswerte/react` only. License is the parent repo's
(MIT with additional terms, in the top-level README).

**Scope additions (Mike, 2026-08-23):** the LOG control joins the port scope (CsvLog entry
model + the cross-thread log viewer control as a React log panel) - survey pending, planned
as its own milestone after the scope control.

## What the surveys established (design inputs)

### Grid (`DataGridControl` + `DataGridBoundData` + `DataGridControlHistory`)
- **Schema-free**: data is a string or double matrix + `ColumnNames`. No POCO binding - the whole
  `colN`-reflection / ITypedList saga is a WinForms artifact the port deletes.
- **Architecture core = undo history as the view recipe**: every mutating op (17 kinds: hides,
  regex show/hide, match/unmatch, unique+count-column, decimate, transpose, sort, column
  resize/move/split, highlight) pushes a Snapshot; undo restores captured pre-op visible rows;
  redo re-dispatches; `SaveView()/RestoreView()` replay the whole history - deliberately portable
  across datasets (regex/match ops replay on new data; index-based hides are best-effort).
- **Sort is cumulative multi-key**, keys derived by walking the history (last-wins per column),
  natural/numeric-aware string compare, stable via `ResortIndex`.
- Filtering is hide-based (rows become invisible), never a stored predicate. No filter row.
- Effectively read-only to the user; programmatic `SetCell`/`CellColour`/`CellDiffs`/`MaskColumns`.
- Rendering extras: per-cell colours, rotating 8-colour substring Highlight, null rendered as
  italic "null", per-cell string-diff-in-red, column collapse (not hide) to MinimumWidth,
  stable original row number in the row header, font zoom Ctrl+wheel.
- Status strip right-aligned buttons with Alt mnemonics; Ctrl+C copies TSV + CF_HTML + CSV;
  Ctrl+F = regex show; Ctrl+Z/Y undo/redo; Ctrl+S/O CSV save/load.
- Host contract: `CellContextMenuStripNeeded` (host owns the whole context menu), tooltip as a
  render-slot event (host can render image previews), `CellKeyDown` with suppress.
- Tier-1 API actually used by Flitescope: `LoadRows(strings|doubles)`, `LoadJson`, `AppendRows`
  (streaming), `GetSelectedRowsOfColumn` (the most-used call), `SetCell`, `CellColour`,
  `ApplyColumnColour`, `MaskColumns`, `SortByColumn`, `SplitColumn`, `SaveView/RestoreView`,
  `SelectedCellsToClipboardFormats`, collapse/expand column.
- **No virtualization in the original** - the port must add it (TanStack Virtual).

### Scope (`SehensControl`)
- Two-level model: `TraceData` (named sample channel: `Update(samples[, sps])`,
  `Update(samples, unixTime)`, `UpdateByRef`, `AppendRing` streaming ring, features, statistics,
  affine axis) and `TraceView` (per-display state: paint mode, colour, log modes, visibility).
  Multiple views can share one TraceData. Groups = stacked panes sharing axes.
- Three time regimes: sample-number, uniform rate (`sps`), and YT (explicit unix-time array,
  non-uniform). `MatchUnixTimes` aligns traces on wall clock.
- **Port near-verbatim (pure, tested, high bug density in C# history)**:
  `GroupHorizontal` classification (Stretch / ValueAlign / Incompatible + per-trace ValueRect
  sub-windows), `ProjectLog` (2-decade log compress), `Project2dCurves` min/max-envelope
  decimation (one point per pixel: dense -> min+max envelopes + filled band; sparse -> nearest or
  interpolate; Average/PeakHold/Points variants), the canonical
  `HorizontalValueAt`/`SampleAtHorizontalValue` map (everything - hover, gutter, extents - must
  route through it), the stored-vs-effective `LogVertical` split.
- Interaction model to preserve: wheel = list scroll, Ctrl+wheel = H zoom, Shift+wheel = V zoom,
  Alt+wheel = pan; left-drag reorders groups / resizes pane height / drags handles; right-drag =
  wipe select (sample region, NOT box zoom); right-click = context menu; double-click = trace/group
  settings editor; hover = crosshair + stacked value/stats labels, suppressed beyond drawn data.
  Global `ZoomValue`/`PanValue`, pan clamped to `[0, 1-zoom]`.
- Host integration that must survive: `OnPanZoomChanged` + first-class "currently drawn samples"
  output (`DrawnSamples`/`DrawnSamplesYT`) - Flitescope's map linkage depends on it.
- Flitescope only uses a ~20-member API slice; the 13 paint modes x 25 calculated types x FFT
  filters long tail is NOT needed for milestone 1.
- Dropped in the port: click-zone hit-testing (DOM gives it free), bitmap-per-group parallel
  paint + generation guards (single-threaded JS), WinForms focus juggling, registry persistence
  (-> localStorage).

### admin_site facts that shape integration
- MUI 5 installed but entirely unused; look is a hand-rolled dark theme (`#0e1116` bg, `#151a21`
  panels, `#1f6feb` accent) with inline styles. **The port ships its own CSS (CSS modules or plain
  .css), themable via CSS variables - do not adopt MUI** (keeps the future repo dependency-light).
- Data channel: `POST /flitescope/db/execute` -> `{headers, rows, content}` (gzip transparent to
  fetch). WS dbexecute exists for large results (permission `flitescope_dbexecute_ws`, JWT in
  message body, `db_page`/`GZIP`/`db_done` frames) - defer to a later milestone.
- Known admin_site bugs to fix while integrating (client-side only):
  1. `canWrite` omits `flitescope_techsupport && database_write` (backend allows it).
  2. Missing `flitescope_pw` app-config silently downgrades to read-only - make it an explicit error.
  3. `content: "Truncated"` is never surfaced - a truncated result looks complete.

## Deliverable structure

```
webapps/admin_site/
  src/sehenswerte/              # submodule: the sehenswerte repo itself
   react/                       # this port (paths below are relative to react/)
    LICENSE  README.md
    core/
      naturalCompare.ts         # natural/numeric-aware compare (port of NaturalCompare)
      csv.ts                    # CSV parse/serialize (RFC-ish, matches CSVLoad/CSVSave behaviour)
      stringDiff.ts             # port of StringDiff (for cell diff-in-red)
    grid/
      GridModel.ts              # BoundData port: unfiltered/filtered rows, stable Index,
                                #   ResortIndex, all 17 ops, Refilter, multi-key sort
      GridHistory.ts            # Snapshot type (a discriminated union - JSON-serializable),
                                #   push/undo/redo, DispatchAction replay, SaveView/RestoreView
      GridModel.test.ts         # ports of DataGridBoundDataTest + DataGridControlHistoryTest
      DataGrid.tsx              # TanStack Table + Virtual rendering of a GridModel
      StatusStrip.tsx           # the button strip, counts, Alt mnemonics
      cells.tsx                 # cell renderer: colours, highlight, null-italic, mask, diff spans
      clipboard.ts              # selection -> TSV + text/html (Excel-compatible table)
      dialogs/                  # RegexPromptDialog (live preview + localStorage MRU),
                                #   ColumnPickerDialog (filterable checkbox list)
    scope/
      TraceData.ts              # samples (Float64Array), sps, unixTime, affine axis, AppendRing,
                                #   features, statistics, generation counter
      TraceView.ts              # per-view display state + drawn-projection cache
      ScopeModel.ts             # trace map, groups, zoom/pan, selection, events
      groupHorizontal.ts        # near-verbatim port (pure)
      projection.ts             # Project2dCurves min/max binning + sparse/interpolate/average,
                                #   ProjectLog, canonical horizontalValueAt/sampleAtHorizontalValue
      projection.test.ts        # port the C# projection/GroupHorizontal tests
      ScopePainter.ts           # canvas painter: envelope band, polygons, points, axes/gutters,
                                #   DPR-aware, rAF-coalesced invalidation
      Scope.tsx                 # component: stacked group panes (one canvas each), DOM overlays
                                #   for hover labels / features / handles / embedded mode buttons
      TraceList.tsx             # left panel: rows, regex filter (localStorage MRU), bulk toggles
      interactions.ts           # wheel-modifier dispatch, drag state machines, wipe select
      ContextMenu.tsx           # menu framework (Sort/ShowWhen/hotkeys) + the milestone-1 items
  src/pages/CloudDataPage.js    # modified: raw <table> -> DataGrid
  src/pages/ScopePage.jsx       # new: the /scope route (currently commented out in MainShell)
  src/shell/MainShell.js        # modified: enable /scope route
  src/api/BackendApi.js         # modified: bug fixes 1-3 above
```

TypeScript in CRA 5: `npm i -D typescript @types/react @types/react-dom` inside admin_site; CRA
auto-detects `.ts/.tsx` and generates `tsconfig.json`. Tests run under CRA's built-in Jest
(`yarn test`). Dependencies added: `@tanstack/react-table`, `@tanstack/react-virtual` - nothing
else (no chart lib, no MUI usage).

Style note: new library code uses idiomatic TS naming (camelCase members, no `m_`), ASCII-only
source, and ports the C# tests alongside the code (same-directory `.test.ts`, mirroring the
repo's tests-next-to-code convention).

## Key design translations (C# -> web)

- **History as data**: the C# Snapshot's one non-serializable member is the `SplitRecipe`
  delegate; represent it as data (`{sourceColumn, kind: "json", fields: [...]}`) so the entire
  history - and therefore SaveView blobs - is plain JSON (localStorage-persistable, attachable to
  a URL later). Keep the C# invariant triad: every op = union variant + push-before-mutate +
  replay case; a missed replay case is the known bug class.
- **Sort/filter stay in the model, not TanStack**: TanStack renders columns, sizing, and the
  virtualized row window; row order and visibility come from `GridModel.filteredRows`. Do not use
  TanStack's own sorting/filtering state - it cannot express the history-derived semantics.
- **Clipboard**: `ClipboardItem` with `text/plain` (TSV) + `text/html` (bordered `<table>`,
  which Excel/Sheets accept). Browsers cannot write a CSV clipboard format - drop it; Save-to-CSV
  covers that need. Copy shape matches C#: bounding rows x cols of the selection, sparse cells
  empty, header row only when >1 row.
- **Cell diff / highlight**: rendered as `<span>` runs inside the cell instead of
  MeasureCharacterRanges clip regions - simpler and faster in DOM.
- **Column collapse** (not hide) preserved: collapsed = fixed narrow width, still exported/copied.
  The 200-column render cap can be dropped (virtualize columns if a real dataset ever needs it;
  status text keeps the `shown/total` pattern).
- **Canvas mechanics**: one canvas per group pane sized by `devicePixelRatio`; waveforms + axes
  aliased 1px paths (matching the C# deliberate no-AA policy and its perf rationale); the min/max
  envelope is a single `Path2D` band fill + two polylines. Projection cache keyed on
  (data generation, pane width, zoom/pan, log modes) - recompute only on miss, exactly like
  `SnapshotReprojectionRequired`.
- **Wheel handling**: non-passive wheel listener with `preventDefault()` so Ctrl+wheel doesn't
  browser-zoom; `contextmenu` prevented on the paint surface so right-drag wipe-select and
  right-click menu work as in WinForms.
- **Hover/hit-testing**: labels, trim/trigger handles, features, and embedded mode buttons are
  absolutely-positioned DOM elements over the canvas - the whole `TraceViewClickZone` subsystem
  disappears.
- **Dialogs**: small custom modal kit (native `<dialog>` element + focus trap); RegexPrompt keeps
  the live-preview event (grid tints matches while typing) and MRU (localStorage instead of
  registry).
- **"What is drawn" as output**: `Scope` exposes `onPanZoomChanged(view => {drawnRange, samples})`
  mirroring `DrawnSamplesYT` - this is the seam a future map page uses.

## Progress

- 2026-08-23: M1 DONE - GridModel/GridHistory + naturalCompare/csv/stringDiff ported with the
  full C# test suites (104 tests green under `yarn test`). M2 DONE (core) - DataGrid.tsx
  (TanStack Virtual rows; react-table v9 skipped as pure overhead since the model owns all
  table semantics), StatusStrip, cells (highlight palette/null-italic/mask/diff spans),
  Modal/RegexPrompt/ColumnPicker dialogs, clipboard TSV+HTML, CSV save/load, wired into
  CloudDataPage with saveView/restoreView across re-runs; admin_site bugs 1-3 fixed
  (BackendApi write parity + flitescope_pw warning; Truncated/URL surfaced in CloudDataPage).
  Extras: fixed the long-broken App.test.js (react-router-dom v7 ships a dead `main` entry -
  jest moduleNameMapper points at dist/index.js + react-router dom-export; TextEncoder +
  ResizeObserver polyfills in setupTests.js). Everything uncommitted, per Mike.
  M2 remainder for later: host context menu on CloudDataPage (drill-down queries), row-height
  autofit, arrow-key cell navigation.

## Milestones

**M1 - grid engine (pure TS, no UI).** `GridModel` + `GridHistory` + `naturalCompare` + `csv` +
ported unit tests (row ops, hide ops, regex ops, unique+count column, multi-key sort from history,
undo/redo incl. redo-clear, replay-on-different-data, column move/resize/split). Done when the
ported C# test suite passes under `yarn test`.

**M2 - grid UI, wired in.** `DataGrid.tsx` (virtualized rows, sticky header, hand-rolled header
drag-reorder with drop indicator + edge auto-scroll, live edge resize, divider double-click
autofit, selection model, Ctrl+C multi-format copy, Ctrl+wheel font zoom, status strip with all
buttons + mnemonics, the two dialogs, CSV save/load, host-owned context menu + tooltip render
slot). Replace `CloudDataPage`'s raw table with it; fix admin_site bugs 1-3 (write-perm parity,
explicit `flitescope_pw` error, surface `Truncated`). Done when a dbexecute result is browsable
with the same gestures as the WinForms grid.

**M3 - scope core.** `TraceData`/`TraceView`/`ScopeModel`, `groupHorizontal` + `projection` ports
with tests, `ScopePainter` + `Scope.tsx` static rendering: stacked groups, min/max envelope for
dense traces, PolygonDigital/PolygonContinuous/Points modes, sample/rate/YT + affine axes, axis
gutters and ticks, auto-range, trace colours from the 12-colour skin palette. Done when a
1M-sample synthetic trace renders and re-renders at interactive rates.

**M4 - scope interaction + page.** Wheel-modifier zoom/pan/scroll, pan clamp, hover crosshair +
value/stats labels (suppressed beyond drawn data), wipe select, group drag-reorder and height
resize, trace list panel with regex filter, `GroupViews`/`MatchUnixTimes`, features
(Text/Line/Highlight/handles subset), context menu shell (auto range, group/ungroup, hide/show/
close, paint mode, log modes, PNG screenshot via `canvas.toBlob`), `onPanZoomChanged` output.
New `/scope` route: run a query -> grid -> "scope selected columns" (the FliteGridTab pattern),
plus a session `playback_frames` loader as the YT demo source. Done when the core WinForms
muscle-memory gestures behave identically.

**Backlog (explicitly deferred, tracked in the port's README):** FFT/spectral/XY/XYZ paint modes,
calculated traces + PythonScript hook, triggers, audio playback, WAV/Parquet/Matlab import-export,
transpose-grid edge cases, WS dbexecute streaming into `AppendRows`, dual-axis (never existed in
C# either), repo extraction to GitHub + submodule wiring (Mike does the repo creation).

## Verification

- `yarn test` in admin_site: the ported grid-model, history, groupHorizontal, and projection test
  suites (these are direct translations of the C# `[TestClass]` tests, which encode the known bug
  classes: redo-dispatch gaps, replay-on-different-data, count-column side effects).
- `python localdev.py` + `yarn start`: log in against localhost, run a query on CloudDataPage,
  exercise every status-strip op + undo/redo + SaveView across a re-run, copy into Excel/Sheets.
- Scope: synthetic generator page-load traces (sine/noise/step at 1e6 samples) + a real session's
  `playback_frames`; check hover values against known sample values, zoom/pan clamping, YT
  alignment of two traces via MatchUnixTimes.
- Perf sanity: Chrome performance trace while wheel-zooming the 1M-sample trace - projection
  recompute only on zoom change, paint under ~16ms at steady state.

## Non-goals / guardrails

- No MUI adoption, no chart library - the scope is the chart.
- No changes to app2-cloud backend or deploy plumbing (admin_site stays undeployed/local).
- `src/sehenswerte/**` never imports admin_site code, and carries MIT license + README from the
  first commit, ready for extraction.
