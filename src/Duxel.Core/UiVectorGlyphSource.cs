using System.Numerics;

namespace Duxel.Core;

/// <summary>Immutable glyph outline compiled once for the shared GPU area-coverage renderer.</summary>
internal sealed partial class UiVectorGlyphSource(UiGpuVectorScene? scene, UiRect bounds)
{
    internal UiGpuVectorScene? Scene { get; } = scene;
    internal UiRect Bounds { get; } = bounds;

    private static UiVectorGlyphSource CreateTriangles(in PlatformTextMeshResult mesh, float rasterScale, UiVector2 rasterOrigin)
    {
        if (!float.IsFinite(rasterScale) || rasterScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(rasterScale));
        if (!float.IsFinite(rasterOrigin.X) || !float.IsFinite(rasterOrigin.Y))
            throw new ArgumentOutOfRangeException(nameof(rasterOrigin));
        var edges = new Dictionary<(UiVector2 A, UiVector2 B), int>();
        var minimum = new UiVector2(float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new UiVector2(float.NegativeInfinity, float.NegativeInfinity);
        static bool Before(UiVector2 a, UiVector2 b) => a.X < b.X || a.X == b.X && a.Y < b.Y;
        static UiVector2 Translate(UiVector2 point, UiVector2 offset) => new(point.X + offset.X, point.Y + offset.Y);
        void Edge(UiVector2 a, UiVector2 b)
        {
            var forward = Before(a, b);
            var key = forward ? (a, b) : (b, a);
            edges.TryGetValue(key, out var count);
            edges[key] = checked(count + (forward ? 1 : -1));
        }
        void Triangle(UiVector2 a, UiVector2 b, UiVector2 c)
        {
            if (!float.IsFinite(a.X) || !float.IsFinite(a.Y) || !float.IsFinite(b.X) || !float.IsFinite(b.Y)
                || !float.IsFinite(c.X) || !float.IsFinite(c.Y))
                throw new ArgumentException("Glyph vertices must be finite.", nameof(mesh));
            var cross = ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);
            if (cross == 0)
                return;
            if (cross < 0)
                (b, c) = (c, b);
            Edge(a, b);
            Edge(b, c);
            Edge(c, a);
            minimum = new(MathF.Min(minimum.X, MathF.Min(a.X, MathF.Min(b.X, c.X))), MathF.Min(minimum.Y, MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
            maximum = new(MathF.Max(maximum.X, MathF.Max(a.X, MathF.Max(b.X, c.X))), MathF.Max(maximum.Y, MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        }
        if (mesh.TriangleVertices.Length % 3 != 0 || !mesh.TriangleVertices.IsEmpty && !mesh.Glyphs.IsEmpty)
            throw new ArgumentException("Glyph geometry must contain one triangle representation.", nameof(mesh));
        var triangles = mesh.TriangleVertices.Span;
        for (var i = 0; i < triangles.Length; i += 3)
            Triangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        foreach (var glyph in mesh.Glyphs.Span)
        {
            var points = glyph.Vertices.Span;
            var indices = glyph.Indices.Span;
            if (indices.Length % 3 != 0)
                throw new ArgumentException("Glyph indices must contain complete triangles.", nameof(mesh));
            for (var i = 0; i < indices.Length; i += 3)
            {
                if (indices[i] >= points.Length || indices[i + 1] >= points.Length || indices[i + 2] >= points.Length)
                    throw new ArgumentException("Glyph index exceeds its vertex buffer.", nameof(mesh));
                Triangle(Translate(points[(int)indices[i]], glyph.Offset), Translate(points[(int)indices[i + 1]], glyph.Offset),
                    Translate(points[(int)indices[i + 2]], glyph.Offset));
            }
        }
        var count = 0;
        foreach (var winding in edges.Values)
            count = checked(count + Math.Abs(winding));
        if (count == 0)
            return new(null, default);
        // Fit the em canvas, not the native paragraph baseline, to avoid filtering a
        // second time when the completed coverage image is placed in its icon box.
        var bounds = CoverageBounds(minimum, maximum, rasterScale, rasterOrigin);
        var left = bounds.X;
        var top = bounds.Y;
        var data = new Vector4[checked(6 + count)];
        data[0] = new(0, 0, bounds.Width, bounds.Height);
        data[1] = new(6, count, 0, 0);
        data[2] = Vector4.One;
        data[4] = new(1, 0, 0, 1);
        data[5] = new(0, 0, 1, 0);
        var at = 6;
        foreach (var (edge, winding) in edges)
        {
            var (a, b) = winding > 0 ? edge : (edge.B, edge.A);
            for (var i = 0; i < Math.Abs(winding); i++)
                data[at++] = new(a.X - left, a.Y - top, b.X - left, b.Y - top);
        }
        return new(new(new(bounds.Width, bounds.Height), data, 1), bounds);
    }

    private static UiRect CoverageBounds(UiVector2 minimum, UiVector2 maximum, float rasterScale, UiVector2 rasterOrigin)
    {
        var left = (MathF.Floor((minimum.X + rasterOrigin.X) * rasterScale) - 1) / rasterScale - rasterOrigin.X;
        var top = (MathF.Floor((minimum.Y + rasterOrigin.Y) * rasterScale) - 1) / rasterScale - rasterOrigin.Y;
        var right = (MathF.Ceiling((maximum.X + rasterOrigin.X) * rasterScale) + 1) / rasterScale - rasterOrigin.X;
        var bottom = (MathF.Ceiling((maximum.Y + rasterOrigin.Y) * rasterScale) + 1) / rasterScale - rasterOrigin.Y;
        return new(left, top, right - left, bottom - top);
    }
}

internal readonly struct UiVectorSceneTextureProvider : IUiRasterTextureProvider<UiGpuVectorScene>
{
    public static long Revision(UiGpuVectorScene source) => 0;
    public static UiTextureUpdate Prepare(UiGpuVectorScene source, int width, int height, UiTextureId texture, UiGpuVectorCamera? camera)
        => new(UiTextureUpdateKind.RenderVectorScene, texture, UiTextureFormat.Rgba8Unorm, width, height, ReadOnlyMemory<byte>.Empty)
        {
            VectorScene = source,
            VectorCamera = camera,
            IsColorImage = true
        };
}
