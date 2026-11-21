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
        var wholeSample = GallerySample.Create(() => Fluent.Rating(GalleryText.Get("Whole-star rating"), _rating.Binding), setup: state);
        var halfSample = GallerySample.Create(() => Fluent.Rating(GalleryText.Get("Half-star rating"), _rating.Binding,
            new UiRatingOptions { Step = .5f, ItemLabel = static value => string.Format(GalleryText.Get("{0:0.#} out of 5"), value) }), setup: state);
        var wrappedSample = GallerySample.Create(() => Dux.Container(
            Fluent.Rating(GalleryText.Get("Wrapped rating"), _rating.Binding, new UiRatingOptions { Step = .5f }), new(Width: 56)), setup: state);
        var displaySample = GallerySample.Create(() => Fluent.RatingDisplay(GalleryText.Get("Submitted rating"), () => new(_rating.Value, _reviews.Value)), setup: state);
        var compactSample = GallerySample.Create(() => Fluent.RatingDisplay(GalleryText.Get("Compact rating"), () => new(_rating.Value, _reviews.Value),
            new UiRatingDisplayOptions { Compact = true }), setup: state);
        var whole = wholeSample.Preview;
        var half = halfSample.Preview;
        var wrapped = (UiFluentRating)wrappedSample.Preview.Child!;
        var display = displaySample.Preview;
        var compact = compactSample.Preview;
        wholeSample.BindSource(() => (_rating.Value, whole.Options),
            state => RatingSource(GalleryText.Get("Whole-star rating"), state.Value, state.Options));
        halfSample.BindSource(() => (_rating.Value, half.Options),
            state => RatingSource(GalleryText.Get("Half-star rating"), state.Value, state.Options));
        wrappedSample.BindSource(() => (_rating.Value, wrapped.Options),
            state => RatingSource(GalleryText.Get("Wrapped rating"), state.Value, state.Options, wrapped: true));
        displaySample.BindSource(() => (_rating.Value, _reviews.Value, display.Options),
            state => DisplaySource(GalleryText.Get("Submitted rating"), state.Item1, state.Item2, state.Options));
        compactSample.BindSource(() => (_rating.Value, _reviews.Value, compact.Options),
            state => DisplaySource(GalleryText.Get("Compact rating"), state.Item1, state.Item2, state.Options));
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
            Fluent.Body1(GalleryText.Get("Hover over the stars to preview a rating, then click to select. Use the arrow keys to move and Tab to leave.")),
            Fluent.Caption1(GalleryText.Get("Whole-star and half-star controls share the same value.")),
            Record(wholeSample), Record(halfSample),
            Fluent.Body1(() => GalleryText.Format("Rating: {0:0.#} / 5", _rating.Value)),
            Fluent.Button(GalleryText.Get("Clear rating"), () => _rating.Value = 0),
            Fluent.Divider(GalleryText.Get("Submitted rating display")),
            Record(displaySample), Record(compactSample),
            Fluent.Button(GalleryText.Get("Increase review count"), () => _reviews.Value++),
            Fluent.Switch(GalleryText.Get("Right-to-left order"), new UiBinding<bool>(() => whole.Options.RightToLeft, value =>
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
                Fluent.RadioGroup(GalleryText.Get("Rating color"), new UiBinding<string?>(() => _color.Value, value => { _color.Value = value; ApplyStyle(); }),
                    new UiRadioOption[] { new("neutral", GalleryText.Get("Neutral")), new("brand", GalleryText.Get("Brand")), new("marigold", GalleryText.Get("Marigold")) }, 200),
                Fluent.RadioGroup(GalleryText.Get("Rating shape"), new UiBinding<string?>(() => _shape.Value, value => { _shape.Value = value; ApplyStyle(); }),
                    new UiRadioOption[] { new("star", GalleryText.Get("Star")), new("circle", GalleryText.Get("Circle")), new("square", GalleryText.Get("Square")) }, 200)),
            Fluent.Caption1(GalleryText.Get("In a narrow container, the stars wrap without changing size.")),
            Record(wrappedSample));
    });
    private static string RatingSource(string label, float value, UiRatingOptions options, bool wrapped = false)
    {
        var control = FormattableString.Invariant($$"""
            Fluent.Rating({{GallerySample.String(label)}}, value.Binding, new UiRatingOptions
            {
                Maximum = {{options.Maximum}},
                Step = {{options.Step}}f,
                Size = UiRatingSize.{{options.Size}},
                Color = UiRatingColor.{{options.Color}},
                Shape = UiRatingShape.{{options.Shape}},
                RightToLeft = {{Boolean(options.RightToLeft)}}
            """);
        if (options.ItemLabel is not null)
            control += ",\n    ItemLabel = static value => string.Format(" + GallerySample.String(GalleryText.Get("{0:0.#} out of 5")) + ", value)";
        control += "\n})";
        if (wrapped) control = "Dux.Container(" + control + ", new(Width: 56))";
        return FormattableString.Invariant($"var value = new UiState<float>({value}f);\n\nUiWidget example = {control};");
    }
    private static string DisplaySource(string label, float value, int count, UiRatingDisplayOptions options)
        => FormattableString.Invariant($$"""
            var value = new UiState<float>({{value}}f);
            var count = new UiState<int>({{count}});

            UiWidget example = Fluent.RatingDisplay({{GallerySample.String(label)}}, () => new(value.Value, count.Value), new UiRatingDisplayOptions
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
        var directionFactory = directionSample.Source;
        directionSample.BindSource(() => _rightToLeft.Value, rtl => directionFactory
            + $"\n((UiInlineText)((UiSizedBox)example).Child!).RightToLeft = {GallerySample.Boolean(rtl)};");
        var rotatedTitle = Fluent.MessageBarTitle(GalleryText.Get("Rotated title"));
        rotatedTitle.Margin = new(4);
        rotatedTitle.Transform = new(RotationDegrees: 15);
        var ownedSample = GallerySample.Create(() => Dux.SizedBox(Fluent.MessageBarBody(GalleryText.Get("abc אב גד 123 / text and بِت لا"), rotatedTitle,
            Fluent.Link(GalleryText.Get("Live link"), new Uri("https://github.com/dimohy/Duxel"), UiLinkNavigation.OpenExternal)), width: 320),
            setup: "var rotatedTitle = Fluent.MessageBarTitle(" + GallerySample.String(GalleryText.Get("Rotated title")) + ");\nrotatedTitle.Margin = new(4);\nrotatedTitle.Transform = new(RotationDegrees: 15);");
        _ownedDirectionExample = (UiMessageBarBodyWidget)ownedSample.Preview.Child!;
        var ownedFactory = ownedSample.Source;
        ownedSample.BindSource(() => _rightToLeft.Value, rtl => ownedFactory
            + $"\n((UiMessageBarBodyWidget)((UiSizedBox)example).Child!).RightToLeft = {GallerySample.Boolean(rtl)};");
        return
        Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
            Record(GallerySample.Create(() => Fluent.Display("Display"))), Record(GallerySample.Create(() => Fluent.LargeTitle("LargeTitle"))),
            Record(GallerySample.Create(() => Fluent.Title1("Title1"))), Record(GallerySample.Create(() => Fluent.Title2("Title2"))), Record(GallerySample.Create(() => Fluent.Title3("Title3"))),
            Record(GallerySample.Create(() => Fluent.Subtitle1("Subtitle1"))), Record(GallerySample.Create(() => Fluent.Subtitle2("Subtitle2"))), Record(GallerySample.Create(() => Fluent.Subtitle2Stronger("Subtitle2Stronger"))),
            Record(GallerySample.Create(() => Fluent.Body2(GalleryText.Get("Body2: sample text with English 123")))), Record(GallerySample.Create(() => Fluent.Body1(GalleryText.Get("Body1: sample text with English 123")))),
            Record(GallerySample.Create(() => Fluent.Body1Strong("Body1Strong"))), Record(GallerySample.Create(() => Fluent.Body1Stronger("Body1Stronger"))),
            Record(GallerySample.Create(() => Fluent.Caption1("Caption1"))), Record(GallerySample.Create(() => Fluent.Caption1Strong("Caption1Strong"))), Record(GallerySample.Create(() => Fluent.Caption1Stronger("Caption1Stronger"))),
            Record(GallerySample.Create(() => Fluent.Caption2("Caption2"))), Record(GallerySample.Create(() => Fluent.Caption2Strong("Caption2Strong"))), Record(GallerySample.Create(() => Fluent.Text(GalleryText.Get("Text: default Body1 style")))),
            Fluent.Subtitle1(GalleryText.Get("Paragraph with mixed weights")),
            Fluent.Caption1(GalleryText.Get("Change font weight and typeface in a paragraph and see how it wraps. Windows and Linux both use the operating system's typesetting.")),
            Record(GallerySample.Create(() => Dux.SizedBox(new UiInlineText([
                new(GalleryText.Get("Sample heading") + " ", UiInlineTextWeight.Semibold), new(GalleryText.Get("Sample body with English, AV office é."))]), width: 420))),
            Fluent.Switch(GalleryText.Get("Right-to-left paragraph"), _rightToLeft.Binding),
            Record(directionSample),
            Record(GallerySample.Create(() => Dux.SizedBox(new UiInlineText([
                new("A \u202Bאב " + GalleryText.Get("Bidi sample character") + " ", UiInlineTextWeight.Semibold), new("גד\u202C Z / " + GalleryText.Get("Explicit directional context"))]), width: 240))),
            Fluent.Subtitle1(GalleryText.Get("Paragraph with widgets")),
            Fluent.Caption1(GalleryText.Get("The switch above also changes the direction of inline widgets. The rotated title and link are separate widgets.")),
            Record(ownedSample),
            Record(GallerySample.Create(() => Dux.SizedBox(Fluent.MessageBarBody(GalleryText.Get("גד regular 123 / text and بِت لا"),
                Fluent.MessageBarTitle("abc אב")), width: 240))));
    }
}
