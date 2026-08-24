// The scope's model layer (the SehensControl state minus WinForms): named
// TraceData channels, TraceViews over them, stacked view groups, global
// zoom/pan, and change notification. scope.ensure(name) mirrors the C#
// scope["name"] create-on-demand indexer.

import { EntrySink, newEntry } from "../log/LogEntry";
import { ScopeSkin, darkSkin } from "./skin";
import { TraceData } from "./TraceData";
import { autoRangeGroup, TraceView } from "./TraceView";

export class ScopeModel {
  skin: ScopeSkin;
  views: TraceView[] = [];
  groups: TraceView[][] = []; // one inner list per stacked pane
  selected = new Set<TraceView>();

  private traces = new Map<string, TraceData>();
  private zoom = 1.0;
  private pan = 0.0;
  private colourIndex = 0;
  private unsubscribes = new Map<TraceData, () => void>();
  private onLog: EntrySink;

  version = 0;
  private listeners = new Set<() => void>();
  onPanZoomChanged: ((model: ScopeModel) => void) | null = null;

  constructor(skin: ScopeSkin = darkSkin, onLog?: EntrySink) {
    this.skin = skin;
    this.onLog = onLog ?? (() => undefined);
  }

  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private notify(): void {
    this.version++;
    this.listeners.forEach((l) => l());
  }

  get zoomValue(): number {
    return this.zoom;
  }

  get panValue(): number {
    return this.pan;
  }

  // Pan clamps to [0, 1-zoom] - the left-edge fraction ceiling (a bare [0,1]
  // clamp lets drags over-pan past the data and snap back).
  setZoomPan(zoom: number, pan: number): void {
    const z = Math.max(1e-9, Math.min(1.0, zoom));
    const p = Math.max(0.0, Math.min(1.0 - z, pan));
    if (z === this.zoom && p === this.pan) return;
    this.zoom = z;
    this.pan = p;
    this.notify();
    this.onPanZoomChanged?.(this);
  }

  // scope["name"]: get-or-create the named channel; a new channel gets a view
  // (next palette colour) in its own group.
  ensure(name: string): TraceData {
    let data = this.traces.get(name);
    if (data === undefined) {
      data = new TraceData(name);
      this.traces.set(name, data);
      const colour = this.skin.traceColours[this.colourIndex % this.skin.traceColours.length];
      this.colourIndex++;
      const view = new TraceView(data, name, colour);
      this.views.push(view);
      this.groups.push([view]);
      this.unsubscribes.set(
        data,
        data.subscribe(() => this.notify())
      );
      this.onLog(newEntry(`new trace ${name}`, "debug"));
      this.notify();
    }
    return data;
  }

  traceByName(name: string): TraceData | undefined {
    return this.traces.get(name);
  }

  viewByName(name: string): TraceView | undefined {
    return this.views.find((v) => v.viewName === name);
  }

  get allViewNames(): string[] {
    return this.views.map((v) => v.viewName);
  }

  // Stack the named views into one pane (C# GroupViews); with colour=true the
  // group is re-coloured as a run of consecutive palette entries.
  groupViews(names: string[], colour: boolean = false): void {
    const members = names
      .map((n) => this.viewByName(n))
      .filter((v): v is TraceView => v !== undefined);
    if (members.length === 0) return;
    const memberSet = new Set(members);
    this.groups = this.groups
      .map((g) => g.filter((v) => !memberSet.has(v)))
      .filter((g) => g.length > 0);
    this.groups.push(members);
    if (colour) {
      members.forEach((v, i) => {
        v.colour = this.skin.traceColours[i % this.skin.traceColours.length];
      });
    }
    this.notify();
  }

  ungroupViews(names: string[]): void {
    const members = names
      .map((n) => this.viewByName(n))
      .filter((v): v is TraceView => v !== undefined);
    const memberSet = new Set(members);
    this.groups = this.groups
      .map((g) => g.filter((v) => !memberSet.has(v)))
      .filter((g) => g.length > 0);
    for (const v of members) {
      this.groups.push([v]);
    }
    this.notify();
  }

  // Move a whole group up/down the stack (left-drag reorder in the C#).
  moveGroup(fromIndex: number, toIndex: number): void {
    if (
      fromIndex === toIndex ||
      fromIndex < 0 ||
      fromIndex >= this.groups.length ||
      toIndex < 0 ||
      toIndex >= this.groups.length
    ) {
      return;
    }
    const [g] = this.groups.splice(fromIndex, 1);
    this.groups.splice(toIndex, 0, g);
    this.notify();
  }

  closeView(name: string): void {
    const view = this.viewByName(name);
    if (view === undefined) return;
    this.views = this.views.filter((v) => v !== view);
    this.groups = this.groups.map((g) => g.filter((v) => v !== view)).filter((g) => g.length > 0);
    this.selected.delete(view);
    // drop the TraceData when no view shows it any more
    if (!this.views.some((v) => v.data === view.data)) {
      this.unsubscribes.get(view.data)?.();
      this.unsubscribes.delete(view.data);
      this.traces.delete(view.data.name);
    }
    this.notify();
  }

  clear(): void {
    this.unsubscribes.forEach((unsub) => unsub());
    this.unsubscribes.clear();
    this.traces.clear();
    this.views = [];
    this.groups = [];
    this.selected.clear();
    this.colourIndex = 0;
    this.notify();
  }

  visibleViewGroups(): TraceView[][] {
    return this.groups
      .map((g) => g.filter((v) => v.visible))
      .filter((g) => g.length > 0);
  }

  setViewVisible(name: string, visible: boolean): void {
    const view = this.viewByName(name);
    if (view === undefined || view.visible === visible) return;
    view.visible = visible;
    this.notify();
  }

  // C# SetGroupHighLow: the vertical range is shared across a pane.
  setGroupHighLow(view: TraceView, high: number, low: number): void {
    const group = this.groups.find((g) => g.includes(view)) ?? [view];
    for (const v of group) {
      v.highestValue = high;
      v.lowestValue = low;
    }
    this.notify();
  }

  // Auto range every visible group over its currently drawn window.
  autoRangeAll(): void {
    for (const group of this.visibleViewGroups()) {
      const windows = group.map((v) =>
        v.data.isYTTrace || v.data.isFakeYT
          ? v.drawnWindow("stretch", 0, 0, this.zoom, this.pan)
          : v.drawnWindow("stretch", 0, 0, this.zoom, this.pan)
      );
      const range = autoRangeGroup(group, windows);
      if (range !== null) {
        for (const v of group) {
          v.highestValue = range.high;
          v.lowestValue = range.low;
        }
      }
    }
    this.notify();
  }

  // Align YT traces on wall clock by grouping them (C# MatchUnixTimes).
  matchUnixTimes(names: string[]): void {
    this.groupViews(names);
  }
}
