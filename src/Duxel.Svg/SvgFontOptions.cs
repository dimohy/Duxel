namespace Duxel.Svg;

/// <summary>Explicit family alias for one font file/collection face. Style comes from the font, not the host font manager.</summary>
public sealed record SvgFontFace(string Family, string Path, int CollectionIndex = 0);
/// <summary>Snapshots explicit font files when the store is created. The byte limit applies to each file.</summary>
public sealed record SvgFontOptions(string DefaultFamily, IReadOnlyList<SvgFontFace> Faces,
    long MaximumFontBytes = 32 * 1024 * 1024);
/// <summary>Counts registered native faces, provider requests and cached glyph-coverage checks; not raster work.</summary>
public readonly record struct SvgFontStatistics(int LoadedFaces, long TypefaceRequests, long GlyphChecks);
