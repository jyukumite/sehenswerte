// Ports of the C# DataGridControlHistoryTest and DataGridBoundDataTest suites
// (sehenswerte DataGridControlHistory.cs / DataGridBoundData.cs).

import { GridModel } from "./GridModel";

function createTestData(): GridModel {
  return GridModel.fromStrings(
    [
      ["a", "1"],
      ["b", "2"],
      ["c", "3"],
    ],
    ["Name", "Value"]
  );
}

function make(cols: string[], ...rows: (string | null)[][]): GridModel {
  return GridModel.fromStrings(rows, cols);
}

describe("GridHistory (DataGridControlHistoryTest port)", () => {
  test("saveViewContainsInitialAction", () => {
    const bd = createTestData();
    const state = bd.saveBoundState();
    // Constructor runs showAll, so history is non-empty.
    expect(state.history.length).toBeGreaterThanOrEqual(1);
    expect(bd.filteredData.length).toBe(3);
  });

  test("filterPushesOntoStack", () => {
    const bd = createTestData();
    const before = bd.saveBoundState().history.length;

    bd.hideRowsMatching("Name", ["b"]);

    const state = bd.saveBoundState();
    // The filter pushed one snapshot, so history grows by 1
    expect(state.history.length).toBe(before + 1);
    // Live view shows only the 2 non-b rows
    expect(bd.filteredData.length).toBe(2);
  });

  test("restoreViewRestoresSavedState", () => {
    const bd = createTestData();
    bd.hideRowsMatching("Name", ["b"]);
    const state = bd.saveBoundState();

    bd.hideRowsMatching("Name", ["c"]); // now only "a" visible
    expect(bd.filteredData.length).toBe(1);

    bd.restoreBoundState(state);
    expect(bd.filteredData.length).toBe(2); // back to a+c visible
  });

  test("undoAfterRestoreWalksHistory", () => {
    const bd = createTestData();
    bd.hideRowsMatching("Name", ["b"]);
    const state = bd.saveBoundState();

    bd.restoreBoundState(state);

    // Undo each replayed action.
    const undoDepth = state.history.length;
    for (let i = 0; i < undoDepth; i++) {
      const widths = bd.undo();
      expect(widths).not.toBeNull();
    }
    // Oldest snapshot is the initial all-visible state
    expect(bd.filteredData.length).toBe(3);
  });

  test("redoRestoresUndoneFilter", () => {
    const bd = createTestData();
    bd.hideRowsMatching("Name", ["b"]);
    expect(bd.filteredData.length).toBe(2);
    expect(bd.canRedo).toBe(false);

    bd.undo();
    expect(bd.filteredData.length).toBe(3);
    expect(bd.canRedo).toBe(true);

    bd.redo();
    expect(bd.filteredData.length).toBe(2);
    expect(bd.filteredData.some((r) => r.column(0) === "b")).toBe(false);
    expect(bd.canRedo).toBe(false);
  });

  test("hideUnselectedRedoRestoresView", () => {
    const bd = createTestData(); // rows a/1, b/2, c/3 at indices 0,1,2
    // Stack an earlier op so we exercise ordering, not just a lone hide.
    bd.hideRowsMatching("Name", ["a"]); // hides index 0; b,c visible
    expect(bd.filteredData.length).toBe(2);

    // Hide unselected: keep only index 2 ("c").
    bd.hideRowsOtherThan([2]);
    expect(bd.filteredData.length).toBe(1);
    expect(bd.filteredData[0].column(0)).toBe("c");
    expect(bd.canRedo).toBe(false);

    bd.undo();
    expect(bd.filteredData.length).toBe(2); // back to b,c
    expect(bd.canRedo).toBe(true);

    bd.redo();
    expect(bd.filteredData.length).toBe(1);
    expect(bd.filteredData[0].column(0)).toBe("c");
    expect(bd.canRedo).toBe(false);
  });

  test("hideSelectedRedoRestoresView", () => {
    const bd = createTestData(); // rows a/1, b/2, c/3 at indices 0,1,2
    bd.hideRows([1]); // hide "b"
    expect(bd.filteredData.length).toBe(2);
    expect(bd.filteredData.some((r) => r.column(0) === "b")).toBe(false);

    bd.undo();
    expect(bd.filteredData.length).toBe(3);
    expect(bd.canRedo).toBe(true);

    bd.redo();
    expect(bd.filteredData.length).toBe(2);
    expect(bd.filteredData.some((r) => r.column(0) === "b")).toBe(false);
    expect(bd.canRedo).toBe(false);
  });

  test("pushSnapshotClearsRedoStack", () => {
    const bd = createTestData();
    bd.hideRowsMatching("Name", ["b"]);
    bd.undo();
    expect(bd.canRedo).toBe(true);

    // A new operation must invalidate the redo stack - otherwise redo would
    // re-apply an action against a state it was never recorded against.
    bd.hideRowsMatching("Name", ["c"]);
    expect(bd.canRedo).toBe(false);
  });

  test("undoRedoUndoIsIdempotent", () => {
    const bd = createTestData();
    bd.hideRowsMatching("Name", ["b"]);
    const afterFilter = bd.filteredData.length;

    bd.undo();
    const afterUndo = bd.filteredData.length;
    bd.redo();
    const afterRedo = bd.filteredData.length;
    bd.undo();
    const afterUndo2 = bd.filteredData.length;

    expect(afterFilter).toBe(2);
    expect(afterUndo).toBe(3);
    expect(afterRedo).toBe(2);
    expect(afterUndo2).toBe(3);
    expect(bd.canRedo).toBe(true);
  });

  test("redoOnEmptyStackReturnsNull", () => {
    const bd = createTestData();
    expect(bd.redo()).toBeNull();
  });

  test("decimateRedoes", () => {
    const rows: (string | null)[][] = [];
    for (let loop = 0; loop < 30; loop++) {
      rows.push([String(loop), "x"]);
    }
    const bd = GridModel.fromStrings(rows, ["Name", "Value"]);

    bd.decimate(10);
    const afterDecimate = bd.filteredData.length;
    expect(afterDecimate).toBe(3); // rows 0, 10, 20 visible

    bd.undo();
    expect(bd.filteredData.length).toBe(30);

    bd.redo();
    expect(bd.filteredData.length).toBe(afterDecimate);
  });

  test("transposeForwardThenReverseRoundTrips", () => {
    const bd = createTestData();
    // start: 3 rows, 2 cols (Name, Value)
    expect(bd.filteredData.length).toBe(3);

    bd.transpose();
    // after forward: 2 rows (one per source col), 4 cols (headers + row 1..3)
    expect(bd.filteredData.length).toBe(2);
    expect(bd.filteredData[0].column(0)).toBe("Name");
    expect(bd.filteredData[0].column(1)).toBe("a");
    expect(bd.filteredData[0].column(2)).toBe("b");
    expect(bd.filteredData[0].column(3)).toBe("c");
    expect(bd.filteredData[1].column(0)).toBe("Value");
    expect(bd.filteredData[1].column(1)).toBe("1");
    expect(bd.filteredData[1].column(2)).toBe("2");
    expect(bd.filteredData[1].column(3)).toBe("3");

    // second click detects shape and reverses
    bd.transpose();
    expect(bd.filteredData.length).toBe(3);
    expect(bd.filteredData[0].column(0)).toBe("a");
    expect(bd.filteredData[0].column(1)).toBe("1");
    expect(bd.filteredData[1].column(0)).toBe("b");
    expect(bd.filteredData[1].column(1)).toBe("2");
    expect(bd.filteredData[2].column(0)).toBe("c");
    expect(bd.filteredData[2].column(1)).toBe("3");
  });

  test("transposeIsUndoable", () => {
    const bd = createTestData();
    bd.transpose();
    expect(bd.filteredData.length).toBe(2); // forward result

    bd.undo();
    // undo of transpose restores visible content equivalent to the
    // pre-transpose view.
    expect(bd.filteredData.length).toBe(3);
    expect(bd.filteredData[0].column(0)).toBe("a");
    expect(bd.filteredData[0].column(1)).toBe("1");
  });

  test("transposeRedoRestoresTransposedView", () => {
    const bd = createTestData();
    bd.transpose();
    bd.undo();
    expect(bd.filteredData.length).toBe(3);
    expect(bd.canRedo).toBe(true);

    bd.redo();
    expect(bd.filteredData.length).toBe(2);
    expect(bd.filteredData[0].column(0)).toBe("Name");
    expect(bd.filteredData[1].column(0)).toBe("Value");
  });

  test("undoFilterAfterUndoTransposeRestoresOriginalCounts", () => {
    // Repro for "164/1" bug: filter, transpose, undo (transpose), undo
    // (filter) must put both unfilteredData and filteredData back in sync
    // with the pre-filter state.
    const bd = createTestData();
    bd.hideRowsMatching("Name", ["a", "b"]); // FD=1 (only "c")
    expect(bd.filteredData.length).toBe(1);
    expect(bd.unfilteredData.length).toBe(3);

    bd.transpose();
    bd.undo(); // undo transpose
    expect(bd.filteredData.length).toBe(1);
    expect(bd.unfilteredData.length).toBe(3);

    bd.undo(); // undo filter
    expect(bd.filteredData.length).toBe(3);
    expect(bd.unfilteredData.length).toBe(3);
  });

  test("replayOnDifferentData", () => {
    // The headline scenario: save a recipe of actions on one dataset, then
    // restore (replay) on a similar-but-not-identical dataset. The actions
    // reference column names and string values, so they apply meaningfully.
    const original = GridModel.fromStrings(
      [
        ["x", "10"],
        ["y", "20"],
        ["z", "0"],
        ["x", "0"],
      ],
      ["Name", "Value"]
    );

    // Sort by Name, then hide rows where Value matches "0".
    original.sortByColumn("Name", "asc");
    original.hideRowsMatching("Value", ["0"]);
    const recipe = original.saveBoundState();

    // Different rows, same column shape; one row also has Value="0".
    const fresh = GridModel.fromStrings(
      [
        ["alpha", "5"],
        ["bravo", "0"],
        ["charlie", "7"],
      ],
      ["Name", "Value"]
    );

    fresh.restoreBoundState(recipe);

    // Replay should have sorted by Name (alpha/bravo/charlie are already
    // alphabetical) and hidden the "0" row, leaving alpha and charlie.
    expect(fresh.filteredData.length).toBe(2);
    expect(fresh.filteredData[0].column(0)).toBe("alpha");
    expect(fresh.filteredData[1].column(0)).toBe("charlie");

    // Undo: restore the row that was hidden by hideRowsMatching.
    fresh.undo();
    expect(fresh.filteredData.length).toBe(3);

    // Undo again: unsort - back to insertion order.
    fresh.undo();
    expect(fresh.filteredData[0].column(0)).toBe("alpha");
    expect(fresh.filteredData[1].column(0)).toBe("bravo");
    expect(fresh.filteredData[2].column(0)).toBe("charlie");
  });
});

describe("GridModel (DataGridBoundDataTest port)", () => {
  test("insertColumnPlacesHeaderAndShiftsValues", () => {
    const bd = make(["key", "val"], ["a", "1"], ["b", "2"]);
    bd.insertColumn("mid", 1, ["x", "y"]);

    expect(bd.columnNames).toEqual(["key", "mid", "val"]);
    expect(bd.unfilteredData[0].column(0)).toBe("a");
    expect(bd.unfilteredData[0].column(1)).toBe("x");
    expect(bd.unfilteredData[0].column(2)).toBe("1");
    expect(bd.unfilteredData[1].column(1)).toBe("y");
  });

  test("insertColumnAtCountAppends", () => {
    const bd = make(["key", "val"], ["a", "1"]);
    bd.insertColumn("tail", bd.columnNames.length, ["z"]);

    expect(bd.columnNames).toEqual(["key", "val", "tail"]);
    expect(bd.unfilteredData[0].column(2)).toBe("z");
  });

  test("removeColumnDropsHeaderAndShiftsDataLeft", () => {
    const bd = make(["key", "val"], ["a", "1"], ["b", "2"]);
    bd.insertColumn("mid", 1, ["x", "y"]);

    const removed = bd.removeColumn("mid");

    expect(removed).toBe(true);
    expect(bd.columnNames).toEqual(["key", "val"]);
    expect(bd.unfilteredData[0].column(0)).toBe("a");
    expect(bd.unfilteredData[0].column(1)).toBe("1");
  });

  test("removeColumnReturnsFalseWhenMissing", () => {
    const bd = make(["key", "val"], ["a", "1"]);
    expect(bd.removeColumn("nope")).toBe(false);
    expect(bd.columnNames.length).toBe(2);
  });

  test("appendColumnValueDelegatesToInsertAtEnd", () => {
    const bd = make(["key", "val"], ["a", "1"]);
    const row = bd.unfilteredData[0];
    const prev = row.count;

    row.appendColumnValue("appended");

    expect(row.count).toBe(prev + 1);
    expect(row.column(prev)).toBe("appended");
    // existing values untouched
    expect(row.column(0)).toBe("a");
    expect(row.column(1)).toBe("1");
  });

  test("hideNotFirstUniqueInsertsCountColumnAndCollapsesDuplicates", () => {
    const bd = make(
      ["key", "val"],
      ["a", "1"],
      ["b", "2"],
      ["a", "3"],
      ["a", "4"],
      ["b", "5"]
    );

    bd.hideNotFirstUnique("key");

    expect(bd.columnNames).toEqual(["key", "key count", "val"]);
    expect(bd.filteredData.length).toBe(2);

    expect(bd.filteredData[0].column(0)).toBe("a");
    expect(bd.filteredData[0].column(1)).toBe("3");
    expect(bd.filteredData[0].column(2)).toBe("1");

    expect(bd.filteredData[1].column(0)).toBe("b");
    expect(bd.filteredData[1].column(1)).toBe("2");
    expect(bd.filteredData[1].column(2)).toBe("2");
  });

  test("hideNotFirstUniqueLeavesExistingCountColumnAlone", () => {
    const bd = make(["key", "val"], ["a", "1"], ["a", "2"], ["b", "3"]);

    bd.hideNotFirstUnique("key");
    const afterFirst = bd.columnNames.length;

    bd.hideNotFirstUnique("key"); // already unique - no fresh column should appear

    expect(bd.columnNames.length).toBe(afterFirst);
    expect(bd.columnNames.filter((c) => c === "key count").length).toBe(1);
  });

  test("undoHideNotFirstUniqueRemovesCountColumn", () => {
    const bd = make(["key", "val"], ["a", "1"], ["a", "2"], ["b", "3"]);

    bd.hideNotFirstUnique("key");
    expect(bd.columnNames.includes("key count")).toBe(true);

    bd.undo();

    expect(bd.columnNames).toEqual(["key", "val"]);
    expect(bd.filteredData.length).toBe(3);
    // row data shape restored - col(1) should be the original val again
    expect(bd.filteredData[0].column(1)).toBe("1");
    expect(bd.filteredData[1].column(1)).toBe("2");
    expect(bd.filteredData[2].column(1)).toBe("3");
  });

  test("redoHideNotFirstUniqueRestoresCountColumn", () => {
    const bd = make(["key", "val"], ["a", "1"], ["a", "2"], ["b", "3"]);

    bd.hideNotFirstUnique("key");
    bd.undo();
    bd.redo();

    expect(bd.columnNames).toEqual(["key", "key count", "val"]);
    expect(bd.filteredData.length).toBe(2);
    expect(bd.filteredData[0].column(1)).toBe("2"); // a appears 2x
    expect(bd.filteredData[1].column(1)).toBe("1"); // b appears 1x
  });

  test("coloursShiftWithInsertAndRemove", () => {
    const bd = make(["key", "val"], ["a", "1"]);
    const row = bd.unfilteredData[0];
    row.cellColour(1, "red");

    bd.insertColumn("mid", 1, ["x"]);
    // val moved from col 1 to col 2 - red should follow it
    expect(row.colours).not.toBeNull();
    expect(row.colours![2]).toBe("red");
    expect(row.colours![1]).toBeNull();

    bd.removeColumn("mid");
    expect(row.colours![1]).toBe("red");
  });

  test("diffsShiftWithInsertAndRemove", () => {
    const bd = make(["key", "val"], ["a", "1"]);
    const row = bd.unfilteredData[0];
    const marker = { tag: "diff-marker" };
    row.cellDiffs(1, marker);

    bd.insertColumn("mid", 1, ["x"]);
    expect(row.diffs).not.toBeNull();
    expect(row.diffs![2]).toBe(marker);
    expect(row.diffs![1]).toBeNull();

    bd.removeColumn("mid");
    expect(row.diffs![1]).toBe(marker);
  });
});

describe("GridModel port-specific coverage", () => {
  test("appendRowsAppendsVisibleRowsAndRespectsFilter", () => {
    const bd = createTestData();
    bd.hideRowsMatching("Name", ["b"]);
    expect(bd.filteredData.length).toBe(2);

    bd.appendRows([["d", "4"]]);
    expect(bd.unfilteredData.length).toBe(4);
    expect(bd.filteredData.length).toBe(3);
    expect(bd.filteredData[2].column(0)).toBe("d");
  });

  test("cumulativeSortIsStableAcrossKeys", () => {
    // Sort by Value then by Group: equal Group rows keep Value order.
    const bd = GridModel.fromStrings(
      [
        ["g1", "30"],
        ["g2", "10"],
        ["g1", "20"],
        ["g2", "40"],
      ],
      ["Group", "Value"]
    );
    bd.sortByColumn("Value", "asc"); // 10,20,30,40
    bd.sortByColumn("Group", "asc"); // g1(20,30), g2(10,40) - stable
    expect(bd.filteredData.map((r) => r.column(1))).toEqual([
      "20",
      "30",
      "10",
      "40",
    ]);
  });

  test("toggleSortByColumnAlternatesDirection", () => {
    const bd = createTestData();
    bd.toggleSortByColumn("Name");
    expect(bd.primarySortKey()).toEqual({ columnName: "Name", direction: "asc" });
    bd.toggleSortByColumn("Name");
    expect(bd.primarySortKey()).toEqual({ columnName: "Name", direction: "desc" });
    expect(bd.filteredData[0].column(0)).toBe("c");
  });

  test("naturalSortOrdersNumericStrings", () => {
    const bd = GridModel.fromStrings(
      [["item10"], ["item2"], ["item1"]],
      ["Name"]
    );
    bd.sortByColumn("Name", "asc");
    expect(bd.filteredData.map((r) => r.column(0))).toEqual([
      "item1",
      "item2",
      "item10",
    ]);
  });

  test("doubleGridSortsNumerically", () => {
    const bd = GridModel.fromDoubles([[10], [2], [33]], ["Value"]);
    bd.sortByColumn("Value", "asc");
    expect(bd.filteredData.map((r) => r.columnDouble(0))).toEqual([2, 10, 33]);
    bd.sortByColumn("Value", "desc");
    expect(bd.filteredData.map((r) => r.columnDouble(0))).toEqual([33, 10, 2]);
  });

  test("regexShowKeepsOnlyMatches", () => {
    const bd = createTestData();
    bd.showRowsMatchingRegex("^[ab]$", "Name");
    expect(bd.filteredData.length).toBe(2);
    bd.undo();
    expect(bd.filteredData.length).toBe(3);
  });

  test("regexTreatsNullCellAsLiteralNull", () => {
    const bd = GridModel.fromStrings([[null], ["x"]], ["Name"]);
    bd.showRowsMatchingRegex("null", "Name");
    expect(bd.filteredData.length).toBe(1);
    expect(bd.filteredData[0].column(0)).toBeNull();
  });

  test("highlightIsUndoable", () => {
    const bd = createTestData();
    bd.addHighlight("abc");
    expect(bd.highlights).toEqual(["abc"]);
    bd.undo();
    expect(bd.highlights).toEqual([]);
    bd.redo();
    expect(bd.highlights).toEqual(["abc"]);
  });

  test("moveColumnUndoRestoresOrder", () => {
    const bd = make(["a", "b", "c"], ["1", "2", "3"]);
    bd.moveColumn("c", ""); // leftmost
    expect(bd.columnNames).toEqual(["c", "a", "b"]);
    expect(bd.unfilteredData[0].strings).toEqual(["3", "1", "2"]);

    bd.undo();
    expect(bd.columnNames).toEqual(["a", "b", "c"]);
    expect(bd.unfilteredData[0].strings).toEqual(["1", "2", "3"]);

    bd.redo();
    expect(bd.columnNames).toEqual(["c", "a", "b"]);
  });

  test("splitColumnInsertsAfterSourceAndUndoes", () => {
    const bd = make(["json", "other"], ['{"a":1}', "x"], ['{"a":2}', "y"]);
    bd.splitColumn("json", (source) => [
      { header: `${source}::a`, values: ["1", "2"] },
    ]);
    expect(bd.columnNames).toEqual(["json", "json::a", "other"]);
    expect(bd.unfilteredData[0].column(1)).toBe("1");

    bd.undo();
    expect(bd.columnNames).toEqual(["json", "other"]);

    bd.redo();
    expect(bd.columnNames).toEqual(["json", "json::a", "other"]);
    expect(bd.unfilteredData[1].column(1)).toBe("2");
  });

  test("columnResizeUndoReturnsPriorWidth", () => {
    const bd = createTestData();
    bd.recordColumnResize("Name", 150);
    bd.recordColumnResize("Name", 220);

    const widths = bd.undo();
    expect(widths).toEqual([{ name: "Name", width: 150 }]);

    const widths2 = bd.undo();
    expect(widths2).toEqual([{ name: "Name", width: 100 }]); // default
  });

  test("hideRowsAboveBelowUsesAnchorOnReplay", () => {
    // Sorted data; hide-below anchored to the sorted column value replays
    // onto different data by finding the nearest value.
    const original = GridModel.fromStrings(
      [["10"], ["20"], ["30"], ["40"]],
      ["Value"]
    );
    original.sortByColumn("Value", "asc");
    original.hideRowsBelow(1); // keep 10,20
    expect(original.filteredData.length).toBe(2);
    const recipe = original.saveBoundState();

    const fresh = GridModel.fromStrings(
      [["15"], ["25"], ["35"]],
      ["Value"]
    );
    fresh.restoreBoundState(recipe);
    // anchor value 20 -> nearest is 15 or 25 (15 is nearer: dist 5 vs 5...
    // ties resolve to the first best, which is 15) -> keep rows up to it
    expect(fresh.filteredData.length).toBeLessThan(3);
  });
});
