using Duxel.Core;
using Duxel.Svg;

namespace Duxel.Gallery;

[UiFluentIcon("navigation", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct GalleryNavigationIcon;
[UiFluentIcon("edit", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct GalleryInputIcon;
[UiFluentIcon("apps", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct GalleryContentIcon;
[UiFluentIcon("people", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct GalleryPeopleIcon;
[UiFluentIcon("alert", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct GalleryFeedbackIcon;
[UiFluentIcon("image", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct GalleryMediaIcon;
[UiFluentIcon("calendar", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct GalleryCalendarIcon;

internal enum GalleryDestinationKind { Overview, Fluent, Duxel, Integration, Diagnostics }

/// <summary>Canonical component routes borrow persistent example pages; menu state never owns those pages.</summary>
internal sealed class GalleryNavigation : IDisposable
{
    private static readonly Dictionary<string, DateOnly> MenuRegistrationDates = new(StringComparer.Ordinal)
    {
        ["NavigationBar"] = new(2026, 11, 20),
        ["DataGrid"] = new(2026, 11, 21)
    };
    private sealed record Route(string Key, string Label, string Page);
    private readonly UiSizedBox _root;
    private readonly SvgStore _logoStore = new();
    private readonly UiBinding<string?> _selection;
    private readonly Dictionary<string, Route> _routes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _pageKeys = new(StringComparer.Ordinal);
    private readonly UiFluentNav _navigation;
    private readonly Dictionary<string, GalleryDestinationKind> _pageKinds = new(StringComparer.Ordinal);

    internal static bool IsNewMenu(string key, DateOnly today) =>
        MenuRegistrationDates.TryGetValue(key, out var registered) && today.DayNumber - registered.DayNumber is >= 0 and < 7;

    internal GalleryNavigation(IEnumerable<string> pages, string[] planned, UiBinding<string?> selection)
    {
        _selection = selection;
        var available = pages.ToHashSet(StringComparer.Ordinal);
        var pending = planned.ToHashSet(StringComparer.Ordinal);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(9));
        var home = UiMenuIcon.Create<HomeTabIcon, HomeTabFilledIcon>();
        var settings = UiMenuIcon.Create<SettingsTabIcon, SettingsTabFilledIcon>();
        var navigation = UiMenuIcon.Create<GalleryNavigationIcon, GalleryNavigationIcon>();
        var input = UiMenuIcon.Create<GalleryInputIcon, GalleryInputIcon>();
        var content = UiMenuIcon.Create<GalleryContentIcon, GalleryContentIcon>();
        var people = UiMenuIcon.Create<GalleryPeopleIcon, GalleryPeopleIcon>();
        var feedback = UiMenuIcon.Create<GalleryFeedbackIcon, GalleryFeedbackIcon>();
        var media = UiMenuIcon.Create<GalleryMediaIcon, GalleryMediaIcon>();
        (string Label, string Description, UiMenuIcon Icon, string[] Components)[] groups =
        [
            ("동작과 탐색", "명령·링크·화면 이동", navigation, ["Breadcrumb", "Button", "Link", "Menu", "Nav", "NavigationBar", "TabList", "Toolbar"]),
            ("입력과 선택", "입력·검증·선택·값 조절", input, ["Checkbox", "Combobox", "Dropdown", "Field", "Input", "Label", "RadioGroup", "Rating", "SearchBox", "SegmentedControl", "Select", "Slider", "SpinButton", "Switch", "TagPicker", "Textarea", "TextField"]),
            ("콘텐츠와 목록", "콘텐츠 배치·목록·계층", content, ["Accordion", "Card", "CardNudge", "Carousel", "DataGrid", "Divider", "List", "Tag", "Tree"]),
            ("사용자 정보", "아바타·프로필·배지", people, ["Avatar", "AvatarGroup", "Badge", "Persona"]),
            ("알림과 진행 상태", "공지·팝업·도움말·로딩", feedback, ["ActivityIndicator", "Dialog", "Drawer", "HeadsUpDisplay", "InfoLabel", "MessageBar", "Popover", "ProgressBar", "ProgressIndicator", "Shimmer", "Skeleton", "Spinner", "Toast", "Tooltip"]),
            ("텍스트와 그래픽", "글꼴·아이콘·이미지", media, ["Icon", "Image", "Text"]),
            ("테마와 설정", "디자인과 공통 설정", settings, ["FluentProvider"])
        ];
        var components = groups.SelectMany(static group => group.Components).ToArray();
        if (components.Distinct(StringComparer.Ordinal).Count() != components.Length
            || !Fluent.RequiredComponentNames.ToArray().Append("DataGrid").ToHashSet(StringComparer.Ordinal).SetEquals(components))
            throw new InvalidOperationException("Gallery groups must cover each Fluent destination exactly once.");
        var componentPages = CatalogPage.Groups.SelectMany(static group => group.Components.Select(component => (component, group.Page)))
            .ToDictionary(static entry => entry.component, static entry => entry.Page, StringComparer.Ordinal);
        var sections = new List<UiNavGroup>();
        void AddSection(string label, string description, UiMenuIcon icon, IEnumerable<Route> routes, bool preferredPage = false, GalleryDestinationKind kind = GalleryDestinationKind.Fluent)
        {
            var entries = routes.ToArray();
            var badge = new UiNavBadge("미구현", new(UiBadgeSize.Small, Appearance: UiBadgeAppearance.Tint, Color: UiBadgeColor.Severe));
            var newBadge = new UiNavBadge("NEW", new(UiBadgeSize.Small, Appearance : UiBadgeAppearance.Tint, Color : UiBadgeColor.Success));
            sections.Add(new("group-" + sections.Count, label, entries.Select(route => new UiNavItem(route.Key, route.Label,
                Badge: pending.Contains(route.Page) ? badge : IsNewMenu(route.Key, today) ? newBadge : null)).ToArray(), icon, description));
            foreach (var route in entries)
            {
                if (!available.Contains(route.Page))
                    throw new InvalidOperationException("Gallery component route has no example page : " + route.Page);
                _routes.Add(route.Key, route);
                _pageKinds.Add(route.Page, kind);
                if (preferredPage)
                    _pageKeys[route.Page] = route.Key;
                else
                    _pageKeys.TryAdd(route.Page, route.Key);
            }
        }
        AddSection("시작", "소개와 컨트롤 모음", home,
            [new("개요", "개요", "개요")], preferredPage: true, kind: GalleryDestinationKind.Overview);
        sections.Add(UiNavGroup.SectionHeader("section-fluent", "Fluent 컨트롤"));
        foreach (var group in groups)
            AddSection(group.Label, group.Description, group.Icon, group.Components.Order(StringComparer.Ordinal).Select(component =>
                new Route(component, component, available.Contains(component) ? component
                    : component is "TabList" or "Menu" ? "컨테이너" : componentPages[component])));
        sections.Add(UiNavGroup.SectionHeader("section-duxel", "Duxel 위젯"));
        AddSection("날짜와 시간", "날짜·시각·기간·공휴일", UiMenuIcon.Create<GalleryCalendarIcon, GalleryCalendarIcon>(),
            [new("Calendar", "Calendar", "Calendar"), new("DatePicker", "DatePicker", "DatePicker"),
             new("DateTimePicker", "DateTimePicker", "DateTimePicker"), new("RangeCalendar", "RangeCalendar", "RangeCalendar"),
             new("TimePicker", "TimePicker", "TimePicker"), new("공휴일 제공자", "공휴일 제공자", "공휴일 제공자")],
            preferredPage: true, kind: GalleryDestinationKind.Duxel);
        AddSection("문서와 그래픽", "Duxel의 문서·벡터 위젯", media,
            [new("SVG", "SVG", "SVG"), new("Markdown", "Markdown", "Markdown")],
            preferredPage: true, kind: GalleryDestinationKind.Duxel);
        AddSection("레이아웃과 입력", "배치·변환·가상화·입력 조합", content,
            [new("컨테이너", "레이아웃 예제", "컨테이너"), new("위젯 배치", "위젯 변환", "위젯 배치"),
             new("가상 목록", "가상 목록", "가상 목록"), new("가상 그리드", "가상 그리드", "가상 그리드"),
             new("팝업 입력", "팝업 입력", "팝업 입력")],
            preferredPage: true, kind: GalleryDestinationKind.Duxel);
        string[] tools = ["갱신 관찰", "측정 재사용", "렌더링", "관찰 범위", "기존 API"];
        var combined = available.Except(_pageKeys.Keys, StringComparer.Ordinal).Except(tools, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).Select(page => new Route(page, page + " · 종합", page)).ToArray();
        sections.Add(UiNavGroup.SectionHeader("section-development", "통합·개발 도구"));
        if (combined.Length > 0) AddSection("통합 예제", "여러 구성요소를 함께 사용하는 예제", settings, combined,
            preferredPage: true, kind: GalleryDestinationKind.Integration);
        AddSection("개발 도구", "그리기·캐시·갱신 범위", settings,
            [new("갱신 관찰", "갱신 관찰", "갱신 관찰"), new("측정 재사용", "측정 재사용", "측정 재사용"),
             new("렌더링", "렌더링", "렌더링"), new("관찰 범위", "구현 범위", "관찰 범위"),
             new("기존 API", "기존 API", "기존 API")], preferredPage: true, kind: GalleryDestinationKind.Diagnostics);
        if (_routes.Values.Select(static route => route.Page).Distinct(StringComparer.Ordinal).Count() != _routes.Count)
            throw new InvalidOperationException("Each Gallery destination must have its own example page.");
        if (!available.SetEquals(_pageKeys.Keys))
            throw new InvalidOperationException("Every persistent gallery page must have a navigation route.");
        if (MenuRegistrationDates.Keys.Any(key => !_routes.ContainsKey(key) || pending.Contains(_routes[key].Page)))
            throw new InvalidOperationException("Menu registration dates must refer to implemented destinations.");
        _navigation = Fluent.Nav("갤러리 탐색", selection, sections.ToArray());
        var scroll = Dux.ScrollView(_navigation);
        scroll.AccessibleName = "갤러리 탐색";
        var logo = new UiSvg(_logoStore.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "duxel-mark.svg")))
        {
            Size = new(36, 36),
            AccessibleName = "Duxel 로고"
        };
        var componentCount = Fluent.Badge($"{components.Length}개 구성요소", new(UiBadgeSize.Small, Appearance : UiBadgeAppearance.Ghost, Color : UiBadgeColor.Informative));
        var counts = planned.Length == 0 ? (UiWidget)componentCount : Dux.Wrap(new(Spacing : 8, RunSpacing : 6),
            componentCount,
            Fluent.Badge($"미구현 {planned.Length}", new(UiBadgeSize.Small, Appearance : UiBadgeAppearance.Tint, Color : UiBadgeColor.Severe)));
        var header = Dux.Padding(Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min),
            Dux.Row(new(Spacing : 10, CrossAxisAlignment : UiCrossAxisAlignment.Center), logo,
                Dux.Expanded(Dux.Column(new(Spacing : 2, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Subtitle2("Duxel 갤러리"), Fluent.Text("컨트롤 · 그래픽 · 문서")))),
            counts), new(8));
        _root = Dux.SizedBox(Dux.Column(new(Spacing : 8, CrossAxisAlignment : UiCrossAxisAlignment.Stretch),
            header, Dux.Expanded(scroll)));
    }

    internal GalleryDestinationKind KindForPage(string page) => _pageKinds[page];
    internal string? ResolvePage(string? key) => key is null ? null : _routes[key].Page;
    internal string KeyForPage(string page) => _pageKeys[page];
    internal string LabelForPage(string page) => _routes[KeyForPage(page)].Label;
    internal string SelectedLabel => _routes[_selection.Value ?? throw new InvalidOperationException("No gallery route is selected.")].Label;

    internal UiWidget Content => _root;
    internal void PrepareOverlay() => _root.Options = default;
    internal void Render(UiImmediateContext ui, UiVector2 size)
    {
        _root.Options = new(size.X, size.Y);
        _root.Render(ui);
    }

    internal void RenderPanel(UiImmediateContext ui, string page, Action<UiImmediateContext> render, bool navigationVisible = true)
    {
        var key = _selection.Value ?? throw new InvalidOperationException("No gallery route is selected.");
        if (_routes[key].Page != page)
            throw new InvalidOperationException("Gallery content does not match the selected route.");
        _navigation.RenderPage(ui, key, render, navigationVisible : navigationVisible);
    }

    public void Dispose() { _root.Dispose(); _logoStore.Dispose(); }
}
