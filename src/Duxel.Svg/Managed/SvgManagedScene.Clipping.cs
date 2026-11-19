using System.Numerics;

namespace Duxel.Svg.Managed;

internal sealed partial class SvgManagedScene
{
    private static bool TryCompositeClip(Node node, Matrix3x2 transform, out Vector4 rectangle)
    {
        rectangle = default;
        // Keep geometric coverage outside the intermediate opacity layer's RGBA8 storage.
        return node.Opacity != 1 && node.Mask is null && node.Filter is null && node.Clip is { } clip &&
            TryClipRectangle(clip, clip.Transform * transform, out rectangle);
    }

    private static Node FoldRectangularClip(Node node)
    {
        if (node.Clip is not { } clip || node.Opacity != 1 || node.Mask is not null || node.Filter is not null ||
            !TryClipRectangle(clip, clip.Transform, out var clipBounds) ||
            !TryIntersectPaint(node, Matrix3x2.Identity, true, out var clipped))
            return node;
        return clipped with
        {
            Clip = null
        };

        bool TryIntersectPaint(Node current, Matrix3x2 frame, bool root, out Node result)
        {
            result = current;
            if (!current.Visible || !root && (current.Opacity != 1 || current.Clip is not null) ||
                current.Mask is not null || current.Filter is not null || current.Stroke is not null)
                return false;
            if (current.Fill is { } geometry && current.Children.Length == 0)
            {
                if (!SvgRectangleGeometry.TryBounds(geometry, frame, out var bounds) || !Matrix3x2.Invert(frame, out var inverse))
                    return false;
                var left = Math.Max(bounds.X, clipBounds.X);
                var top = Math.Max(bounds.Y, clipBounds.Y);
                var right = Math.Min(bounds.X + bounds.Z, clipBounds.X + clipBounds.Z);
                var bottom = Math.Min(bounds.Y + bounds.W, clipBounds.Y + clipBounds.W);
                if (right <= left || bottom <= top)
                {
                    result = current with
                    {
                        Visible = false,
                        ObjectBounds = current.Bounds
                    };
                    return true;
                }
                var first = Vector2.Transform(new(left, top), inverse);
                var intersection = new SvgGeometry([
                    new(SvgSegmentKind.Move, first),
                    new(SvgSegmentKind.Line, Vector2.Transform(new(right, top), inverse)),
                    new(SvgSegmentKind.Line, Vector2.Transform(new(right, bottom), inverse)),
                    new(SvgSegmentKind.Line, Vector2.Transform(new(left, bottom), inverse)),
                    new(SvgSegmentKind.Close, first)
                ]);
                // Paint coordinates and object bounds still belong to the authored geometry.
                result = current with
                {
                    Fill = intersection,
                    ObjectBounds = current.Bounds
                };
                return true;
            }
            if (current.Fill is not null || current.Children.Length != 1)
                return false;
            var child = current.Children[0];
            if (!TryIntersectPaint(child, child.Transform * frame, false, out var clippedChild))
                return false;
            result = current with
            {
                Children = [clippedChild],
                ObjectBounds = current.Bounds
            };
            return true;
        }
    }

    private static bool TryClipRectangle(Node node, Matrix3x2 frame, out Vector4 bounds)
    {
        bounds = default;
        if (!node.Visible || node.Opacity != 1 || node.Clip is not null || node.Mask is not null ||
            node.Filter is not null || node.Stroke is not null)
            return false;
        if (node.Fill is { } geometry && node.Children.Length == 0)
            return SvgRectangleGeometry.TryBounds(geometry, frame, out bounds);
        return node.Fill is null && node.Children.Length == 1 &&
            TryClipRectangle(node.Children[0], node.Children[0].Transform * frame, out bounds);
    }
}
