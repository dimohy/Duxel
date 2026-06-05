using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Duxel.Core;

public sealed class UiDrawListBuilder(UiRect clipRect)
{
    private const int MaxVerticesPerList = 60_000;
    private const int MaxIndicesPerList = 120_000;
    private const int MaxBuilderCommandScheduleCacheEntries = 64;
    private static readonly bool BuilderCommandSchedulerEnabled = ParseBooleanEnvironmentFlag("DUXEL_UI_COMMAND_SCHEDULER");
    private static readonly int CommandSchedulerMaxWindow = ParsePositiveIntEnvironment("DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW", 768);
    private static readonly Dictionary<ulong, BuilderCommandScheduleCacheEntry> BuilderCommandScheduleCache = new();

    private PooledBuffer<UiDrawVertex> _vertices = new();
    private PooledBuffer<uint> _indices = new();
    private PooledBuffer<UiDrawCommand> _commands = new();
    private PooledBuffer<UiRectFilledPrimitive> _rectFilledPrimitives = new();
    private PooledBuffer<UiCircleFilledPrimitive> _circleFilledPrimitives = new();
    private List<UiDrawList> _drawLists = [];
    private UiRect _clipRect = clipRect;
    private UiRect _currentClipRect = clipRect;
    private readonly Stack<UiRect> _clipStack = new();
    private UiTextureId _currentTexture = default;
    private readonly Stack<UiTextureId> _textureStack = new();
    private object? _currentCommandUserData;
    private readonly Stack<object?> _commandUserDataStack = new();
    private readonly List<UiVector2> _path = [];
    private readonly List<UiVector2> _scratchPath = [];
    private UiDrawListSharedData? _sharedData;
    private List<Channel>? _channels;
    private readonly List<Channel> _channelPool = [];
    private int _currentChannel;

    private sealed class BuilderCommandScheduleCacheEntry(int commandCount, UiDrawCommand[] commands)
    {
        public int CommandCount { get; } = commandCount;
        public UiDrawCommand[] Commands { get; } = commands;
    }

    private enum BuilderCommandSchedulingClass
    {
        SolidPrimitive = 0,
        SolidTriangle = 1,
        FontTriangle = 2,
        TexturedTriangle = 3,
    }

    private sealed class Channel(PooledBuffer<UiDrawVertex> vertices, PooledBuffer<uint> indices, PooledBuffer<UiDrawCommand> commands, PooledBuffer<UiRectFilledPrimitive> rectFilledPrimitives, PooledBuffer<UiCircleFilledPrimitive> circleFilledPrimitives, List<UiDrawList> drawLists)
    {
        public PooledBuffer<UiDrawVertex> Vertices { get; } = vertices;
        public PooledBuffer<uint> Indices { get; } = indices;
        public PooledBuffer<UiDrawCommand> Commands { get; } = commands;
        public PooledBuffer<UiRectFilledPrimitive> RectFilledPrimitives { get; } = rectFilledPrimitives;
        public PooledBuffer<UiCircleFilledPrimitive> CircleFilledPrimitives { get; } = circleFilledPrimitives;
        public List<UiDrawList> DrawLists { get; } = drawLists;
    }

    public void Reserve(int vertexCapacity, int indexCapacity, int commandCapacity)
    {
        _vertices.EnsureCapacity(vertexCapacity);
        _indices.EnsureCapacity(indexCapacity);
        _commands.EnsureCapacity(commandCapacity);
    }

    public void AddRectFilled(UiRect rect, UiColor color, UiTextureId textureId)
    {
        AddRectFilledGeometry(rect, color, textureId, _currentClipRect);
    }

    public void AddRectFilled(UiRect rect, UiColor color, UiTextureId textureId, UiRect clipRect)
    {
        clipRect = Intersect(clipRect, _currentClipRect);
        AddRectFilledGeometry(rect, color, textureId, clipRect);
    }

    private void AddRectFilledGeometry(UiRect rect, UiColor color, UiTextureId textureId, UiRect clipRect)
    {
        var primitiveOffset = (uint)_rectFilledPrimitives.Count;
        _rectFilledPrimitives.Add(new UiRectFilledPrimitive(rect, color));
        AddRectFilledPrimitiveCommand(clipRect, textureId, primitiveOffset, rect);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddRectFilledPrimitiveCommand(UiRect clipRect, UiTextureId textureId, uint primitiveOffset, UiRect bounds)
    {
        var userData = _currentCommandUserData;
        if (_commands.Count > 0)
        {
            var last = _commands[^1];
            if (last.Kind == UiDrawCommandKind.RectFilledPrimitives
                && last.IndexOffset + last.ElementCount == primitiveOffset
                && last.TextureId == textureId
                && last.ClipRect == clipRect
                && last.Translation == default
                && last.Callback is null
                && Equals(last.UserData, userData))
            {
                _commands[^1] = last with
                {
                    ElementCount = last.ElementCount + 1,
                    Bounds = last.HasBounds ? Union(last.Bounds, bounds) : bounds,
                    HasBounds = true
                };
                return;
            }
        }

        _commands.Add(new UiDrawCommand(clipRect, textureId, primitiveOffset, 1, 0, UserData: userData, Kind: UiDrawCommandKind.RectFilledPrimitives, Bounds: bounds, HasBounds: true));
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

        var clipMaxX = clipRect.X + clipRect.Width;
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
        var segmentBounds = default(UiRect);
        var hasSegmentBounds = false;

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

            if (x > clipMaxX)
            {
                var nextLine = span.Slice(index).IndexOf('\n');
                if (nextLine < 0)
                {
                    break;
                }

                index += nextLine + 1;
                x = Snap(position.X, pixelSnap);
                y = Snap(y + effectiveLineHeight, pixelSnap);
                baselineY = Snap(y + baselineOffset, pixelSnap);
                hasPrev = false;
                lineStart = true;
                continue;
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

            var hasGlyph = font.TryGetGlyph(codepoint, out var glyph);
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
                    AddCommand(new UiDrawCommand(clipRect, textureId, segmentStartIndex, (uint)segmentElementCount, 0, Bounds: segmentBounds, HasBounds: hasSegmentBounds));
                }

                Flush();
                segmentStartIndex = (uint)_indices.Count;
                segmentElementCount = 0;
                segmentBounds = default;
                hasSegmentBounds = false;
            }

            var x0 = Snap(x + (glyph.OffsetX * scale), pixelSnap);
            var y0 = pixelSnap
                ? baselineY + MathF.Round(glyph.OffsetY * scale)
                : y + baselineOffset + (glyph.OffsetY * scale);
            var x1 = Snap(x0 + (glyph.Width * scale), pixelSnap);
            var y1 = Snap(y0 + (glyph.Height * scale), pixelSnap);

            var isOutsideClip = x1 <= clipRect.X || x0 >= clipMaxX || y1 <= clipRect.Y || y0 >= clipMaxY;
            if (isOutsideClip)
            {
                x = Snap(x + (glyph.AdvanceX * scale), pixelSnap);
                hasPrev = true;
                prevChar = codepoint;
                continue;
            }

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
            var glyphBounds = new UiRect(x0, y0, x1 - x0, y1 - y0);
            segmentBounds = hasSegmentBounds ? Union(segmentBounds, glyphBounds) : glyphBounds;
            hasSegmentBounds = true;
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
            AddCommand(new UiDrawCommand(clipRect, textureId, segmentStartIndex, (uint)segmentElementCount, 0, Bounds: segmentBounds, HasBounds: hasSegmentBounds));
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
        var primitiveOffset = (uint)_circleFilledPrimitives.Count;
        _circleFilledPrimitives.Add(new UiCircleFilledPrimitive(center, radius, color, segments));
        AddCircleFilledPrimitiveCommand(clipRect, textureId, primitiveOffset, (uint)segments, CreateCircleBounds(center, radius));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddCircleFilledPrimitiveCommand(UiRect clipRect, UiTextureId textureId, uint primitiveOffset, uint segments, UiRect bounds)
    {
        var userData = _currentCommandUserData;
        if (_commands.Count > 0)
        {
            var last = _commands[^1];
            if (last.Kind == UiDrawCommandKind.CircleFilledPrimitives
                && last.IndexOffset + last.ElementCount == primitiveOffset
                && last.VertexOffset == segments
                && last.TextureId == textureId
                && last.ClipRect == clipRect
                && last.Translation == default
                && last.Callback is null
                && Equals(last.UserData, userData))
            {
                _commands[^1] = last with
                {
                    ElementCount = last.ElementCount + 1,
                    Bounds = last.HasBounds ? Union(last.Bounds, bounds) : bounds,
                    HasBounds = true
                };
                return;
            }
        }

        _commands.Add(new UiDrawCommand(clipRect, textureId, primitiveOffset, 1, segments, UserData: userData, Kind: UiDrawCommandKind.CircleFilledPrimitives, Bounds: bounds, HasBounds: true));
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

    public void PushCommandUserData(object? userData)
    {
        _commandUserDataStack.Push(_currentCommandUserData);
        _currentCommandUserData = userData;
    }

    public void PopCommandUserData()
    {
        if (_commandUserDataStack.Count == 0)
        {
            return;
        }

        _currentCommandUserData = _commandUserDataStack.Pop();
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
        if (dy == 0f)
        {
            var width = MathF.Abs(dx);
            if (width <= 0f)
            {
                return;
            }

            var half = thickness * 0.5f;
            AddRectFilledGeometry(new UiRect(MathF.Min(p1.X, p2.X), p1.Y - half, width, thickness), color, textureId, _currentClipRect);
            return;
        }

        if (dx == 0f)
        {
            var height = MathF.Abs(dy);
            if (height <= 0f)
            {
                return;
            }

            var half = thickness * 0.5f;
            AddRectFilledGeometry(new UiRect(p1.X - half, MathF.Min(p1.Y, p2.Y), thickness, height), color, textureId, _currentClipRect);
            return;
        }

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
        if (thickness <= 0f)
        {
            return;
        }

        if (rounding <= 0f)
        {
            AddAxisAlignedRectStroke(rect, color, thickness, _currentTexture, _currentClipRect);
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

    private void AddAxisAlignedRectStroke(UiRect rect, UiColor color, float thickness, UiTextureId textureId, UiRect clipRect)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var half = thickness * 0.5f;
        var x0 = rect.X - half;
        var y0 = rect.Y - half;
        var x1 = rect.X + rect.Width + half;
        var y1 = rect.Y + rect.Height + half;
        var width = x1 - x0;
        var height = y1 - y0;

        AddRectFilledGeometry(new UiRect(x0, y0, width, thickness), color, textureId, clipRect);
        AddRectFilledGeometry(new UiRect(x0, y1 - thickness, width, thickness), color, textureId, clipRect);

        var sideHeight = height - (thickness * 2f);
        if (sideHeight > 0f)
        {
            AddRectFilledGeometry(new UiRect(x0, y0 + thickness, thickness, sideHeight), color, textureId, clipRect);
            AddRectFilledGeometry(new UiRect(x1 - thickness, y0 + thickness, thickness, sideHeight), color, textureId, clipRect);
        }
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

        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X, rect.Y), default, colUprLeft));
        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X + rect.Width, rect.Y), default, colUprRight));
        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X + rect.Width, rect.Y + rect.Height), default, colBotRight));
        _vertices.Add(new UiDrawVertex(new UiVector2(rect.X, rect.Y + rect.Height), default, colBotLeft));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 3);

        AddCommand(new UiDrawCommand(_currentClipRect, _currentTexture, startIndex, 6, 0, Bounds: rect, HasBounds: true));
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

        _vertices.Add(new UiDrawVertex(a, default, color));
        _vertices.Add(new UiDrawVertex(b, default, color));
        _vertices.Add(new UiDrawVertex(c, default, color));
        _vertices.Add(new UiDrawVertex(d, default, color));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 3);

        AddCommand(new UiDrawCommand(_currentClipRect, _currentTexture, startIndex, 6, 0, Bounds: CreateQuadBounds(a, b, c, d), HasBounds: true));
    }

    public void AddQuadFilled(UiVector2 a, UiVector2 b, UiVector2 c, UiVector2 d, UiColor color, UiTextureId textureId)
    {
        EnsureCapacityFor(4, 6);
        var startVertex = (uint)_vertices.Count;
        var startIndex = (uint)_indices.Count;

        _vertices.Add(new UiDrawVertex(a, default, color));
        _vertices.Add(new UiDrawVertex(b, default, color));
        _vertices.Add(new UiDrawVertex(c, default, color));
        _vertices.Add(new UiDrawVertex(d, default, color));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 2);
        _indices.Add(startVertex + 3);

        AddCommand(new UiDrawCommand(_currentClipRect, textureId, startIndex, 6, 0, Bounds: CreateQuadBounds(a, b, c, d), HasBounds: true));
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

        _vertices.Add(new UiDrawVertex(a, default, color));
        _vertices.Add(new UiDrawVertex(b, default, color));
        _vertices.Add(new UiDrawVertex(c, default, color));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);

        AddCommand(new UiDrawCommand(_currentClipRect, textureId, startIndex, 3, 0, Bounds: CreateTriangleBounds(a, b, c), HasBounds: true));
    }

    public void AddTriangleFilled(UiVector2 a, UiVector2 b, UiVector2 c, UiColor color, UiTextureId textureId, UiRect clipRect)
    {
        clipRect = Intersect(clipRect, _currentClipRect);
        EnsureCapacityFor(3, 3);
        var startVertex = (uint)_vertices.Count;
        var startIndex = (uint)_indices.Count;

        _vertices.Add(new UiDrawVertex(a, default, color));
        _vertices.Add(new UiDrawVertex(b, default, color));
        _vertices.Add(new UiDrawVertex(c, default, color));

        _indices.Add(startVertex + 0);
        _indices.Add(startVertex + 1);
        _indices.Add(startVertex + 2);

        AddCommand(new UiDrawCommand(clipRect, textureId, startIndex, 3, 0, Bounds: CreateTriangleBounds(a, b, c), HasBounds: true));
    }

    public void AddCircle(UiVector2 center, float radius, UiColor color, int segments = 0, float thickness = 1f)
    {
        var seg = segments > 0 ? segments : _CalcCircleAutoSegmentCount(radius);
        if (seg < 3)
        {
            return;
        }

        AddPolyline(BuildEllipseScratch(center, new UiVector2(radius, radius), seg), true, color, thickness);
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

        AddConvexPolyFilled(BuildEllipseScratch(center, new UiVector2(radius, radius), segments), color);
    }

    public void AddEllipse(UiVector2 center, UiVector2 radius, UiColor color, int segments = 0, float thickness = 1f)
    {
        var seg = segments > 0 ? segments : _CalcCircleAutoSegmentCount(MathF.Max(radius.X, radius.Y));
        if (seg < 3)
        {
            return;
        }

        AddPolyline(BuildEllipseScratch(center, radius, seg), true, color, thickness);
    }

    public void AddEllipseFilled(UiVector2 center, UiVector2 radius, UiColor color, int segments = 0)
    {
        var seg = segments > 0 ? segments : _CalcCircleAutoSegmentCount(MathF.Max(radius.X, radius.Y));
        if (seg < 3)
        {
            return;
        }

        AddConvexPolyFilled(BuildEllipseScratch(center, radius, seg), color);
    }

    public void AddText(UiVector2 position, UiColor color, string text)
    {
        if (_sharedData is null)
        {
            throw new InvalidOperationException("Draw list shared data is not configured.");
        }

        AddText(_sharedData.FontAtlas, text, position, color, _sharedData.FontTexture, _currentClipRect, _sharedData.TextSettings, _sharedData.LineHeight);
    }

    public void AddBezierCubic(UiVector2 p1, UiVector2 p2, UiVector2 p3, UiVector2 p4, UiColor color, float thickness = 1f, int segments = 0)
    {
        var seg = segments > 0 ? segments : 12;
        AddPolyline(BuildCubicBezierScratch(p1, p2, p3, p4, seg), false, color, thickness);
    }

    public void AddBezierQuadratic(UiVector2 p1, UiVector2 p2, UiVector2 p3, UiColor color, float thickness = 1f, int segments = 0)
    {
        var seg = segments > 0 ? segments : 12;
        AddPolyline(BuildQuadraticBezierScratch(p1, p2, p3, seg), false, color, thickness);
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

        AddCommand(new UiDrawCommand(_currentClipRect, _currentTexture, startIndex, (uint)idxCount, 0, Bounds: CreatePolylineBounds(points, halfThick), HasBounds: true));
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

        AddCommand(new UiDrawCommand(_currentClipRect, _currentTexture, startIndex, (uint)((points.Length - 2) * 3), 0, Bounds: CreatePointsBounds(points), HasBounds: true));
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

        AddCommand(new UiDrawCommand(_currentClipRect, _currentTexture, startIndex, (uint)indices.Count, 0, Bounds: CreatePointsBounds(points), HasBounds: true));
    }

    public void AddImage(UiTextureId textureId, UiVector2 pMin, UiVector2 pMax, UiVector2 uvMin, UiVector2 uvMax, UiColor color)
    {
        AddImageQuad(textureId, pMin, new UiVector2(pMax.X, pMin.Y), pMax, new UiVector2(pMin.X, pMax.Y), uvMin, new UiVector2(uvMax.X, uvMin.Y), uvMax, new UiVector2(uvMin.X, uvMax.Y), color, _currentClipRect);
    }

    public void AddImage(UiTextureId textureId, UiVector2 pMin, UiVector2 pMax, UiVector2 uvMin, UiVector2 uvMax, UiColor color, UiRect clipRect)
    {
        AddImageQuad(textureId, pMin, new UiVector2(pMax.X, pMin.Y), pMax, new UiVector2(pMin.X, pMax.Y), uvMin, new UiVector2(uvMax.X, uvMin.Y), uvMax, new UiVector2(uvMin.X, uvMax.Y), color, Intersect(clipRect, _currentClipRect));
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
        AddImageQuad(textureId, p1, p2, p3, p4, uv1, uv2, uv3, uv4, color, _currentClipRect);
    }

    private void AddImageQuad(
        UiTextureId textureId,
        UiVector2 p1,
        UiVector2 p2,
        UiVector2 p3,
        UiVector2 p4,
        UiVector2 uv1,
        UiVector2 uv2,
        UiVector2 uv3,
        UiVector2 uv4,
        UiColor color,
        UiRect clipRect)
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

        AddCommand(new UiDrawCommand(clipRect, textureId, startIndex, 6, 0, Bounds: CreateQuadBounds(p1, p2, p3, p4), HasBounds: true));
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
            UiPooledList<UiDrawCommand>.RentAndCopy(_commands.AsSpan()),
            RectFilledPrimitives: UiPooledList<UiRectFilledPrimitive>.RentAndCopy(_rectFilledPrimitives.AsSpan()),
            CircleFilledPrimitives: UiPooledList<UiCircleFilledPrimitive>.RentAndCopy(_circleFilledPrimitives.AsSpan()),
            CommandScheduleStamp: UiDrawList.CommandScheduleStampEnabled ? UiDrawList.ComputeCommandScheduleStamp(_commands.AsSpan()) : 0UL
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

    public void _SetClipRect(UiRect clipRect)
    {
        _clipRect = clipRect;
        _currentClipRect = clipRect;
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
                channel.RectFilledPrimitives.Clear();
                channel.CircleFilledPrimitives.Clear();
                channel.DrawLists.Clear();
            }
        }

        _vertices.Clear();
        _indices.Clear();
        _commands.Clear();
        _rectFilledPrimitives.Clear();
        _circleFilledPrimitives.Clear();
        _drawLists.Clear();
        _path.Clear();
        _clipStack.Clear();
        _textureStack.Clear();
        _commandUserDataStack.Clear();
        _currentClipRect = _clipRect;
        _currentTexture = default;
        _currentCommandUserData = null;
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
                channel.RectFilledPrimitives.Clear();
                channel.CircleFilledPrimitives.Clear();
                channel.DrawLists.Clear();
                channel.Vertices.TrimExcess();
                channel.Indices.TrimExcess();
                channel.Commands.TrimExcess();
                channel.RectFilledPrimitives.TrimExcess();
                channel.CircleFilledPrimitives.TrimExcess();
                channel.DrawLists.TrimExcess();
            }
        }

        _vertices.Clear();
        _indices.Clear();
        _commands.Clear();
        _rectFilledPrimitives.Clear();
        _circleFilledPrimitives.Clear();
        _drawLists.Clear();
        _vertices.TrimExcess();
        _indices.TrimExcess();
        _commands.TrimExcess();
        _rectFilledPrimitives.TrimExcess();
        _circleFilledPrimitives.TrimExcess();
        _drawLists.TrimExcess();
        foreach (var channel in _channelPool)
        {
            channel.Vertices.Clear();
            channel.Indices.Clear();
            channel.Commands.Clear();
            channel.RectFilledPrimitives.Clear();
            channel.CircleFilledPrimitives.Clear();
            channel.DrawLists.Clear();
            channel.Vertices.TrimExcess();
            channel.Indices.TrimExcess();
            channel.Commands.TrimExcess();
            channel.RectFilledPrimitives.TrimExcess();
            channel.CircleFilledPrimitives.TrimExcess();
            channel.DrawLists.TrimExcess();
        }
        _commandUserDataStack.Clear();
        _currentCommandUserData = null;
    }

    public void _PopUnusedDrawCmd()
    {
        PopUnusedDrawCmd(_commands);
    }

    private static void PopUnusedDrawCmd(PooledBuffer<UiDrawCommand> commands)
    {
        if (commands.Count == 0)
        {
            return;
        }

        if (commands[^1].ElementCount == 0)
        {
            commands.RemoveAt(commands.Count - 1);
        }
    }

    public void _TryMergeDrawCmds()
    {
        TryMergeDrawCmds(_commands);
    }

    private static void TryMergeDrawCmds(PooledBuffer<UiDrawCommand> commands)
    {
        if (commands.Count <= 1)
        {
            return;
        }

        for (var i = commands.Count - 2; i >= 0; i--)
        {
            var a = commands[i];
            var b = commands[i + 1];
            if (a.TextureId == b.TextureId
                && a.ClipRect == b.ClipRect
                && a.Translation == b.Translation
                && a.Kind == b.Kind
                && a.VertexOffset == b.VertexOffset
                && a.IndexOffset + a.ElementCount == b.IndexOffset
                && MathF.Abs(a.Opacity - b.Opacity) <= 0.000001f
                && a.Callback == b.Callback
                && Equals(a.UserData, b.UserData))
            {
                commands[i] = a with
                {
                    ElementCount = a.ElementCount + b.ElementCount,
                    Bounds = a.HasBounds && b.HasBounds ? Union(a.Bounds, b.Bounds) : default,
                    HasBounds = a.HasBounds && b.HasBounds
                };
                commands.RemoveAt(i + 1);
            }
        }
    }

    private void _TryScheduleDrawCmds()
    {
        TryScheduleDrawCmds(_commands);
    }

    private void TryScheduleDrawCmds(PooledBuffer<UiDrawCommand> commandBuffer)
    {
        if (!BuilderCommandSchedulerEnabled || commandBuffer.Count < 4)
        {
            return;
        }

        var commands = commandBuffer.AsSpan();
        var sourceStamp = UiDrawList.ComputeCommandScheduleStamp(commands);
        var canCacheSchedule = CanCacheCommandSchedule(commands);
        if (canCacheSchedule
            && BuilderCommandScheduleCache.TryGetValue(sourceStamp, out var cached)
            && cached.CommandCount == commandBuffer.Count)
        {
            commandBuffer.SetCount(cached.CommandCount);
            cached.Commands.AsSpan(0, cached.CommandCount).CopyTo(commandBuffer.AsSpan());
            return;
        }

        var commandCount = commands.Length;
        var start = 0;
        while (start < commandCount)
        {
            while (start < commandCount && !CanScheduleDrawCommand(in commands[start]))
            {
                start++;
            }

            if (start >= commandCount)
            {
                break;
            }

            var end = start + 1;
            while (end < commandCount && CanScheduleDrawCommand(in commands[end]))
            {
                end++;
            }

            while (start < end)
            {
                var length = Math.Min(CommandSchedulerMaxWindow, end - start);
                if (length >= 4)
                {
                    ScheduleDrawCommandWindow(commands.Slice(start, length));
                }

                start += length;
            }
        }

        TryMergeDrawCmds(commandBuffer);

        if (canCacheSchedule)
        {
            if (BuilderCommandScheduleCache.Count >= MaxBuilderCommandScheduleCacheEntries)
            {
                BuilderCommandScheduleCache.Clear();
            }

            var scheduledCommands = commandBuffer.AsSpan().ToArray();
            BuilderCommandScheduleCache[sourceStamp] = new BuilderCommandScheduleCacheEntry(commandBuffer.Count, scheduledCommands);
        }
    }

    private static bool CanCacheCommandSchedule(ReadOnlySpan<UiDrawCommand> commands)
    {
        for (var i = 0; i < commands.Length; i++)
        {
            ref readonly var command = ref commands[i];
            if (command.Callback is not null || command.UserData is not null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanScheduleDrawCommand(in UiDrawCommand command)
    {
        return command.ElementCount > 0
            && command.Callback is null
            && command.UserData is null
            && command.HasBounds
            && command.Bounds.Width > 0f
            && command.Bounds.Height > 0f;
    }

    private void ScheduleDrawCommandWindow(Span<UiDrawCommand> commands)
    {
        var length = commands.Length;
        var indegrees = ArrayPool<int>.Shared.Rent(length);
        var selected = ArrayPool<int>.Shared.Rent(length);
        var order = ArrayPool<int>.Shared.Rent(length);
        var classes = ArrayPool<BuilderCommandSchedulingClass>.Shared.Rent(length);
        var bounds = ArrayPool<UiRect>.Shared.Rent(length);

        try
        {
            for (var i = 0; i < length; i++)
            {
                indegrees[i] = 0;
                selected[i] = 0;
                order[i] = i;
                classes[i] = GetBuilderSchedulingClass(in commands[i]);
                bounds[i] = GetEffectiveSchedulingBounds(in commands[i]);
            }

            for (var i = 0; i < length - 1; i++)
            {
                var a = bounds[i];
                for (var j = i + 1; j < length; j++)
                {
                    if (Overlaps(a, bounds[j]))
                    {
                        indegrees[j]++;
                    }
                }
            }

            var changed = false;
            var hasLastClass = false;
            var lastClass = default(BuilderCommandSchedulingClass);
            for (var output = 0; output < length; output++)
            {
                var pick = PickNextScheduledDrawCommand(indegrees, selected, classes, length, hasLastClass, lastClass);
                if (pick < 0)
                {
                    return;
                }

                selected[pick] = 1;
                order[output] = pick;
                if (pick != output)
                {
                    changed = true;
                }

                hasLastClass = true;
                lastClass = classes[pick];

                var pickedBounds = bounds[pick];
                for (var j = pick + 1; j < length; j++)
                {
                    if (selected[j] == 0 && Overlaps(pickedBounds, bounds[j]))
                    {
                        indegrees[j]--;
                    }
                }
            }

            if (changed)
            {
                ApplyScheduledDrawCommandOrder(commands, order);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(indegrees, clearArray: false);
            ArrayPool<int>.Shared.Return(selected, clearArray: false);
            ArrayPool<int>.Shared.Return(order, clearArray: false);
            ArrayPool<BuilderCommandSchedulingClass>.Shared.Return(classes, clearArray: false);
            ArrayPool<UiRect>.Shared.Return(bounds, clearArray: false);
        }
    }

    private BuilderCommandSchedulingClass GetBuilderSchedulingClass(in UiDrawCommand command)
    {
        if (command.Kind is not UiDrawCommandKind.Triangles)
        {
            return BuilderCommandSchedulingClass.SolidPrimitive;
        }

        if (_sharedData is not null)
        {
            if (command.TextureId == _sharedData.WhiteTexture)
            {
                return BuilderCommandSchedulingClass.SolidTriangle;
            }

            if (command.TextureId == _sharedData.FontTexture)
            {
                return BuilderCommandSchedulingClass.FontTriangle;
            }
        }

        return BuilderCommandSchedulingClass.TexturedTriangle;
    }

    private static int PickNextScheduledDrawCommand(
        int[] indegrees,
        int[] selected,
        BuilderCommandSchedulingClass[] classes,
        int length,
        bool hasLastClass,
        BuilderCommandSchedulingClass lastClass)
    {
        if (hasLastClass)
        {
            for (var i = 0; i < length; i++)
            {
                if (selected[i] == 0 && indegrees[i] == 0 && classes[i] == lastClass)
                {
                    return i;
                }
            }
        }

        var pick = -1;
        var bestPriority = int.MaxValue;
        for (var i = 0; i < length; i++)
        {
            if (selected[i] != 0 || indegrees[i] != 0)
            {
                continue;
            }

            var priority = (int)classes[i];
            if (priority < bestPriority)
            {
                bestPriority = priority;
                pick = i;
            }
        }

        return pick;
    }

    private static void ApplyScheduledDrawCommandOrder(Span<UiDrawCommand> commands, int[] order)
    {
        var temp = ArrayPool<UiDrawCommand>.Shared.Rent(commands.Length);
        try
        {
            for (var i = 0; i < commands.Length; i++)
            {
                temp[i] = commands[order[i]];
            }

            for (var i = 0; i < commands.Length; i++)
            {
                commands[i] = temp[i];
            }
        }
        finally
        {
            ArrayPool<UiDrawCommand>.Shared.Return(temp, clearArray: true);
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
        _channels.Add(new Channel(_vertices, _indices, _commands, _rectFilledPrimitives, _circleFilledPrimitives, _drawLists));
        for (var i = 1; i < count; i++)
        {
            _channels.Add(GetOrCreateAuxChannel(i - 1));
        }
        _currentChannel = 0;
    }

    private Channel GetOrCreateAuxChannel(int index)
    {
        while (_channelPool.Count <= index)
        {
            _channelPool.Add(new Channel(
                new PooledBuffer<UiDrawVertex>(),
                new PooledBuffer<uint>(),
                new PooledBuffer<UiDrawCommand>(),
                new PooledBuffer<UiRectFilledPrimitive>(),
                new PooledBuffer<UiCircleFilledPrimitive>(),
                new List<UiDrawList>()));
        }

        var channel = _channelPool[index];
        channel.Vertices.Clear();
        channel.Indices.Clear();
        channel.Commands.Clear();
        channel.RectFilledPrimitives.Clear();
        channel.CircleFilledPrimitives.Clear();
        channel.DrawLists.Clear();
        return channel;
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
                list.ReleasePooled();
            }

            channel.DrawLists.Clear();
        }

        _channels = null;
        _currentChannel = 0;
    }

    public void MergeChannelsAsDrawLists()
    {
        if (_channels is null)
        {
            return;
        }

        SetCurrentChannel(0);
        var targetDrawLists = _channels[0].DrawLists;
        for (var i = 0; i < _channels.Count; i++)
        {
            var channel = _channels[i];
            FlushChannel(channel);
            if (i == 0)
            {
                continue;
            }

            foreach (var list in channel.DrawLists)
            {
                targetDrawLists.Add(list);
            }

            channel.DrawLists.Clear();
        }

        _drawLists = targetDrawLists;
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
        _rectFilledPrimitives = target.RectFilledPrimitives;
        _circleFilledPrimitives = target.CircleFilledPrimitives;
        _drawLists = target.DrawLists;
        _currentChannel = channelIndex;
    }

    public void AddDrawList(UiDrawList list)
    {
        if (list.Commands.Count == 0 || (list.Vertices.Count == 0 && list.Indices.Count == 0 && (list.RectFilledPrimitives?.Count ?? 0) == 0 && (list.CircleFilledPrimitives?.Count ?? 0) == 0))
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
        var rectOffset = (uint)_rectFilledPrimitives.Count;
        var circleOffset = (uint)_circleFilledPrimitives.Count;

        _vertices.AddRange(list.Vertices);
        foreach (var index in list.Indices)
        {
            _indices.Add(index + vertexOffset);
        }

        if (list.CircleFilledPrimitives is not null)
        {
            _circleFilledPrimitives.AddRange(list.CircleFilledPrimitives);
        }

        if (list.RectFilledPrimitives is not null)
        {
            _rectFilledPrimitives.AddRange(list.RectFilledPrimitives);
        }

        foreach (var cmd in list.Commands)
        {
            var offset = cmd.Kind switch
            {
                UiDrawCommandKind.RectFilledPrimitives => cmd.IndexOffset + rectOffset,
                UiDrawCommandKind.CircleFilledPrimitives => cmd.IndexOffset + circleOffset,
                _ => cmd.IndexOffset + indexOffset,
            };
            AddCommand(cmd with
            {
                IndexOffset = offset,
                VertexOffset = cmd.Kind == UiDrawCommandKind.CircleFilledPrimitives ? cmd.VertexOffset : 0
            });
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

            AddCommand(cmd with
            {
                IndexOffset = cmd.IndexOffset + indexOffset,
                ElementCount = (uint)indexCount,
                VertexOffset = 0
            });
            quadIndex += quadCount;
        }
    }

    private void AddCommand(UiDrawCommand cmd)
    {
        if (cmd.UserData is null && _currentCommandUserData is not null)
        {
            cmd = cmd with { UserData = _currentCommandUserData };
        }

        if (cmd.ElementCount > 0
            && cmd.Callback is null
            && _commands.Count > 0
            && _commands[^1] is { ElementCount: 0, Callback: null })
        {
            _commands.RemoveAt(_commands.Count - 1);
        }

        if (_commands.Count > 0)
        {
            var last = _commands[^1];
            var contiguous = last.IndexOffset + last.ElementCount == cmd.IndexOffset;
            if (contiguous
                && last.TextureId == cmd.TextureId
                && last.ClipRect == cmd.ClipRect
                && last.Translation == cmd.Translation
                && last.Kind == cmd.Kind
                && last.VertexOffset == cmd.VertexOffset
                && MathF.Abs(last.Opacity - cmd.Opacity) <= 0.000001f
                && last.Callback == cmd.Callback
                && Equals(last.UserData, cmd.UserData))
            {
                _commands[^1] = last with
                {
                    ElementCount = last.ElementCount + cmd.ElementCount,
                    Bounds = last.HasBounds && cmd.HasBounds ? Union(last.Bounds, cmd.Bounds) : default,
                    HasBounds = last.HasBounds && cmd.HasBounds
                };
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
        if (drawList.Commands.Count == 0 || (drawList.Vertices.Count == 0 && drawList.Indices.Count == 0 && (drawList.RectFilledPrimitives?.Count ?? 0) == 0 && (drawList.CircleFilledPrimitives?.Count ?? 0) == 0))
        {
            return;
        }

        var opacityFactor = Math.Clamp(opacity, 0f, 1f);
        var applyOpacity = opacityFactor < 0.999f;
        var applyTranslation = MathF.Abs(translation.X) > 0.0001f || MathF.Abs(translation.Y) > 0.0001f;

        if (!applyOpacity && !applyTranslation && drawList.RectFilledPrimitives is null && drawList.CircleFilledPrimitives is null)
        {
            Append(drawList.Vertices.AsSpan(), drawList.Indices.AsSpan(), drawList.Commands.AsSpan());
            return;
        }

        EnsureCapacityFor(drawList.Vertices.Count, drawList.Indices.Count);

        var vertexOffset = (uint)_vertices.Count;
        var indexOffset = (uint)_indices.Count;
        var rectOffset = (uint)_rectFilledPrimitives.Count;
        var circleOffset = (uint)_circleFilledPrimitives.Count;

        _vertices.AddRange(drawList.Vertices);

        for (var i = 0; i < drawList.Indices.Count; i++)
        {
            _indices.Add(drawList.Indices[i] + vertexOffset);
        }

        if (drawList.CircleFilledPrimitives is not null)
        {
            _circleFilledPrimitives.AddRange(drawList.CircleFilledPrimitives);
        }

        if (drawList.RectFilledPrimitives is not null)
        {
            _rectFilledPrimitives.AddRange(drawList.RectFilledPrimitives);
        }

        for (var i = 0; i < drawList.Commands.Count; i++)
        {
            var command = drawList.Commands[i];
            if (applyTranslation)
            {
                command = command with
                {
                    Translation = new UiVector2(command.Translation.X + translation.X, command.Translation.Y + translation.Y)
                };
            }

            if (applyOpacity)
            {
                command = command with { Opacity = command.Opacity * opacityFactor };
            }

            var offset = command.Kind switch
            {
                UiDrawCommandKind.RectFilledPrimitives => command.IndexOffset + rectOffset,
                UiDrawCommandKind.CircleFilledPrimitives => command.IndexOffset + circleOffset,
                _ => command.IndexOffset + indexOffset,
            };
            AddCommand(command with { IndexOffset = offset });
        }
    }

    public void AppendStaticReference(UiDrawList drawList)
    {
        if (string.IsNullOrWhiteSpace(drawList.StaticGeometryKey))
        {
            throw new InvalidOperationException("Static reference draw lists require a static geometry key.");
        }

        if (drawList.Commands.Count == 0 || (drawList.Vertices.Count == 0 && drawList.Indices.Count == 0 && (drawList.RectFilledPrimitives?.Count ?? 0) == 0 && (drawList.CircleFilledPrimitives?.Count ?? 0) == 0))
        {
            return;
        }

        Flush();
        _drawLists.Add(new UiDrawList(
            UiPooledList<UiDrawVertex>.RentAndCopy(drawList.Vertices.AsSpan()),
            UiPooledList<uint>.RentAndCopy(drawList.Indices.AsSpan()),
            UiPooledList<UiDrawCommand>.RentAndCopy(drawList.Commands.AsSpan()),
            StaticGeometryKey: drawList.StaticGeometryKey,
            RectFilledPrimitives: drawList.RectFilledPrimitives is null
                ? null
                : UiPooledList<UiRectFilledPrimitive>.RentAndCopy(drawList.RectFilledPrimitives.AsSpan()),
            CircleFilledPrimitives: drawList.CircleFilledPrimitives is null
                ? null
                : UiPooledList<UiCircleFilledPrimitive>.RentAndCopy(drawList.CircleFilledPrimitives.AsSpan()),
            StaticGeometryStamp: drawList.StaticGeometryStamp,
            CommandScheduleStamp: drawList.CommandScheduleStamp));
    }

    public void AppendRetainedStaticReference(UiDrawList drawList)
    {
        if (string.IsNullOrWhiteSpace(drawList.StaticGeometryKey))
        {
            throw new InvalidOperationException("Retained static reference draw lists require a static geometry key.");
        }

        if (drawList.Commands.Count == 0 || (drawList.Vertices.Count == 0 && drawList.Indices.Count == 0 && (drawList.RectFilledPrimitives?.Count ?? 0) == 0 && (drawList.CircleFilledPrimitives?.Count ?? 0) == 0))
        {
            return;
        }

        Flush();
        if (!drawList.OwnsBuffers)
        {
            _drawLists.Add(drawList);
            return;
        }

        _drawLists.Add(new UiDrawList(
            drawList.Vertices,
            drawList.Indices,
            drawList.Commands,
            OwnsBuffers: false,
            StaticGeometryKey: drawList.StaticGeometryKey,
            RectFilledPrimitives: drawList.RectFilledPrimitives,
            CircleFilledPrimitives: drawList.CircleFilledPrimitives,
            StaticGeometryStamp: drawList.StaticGeometryStamp,
            CommandScheduleStamp: drawList.CommandScheduleStamp));
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

    private void PrepareDrawCommandsForFlush(PooledBuffer<UiDrawCommand> commands)
    {
        PopUnusedDrawCmd(commands);
        TryMergeDrawCmds(commands);
        TryScheduleDrawCmds(commands);
    }

    private void Flush()
    {
        if (_vertices.Count == 0 && _indices.Count == 0 && _rectFilledPrimitives.Count == 0 && _circleFilledPrimitives.Count == 0)
        {
            return;
        }

        PrepareDrawCommandsForFlush(_commands);
        var commandScheduleStamp = UiDrawList.CommandScheduleStampEnabled ? UiDrawList.ComputeCommandScheduleStamp(_commands.AsSpan()) : 0UL;

        _drawLists.Add(
            new UiDrawList(
                _vertices.TransferToPooledList(),
                _indices.TransferToPooledList(),
                _commands.TransferToPooledList(),
                RectFilledPrimitives: _rectFilledPrimitives.TransferToPooledList(),
                CircleFilledPrimitives: _circleFilledPrimitives.TransferToPooledList(),
                CommandScheduleStamp: commandScheduleStamp
            )
        );
    }

    private void FlushChannel(Channel channel)
    {
        if (channel.Vertices.Count == 0 && channel.Indices.Count == 0 && channel.RectFilledPrimitives.Count == 0 && channel.CircleFilledPrimitives.Count == 0)
        {
            return;
        }

        PrepareDrawCommandsForFlush(channel.Commands);
        var commandScheduleStamp = UiDrawList.CommandScheduleStampEnabled ? UiDrawList.ComputeCommandScheduleStamp(channel.Commands.AsSpan()) : 0UL;

        channel.DrawLists.Add(
            new UiDrawList(
                channel.Vertices.TransferToPooledList(),
                channel.Indices.TransferToPooledList(),
                channel.Commands.TransferToPooledList(),
                RectFilledPrimitives: channel.RectFilledPrimitives.TransferToPooledList(),
                CircleFilledPrimitives: channel.CircleFilledPrimitives.TransferToPooledList(),
                CommandScheduleStamp: commandScheduleStamp
            )
        );
    }

    private ReadOnlySpan<UiVector2> BuildEllipseScratch(UiVector2 center, UiVector2 radius, int segments)
    {
        _scratchPath.Clear();
        if (_scratchPath.Capacity < segments)
        {
            _scratchPath.Capacity = segments;
        }

        var step = MathF.Tau / segments;
        for (var i = 0; i < segments; i++)
        {
            var a = step * i;
            _scratchPath.Add(new UiVector2(center.X + MathF.Cos(a) * radius.X, center.Y + MathF.Sin(a) * radius.Y));
        }

        return CollectionsMarshal.AsSpan(_scratchPath);
    }

    private ReadOnlySpan<UiVector2> BuildCubicBezierScratch(UiVector2 p1, UiVector2 p2, UiVector2 p3, UiVector2 p4, int segments)
    {
        var count = segments + 1;
        _scratchPath.Clear();
        if (_scratchPath.Capacity < count)
        {
            _scratchPath.Capacity = count;
        }

        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            _scratchPath.Add(CubicBezier(p1, p2, p3, p4, t));
        }

        return CollectionsMarshal.AsSpan(_scratchPath);
    }

    private ReadOnlySpan<UiVector2> BuildQuadraticBezierScratch(UiVector2 p1, UiVector2 p2, UiVector2 p3, int segments)
    {
        var count = segments + 1;
        _scratchPath.Clear();
        if (_scratchPath.Capacity < count)
        {
            _scratchPath.Capacity = count;
        }

        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            _scratchPath.Add(QuadraticBezier(p1, p2, p3, t));
        }

        return CollectionsMarshal.AsSpan(_scratchPath);
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

    private static UiRect Union(UiRect a, UiRect b)
    {
        var x1 = MathF.Min(a.X, b.X);
        var y1 = MathF.Min(a.Y, b.Y);
        var x2 = MathF.Max(a.X + a.Width, b.X + b.Width);
        var y2 = MathF.Max(a.Y + a.Height, b.Y + b.Height);
        return new UiRect(x1, y1, x2 - x1, y2 - y1);
    }

    private static UiRect CreateCircleBounds(UiVector2 center, float radius)
    {
        return new UiRect(center.X - radius, center.Y - radius, radius * 2f, radius * 2f);
    }

    private static UiRect CreateTriangleBounds(UiVector2 a, UiVector2 b, UiVector2 c)
    {
        var minX = MathF.Min(a.X, MathF.Min(b.X, c.X));
        var minY = MathF.Min(a.Y, MathF.Min(b.Y, c.Y));
        var maxX = MathF.Max(a.X, MathF.Max(b.X, c.X));
        var maxY = MathF.Max(a.Y, MathF.Max(b.Y, c.Y));
        return new UiRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static UiRect CreateQuadBounds(UiVector2 a, UiVector2 b, UiVector2 c, UiVector2 d)
    {
        var minX = MathF.Min(MathF.Min(a.X, b.X), MathF.Min(c.X, d.X));
        var minY = MathF.Min(MathF.Min(a.Y, b.Y), MathF.Min(c.Y, d.Y));
        var maxX = MathF.Max(MathF.Max(a.X, b.X), MathF.Max(c.X, d.X));
        var maxY = MathF.Max(MathF.Max(a.Y, b.Y), MathF.Max(c.Y, d.Y));
        return new UiRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static UiRect CreatePointsBounds(ReadOnlySpan<UiVector2> points)
    {
        if (points.Length == 0)
        {
            return default;
        }

        var minX = points[0].X;
        var minY = points[0].Y;
        var maxX = minX;
        var maxY = minY;
        for (var i = 1; i < points.Length; i++)
        {
            var point = points[i];
            minX = MathF.Min(minX, point.X);
            minY = MathF.Min(minY, point.Y);
            maxX = MathF.Max(maxX, point.X);
            maxY = MathF.Max(maxY, point.Y);
        }

        return new UiRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static UiRect CreatePolylineBounds(ReadOnlySpan<UiVector2> points, float padding)
    {
        var bounds = CreatePointsBounds(points);
        return new UiRect(bounds.X - padding, bounds.Y - padding, bounds.Width + padding * 2f, bounds.Height + padding * 2f);
    }

    private static UiRect GetEffectiveSchedulingBounds(in UiDrawCommand command)
    {
        var bounds = command.Bounds;
        if (command.Translation == default)
        {
            return Intersect(bounds, command.ClipRect);
        }

        var translatedBounds = new UiRect(
            bounds.X + command.Translation.X,
            bounds.Y + command.Translation.Y,
            bounds.Width,
            bounds.Height);
        var translatedClip = new UiRect(
            command.ClipRect.X + command.Translation.X,
            command.ClipRect.Y + command.Translation.Y,
            command.ClipRect.Width,
            command.ClipRect.Height);
        return Intersect(translatedBounds, translatedClip);
    }

    private static bool Overlaps(UiRect a, UiRect b)
    {
        return a.Width > 0f
            && a.Height > 0f
            && b.Width > 0f
            && b.Height > 0f
            && a.X < b.X + b.Width
            && b.X < a.X + a.Width
            && a.Y < b.Y + b.Height
            && b.Y < a.Y + a.Height;
    }

    private static int ParsePositiveIntEnvironment(string name, int defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out var value) && value > 0
            ? value
            : defaultValue;
    }

    private static bool ParseBooleanEnvironmentFlag(string name)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
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
    private static float Snap(float value, bool enabled) => enabled ? MathF.Round(value) : value;
}

