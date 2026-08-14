# agents.md — Sehenswerte Architecture & Agent Instructions

## Agent Instruction

**Always update this file** when you learn something architectural -- even if not asked. If you add
a class, change a major data flow, rename a subsystem, or discover how something works, record it
here. Keep entries concise and accurate; remove stale entries when things change.

**This is a public MIT-licensed repository.** Never reference private host projects or
their internals (project names, tab/class names, business context) in code, comments,
tests, docs, or commit messages here. Describe behaviour generically instead.

**Use parallel subagents aggressively.** For any task involving multiple files, multiple searches,
or independent exploration, dispatch subagents in parallel by issuing multiple Agent tool calls in a
single message. Sequential work should be reserved for steps that genuinely depend on earlier
results. Examples that should be parallelised: surveying related code under `src/core/`,
`src/sehens/`, and `example/`; reading several candidate files; running independent greps.

---

## Project Overview

**Sehenswerte** — C# .NET 6 WinForms library ("worth seeing" in German). Provides a reusable core of
signal processing utilities, visual controls, and the `Sehens` oscilloscope control for high-speed
real-time data acquisition and visualization.

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
| `TraceNameHints` | `src/sehens/TraceNameHints.cs` | Host-declared trace name decorations (prefixes/suffixes) for fuzzy state matching. Host sets `SehensControl.TraceNameHints` in one go after loading; cleared by `Clear()`; embedded in saved state XML |
| `FilterInput` | `src/core/filters/FilterInput.cs` | Entry point to the filter chain; feeds data into connected filters |
| `FilterOutput` | `src/core/filters/FilterOutput.cs` | End of filter chain; provides resampled output to consumers |
| `FftFilter` | `src/core/filters/FftFilter.cs` | FFT filter stage |
| `FftAnalyse` | `src/core/maths/FftAnalyse.cs` | FFT analysis math |
| `ToneGenerator` | `src/core/generators/ToneGenerator.cs` | Configurable sine/tone generator |
| `WaveformGenerator` | `src/core/generators/WaveformGenerator.cs` | Multi-waveform generator |
| `AutoEditor` | `src/core/controls/AutoEditor.cs` | Reflection-based binder between controls and a decorated data object |
| `AutoEditorControl` | `src/core/controls/AutoEditorControl.cs` | UserControl wrapper — call `Generate(sourceData)` to build the settings panel |
| `AutoEditorGroupForm` | `src/core/controls/AutoEditorGroupForm.cs` | Wide modal editor with an AutoEditorControl column per source object. |
| `AutoEditorBase` | `src/core/controls/AutoEditorBase.cs` | Base class for auto-editable settings objects |
| `CsvLog` | `src/core/files/CsvLog.cs` | Structured append-only CSV logger with path-based subsystem tagging |
| `SerialPort` | `src/core/comms/SerialPort.cs` | Serial port wrapper |
| `Ring<T>` | `src/core/Ring.cs` | Generic ring/circular buffer |
| `StatsFilter` | `src/core/filters/StatsFilter.cs` | Rolling statistics (mean, variance, RMS) filter |
| `MruComboBox` | `src/core/controls/MruComboBox.cs` | Editable ComboBox whose drop-down is an MRU history persisted via `WindowsRegistry` (REG_MULTI_SZ, default 10 entries; entries must be non-empty). Set `RegistryKey`, call `CommitMru()` when the text is used. Used by `InputFieldForm.Show(saveMRU: true)` and the trace list's "Filter by (regex)" box (`SehensControl.FilterMruRegistryKey`, default shared key `SehensTraceFilterMru`) |
| `DataGridControl` | `src/core/controls/DataGridControl.cs` | Filterable, sortable data grid with undo/replay stack and save/restore view state |
| `BoundData` | `src/core/controls/DataGridBoundData.cs` | `IBindingList` backing store for `DataGridControl`; owns `UnfilteredData`, `FilteredData`, `SortKeys`, `UndoList` |
| `DataGridControlHistory` | `src/core/controls/DataGridControlHistory.cs` | Snapshot history for `DataGridControl.SaveView` / `RestoreView` |
| `ProgressForm` | `src/core/controls/ProgressForm.cs` | Small modeless floating progress window that floats above its owner form. Construct on the UI thread (ctor creates the handle); SetProgress/ShowOver/HideProgress are thread-safe (self-marshal via BeginInvoke). |
| `PaintTraceBase` | `src/sehens/paint/PaintTraceBase.cs` | Base painter -- horizontal/vertical axis rendering, `ProjectLog`, partition helpers |
| `Paint2dTrace` | `src/sehens/paint/Paint2dTrace.cs` | 2D line/polygon painter; owns the `Project2dCurves` resample/decimate pipeline |

---

## Painter Pipeline

Each `TraceView` has a `Painter` (one of `Paint2dTrace`, `Paint2dFFTTrace`, `PaintXYTrace`,
`PaintPiPTrace`) chosen from `PaintMode`. All derive from `PaintTraceBase`.

- `PaintProjection` runs per repaint with a `TraceGroupDisplay` (geometry + axis extents).
- `Project2dCurves` is the expensive resample/decimate step. It runs only when
  `SnapshotReprojectionRequired` is true and caches into `DrawnProjection1` / `DrawnProjection2` /
  `DrawnPolygon`.
- To force a recompute, call `TraceView.RecalculateProjectionRequired()`. Any property that changes
  projection geometry (zoom, math type, log axes, paint mode) must call this in its setter.
- `TraceView.CalculateTrace()` runs concurrently - the paint box calls it via `Parallel.ForEach`
  while the `ProcessAtInput` path calls it inline from whatever thread called `TraceData.Update`.
  The recalc flags (`m_BeforeZoomCalculateRequired` etc.) are consumable booleans, so a slow
  calculation could finish LAST and store an older projection over a fresher one with the flags
  already spent - a permanently stale drawn trace/stats until the next external invalidation. Guard:
  `TraceData.SamplesGeneration` (bumped under `DataLock` with every sample mutation) is captured
  with the input snapshot and re-checked at projection-store time; on mismatch the result is
  DISCARDED and the view re-armed (`BeforeZoomCalculateRequired` + `ViewNeedsRepaint`). Any new
  sample-mutation path on `TraceData` must bump `m_SamplesGeneration` inside its `DataLock` block.
- `TraceGroupDisplay.LeftSampleNumberValue` / `RightSampleNumberValue` carry the X-axis values for
  the current view (Hz for FFT, seconds for time-with-rate, sample number otherwise). They come from
  `TraceView.DrawnExtents()` -- do not recompute from Nyquist or sample rate yourself.

### Axis log scaling

`PaintTraceBase.ProjectLog(maxInput, input, out newMax, out output, staves=2)` is the canonical log
mapping. It compresses `staves` decades (default 2); values below `maxInput / 10^staves` clamp to 0.
The inverse is `input = maxInput * 10^(output - newMax)`.

`TraceView.LogVertical` is a 5-state enum `LogVerticalMode { Auto, Off, Log, dB10, dB20 }`:

- `Auto` -- no explicit choice; resolves per display mode (see below). The default for new traces.
- `Off` -- linear values, linear pixel mapping.
- `Log` -- linear values, **pixel-log** Y mapping via `ProjectLog`. Use case: linear-magnitude FFT
  where peaks span many orders of magnitude. Painters check this via `view.IsLogY`.
- `dB10` / `dB20` -- values converted to `10*log10(v)` / `20*log10(v)` inside `ExecuteFft`; linear
  pixel mapping. The " dB" axis label suffix is gated by `view.IsLogarithmicY` (which means
  "value-domain dB", not pixel-log).

`TraceView.LogHorizontal` is a 2-state enum `LogHorizontalMode { Off, Log }`. Painters check this
via `view.IsLogX`.

`MathTypes` only describes the math transform (`Normal`, `FFTMagnitude`, `FFTPhase`). dB conversion
is orthogonal -- entirely driven by `LogVertical`. The legacy `FFT10Log10` / `FFT20Log10` values are
gone; `SehensSave.View.TranslateLegacyTraceXml` rewrites old saved files into the new shape
(`FFTMagnitude` + `LogVertical=dB10/dB20`, and the old `True/False` bool serialisations of
`LogVertical` / `LogHorizontal` into `Log`/`Off`).

`LogVertical` is the STORED choice; `TraceView.EffectiveLogVertical` is what gets applied, and is
what `IsLogarithmicY` / `IsLogY` / `ApplyDbInPlace` must read. `Auto` resolves against the DISPLAY
mode, not just `MathType`:

- `PaintMode == FFT2D` -> `Log`. FFT2D paints frequency up the Y axis and leaves `MathType` at
  `Normal`, so keying off `IsFftTrace` misses it.
- `MathType == FFTMagnitude` -> `dB10`.
- anything else, including `FFTPhase` -> `Off`. `ApplyDbInPlace` is not gated on math type, so a dB
  mode here would log-scale phase values.

`Off` is an explicit "linear" that must stick even on an FFT trace, which is why it cannot double as
"unset". Do not reintroduce a getter that overrides the stored value -- that breaks `NextEnumValue`
cycling (modes become unreachable) and `XmlSave` round-tripping.

The inset buttons next to "FFT" (`ContextMenus.AddTraceEmbeddedMenu`) cycle these per trace:
vertical steps `Auto` / `LinV` / `LogV` / `10Log10` / `20Log10`, horizontal `LinH` / `LogH`. The
vertical button labels the EFFECTIVE mode, with a trailing `*` when stored is `Auto` -- without it,
`Auto` and the same mode picked explicitly are adjacent steps that render identically.

### Saved-state versioning

`SehensSave.CurrentSaveVersion` is stamped into `Sehens.SaveVersion` by the live-object constructor
and threaded into `View.SaveTo` AND `View.ApplyTo` -- miss either and that path skips migrations.
`TranslateLegacyTraceXml` keys off it (v1 -> v2 rewrites `LogVertical=Off` to `Auto`).

`SaveVersion` MUST stay initialised to `1`: pre-versioning files have no `<SaveVersion>` element, so
deserialising leaves the field at its initialiser, and initialising it to the current version makes
every legacy file skip its migration. The binary format (`.sehens`) inherits this free --
`BinarySave.Xml` holds the same serialised root. Version the XML root, never `BinarySave`, whose own
`Version` field nothing reads.

### Painter / mouse mapping invariant

`TraceView.Measure(MouseEventArgs)` converts mouse X back to a sample index for hover labels. Its
X-axis remap MUST match whatever transform the painter applied. If you add a non-linear X projection
in a painter, mirror its inverse in `Measure` or hover labels will report the wrong frequency/time.

### Axis painting

`PaintTraceBase.PaintHorizontalAxis` already dispatches linear vs log (`PaintGutterBottomPartition`
/ `PaintGutterBottomPartitionLog`) and includes label-overlap skip logic. Painter subclasses should
call `base.PaintHorizontalAxis(...)` rather than re-implementing tick layout.

### Horizontal axis (affine)

A trace's horizontal axis is the affine terms on `TraceData`
(`HorizontalOffset`/`HorizontalMultiplier`/`HorizontalAxisUnit`, set via
`SetHorizontalAffine(offset, multiplier, unit)`, multiplier > 0). There is NO enable flag: the
identity map (0, 1, "") IS the plain sample-number axis; `ClearHorizontalAxis()` resets to it. The
OFFSET is always in SAMPLES - it never switches meaning between samples and axis units, so it
composes identically with either scale (a caller with a value offset divides by the multiplier).
Composition with a sample rate (the canonical map is `TraceData.HorizontalValueAt` / inverse
`SampleAtHorizontalValue`): - sps > 0: `value = (sample + offset) / sps` - the RATE supplies the
scale (a multiplier cannot compose with a rate and is silently ignored while sps > 0); the unit
overrides the `"s"` default (`HorizontalUnitEffective`).
- sps == 0: `value = multiplier * (sample + offset)` with `HorizontalAxisUnit`. Both maps are `value
  = scale * (sample + offset)` - the multiplier is effectively `1/sps`. sps stays a separate field
  (rather than being folded into the multiplier) because it doubles as the "horizontal axis is time"
  flag: it defaults the unit to "s", makes Time members group-compatible with affine-"s", and feeds
  the non-axis machinery a multiplier never could (fake-YT eligibility, FFT Nyquist/Hz, FFT
  bandpass, audio playback rate). The idle multiplier survives sps being set and takes back over
  when sps returns to 0. `HasExplicitHorizontalAxis` means "the AFFINE map positions the samples":
  sps == 0, usable terms, not the identity. Kind precedence: FFT > Time (sps) > Affine > None.
  INVALID terms (a bad multiplier when sps == 0 would use it, or a non-finite offset) are stored as
  given, NOT coerced: `TraceData.HorizontalAffineInvalid` flags it, every consumer falls back to
  sample numbers, and the trace paints a "(bad horizontal axis)" warning via the `PaintTraceSamples`
  warning seam. Non-uniform axes are not affine-representable - future: generalise YT from unix-time
  to a tabulated horizontal axis Grouped-trace positioning (`GroupHorizontal`,
  `src/sehens/paint/GroupHorizontal.cs`): a group resolves to one of three `HorizontalMode`s from
  its members' axis kinds/units. **Stretch** (all members have no axis / plain index, and the SAME
  sample count) - each trace fills the pane width (legacy behaviour). Plain-index members with
  DIFFERING counts classify Incompatible: each still stretches, but the shared sample-number gutter
  is only correct for the leader, so the "mixed horizontal axes" warning paints. **ValueAlign** (all
  members Time or Affine, one shared unit; Time counts as `"s"` so SPS and affine-"s" mix) - all
  members share the union value domain `[GroupHLeft,GroupHRight]`; each trace draws into its pixel
  `ValueRect` sub-window (computed in `TraceGroupDisplay`), so different ranges/counts align by
  value (ragged, short traces end early); the gutter spans the shared domain. **Incompatible**
  (mixed kinds, differing units, a lin-X member grouped with a log-X member, or an all-log group
  with differing ranges) - falls back to the leader's axis + index-stretch and paints a "mixed
  horizontal axes" warning. Log-X is part of axis compatibility because the log projection
  reconstructs sample values from the info endpoints and `SubWindow`'s pixel placement is linear:
  lin+log can never share one value->pixel map, and all-log groups value-align only when every
  member's range is IDENTICAL (full-pane sub-windows, so each member's own log map is the same map).
  This applies to plain sample-number (None) groups too: mixed lin/log Nones are Incompatible;
  all-log Nones Stretch as usual. FFT kinds: an all-FFT group classifies Stretch (the FFT painter's
  own Hz path handles alignment, untouched); FFT + non-FFT classifies Incompatible. YT kinds
  (`HorizontalKind.Yt` = `TraceView.IsYtDisplay`, the SAME predicate as `TraceGroupDisplay.YTTrace`
  - keep them identical or classification disagrees with the draw mode): an all-YT group classifies
    Stretch (YT's own group-shared unix-time window, untouched); YT mixed with anything else
    classifies Incompatible. The classification runs for EVERY leader view - FFT and YT included
    (`TraceGroupDisplay` no longer skips them); previously an FFT-led or YT-led mixed group painted
    no warning because the warning comes from the leader's info. The axis is editable in the
    double-click trace editor via proxies on `TraceView` in DisplayOrder band 6 "Horizontal Axis":
    `HorizontalAxisOffset`/`HorizontalAxisMultiplier`/`HorizontalAxisUnit`, `SamplesPerSecond`,
    `SamplesNumberDisplayOffset`, `ViewLengthOverride`/`ViewOffsetOverride`,
    `PadLeftWithFirstValue`/`PadRightWithLastValue`, `HoldPanZoom` - every horizontal input in one
    band; `AutoReduceRange` lives in the "Data Range" band (7); the old "View and Navigation" band
    (5) is gone. No enable checkbox: values act as typed (identity == off). `TraceData` itself is
    `[AutoEditor.Hidden]`; no `[XmlSave]` on the proxies (persistence is SehensSave.Trace, which
    round-trips just the three terms - no flag needed since identity means "no axis"). RULE: an
    AutoEditor row setter must not side-effect another row's state - AutoEditor does not refresh
    sibling rows after a commit, so the panel desyncs (this killed the earlier enable-checkbox
    design). View length/offset changes must invalidate the GROUP's projections
    (`TraceView.ViewOverrideChanged` -> `RecalculateProjectionRequired` on every member): the
    projection cache holds the old extents, and without the invalidation the horizontal axis only
    caught up on the next zoom nudge. Pan clamp: `SetZoomPan` clamps pan to `[0, 1 - zoom]` (the
    LEFT-edge fraction ceiling), and `GroupHorizontal.Window` clamps the same way - a bare `[0, 1]`
    clamp let a mouse drag over-pan past the data and snap back on release. `DrawnExtents`,
    `FullHorizontalAffine`, hover (`SampleNumberText`) and the group domain all go through the ONE
    canonical map (`TraceData.HorizontalValueAt`, plus `InputSampleNumberDisplayOffset` added to the
    sample number first). Keep it that way: when extents and `FullHorizontalAffine` disagreed on
    units, `SubWindow` placed the ValueRect entirely off-pane and a trace with both sps and an
    offset drew NOTHING. View Length/Offset vs Display Sample Offset - two knobs, two jobs:
    `ViewOffsetOverride`/`ViewLengthOverride` reshape the RAW samples in `ApplyOffsetAndLength`
    (offset N puts source[N] at drawn index 0, negative pads left, length trims or extends/pads
    right) and the axis must NOT re-add the view offset - re-adding it made the axis follow the move
    so nothing appeared to shift (and a grouped cropped member re-anchored to its original
    position). `InputSampleNumberDisplayOffset` is the pure axis-relabel knob and IS added into the
    map. Hover values use `IndexAfterTrim` + display offset (matches the gutter); the `[n]` hover
    index stays `IndexBeforeTrim` (the source index). Hover stats are SUPPRESSED outside a trace's
    drawn data (`MouseInfo.BeyondDrawnData`, set in `Measure`: X outside the ValueRect sub-window,
    or a YT hover time outside the trace's own time extent) - a pad flag exempts its side, since the
    pad paints a flat line there. `TraceData.ViewedSampleAtUnixTime`'s fake-YT branch clamps its
    index to the data (it used to return negative indices and zero values) and computes the sample
    time as index/sps (was index*sps - hover labels showed start+68400 for sample 684 at 100 sps).
    Gutter lin/log dispatch (`PaintTraceBase.HorizontalGutter`): tick POSITIONS always follow the
    view's `IsLogX` - only the label VALUES switch between units and bare sample numbers
    (`ShowHorizontalUnits`, which is false for no-sps/no-affine traces and during the gutter-hover
    peek). Gating the log gutter on `ShowHorizontalUnits` (the old code) left a plain sample-number
    log-X trace with a linear gutter under a log curve. `TraceView.Measure` and
    `PaintTraceBase.SampleToRatio` invert through the same sub-window. Reprojection is forced when
    `ValueRect` changes (the projection cache is keyed on data+logH, not the rect). Horizontal
    zoom/pan on a ValueAlign group selects a shared VALUE window of the group's full domain
    (`TraceView.TryGroupValueWindow` inside `GetDrawnSamples`), so members stay aligned under
    pan/zoom; Stretch/Incompatible groups keep the legacy per-trace count-fraction zoom. Remaining
    work (dual-axis for incompatible groups, unit-agnostic YT)

### Context menu ordering

`ScopeContextMenu.MenuItem.Sort` orders items WITHIN a submenu: lower shows first, equal (default 0)
falls back to alphabetical. Gotcha: the menu builder `Insert(0, ...)`s items, so the list comparator
sorts DESCENDING to render ascending - mirror the existing `(b.Sort - a.Sort)` /
`b.Text.CompareTo(a.Text)` convention when touching it. The Generate submenu uses it: signal
generators (Tone/Sweep/Noise/Sinc/Window, Sort 1-5), reference sets (All windows/All filters/Filter
Coefficients, 6-8), bulk test data (Axis test matrix/YT test traces/100 test traces, 10-12).

### Calculated (math) views - notify and ordering rules

A calculated view (`CalculateType != None`) recomputes in `CalculateTrace` and then notifies.
RULE: it must never notify itself (it is a viewer of its own TraceData), which would cause a continuous
recalculate/repaint loop for any math trace. Sources have no viewer wiring (`CalculatedSourceViews`
is a plain list), so downstream propagation is EXPLICIT: after a recompute, views whose
`CalculatedSourceViews` contain this view are armed directly, and the chain terminates at the leaves.

That arming carries an update into a chain; ordering is what makes the chain resolve. `CalculateBefore`
orders calculated views `2 + depth` by their depth in the `CalculatedSourceViews` chain and runs one
wave per order, so `Differentiate(Differentiate(x))` computes in ONE paint. One shared wave for every
calculated view (the original `InputSampleCount == 0 ? 3 : 2`) filled a chain in one level per paint
and only while a repaint kept arriving - a calc-of-a-calc pane rendered EMPTY in the field. Keep the
wave loop bounded by the highest assigned order, not a hardcoded count.

A calculated view's own `TraceData` holds no input samples - its samples only ever exist as
`m_CalculatedBeforeZoom`, and `CalculateTrace` publishes that at the end of the pass. So anything asking
a calculated view for its own length mid-pass gets the PREVIOUS pass's length, or 0 on the first pass.
That is what made `Math > Differentiate` on a generated tone paint BLANK: the Math menu copies the
source's samples-per-second onto the new view, which puts it on a seconds axis and therefore into the
value-aligned path, `FullHorizontalAffine` reported a zero-width domain, and `TryGroupValueWindow`
clamped the drawn window to ONE sample - correct maths, blank pane, then settled. Pass the real length
via `FullHorizontalAffine(fullCountOverride)` on any mid-pass call. This was latent for as long as calc
views re-armed themselves every paint (the second pass saw a published length and drew correctly);
removing the self-notify is what exposed it.

`CalculatedViewSettlesAndStillFollowsItsSource` pins the notify halves,
`MathViewOnASecondsAxisDrawsOnTheFirstPaint` pins the blank-trace case (the exact field repro:
Generate > Tone, then Math > Differentiate, ONE paint),
`ChainedCalculatedViewsResolveInOnePaint` pins the ordering (3-deep, driven through the real paint
driver - note `SehensTestHarness.Layout`'s flat loop happens to be dependency-ordered, so it cannot
catch either of these), and `MathTestTracesComputeAndSettle` covers every CalculatedTypes value.

### Axis test matrix (visual)

Right-click > Generate > "Axis test matrix" (`ContextMenus.GenerateAxisTestMatrix`, internal for its
smoke test) creates one small deterministic group per taxonomy row, named `ax01`..`ax24`: stretch,
aligned/ragged/gapped affine, mixed units, plain+affine, time (counts/rates/sample-offset),
sps+affine-"s", lin+log, log pair, fake/real YT, YT+plain, FFT pair, FFT+plain, YT+FFT, the
bad-multiplier warning, the ax20 "window zoo" (IDENTICAL source data with one member per view-window
reshape: trim left/right/both, slide, pad left/right/both), view length/offset windows on affine
(ax21) and time (ax22) groups, and the everything-combined rows ax23 (affine offset+multiplier+unit
+ view window) / ax24 (sps + sample-offset + view window). ax25 is an FFT of a real-YT trace:
  `TraceData.InterpolateYT` resamples the non-uniform samples onto a uniform grid whose rate comes
  from the SMALLEST positive time gap (`CalculateSamplesPerSecond` - dense regions lose nothing,
  sparse stretches upsample), and the FFT's Hz axis derives from that rate. ax26 is a mixed-rate
  fake-YT pair (alignment is by wall clock, rates independent); ax27 shows YT pads. YT pads are
  PAINT-level (`Paint2dTrace.ProjectYT`): with the pad flags set, the first/last value is held flat
  to the edges of the visible time window. The array-reshape pads in `ApplyOffsetAndLength` never
  run for YT - that pipeline draws straight from (time, value) pairs, and view length/offset reach
  YT only through `GetGroupUnixTimeRange`'s time-window math. The dense YT polygon path (> 10x pane
  width samples) does not pad. Distinct sine cycle counts per member make misalignment obvious at a
  glance. `AxisTestMatrixTests` pins each group's HMode and screenshots the board. Paint exceptions
  are caught and painted as on-screen text (one bad trace must not kill the app), which is invisible
  to tests - so every catch site also calls `SehensPaintBox.RecordPaintException` (public
  `PaintExceptionCount` / `LastPaintExceptionText`, plus CsvLog + Debug.WriteLine). Screenshot-based
  tests MUST assert `scope.PaintBox.PaintExceptionCount == 0` after painting; that is how the
  fake-YT IndexOutOfRange would have been caught. When adding a new catch in the paint pipeline,
  route it through `RecordPaintException`. `CalculateBefore`'s Parallel.ForEach catches PER TRACE
  (recorded with the view name): one bad trace must not abort the whole calculate pass, and an
  uncaught worker exception detours through the TPL as an AggregateException - which also trips the
  debugger's "user-unhandled" break on framework code. `PaintTraces`' parallel per-group worker
  likewise catches PER GROUP (the TraceGroupDisplay ctor and Bitmap creation sit above
  PaintTraceGroup's own catch). Parallel paint (the live OnPaint path: each group painted into its
  own bitmap, composited under a lock): the group bitmaps copy the target's
  SmoothingMode/InterpolationMode/TextRenderingHint, so render quality is decided ONCE at the
  target. Render-quality POLICY: live trace painting is deliberately FAST (aliased curves - AA costs
  ~4x on dense session traces, measured 429 vs 97 ms on a real session screenshot); OnPaint applies
  HighQualityRender's AA/bicubic only AFTER PaintTraces, so the cursor/overlay layer is smooth and
  the 1:1 bitmap compositing is not bicubic-taxed. Screenshots/exports honour `HighQualityRender`
  fully (ScreenshotToBitmap sets the target smoothing from it). Axis gutters force
  SmoothingMode.None internally - axis-aligned dashed gridlines gain nothing from AA and were the
  single biggest AA cost (PaintHorizontalAxis 4.8 -> 19.6 ms avg). `ScreenshotToBitmap(skin,
  singleGroup, parallel: true)` routes a screenshot through the parallel path;
  `ParallelPaintMatchesSequential` pixel-compares it against the sequential path over the axis test
  matrix (prime one paint first - AutoRange during paint shifts ranges between the first and second
  frames, which is not a parallel bug). 

### Trace-view test harness

`src/sehens/SehensTestHarness.cs` (internal) builds headless scope geometry so paint-layer tests run
without a real paint cycle: `Layout(scope)` recomputes `Painted` info + paint-box sizes (mirrors
`ScreenshotToBitmap`'s setup; call after every trace/group/zoom mutation), `AffineTrace(...)` makes
a ramp trace with an affine axis, `ZoomPan(...)` fans zoom out to views. Fixtures using it:
`TraceGroupDisplayTests` (ValueRect/domain/gutter override), `TraceViewHorizontalTests` (per-kind
`FullHorizontalAffine`, value-window zoom slices, gutter-vs-drawn consistency, FFT Hz-from-sps incl.
affine-ignored, fake-YT time-range-from-sps + value-align bypass, pan clamp, view-length
reprojection), `PaintTraceBaseMappingTests` (`SampleToRatio` + `Measure` forward/inverse, axis-title
gutter reservation), `Paint2dProjectionTests` (stretch no-op vs legacy index math, ValueRect-change
reprojection), `SehensPaintBoxTests` (hover-label stacking via the extracted pure
`LayoutHoverLabels`, full-paint warning smokes via `ScreenshotToBitmap` - set `skin.ExportTraces =
VisibleTraces`, the default exports selected-only), `SehensSaveTest.AffineAxisRoundTripsThroughSave`
/ `LegacyHorizontalAxisValuesFileLoadsCleanly` (a legacy `<HorizontalAxisValues>` file element is
absorbed into `OtherElements` and ignored). `SampleToRatio` and `BottomTitleReservedRight` are
`internal` (not private) for these tests. On Windows run the suite with
`example\bin\Debug\net6.0-windows\Use.exe runtest [classSubstr] [methodSubstr]` (results also in
`runtest-results.txt`); on macOS use `./run-tests.sh`.

---

## Filter Chain Pattern

Filters implement `IFilter` and are chained producer → consumer:

```
AudioSource → FilterInput → [FirFilter / IirFilter / FftFilter / …] → FilterOutput → SehensControl trace
```

- `FilterInput` is the source adapter; it accepts raw sample arrays.
- Intermediate filters transform or analyse the signal.
- `FilterOutput` is the sink adapter; consumers poll or subscribe to get processed samples. Also
  used for display-rate resampling when feeding a `SehensControl`.

---

## AutoEditor Convention

Settings objects inherit `AutoEditorBase`. Decorate fields/properties with attributes to control
rendering:

| Attribute | Effect |
|-----------|--------|
| `[AutoEditor.DisplayOrder(n, groupName?)]` | Sort order; items sharing `(int)n` render under the same group header |
| `[AutoEditor.DisplayName("...")]` | Override the label (default is pretty-printed field name) |
| `[AutoEditor.Values(new[]{...})]` / `Values(typeof(Enum))` / `Values(typeof(IValuesAttrInterface))` | Render as a `ComboBox` with the given list |
| `[AutoEditor.Range(min, max, step)]` | On a numeric field, adds `-`/`+` kick buttons that nudge by `step`, clamped to `[min, max]` |
| `[AutoEditor.Hidden]` | Skip rendering |
| `[AutoEditor.Tooltip("...")]` | Hover tooltip on the row's label and editor control. `\n` for line breaks. One shared `ToolTip` per `AutoEditorControl` |
| `[AutoEditor.Disabled]` | Render greyed/disabled (`Enabled=false`); see the `ReadOnly` property for a legible viewer |
| `[AutoEditor.Password]` | Mask the TextBox content |
| `[AutoEditor.Radix(n)]` | On an integer field, display and parse in radix `n` (default 16 = hex; 2 = binary, 8 = octal). Shown `0x`/`0b`/`0o`-prefixed, zero-padded to the type's native width; input prefix optional; signed types use raw two's-complement bits (`0xFF` -> sbyte -1). Non-fitting or unparseable input is not committed. Ignored on non-integer types (incl. float/double) and unsupported radixes; composes with `[Range]` kick buttons. On an array field it applies to the elements |
| `[AutoEditor.PushButton("caption")]` | On a `bool` or delegate field, render as a clickable Button |
| `[AutoEditor.SubEditor]` | Render a `...` button that opens an `AutoEditorForm` for the nested object |
| `[AutoEditor.InlineClass]` | Flatten a nested class's fields directly into the parent panel at the host field's `[DisplayOrder]` slot. Child rows keep their own ordering/grouping/display names inside that slot. Alternative to `[SubEditor]` -- no button, no popup. |
| `[AutoEditor.ArrayEditor(mode, itemLabelFormat?, buttonCaption?)]` | Editor for an `IList`/array field. `Inline` mode emits one row per element directly in the parent panel; `SubForm` mode emits one button that opens a popup. Scalar-typed elements render as their normal scalar control; class-typed elements render as a button-per-element opening a per-element subeditor. Default `itemLabelFormat` is `"[{0}]"`. Length changes between `UpdateControls` invocations trigger a panel rebuild. |

Host a panel by adding an `AutoEditorControl` to your form and calling `Generate(sourceData)`.
`AutoEditorBase` exposes an `OnChanged` callback and an `UpdateControls` action for round-tripping
between the UI and model. Combo rows keep the standard Windows wheel behaviour (wheel changes the
focused combo's value - a deliberate decision; a suppress-wheel guard was tried and reverted).
Consequence: any setting reachable by wheel must tolerate transient/odd states - e.g. a
CalculateType set before any source views exist (`TraceView.ExecuteCalculate` returns empty for zero
sources instead of throwing on the paint thread).

`AutoEditorControl` per-instance options (set BEFORE `Generate`; changing later has no effect until
the next `Generate`):

- `CommitMode` (`AutoEditor.CommitMode`): `Immediate` (default) commits text fields on every
  keystroke (`TextChanged`); `OnValidated` commits text fields on focus-leave (`Validated`) or
  Enter. CheckBox/RadioButton/ComboBox selections always commit immediately (discrete gestures, no
  partial state). Commits fire `OnChanging`/`OnChanged` only when the parsed value actually differs
  from the current source value (`SetValue` gates on `Equals`): `Validated` fires on EVERY
  focus-leave, incl. tabbing off an untouched field, so without the gate an unchanged field would
  spuriously fire the change delegate (e.g. re-`Redraw()` a sim tab, re-send a BLE TX packet).
  Limitations: do not host an `OnValidated` panel inside `AutoEditorForm` (the form's KeyPreview
  Enter fires OK before the control-level commit); a value still being typed when the form closes is
  not committed; `[Range]` kick buttons commit on the NEXT focus loss, not the click.
- `ReadOnly` (bool): legible non-editable viewer, distinct from `[Disabled]` greying. TextBoxes get
  `TextBox.ReadOnly=true` (selectable/copyable), `[Values]`/enum rows render as read-only TextBoxes
  instead of ComboBoxes, bool CheckBoxes get `AutoCheck=false`, buttons are disabled, and NO commit
  wiring is attached at all.
- `UpdateControls()` (public method): push current `SourceData` values into the generated controls.
  The refresh path for `SourceData` objects that are NOT `AutoEditorBase` (e.g. protocol packet
  objects mutated in place by a read thread). Safe to call from a non-UI thread (marshals via
  `BeginInvoke` once the handle exists; runs synchronously on the calling thread before the handle
  is created).

---

## SehensControl Usage

- Embed an instance in a form (usually via the Designer); the host owns the control.
- Feed traces by writing samples through a `FilterOutput`, or by directly calling into `TraceData`.
- `Scope.Import(path)` loads a previously saved state or trace file — hosts typically wire it to
  drag-drop or a command-line argument.
- Right-click context menu is built from `ScopeContextMenu`; per-trace menus are in
  `src/sehens/ui/ContextMenus.cs`.

---

## Trace Annotations (TraceFeature)

`TraceFeature` ([src/sehens/data/TraceFeature.cs](src/sehens/data/TraceFeature.cs)) is the canonical
way to draw text, lines, highlights, and handles on a trace. Use this any time you want to put a
label, vertical line, or shaded span at a specific sample on an existing trace -- do NOT invent a
separate "label trace" or scope name encoding.

Types (`TraceFeature.Feature`): `Text`, `GutterText`, `Line`, `Highlight`, `LeftHandle`,
`RightHandle`, `TriggerHandle`.

Per-feature fields: `SampleNumber` (anchor), `RightSampleNumber` (for spans), `UnixTime` /
`RightUnixTime` (for YT traces), `Text`, `Colour` (`null` = skin default), `Angle` (`-90` = vertical
bottom-to-top, the default).

Vertical placement for `Text` features:
- `VerticalAnchor = Centre` (default): pixel-space centre of the plot rectangle. Ignores
  `VerticalPosition`, value range, and Y scaling. Reproduces the legacy mid-trace placement.
- `VerticalAnchor = Y`: value-space. `VerticalPosition` is a literal Y value, projected through the
  painter's linear/log Y mapping.
- `VerticalAnchor = Sample`: value-space. The sample value at `SampleNumber`, projected through the
  same Y mapping, so the label rides the trace.
- `VerticalJustify`: `Top` / `Middle` (default) / `Bottom` -- where the text bbox sits relative to
  the anchor Y. For rotated text (e.g. `Angle = -90`), `Top` / `Bottom` refer to the rotated bbox's
  screen-space edges, not to the first/last character of the string.
- The painter clamps the bbox into the plot rectangle so labels near the edges aren't clipped.

Features live on `TraceData`, not `TraceView`. `scope[name]` returns the `TraceData` -- so:

```csharp
scope["foo"].AddFeature(sampleNumber, "label");          // append one text feature
scope["foo"].AddFeature(new TraceFeature { ... });        // append arbitrary feature
scope["foo"].InputFeatures = listOfFeatures;              // replace (clears existing) + auto-sort
```

`InputFeatures = ...` is the right choice when a feature set is derived fresh each `Run()` -- it
clears and re-sorts in one shot so re-runs are idempotent.

Visibility is gated by `Scope.ShowTraceFeatures` (toggle in the right-click context menu). If
features don't appear, check that flag before debugging anything else.

---

## DataGridControl

A `DataGridView` wrapper with a status-strip toolbar offering filter/sort operations. Data lives in
`BoundData` (implements `IBindingList`):
- `UnfilteredData` - all rows in original order. Each `BoundDataRow.Index` is the stable identity
  used everywhere instead of grid position.
- `FilteredData` - currently visible/sorted rows; what the grid shows.
- `m_History` / `m_RedoStack` - `DataGridControlHistory` lists of `Snapshot` view states (undo and
  redo stacks).

### Column mutations

Limit: column display is bound via the hardcoded `col0..col99` accessors on `BoundDataRow`, which
bind via reflection to `DataPropertyName = "col{N}"`. This caps the grid at 100 displayable columns;
columns beyond index 99 will not render values. Extend that `col0..colN` accessor block if a use
case needs more.

Replacing the hardcoded accessors with `ITypedList` on `BoundData` plus a dynamic
`IndexedColumnDescriptor` was tried and reverted: sorting on a newly-added column (via
`AddColumns`/`InsertColumns`) blanked only that column's cells after the sort, while other columns
stayed intact. Root cause was not fully pinned - suspected a stale `CurrentSortProperty` descriptor
surviving the `DataSource` cycle, or `::` in the column `Name` confusing WinForms cell rendering.
The static reflection binding sorts correctly across `AddColumns`; the dynamic approach did not. If
you must revisit it, first reproduce and pin that sort-blank bug in a focused test.

### SaveView / RestoreView

`grid.SaveView()` returns a `DataGridControlHistory` snapshot; `grid.RestoreView(view)` replays it.

### Undo / redo (the snapshot triad)

Every undoable/redoable op needs three things in lockstep, or it silently falls out of the history
(the "hide unselected redo doesn't work" class of bug):
1. An `Operation` enum value in `DataGridControlHistory.Snapshot.Operation`.
2. A `PushSnapshot(...)` call at the start of the public method - this captures the pre-op visible
   set, which is what `Undo()`'s `ApplyVisible` restores.
3. A `case` in `DispatchAction` - `Redo()` and `RestoreBoundState` re-execute the op by re-calling
   the public method through here. No case means nothing to replay. Undo restores the captured
   pre-op view; redo re-runs the op. Selection-based hides (`HideRows`, `HideRowsOtherThan`) store
   stable row `Index` values, so they replay only against the same data (fine for in-session
   undo/redo and SaveView/RestoreView; best-effort, bounds-guarded, on a different dataset).
   Data-driven hides (`HideRowsMatching`, anchors) replay meaningfully on different data.

### Column reorder and horizontal scroll

`RebuildGridColumns` resets `grid.DataSource`, which snaps the horizontal scroll to 0. A reorder
(`DoMove`) must not move the viewport, so it captures `HorizontalScrollingOffset` and restores it
via `RestoreHorizontalScroll` - deferred through `BeginInvoke` because the `DataSource` reset and
the `ListChanged.Reset` both re-zero it before layout settles. Column drag is hand-rolled (the
WinForms `AllowUserToOrderColumns` is off); a `DragScrollTimer` auto-scrolls when the drag mouse
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
- `XmlSaveAttribute.Extract` and `Inject` both key on `attribute.Name ?? member.Name`. Keep them
  symmetric -- if `Inject` ever matches on the member name alone, every renamed member saves fine and
  silently never loads back, with no exception (`Inject` swallows errors).
- The example app (`example/`) is the canonical integration test, keep it compiling.
- ASCII only in source and docs. No em-dashes, en-dashes, curly quotes, arrows, checkmarks, or other
  non-ASCII punctuation. Use `-`, `--`, `->`, straight quotes, plain words.

### Coding style

Follow C# standard guidelines, with these specific rules:
- Use the prefix `m_` for module-level variables, excluding simple classes where it's not necessary
- Use leading capital letters for property and field names
- Try for one return statement in functions, except for first-in checks
- Avoid modifying parameter variables unless necessary for the caller
- Use exception handling for exceptional situations rather than normal cases
- Use unit tests to verify correctness and behaviour when applicable. Tests typically  live in the
  same source file as the code they test, in a `[TestClass]` with `[TestMethod]` members from
  `Microsoft.VisualStudio.TestTools.UnitTesting`. Run with `dotnet test src/core/Core.csproj`. On
  macOS `dotnet test` cannot host the x64/WinForms test assembly; instead use `./run-tests.sh
  [classSubstr] [methodSubstr]` which builds `example/Use.csproj` and runs the tests headless under
  Wine via the Windows dotnet host (`Utils.Process.RunTests` + the `runtest` verb in
  `example/Program.cs`; prints PASS/FAIL, exit 0 iff all matched pass, also writes
  `runtest-results.txt`).
- Name loop variables `loop`, not `i`
- Always use braces for if/else/foreach/while/try/finally bodies, even single-line ones. Exception:
  guard clauses that immediately return/continue/break may stay on one line without braces: `if
  (!foo) return;`. This applies to lambdas too -- `() => { foo(); }` must be expanded to multi-line.
  For all other cases, put `{` on the next line (Allman style)
- Do not use `using static` - qualify static class members explicitly
- Large classes are split into partial classes for clarity (e.g. `DataGridControl` /
  `DataGridBoundData`)
- Forms/controls use `AutoScaleMode.Font` - do not change
- Keep comments short and pithy. They should describe non-obvious behaviour, not obvious code (e.g.
  avoid `int a = 5; // set a to 5`)
