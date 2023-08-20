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
        [AutoEditor.DisplayName("High quality render")]
        [AutoEditor.DisplayOrder(-10)]
        public bool HighQualityRender = true;

        [AutoEditor.DisplayName("Trace width (pixels)")]
        [AutoEditor.DisplayOrder(-10)]
        public int TraceWidth = 1600;

        [AutoEditor.DisplayName("Per trace height (pixels)")]
        [AutoEditor.DisplayOrder(-10)]
        public int TraceHeight = 422;

        [AutoEditor.DisplayName("Scope of export/screenshot")]
        [AutoEditor.DisplayOrder(-10)]
        public TraceSelections ExportTraces = TraceSelections.SelectedTraces;

        //trace line
        [AutoEditor.DisplayName("Trace line width (pixels)")]
        [AutoEditor.DisplayOrder(-9)]
        public int TraceLineWidth = 2;

        [AutoEditor.DisplayName("Default trace colour")]
        [AutoEditor.DisplayOrder(-9)]
        public Color DefaultTraceColour = Color.Black;

        // axis
        [AutoEditor.DisplayName("Show Axes Titles")]
        [AutoEditor.DisplayOrder(-8)]
        public bool ShowAxisLabels = true;

        [AutoEditor.DisplayName("Axis Title font")]
        [AutoEditor.DisplayOrder(-8)]
        public string AxisTitleFontName { get => AxisTitleFont.Name; set => AxisTitleFont.Name = value; }

        [AutoEditor.DisplayName("Axis Title height")]
        [AutoEditor.DisplayOrder(-8)]
        public float AxisTitleFontHeight { get => AxisTitleFont.EmSize; set => AxisTitleFont.EmSize = value; }

        [AutoEditor.DisplayName("Axis Title colour")]
        [AutoEditor.DisplayOrder(-8)]
        public Color AxisTitleFontColour { get => AxisTitleFont.Color; set => AxisTitleFont.Color = value; }

        [AutoEditor.DisplayName("Axis Text font")]
        [AutoEditor.DisplayOrder(-8)]
        public string AxisTextFontName { get => AxisTextFont.Name; set => AxisTextFont.Name = value; }

        [AutoEditor.DisplayName("Axis Text height")]
        [AutoEditor.DisplayOrder(-8)]
        public float AxisTextFontHeight { get => AxisTextFont.EmSize; set => AxisTextFont.EmSize = value; }

        [AutoEditor.DisplayName("Axis Text colour")]
        [AutoEditor.DisplayOrder(-8)]
        public Color AxisTextFontColour { get => AxisTextFont.Color; set => AxisTextFont.Color = value; }

        [AutoEditor.DisplayName("Axis Lines")]
        [AutoEditor.DisplayOrder(-8)]
        public AxisLines AxisLineStyle;

        [AutoEditor.DisplayName("Axis Line colour")]
        [AutoEditor.DisplayOrder(-8)]
        public Color AxisLineColour = Color.Yellow;

        [AutoEditor.DisplayName("Vertical Axis Width")]
        [AutoEditor.DisplayOrder(-8)]
        public int VerticalAxisWidth = 80;

        [AutoEditor.DisplayName("Gutter Text rows")]
        [AutoEditor.DisplayOrder(-8)]
        public int LeftGutterTextRows = 4;

        [AutoEditor.DisplayName("Vertical Axis Position")]
        [AutoEditor.DisplayOrder(-8)]
        public VerticalAxisPositions VerticalAxisPosition = VerticalAxisPositions.Left;

        [AutoEditor.DisplayName("Legend Text font")]
        [AutoEditor.DisplayOrder(-8)]
        public string LegendTextFontName { get => LegendTextFont.Name; set => LegendTextFont.Name = value; }

        [AutoEditor.DisplayName("Legend Text height")]
        [AutoEditor.DisplayOrder(-8)]
        public float LegendTextFontHeight { get => LegendTextFont.EmSize; set => LegendTextFont.EmSize = value; }

        // display elements
        [AutoEditor.DisplayName("Trace features")]
        [AutoEditor.DisplayOrder(-7)]
        public bool ShowTraceFeatures = true;

        [AutoEditor.DisplayName("Feature Text font")]
        [AutoEditor.DisplayOrder(-7)]
        public string FeatureTextFontName { get => FeatureTextFont.Name; set => FeatureTextFont.Name = value; }

        [AutoEditor.DisplayName("Feature Text style")]
        [AutoEditor.DisplayOrder(-7)]
        public string FeatureTextFontStyle { get => FeatureTextFont.Style.ToString(); set => FeatureTextFont.Style = (FontStyle)FeatureTextFont.Style.EnumValue(value); }

        [AutoEditor.DisplayName("Feature Text height")]
        [AutoEditor.DisplayOrder(-7)]
        public float FeatureTextFontHeight { get => FeatureTextFont.EmSize; set => FeatureTextFont.EmSize = value; }

        [AutoEditor.DisplayName("Feature Text colour")]
        [AutoEditor.DisplayOrder(-7)]
        public Color FeatureTextFontColour { get => FeatureTextFont.Color; set => FeatureTextFont.Color = value; }

        [AutoEditor.DisplayName("Show Warnings")]
        [AutoEditor.DisplayOrder(-7)]
        public int TraceStatsWidth = 120;

        [AutoEditor.DisplayName("Warning font")]
        [AutoEditor.DisplayOrder(-7)]
        public string WarningFontName { get => WarningFont.Name; set => WarningFont.Name = value; }

        [AutoEditor.DisplayName("Warning height")]
        [AutoEditor.DisplayOrder(-7)]
        public float WarningFontHeight { get => WarningFont.EmSize; set => WarningFont.EmSize = value; }

        [AutoEditor.DisplayName("Warning colour")]
        [AutoEditor.DisplayOrder(-7)]
        public Color WarningFontColour { get => WarningFont.Color; set => WarningFont.Color = value; }

        [AutoEditor.DisplayName("Show Statistics")]
        [AutoEditor.DisplayOrder(-7)]
        public TraceStatistics TraceStats;

        [AutoEditor.DisplayName("Stats font")]
        [AutoEditor.DisplayOrder(-7)]
        public string StatsFontName { get => StatsFont.Name; set => StatsFont.Name = value; }

        [AutoEditor.DisplayName("Show Labels")]
        [AutoEditor.DisplayOrder(-7)]
        public TraceLabels TraceLabel = TraceLabels.Embedded;

        [AutoEditor.DisplayName("Cursor mode")]
        [AutoEditor.DisplayOrder(-7)]
        public Cursors CursorMode = Cursors.CrossHair;

        //major colours

        [AutoEditor.DisplayName("Foreground colour")]
        [AutoEditor.DisplayOrder(-6)]
        public Color ForegroundColour = Color.Black;

        [AutoEditor.DisplayName("Gutter colour")]
        [AutoEditor.DisplayOrder(-6)]
        public Color GutterColour = Color.FromKnownColor(KnownColor.Control);

        [AutoEditor.DisplayName("Pen colour")]
        [AutoEditor.DisplayOrder(-6)]
        public Color DefaultPenColour = Color.Black;

        [AutoEditor.DisplayName("Graduation colour")]
        [AutoEditor.DisplayOrder(-6)]
        public Color GraduationColour = Color.Green;

        [AutoEditor.DisplayName("Selected context colour")]
        [AutoEditor.DisplayOrder(-6)]
        public Color SelectedContextColour = Color.Gainsboro;

        [AutoEditor.DisplayName("Selected embed colour")]
        [AutoEditor.DisplayOrder(-6)]
        public Color SelectedEmbedColour = Color.LightSlateGray;

        [AutoEditor.DisplayName("Background colour")]
        [AutoEditor.DisplayOrder(-6)]
        public Color BackgroundColour = Color.White;

        [AutoEditor.DisplayName("Hover label colour")]
        [AutoEditor.DisplayOrder(-6)]
        public Color HoverLabelColour = Color.FromArgb(128, Color.Yellow.R, Color.Yellow.G, Color.Yellow.B);

        [AutoEditor.DisplayName("Hover Text font")]
        [AutoEditor.DisplayOrder(-6)]
        public string HoverTextFontName { get => HoverTextFont.Name; set => HoverTextFont.Name = value; }

        [AutoEditor.DisplayName("Hover Text height")]
        [AutoEditor.DisplayOrder(-6)]
        public float HoverTextFontHeight { get => HoverTextFont.EmSize; set => HoverTextFont.EmSize = value; }

        [AutoEditor.DisplayName("Hover Text colour")]
        [AutoEditor.DisplayOrder(-6)]
        public Color HoverTextFontColour { get => HoverTextFont.Color; set => HoverTextFont.Color = value; }

        [AutoEditor.DisplayName("Crosshair colour")]
        [AutoEditor.DisplayOrder(-6)]
        public Color CrossHairColour = Color.Red;

        //hidden
        [AutoEditor.Hidden]
        public FontHelper HotPointTextFont => HoverTextFont;

        [AutoEditor.Hidden]
        public FontHelper AxisTitleFont = new FontHelper(Color.Navy, "Arial", 10f, FontStyle.Bold);

        [AutoEditor.Hidden]
        public FontHelper StatsFont = new FontHelper(Color.Black, "Courier New", 8f);

        [AutoEditor.Hidden]
        public FontHelper WarningFont = new FontHelper(Color.FromArgb(128, Color.Red), "Courier New", 10f);

        [AutoEditor.Hidden]
        public FontHelper AxisTextFont = new FontHelper(Color.Black, "Arial", 10f);

        [AutoEditor.Hidden]
        public FontHelper HoverTextFont = new FontHelper(Color.Black, "Arial", 10f);

        [AutoEditor.Hidden]
        public FontHelper FeatureTextFont = new FontHelper(Color.Black, "Arial", 10f);

        [AutoEditor.Hidden]
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
