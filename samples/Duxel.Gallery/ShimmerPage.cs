using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ShimmerPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "Shimmer";
    public string Description => "Show a placeholder while loading and transition to the actual content when it is ready.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    public void Render(UiImmediateContext ui) => (_content ??= new()).Root.Render(ui);
    public void Dispose() => _content?.Root.Dispose();
    private sealed class Content
    {
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal UiWidget Root { get; }
        internal Content()
        {
            var loading = GallerySample.Define("placeholder", () =>
            {
                var skeleton = Fluent.Skeleton(Dux.Row(new(Spacing : 12),
                    Fluent.SkeletonItem(new(Shape : UiSkeletonShape.Circle, Size : 40)),
                    Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                        Fluent.SkeletonItem(new(Width : 220)), Fluent.SkeletonItem(new(Size : 12, Width : 160)))));
                skeleton.AccessibleName = GalleryText.Get("Loading profile");
                return skeleton;
            });
            var placeholder = loading.Value;
            var sample = GallerySample.Create(() => Fluent.Shimmer(
                Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Subtitle1(GalleryText.Get("Dimoi")), Fluent.Body1(GalleryText.Get("The profile is ready.")), Fluent.Button(GalleryText.Get("Open profile"))), placeholder));
            var preview = sample.Preview;
            var factory = sample.Source.Replace("UiWidget example =", "var example =", StringComparison.Ordinal);
            sample.BindSource(() => (preview.IsDataLoaded, preview.Options.ReducedMotion), state =>
                loading.Source + "\n\n" + factory
                + "\nexample.IsDataLoaded = " + GallerySample.Boolean(state.IsDataLoaded) + ";"
                + "\nexample.Options = new(ReducedMotion : " + GallerySample.Boolean(state.ReducedMotion) + ");");
            Samples = [sample];
            Root = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                    Fluent.Button(GalleryText.Get("Toggle loading state"), () => preview.IsDataLoaded = !preview.IsDataLoaded),
                    Fluent.Button(GalleryText.Get("Toggle reduced motion"), () => preview.Options = preview.Options with { ReducedMotion = !preview.Options.ReducedMotion })),
                Fluent.Body1(GalleryText.Get("While loading, the content cannot receive input or keyboard focus. The placeholder stops animating when the content is ready.")), sample);
        }
    }
}
