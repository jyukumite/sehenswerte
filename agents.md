# agents.md — Sehenswerte Architecture & Agent Instructions

## Agent Instruction

**Always update this file** when you learn something architectural — even if not asked. If you add a class, change a major data flow, rename a subsystem, or discover how something works, record it here. Keep entries concise and accurate; remove stale entries when things change.

---

## Project Overview

**Sehenswerte** — C# .NET 6 WinForms library ("worth seeing" in German). Provides a reusable core of signal processing utilities, visual controls, and the `Sehens` oscilloscope control for high-speed real-time data acquisition and visualization.

- Solution root: `sehenswerte/`
- Core library: `src/core/Core.csproj` (namespace not fixed — utilities are standalone classes)
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
    files/                — CSV load/save, CsvLog, RIFF audio read/write, AudioReader
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
| `AutoEditor` | `src/core/controls/AutoEditor.cs` | Generates a settings UI panel from a decorated class |
| `AutoEditorBase` | `src/core/controls/AutoEditorBase.cs` | Base class for auto-editable settings objects |
| `CsvLog` | `src/core/files/CsvLog.cs` | Structured append-only CSV logger with path-based subsystem tagging |
| `SerialPort` | `src/core/comms/SerialPort.cs` | Serial port wrapper |
| `Ring<T>` | `src/core/Ring.cs` | Generic ring/circular buffer |
| `StatsFilter` | `src/core/filters/StatsFilter.cs` | Rolling statistics (mean, variance, RMS) filter |

---

## Filter Chain Pattern

Filters implement `IFilter` and are chained producer → consumer:

```
AudioSource → FilterInput → [FirFilter / IirFilter / FftFilter / …] → FilterOutput → SehensControl trace
```

- `FilterInput` is the source adapter; it accepts raw sample arrays.
- Intermediate filters transform or analyse the signal.
- `FilterOutput` is the sink adapter; consumers poll or subscribe to get processed samples.

---

## AutoEditor Convention

Settings objects inherit `AutoEditorBase` and decorate fields with `[AutoEditor.DisplayOrder(n)]`.  
Call `AutoEditor.Generate(settingsObject)` to produce a `Panel` with auto-generated controls.  
Numeric fields tagged with `[AutoEditor.Range(min, max, step)]` render with `-`/`+` kick buttons that nudge the text value by `step`, clamped to `[min, max]`.  
---

## Native Dependencies

- **FFTW** (`x86/`, `x64/`, `arm64/`) — native FFT library; see `COPYING.FFTW` / `README.FFTW`
- **FFmpeg** headers (`avcodec.h`, `avformat.h`, etc.) — referenced by `AudioReader`

---

## Conventions

- Core utilities have no dependency on the Sehens control; keep it that way.
- Filters are stateful objects — one instance per channel/pipeline, not shared.
- `CsvLog` paths use `/`-separated extension segments to tag log subsystems.
- XML serialization uses `XmlSerialise` helpers, not `JsonSerializer`.
- The example app (`example/`) is the canonical integration test — keep it compiling.
