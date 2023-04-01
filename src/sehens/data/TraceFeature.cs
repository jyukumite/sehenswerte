namespace SehensWerte.Controls.Sehens
{
    public class TraceFeature : ICloneable
    {
        public enum Feature
        {
            Text, GutterText, Line,
            Highlight, LeftHandle, RightHandle, TriggerHandle,
        }

        public Feature Type;
        public int SampleNumber;
        public int RightSampleNumber;
        public double UnixTime;
        public double RightUnixTime;
        public string Text = "";
        public Color? Colour; // null -> default colour
        public int Angle = -90; // -90 is vertical bottom to top
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
                Angle = Angle
            };
        }

        public class FeatureCompare : IComparer<TraceFeature>
        {
            public int Compare(TraceFeature? left, TraceFeature? right)
            {
                double byTime = (left?.UnixTime ?? 0) - (right?.UnixTime ?? 0);
                int bySample = (left?.SampleNumber ?? 0) - (right?.SampleNumber ?? 0);
                int byText = (left?.Type ?? Feature.Text) - (right?.Type ?? Feature.Text);
                if (byTime != 0) return byTime < 0 ? -1 : 1;
                if (bySample != 0) return bySample;
                return byText;
            }
        }
    }
}
