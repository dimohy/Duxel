namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    public void Triangle(UiVector2 a, UiVector2 b, UiVector2 c, UiColor color) =>
        _ui.PaintControlTriangle(a, b, c, color);
}

public sealed partial class UiImmediateContext
{
    internal void PaintControlTriangle(UiVector2 a, UiVector2 b, UiVector2 c, UiColor color) =>
        _builder.AddTriangleFilled(a, b, c, color, _whiteTexture, CurrentClipRect);
}
