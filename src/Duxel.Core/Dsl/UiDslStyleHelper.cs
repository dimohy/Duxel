using System.Globalization;

namespace Duxel.Core.Dsl;

/// <summary>
/// Extracts Style.* named arguments from DSL node args and pushes/pops style colors.
/// </summary>
/// <remarks>
/// DSL syntax: <c>Button Text="Save" Style.Button=#28A745 Style.ButtonHovered=#38B755</c>
/// </remarks>
internal static class UiDslStyleHelper
{
    private const string StylePrefix = "Style.";

    /// <summary>
    /// Scans <paramref name="args"/> for <c>Style.ColorName=#HexValue</c> entries,
    /// pushes corresponding <see cref="UiStyleColor"/> overrides, and returns the count pushed.
    /// </summary>
    public static int PushInlineStyles(UiImmediateContext ui, IReadOnlyList<string> args)
    {
        var count = 0;

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (!token.StartsWith(StylePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var eqIdx = token.IndexOf('=', StylePrefix.Length);
            if (eqIdx <= StylePrefix.Length)
            {
                continue;
            }

            var colorName = token.AsSpan(StylePrefix.Length, eqIdx - StylePrefix.Length);
            var hexValue = token.AsSpan(eqIdx + 1);

            if (TryParseStyleColor(colorName, out var styleColor) &&
                TryParseHexColor(hexValue, out var uiColor))
            {
                ui.PushStyleColor(styleColor, uiColor);
                count++;
            }
        }

        return count;
    }

    private static bool TryParseStyleColor(ReadOnlySpan<char> name, out UiStyleColor result)
    {
        // Use Enum.TryParse with ReadOnlySpan<char> overload (.NET 8+)
        return Enum.TryParse(name, ignoreCase: true, out result);
    }

    private static bool TryParseHexColor(ReadOnlySpan<char> value, out UiColor result)
    {
        result = default;

        if (value.IsEmpty || value[0] is not '#')
        {
            return false;
        }

        var hex = value[1..];

        if (hex.Length is 6)
        {
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
}
