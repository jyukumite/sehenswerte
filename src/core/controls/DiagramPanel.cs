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
            public bool Visible = true;
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
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
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

        // Arrange all nodes so FK-connected ones end up adjacent in the grid, minimising edge lengths.
        // Uses a greedy nearest-neighbour ordering: start from the highest-degree node, then
        // always pick the unplaced node with the most connections to already-placed nodes.
        public void ArrangeByConnectivity(int columns = 0)
        {
            if (Nodes.Count == 0) return;
            EnsureSizes();
            PlaceInGrid(OrderByConnectivity(Nodes), columns);
        }

        // Re-arrange visible nodes by connectivity grid layout, then zoom to fit.
        public void ArrangeByConnectivityGrid()
        {
            var visible = Nodes.Where(n => n.Visible).ToList();
            if (visible.Count == 0) return;
            EnsureSizes();
            PlaceInGrid(OrderByConnectivity(visible), Math.Max(1, (int)Math.Ceiling(Math.Sqrt(visible.Count * 0.6))));
            ZoomToFit();
        }

        // Re-arrange visible nodes using force-directed layout, then zoom to fit.
        public void ArrangeForceDirected()
        {
            var visible = Nodes.Where(n => n.Visible).ToList();
            if (visible.Count == 0) return;
            EnsureSizes();
            ForceDirectedLayout(visible);
            ZoomToFit();
        }

        // Re-arrange visible nodes using hierarchical layout, then zoom to fit.
        public void ArrangeHierarchical()
        {
            var visible = Nodes.Where(n => n.Visible).ToList();
            if (visible.Count == 0) return;
            EnsureSizes();
            HierarchicalLayout(visible);
            ZoomToFit();
        }

        public void ZoomToFit(float padding = 40f)
        {
            var visible = Nodes.Where(n => n.Visible).ToList();
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

        private List<DiagramNode> OrderByConnectivity(List<DiagramNode> subset)
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

        private void PlaceInGrid(List<DiagramNode> ordered, int columns)
        {
            EnsureSizes();
            if (ordered.Count == 0) return;

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
            Invalidate();
        }

        private void ForceDirectedLayout(List<DiagramNode> nodes)
        {
            int n = nodes.Count;
            float avgW = nodes.Sum(nd => nd.Size.Width) / n;
            float avgH = nodes.Sum(nd => nd.Size.Height) / n;
            // Canvas sized to total node area x 5 keeps nodes packed rather than spread
            float canvasArea = n * avgW * avgH * 5f;
            float W = (float)Math.Sqrt(canvasArea * 4.0 / 3.0);
            float H = W * 3f / 4f;
            float k = (float)Math.Sqrt(W * H / n);
            // Per-pair minimum distance avoids overlap; gravity prevents drift to edges
            float gravity = k * 0.012f;

            var rng = new Random(42);
            float[] px = new float[n], py = new float[n];
            for (int i = 0; i < n; i++)
            {
                px[i] = W / 2 + (float)(rng.NextDouble() - 0.5) * W * 0.4f;
                py[i] = H / 2 + (float)(rng.NextDouble() - 0.5) * H * 0.4f;
            }

            float[] ddx = new float[n], ddy = new float[n];
            var idToIdx = new Dictionary<string, int>();
            for (int i = 0; i < n; i++) idToIdx[nodes[i].Id] = i;
            var edgePairs = Edges
                .Where(e => idToIdx.ContainsKey(e.FromId) && idToIdx.ContainsKey(e.ToId))
                .Select(e => (idToIdx[e.FromId], idToIdx[e.ToId]))
                .ToList();

            float temp = W / 5f;
            float cooling = temp / 350f;
            for (int iter = 0; iter < 350; iter++)
            {
                Array.Clear(ddx, 0, n);
                Array.Clear(ddy, 0, n);

                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        float ex = px[i] - px[j], ey = py[i] - py[j];
                        float minD = (nodes[i].Size.Width + nodes[j].Size.Width + nodes[i].Size.Height + nodes[j].Size.Height) / 4f + 20f;
                        float dist = Math.Max(minD, (float)Math.Sqrt(ex * ex + ey * ey));
                        float f = k * k / dist;
                        float fx = ex / dist * f, fy = ey / dist * f;
                        ddx[i] += fx; ddy[i] += fy;
                        ddx[j] -= fx; ddy[j] -= fy;
                    }
                }

                foreach (var (a, b) in edgePairs)
                {
                    float ex = px[b] - px[a], ey = py[b] - py[a];
                    float dist = Math.Max(1f, (float)Math.Sqrt(ex * ex + ey * ey));
                    float f = dist * dist / k;
                    float fx = ex / dist * f, fy = ey / dist * f;
                    ddx[a] += fx; ddy[a] += fy;
                    ddx[b] -= fx; ddy[b] -= fy;
                }

                // Gravity toward centre prevents isolated nodes drifting to edges
                for (int i = 0; i < n; i++)
                {
                    ddx[i] += gravity * (W / 2 - px[i]);
                    ddy[i] += gravity * (H / 2 - py[i]);
                }

                for (int i = 0; i < n; i++)
                {
                    float dispLen = Math.Max(1f, (float)Math.Sqrt(ddx[i] * ddx[i] + ddy[i] * ddy[i]));
                    float move = Math.Min(dispLen, temp);
                    px[i] = Math.Max(0, Math.Min(W, px[i] + ddx[i] / dispLen * move));
                    py[i] = Math.Max(0, Math.Min(H, py[i] + ddy[i] / dispLen * move));
                }
                temp = Math.Max(1f, temp - cooling);
            }

            for (int i = 0; i < n; i++)
            {
                nodes[i].Position = new PointF(px[i] - nodes[i].Size.Width / 2, py[i] - nodes[i].Size.Height / 2);
            }

            RemoveOverlaps(nodes, 60);
        }

        private void RemoveOverlaps(List<DiagramNode> nodes, int passes, DiagramNode? pinned = null)
        {
            const float gap = 35f;
            for (int pass = 0; pass < passes; pass++)
            {
                bool any = false;
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        var a = nodes[i]; var b = nodes[j];
                        bool aPin = a == pinned, bPin = b == pinned;
                        float ex = (a.Position.X + a.Size.Width / 2) - (b.Position.X + b.Size.Width / 2);
                        float ey = (a.Position.Y + a.Size.Height / 2) - (b.Position.Y + b.Size.Height / 2);
                        float ox = (a.Size.Width + b.Size.Width) / 2 + gap - Math.Abs(ex);
                        float oy = (a.Size.Height + b.Size.Height) / 2 + gap - Math.Abs(ey);
                        if (ox <= 0 || oy <= 0) continue;
                        any = true;
                        float scale = (aPin || bPin) ? 1f : 0.5f;
                        if (ox < oy)
                        {
                            float push = (ox + 1) * scale;
                            float sign = ex >= 0 ? 1 : -1;
                            if (!aPin) a.Position = new PointF(a.Position.X + sign * push, a.Position.Y);
                            if (!bPin) b.Position = new PointF(b.Position.X - sign * push, b.Position.Y);
                        }
                        else
                        {
                            float push = (oy + 1) * scale;
                            float sign = ey >= 0 ? 1 : -1;
                            if (!aPin) a.Position = new PointF(a.Position.X, a.Position.Y + sign * push);
                            if (!bPin) b.Position = new PointF(b.Position.X, b.Position.Y - sign * push);
                        }
                    }
                }
                if (!any) break;
            }
        }

        private void HierarchicalLayout(List<DiagramNode> nodes)
        {
            var idSet = new HashSet<string>(nodes.Select(n => n.Id));
            var outgoing = nodes.ToDictionary(n => n.Id, _ => new HashSet<string>());
            var incoming = nodes.ToDictionary(n => n.Id, _ => new HashSet<string>());
            foreach (var e in Edges)
            {
                if (!idSet.Contains(e.FromId) || !idSet.Contains(e.ToId) || e.FromId == e.ToId) continue;
                outgoing[e.FromId].Add(e.ToId);
                incoming[e.ToId].Add(e.FromId);
            }

            // Layer = max layer of referenced tables + 1; tables referencing nobody = layer 0 (top)
            int maxDepth = Math.Max(6, (int)Math.Ceiling(Math.Log(nodes.Count + 1, 2)) + 2);
            var layer = nodes.ToDictionary(n => n.Id, _ => 0);
            for (int iter = 0; iter < nodes.Count; iter++)
            {
                bool changed = false;
                foreach (var nd in nodes)
                {
                    if (outgoing[nd.Id].Count == 0) continue;
                    int want = Math.Min(maxDepth, outgoing[nd.Id].Max(nb => layer[nb]) + 1);
                    if (want > layer[nd.Id]) { layer[nd.Id] = want; changed = true; }
                }
                if (!changed) break;
            }

            var byLayer = nodes.GroupBy(n => layer[n.Id]).OrderBy(g => g.Key).ToList();

            // Barycenter sweep to reduce crossings
            var xCtr = nodes.ToDictionary(n => n.Id, n => n.Position.X + n.Size.Width / 2);
            for (int pass = 0; pass < 6; pass++)
            {
                var sweep = pass % 2 == 0 ? (IEnumerable<IGrouping<int, DiagramNode>>)byLayer : byLayer.AsEnumerable().Reverse();
                foreach (var lg in sweep)
                {
                    var sorted = lg.OrderBy(nd =>
                    {
                        var nbrs = outgoing[nd.Id].Union(incoming[nd.Id]).Where(nb => layer[nb] != layer[nd.Id]);
                        var xs = nbrs.Select(nb => xCtr[nb]).ToList();
                        return xs.Count > 0 ? xs.Average() : xCtr[nd.Id];
                    }).ToList();
                    float x = 40f;
                    foreach (var nd in sorted) { xCtr[nd.Id] = x + nd.Size.Width / 2f; x += nd.Size.Width + 60f; }
                }
            }

            float y = 40f;
            var layerY = new Dictionary<int, float>();
            foreach (var lg in byLayer) { layerY[lg.Key] = y; y += lg.Max(nd => nd.Size.Height) + 80f; }

            foreach (var nd in nodes)
                nd.Position = new PointF(Math.Max(0, xCtr[nd.Id] - nd.Size.Width / 2), layerY[layer[nd.Id]]);
        }

        private void EnsureSizes()
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            foreach (var node in Nodes)
            {
                if (node.Size.IsEmpty)
                {
                    node.Size = MeasureNode(g, node);
                }
            }
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
                var rect = new RectangleF(node.Position, node.Size);
                if (rect.Contains(diagPt) && node.Visible)
                {
                    return node;
                }
            }
            return null;
        }

        private DiagramNode? HitTestHeader(PointF screenPt)
        {
            float headerScreenH = HeaderHeight * m_Zoom;
            for (int loop = Nodes.Count - 1; loop >= 0; loop--)
            {
                var node = Nodes[loop];
                var r = NodeScreenRect(node);
                var headerRect = new RectangleF(r.X, r.Y, r.Width, headerScreenH);
                if (headerRect.Contains(screenPt) && node.Visible)
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
                if (rect.Contains(screenPt) && node.Visible)
                {
                    float headerH = HeaderHeight * m_Zoom;
                    float rowH = RowHeight * m_Zoom;
                    if (screenPt.Y < rect.Y + headerH) return (node, -1); // header
                    int lineIdx = (int)((screenPt.Y - rect.Y - headerH) / rowH);
                    if (lineIdx >= 0 && lineIdx < node.LineLeftLabels.Length) return (node, lineIdx);
                    return (node, -1);
                }
            }
            return (null, -1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
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

        private void ShovePushApart(DiagramNode pinned) =>
            RemoveOverlaps(Nodes.Where(n => n.Visible).ToList(), 5, pinned);

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
                ShovePushApart(m_DragNode);
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
            var visible = Nodes.Where(n => n.Visible).ToList();
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

        public void ShowDirectlyConnected(DiagramNode focus)
        {
            foreach (var edge in Edges)
            {
                if (edge.FromId == focus.Id)
                {
                    var n = Nodes.FirstOrDefault(n => n.Id == edge.ToId);
                    if (n != null) { n.Visible = true; }
                }
                if (edge.ToId == focus.Id)
                {
                    var n = Nodes.FirstOrDefault(n => n.Id == edge.FromId);
                    if (n != null) { n.Visible = true; }
                }
            }
            Invalidate();
        }

        public void HideAllExceptConnectedTo(DiagramNode focus)
        {
            var visible = new HashSet<string>(Nodes.Where(n => n.Visible).Select(n => n.Id));
            var connected = new HashSet<string> { focus.Id };
            foreach (var edge in Edges)
            {
                if (edge.FromId == focus.Id && visible.Contains(edge.ToId))
                {
                    connected.Add(edge.ToId);
                }
                if (edge.ToId == focus.Id && visible.Contains(edge.FromId))
                {
                    connected.Add(edge.FromId);
                }
            }
            foreach (var node in Nodes)
            {
                node.Visible = connected.Contains(node.Id);
            }
            Invalidate();
        }

        public void HideAllExceptConnectedToTransitive(DiagramNode focus)
        {
            var visible = new HashSet<string>(Nodes.Where(n => n.Visible).Select(n => n.Id));
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
                        if (edge.FromId == id && !connected.Contains(edge.ToId) && visible.Contains(edge.ToId))
                        {
                            queue.Enqueue(edge.ToId);
                        }
                        if (edge.ToId == id && !connected.Contains(edge.FromId) && visible.Contains(edge.FromId))
                        {
                            queue.Enqueue(edge.FromId);
                        }
                    }
                }
            }
            foreach (var node in Nodes)
            {
                node.Visible = connected.Contains(node.Id);
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
                var hideItem = new ToolStripMenuItem("Hide");
                hideItem.Click += (_, __) => { node.Visible = false; Invalidate(); };
                menu.Items.Add(hideItem);

                var showConnected = new ToolStripMenuItem("Show directly connected nodes");
                showConnected.Click += (_, __) => ShowDirectlyConnected(node);
                menu.Items.Add(showConnected);

                var isolateDirect = new ToolStripMenuItem("Isolate to directly connected nodes");
                isolateDirect.Click += (_, __) => HideAllExceptConnectedTo(node);
                menu.Items.Add(isolateDirect);

                var isolateAll = new ToolStripMenuItem("Isolate to all connected nodes");
                isolateAll.Click += (_, __) => HideAllExceptConnectedToTransitive(node);
                menu.Items.Add(isolateAll);

                menu.Items.Add(new ToolStripSeparator());
            }

            var showAll = new ToolStripMenuItem("Show all");
            showAll.Click += (_, __) => { foreach (var n in Nodes) n.Visible = true; Invalidate(); };
            menu.Items.Add(showAll);

            if (node == null)
            {
                menu.Items.Add(new ToolStripSeparator());
                var relayout = new ToolStripMenuItem("Relayout");
                relayout.DropDownItems.Add(new ToolStripMenuItem("Grid", null, (_, __) => ArrangeByConnectivityGrid()));
                relayout.DropDownItems.Add(new ToolStripMenuItem("Force-directed", null, (_, __) => ArrangeForceDirected()));
                relayout.DropDownItems.Add(new ToolStripMenuItem("Hierarchical", null, (_, __) => ArrangeHierarchical()));
                menu.Items.Add(relayout);
            }

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
                    hit.Visible = !hit.Visible;
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
                    .Where(n => n.Visible && n.Title.Contains(m_SearchText, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(n => n.Title.Equals(m_SearchText, StringComparison.OrdinalIgnoreCase) ? 0
                                : n.Title.StartsWith(m_SearchText, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                    .ThenBy(n => n.Title.Length)
                    .FirstOrDefault();
                if (match != null)
                {
                    EnsureSizes();
                    float panX = ClientSize.Width / 2f - (match.Position.X + match.Size.Width / 2f) * m_Zoom;
                    float panY = ClientSize.Height / 2f - (match.Position.Y + match.Size.Height / 2f) * m_Zoom;
                    // Don't let the top of a tall node scroll off screen
                    panY = Math.Max(panY, 20f - match.Position.Y * m_Zoom);
                    m_Pan = new PointF(panX, panY);
                    Invalidate();
                }
            }
        }

        // NOTE: when adding shortcuts here, also add them to DrawSnackbar so they appear in the hint bar
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.H | Keys.Control:
                    {
                        var node = NodeUnderMouse();
                        if (node != null) { node.Visible = false; Invalidate(); }
                        return true;
                    }
                case Keys.I | Keys.Control:
                    {
                        var node = NodeUnderMouse();
                        if (node != null) { HideAllExceptConnectedToTransitive(node); }
                        return true;
                    }
                case Keys.F | Keys.Control:
                    {
                        ArrangeForceDirected();
                        return true;
                    }
                case Keys.C | Keys.Control:
                    {
                        using var bmp = RenderToBitmap();
                        Clipboard.SetImage(bmp);
                        return true;
                    }
                case Keys.S | Keys.Control:
                    {
                        using var dlg = new SaveFileDialog
                        {
                            Title = "Save diagram as PNG",
                            Filter = "PNG image|*.png",
                            FileName = "diagram.png",
                        };
                        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                        {
                            using var bmp = RenderToBitmap();
                            bmp.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        return true;
                    }
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            ApplySearchKey(e.KeyChar);
            e.Handled = true;
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
                ("   Ctrl+H", SnackbarKeyBrush), ("=hide", SnackbarTextBrush),
                ("   Ctrl+I", SnackbarKeyBrush), ("=isolate", SnackbarTextBrush),
                ("   Ctrl+F", SnackbarKeyBrush), ("=force layout", SnackbarTextBrush),
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
                if (from == to || !from.Visible || !to.Visible) continue;

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
            foreach (var node in Nodes.Where(x => x.Visible))
            {
                DrawNode(g, node);
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

