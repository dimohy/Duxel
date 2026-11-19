using System.Text;

namespace Duxel.Svg.Managed;

internal static class SvgCssEscapes
{
    internal static string Decode(string text)
    {
        var decoded = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\') { decoded.Append(text[i]); continue; }
            if (++i == text.Length) throw new InvalidDataException("Incomplete SVG CSS escape.");
            var codepoint = 0;
            var digits = 0;
            while (i < text.Length && digits < 6 && char.IsAsciiHexDigit(text[i]))
            {
                var c = text[i++];
                codepoint = codepoint * 16 + (c <= '9' ? c - '0' : char.ToUpperInvariant(c) - 'A' + 10);
                digits++;
            }
            if (digits == 0)
            {
                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                else if (text[i] is not ('\r' or '\n' or '\f')) decoded.Append(text[i]);
            }
            else
            {
                if (!Rune.IsValid(codepoint)) throw new InvalidDataException("Invalid SVG CSS code point.");
                decoded.Append(new Rune(codepoint).ToString());
                if (i == text.Length || !char.IsWhiteSpace(text[i])) i--;
                else if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
        }
        return decoded.ToString();
    }
}
