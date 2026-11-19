using System.Runtime.CompilerServices;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ComponentExamplePage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private readonly UiScrollView _content;
    private readonly GallerySample<UiWidget> _sample;
    public string Title { get; }
    public string Description { get; }
    internal string Source => _sample.Source;
    public IReadOnlyList<IGallerySample> Samples { get; }
    internal ComponentExamplePage(string name, string description, GallerySample<UiWidget> sample,
        params GallerySample<UiWidget>[] additional)
    {
        Title = name;
        Description = description;
        _sample = sample;
        Samples = [sample, ..additional];
        var body = additional.Length == 0 ? (UiWidget)sample
            : Dux.Column(new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min), [sample, ..additional]);
        _content = Dux.ScrollView(body);
    }
    internal UiWidget Body => _content.Child;
    UiWidget IObserverNaturalPage.MeasureRoot => Body;
    public void Render(UiImmediateContext ui) => _content.Render(ui);
    public void Dispose() => _content.Dispose();
}

internal static class GalleryComponentExamples
{
    private static ComponentExamplePage Example(string name, string description, Func<UiWidget> create,
        [CallerArgumentExpression(nameof(create))] string source = "")
        => new(name, description, GallerySample.Create(create, expression : source, declarations : name == "Icon"
            ? "[UiFluentIcon(\"home\")] readonly partial struct HomeTabIcon;\n"
                + "[UiFluentIcon(\"home\", UiFluentIconStyle.Filled)] readonly partial struct HomeTabFilledIcon;"
            : string.Empty));

    private static string FactoryBody(string source)
    {
        var arrow = source.IndexOf("=>", StringComparison.Ordinal);
        if (arrow < 0 || !source.AsSpan(arrow + 2).TrimStart().StartsWith("{"))
            throw new InvalidOperationException("A stateful Gallery factory must have a captured block lambda.");
        return "() => " + source[(arrow + 2)..].TrimStart();
    }

    private static ComponentExamplePage Stateful<T>(string name, string description, UiState<T> value, Func<UiState<T>, UiWidget> create,
        Func<T, string> setup, [CallerArgumentExpression(nameof(create))] string source = "")
        => new(name, description, StatefulSample(value, create, setup, source));

    private static GallerySample<UiWidget> StatefulSample<T>(UiState<T> value, Func<UiState<T>, UiWidget> create,
        Func<T, string> setup, [CallerArgumentExpression(nameof(create))] string source = "")
    {
        var sample = GallerySample.Create<UiWidget>(() => create(value), expression : FactoryBody(source));
        var factory = sample.Source;
        return sample.BindSource(() => value.Value, current => setup(current) + "\n\n" + factory);
    }

    private static ComponentExamplePage Stateful<TFirst, TSecond>(string name, string description, UiState<TFirst> first, UiState<TSecond> second,
        Func<UiState<TFirst>, UiState<TSecond>, UiWidget> create, Func<TFirst, TSecond, string> setup,
        [CallerArgumentExpression(nameof(create))] string source = "")
        => new(name, description, StatefulSample(first, second, create, setup, source));

    private static GallerySample<UiWidget> StatefulSample<TFirst, TSecond>(UiState<TFirst> first, UiState<TSecond> second,
        Func<UiState<TFirst>, UiState<TSecond>, UiWidget> create, Func<TFirst, TSecond, string> setup,
        [CallerArgumentExpression(nameof(create))] string source = "")
    {
        var sample = GallerySample.Create<UiWidget>(() => create(first, second), expression : FactoryBody(source));
        var factory = sample.Source;
        return sample.BindSource(() => (first.Value, second.Value), current => setup(current.Item1, current.Item2) + "\n\n" + factory);
    }

    private static string TextState(string name, string? value, bool nullable = false)
        => "var " + name + " = new UiState<string" + (nullable ? "?" : "") + ">(" + GallerySample.String(value) + ");";

    private static string StringArray(string[] values) => "[" + string.Join(", ", values.Select(GallerySample.String)) + "]";

    private static ComponentExamplePage RadioGroupExample()
    {
        var choices = StatefulSample(new UiState<string?>("balanced"), value =>
        {
            return Fluent.RadioGroup("처리 방식", value.Binding,
                new UiRadioOption[] { new("efficient", "속도 우선"), new("balanced", "균형"), new("quality", "품질 우선") });
        }, current => TextState("value", current, nullable : true));

        var allocation = StatefulSample(new UiState<string?>("balanced"), new UiState<float>(180), (selection, width) =>
        {
            var group = Fluent.RadioGroup("처리 우선순위", selection.Binding, new UiRadioOption[]
            {
                new("efficient", "응답 시간을 줄이는 데 집중합니다."),
                new("balanced", "속도와 결과의 품질을 고르게 유지합니다."),
                new("quality", "시간이 더 걸려도 결과의 품질을 높입니다.")
            });
            // A finite maximum lets the sample fit when the Gallery is narrower than the requested width.
            var parent = Dux.ConstrainedBox(group, new(0, width.Value, 0, float.PositiveInfinity));
            return Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle2("부모 폭에 따라 줄바꿈"),
                Fluent.Button("부모 폭 전환", () =>
                {
                    width.Value = width.Value == 180 ? 360 : 180;
                    parent.Options = parent.Options with { MaxWidth = width.Value };
                }),
                Fluent.Caption1(() => $"부모 최대 폭 {width.Value:F0}px"),
                parent);
        }, (selection, width) => TextState("selection", selection, nullable : true)
            + "\nvar width = new UiState<float>(" + GallerySample.Number(width) + ");");

        return new("RadioGroup", "방향키로 선택을 바꾸고, 부모 폭에 따라 긴 라벨이 어떻게 줄바꿈되는지 살펴보세요.",
            choices, allocation);
    }

    internal static IObserverPage[] Create() =>
    [
        Stateful("TagPicker", "이름을 검색해 받는 사람을 추가하고, 선택한 태그를 지워 보세요.", new UiState<string>(string.Empty), new UiState<string[]>(["dimoi"]), (query, selected) =>
        {
            UiSelectOption[] people = [new("dimoi", "디모이"), new("kim", "김민수"), new("park", "박지연"), new("guest", "손님", false)];
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.TagPicker("받는 사람", query.Binding, selected.Binding, people,
                    new(Field: new(Width: 520, Placeholder: "이름을 입력하세요"), EmptyText: "일치하는 사람이 없습니다", DismissLabel: "제거")),
                Fluent.Caption1("이름을 입력해 검색하고 Enter로 추가하세요. 커서가 입력창 맨 앞에 있을 때 ←나 Backspace를 누르면 마지막 태그로 이동합니다."),
                Fluent.Body1(() => "선택 : " + string.Join(", ", selected.Value)));
        }, (query, selected) => TextState("query", query) + "\nvar selected = new UiState<string[]>(" + StringArray(selected) + ");"),
        Stateful("Button", "버튼을 눌러 클릭 횟수를 늘려 보세요.", new UiState<int>(0), clicks =>
        {
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Button("카운터 증가", () => clicks.Value++),
                Fluent.Body1(() => "클릭 횟수 : " + clicks.Value));
        }, current => "var clicks = new UiState<int>(" + current.ToString(System.Globalization.CultureInfo.InvariantCulture) + ");"),
        Stateful("Checkbox", "일부만 선택된 상태에서 시작하는 체크박스를 눌러 보세요.", new UiState<bool?>(null), value =>
        {
            return Fluent.Checkbox("혼합 상태에서 시작", value.Binding);
        }, current => "var value = new UiState<bool?>(" + (current is null ? "null" : GallerySample.Boolean(current.Value)) + ");"),
        Stateful("Switch", "스위치로 기능을 켜거나 끄고, 아래 코드에 바뀐 값을 확인해 보세요.", new UiState<bool>(false), value =>
        {
            return Fluent.Switch("기능 사용", value.Binding);
        }, current => "var value = new UiState<bool>(" + GallerySample.Boolean(current) + ");"),
        RadioGroupExample(),
        Stateful("Slider", "손잡이를 끌거나 방향키를 눌러 0부터 1까지 값을 조절해 보세요.", new UiState<float>(.35f), value =>
        {
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Slider("값 조절", value.Binding, 0, 1, .01f),
                Fluent.Body1(() => "값 : " + value.Value.ToString("F2")));
        }, current => "var value = new UiState<float>(" + GallerySample.Number(current) + ");"),
        Example("ProgressBar", "진행률을 막대로 표시합니다. 예제는 작업이 35% 진행된 상태입니다.", () =>
        {
            var progress = Fluent.ProgressBar(() => new(.35f, 1));
            progress.Size = new(280, 4);
            return progress;
        }),
        Example("Badge", "상태를 짧은 글자로 표시하는 배지와 개수를 보여 주는 배지를 비교해 보세요.", () =>
        {
            return Dux.Wrap(new(Spacing : 12), Fluent.Badge("Ready"), Fluent.CounterBadge(() => new(5)));
        }),
        Example("Divider", "구분선과 가운데 라벨로 위아래 내용을 나눕니다.", () =>
        {
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1("위쪽 내용"), Fluent.Divider("구분"), Fluent.Body1("아래쪽 내용"));
        }),
        Stateful("Link", "링크를 눌러 선택한 목적지가 아래에 표시되는지 확인해 보세요.", new UiState<string>("아직 선택하지 않았습니다."), destination =>
        {
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Link("목적지 선택", new Uri("/gallery", UriKind.Relative), uri => destination.Value = uri.ToString()),
                Fluent.Body1(() => "선택한 목적지 : " + destination.Value));
        }, current => TextState("destination", current)),
        Stateful("Spinner", "진행률을 알 수 없는 작업을 표시합니다. 버튼으로 회전을 시작하거나 멈춰 보세요.", new UiState<bool>(true), paused =>
        {
            var spinner = Fluent.Spinner("일시 정지", new(ReducedMotion : true) { Paused = paused.Value });
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Button("재생 전환", () =>
                {
                    paused.Value = !paused.Value;
                    spinner.Options = spinner.Options with { Paused = paused.Value };
                }), spinner);
        }, current => "var paused = new UiState<bool>(" + GallerySample.Boolean(current) + ");"),
        Stateful("Input", "한 줄에 한글과 영문을 입력하고 일부를 선택해 수정해 보세요.", new UiState<string>("한글과 English"), text =>
        {
            return Fluent.Input("한 줄 입력", text.Binding, 4096, 280);
        }, current => TextState("text", current)),
        Stateful("Textarea", "여러 줄에 걸쳐 글을 입력하거나 줄 사이를 이동해 수정해 보세요.", new UiState<string>("첫 줄\n둘째 줄"), text =>
        {
            return Fluent.Textarea("여러 줄 입력", text.Binding, 16384, 280, 180);
        }, current => TextState("text", current)),
        Stateful("Field", "이름 입력란에 필수 표시와 입력 안내를 함께 배치합니다.", new UiState<string>(string.Empty), text =>
        {
            var field = Fluent.Field(Fluent.Input("이름", text.Binding, 256, 280));
            field.Options = new() { Label = "이름", Required = true, Hint = "두 글자 이상 입력하세요." };
            return field;
        }, current => TextState("text", current)),
        Stateful("Label", "라벨을 누르면 연결된 입력란으로 포커스가 옮겨갑니다.", new UiState<string>(string.Empty), text =>
        {
            var input = Fluent.Input("라벨 대상", text.Binding, 256, 280);
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), Fluent.Label("눌러서 입력창으로 이동", input), input);
        }, current => TextState("text", current)),
        Stateful("Dropdown", "목록을 열고 작업 위치를 하나 선택해 보세요.", new UiState<string?>(null), selected =>
        {
            return Fluent.Dropdown("작업 위치", selected.Binding,
                [new("kr", "한국"), new("us", "United States"), new("jp", "日本")],
                new(Placeholder : "위치를 선택하세요", ExpandLabel : "목록 열기"));
        }, current => TextState("selected", current, nullable : true)),
        Stateful("Combobox", "도시 이름을 입력해 목록을 좁히거나, 목록에 없는 이름을 직접 입력해 보세요.", new UiState<string>(string.Empty), new UiState<string?>(null), (query, selected) =>
        {
            return Fluent.Combobox("도시 검색", query.Binding, selected.Binding,
                [new("seoul", "서울"), new("busan", "부산"), new("london", "London")],
                new(Field : new(Placeholder : "도시 이름을 입력하세요", ExpandLabel : "도시 목록 열기"), Freeform : true));
        }, (query, selected) => TextState("query", query) + "\n" + TextState("selected", selected, nullable : true)),
        Example("Tag", "세 도시 이름을 작은 태그로 나란히 표시합니다.", () =>
        {
            return Dux.Wrap(new(Spacing : 8), Fluent.Tag("서울"), Fluent.Tag("부산"), Fluent.Tag("제주"));
        }),
        Stateful("TabList", "탭을 클릭하거나 키보드로 이동해 선택 표시가 어떻게 바뀌는지 살펴보세요.", new UiState<string?>("first"), selected =>
        {
            return Fluent.TabList("예제 탭", selected.Binding,
                new UiTabOption[] { new("first", "첫 번째"), new("second", "두 번째"), new("third", "세 번째") });
        }, current => TextState("selected", current, nullable : true)),
        Stateful("Menu", "메뉴를 열어 새 문서나 저장 명령을 실행해 보세요.", new UiState<string>("선택 없음"), chosen =>
        {
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Menu("메뉴 열기", [new("new", "새 문서"), new("save", "저장")], value => chosen.Value = value),
                Fluent.Body1(() => "실행 : " + chosen.Value));
        }, current => TextState("chosen", current)),
        Example("Icon", "홈 아이콘에 크기와 색상을 지정한 예제입니다.", () =>
        {
            return Fluent.Icon(UiMenuIcon.Create<HomeTabIcon, HomeTabFilledIcon>(), new(48, 48), new UiColor(0, 120, 212));
        }),
        Example("Image", "네 픽셀의 이미지 데이터를 위젯에 넣고 160 × 160 크기로 표시합니다.", () =>
        {
            var pixels = new byte[] { 0, 120, 212, 255, 100, 180, 240, 255, 100, 180, 240, 255, 0, 120, 212, 255 };
            var image = Fluent.Image(new UiImageSource(new UiImageData(2, 2, pixels)));
            image.Size = new(160, 160);
            return image;
        })
    ];
}
