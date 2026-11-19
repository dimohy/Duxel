namespace Duxel.Core;

public sealed partial class UiDrawListBuilder
{
    private const int TextRunMaxCharacters = 256;
    private const int TextRunMaxEntries = 256;
    private const int TextRunMaxBytes = 262_144;
    private const int TextRunMaxIdleFrames = 120;
    private static readonly int TextRunVertexBytes = System.Runtime.CompilerServices.Unsafe.SizeOf<UiDrawVertex>();
    private readonly record struct TextRunKey(UiFontAtlas Font, string Text, UiVector2 Position,
        UiColor Color, UiTextureId Texture, UiRect Clip, UiTextSettings Settings, float? LineHeight);
    private struct CachedTextRun
    {
        internal UiDrawVertex[]? Vertices;
        internal uint[]? Indices;
        internal UiRect Bounds;
        internal int LastUsedFrame;
        internal int Bytes => (Vertices?.Length ?? 0) * TextRunVertexBytes + (Indices?.Length ?? 0) * sizeof(uint);
    }
    private Dictionary<TextRunKey, CachedTextRun>? _textRuns;
    private Queue<TextRunKey>? _textRunOrder;
    private int _textRunBytes;
    private int _textRunFrame;
    internal int TextRunReplayCount { get; private set; }
    internal (int Entries, int GeometryBytes) TextRunCacheState => (_textRuns?.Count ?? 0, _textRunBytes);

    private bool TryReplayTextRun(in TextRunKey key)
    {
        if (_textRuns is null) return false;
        ref var run = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrNullRef(_textRuns, key);
        if (System.Runtime.CompilerServices.Unsafe.IsNullRef(ref run)) return false;
        run.LastUsedFrame = _textRunFrame;
        if (run.Vertices is not { } vertices || run.Indices is not { } indices) return false;
        var vertexOffset = (uint)_vertices.Count;
        var indexOffset = (uint)_indices.Count;
        _vertices.SetCount(_vertices.Count + vertices.Length);
        vertices.AsSpan().CopyTo(_vertices.AsSpan().Slice((int)vertexOffset));
        _indices.SetCount(_indices.Count + indices.Length);
        var destination = _indices.AsSpan().Slice((int)indexOffset);
        for (var i = 0; i < indices.Length; i++) destination[i] = indices[i] + vertexOffset;
        if (indices.Length > 0)
            AddCommand(new(key.Clip, key.Texture, indexOffset, (uint)indices.Length, 0,
                Bounds: run.Bounds, HasBounds: true));
        TextRunReplayCount++;
        return true;
    }

    private void CaptureTextRun(in TextRunKey key, int vertexStart, int indexStart)
    {
        _textRuns ??= [];
        _textRunOrder ??= [];
        if (!_textRuns.TryGetValue(key, out var run))
        {
            while (_textRuns.Count >= TextRunMaxEntries) EvictOldestTextRun();
            _textRuns.Add(key, new() { LastUsedFrame = _textRunFrame });
            _textRunOrder.Enqueue(key);
            return; // Admit geometry only on a repeated request; changing counters stay metadata-only.
        }
        var vertexCount = _vertices.Count - vertexStart;
        var indexCount = _indices.Count - indexStart;
        var bytes = vertexCount * TextRunVertexBytes + indexCount * sizeof(uint);
        while (_textRunBytes + bytes > TextRunMaxBytes) EvictOldestTextRun();
        // Budget eviction can remove this probationary entry as well.
        if (!_textRuns.ContainsKey(key)) return;
        var vertices = _vertices.AsSpan().Slice(vertexStart, vertexCount).ToArray();
        var indices = _indices.AsSpan().Slice(indexStart, indexCount).ToArray();
        for (var i = 0; i < indices.Length; i++) indices[i] -= (uint)vertexStart;
        var bounds = default(UiRect);
        for (var i = 0; i < vertices.Length; i += 4)
        {
            var a = vertices[i].Position;
            var b = vertices[i + 2].Position;
            var glyphBounds = new UiRect(a.X, a.Y, b.X - a.X, b.Y - a.Y);
            bounds = i == 0 ? glyphBounds : Union(bounds, glyphBounds);
        }
        run.Vertices = vertices;
        run.Indices = indices;
        run.Bounds = bounds;
        _textRuns[key] = run;
        _textRunBytes += bytes;
    }

    private void EvictOldestTextRun()
    {
        var key = _textRunOrder!.Dequeue();
        if (_textRuns!.Remove(key, out var run)) _textRunBytes -= run.Bytes;
    }

    private void AdvanceTextRunCacheFrame()
    {
        _textRunFrame++;
        TextRunReplayCount = 0;
        if (_textRunFrame % 60 != 0 || _textRunOrder is null) return;
        var count = _textRunOrder.Count;
        for (var i = 0; i < count; i++)
        {
            var key = _textRunOrder.Dequeue();
            var run = _textRuns![key];
            if (unchecked(_textRunFrame - run.LastUsedFrame) >= TextRunMaxIdleFrames)
            {
                _textRuns.Remove(key);
                _textRunBytes -= run.Bytes;
            }
            else _textRunOrder.Enqueue(key);
        }
    }

    private void ClearTextRunCache()
    {
        _textRuns = null;
        _textRunOrder = null;
        _textRunBytes = 0;
        TextRunReplayCount = 0;
    }
}
