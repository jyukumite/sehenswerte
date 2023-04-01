namespace SehensWerte.Controls.Sehens
{
    public partial class TraceViewClickZone
    {
        [Flags]
        public enum Flags
        {
            Trace = 0x1,
            Group = 0x2
        }
        public Rectangle Rect;
        public Flags Flag;
        public PaintBoxMouseInfo.GuiSection GuiSection;

        public virtual void Click(TraceView trace, List<TraceView> groupList, MouseEventArgs e) { }
        public virtual void Drag(TraceView trace, TraceView.MouseInfo down, TraceView.MouseInfo now) { }
    }
}

