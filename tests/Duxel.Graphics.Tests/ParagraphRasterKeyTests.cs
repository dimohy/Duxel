using Duxel.Core;

internal static class ParagraphRasterKeyTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlatformTextParagraphRun Run(float x = 10, float y = 20, uint glyph = 17, float advance = 8,
            float size = 14, uint bidi = 0, int source = 0) => new(0, "font.ttf", size, bidi, new(x, y),
                [new(glyph, source, advance, 0, 0, x, y)]);
        var original = Run();
        var translated = Run(40, 60, source: 40);
        check(original.RasterEquals(translated) && original.RasterHash == translated.RasterHash,
            "whole-pixel paragraph placement and source shifts preserve raster identity");
        check(!original.RasterEquals(Run(10.25f)), "subpixel raster phase remains part of paragraph texture identity");
        check(!original.RasterEquals(Run(glyph: 18)), "changed paragraph glyph invalidates its raster identity");
        check(!original.RasterEquals(Run(advance: 9)), "changed glyph advance invalidates paragraph raster identity");
        check(!original.RasterEquals(Run(size: 16)) && !original.RasterEquals(Run(bidi: 1)),
            "font size and bidi direction remain raster identity inputs");
        var key = new UiImmediateContext.DirectTextCacheKey("font.ttf", null, "", 14, 1) { ParagraphRun = original };
        var moved = key with { ParagraphRun = translated };
        var dictionary = new Dictionary<UiImmediateContext.DirectTextCacheKey, int> { [key] = 42 };
        check(dictionary.TryGetValue(moved, out var value) && value == 42,
            "paragraph texture cache reuses equal glyph content across new layout instances");
        check(!dictionary.ContainsKey(moved with { BackendGeneration = 2 }),
            "text backend generation still invalidates a matching paragraph raster");
    }
}
