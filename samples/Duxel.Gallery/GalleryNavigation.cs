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

internal sealed record GalleryDestination(string Key, string Label, string Page, string Section, string Category, bool Planned);

/// <summary>Canonical component routes borrow persistent example pages; menu state never owns those pages.</summary>
internal sealed class GalleryNavigation : IDisposable
{
    // Increase once for each fix or feature delivered in the Gallery.
    private const int GalleryRevision = 6;
    internal static string DisplayVersion { get; } = FormatVersion();

    private static string FormatVersion()
    {
        var version = typeof(Duxel.App.DuxelAppSession).Assembly.GetName().Version
            ?? throw new InvalidOperationException("The Duxel App assembly must provide its version.");
        return FormattableString.Invariant($"{version.Major}.{version.Minor}.{GalleryRevision}");
    }

    private static readonly Dictionary<string, DateOnly> MenuRegistrationDates = new(StringComparer.Ordinal)
    {
        ["NavigationBar"] = new(2026, 11, 20),
        ["CodeView"] = new(2026, 11, 23),
        ["Animation"] = new(2026, 11, 23),
        ["DataGrid"] = new(2026, 11, 21),
        ["파일 대화 상자"] = new(2026, 11, 21)
    };
    private sealed record Route(string Key, string Label, string Page);
    private readonly UiSizedBox _root;
    private readonly SvgStore _logoStore = new();
    private readonly UiBinding<string?> _selection;
    private readonly Dictionary<string, Route> _routes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _pageKeys = new(StringComparer.Ordinal);
    private readonly UiFluentNav _navigation;
    private readonly Dictionary<string, GalleryDestinationKind> _pageKinds = new(StringComparer.Ordinal);
    private readonly List<GalleryDestination> _destinations = [];

    internal IReadOnlyList<GalleryDestination> Destinations => _destinations;

    internal static bool IsNewMenu(string key, DateOnly today) =>
        MenuRegistrationDates.TryGetValue(key, out var registered) && today.DayNumber - registered.DayNumber is >= 0 and < 7;

    internal GalleryNavigation(IEnumerable<string> pages, string[] planned, UiBinding<string?> selection)
    {
        _selection = selection;
        var registered = pages.ToArray();
        var available = registered.ToHashSet(StringComparer.Ordinal);
        if (available.Count != registered.Length)
            throw new InvalidOperationException("Gallery destinations must have unique page titles.");
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
            ("Actions and navigation", "Commands, links, and page navigation", navigation, ["Breadcrumb", "Button", "Link", "Menu", "Nav", "NavigationBar", "TabList", "Toolbar"]),
            ("Input and selection", "Input, validation, selection, and value adjustment", input, ["Checkbox", "Combobox", "Dropdown", "Field", "Input", "Label", "RadioGroup", "Rating", "SearchBox", "SegmentedControl", "Select", "Slider", "SpinButton", "Switch", "TagPicker", "Textarea", "TextField"]),
            ("Content and lists", "Content layout, lists, and hierarchies", content, ["Accordion", "Card", "CardNudge", "Carousel", "DataGrid", "Divider", "List", "Tag", "Tree"]),
            ("People", "Avatars, profiles, and badges", people, ["Avatar", "AvatarGroup", "Badge", "Persona"]),
            ("Alerts and progress", "Notices, pop-ups, help, and loading", feedback, ["ActivityIndicator", "Dialog", "Drawer", "HeadsUpDisplay", "InfoLabel", "MessageBar", "Popover", "ProgressBar", "ProgressIndicator", "Shimmer", "Skeleton", "Spinner", "Toast", "Tooltip"]),
            ("Text and graphics", "Fonts, icons, and images", media, ["Icon", "Image", "Text"]),
            ("Themes and settings", "Design and shared settings", settings, ["FluentProvider"])
        ];
        var components = groups.SelectMany(static group => group.Components).ToArray();
        if (components.Distinct(StringComparer.Ordinal).Count() != components.Length
            || !Fluent.RequiredComponentNames.ToArray().Append("DataGrid").ToHashSet(StringComparer.Ordinal).SetEquals(components))
            throw new InvalidOperationException("Gallery groups must cover each Fluent destination exactly once.");
        var componentPages = CatalogPage.Groups.SelectMany(static group => group.Components.Select(component => (component, group.Page)))
            .ToDictionary(static entry => entry.component, static entry => entry.Page, StringComparer.Ordinal);
        var sections = new List<UiNavGroup>();
        var section = string.Empty;
        void AddHeader(string key, string text)
        {
            section = GalleryText.Get(text);
            sections.Add(UiNavGroup.SectionHeader(key, section));
        }
        void AddSection(string label, string description, UiMenuIcon icon, IEnumerable<Route> routes, bool preferredPage = false, GalleryDestinationKind kind = GalleryDestinationKind.Fluent, bool labelsLocalized = false)
        {
            var entries = routes.Select(route => route with { Label = labelsLocalized ? route.Label : GalleryText.Get(route.Label) }).ToArray();
            var badge = new UiNavBadge(GalleryText.Get("Planned"), new(UiBadgeSize.Small, Appearance: UiBadgeAppearance.Tint, Color: UiBadgeColor.Severe));
            var newBadge = new UiNavBadge(GalleryText.Get("NEW"), new(UiBadgeSize.Small, Appearance : UiBadgeAppearance.Tint, Color : UiBadgeColor.Success));
            sections.Add(new("group-" + sections.Count, GalleryText.Get(label), entries.Select(route => new UiNavItem(route.Key, route.Label,
                Badge: pending.Contains(route.Page) ? badge : IsNewMenu(route.Key, today) ? newBadge : null)).ToArray(), icon, GalleryText.Get(description)));
            foreach (var route in entries)
            {
                if (!available.Contains(route.Page))
                    throw new InvalidOperationException("Gallery component route has no example page : " + route.Page);
                _routes.Add(route.Key, route);
                _destinations.Add(new(route.Key, route.Label, route.Page, section, GalleryText.Get(label), pending.Contains(route.Page)));
                _pageKinds.Add(route.Page, kind);
                if (preferredPage)
                    _pageKeys[route.Page] = route.Key;
                else
                    _pageKeys.TryAdd(route.Page, route.Key);
            }
        }
        AddSection("Get started", "Introduction and examples", home,
            [new("개요", "Overview", "개요"), new("관찰 범위", "Feature index", "관찰 범위")], preferredPage: true, kind: GalleryDestinationKind.Overview);
        AddHeader("section-fluent", "Fluent controls");
        foreach (var group in groups)
            AddSection(group.Label, group.Description, group.Icon, group.Components.Order(StringComparer.Ordinal).Select(component =>
                new Route(component, component, available.Contains(component) ? component
                    : component is "TabList" or "Menu" ? "컨테이너" : componentPages[component])));
        AddHeader("section-duxel", "Duxel widgets");
        AddSection("Date and time", "Dates, times, ranges, and holidays", UiMenuIcon.Create<GalleryCalendarIcon, GalleryCalendarIcon>(),
            [new("Calendar", "Calendar", "Calendar"), new("DatePicker", "DatePicker", "DatePicker"),
             new("DateTimePicker", "DateTimePicker", "DateTimePicker"), new("RangeCalendar", "RangeCalendar", "RangeCalendar"),
             new("TimePicker", "TimePicker", "TimePicker"), new("공휴일 제공자", "Holiday provider", "공휴일 제공자")],
            preferredPage: true, kind: GalleryDestinationKind.Duxel);
        AddSection("Documents and graphics", "Duxel document and vector widgets", media,
            [new("CodeView", "CodeView", "CodeView"), new("SVG", "SVG", "SVG"), new("Markdown", "Markdown", "Markdown"),
             new("Localization", "Localization", "Localization")],
            preferredPage: true, kind: GalleryDestinationKind.Duxel);
        AddSection("Motion", "Transitions and reduced motion", media,
            [new("Animation", "Animation", "Animation")], preferredPage: true, kind: GalleryDestinationKind.Duxel);
        AddSection("Layout and input", "Layout, transforms, virtualization, and input", content,
            [new("컨테이너", "Layout examples", "컨테이너"), new("위젯 배치", "Widget transforms", "위젯 배치"),
             new("가상 목록", "Virtual list", "가상 목록"), new("가상 그리드", "Virtual grid", "가상 그리드"),
             new("팝업 입력", "Pop-up input", "팝업 입력")],
            preferredPage: true, kind: GalleryDestinationKind.Duxel);
        string[] tools = ["갱신 관찰", "측정 재사용", "렌더링", "기존 API"];
        AddHeader("section-integration", "System integration");
        AddSection("Files and folders", "Operating system file and folder selection", settings,
            [new("파일 대화 상자", "File dialogs", "파일 대화 상자")],
            preferredPage: true, kind: GalleryDestinationKind.Integration);
        var combined = available.Except(_pageKeys.Keys, StringComparer.Ordinal).Except(tools, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).Select(page => new Route(page, GalleryText.Format("{0} · combined", GalleryText.PageTitle(page)), page)).ToArray();
        if (combined.Length > 0) AddSection("Combined examples", "Examples that combine several components", settings, combined,
            preferredPage: true, kind: GalleryDestinationKind.Integration, labelsLocalized: true);
        AddHeader("section-development", "Development tools");
        AddSection("Rendering and diagnostics", "Drawing, caching, and repaint regions", settings,
            [new("갱신 관찰", "Repaint observer", "갱신 관찰"), new("측정 재사용", "Measurement reuse", "측정 재사용"),
             new("렌더링", "Rendering", "렌더링"),
             new("기존 API", "Legacy API", "기존 API")], preferredPage: true, kind: GalleryDestinationKind.Diagnostics);
        if (_routes.Values.Select(static route => route.Page).Distinct(StringComparer.Ordinal).Count() != _routes.Count)
            throw new InvalidOperationException("Each Gallery destination must have its own example page.");
        if (!available.SetEquals(_pageKeys.Keys))
            throw new InvalidOperationException("Every persistent gallery page must have a navigation route.");
        if (MenuRegistrationDates.Keys.Any(key => !_routes.ContainsKey(key) || pending.Contains(_routes[key].Page)))
            throw new InvalidOperationException("Menu registration dates must refer to implemented destinations.");
        _navigation = Fluent.Nav(GalleryText.Get("Gallery navigation"), selection, sections.ToArray());
        var scroll = Dux.ScrollView(_navigation);
        scroll.AccessibleName = GalleryText.Get("Gallery navigation");
        var logo = new UiSvg(_logoStore.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "duxel-mark.svg")))
        {
            Size = new(36, 36),
            AccessibleName = GalleryText.Get("Duxel logo")
        };
        var componentCount = Fluent.Badge(GalleryText.Format("{0} components", components.Length), new(UiBadgeSize.Small, Appearance : UiBadgeAppearance.Ghost, Color : UiBadgeColor.Informative));
        var counts = planned.Length == 0 ? (UiWidget)componentCount : Dux.Wrap(new(Spacing : 8, RunSpacing : 6),
            componentCount,
            Fluent.Badge(GalleryText.Format("{0} planned", planned.Length), new(UiBadgeSize.Small, Appearance : UiBadgeAppearance.Tint, Color : UiBadgeColor.Severe)));
        var header = Dux.Padding(Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min),
            Dux.Row(new(Spacing : 10, CrossAxisAlignment : UiCrossAxisAlignment.Center), logo,
                Dux.Expanded(Dux.Column(new(Spacing : 2, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Subtitle2(GalleryText.Get("Duxel Gallery")), Fluent.Caption1(GalleryText.Get("Controls · Graphics · Documents"))))),
            Dux.Wrap(new(Spacing : 10, RunSpacing : 4, CrossAlignment : UiWrapCrossAlignment.Center),
                counts, Fluent.Caption1(GalleryText.Format("Version {0}", DisplayVersion)))), new(8));
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
