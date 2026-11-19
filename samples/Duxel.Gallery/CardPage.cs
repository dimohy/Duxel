using Duxel.Core;
using Duxel.Svg;

namespace Duxel.Gallery;

internal sealed class CardPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "카드";
    public string Description => "카드 안에 글자를 입력하거나 버튼을 눌러 보세요. 크기와 모양을 바꾸고 Enter·Tab·Escape로 이동할 수 있습니다.";
    internal UiFluentCard? Preview => _content?.Preview;
    internal int Executions => _content?.Executions ?? 0;
    internal int HeaderExecutions => _content?.HeaderExecutions ?? 0;
    internal int RootExecutions => _content?.RootExecutions ?? 0;
    internal bool Selected => _content?.Selected ?? false;
    internal int SelectionWrites => _content?.SelectionWrites ?? 0;
    internal bool FloatingSelected => _content?.FloatingSelected ?? false;
    internal int FloatingWrites => _content?.FloatingWrites ?? 0;
    internal int FloatingExecutions => _content?.FloatingExecutions ?? 0;
    internal int ClippedExecutions => _content?.ClippedExecutions ?? 0;
    internal bool ClippingEnabled => _content?.ClippingEnabled ?? false;
    internal string Memo => _content?.Memo ?? string.Empty;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Dispose();

    private sealed class Content : IDisposable
    {
        internal readonly List<IGallerySample> Samples = [];
        internal int Executions;
        internal int HeaderExecutions;
        internal int RootExecutions;
        internal bool Selected;
        internal int SelectionWrites;
        internal bool FloatingSelected;
        internal int FloatingWrites;
        internal int FloatingExecutions;
        private readonly UiBinding<bool> _selection;
        private readonly SvgStore _previewStore = new();
        private readonly UiCard<FlushCardPresentation> _clippedCard;
        internal int ClippedExecutions;
        internal bool ClippingEnabled => _clippedCard.Options.Size != UiCardSize.Small;
        internal string Memo = "편집하세요";
        internal UiFluentCard Preview { get; }
        internal UiWidget Root { get; }
        internal Content()
        {
            _selection = new(() => Selected, value => { Selected = value; SelectionWrites++; });
            var PreviewSample = GallerySample.Create(() => Fluent.Card("작업 카드", Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.CardHeader(Fluent.Body1Strong("작업"),
                    Fluent.Caption1(() => $"안내 : {HeaderExecutions}"),
                    Fluent.Avatar("디모이", new(UiAvatarSize.Size24, Color: UiAvatarColor.Colorful)),
                    Fluent.Button("카드 정보", () => HeaderExecutions++)),
                Fluent.Input("카드 메모", new UiBinding<string>(() => Memo, value => Memo = value), 100, 240),
                Fluent.CardFooter([Fluent.Button("카드 내부 실행", () => Executions++)],
                    Fluent.Caption1(() => $"실행 : {Executions}"))), new(Width: 320)),
                setup: "var Memo = \"편집하세요\";\nvar Executions = 0;\nvar HeaderExecutions = 0;");
            Preview = PreviewSample.Preview;
            Samples.Add(PreviewSample);
            var floatingSelection = new UiBinding<bool>(() => FloatingSelected,
                value => { FloatingSelected = value; FloatingWrites++; });
            var floatingAction = Fluent.Checkbox("##floating-selection", floatingSelection);
            floatingAction.AccessibleName = "떠 있는 카드 선택";
            var floatingCardSample = GallerySample.Create(() => Fluent.Card("떠 있는 동작 카드", Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Body1("카드 선택"), Fluent.Button("떠 있는 카드 내부 실행", () => FloatingExecutions++)),
                new(Width: 320, Height: 120), selection: floatingSelection, floatingAction: floatingAction),
                setup: "var FloatingSelected = false;\nvar FloatingWrites = 0;\nvar FloatingExecutions = 0;\nvar floatingSelection = new UiBinding<bool>(() => FloatingSelected, value => { FloatingSelected = value; FloatingWrites++; });\nvar floatingAction = Fluent.Checkbox(\"떠 있는 카드 선택\", floatingSelection);");
            var floatingCard = floatingCardSample.Preview;
            Samples.Add(floatingCardSample);
            var clippedCardSample = GallerySample.Create(() => new UiCard<FlushCardPresentation>("둥근 경계 카드", Dux.Expand(Fluent.Button("둥근 카드 내부 실행", () => ClippedExecutions++)),
                new(Width: 320, Height: 120)),
                setup: "var ClippedExecutions = 0;",
                declarations: "readonly struct FlushCardPresentation : IUiCardPresentation\n{\n    public static bool CanRetainMeasurement => true;\n    public static UiCardMetrics Measure(UiCardOptions options) => new(0, options.Size == UiCardSize.Small ? 0 : 32);\n    public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options, UiCardMetrics metrics, UiButtonInteraction interaction) { }\n}");
            _clippedCard = clippedCardSample.Preview;
            Samples.Add(clippedCardSample);
            var clipping = Fluent.Button("둥근 모서리 : 켜짐");
            clipping.Activated = () =>
            {
                _clippedCard.Options = _clippedCard.Options with { Size = ClippingEnabled ? UiCardSize.Small : UiCardSize.Medium };
                clipping.Label = ClippingEnabled ? "둥근 모서리 : 켜짐" : "둥근 모서리 : 꺼짐";
            };
            var artwork = _previewStore.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "artwork.svg"));
            var mediaCardSample = GallerySample.Create(() => Fluent.Card("미리보기 카드", Dux.CardContent([
                Fluent.CardPreview(new UiSvg(artwork) { Size = new(320, 140) },
                    Fluent.Avatar("디모이", new(UiAvatarSize.Size32, Color: UiAvatarColor.Colorful))),
                Fluent.CardHeader(Fluent.Body1Strong("SVG 미리보기"), Fluent.Caption1("부모 여백까지 채운 이미지와 고정 로고")),
                Fluent.CardFooter([Fluent.Button("미리보기 실행", () => Executions++)])]), new(Width: 320)),
                setup: "var store = new SvgStore();\nvar artwork = store.Load(Path.Combine(AppContext.BaseDirectory, \"Assets\", \"Svg\", \"artwork.svg\"));\nvar Executions = 0;");
            var mediaCard = mediaCardSample.Preview;
            Samples.Add(mediaCardSample);
            var horizontalMediaCardSample = GallerySample.Create(() => Fluent.Card("가로 미리보기 카드", Dux.CardContent([
                Fluent.CardPreview(new UiSvg(artwork) { Size = new(180, 100) }),
                Fluent.CardHeader(Fluent.Body1Strong("가로 카드"), Fluent.Caption1("이미지 다음 Header가 남은 폭을 채웁니다."))]),
                new(Width: 520, Orientation: UiCardOrientation.Horizontal)),
                setup: "var store = new SvgStore();\nvar artwork = store.Load(Path.Combine(AppContext.BaseDirectory, \"Assets\", \"Svg\", \"artwork.svg\"));");
            var horizontalMediaCard = horizontalMediaCardSample.Preview;
            Samples.Add(horizontalMediaCardSample);
            var minimumCard = Fluent.Card("최소 크기 카드", Dux.CardContent([
                Fluent.CardPreview(new UiSvg(artwork) { Size = new(180, 80) }),
                Fluent.CardHeader(Fluent.Body1Strong("부모 최소 크기"), Fluent.Caption1("여유 폭·중앙 정렬"))]),
                new(Orientation: UiCardOrientation.Horizontal));
            var minimumRootSample = GallerySample.Create(() => Dux.ConstrainedBox(minimumCard, new(340, 420, 180, 240)),
                setup: "var store = new SvgStore();\nvar artwork = store.Load(Path.Combine(AppContext.BaseDirectory, \"Assets\", \"Svg\", \"artwork.svg\"));\nvar minimumCard = Fluent.Card(\"최소 크기 카드\", Dux.CardContent([\n                Fluent.CardPreview(new UiSvg(artwork) { Size = new(180, 80) }),\n                Fluent.CardHeader(Fluent.Body1Strong(\"부모 최소 크기\"), Fluent.Caption1(\"여유 폭·중앙 정렬\"))]),\n                new(Orientation: UiCardOrientation.Horizontal));");
            var minimumRoot = minimumRootSample.Preview;
            Samples.Add(minimumRootSample);
            var gallery = new List<UiWidget>();
            foreach (var size in Enum.GetValues<UiCardSize>())
            foreach (var appearance in Enum.GetValues<UiCardAppearance>())
            {
                var sample = GallerySample.Create(() => Fluent.Card($"비교 {size} {appearance}",
                    Dux.Column(new(Spacing: 4, MainAxisSize: UiMainAxisSize.Min),
                        Fluent.Body1(appearance.ToString()), Fluent.Caption1(size.ToString())), new(size, appearance, Width: 200)),
                    setup: $"var size = UiCardSize.{size};\nvar appearance = UiCardAppearance.{appearance};");
                Samples.Add(sample);
                gallery.Add(sample);
            }
            Root = Dux.ScrollView(Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button("카드 외관 변경", () => Preview.Options = Preview.Options with
                    { Appearance = (UiCardAppearance)(((int)Preview.Options.Appearance + 1) % 4) }),
                    Fluent.Button("카드 크기 변경", () => Preview.Options = Preview.Options with
                    { Size = (UiCardSize)(((int)Preview.Options.Size + 1) % 3) }),
                    Fluent.Button("카드 폭 변경", () => Preview.Options = Preview.Options with
                    { Width = Preview.Options.Width == 320 ? 240 : 320 }),
                    Fluent.Button("카드 포커스 방식", () => Preview.Options = Preview.Options with
                    { FocusMode = (UiFocusGroupMode)(((int)(Preview.Options.FocusMode ?? UiFocusGroupMode.Off) + 1) % 4) }),
                    Fluent.Button("카드 루트 사용 전환", () => Preview.Options = Preview.Options with
                    { Disabled = !Preview.Options.Disabled }),
                    Fluent.Button("카드 루트 동작 전환", () => Preview.Activated = Preview.Activated is null ? () => RootExecutions++ : null),
                    Fluent.Button("카드 선택 모드 전환", () => Preview.Selection = Preview.Selection is null ? _selection : null)),
                Fluent.Body1(() => $"카드 루트 실행 횟수 : {RootExecutions}"),
                Fluent.Body1(() => $"선택 모드 : {(Preview.Selection is null ? "꺼짐" : Selected ? "선택됨" : "선택 해제")} / 변경 횟수 : {SelectionWrites}"),
                Fluent.Body1(() => Preview.Selection is not null ? "포커스 : 선택 체크박스 · Enter/Space 전환 · 내부 동작 독립"
                    : (Preview.Options.FocusMode ?? (Preview.Activated is null ? UiFocusGroupMode.Off : UiFocusGroupMode.NoTab)) switch
                {
                    UiFocusGroupMode.NoTab => "포커스 : Enter 진입 · Tab 내부 순환 · Escape 복귀",
                    UiFocusGroupMode.TabExit => "포커스 : Enter 진입 · 마지막 Tab으로 바깥 이동 · Escape 복귀",
                    UiFocusGroupMode.TabOnly => "포커스 : Tab으로 내부 이동 · Escape 복귀",
                    _ => "포커스 : 카드 자체는 건너뛰고 내부 동작으로 이동"
                }),
                Fluent.Body1(() => Preview.Options.Disabled ? "카드 루트 : 사용 불가 / 내부 입력·버튼은 사용 가능" : "카드 루트 : 사용 가능"),
                Fluent.Text("카드 안의 입력과 버튼은 독립적으로 동작하며 탭을 바꿔도 내용을 유지합니다."),
                Fluent.Text("카드 배경을 눌렀을 때의 선택과 실행은 카드 안의 버튼·입력창과 별개로 동작합니다. 헤더와 푸터, 카드 위에 띄운 버튼을 넣을 수 있고, 콘텐츠를 둥근 모서리에 맞춰 자를 수도 있습니다. Preview는 카드 여백까지 채운 이미지와 로고를 표시합니다."),
                PreviewSample,
                Fluent.Subtitle1("떠 있는 선택 동작"),
                Fluent.Caption1(() => $"떠 있는 카드 선택 : {FloatingSelected} / 변경 : {FloatingWrites} / 내부 실행 : {FloatingExecutions}"),
                Fluent.Text("오른쪽 체크박스와 배경은 선택값을 바꿉니다. 내부 버튼은 별도로 실행됩니다."),
                floatingCardSample,
                Fluent.Subtitle1("둥근 경계와 입력"),
                clipping,
                Fluent.Caption1(() => $"둥근 카드 내부 실행 : {ClippedExecutions}"),
                Fluent.Text("버튼을 안쪽 여백 없이 채웠습니다. 잘려 나간 모서리를 눌러도 반응하지 않으며, 가운데 버튼은 따로 실행됩니다."),
                clippedCardSample,
                Fluent.Subtitle1("미디어 미리보기"),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button("미리보기 폭 변경", () => mediaCard.Options = mediaCard.Options with
                        { Width = mediaCard.Options.Width == 320 ? 400 : 320 }),
                    Fluent.Button("미리보기 크기 변경", () => mediaCard.Options = mediaCard.Options with
                        { Size = (UiCardSize)(((int)mediaCard.Options.Size + 1) % 3) })),
                Fluent.Text("SVG는 한 번 읽고 재사용합니다. 로고는 32px이며 왼쪽·아래 12px를 유지합니다."),
                mediaCardSample,
                horizontalMediaCardSample,
                Fluent.Subtitle1("부모의 최소 크기"),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button("부모 최소 폭 변경", () => minimumRoot.Options = minimumRoot.Options with
                        { MinWidth = minimumRoot.Options.MinWidth == 340 ? 400 : 340 }),
                    Fluent.Button("최소 크기 카드 방향", () => minimumCard.Options = minimumCard.Options with
                        { Orientation = minimumCard.Options.Orientation == UiCardOrientation.Horizontal
                            ? UiCardOrientation.Vertical : UiCardOrientation.Horizontal })),
                Fluent.Text("헤더는 최소 크기보다 작아지지 않으면서 가로로 남은 공간을 채웁니다. 이미지는 세로 가운데에 놓입니다."),

                minimumRootSample,
                Dux.Column(new(Spacing: 24, MainAxisSize: UiMainAxisSize.Min, CrossAxisAlignment: UiCrossAxisAlignment.Stretch), gallery.ToArray())));
            Root.AccessibleName = "카드 예제";
        }
        private readonly struct FlushCardPresentation : IUiCardPresentation
        {
            public static bool CanRetainMeasurement => true;
            public static UiCardMetrics Measure(UiCardOptions options) => new(0, options.Size == UiCardSize.Small ? 0 : 32);
            // The owned button supplies the fill so the sample exposes the actual clip boundary.
            public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options, UiCardMetrics metrics, UiButtonInteraction interaction) { }
        }
        public void Dispose() { Root.Dispose(); _previewStore.Dispose(); }
    }
}
