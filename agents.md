# agents.md — Sehenswerte Architecture & Agent Instructions

## Agent Instruction

**Always update this file** when you learn something architectural -- even if not asked. If you add a class, change a major data flow, rename a subsystem, or discover how something works, record it here. Keep entries concise and accurate; remove stale entries when things change.

**Use parallel subagents aggressively.** For any task involving multiple files, multiple searches, or independent exploration, dispatch subagents in parallel by issuing multiple Agent tool calls in a single message. Sequential work should be reserved for steps that genuinely depend on earlier results. Examples that should be parallelised: surveying related code under `src/core/`, `src/sehens/`, and `example/`; reading several candidate files; running independent greps.

---

## Project Overview

**Sehenswerte** — C# .NET 6 WinForms library ("worth seeing" in German). Provides a reusable core of signal processing utilities, visual controls, and the `Sehens` oscilloscope control for high-speed real-time data acquisition and visualization.

- Solution root: `sehenswerte/`
- Core library: `src/core/Core.csproj` (namespace not fixed -- utilities are standalone classes)
- Sehens control: `src/sehens/Sehens.csproj`
- Example app: `example/Use.csproj`

---

## Source Layout

```
src/
  core/                   — standalone utilities (no UI dependency)
    comms/                — serial port, communication queue
    controls/             — WinForms helper controls and AutoEditor
    filters/              — signal filter chain (FIR, IIR, FFT, Kalman, NLMS, RLS, …)
    generators/           — tone, noise, waveform generators
    maths/                — FFT, interpolation, statistics, PID, LQR, rolling averages
    files/                — CSV load/save, CsvLog, RIFF audio read/write, AudioReader, ParquetNumeric (Parquet.Net wrapper for numeric columns)
    AES.cs                — AES encryption helper
    CodeProfile.cs        — lightweight performance profiler
    Compression.cs        — data compression utilities
    EnumExtension.cs      — enum helpers
    HighResTimer.cs       — high-resolution timer
    ListExtensions.cs     — IList/IEnumerable extensions
    NaturalStringCompare.cs
    ObjectExtension.cs
    Process.cs            — process launching/management
    Reflection.cs         — object dump / reflection helpers
    Ring.cs               — ring buffer
    SqlQuery.cs           — lightweight SQL query helper
    StateMachine.cs
    StreamExtensions.cs
    StringExtensions.cs
    WindowsRegistry.cs
    XmlSerialise.cs       — generic XML serialization helpers

  sehens/                 — Sehens oscilloscope control
    SehensControl.cs      — main oscilloscope WinForms control
    SehensSave.cs         — state save/load
    data/                 — TraceData, TraceFeature, import/export, peak hold
    paint/                — per-trace painters (2D, FFT, PiP, XY)
    ui/                   — paint box, context menus, trace list, skin, click zones
```

---

## Key Classes

| Class | Location | Role |
|-------|----------|------|
| `SehensControl` | `src/sehens/SehensControl.cs` | Main oscilloscope WinForms control — embed in host forms |
| `TraceData` | `src/sehens/data/TraceData.cs` | Holds sample data and metadata for one trace channel |
| `FilterInput` | `src/core/filters/FilterInput.cs` | Entry point to the filter chain; feeds data into connected filters |
| `FilterOutput` | `src/core/filters/FilterOutput.cs` | End of filter chain; provides resampled output to consumers |
| `FftFilter` | `src/core/filters/FftFilter.cs` | FFT filter stage |
| `FftAnalyse` | `src/core/maths/FftAnalyse.cs` | FFT analysis math |
| `ToneGenerator` | `src/core/generators/ToneGenerator.cs` | Configurable sine/tone generator |
| `WaveformGenerator` | `src/core/generators/WaveformGenerator.cs` | Multi-waveform generator |
| `AutoEditor` | `src/core/controls/AutoEditor.cs` | Reflection-based binder between controls and a decorated data object |
| `AutoEditorControl` | `src/core/controls/AutoEditorControl.cs` | UserControl wrapper — call `Generate(sourceData)` to build the settings panel |
| `AutoEditorBase` | `src/core/controls/AutoEditorBase.cs` | Base class for auto-editable settings objects |
| `CsvLog` | `src/core/files/CsvLog.cs` | Structured append-only CSV logger with path-based subsystem tagging |
| `SerialPort` | `src/core/comms/SerialPort.cs` | Serial port wrapper |
| `Ring<T>` | `src/core/Ring.cs` | Generic ring/circular buffer |
| `StatsFilter` | `src/core/filters/StatsFilter.cs` | Rolling statistics (mean, variance, RMS) filter |
| `DataGridControl` | `src/core/controls/DataGridControl.cs` | Filterable, sortable data grid with undo/replay stack and save/restore view state |
| `BoundData` | `src/core/controls/DataGridBoundData.cs` | `IBindingList` backing store for `DataGridControl`; owns `UnfilteredData`, `FilteredData`, `SortKeys`, `UndoList` |
| `DataGridControlHistory` | `src/core/controls/DataGridControlHistory.cs` | Snapshot history for `DataGridControl.SaveView` / `RestoreView` |
| `PaintTraceBase` | `src/sehens/paint/PaintTraceBase.cs` | Base painter -- horizontal/vertical axis rendering, `ProjectLog`, partition helpers |
| `Paint2dTrace` | `src/sehens/paint/Paint2dTrace.cs` | 2D line/polygon painter; owns the `Project2dCurves` resample/decimate pipeline |

---

## Painter Pipeline

Each `TraceView` has a `Painter` (one of `Paint2dTrace`, `Paint2dFFTTrace`, `PaintXYTrace`, `PaintPiPTrace`) chosen from `PaintMode`. All derive from `PaintTraceBase`.

- `PaintProjection` runs per repaint with a `TraceGroupDisplay` (geometry + axis extents).
- `Project2dCurves` is the expensive resample/decimate step. It runs only when `SnapshotReprojectionRequired` is true and caches into `DrawnProjection1` / `DrawnProjection2` / `DrawnPolygon`.
- To force a recompute, call `TraceView.RecalculateProjectionRequired()`. Any property that changes projection geometry (zoom, math type, log axes, paint mode) must call this in its setter.
- `TraceGroupDisplay.LeftSampleNumberValue` / `RightSampleNumberValue` carry the X-axis values for the current view (Hz for FFT, seconds for time-with-rate, sample number otherwise). They come from `TraceView.DrawnExtents()` -- do not recompute from Nyquist or sample rate yourself.

### Axis log scaling

`PaintTraceBase.ProjectLog(maxInput, input, out newMax, out output, staves=2)` is the canonical log mapping. It compresses `staves` decades (default 2); values below `maxInput / 10^staves` clamp to 0. The inverse is `input = maxInput * 10^(output - newMax)`.

`TraceView.LogVertical` is a 4-state enum `LogVerticalMode { Off, Log, dB10, dB20 }`:

- `Off` -- linear values, linear pixel mapping.
- `Log` -- linear values, **pixel-log** Y mapping via `ProjectLog`. Use case: linear-magnitude FFT where peaks span many orders of magnitude. Painters check this via `view.IsLogY`.
- `dB10` / `dB20` -- values converted to `10*log10(v)` / `20*log10(v)` inside `ExecuteFft`; linear pixel mapping. The " dB" axis label suffix is gated by `view.IsLogarithmicY` (which means "value-domain dB", not pixel-log).

`TraceView.LogHorizontal` is a 2-state enum `LogHorizontalMode { Off, Log }`. Painters check this via `view.IsLogX`.

`MathTypes` only describes the math transform (`Normal`, `FFTMagnitude`, `FFTPhase`). dB conversion is orthogonal -- entirely driven by `LogVertical`. The legacy `FFT10Log10` / `FFT20Log10` values are gone; `SehensSave.View.TranslateLegacyTraceXml` rewrites old saved files into the new shape (`FFTMagnitude` + `LogVertical=dB10/dB20`, and the old `True/False` bool serialisations of `LogVertical` / `LogHorizontal` into `Log`/`Off`).

The inset "V" and "H" buttons next to "FFT" (in `ContextMenus.AddTraceEmbeddedMenu`) cycle these enums per trace. The "FFT" button auto-sets `LogVertical = dB10` when entering FFT only if the user has not already picked a non-`Off` vertical mode.

### Painter / mouse mapping invariant

`TraceView.Measure(MouseEventArgs)` converts mouse X back to a sample index for hover labels. Its X-axis remap MUST match whatever transform the painter applied. If you add a non-linear X projection in a painter, mirror its inverse in `Measure` or hover labels will report the wrong frequency/time.

### Axis painting

`PaintTraceBase.PaintHorizontalAxis` already dispatches linear vs log (`PaintGutterBottomPartition` / `PaintGutterBottomPartitionLog`) and includes label-overlap skip logic. Painter subclasses should call `base.PaintHorizontalAxis(...)` rather than re-implementing tick layout.

---

## Filter Chain Pattern

Filters implement `IFilter` and are chained producer → consumer:

```
AudioSource → FilterInput → [FirFilter / IirFilter / FftFilter / …] → FilterOutput → SehensControl trace
```

- `FilterInput` is the source adapter; it accepts raw sample arrays.
- Intermediate filters transform or analyse the signal.
- `FilterOutput` is the sink adapter; consumers poll or subscribe to get processed samples. Also used for display-rate resampling when feeding a `SehensControl`.

---

## AutoEditor Convention

Settings objects inherit `AutoEditorBase`. Decorate fields/properties with attributes to control rendering:

| Attribute | Effect |
|-----------|--------|
| `[AutoEditor.DisplayOrder(n, groupName?)]` | Sort order; items sharing `(int)n` render under the same group header |
| `[AutoEditor.DisplayName("...")]` | Override the label (default is pretty-printed field name) |
| `[AutoEditor.Values(new[]{...})]` / `Values(typeof(Enum))` / `Values(typeof(IValuesAttrInterface))` | Render as a `ComboBox` with the given list |
| `[AutoEditor.Range(min, max, step)]` | On a numeric field, adds `-`/`+` kick buttons that nudge by `step`, clamped to `[min, max]` |
| `[AutoEditor.Hidden]` | Skip rendering |
| `[AutoEditor.Disabled]` | Render greyed/disabled (`Enabled=false`); see the `ReadOnly` property for a legible viewer |
| `[AutoEditor.Password]` | Mask the TextBox content |
| `[AutoEditor.PushButton("caption")]` | On a `bool` or delegate field, render as a clickable Button |
| `[AutoEditor.SubEditor]` | Render a `...` button that opens an `AutoEditorForm` for the nested object |
| `[AutoEditor.InlineClass]` | Flatten a nested class's fields directly into the parent panel at the host field's `[DisplayOrder]` slot. Child rows keep their own ordering/grouping/display names inside that slot. Alternative to `[SubEditor]` -- no button, no popup. |
| `[AutoEditor.ArrayEditor(mode, itemLabelFormat?, buttonCaption?)]` | Editor for an `IList`/array field. `Inline` mode emits one row per element directly in the parent panel; `SubForm` mode emits one button that opens a popup. Scalar-typed elements render as their normal scalar control; class-typed elements render as a button-per-element opening a per-element subeditor. Default `itemLabelFormat` is `"[{0}]"`. Length changes between `UpdateControls` invocations trigger a panel rebuild. |

Host a panel by adding an `AutoEditorControl` to your form and calling `Generate(sourceData)`.  
`AutoEditorBase` exposes an `OnChanged` callback and an `UpdateControls` action for round-tripping between the UI and model.

`AutoEditorControl` per-instance options (set BEFORE `Generate`; changing later has no effect until the next `Generate`):

- `CommitMode` (`AutoEditor.CommitMode`): `Immediate` (default) commits text fields on every keystroke (`TextChanged`); `OnValidated` commits text fields on focus-leave (`Validated`) or Enter. CheckBox/RadioButton/ComboBox selections always commit immediately (discrete gestures, no partial state). Limitations: do not host an `OnValidated` panel inside `AutoEditorForm` (the form's KeyPreview Enter fires OK before the control-level commit); a value still being typed when the form closes is not committed; `[Range]` kick buttons commit on the NEXT focus loss, not the click.
- `ReadOnly` (bool): legible non-editable viewer, distinct from `[Disabled]` greying. TextBoxes get `TextBox.ReadOnly=true` (selectable/copyable), `[Values]`/enum rows render as read-only TextBoxes instead of ComboBoxes, bool CheckBoxes get `AutoCheck=false`, buttons are disabled, and NO commit wiring is attached at all.
- `UpdateControls()` (public method): push current `SourceData` values into the generated controls. The refresh path for `SourceData` objects that are NOT `AutoEditorBase` (e.g. protocol packet objects mutated in place by a read thread). Safe to call from a non-UI thread (marshals via `BeginInvoke` once the handle exists; runs synchronously on the calling thread before the handle is created).

---

## SehensControl Usage

- Embed an instance in a form (usually via the Designer); the host owns the control.
- Feed traces by writing samples through a `FilterOutput`, or by directly calling into `TraceData`.
- `Scope.Import(path)` loads a previously saved state or trace file — hosts typically wire it to drag-drop or a command-line argument.
- Right-click context menu is built from `ScopeContextMenu`; per-trace menus are in `src/sehens/ui/ContextMenus.cs`.

---

## Trace Annotations (TraceFeature)

`TraceFeature` ([src/sehens/data/TraceFeature.cs](src/sehens/data/TraceFeature.cs)) is the canonical way to draw text, lines, highlights, and handles on a trace. Use this any time you want to put a label, vertical line, or shaded span at a specific sample on an existing trace -- do NOT invent a separate "label trace" or scope name encoding.

Types (`TraceFeature.Feature`): `Text`, `GutterText`, `Line`, `Highlight`, `LeftHandle`, `RightHandle`, `TriggerHandle`.

Per-feature fields: `SampleNumber` (anchor), `RightSampleNumber` (for spans), `UnixTime` / `RightUnixTime` (for YT traces), `Text`, `Colour` (`null` = skin default), `Angle` (`-90` = vertical bottom-to-top, the default).

Vertical placement for `Text` features:
- `VerticalAnchor = Centre` (default): pixel-space centre of the plot rectangle. Ignores `VerticalPosition`, value range, and Y scaling. Reproduces the legacy mid-trace placement.
- `VerticalAnchor = Y`: value-space. `VerticalPosition` is a literal Y value, projected through the painter's linear/log Y mapping.
- `VerticalAnchor = Sample`: value-space. The sample value at `SampleNumber`, projected through the same Y mapping, so the label rides the trace.
- `VerticalJustify`: `Top` / `Middle` (default) / `Bottom` -- where the text bbox sits relative to the anchor Y. For rotated text (e.g. `Angle = -90`), `Top` / `Bottom` refer to the rotated bbox's screen-space edges, not to the first/last character of the string.
- The painter clamps the bbox into the plot rectangle so labels near the edges aren't clipped.

Features live on `TraceData`, not `TraceView`. `scope[name]` returns the `TraceData` -- so:

```csharp
scope["foo"].AddFeature(sampleNumber, "label");          // append one text feature
scope["foo"].AddFeature(new TraceFeature { ... });        // append arbitrary feature
scope["foo"].InputFeatures = listOfFeatures;              // replace (clears existing) + auto-sort
```

`InputFeatures = ...` is the right choice when a feature set is derived fresh each `Run()` -- it clears and re-sorts in one shot so re-runs are idempotent.

Visibility is gated by `Scope.ShowTraceFeatures` (toggle in the right-click context menu). If features don't appear, check that flag before debugging anything else.

---

## DataGridControl

A `DataGridView` wrapper with a status-strip toolbar offering filter/sort operations.
Data lives in `BoundData` (implements `IBindingList`):
- `UnfilteredData` - all rows in original order. Each `BoundDataRow.Index` is the
  stable identity used everywhere instead of grid position.
- `FilteredData` - currently visible/sorted rows; what the grid shows.
- `m_History` / `m_RedoStack` - `DataGridControlHistory` lists of
  `Snapshot` view states (undo and redo stacks).

### Column mutations

Limit: column display is bound via the hardcoded `col0..col99` accessors on
`BoundDataRow`, which bind via reflection to `DataPropertyName = "col{N}"`. This
caps the grid at 100 displayable columns; columns beyond index 99 will not render
values. Extend that `col0..colN` accessor block if a use case needs more.

Replacing the hardcoded accessors with `ITypedList` on `BoundData` plus a dynamic
`IndexedColumnDescriptor` was tried and reverted: sorting on a newly-added column
(via `AddColumns`/`InsertColumns`) blanked only that column's cells after the sort,
while other columns stayed intact. Root cause was not fully pinned - suspected a
stale `CurrentSortProperty` descriptor surviving the `DataSource` cycle, or `::`
in the column `Name` confusing WinForms cell rendering. The static reflection 
binding sorts correctly across `AddColumns`; the dynamic approach did not. If you
must revisit it, first reproduce and pin that sort-blank bug in a focused test.

### SaveView / RestoreView

`grid.SaveView()` returns a `DataGridControlHistory` snapshot; `grid.RestoreView(view)`
replays it.

### Undo / redo (the snapshot triad)

Every undoable/redoable op needs three things in lockstep, or it silently falls out
of the history (the "hide unselected redo doesn't work" class of bug):
1. An `Operation` enum value in `DataGridControlHistory.Snapshot.Operation`.
2. A `PushSnapshot(...)` call at the start of the public method - this captures the
   pre-op visible set, which is what `Undo()`'s `ApplyVisible` restores.
3. A `case` in `DispatchAction` - `Redo()` and `RestoreBoundState` re-execute the op
   by re-calling the public method through here. No case means nothing to replay.
Undo restores the captured pre-op view; redo re-runs the op. Selection-based hides
(`HideRows`, `HideRowsOtherThan`) store stable row `Index` values, so they replay
only against the same data (fine for in-session undo/redo and SaveView/RestoreView;
best-effort, bounds-guarded, on a different dataset). Data-driven hides
(`HideRowsMatching`, anchors) replay meaningfully on different data.

### Column reorder and horizontal scroll

`RebuildGridColumns` resets `grid.DataSource`, which snaps the horizontal scroll to 0.
A reorder (`DoMove`) must not move the viewport, so it captures
`HorizontalScrollingOffset` and restores it via `RestoreHorizontalScroll` - deferred
through `BeginInvoke` because the `DataSource` reset and the `ListChanged.Reset` both
re-zero it before layout settles. Column drag is hand-rolled (the WinForms
`AllowUserToOrderColumns` is off); a `DragScrollTimer` auto-scrolls when the drag mouse
enters the left/right edge zone, gated on `ColumnsOverflowViewport`.

---

## Native Dependencies

- **FFTW** (`x86/`, `x64/`, `arm64/`) — native FFT library; see `COPYING.FFTW` / `README.FFTW`
- **FFmpeg** headers (`avcodec.h`, `avformat.h`, etc.) — referenced by `AudioReader`

---

## Conventions

- Core utilities have no dependency on the Sehens control; keep it that way.
- Filters are stateful objects, one instance per channel/pipeline, not shared.
- `CsvLog` paths use `/`-separated extension segments to tag log subsystems.
- XML serialization uses `XmlSerialise` helpers, not `JsonSerializer`.
- The example app (`example/`) is the canonical integration test, keep it compiling.
- ASCII only in source and docs. No em-dashes, en-dashes, curly quotes, arrows, checkmarks, or other non-ASCII punctuation. Use `-`, `--`, `->`, straight quotes, plain words.

### Coding style

Follow C# standard guidelines, with these specific rules:
- Use the prefix `m_` for module-level variables, excluding simple classes where
  it's not necessary
- Use leading capital letters for property and field names
- Try for one return statement in functions, except for first-in checks
- Avoid modifying parameter variables unless necessary for the caller
- Use exception handling for exceptional situations rather than normal cases
- Use unit tests to verify correctness and behaviour when applicable. Tests typically 
  live in the same source file as the code they test, in a `[TestClass]` with
  `[TestMethod]` members from `Microsoft.VisualStudio.TestTools.UnitTesting`. Run
  with `dotnet test src/core/Core.csproj`
- Name loop variables `loop`, not `i`
- Always use braces for if/else/foreach/while/try/finally bodies, even single-line
  ones. Exception: guard clauses that immediately return/continue/break may stay
  on one line without braces: `if (!foo) return;`. This applies to lambdas too --
  `() => { foo(); }` must be expanded to multi-line. For all other cases, put `{`
  on the next line (Allman style)
- Do not use `using static` - qualify static class members explicitly
- Large classes are split into partial classes for clarity (e.g. `DataGridControl`
  / `DataGridBoundData`)
- Forms/controls use `AutoScaleMode.Font` - do not change
- Keep comments short and pithy. They should describe non-obvious behaviour,
  not obvious code (e.g. avoid `int a = 5; // set a to 5`)
