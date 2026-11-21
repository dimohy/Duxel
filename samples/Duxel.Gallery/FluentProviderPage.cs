using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class FluentProviderPage(UiTypographyFonts? fonts = null) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "FluentProvider";
    public string Description => "Set the theme, font, and layout direction per region and see how child widgets inherit them.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new(fonts)).Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new(fonts)).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new(fonts)).Root.Render(ui);
    public void Dispose() => _content?.Dispose();

    private sealed class Content : IDisposable
    {
        private readonly UiState<string> _text = new(GalleryText.Get("Regions share this value."));
        private readonly List<IGallerySample> _samples = [];
        internal IReadOnlyList<IGallerySample> Samples => _samples;
        internal UiWidget Root { get; }
        internal Content(UiTypographyFonts? fonts)
        {
            var light = Example(GalleryText.Get("First region"), UiFluentProviderTheme.Light);
            var dark = Example(GalleryText.Get("Second region"), UiFluentProviderTheme.Dark);
            var nested = GallerySample.Create(() => Fluent.FluentProvider(
                Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Title3(GalleryText.Get("Dark parent region")),
                    Fluent.FluentProvider(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                        Fluent.Body1(GalleryText.Get("Light child region")), Fluent.Input(GalleryText.Get("Nested region input"), _text.Binding, 1024, 280)),
                        UiFluentProviderTheme.Light, new(Padding : new(16))),
                    Fluent.Body1(GalleryText.Get("Outside the child region, the parent theme applies again."))),
                UiFluentProviderTheme.Dark, new(Padding : new(16), Width : 440)));
            var factory = nested.Source;
            nested.BindSource(() => _text.Value, text => TextSetup(text) + "\n\n" + factory);
            _samples.Add(nested);
            var fontExample = fonts is null ? Dux.Column(new UiFlexOptions(MainAxisSize : UiMainAxisSize.Min)) : FontExample(fonts);
            Root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1(GalleryText.Get("Theme changes apply only to the current region. Choose Inherit to use the surrounding theme. The example inputs share their value.")),
                light, dark, Fluent.Subtitle1(GalleryText.Get("Nested theme")), nested, fontExample, DirectionExample(),
                Fluent.Caption1(GalleryText.Get("When the operating system uses a contrast theme, its colors take precedence.")));
        }
        private UiWidget DirectionExample()
        {
            var view = new UiState<string?>("week");
            var query = new UiState<string>(string.Empty);
            var search = new UiState<string>(string.Empty);
            var chosen = new UiState<string?>(null);
            var frequency = new UiState<string?>("daily");
            var notify = new UiState<bool>(true);
            var sound = new UiState<bool>(false);
            var volume = new UiState<float>(30);
            var expanded = new UiState<string[]>(["workspace"]);
            var placement = new UiTooltipOptions { Positioning = new() { Position = UiPopupPosition.BelowStart } };
            var sample = GallerySample.Create(() => Fluent.FluentProvider(
                Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.NavigationBar(GalleryText.Get("Schedule navigation"), GalleryText.Get("Schedule"),
                        leading : Fluent.Tooltip(Fluent.Button(GalleryText.Get("Back")), GalleryText.Get("Return to the previous screen."), placement),
                        trailing : Fluent.Button(GalleryText.Get("Today"), () => view.Value = "week"),
                        secondary : Fluent.SegmentedControl(GalleryText.Get("View"), view.Binding,
                            new UiSegmentOption[] { new("week", GalleryText.Get("Weekly view")), new("month", GalleryText.Get("Monthly view")) })),
                    Fluent.Toolbar(GalleryText.Get("Schedule tools"),
                        [Fluent.Button(GalleryText.Get("Weekly view"), () => view.Value = "week"), Fluent.Button(GalleryText.Get("Monthly view"), () => view.Value = "month")]),
                    Fluent.SearchBox(GalleryText.Get("Search schedule"), search.Binding,
                        options : new(Placeholder : GalleryText.Get("Enter an event name"), ClearLabel : GalleryText.Get("Clear search"))),
                    new UiRadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<UiRadioHorizontalLayout>>(
                        GalleryText.Get("Reminder frequency"), frequency.Binding, new UiRadioOption[]
                        { new("daily", GalleryText.Get("Every day")), new("weekly", GalleryText.Get("Every week")), new("monthly", GalleryText.Get("Every month")) }),
                    Dux.Wrap(new(Spacing : 8), Fluent.Checkbox(GalleryText.Get("Reminders"), notify.Binding), Fluent.Switch(GalleryText.Get("Sound"), sound.Binding)),
                    Fluent.Body1(() => string.Format(GalleryText.Get("Reminder volume: {0:0}%"), volume.Value)),
                    Fluent.Slider(GalleryText.Get("Reminder volume"), volume.Binding, 0, 100, 5),
                    Fluent.Combobox(GalleryText.Get("Event location"), query.Binding, chosen.Binding,
                        [new("seoul", GalleryText.Get("Seoul")), new("busan", GalleryText.Get("Busan"))],
                        new(Field : new(Placeholder : GalleryText.Get("Enter a city name"), ExpandLabel : GalleryText.Get("Open location list")))),
                    Fluent.Tree(GalleryText.Get("Schedule files"), expanded.Binding,
                        [new("workspace", null, GalleryText.Get("Workspace")), new("plan", "workspace", GalleryText.Get("Plan"))])),
                options : new(Width : 440), paintBackground : false));
            var provider = sample.Preview;
            provider.Direction = UiFlowDirection.RightToLeft;
            var factory = sample.Source.Replace("UiWidget example = ", "using var example = ", StringComparison.Ordinal);
            sample.BindSource(() => (View: view.Value, Query: query.Value, Search: search.Value, Chosen: chosen.Value,
                Expanded: expanded.Value, Frequency: frequency.Value, Notify: notify.Value, Sound: sound.Value, Volume: volume.Value, provider.Direction), state =>
                $"var view = new UiState<string?>({GallerySample.String(state.View)});\n"
                + $"var query = new UiState<string>({GallerySample.String(state.Query)});\n"
                + $"var search = new UiState<string>({GallerySample.String(state.Search)});\n"
                + $"var chosen = new UiState<string?>({GallerySample.String(state.Chosen)});\n"
                + $"var frequency = new UiState<string?>({GallerySample.String(state.Frequency)});\n"
                + $"var notify = new UiState<bool>({(state.Notify ? "true" : "false")});\n"
                + $"var sound = new UiState<bool>({(state.Sound ? "true" : "false")});\n"
                + $"var volume = new UiState<float>({GallerySample.Number(state.Volume)});\n"
                + $"var expanded = new UiState<string[]>([{string.Join(", ", state.Expanded.Select(GallerySample.String))}]);\n"
                + "var placement = new UiTooltipOptions { Positioning = new() { Position = UiPopupPosition.BelowStart } };\n"
                + factory
                + $"\nexample.Direction = UiFlowDirection.{state.Direction};\nexample.Render(ui);");
            _samples.Add(sample);
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle1(GalleryText.Get("Layout direction per region")),
                Fluent.Button(GalleryText.Get("Change layout direction"), () => provider.Direction = provider.Direction == UiFlowDirection.RightToLeft
                    ? UiFlowDirection.LeftToRight : UiFlowDirection.RightToLeft),
                sample);
        }
        private UiWidget FontExample(UiTypographyFonts fonts)
        {
            var monospace = fonts.Monospace ?? throw new InvalidOperationException("Provider font demo requires a configured monospace font.");
            var localFonts = fonts with { Regular = monospace, Semibold = monospace, Bold = monospace };
            var localTheme = new UiFluentProviderTheme(UiCompiledDesign.Windows11Dark.Theme, fonts : localFonts);
            var sample = GallerySample.Create(() => Fluent.FluentProvider(
                Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Subtitle1("Local typography 123"),
                    Fluent.Body1(GalleryText.Get("Local font and input Agyp")),
                    Fluent.Input(GalleryText.Get("Font region input"), _text.Binding, 1024, 280)),
                localTheme, new(Padding : new(16), Width : 440)));
            var factory = sample.Source;
            sample.BindSource(() => (_text.Value, sample.Preview.Theme), state => TextSetup(state.Value)
                + "\nvar localFonts = new UiTypographyFonts(" + string.Join(", ", new[] { localFonts.Regular, localFonts.Semibold,
                    localFonts.Bold, localFonts.SecondaryRegular, localFonts.SecondarySemibold, localFonts.SecondaryBold }
                    .Select(path => path is null ? "null" : GallerySample.String(path)))
                + ") { Monospace = " + GallerySample.String(monospace) + " };\n"
                + "UiFluentProviderTheme? localTheme = " + (state.Theme?.Fonts is null ? "null"
                    : "new(UiCompiledDesign.Windows11Dark.Theme, fonts : localFonts)") + ";\n\n" + factory);
            _samples.Add(sample);
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Subtitle1(GalleryText.Get("Font per region")),
                Fluent.Body1(GalleryText.Get("Latin text uses a monospace font. The CJK fallback font and system text size are preserved.")),
                Fluent.Button(GalleryText.Get("Toggle the region's font"), () => sample.Preview.Theme = sample.Preview.Theme is null ? localTheme : null), sample);
        }
        private UiWidget Example(string name, UiFluentProviderTheme? theme)
        {
            var paintBackground = true;
            var sample = GallerySample.Create(() => Fluent.FluentProvider(
                Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Title3(name), Fluent.Input(string.Format(GalleryText.Get("{0} input"), name), _text.Binding, 1024, 280),
                    Fluent.Button(string.Format(GalleryText.Get("{0} button"), name)),
                    Fluent.Tooltip(Fluent.Button(string.Format(GalleryText.Get("{0} tooltip"), name)), GalleryText.Get("This tooltip also follows the region's theme."))),
                theme, new(Padding : new(16), Width : 440), paintBackground));
            var preview = sample.Preview;
            var factory = sample.Source;
            sample.BindSource(() => (_text.Value, preview.Theme, preview.PaintBackground), state =>
                TextSetup(state.Value) + "\nvar name = " + GallerySample.String(name) + ";\n"
                + "UiFluentProviderTheme? theme = " + (state.Theme is null ? "null"
                    : ReferenceEquals(state.Theme, UiFluentProviderTheme.Light) ? "UiFluentProviderTheme.Light" : "UiFluentProviderTheme.Dark")
                + ";\nvar paintBackground = " + GallerySample.Boolean(state.PaintBackground) + ";\n\n" + factory);
            _samples.Add(sample);
            return Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Format("Change theme for {0}", name), () => preview.Theme = preview.Theme is null ? UiFluentProviderTheme.Light
                        : ReferenceEquals(preview.Theme, UiFluentProviderTheme.Light) ? UiFluentProviderTheme.Dark : null),
                    Fluent.Button(GalleryText.Format("Toggle background for {0}", name), () => preview.PaintBackground = !preview.PaintBackground)),
                sample);
        }
        private static string TextSetup(string text) => "var _text = new UiState<string>(" + GallerySample.String(text) + ");";
        public void Dispose() => Root.Dispose();
    }
}
