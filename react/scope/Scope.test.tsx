// Rendered tests for the Scope component. jsdom has neither layout nor a canvas
// context, so the paint host is given a size and getContext returns a recording
// stub: these assert THAT a paint happened and how it is scheduled, never what it
// looks like (the geometry/projection maths is covered by the pure-module suites).

import React from "react";
import { act, render } from "@testing-library/react";
import { Scope } from "./Scope";
import { ScopeModel } from "./ScopeModel";

const HOST_W = 600;
const HOST_H = 400;

// canvas ops seen since the last reset, e.g. "fillRect" for the background fill
let painted: string[] = [];

function stubContext(): CanvasRenderingContext2D {
  const assigned: Record<string, unknown> = {};
  return new Proxy(assigned, {
    get(target, prop) {
      if (typeof prop !== "string") return undefined;
      if (prop === "measureText") return () => ({ width: 10 });
      if (prop in target) return target[prop]; // fillStyle etc. read back
      return (): void => {
        painted.push(prop);
      };
    },
    set(target, prop, value) {
      if (typeof prop === "string") target[prop] = value;
      return true;
    },
  }) as unknown as CanvasRenderingContext2D;
}

// A faithful rAF queue: requestAnimationFrame hands back a non-zero handle and
// cancelAnimationFrame really drops the callback, so a cancelled frame never runs.
// Both halves matter - a no-op cancel would let a stale frame paint and hide the
// scheduling bug the remount tests below are here to catch.
let frames = new Map<number, FrameRequestCallback>();
let nextHandle = 1;

const realGetContext = HTMLCanvasElement.prototype.getContext;

function flushFrames(): void {
  const queued = [...frames.values()];
  frames.clear();
  act(() => {
    queued.forEach((cb) => cb(0));
  });
}

beforeAll(() => {
  Object.defineProperty(HTMLElement.prototype, "clientWidth", {
    configurable: true,
    get(this: HTMLElement) {
      return this.classList?.contains("sw-scope-paint") ? HOST_W : 0;
    },
  });
  Object.defineProperty(HTMLElement.prototype, "clientHeight", {
    configurable: true,
    get(this: HTMLElement) {
      return this.classList?.contains("sw-scope-paint") ? HOST_H : 0;
    },
  });
  HTMLCanvasElement.prototype.getContext = (() => stubContext()) as never;
});

// Put the environment back: these patch shared prototypes and window, and jsdom's
// own teardown runs after this file (leaving them replaced makes jest complain
// about a worker that would not exit).
afterAll(() => {
  jest.restoreAllMocks();
  Reflect.deleteProperty(HTMLElement.prototype, "clientWidth");
  Reflect.deleteProperty(HTMLElement.prototype, "clientHeight");
  HTMLCanvasElement.prototype.getContext = realGetContext;
});

// The rAF spies belong here and not in beforeAll: the host's jest config sets
// resetMocks, which strips mock implementations before every test, and a
// requestAnimationFrame stubbed to return undefined paints nothing while looking
// like a component bug.
beforeEach(() => {
  painted = [];
  frames = new Map();
  jest.spyOn(window, "requestAnimationFrame").mockImplementation((cb: FrameRequestCallback) => {
    const handle = nextHandle++;
    frames.set(handle, cb);
    return handle;
  });
  jest.spyOn(window, "cancelAnimationFrame").mockImplementation((handle: number) => {
    frames.delete(handle);
  });
});

function makeModel(): ScopeModel {
  const model = new ScopeModel();
  const n = 500;
  const samples = new Float64Array(n);
  for (let i = 0; i < n; i++) samples[i] = Math.sin(i / 10);
  model.ensure("trace").update(samples);
  model.autoRangeAll();
  return model;
}

function canvasOf(container: HTMLElement): HTMLCanvasElement {
  const canvas = container.querySelector("canvas");
  if (canvas === null) throw new Error("no canvas rendered");
  return canvas;
}

test("paints on mount, sizing the backing store to the host box", () => {
  const { container } = render(<Scope model={makeModel()} />);
  const canvas = canvasOf(container);
  expect(canvas.width).not.toBe(HOST_W); // still jsdom's default, no frame yet
  flushFrames();
  expect(canvas.width).toBe(HOST_W); // devicePixelRatio is 1 under jsdom
  expect(canvas.height).toBe(HOST_H);
  expect(painted).toContain("fillRect"); // background
  expect(painted).toContain("stroke"); // gridlines and the trace
});

test("still paints after a remount - StrictMode mounts, unmounts, remounts", () => {
  // The regression: the effect cancels its pending frame on cleanup, and the
  // frame handle doubles as the "already scheduled" guard. Leave it set and every
  // later repaint() returns early, so the canvas never paints again - blank scope,
  // no error anywhere. StrictMode hits this on the very first paint.
  const { container } = render(
    <React.StrictMode>
      <Scope model={makeModel()} />
    </React.StrictMode>
  );
  flushFrames();
  expect(canvasOf(container).width).toBe(HOST_W);
  expect(painted).toContain("fillRect");
});

test("repaints on model changes after a remount", () => {
  const model = makeModel();
  const first = render(<Scope model={model} />);
  flushFrames();
  first.unmount();

  const { container } = render(<Scope model={model} />);
  flushFrames();
  painted = [];
  act(() => {
    model.setZoomPan(0.5, 0.1);
  });
  flushFrames();
  expect(painted).toContain("fillRect");
  expect(canvasOf(container).width).toBe(HOST_W);
});