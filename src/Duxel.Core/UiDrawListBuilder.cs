using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Duxel.Core;

public sealed class UiDrawListBuilder
{
    private const int MaxVerticesPerList = 60_000;
    private const int MaxIndicesPerList = 120_000;

    private PooledBuffer<UiDrawVertex> _vertices = new();
    private PooledBuffer<uint> _indices = new();
    private PooledBuffer<UiDrawCommand> _commands = new();
    private List<UiDrawList> _drawLists = [];
    private readonly UiRect _clipRect;
    private UiRect _currentClipRect;
    private readonly Stack<UiRect> _clipStack = new();
    private UiTextureId _currentTexture;
    private readonly Stack<UiTextureId> _textureStack = new();
    private readonly List<UiVector2> _path = [];
    private UiDrawListSharedData? _sharedData;
    private List<Channel>? _channels;
    private int _currentChannel;

    private sealed class Channel
    {
        public PooledBuffer<UiDrawVertex> Vertices { get; }
        public PooledBuffer<uint> Indices { get; }
        public PooledBuffer<UiDrawCommand> Commands { get; }
        public List<UiDrawList> DrawLists { get; }

        public Channel(PooledBuffer<UiDrawVertex> vertices, PooledBuffer<uint> indices, PooledBuffer<UiDrawCommand> commands, List<UiDrawList> drawLists)
        {
            Vertices = vertices;
            Indices = indices;
            Commands = commands;
            DrawLists = drawLists;
        }
    }

    public UiDrawListBuilder(UiRect clipRect)
    {
        _clipRect = clipRect;
        _currentClipRect = clipRect;
        _currentTexture = default;
    }

    public void Reserve(int vertexCapacity, int indexCapacity, int commandCapacity)
    {
        _vertices.EnsureCapacity(vertexCapacity);
        _indices.EnsureCapacity(indexCapacity);
        _commands.EnsureCapacity(commandCapacity);
    }

    public void AddRectFilled(UiRect rect, UiColor color, UiTextureId textureId)
    {
        EnsureCapacityFor(4, 6);
        var startVertex = (uint)_vertices.Count;
        var startIndex = (uint)_indices.Count;

        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X, rect.Y), new UiVector2(0, 0), color));
        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X + rect.Width, rect.Y), new UiVector2(1, 0), color));
        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X + rect.Width, rect.Y + rect.Height), new UiVector2(1, 1), color));
        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X, rect.Y + rect.Height), new UiVector2(0, 1), color));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 3);

        AddCommand(new UiDrawCommand(_currentClipRect, textureId, startIndex, 6, 0));
    }

    public void AddRectFilled(UiRect rect, UiColor color, UiTextureId textureId, UiRect clipRect)
    {
        clipRect = Intersect(clipRect, _currentClipRect);
        EnsureCapacityFor(4, 6);
        var startVertex = (uint)_vertices.Count;
        var startIndex = (uint)_indices.Count;

        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X, rect.Y), new UiVector2(0, 0), color));
        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X + rect.Width, rect.Y), new UiVector2(1, 0), color));
        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X + rect.Width, rect.Y + rect.Height), new UiVector2(1, 1), color));
        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X, rect.Y + rect.Height), new UiVector2(0, 1), color));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 3);

        AddCommand(new UiDrawCommand(clipRect, textureId, startIndex, 6, 0));
    }

    public void AddText(
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
        if (string.IsNullOrEmpty(text) || clipRect.Width <= 0f || clipRect.Height <= 0f)
        {
            return;
        }

        var scale = settings.Scale;
        var pixelSnap = settings.PixelSnap;
        var x = Snap(position.X, pixelSnap);
        var y = Snap(position.Y, pixelSnap);
        var hasPrev = false;
        var prevChar = 0;
        var lineHeight = lineHeightOverride ?? (font.LineHeight * settings.LineHeightScale);
        var effectiveLineHeight = lineHeight * scale;
        var baselineOffset = settings.UseBaseline ? font.Ascent * scale : 0f;
        var baselineY = Snap(y + baselineOffset, pixelSnap);
        var hasKerning = font.Kerning.Count > 0;
        var useFallbackGlyph = settings.UseFallbackGlyph;
        var missingGlyphObserver = settings.MissingGlyphObserver;
        var span = text.AsSpan();
        var index = 0;
        var checkClipY = effectiveLineHeight > 0f && clipRect.Height > 0f;
        if (checkClipY && y + effectiveLineHeight < clipRect.Y)
        {
            var targetLines = (int)((clipRect.Y - y) / effectiveLineHeight);
            if (targetLines > 0)
            {
                var linesSkipped = 0;
                var searchIndex = 0;
                while (linesSkipped < targetLines && searchIndex < span.Length)
                {
                    var relative = span.Slice(searchIndex).IndexOf('\n');
                    if (relative < 0)
                    {
                        searchIndex = span.Length;
                        break;
                    }

                    searchIndex += relative + 1;
                    linesSkipped++;
                }

                if (searchIndex > 0)
                {
                    index = searchIndex;
                    y = Snap(position.Y + (effectiveLineHeight * linesSkipped), pixelSnap);
                    baselineY = Snap(y + baselineOffset, pixelSnap);
                    x = Snap(position.X, pixelSnap);
                    hasPrev = false;
                }
            }
        }

        var clipMaxY = clipRect.Y + clipRect.Height;
        var lineStart = true;

        var estimatedVertices = text.Length * 4;
        var estimatedIndices = text.Length * 6;
        var canBatch = estimatedVertices <= MaxVerticesPerList && estimatedIndices <= MaxIndicesPerList;

        if (canBatch)
        {
            if (_vertices.Count + estimatedVertices > MaxVerticesPerList || _indices.Count + estimatedIndices > MaxIndicesPerList)
            {
                Flush();
            }

            EnsureCapacityFor(estimatedVertices, estimatedIndices);
        }

        var segmentStartIndex = (uint)_indices.Count;
        var segmentElementCount = 0;

        // Pre-grow lists once for the batch path, then write via spans
        int vtxWritePos, idxWritePos;
        if (canBatch)
        {
            vtxWritePos = _vertices.Count;
            idxWritePos = _indices.Count;
            _vertices.SetCount(vtxWritePos + estimatedVertices);
            _indices.SetCount(idxWritePos + estimatedIndices);
        }
        else
        {
            vtxWritePos = -1;
            idxWritePos = -1;
        }

        while (index < span.Length)
        {
            if (checkClipY && lineStart && y > clipMaxY)
            {
                break;
            }

            // Fast BMP path: direct char iteration (avoids Rune.DecodeFromUtf16)
            int codepoint;
            var c = span[index];
            if (char.IsHighSurrogate(c) && index + 1 < span.Length && char.IsLowSurrogate(span[index + 1]))
            {
                codepoint = char.ConvertToUtf32(c, span[index + 1]);
                index += 2;
            }
            else
            {
                codepoint = c;
                index++;
            }

            if (codepoint == '\n')
            {
                x = Snap(position.X, pixelSnap);
                y = Snap(y + effectiveLineHeight, pixelSnap);
                baselineY = Snap(y + baselineOffset, pixelSnap);
                hasPrev = false;
                lineStart = true;
                continue;
            }

            lineStart = false;

            if (hasPrev && hasKerning)
            {
                x += font.GetKerning(prevChar, codepoint) * scale;
            }

                var allowFallbackForCodepoint = useFallbackGlyph && !IsHangulCodepoint(codepoint);
                var hasGlyph = allowFallbackForCodepoint
                    ? font.GetGlyphOrFallback(codepoint, out var glyph)
                    : font.TryGetGlyph(codepoint, out glyph);
            if (!hasGlyph)
            {
                missingGlyphObserver?.Invoke(codepoint);
                x += effectiveLineHeight * 0.5f;
                continue;
            }

            if (glyph.Width <= 0 || glyph.Height <= 0)
            {
                x += glyph.AdvanceX * scale;
                continue;
            }

            if (!canBatch && (_vertices.Count + 4 > MaxVerticesPerList || _indices.Count + 6 > MaxIndicesPerList))
            {
                if (segmentElementCount > 0)
                {
                    AddCommand(new UiDrawCommand(clipRect, textureId, segmentStartIndex, (uint)segmentElementCount, 0));
                }

                Flush();
                segmentStartIndex = (uint)_indices.Count;
                segmentElementCount = 0;
            }

            var x0 = Snap(x + (glyph.OffsetX * scale), pixelSnap);
            var y0 = pixelSnap
                ? baselineY + MathF.Round(glyph.OffsetY * scale)
                : y + baselineOffset + (glyph.OffsetY * scale);
            var x1 = Snap(x0 + (glyph.Width * scale), pixelSnap);
            var y1 = Snap(y0 + (glyph.Height * scale), pixelSnap);

            var u0 = glyph.UvRect.X;
            var v0 = glyph.UvRect.Y;
            var u1 = u0 + glyph.UvRect.Width;
            var v1 = v0 + glyph.UvRect.Height;

            if (canBatch)
            {
                // Direct span write — no bounds check per Add, no list growth
                var vtxSpan = _vertices.AsSpan();
                var startVertex = (uint)vtxWritePos;
                vtxSpan[vtxWritePos] = new UiDrawVertex(new UiVector2(x0, y0), new UiVector2(u0, v0), color);
                vtxSpan[vtxWritePos + 1] = new UiDrawVertex(new UiVector2(x1, y0), new UiVector2(u1, v0), color);
                vtxSpan[vtxWritePos + 2] = new UiDrawVertex(new UiVector2(x1, y1), new UiVector2(u1, v1), color);
                vtxSpan[vtxWritePos + 3] = new UiDrawVertex(new UiVector2(x0, y1), new UiVector2(u0, v1), color);
                vtxWritePos += 4;

                var idxSpan = _indices.AsSpan();
                idxSpan[idxWritePos] = startVertex;
                idxSpan[idxWritePos + 1] = startVertex + 1;
                idxSpan[idxWritePos + 2] = startVertex + 2;
                idxSpan[idxWritePos + 3] = startVertex;
                idxSpan[idxWritePos + 4] = startVertex + 2;
                idxSpan[idxWritePos + 5] = startVertex + 3;
                idxWritePos += 6;
            }
            else
            {
                var startVertex = (uint)_vertices.Count;
                _vertices.Add(new UiDrawVertex(new UiVector2(x0, y0), new UiVector2(u0, v0), color));
                _vertices.Add(new UiDrawVertex(new UiVector2(x1, y0), new UiVector2(u1, v0), color));
                _vertices.Add(new UiDrawVertex(new UiVector2(x1, y1), new UiVector2(u1, v1), color));
                _vertices.Add(new UiDrawVertex(new UiVector2(x0, y1), new UiVector2(u0, v1), color));

                _indices.Add(startVertex);
                _indices.Add(startVertex + 1);
                _indices.Add(startVertex + 2);
                _indices.Add(startVertex);
                _indices.Add(startVertex + 2);
                _indices.Add(startVertex + 3);
            }

            segmentElementCount += 6;
            x = Snap(x + (glyph.AdvanceX * scale), pixelSnap);
            hasPrev = true;
            prevChar = codepoint;
        }

        // Trim over-allocated space in batch mode (newlines/invisible glyphs used fewer slots)
        if (canBatch)
        {
            _vertices.SetCount(vtxWritePos);
            _indices.SetCount(idxWritePos);
        }

        if (segmentElementCount > 0)
        {
            AddCommand(new UiDrawCommand(clipRect, textureId, segmentStartIndex, (uint)segmentElementCount, 0));
        }
    }

    public void AddCircleFilled(UiVector2 center, float radius, UiColor color, UiTextureId textureId, int segments = 12)
    {
        AddCircleFilled(center, radius, color, textureId, _currentClipRect, segments);
    }

    public void AddCircleFilled(UiVector2 center, float radius, UiColor color, UiTextureId textureId, UiRect clipRect, int segments = 12)
    {
        clipRect = Intersect(clipRect, _currentClipRect);
        segments = Math.Max(3, segments);
        EnsureCapacityFor(segments + 1, segments * 3);

        var startVertex = (uint)_vertices.Count;
        var startIndex = (uint)_indices.Count;

        _vertices.Add(new UiDrawVertex(center, new UiVector2(0.5f, 0.5f), color));
        var step = (MathF.PI * 2f) / segments;
        for (var i = 0; i < segments; i++)
        {
            var angle = step * i;
            var x = center.X + MathF.Cos(angle) * radius;
            var y = center.Y + MathF.Sin(angle) * radius;
            _vertices.Add(new UiDrawVertex(new UiVector2(x, y), new UiVector2(0.5f, 0.5f), color));
        }

        for (var i = 0; i < segments; i++)
        {
            var current = (uint)(i + 1);
            var next = (uint)((i + 1) % segments + 1);
            _indices.Add(startVertex);
            _indices.Add(startVertex + current);
            _indices.Add(startVertex + next);
        }

        AddCommand(new UiDrawCommand(clipRect, textureId, startIndex, (uint)(segments * 3), 0));
    }

    public void PushClipRect(UiRect rect, bool intersectWithCurrentClipRect = true)
    {
        if (intersectWithCurrentClipRect)
        {
            rect = Intersect(rect, _currentClipRect);
        }

        _clipStack.Push(_currentClipRect);
        _currentClipRect = rect;
        _OnChangedClipRect();
    }

    public void PushClipRectFullScreen()
    {
        PushClipRect(_clipRect, false);
    }

    public void PopClipRect()
    {
        if (_clipStack.Count == 0)
        {
            return;
        }

        _currentClipRect = _clipStack.Pop();
        _OnChangedClipRect();
    }

    public void PushTexture(UiTextureId textureId)
    {
        _textureStack.Push(_currentTexture);
        _currentTexture = textureId;
        _OnChangedTexture();
    }

    public void PopTexture()
    {
        if (_textureStack.Count == 0)
        {
            return;
        }

        _currentTexture = _textureStack.Pop();
        _OnChangedTexture();
    }

    public void AddLine(UiVector2 p1, UiVector2 p2, UiColor color, float thickness = 1f)
    {
        AddLine(p1, p2, color, thickness, _currentTexture);
    }

    public void AddLine(UiVector2 p1, UiVector2 p2, UiColor color, float thickness, UiTextureId textureId)
    {
        if (thickness <= 0f)
        {
            return;
        }

        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len <= 0f)
        {
            return;
        }

        var nx = dx / len;
        var ny = dy / len;
        var px = -ny * (thickness * 0.5f);
        var py = nx * (thickness * 0.5f);

        var a = new UiVector2(p1.X + px, p1.Y + py);
        var b = new UiVector2(p2.X + px, p2.Y + py);
        var c = new UiVector2(p2.X - px, p2.Y - py);
        var d = new UiVector2(p1.X - px, p1.Y - py);

        AddQuadFilled(a, b, c, d, color, textureId);
    }

    public void AddRect(UiRect rect, UiColor color, float rounding = 0f, float thickness = 1f)
    {
        _ = rounding;
        if (thickness <= 0f)
        {
            return;
        }

        var x0 = rect.X;
        var y0 = rect.Y;
        var x1 = rect.X + rect.Width;
        var y1 = rect.Y + rect.Height;

        ReadOnlySpan<UiVector2> points =
        [
            new(x0, y0),
            new(x1, y0),
            new(x1, y1),
            new(x0, y1),
        ];
        AddPolyline(points, true, color, thickness);
    }

    public void AddRectFilled(UiRect rect, UiColor color)
    {
        AddRectFilled(rect, color, _currentTexture, _currentClipRect);
    }

    public void AddRectFilledMultiColor(UiRect rect, UiColor colUprLeft, UiColor colUprRight, UiColor colBotRight, UiColor colBotLeft)
    {
        EnsureCapacityFor(4, 6);
        var startVertex = (uint)_vertices.Count;
        var startIndex = (uint)_indices.Count;

        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X, rect.Y), new UiVector2(0, 0), colUprLeft));
        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X + rect.Width, rect.Y), new UiVector2(1, 0), colUprRight));
        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X + rect.Width, rect.Y + rect.Height), new UiVector2(1, 1), colBotRight));
        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X, rect.Y + rect.Height), new UiVector2(0, 1), colBotLeft));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 3);

        AddCommand(new UiDrawCommand(_currentClipRect, _currentTexture, startIndex, 6, 0));
    }

    public void AddQuad(UiVector2 a, UiVector2 b, UiVector2 c, UiVector2 d, UiColor color, float thickness = 1f)
    {
        ReadOnlySpan<UiVector2> points = [a, b, c, d];
        AddPolyline(points, true, color, thickness);
    }

    public void AddQuadFilled(UiVector2 a, UiVector2 b, UiVector2 c, UiVector2 d, UiColor color)
    {
        EnsureCapacityFor(4, 6);
        var startVertex = (uint)_vertices.Count;
        var startIndex = (uint)_indices.Count;

        _vertices.Add(new UiDrawVertex(a, new UiVector2(0, 0), color));
        _vertices.Add(new UiDrawVertex(b, new UiVector2(1, 0), color));
        _vertices.Add(new UiDrawVertex(c, new UiVector2(1, 1), color));
        _vertices.Add(new UiDrawVertex(d, new UiVector2(0, 1), color));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 3);

        AddCommand(new UiDrawCommand(_currentClipRect, _currentTexture, startIndex, 6, 0));
    }

    public void AddQuadFilled(UiVector2 a, UiVector2 b, UiVector2 c, UiVector2 d, UiColor color, UiTextureId textureId)
    {
        EnsureCapacityFor(4, 6);
        var startVertex = (uint)_vertices.Count;
        var startIndex = (uint)_indices.Count;

        _vertices.Add(new UiDrawVertex(a, new UiVector2(0, 0), color));
        _vertices.Add(new UiDrawVertex(b, new UiVector2(1, 0), color));
        _vertices.Add(new UiDrawVertex(c, new UiVector2(1, 1), color));
        _vertices.Add(new UiDrawVertex(d, new UiVector2(0, 1), color));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 3);

        AddCommand(new UiDrawCommand(_currentClipRect, textureId, startIndex, 6, 0));
    }

    public void AddTriangle(UiVector2 a, UiVector2 b, UiVector2 c, UiColor color, float thickness = 1f)
    {
        ReadOnlySpan<UiVector2> points = [a, b, c];
        AddPolyline(points, true, color, thickness);
    }

    public void AddTriangleFilled(UiVector2 a, UiVector2 b, UiVector2 c, UiColor color)
    {
        AddTriangleFilled(a, b, c, color, _currentTexture);
    }

    public void AddTriangleFilled(UiVector2 a, UiVector2 b, UiVector2 c, UiColor color, UiTextureId textureId)
    {
        EnsureCapacityFor(3, 3);
        var startVertex = (uint)_vertices.Count;
        var startIndex = (uint)_indices.Count;

        _vertices.Add(new UiDrawVertex(a, new UiVector2(0, 0), color));
        _vertices.Add(new UiDrawVertex(b, new UiVector2(1, 0), color));
        _vertices.Add(new UiDrawVertex(c, new UiVector2(0, 1), color));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);

        AddCommand(new UiDrawCommand(_currentClipRect, textureId, startIndex, 3, 0));
    }

    public void AddTriangleFilled(UiVector2 a, UiVector2 b, UiVector2 c, UiColor color, UiTextureId textureId, UiRect clipRect)
    {
        clipRect = Intersect(clipRect, _currentClipRect);
        EnsureCapacityFor(3, 3);
        var startVertex = (uint)_vertices.Count;
        var startIndex = (uint)_indices.Count;

        _vertices.Add(new UiDrawVertex(a, new UiVector2(0, 0), color));
        _vertices.Add(new UiDrawVertex(b, new UiVector2(1, 0), color));
        _vertices.Add(new UiDrawVertex(c, new UiVector2(0, 1), color));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);

        AddCommand(new UiDrawCommand(clipRect, textureId, startIndex, 3, 0));
    }

    public void AddCircle(UiVector2 center, float radius, UiColor color, int segments = 0, float thickness = 1f)
    {
        var seg = segments > 0 ? segments : _CalcCircleAutoSegmentCount(radius);
        if (seg < 3)
        {
            return;
        }

        var points = new UiVector2[seg];
        var step = MathF.Tau / seg;
        for (var i = 0; i < seg; i++)
        {
            var a = step * i;
            points[i] = new UiVector2(center.X + MathF.Cos(a) * radius, center.Y + MathF.Sin(a) * radius);
        }

        AddPolyline(points, true, color, thickness);
    }

    public void AddNgon(UiVector2 center, float radius, int segments, UiColor color, float thickness = 1f)
    {
        AddCircle(center, radius, color, segments, thickness);
    }

    public void AddNgonFilled(UiVector2 center, float radius, int segments, UiColor color)
    {
        if (segments < 3)
        {
            return;
        }

        var points = new UiVector2[segments];
        var step = MathF.Tau / segments;
        for (var i = 0; i < segments; i++)
        {
            var a = step * i;
            points[i] = new UiVector2(center.X + MathF.Cos(a) * radius, center.Y + MathF.Sin(a) * radius);
        }

        AddConvexPolyFilled(points, color);
    }

    public void AddEllipse(UiVector2 center, UiVector2 radius, UiColor color, int segments = 0, float thickness = 1f)
    {
        var seg = segments > 0 ? segments : _CalcCircleAutoSegmentCount(MathF.Max(radius.X, radius.Y));
        if (seg < 3)
        {
            return;
        }

        var points = new UiVector2[seg];
        var step = MathF.Tau / seg;
        for (var i = 0; i < seg; i++)
        {
            var a = step * i;
            points[i] = new UiVector2(center.X + MathF.Cos(a) * radius.X, center.Y + MathF.Sin(a) * radius.Y);
        }

        AddPolyline(points, true, color, thickness);
    }

    public void AddEllipseFilled(UiVector2 center, UiVector2 radius, UiColor color, int segments = 0)
    {
        var seg = segments > 0 ? segments : _CalcCircleAutoSegmentCount(MathF.Max(radius.X, radius.Y));
        if (seg < 3)
        {
            return;
        }

        var points = new UiVector2[seg];
        var step = MathF.Tau / seg;
        for (var i = 0; i < seg; i++)
        {
            var a = step * i;
            points[i] = new UiVector2(center.X + MathF.Cos(a) * radius.X, center.Y + MathF.Sin(a) * radius.Y);
        }

        AddConvexPolyFilled(points, color);
    }

    public void AddText(UiVector2 position, UiColor color, string text)
    {
        if (_sharedData is null)
        {
            throw new InvalidOperationException("Draw list shared data is not configured.");
        }

        AddText(_sharedData.FontAtlas, text, position, color, _currentTexture, _currentClipRect, _sharedData.TextSettings, _sharedData.LineHeight);
    }

    public void AddBezierCubic(UiVector2 p1, UiVector2 p2, UiVector2 p3, UiVector2 p4, UiColor color, float thickness = 1f, int segments = 0)
    {
        var seg = segments > 0 ? segments : 12;
        var points = new UiVector2[seg + 1];
        for (var i = 0; i <= seg; i++)
        {
            var t = i / (float)seg;
            points[i] = CubicBezier(p1, p2, p3, p4, t);
        }

        AddPolyline(points, false, color, thickness);
    }

    public void AddBezierQuadratic(UiVector2 p1, UiVector2 p2, UiVector2 p3, UiColor color, float thickness = 1f, int segments = 0)
    {
        var seg = segments > 0 ? segments : 12;
        var points = new UiVector2[seg + 1];
        for (var i = 0; i <= seg; i++)
        {
            var t = i / (float)seg;
            points[i] = QuadraticBezier(p1, p2, p3, t);
        }

        AddPolyline(points, false, color, thickness);
    }

    public void AddPolyline(ReadOnlySpan<UiVector2> points, bool closed, UiColor color, float thickness = 1f)
    {
        var count = points.Length;
        if (count < 2)
        {
            return;
        }

        var halfThick = thickness * 0.5f;
        var segCount = closed ? count : count - 1;

        // For a single segment (open polyline with 2 points), fall back to AddLine
        if (segCount == 1 && !closed)
        {
            AddLine(points[0], points[1], color, thickness);
            return;
        }

        // Allocate vertices: 2 per point (inner + outer)
        var vertCount = count * 2;
        var idxCount = segCount * 6; // 2 triangles per segment
        EnsureCapacityFor(vertCount, idxCount);

        var startVertex = (uint)_vertices.Count;
        var startIndex = (uint)_indices.Count;
        var uv = new UiVector2(0.5f, 0.5f);

        // Compute offset direction at each point via miter
        for (var i = 0; i < count; i++)
        {
            UiVector2 prev, curr, next;
            curr = points[i];

            if (closed)
            {
                prev = points[(i + count - 1) % count];
                next = points[(i + 1) % count];
            }
            else
            {
                prev = i > 0 ? points[i - 1] : curr;
                next = i < count - 1 ? points[i + 1] : curr;
            }

            // Edge normals
            var d0x = curr.X - prev.X;
            var d0y = curr.Y - prev.Y;
            var d1x = next.X - curr.X;
            var d1y = next.Y - curr.Y;

            // Normalize
            var len0 = MathF.Sqrt(d0x * d0x + d0y * d0y);
            var len1 = MathF.Sqrt(d1x * d1x + d1y * d1y);

            float n0x, n0y, n1x, n1y;
            if (len0 > 0.0001f)
            {
                n0x = -d0y / len0;
                n0y = d0x / len0;
            }
            else
            {
                n0x = 0;
                n0y = 0;
            }

            if (len1 > 0.0001f)
            {
                n1x = -d1y / len1;
                n1y = d1x / len1;
            }
            else
            {
                n1x = 0;
                n1y = 0;
            }

            // Average normal for miter
            float mx, my;
            if (i == 0 && !closed)
            {
                mx = n1x;
                my = n1y;
            }
            else if (i == count - 1 && !closed)
            {
                mx = n0x;
                my = n0y;
            }
            else
            {
                mx = (n0x + n1x) * 0.5f;
                my = (n0y + n1y) * 0.5f;
            }

            var mLen = MathF.Sqrt(mx * mx + my * my);
            if (mLen > 0.0001f)
            {
                mx /= mLen;
                my /= mLen;
            }

            // Miter length: halfThick / dot(miter, normal)
            var dot = mx * n0x + my * n0y;
            if (i == 0 && !closed)
            {
                dot = mx * n1x + my * n1y;
            }

            if (MathF.Abs(dot) < 0.1f)
            {
                dot = 0.1f; // Clamp to avoid extreme miter spikes
            }

            var miterLen = halfThick / dot;
            // Clamp miter length to avoid spikes at very sharp angles
            var maxMiter = halfThick * 3f;
            if (miterLen > maxMiter) miterLen = maxMiter;
            if (miterLen < -maxMiter) miterLen = -maxMiter;

            var ox = mx * miterLen;
            var oy = my * miterLen;

            _vertices.Add(new UiDrawVertex(new UiVector2(curr.X + ox, curr.Y + oy), uv, color)); // outer
            _vertices.Add(new UiDrawVertex(new UiVector2(curr.X - ox, curr.Y - oy), uv, color)); // inner
        }

        // Indices: for each segment, make a quad from 2 consecutive vertex pairs
        for (var i = 0; i < segCount; i++)
        {
            var i0 = (uint)(i * 2);
            var i1 = (uint)(((i + 1) % count) * 2);

            _indices.Add(startVertex + i0);     // outer current
            _indices.Add(startVertex + i1);     // outer next
            _indices.Add(startVertex + i1 + 1); // inner next

            _indices.Add(startVertex + i0);     // outer current
            _indices.Add(startVertex + i1 + 1); // inner next
            _indices.Add(startVertex + i0 + 1); // inner current
        }

        AddCommand(new UiDrawCommand(_currentClipRect, _currentTexture, startIndex, (uint)idxCount, 0));
    }

    public void AddConvexPolyFilled(ReadOnlySpan<UiVector2> points, UiColor color)
    {
        if (points.Length < 3)
        {
            return;
        }

        var startVertex = (uint)_vertices.Count;
        for (var i = 0; i < points.Length; i++)
        {
            _vertices.Add(new UiDrawVertex(points[i], new UiVector2(0, 0), color));
        }

        var startIndex = (uint)_indices.Count;
        for (var i = 1; i < points.Length - 1; i++)
        {
            _indices.Add(startVertex);
            _indices.Add(startVertex + (uint)i);
            _indices.Add(startVertex + (uint)i + 1);
        }

        AddCommand(new UiDrawCommand(_currentClipRect, _currentTexture, startIndex, (uint)((points.Length - 2) * 3), 0));
    }

    public void AddConcavePolyFilled(ReadOnlySpan<UiVector2> points, UiColor color)
    {
        if (points.Length < 3)
        {
            return;
        }

        var indices = TriangulateConcave(points);
        if (indices.Count == 0)
        {
            return;
        }

        var startVertex = (uint)_vertices.Count;
        for (var i = 0; i < points.Length; i++)
        {
            _vertices.Add(new UiDrawVertex(points[i], new UiVector2(0, 0), color));
        }

        var startIndex = (uint)_indices.Count;
        foreach (var index in indices)
        {
            _indices.Add(startVertex + (uint)index);
        }

        AddCommand(new UiDrawCommand(_currentClipRect, _currentTexture, startIndex, (uint)indices.Count, 0));
    }

    public void AddImage(UiTextureId textureId, UiVector2 pMin, UiVector2 pMax, UiVector2 uvMin, UiVector2 uvMax, UiColor color)
    {
        AddImageQuad(textureId, pMin, new UiVector2(pMax.X, pMin.Y), pMax, new UiVector2(pMin.X, pMax.Y), uvMin, new UiVector2(uvMax.X, uvMin.Y), uvMax, new UiVector2(uvMin.X, uvMax.Y), color);
    }

    public void AddImageQuad(
        UiTextureId textureId,
        UiVector2 p1,
        UiVector2 p2,
        UiVector2 p3,
        UiVector2 p4,
        UiVector2 uv1,
        UiVector2 uv2,
        UiVector2 uv3,
        UiVector2 uv4,
        UiColor color)
    {
        EnsureCapacityFor(4, 6);
        var startVertex = (uint)_vertices.Count;
        var startIndex = (uint)_indices.Count;

        _vertices.Add(new UiDrawVertex(p1, uv1, color));
        _vertices.Add(new UiDrawVertex(p2, uv2, color));
        _vertices.Add(new UiDrawVertex(p3, uv3, color));
        _vertices.Add(new UiDrawVertex(p4, uv4, color));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 3);

        AddCommand(new UiDrawCommand(_currentClipRect, textureId, startIndex, 6, 0));
    }

    public void AddImageRounded(UiTextureId textureId, UiVector2 pMin, UiVector2 pMax, UiVector2 uvMin, UiVector2 uvMax, UiColor color, float rounding)
    {
        _ = rounding;
        AddImage(textureId, pMin, pMax, uvMin, uvMax, color);
    }

    public void PathClear() => _path.Clear();

    public void PathArcTo(UiVector2 center, float radius, float aMin, float aMax, int numSegments = 0)
    {
        var segments = numSegments > 0 ? numSegments : _CalcCircleAutoSegmentCount(radius);
        if (segments < 3)
        {
            return;
        }

        _PathArcToN(center, radius, aMin, aMax, segments);
    }

    public void PathArcToFast(UiVector2 center, float radius, int aMinOf12, int aMaxOf12)
    {
        _PathArcToFastEx(center, radius, aMinOf12, aMaxOf12, 12);
    }

    public void PathEllipticalArcTo(UiVector2 center, UiVector2 radius, float aMin, float aMax, int numSegments = 0)
    {
        var segments = numSegments > 0 ? numSegments : _CalcCircleAutoSegmentCount(MathF.Max(radius.X, radius.Y));
        if (segments < 3)
        {
            return;
        }

        var step = (aMax - aMin) / segments;
        for (var i = 0; i <= segments; i++)
        {
            var a = aMin + step * i;
            _path.Add(new UiVector2(center.X + MathF.Cos(a) * radius.X, center.Y + MathF.Sin(a) * radius.Y));
        }
    }

    public void PathBezierCubicCurveTo(UiVector2 p2, UiVector2 p3, UiVector2 p4, int numSegments = 0)
    {
        if (_path.Count == 0)
        {
            return;
        }

        var p1 = _path[^1];
        var segments = numSegments > 0 ? numSegments : 12;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            _path.Add(CubicBezier(p1, p2, p3, p4, t));
        }
    }

    public void PathBezierQuadraticCurveTo(UiVector2 p2, UiVector2 p3, int numSegments = 0)
    {
        if (_path.Count == 0)
        {
            return;
        }

        var p1 = _path[^1];
        var segments = numSegments > 0 ? numSegments : 12;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            _path.Add(QuadraticBezier(p1, p2, p3, t));
        }
    }

    public void PathRect(UiVector2 pMin, UiVector2 pMax, float rounding = 0f)
    {
        _ = rounding;
        _path.Add(pMin);
        _path.Add(new UiVector2(pMax.X, pMin.Y));
        _path.Add(pMax);
        _path.Add(new UiVector2(pMin.X, pMax.Y));
    }

    public void AddCallback(UiDrawCallback callback, object? userData = null)
    {
        ArgumentNullException.ThrowIfNull(callback);
        AddCommand(new UiDrawCommand(_currentClipRect, _currentTexture, (uint)_indices.Count, 0, 0, callback, userData));
    }

    public void AddDrawCmd()
    {
        AddCommand(new UiDrawCommand(_currentClipRect, _currentTexture, (uint)_indices.Count, 0, 0));
    }

    public UiDrawList CloneOutput()
    {
        return new UiDrawList(
            UiPooledList<UiDrawVertex>.RentAndCopy(_vertices.AsSpan()),
            UiPooledList<uint>.RentAndCopy(_indices.AsSpan()),
            UiPooledList<UiDrawCommand>.RentAndCopy(_commands.AsSpan())
        );
    }

    public void PrimReserve(int indexCount, int vertexCount)
    {
        EnsureCapacityFor(vertexCount, indexCount);
    }

    public void PrimUnreserve(int indexCount, int vertexCount)
    {
        if (indexCount > 0 && _indices.Count >= indexCount)
        {
            _indices.RemoveRange(_indices.Count - indexCount, indexCount);
        }

        if (vertexCount > 0 && _vertices.Count >= vertexCount)
        {
            _vertices.RemoveRange(_vertices.Count - vertexCount, vertexCount);
        }
    }

    public void PrimRect(UiVector2 a, UiVector2 b, UiColor color)
    {
        AddRectFilled(new UiRect(a.X, a.Y, b.X - a.X, b.Y - a.Y), color);
    }

    public void PrimRectUV(UiVector2 a, UiVector2 c, UiVector2 uvA, UiVector2 uvC, UiColor color)
    {
        var b = new UiVector2(c.X, a.Y);
        var d = new UiVector2(a.X, c.Y);
        var uvB = new UiVector2(uvC.X, uvA.Y);
        var uvD = new UiVector2(uvA.X, uvC.Y);
        AddImageQuad(_currentTexture, a, b, c, d, uvA, uvB, uvC, uvD, color);
    }

    public void PrimQuadUV(UiVector2 a, UiVector2 b, UiVector2 c, UiVector2 d, UiVector2 uvA, UiVector2 uvB, UiVector2 uvC, UiVector2 uvD, UiColor color)
    {
        AddImageQuad(_currentTexture, a, b, c, d, uvA, uvB, uvC, uvD, color);
    }

    public void _SetDrawListSharedData(UiDrawListSharedData data)
    {
        _sharedData = data ?? throw new ArgumentNullException(nameof(data));
    }

    public void _ResetForNewFrame()
    {
        if (_channels is not null)
        {
            SetCurrentChannel(0);
            foreach (var channel in _channels)
            {
                channel.Vertices.Clear();
                channel.Indices.Clear();
                channel.Commands.Clear();
                channel.DrawLists.Clear();
            }
        }

        _vertices.Clear();
        _indices.Clear();
        _commands.Clear();
        _drawLists.Clear();
        _path.Clear();
        _clipStack.Clear();
        _textureStack.Clear();
        _currentClipRect = _clipRect;
        _currentTexture = default;
        _channels = null;
        _currentChannel = 0;
    }

    public void _ClearFreeMemory()
    {
        if (_channels is not null)
        {
            foreach (var channel in _channels)
            {
                channel.Vertices.Clear();
                channel.Indices.Clear();
                channel.Commands.Clear();
                channel.DrawLists.Clear();
                channel.Vertices.TrimExcess();
                channel.Indices.TrimExcess();
                channel.Commands.TrimExcess();
                channel.DrawLists.TrimExcess();
            }
        }

        _vertices.Clear();
        _indices.Clear();
        _commands.Clear();
        _drawLists.Clear();
        _vertices.TrimExcess();
        _indices.TrimExcess();
        _commands.TrimExcess();
        _drawLists.TrimExcess();
    }

    public void _PopUnusedDrawCmd()
    {
        if (_commands.Count == 0)
        {
            return;
        }

        if (_commands[^1].ElementCount == 0)
        {
            _commands.RemoveAt(_commands.Count - 1);
        }
    }

    public void _TryMergeDrawCmds()
    {
        if (_commands.Count <= 1)
        {
            return;
        }

        for (var i = _commands.Count - 2; i >= 0; i--)
        {
            var a = _commands[i];
            var b = _commands[i + 1];
            if (a.TextureId == b.TextureId
                && a.ClipRect == b.ClipRect
                && a.Translation == b.Translation
                && a.IndexOffset + a.ElementCount == b.IndexOffset
                && a.Callback == b.Callback
                && Equals(a.UserData, b.UserData))
            {
                _commands[i] = a with { ElementCount = a.ElementCount + b.ElementCount };
                _commands.RemoveAt(i + 1);
            }
        }
    }

    public void _OnChangedClipRect()
    {
        _PopUnusedDrawCmd();
        AddDrawCmd();
    }

    public void _OnChangedTexture()
    {
        _PopUnusedDrawCmd();
        AddDrawCmd();
    }

    public void _OnChangedVtxOffset()
    {
        _PopUnusedDrawCmd();
        AddDrawCmd();
    }

    public void _SetTexture(UiTextureId textureId)
    {
        _currentTexture = textureId;
        _OnChangedTexture();
    }

    public int _CalcCircleAutoSegmentCount(float radius)
    {
        if (radius <= 0f)
        {
            return 0;
        }

        var segments = (int)MathF.Ceiling(MathF.Tau * radius / 8f);
        return Math.Clamp(segments, 6, 100);
    }

    public void _PathArcToFastEx(UiVector2 center, float radius, int aMinOf12, int aMaxOf12, int steps)
    {
        _PathArcToN(center, radius, (aMinOf12 / 12f) * MathF.Tau, (aMaxOf12 / 12f) * MathF.Tau, steps);
    }

    public void _PathArcToN(UiVector2 center, float radius, float aMin, float aMax, int numSegments)
    {
        if (numSegments < 1)
        {
            return;
        }

        var step = (aMax - aMin) / numSegments;
        for (var i = 0; i <= numSegments; i++)
        {
            var a = aMin + step * i;
            _path.Add(new UiVector2(center.X + MathF.Cos(a) * radius, center.Y + MathF.Sin(a) * radius));
        }
    }

    public void Split(int count)
    {
        if (count <= 1 || _channels is not null)
        {
            return;
        }

        _channels = new List<Channel>(count);
        _channels.Add(new Channel(_vertices, _indices, _commands, _drawLists));
        for (var i = 1; i < count; i++)
        {
            _channels.Add(new Channel(new PooledBuffer<UiDrawVertex>(), new PooledBuffer<uint>(), new PooledBuffer<UiDrawCommand>(), new List<UiDrawList>()));
        }
        _currentChannel = 0;
    }

    public void Merge()
    {
        if (_channels is null)
        {
            return;
        }

        SetCurrentChannel(0);
        for (var i = 1; i < _channels.Count; i++)
        {
            var channel = _channels[i];
            FlushChannel(channel);
            foreach (var list in channel.DrawLists)
            {
                AddDrawList(list);
            }
        }

        _channels = null;
        _currentChannel = 0;
    }

    public void SetCurrentChannel(int channelIndex)
    {
        if (_channels is null)
        {
            return;
        }

        if (channelIndex < 0 || channelIndex >= _channels.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(channelIndex));
        }

        if (_currentChannel == channelIndex)
        {
            return;
        }

        var target = _channels[channelIndex];
        _vertices = target.Vertices;
        _indices = target.Indices;
        _commands = target.Commands;
        _drawLists = target.DrawLists;
        _currentChannel = channelIndex;
    }

    public void AddDrawList(UiDrawList list)
    {
        if (list.Vertices.Count == 0 || list.Indices.Count == 0)
        {
            return;
        }

        if (RequiresSplit(list))
        {
            AddSplitDrawList(list);
            return;
        }

        EnsureCapacityFor(list.Vertices.Count, list.Indices.Count);
        var vertexOffset = (uint)_vertices.Count;
        var indexOffset = (uint)_indices.Count;

        _vertices.AddRange(list.Vertices);
        foreach (var index in list.Indices)
        {
            _indices.Add(index + vertexOffset);
        }

        foreach (var cmd in list.Commands)
        {
            AddCommand(new UiDrawCommand(cmd.ClipRect, cmd.TextureId, cmd.IndexOffset + indexOffset, cmd.ElementCount, 0, cmd.Callback, cmd.UserData));
        }
    }

    private static bool RequiresSplit(UiDrawList list)
    {
        if (list.Vertices.Count <= MaxVerticesPerList && list.Indices.Count <= MaxIndicesPerList)
        {
            return false;
        }

        if (list.Commands.Count != 1)
        {
            return false;
        }

        return list.Vertices.Count % 4 == 0 && list.Indices.Count == (list.Vertices.Count / 4) * 6;
    }

    private void AddSplitDrawList(UiDrawList list)
    {
        var maxQuadsByVertices = MaxVerticesPerList / 4;
        var maxQuadsByIndices = MaxIndicesPerList / 6;
        var maxQuads = Math.Max(1, Math.Min(maxQuadsByVertices, maxQuadsByIndices));
        var totalQuads = list.Vertices.Count / 4;
        var cmd = list.Commands[0];
        var quadIndex = 0;

        while (quadIndex < totalQuads)
        {
            var quadCount = Math.Min(maxQuads, totalQuads - quadIndex);
            var vertexStart = quadIndex * 4;
            var vertexCount = quadCount * 4;
            var indexStart = quadIndex * 6;
            var indexCount = quadCount * 6;

            EnsureCapacityFor(vertexCount, indexCount);
            var vertexOffset = (uint)_vertices.Count;
            var indexOffset = (uint)_indices.Count;

            for (var i = 0; i < vertexCount; i++)
            {
                _vertices.Add(list.Vertices[vertexStart + i]);
            }

            for (var i = 0; i < indexCount; i++)
            {
                var local = list.Indices[indexStart + i] - (uint)vertexStart;
                _indices.Add(local + vertexOffset);
            }

            AddCommand(new UiDrawCommand(cmd.ClipRect, cmd.TextureId, cmd.IndexOffset + indexOffset, (uint)indexCount, 0));
            quadIndex += quadCount;
        }
    }

    private void AddCommand(UiDrawCommand cmd)
    {
        if (_commands.Count > 0)
        {
            var last = _commands[^1];
            var contiguous = last.IndexOffset + last.ElementCount == cmd.IndexOffset;
            if (contiguous
                && last.TextureId == cmd.TextureId
                && last.ClipRect == cmd.ClipRect
                && last.Translation == cmd.Translation
                && last.Callback == cmd.Callback
                && Equals(last.UserData, cmd.UserData))
            {
                _commands[^1] = last with { ElementCount = last.ElementCount + cmd.ElementCount };
                return;
            }
        }

        _commands.Add(cmd);
    }

    public UiPooledList<UiDrawList> Build()
    {
        Flush();
        return _drawLists.Count == 0
            ? UiPooledList<UiDrawList>.FromArray(Array.Empty<UiDrawList>())
            : UiPooledList<UiDrawList>.RentAndCopy(_drawLists);
    }

    public void Append(UiDrawList drawList, float opacity = 1f, UiVector2 translation = default)
    {
        if (drawList.Vertices.Count == 0 || drawList.Indices.Count == 0 || drawList.Commands.Count == 0)
        {
            return;
        }

        var alphaFactor = Math.Clamp(opacity, 0f, 1f);
        var applyAlpha = alphaFactor < 0.999f;
        var applyTranslation = MathF.Abs(translation.X) > 0.0001f || MathF.Abs(translation.Y) > 0.0001f;

        if (!applyAlpha && !applyTranslation)
        {
            Append(drawList.Vertices.AsSpan(), drawList.Indices.AsSpan(), drawList.Commands.AsSpan());
            return;
        }

        EnsureCapacityFor(drawList.Vertices.Count, drawList.Indices.Count);

        var vertexOffset = (uint)_vertices.Count;
        var indexOffset = (uint)_indices.Count;

        for (var i = 0; i < drawList.Vertices.Count; i++)
        {
            var vertex = drawList.Vertices[i];
            if (applyTranslation)
            {
                vertex = vertex with
                {
                    Position = new UiVector2(vertex.Position.X + translation.X, vertex.Position.Y + translation.Y)
                };
            }

            if (applyAlpha)
            {
                var rgba = vertex.Color.Rgba;
                var alpha = (uint)((rgba >> 24) & 0xFFu);
                var nextAlpha = (uint)Math.Clamp((int)MathF.Round(alpha * alphaFactor), 0, 255);
                vertex = vertex with { Color = new UiColor((rgba & 0x00FFFFFFu) | (nextAlpha << 24)) };
            }

            _vertices.Add(vertex);
        }

        for (var i = 0; i < drawList.Indices.Count; i++)
        {
            _indices.Add(drawList.Indices[i] + vertexOffset);
        }

        for (var i = 0; i < drawList.Commands.Count; i++)
        {
            var command = drawList.Commands[i];
            if (applyTranslation)
            {
                var clip = command.ClipRect;
                command = command with
                {
                    ClipRect = new UiRect(clip.X + translation.X, clip.Y + translation.Y, clip.Width, clip.Height)
                };
            }

            AddCommand(command with { IndexOffset = command.IndexOffset + indexOffset });
        }
    }

    public void Append(ReadOnlySpan<UiDrawVertex> vertices, ReadOnlySpan<uint> indices, ReadOnlySpan<UiDrawCommand> commands)
    {
        if (vertices.Length == 0 || indices.Length == 0 || commands.Length == 0)
        {
            return;
        }

        EnsureCapacityFor(vertices.Length, indices.Length);

        var vertexOffset = (uint)_vertices.Count;
        var indexOffset = (uint)_indices.Count;

        var vertexWriteStart = _vertices.Count;
        _vertices.SetCount(vertexWriteStart + vertices.Length);
        vertices.CopyTo(_vertices.AsSpan().Slice(vertexWriteStart, vertices.Length));

        var indexWriteStart = _indices.Count;
        _indices.SetCount(indexWriteStart + indices.Length);
        var targetIndices = _indices.AsSpan().Slice(indexWriteStart, indices.Length);

        if (vertexOffset == 0)
        {
            indices.CopyTo(targetIndices);
        }
        else
        {
            for (var i = 0; i < indices.Length; i++)
            {
                targetIndices[i] = indices[i] + vertexOffset;
            }
        }

        for (var i = 0; i < commands.Length; i++)
        {
            var command = commands[i];
            AddCommand(command with { IndexOffset = command.IndexOffset + indexOffset });
        }
    }

    public void FlushCurrentDrawList()
    {
        Flush();
    }

    private void EnsureCapacityFor(int addVertices, int addIndices)
    {
        if (_vertices.Count == 0 && _indices.Count == 0)
        {
            return;
        }

        if (_vertices.Count + addVertices > MaxVerticesPerList || _indices.Count + addIndices > MaxIndicesPerList)
        {
            Flush();
        }
    }

    private void Flush()
    {
        if (_vertices.Count == 0 && _indices.Count == 0)
        {
            return;
        }

        _PopUnusedDrawCmd();
        _TryMergeDrawCmds();

        _drawLists.Add(
            new UiDrawList(
                _vertices.TransferToPooledList(),
                _indices.TransferToPooledList(),
                _commands.TransferToPooledList()
            )
        );
    }

    private void FlushChannel(Channel channel)
    {
        if (channel.Vertices.Count == 0 && channel.Indices.Count == 0)
        {
            return;
        }

        if (channel.Commands.Count > 0 && channel.Commands[^1].ElementCount == 0)
        {
            channel.Commands.RemoveAt(channel.Commands.Count - 1);
        }

        channel.DrawLists.Add(
            new UiDrawList(
                channel.Vertices.TransferToPooledList(),
                channel.Indices.TransferToPooledList(),
                channel.Commands.TransferToPooledList()
            )
        );
    }

    private static UiRect Intersect(UiRect a, UiRect b)
    {
        var x1 = MathF.Max(a.X, b.X);
        var y1 = MathF.Max(a.Y, b.Y);
        var x2 = MathF.Min(a.X + a.Width, b.X + b.Width);
        var y2 = MathF.Min(a.Y + a.Height, b.Y + b.Height);
        if (x2 < x1 || y2 < y1)
        {
            return new UiRect(x1, y1, 0f, 0f);
        }

        return new UiRect(x1, y1, x2 - x1, y2 - y1);
    }

    private static UiVector2 CubicBezier(UiVector2 p1, UiVector2 p2, UiVector2 p3, UiVector2 p4, float t)
    {
        var u = 1f - t;
        var tt = t * t;
        var uu = u * u;
        var uuu = uu * u;
        var ttt = tt * t;

        var x = uuu * p1.X + 3f * uu * t * p2.X + 3f * u * tt * p3.X + ttt * p4.X;
        var y = uuu * p1.Y + 3f * uu * t * p2.Y + 3f * u * tt * p3.Y + ttt * p4.Y;
        return new UiVector2(x, y);
    }

    private static UiVector2 QuadraticBezier(UiVector2 p1, UiVector2 p2, UiVector2 p3, float t)
    {
        var u = 1f - t;
        var tt = t * t;
        var uu = u * u;
        var x = uu * p1.X + 2f * u * t * p2.X + tt * p3.X;
        var y = uu * p1.Y + 2f * u * t * p2.Y + tt * p3.Y;
        return new UiVector2(x, y);
    }

    private static List<int> TriangulateConcave(ReadOnlySpan<UiVector2> points)
    {
        var n = points.Length;
        var result = new List<int>(Math.Max(0, (n - 2) * 3));
        if (n < 3)
        {
            return result;
        }

        var v = new List<int>(n);
        if (PolygonArea(points) > 0f)
        {
            for (var i = 0; i < n; i++)
            {
                v.Add(i);
            }
        }
        else
        {
            for (var i = 0; i < n; i++)
            {
                v.Add(n - 1 - i);
            }
        }

        var count = 2 * n;
        for (var m = 0; n > 2;)
        {
            if (count-- <= 0)
            {
                break;
            }

            var u = m % n;
            var w = (m + 1) % n;
            var t = (m + 2) % n;

            if (Snip(u, w, t, n, v, points))
            {
                var a = v[u];
                var b = v[w];
                var c = v[t];
                result.Add(a);
                result.Add(b);
                result.Add(c);
                v.RemoveAt(w);
                n--;
                count = 2 * n;
                m = 0;
            }
            else
            {
                m++;
            }
        }

        return result;
    }

    private static float PolygonArea(ReadOnlySpan<UiVector2> points)
    {
        var n = points.Length;
        var area = 0f;
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            area += points[i].X * points[j].Y - points[j].X * points[i].Y;
        }

        return area * 0.5f;
    }

    private static bool Snip(int u, int v, int w, int n, List<int> vtx, ReadOnlySpan<UiVector2> points)
    {
        var a = points[vtx[u]];
        var b = points[vtx[v]];
        var c = points[vtx[w]];

        if (((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) <= 1e-5f)
        {
            return false;
        }

        for (var p = 0; p < n; p++)
        {
            if (p == u || p == v || p == w)
            {
                continue;
            }

            var pt = points[vtx[p]];
            if (PointInTriangle(a, b, c, pt))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PointInTriangle(UiVector2 a, UiVector2 b, UiVector2 c, UiVector2 p)
    {
        var ab = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        var bc = (c.X - b.X) * (p.Y - b.Y) - (c.Y - b.Y) * (p.X - b.X);
        var ca = (a.X - c.X) * (p.Y - c.Y) - (a.Y - c.Y) * (p.X - c.X);
        var hasNeg = (ab < 0f) || (bc < 0f) || (ca < 0f);
        var hasPos = (ab > 0f) || (bc > 0f) || (ca > 0f);
        return !(hasNeg && hasPos);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHangulCodepoint(int codepoint)
    {
        return (codepoint >= 0xAC00 && codepoint <= 0xD7A3)
            || (codepoint >= 0x1100 && codepoint <= 0x11FF)
            || (codepoint >= 0x3130 && codepoint <= 0x318F);
    }

    private static float Snap(float value, bool enabled) => enabled ? MathF.Round(value) : value;
}

