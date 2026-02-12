using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Duxel.Core;

public sealed class UiFontAtlasState
{
    private readonly List<UiFontSource> _sources = [];
    private readonly List<UiCustomRect> _customRects = [];
    private Func<IReadOnlyList<UiFontSource>, int, int, int, int, int, UiFontAtlas>? _fontLoader;
    private UiFontAtlas? _atlas;

    public UiFontAtlas? Atlas => _atlas;

    public UiFontAtlas Create(int fontSize = 18, int atlasWidth = 512, int atlasHeight = 512, int padding = 1, int oversample = 2)
    {
        if (_sources.Count == 0)
        {
            var defaultFont = GetDefaultFontPath();
            if (defaultFont is null)
            {
                throw new InvalidOperationException("No font sources and no default font found.");
            }

            var defaultRanges = GetGlyphRangesDefault();
            var codepoints = BuildCodepointsFromRanges(defaultRanges);
            _sources.Add(new UiFontSource(defaultFont, codepoints));
        }

        _atlas = _fontLoader is null
            ? UiFontAtlasBuilder.CreateFromTtfMerged(_sources, fontSize, atlasWidth, atlasHeight, padding, oversample)
            : _fontLoader(_sources, fontSize, atlasWidth, atlasHeight, padding, oversample);

        return _atlas;
    }

    public void DestroyPixels()
    {
        if (_atlas is null)
        {
            return;
        }

        Array.Clear(_atlas.Pixels, 0, _atlas.Pixels.Length);
    }

    public void AddRanges(IEnumerable<UiFontRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        foreach (var range in ranges)
        {
            _sources.Add(new UiFontSource(GetDefaultFontPath() ?? string.Empty, BuildCodepointsFromRanges([range])));
        }
    }

    public UiFontAtlas AddFont(string fontPath, IReadOnlyList<int> codepoints, int fontSize = 18, int atlasWidth = 512, int atlasHeight = 512, int padding = 1, int oversample = 2, float scale = 1f, int priority = 0)
    {
        _sources.Add(new UiFontSource(fontPath, codepoints, priority, scale));
        return Create(fontSize, atlasWidth, atlasHeight, padding, oversample);
    }

    public UiFontAtlas AddFontDefault(int fontSize = 18, int atlasWidth = 512, int atlasHeight = 512, int padding = 1, int oversample = 2)
    {
        var fontPath = GetDefaultFontPath() ?? throw new InvalidOperationException("Default font not found.");
        var ranges = GetGlyphRangesDefault();
        var codepoints = BuildCodepointsFromRanges(ranges);
        return AddFont(fontPath, codepoints, fontSize, atlasWidth, atlasHeight, padding, oversample);
    }

    public UiFontAtlas AddFontDefaultVector(int fontSize = 18, int atlasWidth = 512, int atlasHeight = 512, int padding = 1, int oversample = 2)
    {
        return AddFontDefault(fontSize, atlasWidth, atlasHeight, padding, oversample);
    }

    public UiFontAtlas AddFontDefaultBitmap(int fontSize = 18, int atlasWidth = 512, int atlasHeight = 512, int padding = 1, int oversample = 2)
    {
        return AddFontDefault(fontSize, atlasWidth, atlasHeight, padding, oversample);
    }

    public UiFontAtlas AddFontFromFileTTF(string filename, IReadOnlyList<UiFontRange> ranges, int fontSize = 18, int atlasWidth = 512, int atlasHeight = 512, int padding = 1, int oversample = 2, float scale = 1f, int priority = 0)
    {
        var codepoints = BuildCodepointsFromRanges(ranges);
        return AddFont(filename, codepoints, fontSize, atlasWidth, atlasHeight, padding, oversample, scale, priority);
    }

    public UiFontAtlas AddFontFromMemoryTTF(byte[] ttfData, IReadOnlyList<UiFontRange> ranges, int fontSize = 18, int atlasWidth = 512, int atlasHeight = 512, int padding = 1, int oversample = 2, float scale = 1f, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(ttfData);
        var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ".ttf");
        File.WriteAllBytes(tempPath, ttfData);
        try
        {
            return AddFontFromFileTTF(tempPath, ranges, fontSize, atlasWidth, atlasHeight, padding, oversample, scale, priority);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public UiFontAtlas AddFontFromMemoryCompressedTTF(byte[] compressedData, IReadOnlyList<UiFontRange> ranges, int fontSize = 18, int atlasWidth = 512, int atlasHeight = 512, int padding = 1, int oversample = 2, float scale = 1f, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(compressedData);
        var decompressed = DecompressZlib(compressedData);
        return AddFontFromMemoryTTF(decompressed, ranges, fontSize, atlasWidth, atlasHeight, padding, oversample, scale, priority);
    }

    public UiFontAtlas AddFontFromMemoryCompressedBase85TTF(string compressedBase85, IReadOnlyList<UiFontRange> ranges, int fontSize = 18, int atlasWidth = 512, int atlasHeight = 512, int padding = 1, int oversample = 2, float scale = 1f, int priority = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(compressedBase85);
        var decoded = DecodeBase85(compressedBase85.AsSpan());
        return AddFontFromMemoryCompressedTTF(decoded, ranges, fontSize, atlasWidth, atlasHeight, padding, oversample, scale, priority);
    }

    public void RemoveFont(string fontPath)
    {
        _sources.RemoveAll(source => string.Equals(source.FontPath, fontPath, StringComparison.OrdinalIgnoreCase));
        _atlas = null;
    }

    public void CompactCache()
    {
        UiFontAtlasBuilder.ClearCache();
    }

    public void SetFontLoader(Func<IReadOnlyList<UiFontSource>, int, int, int, int, int, UiFontAtlas> loader)
    {
        _fontLoader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    public void ClearInputData()
    {
        _sources.Clear();
    }

    public void ClearFonts()
    {
        _sources.Clear();
        _atlas = null;
    }

    public void ClearTexData()
    {
        if (_atlas is null)
        {
            return;
        }

        Array.Clear(_atlas.Pixels, 0, _atlas.Pixels.Length);
    }

    public void ClearOutputData()
    {
        _atlas = null;
    }

    public byte[] GetTexDataAsAlpha8()
    {
        if (_atlas is null || _atlas.Pixels.Length == 0)
        {
            return Array.Empty<byte>();
        }

        var rgba = _atlas.Pixels;
        var alpha = new byte[rgba.Length / 4];
        for (var i = 0; i < alpha.Length; i++)
        {
            alpha[i] = rgba[(i * 4) + 3];
        }

        return alpha;
    }

    public byte[] GetTexDataAsRGBA32()
    {
        return _atlas?.Pixels ?? Array.Empty<byte>();
    }

    public IReadOnlyList<UiFontRange> GetGlyphRangesDefault() =>
        new[] { new UiFontRange(0x0020, 0x00FF) };

    public IReadOnlyList<UiFontRange> GetGlyphRangesGreek() =>
        new[] { new UiFontRange(0x0020, 0x00FF), new UiFontRange(0x0370, 0x03FF) };

    public IReadOnlyList<UiFontRange> GetGlyphRangesKorean() =>
        new[] { new UiFontRange(0x0020, 0x00FF), new UiFontRange(0x3131, 0x3163), new UiFontRange(0xAC00, 0xD7A3) };

    public IReadOnlyList<UiFontRange> GetGlyphRangesJapanese() =>
        new[] { new UiFontRange(0x0020, 0x00FF), new UiFontRange(0x3000, 0x30FF), new UiFontRange(0x4E00, 0x9FFF) };

    public IReadOnlyList<UiFontRange> GetGlyphRangesChineseFull() =>
        new[] { new UiFontRange(0x0020, 0x00FF), new UiFontRange(0x4E00, 0x9FFF) };

    public IReadOnlyList<UiFontRange> GetGlyphRangesChineseSimplifiedCommon() =>
        new[] { new UiFontRange(0x0020, 0x00FF), new UiFontRange(0x4E00, 0x9FA5) };

    public IReadOnlyList<UiFontRange> GetGlyphRangesCyrillic() =>
        new[] { new UiFontRange(0x0020, 0x00FF), new UiFontRange(0x0400, 0x052F) };

    public IReadOnlyList<UiFontRange> GetGlyphRangesThai() =>
        new[] { new UiFontRange(0x0020, 0x00FF), new UiFontRange(0x0E00, 0x0E7F) };

    public IReadOnlyList<UiFontRange> GetGlyphRangesVietnamese() =>
        new[] { new UiFontRange(0x0020, 0x00FF), new UiFontRange(0x0102, 0x01B0), new UiFontRange(0x1EA0, 0x1EF9) };

    public int AddCustomRect(int width, int height)
    {
        var id = _customRects.Count == 0 ? 1 : _customRects[^1].Id + 1;
        _customRects.Add(new UiCustomRect(id, 0, 0, width, height));
        return id;
    }

    public void RemoveCustomRect(int id)
    {
        _customRects.RemoveAll(rect => rect.Id == id);
    }

    public UiCustomRect? GetCustomRect(int id)
    {
        foreach (var rect in _customRects)
        {
            if (rect.Id == id)
            {
                return rect;
            }
        }

        return null;
    }

    public int AddCustomRectFontGlyph(int width, int height, int codepoint)
    {
        _ = codepoint;
        return AddCustomRect(width, height);
    }

    public int AddCustomRectFontGlyphForSize(int width, int height, int codepoint, int fontSize)
    {
        _ = codepoint;
        _ = fontSize;
        return AddCustomRect(width, height);
    }

    public bool FindGlyph(int codepoint, out UiGlyphInfo glyph)
    {
        if (_atlas is null)
        {
            glyph = default;
            return false;
        }

        return IsHangulCodepoint(codepoint)
            ? _atlas.TryGetGlyph(codepoint, out glyph)
            : _atlas.GetGlyphOrFallback(codepoint, out glyph);
    }

    public bool FindGlyphNoFallback(int codepoint, out UiGlyphInfo glyph)
    {
        if (_atlas is null)
        {
            glyph = default;
            return false;
        }

        return _atlas.TryGetGlyph(codepoint, out glyph);
    }

    public float GetCharAdvance(int codepoint)
    {
        return FindGlyph(codepoint, out var glyph) ? glyph.AdvanceX : 0f;
    }

    public bool IsGlyphLoaded(int codepoint) => _atlas is not null && _atlas.Glyphs.ContainsKey(codepoint);

    public bool IsGlyphInFont(int codepoint) => IsGlyphLoaded(codepoint);

    public UiVector2 CalcTextSizeA(float size, float maxWidth, float wrapWidth, string text)
    {
        if (_atlas is null)
        {
            return default;
        }

        var scale = size <= 0f ? 1f : size / _atlas.LineHeight;
        var settings = new UiTextSettings(scale, 1f, true, true);
        var measured = UiTextBuilder.MeasureText(_atlas, text, settings);
        if (maxWidth > 0f)
        {
            measured = new UiVector2(MathF.Min(measured.X, maxWidth), measured.Y);
        }

        if (wrapWidth > 0f && measured.X > wrapWidth)
        {
            measured = new UiVector2(wrapWidth, measured.Y);
        }

        return measured;
    }

    public int CalcWordWrapPosition(float scale, string text, float wrapWidth)
    {
        if (_atlas is null || string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var width = 0f;
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var hasGlyph = IsHangulCodepoint(rune.Value)
                ? _atlas.TryGetGlyph(rune.Value, out var glyph)
                : _atlas.GetGlyphOrFallback(rune.Value, out glyph);
            if (!hasGlyph)
            {
                break;
            }

            width += glyph.AdvanceX * scale;
            if (width > wrapWidth)
            {
                return index;
            }

            index += rune.Utf16SequenceLength;
        }

        return text.Length;
    }

    private static bool IsHangulCodepoint(int codepoint)
    {
        return (codepoint >= 0xAC00 && codepoint <= 0xD7A3)
            || (codepoint >= 0x1100 && codepoint <= 0x11FF)
            || (codepoint >= 0x3130 && codepoint <= 0x318F);
    }

    public UiDrawList RenderChar(float size, UiVector2 pos, UiColor color, int codepoint)
    {
        if (_atlas is null)
        {
            return new UiDrawList(
                UiPooledList<UiDrawVertex>.FromArray(Array.Empty<UiDrawVertex>()),
                UiPooledList<uint>.FromArray(Array.Empty<uint>()),
                UiPooledList<UiDrawCommand>.FromArray(Array.Empty<UiDrawCommand>())
            );
        }

        var scale = size <= 0f ? 1f : size / _atlas.LineHeight;
        var settings = new UiTextSettings(scale, 1f, true, true);
        var text = char.ConvertFromUtf32(codepoint);
        return UiTextBuilder.BuildText(_atlas, text, pos, color, default, new UiRect(float.MinValue, float.MinValue, float.MaxValue, float.MaxValue), settings.Scale);
    }

    public UiDrawList RenderText(float size, UiVector2 pos, UiColor color, string text)
    {
        if (_atlas is null)
        {
            return new UiDrawList(
                UiPooledList<UiDrawVertex>.FromArray(Array.Empty<UiDrawVertex>()),
                UiPooledList<uint>.FromArray(Array.Empty<uint>()),
                UiPooledList<UiDrawCommand>.FromArray(Array.Empty<UiDrawCommand>())
            );
        }

        var scale = size <= 0f ? 1f : size / _atlas.LineHeight;
        var settings = new UiTextSettings(scale, 1f, true, true);
        return UiTextBuilder.BuildText(_atlas, text, pos, color, default, new UiRect(float.MinValue, float.MinValue, float.MaxValue, float.MaxValue), settings.Scale);
    }

    public void AddRemapChar(int dst, int src, bool overwriteDst = true)
    {
        if (_atlas is null || _atlas.Glyphs is not Dictionary<int, UiGlyphInfo> dict)
        {
            return;
        }

        if (!dict.TryGetValue(src, out var glyph))
        {
            return;
        }

        if (!overwriteDst && dict.ContainsKey(dst))
        {
            return;
        }

        dict[dst] = glyph;
    }

    public bool IsGlyphRangeUnused(int start, int end)
    {
        if (_atlas is null)
        {
            return true;
        }

        for (var i = start; i <= end; i++)
        {
            if (_atlas.Glyphs.ContainsKey(i))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<int> BuildCodepointsFromRanges(IReadOnlyList<UiFontRange> ranges)
    {
        var codepoints = new List<int>();
        foreach (var range in ranges)
        {
            var start = Math.Max(0, range.Start);
            var end = Math.Max(start, range.End);
            for (var c = start; c <= end; c++)
            {
                codepoints.Add(c);
            }
        }

        return codepoints;
    }

    private static string? GetDefaultFontPath()
    {
        var fontsPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var candidates = new[] { "segoeui.ttf", "arial.ttf" };
        foreach (var name in candidates)
        {
            var path = Path.Combine(fontsPath, name);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static byte[] DecompressZlib(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] DecodeBase85(ReadOnlySpan<char> input)
    {
        var table = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!#$%&()*+-./:;=?@[]^_`{|}~";
        Span<int> decode = stackalloc int[256];
        decode.Fill(-1);
        for (var i = 0; i < table.Length; i++)
        {
            decode[table[i]] = i;
        }

        var output = new List<byte>(input.Length);
        var chunk = 0u;
        var count = 0;
        foreach (var ch in input)
        {
            var value = ch < 256 ? decode[ch] : -1;
            if (value < 0)
            {
                continue;
            }

            chunk = chunk * 85u + (uint)value;
            count++;
            if (count == 5)
            {
                output.Add((byte)(chunk >> 24));
                output.Add((byte)(chunk >> 16));
                output.Add((byte)(chunk >> 8));
                output.Add((byte)chunk);
                chunk = 0;
                count = 0;
            }
        }

        if (count > 0)
        {
            for (var i = count; i < 5; i++)
            {
                chunk = chunk * 85u + 84u;
            }

            for (var i = 0; i < count - 1; i++)
            {
                output.Add((byte)(chunk >> (24 - (8 * i))));
            }
        }

        return output.ToArray();
    }

    public readonly record struct UiCustomRect(int Id, int X, int Y, int Width, int Height);
}

