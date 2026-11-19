using System.Numerics;

namespace Duxel.Svg.Managed;

internal sealed partial class SvgManagedScene
{
    private static Vector4? PaintBounds(Node node, Matrix3x2 parent)
    {
        if (!node.Visible || node.Opacity == 0)
            return null;
        var transform = node.Transform * parent;
        if (transform.GetDeterminant() == 0)
            return null;
        if (node.Filter is { } filter)
            return TransformBounds(filter.Region.Bounds, transform);
        Vector4? result = null;
        void Include(Vector4 rectangle)
        {
            if (rectangle.Z <= 0 || rectangle.W <= 0)
                return;
            if (result is not { } previous)
                result = rectangle;
            else
            {
                var minimum = Vector2.Min(new(previous.X, previous.Y), new(rectangle.X, rectangle.Y));
                var maximum = Vector2.Max(new(previous.X + previous.Z, previous.Y + previous.W),
                    new(rectangle.X + rectangle.Z, rectangle.Y + rectangle.W));
                result = new(minimum.X, minimum.Y, maximum.X - minimum.X, maximum.Y - minimum.Y);
            }
        }
        if (node.Fill is { } fill)
            Include(TransformBounds(fill.Bounds, transform));
        if (node.Stroke is { } stroke)
        {
            var bounds = stroke.CompileGpu(transform).Bounds;
            Include(new(bounds.X, bounds.Y, bounds.Z - bounds.X, bounds.W - bounds.Y));
        }
        foreach (var child in node.Children)
            if (PaintBounds(child, transform) is { } bounds)
                Include(bounds);
        return result;
    }
}
