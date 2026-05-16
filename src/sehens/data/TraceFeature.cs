namespace SehensWerte.Controls.Sehens
{
    public class TraceFeature : ICloneable
    {
        public enum Feature
        {
            Text, GutterText, Line,
            Highlight, LeftHandle, RightHandle, TriggerHandle,
        }

        // Where the text's anchor Y comes from.
        //   Centre - pixel-space centre of the plot rectangle. Ignores VerticalPosition.
        //            Default; reproduces the legacy mid-trace placement.
        //   Y      - value-space: VerticalPosition is a literal Y value, projected through
        //            the painter's linear/log Y mapping.
        //   Sample - value-space: the sample value at SampleNumber, projected through the
        //            same Y mapping so the label rides the trace.
        public enum VerticalAnchorMode { Centre, Y, Sample }

        // Where the text's bounding box sits relative to that anchor Y.
        //   Top    - bbox top edge at anchor (text extends downward in screen space).
        //   Middle - bbox centred on anchor. Default, matches legacy behaviour.
        //   Bottom - bbox bottom edge at anchor (text extends upward in screen space).
        // For rotated text (e.g. Angle = -90, reading bottom-to-top), Top/Bottom refer to the
        // rotated bbox edges in screen space, not to the first/last character of the string.
        public enum VerticalJustifyMode { Top, Middle, Bottom }

        public Feature Type = Feature.Text;
        public int SampleNumber;
        public int RightSampleNumber;
        public double UnixTime;
        public double RightUnixTime;
        public string Text = "";
        public Color? Colour; // null -> default colour
        public int Angle = -90; // -90 is vertical bottom to top
        public VerticalAnchorMode VerticalAnchor = VerticalAnchorMode.Centre;
        public double VerticalPosition = 0.0;
        public VerticalJustifyMode VerticalJustify = VerticalJustifyMode.Middle;
        public Rectangle PaintedHitBox;

        public object Clone()
        {
            return new TraceFeature
            {
                Type = Type,
                SampleNumber = SampleNumber,
                RightSampleNumber = RightSampleNumber,
                UnixTime = UnixTime,
                RightUnixTime = RightUnixTime,
                Text = Text,
                Colour = Colour,
                Angle = Angle,
                VerticalAnchor = VerticalAnchor,
                VerticalPosition = VerticalPosition,
                VerticalJustify = VerticalJustify,
            };
        }

        public class FeatureCompare : IComparer<TraceFeature>
        {
            public int Compare(TraceFeature? left, TraceFeature? right)
            {
                double byTime = (left?.UnixTime ?? 0) - (right?.UnixTime ?? 0);
                if (byTime != 0) return byTime < 0 ? -1 : 1;

                int bySample = (left?.SampleNumber ?? 0) - (right?.SampleNumber ?? 0);
                if (bySample != 0) return bySample;

                int byText = (left?.Type ?? Feature.Text) - (right?.Type ?? Feature.Text);
                return byText;
            }
        }
    }
}
