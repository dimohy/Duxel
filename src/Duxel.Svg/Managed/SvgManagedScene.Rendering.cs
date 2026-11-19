using System.Globalization;
using System.Numerics;
using System.Xml.Linq;

namespace Duxel.Svg.Managed;

internal sealed partial class SvgManagedScene
{
    private sealed record Node(Matrix3x2 Transform, float Opacity, SvgGeometry? Fill, ISvgPaint? FillColor,
        bool EvenOdd, SvgStrokePlan? Stroke, ISvgPaint? StrokeColor, Node[] Children,
        Node? Clip = null, Node? Mask = null, bool LuminanceMask = true, GaussianFilter? Filter = null, Vector4? ObjectBounds = null, bool Visible = true)
    {
        internal Vector4? Bounds
        {
            get
            {
                if (ObjectBounds is { } authored)
                    return authored;
                var bounds = Fill?.Bounds;
                foreach (var child in Children)
                {
                    if (child.Bounds is not { } childBounds)
                        continue;
                    var next = TransformBounds(childBounds, child.Transform);
                    if (bounds is not { } old)
                        bounds = next;
                    else
                    {
                        var min = Vector2.Min(new(old.X, old.Y), new(next.X, next.Y));
                        var max = Vector2.Max(new(old.X + old.Z, old.Y + old.W), new(next.X + next.Z, next.Y + next.W));
                        bounds = new(min.X, min.Y, max.X - min.X, max.Y - min.Y);
                    }
                }
                return bounds;
            }
        }

        internal void Render(SvgRasterSurface target, Matrix3x2 parent)
        {
            if (!Visible || Opacity == 0)
                return;
            var transform = Transform * parent;
            if (transform.GetDeterminant() == 0)
                return;
            var left = 0;
            var top = 0;
            var layer = Opacity != 1 || Clip is not null || Mask is not null || Filter is not null;
            var width = target.Width;
            var height = target.Height;
            if (Filter is not null)
            {
                var bounds = TransformBounds(Filter.Region.Bounds, transform);
                if (bounds.Z <= 0 || bounds.W <= 0)
                    return;
                left = checked((int)MathF.Floor(bounds.X));
                top = checked((int)MathF.Floor(bounds.Y));
                width = checked((int)MathF.Ceiling(bounds.X + bounds.Z) - left);
                height = checked((int)MathF.Ceiling(bounds.Y + bounds.W) - top);
                transform *= Matrix3x2.CreateTranslation(-left, -top);
            }
            var rasterOrigin = target.RasterOrigin;
            if (Opacity != 1 && Filter is null && PaintBounds(this, parent) is { } painted)
                rasterOrigin = new(MathF.Floor(painted.X), MathF.Floor(painted.Y));
            if (Filter is not null)
                rasterOrigin = Vector2.Zero;
            var destination = layer ? new SvgRasterSurface(width, height, rasterOrigin: rasterOrigin) : target;
            if (Fill is not null)
                destination.FillPaint(Fill, transform, FillColor!.Bind(transform), EvenOdd);
            if (Stroke is not null)
                destination.FillPaint(Stroke.Outline(transform), transform, StrokeColor!.Bind(transform));
            foreach (var child in Children)
                child.Render(destination, transform);
            if (Filter is not null)
            {
                destination.GaussianBlur(new Vector2(transform.M11, transform.M12) * Filter.Deviation.X,
                    new Vector2(transform.M21, transform.M22) * Filter.Deviation.Y, Filter.LinearRgb);
                var region = new SvgRasterSurface(width, height);
                region.Fill(Filter.Region, transform, Vector4.One);
                destination.Mask(region, false, preserveSamples: true);
            }
            Vector4? compositeClip = null;
            if (TryCompositeClip(this, transform, out var rectangle))
                compositeClip = rectangle;
            if (Clip is not null && compositeClip is null)
            {
                var clip = new SvgRasterSurface(width, height);
                Clip.Render(clip, transform);
                destination.Mask(clip, false, preserveSamples: true);
            }
            if (Mask is not null)
            {
                var mask = new SvgRasterSurface(width, height);
                Mask.Render(mask, transform);
                destination.Mask(mask, LuminanceMask);
            }
            if (layer)
                target.CompositeAt(destination, Opacity, left, top, compositeClip);
        }
    }
    private sealed class PatternPaint(Node content, Vector4 tile, Matrix3x2 mapping) : ISvgPaint
    {
        public SvgGpuPaintData CompileGpu()
        {
            if (!Matrix3x2.Invert(mapping, out var inverse))
                return new SvgSolidPaint(Vector4.Zero).CompileGpu();
            var scene = new SvgManagedScene(content, tile.Z, tile.W, Matrix3x2.CreateTranslation(-tile.X, -tile.Y)).CompileGpu();
            return new(3, tile, default, inverse, [], Pattern: scene);
        }
        public Vector4 Sample(Vector2 point) => throw new InvalidOperationException("SVG patterns must bind to their target resolution before sampling.");
        public ISvgPaint Bind(Matrix3x2 transform)
        {
            var combined = mapping * transform;
            if (!Matrix3x2.Invert(combined, out var inverse))
                return new SvgSolidPaint(Vector4.Zero);
            var width = Math.Max(1, checked((int)MathF.Ceiling(tile.Z * new Vector2(combined.M11, combined.M12).Length())));
            var height = Math.Max(1, checked((int)MathF.Ceiling(tile.W * new Vector2(combined.M21, combined.M22).Length())));
            var surface = new SvgRasterSurface(width, height);
            content.Render(surface, Matrix3x2.CreateTranslation(-tile.X, -tile.Y) * Matrix3x2.CreateScale(width / tile.Z, height / tile.W));
            return new PatternRaster(surface.CopyRgba(), width, height, tile, inverse);
        }
    }
    private sealed class PatternRaster(byte[] rgba, int width, int height, Vector4 tile, Matrix3x2 inverse) : ISvgPaint
    {
        public Vector4 Sample(Vector2 point)
        {
            var local = Vector2.Transform(point, inverse);
            var u = (local.X - tile.X) / tile.Z;
            var v = (local.Y - tile.Y) / tile.W;
            var x = (u - MathF.Floor(u)) * width - .5f;
            var y = (v - MathF.Floor(v)) * height - .5f;
            return SvgRasterSampling.Bilinear(rgba, width, height, x, y, repeat: true);
        }
    }
}
