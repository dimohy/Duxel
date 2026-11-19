namespace Duxel.Core;

/// <summary>One owned body slot, either naturally scrolling or filling a finite viewport supplied to its child.</summary>
internal sealed class UiSurfaceContent : UiControlWidget
{
    private readonly UiScrollView _scroll;
    internal UiScrollView Scroll => _scroll;
    internal bool Stretch { get; private set; }
    internal UiWidget Child => _scroll.Child;
    internal UiVector2 Offset { get => _scroll.Offset; set => _scroll.Offset = value; }
    internal UiSurfaceContent(UiWidget child)
    {
        _scroll = Dux.ScrollView(child);
        OwnChild("scroll", _scroll);
    }
    internal void SetStretch(bool value)
    {
        if (Stretch == value) return;
        Stretch = value;
        RequestRebuild();
    }
    protected override bool CanRetainMeasurement => CanRetainOwnedMeasurement(Stretch ? Child : _scroll);
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureParentDataChild(Stretch ? Child : _scroll,
        null, context.LayoutConstraints);
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderSurfaceContent(this);
}
public sealed partial class UiImmediateContext
{
    internal void RenderSurfaceContent(UiSurfaceContent content)
    {
        var context = new UiControlContext(this);
        var size = context.LayoutConstraints.Validate();
        var child = content.Stretch ? content.Child : content.Scroll;
        var measured = context.MeasureParentDataChild(child, null, size);
        var origin = AllocateLayoutBox(measured);
        var bounds = new UiRect(origin.X, origin.Y, measured.X, measured.Y);
        RenderParentDataChild(child, null, bounds, bounds);
        FinishLayoutBox(origin, measured);
    }
}
