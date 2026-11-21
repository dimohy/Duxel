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
                var options = new UiActivityIndicatorOptions(size, IsAnimating : true, AccessibilityLabel : GalleryText.Get("Loading document"));
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
                examples.Add(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), Fluent.Subtitle2(GalleryText.Get(size switch
                {
                    UiActivityIndicatorSize.ExtraSmall => "Extra small",
                    UiActivityIndicatorSize.Small => "Small",
                    UiActivityIndicatorSize.Medium => "Medium",
                    UiActivityIndicatorSize.Large => "Large",
                    UiActivityIndicatorSize.ExtraLarge => "Extra large",
                    _ => throw new ArgumentOutOfRangeException(nameof(size))
                })), sample));
            }
            Root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Start or stop activity"), () => previews.ForEach(preview => preview.IsAnimating = !preview.IsAnimating)),
                    Fluent.Button(GalleryText.Get("Toggle hiding when stopped"), () => previews.ForEach(preview => preview.Options = preview.Options with
                    { HidesWhenStopped = !preview.Options.HidesWhenStopped })),
                    Fluent.Button(GalleryText.Get("Change activity color"), () => previews.ForEach(preview => preview.Options = preview.Options with
                    { Color = preview.Options.Color is null ? new UiColor(71, 158, 245) : null })),
                    Fluent.Button(GalleryText.Get("Toggle reduced motion for activity"), () => previews.ForEach(preview => preview.Options = preview.Options with
                    { ReducedMotion = !preview.Options.ReducedMotion }))),
                Fluent.Body1(GalleryText.Get("The default is Fluent iOS gray. The indicator rotates a 270-degree arc of constant length.")),
                Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min), examples.ToArray()));
        }
    }
}
