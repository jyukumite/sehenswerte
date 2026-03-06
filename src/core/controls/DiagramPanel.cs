using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace SehensWerte.Controls
{
    // Generic pannable/zoomable diagram control with draggable nodes and bezier connecting edges.
    // Drag nodes by their header bar; edges stay connected with S-curve beziers.
    // Pan canvas with right-mouse drag. Zoom with mouse wheel (centered on cursor).
    // No scrollbars; nodes can be placed anywhere in diagram space.
    //
    // Usage:
    //   var panel = new DiagramPanel();
    //   panel.Nodes.Add(new DiagramNode {
    //       Id = "foo", Title = "Foo",
    //       Lines = new[]{"col1 int","col2 text"},
    //       LineColors = new Color?[]{ Color.LightBlue, null },
    //       LineTooltips = new string?[]{ "Primary key", null }
    //   });
    //   panel.Edges.Add(new DiagramEdge { FromId = "foo", ToId = "bar", FromLineIndex = 0, ToLineIndex = 1 });
    //   panel.ArrangeGrid();
    //   panel.Invalidate();
    public class DiagramPanel : Control
    {
        public class DiagramNode
        {
            public string Id = "";
            public string Title = "";
            public string[] LineLeftLabels = new string[0];
            public string?[] LineRightLabels = new string[0];
            public Color?[] LineColors = new Color?[0];
            public string?[] LineTooltips = new string[0];
            public Color HeaderColor = Color.SteelBlue;
            public PointF Position;
            internal SizeF Size;
            public bool Hide;
        }

        public class DiagramEdge
        {
            public string FromId = "";
            public string ToId = "";
            public string Label = "";
            public int FromLineIndex = -1;
            public int ToLineIndex = -1;
        }

        public List<DiagramNode> Nodes = new();
        public List<DiagramEdge> Edges = new();
        public string StatusText = "";
        public Func<string?, string?, ToolStripItem[]>? GetContextMenuItems;

        private PointF m_Pan = new(40, 40);
        private float m_Zoom = 1.0f;
        private string m_SearchText = "";

        // Canvas pan state (mouse drag)
        private DiagramNode? m_DragNode;
        private PointF m_DragOffset; // offset from node top-left in diagram coords
        private PointF m_MousePosition;
        private bool m_Panning;
        private Point m_PanStart;
        private PointF m_PanOrigin;
        private bool m_RightDragged;
        private Point m_RightClickStart;
        private DiagramNode? m_RightClickNode;
        private int m_RightClickLineIndex;

        // Tooltip state
        private readonly ToolTip m_Tooltip = new() { ShowAlways = true, AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 200 };
        private DiagramNode? m_TooltipNode;
        private int m_TooltipLine = -2;

        // Layout constants (diagram-space pixels, scaled by zoom at paint time)
        private const int HeaderHeight = 24;
        private const int RowHeight = 18;
        private const int NodePadding = 10;
        private const int MinNodeWidth = 160;
        private const float ArrowSize = 7f;
        private const float BezierTension = 120f;

        // Base fonts used for size measurement (zoom=1). Draw methods use ScaledHeaderFont/ScaledRowFont.
        private static readonly Font HeaderFont = new("Segoe UI", 9, FontStyle.Bold);
        private static readonly Font RowFont = new("Segoe UI", 8);

        // Cached fonts scaled to current zoom level
        private float m_CachedFontZoom = 0f;
        private Font? m_CachedHeaderFont;
        private Font? m_CachedRowFont;
        private Font ScaledHeaderFont => m_CachedHeaderFont ?? HeaderFont;
        private Font ScaledRowFont => m_CachedRowFont ?? RowFont;

        private static readonly Pen EdgePen = new(Color.FromArgb(130, 100, 80), 1.8f);
        private static readonly SolidBrush HeaderTextBrush = new(Color.White);
        private static readonly SolidBrush RowTextBrush = new(Color.Black);
        private static readonly SolidBrush RowBackBrush = new(Color.White);
        private static readonly SolidBrush AltRowBackBrush = new(Color.FromArgb(248, 248, 252));
        private static readonly Pen NodeBorderPen = new(Color.FromArgb(140, 140, 140), 1f);

        private static readonly Font SnackbarFont = new("Segoe UI", 8.5f);
        private static readonly SolidBrush SnackbarBgBrush = new(Color.FromArgb(200, 30, 30, 30));
        private static readonly SolidBrush SnackbarTextBrush = new(Color.FromArgb(210, 210, 210));
        private static readonly SolidBrush SnackbarSearchBrush = new(Color.FromArgb(130, 200, 255));
        private static readonly SolidBrush SnackbarKeyBrush = new(Color.FromArgb(255, 220, 100));

        public DiagramPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(238, 241, 246);
        }

        private void UpdateScaledFonts()
        {
            if (Math.Abs(m_CachedFontZoom - m_Zoom) < 0.005f) return;
            m_CachedHeaderFont?.Dispose();
            m_CachedRowFont?.Dispose();
            m_CachedHeaderFont = new Font("Segoe UI", Math.Max(4f, 9f * m_Zoom), FontStyle.Bold);
            m_CachedRowFont = new Font("Segoe UI", Math.Max(3f, 8f * m_Zoom));
            m_CachedFontZoom = m_Zoom;
        }

        public void ArrangeGrid(int columns = 0)
        {
            if (Nodes.Count == 0) return;
            EnsureSizes();
            PlaceInGrid(Nodes, columns);
        }

        // Arrange nodes so FK-connected ones end up adjacent in the grid, minimising edge lengths.
        // Uses a greedy nearest-neighbour ordering: start from the highest-degree node, then
        // always pick the unplaced node with the most connections to already-placed nodes.
        public void ArrangeByConnectivity(int columns = 0)
        {
            if (Nodes.Count == 0) return;
            EnsureSizes();
            PlaceInGrid(ConnectivityOrder(Nodes), columns);
        }

        // Re-run connectivity layout on visible nodes only, compacting them together.
        public void Relayout(int columns = 0)
        {
            var visible = Nodes.Where(n => !n.Hide).ToList();
            if (visible.Count == 0) return;
            EnsureSizes();
            int cols = columns > 0 ? columns : Math.Max(1, (int)Math.Ceiling(Math.Sqrt(visible.Count * 0.6)));
            PlaceInGrid(ConnectivityOrder(visible), cols);
            ZoomToFit();
        }

        public void ZoomToFit(float padding = 40f)
        {
            var visible = Nodes.Where(n => !n.Hide).ToList();
            if (visible.Count == 0 || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            EnsureSizes();
            float minX = visible.Min(n => n.Position.X);
            float minY = visible.Min(n => n.Position.Y);
            float maxX = visible.Max(n => n.Position.X + n.Size.Width);
            float maxY = visible.Max(n => n.Position.Y + n.Size.Height);
            float diagW = maxX - minX;
            float diagH = maxY - minY;
            if (diagW <= 0 || diagH <= 0) return;
            float scaleX = (ClientSize.Width - padding * 2) / diagW;
            float scaleY = (ClientSize.Height - padding * 2) / diagH;
            m_Zoom = Math.Max(0.15f, Math.Min(3.0f, Math.Min(scaleX, scaleY)));
            m_Pan = new PointF(padding - minX * m_Zoom, padding - minY * m_Zoom);
            UpdateScaledFonts();
            Invalidate();
        }

        private List<DiagramNode> ConnectivityOrder(List<DiagramNode> subset)
        {
            var degree = subset.ToDictionary(n => n.Id, _ => 0);
            var neighbours = subset.ToDictionary(n => n.Id, _ => new HashSet<string>());
            foreach (var edge in Edges)
            {
                if (!neighbours.ContainsKey(edge.FromId) || !neighbours.ContainsKey(edge.ToId)) continue;
                neighbours[edge.FromId].Add(edge.ToId);
                neighbours[edge.ToId].Add(edge.FromId);
                degree[edge.FromId]++;
                degree[edge.ToId]++;
            }

            var remaining = new HashSet<string>(subset.Select(n => n.Id));
            var ordered = new List<DiagramNode>(subset.Count);
            var nodeById = subset.ToDictionary(n => n.Id);

            var seed = subset.OrderByDescending(n => degree[n.Id]).First();
            ordered.Add(seed);
            remaining.Remove(seed.Id);

            while (remaining.Count > 0)
            {
                string nextId = remaining
                    .OrderByDescending(id => neighbours[id].Count(nb => !remaining.Contains(nb)))
                    .ThenByDescending(id => degree[id])
                    .First();
                ordered.Add(nodeById[nextId]);
                remaining.Remove(nextId);
            }

            return ordered;
        }

        private static void PlaceInGrid(List<DiagramNode> ordered, int columns)
        {
            int gridCols = columns > 0 ? columns : Math.Max(1, (int)Math.Ceiling(Math.Sqrt(ordered.Count)));
            float x = 40, y = 40;
            float maxH = 0;
            int col = 0;
            foreach (var node in ordered)
            {
                node.Position = new PointF(x, y);
                x += node.Size.Width + 60;
                maxH = Math.Max(maxH, node.Size.Height);
                if (++col >= gridCols)
                {
                    col = 0;
                    x = 40;
                    y += maxH + 50;
                    maxH = 0;
                }
            }
        }

        private void EnsureSizes()
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            foreach (var node in Nodes)
                if (node.Size.IsEmpty)
                    node.Size = MeasureNode(g, node);
        }

        private static SizeF MeasureNode(Graphics g, DiagramNode node)
        {
            float w = g.MeasureString(node.Title, HeaderFont).Width + NodePadding * 2;
            for (int loop = 0; loop < node.LineLeftLabels.Length; loop++)
            {
                float lw = g.MeasureString(node.LineLeftLabels[loop], RowFont).Width;
                if (loop < node.LineRightLabels.Length && node.LineRightLabels[loop] != null)
                {
                    lw += g.MeasureString(node.LineRightLabels[loop]!, RowFont).Width + NodePadding;
                }
                w = Math.Max(w, lw + NodePadding * 2);
            }
            w = Math.Max(MinNodeWidth, w + 4);
            float h = HeaderHeight + node.LineLeftLabels.Length * RowHeight + NodePadding + 4;
            return new SizeF(w, h);
        }

        private PointF DiagramToScreen(PointF p) => new(p.X * m_Zoom + m_Pan.X, p.Y * m_Zoom + m_Pan.Y);

        private PointF ScreenToDiagram(PointF p) => new((p.X - m_Pan.X) / m_Zoom, (p.Y - m_Pan.Y) / m_Zoom);

        private RectangleF NodeScreenRect(DiagramNode n)
        {
            var tl = DiagramToScreen(n.Position);
            return new RectangleF(tl.X, tl.Y, n.Size.Width * m_Zoom, n.Size.Height * m_Zoom);
        }

        public DiagramNode? NodeUnderMouse() => HitTestAnyNode(m_MousePosition);

        private DiagramNode? HitTestAnyNode(PointF diagPt)
        {
            for (int loop = Nodes.Count - 1; loop >= 0; loop--)
            {
                var node = Nodes[loop];
                if (node.Hide) continue;
                var rect = new RectangleF(node.Position, node.Size);
                if (rect.Contains(diagPt)) return node;
            }
            return null;
        }

        private DiagramNode? HitTestHeader(PointF screenPt)
        {
            float headerScreenH = HeaderHeight * m_Zoom;
            for (int loop = Nodes.Count - 1; loop >= 0; loop--)
            {
                var node = Nodes[loop];
                if (node.Hide) continue;
                var r = NodeScreenRect(node);
                var headerRect = new RectangleF(r.X, r.Y, r.Width, headerScreenH);
                if (headerRect.Contains(screenPt))
                {
                    return node;
                }
            }
            return null;
        }

        private (DiagramNode? node, int lineIndex) HitTestRow(PointF screenPt)
        {
            for (int loop = Nodes.Count - 1; loop >= 0; loop--)
            {
                var node = Nodes[loop];
                var rect = NodeScreenRect(node);
                if (!rect.Contains(screenPt)) continue;
                float headerH = HeaderHeight * m_Zoom;
                float rowH = RowHeight * m_Zoom;
                if (screenPt.Y < rect.Y + headerH) return (node, -1); // header
                int lineIdx = (int)((screenPt.Y - rect.Y - headerH) / rowH);
                if (lineIdx >= 0 && lineIdx < node.LineLeftLabels.Length) return (node, lineIdx);
                return (node, -1);
            }
            return (null, -1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                var hit = HitTestHeader(e.Location);
                if (hit != null)
                {
                    m_DragNode = hit;
                    var diag = ScreenToDiagram(e.Location);
                    m_DragOffset = new PointF(diag.X - hit.Position.X, diag.Y - hit.Position.Y);
                    // Bring dragged node to front so it renders on top
                    BringToTop(hit);
                    Cursor = Cursors.SizeAll;
                    m_Tooltip.Hide(this);
                }
                else
                {
                    m_Panning = true;
                    m_PanStart = e.Location;
                    m_PanOrigin = m_Pan;
                    Cursor = Cursors.Hand;
                    m_Tooltip.Hide(this);
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                m_Panning = true;
                m_PanStart = e.Location;
                m_PanOrigin = m_Pan;
                m_RightClickStart = e.Location;
                m_RightDragged = false;
                (m_RightClickNode, m_RightClickLineIndex) = HitTestRow(e.Location);
                Cursor = Cursors.Hand;
                m_Tooltip.Hide(this);
            }
        }

        private void BringToTop(DiagramNode node)
        {
            Nodes.Remove(node);
            Nodes.Add(node);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            m_MousePosition = ScreenToDiagram(e.Location);
            if (m_DragNode != null)
            {
                var diag = ScreenToDiagram(e.Location);
                m_DragNode.Position = new PointF(diag.X - m_DragOffset.X, diag.Y - m_DragOffset.Y);
                Invalidate();
            }
            else if (m_Panning)
            {
                if (Math.Abs(e.X - m_RightClickStart.X) > 4 || Math.Abs(e.Y - m_RightClickStart.Y) > 4)
                {
                    m_RightDragged = true;
                }
                m_Pan = new PointF(m_PanOrigin.X + (e.X - m_PanStart.X), m_PanOrigin.Y + (e.Y - m_PanStart.Y));
                Invalidate();
            }
            else
            {
                Cursor = HitTestHeader(e.Location) != null ? Cursors.SizeAll : Cursors.Default;
                UpdateTooltip(e.Location);
            }
        }

        private void UpdateTooltip(Point mousePos)
        {
            var (hitNode, hitLine) = HitTestRow(mousePos);
            if (hitNode == m_TooltipNode && hitLine == m_TooltipLine) return; // no change

            m_TooltipNode = hitNode;
            m_TooltipLine = hitLine;

            if (hitNode != null && hitLine >= 0
                && hitLine < hitNode.LineTooltips.Length
                && !string.IsNullOrEmpty(hitNode.LineTooltips[hitLine]))
            {
                m_Tooltip.Show(hitNode.LineTooltips[hitLine]!, this, mousePos.X + 14, mousePos.Y + 14, 8000);
            }
            else
            {
                m_Tooltip.Hide(this);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            m_DragNode = null;
            m_Panning = false;
            Cursor = Cursors.Default;
            if (e.Button == MouseButtons.Right && !m_RightDragged)
            {
                ShowNodeContextMenu(e.Location, m_RightClickNode, m_RightClickLineIndex);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            m_Tooltip.Hide(this);
            m_TooltipNode = null;
            m_TooltipLine = -2;
        }

        public Bitmap RenderToBitmap()
        {
            var visible = Nodes.Where(n => !n.Hide).ToList();
            if (visible.Count == 0) return new Bitmap(1, 1);
            EnsureSizes();

            float minX = visible.Min(n => n.Position.X);
            float minY = visible.Min(n => n.Position.Y);
            float maxX = visible.Max(n => n.Position.X + n.Size.Width);
            float maxY = visible.Max(n => n.Position.Y + n.Size.Height);

            const float pad = 40f;
            const float renderZoom = 1.0f;

            int bmpW = (int)((maxX - minX + pad * 2) * renderZoom);
            int bmpH = (int)((maxY - minY + pad * 2) * renderZoom);

            var savedZoom = m_Zoom;
            var savedPan = m_Pan;
            m_Zoom = renderZoom;
            m_Pan = new PointF((pad - minX) * renderZoom, (pad - minY) * renderZoom);
            UpdateScaledFonts();

            var bmp = new Bitmap(Math.Max(1, bmpW), Math.Max(1, bmpH));
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(BackColor);
                DrawEdges(g);
                DrawNodes(g);
            }

            m_Zoom = savedZoom;
            m_Pan = savedPan;
            UpdateScaledFonts();
            return bmp;
        }

        public void HideAllExceptConnectedTo(DiagramNode focus)
        {
            var connected = new HashSet<string> { focus.Id };
            foreach (var edge in Edges)
            {
                if (edge.FromId == focus.Id)
                {
                    connected.Add(edge.ToId);
                }
                if (edge.ToId == focus.Id)
                {
                    connected.Add(edge.FromId);
                }
            }
            foreach (var node in Nodes)
            {
                node.Hide = !connected.Contains(node.Id);
            }

            Invalidate();
        }

        public void HideAllExceptConnectedToTransitive(DiagramNode focus)
        {
            var connected = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(focus.Id);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (connected.Add(id))
                {
                    foreach (var edge in Edges)
                    {
                        if (edge.FromId == id && !connected.Contains(edge.ToId))
                        {
                            queue.Enqueue(edge.ToId);
                        }
                        if (edge.ToId == id && !connected.Contains(edge.FromId))
                        {
                            queue.Enqueue(edge.FromId);
                        }
                    }
                }
            }
            foreach (var node in Nodes)
            {
                node.Hide = !connected.Contains(node.Id);
            }
            Invalidate();
        }

        private void ShowNodeContextMenu(Point screenPt, DiagramNode? node, int lineIndex)
        {
            var menu = new ContextMenuStrip();

            if (GetContextMenuItems != null)
            {
                string? tableName = node?.Id;
                string? fieldName = node != null && lineIndex >= 0 && lineIndex < node.LineLeftLabels.Length
                    ? node.LineLeftLabels[lineIndex].Split(' ')[0]
                    : null;
                var extra = GetContextMenuItems(tableName, fieldName);
                foreach (var item in extra) menu.Items.Add(item);
                if (extra.Length > 0) menu.Items.Add(new ToolStripSeparator());
            }

            if (node != null)
            {
                var hideItem = new ToolStripMenuItem(node.Hide ? "Show" : "Hide");
                hideItem.Click += (_, __) => { node.Hide = !node.Hide; Invalidate(); };
                menu.Items.Add(hideItem);

                var isolateDirect = new ToolStripMenuItem("Isolate to directly connected nodes");
                isolateDirect.Click += (_, __) => HideAllExceptConnectedTo(node);
                menu.Items.Add(isolateDirect);

                var isolateAll = new ToolStripMenuItem("Isolate to all connected nodes");
                isolateAll.Click += (_, __) => HideAllExceptConnectedToTransitive(node);
                menu.Items.Add(isolateAll);

                menu.Items.Add(new ToolStripSeparator());
            }

            var showAll = new ToolStripMenuItem("Show all");
            showAll.Click += (_, __) => { foreach (var n in Nodes) n.Hide = false; Invalidate(); };
            menu.Items.Add(showAll);
            menu.Show(this, screenPt);
        }

        public void ApplySearchKey(char c)
        {
            if (c == 27) // Escape
            {
                m_SearchText = "";
            }
            else if (c == 8) // Backspace
            {
                if (m_SearchText.Length > 0)
                    m_SearchText = m_SearchText[..^1];
            }
            else if (c == 9) // Tab
            {
                var hit = HitTestAnyNode(m_MousePosition);
                if (hit != null)
                {
                    hit.Hide = !hit.Hide;
                }
            }
            else if (!char.IsControl(c))
            {
                m_SearchText += c;
            }

            Invalidate();

            if (m_SearchText.Length != 0)
            {
                // Pan to best matching node: exact > starts-with > contains, then shortest title wins
                var match = Nodes
                    .Where(n => !n.Hide && n.Title.Contains(m_SearchText, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(n => n.Title.Equals(m_SearchText, StringComparison.OrdinalIgnoreCase) ? 0
                                : n.Title.StartsWith(m_SearchText, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                    .ThenBy(n => n.Title.Length)
                    .FirstOrDefault();
                if (match != null)
                {
                    EnsureSizes();
                    float panX = ClientSize.Width / 2f - (match.Position.X + match.Size.Width / 2f) * m_Zoom;
                    float panY = ClientSize.Height / 2f - (match.Position.Y + match.Size.Height / 2f) * m_Zoom;
                    m_Pan = new PointF(panX, panY);
                    Invalidate();
                }
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float oldZoom = m_Zoom;
            float factor = e.Delta > 0 ? 1.1f : 0.909f;
            m_Zoom = Math.Max(0.15f, Math.Min(3.0f, m_Zoom * factor));
            m_Pan = new PointF(
                e.X - (e.X - m_Pan.X) * (m_Zoom / oldZoom),
                e.Y - (e.Y - m_Pan.Y) * (m_Zoom / oldZoom));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            UpdateScaledFonts();
            EnsureSizes();
            DrawEdges(g);
            DrawNodes(g);
            DrawSnackbar(g);
        }

        private void DrawSnackbar(Graphics g)
        {
            // Build segments: alternating key/description pairs then optional search
            // Each segment: (text, brush)
            var segments = new List<(string text, SolidBrush brush)>
            {
                ("Drag header", SnackbarKeyBrush), ("=move", SnackbarTextBrush),
                ("   Drag", SnackbarKeyBrush), ("=pan", SnackbarTextBrush),
                ("   Scroll", SnackbarKeyBrush), ("=zoom", SnackbarTextBrush),
                ("   Right-click", SnackbarKeyBrush), ("=menu", SnackbarTextBrush),
                ("   Ctrl+C", SnackbarKeyBrush), ("=copy", SnackbarTextBrush),
                ("   Ctrl+S", SnackbarKeyBrush), ("=save png", SnackbarTextBrush),
                ("   Esc", SnackbarKeyBrush), ("=clear", SnackbarTextBrush),
            };
            if (m_SearchText.Length > 0)
            {
                segments.Add(("  Search: ", SnackbarTextBrush));
                segments.Add((m_SearchText, SnackbarSearchBrush));
            }
            if (StatusText.Length > 0)
            {
                segments.Add(("   |   ", SnackbarTextBrush));
                segments.Add((StatusText, SnackbarTextBrush));
            }

            float pad = 8f;
            float h = g.MeasureString("Ag", SnackbarFont).Height + pad;
            float y = ClientSize.Height - h;
            float totalW = segments.Sum(s => g.MeasureString(s.text, SnackbarFont).Width);
            totalW -= segments.Count * 3f;
            float barX = Math.Max(0, ClientSize.Width / 2f - totalW / 2f - pad);
            float barW = Math.Min(ClientSize.Width, totalW + pad * 2);

            g.FillRectangle(SnackbarBgBrush, barX, y, barW, h);
            float x = barX + pad;
            float ty = y + pad / 2f;
            foreach (var (text, brush) in segments)
            {
                g.DrawString(text, SnackbarFont, brush, x, ty);
                x += g.MeasureString(text, SnackbarFont).Width - 3f;
            }
        }

        private void DrawEdges(Graphics g)
        {
            var nodeMap = Nodes.ToDictionary(n => n.Id);
            foreach (var edge in Edges)
            {
                if (!nodeMap.TryGetValue(edge.FromId, out var from)) continue;
                if (!nodeMap.TryGetValue(edge.ToId, out var to)) continue;
                if (from == to || from.Hide || to.Hide) continue;

                var (fromPt, toPt, fromRight, toRight) = GetPortPoints(from, to, edge.FromLineIndex, edge.ToLineIndex);
                float tension = BezierTension * m_Zoom;
                var cp1 = new PointF(fromPt.X + (fromRight ? tension : -tension), fromPt.Y);
                var cp2 = new PointF(toPt.X + (toRight ? tension : -tension), toPt.Y);

                g.DrawBezier(EdgePen, fromPt, cp1, cp2, toPt);
                DrawArrowhead(g, cp2, toPt);
                if (!string.IsNullOrEmpty(edge.Label))
                {
                    var mid = new PointF(
                        (fromPt.X + cp1.X + cp2.X + toPt.X) / 4f,
                        (fromPt.Y + cp1.Y + cp2.Y + toPt.Y) / 4f);
                    DrawEdgeLabel(g, edge.Label, mid);
                }
            }
        }

        private (PointF fromPt, PointF toPt, bool fromRight, bool toRight) GetPortPoints(
            DiagramNode from, DiagramNode to, int fromLineIndex, int toLineIndex)
        {
            var rf = NodeScreenRect(from);
            var rt = NodeScreenRect(to);
            float headerH = HeaderHeight * m_Zoom;
            float rowH = RowHeight * m_Zoom;
            float fromY = fromLineIndex >= 0
                ? rf.Y + headerH + (fromLineIndex + 0.5f) * rowH
                : rf.Y + rf.Height / 2f;
            float toY = toLineIndex >= 0
                ? rt.Y + headerH + (toLineIndex + 0.5f) * rowH
                : rt.Y + rt.Height / 2f;

            float dRightLeft = MathF.Abs(rf.Right - rt.Left);
            float dLeftRight = MathF.Abs(rf.Left - rt.Right);
            bool fromRight = dRightLeft <= dLeftRight;
            bool toRight = !fromRight;
            var fromPt = fromRight ? new PointF(rf.Right, fromY) : new PointF(rf.Left, fromY);
            var toPt = toRight ? new PointF(rt.Right, toY) : new PointF(rt.Left, toY);

            return (fromPt, toPt, fromRight, toRight);
        }

        private void DrawArrowhead(Graphics g, PointF cp2, PointF tip)
        {
            float dx = tip.X - cp2.X, dy = tip.Y - cp2.Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len >= 0.5f)
            {
                float ux = dx / len, uy = dy / len;
                float aLen = ArrowSize * m_Zoom;
                float aW = aLen * 0.45f;
                var b1 = new PointF(tip.X - ux * aLen - uy * aW, tip.Y - uy * aLen + ux * aW);
                var b2 = new PointF(tip.X - ux * aLen + uy * aW, tip.Y - uy * aLen - ux * aW);
                using var brush = new SolidBrush(EdgePen.Color);
                g.FillPolygon(brush, new[] { tip, b1, b2 });
            }
        }

        private void DrawEdgeLabel(Graphics g, string label, PointF mid)
        {
            var sz = g.MeasureString(label, ScaledRowFont);
            using var bg = new SolidBrush(Color.FromArgb(210, 255, 255, 220));
            g.FillRectangle(bg, mid.X - sz.Width / 2 - 2, mid.Y - sz.Height / 2, sz.Width + 4, sz.Height);
            g.DrawString(label, ScaledRowFont, RowTextBrush, mid.X - sz.Width / 2, mid.Y - sz.Height / 2);
        }

        private void DrawNodes(Graphics g)
        {
            foreach (var node in Nodes)
            {
                if (!node.Hide) DrawNode(g, node);
            }
        }

        private void DrawNodeTitle(Graphics g, DiagramNode node, RectangleF r, float headerH)
        {
            float tx = r.X + 5 * m_Zoom;
            float ty = r.Y + 3 * m_Zoom;
            float tw = r.Width - 8 * m_Zoom;
            float th = headerH - 4 * m_Zoom;

            int matchIdx = m_SearchText.Length > 0
                ? node.Title.IndexOf(m_SearchText, StringComparison.OrdinalIgnoreCase)
                : -1;

            if (matchIdx < 0)
            {
                g.DrawString(node.Title, ScaledHeaderFont, HeaderTextBrush, new RectangleF(tx, ty, tw, th));
            }
            else
            {
                string before = node.Title[..matchIdx];
                string match = node.Title.Substring(matchIdx, m_SearchText.Length);
                string after = node.Title[(matchIdx + m_SearchText.Length)..];

                var fmt = StringFormat.GenericTypographic;
                fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                float beforeW = MeasureW(before);
                float matchW = MeasureW(match);

                if (before.Length > 0)
                {
                    g.DrawString(before, ScaledHeaderFont, HeaderTextBrush, new RectangleF(tx, ty, tw, th), fmt);
                }
                using var blueBrush = new SolidBrush(Color.FromArgb(140, 220, 255));
                g.DrawString(match, ScaledHeaderFont, blueBrush, new RectangleF(tx + beforeW, ty, tw - beforeW, th), fmt);
                if (after.Length > 0)
                {
                    g.DrawString(after, ScaledHeaderFont, HeaderTextBrush, new RectangleF(tx + beforeW + matchW, ty, tw - beforeW - matchW, th), fmt);
                }

                float MeasureW(string s)
                {
                    if (s.Length == 0) return 0f;
                    var ranges = new CharacterRange[] { new(0, s.Length) };
                    fmt.SetMeasurableCharacterRanges(ranges);
                    var regions = g.MeasureCharacterRanges(s, ScaledHeaderFont, new RectangleF(0, 0, tw, th), fmt);
                    return regions[0].GetBounds(g).Width;
                }
            }
        }

        private void DrawNode(Graphics g, DiagramNode node)
        {
            var rect = NodeScreenRect(node);
            if (!g.ClipBounds.IntersectsWith(rect)) return;

            using (var shadowBrush = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
            {
                g.FillRectangle(shadowBrush, rect.X + 3, rect.Y + 4, rect.Width, rect.Height);
            }

            g.FillRectangle(RowBackBrush, rect);

            float headerH = HeaderHeight * m_Zoom;
            using (var hBrush = new SolidBrush(node.HeaderColor))
            {
                g.FillRectangle(hBrush, rect.X, rect.Y, rect.Width, headerH);
            }

            DrawNodeTitle(g, node, rect, headerH);

            float rowH = RowHeight * m_Zoom;
            float y = rect.Y + headerH;
            for (int loop = 0; loop < node.LineLeftLabels.Length; loop++)
            {
                Color rowBg;
                if (loop < node.LineColors.Length && node.LineColors[loop].HasValue)
                {
                    rowBg = node.LineColors[loop]!.Value;
                }
                else
                {
                    rowBg = loop % 2 == 0 ? Color.White : Color.FromArgb(248, 248, 252);
                }
                using (var rb = new SolidBrush(rowBg))
                {
                    g.FillRectangle(rb, rect.X, y, rect.Width, rowH);
                }
                g.DrawString(node.LineLeftLabels[loop], ScaledRowFont, RowTextBrush,
                    new RectangleF(rect.X + 5 * m_Zoom, y + 1 * m_Zoom, rect.Width - 8 * m_Zoom, rowH));
                if (loop < node.LineRightLabels.Length && node.LineRightLabels[loop] != null)
                {
                    var rl = node.LineRightLabels[loop]!;
                    float rlW = g.MeasureString(rl, ScaledRowFont).Width;
                    using var dimBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
                    g.DrawString(rl, ScaledRowFont, dimBrush,
                        new RectangleF(rect.Right - rlW - 4 * m_Zoom, y + 1 * m_Zoom, rlW, rowH));
                }
                y += rowH;
            }
            g.DrawRectangle(NodeBorderPen, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }
}

