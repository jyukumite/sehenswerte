// Behavioural tests for the ScopeModel/TraceView layer (fresh; the C#
// SehensControl tests are WinForms-bound).

import { ScopeModel } from "./ScopeModel";
import { autoRangeGroup } from "./TraceView";

function ramp(n: number): number[] {
  return Array.from({ length: n }, (_, i) => i);
}

describe("ScopeModel", () => {
  test("ensure creates a trace + view + own group, cycling colours", () => {
    const m = new ScopeModel();
    m.ensure("a").update(ramp(10));
    m.ensure("b").update(ramp(10));
    expect(m.views.length).toBe(2);
    expect(m.groups.length).toBe(2);
    expect(m.views[0].colour).not.toBe(m.views[1].colour);
    // second ensure of the same name returns the same data
    expect(m.ensure("a")).toBe(m.traceByName("a"));
    expect(m.views.length).toBe(2);
  });

  test("groupViews stacks members into one pane", () => {
    const m = new ScopeModel();
    m.ensure("a").update(ramp(10));
    m.ensure("b").update(ramp(10));
    m.ensure("c").update(ramp(10));
    m.groupViews(["a", "c"]);
    expect(m.groups.length).toBe(2);
    const grouped = m.groups.find((g) => g.length === 2);
    expect(grouped?.map((v) => v.viewName)).toEqual(["a", "c"]);

    m.ungroupViews(["a", "c"]);
    expect(m.groups.length).toBe(3);
  });

  test("setZoomPan clamps pan to the left-edge ceiling [0, 1-zoom]", () => {
    const m = new ScopeModel();
    m.setZoomPan(0.25, 0.9);
    expect(m.zoomValue).toBe(0.25);
    expect(m.panValue).toBeCloseTo(0.75, 12);
    m.setZoomPan(1.0, 0.5);
    expect(m.panValue).toBe(0);
  });

  test("closeView drops orphaned trace data", () => {
    const m = new ScopeModel();
    m.ensure("a").update(ramp(10));
    m.closeView("a");
    expect(m.views.length).toBe(0);
    expect(m.traceByName("a")).toBeUndefined();
  });

  test("visibleViewGroups drops hidden views and empty groups", () => {
    const m = new ScopeModel();
    m.ensure("a").update(ramp(10));
    m.ensure("b").update(ramp(10));
    m.setViewVisible("a", false);
    const groups = m.visibleViewGroups();
    expect(groups.length).toBe(1);
    expect(groups[0][0].viewName).toBe("b");
  });

  test("change notification bumps version on data updates", () => {
    const m = new ScopeModel();
    const trace = m.ensure("a");
    const before = m.version;
    trace.update(ramp(5));
    expect(m.version).toBeGreaterThan(before);
  });
});

describe("TraceView", () => {
  test("drawnWindow stretch slices by zoom/pan fractions", () => {
    const m = new ScopeModel();
    const view = (m.ensure("a").update(ramp(100)), m.viewByName("a")!);
    const win = view.drawnWindow("stretch", 0, 0, 0.5, 0.25);
    expect(win.first).toBe(25);
    expect(win.count).toBe(50);
    expect(win.leftValue).toBe(25);
    expect(win.rightValue).toBe(75);
  });

  test("drawnWindow valueAlign slices to the shared value window", () => {
    const m = new ScopeModel();
    const data = m.ensure("a");
    data.update(ramp(11));
    data.setHorizontalAffine(0, 10, "rpm"); // values 0..110
    const view = m.viewByName("a")!;
    const win = view.drawnWindow("valueAlign", 30, 70, 1, 0);
    expect(win.first).toBe(3);
    expect(win.leftValue).toBe(30);
    expect(win.rightValue).toBeGreaterThanOrEqual(70);
  });

  test("groupMember reports the full-extent axis values", () => {
    const m = new ScopeModel();
    const data = m.ensure("a");
    data.update(ramp(11));
    data.setHorizontalAffine(0, 10, "rpm");
    const member = m.viewByName("a")!.groupMember();
    expect(member.kind).toBe("affine");
    expect(member.unit).toBe("rpm");
    expect(member.left).toBe(0);
    expect(member.right).toBe(110); // one-past-last, like DrawnExtents
  });

  test("autoRangeGroup pads 10% and rounds outward to tidy numbers", () => {
    const m = new ScopeModel();
    const data = m.ensure("a");
    data.update([0, 50, 100]);
    const view = m.viewByName("a")!;
    const win = view.drawnWindow("stretch", 0, 0, 1, 0);
    const range = autoRangeGroup([view], [win]);
    expect(range).not.toBeNull();
    expect(range!.high).toBeGreaterThanOrEqual(110);
    expect(range!.low).toBeLessThanOrEqual(-10);
  });

  test("autoRangeGroup handles flat traces", () => {
    const m = new ScopeModel();
    const data = m.ensure("flat");
    data.update([5, 5, 5]);
    const view = m.viewByName("flat")!;
    const win = view.drawnWindow("stretch", 0, 0, 1, 0);
    const range = autoRangeGroup([view], [win]);
    expect(range).not.toBeNull();
    expect(range!.high).toBeGreaterThan(range!.low);
  });
});
