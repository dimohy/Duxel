using Duxel.Core;
using Duxel.Platform.Windows;

internal static class FontVariantTests
{
    internal static void Run(Action<bool, string> check)
    {
        var backend = WindowsPlatformTextBackend.Instance;
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var regular = Path.Combine(fonts, "segoeui.ttf");
        var korean = Path.Combine(fonts, "malgun.ttf");
        var oblique = backend.GetFontVariantResource(regular, PlatformFontSimulation.Oblique);
        var koreanOblique = backend.GetFontVariantResource(korean, PlatformFontSimulation.Oblique);
        check(oblique == backend.GetFontVariantResource(regular, PlatformFontSimulation.Oblique)
            && oblique == backend.GetFontVariantResource(oblique, PlatformFontSimulation.Oblique) && oblique != regular,
            "Oblique font resources are stable and idempotent without changing the original font identity");
        const string text = "한글 italic W";
        check(backend.TryLayout(new(oblique, koreanOblique, text, 28), out var layout)
            && backend.TryGetFontMetrics(oblique, 28, out var metrics) && metrics.Ascent > 0,
            "Oblique font resources serve real layout and metrics for Latin and Korean text");
        var normalOk = backend.TryRasterize(new(regular, korean, text, 28), out var normal);
        var slantedOk = backend.TryRasterize(new(oblique, koreanOblique, text, 28), out var slanted);
        try
        {
            check(normalOk && slantedOk && !normal.RgbaPixels.Span.SequenceEqual(slanted.RgbaPixels.Span),
                "Oblique font resources change actual raster coverage rather than only labeling emphasis");
            check(slantedOk && layout.Width == slanted.Width && layout.Height == slanted.Height
                && layout.Baseline == slanted.Baseline && layout.OriginX == slanted.OriginX && layout.Advance == slanted.Advance,
                "Oblique font layout and raster use identical metrics and origins");
        }
        finally
        {
            normal.PixelOwner?.Dispose();
            slanted.PixelOwner?.Dispose();
        }
        var normalMesh = backend.CreateTextMesh(new(regular, korean, text, 28));
        var slantedMesh = backend.CreateTextMesh(new(oblique, koreanOblique, text, 28));
        var normalGeometry = normalMesh.Glyphs.Span.ToArray().SelectMany(glyph => glyph.Vertices.ToArray()).Concat(normalMesh.TriangleVertices.ToArray()).ToArray();
        var slantedGeometry = slantedMesh.Glyphs.Span.ToArray().SelectMany(glyph => glyph.Vertices.ToArray()).Concat(slantedMesh.TriangleVertices.ToArray()).ToArray();
        check(normalGeometry.Length > 0 && slantedGeometry.Length > 0 && !normalGeometry.AsSpan().SequenceEqual(slantedGeometry),
            "Oblique font resources also change vector outlines");
    }
}
