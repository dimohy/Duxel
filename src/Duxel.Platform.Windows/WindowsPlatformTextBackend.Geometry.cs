using System.Text;
using Duxel.Core;

namespace Duxel.Platform.Windows;

public sealed partial class WindowsPlatformTextBackend
{
    public PlatformTextLineGeometry CreateLineGeometry(in PlatformTextMeasureRequest request, bool vectorMetrics)
    {
        ArgumentNullException.ThrowIfNull(request.Text);
        if (!float.IsFinite(request.FontSize) || request.FontSize <= 0 || request.Text.Contains('\n') || request.Text.Contains('\r'))
            throw new ArgumentException("Line geometry requires a finite positive font size and one complete line.");
        var normalized = new StringBuilder();
        var offsets = new int[request.Text.Length + 1];
        for (var index = 0; index < request.Text.Length; index++)
        {
            offsets[index] = normalized.Length;
            if (request.Text[index] == '\t')
                normalized.Append("    ");
            else
                normalized.Append(request.Text[index]);
        }
        offsets[^1] = normalized.Length;
        var text = normalized.ToString();
        var carets = new float[text.Length + 1];
        var characters = new List<PlatformTextCharacterGeometry>();
        var advance = 0f;
        var size = vectorMetrics ? request.FontSize : Math.Clamp(MathF.Round(request.FontSize), 1, 512);
        var geometry = WindowsDirectWriteGlyphRasterizer.CreateContextualLineGeometry(request.FontPath, request.SecondaryFontPath, text, size);
        carets = geometry.Carets;
        foreach (var character in geometry.Characters)
            characters.Add(new(character.Start, character.Length, character.Left, character.Width, character.Level));
        advance = geometry.Advance;
        var originalCarets = offsets.Select(index => carets[index]).ToArray();
        // Expanded tabs are one source grapheme, even though native layout sees four spaces.
        var originalCharacters = new List<PlatformTextCharacterGeometry>();
        var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(request.Text);
        var nativeIndex = 0;
        for (var index = 0; index < boundaries.Length; index++)
        {
            var from = boundaries[index];
            var to = index + 1 < boundaries.Length ? boundaries[index + 1] : request.Text.Length;
            while (nativeIndex < characters.Count && characters[nativeIndex].Start < offsets[to])
            {
                var character = characters[nativeIndex++] with
                {
                    Start = from,
                    Length = to - from
                };
                if (originalCharacters.Count > 0 && originalCharacters[^1].Start == from
                    && originalCharacters[^1].BidiLevel == character.BidiLevel)
                {
                    var previous = originalCharacters[^1];
                    var left = MathF.Min(previous.Left, character.Left);
                    originalCharacters[^1] = previous with
                    {
                        Left = left,
                        Width = MathF.Max(previous.Left + previous.Width, character.Left + character.Width) - left
                    };
                }
                else
                    originalCharacters.Add(character);
            }
        }
        return new(advance, originalCarets, originalCharacters.ToArray());
    }
}
