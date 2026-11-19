using System.Text;

namespace Duxel.Interop.Windows;

// Unicode 17 section 3.12: modern Hangul composition for glyph input only.
// Original UTF16 positions remain authoritative for editing and accessibility.
internal static class DirectWriteHangulInput
{
    internal readonly record struct Input(string Text, uint[]? OriginalPositions);
    internal static Input Prepare(string source)
    {
        var first = 0;
        while (first < source.Length && !TryCompose(source, first, out _, out _))
            first++;
        if (first == source.Length)
            return new(source, null);
        var text = new StringBuilder(source.Length);
        var positions = new uint[source.Length];
        for (var index = 0; index < source.Length;)
        {
            positions[text.Length] = (uint)index;
            if (TryCompose(source, index, out var syllable, out var consumed))
            {
                text.Append(syllable);
                index += consumed;
            }
            else
                text.Append(source[index++]);
        }
        return new(text.ToString(), positions);
    }
    private static bool TryCompose(string text, int index, out char syllable, out int consumed)
    {
        const int lBase = 0x1100, vBase = 0x1161, tBase = 0x11A7, sBase = 0xAC00;
        const int lCount = 19, vCount = 21, tCount = 28, nCount = vCount * tCount, sCount = lCount * nCount;
        syllable = default;
        consumed = 0;
        var value = (int)text[index];
        if (value >= lBase && value < lBase + lCount && index + 1 < text.Length)
        {
            var vowel = text[index + 1] - vBase;
            if ((uint)vowel >= vCount)
                return false;
            value = sBase + (value - lBase) * nCount + vowel * tCount;
            consumed = 2;
        }
        else if (value >= sBase && value < sBase + sCount && (value - sBase) % tCount == 0)
            consumed = 1;
        else
            return false;
        if (index + consumed < text.Length)
        {
            var trailing = text[index + consumed] - tBase;
            if (trailing > 0 && trailing < tCount)
            {
                value += trailing;
                consumed++;
            }
        }
        if (consumed == 1)
            return false;
        syllable = (char)value;
        return true;
    }
}
