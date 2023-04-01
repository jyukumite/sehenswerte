using SehensWerte.Utils;

namespace SehensWerte.Controls.Sehens
{
    public class Skin : AutoEditorBase
    {
        private static Dictionary<CannedSkins, Skin> SkinList;

        public enum Cursors
        {
            Pointer,
            CrossHair,
            VerticalLine
        }

        public List<Color> TraceColours = new List<Color>
        {
            Color.Red,
            Color.Blue,
            Color.Green,
            Color.Magenta,
            Color.Chocolate,
            Color.DarkMagenta,
            Color.MediumVioletRed,
            Color.ForestGreen,
            Color.DeepPink,
            Color.DarkViolet,
            Color.SlateGray,
            Color.Black
        };

        public enum TraceStatistics
        {
            None,
            Embedded,
            VerticalGutter
        }

        public enum TraceLabels
        {
            None,
            Embedded,
            VerticalGutter
        }

        public enum TraceSelections
        {
            VisibleTraces,
            SelectedTraces
        }

        public enum AxisLines
        {
            None,
            BottomLeft,
            All
        }

        public enum VerticalAxisPositions
        {
            Right,
            Left
        }

        public enum CannedSkins
        {
            Clean,
            ScreenShot,
            Custom
        }

        // screenshot
        [AutoEditorForm.DisplayName("High quality render")]
        [AutoEditorForm.DisplayOrder(-10)]
        public bool HighQualityRender = true;

        [AutoEditorForm.DisplayName("Trace width (pixels)")]
        [AutoEditorForm.DisplayOrder(-10)]
        public int TraceWidth = 1600;

        [AutoEditorForm.DisplayName("Per trace height (pixels)")]
        [AutoEditorForm.DisplayOrder(-10)]
        public int TraceHeight = 422;

        [AutoEditorForm.DisplayName("Scope of export/screenshot")]
        [AutoEditorForm.DisplayOrder(-10)]
        public TraceSelections ExportTraces = TraceSelections.SelectedTraces;

        //trace line
        [AutoEditorForm.DisplayName("Trace line width (pixels)")]
        [AutoEditorForm.DisplayOrder(-9)]
        public int TraceLineWidth = 2;

        [AutoEditorForm.DisplayName("Default trace colour")]
        [AutoEditorForm.DisplayOrder(-9)]
        public Color DefaultTraceColour = Color.Black;

        // axis
        [AutoEditorForm.DisplayName("Show Axes Titles")]
        [AutoEditorForm.DisplayOrder(-8)]
        public bool ShowAxisLabels = true;

        [AutoEditorForm.DisplayName("Axis Title font")]
        [AutoEditorForm.DisplayOrder(-8)]
        public string AxisTitleFontName { get => AxisTitleFont.Name; set => AxisTitleFont.Name = value; }

        [AutoEditorForm.DisplayName("Axis Title height")]
        [AutoEditorForm.DisplayOrder(-8)]
        public float AxisTitleFontHeight { get => AxisTitleFont.EmSize; set => AxisTitleFont.EmSize = value; }

        [AutoEditorForm.DisplayName("Axis Title colour")]
        [AutoEditorForm.DisplayOrder(-8)]
        public Color AxisTitleFontColour { get => AxisTitleFont.Color; set => AxisTitleFont.Color = value; }

        [AutoEditorForm.DisplayName("Axis Text font")]
        [AutoEditorForm.DisplayOrder(-8)]
        public string AxisTextFontName { get => AxisTextFont.Name; set => AxisTextFont.Name = value; }

        [AutoEditorForm.DisplayName("Axis Text height")]
        [AutoEditorForm.DisplayOrder(-8)]
        public float AxisTextFontHeight { get => AxisTextFont.EmSize; set => AxisTextFont.EmSize = value; }

        [AutoEditorForm.DisplayName("Axis Text colour")]
        [AutoEditorForm.DisplayOrder(-8)]
        public Color AxisTextFontColour { get => AxisTextFont.Color; set => AxisTextFont.Color = value; }

        [AutoEditorForm.DisplayName("Axis Lines")]
        [AutoEditorForm.DisplayOrder(-8)]
        public AxisLines AxisLineStyle;

        [AutoEditorForm.DisplayName("Axis Line colour")]
        [AutoEditorForm.DisplayOrder(-8)]
        public Color AxisLineColour = Color.Yellow;

        [AutoEditorForm.DisplayName("Vertical Axis Width")]
        [AutoEditorForm.DisplayOrder(-8)]
        public int VerticalAxisWidth = 80;

        [AutoEditorForm.DisplayName("Gutter Text rows")]
        [AutoEditorForm.DisplayOrder(-8)]
        public int LeftGutterTextRows = 4;

        [AutoEditorForm.DisplayName("Vertical Axis Position")]
        [AutoEditorForm.DisplayOrder(-8)]
        public VerticalAxisPositions VerticalAxisPosition = VerticalAxisPositions.Left;

        [AutoEditorForm.DisplayName("Legend Text font")]
        [AutoEditorForm.DisplayOrder(-8)]
        public string LegendTextFontName { get => LegendTextFont.Name; set => LegendTextFont.Name = value; }

        [AutoEditorForm.DisplayName("Legend Text height")]
        [AutoEditorForm.DisplayOrder(-8)]
        public float LegendTextFontHeight { get => LegendTextFont.EmSize; set => LegendTextFont.EmSize = value; }

        // display elements
        [AutoEditorForm.DisplayName("Trace features")]
        [AutoEditorForm.DisplayOrder(-7)]
        public bool ShowTraceFeatures = true;

        [AutoEditorForm.DisplayName("Feature Text font")]
        [AutoEditorForm.DisplayOrder(-7)]
        public string FeatureTextFontName { get => FeatureTextFont.Name; set => FeatureTextFont.Name = value; }

        [AutoEditorForm.DisplayName("Feature Text style")]
        [AutoEditorForm.DisplayOrder(-7)]
        public string FeatureTextFontStyle { get => FeatureTextFont.Style.ToString(); set => FeatureTextFont.Style = (FontStyle)FeatureTextFont.Style.EnumValue(value); }

        [AutoEditorForm.DisplayName("Feature Text height")]
        [AutoEditorForm.DisplayOrder(-7)]
        public float FeatureTextFontHeight { get => FeatureTextFont.EmSize; set => FeatureTextFont.EmSize = value; }

        [AutoEditorForm.DisplayName("Feature Text colour")]
        [AutoEditorForm.DisplayOrder(-7)]
        public Color FeatureTextFontColour { get => FeatureTextFont.Color; set => FeatureTextFont.Color = value; }

        [AutoEditorForm.DisplayName("Show Warnings")]
        [AutoEditorForm.DisplayOrder(-7)]
        public int TraceStatsWidth = 120;

        [AutoEditorForm.DisplayName("Warning font")]
        [AutoEditorForm.DisplayOrder(-7)]
        public string WarningFontName { get => WarningFont.Name; set => WarningFont.Name = value; }

        [AutoEditorForm.DisplayName("Warning height")]
        [AutoEditorForm.DisplayOrder(-7)]
        public float WarningFontHeight { get => WarningFont.EmSize; set => WarningFont.EmSize = value; }

        [AutoEditorForm.DisplayName("Warning colour")]
        [AutoEditorForm.DisplayOrder(-7)]
        public Color WarningFontColour { get => WarningFont.Color; set => WarningFont.Color = value; }

        [AutoEditorForm.DisplayName("Show Statistics")]
        [AutoEditorForm.DisplayOrder(-7)]
        public TraceStatistics TraceStats;

        [AutoEditorForm.DisplayName("Stats font")]
        [AutoEditorForm.DisplayOrder(-7)]
        public string StatsFontName { get => StatsFont.Name; set => StatsFont.Name = value; }

        [AutoEditorForm.DisplayName("Show Labels")]
        [AutoEditorForm.DisplayOrder(-7)]
        public TraceLabels TraceLabel = TraceLabels.Embedded;

        [AutoEditorForm.DisplayName("Cursor mode")]
        [AutoEditorForm.DisplayOrder(-7)]
        public Cursors CursorMode = Cursors.CrossHair;

        //major colours

        [AutoEditorForm.DisplayName("Foreground colour")]
        [AutoEditorForm.DisplayOrder(-6)]
        public Color ForegroundColour = Color.Black;

        [AutoEditorForm.DisplayName("Gutter colour")]
        [AutoEditorForm.DisplayOrder(-6)]
        public Color GutterColour = Color.FromKnownColor(KnownColor.Control);

        [AutoEditorForm.DisplayName("Pen colour")]
        [AutoEditorForm.DisplayOrder(-6)]
        public Color DefaultPenColour = Color.Black;

        [AutoEditorForm.DisplayName("Graduation colour")]
        [AutoEditorForm.DisplayOrder(-6)]
        public Color GraduationColour = Color.Green;

        [AutoEditorForm.DisplayName("Selected context colour")]
        [AutoEditorForm.DisplayOrder(-6)]
        public Color SelectedContextColour = Color.Gainsboro;

        [AutoEditorForm.DisplayName("Selected embed colour")]
        [AutoEditorForm.DisplayOrder(-6)]
        public Color SelectedEmbedColour = Color.LightSlateGray;

        [AutoEditorForm.DisplayName("Background colour")]
        [AutoEditorForm.DisplayOrder(-6)]
        public Color BackgroundColour = Color.White;

        [AutoEditorForm.DisplayName("Hover label colour")]
        [AutoEditorForm.DisplayOrder(-6)]
        public Color HoverLabelColour = Color.FromArgb(128, Color.Yellow.R, Color.Yellow.G, Color.Yellow.B);

        [AutoEditorForm.DisplayName("Hover Text font")]
        [AutoEditorForm.DisplayOrder(-6)]
        public string HoverTextFontName { get => HoverTextFont.Name; set => HoverTextFont.Name = value; }

        [AutoEditorForm.DisplayName("Hover Text height")]
        [AutoEditorForm.DisplayOrder(-6)]
        public float HoverTextFontHeight { get => HoverTextFont.EmSize; set => HoverTextFont.EmSize = value; }

        [AutoEditorForm.DisplayName("Hover Text colour")]
        [AutoEditorForm.DisplayOrder(-6)]
        public Color HoverTextFontColour { get => HoverTextFont.Color; set => HoverTextFont.Color = value; }

        [AutoEditorForm.DisplayName("Crosshair colour")]
        [AutoEditorForm.DisplayOrder(-6)]
        public Color CrossHairColour = Color.Red;

        //hidden
        [AutoEditorForm.Hidden]
        public FontHelper HotPointTextFont => HoverTextFont;

        [AutoEditorForm.Hidden]
        public FontHelper AxisTitleFont = new FontHelper(Color.Navy, "Arial", 10f, FontStyle.Bold);

        [AutoEditorForm.Hidden]
        public FontHelper StatsFont = new FontHelper(Color.Black, "Courier New", 8f);

        [AutoEditorForm.Hidden]
        public FontHelper WarningFont = new FontHelper(Color.FromArgb(128, Color.Red), "Courier New", 10f);

        [AutoEditorForm.Hidden]
        public FontHelper AxisTextFont = new FontHelper(Color.Black, "Arial", 10f);

        [AutoEditorForm.Hidden]
        public FontHelper HoverTextFont = new FontHelper(Color.Black, "Arial", 10f);

        [AutoEditorForm.Hidden]
        public FontHelper FeatureTextFont = new FontHelper(Color.Black, "Arial", 10f);

        [AutoEditorForm.Hidden]
        public FontHelper LegendTextFont = new FontHelper(Color.Black, "Arial", 10f);

        static Skin()
        {
            SkinList = new Dictionary<CannedSkins, Skin>();

            SkinList.Add(CannedSkins.Clean, new Skin());

            SkinList.Add(CannedSkins.ScreenShot, new Skin
            {
                AxisLineColour = Color.Blue,
                AxisLineStyle = AxisLines.None,
                AxisTextFontColour = Color.Navy,
                AxisTextFontHeight = 10f,
                AxisTextFontName = "Arial",
                AxisTitleFontColour = Color.Navy,
                AxisTitleFontHeight = 10f,
                AxisTitleFontName = "Arial",
                BackgroundColour = Color.White,
                CrossHairColour = Color.SlateBlue,
                DefaultPenColour = Color.Navy,
                ExportTraces = TraceSelections.SelectedTraces,
                FeatureTextFontColour = Color.Navy,
                FeatureTextFontHeight = 10f,
                FeatureTextFontName = "Arial",
                FeatureTextFontStyle = "Bold",
                ForegroundColour = Color.Navy,
                GraduationColour = Color.LightSteelBlue,
                GutterColour = Color.White,
                HoverLabelColour = Color.FromArgb(0x40, Color.Yellow),
                HoverTextFontColour = Color.Navy,
                HoverTextFontHeight = 10f,
                HoverTextFontName = "Arial",
                LeftGutterTextRows = 3,
                LegendTextFontHeight = 10f,
                LegendTextFontName = "Arial",
                SelectedContextColour = Color.Cyan,
                ShowAxisLabels = true,
                ShowTraceFeatures = true,
                TraceHeight = 200,
                TraceLabel = TraceLabels.Embedded,
                TraceStats = TraceStatistics.None,
                TraceStatsWidth = 140,
                TraceWidth = 1000,
                VerticalAxisPosition = VerticalAxisPositions.Left,
                VerticalAxisWidth = 80,
                WarningFontColour = Color.FromArgb(0x80, Color.OrangeRed),
                WarningFontHeight = 15f,
                WarningFontName = "Arial",
            });
        }

        public Skin()
        {
        }

        public Skin(CannedSkins canned)
        {
            if (SkinList.ContainsKey(canned))
            {
                this.CopyMembersFrom(SkinList[canned]);
            }
        }

        public static Color ChangeLightness(Color color, float coef)
        {
            int change(int colour, float coef) => colour == 255 ? colour : Math.Max(0, Math.Min(255, (int)((float)colour * coef)));
            return Color.FromArgb(change(color.R, coef), change(color.G, coef), change(color.B, coef));
        }

        public Color ColourByIndex(int index)
        {
            return TraceColours[index % TraceColours.Count];
        }

        public int ColourIndex(Color find)
        {
            return TraceColours.FindIndex(x => x.ToArgb() == find.ToArgb());
        }

        internal Color ColourNext(Color colour)
        {
            return ColourByIndex(ColourIndex(colour) + 1);
        }

    }
}
