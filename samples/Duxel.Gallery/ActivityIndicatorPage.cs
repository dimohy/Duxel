using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ActivityIndicatorPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "ActivityIndicator";
    public string Description => "Try the Fluent iOS activity indicator. The layout keeps its space when it stops, whether the indicator remains visible or is hidden.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        internal List<IGallerySample> Samples { get; } = [];
        internal UiWidget Root { get; }
        internal Content()
        {
            var previews = new List<UiFluentActivityIndicator>();
            var examples = new List<UiWidget>();
            foreach (var size in new[] { UiActivityIndicatorSize.ExtraSmall, UiActivityIndicatorSize.Small,
                UiActivityIndicatorSize.Medium, UiActivityIndicatorSize.Large, UiActivityIndicatorSize.ExtraLarge })
            {
                var options = new UiActivityIndicatorOptions(size, IsAnimating : true, AccessibilityLabel : "문서를 불러오는 중");
                var sample = GallerySample.Create(() => Fluent.ActivityIndicator(options));
                var preview = sample.Preview;
                previews.Add(preview);
                var factory = sample.Source;
                sample.BindSource(() => preview.Options, state =>
                    "var options = new UiActivityIndicatorOptions(Size : UiActivityIndicatorSize." + state.Size
                    + ",\n    IsAnimating : " + GallerySample.Boolean(state.IsAnimating)
                    + ", HidesWhenStopped : " + GallerySample.Boolean(state.HidesWhenStopped)
                    + ",\n    Color : " + (state.Color is { } color ? "new UiColor(0x" + color.Rgba.ToString("X8") + "u)" : "null")
                    + ", ReducedMotion : " + GallerySample.Boolean(state.ReducedMotion)
                    + ",\n    AccessibilityLabel : " + GallerySample.String(state.AccessibilityLabel) + ");\n\n" + factory);
                Samples.Add(sample);
                examples.Add(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), Fluent.Subtitle2(size.ToString()), sample));
            }
            Root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button("활동 시작·중지", () => previews.ForEach(preview => preview.IsAnimating = !preview.IsAnimating)),
                    Fluent.Button("정지 시 숨김 전환", () => previews.ForEach(preview => preview.Options = preview.Options with
                    { HidesWhenStopped = !preview.Options.HidesWhenStopped })),
                    Fluent.Button("활동 색상 전환", () => previews.ForEach(preview => preview.Options = preview.Options with
                    { Color = preview.Options.Color is null ? new UiColor(71, 158, 245) : null })),
                    Fluent.Button("활동 모션 줄이기", () => previews.ForEach(preview => preview.Options = preview.Options with
                    { ReducedMotion = !preview.Options.ReducedMotion }))),
                Fluent.Body1("기본 색상은 Fluent iOS의 회색입니다. 270도 원호를 회전하며, 웹 Spinner처럼 원호의 길이를 늘였다 줄이지 않습니다."),
                Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min), examples.ToArray()));
        }
    }
}
