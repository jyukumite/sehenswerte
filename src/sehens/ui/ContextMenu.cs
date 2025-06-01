namespace SehensWerte.Controls.Sehens
{
    public class ScopeContextMenu
    {
        internal ContextMenuStrip ScopeMenuStrip;
        internal List<MenuItem> ContextMenuList = new List<MenuItem>();
        internal List<EmbeddedMenu> EmbeddedContextMenuList = new List<EmbeddedMenu>();

        internal class Embed : TraceViewEmbedText
        {
            public EmbeddedMenu menu;

            public Embed(int x, int y, TraceGroupDisplay info, PaintBoxMouseInfo.GuiSection hotPoint, Flags showFlags, EmbeddedMenu menu)
                : base(x, y, info, hotPoint, showFlags)
            {
                this.menu = menu;
            }

            public override void Click(TraceView trace, List<TraceView> groupList, MouseEventArgs e)
            {
                menu?.Clicked(new MenuArgs()
                {
                    Menu = menu,
                    View = trace,
                });
            }
        }

        public class MenuArgs
        {
            public EmbeddedMenu Menu;
            public TraceView View;
        }

        public class DropDownArgs
        {
            public SehensControl Scope;
            public MenuItem Menu = new MenuItem();
            public List<TraceView> Views = new List<TraceView>();
            public PaintBoxMouseInfo Mouse = new PaintBoxMouseInfo();
        }

        public class EmbeddedMenu
        {
            public string Text = "";
            public object? Tag;
            public int Sort;
            public Action<MenuArgs>? Clicked;
            public List<EmbeddedMenu> SubMenu = new List<EmbeddedMenu>();
            public TraceViewEmbedText.Style Style;
            public Action<MenuArgs>? GetStyle;
        }

        public class MenuItem
        {
            public enum ShowWhen
            {
                Always,
                TracesPresent,
                OneSelected,
                OnePlusSelected,
                TwoSelected,
                TwoPlusSelected,
                TwoPlusUnderMouse,
                RightWipeSelect
            }

            public enum TextDisplay
            {
                NoChange,
                AddViewNames
            }

            public enum CallWhen
            {
                Once,
                PerTrace,
                PerTraceGroup
            }

            public enum HotKeyModifierState
            {
                None,
                Alt,
                Ctrl,
                CtrlShift,
                AltCtrl,
            }

            public Action<DropDownArgs>? Clicked;
            public Action<DropDownArgs>? GetStyle;
            public string SubMenuText = "";
            public string Text = "";
            public ShowWhen ShownWhenTrace;
            public TextDisplay ShownText = TextDisplay.NoChange;
            public CallWhen Call;
            public HotKeyModifierState HotKeyModifier = ScopeContextMenu.MenuItem.HotKeyModifierState.None;
            public Keys HotKeyCode = Keys.None;
            public ToolStripMenuItem Menu;
            public ToolStripMenuItem Parent;
            public PaintBoxMouseInfo.GuiSection ShownWhenMouse;

            public bool HotKey(PreviewKeyDownEventArgs e) =>
                    HotKeyCode != 0
                    && e.KeyCode == HotKeyCode
                    && HotKeyCode != 0
                    && HotKeyModifier switch
                    {
                        HotKeyModifierState.Alt => e.Alt && !e.Control && !e.Shift,
                        HotKeyModifierState.AltCtrl => e.Alt && e.Control && !e.Shift,
                        HotKeyModifierState.Ctrl => !e.Alt && e.Control && !e.Shift,
                        HotKeyModifierState.CtrlShift => !e.Alt && e.Control && e.Shift,
                        _ => !e.Alt && !e.Control && !e.Shift,
                    };

            public string DisplayText(string selected)
            {
                string key = HotKeyCode == Keys.None
                    ? ""
                    : HotKeyModifier == HotKeyModifierState.None
                            ? $" ({HotKeyCode})"
                            : $" ({HotKeyModifier}-{HotKeyCode})";

                string views = selected.Length > 80 ? selected.Substring(0, 50) + "..." : selected;
                return ShownText == TextDisplay.AddViewNames
                            ? $"{Text}{key} {views}"
                            : $"{Text}{key}";
            }

            public bool Valid(List<List<TraceView>> traces, List<TraceView> selectedTraces, PaintBoxMouseInfo.GuiSection mousePoint, bool rightTwoPlus = false, bool wipeSelect = false)
            {
                rightTwoPlus |= selectedTraces.Count >= 2;
                return ShownWhenTrace == ShowWhen.Always
                    || (ShownWhenTrace == ShowWhen.TracesPresent && !wipeSelect && traces.Count > 0)
                    || (ShownWhenTrace == ShowWhen.OneSelected && !wipeSelect && selectedTraces.Count == 1)
                    || (ShownWhenTrace == ShowWhen.OnePlusSelected && !wipeSelect && selectedTraces.Count >= 1)
                    || (ShownWhenTrace == ShowWhen.TwoSelected && !wipeSelect && selectedTraces.Count == 2)
                    || (ShownWhenTrace == ShowWhen.TwoPlusSelected && !wipeSelect && selectedTraces.Count >= 2)
                    || (ShownWhenTrace == ShowWhen.TwoPlusUnderMouse && !wipeSelect && rightTwoPlus)
                    || (ShownWhenTrace == ShowWhen.RightWipeSelect && wipeSelect)
                    ? (mousePoint & ShownWhenMouse) != 0
                    : false;
            }
        }

        internal void ContextMenuShow(SehensControl scope, PaintBoxMouseInfo paintBoxMouse, MouseEventArgs e, List<List<TraceView>> traces)
        {
            List<TraceView> selectedViews = paintBoxMouse.CombinedSelectedTraces(traces);
            string selected = String.Join(", ", selectedViews.Select(x => x.ViewName));
            bool rightTwoPlus = paintBoxMouse.RightClickGroup != null && paintBoxMouse.RightClickGroup!.Count > 1;
            bool wipeSelect = paintBoxMouse.ClickType == PaintBoxMouseInfo.Type.WipeSelect;

            foreach (MenuItem contextMenu in ContextMenuList)
            {
                if (contextMenu.Menu != null)
                {
                    contextMenu.Menu.Visible = false;
                }
            }
            foreach (MenuItem menuitem in ContextMenuList.Where(x => x.Menu != null))
            {
                bool valid = menuitem.Valid(traces, selectedViews, paintBoxMouse.MouseGuiSection, rightTwoPlus, wipeSelect);
                menuitem.Menu.Visible = valid;
                if (valid)
                {
                    if (menuitem.Parent != null)
                    {
                        menuitem.Parent.Visible = true;
                    }
                    menuitem.Menu.Text = menuitem.DisplayText(selected);
                    menuitem.GetStyle?.Invoke(new DropDownArgs()
                    {
                        Scope = scope,
                        Views = selectedViews,
                        Menu = menuitem,
                    });
                }
            }
            if (!ScopeMenuStrip.Visible)
            {
                ScopeMenuStrip.Show(scope, e.X, e.Y);
            }
        }

        public ScopeContextMenu(SehensPaintBox paintBox, SehensControl scope)
        {
            ScopeMenuStrip = new ContextMenuStrip();
            ContextMenus.AddContextMenus(ContextMenuList, EmbeddedContextMenuList);
            ImportExport.AddContextMenus(ContextMenuList, EmbeddedContextMenuList);

            ContextMenuList.Sort((a, b) => string.IsNullOrEmpty(a.SubMenuText) || string.IsNullOrEmpty(b.SubMenuText) ? 0 : b.Text.Replace("&", "").CompareTo(a.Text.Replace("&", "")));
            EmbeddedContextMenuList.Sort((a, b) => b.Sort == a.Sort ? b.Text.Replace("&", "").CompareTo(a.Text.Replace("&", "")) : (b.Sort - a.Sort));

            ScopeMenuStrip = new ContextMenuStrip();
            paintBox.ContextMenuStrip = ScopeMenuStrip;

            if (scope.SimpleUi) return;

            bool first = true;
            foreach (MenuItem contextMenu in ContextMenuList)
            {
                if (contextMenu.Menu == null)
                {
                    contextMenu.Menu = new ToolStripMenuItem();
                    contextMenu.Menu.Size = new Size(162, 22);
                    contextMenu.Menu.Click += (o, e) =>
                    {
                        if (!scope.SimpleUi)
                        {
                            ContextMenuList
                                .Where(x => (o as ToolStripMenuItem) == x.Menu)
                                .ForEach(x => scope.ContextMenuClick(contextMenu));
                        }
                    };
                    ToolStripItemCollection items = ScopeMenuStrip.Items;
                    if (contextMenu.SubMenuText.Length != 0)
                    {
                        foreach (var item in items)
                        {
                            if (item is ToolStripMenuItem)
                            {
                                ToolStripMenuItem toolStripMenuItem = (ToolStripMenuItem)item;
                                if (toolStripMenuItem.Text == contextMenu.SubMenuText)
                                {
                                    items = toolStripMenuItem.DropDownItems;
                                    contextMenu.Parent = toolStripMenuItem;
                                }
                            }
                        }
                        if (items == ScopeMenuStrip.Items)
                        {
                            ToolStripMenuItem item = new ToolStripMenuItem();
                            item.Size = new Size(162, 22);
                            item.Text = contextMenu.SubMenuText;
                            items.Insert(0, item);
                            items = item.DropDownItems;
                        }
                    }
                    if (items == ScopeMenuStrip.Items && first)
                    {
                        first = false;
                        items.Insert(0, new ToolStripSeparator());
                    }
                    items.Insert(0, contextMenu.Menu);
                }
            }

            foreach (var dup in ContextMenuList
                .Where(x => x.HotKeyCode != Keys.None)
                .GroupBy(x => new { x.HotKeyModifier, x.HotKeyCode })
                .Where(x => x.Count() > 1)
                .ToArray())
            {
                var issue = dup.FirstOrDefault()!;
                var names = string.Join(", ", dup.Select(x => x.Text));
                throw new Exception($"Duplicate hotkey {issue.HotKeyModifier}-{issue.HotKeyCode} in ({names})");
            }
        }

        internal void AddTrimHandles(TraceGroupDisplay info, Graphics graphics)
        {
            TraceView view = info.View0;
            if (!view.IsFftTrace && view.ViewOriginalSampleCount != 0 && info.View0.Group.Count == 1)
            {
                if ((view.ViewOverrideEnabled && view.ViewLengthOverride == 0 && view.ViewOffsetOverride == 0)
                    || (!view.ViewOverrideEnabled && (view.ViewLengthOverride != 0 || view.ViewOffsetOverride != 0)))
                {
                    int viewOffsetOverride = view.ViewOffsetOverride;
                    int num = ((view.ViewLengthOverride == 0) ? (view.ViewOriginalSampleCount - viewOffsetOverride) : view.ViewLengthOverride);
                    int leftSample = viewOffsetOverride + ((num != 0) ? (num - 1) : 0);

                    TraceViewEmbedHandle leftHandle = new TraceViewEmbedHandle(viewOffsetOverride, TraceFeature.Feature.LeftHandle, view);
                    leftHandle.Paint(info, graphics);
                    view.Painted.ClickZones.Add(leftHandle);

                    TraceViewEmbedHandle rightHandle = new TraceViewEmbedHandle(leftSample, TraceFeature.Feature.RightHandle, view);
                    rightHandle.Paint(info, graphics);
                    view.Painted.ClickZones.Add(rightHandle);
                }

                if (view.TriggerMode != 0)
                {
                    TraceViewEmbedHandle triggerHandle = new TraceViewEmbedHandle(info.LeftSampleNumber, TraceFeature.Feature.TriggerHandle, view);
                    triggerHandle.Paint(info, graphics);
                    view.Painted.ClickZones.Add(triggerHandle);
                }
            }
        }
    }
}
