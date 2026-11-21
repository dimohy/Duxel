using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class FluentProviderPage(UiTypographyFonts? fonts = null) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "FluentProvider";
    public string Description => "Set the theme, font, and layout direction per region and see how child widgets inherit them.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new(fonts)).Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new(fonts)).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new(fonts)).Root.Render(ui);
    public void Dispose() => _content?.Dispose();

    private sealed class Content : IDisposable
    {
        private readonly UiState<string> _text = new("영역 사이에서 값을 공유합니다.");
        private readonly List<IGallerySample> _samples = [];
        internal IReadOnlyList<IGallerySample> Samples => _samples;
        internal UiWidget Root { get; }
        internal Content(UiTypographyFonts? fonts)
        {
            var light = Example("첫 번째 영역", UiFluentProviderTheme.Light);
            var dark = Example("두 번째 영역", UiFluentProviderTheme.Dark);
            var nested = GallerySample.Create(() => Fluent.FluentProvider(
                Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Title3("어두운 부모 영역"),
                    Fluent.FluentProvider(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                        Fluent.Body1("밝은 자식 영역"), Fluent.Input("중첩 영역 입력", _text.Binding, 1024, 280)),
                        UiFluentProviderTheme.Light, new(Padding : new(16))),
                    Fluent.Body1("자식 영역을 벗어나면 부모의 테마로 돌아옵니다.")),
                UiFluentProviderTheme.Dark, new(Padding : new(16), Width : 440)));
            var factory = nested.Source;
            nested.BindSource(() => _text.Value, text => TextSetup(text) + "\n\n" + factory);
            _samples.Add(nested);
            var fontExample = fonts is null ? Dux.Column(new UiFlexOptions(MainAxisSize : UiMainAxisSize.Min)) : FontExample(fonts);
            Root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1("테마 전환은 현재 영역에만 적용됩니다. 상속을 선택하면 바깥 테마를 따릅니다. 예제의 입력값은 서로 연결되어 있습니다."),
                light, dark, Fluent.Subtitle1("중첩 테마"), nested, fontExample, DirectionExample(),
                Fluent.Caption1("운영체제에서 대비 테마를 쓰면 시스템 색상이 우선합니다."));
        }
        private UiWidget DirectionExample()
        {
            var view = new UiState<string?>("week");
            var query = new UiState<string>(string.Empty);
            var search = new UiState<string>(string.Empty);
            var chosen = new UiState<string?>(null);
            var frequency = new UiState<string?>("daily");
            var notify = new UiState<bool>(true);
            var sound = new UiState<bool>(false);
            var volume = new UiState<float>(30);
            var expanded = new UiState<string[]>(["workspace"]);
            var placement = new UiTooltipOptions { Positioning = new() { Position = UiPopupPosition.BelowStart } };
            var sample = GallerySample.Create(() => Fluent.FluentProvider(
                Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.NavigationBar("일정 탐색", "일정",
                        leading : Fluent.Tooltip(Fluent.Button("돌아가기"), "이전 화면으로 돌아갑니다.", placement),
                        trailing : Fluent.Button("오늘", () => view.Value = "week"),
                        secondary : Fluent.SegmentedControl("보기", view.Binding,
                            new UiSegmentOption[] { new("week", "주간"), new("month", "월간") })),
                    Fluent.Toolbar("일정 도구",
                        [Fluent.Button("주간", () => view.Value = "week"), Fluent.Button("월간", () => view.Value = "month")]),
                    Fluent.SearchBox("일정 검색", search.Binding,
                        options : new(Placeholder : "일정 이름을 입력하세요", ClearLabel : "검색어 지우기")),
                    new UiRadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<UiRadioHorizontalLayout>>(
                        "알림 빈도", frequency.Binding, new UiRadioOption[]
                        { new("daily", "매일"), new("weekly", "매주"), new("monthly", "매월") }),
                    Dux.Wrap(new(Spacing : 8), Fluent.Checkbox("미리 알림", notify.Binding), Fluent.Switch("소리", sound.Binding)),
                    Fluent.Body1(() => $"알림 음량 : {volume.Value:0}%"),
                    Fluent.Slider("알림 음량", volume.Binding, 0, 100, 5),
                    Fluent.Combobox("일정 장소", query.Binding, chosen.Binding,
                        [new("seoul", "서울"), new("busan", "부산")],
                        new(Field : new(Placeholder : "도시 이름을 입력하세요", ExpandLabel : "장소 목록 열기"))),
                    Fluent.Tree("일정 파일", expanded.Binding,
                        [new("workspace", null, "작업 공간"), new("plan", "workspace", "계획")])),
                options : new(Width : 440), paintBackground : false));
            var provider = sample.Preview;
            provider.Direction = UiFlowDirection.RightToLeft;
            var factory = sample.Source.Replace("UiWidget example = ", "using var example = ", StringComparison.Ordinal);
            sample.BindSource(() => (View: view.Value, Query: query.Value, Search: search.Value, Chosen: chosen.Value,
                Expanded: expanded.Value, Frequency: frequency.Value, Notify: notify.Value, Sound: sound.Value, Volume: volume.Value, provider.Direction), state =>
                $"var view = new UiState<string?>({GallerySample.String(state.View)});\n"
                + $"var query = new UiState<string>({GallerySample.String(state.Query)});\n"
                + $"var search = new UiState<string>({GallerySample.String(state.Search)});\n"
                + $"var chosen = new UiState<string?>({GallerySample.String(state.Chosen)});\n"
                + $"var frequency = new UiState<string?>({GallerySample.String(state.Frequency)});\n"
                + $"var notify = new UiState<bool>({(state.Notify ? "true" : "false")});\n"
                + $"var sound = new UiState<bool>({(state.Sound ? "true" : "false")});\n"
                + $"var volume = new UiState<float>({GallerySample.Number(state.Volume)});\n"
                + $"var expanded = new UiState<string[]>([{string.Join(", ", state.Expanded.Select(GallerySample.String))}]);\n"
                + "var placement = new UiTooltipOptions { Positioning = new() { Position = UiPopupPosition.BelowStart } };\n"
                + factory
                + $"\nexample.Direction = UiFlowDirection.{state.Direction};\nexample.Render(ui);");
            _samples.Add(sample);
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle1("영역별 배치 방향"),
                Fluent.Button("배치 방향 바꾸기", () => provider.Direction = provider.Direction == UiFlowDirection.RightToLeft
                    ? UiFlowDirection.LeftToRight : UiFlowDirection.RightToLeft),
                sample);
        }
        private UiWidget FontExample(UiTypographyFonts fonts)
        {
            var monospace = fonts.Monospace ?? throw new InvalidOperationException("Provider font demo requires a configured monospace font.");
            var localFonts = fonts with { Regular = monospace, Semibold = monospace, Bold = monospace };
            var localTheme = new UiFluentProviderTheme(UiCompiledDesign.Windows11Dark.Theme, fonts : localFonts);
            var sample = GallerySample.Create(() => Fluent.FluentProvider(
                Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Subtitle1("Local typography 123"),
                    Fluent.Body1("영역별 글꼴과 입력 Agyp"),
                    Fluent.Input("글꼴 영역 입력", _text.Binding, 1024, 280)),
                localTheme, new(Padding : new(16), Width : 440)));
            var factory = sample.Source;
            sample.BindSource(() => (_text.Value, sample.Preview.Theme), state => TextSetup(state.Value)
                + "\nvar localFonts = new UiTypographyFonts(" + string.Join(", ", new[] { localFonts.Regular, localFonts.Semibold,
                    localFonts.Bold, localFonts.SecondaryRegular, localFonts.SecondarySemibold, localFonts.SecondaryBold }
                    .Select(path => path is null ? "null" : GallerySample.String(path)))
                + ") { Monospace = " + GallerySample.String(monospace) + " };\n"
                + "UiFluentProviderTheme? localTheme = " + (state.Theme?.Fonts is null ? "null"
                    : "new(UiCompiledDesign.Windows11Dark.Theme, fonts : localFonts)") + ";\n\n" + factory);
            _samples.Add(sample);
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle1("영역별 글꼴"),
                Fluent.Body1("영문은 고정폭 글꼴로 표시합니다. 한글 보조 글꼴과 시스템 글자 크기는 유지됩니다."),
                Fluent.Button("영역 글꼴 전환", () => sample.Preview.Theme = sample.Preview.Theme is null ? localTheme : null), sample);
        }
        private UiWidget Example(string name, UiFluentProviderTheme? theme)
        {
            var paintBackground = true;
            var sample = GallerySample.Create(() => Fluent.FluentProvider(
                Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Title3(name), Fluent.Input(name + " 입력", _text.Binding, 1024, 280),
                    Fluent.Button(name + " 버튼"),
                    Fluent.Tooltip(Fluent.Button(name + " 설명"), "설명도 이 영역의 테마를 따릅니다.")),
                theme, new(Padding : new(16), Width : 440), paintBackground));
            var preview = sample.Preview;
            var factory = sample.Source;
            sample.BindSource(() => (_text.Value, preview.Theme, preview.PaintBackground), state =>
                TextSetup(state.Value) + "\nvar name = " + GallerySample.String(name) + ";\n"
                + "UiFluentProviderTheme? theme = " + (state.Theme is null ? "null"
                    : ReferenceEquals(state.Theme, UiFluentProviderTheme.Light) ? "UiFluentProviderTheme.Light" : "UiFluentProviderTheme.Dark")
                + ";\nvar paintBackground = " + GallerySample.Boolean(state.PaintBackground) + ";\n\n" + factory);
            _samples.Add(sample);
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(name + " 테마 전환", () => preview.Theme = preview.Theme is null ? UiFluentProviderTheme.Light
                        : ReferenceEquals(preview.Theme, UiFluentProviderTheme.Light) ? UiFluentProviderTheme.Dark : null),
                    Fluent.Button(name + " 배경 전환", () => preview.PaintBackground = !preview.PaintBackground)),
                sample);
        }
        private static string TextSetup(string text) => "var _text = new UiState<string>(" + GallerySample.String(text) + ");";
        public void Dispose() => Root.Dispose();
    }
}
