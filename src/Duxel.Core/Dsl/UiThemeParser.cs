using System.Globalization;

namespace Duxel.Core.Dsl;

/// <summary>
/// Parses .duxel-theme text into a <see cref="UiThemeDef"/>.
/// </summary>
/// <remarks>
/// Syntax:
/// <code>
/// Theme "MyDark" : Dark
///   Text = #E6E8EA
///   WindowBg = #1E1E2E
///   Button = #4A90D9
/// </code>
/// - Colors: #RRGGBB (alpha FF) or #AARRGGBB (8 hex digits).
/// - Base preset: Windows11 | Windows11Dark | Dark | Light | Classic (optional, defaults to Windows11).
/// - Comments: # or // at line start (after optional whitespace).
/// </remarks>
public static class UiThemeParser
{
    private static readonly Dictionary<string, UiStyleColor> ColorMap = BuildColorMap();
    private static readonly Dictionary<string, UiDesignToken> DesignTokenMap = BuildDesignTokenMap();

    public static UiThemeDef Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string? name = null;
        string? basePreset = null;
        var overrides = new List<UiThemeColorEntry>();
        var designTokenOverrides = new List<UiThemeDesignTokenEntry>();
        var lineNumber = 0;

        foreach (var rawLine in text.AsSpan().EnumerateLines())
        {
            lineNumber++;
            var line = rawLine.Trim();

            if (line.IsEmpty || line.StartsWith("//") || line[0] is '#' && !line.StartsWith("#:"))
            {
                // Skip blank lines and comments.
                // But do NOT skip lines starting with #: (preprocessor-like directives for future use).
                // However, color hex values like #FF0000 appear only after '=', so a line
                // starting with # (not #:) that is not inside an assignment is a comment.
                // We detect assignment lines below.

                // Re-check: if the line is purely '#...' and contains '=', it could be a color assignment
                // with a color name starting with... no, UiStyleColor names never start with #.
                // So # at line start with no '=' context is always a comment.
                continue;
            }

            if (line.StartsWith("Theme", StringComparison.OrdinalIgnoreCase))
            {
                ParseHeader(line, lineNumber, out name, out basePreset);
                continue;
            }

            var eqIdx = line.IndexOf('=');
            if (eqIdx <= 0)
            {
                throw new FormatException($"Line {lineNumber}: expected 'ColorName = #HexValue' or 'Design.Token = Number', got '{line}'.");
            }

            var nameToken = line[..eqIdx].Trim();
            var valueToken = line[(eqIdx + 1)..].Trim();

            if (valueToken.StartsWith("#"))
            {
                if (!TryParseColorName(nameToken, out var styleColor))
                {
                    throw new FormatException($"Line {lineNumber}: unknown color name '{nameToken}'.");
                }

                if (!TryParseHexColor(valueToken, out var uiColor))
                {
                    throw new FormatException($"Line {lineNumber}: invalid hex color '{valueToken}'. Expected #RRGGBB or #AARRGGBB.");
                }

                overrides.Add(new UiThemeColorEntry(styleColor, uiColor));
                continue;
            }

            if (!TryParseDesignTokenName(nameToken, out var designToken))
            {
                throw new FormatException($"Line {lineNumber}: unknown design token '{nameToken}'. Use Design.<TokenName> for shape tokens.");
            }

            if (!float.TryParse(valueToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var tokenValue))
            {
                throw new FormatException($"Line {lineNumber}: invalid design token value '{valueToken}'. Expected invariant-culture number.");
            }

            designTokenOverrides.Add(new UiThemeDesignTokenEntry(designToken, tokenValue));
        }

        if (name is null)
        {
            throw new FormatException("Missing 'Theme \"Name\"' header.");
        }

        return new UiThemeDef(name, basePreset, overrides, designTokenOverrides);
    }

    private static void ParseHeader(ReadOnlySpan<char> line, int lineNumber, out string name, out string? basePreset)
    {
        // Expected: Theme "Name" [: PresetName]
        var afterTheme = line[5..].Trim(); // skip "Theme"

        // Extract quoted name
        var firstQuote = afterTheme.IndexOf('"');
        if (firstQuote < 0)
        {
            throw new FormatException($"Line {lineNumber}: Theme name must be quoted, e.g. Theme \"MyDark\".");
        }

        var afterOpen = afterTheme[(firstQuote + 1)..];
        var closeQuote = afterOpen.IndexOf('"');
        if (closeQuote < 0)
        {
            throw new FormatException($"Line {lineNumber}: missing closing quote in Theme name.");
        }

        name = afterOpen[..closeQuote].ToString();
        var rest = afterOpen[(closeQuote + 1)..].Trim();

        if (rest.IsEmpty)
        {
            basePreset = null;
            return;
        }

        if (rest[0] is not ':')
        {
            throw new FormatException($"Line {lineNumber}: expected ':' after Theme name for base preset.");
        }

        basePreset = rest[1..].Trim().ToString();
        if (basePreset.Length is 0)
        {
            throw new FormatException($"Line {lineNumber}: missing preset name after ':'.");
        }
    }

    private static bool TryParseColorName(ReadOnlySpan<char> name, out UiStyleColor result)
    {
        // Fast lookup using pre-built dictionary
        var key = name.ToString();
        return ColorMap.TryGetValue(key, out result);
    }

    private static bool TryParseDesignTokenName(ReadOnlySpan<char> name, out UiDesignToken result)
    {
        const string designPrefix = "Design.";
        if (name.StartsWith(designPrefix, StringComparison.OrdinalIgnoreCase))
        {
            name = name[designPrefix.Length..];
        }

        var key = name.ToString();
        return DesignTokenMap.TryGetValue(key, out result);
    }

    private static bool TryParseHexColor(ReadOnlySpan<char> value, out UiColor result)
    {
        result = default;

        if (value.IsEmpty || value[0] is not '#')
        {
            return false;
        }

        var hex = value[1..]; // strip '#'

        if (hex.Length is 6)
        {
            // #RRGGBB → alpha = FF
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                return false;
            }

            var r = (byte)((rgb >> 16) & 0xFF);
            var g = (byte)((rgb >> 8) & 0xFF);
            var b = (byte)(rgb & 0xFF);
            result = new UiColor(r, g, b, 255);
            return true;
        }

        if (hex.Length is 8)
        {
            // #AARRGGBB
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
            {
                return false;
            }

            var a = (byte)((argb >> 24) & 0xFF);
            var r = (byte)((argb >> 16) & 0xFF);
            var g = (byte)((argb >> 8) & 0xFF);
            var b = (byte)(argb & 0xFF);
            result = new UiColor(r, g, b, a);
            return true;
        }

        return false;
    }

    private static Dictionary<string, UiStyleColor> BuildColorMap()
    {
        var map = new Dictionary<string, UiStyleColor>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in Enum.GetValues<UiStyleColor>())
        {
            map[value.ToString()] = value;
        }

        return map;
    }

    private static Dictionary<string, UiDesignToken> BuildDesignTokenMap()
    {
        var map = new Dictionary<string, UiDesignToken>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in Enum.GetValues<UiDesignToken>())
        {
            map[value.ToString()] = value;
        }

        return map;
    }
}
