using Duxel.Core;
using Duxel.Interop.Windows;

namespace Duxel.Platform.Windows;

public sealed partial class WindowsPlatformTextBackend : IPlatformShapedGlyphBackend
{
    PlatformShapedGlyphRun? IPlatformShapedGlyphBackend.ShapeGlyphs(string font, string? secondaryFont, string text, float fontSize)
    {
        var size = ResolveRasterFontSize(fontSize);
        if (size is not (16 or 18))
            return null;

        var paragraph = WindowsDirectWriteGlyphRasterizer.ShapeContextualParagraph(font, secondaryFont, text, size);
        if (paragraph.Runs.Length != 1 || paragraph.Runs[0].Raster.BidiLevel != 0 || paragraph.Runs[0].FontPath != font)
            return null;

        var run = paragraph.Runs[0].Raster;
        var glyphs = new PlatformShapedGlyph[run.Glyphs.Length];
        var pen = 0f;
        for (var i = 0; i < glyphs.Length; i++)
        {
            var glyph = run.Glyphs[i];
            var x = run.OriginX + pen + glyph.OffsetX;
            var y = run.OriginY + paragraph.Baseline - glyph.OffsetY;
            var ix = (int)MathF.Floor(x);
            var iy = (int)MathF.Floor(y);
            glyphs[i] = new(glyph.Index, ix, iy, x - ix, y - iy, glyph.Advance);
            pen += glyph.Advance;
        }
        return new(size, paragraph.Baseline, glyphs);
    }

    bool IPlatformShapedGlyphBackend.TryRasterizeGlyph(string font, int rasterSize, PlatformShapedGlyph glyph, out PlatformShapedGlyphMask mask)
    {
        mask = default;
        var run = new DirectWriteLongTextShaper.RasterRun(
            [new(glyph.Index, 0, glyph.Advance, 0, 0)], 0, glyph.PhaseX, glyph.PhaseY);
        if (!WindowsDirectWriteGlyphRasterizer.Instance.TryRasterizeTextRun(font, " ", rasterSize, 1, 1, 1, out var source, paragraphRun: run))
            return false;
        if (source.Width == 0 || source.Height == 0)
            return true;
        if (source.Rgb is not { } rgb)
            return false;

        var pixels = new byte[source.Width * source.Height * 4];
        for (var i = 0; i < source.Alpha.Length; i++)
        {
            if (source.Alpha[i] == 0)
                continue;
            rgb.AsSpan(i * 3, 3).CopyTo(pixels.AsSpan(i * 4, 3));
            pixels[i * 4 + 3] = source.Alpha[i];
        }
        mask = new(source.OffsetX, source.OffsetY, source.Width, source.Height, pixels);
        return true;
    }
}
