using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class CatalogPage : UiWidget
{
    internal static readonly (string Page, string[] Components)[] Groups =
    [
        ("왼쪽 탐색 메뉴·Fluent 표현 비교·컨테이너 넘침", ["TabList"]),
        ("Fluent", ["Button", "ToggleButton", "Checkbox", "Switch", "RadioGroup", "Slider", "ProgressBar", "Badge", "CounterBadge", "Divider", "Link", "Spinner"]),
        ("Button", ["CompoundButton", "SplitButton"]),
        ("SegmentedControl", ["SegmentedControl"]),
        ("Toast", ["Toast"]),
        ("Select", ["Select"]),
        ("Tree", ["Tree"]),
        ("TextField", ["TextField"]),
        ("CardNudge", ["CardNudge"]),
        ("Carousel", ["Carousel"]),
        ("NavigationBar", ["NavigationBar"]),
        ("평가", ["Rating", "RatingDisplay"]),
        ("타이포그래피", ["Display", "LargeTitle", "Title1", "Title2", "Title3", "Subtitle1", "Subtitle2", "Subtitle2Stronger", "Body2", "Body1", "Body1Strong", "Body1Stronger", "Caption1", "Caption1Strong", "Caption1Stronger", "Caption2", "Caption2Strong", "Text"]),
        ("입력·IME", ["Input", "Textarea", "Field", "Label"]),
        ("Tooltip", ["Tooltip"]),
        ("Popover", ["Popover"]),
        ("대화 상자", ["Dialog"]),
        ("스켈레톤", ["Skeleton", "SkeletonItem"]),
        ("메시지 표시", ["MessageBar", "MessageBarActions", "MessageBarBody", "MessageBarTitle", "MessageBarGroup"]),
        ("추가 정보", ["InfoLabel"]),
        ("숫자 입력", ["SpinButton"]),
        ("접기·펼치기", ["Accordion"]),
        ("아바타", ["Avatar", "PresenceBadge"]),
        ("아바타 그룹", ["AvatarGroup"]),
        ("도구 모음", ["Toolbar"]),
        ("검색 입력", ["SearchBox"]),
        ("인물 정보", ["Persona"]),
        ("선택 목록", ["Dropdown", "Combobox", "Tag", "InteractionTag"]),
        ("TagPicker", ["TagPicker"]),
        ("FluentProvider", ["FluentProvider"]),
        ("Shimmer", ["Shimmer"]),
        ("ProgressIndicator", ["ProgressIndicator", "CircularProgressIndicator"]),
        ("ActivityIndicator", ["ActivityIndicator"]),
        ("HeadsUpDisplay", ["HeadsUpDisplay"]),
        ("카드", ["Card", "CardFooter", "CardHeader", "CardPreview"]),
        ("경로", ["Breadcrumb"]),
        ("탐색 메뉴", ["Nav"]),
        ("보조 패널", ["Drawer", "InlineDrawer", "OverlayDrawer"]),
        ("목록", ["List"]),
        ("SVG", ["Icon", "Image"]),
        ("컨테이너·Menu 공통 동작", ["Menu"]),
        ("Menu", ["MenuButton"])
    ];

    internal static void Validate()
    {
        var registered = Groups.SelectMany(group => group.Components).ToArray();
        var exposed = Fluent.ComponentNames.ToArray();
        var missing = exposed.Except(registered, StringComparer.Ordinal).ToArray();
        var extra = registered.Except(exposed, StringComparer.Ordinal).ToArray();
        if (registered.Distinct(StringComparer.Ordinal).Count() != registered.Length || missing.Length != 0 || extra.Length != 0)
            throw new InvalidOperationException($"Gallery catalog mappings must match the exposed factories. Missing : {string.Join(", ", missing)} / Extra : {string.Join(", ", extra)}");
    }

    private IReadOnlyList<GalleryDestination> _destinations = [];
    private Action<string?>? _navigate;

    internal void Connect(IReadOnlyList<GalleryDestination> destinations, Action<string?> navigate)
    {
        _destinations = destinations;
        _navigate = navigate;
    }

    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        var rows = new List<UiWidget>
        {
            Fluent.Body1(GalleryText.Format("{0} examples · {1} planned",
                _destinations.Count(item => !item.Planned), _destinations.Count(item => item.Planned))),
            Fluent.Body1(GalleryText.Get("Select an example to open its page."))
        };
        foreach (var section in _destinations.Where(item => item.Page != "관찰 범위").GroupBy(item => item.Section))
        {
            if (section.Key.Length > 0)
                rows.Add(Fluent.Title3(section.Key));
            foreach (var category in section.GroupBy(item => item.Category))
            {
                rows.Add(Fluent.Subtitle1(category.Key));
                rows.Add(Dux.Wrap(new(Spacing : 8, RunSpacing : 6), category.Select(destination =>
                    (UiWidget)Fluent.Button(destination.Planned
                            ? GalleryText.Format("{0} · planned", destination.Label) : destination.Label,
                        () => _navigate!(destination.Key))).ToArray()));
            }
        }

        rows.Add(Fluent.Divider(GalleryText.Get("Fluent factories")));
        foreach (var group in Groups)
        {
            rows.Add(Fluent.Subtitle1(GalleryText.PageTitle(group.Page)));
            rows.Add(Fluent.Body1(string.Join(" / ", group.Components)));
        }
        return new UiColumn(rows.ToArray(), new(Spacing: 16, MainAxisSize: UiMainAxisSize.Min));
    });
}
