using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class CatalogPage : UiWidget
{
    internal static readonly (string Page, string[] Components)[] Groups =
    [
        ("왼쪽 탐색 메뉴·Fluent 표현 비교·컨테이너 넘침", ["TabList"]),
        ("Fluent", ["Button", "ToggleButton", "Checkbox", "Switch", "RadioGroup", "Slider", "ProgressBar", "Badge", "CounterBadge", "Divider", "Link", "Spinner"]),
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
        ("ProgressIndicator", ["ProgressIndicator"]),
        ("ActivityIndicator", ["ActivityIndicator"]),
        ("HeadsUpDisplay", ["HeadsUpDisplay"]),
        ("카드", ["Card", "CardFooter", "CardHeader", "CardPreview"]),
        ("경로", ["Breadcrumb"]),
        ("탐색 메뉴", ["Nav"]),
        ("보조 패널", ["Drawer"]),
        ("목록", ["List"]),
        ("SVG", ["Icon", "Image"]),
        ("컨테이너·Menu 공통 동작", ["Menu"])
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

    protected override IUiView Build(UiBuildContext context) => context.Child("root", () =>
    {
        var rows = new List<UiWidget>
        {
            Fluent.Body1($"사용할 수 있는 팩터리 : {Fluent.ComponentNames.Length} / Fluent 구성요소 : {Fluent.RequiredComponentNames.Length} / 아직 연결하지 않은 구성요소 : {Fluent.MissingComponentNames.Length}"),
            Fluent.Body1("아래에서 구성요소별 예제가 어느 메뉴에 있는지 확인하세요. 아직 제공하지 않는 구성요소는 왼쪽 메뉴에 미구현 배지로 표시합니다. 새 팩터리를 추가하고 예제 메뉴에 연결하지 않으면 시작할 때 오류를 알립니다.")
        };
        foreach (var group in Groups)
        {
            rows.Add(Fluent.Subtitle1(group.Page));
            rows.Add(Fluent.Body1(string.Join(" / ", group.Components)));
        }
        rows.Add(Fluent.Divider("남은 범위"));
        rows.Add(Fluent.Body1("컨테이너 메뉴에서 단일 자식 배치와 Flex·Stack·Wrap·Grid를 비교하세요. 가상 목록에서는 10억 개 행의 높이를 바꾸거나 값을 편집할 수 있으며, 행이 화면 밖으로 나가도 편집한 값은 유지됩니다. 렌더링 메뉴에서는 바뀌지 않는 레이어의 재사용, 기본 도형 그리기, 독립적인 3D 투영을 확인할 수 있습니다."));
        rows.Add(Fluent.Subtitle1("DataGrid"));
        rows.Add(Fluent.Body1("Fluent.DataGrid · 고정 열 제목, 정렬, 행 선택, 키보드 탐색"));
        rows.Add(Fluent.Body1("운영체제별 입력과 화면 표시는 계속 확인하고 있습니다. 예제가 있더라도 Fluent 디자인에 맞는지, 각 상태가 올바르게 표시되는지, 접근성에 문제가 없는지는 추가로 검증해야 합니다."));
        rows.Add(Fluent.Caption1("실행 : 저장소 루트에서 ./run-gallery.ps1 / 안내 : samples/Duxel.Gallery/README.ko.md"));
        return new UiColumn(rows.ToArray(), new(Spacing: 16, MainAxisSize: UiMainAxisSize.Min));
    });
}
