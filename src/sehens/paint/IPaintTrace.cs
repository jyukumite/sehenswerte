namespace SehensWerte.Controls.Sehens
{
    public interface IPaintTrace : IDisposable
    {
        void PaintInitial(Graphics graphics, TraceGroupDisplay info);
        void PaintAxisTitleHorizontal(Graphics graphics, TraceGroupDisplay info);
        void PaintAxisTitleVertical(Graphics graphics, TraceGroupDisplay info);
        void PaintVerticalAxis(Graphics graphics, TraceGroupDisplay info);
        void PaintHorizontalAxis(Graphics graphics, TraceGroupDisplay info);
        void PaintFeatures(Graphics graphics, TraceGroupDisplay info, IEnumerable<TraceFeature> features);
        void PaintProjection(Graphics graphics, TraceGroupDisplay info);
        void PaintEmbeddedControls(Graphics graphics, TraceGroupDisplay info, ScopeContextMenu ScopeContextMenu);
        void PaintLabel(Graphics graphics, TraceGroupDisplay info);
        void PaintStats(Graphics graphics, TraceGroupDisplay info);

        PaintBoxMouseInfo.GuiSection GetGuiSections(TraceView.PaintedInfo painted, MouseEventArgs e);
        string GetHoverValue(List<TraceView> list, MouseEventArgs e);
        string GetHoverStatistics(TraceView view, TraceView.MouseInfo clickInfo);
        int HoverLabelYFromOffsetX(TraceGroupDisplay info, int x);

        void CalculateTraceRange(TraceGroupDisplay divisionInfo);
        void ReprojectionRequired();
    }
}
