using Duxel.Core;
using Duxel.Svg;

namespace Duxel.Gallery;

internal sealed class CardPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    public string Title => "카드";
    public string Description => "Type or press a button inside a card. Change its size and appearance, and navigate with Enter, Tab, and Escape.";
    internal UiFluentCard? Preview => _content?.Preview;
    internal int Executions => _content?.Executions ?? 0;
    internal int HeaderExecutions => _content?.HeaderExecutions ?? 0;
    internal int RootExecutions => _content?.RootExecutions ?? 0;
    internal bool Selected => _content?.Selected ?? false;
    internal int SelectionWrites => _content?.SelectionWrites ?? 0;
    internal bool FloatingSelected => _content?.FloatingSelected ?? false;
    internal int FloatingWrites => _content?.FloatingWrites ?? 0;
    internal int FloatingExecutions => _content?.FloatingExecutions ?? 0;
    internal int ClippedExecutions => _content?.ClippedExecutions ?? 0;
    internal bool ClippingEnabled => _content?.ClippingEnabled ?? false;
    internal string Memo => _content?.Memo ?? string.Empty;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Dispose();

    private sealed class Content : IDisposable
    {
        internal readonly List<IGallerySample> Samples = [];
        internal int Executions;
        internal int HeaderExecutions;
        internal int RootExecutions;
        internal bool Selected;
        internal int SelectionWrites;
        internal bool FloatingSelected;
        internal int FloatingWrites;
        internal int FloatingExecutions;
        private readonly UiBinding<bool> _selection;
        private readonly SvgStore _previewStore = new();
        private readonly UiCard<FlushCardPresentation> _clippedCard;
        internal int ClippedExecutions;
        internal bool ClippingEnabled => _clippedCard.Options.Size != UiCardSize.Small;
        internal string Memo = GalleryText.Get("Edit this note");
        internal UiFluentCard Preview { get; }
        internal UiWidget Root { get; }
        internal Content()
        {
            _selection = new(() => Selected, value => { Selected = value; SelectionWrites++; });
            var PreviewSample = GallerySample.Create(() => Fluent.Card(GalleryText.Get("Task card"), Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.CardHeader(Fluent.Body1Strong(GalleryText.Get("Task")),
                    Fluent.Caption1(() => string.Format(GalleryText.Get("Information actions: {0}"), HeaderExecutions)),
                    Fluent.Avatar(GalleryText.Get("Dimohy"), new(UiAvatarSize.Size24, Color: UiAvatarColor.Colorful)),
                    Fluent.Button(GalleryText.Get("Card information"), () => HeaderExecutions++)),
                Fluent.Input(GalleryText.Get("Card note"), new UiBinding<string>(() => Memo, value => Memo = value), 100, 240),
                Fluent.CardFooter([Fluent.Button(GalleryText.Get("Action inside the card"), () => Executions++)],
                    Fluent.Caption1(() => string.Format(GalleryText.Get("Card actions: {0}"), Executions)))), new(Width: 320)),
                setup: "var Memo = " + GallerySample.String(GalleryText.Get("Edit this note")) + ";\nvar Executions = 0;\nvar HeaderExecutions = 0;");
            Preview = PreviewSample.Preview;
            Samples.Add(PreviewSample);
            var floatingSelection = new UiBinding<bool>(() => FloatingSelected,
                value => { FloatingSelected = value; FloatingWrites++; });
            var floatingAction = Fluent.Checkbox("##floating-selection", floatingSelection);
            floatingAction.AccessibleName = GalleryText.Get("Select the floating card");
            var floatingCardSample = GallerySample.Create(() => Fluent.Card(GalleryText.Get("Card with a floating action"), Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Body1(GalleryText.Get("Card selection")), Fluent.Button(GalleryText.Get("Action inside the floating card"), () => FloatingExecutions++)),
                new(Width: 320, Height: 120), selection: floatingSelection, floatingAction: floatingAction),
                setup: "var FloatingSelected = false;\nvar FloatingWrites = 0;\nvar FloatingExecutions = 0;\nvar floatingSelection = new UiBinding<bool>(() => FloatingSelected, value => { FloatingSelected = value; FloatingWrites++; });\nvar floatingAction = Fluent.Checkbox(\"##floating-selection\", floatingSelection);\nfloatingAction.AccessibleName = " + GallerySample.String(GalleryText.Get("Select the floating card")) + ";");
            var floatingCard = floatingCardSample.Preview;
            Samples.Add(floatingCardSample);
            var clippedCardSample = GallerySample.Create(() => new UiCard<FlushCardPresentation>(GalleryText.Get("Card with rounded edges"), Dux.Expand(Fluent.Button(GalleryText.Get("Action inside the rounded card"), () => ClippedExecutions++)),
                new(Width: 320, Height: 120)),
                setup: "var ClippedExecutions = 0;",
                declarations: "readonly struct FlushCardPresentation : IUiCardPresentation\n{\n    public static bool CanRetainMeasurement => true;\n    public static UiCardMetrics Measure(UiCardOptions options) => new(0, options.Size == UiCardSize.Small ? 0 : 32);\n    public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options, UiCardMetrics metrics, UiButtonInteraction interaction) { }\n}");
            _clippedCard = clippedCardSample.Preview;
            Samples.Add(clippedCardSample);
            var clipping = Fluent.Button(GalleryText.Format("Rounded corners: {0}", GalleryText.Get("On")));
            clipping.Activated = () =>
            {
                _clippedCard.Options = _clippedCard.Options with { Size = ClippingEnabled ? UiCardSize.Small : UiCardSize.Medium };
                clipping.Label = GalleryText.Format("Rounded corners: {0}", GalleryText.Get(ClippingEnabled ? "On" : "Off"));
            };
            var artwork = _previewStore.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "artwork.svg"));
            var mediaCardSample = GallerySample.Create(() => Fluent.Card(GalleryText.Get("Preview card"), Dux.CardContent([
                Fluent.CardPreview(new UiSvg(artwork) { Size = new(320, 140) },
                    Fluent.Avatar(GalleryText.Get("Dimohy"), new(UiAvatarSize.Size32, Color: UiAvatarColor.Colorful))),
                Fluent.CardHeader(Fluent.Body1Strong(GalleryText.Get("SVG preview")), Fluent.Caption1(GalleryText.Get("Edge-to-edge image with a fixed logo"))),
                Fluent.CardFooter([Fluent.Button(GalleryText.Get("Preview action"), () => Executions++)])]), new(Width: 320)),
                setup: "var store = new SvgStore();\nvar artwork = store.Load(Path.Combine(AppContext.BaseDirectory, \"Assets\", \"Svg\", \"artwork.svg\"));\nvar Executions = 0;");
            var mediaCard = mediaCardSample.Preview;
            Samples.Add(mediaCardSample);
            var horizontalMediaCardSample = GallerySample.Create(() => Fluent.Card(GalleryText.Get("Horizontal preview card"), Dux.CardContent([
                Fluent.CardPreview(new UiSvg(artwork) { Size = new(180, 100) }),
                Fluent.CardHeader(Fluent.Body1Strong(GalleryText.Get("Horizontal card")), Dux.SizedBox(Fluent.Text(GalleryText.Get("The header fills the space after the image.")), width : 240))]),
                new(Width: 520, Orientation: UiCardOrientation.Horizontal)),
                setup: "var store = new SvgStore();\nvar artwork = store.Load(Path.Combine(AppContext.BaseDirectory, \"Assets\", \"Svg\", \"artwork.svg\"));");
            var horizontalMediaCard = horizontalMediaCardSample.Preview;
            Samples.Add(horizontalMediaCardSample);
            var minimumCard = Fluent.Card(GalleryText.Get("Minimum-size card"), Dux.CardContent([
                Fluent.CardPreview(new UiSvg(artwork) { Size = new(180, 80) }),
                Fluent.CardHeader(Fluent.Body1Strong(GalleryText.Get("Parent minimum size")), Dux.SizedBox(Fluent.Text(GalleryText.Get("Extra width and centered alignment")), width : 108))]),
                new(Orientation: UiCardOrientation.Horizontal));
            var minimumRootSample = GallerySample.Create(() => Dux.ConstrainedBox(minimumCard, new(340, 420, 180, 240)),
                setup: "var store = new SvgStore();\nvar artwork = store.Load(Path.Combine(AppContext.BaseDirectory, \"Assets\", \"Svg\", \"artwork.svg\"));\nvar minimumCard = Fluent.Card(" + GallerySample.String(GalleryText.Get("Minimum-size card")) + ", Dux.CardContent([\n                Fluent.CardPreview(new UiSvg(artwork) { Size = new(180, 80) }),\n                Fluent.CardHeader(Fluent.Body1Strong(" + GallerySample.String(GalleryText.Get("Parent minimum size")) + "), Dux.SizedBox(Fluent.Text(" + GallerySample.String(GalleryText.Get("Extra width and centered alignment")) + "), width : 108))]),\n                new(Orientation: UiCardOrientation.Horizontal));");
            var minimumRoot = minimumRootSample.Preview;
            Samples.Add(minimumRootSample);
            var gallery = new List<UiWidget>();
            foreach (var size in Enum.GetValues<UiCardSize>())
            foreach (var appearance in Enum.GetValues<UiCardAppearance>())
            {
                var sizeLabel = GalleryText.Get(size switch
                {
                    UiCardSize.Small => "Small",
                    UiCardSize.Medium => "Medium",
                    UiCardSize.Large => "Large",
                    _ => throw new ArgumentOutOfRangeException(nameof(size))
                });
                var appearanceLabel = GalleryText.Get(appearance switch
                {
                    UiCardAppearance.Filled => "Filled",
                    UiCardAppearance.FilledAlternative => "Alternative fill",
                    UiCardAppearance.Outline => "Outline",
                    UiCardAppearance.Subtle => "Subtle",
                    _ => throw new ArgumentOutOfRangeException(nameof(appearance))
                });
                var heading = GalleryText.Format("Comparison: {0}, {1}", sizeLabel, appearanceLabel);
                var sample = GallerySample.Create(() => Fluent.Card(heading,
                    Dux.Column(new(Spacing: 4, MainAxisSize: UiMainAxisSize.Min),
                        Fluent.Body1(appearanceLabel), Fluent.Caption1(sizeLabel)), new(size, appearance, Width: 200)),
                    setup: $"var size = UiCardSize.{size};\nvar appearance = UiCardAppearance.{appearance};\n"
                        + $"var heading = {GallerySample.String(heading)};\nvar sizeLabel = {GallerySample.String(sizeLabel)};\n"
                        + $"var appearanceLabel = {GallerySample.String(appearanceLabel)};");
                Samples.Add(sample);
                gallery.Add(sample);
            }
            Root = Dux.ScrollView(Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Change card appearance"), () => Preview.Options = Preview.Options with
                    { Appearance = (UiCardAppearance)(((int)Preview.Options.Appearance + 1) % 4) }),
                    Fluent.Button(GalleryText.Get("Change card size"), () => Preview.Options = Preview.Options with
                    { Size = (UiCardSize)(((int)Preview.Options.Size + 1) % 3) }),
                    Fluent.Button(GalleryText.Get("Change card width"), () => Preview.Options = Preview.Options with
                    { Width = Preview.Options.Width == 320 ? 240 : 320 }),
                    Fluent.Button(GalleryText.Get("Change card focus mode"), () => Preview.Options = Preview.Options with
                    { FocusMode = (UiFocusGroupMode)(((int)(Preview.Options.FocusMode ?? UiFocusGroupMode.Off) + 1) % 4) }),
                    Fluent.Button(GalleryText.Get("Toggle the card surface"), () => Preview.Options = Preview.Options with
                    { Disabled = !Preview.Options.Disabled }),
                    Fluent.Button(GalleryText.Get("Toggle the card's own action"), () => Preview.Activated = Preview.Activated is null ? () => RootExecutions++ : null),
                    Fluent.Button(GalleryText.Get("Toggle card selection mode"), () => Preview.Selection = Preview.Selection is null ? _selection : null)),
                Fluent.Body1(() => GalleryText.Format("Card surface actions: {0}", RootExecutions)),
                Fluent.Body1(() => GalleryText.Format("Selection mode: {0} / Changes: {1}",
                    GalleryText.Get(Preview.Selection is null ? "Off" : Selected ? "Selected" : "Not selected"), SelectionWrites)),
                Fluent.Body1(() => Preview.Selection is not null ? GalleryText.Get("Focus: Enter or Space toggles the selection checkbox. The actions inside the card stay independent.")
                    : (Preview.Options.FocusMode ?? (Preview.Activated is null ? UiFocusGroupMode.Off : UiFocusGroupMode.NoTab)) switch
                {
                    UiFocusGroupMode.NoTab => GalleryText.Get("Focus: Enter moves inside. Tab cycles within the card. Escape returns to the card."),
                    UiFocusGroupMode.TabExit => GalleryText.Get("Focus: Enter moves inside. Tab from the last control leaves the card. Escape returns to the card."),
                    UiFocusGroupMode.TabOnly => GalleryText.Get("Focus: Tab moves inside. Escape returns to the card."),
                    _ => GalleryText.Get("Focus skips the card surface and moves to its controls.")
                }),
                Fluent.Body1(() => Preview.Options.Disabled ? GalleryText.Get("Card surface disabled. Inputs and buttons inside still work.") : GalleryText.Get("Card surface enabled.")),
                Fluent.Text(GalleryText.Get("Inputs and buttons inside the card work independently and keep their state when you return to the page.")),
                Fluent.Text(GalleryText.Get("The card surface handles selection and activation separately from the controls inside. Add a header, footer, or floating action. The preview can fill the card with an image and overlay a logo.")),
                PreviewSample,
                Fluent.Subtitle1(GalleryText.Get("Floating selection action")),
                Fluent.Caption1(() => GalleryText.Format("Floating selection: {0} / Changes: {1} / Inner actions: {2}",
                    GalleryText.Get(FloatingSelected ? "Selected" : "Not selected"), FloatingWrites, FloatingExecutions)),
                Fluent.Text(GalleryText.Get("The checkbox and card background change the selection. The button inside runs independently.")),
                floatingCardSample,
                Fluent.Subtitle1(GalleryText.Get("Rounded clipping and input")),
                clipping,
                Fluent.Caption1(() => GalleryText.Format("Rounded card actions: {0}", ClippedExecutions)),
                Fluent.Text(GalleryText.Get("The button fills the card without padding. Clipped corners ignore clicks. The center of the button still works.")),
                clippedCardSample,
                Fluent.Subtitle1(GalleryText.Get("Media preview")),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Change preview width"), () => mediaCard.Options = mediaCard.Options with
                        { Width = mediaCard.Options.Width == 320 ? 400 : 320 }),
                    Fluent.Button(GalleryText.Get("Change preview size"), () => mediaCard.Options = mediaCard.Options with
                        { Size = (UiCardSize)(((int)mediaCard.Options.Size + 1) % 3) })),
                Fluent.Text(GalleryText.Get("The SVG is loaded once and reused. The 32 px logo keeps a 12 px inset from the left and bottom.")),
                mediaCardSample,
                horizontalMediaCardSample,
                Fluent.Subtitle1(GalleryText.Get("Minimum size of the parent")),
                Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Change the parent's minimum width"), () => minimumRoot.Options = minimumRoot.Options with
                        { MinWidth = minimumRoot.Options.MinWidth == 340 ? 400 : 340 }),
                    Fluent.Button(GalleryText.Get("Change the minimum-size card's orientation"), () => minimumCard.Options = minimumCard.Options with
                        { Orientation = minimumCard.Options.Orientation == UiCardOrientation.Horizontal
                            ? UiCardOrientation.Vertical : UiCardOrientation.Horizontal })),
                Fluent.Text(GalleryText.Get("The header fills the remaining width and keeps its minimum size. The image is centered vertically.")),

                minimumRootSample,
                Dux.Column(new(Spacing: 24, MainAxisSize: UiMainAxisSize.Min, CrossAxisAlignment: UiCrossAxisAlignment.Stretch), gallery.ToArray())));
            Root.AccessibleName = GalleryText.Get("Card examples");
        }
        private readonly struct FlushCardPresentation : IUiCardPresentation
        {
            public static bool CanRetainMeasurement => true;
            public static UiCardMetrics Measure(UiCardOptions options) => new(0, options.Size == UiCardSize.Small ? 0 : 32);
            // The owned button supplies the fill so the sample exposes the actual clip boundary.
            public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options, UiCardMetrics metrics, UiButtonInteraction interaction) { }
        }
        public void Dispose() { Root.Dispose(); _previewStore.Dispose(); }
    }
}
