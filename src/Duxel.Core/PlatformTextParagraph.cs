using System.Buffers;
using System.Text;

namespace Duxel.Core;

public enum PlatformTextParagraphDirection
{
    LeftToRight,
    RightToLeft
}

/// <summary>A complete source interval with explicit typography; its index is also the paint-style slot.</summary>
public readonly record struct PlatformTextParagraphStyle(int Start, int Length, string FontPath,
    string? SecondaryFontPath, float FontSize);

/// <summary>One U+FFFC source character reserved for an externally painted atomic object.</summary>
public readonly record struct PlatformTextParagraphObject(int Start, float Width, float Height, float Baseline);

/// <summary>Native top-left allocation, retained without an object handle or a glyph surrogate.</summary>
public readonly record struct PlatformTextParagraphObjectPlacement(int Start, UiRect Bounds, bool RightToLeft);

/// <summary>Immutable paragraph input. Width constrains native wrapping; infinity explicitly selects no wrapping.</summary>
public sealed class PlatformTextParagraphRequest
{
    private readonly PlatformTextParagraphStyle[] _styles;
    private readonly PlatformTextParagraphObject[] _objects;
    public string Text
    {
        get;
    }
    public ReadOnlyMemory<PlatformTextParagraphStyle> Styles => _styles;
    public ReadOnlyMemory<PlatformTextParagraphObject> Objects => _objects;
    public float Width
    {
        get;
    }
    public PlatformTextParagraphDirection Direction
    {
        get;
    }

    public PlatformTextParagraphRequest(string text, ReadOnlySpan<PlatformTextParagraphStyle> styles,
        float width = float.PositiveInfinity, PlatformTextParagraphDirection direction = PlatformTextParagraphDirection.LeftToRight)
        : this(text, styles, [], width, direction)
    {
    }

    public PlatformTextParagraphRequest(string text, ReadOnlySpan<PlatformTextParagraphStyle> styles,
        ReadOnlySpan<PlatformTextParagraphObject> objects, float width = float.PositiveInfinity,
        PlatformTextParagraphDirection direction = PlatformTextParagraphDirection.LeftToRight)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (styles.IsEmpty || float.IsNaN(width) || width < 0 || (uint)direction > 1)
            throw new ArgumentException("A paragraph requires explicit styles, a nonnegative width and a valid base direction.");
        for (var position = 0; position < text.Length;)
        {
            if (Rune.DecodeFromUtf16(text.AsSpan(position), out _, out var consumed) != OperationStatus.Done)
                throw new ArgumentException("Paragraph source must contain valid UTF16.", nameof(text));
            position += consumed;
        }
        var covered = 0;
        foreach (var style in styles)
        {
            if (style.Start != covered || style.Length < 0 || style.Length > text.Length - covered
                || string.IsNullOrWhiteSpace(style.FontPath)
                || style.SecondaryFontPath is not null && string.IsNullOrWhiteSpace(style.SecondaryFontPath)
                || !float.IsFinite(style.FontSize) || style.FontSize <= 0)
                throw new ArgumentException("Paragraph typography must cover valid original source intervals exactly.", nameof(styles));
            covered += style.Length;
            if (covered > 0 && covered < text.Length && char.IsHighSurrogate(text[covered - 1]) && char.IsLowSurrogate(text[covered]))
                throw new ArgumentException("A typography interval cannot split a UTF16 surrogate pair.", nameof(styles));
        }
        if (covered != text.Length)
            throw new ArgumentException("Paragraph typography omits original source text.", nameof(styles));
        var previousObject = -1;
        foreach (var item in objects)
        {
            if (item.Start <= previousObject || item.Start >= text.Length || text[item.Start] != '\uFFFC'
                || !float.IsFinite(item.Width) || item.Width < 0 || !float.IsFinite(item.Height) || item.Height < 0
                || !float.IsFinite(item.Baseline))
                throw new ArgumentException("Paragraph objects require ordered unique replacement characters and finite metrics.", nameof(objects));
            previousObject = item.Start;
        }
        Text = text;
        _styles = styles.ToArray();
        _objects = objects.ToArray();
        Width = width;
        Direction = direction;
    }
}

/// <summary>Original native glyph order and offsets together with absolute paragraph positions.</summary>
public readonly record struct PlatformTextParagraphGlyph(uint Index, int SourcePosition, float Advance,
    float AdvanceOffset, float AscenderOffset, float X, float Y);

/// <summary>Managed paint data; no native font face, layout, callback or other handle may be retained.</summary>
public sealed class PlatformTextParagraphRun
{
    private readonly PlatformTextParagraphGlyph[] _glyphs;
    internal int RasterHash { get; }
    private static UiVector2 Fraction(UiVector2 origin) => new(origin.X - MathF.Floor(origin.X), origin.Y - MathF.Floor(origin.Y));
    internal bool RasterEquals(PlatformTextParagraphRun other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (RasterHash != other.RasterHash || FontPath != other.FontPath || FontSize != other.FontSize
            || BidiLevel != other.BidiLevel || Fraction(Origin) != Fraction(other.Origin) || _glyphs.Length != other._glyphs.Length)
            return false;
        for (var i = 0; i < _glyphs.Length; i++)
        {
            var a = _glyphs[i];
            var b = other._glyphs[i];
            if (a.Index != b.Index || a.Advance != b.Advance || a.AdvanceOffset != b.AdvanceOffset
                || a.AscenderOffset != b.AscenderOffset || a.X - Origin.X != b.X - other.Origin.X
                || a.Y - Origin.Y != b.Y - other.Origin.Y)
                return false;
        }
        return true;
    }
    public int StyleIndex
    {
        get;
    }
    public string FontPath
    {
        get;
    }
    public float FontSize
    {
        get;
    }
    public uint BidiLevel
    {
        get;
    }
    public UiVector2 Origin
    {
        get;
    }
    public ReadOnlyMemory<PlatformTextParagraphGlyph> Glyphs => _glyphs;

    public PlatformTextParagraphRun(int styleIndex, string fontPath, float fontSize, uint bidiLevel,
        UiVector2 origin, ReadOnlySpan<PlatformTextParagraphGlyph> glyphs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontPath);
        if (styleIndex < 0 || !float.IsFinite(fontSize) || fontSize <= 0 || !float.IsFinite(origin.X) || !float.IsFinite(origin.Y))
            throw new ArgumentException("Paragraph runs require a valid style, explicit font and finite native origin.");
        foreach (var glyph in glyphs)
            if (glyph.SourcePosition < 0 || !float.IsFinite(glyph.Advance) || glyph.Advance < 0
                || !float.IsFinite(glyph.AdvanceOffset) || !float.IsFinite(glyph.AscenderOffset)
                || !float.IsFinite(glyph.X) || !float.IsFinite(glyph.Y))
                throw new ArgumentException("Paragraph glyphs require original source positions and finite native placement.", nameof(glyphs));
        StyleIndex = styleIndex;
        FontPath = fontPath;
        FontSize = fontSize;
        BidiLevel = bidiLevel;
        Origin = origin;
        _glyphs = glyphs.ToArray();
        // Pixel content follows glyph geometry and subpixel phase, independently
        // of source/style indices and whole-pixel placement after paragraph reflow.
        var hash = new HashCode();
        hash.Add(FontPath);
        hash.Add(FontSize);
        hash.Add(BidiLevel);
        hash.Add(Fraction(Origin));
        foreach (var glyph in _glyphs)
        {
            hash.Add(glyph.Index);
            hash.Add(glyph.Advance);
            hash.Add(glyph.AdvanceOffset);
            hash.Add(glyph.AscenderOffset);
            hash.Add(glyph.X - Origin.X);
            hash.Add(glyph.Y - Origin.Y);
        }
        RasterHash = hash.ToHashCode();
    }
}

/// <summary>A visual line in original source coordinates; Length excludes its hard newline.</summary>
public readonly record struct PlatformTextParagraphLine(int Start, int Length, int NewlineLength,
    float Top, float Height, float Baseline, PlatformTextLineGeometry Geometry)
{
    public float Advance => Geometry.Advance;
    /// <summary>Native formatted width excluding trailing whitespace; source carets and Advance still include it.</summary>
    public float WidthWithoutTrailingWhitespace
    {
        get; init;
    }
}
public readonly record struct PlatformTextParagraphCaret(float X, float Y);

/// <summary>Immutable native paragraph snapshot shared by measurement, painting and source-based interaction.</summary>
public sealed class PlatformTextParagraphLayout
{
    private readonly PlatformTextParagraphRun[] _runs;
    private readonly PlatformTextParagraphLine[] _lines;
    private readonly PlatformTextParagraphCaret[] _carets;
    private readonly PlatformTextParagraphObjectPlacement[] _objects;
    public ReadOnlyMemory<PlatformTextParagraphRun> Runs => _runs;
    public ReadOnlyMemory<PlatformTextParagraphLine> Lines => _lines;
    public ReadOnlyMemory<PlatformTextParagraphCaret> Carets => _carets;
    public ReadOnlyMemory<PlatformTextParagraphObjectPlacement> Objects => _objects;
    public float Width
    {
        get;
    }
    public float Height
    {
        get;
    }

    public PlatformTextParagraphLayout(ReadOnlySpan<PlatformTextParagraphRun> runs,
        ReadOnlySpan<PlatformTextParagraphLine> lines, ReadOnlySpan<PlatformTextParagraphCaret> carets, float width, float height)
        : this(runs, lines, carets, width, height, [])
    {
    }

    public PlatformTextParagraphLayout(ReadOnlySpan<PlatformTextParagraphRun> runs,
        ReadOnlySpan<PlatformTextParagraphLine> lines, ReadOnlySpan<PlatformTextParagraphCaret> carets, float width, float height,
        ReadOnlySpan<PlatformTextParagraphObjectPlacement> objects)
    {
        if (lines.IsEmpty || carets.IsEmpty || !float.IsFinite(width) || width < 0 || !float.IsFinite(height) || height < 0)
            throw new ArgumentException("Paragraph output requires native lines, all source carets and finite dimensions.");
        var covered = 0;
        var bottom = 0f;
        foreach (var line in lines)
        {
            if (line.Geometry is null || line.Geometry.Length != line.Length
                || line.Start != covered || line.Length < 0 || line.NewlineLength < 0
                || line.Length > carets.Length - 1 - covered || line.NewlineLength > carets.Length - 1 - covered - line.Length
                || !float.IsFinite(line.Top) || line.Top < bottom || !float.IsFinite(line.Height) || line.Height < 0
                || !float.IsFinite(line.Baseline) || line.Baseline < 0 || line.Baseline > line.Height
                || !float.IsFinite(line.Advance) || line.Advance < 0)
                throw new ArgumentException("Native paragraph lines must cover the complete original source without gaps.");
            if (!float.IsFinite(line.WidthWithoutTrailingWhitespace) || line.WidthWithoutTrailingWhitespace < 0)
                throw new ArgumentException("Native formatted line width must be finite and nonnegative.");
            covered += line.Length + line.NewlineLength;
            bottom = line.Top + line.Height;
        }
        if (covered != carets.Length - 1 || bottom > height + .001f)
            throw new ArgumentException("Native paragraph output omits source text or exceeds its declared height.");
        foreach (var caret in carets)
            if (!float.IsFinite(caret.X) || !float.IsFinite(caret.Y))
                throw new ArgumentException("Paragraph carets must be finite.");
        foreach (var run in runs)
        {
            ArgumentNullException.ThrowIfNull(run);
            foreach (var glyph in run.Glyphs.Span)
                if (glyph.SourcePosition >= carets.Length - 1)
                    throw new ArgumentException("Paragraph glyph source position exceeds the original input.");
        }
        var previousObject = -1;
        foreach (var item in objects)
        {
            var bounds = item.Bounds;
            if (item.Start <= previousObject || item.Start >= carets.Length - 1
                || !float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y)
                || !float.IsFinite(bounds.Width) || bounds.Width < 0 || !float.IsFinite(bounds.Height) || bounds.Height < 0)
                throw new ArgumentException("Paragraph object allocations require ordered source positions and finite bounds.", nameof(objects));
            previousObject = item.Start;
        }
        _objects = objects.ToArray();
        _runs = runs.ToArray();
        _lines = lines.ToArray();
        _carets = carets.ToArray();
        Width = width;
        Height = height;
    }
}

public readonly record struct PlatformTextParagraphRaster(int Width, int Height, ReadOnlyMemory<byte> RgbaPixels, UiVector2 Origin)
{
    public IMemoryOwner<byte>? PixelOwner
    {
        get; init;
    }
}

/// <summary>Shapes full styled paragraphs once; painting must consume the returned glyphs without reshaping substrings.</summary>
public interface IPlatformTextParagraphBackend
{
    PlatformTextParagraphLayout CreateParagraph(PlatformTextParagraphRequest request, bool vectorMetrics);
    PlatformTextParagraphRaster RasterizeParagraphRun(PlatformTextParagraphRun run);
    PlatformTextMeshResult CreateParagraphRunMesh(PlatformTextParagraphRun run, float flatteningTolerance = .001f);
}
