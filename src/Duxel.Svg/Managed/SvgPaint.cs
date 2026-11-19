using System.Numerics;

namespace Duxel.Svg.Managed;

internal interface ISvgPaint
{
    bool RequiresSourcePrecision => false;
    SvgGpuPaintData CompileGpu() => throw new NotSupportedException("This paint has no GPU vector representation yet.");
    Vector4 Sample(Vector2 point);
    ISvgPaint WithRasterOrigin(Vector2 origin) => this;
    ISvgPaint Bind(Matrix3x2 transform)
    {
        if (!Matrix3x2.Invert(transform, out var inverse))
            throw new InvalidOperationException("Cannot bind paint to a singular transform.");
        return new SvgTransformedPaint(this, inverse, 1);
    }
}
internal sealed record SvgSolidPaint(Vector4 Color) : ISvgPaint
{
    public SvgGpuPaintData CompileGpu() => new(0, Color, default, Matrix3x2.Identity, []);
    public Vector4 Sample(Vector2 point) => Color;
}
internal readonly record struct SvgGradientStop(float Offset, Vector4 Color);
internal sealed class SvgGradientPaint(bool radial, Vector2 start, Vector2 end, float radius,
    SvgGradientStop[] stops, Matrix3x2 coordinates, string spread) : ISvgPaint
{
    // Keep linear stop selection stable across a raster row, matching the reference browser layout.
    private const float LinearPositionBias = .00001f;

    public SvgGpuPaintData CompileGpu() => new(radial ? 2 : 1, new(start.X, start.Y, end.X, end.Y),
        new(radius, 0, 0, spread switch
        {
            "pad" => 0,
            "repeat" => 1,
            "reflect" => 2,
            _ => throw new InvalidDataException("Unknown gradient spread.")
        }),
        coordinates, stops);
    public Vector4 Sample(Vector2 point)
    {
        var local = Vector2.Transform(point, coordinates);
        float position;
        if (radial)
        {
            var ray = local - start;
            var focus = start - end;
            var a = (double)Vector2.Dot(ray, ray);
            var b = 2d * Vector2.Dot(focus, ray);
            var c = (double)Vector2.Dot(focus, focus) - (double)radius * radius;
            var root = a == 0 ? double.PositiveInfinity : (-b + Math.Sqrt(Math.Max(0, b * b - 4 * a * c))) / (2 * a);
            position = root <= 0 ? 1 : (float)(1 / root);
        }
        else
        {
            var direction = end - start;
            var length = direction.LengthSquared();
            position = length == 0 ? 1 : Vector2.Dot(local - start, direction) / length + LinearPositionBias;
        }
        position = spread switch
        {
            "repeat" => position - MathF.Floor(position),
            "reflect" => 1 - MathF.Abs((position - MathF.Floor(position / 2) * 2) - 1),
            "pad" => Math.Clamp(position, 0, 1),
            _ => throw new InvalidDataException("Invalid SVG gradient spread method.")
        };
        if (stops.Length == 0)
            return Vector4.Zero;
        if (position < stops[0].Offset)
            return stops[0].Color;
        for (var i = 1; i < stops.Length; i++)
        {
            if (position >= stops[i].Offset)
                continue;
            var previous = stops[i - 1];
            var next = stops[i];
            return Vector4.Lerp(previous.Color, next.Color, (position - previous.Offset) / (next.Offset - previous.Offset));
        }
        return stops[^1].Color;
    }
}
internal sealed record SvgTransformedPaint(ISvgPaint Source, Matrix3x2 Inverse, float Opacity) : ISvgPaint
{
    public bool RequiresSourcePrecision => Source.RequiresSourcePrecision;
    public SvgGpuPaintData CompileGpu()
    {
        var value = Source.CompileGpu();
        return value with
        {
            Coordinates = Inverse * value.Coordinates,
            Opacity = value.Opacity * Opacity
        };
    }
    public Vector4 Sample(Vector2 point)
    {
        var value = Source.Sample(Vector2.Transform(point, Inverse));
        value.W *= Opacity;
        return value;
    }
}
internal sealed record SvgOpacityPaint(ISvgPaint Source, float Opacity) : ISvgPaint
{
    public bool RequiresSourcePrecision => Source.RequiresSourcePrecision;
    public SvgGpuPaintData CompileGpu()
    {
        var value = Source.CompileGpu();
        return value with
        {
            Opacity = value.Opacity * Opacity
        };
    }
    public Vector4 Sample(Vector2 point)
    {
        var color = Source.Sample(point);
        color.W *= Opacity;
        return color;
    }
    public ISvgPaint Bind(Matrix3x2 transform) => new SvgOpacityPaint(Source.Bind(transform), Opacity);
}
