namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal (int Entries, int Retained, int Limit) DirectTextCacheOccupancy
        => (_directTextCache.Count, _layerCacheStore?.RetainedTextKeys.Count ?? 0, DirectTextCacheMaxEntries);
    private static float ValidateDirectTextAdvance(float advance)
    {
        if (!float.IsFinite(advance) || advance < 0)
            throw new InvalidOperationException("The text backend returned a non-finite or negative logical advance.");
        return advance;
    }

    private void StoreDirectTextEntry(DirectTextCacheKey key, DirectTextCacheEntry entry, bool retainInContext = true)
    {
        if (retainInContext)
        {
            ref var cached = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(_directTextCache, key, out var exists);
            cached = entry;
            if (!exists)
                _directTextCacheAllRetained = false;
        }
        _lastDirectTextCacheKey = key;
        _lastDirectTextCacheEntry = entry;
        _lastDirectTextCacheValid = true;
    }

    private DirectTextCacheEntry EnsureDirectTextPixels(DirectTextCacheKey key, DirectTextCacheEntry entry, float fontSize)
    {
        if (entry.HasRasterPixels)
            return entry;
        if (!_platformTextBackend!.TryRasterize(new(key.FontPath, key.SecondaryFontPath, key.Text, fontSize)
            { RightToLeft = key.RightToLeft }, out var pixels))
            throw new InvalidOperationException("The text backend could lay out visible text but could not rasterize it.");
        var transferred = false;
        try
        {
            var advance = ValidateDirectTextAdvance(pixels.Advance);
            if (entry.Width != pixels.Width || entry.Height != pixels.Height || entry.Baseline != pixels.Baseline || entry.OriginX != pixels.OriginX
                || entry.FontAscent != pixels.FontAscent || entry.Advance != advance)
                throw new InvalidOperationException("The text backend returned different layout and raster metrics for the same request.");
            entry = entry with
            {
                PendingPixels = pixels.RgbaPixels,
                PixelOwner = pixels.PixelOwner,
                HasRasterPixels = true
            };
            StoreDirectTextEntry(key, entry);
            transferred = true;
            return entry;
        }
        finally
        {
            if (!transferred)
                pixels.PixelOwner?.Dispose();
        }
    }
}
