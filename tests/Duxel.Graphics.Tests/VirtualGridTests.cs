using Duxel.Core;

internal static class VirtualGridTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var accessibility = new UiAccessibilityTree();
        context.SetAccessibilityTree(accessibility);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw, UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(10, 10));
                draw(ui);
            }));
            using var data = context.GetDrawData();
        }

        const int count = 1_000_000_000;
        var source = new Source();
        using var grid = Dux.VirtualGrid(source, count, count, new(RowHeight : 40, ColumnWidth : 32, Width : 100, Height : 100));
        Frame(ui =>
        {
            check(grid.MeasureConstrained(ui, new()) == new UiVector2(100, 100) && source.KeyReads == 0,
                "virtual grid measurement is finite and never reads model cells");
            grid.Render(ui);
        });
        check(grid.RealizedCount == 16 && source.Cells[(0, 0)].Bounds == new UiRect(10, 10, 32, 40),
            "the billion-by-billion grid realizes only the intersecting rectangle and overscan");
        var retained = source.Cells[(1, 1)];
        grid.Offset = new(32, 40);
        Frame(grid.Render);
        check(ReferenceEquals(retained, source.Cells[(1, 1)]) && grid.RealizedCount <= 25,
            "overlapping two-axis ranges retain keyed widget identity");
        var gutter = context.GetStyleData().ScrollbarSize;
        var width = 100 - gutter;
        var maxX = 32d * count - width;
        var maxY = 40d * count - width;
        grid.Offset = new(maxX - .25, maxY - .25);
        Frame(grid.Render);
        check(source.Cells[(count - 1, count - 1)].Bounds == new UiRect(10 + width - 32 + .25f, 10 + width - 40 + .25f, 32, 40)
            && grid.RealizedCount <= 25 && retained.Disposed,
            "the final cell rebases on both axes without losing fractional coordinates");

        accessibility.TryGet(accessibility.Root.First!, out var pane);
        check(pane.Node.Scroll is { HorizontallyScrollable : true, VerticallyScrollable : true } range
            && range.HorizontalOffset == maxX - .25 && range.VerticalOffset == maxY - .25,
            "accessibility publishes the exact two-axis scroll range");
        var request = accessibility.RequestScroll(pane.Node.Id, pane.RuntimeId,
            UiAccessibilityScrollCommand.ByPercent(0, 100), wait : false);
        Frame(grid.Render);
        check(request == UiAccessibilityResult.Success && grid.Offset == new UiVirtualGridOffset(0, maxY),
            "one accessibility request can move both logical axes");

        using var wake = new ManualResetEventSlim();
        accessibility.SetRequestFrameCallback(wake.Set);
        T RunClient<T>(Func<T> call)
        {
            wake.Reset();
            var pending = Task.Run(call);
            if (!wake.Wait(TimeSpan.FromSeconds(1)))
                throw new TimeoutException("The native grid lookup did not wake the render writer.");
            Frame(grid.Render);
            return pending.GetAwaiter().GetResult();
        }
        var created = source.Cells.Count;
        var lookup = RunClient(() =>
        {
            var result = accessibility.GetGridItem(pane.Node.Id, pane.RuntimeId, 42, 617, out var item);
            return (result, item);
        });
        check(lookup.result == UiAccessibilityResult.Success && lookup.item is not null
            && accessibility.TryGet(lookup.item.Value.Id, out var cell) && cell.Node.Offscreen
            && cell.Node.VirtualItem is { IsPlaceholder : true } && cell.Node.GridItem is { Row : 42, Column : 617 }
            && source.Cells.Count == created && grid.Offset == new UiVirtualGridOffset(0, maxY),
            "offscreen native coordinate lookup exports one placeholder without scrolling or creating a cell");
        var realize = RunClient(() => accessibility.RealizeVirtualItem(pane.Node.Id, pane.RuntimeId, lookup.item!.Value));
        check(realize == UiAccessibilityResult.Success && grid.Offset == new UiVirtualGridOffset(617 * 32d, 42 * 40d)
            && accessibility.TryGet(lookup.item!.Value.Id, out var realized) && realized.RuntimeId == lookup.item.Value.RuntimeId
            && !realized.Node.Offscreen && realized.Node.VirtualItem is { IsPlaceholder : false }
            && source.Cells.ContainsKey((42, 617)),
            "native Realize moves both axes and publishes the actual cell on the original provider");
        accessibility.SetRequestFrameCallback(null);

        grid.Offset = new(maxX - 1000, maxY - 1000);
        var step = 0f;
        Frame(ui =>
        {
            step = ui.GetFrameHeight() * 3;
            grid.Render(ui);
        }, idle with { MousePosition = new(40, 40), MouseWheelHorizontal = -1, MouseWheel = 1 });
        check(grid.Offset == new UiVirtualGridOffset(maxX - 1000 + step, maxY - 1000 - step),
            "one input frame scrolls the two axes independently");

        grid.SetDimensions(count, 0);
        Frame(grid.Render);
        check(grid.Offset == default && grid.RealizedCount == 0 && source.Cells.Count == 0,
            "emptying one axis releases all cells and both scroll ranges");

        var rtlSource = new Source();
        using var rtl = Dux.VirtualGrid(rtlSource, 100, 100,
            new(RowHeight : 40, ColumnWidth : 20, Width : 100, Height : 100)
            { FlowDirection = UiFlowDirection.RightToLeft, ColumnExtents = new Extents() });
        Frame(rtl.Render);
        check(rtlSource.Cells[(0, 0)].Bounds == new UiRect(88, 10, 22, 40)
            && rtlSource.Cells[(0, 1)].Bounds.X == 52,
            "RTL grid places indexed columns from the right");
        var limitedSource = new Source();
        using var limited = Dux.VirtualGrid(limitedSource, count, count,
            new(RowHeight : 1, ColumnWidth : 1, Width : 100, Height : 100, MaxRealizedCells : 12));
        var refused = false;
        try { Frame(limited.Render); }
        catch (InvalidOperationException exception) when (exception.Message.Contains("MaxRealizedCells", StringComparison.Ordinal))
        {
            refused = true;
        }
        check(refused && limitedSource.KeyReads == 0, "grid budget is enforced before invoking the source");
    }

    private sealed class Extents : IUiVirtualListExtentIndex
    {
        public double GetBoundary(int index) => (index / 2) * 58d + (index % 2 == 0 ? 0 : 22);
        public int FindIndexByKey(string key, int itemCount) => -1;
    }

    private sealed class Source : IUiVirtualGridSource
    {
        internal readonly Dictionary<(int Row, int Column), Cell> Cells = [];
        internal int KeyReads;
        public string GetKey(int row, int column)
        {
            KeyReads++;
            return FormattableString.Invariant($"{row}:{column}");
        }
        public UiWidget Create(int row, int column) => Cells[(row, column)] = new Cell(this, row, column);
        public void Bind(UiWidget widget, int row, int column) { }
    }

    private sealed class Cell(Source source, int row, int column) : UiControlWidget
    {
        internal UiRect Bounds;
        internal bool Disposed;
        protected override UiVector2 MeasureContent(UiControlContext context) => throw new InvalidOperationException("Cells use indexed dimensions.");
        protected override void RenderControl(UiImmediateContext ui)
        {
            var size = new UiControlContext(ui).LayoutConstraints;
            var current = ui.GetCursorScreenPos();
            Bounds = new(current.X, current.Y, size.MaxWidth, size.MaxHeight);
            ui.Dummy(new(Bounds.Width, Bounds.Height));
        }
        protected override void OnDispose()
        {
            Disposed = true;
            source.Cells.Remove((row, column));
        }
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
