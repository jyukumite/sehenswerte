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
| `DataGridControlHistory` | `src/core/controls/DataGridControlHistory.cs` | XML-serialisable snapshot history for `DataGridControl.SaveView` / `RestoreView` |

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
| `[AutoEditor.Disabled]` | Render read-only |
| `[AutoEditor.Password]` | Mask the TextBox content |
| `[AutoEditor.PushButton("caption")]` | On a `bool` or delegate field, render as a clickable Button |
| `[AutoEditor.SubEditor]` | Render a `...` button that opens an `AutoEditorForm` for the nested object |

Host a panel by adding an `AutoEditorControl` to your form and calling `Generate(sourceData)`.  
`AutoEditorBase` exposes an `OnChanged` callback and an `UpdateControls` action for round-tripping between the UI and model.

---

## SehensControl Usage

- Embed an instance in a form (usually via the Designer); the host owns the control.
- Feed traces by writing samples through a `FilterOutput`, or by directly calling into `TraceData`.
- `Scope.Import(path)` loads a previously saved state or trace file — hosts typically wire it to drag-drop or a command-line argument.
- Right-click context menu is built from `ScopeContextMenu`; per-trace menus are in `src/sehens/ui/ContextMenus.cs`.

---

## DataGridControl

A `DataGridView` wrapper with a status-strip toolbar offering filter/sort operations.
Data lives in `BoundData` (implements `IBindingList`):
- `UnfilteredData` - all rows in original order. Each `BoundDataRow.Index` is the
  stable identity used everywhere instead of grid position.
- `FilteredData` - currently visible/sorted rows; what the grid shows.
- `UndoList` - `Stack<UndoEntry>` of full view snapshots.

### Column mutations

Limit: column display is bound via the hardcoded `col0..col99` accessors on
`BoundDataRow`, so columns beyond index 99 will not render values. Extend that
accessor block if a use case needs more.

### SaveView / RestoreView

```csharp
DataGridControlHistory view = grid.SaveView();
string xml = view.ToXml(); // ToXml/FromXml from StringExtensions.cs
File.WriteAllText("view.xml", xml);

DataGridControlHistory? loaded = File.ReadAllText("view.xml").FromXml<DataGridControlHistory>();
if (loaded != null) grid.RestoreView(loaded);
```

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
