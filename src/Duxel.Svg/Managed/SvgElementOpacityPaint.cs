using System.Numerics;

namespace Duxel.Svg.Managed;

/// <summary>Folded single-draw element opacity keeps color precision until paint storage.</summary>
internal sealed record SvgElementOpacityPaint(ISvgPaint Source, float Opacity) : ISvgPaint
{
    public bool RequiresSourcePrecision => Source.RequiresSourcePrecision;
    public SvgGpuPaintData CompileGpu()
    {
        var value = Source.CompileGpu();
        var parameters = value.Parameters;
        parameters.W += 8;
        return value with
        {
            Parameters = parameters,
            Opacity = value.Opacity * Opacity
        };
    }

    public Vector4 Sample(Vector2 point)
    {
        var color = Source.Sample(point);
        var rawAlpha = color.W * Opacity;
        var alpha = SvgCompositing.Channel(SvgShaderColor.QuantizeChannel(rawAlpha)) / 255f;
        if (alpha == 0)
            return Vector4.Zero;
        var scale = rawAlpha / alpha;
        return new(Math.Min(color.X * scale, 1), Math.Min(color.Y * scale, 1), Math.Min(color.Z * scale, 1), alpha);
    }

    public ISvgPaint Bind(Matrix3x2 transform) => new SvgElementOpacityPaint(Source.Bind(transform), Opacity);
}
