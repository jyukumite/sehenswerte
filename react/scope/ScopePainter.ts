// Canvas painter for the scope: stacked group panes, min/max envelope or
// line/dot projections per trace, dashed gridlines + tick labels, embedded
// trace labels, and the "mixed horizontal axes" warning. Follows the C#
// painter's structure (TraceGroupDisplay geometry -> GroupHorizontal
// classification -> Project2dCurves -> aliased 1px paths; AA is deliberately
// off for the waveforms, matching the C# perf decision).
//
// paintScope returns the per-group geometry (PaintedLayout) so the component
// can hit-test hover/click without a second layout pass.

import { formatUnixTime, getPartitions, toStringRoundUnit } from "./axisFormat";
import { GroupMember, HorizontalDomain, subWindow, valueWindow } from "./groupHorizontal";
import { projectCurves } from "./projection";
import { ScopeSkin } from "./skin";
import { ScopeModel } from "./ScopeModel";
import { DrawnWindow, TraceView } from "./TraceView";

export interface PaintedView {
  view: TraceView;
  win: DrawnWindow;
  valueRectLeft: number;
  valueRectWidth: number;
}

export interface PaintedGroup {
  top: number;
  height: number;
  projLeft: number;
  projTop: number;
  projWidth: number;
  projHeight: number;
  mode: "stretch" | "valueAlign" | "incompatible";
  isYT: boolean;
  hLeft: number; // gutter left value (unix time for YT)
  hRight: number;
  unit: string;
  views: PaintedView[];
}

export interface PaintedLayout {
  groups: PaintedGroup[];
  width: number;
  height: number;
}

function valueToY(v: number, top: number, height: number, high: number, low: number): number {
  return top + ((high - v) * height) / (high - low || 1);
}

function isYtGroup(members: GroupMember[]): boolean {
  return members.length > 0 && members.every((m) => m.kind === "yt");
}

export function paintScope(
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
  model: ScopeModel
): PaintedLayout {
  const skin = model.skin;
  ctx.save();
  ctx.fillStyle = skin.backgroundColour;
  ctx.fillRect(0, 0, width, height);
  ctx.font = `${skin.axisTextPx}px ${skin.fontFamily}`;

  const groups = model.visibleViewGroups();
  const layout: PaintedLayout = { groups: [], width, height };
  if (groups.length === 0 || width < 40 || height < 20) {
    ctx.fillStyle = skin.axisTextColour;
    ctx.fillText("no traces", 10, 20);
    ctx.restore();
    return layout;
  }

  const bottomGutter = skin.axisTextPx + skin.bottomGutterExtraPx + 4;
  const axisWidth = skin.verticalAxisWidth;

  groups.forEach((group, groupIndex) => {
    // equal vertical split (per-group height factors are backlog)
    const top = Math.floor((height * groupIndex) / groups.length);
    const bottom = Math.floor((height * (groupIndex + 1)) / groups.length) - 1;
    const groupHeight = bottom - top + 1;
    const projLeft = 0;
    const projTop = top;
    const projWidth = width - axisWidth;
    const projHeight = groupHeight - bottomGutter;

    const members = group.map((v) => v.groupMember());
    const yt = isYtGroup(members);

    let mode: "stretch" | "valueAlign" | "incompatible";
    let hLeft: number;
    let hRight: number;
    let unit: string;
    const paintedViews: PaintedView[] = [];

    if (yt) {
      // YT groups share a unix-time window (their own path in the C#)
      const fullLeft = Math.min(...members.map((m) => m.left));
      const fullRight = Math.max(...members.map((m) => m.right));
      const span = fullRight - fullLeft;
      const wLeft = fullLeft + span * model.panValue;
      const wRight = wLeft + span * model.zoomValue;
      mode = "stretch";
      hLeft = wLeft;
      hRight = wRight;
      unit = "s";
      for (const view of group) {
        const win = view.drawnWindowYT(wLeft, wRight);
        const sub = subWindow(win.leftValue, win.rightValue, wLeft, wRight, projLeft, projWidth);
        paintedViews.push({
          view,
          win,
          valueRectLeft: sub.left,
          valueRectWidth: Math.max(1, sub.width),
        });
      }
    } else {
      const domain: HorizontalDomain = valueWindow(members, model.zoomValue, model.panValue);
      mode = domain.mode;
      if (domain.mode === "valueAlign") {
        hLeft = domain.left;
        hRight = domain.right;
        unit = domain.unit;
        for (const view of group) {
          const win = view.drawnWindow("valueAlign", domain.left, domain.right, model.zoomValue, model.panValue);
          const sub = subWindow(win.leftValue, win.rightValue, domain.left, domain.right, projLeft, projWidth);
          paintedViews.push({
            view,
            win,
            valueRectLeft: sub.left,
            valueRectWidth: Math.max(1, sub.width),
          });
        }
      } else {
        // stretch and incompatible both fall back to per-trace fill; the
        // gutter follows the leader's own axis
        for (const view of group) {
          const win = view.drawnWindow("stretch", 0, 0, model.zoomValue, model.panValue);
          paintedViews.push({ view, win, valueRectLeft: projLeft, valueRectWidth: projWidth });
        }
        const lead = paintedViews[0];
        hLeft = lead.win.leftValue;
        hRight = lead.win.rightValue;
        unit = group[0].data.horizontalUnitEffective;
      }
    }

    // ---- horizontal gutter: dashed gridlines + labels ----
    ctx.strokeStyle = skin.graduationColour;
    ctx.fillStyle = skin.axisTextColour;
    ctx.setLineDash([2, 3]);
    const labelEvery = Math.max(60, yt ? 130 : 70);
    const partitionCount = Math.max(2, Math.floor(projWidth / labelEvery));
    const ticks = getPartitions(hLeft, hRight, partitionCount + 1);
    const span = hRight - hLeft;
    let lastLabelRight = -Infinity;
    for (const value of ticks) {
      const x = projLeft + ((value - hLeft) * projWidth) / (span || 1);
      if (x < projLeft || x > projLeft + projWidth) continue;
      ctx.beginPath();
      ctx.moveTo(Math.round(x) + 0.5, projTop);
      ctx.lineTo(Math.round(x) + 0.5, projTop + projHeight);
      ctx.stroke();
      const text = yt ? formatUnixTime(value, span) : toStringRoundUnit(value, 5, 3, unit);
      const textWidth = ctx.measureText(text).width;
      const textLeft = x - textWidth / 2;
      if (textLeft > lastLabelRight + 6 && textLeft + textWidth < width - axisWidth) {
        ctx.fillText(text, textLeft, projTop + projHeight + skin.axisTextPx + 1);
        lastLabelRight = textLeft + textWidth;
      }
    }
    ctx.setLineDash([]);

    // ---- vertical axis (right) ----
    const lead = group[0];
    const high = lead.highestValue;
    const low = lead.lowestValue;
    ctx.strokeStyle = skin.graduationColour;
    ctx.beginPath();
    ctx.moveTo(projLeft + projWidth + 0.5, projTop);
    ctx.lineTo(projLeft + projWidth + 0.5, projTop + projHeight);
    ctx.stroke();
    const vTicks = getPartitions(low, high, Math.max(2, Math.floor(projHeight / 36)));
    ctx.setLineDash([2, 3]);
    for (const v of vTicks) {
      const y = valueToY(v, projTop, projHeight, high, low);
      if (y < projTop || y > projTop + projHeight) continue;
      ctx.beginPath();
      ctx.moveTo(projLeft, Math.round(y) + 0.5);
      ctx.lineTo(projLeft + projWidth, Math.round(y) + 0.5);
      ctx.stroke();
      const label = toStringRoundUnit(v, 5, 3, lead.data.verticalUnit);
      ctx.fillText(label, projLeft + projWidth + 4, y + skin.axisTextPx / 2 - 1);
    }
    ctx.setLineDash([]);

    // group separator
    if (groupIndex > 0) {
      ctx.strokeStyle = skin.graduationColour;
      ctx.beginPath();
      ctx.moveTo(0, top + 0.5);
      ctx.lineTo(width, top + 0.5);
      ctx.stroke();
    }

    // ---- traces ----
    for (const pv of paintedViews) {
      const { view, win } = pv;
      const samples = view.data.samples;
      const pixelWidth = Math.max(1, Math.round(pv.valueRectWidth));
      const curves = projectCurves(samples, win.first, win.count, pixelWidth, view.paintMode, undefined, {
        lowestValue: view.lowestValue,
        highestValue: view.highestValue,
      });
      const x0 = pv.valueRectLeft;
      const yOf = (v: number): number => valueToY(v, projTop, projHeight, view.highestValue, view.lowestValue);

      ctx.save();
      ctx.beginPath();
      ctx.rect(projLeft, projTop, projWidth, projHeight);
      ctx.clip();

      if (curves.min !== undefined && curves.max !== undefined) {
        // filled band between the envelopes + the max polyline on top
        ctx.beginPath();
        let started = false;
        for (let px = 0; px < pixelWidth; px++) {
          const v = curves.min[px];
          if (!isFinite(v)) continue;
          const x = x0 + px;
          if (!started) {
            ctx.moveTo(x, yOf(v));
            started = true;
          } else {
            ctx.lineTo(x, yOf(v));
          }
        }
        for (let px = pixelWidth - 1; px >= 0; px--) {
          const v = curves.max[px];
          if (!isFinite(v)) continue;
          ctx.lineTo(x0 + px, yOf(v));
        }
        if (started) {
          ctx.closePath();
          ctx.fillStyle = view.colour;
          ctx.globalAlpha = 0.55;
          ctx.fill();
          ctx.globalAlpha = 1;
          ctx.strokeStyle = view.colour;
          ctx.lineWidth = 1;
          ctx.stroke();
        }
      } else if (curves.line !== undefined) {
        ctx.beginPath();
        let pen = false;
        for (let px = 0; px < curves.line.length; px++) {
          const v = curves.line[px];
          if (!isFinite(v)) {
            pen = false; // NaN gap lifts the pen
            continue;
          }
          const x = x0 + px;
          const y = yOf(v);
          if (!pen) {
            ctx.moveTo(x, y);
            pen = true;
          } else {
            ctx.lineTo(x, y);
          }
        }
        ctx.strokeStyle = view.colour;
        ctx.lineWidth = view.lineWidth || 1;
        ctx.stroke();
      }
      if (curves.dots !== undefined) {
        ctx.fillStyle = view.colour;
        for (let loop = 0; loop < curves.dots.x.length; loop++) {
          ctx.fillRect(x0 + curves.dots.x[loop] - 1, yOf(curves.dots.y[loop]) - 1, 3, 3);
        }
      }
      ctx.restore();
    }

    // ---- embedded trace labels (top-left, stacked) ----
    ctx.font = `600 ${skin.legendTextPx}px ${skin.fontFamily}`;
    group.forEach((view, i) => {
      ctx.fillStyle = view.colour;
      const label = model.selected.has(view) ? `[${view.viewName}]` : view.viewName;
      ctx.fillText(label, projLeft + 6, projTop + skin.legendTextPx * (i + 1) + 2);
    });
    ctx.font = `${skin.axisTextPx}px ${skin.fontFamily}`;

    if (mode === "incompatible") {
      ctx.fillStyle = skin.warningColour;
      ctx.fillText("(mixed horizontal axes)", projLeft + projWidth / 2 - 60, projTop + 14);
    }
    const anyBadAffine = group.some((v) => v.data.horizontalAffineInvalid);
    if (anyBadAffine) {
      ctx.fillStyle = skin.warningColour;
      ctx.fillText("(bad horizontal axis)", projLeft + projWidth / 2 - 55, projTop + 26);
    }

    layout.groups.push({
      top,
      height: groupHeight,
      projLeft,
      projTop,
      projWidth,
      projHeight,
      mode,
      isYT: yt,
      hLeft,
      hRight,
      unit,
      views: paintedViews,
    });
  });

  ctx.restore();
  return layout;
}
