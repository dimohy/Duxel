namespace Duxel.Svg;

/// <summary>Reads CSS resource tokens without interpreting comments or quoted property text as executable rules.</summary>
internal static class SvgCssResources
{
    internal static IEnumerable<string> Read(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            SkipTrivia(text, ref index);
            if (index == text.Length) break;
            if (text[index] is '\'' or '"') { ReadString(text, ref index); continue; }
            if (text[index] == '@')
            {
                index++;
                var rule = ReadName(text, ref index);
                if (rule.Equals("import", StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException("External SVG stylesheets are not loaded.");
                if (rule.Equals("font-face", StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException("Document font-face resources are not supported; register explicit SVG font files.");
                continue;
            }
            if (IsName(text[index]) || text[index] == '\\')
            {
                var name = ReadName(text, ref index);
                if (!name.Equals("url", StringComparison.OrdinalIgnoreCase)) continue;
                SkipTrivia(text, ref index);
                if (index < text.Length && text[index] == '(')
                {
                    index++;
                    yield return ReadUrl(text, ref index);
                }
                continue;
            }
            index++;
        }
    }

    private static string ReadName(string text, ref int index)
    {
        var start = index;
        while (index < text.Length)
        {
            if (text[index] == '\\') SkipEscape(text, ref index);
            else if (IsName(text[index])) index++;
            else break;
        }
        return SvgSource.DecodeEscapes(text[start..index]);
    }

    private static string ReadString(string text, ref int index)
    {
        var quote = text[index++];
        var start = index;
        while (index < text.Length)
        {
            if (text[index] == '\\') { SkipEscape(text, ref index); continue; }
            if (text[index] == quote)
            {
                var value = SvgSource.DecodeEscapes(text[start..index]);
                index++;
                return value;
            }
            if (text[index] is '\r' or '\n' or '\f') throw new InvalidDataException("Unterminated SVG CSS string.");
            index++;
        }
        throw new InvalidDataException("Unterminated SVG CSS string.");
    }

    private static string ReadUrl(string text, ref int index)
    {
        SkipTrivia(text, ref index);
        if (index == text.Length) throw new InvalidDataException("Unterminated SVG CSS resource reference.");
        string target;
        if (text[index] is '\'' or '"') target = ReadString(text, ref index);
        else
        {
            var start = index;
            while (index < text.Length && text[index] != ')' && !IsWhitespace(text[index]))
            {
                if (text[index] == '\\') { SkipEscape(text, ref index); continue; }
                if (text[index] is '(' or '\'' or '"') throw new InvalidDataException("Malformed SVG CSS resource reference.");
                index++;
            }
            target = SvgSource.DecodeEscapes(text[start..index]);
        }
        SkipTrivia(text, ref index);
        if (index == text.Length || text[index++] != ')') throw new InvalidDataException("Malformed SVG CSS resource reference.");
        return target;
    }

    private static void SkipTrivia(string text, ref int index)
    {
        while (index < text.Length)
        {
            if (IsWhitespace(text[index])) { index++; continue; }
            if (text[index] != '/' || index + 1 == text.Length || text[index + 1] != '*') break;
            var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
            if (end < 0) throw new InvalidDataException("Unterminated SVG CSS comment.");
            index = end + 2;
        }
    }

    private static void SkipEscape(string text, ref int index)
    {
        if (++index == text.Length) throw new InvalidDataException("Incomplete SVG CSS escape.");
        var digits = 0;
        while (index < text.Length && digits < 6 && char.IsAsciiHexDigit(text[index])) { index++; digits++; }
        if (digits == 0 || index < text.Length && IsWhitespace(text[index]))
        {
            if (text[index++] == '\r' && index < text.Length && text[index] == '\n') index++;
        }
    }

    private static bool IsName(char value) => char.IsAsciiLetterOrDigit(value) || value is '_' or '-' || value >= 128;
    private static bool IsWhitespace(char value) => value is ' ' or '\t' or '\r' or '\n' or '\f';
}
