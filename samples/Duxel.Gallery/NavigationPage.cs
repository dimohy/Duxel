using Duxel.Core;

namespace Duxel.Gallery;

[UiFluentIcon("pin", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct NavPinIcon;
[UiFluentIcon("pin", UiFluentIconStyle.Filled, 20)]
internal readonly partial struct NavPinFilledIcon;

internal sealed class NavigationPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    public IReadOnlyList<IGallerySample> Samples { get; }
    private readonly UiState<string?> _selected = new("overview");
    private readonly UiFluentNav _preview;
    private readonly UiWidget _root;
    private int _pinActions;
    internal int PinActions => _pinActions;
    internal UiFluentNav Preview => _preview;
    public string Title => "탐색 메뉴";
    public string Description => "Try collapsing groups, selecting a page, keyboard navigation, and right-to-left layout.";
    internal NavigationPage()
    {
        var options = new UiNavOptions();
        var sample = GallerySample.Create(() => Fluent.Nav("탐색 예제", _selected, new UiNavGroup[]
        {
            UiNavGroup.SectionHeader("work", "작업 공간"),
            new("start", "시작", new UiNavItem[] { new("overview", "개요", SecondaryAction : new("개요 고정", () => _pinActions++, UiMenuIcon.Create<NavPinIcon, NavPinFilledIcon>())), new("activity", "최근 활동", Badge : new("새 소식")) },
                UiMenuIcon.Create<HomeTabIcon, HomeTabFilledIcon>()),
            UiNavGroup.SectionHeader("library", "라이브러리"),
            new("documents", "문서", new UiNavItem[] { new("documents-all", "프로젝트의 긴 문서 이름은 사용 가능한 너비에 맞게 줄바꿈됩니다"),
                new("disabled", "사용할 수 없음", false) }, UiMenuIcon.Create<GalleryContentIcon, GalleryContentIcon>())
        }, options));
        _preview = sample.Preview;
        var factory = sample.Source;
        sample.BindSource(() => (_selected.Value, _pinActions, _preview.Options), state =>
            "var _selected = new UiState<string?>(" + GallerySample.String(state.Value) + ");\n"
            + FormattableString.Invariant($"var _pinActions = {state._pinActions};\nvar options = new UiNavOptions(Width: {state.Options.Width}f, RightToLeft: {GallerySample.Boolean(state.Options.RightToLeft)});\n\n") + factory
            + "\n\n" + IconDeclarations);
        Samples = [sample];
        _root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1("위·아래 방향키와 Home·End로 이동하고 Enter로 페이지를 선택하세요. 그룹은 좌·우 방향키 또는 클릭으로 접고 펼칩니다."),
            Fluent.Caption1("작은 섹션 제목은 메뉴를 분류하는 데 쓰입니다. 선택하거나 접을 수 없으며 키보드 포커스도 받지 않습니다."),
            Fluent.Button("배치 방향 전환", () => _preview.Options = _preview.Options with { RightToLeft = !_preview.Options.RightToLeft }),
            Fluent.Body1(() => "현재 페이지 : " + _selected.Value),
            Fluent.Body1(() => "개요 고정 실행 : " + _pinActions),
            Fluent.Caption1("개요 위에 마우스를 올리면 고정 버튼이 나타납니다. Tab 또는 가로 방향키로 버튼에 이동하고, 우클릭·Shift+F10으로 같은 작업을 메뉴에서 실행할 수 있습니다."),
            Fluent.Caption1("그룹을 접어도 선택은 유지됩니다. 접힌 그룹의 강조 표시는 현재 페이지가 그 안에 있음을 나타냅니다."),
            sample));
    }
    private const string IconDeclarations = """
        [UiFluentIcon("pin", UiFluentIconStyle.Regular, 20)] readonly partial struct NavPinIcon;
        [UiFluentIcon("pin", UiFluentIconStyle.Filled, 20)] readonly partial struct NavPinFilledIcon;
        [UiFluentIcon("home")] readonly partial struct HomeTabIcon;
        [UiFluentIcon("home", UiFluentIconStyle.Filled)] readonly partial struct HomeTabFilledIcon;
        [UiFluentIcon("apps", UiFluentIconStyle.Regular, 20)] readonly partial struct GalleryContentIcon;
        """;
    UiWidget IObserverNaturalPage.MeasureRoot => _root;
    public void Render(UiImmediateContext ui) => _root.Render(ui);
    public void Dispose() => _root.Dispose();
}
