// Port of sehenswerte StringDiff (C#): token-level Myers diff between two strings.
// The grid paints side !== 'both' substrings red.

export type DiffSide = 'both' | 'left' | 'right';

export interface DiffSegment {
  text: string;
  side: DiffSide;
}

// C# tokeniser: \w+|\s+|[^\s\w] with .NET \w = [\p{L}\p{Mn}\p{Nd}\p{Pc}].
const wordChar = '\\p{L}\\p{Mn}\\p{Nd}\\p{Pc}';
const diffTokeniser = new RegExp('[' + wordChar + ']+|\\s+|[^\\s' + wordChar + ']', 'gu');

const maxDiffTokens = 4000;

export function stringDiff(
  left: string | null | undefined,
  right: string | null | undefined
): DiffSegment[] {
  const result: DiffSegment[] = [];
  const leftStr = left === null || left === undefined ? '' : left;
  const rightStr = right === null || right === undefined ? '' : right;

  if (leftStr.length === 0 && rightStr.length === 0) {
    return result;
  }

  const leftTokens = leftStr.match(diffTokeniser) || [];
  const rightTokens = rightStr.match(diffTokeniser) || [];
  const leftCount = leftTokens.length;
  const rightCount = rightTokens.length;

  // Fallback: one side empty, or token count too high.
  if (leftCount === 0 || rightCount === 0 || leftCount > maxDiffTokens || rightCount > maxDiffTokens) {
    appendSegment(result, leftStr, 'left');
    appendSegment(result, rightStr, 'right');
    return result;
  }

  const pairs = myersDiff(leftTokens, rightTokens);

  let leftIndex = 0;
  let rightIndex = 0;
  for (let i = 0; i < pairs.length; i++) {
    const matchLeft = pairs[i][0];
    const matchRight = pairs[i][1];
    while (leftIndex < matchLeft) {
      appendSegment(result, leftTokens[leftIndex], 'left');
      leftIndex++;
    }
    while (rightIndex < matchRight) {
      appendSegment(result, rightTokens[rightIndex], 'right');
      rightIndex++;
    }
    appendSegment(result, leftTokens[leftIndex], 'both'); // matched segments keep left's casing
    leftIndex++;
    rightIndex++;
  }
  while (leftIndex < leftCount) {
    appendSegment(result, leftTokens[leftIndex], 'left');
    leftIndex++;
  }
  while (rightIndex < rightCount) {
    appendSegment(result, rightTokens[rightIndex], 'right');
    rightIndex++;
  }
  return result;
}

// Concatenation of segments belonging to the left string.
export function diffLeftText(diffs: ReadonlyArray<DiffSegment>): string {
  let out = '';
  for (let i = 0; i < diffs.length; i++) {
    if (diffs[i].side !== 'right') out += diffs[i].text;
  }
  return out;
}

// Concatenation of segments belonging to the right string.
export function diffRightText(diffs: ReadonlyArray<DiffSegment>): string {
  let out = '';
  for (let i = 0; i < diffs.length; i++) {
    if (diffs[i].side !== 'left') out += diffs[i].text;
  }
  return out;
}

function appendSegment(list: DiffSegment[], text: string, side: DiffSide): void {
  if (text.length === 0) {
    return;
  }
  if (list.length > 0) {
    const last = list[list.length - 1];
    if (last.side === side) {
      last.text += text;
      return;
    }
  }
  list.push({ text, side });
}

// Myers O((leftCount+rightCount)*editDist) token-level LCS.
// Returns the [leftIndex, rightIndex] pairs whose tokens match (case-insensitive).
function myersDiff(left: string[], right: string[]): Array<[number, number]> {
  const leftCount = left.length;
  const rightCount = right.length;
  const maxEdits = leftCount + rightCount;
  // endpoints[diag + kOrigin] = farthest leftPos reached on diagonal `diag` so far.
  // diag = leftPos - rightPos and ranges over [-maxEdits, +maxEdits], so we shift by kOrigin.
  const kOrigin = maxEdits;
  const endpoints: number[] = new Array(2 * maxEdits + 1).fill(0);
  const snapshots: number[][] = [];

  for (let editDist = 0; editDist <= maxEdits; editDist++) {
    snapshots.push(endpoints.slice());

    for (let diag = -editDist; diag <= editDist; diag += 2) {
      let leftPos: number;
      if (
        diag === -editDist ||
        (diag !== editDist && endpoints[diag - 1 + kOrigin] < endpoints[diag + 1 + kOrigin])
      ) {
        leftPos = endpoints[diag + 1 + kOrigin];
      } else {
        leftPos = endpoints[diag - 1 + kOrigin] + 1;
      }
      let rightPos = leftPos - diag;
      while (leftPos < leftCount && rightPos < rightCount && tokenEquals(left[leftPos], right[rightPos])) {
        leftPos++;
        rightPos++;
      }
      endpoints[diag + kOrigin] = leftPos;
      if (leftPos >= leftCount && rightPos >= rightCount) {
        return backtrackMyers(snapshots, leftCount, rightCount, kOrigin);
      }
    }
  }
  return [];
}

function backtrackMyers(
  snapshots: number[][],
  leftCount: number,
  rightCount: number,
  kOrigin: number
): Array<[number, number]> {
  const pairs: Array<[number, number]> = [];
  let leftPos = leftCount;
  let rightPos = rightCount;
  for (let editDist = snapshots.length - 1; editDist > 0; editDist--) {
    const endpoints = snapshots[editDist];
    const diag = leftPos - rightPos;
    let prevDiag: number;
    if (
      diag === -editDist ||
      (diag !== editDist && endpoints[diag - 1 + kOrigin] < endpoints[diag + 1 + kOrigin])
    ) {
      prevDiag = diag + 1;
    } else {
      prevDiag = diag - 1;
    }
    const prevLeftPos = endpoints[prevDiag + kOrigin];
    const prevRightPos = prevLeftPos - prevDiag;
    while (leftPos > prevLeftPos && rightPos > prevRightPos) {
      pairs.push([leftPos - 1, rightPos - 1]);
      leftPos--;
      rightPos--;
    }
    leftPos = prevLeftPos;
    rightPos = prevRightPos;
  }
  // editDist=0: walk back the initial common prefix.
  while (leftPos > 0 && rightPos > 0) {
    pairs.push([leftPos - 1, rightPos - 1]);
    leftPos--;
    rightPos--;
  }
  pairs.reverse();
  return pairs;
}

function tokenEquals(left: string, right: string): boolean {
  return left.toLowerCase() === right.toLowerCase();
}
