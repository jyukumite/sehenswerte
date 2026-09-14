# agents.md - Sehenswerte Architecture & Agent Instructions

## Agent Instruction

**Always update this file** when you learn something architectural, even if not asked: a new class,
a changed data flow, a renamed subsystem, or how something works. Keep entries concise; remove
stale ones.

**This is a public MIT-licensed repository.** Never reference private host projects or their
internals (project names, tab/class names, business context) in code, comments, tests, docs or
commit messages. Describe behaviour generically.

**Use parallel subagents aggressively.** For multiple files, multiple searches or independent
exploration, issue several Agent calls in one message; go sequential only where a step depends on
an earlier result. Parallelise: surveying code under `src/core/`, `src/sehens/` and `example/`,
reading candidate files, independent greps.

---

## Project Overview

**Sehenswerte** ("worth seeing" in German) is a C# .NET 6 WinForms library: signal processing
utilities, visual controls, and the `Sehens` oscilloscope control for high-speed real-time data
acquisition and visualization.

- Solution root: `sehenswerte/`
- Core library: `src/core/Core.csproj` (namespace not fixed; utilities are standalone classes)
- Sehens control: `src/sehens/Sehens.csproj`
- Example app: `example/Use.csproj`

---

## Source Layout

```
src/
  core/                   - standalone utilities (no UI dependency)
    comms/                - serial port, communication queue
    controls/             - WinForms helper controls and AutoEditor
    filters/              - signal filter chain (FIR, IIR, FFT, Kalman, NLMS, RLS, ...)
    generators/           - tone, noise, waveform generators
    maths/                - FFT, interpolation, statistics, PID, LQR, rolling averages
    files/                - CSV load/save, CsvLog, RIFF audio read/write, AudioReader,
                            ParquetNumeric (Parquet.Net wrapper for numeric columns)
    AES.cs                - AES encryption helper
    CodeProfile.cs        - lightweight performance profiler
    Compression.cs        - data compression utilities
    DialogExtensions.cs   - file/folder dialogs that remember the last-used path per key
    EnumExtension.cs      - enum helpers
    HighResTimer.cs       - high-resolution timer
    ListExtensions.cs     - IList/IEnumerable extensions
    NaturalStringCompare.cs
    ObjectExtension.cs
    Process.cs            - process launching/management
    Reflection.cs         - object dump / reflection helpers
    Ring.cs               - ring buffer
    SqlQuery.cs           - lightweight SQL query helper
    StateMachine.cs
    StreamExtensions.cs
    StringExtensions.cs
    WindowsRegistry.cs
    XmlSerialise.cs       - generic XML serialization helpers

  sehens/                 - Sehens oscilloscope control
    SehensControl.cs      - main oscilloscope WinForms control
    SehensSave.cs         - state save/load
    data/                 - TraceData, TraceFeature, import/export, peak hold
    paint/                - per-trace painters (2D, FFT, PiP, XY)
    ui/                   - paint box, context menus, trace list, skin, click zones
```

---

## Key Classes

| Class | Location | Role |
|-------|----------|------|
| `SehensControl` | `src/sehens/SehensControl.cs` | Main oscilloscope WinForms control; embed in host forms |
| `TraceData` | `src/sehens/data/TraceData.cs` | Sample data and metadata for one trace channel |
| `TraceNameHints` | `src/sehens/TraceNameHints.cs` | Host-declared trace name decorations (prefixes/suffixes) for fuzzy state matching. Host sets `SehensControl.TraceNameHints` in one go after loading; cleared by `Clear()`; embedded in saved state XML |
| `FilterInput` | `src/core/filters/FilterInput.cs` | Entry point to the filter chain |
| `FilterOutput` | `src/core/filters/FilterOutput.cs` | End of filter chain; resampled output to consumers |
| `FftFilter` | `src/core/filters/FftFilter.cs` | FFT filter stage |
| `FftAnalyse` | `src/core/maths/FftAnalyse.cs` | FFT analysis math |
| `ToneGenerator` | `src/core/generators/ToneGenerator.cs` | Configurable sine/tone generator |
| `WaveformGenerator` | `src/core/generators/WaveformGenerator.cs` | Multi-waveform generator |
| `AutoEditor` | `src/core/controls/AutoEditor.cs` | Reflection-based binder between controls and a decorated data object |
| `AutoEditorControl` | `src/core/controls/AutoEditorControl.cs` | UserControl wrapper; call `Generate(sourceData)` to build the settings panel |
| `AutoEditorGroupForm` | `src/core/controls/AutoEditorGroupForm.cs` | Wide modal editor with an AutoEditorControl column per source object |
| `AutoEditorBase` | `src/core/controls/AutoEditorBase.cs` | Base class for auto-editable settings objects |
| `CsvLog` | `src/core/files/CsvLog.cs` | Structured append-only CSV logger with path-based subsystem tagging |
| `SerialPort` | `src/core/comms/SerialPort.cs` | Serial port wrapper |
| `Ring<T>` | `src/core/Ring.cs` | Generic ring/circular buffer |
| `StatsFilter` | `src/core/filters/StatsFilter.cs` | Rolling statistics (mean, variance, RMS) filter |
| `MruComboBox` | `src/core/controls/MruComboBox.cs` | Editable ComboBox with an MRU drop-down persisted via `WindowsRegistry` (REG_MULTI_SZ, default 10 entries, non-empty only). Set `RegistryKey`, call `CommitMru()` when the text is used. Used by `InputFieldForm.Show(saveMRU: true)` and the trace list's regex filter box (`SehensControl.FilterMruRegistryKey`, default `SehensTraceFilterMru`) |
| `DataGridControl` | `src/core/controls/DataGridControl.cs` | Filterable, sortable data grid with undo/replay stack and save/restore view state |
| `BoundData` | `src/core/controls/DataGridBoundData.cs` | `IBindingList` backing store for `DataGridControl`; owns `UnfilteredData`, `FilteredData`, `SortKeys`, `UndoList` |
| `DataGridControlHistory` | `src/core/controls/DataGridControlHistory.cs` | Snapshot history for `DataGridControl.SaveView` / `RestoreView` |
| `ProgressForm` | `src/core/controls/ProgressForm.cs` | Modeless floating progress window above its owner. Construct on the UI thread (the ctor creates the handle); `SetProgress`/`ShowOver`/`HideProgress` self-marshal via BeginInvoke |
| `PaintTraceBase` | `src/sehens/paint/PaintTraceBase.cs` | Base painter: axis rendering, `ProjectLog`, partition helpers |
| `Paint2dTrace` | `src/sehens/paint/Paint2dTrace.cs` | 2D line/polygon painter; owns the `Project2dCurves` resample/decimate pipeline |

---

## Painter Pipeline

Each `TraceView` has a `Painter` (`Paint2dTrace`, `Paint2dFFTTrace`, `PaintXYTrace`,
`PaintPiPTrace`) chosen from `PaintMode`. All derive from `PaintTraceBase`.

- `PaintProjection` runs per repaint with a `TraceGroupDisplay` (geometry + axis extents).
- `Project2dCurves` is the expensive resample/decimate step; it runs only when
  `SnapshotReprojectionRequired` and caches into `DrawnProjection1` / `DrawnProjection2` /
  `DrawnPolygon`. Force a recompute with `TraceView.RecalculateProjectionRequired()`; any property
  that changes projection geometry (zoom, math type, log axes, paint mode) must call it in its
  setter.
- `TraceView.CalculateTrace()` runs concurrently (the paint box calls it via `Parallel.ForEach`,
  `ProcessAtInput` calls it inline from whatever thread called `TraceData.Update`) and the recalc
  flags (`m_BeforeZoomCalculateRequired` etc.) are consumable booleans, so a slow calculation could
  store an older projection over a fresher one. Guard:
  `TraceData.SamplesGeneration` (bumped under `DataLock` on every sample mutation) is captured with
  the input snapshot and re-checked at projection-store time; on mismatch the result is DISCARDED
  and the view re-armed (`BeforeZoomCalculateRequired` + `ViewNeedsRepaint`). RULE: any new
  sample-mutation path on `TraceData` must bump `m_SamplesGeneration` inside its `DataLock` block.
- `TraceGroupDisplay.LeftSampleNumberValue` / `RightSampleNumberValue` carry the X-axis values for
  the current view (Hz for FFT, seconds for time-with-rate, sample number otherwise), from
  `TraceView.DrawnExtents()`. Do not recompute them from Nyquist or sample rate.

### Axis log scaling

`PaintTraceBase.ProjectLog(maxInput, input, out newMax, out output, staves=2)` is the canonical log
mapping: it compresses `staves` decades and clamps values below `maxInput / 10^staves` to 0. The
inverse is `input = maxInput * 10^(output - newMax)`.

`TraceView.LogVertical` is `LogVerticalMode { Auto, Off, Log, dB10, dB20 }`:
- `Auto` - resolves per display mode (below); the default for new traces.
- `Off` - linear values, linear pixel mapping.
- `Log` - linear values, **pixel-log** Y mapping via `ProjectLog`, for linear-magnitude FFT spanning
  many orders of magnitude. Painters check `view.IsLogY`.
- `dB10` / `dB20` - values converted to `10*log10(v)` / `20*log10(v)` inside `ExecuteFft`, linear
  pixel mapping. The " dB" axis suffix is gated by `view.IsLogarithmicY`, meaning value-domain dB,
  not pixel-log.

`TraceView.LogHorizontal` is `LogHorizontalMode { Off, Log }`; painters check `view.IsLogX`.

`MathTypes` describes only the math transform (`Normal`, `FFTMagnitude`, `FFTPhase`); dB conversion
is orthogonal and driven entirely by `LogVertical`. The legacy `FFT10Log10` / `FFT20Log10` are gone,
and `SehensSave.View.TranslateLegacyTraceXml` rewrites old files into the new shape (`FFTMagnitude`
+ `LogVertical=dB10/dB20`, and old `True/False` bool serialisations of `LogVertical` /
`LogHorizontal` into `Log`/`Off`).

`LogVertical` is the STORED choice; `TraceView.EffectiveLogVertical` is what gets applied and what
`IsLogarithmicY` / `IsLogY` / `ApplyDbInPlace` must read. `Auto` resolves against the DISPLAY mode,
not just `MathType`:
- `PaintMode == FFT2D` -> `Log`. FFT2D paints frequency up the Y axis and leaves `MathType` at
  `Normal`, so keying off `IsFftTrace` misses it.
- `MathType == FFTMagnitude` -> `dB10`.
- anything else, including `FFTPhase` -> `Off`. `ApplyDbInPlace` is not gated on math type, so a dB
  mode here would log-scale phase values.

`Off` is an explicit "linear" that must stick even on an FFT trace, so it cannot double as "unset".
Do NOT reintroduce a getter that overrides the stored value: it breaks `NextEnumValue` cycling and
`XmlSave` round-tripping.

The inset buttons next to "FFT" (`ContextMenus.AddTraceEmbeddedMenu`) cycle these per trace:
vertical `Auto` / `LinV` / `LogV` / `10Log10` / `20Log10`, horizontal `LinH` / `LogH`. The vertical
button labels the EFFECTIVE mode with a trailing `*` when stored is `Auto`; without it, `Auto` and
the same mode picked explicitly render identically.

### Saved-state versioning

`SehensSave.CurrentSaveVersion` is stamped into `Sehens.SaveVersion` by the live-object constructor
and threaded into `View.SaveTo` AND `View.ApplyTo`; miss either and that path skips migrations.
`TranslateLegacyTraceXml` keys off it (v1 -> v2 rewrites `LogVertical=Off` to `Auto`).

`SaveVersion` MUST stay initialised to `1`: pre-versioning files have no `<SaveVersion>` element, so
deserialising leaves the field at its initialiser, and initialising it to the current version makes
every legacy file skip its migration. The binary format (`.sehens`) inherits this free, since
`BinarySave.Xml` holds the same serialised root. Version the XML root, never `BinarySave`, whose own
`Version` field nothing reads.

### Painter / mouse mapping invariant

`TraceView.Measure(MouseEventArgs)` converts mouse X back to a sample index for hover labels. Its
X-axis remap MUST match the transform the painter applied; add a non-linear X projection to a
painter and you must mirror its inverse here, or hover labels report the wrong frequency/time.

### Axis painting

`PaintTraceBase.PaintHorizontalAxis` already dispatches linear vs log (`PaintGutterBottomPartition`
/ `PaintGutterBottomPartitionLog`) and includes label-overlap skip logic. Painter subclasses should
call `base.PaintHorizontalAxis(...)` rather than re-implementing tick layout.

### Horizontal axis (affine)

A trace's horizontal axis is the affine terms on `TraceData`
(`HorizontalOffset`/`HorizontalMultiplier`/`HorizontalAxisUnit`, set via
`SetHorizontalAffine(offset, multiplier, unit)`, multiplier > 0). There is NO enable flag: the
identity map (0, 1, "") IS the plain sample-number axis, and `ClearHorizontalAxis()` resets to it.

- The OFFSET is always in SAMPLES, never switching meaning between samples and axis units, so it
  composes with either scale (a caller with a value offset divides by the multiplier).
- Canonical map `TraceData.HorizontalValueAt`, inverse `SampleAtHorizontalValue`. With sps > 0,
  `value = (sample + offset) / sps`: the RATE supplies the scale, a multiplier is silently ignored,
  and the unit overrides the `"s"` default (`HorizontalUnitEffective`). With sps == 0,
  `value = multiplier * (sample + offset)` with `HorizontalAxisUnit`. The idle multiplier survives
  sps being set and takes back over when sps returns to 0.
- sps stays a separate field rather than folded into the multiplier because it doubles as the
  "horizontal axis is time" flag, feeding fake-YT eligibility, FFT Nyquist/Hz, FFT bandpass and
  audio playback rate.
- `HasExplicitHorizontalAxis` means "the AFFINE map positions the samples": sps == 0, usable terms,
  not the identity. Kind precedence: FFT > Time (sps) > Affine > None.
- INVALID terms (a bad multiplier where sps == 0 would use it, or a non-finite offset) are stored as
  given, NOT coerced: `TraceData.HorizontalAffineInvalid` flags it, every consumer falls back to
  sample numbers, and the trace paints "(bad horizontal axis)" via the `PaintTraceSamples` warning
  seam.
- Non-uniform axes are not affine-representable. Future: generalise YT from unix-time to a tabulated
  horizontal axis.

Grouped-trace positioning (`GroupHorizontal`, `src/sehens/paint/GroupHorizontal.cs`) resolves a
group to one of three `HorizontalMode`s from its members' axis kinds and units:

| Mode | When | Behaviour |
|------|------|-----------|
| Stretch | all members no-axis/plain index with the SAME count; also all-FFT, all-YT, all-log None | each trace fills the pane width (legacy) |
| ValueAlign | all Time or Affine sharing one unit (Time counts as `"s"`, so SPS and affine-"s" mix) | members share the union domain `[GroupHLeft,GroupHRight]`, each drawing into its pixel `ValueRect` sub-window (from `TraceGroupDisplay`), so differing ranges and counts align by value (ragged; short traces end early). The gutter spans the shared domain |
| Incompatible | mixed kinds, differing units, lin-X with log-X, all-log with differing ranges, plain-index with DIFFERING counts, FFT+non-FFT, YT+anything else | leader's axis plus index-stretch, painting a "mixed horizontal axes" warning |

- Log-X is part of axis compatibility because `SubWindow`'s pixel placement is linear while the log
  projection reconstructs sample values from the info endpoints: lin+log can never share one
  value->pixel map, and all-log groups value-align only when every member's range is IDENTICAL.
- `HorizontalKind.Yt` = `TraceView.IsYtDisplay`, the SAME predicate as `TraceGroupDisplay.YTTrace`.
  Keep them identical or classification disagrees with the draw mode.
- Classification runs for EVERY leader view, FFT and YT included, so an FFT-led or YT-led mixed
  group still paints its warning.

Editing and invariants:
- Editable in the double-click trace editor via `TraceView` proxies in DisplayOrder band 6
  "Horizontal Axis": `HorizontalAxisOffset`/`HorizontalAxisMultiplier`/`HorizontalAxisUnit`,
  `SamplesPerSecond`, `SamplesNumberDisplayOffset`, `ViewLengthOverride`/`ViewOffsetOverride`,
  `PadLeftWithFirstValue`/`PadRightWithLastValue`, `HoldPanZoom`. `AutoReduceRange` is in the "Data
  Range" band (7); the old "View and Navigation" band (5) is gone. No enable checkbox: values act as
  typed (identity == off). `TraceData` is `[AutoEditor.Hidden]`, and the proxies carry no
  `[XmlSave]` (persistence is `SehensSave.Trace`, which round-trips the three terms).
- RULE: an AutoEditor row setter must not side-effect another row's state. AutoEditor does not
  refresh sibling rows after a commit, so the panel desyncs.
- View length/offset changes must invalidate the GROUP's projections
  (`TraceView.ViewOverrideChanged` -> `RecalculateProjectionRequired` on every member); the cache
  holds the old extents.
- `SetZoomPan` and `GroupHorizontal.Window` both clamp pan to `[0, 1 - zoom]`, the LEFT-edge
  fraction ceiling. A bare `[0, 1]` clamp lets a drag over-pan past the data and snap back.
- `DrawnExtents`, `FullHorizontalAffine`, hover (`SampleNumberText`) and the group domain all go
  through the ONE canonical map, with `InputSampleNumberDisplayOffset` added to the sample number
  first. Keep it that way: when extents and `FullHorizontalAffine` disagree on units, `SubWindow`
  places the ValueRect off-pane and the trace draws NOTHING.
- Two knobs, two jobs. `ViewOffsetOverride`/`ViewLengthOverride` reshape the RAW samples in
  `ApplyOffsetAndLength` (offset N puts source[N] at drawn index 0, negative pads left, length trims
  or extends/pads right), and the axis must NOT re-add the view offset or nothing appears to shift.
  `InputSampleNumberDisplayOffset` is the pure axis-relabel knob and IS added into the map.
- Hover values use `IndexAfterTrim` + display offset (matching the gutter); the `[n]` hover index
  stays `IndexBeforeTrim`, the source index. Hover stats are SUPPRESSED outside a trace's drawn data
  (`MouseInfo.BeyondDrawnData`, set in `Measure`), with a pad flag exempting its side.
- `TraceData.ViewedSampleAtUnixTime`'s fake-YT branch clamps its index to the data and computes the
  sample time as index/sps.
- Gutter lin/log dispatch (`PaintTraceBase.HorizontalGutter`): tick POSITIONS always follow the
  view's `IsLogX`; only label VALUES switch between units and bare sample numbers
  (`ShowHorizontalUnits`, false for no-sps/no-affine traces and during the gutter-hover peek). Do
  NOT gate the log gutter on `ShowHorizontalUnits`.
- `TraceView.Measure` and `PaintTraceBase.SampleToRatio` invert through the same sub-window.
  Reprojection is forced when `ValueRect` changes, since the cache is keyed on data+logH, not the
  rect.
- Zoom/pan on a ValueAlign group selects a shared VALUE window of the group's full domain
  (`TraceView.TryGroupValueWindow` inside `GetDrawnSamples`) so members stay aligned;
  Stretch/Incompatible keep the legacy per-trace count-fraction zoom.
- `ValueRect.Width` clamps to 1, so a member spanning under a pixel of the shared domain projects a
  one-column min/max envelope: a 2-point polygon. `ProjectPolygon` dropping NaN columns can empty it
  outright. GDI+ `FillPolygon` throws `ArgumentException` ("Parameter is not valid") only for an
  EMPTY point array - 1 and 2 points are tolerated and paint nothing (`GraphicsPath.AddPolygon` is
  the API that rejects under 3). So every fill goes through `Paint2dTrace.CanFillPolygon`
  (Debug-logs `Skip fill <trace>`), which rejects empty to avoid the throw and under-3 because there
  is nothing to fill. Do not assert the under-3 throw in a test; it does not happen.

Remaining work: dual-axis for incompatible groups, unit-agnostic YT.

### Context menu ordering

`ScopeContextMenu.MenuItem.Sort` orders items WITHIN a submenu: lower shows first, equal (default 0)
falls back to alphabetical. Gotcha: the menu builder `Insert(0, ...)`s items, so the list comparator
sorts DESCENDING to render ascending; mirror the existing `(b.Sort - a.Sort)` /
`b.Text.CompareTo(a.Text)` convention when touching it. The Generate submenu uses it: signal
generators (Tone/Sweep/Noise/Sinc/Window, Sort 1-5), reference sets (All windows/All filters/Filter
Coefficients, 6-8), bulk test data (Axis test matrix/YT test traces/100 test traces, 10-12).

### Calculated (math) views - notify and ordering rules

A calculated view (`CalculateType != None`) recomputes in `CalculateTrace` and then notifies.

- RULE: it must never notify itself (it is a viewer of its own TraceData), which causes a continuous
  recalculate/repaint loop for any math trace.
- Sources have no viewer wiring (`CalculatedSourceViews` is a plain list), so downstream propagation
  is EXPLICIT: after a recompute, views whose `CalculatedSourceViews` contain this view are armed
  directly, and the chain terminates at the leaves.
- `CalculateBefore` orders calculated views `2 + depth` by their depth in the
  `CalculatedSourceViews` chain and runs one wave per order, so `Differentiate(Differentiate(x))`
  computes in ONE paint. Keep the wave loop bounded by the highest assigned order, not a hardcoded
  count: one shared wave for every calculated view fills a chain one level per paint, and only
  while repaints keep arriving.
- A calculated view's own `TraceData` holds no input samples - they exist only as
  `m_CalculatedBeforeZoom`, published at the end of `CalculateTrace`. Anything asking a calculated
  view for its own length mid-pass therefore gets the PREVIOUS pass's length, or 0 on the first
  pass, which reports a zero-width domain and clamps the drawn window to one sample (a blank pane).
  Pass the real length via `FullHorizontalAffine(fullCountOverride)` on any mid-pass call.

Tests: `CalculatedViewSettlesAndStillFollowsItsSource` pins the notify halves,
`MathViewOnASecondsAxisDrawsOnTheFirstPaint` pins the blank-trace case (Generate > Tone, then
Math > Differentiate, ONE paint), `ChainedCalculatedViewsResolveInOnePaint` pins the ordering
(3-deep, through the real paint driver - `SehensTestHarness.Layout`'s flat loop happens to be
dependency-ordered, so it cannot catch either), and `MathTestTracesComputeAndSettle` covers every
`CalculatedTypes` value.

### Axis test matrix (visual)

Right-click > Generate > "Axis test matrix" (`ContextMenus.GenerateAxisTestMatrix`, internal for its
smoke test) builds one small deterministic group per taxonomy row, `ax01`..`ax27`: stretch, affine
(aligned/ragged/gapped), mixed units, plain+affine, time (counts/rates/sample-offset),
sps+affine-"s", lin+log, log pairs, fake and real YT, YT+plain, FFT pairs, FFT+plain, YT+FFT, the
bad-multiplier warning, view-window reshapes (`ax20` is a "window zoo" over IDENTICAL source data),
view length/offset on affine and time groups, combined rows, then FFT-of-real-YT (`ax25`), a
mixed-rate fake-YT pair (`ax26`) and YT pads (`ax27`). Distinct sine cycle counts per member make
misalignment obvious at a glance; `AxisTestMatrixTests` pins each group's HMode and screenshots the
board.

- FFT of a real-YT trace: `TraceData.InterpolateYT` resamples the non-uniform samples onto a uniform
  grid whose rate is the SMALLEST positive time gap (`CalculateSamplesPerSecond`, so dense regions
  lose nothing and sparse stretches upsample), and the Hz axis derives from that rate.
- YT pads are PAINT-level (`Paint2dTrace.ProjectYT`): with the pad flags set, the first/last value
  is held flat to the edges of the visible time window. The array-reshape pads in
  `ApplyOffsetAndLength` never run for YT (view length/offset reach YT only through
  `GetGroupUnixTimeRange`'s time-window math), and the dense YT polygon path (> 10x pane width
  samples) does not pad.

Paint exceptions are caught and painted as on-screen text so one bad trace cannot kill the app,
which makes them invisible to tests. Every catch site therefore also calls
`SehensPaintBox.RecordPaintException` (public `PaintExceptionCount` / `LastPaintExceptionText`, plus
CsvLog + Debug.WriteLine).

- RULE: screenshot-based tests MUST assert `scope.PaintBox.PaintExceptionCount == 0` after painting,
  and any new catch in the paint pipeline must route through `RecordPaintException`.
- `CalculateBefore`'s `Parallel.ForEach` catches PER TRACE (recorded with the view name) and
  `PaintTraces`' parallel worker catches PER GROUP (the `TraceGroupDisplay` ctor and Bitmap creation
  sit above `PaintTraceGroup`'s own catch): one bad trace must not abort the pass, and an uncaught
  worker exception detours through the TPL as an `AggregateException`, which also trips the
  debugger's "user-unhandled" break on framework code.

Parallel paint (the live OnPaint path) gives each group its own bitmap, composited under a lock. The
group bitmaps copy the target's `SmoothingMode`/`InterpolationMode`/`TextRenderingHint`, so render
quality is decided ONCE at the target.

- Render-quality POLICY: live trace painting is deliberately FAST (aliased curves; AA costs roughly
  4x on dense traces). OnPaint applies `HighQualityRender`'s AA/bicubic only AFTER `PaintTraces`, so
  the cursor/overlay layer is smooth and the 1:1 bitmap compositing is not bicubic-taxed.
  Screenshots and exports honour `HighQualityRender` fully (`ScreenshotToBitmap` sets the target
  smoothing from it). Axis gutters force `SmoothingMode.None` internally: axis-aligned dashed
  gridlines gain nothing from AA and were the single biggest AA cost.
- `ScreenshotToBitmap(skin, singleGroup, parallel: true)` routes a screenshot through the parallel
  path, and `ParallelPaintMatchesSequential` pixel-compares it against the sequential path over the
  axis test matrix. Prime one paint first: AutoRange during paint shifts ranges between the first
  and second frames, which is not a parallel bug.

### Trace-view test harness

`src/sehens/SehensTestHarness.cs` (internal) builds headless scope geometry so paint-layer tests run
without a real paint cycle: `Layout(scope)` recomputes `Painted` info and paint-box sizes (mirrors
`ScreenshotToBitmap`'s setup; call after every trace/group/zoom mutation), `AffineTrace(...)` makes
a ramp trace with an affine axis, `ZoomPan(...)` fans zoom out to views.

Fixtures using it: `TraceGroupDisplayTests`, `TraceViewHorizontalTests` (per-kind
`FullHorizontalAffine`, value-window zoom slices, gutter-vs-drawn consistency, FFT Hz-from-sps,
fake-YT value-align bypass, pan clamp, view-length reprojection), `PaintTraceBaseMappingTests`
(`SampleToRatio` + `Measure` forward/inverse, axis-title gutter reservation),
`Paint2dProjectionTests`, `SehensPaintBoxTests` (hover-label stacking via the extracted pure
`LayoutHoverLabels`, full-paint warning smokes via `ScreenshotToBitmap` - set `skin.ExportTraces =
VisibleTraces`, since the default exports selected-only), and
`SehensSaveTest.AffineAxisRoundTripsThroughSave` / `LegacyHorizontalAxisValuesFileLoadsCleanly` (a
legacy `<HorizontalAxisValues>` element is absorbed into `OtherElements` and ignored).
`SampleToRatio` and `BottomTitleReservedRight` are `internal`, not private, for these tests.

On Windows run the suite with
`example\bin\Debug\net6.0-windows\Use.exe runtest [classSubstr] [methodSubstr]` (results also in
`runtest-results.txt`); on macOS use `./run-tests.sh`.

---

## Filter Chain Pattern

Filters implement `IFilter` and are chained producer -> consumer:

```
AudioSource -> FilterInput -> [FirFilter / IirFilter / FftFilter / ...] -> FilterOutput -> SehensControl trace
```

- `FilterInput` is the source adapter; it accepts raw sample arrays.
- Intermediate filters transform or analyse the signal.
- `FilterOutput` is the sink adapter; consumers poll or subscribe for processed samples. Also used
  for display-rate resampling when feeding a `SehensControl`.

---

## AutoEditor Convention

Settings objects inherit `AutoEditorBase`. Decorate fields/properties to control rendering:

| Attribute | Effect |
|-----------|--------|
| `[AutoEditor.DisplayOrder(n, groupName?)]` | Sort order; items sharing `(int)n` render under the same group header |
| `[AutoEditor.DisplayName("...")]` | Override the label (default is the pretty-printed field name) |
| `[AutoEditor.Values(new[]{...})]` / `Values(typeof(Enum))` / `Values(typeof(IValuesAttrInterface))` | Render as a `ComboBox` with the given list |
| `[AutoEditor.Range(min, max, step)]` | On a numeric field, adds `-`/`+` kick buttons that nudge by `step`, clamped to `[min, max]` |
| `[AutoEditor.Hidden]` | Skip rendering |
| `[AutoEditor.Tooltip("...")]` | Hover tooltip on the row's label and editor control. `\n` for line breaks. One shared `ToolTip` per `AutoEditorControl` |
| `[AutoEditor.Disabled]` | Render greyed/disabled (`Enabled=false`); see the `ReadOnly` property for a legible viewer |
| `[AutoEditor.Password]` | Mask the TextBox content |
| `[AutoEditor.Radix(n)]` | On an integer field, display and parse in radix `n` (default 16 = hex; 2 = binary, 8 = octal). Shown `0x`/`0b`/`0o`-prefixed, zero-padded to the type's native width; input prefix optional; signed types use raw two's-complement bits (`0xFF` -> sbyte -1). Non-fitting or unparseable input is not committed. Ignored on non-integer types (incl. float/double) and unsupported radixes; composes with `[Range]` kick buttons. On an array field it applies to the elements |
| `[AutoEditor.PushButton("caption")]` | On a `bool` or delegate field, render as a clickable Button |
| `[AutoEditor.SubEditor]` | Render a `...` button that opens an `AutoEditorForm` for the nested object |
| `[AutoEditor.InlineClass]` | Flatten a nested class's fields into the parent panel at the host field's `[DisplayOrder]` slot. Child rows keep their own ordering/grouping/display names inside that slot. Alternative to `[SubEditor]`: no button, no popup |
| `[AutoEditor.ArrayEditor(mode, itemLabelFormat?, buttonCaption?)]` | Editor for an `IList`/array field. `Inline` emits one row per element in the parent panel; `SubForm` emits one button opening a popup. Scalar elements render as their normal scalar control; class-typed elements render as a button-per-element opening a per-element subeditor. Default `itemLabelFormat` is `"[{0}]"`. Length changes between `UpdateControls` invocations trigger a panel rebuild |

Host a panel by adding an `AutoEditorControl` to your form and calling `Generate(sourceData)`.
`AutoEditorBase` exposes an `OnChanged` callback and an `UpdateControls` action for round-tripping.
Combo rows keep the standard Windows wheel behaviour (a suppress-wheel guard was tried and
reverted), so any setting reachable by wheel must tolerate transient states: `TraceView.ExecuteCalculate`
returns empty for zero sources rather than throwing on the paint thread.

`AutoEditorControl` per-instance options, set BEFORE `Generate` (later changes take effect only on
the next `Generate`):

- `CommitMode` (`AutoEditor.CommitMode`): `Immediate` (default) commits text fields on every
  keystroke (`TextChanged`); `OnValidated` commits on focus-leave (`Validated`) or Enter.
  CheckBox/RadioButton/ComboBox selections always commit immediately. Commits fire
  `OnChanging`/`OnChanged` only when the parsed
  value differs from the current source value (`SetValue` gates on `Equals`), because `Validated`
  fires on EVERY focus-leave including an untouched field. Limitations: do not host an
  `OnValidated` panel inside `AutoEditorForm` (the form's KeyPreview Enter fires OK before the
  control-level commit); a value still being typed when the form closes is not committed; `[Range]`
  kick buttons commit on the NEXT focus loss, not the click.
- `ReadOnly`: legible non-editable viewer, distinct from `[Disabled]` greying. TextBoxes get
  `TextBox.ReadOnly=true` (selectable/copyable), `[Values]`/enum rows render as read-only TextBoxes,
  bool CheckBoxes get `AutoCheck=false`, buttons are disabled, and NO commit wiring is attached.
- `UpdateControls()`: push current `SourceData` values into the generated controls. This is the
  refresh path for `SourceData` objects that are NOT `AutoEditorBase`, e.g. ones mutated in place by
  a read thread. Safe from a non-UI thread (marshals via `BeginInvoke` once the handle exists; runs
  synchronously before it is created).

---

## SehensControl Usage

- Embed an instance in a form (usually via the Designer); the host owns the control.
- Feed traces by writing samples through a `FilterOutput`, or by calling into `TraceData` directly.
- `Scope.Import(path)` loads a previously saved state or trace file; hosts typically wire it to
  drag-drop or a command-line argument.
- The right-click context menu is built from `ScopeContextMenu`; per-trace menus are in
  `src/sehens/ui/ContextMenus.cs`.

---

## Trace Annotations (TraceFeature)

`TraceFeature` ([src/sehens/data/TraceFeature.cs](src/sehens/data/TraceFeature.cs)) is the canonical
way to draw text, lines, highlights and handles on a trace. Use it for any label, vertical line or
shaded span at a specific sample; do NOT invent a separate "label trace" or scope name encoding.

Types (`TraceFeature.Feature`): `Text`, `GutterText`, `Line`, `Highlight`, `LeftHandle`,
`RightHandle`, `TriggerHandle`. Per-feature fields: `SampleNumber` (anchor), `RightSampleNumber`
(spans), `UnixTime` / `RightUnixTime` (YT traces), `Text`, `Colour` (`null` = skin default), and `Angle`
(`Angle = -90`, vertical bottom-to-top, is the default).

Vertical placement for `Text` features:
- `VerticalAnchor = Centre` (default): pixel-space centre of the plot rectangle, ignoring
  `VerticalPosition`, value range and Y scaling.
- `VerticalAnchor = Y`: `VerticalPosition` is a literal Y value, projected through the painter's
  linear/log Y mapping.
- `VerticalAnchor = Sample`: the sample value at `SampleNumber`, projected through the same mapping,
  so the label rides the trace.
- `VerticalJustify`: `Top` / `Middle` (default) / `Bottom`, where the text bbox sits relative to the
  anchor Y. For rotated text, `Top`/`Bottom` are the rotated bbox's screen-space edges, not the
  first/last character.
- The painter clamps the bbox into the plot rectangle so edge labels are not clipped.

Features live on `TraceData`, not `TraceView`. `scope[name]` returns the `TraceData`:

```csharp
scope["foo"].AddFeature(sampleNumber, "label");          // append one text feature
scope["foo"].AddFeature(new TraceFeature { ... });        // append arbitrary feature
scope["foo"].InputFeatures = listOfFeatures;              // replace (clears existing) + auto-sort
```

`InputFeatures = ...` is right when a feature set is derived fresh each `Run()`: it clears and
re-sorts in one shot, so re-runs are idempotent.

Visibility is gated by `Scope.ShowTraceFeatures` (toggle in the right-click context menu). If
features do not appear, check that flag before debugging anything else.

---

## DataGridControl

A `DataGridView` wrapper with a status-strip toolbar offering filter/sort operations. Data lives in
`BoundData` (implements `IBindingList`):
- `UnfilteredData` - all rows in original order. Each `BoundDataRow.Index` is the stable identity
  used everywhere instead of grid position.
- `FilteredData` - currently visible/sorted rows; what the grid shows.
- `m_History` / `m_RedoStack` - `DataGridControlHistory` lists of `Snapshot` view states.

### Column mutations

Limit: column display binds via the hardcoded `col0..col99` accessors on `BoundDataRow`, which bind
by reflection to `DataPropertyName = "col{N}"`. This caps the grid at 100 displayable columns;
columns beyond index 99 will not render values. Extend that `col0..colN` accessor block if a use
case needs more.

Replacing those accessors with `ITypedList` on `BoundData` plus a dynamic `IndexedColumnDescriptor`
was tried and reverted: sorting on a newly-added column (via `AddColumns`/`InsertColumns`) blanked
only that column's cells after the sort. The root cause was never pinned (suspected a stale
`CurrentSortProperty` descriptor surviving the `DataSource` cycle, or `::` in the column `Name`
confusing WinForms cell rendering). The static reflection binding sorts correctly across
`AddColumns`; the dynamic approach did not. If you revisit it, first reproduce and pin that
sort-blank bug in a focused test.

### SaveView / RestoreView

`grid.SaveView()` returns a `DataGridControlHistory` snapshot; `grid.RestoreView(view)` replays it.

### Undo / redo (the snapshot triad)

Every undoable/redoable op needs three things in lockstep, or it silently falls out of the history:
1. An `Operation` enum value in `DataGridControlHistory.Snapshot.Operation`.
2. A `PushSnapshot(...)` call at the start of the public method, capturing the pre-op visible set,
   which is what `Undo()`'s `ApplyVisible` restores.
3. A `case` in `DispatchAction`, through which `Redo()` and `RestoreBoundState` re-execute the op by
   re-calling the public method. No case means nothing to replay.

Undo restores the captured pre-op view; redo re-runs the op. Selection-based hides (`HideRows`,
`HideRowsOtherThan`) store stable row `Index` values, so they replay only against the same data
(fine for in-session undo/redo and SaveView/RestoreView; best-effort and bounds-guarded on a
different dataset). Data-driven hides (`HideRowsMatching`, anchors) replay meaningfully on different
data.

### Column reorder and horizontal scroll

`RebuildGridColumns` resets `grid.DataSource`, which snaps the horizontal scroll to 0. A reorder
(`DoMove`) must not move the viewport, so it captures `HorizontalScrollingOffset` and restores it
via `RestoreHorizontalScroll`, deferred through `BeginInvoke` because the `DataSource` reset and the
`ListChanged.Reset` both re-zero it before layout settles. Column drag is hand-rolled (the WinForms
`AllowUserToOrderColumns` is off); a `DragScrollTimer` auto-scrolls when the drag mouse enters the
left/right edge zone, gated on `ColumnsOverflowViewport`.

---

## Native Dependencies

- **FFTW** (`x86/`, `x64/`, `arm64/`) - native FFT library; see `COPYING.FFTW` / `README.FFTW`
- **FFmpeg** headers (`avcodec.h`, `avformat.h`, etc.) - referenced by `AudioReader`

---

## Conventions

- Core utilities have no dependency on the Sehens control; keep it that way.
- Filters are stateful objects, one instance per channel/pipeline, not shared.
- `CsvLog` paths use `/`-separated extension segments to tag log subsystems.
- XML serialization uses `XmlSerialise` helpers, not `JsonSerializer`.
- `XmlSaveAttribute.Extract` and `Inject` both key on `attribute.Name ?? member.Name`. Keep them
  symmetric: if `Inject` ever matches on the member name alone, every renamed member saves fine and
  silently never loads back, with no exception (`Inject` swallows errors).
- The example app (`example/`) is the canonical integration test; keep it compiling.
- ASCII only in source and docs. No em-dashes, en-dashes, curly quotes, arrows, checkmarks or other
  non-ASCII punctuation. Use `-`, `--`, `->`, straight quotes, plain words.

### Coding style

Follow C# standard guidelines, with these specific rules:
- Use the prefix `m_` for module-level variables, excluding simple classes where it is not necessary
- Use leading capital letters for property and field names
- Try for one return statement in functions, except for first-in checks
- Avoid modifying parameter variables unless necessary for the caller
- Use exception handling for exceptional situations rather than normal cases
- Use unit tests to verify correctness and behaviour when applicable. Tests typically live in the
  same source file as the code they test, in a `[TestClass]` with `[TestMethod]` members from
  `Microsoft.VisualStudio.TestTools.UnitTesting`. Run with `dotnet test src/core/Core.csproj`. On
  macOS `dotnet test` cannot host the x64/WinForms test assembly; use `./run-tests.sh [classSubstr]
  [methodSubstr]`, which builds `example/Use.csproj` and runs the tests headless under Wine via the
  Windows dotnet host (`Utils.Process.RunTests` plus the `runtest` verb in `example/Program.cs`;
  prints PASS/FAIL, exits 0 iff all matched pass, also writes `runtest-results.txt`)
- Name loop variables `loop`, not `i`
- Always use braces for if/else/foreach/while/try/finally bodies, even single-line ones. Exception:
  guard clauses that immediately return/continue/break may stay on one line without braces:
  `if (!foo) return;`. This applies to lambdas too - `() => { foo(); }` must be expanded to
  multi-line. For all other cases, put `{` on the next line (Allman style)
- Do not use `using static` - qualify static class members explicitly
- Large classes are split into partial classes for clarity (e.g. `DataGridControl` /
  `DataGridBoundData`)
- Forms/controls use `AutoScaleMode.Font` - do not change
- Keep comments short and pithy: non-obvious behaviour, not obvious code
