using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class CarouselPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new();
    public string Title => "Carousel";
    public string Description => "Browse slides using the previous and next buttons, page names, or thumbnails.";
    public IReadOnlyList<IGallerySample> Samples => State.Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => State.Root;
    public void Render(UiImmediateContext ui) => State.Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();

    private sealed class Content
    {
        internal IReadOnlyList<IGallerySample> Samples { get; } = [CreateSample(false), CreateSample(true)];
        internal UiWidget Root { get; }

        internal Content()
        {
            Root = Dux.Column(new(Spacing: 16, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Title3(GalleryText.Get("Step selection")), (UiWidget)Samples[0],
                Fluent.Title3(GalleryText.Get("Image previews and autoplay")), (UiWidget)Samples[1]);
        }
    }

    private static GallerySample<UiFluentCarousel> CreateSample(bool previews)
    {
        var selected = new UiState<string>("sea");
        var opened = new UiState<int>(0);
        (string Key, string Label, byte R, byte G, byte B)[] slides =
        [
            ("sea", GalleryText.Get("Sea"), 16, 110, 190),
            ("forest", GalleryText.Get("Forest"), 16, 124, 65),
            ("sunset", GalleryText.Get("Sunset"), 219, 98, 38)
        ];
        var label = previews ? GalleryText.Get("Image collection") : GalleryText.Get("Destinations");
        var options = new UiCarouselOptions
        {
            PreviousLabel = GalleryText.Get("Previous slide"), NextLabel = GalleryText.Get("Next slide"), AutoPlayLabel = GalleryText.Get("Autoplay"),
            Wrap = previews, AutoPlayInterval = previews ? TimeSpan.FromSeconds(4) : null
        };
        var chooseLabel = GalleryText.Get("Choose {0}");
        var selectionCount = GalleryText.Get("Selections: {0}");
        var sample = GallerySample.Create(() => Fluent.Carousel(label, selected.Binding,
            slides.Select(item =>
            {
                var image = new UiImageSource(new(1, 1, [item.R, item.G, item.B, 255]));
                var body = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                    new UiImage(image) { Size = new(420, 72) },
                    Fluent.Button(string.Format(chooseLabel, item.Label), () => opened.Value++),
                    Fluent.Body1(() => string.Format(selectionCount, opened.Value)));
                return new UiCarouselSlide(item.Key, item.Label, body, previews ? image : null);
            }).ToArray(), options));
        sample.BindSource(() => (Key: selected.Value, Opened: opened.Value, sample.Preview.IsPlaying), state =>
        {
            var entries = string.Join(",\n", slides.Select(item =>
                $"    Slide({GallerySample.String(item.Key)}, {GallerySample.String(item.Label)}, {item.R}, {item.G}, {item.B})"));
            return $"var selected = new UiState<string>({GallerySample.String(state.Key)});\n"
                + $"var opened = new UiState<int>({state.Opened});\n"
                + "UiCarouselSlide Slide(string key, string label, byte r, byte g, byte b)\n{\n"
                + "    var image = new UiImageSource(new(1, 1, [r, g, b, 255]));\n"
                + "    var body = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),\n"
                + "        new UiImage(image) { Size = new(420, 72) },\n"
                + $"        Fluent.Button(string.Format({GallerySample.String(chooseLabel)}, label), () => opened.Value++),\n"
                + $"        Fluent.Body1(() => string.Format({GallerySample.String(selectionCount)}, opened.Value)));\n"
                + $"    return new UiCarouselSlide(key, label, body{(previews ? ", image" : "")});\n}}\n"
                + $"using var carousel = Fluent.Carousel({GallerySample.String(label)}, selected.Binding, new UiCarouselSlide[]\n{{\n{entries}\n}},\n"
                + "    new()\n    {\n"
                + $"        PreviousLabel = {GallerySample.String(options.PreviousLabel)}, NextLabel = {GallerySample.String(options.NextLabel)},\n"
                + $"        AutoPlayLabel = {GallerySample.String(options.AutoPlayLabel)},\n"
                + (previews ? "        Wrap = true, AutoPlayInterval = TimeSpan.FromSeconds(4)\n" : "")
                + "    });\n"
                + (previews ? $"carousel.IsPlaying = {GallerySample.Boolean(state.IsPlaying)};\n" : "")
                + "carousel.Render(ui);";
        });
        return sample;
    }
}
