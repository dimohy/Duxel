using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class LayoutIntegrationPage : UiWidget, IGallerySourcePage
{
    private readonly UiWidget _root;
    public IReadOnlyList<IGallerySample> Samples { get; }
    internal LayoutIntegrationPage()
    {
        var tabs = new TabOverflowExamples();
        var overflow = new OverflowExamples();
        Samples = [.. tabs.Samples, .. overflow.Samples];
        _root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(GalleryText.Get("Combine tabs and menus. Check what happens when space runs out and whether items retain their state when shown again.")), tabs, overflow);
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}
