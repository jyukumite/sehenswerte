// Rendered smoke tests for the DataGrid component: verifies the component
// tree mounts, the status strip reflects model state, and header/status
// interactions drive the model. jsdom has no layout, so the virtualizer is
// fed a fixed viewport height.

import React from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { DataGrid, DataGridHandle } from "./DataGrid";
import { GridModel } from "./GridModel";

// default geometry the scroll accessors are measured against: fontSize 13 ->
// rowHeight 22, headerHeight 24, so a 400px box shows 376px of rows
const ROW_HEIGHT = 22;

function makeModel(): GridModel {
  return GridModel.fromStrings(
    [
      ["a", "1"],
      ["b", "2"],
      ["c", "3"],
    ],
    ["Name", "Value"]
  );
}

beforeAll(() => {
  // give the virtualizer a viewport: @tanstack/virtual-core measures the
  // scroll element via offsetWidth/offsetHeight, which jsdom reports as 0
  Object.defineProperty(HTMLElement.prototype, "offsetHeight", {
    configurable: true,
    get(this: HTMLElement) {
      return this.classList?.contains("sw-grid-scroll") ? 400 : 20;
    },
  });
  Object.defineProperty(HTMLElement.prototype, "offsetWidth", {
    configurable: true,
    get() {
      return 800;
    },
  });
});

test("renders headers, cells, and the row count", () => {
  render(<DataGrid model={makeModel()} />);
  expect(screen.getByText("Name")).toBeInTheDocument();
  expect(screen.getByText("Value")).toBeInTheDocument();
  expect(screen.getByText(/3 rows/)).toBeInTheDocument();
  expect(screen.getByText("a")).toBeInTheDocument();
  expect(screen.getByText("c")).toBeInTheDocument();
});

test("header click sorts and shows the glyph; second click reverses", () => {
  const model = makeModel();
  render(<DataGrid model={model} />);
  // jsdom rects are zero-sized, so clientX must sit left of the zero-width
  // resize edge for the mousedown to register as a click
  const header = screen.getByText("Name");
  fireEvent.mouseDown(header, { button: 0, clientX: -50 });
  fireEvent.mouseUp(document, { clientX: -50 });
  expect(model.primarySortKey()).toEqual({ columnName: "Name", direction: "asc" });
  fireEvent.mouseDown(header, { button: 0, clientX: -50 });
  fireEvent.mouseUp(document, { clientX: -50 });
  expect(model.primarySortKey()).toEqual({ columnName: "Name", direction: "desc" });
  expect(model.filteredData[0].column(0)).toBe("c");
});

test("decimate and undo through the status strip", () => {
  const model = GridModel.fromStrings(
    Array.from({ length: 30 }, (_, i) => [String(i)]),
    ["N"]
  );
  render(<DataGrid model={model} />);
  fireEvent.click(screen.getByText("Decimate"));
  expect(model.filteredData.length).toBe(3);
  expect(screen.getByText(/3\/30 rows/)).toBeInTheDocument();
  fireEvent.click(screen.getByText("Undo"));
  expect(model.filteredData.length).toBe(30);
});

test("cell selection enables hide selected, which hides the row", () => {
  const model = makeModel();
  render(<DataGrid model={model} />);
  const cell = screen.getByText("b");
  fireEvent.mouseDown(cell, { button: 0 });
  fireEvent.mouseUp(document);
  fireEvent.click(screen.getByText("Hide Selected"));
  expect(model.filteredData.length).toBe(2);
  expect(model.filteredData.some((r) => r.column(0) === "b")).toBe(false);
});

test("null cells render as italic null", () => {
  const model = GridModel.fromStrings([[null, "x"]], ["A", "B"]);
  render(<DataGrid model={model} />);
  const nullSpan = screen.getByText("null");
  expect(nullSpan).toHaveClass("sw-null");
});

test("masked columns render bullets", () => {
  const model = GridModel.fromStrings([["secret", "plain"]], ["Pw", "B"]);
  render(<DataGrid model={model} maskColumns={["Pw"]} />);
  expect(screen.queryByText("secret")).not.toBeInTheDocument();
  expect(screen.getByText("•••••")).toBeInTheDocument();
});

// ---- scroll accessors (C# ScrollRowToTop / FirstVisibleRow / VisibleRowCount) ----

// jsdom reports scrollTop/clientHeight as 0 and drops scrollTop assignments,
// which would make every assertion below pass on any arithmetic - back them
// with real per-element storage.
function mockScrollBox(root: HTMLElement, clientHeight: number): HTMLElement {
  const el = root.querySelector(".sw-grid-scroll") as HTMLElement;
  let top = 0;
  Object.defineProperty(el, "scrollTop", {
    configurable: true,
    get: () => top,
    set: (v: number) => {
      top = v;
    },
  });
  Object.defineProperty(el, "clientHeight", {
    configurable: true,
    get: () => clientHeight,
  });
  return el;
}

function renderRows(count: number, clientHeight: number = 400) {
  const model = GridModel.fromStrings(
    Array.from({ length: count }, (_, i) => [String(i)]),
    ["N"]
  );
  const ref = React.createRef<DataGridHandle>();
  const { container } = render(<DataGrid ref={ref} model={model} />);
  const scroll = mockScrollBox(container, clientHeight);
  return { handle: ref.current as DataGridHandle, scroll };
}

test("firstVisibleRow and visibleRowCount track scrollTop past the sticky header", () => {
  const { handle, scroll } = renderRows(100);
  expect(handle.firstVisibleRow()).toBe(0);
  // 400px box - 24px header = 376px of rows, so 17 whole plus a partial
  expect(handle.visibleRowCount()).toBe(18);

  scroll.scrollTop = 30; // one whole row plus 8px
  expect(handle.firstVisibleRow()).toBe(1);
  expect(handle.visibleRowCount()).toBe(18);

  scroll.scrollTop = 10 * ROW_HEIGHT;
  expect(handle.firstVisibleRow()).toBe(10);
});

test("visibleRowCount is bounded by the row count and a collapsed viewport", () => {
  const { handle } = renderRows(3);
  expect(handle.visibleRowCount()).toBe(3);
  const short = renderRows(100, 20); // shorter than the header
  expect(short.handle.visibleRowCount()).toBe(0);
});

test("firstVisibleRow is -1 and visibleRowCount 0 with no rows", () => {
  const { handle } = renderRows(0);
  expect(handle.firstVisibleRow()).toBe(-1);
  expect(handle.visibleRowCount()).toBe(0);
});

test("scrollRowToTop puts the row under the header and ignores out-of-range rows", () => {
  const { handle, scroll } = renderRows(100);
  handle.scrollRowToTop(5);
  expect(scroll.scrollTop).toBe(5 * ROW_HEIGHT);
  expect(handle.firstVisibleRow()).toBe(5);
  handle.scrollRowToTop(-1);
  handle.scrollRowToTop(100);
  expect(scroll.scrollTop).toBe(5 * ROW_HEIGHT);
});

test("onVerticalScroll fires only when scrollTop changes", () => {
  const model = GridModel.fromStrings(
    Array.from({ length: 100 }, (_, i) => [String(i)]),
    ["N"]
  );
  const onVerticalScroll = jest.fn();
  const { container } = render(
    <DataGrid model={model} onVerticalScroll={onVerticalScroll} />
  );
  const scroll = mockScrollBox(container, 400);

  scroll.scrollTop = 2 * ROW_HEIGHT;
  fireEvent.scroll(scroll);
  expect(onVerticalScroll).toHaveBeenCalledTimes(1);

  // a horizontal-only scroll leaves scrollTop alone (header drag edge-scroll)
  fireEvent.scroll(scroll);
  expect(onVerticalScroll).toHaveBeenCalledTimes(1);

  scroll.scrollTop = 3 * ROW_HEIGHT;
  fireEvent.scroll(scroll);
  expect(onVerticalScroll).toHaveBeenCalledTimes(2);
});
