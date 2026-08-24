# sehenswerte react

A React/TypeScript port of the sehenswerte flagship controls, living alongside the C# originals
so a change to one is a visible reminder to update the other:

* **Scope** - the oscilloscope control: stacked trace groups, min/max envelope decimation for
  large sample arrays, sample/rate/wall-clock time axes, zoom/pan, hover readout, features
  (annotations), rendered on canvas.
* **DataGrid** - the data mining grid: schema-free row/column data, hide-based filtering,
  cumulative multi-key natural sort, and an undo history that doubles as a portable,
  replayable view recipe.
* **Log** - the cross-thread log viewer's browser counterpart: priority filtering, regex
  search, pause/buffer, CSV export.

The port aims to keep the look, feel, and interaction of the WinForms originals. Library code
has no dependencies beyond React and TanStack Virtual, and imports nothing from any host
application. Hosts import from `react/index.ts` only.

Tests are jest ports of the C# MSTest suites and run under the host app's `yarn test`
(`run-tests.ps1` finds the host and drives it, the counterpart to `../run-tests.sh`).

## Seeing it run

`example/` is a standalone vite app - the react counterpart to the C# `example/` project -
that mounts `DevPlayground` over synthetic data. No host app, no backend, no config:

```
cd example
npm install
npm run dev          # http://localhost:5174
```

A tab each for DataGrid, Log and Scope, with buttons for the fixtures worth poking at:
100-row/5k/100k grids, a 1M-sample trace, affine and wall-clock-time trace groups, a
2000-entry log flood. This is where interaction feel gets judged - header drag/resize/sort,
scope zoom/pan/hover, log scrolling - which the jest suites cannot cover.

`npm run build` there is also the quickest proof the whole import graph (CSS included)
resolves outside the host's toolchain.

# Port status

Early development. DataGrid and Log are done in core; Scope has its core done with interactions
outstanding. The lists below are the gap against the C# originals.

Bucketing:

- **Not ported** - the C# has it, this does not.
- **Partial** - present but reduced.
- **Divergent** - ported, but behaves differently from the C#. These are defects, not scope
  decisions.
- **Not in the C# either** - listed at the end so nobody re-derives that it was never there.

The port plan, milestones and the per-C#-commit reconciliation ledger live in
`sehenswerte-react.md`. That file currently sits at the **workspace root** alongside the repo
checkouts (`<workspace>/sehenswerte-react.md`), not inside this repo - it is untracked, so it
does not travel with a clone. C# base reconciled: `dd46940`.

---

## Log (`LogControl.cs` + `CsvLog.cs` -> `log/`)

Ported: entry model, `extendPath`/`demoteToDebug` sink decorators, display-line composition,
multi-line split into rows sharing one entry, priority threshold + regex filter, ring/itemLimit,
pause, follow, mouse-over toggle, status overlay, context menu (Clear/Copy/Save CSV), tooltips,
priority colouring, the 11-column CSV projection.

### Not ported

- [ ] **Continuous file logging - the whole of `CsvLog.cs`.** `LogFolder`, the always-on
      streaming writer, per-process filename (`yyyyMMddHHmmsszzz` plus a `.N` collision index),
      `CompressLogFile` choosing a live `GZipStream` vs plaintext with `AutoFlush`, the header
      row and "Logging started" seed entry, `Close("End of logfile")`, `Flush()`, and the 1s
      timer that drains to disk regardless of repaint. Consequence: every entry reaches the C#
      file including ones below the filter threshold and ones later evicted by `ItemLimit`,
      whereas the browser CSV can only ever contain what the in-memory ring still holds. A
      crash or tab close leaves no record at all
- [ ] Horizontal scrolling of long lines (`LogControl.HorizontalBar` + `PaintDrawEntries`). Rows
      here are `white-space: pre; overflow: hidden`, so a long line is clipped with no way to
      read the tail except the hover tooltip
- [ ] Imperative scroll API - `LogControl.ScrollIndex` get/set, and mouse-wheel honouring the
      system scroll-lines setting. `LogPanel` exposes no handle at all, inconsistent with
      `DataGridHandle`
- [ ] Host-settable appearance: `TextBackColor`, `BackColor`/`DefaultColorBack`, per-entry
      `LineColor`/`BackColor`, `LineHeight` derived from the font, `WarningFont`. Colour here
      comes solely from a priority CSS class, and `ROW_HEIGHT = 17` is hardcoded against
      `font-size: 12px`, so changing the font breaks virtualized row alignment
- [ ] Add-batching. The C# coalesces via an `m_NewItems` flag plus the 1s timer; `addNow` calls
      `notify()` per entry, and once the ring is full `trim()` runs an O(itemLimit) filter on
      every add. The 2000-entry flood fixture is where this bites
- [ ] `threadId` population, and `ComputerID`/`ProcessID` in the CSV (hardcoded empty)
- [ ] Caller-attribute capture. `[CallerMemberName]`/`[CallerFilePath]`/`[CallerLineNumber]`
      have no TS equivalent, so Source is host-supplied and in practice empty
- [ ] `byte[]` binary payloads with a hex helper - `binaryHex` is a pre-rendered string, so every
      host reimplements the hex conversion
- [ ] Sub-millisecond timestamps. `HighResTimer.StaticNow` gives 7 fractional digits plus a local
      UTC offset; `Date.now()` + `toISOString()` gives 3 digits and `Z`. Ordering within a
      millisecond and the originating timezone are both lost in the export
- [ ] Menu mnemonics and keyboard handling (`&Clear`, `C&opy to clipboard`); no Escape-to-close
      or arrow-key navigation on the menu
- [ ] Clipboard error reporting - `navigator.clipboard.writeText` is called with no catch, so a
      denied permission fails silently and produces an unhandled rejection

### Partial

- [ ] Status overlay is drawn top-right; the C# `PaintWarning` draws it bottom-right
- [ ] Item-limit trimming: the C# caps the raw and filtered queues independently, so matches
      outlive the eviction of their raw rows. `trim()` here deletes trimmed rows from
      `filteredRows`, so under a flood with a narrow filter the filtered view can empty out
      between filter edits
- [ ] Follow/auto-scroll: `onScroll` flips the checkbox itself, so the control state churns as
      the user scrolls back through history. In the C# the checkbox stays checked and following
      silently resumes at the end
- [ ] Tooltip: no `ReshowDelay` (always the full 500ms), and no suppression on empty lines
- [ ] Clear also discards `pendingWhilePaused`; the C# leaves the input queue intact so it drains
      into the cleared view

### Divergent

- [ ] **Regex match target is wrong.** `LogControl.MatchesFilter` matches
      `Original.DisplayedLine` - the whole entry - so if any part of a multi-line entry matches,
      every row of it stays visible (filter a stack trace by its exception type and you keep all
      the frames). `LogModel.matches` tests `row.line`, the individual split line, so multi-line
      entries are shredded down to the matching lines. The comment above it claims C# parity
- [ ] **`index.ts` exports the wrong `LogPriority`.** Line 16 re-exports the grid's 4-value type
      (`debug|info|warn|error`); the log's 5-value type in `log/LogEntry.ts` including
      `exception` is never exported, so a host writing `"exception"` against the public type
      fails to compile
- [ ] `toToolTip` uses the raw full source path; the C# uses `Path.GetFileName`. `csvExport.ts`
      strips it correctly, so the port disagrees with itself, and `LogModel.test.ts:65` pins the
      wrong behaviour
- [ ] `LogModel.sink` is a getter returning a fresh closure per access, so it cannot be
      unsubscribed by identity and is an unstable hook dependency. The canonical C# usage needs
      exactly that identity (`scope.OnLog += control.Add` / `-= control.Add`)

Not exported but arguably should be: `toToolTip`, `CSV_HEADER`, `entryToCsvRow`,
`PRIORITY_DISPLAY`.

---

## DataGrid (`DataGridControl.cs` + `DataGridBoundData.cs` -> `grid/`)

Ported: the full row/column model with stable index and resort index, all 10 hide ops, the
17-op undo history and its replay/`saveView` semantics, cumulative multi-key natural sort,
column resize/reorder/autofit, the column picker with collapse-not-hide, cell colours,
highlights, null-italic, masking, diff spans, TSV/HTML/CSV clipboard build, all 18 status-strip
buttons with their C# visibility rules and Alt mnemonics, font zoom, and row virtualization
(which the C# does not have).

### Not ported

Import / export:

- [ ] JSON load (`LoadJson` + `JsonElementToString`) - array-of-objects with a unioned key set,
      bare object to Key/Value columns, scalar to Value. The file input accepts CSV only
- [ ] JSON save (`BoundData.SaveToJson`), including `_2`/`_3` de-duplication of repeated headers
- [ ] Save-file dialog with a CSV/JSON filter; the download is a hardcoded `grid.csv`
- [ ] CF_HTML clipboard framing (`WrapHtml`) - the `Version:1.0/StartHTML:` header Excel wants.
      Declared deferred in `clipboard.ts`
- [ ] The built `text/csv` flavour is never actually placed on the clipboard

Host API:

- [ ] **`SelectionChanged` event.** There is no `onSelectionChanged` prop, so a host cannot react
      to selection - no detail pane, no dependent toolbar, no chart sync. Highest-impact
      omission: Flitescope's map linkage is driven by exactly this
- [ ] `ShowTooltipWindow`/`HideTooltipWindow` events. The render slot cannot suppress or
      side-effect on hover
- [ ] Settable `ToolTipShowMilliseconds`, `ToolTipPauseMilliseconds`, `ToolTipMaxLength`,
      `MaskString`, `NullForeColor`, `AllowUserToOrderColumns` - all module constants or CSS
      variables here
- [ ] Handle members: `Focus()`, `ScrollColumnIntoView(name)`, `MoveCursorToColumn(name)`,
      `ShowRowsOfColumn(col, value)`, `RowCount`, `GetRow`, `GetCell`, `ColumnName(index)`,
      `GetSelectedRowCount`, `CurrentCell`, `Clear()`
- [ ] Imperative `ApplyColumnColour(name, colour)`; only a static `columnColours` prop exists,
      so a host cannot tint a column in response to a click
- [ ] `CodeProfile` instrumentation around refilter/sort/GetColumn

Rendering:

- [ ] Multi-line / wrapped cell text, `CellStyle.Alignment` horizontal and vertical, and the
      "Cell content too large" fallback. Cells are `nowrap` + ellipsis, single line only
- [ ] Row-height autofit on row-divider double-click; there are no per-row heights at all
- [ ] `NumericGrid` mode (drives no-wrap and load-time typing). `fromDoubles` exists but the
      component has no numeric flag
- [ ] Column virtualization - every column of every visible row is in the DOM

Interaction:

- [ ] Arrow-key / Home / End / PageUp cell navigation
- [ ] `ProcessMnemonic` generality - the C# walks every enabled strip item; here an 8-entry map
      is hardcoded, so a new button silently gets no mnemonic
- [ ] Column picker: scroll the first matching header into view while typing
      (`HighlightMatchingHeaders(scrollIntoView: true)`). Matches are tinted but can be off
      screen
- [ ] Highlight: `ScrollFirstHighlightIntoView` - the preview colours cells you cannot see
- [ ] `CheckBoxListForm` picker behaviour: type-to-filter on the list itself, startsWith-first
      then contains ordering, All/None acting on visible items only, per-item substring
      highlighting
- [ ] Append-rows scroll preservation (`FirstDisplayedScrollingRowIndex` save/restore) - a
      streaming grid will jump
- [ ] Error surfacing. Every C# strip handler wraps in `ExceptionToMessagebox`; ops here are
      unguarded and a throw goes into the React tree with nothing shown

### Partial

- [ ] Regex prompt does not seed with the previous pattern (the C# persists `RegexInput` per
      control). It does add inline regex-error display, which the C# lacks
- [ ] Transpose is disabled above 200 visible rows. That limit exists in the C# only because of
      the `col0..col199` reflection binding, which this port deleted - the restriction is
      inherited spuriously and should go
- [ ] Status label format differs (`12/50 rows, 8 cols` vs the C# `12/50`), and there is no
      analogue of the C# 200-column warning (correctly, since there is no column cap here)
- [ ] `saveView`/`restoreView` are session-scoped on both sides: `SplitRecipe` is a live function
      in the C# and in `GridHistory.ts`, so a view recipe still cannot be persisted to disk or a
      URL. Making it data was the original design intent
- [ ] `onCellClick` fires on mousedown, not click; there is no separate mouse-down hook

### Divergent

- [ ] Ctrl+F with no selection filters column 0. `ShowByRegexStatus_Click` returns early when
      there is no current cell; `onKeyDown` opens the dialog unguarded and `currentColumnName()`
      falls back to `columnNames[0]`. Same class: Decimate and Unique have no `enabled`
      predicate here but return early on an empty selection in the C#
- [ ] Masked null/empty cells render `.....` instead of `null`/blank. `CellDisplayText` masks
      only when the cell is neither null nor empty; `renderCellText` checks `masked` first
- [ ] Tooltip truncation has no `...` marker
- [ ] Hide Above/Below anchors on `currentCellInfo.stableRow`; the C# uses
      `RowsWithSelection().FirstOrDefault()`, a different row when the selection was made
      bottom-up
- [ ] Diff spans render on selected cells; the C# skips diff painting when a cell is selected or
      masked
- [ ] Drop indicator during header drag is header-height; the C# draws it the full client height

---

## Scope (`SehensControl.cs` -> `scope/`)

Ported: the two-level TraceData/TraceView model, group stacking, ring append, affine and
wall-clock axes, the decimation pipeline (min/max envelope, nearest, interpolate, average,
dots), `projectLog`, group horizontal classification, axis formatting and partitions, canvas
paint with DPR and rAF coalescing, Ctrl+wheel zoom, Alt+wheel pan, hover crosshair, Ctrl+R auto
range, and a trace list with a regex filter.

### Not ported

Persistence and state (nothing at all exists):

- [ ] `SehensSave.cs` - the whole XML save/restore of layout, groups, per-view settings, skins
      and samples; `CurrentSaveVersion = 2` and `TranslateLegacyTraceXml` migration
- [ ] `BinarySave`/`BinaryTrace` - the `.sehens` binary state format
- [ ] `SehensSave.ApplyTo` exact-vs-fuzzy matching, i.e. re-applying a saved layout onto a
      different dataset
- [ ] `TraceNameHints.cs` - prefix/suffix stripping so per-file-decorated trace names still match
      saved state
- [ ] `SehensControl.Import(path)`
- [ ] Numbered layout presets on the trace list (left-click recall, right-click store)
- [ ] MRU history on the trace filter box

Import / export / screenshot (nothing at all exists):

- [ ] Export as SinglePng, MultiplePng (rtf), TraceNames, CsvVertical, TsvVertical, WAV, Matlab
      SSV, `.sehens`, `.xml`, Parquet (`data/ImportExport.cs`)
- [ ] Import CsvVertical, Csv.gz, Tsv, AVCodec audio, 8-bit binary as 8 traces, Saleae analysis
      as features, events-as-features text, `.sehens`, `.xml`, Parquet with its YT/Y load modes
- [ ] Export scope choice (SelectedDisplayedSamples / Selected / Visible / AllTracesBeforeZoom)
- [ ] Export and import via the clipboard (Ctrl+S/C/O/V)
- [ ] `ScreenshotToBitmap` / `ScreenshotToClipboard(w, h)` / `ScreenshotToRtf` at an arbitrary
      render size, and the separate high-res screenshot skin

Context menu and embedded toolbar (nothing at all exists):

- [ ] The `ui/ContextMenu.cs` framework itself - `ShowWhen` gating, `CallWhen` PerTrace/Once,
      `Sort`, hotkey codes
- [ ] Top level: time match source/target, auto range (all/time), recalculate traces, screenshot,
      hide controls, stop view updates, new trace (reference/copy/copy visible samples), combine
      to new YT trace, show selected samples in a data grid, group/ungroup, hide, close view, set
      samples to 0, play samples, zoom
- [ ] Submenus: Re-colour, Skin (incl. Edit Skin), Sort Traces, Display (rate-limit refresh,
      crosshair, vertical cursor, trace statistics, trace labels, hover statistics, hover value),
      Diagnostic (log window, paint statistics, paint benchmark), Generate (filter coefficients,
      window, noise, tone, sweep, sinc, test-trace matrices), Import/Export, Trace (settings,
      match vertical/horizontal, copy trigger, rename, close empty/flat/hidden, XY, FFT, FFT2D,
      spectral, auto shrink, hold zoom, PiP), Math, Trace Filter, Features
- [ ] The per-trace embedded toolbar painted on the trace (PiP, FFT/Spectral/FFT2D,
      LinV/LogV/dB10/dB20, LinH/LogH, paint-mode cycle, Range/Shrink, Phase, Hold Zoom, Trim,
      Trigger, Audio)
- [ ] Keyboard shortcuts R, X, Space, D, G, U, Delete, H, F2, A, S/C/O/V, Escape. Only Ctrl+R
      exists

Paint modes and painters:

- [ ] 7 of 14 modes: PeakHold, XYLine, XYPoints, XYCurve, XYZProjection, FFT2D, Spectral
- [ ] `TraceDataPeakHold.cs` rolling min/max accumulation
- [ ] `PaintXYTrace.cs` / `PaintXYZTrace.cs` and their per-axis zoom/pan (Ctrl gates X, Shift Y,
      Alt Z) and left-drag pan
- [ ] `Paint2dFFTTrace.cs` - the FFT painter with a Hz axis and Nyquist-derived extents
- [ ] `PaintPiPTrace.cs` - the picture-in-picture inset showing the unzoomed trace

Calculated traces, filters, FFT, triggers:

- [ ] All 25 `CalculatedTypes` including the PythonScript hook, plus `CalculatedSourceViews`,
      the parameter editors, `MathPhase` before/after zoom, and chained recalculation
- [ ] Per-view FFT magnitude/phase (`MathTypes`) and the FFT window choice
- [ ] FFT band-pass / low-pass filtering with editable corner frequencies
- [ ] The `src/core/filters` chain applied per trace (`TraceFilter` / `FilterChoice`)
- [ ] Triggering entirely: `TriggerModes`, `TriggerValue`, `TriggerView`,
      `PreTriggerSampleCount`, `TriggerPhase`, copy trigger, triggered slice

Audio:

- [ ] `TraceViewAudioPlayback.cs` - play a trace or the selected region, loop, pre/post filter,
      and the live playback-position cursor

Selection, cursors, annotation rendering:

- [ ] Right-drag wipe select of a sample region, and everything gated on it (highlight, set
      samples to 0, zoom, play samples, copy visible samples, send to grid)
- [ ] Canvas click / Ctrl / Shift range selection, the painted selection highlight, select-all
- [ ] Cursor mode choice (Pointer / CrossHair / VerticalLine)
- [ ] Hover statistics header, multi-label stacking and clamping, suppression outside drawn data,
      over NaN gaps, and before YT data starts
- [ ] **No feature is ever drawn.** `PaintFeatures` covers Text (angle, vertical anchor and
      justify modes), GutterText, Line spanning the GroupArea, Highlight bands as group
      background behind traces, LeftHandle/RightHandle/TriggerHandle glyphs, and hit boxes
- [ ] Click zones, embedded text and embedded handles - no on-canvas hotspots at all

Geometry and axes:

- [ ] Trim / slide / pad per view (`ViewOverrideEnabled`, `ViewLengthOverride`,
      `ViewOffsetOverride`, `PadLeftWithFirstValue`, `PadRightWithLastValue`) and their drag
      handles
- [ ] Per-group height (`HeightFactor` 1.0-2.5), bottom-gutter drag resize, the vertical
      scrollbar and a stack taller than the control
- [ ] `AutoReduceRange` (auto shrink), `HoldPanZoom`, per-view zoom and pan
- [ ] Log and dB vertical modes (Auto/Off/Log/dB10/dB20, stored vs effective) and log horizontal
- [ ] Vertical axis on the left; `AxisLines` None/BottomLeft/All
- [ ] Axis titles - `axisTitleBottom`/`axisTitleLeft` are stored and notify, but never drawn
- [ ] On-canvas per-trace statistics block (embedded or vertical gutter)
- [ ] Label placement choice; the left, top and right gutters do not exist
- [ ] Per-role fonts (axis title, axis text, legend, feature, warning, stats, hover)
- [ ] Canned skins (Clean / ScreenShot / Custom) and Edit Skin

Control-level API:

- [ ] `DuplicateTraceView` / `DuplicateTraceData`, `EnsureView`, `AddView`, `RemoveView`,
      `RenameView`, `ViewByTrace`, `GroupWithView` declarative grouping, `SortViewGroups`,
      `OrderViewGroups`, `CloseVisible`/`CloseFlatTraces`/`CloseEmptyTraces`/
      `CloseInvisibleTraces`
- [ ] `BeginUpdate`/`EndUpdate`, `StopUpdates` (frozen viewed-data clone plus a "stopped"
      overlay)
- [ ] `UpdateBitwise(name, byte[], sps)` - a byte stream into 8 bit traces
- [ ] `SetSelectedSamples`, `InputValuesAllIdentical`, viewer registration, `TraceData.Close`
- [ ] `AutoRangeTimeAll`, `ReprocessMathAfterZoom`
- [ ] Zoom/pan scrollbars with the C# logarithmic mapping (`ZoomExp = 200`), horizontal-wheel
      pan, plain-wheel vertical scroll, Shift+wheel vertical zoom
- [ ] Double-click to open the per-trace and per-group settings editors
- [ ] Trace list buttons: Save, Load, Auto All, Sort, Menu

### Partial

- [ ] **Log axes: full math, zero wiring.** `projectLog`, the log-H value/fraction/pixel maps and
      `getLogPartitions` are all implemented and exported, but `TraceView` has no log flag,
      `groupMember()` never sets `log`, and the painter never passes a log axis. Dead code until
      wired
- [ ] **Features: data ported, never painted.** The `TraceFeature` type, its seven-type sort
      order, `addFeature` and `setInputFeatures` all exist; `ScopePainter` draws none of them.
      When it lands it must honour C# `dd46940`: Highlight bands are group-level background,
      painted for every member before any waveform, and a Line feature spans the whole GroupArea
      (clipped to it), not just the ProjectionArea
- [ ] **Statistics: computed, never shown.** `computeStatistics` is implemented and exported with
      nothing rendering it
- [ ] Skin has ~20 fields against the C# ~50 plus 7 font objects, and `wipeSelectFill`,
      `hoverLabelBackground`, `foregroundColour`, `gutterColour`, `traceLineWidth` and
      `defaultTraceColour` are declared but never read
- [ ] The vertical axis is taken from `group[0]` only - per-member ranges and units are ignored,
      and there is no `UpdateLinkedRanges`
- [ ] Group layout is an equal vertical split; no height factors, no drag resize, no vertical
      scrolling
- [ ] `moveGroup` exists on the model with no UI to drive it
- [ ] Selection is a `Set<TraceView>` toggled from the list only, and the handler fakes a notify
      via `setZoomPan(zoom, pan)`
- [ ] Hover shows one value line per view plus the horizontal value; no stats, no gap or
      out-of-range suppression, no stacking or clamping
- [ ] Paint mode is initialised to `polygonDigital` and nothing in `Scope.tsx` ever changes it;
      no line width, dash or circle styles

### Divergent

- [ ] **No projection cache.** `projection.ts` says callers must key on (data generation, first,
      count, pixelWidth, log-H window); `ScopePainter` calls `projectCurves` fresh for every
      trace on every repaint. The C# gates this behind `SnapshotReprojectionRequired` /
      `DrawnProjection1/2`. The "1M-sample paint under 16ms" target is unverified against this
- [ ] **`ScopeModel.autoRangeAll` auto-ranges the wrong window for two of three group kinds.**
      Both branches of its `isYTTrace` ternary are byte-identical and it always passes
      `"stretch"`, so valueAlign and YT groups range over a window they are not drawn on
- [ ] `ScopeModel.matchUnixTimes(names)` is a stub that just calls `groupViews` - no wall-clock
      alignment, and no time match source/target
- [ ] Zoom uses a linear 0.8/1.25 factor rather than the C# logarithmic bar mapping

Two deliberate improvements over the C#, kept: the `appendRing` rate guard is fixed (the C#
tests the current rate, not the incoming one, so a rate-less first append stores NaN), and the
YT `appendRing` overload is real (the C# throws `NotImplementedException`).

---

## Not in the C# either

Do not file these as port gaps:

- Cell editing in the grid. `BoundData` declares `AllowEdit => false`; mutation is programmatic
  (`SetCell`), which is ported. Flitescope's AutoEdit lives in its own tab, not in the control
- Column pinning or freeze (the sticky row header here is marginally more than the C# has)
- Grouping, aggregation and totals beyond the `<column> count` column that `HideNotFirstUnique`
  inserts, which is ported
- Printing, in either control
- Grid context menus - host-owned on both sides
- Log: word wrap, columns or column visibility in the view, counters UI, match highlighting, a
  plain-text vs regex toggle, size or age based rotation, per-thread filtering, level-specific
  sinks
- Dual Y axes on the scope

---

## License

Same as the parent repository - see the license section in the top-level README.md.