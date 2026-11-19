using System.Numerics;

namespace Duxel.Core;

internal sealed partial class UiVectorGlyphSource
{
    internal static UiVectorGlyphSource Create(in PlatformTextMeshResult mesh, float rasterScale, UiVector2 rasterOrigin = default)
    {
        if (!float.IsFinite(rasterScale) || rasterScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(rasterScale));
        if (!float.IsFinite(rasterOrigin.X) || !float.IsFinite(rasterOrigin.Y))
            throw new ArgumentOutOfRangeException(nameof(rasterOrigin));
        if (!mesh.TriangleVertices.IsEmpty && !mesh.Glyphs.IsEmpty)
            throw new ArgumentException("Glyph geometry must contain one triangle representation.", nameof(mesh));
        foreach (var glyph in mesh.Glyphs.Span)
            if (!glyph.Outline.Curves.IsEmpty)
                return CreateRetainedCurves(mesh, rasterScale, rasterOrigin);
        return CreateTriangles(mesh, rasterScale, rasterOrigin);
    }

    private readonly record struct GlyphPath(UiVectorCurve[] Curves, bool EvenOdd);
    private static UiVectorGlyphSource CreateRetainedCurves(in PlatformTextMeshResult mesh, float rasterScale, UiVector2 rasterOrigin)
    {
        var paths = new List<GlyphPath>();
        foreach (var glyph in mesh.Glyphs.Span)
        {
            if (!glyph.Outline.Curves.IsEmpty)
            {
                var curves = glyph.Outline.Curves.ToArray();
                var first = curves[0].Start;
                var previous = first;
                foreach (var curve in curves)
                {
                    if (curve.Start != previous)
                    {
                        if (previous != first)
                            throw new ArgumentException("Retained glyph contours must close.", nameof(mesh));
                        first = curve.Start;
                    }
                    previous = curve.End;
                }
                if (previous != first)
                    throw new ArgumentException("Retained glyph contours must close.", nameof(mesh));
                UiVector2 Offset(UiVector2 value) => new(value.X + glyph.Offset.X, value.Y + glyph.Offset.Y);
                for (var i = 0; i < curves.Length; i++)
                    curves[i] = new(Offset(curves[i].Start), Offset(curves[i].Control1), Offset(curves[i].Control2), Offset(curves[i].End), curves[i].IsCubic);
                paths.Add(new(curves, glyph.Outline.EvenOdd));
            }
            else
            {
                var triangles = new PlatformTextMeshResult(ReadOnlyMemory<UiVector2>.Empty, mesh.Width, mesh.Height, mesh.Baseline)
                {
                    Glyphs = new PlatformTextGlyphMesh[] { glyph }
                };
                var source = CreateTriangles(triangles, rasterScale, rasterOrigin);
                if (source.Scene is not { } scene)
                    continue;
                var edges = scene.Data.Span[6..];
                var curves = new UiVectorCurve[edges.Length];
                for (var i = 0; i < edges.Length; i++)
                    curves[i] = UiVectorCurve.Line(new(edges[i].X + source.Bounds.X, edges[i].Y + source.Bounds.Y),
                        new(edges[i].Z + source.Bounds.X, edges[i].W + source.Bounds.Y));
                paths.Add(new(curves, false));
            }
        }
        var minimum = new UiVector2(float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new UiVector2(float.NegativeInfinity, float.NegativeInfinity);
        void Bound(UiVector2 point)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                throw new ArgumentException("Retained glyph curves must be finite.", nameof(mesh));
            minimum = new(MathF.Min(minimum.X, point.X), MathF.Min(minimum.Y, point.Y));
            maximum = new(MathF.Max(maximum.X, point.X), MathF.Max(maximum.Y, point.Y));
        }
        var length = checked(paths.Count * 6);
        foreach (var path in paths)
        {
            var cubic = path.Curves.Any(curve => curve.IsCubic);
            length = checked(length + path.Curves.Length * (cubic ? 3 : 1));
            foreach (var curve in path.Curves)
            {
                Bound(curve.Start);
                Bound(curve.End);
                if (curve.IsCubic)
                {
                    Bound(curve.Control1);
                    Bound(curve.Control2);
                }
            }
        }
        var bounds = CoverageBounds(minimum, maximum, rasterScale, rasterOrigin);
        var data = new Vector4[length];
        var at = paths.Count * 6;
        for (var i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            var cubic = path.Curves.Any(curve => curve.IsCubic);
            var command = i * 6;
            data[command] = new(0, 0, bounds.Width, bounds.Height);
            data[command + 1] = new(at, path.Curves.Length, 0, (cubic ? 4 : 0) | (path.EvenOdd ? 1 : 0));
            data[command + 2] = Vector4.One;
            data[command + 4] = new(1, 0, 0, 1);
            data[command + 5] = new(0, 0, 1, 0);
            foreach (var curve in path.Curves)
            {
                var a = new UiVector2(curve.Start.X - bounds.X, curve.Start.Y - bounds.Y);
                var d = new UiVector2(curve.End.X - bounds.X, curve.End.Y - bounds.Y);
                if (!cubic)
                {
                    data[at++] = new(a.X, a.Y, d.X, d.Y);
                    continue;
                }
                var b = new UiVector2(curve.Control1.X - bounds.X, curve.Control1.Y - bounds.Y);
                var c = new UiVector2(curve.Control2.X - bounds.X, curve.Control2.Y - bounds.Y);
                data[at++] = new(a.X, a.Y, b.X, b.Y);
                data[at++] = new(c.X, c.Y, d.X, d.Y);
                var extrema = curve.IsCubic ? UiCubicBezierMath.YExtrema(a.Y, b.Y, c.Y, d.Y) : default;
                data[at++] = new(curve.IsCubic ? 1 : 0, extrema.First, extrema.Second, extrema.Count);
            }
        }
        return new(new(new(bounds.Width, bounds.Height), data, paths.Count), bounds);
    }
}
