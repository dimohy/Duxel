using System.Numerics;
using Duxel.Core;

internal static class VirtualListRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiState state, bool vector, Action<string, Action<UiImmediateContext>>? captureHost = null, bool rightToLeft = false)
    {
        foreach (var count in new[] { 10, 1_000_000_000 })
        foreach (var horizontal in new[] { false, true })
        foreach (var position in new[] { 0, 1, 2 })
        foreach (var narrow in new[] { false, true })
        foreach (var columns in new[] { false, true })
        foreach (var variable in new[] { false, true })
        {
            if (rightToLeft && !horizontal)
                continue;
            if (captureHost is not null && (count != 1_000_000_000 || position != 2 || narrow || columns || !variable))
                continue;
            var contentExtent = (double)count * (variable ? 48 : 32);
            var maximum = contentExtent - (horizontal ? 100 : 96);
            var requested = position == 0 ? 0 : position == 1 ? 123.25 : maximum - 0.25;
            var source = new Source();
            var options = new UiVirtualListOptions(32, 100, 96)
            {
                Axis = horizontal ? UiScrollAxis.Horizontal : UiScrollAxis.Vertical,
                FlowDirection = rightToLeft ? UiFlowDirection.RightToLeft : UiFlowDirection.LeftToRight,
                ExtentIndex = variable ? new AlternatingExtents() : null
            };
            UiVirtualList<Source> CreateList(Source itemSource)
            {
                var result = Dux.VirtualList(itemSource, count, options);
                result.Offset = 1;
                result.Offset = requested;
                return result;
            }
            using var list = CreateList(source);
            var name = $"virtual-{count}-{position}-{narrow}-{columns}-{vector}" + (variable ? "-variable" : "")
                + (horizontal ? "-horizontal" : "") + (rightToLeft ? "-rtl" : "");
            var referenceId = "virtual-reference-" + state.FrameCount;
            void Scene(UiImmediateContext ui, UiVirtualList<Source>? actual)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (vector)
                    ui.BeginLayer("virtual-pixels", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                try
                {
                    ui.SetCursorScreenPos(new(20, 20));
                    if (columns)
                    {
                        ui.Columns(2);
                        ui.NextColumn();
                    }
                    var anchor = ui.GetCursorScreenPos();
                    ui.PushClipRect(new(anchor.X, anchor.Y, narrow ? 55 : 100, narrow ? 45 : 96), true);
                    try
                    {
                        if (actual is not null)
                        {
                            actual.Render(ui);
                            return;
                        }
                        ui.Dummy(new(100, 96));
                        var origin = ui.GetItemRectMin();
                        var context = new UiControlContext(ui);
                        var thickness = context.ScrollViewScrollbarThickness;
                        var viewport = horizontal ? new UiRect(origin.X, origin.Y, 100, 96 - thickness)
                            : new UiRect(origin.X, origin.Y, 100 - thickness, 96);
                        using (var clip = context.Clip(viewport))
                        {
                            var approximate = variable ? (long)Math.Floor(requested / 96) * 2 : (long)Math.Floor(requested / 32);
                            var first = Math.Max(0, approximate - 2);
                            for (var index = first; index < Math.Min(count, first + 8); index++)
                            {
                                var odd = index % 2 != 0;
                                var rowStart = variable ? index * 48d - (odd ? 12 : 0) : index * 32d;
                                var rowHeight = variable ? odd ? 60 : 36 : 32;
                                var local = (float)(rowStart - requested);
                                var x = rightToLeft ? origin.X + viewport.Width - local - rowHeight : origin.X + local;
                                var bounds = horizontal ? new UiRect(x, origin.Y, rowHeight, viewport.Height)
                                    : new UiRect(origin.X, origin.Y + local, viewport.Width, rowHeight);
                                using var rowClip = context.Clip(bounds);
                                Paint(ui, (int)index, bounds);
                            }
                        }
                        var track = horizontal ? new UiRect(origin.X, origin.Y + viewport.Height, 100, thickness)
                            : new UiRect(origin.X + viewport.Width, origin.Y, thickness, 96);
                        ui.RenderPreciseScrollbar(referenceId, track, requested, maximum, contentExtent, context.ClipRect, horizontal,
                            reverse : rightToLeft);
                    }
                    finally
                    {
                        ui.PopClipRect();
                        if (columns)
                            ui.Columns(1);
                    }
                }
                finally
                {
                    if (vector)
                        ui.EndLayer();
                }
            }
            if (captureHost is not null)
            {
                captureHost(name + "-clear", ui => ui.EnableRootViewportContentLayout(contentPadding : 0));
                using var geometry = CreateList(new() { DrawText = false });
                captureHost(name + "-geometry", ui => Scene(ui, geometry));
                captureHost(name + "-complete", ui => Scene(ui, list));
                continue;
            }
            var actualPixels = capture(name, ui => Scene(ui, list));
            var expectedPixels = capture(name + "-reference", ui => Scene(ui, null));
            check(actualPixels.AsSpan().SequenceEqual(expectedPixels), name + " matches explicitly rebased row coordinates and the common scrollbar");
            check(source.Created <= 6 && list.RealizedCount <= 6 && list.Offset == requested,
                name + " keeps realization bounded and retains the requested double offset");
            state.ReleaseScrollbar(referenceId);
        }
    }

    private sealed class AlternatingExtents : IUiVirtualListExtentIndex
    {
        public double GetBoundary(int index) => (double)(index / 2) * 96 + (index % 2 == 0 ? 0 : 36);

        public int FindIndexByKey(string key, int itemCount) =>
            int.TryParse(key, out var index) && index >= 0 && index < itemCount ? index : -1;
    }

    internal static void Paint(UiImmediateContext ui, int index, UiRect bounds, bool drawText = true)
    {
        var context = new UiControlContext(ui);
        context.Frame(bounds, new((byte)(30 + index % 7 * 25), 80, 145), new(220, 210, 190), 0, 1);
        context.Circle(new(bounds.X + 8, bounds.Y + 8), 4, new(240, 180, 40));
        if (drawText)
            context.Text("Row : " + index, new(bounds.X + 4, bounds.Y + 10), new(240, 240, 240));
    }

    private sealed class Source : IUiVirtualListSource
    {
        internal int Created;
        internal bool DrawText = true;
        public string GetKey(int index) => index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public UiWidget Create(int index)
        {
            Created++;
            return new Row(index, this);
        }
        public void Bind(UiWidget widget, int index) { }
    }

    private sealed class Row(int index, Source source) : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => throw new InvalidOperationException("Virtual rows must not be measured.");
        protected override void RenderControl(UiImmediateContext ui)
        {
            var constraints = new UiControlContext(ui).LayoutConstraints;
            var origin = ui.GetCursorScreenPos();
            var size = new UiVector2(constraints.MaxWidth, constraints.MaxHeight);
            ui.Dummy(size);
            Paint(ui, index, new(origin.X, origin.Y, size.X, size.Y), source.DrawText);
        }
    }
}
