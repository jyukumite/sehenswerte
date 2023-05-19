namespace SehensWerte.Controls.Sehens
{
    public class PaintBoxMouseInfo
    {
        public enum Type
        {
            None,
            DragTrace,
            DragOverlayHitbox,
            TraceHeight,
            WipeSelectStart,
            WipeSelect
        }

        [Flags]
        public enum GuiSection
        {
            None = 0x0,
            TopGutter = 0x1,
            BottomGutter = 0x2,
            LeftGutter = 0x4,
            RightGutter = 0x8,
            ContextOverlay = 0x10,
            EmptyScope = 0x20,
            TraceArea = 0x40,
            TraceLabel = 0x80,
            TriggerHandle = 0x100,
            TrimHandleLeft = 0x200,
            TrimHandleRight = 0x400,
            VerticalAxis = 0x800,
            Anywhere = 0xFFFF
        }

        public GuiSection MouseGuiSection;
        public GuiSection DragGuiSection;
        public Type PreviousClickType;
        public Type ClickType;

        public MouseEventArgs? WipeStart;
        public MouseEventArgs? Click;
        public MouseEventArgs? WipeTopLeft =>
            Click == null || WipeStart == null || PreviousClickType != PaintBoxMouseInfo.Type.WipeSelect
            ? null
            : new MouseEventArgs(MouseButtons.Left, 1, Math.Min(WipeStart.X, Click.X), Math.Min(WipeStart.Y, Click.Y), 0);

        public MouseEventArgs? WipeBottomRight =>
            Click == null || WipeStart == null || PreviousClickType != PaintBoxMouseInfo.Type.WipeSelect
            ? null
            : new MouseEventArgs(MouseButtons.Left, 1, Math.Max(WipeStart.X, Click.X), Math.Max(WipeStart.Y, Click.Y), 0);

        public List<TraceView>? RightClickGroup;
        public TraceGroupDisplay? MouseDownGroupDisplay;
        public TraceGroupDisplay? MouseMoveGroupDisplay;
        public bool HideCursor;
        public double DownSeconds;
        public double XDragPixels;
        public int ClickGroupIndex;
        public int MouseDownVisibleGroupIndex;
        public int MouseMoveGroupIndex;

        public List<TraceView> CombinedSelectedTraces(List<List<TraceView>> groups)
        {
            var list = groups.SelectMany(x => x).Where(x => x.Selected).ToList();
            if (RightClickGroup != null)
            {
                list.AddRange(RightClickGroup.Where(x => !list.Contains(x)).ToArray());
            }
            return list;
        }
    }
}
