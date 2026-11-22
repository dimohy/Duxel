using System.Globalization;
using System.Text;
using Duxel.Core;
using static Duxel.Platform.Linux.LinuxFontNative;

namespace Duxel.Platform.Linux;

public sealed unsafe partial class LinuxPlatformTextBackend
{
    // Keep a space in the preceding face when that face contains it.
    internal static LinuxFontResource SelectFont(LinuxFontResource primary, LinuxFontResource? secondary, Rune rune,
        LinuxFontResource? preceding)
    {
        var codePoint = (nuint)rune.Value;
        if (preceding is not null && !ReferenceEquals(preceding, primary)
            && Rune.GetUnicodeCategory(rune) == UnicodeCategory.SpaceSeparator
            && FT_Get_Char_Index(preceding.Face, codePoint) != 0)
            return preceding;
        if (FT_Get_Char_Index(primary.Face, codePoint) != 0)
            return primary;
        if (secondary is not null && FT_Get_Char_Index(secondary.Face, codePoint) != 0)
            return secondary;
        throw new NotSupportedException($"No explicitly supplied font contains U+{rune.Value:X4}.");
    }

    public IReadOnlyList<PlatformTextFontRun> GetFontRuns(in PlatformTextMeasureRequest request, string nameLanguage)
    {
        PlatformFontMetadata.ValidateNameLanguage(nameLanguage);
        var language = hb_language_from_string(nameLanguage, -1);
        if (language == 0)
            throw new ArgumentException("Invalid font name language.", nameof(nameLanguage));
        var normalized = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(hb_language_to_string(language))
            ?? throw new InvalidOperationException("HarfBuzz did not publish the requested name language.");
        ArgumentNullException.ThrowIfNull(request.Text);
        _ = StrictUtf16.GetByteCount(request.Text);
        lock (_sync)
        {
            var primary = Font(request.FontPath, request.FontSize);
            var secondary = request.SecondaryFontPath is { } second ? Font(second, request.FontSize, primary) : null;
            List<PlatformTextFontRun> runs = [];
            var offset = 0;
            LinuxFontResource? selected = null;
            foreach (var rune in request.Text.EnumerateRunes())
            {
                var end = offset + rune.Utf16SequenceLength;
                if (rune.Value is not ('\r' or '\n'))
                {
                    // The renderer expands a source tab to four spaces without changing its face.
                    var font = Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format ? selected ?? primary
                        : SelectFont(primary, secondary, rune.Value == '\t' ? new Rune(' ') : rune, selected);
                    selected = font;
                    var metadata = font.GetMetadata(normalized, language);
                    if (runs.Count > 0 && runs[^1].EndUtf16 == offset && ReferenceEquals(runs[^1].Font, metadata))
                        runs[^1] = runs[^1] with
                        {
                            EndUtf16 = end
                        };
                    else
                        runs.Add(new(offset, end, metadata));
                }
                else
                    selected = null;
                offset = end;
            }
            return runs.AsReadOnly();
        }
    }
}
