namespace SehensWerte.Controls.Sehens
{
    public class TraceViewEmbedHandle : TraceViewClickZone
    {
        private int SampleNumber;
        private TraceFeature.Feature FeatureType;

        public TraceViewEmbedHandle(int leftSample, TraceFeature.Feature featureType, TraceView trace)
        {
            SampleNumber = leftSample;
            FeatureType = featureType;
            switch (FeatureType)
            {
                case TraceFeature.Feature.LeftHandle:
                    GuiSection = PaintBoxMouseInfo.GuiSection.TrimHandleLeft;
                    break;
                case TraceFeature.Feature.RightHandle:
                    GuiSection = PaintBoxMouseInfo.GuiSection.TrimHandleRight;
                    break;
                case TraceFeature.Feature.TriggerHandle:
                    GuiSection = PaintBoxMouseInfo.GuiSection.TriggerHandle;
                    break;
            }
        }

        public void Paint(TraceGroupDisplay info, Graphics graphics)
        {
            TraceFeature traceFeature = new TraceFeature
            {
                SampleNumber = SampleNumber,
                Type = FeatureType,
                Colour = Color.Blue
            };
            info.View0.Painter.PaintFeatures(graphics, info, new[] { traceFeature });
            Rect = new Rectangle(traceFeature.PaintedHitBox.X, traceFeature.PaintedHitBox.Y + info.PaintVerticalOffset, traceFeature.PaintedHitBox.Width, traceFeature.PaintedHitBox.Height);
        }

        public override void Drag(TraceView trace, TraceView.MouseInfo down, TraceView.MouseInfo now)
        {
            switch (GuiSection)
            {
                case PaintBoxMouseInfo.GuiSection.TrimHandleLeft:
                    trace.ViewLengthOverride += trace.ViewOffsetOverride - now.IndexBeforeTrim;
                    trace.ViewOffsetOverride = now.IndexBeforeTrim;
                    trace.ViewOverrideEnabled = false;
                    break;
                case PaintBoxMouseInfo.GuiSection.TrimHandleRight:
                    trace.ViewLengthOverride = now.IndexBeforeTrim - trace.ViewOffsetOverride;
                    trace.ViewOverrideEnabled = false;
                    break;
                case PaintBoxMouseInfo.GuiSection.TriggerHandle:
                    trace.TriggerValue = now.YValue;
                    break;
            }
        }
    }
}
