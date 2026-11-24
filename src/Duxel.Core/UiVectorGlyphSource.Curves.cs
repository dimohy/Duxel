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

    private readonly record struct GlyphPath(ReadOnlyMemory<UiVectorCurve> Curves, UiVector2 Origin, bool EvenOdd, bool Cubic)
    {
        public UiVector2 Minimum { get; init; }
        public UiVector2 Maximum { get; init; }
    }
    private readonly record struct CurveAllocation(bool Shared, int Offset);
    private static UiVectorGlyphSource CreateRetainedCurves(in PlatformTextMeshResult mesh, float rasterScale, UiVector2 rasterOrigin)
    {
        var paths = new List<GlyphPath>();
        // Native text providers return the same immutable range for repeated glyphs.
        var curveOffsets = mesh.Glyphs.Length > 1
            ? new Dictionary<ReadOnlyMemory<UiVectorCurve>, CurveAllocation>() : null;
        var payloadLength = 0;
        foreach (var glyph in mesh.Glyphs.Span)
        {
            if (!glyph.Outline.Curves.IsEmpty)
            {
                var memory = glyph.Outline.Curves;
                var curves = memory.Span;
                var first = curves[0].Start;
                var previous = first;
                var cubic = false;
                foreach (var curve in curves)
                {
                    if (curve.Start != previous)
                    {
                        if (previous != first)
                            throw new ArgumentException("Retained glyph contours must close.", nameof(mesh));
                        first = curve.Start;
                    }
                    previous = curve.End;
                    cubic |= curve.IsCubic;
                }
                if (previous != first)
                    throw new ArgumentException("Retained glyph contours must close.", nameof(mesh));
                if (curveOffsets is null || curveOffsets.TryAdd(memory, new(false, -1)))
                    payloadLength = checked(payloadLength + curves.Length * (cubic ? 3 : 1));
                else
                    curveOffsets[memory] = new(true, -1);
                paths.Add(new(memory, glyph.Offset, glyph.Outline.EvenOdd, cubic));
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
                paths.Add(new(curves, default, false, false));
                payloadLength = checked(payloadLength + curves.Length);
            }
        }
        var minimum = new UiVector2(float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new UiVector2(float.NegativeInfinity, float.NegativeInfinity);
        for (var i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            var (pathMin, pathMax) = GetGlyphControlBounds(in path);
            minimum = new(MathF.Min(minimum.X, pathMin.X), MathF.Min(minimum.Y, pathMin.Y));
            maximum = new(MathF.Max(maximum.X, pathMax.X), MathF.Max(maximum.Y, pathMax.Y));
            paths[i] = path with { Minimum = pathMin, Maximum = pathMax };
        }
        var bounds = CoverageBounds(minimum, maximum, rasterScale, rasterOrigin);
        var at = checked(paths.Count * 6);
        var data = new Vector4[checked(at + payloadLength)];
        for (var i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            var allocation = curveOffsets?.GetValueOrDefault(path.Curves) ?? default;
            var shared = allocation.Shared;
            var placed = shared && allocation.Offset >= 0;
            var command = i * 6;
            // The control hull encloses the entire curve. Carry one local unit past
            // its integer bounds for equivalent CPU and GPU placement at the edge.
            data[command] = new(
                MathF.Max(0, MathF.Floor(path.Minimum.X - bounds.X) - 1),
                MathF.Max(0, MathF.Floor(path.Minimum.Y - bounds.Y) - 1),
                MathF.Min(bounds.Width, MathF.Ceiling(path.Maximum.X - bounds.X) + 1),
                MathF.Min(bounds.Height, MathF.Ceiling(path.Maximum.Y - bounds.Y) + 1));
            data[command + 1] = new(placed ? allocation.Offset : at, path.Curves.Length, 0,
                UiGpuVectorScene.GlyphCoverageBounds | (path.Cubic ? 4 : 0) | (path.EvenOdd ? 1 : 0)
                | (shared ? UiGpuVectorScene.PlacedCurvePath : 0));
            data[command + 2] = Vector4.One;
            data[command + 4] = shared ? new(path.Origin.X, path.Origin.Y, bounds.X, bounds.Y) : new(1, 0, 0, 1);
            data[command + 5] = new(0, 0, 1, 0);
            if (shared)
            {
                if (placed)
                    continue;
                curveOffsets![path.Curves] = allocation with { Offset = at };
            }
            UiVector2 Position(UiVector2 point) => shared ? point
                : new(point.X + path.Origin.X - bounds.X, point.Y + path.Origin.Y - bounds.Y);
            foreach (var curve in path.Curves.Span)
            {
                var a = Position(curve.Start);
                var d = Position(curve.End);
                if (!path.Cubic)
                {
                    data[at++] = new(a.X, a.Y, d.X, d.Y);
                    continue;
                }
                var b = Position(curve.Control1);
                var c = Position(curve.Control2);
                data[at++] = new(a.X, a.Y, b.X, b.Y);
                data[at++] = new(c.X, c.Y, d.X, d.Y);
                var extrema = curve.IsCubic ? UiCubicBezierMath.YExtrema(a.Y, b.Y, c.Y, d.Y) : default;
                data[at++] = new(curve.IsCubic ? 1 : 0, extrema.First, extrema.Second, extrema.Count);
            }
        }
        return new(new(new(bounds.Width, bounds.Height), data, paths.Count), bounds);
    }

    private static (UiVector2 Minimum, UiVector2 Maximum) GetGlyphControlBounds(in GlyphPath path)
    {
        var minimum = new UiVector2(float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new UiVector2(float.NegativeInfinity, float.NegativeInfinity);
        var origin = path.Origin;
        foreach (var curve in path.Curves.Span)
        {
            Bound(curve.Start);
            Bound(curve.End);
            if (curve.IsCubic)
            {
                Bound(curve.Control1);
                Bound(curve.Control2);
            }
        }
        return (minimum, maximum);

        void Bound(UiVector2 point)
        {
            var x = point.X + origin.X;
            var y = point.Y + origin.Y;
            if (!float.IsFinite(x) || !float.IsFinite(y))
                throw new ArgumentException("Retained glyph curves must be finite.", nameof(path));
            minimum = new(MathF.Min(minimum.X, x), MathF.Min(minimum.Y, y));
            maximum = new(MathF.Max(maximum.X, x), MathF.Max(maximum.Y, y));
        }
    }
}
