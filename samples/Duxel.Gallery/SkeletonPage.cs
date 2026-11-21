using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class SkeletonPage : UiWidget, IGallerySourcePage
{
    private readonly List<IGallerySample> _samples = [];
    public IReadOnlyList<IGallerySample> Samples => _samples;
    private readonly UiState<bool> _loading = new(true);
    private readonly UiState<bool> _pulse = new(false);
    private readonly UiState<bool> _translucent = new(false);
    private readonly UiState<bool> _reduced = new(false);
    private readonly UiState<bool> _paused = new(false);

    protected override IUiView Build(UiBuildContext context)
    {
        _samples.Clear();
        var loading = context.Watch(_loading);
        var pulse = context.Watch(_pulse);
        var translucent = context.Watch(_translucent);
        var reduced = context.Watch(_reduced);
        var paused = context.Watch(_paused);
        var previewDefinition = GallerySample.Define<Func<int, UiWidget>>("preview", () => (int size) =>
        {
            var item = Fluent.Skeleton(Dux.Row(new(Spacing : 16, CrossAxisAlignment : UiCrossAxisAlignment.Center),
                Fluent.SkeletonItem(new(Size : size, Shape : UiSkeletonShape.Circle)),
                Dux.Expanded(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.SkeletonItem(), Fluent.SkeletonItem(new(Width : 180)),
                    Fluent.SkeletonItem(new(Size : 12, Width : 120))))),
                new(Animation : pulse ? UiSkeletonAnimation.Pulse : UiSkeletonAnimation.Wave,
                    Appearance : translucent ? UiSkeletonAppearance.Translucent : UiSkeletonAppearance.Opaque,
                    ReducedMotion : reduced, Paused : paused));
            item.AccessibleName = GalleryText.Get("Loading profile");
            return item;
        });
        var preview = previewDefinition.Value;
        string Source(string factory) => $"var loading = {GallerySample.Boolean(loading)};\nvar pulse = {GallerySample.Boolean(pulse)};\n"
            + $"var translucent = {GallerySample.Boolean(translucent)};\nvar reduced = {GallerySample.Boolean(reduced)};\nvar paused = {GallerySample.Boolean(paused)};\n\n"
            + previewDefinition.Source + "\n\n" + factory;
        var small = GallerySample.Create<UiWidget>(() => loading ? preview(48) : Fluent.Body1(GalleryText.Get("Dimoi · Profile loaded.")));
        var smallFactory = Source(small.Source);
        small.BindSource(() => smallFactory, value => value);
        var large = GallerySample.Create<UiWidget>(() => loading ? preview(96) : Fluent.Body1(GalleryText.Get("The content is ready to use.")));
        var largeFactory = Source(large.Source);
        large.BindSource(() => largeFactory, value => value);
        _samples.AddRange([small, large]);
        return Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(GalleryText.Get("Preview the layout of content while it loads. Items that are offscreen or paused do not request animation frames.")),
            Dux.Wrap(new(Spacing : 16, RunSpacing : 8),
                Fluent.Switch(GalleryText.Get("Loading"), _loading.Binding), Fluent.Switch(GalleryText.Get("Pulse"), _pulse.Binding),
                Fluent.Switch(GalleryText.Get("Translucent"), _translucent.Binding), Fluent.Switch(GalleryText.Get("Reduce motion"), _reduced.Binding),
                Fluent.Switch(GalleryText.Get("Pause animation"), _paused.Binding)),
            small,
            large,
            Fluent.Caption1(GalleryText.Get("Placeholders shown together share the same animation timing.")));
    }
}
