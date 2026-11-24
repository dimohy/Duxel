using System.Globalization;
using System.Text;

namespace Duxel.Core;

internal static class UiMenuLabelWrapping
{
    internal static string Wrap(UiControlContext context, string label, float width)
    {
        foreach (var rune in label.EnumerateRunes())
            if (IsEastAsianLetter(rune.Value))
                return WrapEastAsian(context, label, width);
        return context.WrapText(label, width);
    }

    private static string WrapEastAsian(UiControlContext context, string label, float width)
    {
        var output = new StringBuilder(label.Length);
        var paragraphs = label.Replace("\r", string.Empty, StringComparison.Ordinal).AsSpan();
        for (var paragraphStart = 0; paragraphStart <= paragraphs.Length;)
        {
            if (paragraphStart != 0)
                output.Append('\n');
            var paragraph = paragraphs[paragraphStart..];
            var newline = paragraph.IndexOf('\n');
            var text = (newline < 0 ? paragraph : paragraph[..newline]).ToString();
            var start = 0;
            var lastFit = 0;
            var previous = default(Rune);
            for (var next = 0; next < text.Length;)
            {
                Rune.DecodeFromUtf16(text.AsSpan(next), out var current, out _);
                if (next > start && CanBreak(previous, current))
                    FitThrough(next);
                previous = current;
                next += StringInfo.GetNextTextElementLength(text.AsSpan(next));
            }
            FitThrough(text.Length);
            output.Append(text.AsSpan(start));
            if (newline < 0)
                break;
            paragraphStart += newline + 1;

            void FitThrough(int end)
            {
                if (end == start || Fits(start, end))
                {
                    lastFit = end;
                    return;
                }
                if (lastFit > start)
                    AppendLine(lastFit);
                if (end < text.Length && !Fits(start, end))
                    AppendLine(end);
                lastFit = end;
            }

            bool Fits(int from, int to) => context.MeasureText(text.AsSpan(from, to - from).TrimEnd(' ').ToString()).X <= width;
            void AppendLine(int end)
            {
                output.Append(text.AsSpan(start, end - start).TrimEnd(' ')).Append('\n');
                start = end;
            }
        }
        return output.ToString();
    }

    private static bool CanBreak(Rune previous, Rune current)
    {
        if (previous.Value is 0x00A0 or 0x202F or 0x2060 or 0xFEFF or 0x200D
            || current.Value is ' ' or 0x00A0 or 0x202F or 0x2060 or 0xFEFF)
            return false;
        if (Rune.GetUnicodeCategory(previous) is UnicodeCategory.OpenPunctuation or UnicodeCategory.InitialQuotePunctuation
            || Rune.GetUnicodeCategory(current) is UnicodeCategory.ClosePunctuation or UnicodeCategory.FinalQuotePunctuation)
            return false;
        if (current.Value is '.' or ',' or '!' or '?' or ':' or ';' or 0x3001 or 0x3002 or 0x3005 or 0x303B
            or 0xFF01 or 0xFF0C or 0xFF0E or 0xFF1A or 0xFF1B or 0xFF1F or 0x30FC
            or 0x3083 or 0x3085 or 0x3087 or 0x308E or 0x30E3 or 0x30E5 or 0x30E7 or 0x30EE
            or 0x3041 or 0x3043 or 0x3045 or 0x3047 or 0x3049 or 0x3063
            or 0x30A1 or 0x30A3 or 0x30A5 or 0x30A7 or 0x30A9 or 0x30C3 or 0x30F5 or 0x30F6)
            return false;
        return previous.Value == ' ' || IsEastAsianLetter(previous.Value) || IsEastAsianLetter(current.Value);
    }

    private static bool IsEastAsianLetter(int value) => value is
        >= 0x1100 and <= 0x11FF or >= 0x3041 and <= 0x30FA or >= 0x3131 and <= 0x318E
        or >= 0x31F0 and <= 0x31FF or >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF
        or >= 0xA960 and <= 0xA97C or >= 0xAC00 and <= 0xD7FF or >= 0xF900 and <= 0xFAFF
        or >= 0xFF66 and <= 0xFF9D or >= 0x1B000 and <= 0x1B2FF
        or >= 0x20000 and <= 0x2FA1F or >= 0x30000 and <= 0x3347F;
}
