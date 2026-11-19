using System.Numerics;
using Duxel.Core;

internal static class VirtualGridRenderingTests
{
    private const float Width = 128, Height = 96, CellSize = 32;

    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiState state, float scrollbar, bool vector, Action<string, Action<UiImmediateContext>>? capturePart = null)
    {
        Scenario[] scenarios =
        [
            new("fit", 3, 3, false, false),
            new("vertical", 8, 2, false, true),
            new("horizontal", 2, 8, true, false),
            new("vertical-coupled", 8, 4, true, true),
            new("horizontal-coupled", 3, 8, true, true),
            new("fixed-billion", 1_000_000_000, 1_000_000_000, true, true),
            new("indexed-billion", 1_000_000_000, 1_000_000_000, true, true, Indexed : true)
        ];
        foreach (var scenario in scenarios)
        foreach (var position in new[] { "start", "middle", "tail" })
        foreach (var rtl in new[] { false, true })
        foreach (var narrow in new[] { false, true })
        {
            var cellWidth = scenario.Indexed ? new Extents(30, 58) : null;
            var cellHeight = scenario.Indexed ? new Extents(26, 50) : null;
            var totalWidth = cellWidth?.GetBoundary(scenario.Columns) ?? scenario.Columns * (double)CellSize;
            var totalHeight = cellHeight?.GetBoundary(scenario.Rows) ?? scenario.Rows * (double)CellSize;
            var viewWidth = Width - (scenario.VerticalBar ? scrollbar : 0);
            var viewHeight = Height - (scenario.HorizontalBar ? scrollbar : 0);
            var maxX = Math.Max(0, totalWidth - viewWidth);
            var maxY = Math.Max(0, totalHeight - viewHeight);
            var offset = position switch
            {
                "middle" => new UiVirtualGridOffset(Math.Min(maxX, 73.125), Math.Min(maxY, 87.625)),
                "tail" => new UiVirtualGridOffset(Math.Max(0, maxX - .25), Math.Max(0, maxY - .75)),
                _ => default
            };
            var source = new Source();
            using var grid = Dux.VirtualGrid(source, scenario.Rows, scenario.Columns, new UiVirtualGridOptions(CellSize, CellSize, Width, Height)
            {
                ColumnExtents = cellWidth,
                RowExtents = cellHeight,
                FlowDirection = rtl ? UiFlowDirection.RightToLeft : UiFlowDirection.LeftToRight
            });
            // Keep the newly used scrollbar visible in both captures, including at offset zero.
            grid.Offset = new(1, 1);
            grid.Offset = offset;
            var name = $"grid-{scenario.Name}-{position}-rtl{rtl}-narrow{narrow}-vector{vector}";
            var referenceId = name + "-" + state.FrameCount;
            void Scene(UiImmediateContext ui, bool actual, PaintPart part = PaintPart.All)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (vector)
                    ui.BeginLayer("grid-reference", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                try
                {
                    ui.SetCursorScreenPos(new(20, 20));
                    var root = new UiRect(20, 20, Width, Height);
                    var context = new UiControlContext(ui);
                    var mask = narrow ? new UiRect(45, 35, 60, 45) : root;
                    using var outer = context.Clip(mask);
                    if (actual)
                    {
                        grid.Render(ui);
                        return;
                    }

                    var gutter = Width - viewWidth;
                    var left = root.X + (rtl ? gutter : 0);
                    var view = new UiRect(left, root.Y, viewWidth, viewHeight);
                    if (part != PaintPart.Scrollbar)
                    {
                        using var viewportClip = context.Clip(view);
                        var (firstRow, endRow) = Nearby(offset.Y, scenario.Rows, cellHeight);
                        var (firstColumn, endColumn) = Nearby(offset.X, scenario.Columns, cellWidth);
                        for (var row = firstRow; row < endRow; row++)
                        for (var column = firstColumn; column < endColumn; column++)
                        {
                            double x = cellWidth?.GetBoundary(column) ?? column * (double)CellSize;
                            double y = cellHeight?.GetBoundary(row) ?? row * (double)CellSize;
                            var w = cellWidth?.SizeAt(column) ?? CellSize;
                            var h = cellHeight?.SizeAt(row) ?? CellSize;
                            var cellLeft = rtl ? (float)(left + viewWidth - (x - offset.X) - w)
                                : left + (float)(x - offset.X);
                            var bounds = new UiRect(cellLeft, root.Y + (float)(y - offset.Y), w, h);
                            using var cell = context.Clip(bounds);
                            Paint(ui, row, column, bounds, part);
                        }
                    }
                    if (part is PaintPart.Frame or PaintPart.Circle)
                        return;
                    if (scenario.HorizontalBar)
                    {
                        var track = new UiRect(left, root.Y + viewHeight, viewWidth, scrollbar);
                        ui.RenderPreciseScrollbar(referenceId + "x", track, offset.X, maxX, totalWidth, context.ClipRect,
                            horizontal : true, reverse : rtl);
                    }
                    if (scenario.VerticalBar)
                    {
                        var track = new UiRect(rtl ? root.X : root.X + viewWidth, root.Y, scrollbar, viewHeight);
                        ui.RenderPreciseScrollbar(referenceId + "y", track, offset.Y, maxY, totalHeight, context.ClipRect, horizontal : false);
                    }
                }
                finally
                {
                    if (vector)
                        ui.EndLayer();
                }
            }

            var pixels = capture(name, ui => Scene(ui, actual : true));
            state.RequestScrollbarReveal(referenceId + "x");
            state.RequestScrollbarReveal(referenceId + "y");
            var reference = capture(name + "-reference", ui => Scene(ui, actual : false));
            check(pixels.AsSpan().SequenceEqual(reference), name + " matches independently positioned cells and scrollbars");
            check(grid.Offset == offset && grid.RealizedCount == source.Created && source.Created is > 0 and <= 48,
                name + " retains fractional offsets and bounds cell creation");
            if (capturePart is not null && vector && position == "tail"
                && (scenario.Name == "fixed-billion" && !rtl && !narrow
                    || scenario.Name == "indexed-billion" && rtl && narrow))
            {
                foreach (var part in new[] { PaintPart.Frame, PaintPart.Circle, PaintPart.Scrollbar })
                    capturePart(name + "-part-" + part, ui => Scene(ui, actual : false, part));
            }
            state.ReleaseScrollbar(referenceId + "x");
            state.ReleaseScrollbar(referenceId + "y");
        }
    }

    private static (int First, int End) Nearby(double offset, int count, Extents? extents)
    {
        var first = extents is null ? (int)(offset / CellSize) : (int)(offset / extents.Pair) * 2;
        // At most six cells fit along either axis in this fixture. Include adjacent candidates to cover
        // the leading fractional cell, offscreen overscan and the alternating extent boundary.
        return (Math.Max(0, first - 2), Math.Min(count, first + 9));
    }

    private enum PaintPart { All, Frame, Circle, Scrollbar }

    private static void Paint(UiImmediateContext ui, int row, int column, UiRect bounds, PaintPart part = PaintPart.All)
    {
        var context = new UiControlContext(ui);
        if (part is PaintPart.All or PaintPart.Frame)
        {
            var fill = new UiColor((byte)(40 + row % 7 * 17), (byte)(50 + column % 7 * 21), 160);
            context.Frame(bounds, fill, new(220, 210, 190), 0, 1);
        }
        if (part is PaintPart.All or PaintPart.Circle)
            context.Circle(new(bounds.X + 8, bounds.Y + 8), 4, new(240, 180, 40));
    }

    private sealed class Extents(float even, float odd) : IUiVirtualListExtentIndex
    {
        internal double Pair => even + odd;
        internal float SizeAt(int index) => index % 2 == 0 ? even : odd;
        public double GetBoundary(int index) => (index / 2) * Pair + (index % 2 == 0 ? 0 : even);
        public int FindIndexByKey(string key, int itemCount) => -1;
    }

    private sealed class Source : IUiVirtualGridSource
    {
        internal int Created;
        public string GetKey(int row, int column) => FormattableString.Invariant($"{row}:{column}");
        public UiWidget Create(int row, int column)
        {
            Created++;
            return new Cell(row, column);
        }
        public void Bind(UiWidget widget, int row, int column) { }
    }

    private sealed class Cell(int row, int column) : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => throw new InvalidOperationException("Virtual grid cells use indexed sizes.");
        protected override void RenderControl(UiImmediateContext ui)
        {
            var constraints = new UiControlContext(ui).LayoutConstraints;
            var origin = ui.GetCursorScreenPos();
            Paint(ui, row, column, new(origin.X, origin.Y, constraints.MaxWidth, constraints.MaxHeight));
        }
    }

    private readonly record struct Scenario(string Name, int Rows, int Columns, bool HorizontalBar, bool VerticalBar, bool Indexed = false);
}
