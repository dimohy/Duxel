namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private const int ShapedGlyphAtlasWidth = 256;
    private const int ShapedGlyphAtlasHeight = 128;
    private const int MaxShapedGlyphs = 256;
    private DirectTextAtlasPage? _shapedGlyphAtlas;
    private byte[]? _shapedGlyphColumnHeights;
    private Dictionary<ShapedGlyphKey, ShapedGlyphCell>? _shapedGlyphs;

    private readonly record struct ShapedGlyphKey(string Font, long Generation, int RasterSize, ushort Index, float PhaseX, float PhaseY, float Advance);
    private readonly record struct ShapedGlyphCell(int OffsetX, int OffsetY, int Width, int Height, int AtlasX, int AtlasY);

    private bool TryDrawShapedGlyphs(UiDrawListBuilder drawList, DirectTextCacheKey key, DirectTextCacheEntry entry,
        UiVector2 origin, UiColor color, UiRect clip)
    {
        if (!key.ShortLived || _contentScale != 1 || _capturingPaintGroup is not null || color.Rgba >> 24 != 255
            || origin.X != MathF.Floor(origin.X) || origin.Y != MathF.Floor(origin.Y)
            || (key.FontSize != 16 && !IsCapturingRetainedGlyphGeometry())
            || _platformTextBackend is not IPlatformShapedGlyphBackend backend || !IsNumericAscii(key.Text))
            return false;

        var run = entry.ShapedGlyphs ?? backend.ShapeGlyphs(key.FontPath, key.SecondaryFontPath, key.Text, key.FontSize);
        if (run is null || run.Glyphs.Length > 64)
            return false;

        Span<ShapedGlyphCell> cells = stackalloc ShapedGlyphCell[run.Glyphs.Length];
        var top = run.Baseline - entry.Baseline;
        for (var i = 0; i < run.Glyphs.Length; i++)
        {
            var glyph = run.Glyphs[i];
            if (!TryGetShapedGlyph(backend, key, run.RasterSize, glyph, out var cell))
                return false;
            var left = glyph.X + cell.OffsetX - entry.OriginX;
            var y = glyph.Y + cell.OffsetY - top;
            if (cell.Width != 0 && (left < 0 || y < 0 || left + cell.Width > entry.Width || y + cell.Height > entry.Height
                || left != MathF.Floor(left) || y != MathF.Floor(y)))
                return false;
            cells[i] = cell;
        }

        if (entry.ShapedGlyphs is null)
            StoreDirectTextEntry(key, entry with { ShapedGlyphs = run });
        for (var i = 0; i < run.Glyphs.Length; i++)
        {
            var cell = cells[i];
            if (cell.Width == 0)
                continue;
            var glyph = run.Glyphs[i];
            var p = new UiVector2(origin.X + glyph.X + cell.OffsetX - entry.OriginX, origin.Y + glyph.Y + cell.OffsetY - top);
            drawList.AddTextImage(_shapedGlyphAtlas!.TextureId, p, new(p.X + cell.Width, p.Y + cell.Height),
                new((float)cell.AtlasX / ShapedGlyphAtlasWidth, (float)cell.AtlasY / ShapedGlyphAtlasHeight),
                new((float)(cell.AtlasX + cell.Width) / ShapedGlyphAtlasWidth,
                    (float)(cell.AtlasY + cell.Height) / ShapedGlyphAtlasHeight), color, clip);
        }
        return true;
    }

    private bool IsCapturingRetainedGlyphGeometry()
        => _layerFrames.TryPeek(out var layer) && layer.ShouldDraw
            && (layer.Options.StaticCache || layer.Options.ContentVersion.HasValue);

    private static bool IsNumericAscii(string text)
    {
        if (text.Length > 64)
            return false;
        var hasDigit = false;
        foreach (var character in text)
        {
            if (character is < ' ' or > '~')
                return false;
            hasDigit |= char.IsAsciiDigit(character);
        }
        return hasDigit;
    }

    private bool TryAllocateShapedGlyphRegion(int width, int height, out int x, out int y)
    {
        // Choose the smallest Y without moving a published glyph.
        // The dimensions already include the gutters on all four sides.
        var heights = _shapedGlyphColumnHeights ??= new byte[ShapedGlyphAtlasWidth];
        x = -1;
        y = ShapedGlyphAtlasHeight - height + 1;
        for (var column = 0; column <= heights.Length - width; column++)
        {
            var top = 0;
            for (var i = column; i < column + width && top < y; i++)
                top = Math.Max(top, heights[i]);

            if (top >= y)
                continue;

            x = column;
            y = top;
            if (y == 0)
                break;
        }

        if (x < 0)
            return false;

        heights.AsSpan(x, width).Fill((byte)(y + height));
        return true;
    }

    private bool TryGetShapedGlyph(IPlatformShapedGlyphBackend backend, DirectTextCacheKey text, int rasterSize, PlatformShapedGlyph glyph,
        out ShapedGlyphCell cell)
    {
        var key = new ShapedGlyphKey(text.FontPath, text.BackendGeneration, rasterSize, glyph.Index, glyph.PhaseX, glyph.PhaseY, glyph.Advance);
        var cache = _shapedGlyphs ??= [];
        if (cache.TryGetValue(key, out cell))
            return true;
        if (cache.Count >= MaxShapedGlyphs || !backend.TryRasterizeGlyph(text.FontPath, rasterSize, glyph, out var mask))
            return false;
        if (mask.Pixels is null)
        {
            cache.Add(key, cell);
            return true;
        }

        var page = _shapedGlyphAtlas ??= new(AllocateOwnedTextureId(), ShapedGlyphAtlasWidth, ShapedGlyphAtlasHeight);
        const int padding = DirectTextAtlasPadding;
        var width = mask.Width + 2 * padding;
        var height = mask.Height + 2 * padding;
        if (!TryAllocateShapedGlyphRegion(width, height, out var x, out var y))
            return false;
        CopyDirectTextBitmapToAtlas(mask.Pixels, mask.Width, mask.Height, page.Pixels, page.Width, x + padding, y + padding, padding);
        if (!page.TextureCreated)
        {
            QueueTextureUpdate(new(UiTextureUpdateKind.Create, page.TextureId, UiTextureFormat.Rgba8Unorm,
                page.Width, page.Height, (byte[])page.Pixels.Clone()));
            page.TextureCreated = true;
        }
        else
        {
            var pixels = CreateDirectTextAtlasUploadRegion(page.Pixels, page.Width, x, y, width, height);
            QueueTextureUpdate(new(UiTextureUpdateKind.Update, page.TextureId, UiTextureFormat.Rgba8Unorm,
                page.Width, page.Height, pixels) { X = x, Y = y, RegionWidth = width, RegionHeight = height });
        }
        cell = new(mask.X, mask.Y, mask.Width, mask.Height, x + padding, y + padding);
        cache.Add(key, cell);
        return true;
    }
}
