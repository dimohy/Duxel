using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class SvgBrowserPair : UiControlWidget
{
    private const float Spacing = 8;
    private readonly UiFluentImage _reference;
    private readonly UiSvg _vector;
    private readonly float _rasterScale;
    private readonly Func<bool> _includeBackground;
    private readonly UiViewHost _host;
    internal (UiRect Reference, UiRect Vector) Bounds { get; private set; }
    internal SvgBrowserPair(UiFluentImage reference, UiSvg vector, float rasterScale, Func<bool> includeBackground)
    {
        _reference = reference;
        _vector = vector;
        _rasterScale = rasterScale;
        _includeBackground = includeBackground;
        _host = new(new UiView(RenderPair));
        OwnChild("reference", reference);
        OwnChild("vector", vector);
        OwnChild("host", _host);
    }
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        var constraints = new UiBoxConstraints();
        var reference = context.MeasureConstrained(_reference, constraints);
        var vector = context.MeasureConstrained(_vector, constraints);
        return context.LayoutConstraints.Constrain(new(reference.X + Spacing + vector.X, MathF.Max(reference.Y, vector.Y)));
    }
    protected override void RenderControl(UiImmediateContext ui) => _host.Render(ui);
    private void RenderPair(UiImmediateContext ui)
    {
        _vector.Background = _includeBackground() ? new(255, 255, 255) : default;
        var measured = _vector.MeasureConstrained(ui, new UiBoxConstraints());
        var referencePosition = ui.GetCursorScreenPos();
        var referenceSize = _reference.MeasureConstrained(ui, new UiBoxConstraints());
        _reference.Render(ui);
        ui.SameLine(Spacing);
        _vector.RasterScale = _rasterScale / ui.ContentScale;
        var position = ui.GetCursorScreenPos();
        Bounds = (new(referencePosition.X, referencePosition.Y, referenceSize.X, referenceSize.Y),
            new(position.X, position.Y, measured.X, measured.Y));
        ui.GetWindowDrawList().AddRectFilled(Bounds.Vector, new(255, 255, 255), ui.WhiteTextureId);
        _vector.Render(ui);
    }
}
