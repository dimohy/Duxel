using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class CarouselPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    private Content State => _content ??= new();
    public string Title => "Carousel";
    public string Description => "이전·다음 버튼, 페이지 이름, 미리보기로 슬라이드를 넘겨 보세요.";
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
                Fluent.Title3("단계 선택"), (UiWidget)Samples[0],
                Fluent.Title3("이미지 미리보기와 자동 재생"), (UiWidget)Samples[1]);
        }
    }

    private static GallerySample<UiFluentCarousel> CreateSample(bool previews)
    {
        var selected = new UiState<string>("sea");
        var opened = new UiState<int>(0);
        (string Key, string Label, byte R, byte G, byte B)[] slides =
        [
            ("sea", "바다", 16, 110, 190),
            ("forest", "숲", 16, 124, 65),
            ("sunset", "노을", 219, 98, 38)
        ];
        var label = previews ? "이미지 모음" : "여행지";
        var options = new UiCarouselOptions
        {
            PreviousLabel = "이전 슬라이드", NextLabel = "다음 슬라이드", AutoPlayLabel = "자동 재생",
            Wrap = previews, AutoPlayInterval = previews ? TimeSpan.FromSeconds(4) : null
        };
        var sample = GallerySample.Create(() => Fluent.Carousel(label, selected.Binding,
            slides.Select(item =>
            {
                var image = new UiImageSource(new(1, 1, [item.R, item.G, item.B, 255]));
                var body = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                    new UiImage(image) { Size = new(420, 72) },
                    Fluent.Button(item.Label + " 선택", () => opened.Value++),
                    Fluent.Body1(() => $"선택 횟수 {opened.Value}회"));
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
                + "        Fluent.Button(label + \" 선택\", () => opened.Value++),\n"
                + "        Fluent.Body1(() => $\"선택 횟수 {opened.Value}회\"));\n"
                + $"    return new UiCarouselSlide(key, label, body{(previews ? ", image" : "")});\n}}\n"
                + $"using var carousel = Fluent.Carousel({GallerySample.String(label)}, selected.Binding, new UiCarouselSlide[]\n{{\n{entries}\n}},\n"
                + "    new()\n    {\n        PreviousLabel = \"이전 슬라이드\", NextLabel = \"다음 슬라이드\", AutoPlayLabel = \"자동 재생\",\n"
                + (previews ? "        Wrap = true, AutoPlayInterval = TimeSpan.FromSeconds(4)\n" : "")
                + "    });\n"
                + (previews ? $"carousel.IsPlaying = {GallerySample.Boolean(state.IsPlaying)};\n" : "")
                + "carousel.Render(ui);";
        });
        return sample;
    }
}
