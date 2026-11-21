using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class NavigationBarPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new();
    public string Title => "NavigationBar";
    public string Description => "현재 화면의 제목을 표시하고, 이전 화면으로 돌아가거나 화면 안의 작업을 실행합니다.";
    public IReadOnlyList<IGallerySample> Samples => State.Samples;
    internal UiFluentSegmentedControl PeriodSelector => State.PeriodSelector;
    UiWidget IObserverNaturalPage.MeasureRoot => State.Root;
    public void Render(UiImmediateContext ui) => State.Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();

    private sealed class Content
    {
        internal IGallerySample[] Samples { get; }
        internal UiFluentSegmentedControl PeriodSelector { get; }
        internal UiWidget Root { get; }

        internal Content()
        {
            var actions = DocumentSample();
            var filters = PeriodSample(out var periodSelector);
            PeriodSelector = periodSelector;
            Samples = [actions, filters];
            Root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Title3(GalleryText.Get("제목과 화면 동작")),
                Fluent.Button(GalleryText.Get("너비 전환"), () => actions.Preview.Options = actions.Preview.Options with
                {
                    Width = actions.Preview.Options.Width == 440 ? 210 : 440
                }),
                actions,
                Fluent.Title3(GalleryText.Get("브랜드 배경과 기간 선택")),
                Fluent.Button(GalleryText.Get("배치 방향 전환"), () => filters.Preview.Options = filters.Preview.Options with
                {
                    RightToLeft = !filters.Preview.Options.RightToLeft
                }),
                filters);
        }
    }

    private static GallerySample<UiFluentNavigationBar> DocumentSample()
    {
        var saves = new UiState<int>(0);
        var name = GalleryText.Get("문서 작업");
        var back = GalleryText.Get("돌아가기");
        var documentList = GalleryText.Get("문서 목록");
        var save = GalleryText.Get("저장");
        var saved = GalleryText.Get("저장 {0}회");
        UiFluentNavigationBar bar = null!;
        bar = Fluent.NavigationBar(name, GalleryText.Get("회의록"),
            leading : Fluent.Button(back, () => bar.Title = documentList),
            trailing : Fluent.Button(save, () =>
            {
                saves.Value++;
                bar.Title = string.Format(saved, saves.Value);
            }),
            options : new(Width : 440));
        var sample = GallerySample.Create(() => bar);
        return sample.BindSource(() => (bar.Title, saves.Value, bar.Options), state =>
            $$"""
            var saves = new UiState<int>({{state.Value}});
            UiFluentNavigationBar bar = null!;
            using var preview = bar = Fluent.NavigationBar({{GallerySample.String(name)}}, {{GallerySample.String(state.Title)}},
                leading : Fluent.Button({{GallerySample.String(back)}}, () => bar.Title = {{GallerySample.String(documentList)}}),
                trailing : Fluent.Button({{GallerySample.String(save)}}, () =>
                {
                    saves.Value++;
                    bar.Title = string.Format({{GallerySample.String(saved)}}, saves.Value);
                }),
                options : new(Width : {{GallerySample.Number(state.Options.Width!.Value)}}));
            preview.Render(ui);
            """);
    }

    private static GallerySample<UiFluentNavigationBar> PeriodSample(out UiFluentSegmentedControl selection)
    {
        var period = new UiState<string?>("day");
        var periodLabel = GalleryText.Get("기간");
        var barLabel = GalleryText.Get("기간 탐색");
        var title = GalleryText.Get("일정");
        var today = GalleryText.Get("오늘");
        UiSegmentOption[] periods = [new("day", GalleryText.Get("일간")), new("week", GalleryText.Get("주간")), new("month", GalleryText.Get("월간"))];
        var sourceOptions = string.Join(", ", periods.Select(option =>
            $"new({GallerySample.String(option.Value)}, {GallerySample.String(option.Label)})"));
        UiFluentNavigationBar bar = null!;
        selection = Fluent.SegmentedControl(periodLabel, period.Binding, periods,
            new(Width : 0, IsFixedWidth : false, Style : UiSegmentedControlStyle.BrandOverNavBarPill));
        bar = Fluent.NavigationBar(barLabel, title,
            leading : Fluent.Button(today, () => period.Value = "day"),
            secondary : selection,
            options : new(Width : 440, Appearance : UiNavigationBarAppearance.Brand));
        var sample = GallerySample.Create(() => bar);
        return sample.BindSource(() => (period.Value, bar.Options), state =>
            $$"""
            var period = new UiState<string?>({{GallerySample.String(state.Value)}});
            var selection = Fluent.SegmentedControl({{GallerySample.String(periodLabel)}}, period.Binding,
                new UiSegmentOption[] { {{sourceOptions}} },
                new(Width : 0, IsFixedWidth : false, Style : UiSegmentedControlStyle.BrandOverNavBarPill));
            using var bar = Fluent.NavigationBar({{GallerySample.String(barLabel)}}, {{GallerySample.String(title)}},
                leading : Fluent.Button({{GallerySample.String(today)}}, () => period.Value = "day"),
                secondary : selection,
                options : new(Width : 440, RightToLeft : {{GallerySample.Boolean(state.Options.RightToLeft)}},
                    Appearance : UiNavigationBarAppearance.Brand));
            bar.Render(ui);
            """);
    }
}
