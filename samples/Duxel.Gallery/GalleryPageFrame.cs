using Duxel.Core;

namespace Duxel.Gallery;

/// <summary>Owns only presentation slots; the persistent example page is borrowed.</summary>
internal sealed class GalleryPageFrame : IDisposable
{
    private readonly IObserverPage _page;
    private readonly UiWidget _root;
    private readonly ExampleHost _body;
    private readonly string? _source;
    internal string? Source => _samples is null ? _source : string.Join("\n\n", _samples.Select(sample => sample.Source));
    private readonly UiCodeView? _codeView;
    private readonly IReadOnlyList<IGallerySample>? _samples;
    internal (int Measure, int Paint, int Parse) CodeDiagnostics
    {
        get
        {
            if (_samples is not null)
            {
                var measure = 0; var paint = 0; var parse = 0;
                for (var i = 0; i < _samples.Count; i++)
                {
                    var sampleMetrics = _samples[i].Metrics;
                    measure += sampleMetrics.MeasurementBuilds; paint += sampleMetrics.PaintBuilds; parse += sampleMetrics.SyntaxParses;
                }
                return (measure, paint, parse);
            }
            if (_codeView is null) return default;
            var metrics = _codeView.Metrics;
            return (metrics.MeasurementBuilds, metrics.PaintBuilds, metrics.SyntaxParses);
        }
    }
    internal GalleryPageFrame(IObserverPage page, string? navigationLabel = null)
    {
        _page = page;
        _body = new ExampleHost(this);
        var title = Fluent.Title2(navigationLabel ?? page.Title);
        var header = string.IsNullOrWhiteSpace(page.Description)
            ? (UiWidget)title
            : Dux.Column(new(Spacing : 6, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Stretch),
                title, new UiInlineText([new(page.Description)]));
        UiWidget body = _body;
        if (page is IGallerySourcePage { HasInlineSamples: true } inline)
            _samples = inline.Samples;
        else if (HasSource(page.Title))
        {
            _source = GalleryPageSource.For(page);
            _codeView = new UiCodeView(_source, new(ShowToolbar : false));
            body = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min,
                CrossAxisAlignment : UiCrossAxisAlignment.Stretch), _body, _codeView);
        }
        var scroll = Dux.ScrollView(Dux.Column(new(Spacing : 28, MainAxisSize : UiMainAxisSize.Min,
            CrossAxisAlignment : UiCrossAxisAlignment.Stretch), header, body));
        scroll.AccessibleName = "갤러리 페이지";
        _root = scroll;
    }
    internal static bool HasSource(string title) => title is not ("개요" or "관찰 범위") && !Fluent.MissingComponentNames.Contains(title);
    internal void Render(UiImmediateContext ui)
    {
        if (_page is IObserverNaturalPage prepared) prepared.PrepareContent(ui);
        if (_samples is not null && _page is IGallerySourcePage { SynchronizesOwnSources: false })
            foreach (var sample in _samples) sample.SynchronizeSource();
        var available = ui.GetContentRegionAvail();
        _body.ViewportHeight = available.Y;
        _root.Render(ui);
        if (_page is IObserverNaturalPage natural) natural.RenderOverlays(ui);
    }
    private void RenderExample(UiImmediateContext ui)
    {
        if (_page is not IObserverNaturalPage natural) { _page.Render(ui); return; }
        var content = natural.MeasureRoot;
        while (content is UiScrollView scroll) content = scroll.Child;
        content.Render(ui);
    }
    private UiWidget? NaturalContent
    {
        get
        {
            var content = (_page as IObserverNaturalPage)?.MeasureRoot;
            while (content is UiScrollView scroll) content = scroll.Child;
            return content;
        }
    }
    private sealed class ExampleHost : UiControlWidget
    {
        private readonly GalleryPageFrame _frame;
        private readonly UiViewHost _host;
        internal float ViewportHeight { get; set; }
        internal ExampleHost(GalleryPageFrame frame)
        {
            _frame = frame;
            _host = new(new UiView(frame.RenderExample));
        }
        protected override UiVector2 MeasureContent(UiControlContext context) => _frame.NaturalContent is { } natural
            ? context.MeasureConstrained(natural, new(0, context.LayoutConstraints.MaxWidth, 0, float.PositiveInfinity))
            : new(context.LayoutConstraints.MaxWidth, ViewportHeight);
        protected override void RenderControl(UiImmediateContext ui) => _host.Render(ui);
        protected override void OnDispose() => _host.Dispose();
    }
    public void Dispose() => _root.Dispose();
}

internal static class GalleryPageSource
{
    internal static string OmitCommonImport(string source)
    {
        const string import = "using Duxel.Core;";
        var offset = 0;
        while (offset < source.Length)
        {
            var end = source.IndexOf('\n', offset);
            if (end < 0) end = source.Length;
            var line = source.AsSpan(offset, end - offset).Trim();
            if (line.SequenceEqual(import))
                return source.Remove(offset, Math.Min(end + 1, source.Length) - offset).TrimStart('\r', '\n');
            if (!line.IsEmpty && !line.StartsWith("using ")) break;
            offset = end + 1;
        }
        return source;
    }
    private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);
    internal static string For(IObserverPage page)
    {
        if (page is ComponentExamplePage example) return example.Source;
        if (Fluent.MissingComponentNames.Contains(page.Title)) return "// 아직 미구현됨: 실행 가능한 예제 소스가 없습니다.";
        var file = page.Title switch
        {
            "개요" => "ObserverScreen",
            "Fluent" => "ControlsPage",
            "평가" or "타이포그래피" => "FluentPages",
            "입력·IME" => "EditingPage",
            "Tooltip" => "TooltipPage",
            "컨테이너" => "LayoutPage",
            "배치 통합" => "LayoutIntegrationPage",
            "접기·펼치기" => "AccordionPage",
            "스켈레톤" => "SkeletonPage",
            "관찰 범위" => "CatalogPage",
            "측정 재사용" => "MeasurementPage",
            "가상 목록" => "VirtualPage",
            "가상 그리드" => "VirtualGridPage",
            "렌더링" => "RenderingPage",
            "SVG" => "SvgPage",
            "위젯 배치" => "WidgetLayoutPage",
            "아바타" => "AvatarPage",
            "아바타 그룹" => "AvatarGroupPage",
            "인물 정보" => "PersonaPage",
            "검색 입력" => "SearchBoxPage",
            "도구 모음" => "ToolbarPage",
            "팝업 입력" => "PopupInputPage",
            "Popover" => "PopoverPage",
            "대화 상자" => "DialogPage",
            "메시지 표시" => "MessageBarPage",
            "추가 정보" => "InfoLabelPage",
            "숫자 입력" => "SpinButtonPage",
            "목록" => "ListPage",
            "선택 목록" => "DropdownPage",
            "카드" => "CardPage",
            "경로" => "BreadcrumbPage",
            "탐색 메뉴" => "NavigationPage",
            "보조 패널" => "DrawerPage",
            "갱신 관찰" => "RepaintExperiment",
            "Markdown" => "MarkdownPage",
            _ => throw new InvalidOperationException("Gallery source is not registered : " + page.Title)
        };
        return Read(file);
    }
    internal static string Declaration(string file)
    {
        var source = Read(file).TrimStart();
        const string header = "namespace Duxel.Gallery;";
        if (!source.StartsWith(header, StringComparison.Ordinal))
            throw new InvalidOperationException("Gallery declaration files must contain the common Core import, Gallery namespace and complete declarations : " + file);
        return source[header.Length..].Trim();
    }
    private static string Read(string file)
    {
        if (Cache.TryGetValue(file, out var source)) return source;
        using var stream = typeof(GalleryPageSource).Assembly.GetManifestResourceStream("GallerySource." + file + ".cs")
            ?? throw new InvalidOperationException("Gallery source resource is missing : " + file);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        source = OmitCommonImport(reader.ReadToEnd());
        Cache.Add(file, source);
        return source;
    }
}
