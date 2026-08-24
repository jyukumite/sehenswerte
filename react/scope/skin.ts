// Scope theming and layout metrics (port of the Skin defaults the WinForms
// control ships). Two palettes: the classic light palette matches the C#
// TraceColours exactly; the dark palette is the same hues lifted for a dark
// background (admin_site's theme). Hosts can pass their own Skin.

export interface ScopeSkin {
  backgroundColour: string;
  foregroundColour: string;
  gutterColour: string; // gutter background
  graduationColour: string; // dashed gridlines
  axisTextColour: string;
  warningColour: string;
  traceColours: string[]; // cycled per new trace, matching C# Skin.TraceColours order
  defaultTraceColour: string;
  hoverLabelBackground: string;
  crosshairColour: string;
  wipeSelectFill: string;
  fontFamily: string;
  axisTextPx: number;
  legendTextPx: number;
  verticalAxisWidth: number;
  bottomGutterExtraPx: number; // added to the axis text line height
  traceLineWidth: number;
}

// C# Skin.TraceColours: Red, Blue, Green, Magenta, Chocolate, DarkMagenta,
// MediumVioletRed, ForestGreen, DeepPink, DarkViolet, SlateGray, Black.
export const LIGHT_TRACE_COLOURS = [
  "#ff0000",
  "#0000ff",
  "#008000",
  "#ff00ff",
  "#d2691e",
  "#8b008b",
  "#c71585",
  "#228b22",
  "#ff1493",
  "#9400d3",
  "#708090",
  "#000000",
];

// Same hue order, raised for contrast on a dark background (black becomes white).
export const DARK_TRACE_COLOURS = [
  "#ff5252",
  "#5c8dff",
  "#3fbf3f",
  "#ff5cff",
  "#e08b4e",
  "#c95cc9",
  "#e0559c",
  "#4fc94f",
  "#ff69b4",
  "#b366e0",
  "#94a3b8",
  "#ffffff",
];

export const lightSkin: ScopeSkin = {
  backgroundColour: "#ffffff",
  foregroundColour: "#000000",
  gutterColour: "#f4f4f4",
  graduationColour: "#c8c8c8",
  axisTextColour: "#404040",
  warningColour: "#c0392b",
  traceColours: LIGHT_TRACE_COLOURS,
  defaultTraceColour: "#ff0000",
  hoverLabelBackground: "rgba(255,255,0,0.5)",
  crosshairColour: "#808080",
  wipeSelectFill: "rgba(100,149,237,0.25)",
  fontFamily: '"Segoe UI", system-ui, sans-serif',
  axisTextPx: 11,
  legendTextPx: 12,
  verticalAxisWidth: 56,
  bottomGutterExtraPx: 3,
  traceLineWidth: 1,
};

export const darkSkin: ScopeSkin = {
  ...lightSkin,
  backgroundColour: "#0e1116",
  foregroundColour: "#d7dde5",
  gutterColour: "#11151c",
  graduationColour: "#2a3038",
  axisTextColour: "#8a93a0",
  warningColour: "#ff6b6b",
  traceColours: DARK_TRACE_COLOURS,
  defaultTraceColour: "#ff5252",
  hoverLabelBackground: "rgba(255,255,0,0.25)",
  crosshairColour: "#5a6472",
  wipeSelectFill: "rgba(31,111,235,0.25)",
};
