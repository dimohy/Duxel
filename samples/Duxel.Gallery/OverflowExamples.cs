using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class OverflowExamples : UiWidget, IGallerySourcePage
{
    private readonly UiWidget _root;
    private readonly List<IGallerySample> _samples = [];
    public IReadOnlyList<IGallerySample> Samples => _samples;
    internal OverflowExamples()
    {
        var counts = Enumerable.Range(0, 4).Select(_ => new UiState<int>(0)).ToArray();
        var buttonsDefinition = GallerySample.Define("buttons", () => Enumerable.Range(1, 4).Select(index =>
        {
            var count = counts[index - 1];
            var button = Fluent.Button($"넘침 항목 {index} : {count.Value}");
            button.Size = new(120, 32);
            button.Activated = () => button.Label = $"넘침 항목 {index} : {++count.Value}";
            return (UiWidget)button;
        }).ToArray());
        var buttons = buttonsDefinition.Value;
        var overflowOptions = new UiOverflowOptions(Spacing : 8);
        var overflowDefinition = GallerySample.Define("overflow", () => Dux.Overflow(buttons, Fluent.Badge("숨김"), overflowOptions));
        var overflow = overflowDefinition.Value;
        var priorityDefinition = GallerySample.Define<Action<bool>>("applyPriorities", () => (bool last) =>
            overflow.SetPriorities(last ? [0, 0, 0, 2] : [2, 0, 0, 0]));
        var applyPriorities = priorityDefinition.Value;
        var prioritizeLast = false;
        applyPriorities(prioritizeLast);
        var widthValue = 200f;
        var overflowSample = GallerySample.Create(() => Dux.SizedBox(overflow, width : widthValue, height : 48));
        var box = overflowSample.Preview;
        var overflowFactory = overflowSample.Source;
        overflowSample.BindSource(() => (overflow.Options, box.Options.Width, prioritizeLast,
            Counts : (counts[0].Value, counts[1].Value, counts[2].Value, counts[3].Value)), state =>
            $"UiState<int>[] counts = [new({state.Counts.Item1}), new({state.Counts.Item2}), new({state.Counts.Item3}), new({state.Counts.Item4})];\n"
            + $"var overflowOptions = new UiOverflowOptions(Spacing : {GallerySample.Number(state.Options.Spacing)}, MinimumVisible : {state.Options.MinimumVisible},\n"
            + $"    CollapseFrom : UiOverflowCollapseDirection.{state.Options.CollapseFrom}, RightToLeft : {GallerySample.Boolean(state.Options.RightToLeft)});\n"
            + $"float? widthValue = {GallerySample.Number(state.Width)};\nvar prioritizeLast = {GallerySample.Boolean(state.prioritizeLast)};\n\n"
            + buttonsDefinition.Source + "\n\n" + overflowDefinition.Source + "\n\n" + priorityDefinition.Source
            + "\napplyPriorities(prioritizeLast);\n\n" + overflowFactory);
        _samples.Add(overflowSample);
        var width = Fluent.Button("넘침 폭 : 200");
        width.Activated = () =>
        {
            var next = box.Options.Width == 200 ? 560 : 200;
            box.Options = box.Options with { Width = next };
            width.Label = $"넘침 폭 : {next}";
        };
        var priority = Fluent.Button("우선 항목 : 1");
        priority.Activated = () =>
        {
            prioritizeLast = !prioritizeLast;
            applyPriorities(prioritizeLast);
            priority.Label = prioritizeLast ? "우선 항목 : 4" : "우선 항목 : 1";
        };
        var direction = Fluent.Button("넘침 방향 : LTR");
        direction.Activated = () =>
        {
            overflow.Options = overflow.Options with { RightToLeft = !overflow.Options.RightToLeft };
            direction.Label = overflow.Options.RightToLeft ? "넘침 방향 : RTL" : "넘침 방향 : LTR";
        };
        var lastAction = new UiState<string>("선택 : 없음");
        void RecordMenu<T, TState>(GallerySample<T> sample, string optionName, Func<UiMenuOptions> readOptions,
            Func<TState> readState, Func<TState, string> setup) where T : UiWidget
        {
            var factory = sample.Source;
            sample.BindSource(() => (Options : readOptions(), Action : lastAction.Value, State : readState()), state =>
                $"var lastAction = new UiState<string>({GallerySample.String(state.Action)});\n"
                + $"var {optionName} = {MenuOptions(state.Options)};\n" + setup(state.State) + "\n" + factory);
            _samples.Add(sample);
        }
        var commonOptions = new UiMenuOptions();
        var common = GallerySample.Create(() => Dux.Menu("공통 메뉴 열기",
            [new("first", "첫 번째 동작"), new("disabled", "사용할 수 없는 동작", false), new("last", "마지막 동작")],
            value => lastAction.Value = "선택 : " + value, commonOptions));
        RecordMenu(common, nameof(commonOptions), () => common.Preview.Options, () => 0, _ => string.Empty);
        var fluentOptions = UiFluentMenuPresentation.DefaultOptions;
        var fluent = GallerySample.Create(() => Fluent.Menu("Fluent 메뉴 열기",
            [new("first", "Fluent 첫 번째"), new("disabled", "Fluent 사용 불가", false), new("last", "Fluent 마지막")],
            value => lastAction.Value = "Fluent 선택 : " + value, fluentOptions));
        RecordMenu(fluent, nameof(fluentOptions), () => fluent.Preview.Options, () => 0, _ => string.Empty);
        var longOptions = new UiMenuOptions(138, 32, 5, ItemSpacing : 2, MaximumWidth : 300) { MaximumHeight = 210 };
        var longMenu = GallerySample.Create(() => Fluent.Menu("긴 메뉴·문자 탐색",
            [new("apple", "Apple"), new("apricot", "Apricot"), new("banana", "Banana"),
                .. Enumerable.Range(1, 24).Select(i => new UiMenuOption("item-" + i, "항목 " + i)), new("last", "Zulu 마지막 동작")],
            value => lastAction.Value = "긴 메뉴 선택 : " + value, longOptions));
        RecordMenu(longMenu, nameof(longOptions), () => longMenu.Preview.Options, () => 0, _ => string.Empty);
        var showDetails = new UiState<bool>(true);
        var showGrid = new UiState<bool>(false);
        var checkedOptions = UiFluentMenuPresentation.DefaultOptions;
        var checkedMenu = GallerySample.Create(() => Fluent.Menu("체크 메뉴 열기",
            [UiMenuOption.Checkbox("details", "상세 정보 표시", showDetails),
                UiMenuOption.Checkbox("grid", "격자 표시", showGrid),
                UiMenuOption.Checkbox("disabled", "변경할 수 없는 옵션", showGrid, false), new("done", "설정 마침")],
            value => lastAction.Value = "체크 메뉴 선택 : " + value, checkedOptions));
        RecordMenu(checkedMenu, nameof(checkedOptions), () => checkedMenu.Preview.Options,
            () => (Details : showDetails.Value, Grid : showGrid.Value), state =>
            $"var showDetails = new UiState<bool>({GallerySample.Boolean(state.Details)});\n"
            + $"var showGrid = new UiState<bool>({GallerySample.Boolean(state.Grid)});\n");
        var sortState = new UiState<string?>("name");
        var viewState = new UiState<string?>("compact");
        var sortDefinition = GallerySample.Define("sortGroup", () => new UiMenuRadioGroup("정렬 기준", sortState));
        var viewDefinition = GallerySample.Define("viewGroup", () => new UiMenuRadioGroup("보기 방식", viewState));
        var sortGroup = sortDefinition.Value;
        var viewGroup = viewDefinition.Value;
        var radioOptions = UiFluentMenuPresentation.DefaultOptions;
        var radioMenu = GallerySample.Create(() => Fluent.Menu("라디오 메뉴 열기",
            [UiMenuOption.Radio("name", "이름순 정렬", sortGroup), UiMenuOption.Radio("date", "날짜순 정렬", sortGroup),
                UiMenuOption.Radio("disabled-sort", "사용할 수 없는 정렬", sortGroup, false),
                UiMenuOption.Radio("compact", "간결하게 보기", viewGroup), UiMenuOption.Radio("wide", "넓게 보기", viewGroup)],
            value => lastAction.Value = "라디오 메뉴 선택 : " + value, radioOptions));
        RecordMenu(radioMenu, nameof(radioOptions), () => radioMenu.Preview.Options,
            () => (Sort : sortState.Value, View : viewState.Value), state =>
            $"var sortState = new UiState<string?>({GallerySample.String(state.Sort)});\n"
            + $"var viewState = new UiState<string?>({GallerySample.String(state.View)});\n"
            + sortDefinition.Source + "\n" + viewDefinition.Source + "\n");
        var iconDefinition = GallerySample.Define("menuIcon", () => UiMenuIcon.Create<UiFluentAddIcon, UiFluentAddFilledIcon>());
        var menuIcon = iconDefinition.Value;
        var iconOptions = UiFluentMenuPresentation.DefaultOptions;
        var contentMenu = GallerySample.Create(() => Fluent.Menu("아이콘 메뉴 열기",
            [new("new", "새 항목 선택") { Icon = menuIcon, SecondaryContent = "템플릿" },
                new("format", "형식 선택") { SecondaryContent = "PNG" },
                UiMenuOption.Checkbox("preview", "미리보기 표시", showDetails) with { Icon = menuIcon, SecondaryContent = "선택" },
                new("locked", "사용할 수 없는 항목", false) { Icon = menuIcon, SecondaryContent = "잠김" }],
            value => lastAction.Value = "아이콘 메뉴 선택 : " + value, iconOptions));
        RecordMenu(contentMenu, nameof(iconOptions), () => contentMenu.Preview.Options,
            () => showDetails.Value, state =>
            $"var showDetails = new UiState<bool>({GallerySample.Boolean(state)});\n" + iconDefinition.Source + "\n");
        var menuDirection = Fluent.Button("메뉴 방향 : LTR");
        menuDirection.Activated = () =>
        {
            contentMenu.Preview.Options = contentMenu.Preview.Options with { RightToLeft = !contentMenu.Preview.Options.RightToLeft };
            menuDirection.Label = contentMenu.Preview.Options.RightToLeft ? "메뉴 방향 : RTL" : "메뉴 방향 : LTR";
        };
        _root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Divider("Overflow 공통 배치"),
            Fluent.Body1("폭을 줄이면 우선순위가 낮은 항목을 숨깁니다. 다시 펼치면 같은 버튼과 카운터를 복원합니다."),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8), width, priority, direction), overflowSample,
            Fluent.Body1(() => overflow.Result is { } result
                ? $"표시 : {result.VisibleCount} / 숨김 : {result.HiddenCount} / 배치 가능 : {result.Fits}" : "배치 : 측정 전"),
            Fluent.Caption1("배지는 공간이 부족해 숨겨진 항목의 수를 나타냅니다. 탭을 숨겼다가 선택할 때 본문도 바뀌는 모습은 위 예제에서 확인하세요."),
            Fluent.Divider("Menu 공통 동작"), Fluent.Body1(() => lastAction.Value), common, fluent, longMenu, checkedMenu,
            Fluent.Body1(() => $"상세 정보 : {showDetails.Value} / 격자 : {showGrid.Value}"),
            radioMenu, Fluent.Body1(() => $"정렬 : {(sortState.Value == "name" ? "이름순" : "날짜순")} / 보기 : {(viewState.Value == "compact" ? "간결하게" : "넓게")}"),
            menuDirection, contentMenu,
            Fluent.Caption1("방향키·Enter·Space로 선택하고 Escape·Tab·바깥 클릭으로 닫습니다. 디자인의 세부 표현과 메뉴 아이콘, 이벤트 동작은 계속 검증하고 있습니다."));
        OwnChild("root", _root);
    }
    private static string MenuOptions(UiMenuOptions options) =>
        $"new UiMenuOptions(MinimumWidth : {GallerySample.Number(options.MinimumWidth)}, ItemHeight : {GallerySample.Number(options.ItemHeight)},\n"
        + $"    Padding : {GallerySample.Number(options.Padding)}, RightToLeft : {GallerySample.Boolean(options.RightToLeft)}, ItemSpacing : {GallerySample.Number(options.ItemSpacing)},\n"
        + $"    MaximumWidth : {GallerySample.Number(options.MaximumWidth)}) {{ MaximumHeight = {GallerySample.Number(options.MaximumHeight)} }}";
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}
