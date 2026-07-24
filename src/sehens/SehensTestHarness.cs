namespace SehensWerte.Controls.Sehens
{
    // Shared setup for headless trace-view unit tests: builds a scope with painted geometry so
    // TraceGroupDisplay / Measure / DrawnSamples work without a real on-screen paint cycle.
    // Mirrors the geometry part of SehensPaintBox.ScreenshotToBitmap.
    internal static class SehensTestHarness
    {
        public const int Width = 800;
        public const int Height = 400;

        // Recompute Painted info + paint-box geometry after traces, groups, or zoom change.
        // Call after every mutation and before building any TraceGroupDisplay.
        public static void Layout(SehensControl scope, int width = Width, int height = Height)
        {
            foreach (TraceView view in scope.AllViews)
            {
                view.CalculateTrace();
            }
            scope.PaintBox.PaintedTraces = scope.GetPaintedTraces();
            scope.PaintBox.PaintBoxWidth = width;
            scope.PaintBox.PaintBoxRealHeight = height;
            scope.PaintBox.PaintBoxVirtualHeight = height;
            scope.PaintBox.PaintBoxVirtualOffset = 0;
        }

        public static TraceView View(SehensControl scope, string name)
        {
            return scope.ViewByName(name) ?? throw new InvalidOperationException($"view {name} missing");
        }

        // A ramp trace (sample value == sample number) with an affine horizontal axis.
        public static TraceView AffineTrace(SehensControl scope, string name, int count,
            double offset, double multiplier, string unit)
        {
            scope[name].Update(Ramp(count));
            scope[name].SetHorizontalAffine(offset, multiplier, unit);
            return View(scope, name);
        }

        public static double[] Ramp(int count)
        {
            double[] samples = new double[count];
            for (int loop = 0; loop < count; loop++)
            {
                samples[loop] = loop;
            }
            return samples;
        }

        // Zoom/pan the scope and fan the values out to every view (what the scrollbar path does).
        public static void ZoomPan(SehensControl scope, double zoom, double pan)
        {
            scope.SetZoomPan(zoom, pan);
            scope.ReprocessMathAfterZoom();
        }
    }
}
