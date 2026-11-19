using System.Numerics;
using System.Text.Json;
using System.Xml.Linq;

namespace Duxel.Svg.Managed;

// Test-only access to the real compiled tree; no parallel parser or renderer.
internal sealed partial class SvgManagedScene
{
    internal void WritePixelProbe(Stream output, XDocument document, int width, int height, int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, width);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, height);
        var root = SvgCssCascade.Apply(document).Root!;
        var viewport = SvgViewport.Fit(Width, Height, width, height);
        var parent = _view * viewport.Transform;
        var full = Render(width, height);
        var offset = checked((y * width + x) * 4);
        using var writer = new Utf8JsonWriter(output, new() { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("scope", "Actual CPU scene pixel and isolated leaf contributions; ancestor effects are reported, not applied to isolated leaves");
        writer.WriteNumber("x", x);
        writer.WriteNumber("y", y);
        WritePixel("sceneStraightRgba", full.AsSpan(offset, 4));
        writer.WriteStartArray("leaves");
        Visit(_root, root, parent, "root", []);
        writer.WriteEndArray();
        writer.WriteEndObject();

        void WritePixel(string name, ReadOnlySpan<byte> pixel)
        {
            writer.WriteStartArray(name);
            foreach (var value in pixel)
                writer.WriteNumberValue(value);
            writer.WriteEndArray();
        }

        void Visit(Node node, XElement element, Matrix3x2 ancestor, string path, string[] effects)
        {
            if (!node.Visible || node.Opacity == 0)
                return;
            var transform = node.Transform * ancestor;
            var inheritedEffects = effects;
            if (node.Opacity != 1 || node.Clip is not null || node.Mask is not null || node.Filter is not null)
                inheritedEffects = [.. effects, $"{path} opacity={node.Opacity} clip={node.Clip is not null} mask={node.Mask is not null} filter={node.Filter is not null}"];
            if (node.Children.Length > 0)
            {
                if (element.Name.LocalName is not ("svg" or "g" or "a"))
                    throw new NotSupportedException("Pixel provenance currently requires direct svg/g/a children, without use, text or markers.");
                var children = element.Elements().Where(child => child.Name.LocalName is not
                    ("linearGradient" or "radialGradient" or "pattern" or "clipPath" or "mask" or "symbol" or "marker" or "filter")).ToArray();
                if (children.Length != node.Children.Length)
                    throw new InvalidDataException("Compiled children do not match source provenance.");
                for (var i = 0; i < children.Length; i++)
                    Visit(node.Children[i], children[i], transform, $"{path}/{i}", inheritedEffects);
                return;
            }
            if (node.Fill is null && node.Stroke is null)
                return;
            if (PaintBounds(node, ancestor) is not { } bounds
                || x + 1 < bounds.X || y + 1 < bounds.Y || x > bounds.X + bounds.Z || y > bounds.Y + bounds.W)
                return;
            var surface = new SvgRasterSurface(width, height, clip: viewport.Letterboxed ? viewport.Clip : null);
            node.Render(surface, ancestor);
            var pixels = surface.CopyRgba();
            writer.WriteStartObject();
            writer.WriteString("path", path);
            writer.WriteString("id", (string?)element.Attribute("id"));
            writer.WriteString("element", element.Name.LocalName);
            writer.WriteBoolean("contributes", pixels[offset + 3] != 0);
            writer.WriteString("fill", node.FillColor?.GetType().Name);
            writer.WriteString("stroke", node.StrokeColor?.GetType().Name);
            WritePixel("isolatedStraightRgba", pixels.AsSpan(offset, 4));
            if (node.Fill is { SmoothPrimitive: false } sampled)
            {
                int? actualMask = null;
                SvgCoverageRasterizer.Rasterize(sampled, transform, node.EvenOdd, width, height,
                    (row, coverage, masks, points) =>
                    {
                        if (row == y && !masks.IsEmpty)
                            actualMask = masks[x];
                    });
                if (actualMask is { } sampleMask)
                    writer.WriteNumber("fillSampleMask", sampleMask);
                else
                    writer.WriteNull("fillSampleMask");
                writer.WriteNumber("fixedPointFillSampleMask", FixedPointMask(sampled, transform, node.EvenOdd, x, y));
            }
            writer.WriteStartArray("ancestorEffects");
            foreach (var effect in effects)
                writer.WriteStringValue(effect);
            writer.WriteEndArray();
            writer.WriteStartArray("commands");
            if (node.Fill is { } geometry)
                foreach (var segment in geometry.Segments)
                    writer.WriteStringValue(segment.Kind.ToString());
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }

    private static int FixedPointMask(SvgGeometry geometry, Matrix3x2 transform, bool evenOdd, int x, int y)
    {
        var edges = geometry.Flatten(transform, .25f, uniformCubics: true);
        ReadOnlySpan<int> sampleX = [96, 224, 32, 160];
        var mask = 0;
        for (var sample = 0; sample < 4; sample++)
        {
            var px = (long)x * 256 + sampleX[sample];
            var py = (long)y * 256 + 32 + sample * 64;
            var winding = 0;
            foreach (var edge in edges)
            {
                var ax = checked((long)Math.Round((double)edge.Start.X * 256));
                var ay = checked((long)Math.Round((double)edge.Start.Y * 256));
                var bx = checked((long)Math.Round((double)edge.End.X * 256));
                var by = checked((long)Math.Round((double)edge.End.Y * 256));
                if (py < Math.Min(ay, by) || py >= Math.Max(ay, by))
                    continue;
                var numerator = checked((ax - px) * (by - ay) + (py - ay) * (bx - ax));
                if (by > ay ? numerator > 0 : numerator < 0)
                    winding += by > ay ? 1 : -1;
            }
            if (evenOdd ? (winding & 1) != 0 : winding != 0)
                mask |= 1 << sample;
        }
        return mask;
    }
}
