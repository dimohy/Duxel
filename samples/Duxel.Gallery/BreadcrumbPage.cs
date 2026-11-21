using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class BreadcrumbPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "경로";
    public string Description => "현재 문서까지 이어지는 경로를 표시합니다. 숨겨진 상위 경로를 열거나 키보드로 이동하고, 긴 이름 위에 마우스를 올려 전체 이름을 확인해 보세요.";
    internal UiFluentBreadcrumb? Preview => _content?.Preview;
    internal string LastNavigation => _content?.LastNavigation ?? string.Empty;
    internal int Navigations => _content?.Navigations ?? 0;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Dispose();
    private sealed class Content : IDisposable
    {
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal string LastNavigation = GalleryText.Get("선택한 경로가 없습니다.");
        internal int Navigations;
        internal UiFluentBreadcrumb Preview
        {
            get;
        }
        internal UiWidget Root
        {
            get;
        }
        internal Content()
        {
            var caption = LastNavigation;
            var options = new UiBreadcrumbOptions(MaximumCharacters: 12, OverflowLabel: GalleryText.Get("숨긴 경로"), CurrentLabel: GalleryText.Get("현재 위치"));
            var maximumWidth = 520f;
            var sample = GallerySample.Create(() =>
            {
                UiBreadcrumbOption[] items = [new("home", GalleryText.Get("홈")), new("workspace", GalleryText.Get("작업 공간")),
                    new("category", GalleryText.Get("문서 보관함"), false), new("team", GalleryText.Get("함께 작업하는 팀")),
                    new("project", GalleryText.Get("디모이의 한글과 English를 포함한 긴 프로젝트 이름")), new("current", GalleryText.Get("현재 문서"))];
                var names = items.ToDictionary(item => item.Value, item => item.Label, StringComparer.Ordinal);
                return Fluent.Breadcrumb(GalleryText.Get("프로젝트 경로"), items,
                    value => { LastNavigation = value; caption = names[value]; Navigations++; }, options);
            }, layout: preview => Dux.ConstrainedBox(preview, new(0, maximumWidth, 0, float.PositiveInfinity)));
            Preview = sample.Preview;
            var viewport = (UiConstrainedBox)sample.PreviewLayout;
            var factory = sample.Source;
            sample.BindSource(() => (Location: LastNavigation, Count: Navigations, Caption: caption, Preview.Options, Width: viewport.Options.MaxWidth), state =>
                FormattableString.Invariant($$"""
                    var LastNavigation = {{GallerySample.String(state.Location)}};
                    var Navigations = {{state.Count}};
                    var caption = {{GallerySample.String(state.Caption)}};
                    var maximumWidth = {{state.Width}}f;
                    var options = new UiBreadcrumbOptions(
                        Size: UiBreadcrumbSize.{{state.Options.Size}},
                        FocusMode: UiBreadcrumbFocusMode.{{state.Options.FocusMode}},
                        RightToLeft: {{GallerySample.Boolean(state.Options.RightToLeft)}},
                        MaximumCharacters: {{state.Options.MaximumCharacters}},
                        TooltipMaximumCharacters: {{state.Options.TooltipMaximumCharacters}},
                        OverflowLabel: {{GallerySample.String(state.Options.OverflowLabel)}},
                        CurrentLabel: {{GallerySample.String(state.Options.CurrentLabel)}});

                    """) + factory);
            Samples = [sample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("경로 폭 변경"), () => viewport.Options = viewport.Options with
                    {
                        MaxWidth = viewport.Options.MaxWidth == 520 ? 280 : viewport.Options.MaxWidth == 280 ? 80 : 520
                    }),
                    Fluent.Button(GalleryText.Get("경로 키보드 방식"), () => Preview.Options = Preview.Options with
                    {
                        FocusMode = Preview.Options.FocusMode == UiBreadcrumbFocusMode.Tab ? UiBreadcrumbFocusMode.Arrow : UiBreadcrumbFocusMode.Tab
                    }),
                    Fluent.Button(GalleryText.Get("경로 방향 전환"), () => Preview.Options = Preview.Options with { RightToLeft = !Preview.Options.RightToLeft }),
                    Fluent.Button(GalleryText.Get("경로 크기 변경"), () => Preview.Options = Preview.Options with
                    {
                        Size = (UiBreadcrumbSize)(((int)Preview.Options.Size + 1) % 3)
                    })),
                Fluent.Body1(() => GalleryText.Format("이동 : {0} / 실행 : {1}", caption, Navigations)),
                Fluent.Body1(() => GalleryText.Format("키보드 : {0} / 방향 : {1}",
                    Preview.Options.FocusMode == UiBreadcrumbFocusMode.Tab ? GalleryText.Get("Tab 이동") : GalleryText.Get("방향키 이동"),
                    Preview.Options.RightToLeft ? GalleryText.Get("오른쪽부터") : GalleryText.Get("왼쪽부터"))),
                Fluent.Text(GalleryText.Get("너비가 좁아지면 두 번째 경로부터 더 보기 메뉴에 숨깁니다. 매우 좁으면 현재 위치만 표시합니다.")),
                Fluent.Text(GalleryText.Get("문서 보관함으로는 이동할 수 없지만 Tab과 방향키로 포커스를 옮겨 설명을 읽을 수 있습니다. 현재 위치를 눌러도 다른 곳으로 이동하지 않습니다.")),
                Fluent.Text(GalleryText.Get("긴 이름 위에 마우스를 올리거나 포커스를 두면 Tooltip이 표시됩니다. 탭을 바꿔도 설정과 이동 기록을 유지합니다.")),
                sample));
            Root.AccessibleName = GalleryText.Get("경로 예제");
        }
        public void Dispose() => Root.Dispose();
    }
}
