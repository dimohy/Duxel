using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class TooltipPage : UiWidget, IGallerySourcePage
{
    private readonly UiWidget _root;
    private readonly UiFluentTooltip _controlled;
    internal UiFluentTooltip Controlled => _controlled;
    public IReadOnlyList<IGallerySample> Samples { get; }

    internal TooltipPage()
    {
        var normal = GallerySample.Create(() => Fluent.Tooltip(Fluent.Button(GalleryText.Get("Tooltip target")),
            GalleryText.Get("Hover over this target or focus it with the keyboard.")));
        var arrow = GallerySample.Create(() => Fluent.Tooltip(Fluent.Button(GalleryText.Get("Arrow example target")), GalleryText.Get("This tooltip has an arrow."), new()
        {
            Positioning = new()
            {
                ArrowHeight = 6
            }
        }));
        var label = GallerySample.Create(() => Fluent.Tooltip(Fluent.Button("?"), GalleryText.Get("Details"), new()
        {
            Relationship = UiTooltipRelationship.Label
        }));
        var controlledDefinition = GallerySample.Define("controlled", () =>
        {
            var tooltip = Fluent.Tooltip(Fluent.Button(GalleryText.Get("Controlled target")), GalleryText.Get("Code controls whether this tooltip is shown."));
            tooltip.Visible = false;
            tooltip.VisibleChangeRequested = change => tooltip.Visible = change.Visible;
            return tooltip;
        });
        var controlled = _controlled = controlledDefinition.Value;
        var manual = GallerySample.Create(() => Dux.Wrap(new(Spacing : 24, RunSpacing : 12, CrossAlignment : UiWrapCrossAlignment.Center),
            controlled, Fluent.Button(GalleryText.Get("Toggle tooltip visibility"), () => controlled.Visible = controlled.Visible != true)));
        var manualFactory = manual.Source;
        manual.BindSource(() => controlled.Visible, visible => controlledDefinition.Source
            + $"\ncontrolled.Visible = {GallerySample.Boolean(visible is true)};\n\n" + manualFactory);
        Samples = [normal, arrow, label, manual];
        _root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(GalleryText.Get("Hover or use Tab to focus a target and wait for the tooltip. Press Escape to dismiss it.")),
            Fluent.Subtitle2(GalleryText.Get("Automatic tooltip")), normal,
            Fluent.Subtitle2(GalleryText.Get("Tooltip with an arrow")), arrow,
            Fluent.Subtitle2(GalleryText.Get("Accessible name of the target")),
            Fluent.Caption1(GalleryText.Get("Description mode provides HelpText. Label mode provides Name, the accessible name of the target.")), label,
            Fluent.Subtitle2(GalleryText.Get("Control visibility from code")), manual);
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}
