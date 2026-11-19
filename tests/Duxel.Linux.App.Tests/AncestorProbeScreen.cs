using Duxel.Core;

internal sealed class AncestorProbeScreen : UiScreen, IDisposable
{
    private readonly UiScrollView[] _views;
    internal ManualResetEventSlim FirstFrame { get; } = new();

    internal AncestorProbeScreen(bool rightToLeft = false)
    {
        _views = new[] { "single", "multiline", "markdown" }.Select(mode =>
        {
            var probe = new AncestorTextScrollTests.Probe(mode);
            var padded = Dux.Padding(probe, new(300, 300, 100, 100));
            var inner = new UiScrollView(padded, new(UiScrollAxis.Both, 280, 300) { RightToLeft = rightToLeft });
            return new UiScrollView(inner, new(UiScrollAxis.Both, 190, 180) { RightToLeft = rightToLeft });
        }).ToArray();
    }

    public override void Render(UiImmediateContext ui)
    {
        ui.EnableRootViewportContentLayout(contentPadding: 0);
        for (var index = 0; index < _views.Length; index++)
        {
            ui.SetCursorScreenPos(new(10 + index * 210, 10));
            _views[index].Render(ui);
        }
        FirstFrame.Set();
    }

    public void Dispose()
    {
        foreach (var view in _views)
            view.Dispose();
        FirstFrame.Dispose();
    }
}
