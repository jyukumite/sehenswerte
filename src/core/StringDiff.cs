using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SehensWerte
{
    public static class StringDiff
    {
        // diff between two strings
        public sealed class Diffs : List<(string Text, Diffs.Side Side)>
        {
            public enum Side
            {
                Both,
                Left,
                Right,
            }

            public string LeftText
                => string.Concat(this.Where(s => s.Side != Side.Right).Select(s => s.Text));

            public string RightText
                => string.Concat(this.Where(s => s.Side != Side.Left).Select(s => s.Text));
        }

        private static readonly Regex DiffTokeniser =
            new Regex(@"\w+|\s+|[^\s\w]", RegexOptions.Compiled);

        private const int MaxDiffTokens = 4000;

        public static Diffs Compute(string? left, string? right)
        {
            var result = new Diffs();
            string leftStr = left ?? "";
            string rightStr = right ?? "";

            if (leftStr.Length == 0 && rightStr.Length == 0)
            {
                return result;
            }

            var leftMatches = DiffTokeniser.Matches(leftStr);
            var rightMatches = DiffTokeniser.Matches(rightStr);
            int leftCount = leftMatches.Count;
            int rightCount = rightMatches.Count;

            // Fallback: one side empty, or token count too high.
            if (leftCount == 0 || rightCount == 0 || leftCount > MaxDiffTokens || rightCount > MaxDiffTokens)
            {
                AppendSegment(result, leftStr, Diffs.Side.Left);
                AppendSegment(result, rightStr, Diffs.Side.Right);
                return result;
            }

            string[] leftTokens = new string[leftCount];
            string[] rightTokens = new string[rightCount];
            for (int loop = 0; loop < leftCount; loop++)
            {
                leftTokens[loop] = leftMatches[loop].Value;
            }
            for (int loop = 0; loop < rightCount; loop++)
            {
                rightTokens[loop] = rightMatches[loop].Value;
            }

            var pairs = MyersDiff(leftTokens, rightTokens);

            int leftIndex = 0;
            int rightIndex = 0;
            foreach (var (matchLeft, matchRight) in pairs)
            {
                while (leftIndex < matchLeft)
                {
                    AppendSegment(result, leftTokens[leftIndex], Diffs.Side.Left);
                    leftIndex++;
                }
                while (rightIndex < matchRight)
                {
                    AppendSegment(result, rightTokens[rightIndex], Diffs.Side.Right);
                    rightIndex++;
                }
                AppendSegment(result, leftTokens[leftIndex], Diffs.Side.Both);
                leftIndex++;
                rightIndex++;
            }
            while (leftIndex < leftCount)
            {
                AppendSegment(result, leftTokens[leftIndex], Diffs.Side.Left);
                leftIndex++;
            }
            while (rightIndex < rightCount)
            {
                AppendSegment(result, rightTokens[rightIndex], Diffs.Side.Right);
                rightIndex++;
            }
            return result;
        }

        private static void AppendSegment(Diffs list, string text, Diffs.Side side)
        {
            if (text.Length == 0)
            {
                return;
            }
            if (list.Count > 0)
            {
                var last = list[list.Count - 1];
                if (last.Side == side)
                {
                    list[list.Count - 1] = (last.Text + text, side);
                    return;
                }
            }
            list.Add((text, side));
        }

        // Myers O((leftCount+rightCount)*editDist) token-level LCS.
        // Returns the (leftIndex, rightIndex) pairs whose tokens match (case-insensitive).
        private static List<(int Left, int Right)> MyersDiff(string[] left, string[] right)
        {
            int leftCount = left.Length;
            int rightCount = right.Length;
            int maxEdits = leftCount + rightCount;
            // endpoints[diag + kOrigin] = farthest leftPos reached on diagonal `diag` so far.
            // diag = leftPos - rightPos and ranges over [-maxEdits, +maxEdits], so we shift by kOrigin.
            int kOrigin = maxEdits;
            int[] endpoints = new int[2 * maxEdits + 1];
            var snapshots = new List<int[]>();

            for (int editDist = 0; editDist <= maxEdits; editDist++)
            {
                int[] snapshot = new int[2 * maxEdits + 1];
                Array.Copy(endpoints, snapshot, endpoints.Length);
                snapshots.Add(snapshot);

                for (int diag = -editDist; diag <= editDist; diag += 2)
                {
                    int leftPos;
                    if (diag == -editDist || (diag != editDist
                        && endpoints[diag - 1 + kOrigin] < endpoints[diag + 1 + kOrigin]))
                    {
                        leftPos = endpoints[diag + 1 + kOrigin];
                    }
                    else
                    {
                        leftPos = endpoints[diag - 1 + kOrigin] + 1;
                    }
                    int rightPos = leftPos - diag;
                    while (leftPos < leftCount && rightPos < rightCount
                        && TokenEquals(left[leftPos], right[rightPos]))
                    {
                        leftPos++;
                        rightPos++;
                    }
                    endpoints[diag + kOrigin] = leftPos;
                    if (leftPos >= leftCount && rightPos >= rightCount)
                    {
                        return BacktrackMyers(snapshots, leftCount, rightCount, kOrigin);
                    }
                }
            }
            return new List<(int, int)>();
        }

        private static List<(int Left, int Right)> BacktrackMyers(
            List<int[]> snapshots, int leftCount, int rightCount, int kOrigin)
        {
            var pairs = new List<(int Left, int Right)>();
            int leftPos = leftCount;
            int rightPos = rightCount;
            for (int editDist = snapshots.Count - 1; editDist > 0; editDist--)
            {
                int[] endpoints = snapshots[editDist];
                int diag = leftPos - rightPos;
                int prevDiag;
                if (diag == -editDist || (diag != editDist
                    && endpoints[diag - 1 + kOrigin] < endpoints[diag + 1 + kOrigin]))
                {
                    prevDiag = diag + 1;
                }
                else
                {
                    prevDiag = diag - 1;
                }
                int prevLeftPos = endpoints[prevDiag + kOrigin];
                int prevRightPos = prevLeftPos - prevDiag;
                while (leftPos > prevLeftPos && rightPos > prevRightPos)
                {
                    pairs.Add((leftPos - 1, rightPos - 1));
                    leftPos--;
                    rightPos--;
                }
                leftPos = prevLeftPos;
                rightPos = prevRightPos;
            }
            // editDist=0: walk back the initial common prefix.
            while (leftPos > 0 && rightPos > 0)
            {
                pairs.Add((leftPos - 1, rightPos - 1));
                leftPos--;
                rightPos--;
            }
            pairs.Reverse();
            return pairs;
        }

        private static bool TokenEquals(string left, string right)
        {
            return string.Compare(left, right, StringComparison.InvariantCultureIgnoreCase) == 0;
        }
    }

    [TestClass]
    public class StringDiffTests
    {
        private static int CountWhere(StringDiff.Diffs diffs, StringDiff.Diffs.Side side)
        {
            return diffs.Where(s => s.Side == side).Sum(s => s.Text.Length);
        }

        [TestMethod]
        public void EqualStringsAllCommon()
        {
            string s = "allow_auto,debug_upload";
            var diff = StringDiff.Compute(s, s);
            Assert.AreEqual(1, diff.Count);
            Assert.AreEqual((s, StringDiff.Diffs.Side.Both), diff[0]);
        }

        [TestMethod]
        public void NoOverlapEmitsLeftThenRight()
        {
            var diff = StringDiff.Compute("abc", "xyz");
            Assert.AreEqual(2, diff.Count);
            Assert.AreEqual(("abc", StringDiff.Diffs.Side.Left), diff[0]);
            Assert.AreEqual(("xyz", StringDiff.Diffs.Side.Right), diff[1]);
        }

        [TestMethod]
        public void EmptyLeftEmitsRightOnly()
        {
            var diff = StringDiff.Compute("", "abc");
            Assert.AreEqual(1, diff.Count);
            Assert.AreEqual(("abc", StringDiff.Diffs.Side.Right), diff[0]);
        }

        [TestMethod]
        public void EmptyRightEmitsLeftOnly()
        {
            var diff = StringDiff.Compute("abc", "");
            Assert.AreEqual(1, diff.Count);
            Assert.AreEqual(("abc", StringDiff.Diffs.Side.Left), diff[0]);
        }

        [TestMethod]
        public void BothEmptyReturnsEmpty()
        {
            Assert.AreEqual(0, StringDiff.Compute("", "").Count);
            Assert.AreEqual(0, StringDiff.Compute(null, null).Count);
        }

        [TestMethod]
        public void MiddleInsertion()
        {
            // 'foo,bar,baz' vs 'foo,quux,bar,baz' -- 'quux,' is inserted in right.
            var diff = StringDiff.Compute("foo,bar,baz", "foo,quux,bar,baz");
            Assert.AreEqual("foo,bar,baz".Length,
                CountWhere(diff, StringDiff.Diffs.Side.Both) + CountWhere(diff, StringDiff.Diffs.Side.Left));
            Assert.AreEqual("foo,quux,bar,baz".Length,
                CountWhere(diff, StringDiff.Diffs.Side.Both) + CountWhere(diff, StringDiff.Diffs.Side.Right));
            Assert.IsTrue(CountWhere(diff, StringDiff.Diffs.Side.Right) >= "quux".Length);
        }

        [TestMethod]
        public void NoAdjacentSameSideSegments()
        {
            // Adjacency merging: consecutive same-side segments must be coalesced.
            var diff = StringDiff.Compute("foo bar baz", "foo qux baz");
            for (int loop = 1; loop < diff.Count; loop++)
            {
                Assert.AreNotEqual(diff[loop - 1].Side, diff[loop].Side,
                    $"adjacent same-side at index {loop}");
            }
        }

        [TestMethod]
        public void WhitespacePreserved()
        {
            // Whitespace runs are tokens, so they participate in matching.
            var diff = StringDiff.Compute("foo bar", "foo bar");
            Assert.AreEqual(1, diff.Count);
            Assert.AreEqual(("foo bar", StringDiff.Diffs.Side.Both), diff[0]);
        }

        [TestMethod]
        public void CaseInsensitiveMatching()
        {
            var diff = StringDiff.Compute("Foo,Bar", "foo,bar");
            // All matched -> single common segment using left's casing.
            Assert.AreEqual(1, diff.Count);
            Assert.AreEqual(("Foo,Bar", StringDiff.Diffs.Side.Both), diff[0]);
        }

        [TestMethod]
        public void LeftTextAndRightTextReconstructInputs()
        {
            var diff = StringDiff.Compute("foo,bar,baz", "foo,quux,bar,baz");
            Assert.AreEqual("foo,bar,baz", diff.LeftText);
            Assert.AreEqual("foo,quux,bar,baz", diff.RightText);
        }

        [TestMethod]
        public void ExtensionAllCommon()
        {
            // Left fully exists in right -> no Side=Left segments in result.
            var diff = "foo,bar,baz".StringDiff("foo,quux,bar,baz");
            Assert.AreEqual("foo,bar,baz", diff.LeftText);
            Assert.IsFalse(diff.Any(s => s.Side == StringDiff.Diffs.Side.Left));
        }

        [TestMethod]
        public void ExtensionMarksDifferingTokens()
        {
            var diff = "foo,bar,baz".StringDiff("foo,baz");
            Assert.AreEqual("foo,bar,baz", diff.LeftText);
            // 'bar' must be flagged left-only.
            int leftOnlyChars = CountWhere(diff, StringDiff.Diffs.Side.Left);
            Assert.IsTrue(leftOnlyChars >= "bar".Length);
        }

        [TestMethod]
        public void ExtensionNoOverlap()
        {
            var diff = "abc".StringDiff("xyz");
            Assert.AreEqual("abc", diff.LeftText);
            Assert.AreEqual(("abc", StringDiff.Diffs.Side.Left), diff[0]);
            Assert.AreEqual(("xyz", StringDiff.Diffs.Side.Right), diff[1]);
        }
    }
}
