using Duxel.Core;

internal sealed class VirtualTextProbeScreen : UiScreen, IDisposable
{
    private readonly UiVirtualList<VirtualTextScrollTests.Source>[] _lists;
    internal ManualResetEventSlim FirstFrame { get; } = new();

    internal VirtualTextProbeScreen(bool variableExtents = false)
    {
        _lists = new[] { "single", "multiline", "markdown" }.Select(mode =>
        {
            var source = new VirtualTextScrollTests.Source(mode, variableExtents);
            return new UiVirtualList<VirtualTextScrollTests.Source>(source, 1_000_000_000,
                new(VirtualTextScrollTests.Extent, 190, 180) { ExtentIndex = variableExtents ? source : null })
            {
                Offset = variableExtents ? source.GetBoundary(VirtualTextScrollTests.BaseIndex) + 17.375 : VirtualTextScrollTests.InitialOffset
            };
        }).ToArray();
    }
    public override void Render(UiImmediateContext ui)
    {
        ui.EnableRootViewportContentLayout(contentPadding: 0);
        for (var index = 0; index < _lists.Length; index++)
        {
            ui.SetCursorScreenPos(new(10 + index * 210, 10));
            _lists[index].Render(ui);
        }
        FirstFrame.Set();
    }
    public void Dispose()
    {
        foreach (var list in _lists)
            list.Dispose();
        FirstFrame.Dispose();
    }
}
