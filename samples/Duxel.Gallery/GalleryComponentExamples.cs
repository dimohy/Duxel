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
        => new(name, description, GallerySample.Create(create, expression : source));

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
            return Fluent.RadioGroup(GalleryText.Get("Processing mode"), value.Binding,
                new UiRadioOption[] { new("efficient", GalleryText.Get("Prioritize speed")), new("balanced", GalleryText.Get("Balanced")), new("quality", GalleryText.Get("Prioritize quality")) });
        }, current => TextState("value", current, nullable : true));

        var horizontal = StatefulSample(new UiState<string?>("small"), value =>
        {
            return new UiRadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<UiRadioHorizontalLayout>>(
                GalleryText.Get("Size"), value.Binding,
                new UiRadioOption[] { new("small", GalleryText.Get("Small")), new("medium", GalleryText.Get("Medium")), new("large", GalleryText.Get("Large")) });
        }, current => TextState("value", current, nullable : true));

        var stacked = StatefulSample(new UiState<string?>("left"), value =>
        {
            return new UiRadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<UiRadioHorizontalStackedLayout>>(
                GalleryText.Get("Alignment"), value.Binding,
                new UiRadioOption[] { new("left", GalleryText.Get("Left edge")), new("center", GalleryText.Get("Center")), new("right", GalleryText.Get("Right edge")) });
        }, current => TextState("value", current, nullable : true));

        var allocation = StatefulSample(new UiState<string?>("balanced"), new UiState<float>(180), (selection, width) =>
        {
            var group = Fluent.RadioGroup(GalleryText.Get("Processing priority"), selection.Binding, new UiRadioOption[]
            {
                new("efficient", GalleryText.Get("Focus on reducing response time.")),
                new("balanced", GalleryText.Get("Balance speed and result quality.")),
                new("quality", GalleryText.Get("Improve result quality even if it takes longer."))
            });
            // A finite maximum lets the sample fit when the Gallery is narrower than the requested width.
            var parent = Dux.ConstrainedBox(group, new(0, width.Value, 0, float.PositiveInfinity));
            return Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle2(GalleryText.Get("Wrapping at different container widths")),
                Fluent.Button(GalleryText.Get("Change container width"), () =>
                {
                    width.Value = width.Value == 180 ? 360 : 180;
                    parent.Options = parent.Options with { MaxWidth = width.Value };
                }),
                Fluent.Caption1(() => string.Format(GalleryText.Get("Maximum container width: {0:F0} px"), width.Value)),
                parent);
        }, (selection, width) => TextState("selection", selection, nullable : true)
            + "\nvar width = new UiState<float>(" + GallerySample.Number(width) + ");");

        var requiredField = StatefulSample(new UiState<string?>(null), value =>
        {
            var group = Fluent.RadioGroup(string.Empty, value.Binding, new UiRadioOption[]
            {
                new("daily", GalleryText.Get("Daily")), new("weekly", GalleryText.Get("Weekly")), new("monthly", GalleryText.Get("Monthly"))
            });
            var field = Fluent.Field(group);
            field.Options = new()
            {
                Label = GalleryText.Get("Notification frequency"),
                Hint = GalleryText.Get("Choose how often to receive notifications."),
                Required = true
            };
            return field;
        }, current => TextState("value", current, nullable : true));

        return new("RadioGroup", "Use the arrow keys to change the selection and see how long labels wrap at different container widths.",
            choices, horizontal, stacked, allocation, requiredField);
    }

    private static ComponentExamplePage CreateAnimation()
    {
        var timing = MotionExample(() => new MotionPreview(), """
            var transition = new UiFloatTransition(0);
            var options = new UiTransitionOptions(duration, easing, reducedMotion, 1d / frameRate)
            {
                Curve = new UiCubicBezier(.25, .1, .25, 1)
            };
            float value = ui.AnimateFloat(ref transition, target, options);
            """);
        var rotation = MotionExample(() => new RotationMotionPreview(), """
            float degrees = ui.AnimateToggleRotationDegrees("marker", rotated,
                expandedDegrees : 180, durationSeconds : reducedMotion ? 0 : .9f);
            """);
        var scrollbar = MotionExample(() => new ScrollbarMotionPreview(), """
            var frame = motion.Update(offset, active, hovered, ui.GetTime(),
                new(HoldSeconds : .5, ReducedMotion : reducedMotion));
            if (frame.NextFrameTimeSeconds is { } deadline)
                ui.RequestFrameAt(deadline);
            // Paint the thumb with frame.Opacity.
            """);
        var presence = MotionExample(() => new PresenceMotionPreview(), """
            var presence = new UiPresenceGroupWidget(children, new(Gap : 8, AnimateEnter : true));
            presence.SetItems(nextItems);
            presence.Options = presence.Options with { ReducedMotion = reducedMotion };
            """);
        return new("Animation", "Compare timing, reverse or reset a transition, and try rotation, scrollbar fades, and animated layout changes.",
            timing, rotation, scrollbar, presence);
    }

    private static GallerySample<UiWidget> MotionExample<T>(Func<T> create, string excerpt,
        [CallerArgumentExpression(nameof(create))] string expression = "") where T : UiWidget =>
        GallerySample.Create<UiWidget>(() => create(), expression : expression,
            declarations : GallerySample.LocalizeSource(GalleryPageSource.Declaration(typeof(T).Name))).WithExcerpt(excerpt);

    private static ComponentExamplePage CreateImage(IUiImageDecoder decoder)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Image");
        const string pathSetup = """
            var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Image");
            """;
        UiImageSource Load(string extension)
        {
            var path = Path.Combine(directory, "alpine-lake." + extension);
            return extension == "ppm" ? UiImageSource.LoadFromFile(path) : new(decoder.Decode(path));
        }

        var photo = Load("jpg");
        var png = Load("png");
        var gif = Load("gif");
        var bmp = Load("bmp");
        var ico = Load("ico");
        var ppm = Load("ppm");
        const string photoSetup = pathSetup + "\nvar photo = UiImageSource.LoadFromFile(Path.Combine(directory, \"alpine-lake.jpg\"));";

        var fitSample = GallerySample.Create<UiWidget>(() =>
        {
            UiWidget Preview(string label, UiImageFit fit)
            {
                var image = Fluent.Image(photo);
                image.Size = new(172, 136);
                image.Fit = fit;
                image.AccessibleName = label;
                return Dux.Column(new(Spacing : 4, MainAxisSize : UiMainAxisSize.Min), Fluent.Caption1(label), image);
            }

            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle2(GalleryText.Get("Image fit modes")),
                Fluent.Body1(GalleryText.Get("Stretch changes the aspect ratio. Contain shows the whole image. Cover fills the box and crops the edges.")),
                Dux.Wrap(new(Spacing : 12, RunSpacing : 12),
                    Preview("Stretch", UiImageFit.Stretch), Preview("Contain", UiImageFit.Contain), Preview("Cover", UiImageFit.Cover)));
        }, setup : photoSetup);
        var formatSample = GallerySample.Create<UiWidget>(() =>
        {
            UiWidget Preview(string label, UiImageSource source)
            {
                var image = Fluent.Image(source);
                image.Size = new(104, 72);
                image.Fit = UiImageFit.Contain;
                image.AccessibleName = label;
                return Dux.Column(new(Spacing : 4, MainAxisSize : UiMainAxisSize.Min), Fluent.Caption1(label), image);
            }

            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle2(GalleryText.Get("Image file formats")),
                Dux.Wrap(new(Spacing : 12, RunSpacing : 12),
                    Preview("PNG", png), Preview("JPEG (.jpg, .jpeg)", photo), Preview("GIF", gif),
                    Preview("BMP", bmp), Preview("ICO", ico), Preview("PPM", ppm)),
                Fluent.Caption1(GalleryText.Get("Image displays the first GIF frame. The SVG page shows vector rendering.")));
        }, setup : photoSetup + """

            var png = UiImageSource.LoadFromFile(Path.Combine(directory, "alpine-lake.png"));
            var gif = UiImageSource.LoadFromFile(Path.Combine(directory, "alpine-lake.gif"));
            var bmp = UiImageSource.LoadFromFile(Path.Combine(directory, "alpine-lake.bmp"));
            var ico = UiImageSource.LoadFromFile(Path.Combine(directory, "alpine-lake.ico"));
            var ppm = UiImageSource.LoadFromFile(Path.Combine(directory, "alpine-lake.ppm"));
            """);
        var appearanceSample = GallerySample.Create<UiWidget>(() =>
        {
            var rounded = Fluent.Image(photo);
            rounded.Size = new(172, 114);
            rounded.CornerRadius = 16;
            var tinted = Fluent.Image(photo);
            tinted.Size = rounded.Size;
            tinted.CornerRadius = rounded.CornerRadius;
            tinted.Tint = new(180, 210, 255);
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle2(GalleryText.Get("Rounded corners and tint")),
                Dux.Wrap(new(Spacing : 12, RunSpacing : 12), rounded, tinted));
        }, setup : photoSetup);
        return new("Image", "Compare bitmap formats, fit modes, rounded corners, and tint.", fitSample, formatSample, appearanceSample);
    }

    internal static IObserverPage[] CreateLayoutExamples() =>
    [
        Example("SizedBox", "Choose a fixed width and height for a child.", () =>
            Dux.Wrap(new(Spacing : 12, RunSpacing : 8),
                Dux.SizedBox(Fluent.Button("208 × 52"), width : 208, height : 52),
                Dux.SizedBox(Fluent.Button("Width : 180"), width : 180),
                Dux.SizedBox(Fluent.Button("Height : 52"), height : 52),
                Dux.SizedBox(Fluent.Button("Natural size")),
                Dux.Row(new(Spacing : 8), Fluent.Badge("Left"), Dux.SizedBox(width : 32), Fluent.Badge("32 px gap")))),
        Example("ConstrainedBox", "Set minimum and maximum dimensions for a child.", () =>
            Dux.Wrap(new(Spacing : 12, RunSpacing : 8),
                Dux.ConstrainedBox(Fluent.Button("160–240 × 40–56"), new(160, 240, 40, 56)),
                Dux.ConstrainedBox(Fluent.Button("Tight : 140 × 48"), UiBoxConstraints.Tight(new(140, 48))),
                Dux.ConstrainedBox(Fluent.Button("Loose : ≤ 200 × 56"), UiBoxConstraints.Loose(new(200, 56))))),
        Example("Expand", "Fill the finite width and height supplied by the parent.", () =>
            Dux.SizedBox(Dux.Expand(Fluent.Button("240 × 64")), width : 240, height : 64)),
        Example("Align", "Align a child within the space available.", () =>
        {
            (string Name, UiAlignment Alignment)[] positions =
            [
                ("TopLeft", new(-1, -1)), ("TopCenter", new(0, -1)), ("TopRight", new(1, -1)),
                ("CenterLeft", new(-1, 0)), ("Center", new(0, 0)), ("CenterRight", new(1, 0)),
                ("BottomLeft", new(-1, 1)), ("BottomCenter", new(0, 1)), ("BottomRight", new(1, 1))
            ];
            UiWidget Frame(string name) => new UiViewHost(new UiView(ui =>
            {
                var origin = ui.GetCursorScreenPos();
                var size = ui.GetContentRegionAvail();
                var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
                var draw = ui.GetWindowDrawList();
                draw.AddRectFilled(bounds, ui.GetColorU32(UiStyleColor.FrameBg), ui.WhiteTextureId, bounds);
                draw.AddRect(new(bounds.X + .5f, bounds.Y + .5f, bounds.Width - 1, bounds.Height - 1),
                    ui.GetColorU32(UiStyleColor.Separator));
            })) { AccessibleName = GalleryText.Format("Alignment frame {0}", name) };
            UiWidget Cell(string name, UiAlignment alignment) => Dux.Stack(new(Fit : UiStackFit.Expand), Frame(name),
                Dux.Align(Fluent.Button(name), alignment));
            return Dux.Column(new(Spacing : 14, MainAxisSize : UiMainAxisSize.Min),
                Dux.SizedBox(Dux.UniformGrid(new(3, CrossAxisSpacing : 12, MainAxisSpacing : 12, MainAxisExtent : 64),
                    [..positions.Select(position => Cell(position.Name, position.Alignment))]), width : 450),
                Dux.Wrap(new(Spacing : 12, RunSpacing : 12),
                    Dux.SizedBox(Cell("Custom (-0.5, 0.5)", new(-.5f, .5f)), width : 142, height : 64),
                    Dux.Align(Fluent.Button("Width 1.5×"), UiAlignment.TopLeft, widthFactor : 1.5f),
                    Dux.Align(Fluent.Button("Height 2×"), UiAlignment.BottomRight, heightFactor : 2),
                    Dux.Align(Fluent.Badge("Width 1.5×, Height 2×"), UiAlignment.BottomRight, widthFactor : 1.5f, heightFactor : 2)));
        }),
        Example("Center", "Center a child horizontally and vertically.", () =>
            Dux.Wrap(new(Spacing : 12, RunSpacing : 8),
                Dux.SizedBox(Dux.Center(Fluent.Button("Center")), width : 240, height : 72),
                Dux.Center(Fluent.Button("1.5× width"), widthFactor : 1.5f),
                Dux.Center(Fluent.Button("2× height"), heightFactor : 2),
                Dux.Center(Fluent.Button("Both factors"), widthFactor : 1.5f, heightFactor : 2))),
        Example("Padding", "Add spacing inside the layout around a child.", () =>
            Dux.Wrap(new(Spacing : 12, RunSpacing : 8), Dux.Padding(Fluent.Button("Uniform : 16"), new(16)),
                Dux.Padding(Fluent.Button("Left : 24, Top : 8"), new(24, 8, 0, 0)))),
        Example("Container", "Combine margin and padding around content.", () =>
            Dux.Wrap(new(Spacing : 12, RunSpacing : 8),
                Dux.Container(Fluent.Button("Padding : 16, Margin : 8"), new(Padding : new(16), Margin : new(8))),
                Dux.Container(Fluent.Button("BottomRight"), new(Alignment : UiAlignment.BottomRight, Width : 180, Height : 72)),
                Dux.Container(Fluent.Button("Min width : 140"), new(Constraints : new(140, 200, 32, 56))))),
        FlexExamples("Row", true),
        FlexExamples("Column", false),
        WrapExamples("Wrap", false),
        WrapExamples("VerticalWrap", true),
        GridExamples("UniformGrid", false),
        GridExamples("HorizontalGrid", true),
        Example("Stack", "Layer widgets in the same space.", () =>
            Dux.Wrap(new(Spacing : 12, RunSpacing : 8), [..Enum.GetValues<UiStackFit>().Select(fit =>
                Dux.SizedBox(Dux.Stack(new(UiAlignment.Center, fit), Fluent.Card(fit.ToString(), Fluent.Body1(fit.ToString())),
                    Dux.Positioned(Fluent.Badge(fit.ToString()), new(Right : 6, Bottom : 6))), width : 180, height : 64))])),
        Example("Positioned", "Anchor a child to the edges of a Stack.", () =>
            Dux.Wrap(new(Spacing : 12, RunSpacing : 8),
                Dux.SizedBox(Dux.Stack(new(Fit : UiStackFit.Expand),
                    Dux.Positioned(Fluent.Button("Left + top"), new(Left : 8, Top : 8)),
                    Dux.Positioned(Fluent.Button("Right + bottom"), new(Right : 8, Bottom : 8)),
                    Dux.Positioned(Fluent.Button("Both sides"), new(Left : 8, Right : 8, Top : 48)),
                    Dux.Positioned(Fluent.Button("140 × 32"), new(Left : 8, Top : 100, Width : 140, Height : 32)),
                    Dux.Positioned(Fluent.Badge("Stack alignment"))), width : 260, height : 180))),
        Example("ScrollView", "Scroll content inside a fixed viewport.", () =>
        {
            UiWidget Row(int i) => Dux.Row(new(Spacing : 6, MainAxisSize : UiMainAxisSize.Min),
                [..Enumerable.Range(1, 8).Select(j => Dux.SizedBox(Fluent.Button($"{i},{j}"), width : 52))]);
            UiWidget Content(UiScrollAxis axis) => axis switch
            {
                UiScrollAxis.Horizontal => Row(1),
                UiScrollAxis.Vertical => Dux.Column(new(Spacing : 6, MainAxisSize : UiMainAxisSize.Min),
                    [..Enumerable.Range(1, 7).Select(i => Fluent.Button(i.ToString()))]),
                _ => Dux.Column(new(Spacing : 6, MainAxisSize : UiMainAxisSize.Min),
                    [..Enumerable.Range(1, 7).Select(Row)])
            };
            return Dux.Wrap(new(Spacing : 12, RunSpacing : 10), [..Enum.GetValues<UiScrollAxis>().Select(axis =>
                Dux.Column(new(Spacing : 4, MainAxisSize : UiMainAxisSize.Min), Fluent.Caption1(axis.ToString()),
                    Dux.ScrollView(Content(axis), new(axis, Width : 182, Height : 116)))),
                Dux.Column(new(Spacing : 4, MainAxisSize : UiMainAxisSize.Min), Fluent.Caption1("RightToLeft"),
                    Dux.ScrollView(Content(UiScrollAxis.Horizontal), new(UiScrollAxis.Horizontal, Width : 182, Height : 116) { RightToLeft = true }))]);
        }),
        Example("Expanded", "Use the row's remaining width.", () =>
            Dux.SizedBox(Dux.Row(new(Spacing : 8), Fluent.Button("Fixed"), Dux.Expanded(Fluent.Button("Flex 1")),
                Dux.Expanded(Fluent.Button("Flex 2"), flex : 2)), width : 320)),
        Example("Flexible", "Allow a child to use part of the remaining width.", () =>
            Dux.SizedBox(Dux.Row(new(Spacing : 8), Dux.Flexible(Fluent.Button("Flex 1")),
                Dux.Flexible(Fluent.Button("Flex 2"), flex : 2), Fluent.Button("Fixed")), width : 320)),
        Example("FlexSpacer", "Reserve remaining width between siblings.", () =>
            Dux.SizedBox(Dux.Row(new(Spacing : 8), Fluent.Button("1"), Dux.FlexSpacer(), Fluent.Button("2"),
                Dux.FlexSpacer(flex : 2), Fluent.Button("3")), width : 320)),
        OverflowExamplesFor("Overflow", false),
        OverflowExamplesFor("VerticalOverflow", true)
    ];

    private static ComponentExamplePage FlexExamples(string name, bool horizontal) =>
        Stateful(name, horizontal ? "Place widgets side by side." : "Stack widgets vertically.", new UiState<bool>(horizontal), direction =>
        {
            UiWidget Case(string label, UiFlexOptions options)
            {
                UiWidget[] children = [Dux.SizedBox(Fluent.Button("1"), width : 38, height : 30),
                    Dux.SizedBox(Fluent.Button("2"), width : 66, height : 35), Dux.SizedBox(Fluent.Button("3"), width : 48, height : 42)];
                var content = direction.Value ? (UiWidget)Dux.Row(options, children) : Dux.Column(options, children);
                UiBoxConstraints bounds = direction.Value ? new(0, 200, 52, 52) : new(200, 200, 0, 152);
                return Dux.Column(new(Spacing : 5, MainAxisSize : UiMainAxisSize.Min), Fluent.Caption1(label),
                    Dux.ConstrainedBox(content, bounds));
            }
            return Dux.Wrap(new(Spacing : 14, RunSpacing : 12),
                [..Enum.GetValues<UiMainAxisAlignment>().Select(main => Case(main.ToString(), new(Spacing : 3, MainAxisAlignment : main))),
                ..Enum.GetValues<UiCrossAxisAlignment>().Select(cross => Case("Cross : " + cross, new(Spacing : 3, CrossAxisAlignment : cross))),
                Case("Main size : Min", new(Spacing : 3, MainAxisSize : UiMainAxisSize.Min))]);
        }, horizontal => "var direction = new UiState<bool>(" + GallerySample.Boolean(horizontal) + ");");

    private static ComponentExamplePage WrapExamples(string name, bool vertical) =>
        Stateful(name, vertical ? "Move items to a new column when they no longer fit." : "Move items to a new row when they no longer fit.", new UiState<bool>(vertical), direction =>
        {
            UiWidget Case(string label, UiWrapOptions options)
            {
                UiWidget[] children = [Dux.SizedBox(Fluent.Button("1"), width : 58),
                    Dux.SizedBox(Fluent.Button("2"), width : 70), Dux.SizedBox(Fluent.Button("3"), width : 64, height : 46),
                    Dux.SizedBox(Fluent.Button("4"), width : 58), Dux.SizedBox(Fluent.Button("5"), width : 58)];
                var content = direction.Value ? (UiWidget)Dux.VerticalWrap(options, children) : Dux.Wrap(options, children);
                return Dux.Column(new(Spacing : 5, MainAxisSize : UiMainAxisSize.Min), Fluent.Caption1(label),
                    Dux.SizedBox(content, width : 200, height : 138));
            }
            return Dux.Wrap(new(Spacing : 14, RunSpacing : 12),
                [..Enum.GetValues<UiMainAxisAlignment>().Select(value => Case("Alignment : " + value, new(4, 4, Alignment : value))),
                ..Enum.GetValues<UiMainAxisAlignment>().Select(value => Case("RunAlignment : " + value, new(4, 4, RunAlignment : value))),
                ..Enum.GetValues<UiWrapCrossAlignment>().Select(value => Case("Cross : " + value, new(4, 4, CrossAlignment : value))),
                Case("RightToLeft", new(4, 4) { RightToLeft = true })]);
        }, vertical => "var direction = new UiState<bool>(" + GallerySample.Boolean(vertical) + ");");

    private static ComponentExamplePage GridExamples(string name, bool horizontal) =>
        Stateful(name, "Give each item an equally sized grid cell.", new UiState<bool>(horizontal), direction =>
        {
            UiWidget Case(string label, UiUniformGridOptions options)
            {
                UiWidget[] children = [..Enumerable.Range(1, 6).Select(i => Fluent.Button(i.ToString()))];
                var grid = direction.Value ? (UiWidget)Dux.HorizontalGrid(options, children) : Dux.UniformGrid(options, children);
                return Dux.Column(new(Spacing : 5, MainAxisSize : UiMainAxisSize.Min), Fluent.Caption1(label),
                    direction.Value ? Dux.SizedBox(grid, height : 132) : Dux.SizedBox(grid, width : 192));
            }
            return Dux.Wrap(new(Spacing : 14, RunSpacing : 12),
                Case("MainAxisExtent : 36", new(3, CrossAxisSpacing : 6, MainAxisSpacing : 8, MainAxisExtent : 36)),
                Case("ChildAspectRatio : 1.8", new(2, CrossAxisSpacing : 4, MainAxisSpacing : 6, ChildAspectRatio : 1.8f)));
        }, horizontal => "var direction = new UiState<bool>(" + GallerySample.Boolean(horizontal) + ");");

    private static ComponentExamplePage OverflowExamplesFor(string name, bool vertical) =>
        Stateful(name, "Keep the items that fit and show an indicator for the rest.", new UiState<bool>(vertical), direction =>
        {
            UiWidget Case(string label, UiOverflowOptions options, bool prioritizeLast = false, bool wide = false)
            {
                UiWidget[] items = [..Enumerable.Range(1, 5).Select(i => Dux.SizedBox(Fluent.Button(i.ToString()), width : 50, height : 30))];
                int[]? priorities = prioritizeLast ? [0, 0, 0, 0, 2] : null;
                var indicator = Fluent.Badge("+", new(UiBadgeSize.Small));
                var overflow = direction.Value ? (UiWidget)Dux.VerticalOverflow(items, indicator, options, priorities)
                    : Dux.Overflow(items, indicator, options, priorities);
                return Dux.Column(new(Spacing : 5, MainAxisSize : UiMainAxisSize.Min), Fluent.Caption1(label),
                    Dux.SizedBox(overflow, width : direction.Value ? 65 : wide ? 300 : 180,
                        height : direction.Value ? wide ? 186 : 108 : 32));
            }
            return Dux.Wrap(new(Spacing : 14, RunSpacing : 12),
                Case("All fit", new(Spacing : 4), wide : true),
                Case("Collapse : End", new(Spacing : 4)),
                Case("Collapse : Start", new(Spacing : 4, CollapseFrom : UiOverflowCollapseDirection.Start)),
                Case("Priority : last", new(Spacing : 4), prioritizeLast : true),
                Case("MinimumVisible : 2", new(Spacing : 4, MinimumVisible : 2)),
                Case("RightToLeft", new(Spacing : 4, RightToLeft : true)));
        }, vertical => "var direction = new UiState<bool>(" + GallerySample.Boolean(vertical) + ");");

    internal static IObserverPage[] Create(IUiImageDecoder imageDecoder) =>
    [
        new IconCatalogPage(),
        Stateful("TagPicker", "Search for a recipient by name, then try removing a selected tag.", new UiState<string>(string.Empty), new UiState<string[]>(["dimoi"]), (query, selected) =>
        {
            UiSelectOption[] people = [new("dimoi", GalleryText.Get("Dimoi")), new("kim", GalleryText.Get("Min-su Kim")), new("park", GalleryText.Get("Ji-yeon Park")), new("guest", GalleryText.Get("Guest"), false)];
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.TagPicker(GalleryText.Get("Recipients"), query.Binding, selected.Binding, people,
                    new(Field : new(Width : 520, Placeholder : GalleryText.Get("Enter a name")),
                        EmptyText : GalleryText.Get("No matching people"), DismissLabel : GalleryText.Get("Remove"))
                    {
                        SecondaryAction = new UiButton<UiReleaseButtonBehavior, UiFluentSmallButtonPresentation<UiFluentSubtleButtonAppearance>>(
                            GalleryText.Get("Clear selection"), () => selected.Value = [])
                    }),
                Fluent.Caption1(GalleryText.Get("Type a name to search and press Enter to add it. At the start of the input, press Left Arrow or Backspace to focus the last tag.")),
                Fluent.Body1(() => GalleryText.Get("Selected: ") + string.Join(", ", selected.Value)));
        }, (query, selected) => TextState("query", query) + "\nvar selected = new UiState<string[]>(" + StringArray(selected) + ");"),
        Stateful("Button", "Press the button to increase the click count.", new UiState<int>(0), clicks =>
        {
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Button(GalleryText.Get("Increase count"), () => clicks.Value++),
                Fluent.CompoundButton(GalleryText.Get("Create document"), GalleryText.Get("Start a new document from a blank form."), () => clicks.Value++),
                Fluent.SplitButton(GalleryText.Get("Save as PNG"), GalleryText.Get("Save in another format"), () => clicks.Value++,
                    [new("pdf", GalleryText.Get("Save as PDF")), new("svg", GalleryText.Get("Save as SVG"))], _ => clicks.Value += 10),
                Fluent.Body1(() => GalleryText.Get("Click count: ") + clicks.Value));
        }, current => "var clicks = new UiState<int>(" + current.ToString(System.Globalization.CultureInfo.InvariantCulture) + ");"),
        Stateful("Checkbox", "Select the checkbox that starts in the mixed state.", new UiState<bool?>(null), value =>
        {
            return Fluent.Checkbox(GalleryText.Get("Start in mixed state"), value.Binding);
        }, current => "var value = new UiState<bool?>(" + (current is null ? "null" : GallerySample.Boolean(current.Value)) + ");"),
        new ComponentExamplePage("Switch", "Turn the feature on or off and watch the value change in the code below.",
            StatefulSample(new UiState<bool>(false), value =>
            {
                return Fluent.Switch(GalleryText.Get("Enable feature"), value.Binding);
            }, current => "var value = new UiState<bool>(" + GallerySample.Boolean(current) + ");"),
            StatefulSample(new UiState<bool>(false), value =>
            {
                return Dux.Row(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Center),
                    Fluent.IconSwitch(GalleryText.Get("Theme preview"), value.Binding,
                        UiSwitchIcon.Create<GalleryDarkThemeIcon>(new(14, 51, 95), new(190, 222, 255)),
                        UiSwitchIcon.Create<GalleryLightThemeIcon>(new(128, 69, 0), new(255, 232, 169))),
                    Fluent.Body1(GalleryText.Get("Theme preview")));
            }, current => "var value = new UiState<bool>(" + GallerySample.Boolean(current) + ");")),
        RadioGroupExample(),
        Stateful("Slider", "Drag the thumb or use the arrow keys to adjust the value between 0 and 1.", new UiState<float>(.35f), value =>
        {
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Slider(GalleryText.Get("Adjust value"), value.Binding, 0, 1, .01f),
                Fluent.Body1(() => GalleryText.Get("Value: ") + value.Value.ToString("F2")));
        }, current => "var value = new UiState<float>(" + GallerySample.Number(current) + ");"),
        Example("ProgressBar", "The bar shows a task that is 35% complete.", () =>
        {
            var progress = Fluent.ProgressBar(() => new(.35f, 1));
            progress.Size = new(280, 4);
            return progress;
        }),
        Example("Badge", "Compare a short status badge with a badge that shows a count.", () =>
        {
            return Dux.Wrap(new(Spacing : 12), Fluent.Badge(GalleryText.Get("Ready")), Fluent.CounterBadge(() => new(5)));
        }),
        Example("Divider", "Divide the content above and below with a line and a centered label.", () =>
        {
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1(GalleryText.Get("Content above")), Fluent.Divider(GalleryText.Get("Section break")), Fluent.Body1(GalleryText.Get("Content below")));
        }),
        Stateful("Link", "Activate the link to display its destination below.", new UiState<string>(GalleryText.Get("No destination selected yet.")), destination =>
        {
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Link(GalleryText.Get("Choose a destination"), new Uri("/gallery", UriKind.Relative), uri => destination.Value = uri.ToString()),
                Fluent.Body1(() => GalleryText.Get("Selected destination: ") + destination.Value));
        }, current => TextState("destination", current)),
        Stateful("Spinner", "Show work with no known completion percentage. Use the button to start or stop the spinner.", new UiState<bool>(true), paused =>
        {
            var spinner = Fluent.Spinner(GalleryText.Get("Paused"), new(ReducedMotion : true) { Paused = paused.Value });
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Button(GalleryText.Get("Toggle playback"), () =>
                {
                    paused.Value = !paused.Value;
                    spinner.Options = spinner.Options with { Paused = paused.Value };
                }), spinner);
        }, current => "var paused = new UiState<bool>(" + GallerySample.Boolean(current) + ");"),
        Stateful("Input", "Type on a single line, then select and replace part of the text.", new UiState<string>(GalleryText.Get("Hello, world")), text =>
        {
            return Fluent.Input(GalleryText.Get("Single-line input"), text.Binding, 4096, 280);
        }, current => TextState("text", current)),
        Stateful("Textarea", "Type across multiple lines or move between lines to edit the text.", new UiState<string>(GalleryText.Get("First line") + "\n" + GalleryText.Get("Second line")), text =>
        {
            return Fluent.Textarea(GalleryText.Get("Multiline input"), text.Binding, 16384, 280, 180);
        }, current => TextState("text", current)),
        Stateful("Field", "A name field with both a required indicator and a hint.", new UiState<string>(string.Empty), text =>
        {
            var field = Fluent.Field(Fluent.Input(GalleryText.Get("Name"), text.Binding, 256, 280));
            field.Options = new() { Label = GalleryText.Get("Name"), Required = true, Hint = GalleryText.Get("Enter at least two characters.") };
            return field;
        }, current => TextState("text", current)),
        Stateful("Label", "Activate the label to focus its associated input.", new UiState<string>(string.Empty), text =>
        {
            var input = Fluent.Input(GalleryText.Get("Labeled input"), text.Binding, 256, 280);
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), Fluent.Label(GalleryText.Get("Select to focus the input"), input), input);
        }, current => TextState("text", current)),
        Stateful("Dropdown", "Open the list and choose a work location.", new UiState<string?>(null), selected =>
        {
            return Fluent.Dropdown(GalleryText.Get("Work location"), selected.Binding,
                [new("kr", GalleryText.Get("South Korea")), new("us", GalleryText.Get("United States")), new("jp", GalleryText.Get("Japan"))],
                new(Placeholder : GalleryText.Get("Choose a location"), ExpandLabel : GalleryText.Get("Open list")));
        }, current => TextState("selected", current, nullable : true)),
        Stateful("Combobox", "Type a city name to filter the list or to enter a city of your own.", new UiState<string>(string.Empty), new UiState<string?>(null), (query, selected) =>
        {
            return Fluent.Combobox(GalleryText.Get("Search cities"), query.Binding, selected.Binding,
                [new("seoul", GalleryText.Get("Seoul")), new("busan", GalleryText.Get("Busan")), new("london", GalleryText.Get("London"))],
                new(Field : new(Placeholder : GalleryText.Get("Enter a city name"), ExpandLabel : GalleryText.Get("Open city list")), Freeform : true));
        }, (query, selected) => TextState("query", query) + "\n" + TextState("selected", selected, nullable : true)),
        Example("Tag", "Show three city names as small tags side by side.", () =>
        {
            return Dux.Wrap(new(Spacing : 8), Fluent.Tag(GalleryText.Get("Seoul")), Fluent.Tag(GalleryText.Get("Busan")), Fluent.Tag(GalleryText.Get("Jeju")));
        }),
        Stateful("TabList", "Click a tab or use the keyboard to see how the selection indicator changes.", new UiState<string?>("first"), selected =>
        {
            return Fluent.TabList(GalleryText.Get("Example tabs"), selected.Binding,
                new UiTabOption[] { new("first", GalleryText.Get("First")), new("second", GalleryText.Get("Second")), new("third", GalleryText.Get("Third")) });
        }, current => TextState("selected", current, nullable : true)),
        Stateful("Menu", "Open the menu and run the New document or Save command.", new UiState<string>(GalleryText.Get("None selected")), chosen =>
        {
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Menu(GalleryText.Get("Open menu"), [new("new", GalleryText.Get("New document")), new("save", GalleryText.Get("Save"))], value => chosen.Value = value),
                Fluent.MenuButton(GalleryText.Get("Export"), [new("pdf", GalleryText.Get("Export as PDF")), new("png", GalleryText.Get("Export as PNG"))], value => chosen.Value = value),
                Fluent.Body1(() => GalleryText.Get("Command: ") + chosen.Value));
        }, current => TextState("chosen", current)),
        Stateful("MenuBar", "Use Tab to focus the menu bar, then use the arrow keys to move between menus.",
            new UiState<string>(GalleryText.Get("None selected")), new UiState<bool>(true), (chosen, showStatus) =>
        {
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min,
                    CrossAxisAlignment : UiCrossAxisAlignment.Stretch),
                Fluent.MenuBar(GalleryText.Get("Application menu"),
                    new(GalleryText.Get("File"),
                        [new("new", GalleryText.Get("New document")),
                         UiMenuOption.Submenu("export", GalleryText.Get("Export"),
                            [new("pdf", GalleryText.Get("Export as PDF")), new("png", GalleryText.Get("Export as PNG"))]),
                         UiMenuOption.Separator("file-divider"), new("save", GalleryText.Get("Save"))],
                        value => chosen.Value = value, UiKey.F),
                    new(GalleryText.Get("Edit"), [new("copy", GalleryText.Get("Copy"), Enabled : false),
                        new("clear", GalleryText.Get("Clear"))], value => chosen.Value = value, UiKey.E),
                    new(GalleryText.Get("View"), [UiMenuOption.Checkbox("status", GalleryText.Get("Status bar"), showStatus.Binding)],
                        value => chosen.Value = value, UiKey.V)),
                Fluent.Divider(),
                Fluent.Body1(() => showStatus.Value ? GalleryText.Get("Command: ") + chosen.Value : string.Empty));
        }, (chosen, showStatus) => TextState("chosen", chosen) + "\nvar showStatus = new UiState<bool>(true);"),
        Example("CodeView", "Try syntax highlighting, selection, copying, line wrapping, and line numbers.", () =>
        {
            var text = GalleryText.Get("This intentionally long string lets you test horizontal scrolling, line wrapping, text selection, and copying in the code viewer.");
            var lines = Enumerable.Range(1, 60).Select(index =>
                $"    public const string Message{index} = \"{text} ({index})\";");
            var source = "public static class Messages\n{\n" + string.Join("\n", lines) + "\n}";
            return new UiCodeView(source, new(Wrap : false, MaximumVisibleLines : 10, InitiallyExpanded : false),
                GalleryText.CodeViewLabels() with { AccessibleName = GalleryText.Get("CodeView preview") });
        }),
        CreateAnimation(),
        CreateImage(imageDecoder)
    ];
}
