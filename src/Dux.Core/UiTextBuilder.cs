using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dux.Core;

public readonly record struct UiTextSettings(
    float Scale,
    float LineHeightScale,
    bool PixelSnap,
    bool UseBaseline
)
{
    public static UiTextSettings Default => new(1f, 1f, true, true);
}

public static class UiTextBuilder
{
    private const int MaxMeasureCacheEntries = 512;
    private const int MaxMeasureCacheTextLength = 256;
    private static readonly Dictionary<TextMeasureKey, UiVector2> MeasureCache = new();
    private static readonly Queue<TextMeasureKey> MeasureCacheOrder = new();

    public static UiDrawList BuildText(
        UiFontAtlas font,
        string text,
        UiVector2 position,
        UiColor color,
        UiTextureId textureId,
        UiRect clipRect,
        float scale = 1f,
        float? lineHeight = null
    )
    {
        return BuildText(
            font,
            text,
            position,
            color,
            textureId,
            clipRect,
            new UiTextSettings(
                scale,
                lineHeight is null ? 1f : lineHeight.Value / font.LineHeight,
                true,
                true
            ),
            lineHeight
        );
    }

    public static UiDrawList BuildText(
        UiFontAtlas font,
        string text,
        UiVector2 position,
        UiColor color,
        UiTextureId textureId,
        UiRect clipRect,
        UiTextSettings settings,
        float? lineHeightOverride = null
    )
    {
        if (string.IsNullOrEmpty(text))
        {
            return new UiDrawList(
                UiPooledList<UiDrawVertex>.FromArray(Array.Empty<UiDrawVertex>()),
                UiPooledList<uint>.FromArray(Array.Empty<uint>()),
                UiPooledList<UiDrawCommand>.FromArray(Array.Empty<UiDrawCommand>())
            );
        }

        var maxVertices = text.Length * 4;
        var maxIndices = text.Length * 6;
        var vertexBuffer = ArrayPool<UiDrawVertex>.Shared.Rent(maxVertices);
        var indexBuffer = ArrayPool<uint>.Shared.Rent(maxIndices);
        var vertexCount = 0;
        var indexCount = 0;

        var scale = settings.Scale;
        var pixelSnap = settings.PixelSnap;
        var x = Snap(position.X, pixelSnap);
        var y = Snap(position.Y, pixelSnap);
        var hasPrev = false;
        var prevChar = 0;
        var lineHeight = lineHeightOverride ?? (font.LineHeight * settings.LineHeightScale);
        var effectiveLineHeight = lineHeight * scale;
        var baselineOffset = settings.UseBaseline ? font.Ascent * scale : 0f;
        var hasKerning = font.Kerning.Count > 0;

        var span = text.AsSpan();
        var hasSurrogate = false;
        for (var i = 0; i < span.Length; i++)
        {
            if (char.IsSurrogate(span[i]))
            {
                hasSurrogate = true;
                break;
            }
        }

        if (!hasSurrogate)
        {
            for (var i = 0; i < span.Length; i++)
            {
                var code = span[i];
                if (code == '\n')
                {
                    x = Snap(position.X, pixelSnap);
                    y = Snap(y + effectiveLineHeight, pixelSnap);
                    hasPrev = false;
                    continue;
                }

                if (hasPrev && hasKerning)
                {
                    x += font.GetKerning(prevChar, code) * scale;
                }

                if (!font.GetGlyphOrFallback(code, out var glyph))
                {
                    x += effectiveLineHeight * 0.5f;
                    continue;
                }

                if (glyph.Width <= 0 || glyph.Height <= 0)
                {
                    x += glyph.AdvanceX * scale;
                    continue;
                }

                var x0 = Snap(x + (glyph.OffsetX * scale), pixelSnap);
                var y0 = Snap(y + baselineOffset + (glyph.OffsetY * scale), pixelSnap);
                var x1 = Snap(x0 + (glyph.Width * scale), pixelSnap);
                var y1 = Snap(y0 + (glyph.Height * scale), pixelSnap);

                var u0 = glyph.UvRect.X;
                var v0 = glyph.UvRect.Y;
                var u1 = u0 + glyph.UvRect.Width;
                var v1 = v0 + glyph.UvRect.Height;

                var startIndex = (uint)vertexCount;
                vertexBuffer[vertexCount++] = new UiDrawVertex(new UiVector2(x0, y0), new UiVector2(u0, v0), color);
                vertexBuffer[vertexCount++] = new UiDrawVertex(new UiVector2(x1, y0), new UiVector2(u1, v0), color);
                vertexBuffer[vertexCount++] = new UiDrawVertex(new UiVector2(x1, y1), new UiVector2(u1, v1), color);
                vertexBuffer[vertexCount++] = new UiDrawVertex(new UiVector2(x0, y1), new UiVector2(u0, v1), color);

                indexBuffer[indexCount++] = startIndex;
                indexBuffer[indexCount++] = startIndex + 1;
                indexBuffer[indexCount++] = startIndex + 2;
                indexBuffer[indexCount++] = startIndex;
                indexBuffer[indexCount++] = startIndex + 2;
                indexBuffer[indexCount++] = startIndex + 3;

                x = Snap(x + (glyph.AdvanceX * scale), pixelSnap);
                hasPrev = true;
                prevChar = code;
            }
        }
        else
        {
            foreach (var rune in text.EnumerateRunes())
            {
                if (rune.Value == '\n')
                {
                    x = Snap(position.X, pixelSnap);
                    y = Snap(y + effectiveLineHeight, pixelSnap);
                    hasPrev = false;
                    continue;
                }

                if (hasPrev && hasKerning)
                {
                    x += font.GetKerning(prevChar, rune.Value) * scale;
                }

                if (!font.GetGlyphOrFallback(rune.Value, out var glyph))
                {
                    x += effectiveLineHeight * 0.5f;
                    continue;
                }

                if (glyph.Width <= 0 || glyph.Height <= 0)
                {
                    x += glyph.AdvanceX * scale;
                    continue;
                }

                var x0 = Snap(x + (glyph.OffsetX * scale), pixelSnap);
                var y0 = Snap(y + baselineOffset + (glyph.OffsetY * scale), pixelSnap);
                var x1 = Snap(x0 + (glyph.Width * scale), pixelSnap);
                var y1 = Snap(y0 + (glyph.Height * scale), pixelSnap);

                var u0 = glyph.UvRect.X;
                var v0 = glyph.UvRect.Y;
                var u1 = u0 + glyph.UvRect.Width;
                var v1 = v0 + glyph.UvRect.Height;

                var startIndex = (uint)vertexCount;
                vertexBuffer[vertexCount++] = new UiDrawVertex(new UiVector2(x0, y0), new UiVector2(u0, v0), color);
                vertexBuffer[vertexCount++] = new UiDrawVertex(new UiVector2(x1, y0), new UiVector2(u1, v0), color);
                vertexBuffer[vertexCount++] = new UiDrawVertex(new UiVector2(x1, y1), new UiVector2(u1, v1), color);
                vertexBuffer[vertexCount++] = new UiDrawVertex(new UiVector2(x0, y1), new UiVector2(u0, v1), color);

                indexBuffer[indexCount++] = startIndex;
                indexBuffer[indexCount++] = startIndex + 1;
                indexBuffer[indexCount++] = startIndex + 2;
                indexBuffer[indexCount++] = startIndex;
                indexBuffer[indexCount++] = startIndex + 2;
                indexBuffer[indexCount++] = startIndex + 3;

                x = Snap(x + (glyph.AdvanceX * scale), pixelSnap);
                hasPrev = true;
                prevChar = rune.Value;
            }
        }

        if (indexCount == 0)
        {
            ArrayPool<UiDrawVertex>.Shared.Return(vertexBuffer, clearArray: false);
            ArrayPool<uint>.Shared.Return(indexBuffer, clearArray: false);
            return new UiDrawList(
                UiPooledList<UiDrawVertex>.FromArray(Array.Empty<UiDrawVertex>()),
                UiPooledList<uint>.FromArray(Array.Empty<uint>()),
                UiPooledList<UiDrawCommand>.FromArray(Array.Empty<UiDrawCommand>())
            );
        }

        var command = new UiDrawCommand(clipRect, textureId, 0, (uint)indexCount, 0);
        var commandBuffer = ArrayPool<UiDrawCommand>.Shared.Rent(1);
        commandBuffer[0] = command;
        return new UiDrawList(
            new UiPooledList<UiDrawVertex>(vertexBuffer, vertexCount, pooled: true),
            new UiPooledList<uint>(indexBuffer, indexCount, pooled: true),
            new UiPooledList<UiDrawCommand>(commandBuffer, 1, pooled: true)
        );
    }

    public static UiVector2 MeasureText(
        UiFontAtlas font,
        string text,
        UiTextSettings settings,
        float? lineHeightOverride = null
    )
    {
        if (string.IsNullOrEmpty(text))
        {
            return new UiVector2(0, 0);
        }

        if (text.Length <= MaxMeasureCacheTextLength)
        {
            var key = new TextMeasureKey(
                RuntimeHelpers.GetHashCode(font),
                settings,
                lineHeightOverride ?? -1f,
                text
            );

            if (MeasureCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var measured = MeasureTextCore(font, text, settings, lineHeightOverride);
            StoreMeasureCache(key, measured);
            return measured;
        }

        return MeasureTextCore(font, text, settings, lineHeightOverride);
    }

    private static UiVector2 MeasureTextCore(
        UiFontAtlas font,
        string text,
        UiTextSettings settings,
        float? lineHeightOverride
    )
    {
        var scale = settings.Scale;
        var lineHeight = (lineHeightOverride ?? (font.LineHeight * settings.LineHeightScale)) * scale;
        var maxWidth = 0f;
        var lineWidth = 0f;
        var height = lineHeight;
        var hasPrev = false;
        var prevChar = 0;
        var hasKerning = font.Kerning.Count > 0;

        var span = text.AsSpan();
        var hasSurrogate = false;
        for (var i = 0; i < span.Length; i++)
        {
            if (char.IsSurrogate(span[i]))
            {
                hasSurrogate = true;
                break;
            }
        }

        if (!hasSurrogate)
        {
            for (var i = 0; i < span.Length; i++)
            {
                var code = span[i];
                if (code == '\n')
                {
                    maxWidth = MathF.Max(maxWidth, lineWidth);
                    lineWidth = 0f;
                    height += lineHeight;
                    hasPrev = false;
                    continue;
                }

                if (hasPrev && hasKerning)
                {
                    lineWidth += font.GetKerning(prevChar, code) * scale;
                }

                if (!font.GetGlyphOrFallback(code, out var glyph))
                {
                    lineWidth += lineHeight * 0.5f;
                    hasPrev = false;
                    continue;
                }

                lineWidth += glyph.AdvanceX * scale;
                hasPrev = true;
                prevChar = code;
            }
        }
        else
        {
            foreach (var rune in text.EnumerateRunes())
            {
                if (rune.Value == '\n')
                {
                    maxWidth = MathF.Max(maxWidth, lineWidth);
                    lineWidth = 0f;
                    height += lineHeight;
                    hasPrev = false;
                    continue;
                }

                if (hasPrev && hasKerning)
                {
                    lineWidth += font.GetKerning(prevChar, rune.Value) * scale;
                }

                if (!font.GetGlyphOrFallback(rune.Value, out var glyph))
                {
                    lineWidth += lineHeight * 0.5f;
                    hasPrev = false;
                    continue;
                }

                lineWidth += glyph.AdvanceX * scale;
                hasPrev = true;
                prevChar = rune.Value;
            }
        }

        maxWidth = MathF.Max(maxWidth, lineWidth);
        return new UiVector2(maxWidth, height);
    }

    private static void StoreMeasureCache(TextMeasureKey key, UiVector2 value)
    {
        if (MeasureCache.TryGetValue(key, out _))
        {
            MeasureCache[key] = value;
            return;
        }

        if (MeasureCache.Count >= MaxMeasureCacheEntries)
        {
            while (MeasureCacheOrder.Count > 0)
            {
                var oldest = MeasureCacheOrder.Dequeue();
                if (MeasureCache.Remove(oldest))
                {
                    break;
                }
            }
        }

        MeasureCache[key] = value;
        MeasureCacheOrder.Enqueue(key);
    }

    private readonly record struct TextMeasureKey(
        int FontHash,
        UiTextSettings Settings,
        float LineHeightOverride,
        string Text
    );

    private static float Snap(float value, bool enabled) => enabled ? MathF.Round(value) : value;
}
