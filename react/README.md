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

## Status

Early development. The initial scope is the API surface the originals' main consumer actually
uses, plus the grid's status-strip operations; the long tail (FFT paint modes, calculated
traces, triggers, audio, exotic import/export formats) is deferred.

See `sehenswerte-react.md` at the repo root for the port plan.

## License

Same as the parent repository - see the license section in the top-level README.md.
