// Cell content rendering: per-cell colours, rotating highlight palette,
// null-italic, masking, and string-diff spans. Port of the visual layers in
// DataGridControl.PaintGridCell.

import React from "react";
import { DiffSegment, diffLeftText } from "../core/stringDiff";
import { GridRow } from "./GridModel";

// 8-colour rotating palette for Highlight (semi-transparent so cell text and
// selection stay readable on a dark theme).
export const highlightPalette = [
  "rgba(255, 215, 0, 0.35)", // gold
  "rgba(144, 238, 144, 0.30)", // lightgreen
  "rgba(135, 206, 250, 0.30)", // lightskyblue
  "rgba(221, 160, 221, 0.32)", // plum
  "rgba(250, 128, 114, 0.32)", // salmon
  "rgba(240, 230, 140, 0.32)", // khaki
  "rgba(127, 255, 212, 0.28)", // aquamarine
  "rgba(255, 165, 0, 0.32)", // orange
];

export const MASK_STRING = "•••••";

// Background priority (bottom to top): column tint < per-cell colour <
// highlight overlay; the most recent committed highlight wins on overlap and
// a live preview highlight wins over all committed ones.
export function cellBackground(
  row: GridRow,
  colIndex: number,
  text: string | null,
  columnColour: string | undefined,
  highlights: string[],
  previewHighlight: string | null
): string | undefined {
  const lower = text?.toLowerCase();
  if (previewHighlight !== null && previewHighlight !== "" && lower !== undefined) {
    if (lower.includes(previewHighlight.toLowerCase())) {
      return highlightPalette[highlights.length % highlightPalette.length];
    }
  }
  if (lower !== undefined) {
    for (let loop = highlights.length - 1; loop >= 0; loop--) {
      if (lower.includes(highlights[loop].toLowerCase())) {
        return highlightPalette[loop % highlightPalette.length];
      }
    }
  }
  const cellColour = row.colours?.[colIndex];
  if (cellColour !== null && cellColour !== undefined) {
    return cellColour;
  }
  return columnColour;
}

// The diff is only honoured if its left text still equals the display text
// (matching the C# "diff dropped if the cell text changed since" rule).
export function renderCellText(
  text: string | null,
  diff: DiffSegment[] | null | undefined,
  masked: boolean
): React.ReactNode {
  if (masked) {
    return MASK_STRING;
  }
  if (text === null) {
    return <span className="sw-null">null</span>;
  }
  if (diff && diffLeftText(diff) === text) {
    return diff
      .filter((seg) => seg.side !== "right")
      .map((seg, i) =>
        seg.side === "left" ? (
          <span key={i} className="sw-diff">
            {seg.text}
          </span>
        ) : (
          <React.Fragment key={i}>{seg.text}</React.Fragment>
        )
      );
  }
  return text;
}
