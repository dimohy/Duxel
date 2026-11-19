using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Duxel.Core;

/// <summary>Fluent name rules with automatic Hangul first-syllable initials.</summary>
public static partial class UiAvatarInitials
{
    [GeneratedRegex(@"[\(\[\{][^\)\]\}]*([\)\]\}]|$)")]
    private static partial Regex Enclosures();
    [GeneratedRegex(@"[\0-\u001F\!-/:-@\[-`\{-\u00BF\u0250-\u036F\uE000-\uFFFF]")]
    private static partial Regex UnwantedCharacters();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
    [GeneratedRegex(@"^(?:\d[\d\s]*\d|\d[\d\s]*(?:ext|x)\s*\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PhoneNumber();

    public static string FromName(string name, bool firstOnly = false, bool rightToLeft = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        var clean = Enclosures().Replace(name, match => match.Groups[1].Value.Length > 0 ? string.Empty : match.Value);
        clean = Whitespace().Replace(UnwantedCharacters().Replace(clean, string.Empty), " ").Trim();
        if (clean.Length == 0 || PhoneNumber().IsMatch(clean)) return string.Empty;
        var firstRune = Rune.GetRuneAt(clean, 0).Value;
        if (firstRune is >= 0x1100 and <= 0x11FF or >= 0x3130 and <= 0x318F or >= 0xA960 and <= 0xA97F
            or >= 0xAC00 and <= 0xD7AF or >= 0xD7B0 and <= 0xD7FF)
            return StringInfo.GetNextTextElement(clean).Normalize(NormalizationForm.FormC);
        if (firstRune is >= 0x0600 and <= 0x06FF or >= 0x0750 and <= 0x077F or >= 0x08A0 and <= 0x08FF
            or >= 0x3040 and <= 0x30FF
            or >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF or >= 0xF900 and <= 0xFAFF)
            return string.Empty;
        var words = clean.Split(' ');
        var first = Rune.GetRuneAt(words[0], 0).ToString().ToUpperInvariant();
        if (firstOnly || words.Length is not (2 or 3)) return first;
        var last = Rune.GetRuneAt(words[^1], 0).ToString().ToUpperInvariant();
        return rightToLeft ? last + first : first + last;
    }
}
