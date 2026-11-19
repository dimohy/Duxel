namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private int _validationParagraphRasters;
    private int _validationEmptyParagraphRasters;
    private int _validationParagraphEvictions;
    private int _validationEmptyParagraphEvictions;

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
        if (key.ParagraphRun is null)
            return;
        _validationParagraphEvictions++;
        if (entry.Width == 0 || entry.Height == 0)
            _validationEmptyParagraphEvictions++;
    }
}
