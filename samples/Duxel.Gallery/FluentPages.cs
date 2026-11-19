using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class RatingPage : UiWidget, IGallerySourcePage
{
    private readonly List<IGallerySample> _samples = [];
    public IReadOnlyList<IGallerySample> Samples => _samples;
    private GallerySample<T> Record<T>(GallerySample<T> sample) where T : UiWidget
    {
        _samples.Add(sample);
        return sample;
    }
    private readonly UiState<float> _rating = new(2.5f);
    private readonly UiState<int> _reviews = new(1234);
    private readonly UiState<string?> _color = new("neutral");
    private readonly UiState<string?> _shape = new("star");
    protected override IUiView Build(UiBuildContext context) => context.Child("rating-content", () =>
    {
        _samples.Clear();
        const string state = "var _rating = new UiState<float>(2.5f);\nvar _reviews = new UiState<int>(1234);";
        var wholeSample = GallerySample.Create(() => Fluent.Rating("한 점 단위 평가", _rating.Binding), setup: state);
        var halfSample = GallerySample.Create(() => Fluent.Rating("반 점 단위 평가", _rating.Binding,
            new UiRatingOptions { Step = .5f, ItemLabel = static value => $"5점 만점에 {value:0.#}점" }), setup: state);
        var wrappedSample = GallerySample.Create(() => Dux.Container(
            Fluent.Rating("줄바꿈 평가", _rating.Binding, new UiRatingOptions { Step = .5f }), new(Width: 56)), setup: state);
        var displaySample = GallerySample.Create(() => Fluent.RatingDisplay("등록된 평가", () => new(_rating.Value, _reviews.Value)), setup: state);
        var compactSample = GallerySample.Create(() => Fluent.RatingDisplay("요약 평가", () => new(_rating.Value, _reviews.Value),
            new UiRatingDisplayOptions { Compact = true }), setup: state);
        var whole = wholeSample.Preview;
        var half = halfSample.Preview;
        var wrapped = (UiFluentRating)wrappedSample.Preview.Child!;
        var display = displaySample.Preview;
        var compact = compactSample.Preview;
        wholeSample.BindSource(() => (_rating.Value, whole.Options),
            state => RatingSource("한 점 단위 평가", state.Value, state.Options));
        halfSample.BindSource(() => (_rating.Value, half.Options),
            state => RatingSource("반 점 단위 평가", state.Value, state.Options));
        wrappedSample.BindSource(() => (_rating.Value, wrapped.Options),
            state => RatingSource("줄바꿈 평가", state.Value, state.Options, wrapped: true));
        displaySample.BindSource(() => (_rating.Value, _reviews.Value, display.Options),
            state => DisplaySource("등록된 평가", state.Item1, state.Item2, state.Options));
        compactSample.BindSource(() => (_rating.Value, _reviews.Value, compact.Options),
            state => DisplaySource("요약 평가", state.Item1, state.Item2, state.Options));
        void ApplyStyle()
        {
            var color = _color.Value switch
            {
                "neutral" => UiRatingColor.Neutral,
                "brand" => UiRatingColor.Brand,
                "marigold" => UiRatingColor.Marigold,
                _ => throw new InvalidOperationException("Unknown rating color.")
            };
            var shape = _shape.Value switch
            {
                "star" => UiRatingShape.Star,
                "circle" => UiRatingShape.Circle,
                "square" => UiRatingShape.Square,
                _ => throw new InvalidOperationException("Unknown rating shape.")
            };
            whole.Options = whole.Options with
            {
                Color = color,
                Shape = shape
            };
            half.Options = half.Options with
            {
                Color = color,
                Shape = shape
            };
            wrapped.Options = wrapped.Options with
            {
                Color = color,
                Shape = shape
            };
            display.Options = display.Options with
            {
                Color = color,
                Shape = shape
            };
            compact.Options = compact.Options with
            {
                Color = color,
                Shape = shape
            };
        }
        return Dux.Column(new(Spacing: 16, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Body1("별 위에서 미리 보고 클릭해서 선택하세요. 방향키로 이동하며 Tab으로 빠져나옵니다."),
            Fluent.Caption1("한 점 단위 · 반 점 단위, 같은 값 공유"),
            Record(wholeSample), Record(halfSample),
            Fluent.Body1(() => $"평점 : {_rating.Value:0.#} / 5"),
            Fluent.Button("평점 지우기", () => _rating.Value = 0),
            Fluent.Divider("등록된 평가 표시"),
            Record(displaySample), Record(compactSample),
            Fluent.Button("리뷰 수 늘리기", () => _reviews.Value++),
            Fluent.Switch("오른쪽에서 왼쪽 순서", new UiBinding<bool>(() => whole.Options.RightToLeft, value =>
            {
                whole.Options = whole.Options with
                {
                    RightToLeft = value
                };
                half.Options = half.Options with
                {
                    RightToLeft = value
                };
                wrapped.Options = wrapped.Options with
                {
                    RightToLeft = value
                };
                display.Options = display.Options with
                {
                    RightToLeft = value
                };
                compact.Options = compact.Options with
                {
                    RightToLeft = value
                };
            })),
            Dux.Wrap(new(Spacing: 24),
                Fluent.RadioGroup("평가 색상", new UiBinding<string?>(() => _color.Value, value => { _color.Value = value; ApplyStyle(); }),
                    new UiRadioOption[] { new("neutral", "기본"), new("brand", "브랜드"), new("marigold", "메리골드") }, 200),
                Fluent.RadioGroup("평가 모양", new UiBinding<string?>(() => _shape.Value, value => { _shape.Value = value; ApplyStyle(); }),
                    new UiRadioOption[] { new("star", "별"), new("circle", "원"), new("square", "사각형") }, 200)),
            Fluent.Caption1("좁은 부모 영역에서는 별 크기를 유지하며 줄을 바꿉니다."),
            Record(wrappedSample));
    });
    private static string RatingSource(string label, float value, UiRatingOptions options, bool wrapped = false)
    {
        var control = FormattableString.Invariant($$"""
            Fluent.Rating("{{label}}", value.Binding, new UiRatingOptions
            {
                Maximum = {{options.Maximum}},
                Step = {{options.Step}}f,
                Size = UiRatingSize.{{options.Size}},
                Color = UiRatingColor.{{options.Color}},
                Shape = UiRatingShape.{{options.Shape}},
                RightToLeft = {{Boolean(options.RightToLeft)}}
            """);
        if (options.ItemLabel is not null)
            control += ",\n    ItemLabel = static value => $\"5점 만점에 {value:0.#}점\"";
        control += "\n})";
        if (wrapped) control = "Dux.Container(" + control + ", new(Width: 56))";
        return FormattableString.Invariant($"var value = new UiState<float>({value}f);\n\nUiWidget example = {control};");
    }
    private static string DisplaySource(string label, float value, int count, UiRatingDisplayOptions options)
        => FormattableString.Invariant($$"""
            var value = new UiState<float>({{value}}f);
            var count = new UiState<int>({{count}});

            UiWidget example = Fluent.RatingDisplay("{{label}}", () => new(value.Value, count.Value), new UiRatingDisplayOptions
            {
                Maximum = {{options.Maximum}},
                Size = UiRatingSize.{{options.Size}},
                Color = UiRatingColor.{{options.Color}},
                Shape = UiRatingShape.{{options.Shape}},
                Compact = {{Boolean(options.Compact)}},
                RightToLeft = {{Boolean(options.RightToLeft)}},
                ShowValue = {{Boolean(options.ShowValue)}},
                ShowCount = {{Boolean(options.ShowCount)}}
            });
            """);
    private static string Boolean(bool value) => value ? "true" : "false";
}

internal sealed class TypographyPage : UiWidget, IGallerySourcePage
{
    private readonly List<IGallerySample> _samples = [];
    public IReadOnlyList<IGallerySample> Samples => _samples;
    private GallerySample<T> Record<T>(GallerySample<T> sample) where T : UiWidget
    {
        _samples.Add(sample);
        return sample;
    }
    private readonly UiState<bool> _rightToLeft = new(false);
    private UiInlineText? _directionExample;
    private UiMessageBarBodyWidget? _ownedDirectionExample;

    internal bool DirectionReady(bool expected)
        => _rightToLeft.Value == expected && _directionExample?.RightToLeft == expected
            && _ownedDirectionExample?.RightToLeft == expected;

    internal void VerifyDirection(bool expected)
    {
        if (!DirectionReady(expected))
            throw new InvalidOperationException($"Paragraph direction is not settled : expected={expected},state={_rightToLeft.Value},widget={_directionExample?.RightToLeft}.");
    }

    protected override IUiView Build(UiBuildContext context)
    {
        var direction = context.Watch(_rightToLeft);
        var root = context.Child("root", CreateRoot);
        _directionExample!.RightToLeft = direction;
        _ownedDirectionExample!.RightToLeft = direction;
        return root;
    }

    private UiWidget CreateRoot()
    {
        _samples.Clear();
        var directionSample = GallerySample.Create(() => Dux.SizedBox(new UiInlineText([
            new("abc אב ", UiInlineTextWeight.Bold), new("גד regular 123 / بِت لا 123")]), width: 240));
        _directionExample = (UiInlineText)directionSample.Preview.Child!;
        var rotatedTitle = Fluent.MessageBarTitle("회전 제목");
        rotatedTitle.Margin = new(4);
        rotatedTitle.Transform = new(RotationDegrees: 15);
        var ownedSample = GallerySample.Create(() => Dux.SizedBox(Fluent.MessageBarBody("abc אב גד 123 / 한글과 بِت لا ", rotatedTitle,
            Fluent.Link("동작하는 링크", new Uri("https://github.com/dimohy/Duxel"), UiLinkNavigation.OpenExternal)), width: 320),
            setup: "var rotatedTitle = Fluent.MessageBarTitle(\"회전 제목\");\nrotatedTitle.Margin = new(4);\nrotatedTitle.Transform = new(RotationDegrees: 15);");
        _ownedDirectionExample = (UiMessageBarBodyWidget)ownedSample.Preview.Child!;
        return
        Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
            Record(GallerySample.Create(() => Fluent.Display("Display"))), Record(GallerySample.Create(() => Fluent.LargeTitle("LargeTitle"))),
            Record(GallerySample.Create(() => Fluent.Title1("Title1"))), Record(GallerySample.Create(() => Fluent.Title2("Title2"))), Record(GallerySample.Create(() => Fluent.Title3("Title3"))),
            Record(GallerySample.Create(() => Fluent.Subtitle1("Subtitle1"))), Record(GallerySample.Create(() => Fluent.Subtitle2("Subtitle2"))), Record(GallerySample.Create(() => Fluent.Subtitle2Stronger("Subtitle2Stronger"))),
            Record(GallerySample.Create(() => Fluent.Body2("Body2 : 본문과 English 123"))), Record(GallerySample.Create(() => Fluent.Body1("Body1 : 본문과 English 123"))),
            Record(GallerySample.Create(() => Fluent.Body1Strong("Body1Strong"))), Record(GallerySample.Create(() => Fluent.Body1Stronger("Body1Stronger"))),
            Record(GallerySample.Create(() => Fluent.Caption1("Caption1"))), Record(GallerySample.Create(() => Fluent.Caption1Strong("Caption1Strong"))), Record(GallerySample.Create(() => Fluent.Caption1Stronger("Caption1Stronger"))),
            Record(GallerySample.Create(() => Fluent.Caption2("Caption2"))), Record(GallerySample.Create(() => Fluent.Caption2Strong("Caption2Strong"))), Record(GallerySample.Create(() => Fluent.Text("Text : 기본 Body1 표현"))),
            Fluent.Subtitle1("혼합 굵기 문단"),
            Fluent.Caption1("한 문단 안에서 글자 굵기와 글꼴을 바꾸고 줄바꿈되는 모습을 확인해 보세요. Windows와 Linux 모두 운영체제에서 조판한 결과를 사용합니다."),
            Record(GallerySample.Create(() => Dux.SizedBox(new UiInlineText([
                new("한글 제목 ", UiInlineTextWeight.Semibold), new("본문과 English, AV office e\u0301를 함께 표시합니다.")]), width: 420))),
            Fluent.Switch("오른쪽에서 왼쪽 문단", _rightToLeft.Binding),
            Record(directionSample),
            Record(GallerySample.Create(() => Dux.SizedBox(new UiInlineText([
                new("A \u202Bאב 정 ", UiInlineTextWeight.Semibold), new("גד\u202C Z / 명시적 방향 문맥")]), width: 240))),
            Fluent.Subtitle1("위젯을 포함한 문단"),
            Fluent.Caption1("위 스위치를 바꾸면 문단 안의 위젯도 방향이 바뀝니다. 회전한 제목과 링크는 각각 별도의 위젯입니다."),
            Record(ownedSample),
            Record(GallerySample.Create(() => Dux.SizedBox(Fluent.MessageBarBody("גד regular 123 / 한글과 بِت لا",
                Fluent.MessageBarTitle("abc אב")), width: 240))));
    }
}
