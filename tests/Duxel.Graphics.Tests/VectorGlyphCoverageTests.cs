using Duxel.Core;

internal static class VectorGlyphCoverageTests
{
    internal static void Run(Action<bool, string> check)
    {
        var square = new PlatformTextMeshResult(ReadOnlyMemory<UiVector2>.Empty, 4, 4, 0)
        {
            Glyphs = new PlatformTextGlyphMesh[] { new(new UiVector2[] { new(0, 0), new(4, 0), new(4, 4), new(0, 4) },
                new uint[] { 0, 1, 2, 0, 3, 2 }, default) }
        };
        var outline = UiVectorGlyphSource.Create(square, 1);
        check(outline.Scene is { } scene && scene.Data.Span[1].Y == 4 && Inside(outline, 2, 2) && !Inside(outline, 5, 2),
            "Vector glyph coverage / indexed triangles with mixed orientation cancel their interior diagonal");
        var ring = new List<UiVector2>();
        void Rectangle(float x, float y, float width, float height)
        {
            ring.AddRange([new(x, y), new(x + width, y), new(x + width, y + height),
                new(x, y), new(x + width, y + height), new(x, y + height)]);
        }
        Rectangle(0, 0, 10, 3);
        Rectangle(7, 3, 3, 4);
        Rectangle(0, 7, 10, 3);
        Rectangle(0, 3, 3, 4);
        var hole = UiVectorGlyphSource.Create(new(ring.ToArray(), 10, 10, 0), 1);
        check(Inside(hole, 1, 1) && Inside(hole, 9, 9) && !Inside(hole, 5, 5) && !Inside(hole, 11, 5),
            "Vector glyph coverage / nonzero contour winding preserves holes and collinear split boundaries");
        var aligned = UiVectorGlyphSource.Create(square, 1, new(0, .48f));
        check(aligned.Bounds.Y + .48f == MathF.Round(aligned.Bounds.Y + .48f)
            && aligned.Bounds.Height == MathF.Round(aligned.Bounds.Height),
            "Vector glyph coverage / em-canvas phase produces integral image placement without moving the outline");
        var empty = UiVectorGlyphSource.Create(new PlatformTextMeshResult(ReadOnlyMemory<UiVector2>.Empty, 0, 0, 0), 1);
        check(empty.Scene is null, "Vector glyph coverage / empty glyphs do not allocate a GPU scene");
        var rejected = 0;
        foreach (var invalid in new Action[]
        {
            () => UiVectorGlyphSource.Create(square, float.NaN),
            () => UiVectorGlyphSource.Create(square, 1, new(float.PositiveInfinity, 0)),
            () => UiVectorGlyphSource.Create(new(new UiVector2[] { new(0, 0) }, 1, 1, 0), 1),
            () => UiVectorGlyphSource.Create(new PlatformTextMeshResult(ReadOnlyMemory<UiVector2>.Empty, 1, 1, 0)
                { Glyphs = new PlatformTextGlyphMesh[] { new(new UiVector2[] { new(0, 0) }, new uint[] { 0, 1, 2 }, default) } }, 1),
            () => UiVectorGlyphSource.Create(new(new UiVector2[] { new(float.NaN, 0), new(1, 0), new(0, 1) }, 1, 1, 0), 1)
        })
            try
            {
                invalid();
            }
            catch (ArgumentException) { rejected++; }
        check(rejected == 5, "Vector glyph coverage / malformed indices, triangles and nonfinite geometry fail explicitly");
        var full = UiRasterTextureViewport.Create(new(10, 20, 100, 50), new(10, 20, 100, 50), 1.25f);
        check(full is { Width: 125, Height: 63, Camera: null } && full.UvMinimum == default && full.UvMaximum == new UiVector2(1, 1),
            "Vector viewport / complete SVG and glyph targets retain the full source mapping");
        var clipped = UiRasterTextureViewport.Create(new(0, 0, 1_000_000, 1_000_000), new(100, 200, 80, 60), 1);
        check(clipped is { Width: 82, Height: 62, Camera: { } camera }
            && camera.FullPixelSize == new UiVector2(1_000_000, 1_000_000) && camera.PixelOrigin == new UiVector2(99, 199),
            "Vector viewport / a million-unit virtual scene allocates only its visible pixel window");
        var update = UiVectorSceneTextureProvider.Prepare(outline.Scene!, 6, 6, new(50), null);
        check(update.Kind == UiTextureUpdateKind.RenderVectorScene && update.RgbaPixels.IsEmpty && update.IsColorImage,
            "Vector glyph coverage / shared GPU provider queues scene commands without CPU raster pixels");
        var cubicGlyph = new PlatformTextGlyphMesh(ReadOnlyMemory<UiVector2>.Empty, ReadOnlyMemory<uint>.Empty, default)
        {
            Outline = new(new UiVectorCurve[] { new(new(0, 0), new(0, 3), new(3, -3), new(3, 0)), UiVectorCurve.Line(new(3, 0), default) }, true)
        };
        var retained = UiVectorGlyphSource.Create(new PlatformTextMeshResult(ReadOnlyMemory<UiVector2>.Empty, 3, 3, 0)
        {
            Glyphs = new PlatformTextGlyphMesh[] { cubicGlyph }
        }, 1);
        var commands = retained.Scene!.Data.Span;
        check(commands[1].W == (UiGpuVectorScene.GlyphCoverageBounds | 5)
            && commands[1].Y == 2 && commands[8].X == 1 && commands[8].W == 2,
            "Vector glyph curves / native cubic controls, even-odd fill and two monotone intervals reach the GPU unchanged");
        check(Math.Abs(commands[8].Y - (3 - Math.Sqrt(3)) / 6) < 1e-6
            && Math.Abs(commands[8].Z - (3 + Math.Sqrt(3)) / 6) < 1e-6,
            "Vector glyph curves / shared SVG derivative roots match independent cubic extrema");
        var openRejected = false;
        try
        {
            UiVectorGlyphSource.Create(new PlatformTextMeshResult(ReadOnlyMemory<UiVector2>.Empty, 1, 1, 0)
            {
                Glyphs = new PlatformTextGlyphMesh[] { cubicGlyph with { Outline = new(new UiVectorCurve[] { UiVectorCurve.Line(default, new(1, 1)) }) } }
            }, 1);
        }
        catch (ArgumentException) { openRejected = true; }
        check(openRejected, "Vector glyph curves / open native contours fail before GPU publication");
    }

    private static bool Inside(UiVectorGlyphSource source, float x, float y)
    {
        var point = new UiVector2(x - source.Bounds.X, y - source.Bounds.Y);
        var winding = 0;
        foreach (var edge in source.Scene!.Data.Span[6..])
        {
            if ((edge.Y <= point.Y && edge.W > point.Y || edge.W <= point.Y && edge.Y > point.Y)
                && edge.X + (point.Y - edge.Y) * (edge.Z - edge.X) / (edge.W - edge.Y) > point.X)
                winding += edge.W > edge.Y ? 1 : -1;
        }
        return winding != 0;
    }
}
