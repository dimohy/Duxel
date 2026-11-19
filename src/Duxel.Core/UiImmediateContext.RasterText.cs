namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    // Raster coverage is already sampled on a physical pixel grid. Translate the completed
    // bitmap as a unit; rounding individual glyph positions would destroy native shaping.
    private UiVector2 RasterTextOrigin(float x, float y, bool pixelSnap)
        => pixelSnap ? new(MathF.Round(x * _contentScale) / _contentScale,
            MathF.Round(y * _contentScale) / _contentScale) : new(x, y);
}
