using Duxel.Core;

namespace Duxel.Gallery;

internal interface IObserverNaturalPage
{
    UiWidget MeasureRoot { get; }
    void PrepareContent(UiImmediateContext ui) { }
    void RenderOverlays(UiImmediateContext ui) { }
}

internal interface IObserverPage : IDisposable
{
    string Title { get; }
    string Description { get; }
    void Render(UiImmediateContext ui);
}

internal sealed class WidgetPage(string title, string description, UiWidget content) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private readonly UiScrollView _scroll = new(content);
    public bool HasInlineSamples => _scroll.Child is IGallerySourcePage { HasInlineSamples: true };
    public IReadOnlyList<IGallerySample> Samples => (_scroll.Child as IGallerySourcePage)?.Samples ?? [];
    public string Title => title;
    public string Description => description;
    internal UiWidget Body => _scroll.Child;
    UiWidget IObserverNaturalPage.MeasureRoot => Body;
    public void Render(UiImmediateContext ui) => _scroll.Render(ui);
    public void Dispose() => _scroll.Dispose();
}

internal sealed class LegacyPage : IObserverPage, IGallerySourcePage
{
    private readonly SampleScreen _screen = new() { UseEmbeddedMenuBar = true };
    private readonly List<IGallerySample> _visible = [];
    private readonly Dictionary<ImmediateSampleSection, SourceBlock> _sources = [];
    public IReadOnlyList<IGallerySample> Samples => _visible;
    internal IReadOnlyCollection<SourceBlock> CreatedSources => _sources.Values;
    public LegacyPage() => _screen.ShowSource = RenderSource;
    public string Title => "기존 API";
    public string Description => "기존 종합 샘플의 실제 Hello / Immediate 탭을 같은 창에서 조작합니다.";
    public void Render(UiImmediateContext ui)
    {
        _visible.Clear();
        var available = ui.GetContentRegionAvail();
        if (ui.BeginChild("observer-legacy-page", new(available.X, MathF.Max(120, available.Y - 8)), true))
            _screen.Render(ui);
        ui.EndChild();
    }
    private void RenderSource(UiImmediateContext ui, ImmediateSampleSection section)
    {
        if (!_sources.TryGetValue(section, out var block))
            _sources.Add(section, block = new(section));
        block.SynchronizeSource();
        _visible.Add(block);
        ui.RenderNaturalHeight(block.Code);
    }
    internal sealed class SourceBlock(ImmediateSampleSection section) : IGallerySample, IDisposable
    {
        internal string Title => section.Title;
        internal UiCodeView Code { get; } = new(section.Source, new(MaximumVisibleLines : null, ShowToolbar : false));
        public string Source => Code.Code;
        public UiCodeViewMetrics Metrics => Code.Metrics;
        public void SynchronizeSource() => Code.Code = section.Source;
        public void Dispose() => Code.Dispose();
    }
    public void Dispose()
    {
        foreach (var block in _sources.Values) block.Dispose();
    }
}
