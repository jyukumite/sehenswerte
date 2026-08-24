// Tests for the clipboard-format port of sehenswerte DataGridBoundData.cs
// (SelectedCellsToClipboardFormats / StringArrayToCsv / StringArrayToHtml).

import {
  buildSelectedCellsData,
  selectionBoundingCells,
  writeSelectionToClipboard,
} from "./clipboard";

describe("buildSelectedCellsData - TSV", () => {
  test("single data row omits the header row", () => {
    const data = buildSelectedCellsData(["a", "b"], [["1", "2"]]);
    expect(data.tsv).toBe("1\t2");
  });

  test("multiple data rows prepend the tab-joined header row", () => {
    const data = buildSelectedCellsData(
      ["a", "b"],
      [
        ["1", "2"],
        ["3", "4"],
      ]
    );
    expect(data.tsv).toBe("a\tb\n1\t2\n3\t4");
  });

  test("null cells render as empty strings", () => {
    const data = buildSelectedCellsData(
      ["a", "b"],
      [
        ["1", null],
        [null, "4"],
      ]
    );
    expect(data.tsv).toBe("a\tb\n1\t\n\t4");
  });
});

describe("buildSelectedCellsData - CSV", () => {
  test("always includes the header row, even for a single data row", () => {
    const data = buildSelectedCellsData(["a", "b"], [["1", "2"]]);
    expect(data.csv).toBe("a,b\n1,2");
  });

  test("delegates quoting to rowToCsvText", () => {
    const data = buildSelectedCellsData(["a", "b"], [["x,y", 'he said "hi"']]);
    expect(data.csv).toBe('a,b\n"x,y","he said ""hi"""');
  });
});

describe("buildSelectedCellsData - HTML", () => {
  test("escapes & < > \" in headers and cells", () => {
    const data = buildSelectedCellsData(
      ["a&b"],
      [['<td class="x"> & more >']]
    );
    expect(data.html).toContain("<th>a&amp;b</th>");
    expect(data.html).toContain(
      "<td>&lt;td class=&quot;x&quot;&gt; &amp; more &gt;</td>"
    );
  });

  test("produces thead/tbody structure with empty cells for null", () => {
    const data = buildSelectedCellsData(
      ["a", "b"],
      [
        ["1", null],
        ["3", "4"],
      ]
    );
    expect(data.html).toContain('<table border="1">');
    expect(data.html).toContain("<thead><tr>");
    expect(data.html).toContain("</tr></thead>");
    expect(data.html).toContain("<tbody>");
    expect(data.html).toContain("<td></td>");
    // One header row plus two body rows.
    expect(data.html.match(/<tr>/g)).toHaveLength(3);
  });
});

describe("selectionBoundingCells", () => {
  const columns = ["c0", "c1", "c2", "c3"];

  test("covers distinct rows x distinct columns with nulls at unselected intersections", () => {
    const { headers, strings } = selectionBoundingCells(
      [
        { row: 0, col: 0, value: "x" },
        { row: 2, col: 2, value: "y" },
      ],
      columns
    );
    expect(headers).toEqual(["c0", "c2"]);
    expect(strings).toEqual([
      ["x", null],
      [null, "y"],
    ]);
  });

  test("orders rows and columns ascending regardless of input order", () => {
    const { headers, strings } = selectionBoundingCells(
      [
        { row: 5, col: 3, value: "d" },
        { row: 1, col: 1, value: "a" },
        { row: 5, col: 1, value: "c" },
        { row: 1, col: 3, value: "b" },
      ],
      columns
    );
    expect(headers).toEqual(["c1", "c3"]);
    expect(strings).toEqual([
      ["a", "b"],
      ["c", "d"],
    ]);
  });

  test("sparse selection flows through to empty TSV cells", () => {
    const { headers, strings } = selectionBoundingCells(
      [
        { row: 0, col: 1, value: "top" },
        { row: 3, col: 2, value: "bottom" },
      ],
      columns
    );
    const data = buildSelectedCellsData(headers, strings);
    expect(data.tsv).toBe("c1\tc2\ntop\t\n\tbottom");
  });
});

describe("writeSelectionToClipboard", () => {
  afterEach(() => {
    delete (navigator as any).clipboard;
  });

  test("falls back to writeText with the TSV when ClipboardItem is unavailable", async () => {
    const writeText = jest.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText },
      configurable: true,
    });

    const data = buildSelectedCellsData(["a"], [["1"], ["2"]]);
    await writeSelectionToClipboard(data);

    expect(writeText).toHaveBeenCalledTimes(1);
    expect(writeText).toHaveBeenCalledWith(data.tsv);
  });
});
