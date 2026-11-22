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
        : this(name, description, sample, additional, null) { }

    internal ComponentExamplePage(string name, string description, GallerySample<UiWidget> sample,
        GallerySample<UiWidget>[] additional, UiWidget? introduction)
    {
        Title = name;
        Description = description;
        _sample = sample;
        Samples = [sample, ..additional];
        var body = additional.Length == 0 && introduction is null ? (UiWidget)sample
            : Dux.Column(new(Spacing : 24, MainAxisSize : UiMainAxisSize.Min),
                introduction is null ? [sample, ..additional] : [introduction, sample, ..additional]);
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

        return new("RadioGroup", "Use the arrow keys to change the selection and see how long labels wrap at different container widths.",
            choices, allocation);
    }

    private static ComponentExamplePage CreateAnimation(Action<string>? navigate)
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
        UiWidget? examples = null;
        if (navigate is not null)
        {
            (string Page, string Label)[] destinations =
            [
                ("ActivityIndicator", "ActivityIndicator"), ("ProgressIndicator", "ProgressIndicator"), ("ProgressBar", "ProgressBar"),
                ("Spinner", "Spinner"), ("Shimmer", "Shimmer"), ("스켈레톤", "Skeleton"),
                ("Toast", "Toast"), ("메시지 표시", "MessageBar"), ("HeadsUpDisplay", "HeadsUpDisplay"), ("Popover", "Popover"), ("Tooltip", "Tooltip"),
                ("대화 상자", "Dialog"), ("보조 패널", "Drawer"), ("접기·펼치기", "Accordion"),
                ("Tree", "Tree"), ("Carousel", "Carousel"), ("CardNudge", "CardNudge"),
                ("SegmentedControl", "SegmentedControl"), ("TabList", "TabList"), ("렌더링", "Rendering")
            ];
            examples = Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle2(GalleryText.Get("Motion in other examples")),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 6), destinations.Select(route =>
                    (UiWidget)Fluent.Button(GalleryText.Get(route.Label), () => navigate(route.Page))).ToArray()));
        }
        return new("Animation", "Compare timing, reverse or reset a transition, and try rotation, scrollbar fades, and animated layout changes.",
            timing, [rotation, scrollbar, presence], examples);
    }

    private static GallerySample<UiWidget> MotionExample<T>(Func<T> create, string excerpt,
        [CallerArgumentExpression(nameof(create))] string expression = "") where T : UiWidget =>
        GallerySample.Create<UiWidget>(() => create(), expression : expression,
            declarations : GallerySample.LocalizeSource(GalleryPageSource.Declaration(typeof(T).Name))).WithExcerpt(excerpt);

    internal static IObserverPage[] Create(Action<string>? navigate = null) =>
    [
        new IconCatalogPage(),
        Stateful("TagPicker", "Search for a recipient by name, then try removing a selected tag.", new UiState<string>(string.Empty), new UiState<string[]>(["dimoi"]), (query, selected) =>
        {
            UiSelectOption[] people = [new("dimoi", GalleryText.Get("Dimoi")), new("kim", GalleryText.Get("Min-su Kim")), new("park", GalleryText.Get("Ji-yeon Park")), new("guest", GalleryText.Get("Guest"), false)];
            return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.TagPicker(GalleryText.Get("Recipients"), query.Binding, selected.Binding, people,
                    new(Field: new(Width: 520, Placeholder: GalleryText.Get("Enter a name")), EmptyText: GalleryText.Get("No matching people"), DismissLabel: GalleryText.Get("Remove"))),
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
        Stateful("Switch", "Turn the feature on or off and watch the value change in the code below.", new UiState<bool>(false), value =>
        {
            return Fluent.Switch(GalleryText.Get("Enable feature"), value.Binding);
        }, current => "var value = new UiState<bool>(" + GallerySample.Boolean(current) + ");"),
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
        Example("CodeView", "Try syntax highlighting, selection, copying, line wrapping, and line numbers.", () =>
        {
            var text = GalleryText.Get("This intentionally long string lets you test horizontal scrolling, line wrapping, text selection, and copying in the code viewer.");
            var lines = Enumerable.Range(1, 60).Select(index =>
                $"    public const string Message{index} = \"{text} ({index})\";");
            var source = "public static class Messages\n{\n" + string.Join("\n", lines) + "\n}";
            return new UiCodeView(source, new(Wrap : false, MaximumVisibleLines : 10, InitiallyExpanded : false),
                new(GalleryText.Get("C# source"), GalleryText.Get("{0} lines"), GalleryText.Get("Wrap lines"),
                    GalleryText.Get("Line numbers"), GalleryText.Get("Show all"), GalleryText.Get("Copy source"),
                    GalleryText.Get("Copied"), GalleryText.Get("CodeView preview")));
        }),
        CreateAnimation(navigate),
        Example("Image", "Display a four-pixel image in a 160 × 160 widget.", () =>
        {
            var pixels = new byte[] { 0, 120, 212, 255, 100, 180, 240, 255, 100, 180, 240, 255, 0, 120, 212, 255 };
            var image = Fluent.Image(new UiImageSource(new UiImageData(2, 2, pixels)));
            image.Size = new(160, 160);
            return image;
        })
    ];
}
