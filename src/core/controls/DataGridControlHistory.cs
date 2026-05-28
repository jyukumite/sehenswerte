using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.ComponentModel;

namespace SehensWerte.Controls
{
    public class DataGridControlHistory
    {
        public class Snapshot
        {
            // The filter/sort/resize operation that produced this snapshot
            public FilterAction? Action { get; set; }

            // original Index values from UnfilteredData
            public List<int> VisibleRows { get; set; } = new();

            // direct row refs for fast undo
            public List<DataGridControl.BoundDataRow>? VisibleRowRefs;

            // Pre-Transpose data
            public List<DataGridControl.BoundDataRow>? PreTransposeUnfiltered;
            public List<string>? PreTransposeColumnNames;

            // Column inserted by this action
            public string? InsertedColumn;
        }

        // Records what user operation produced a Snapshot. Replay-safe because every
        // identifier is by column NAME and string value rather than by row index, so
        // the same recipe can apply to a freshly loaded dataset with similar columns.
        public class FilterAction
        {
            public enum Operation
            {
                None,
                ShowAll,
                ApplySort,
                HideRowsMatching,
                HideRowsNotMatching,
                ShowRowsMatchingRegex,
                HideRowsMatchingRegex,
                HideNotFirstUnique,
                HideRowsAbove,
                HideRowsBelow,
                ColumnResize,
                Decimate,
                Transpose,
            }

            public Operation Kind { get; set; } = Operation.None;
            public string Column { get; set; } = "";
            public ListSortDirection Direction { get; set; } = ListSortDirection.Ascending; // ApplySort
            public string Pattern { get; set; } = "";   // regex
            public string AnchorValue { get; set; } = ""; // HideRowsAbove/Below: cell value at click time
            public int Row { get; set; } = -1;            // HideRowsAbove/Below positional fallback
            public int Width { get; set; } = -1;          // ColumnResize
            public int Stride { get; set; } = 0;          // Decimate: keep every Nth row
            public List<string> Values { get; set; } = new(); // HideRowsMatching/NotMatching
        }

        // Full history oldest-first; last entry is the current state.
        // Callers can replay forward or step back via Undo() after RestoreView().
        public List<Snapshot> History { get; set; } = new();
    }

    [TestClass]
    public class DataGridControlHistoryTest
    {
        private static DataGridControl.BoundData CreateTestData()
        {
            var rows = new List<List<string?>>
            {
                new() { "a", "1" },
                new() { "b", "2" },
                new() { "c", "3" },
            };
            var cols = new List<string> { "Name", "Value" };
            return new DataGridControl.BoundData(rows, cols, _ => { });
        }

        [TestMethod]
        public void SaveViewContainsCurrent()
        {
            var bd = CreateTestData();
            var state = bd.SaveBoundState();
            // Must include at least the current state as the last entry
            Assert.IsTrue(state.History.Count >= 1);
            // Current state shows all 3 rows
            Assert.AreEqual(3, state.History[^1].VisibleRows.Count);
        }

        [TestMethod]
        public void FilterPushesOntoStack()
        {
            var bd = CreateTestData();
            int before = bd.SaveBoundState().History.Count;

            bd.HideRowsMatching("Name", new[] { "b" });

            var state = bd.SaveBoundState();
            // The filter pushed one snapshot, so history grows by 1
            Assert.AreEqual(before + 1, state.History.Count);
            // Current state (last entry) shows only the 2 non-b rows
            Assert.AreEqual(2, state.History[^1].VisibleRows.Count);
        }

        [TestMethod]
        public void RestoreViewRestoresSavedState()
        {
            var bd = CreateTestData();
            bd.HideRowsMatching("Name", new[] { "b" });
            var state = bd.SaveBoundState();

            bd.HideRowsMatching("Name", new[] { "c" }); // now only "a" visible
            Assert.AreEqual(1, bd.FilteredData.Count);

            bd.RestoreBoundState(state);
            Assert.AreEqual(2, bd.FilteredData.Count); // back to a+c visible
        }

        [TestMethod]
        public void UndoAfterRestoreWalksHistory()
        {
            var bd = CreateTestData();
            bd.HideRowsMatching("Name", new[] { "b" });
            var state = bd.SaveBoundState();

            bd.RestoreBoundState(state);

            // UndoList holds (History.Count - 1) entries; undo each one
            int undoDepth = state.History.Count - 1;
            for (int i = 0; i < undoDepth; i++)
            {
                var widths = bd.Undo();
                Assert.IsNotNull(widths);
            }
            // Oldest snapshot is the initial all-visible state
            Assert.AreEqual(3, bd.FilteredData.Count);
        }

        [TestMethod]
        public void RedoRestoresUndoneFilter()
        {
            var bd = CreateTestData();
            bd.HideRowsMatching("Name", new[] { "b" });
            Assert.AreEqual(2, bd.FilteredData.Count);
            Assert.IsFalse(bd.CanRedo);

            bd.Undo();
            Assert.AreEqual(3, bd.FilteredData.Count);
            Assert.IsTrue(bd.CanRedo);

            bd.Redo();
            Assert.AreEqual(2, bd.FilteredData.Count);
            Assert.IsFalse(bd.FilteredData.Any(r => r.Column(0) == "b"));
            Assert.IsFalse(bd.CanRedo);
        }

        [TestMethod]
        public void PushSnapshotClearsRedoStack()
        {
            var bd = CreateTestData();
            bd.HideRowsMatching("Name", new[] { "b" });
            bd.Undo();
            Assert.IsTrue(bd.CanRedo);

            // A new operation must invalidate the redo stack - otherwise Redo would
            // re-apply an action against a state it was never recorded against.
            bd.HideRowsMatching("Name", new[] { "c" });
            Assert.IsFalse(bd.CanRedo);
        }

        [TestMethod]
        public void UndoRedoUndoIsIdempotent()
        {
            var bd = CreateTestData();
            bd.HideRowsMatching("Name", new[] { "b" });
            int afterFilter = bd.FilteredData.Count;

            bd.Undo();
            int afterUndo = bd.FilteredData.Count;
            bd.Redo();
            int afterRedo = bd.FilteredData.Count;
            bd.Undo();
            int afterUndo2 = bd.FilteredData.Count;

            Assert.AreEqual(2, afterFilter);
            Assert.AreEqual(3, afterUndo);
            Assert.AreEqual(2, afterRedo);
            Assert.AreEqual(3, afterUndo2);
            Assert.IsTrue(bd.CanRedo);
        }

        [TestMethod]
        public void RedoOnEmptyStackReturnsNull()
        {
            var bd = CreateTestData();
            Assert.IsNull(bd.Redo());
        }

        [TestMethod]
        public void DecimateRedoes()
        {
            var rows = new List<List<string?>>();
            for (int loop = 0; loop < 30; loop++)
            {
                rows.Add(new List<string?> { loop.ToString(), "x" });
            }
            var bd = new DataGridControl.BoundData(rows, new List<string> { "Name", "Value" }, _ => { });

            bd.Decimate(10);
            int afterDecimate = bd.FilteredData.Count;
            Assert.AreEqual(3, afterDecimate); // rows 0, 10, 20 visible

            bd.Undo();
            Assert.AreEqual(30, bd.FilteredData.Count);

            bd.Redo();
            Assert.AreEqual(afterDecimate, bd.FilteredData.Count);
        }

        [TestMethod]
        public void TransposeForwardThenReverseRoundTrips()
        {
            var bd = CreateTestData();
            // start: 3 rows, 2 cols (Name, Value)
            Assert.AreEqual(3, bd.FilteredData.Count);

            bd.Transpose();
            // after forward: 2 rows (one per source col), 4 cols (headers + row 1..3)
            Assert.AreEqual(2, bd.FilteredData.Count);
            Assert.AreEqual("Name", bd.FilteredData[0].Column(0));
            Assert.AreEqual("a", bd.FilteredData[0].Column(1));
            Assert.AreEqual("b", bd.FilteredData[0].Column(2));
            Assert.AreEqual("c", bd.FilteredData[0].Column(3));
            Assert.AreEqual("Value", bd.FilteredData[1].Column(0));
            Assert.AreEqual("1", bd.FilteredData[1].Column(1));
            Assert.AreEqual("2", bd.FilteredData[1].Column(2));
            Assert.AreEqual("3", bd.FilteredData[1].Column(3));

            // second click detects shape and reverses
            bd.Transpose();
            Assert.AreEqual(3, bd.FilteredData.Count);
            Assert.AreEqual("a", bd.FilteredData[0].Column(0));
            Assert.AreEqual("1", bd.FilteredData[0].Column(1));
            Assert.AreEqual("b", bd.FilteredData[1].Column(0));
            Assert.AreEqual("2", bd.FilteredData[1].Column(1));
            Assert.AreEqual("c", bd.FilteredData[2].Column(0));
            Assert.AreEqual("3", bd.FilteredData[2].Column(1));
        }

        [TestMethod]
        public void TransposeIsUndoable()
        {
            var bd = CreateTestData();
            bd.Transpose();
            Assert.AreEqual(2, bd.FilteredData.Count); // forward result

            bd.Undo();
            // undo of transpose re-runs transpose - the reverse direction restores
            // visible content equivalent to the pre-transpose view.
            Assert.AreEqual(3, bd.FilteredData.Count);
            Assert.AreEqual("a", bd.FilteredData[0].Column(0));
            Assert.AreEqual("1", bd.FilteredData[0].Column(1));
        }

        [TestMethod]
        public void TransposeRedoRestoresTransposedView()
        {
            var bd = CreateTestData();
            bd.Transpose();
            bd.Undo();
            Assert.AreEqual(3, bd.FilteredData.Count);
            Assert.IsTrue(bd.CanRedo);

            bd.Redo();
            Assert.AreEqual(2, bd.FilteredData.Count);
            Assert.AreEqual("Name", bd.FilteredData[0].Column(0));
            Assert.AreEqual("Value", bd.FilteredData[1].Column(0));
        }

        [TestMethod]
        public void UndoFilterAfterUndoTransposeRestoresOriginalCounts()
        {
            // Repro for "164/1" bug: filter, transpose, undo (transpose), undo
            // (filter) must put both UnfilteredData and FilteredData back in sync
            // with the pre-filter state.
            var bd = CreateTestData();
            bd.HideRowsMatching("Name", new[] { "a", "b" }); // FD=1 (only "c")
            Assert.AreEqual(1, bd.FilteredData.Count);
            Assert.AreEqual(3, bd.UnfilteredData.Count);

            bd.Transpose();
            bd.Undo(); // undo transpose
            Assert.AreEqual(1, bd.FilteredData.Count);
            Assert.AreEqual(3, bd.UnfilteredData.Count);

            bd.Undo(); // undo filter
            Assert.AreEqual(3, bd.FilteredData.Count);
            Assert.AreEqual(3, bd.UnfilteredData.Count);
        }

        [TestMethod]
        public void ReplayOnDifferentData()
        {
            // The headline scenario: save a recipe of actions on one dataset, then
            // restore (replay) on a similar-but-not-identical dataset. The actions
            // reference column names and string values, so they apply meaningfully.
            var original = new DataGridControl.BoundData(
                new List<List<string?>>
                {
                    new() { "x", "10" },
                    new() { "y", "20" },
                    new() { "z", "0" },
                    new() { "x", "0" },
                },
                new List<string> { "Name", "Value" }, _ => { });

            // Sort by Name, then hide rows where Value matches "0".
            ((IBindingList)original).ApplySort(TypeDescriptor.GetProperties(typeof(DataGridControl.BoundDataRow))["col0"]!, ListSortDirection.Ascending);
            original.HideRowsMatching("Value", new[] { "0" });
            var recipe = original.SaveBoundState();

            // Different rows, same column shape; one row also has Value="0".
            var fresh = new DataGridControl.BoundData(
                new List<List<string?>>
                {
                    new() { "alpha", "5" },
                    new() { "bravo", "0" },
                    new() { "charlie", "7" },
                },
                new List<string> { "Name", "Value" }, _ => { });

            fresh.RestoreBoundState(recipe);

            // Replay should have sorted by Name (alpha/bravo/charlie are already
            // alphabetical) and hidden the "0" row, leaving alpha and charlie.
            Assert.AreEqual(2, fresh.FilteredData.Count);
            Assert.AreEqual("alpha", fresh.FilteredData[0].Column(0));
            Assert.AreEqual("charlie", fresh.FilteredData[1].Column(0));

            // Undo: restore the row that was hidden by HideRowsMatching.
            fresh.Undo();
            Assert.AreEqual(3, fresh.FilteredData.Count);

            // Undo again: unsort - back to insertion order.
            fresh.Undo();
            Assert.AreEqual("alpha", fresh.FilteredData[0].Column(0));
            Assert.AreEqual("bravo", fresh.FilteredData[1].Column(0));
            Assert.AreEqual("charlie", fresh.FilteredData[2].Column(0));
        }
    }
}
