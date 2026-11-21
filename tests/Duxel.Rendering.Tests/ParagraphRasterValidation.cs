namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private int _validationParagraphRasters;
    private int _validationEmptyParagraphRasters;
    private int _validationParagraphEvictions;
    private int _validationEmptyParagraphEvictions;
    private long _validationCodeLayoutTicks, _validationParagraphRasterTicks;
    internal bool ValidationParagraphTimingEnabled { get; set; }
    private readonly bool _validationDisableSharedCodeLayout =
        Environment.GetEnvironmentVariable("DUXEL_TEST_DISABLE_SHARED_CODE_LAYOUT") == "1";
    internal int ValidationDirectTextCacheLimit { get; set; }
    internal int ValidationDirectTextCacheEntries => _directTextCache.Count;
    internal (long Layout, long Raster) ValidationParagraphTiming => (_validationCodeLayoutTicks, _validationParagraphRasterTicks);

    partial void BeginValidationParagraphTiming(ref long timestamp)
    {
        if (ValidationParagraphTimingEnabled)
            timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    partial void SelectValidationDirectTextCacheLimit(ref int entryLimit)
    {
        if (ValidationDirectTextCacheLimit > 0)
            entryLimit = ValidationDirectTextCacheLimit;
    }

    partial void SelectValidationCodeLineLayoutCache(ref UiCodeLineLayoutCache? cache)
    {
        if (_validationDisableSharedCodeLayout)
            cache = null;
    }

    partial void EndValidationParagraphTiming(long timestamp, bool raster)
    {
        if (timestamp == 0)
            return;
        var elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - timestamp;
        if (raster)
            _validationParagraphRasterTicks += elapsed;
        else
            _validationCodeLayoutTicks += elapsed;
    }

    internal (int Rasters, int EmptyRasters, int Evictions, int EmptyEvictions) ValidationParagraphRasterCounts =>
        (_validationParagraphRasters, _validationEmptyParagraphRasters, _validationParagraphEvictions,
            _validationEmptyParagraphEvictions);

    partial void ObserveValidationParagraphRaster(bool empty)
    {
        _validationParagraphRasters++;
        if (empty)
            _validationEmptyParagraphRasters++;
    }

    partial void ObserveValidationParagraphEviction(in DirectTextCacheKey key, in DirectTextCacheEntry entry)
    {
        if (key.ParagraphRun is null && key.WholeParagraph is null)
            return;
        _validationParagraphEvictions++;
        if (entry.Width == 0 || entry.Height == 0)
            _validationEmptyParagraphEvictions++;
    }
}
