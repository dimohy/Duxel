namespace Duxel.Core;

/// <summary>OpenType face metadata in an explicitly requested name language; missing fields remain null.</summary>
public sealed record PlatformFontMetadata(string? FamilyName, string? SubfamilyName, int? Weight,
    bool? Italic, bool SimulatedOblique, string NameLanguage)
{
    public bool? Oblique
    {
        get; init;
    }
    public void Validate()
    {
        ValidateNameLanguage(NameLanguage);
        if (Weight is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(Weight));
        if (FamilyName?.Contains('\0') == true || SubfamilyName?.Contains('\0') == true)
            throw new ArgumentException("Font names cannot contain NUL.");
    }
    public static void ValidateNameLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        if (language.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Font name language must be an explicit language tag.", nameof(language));
    }
}

/// <summary>Selected face for a half-open UTF16 source interval, before shaping. Separators without glyphs are omitted.</summary>
public readonly record struct PlatformTextFontRun(int StartUtf16, int EndUtf16, PlatformFontMetadata Font);

/// <summary>Optional source-font selection capability. It does not replace layout/shaping validation.</summary>
public interface IPlatformTextFontRunBackend
{
    IReadOnlyList<PlatformTextFontRun> GetFontRuns(in PlatformTextMeasureRequest request, string nameLanguage);
}
