namespace Duxel.Platform.Linux;

internal static class LinuxParagraphSourceMap
{
    internal static int GlyphPosition(ReadOnlySpan<int> sourceBytes, int nativeByte)
    {
        var low = 0;
        var high = sourceBytes.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (sourceBytes[middle] <= nativeByte)
                low = middle + 1;
            else
                high = middle;
        }
        var source = low - 1;
        // Expanded tab bytes belong to the tab; duplicate UTF16 entries belong to the first surrogate unit.
        while (source > 0 && sourceBytes[source - 1] == sourceBytes[source])
            source--;
        return source;
    }
}
