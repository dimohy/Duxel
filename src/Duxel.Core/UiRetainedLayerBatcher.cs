using System;
using System.Collections.Generic;

namespace Duxel.Core;

/// <summary>
/// Combines adjacent resident lists sharing one 3D transform and depth scope.
/// Geometry is repacked only when source content changes, never for camera motion.
/// One synchronous consumer frame may borrow the result at a time.
/// </summary>
public sealed class UiRetainedLayerBatcher : IDisposable
{
    private const int MaxMembers = 512, MaxEntries = 32, MaxIdleFrames = 180;
    private const long MaxGroupBytes = 4 * 1024 * 1024;
    // Geometry equality does not imply equal texture, clip, opacity or offsets.
    // A promoted presentation list can keep its bytes while changing these commands.
    private readonly record struct Source(string Key, ulong Stamp, ulong Commands);
    private static Source Describe(UiDrawList list) => new(list.StaticGeometryKey!, list.GetStaticGeometryStamp(),
        list.CommandStateStamp != 0 ? list.CommandStateStamp : UiDrawList.ComputeCommandStateStamp(list.Commands.AsSpan()));
    private sealed class Entry(Source[] sources, UiDrawList[] owned, UiDrawList[] replay)
    {
        public readonly Source[] Sources = sources;
        public readonly UiDrawList[] Owned = owned;
        public readonly UiDrawList[] Replay = replay;
        public ulong LastFrame;
        public void Release() { foreach (var list in Owned) list.ReleasePooled(); }
    }
    private readonly Dictionary<(string Key, int Position), Entry> _entries = [];
    private readonly Dictionary<string, (ulong Stamp, ulong Seen, ulong ExcludeUntil)> _history = new(StringComparer.Ordinal);
    private readonly List<UiDrawList> _output = [];
    private readonly List<(string Key, int Position)> _expired = [];
    private readonly List<string> _expiredHistory = [];
    private readonly UiDrawListBuilder _builder = new(new(-16_777_216, -16_777_216, 33_554_432, 33_554_432),
        retainScratchCapacity: false, poolScratchBuffers: false);
    private ulong _frame;
    private bool _active, _disposed;

    public int BatchedSourceLists { get; private set; }
    public int RebuiltGroups { get; private set; }

    public readonly struct Frame : IDisposable
    {
        private readonly UiRetainedLayerBatcher _owner;
        private readonly ulong _generation;
        private readonly UiPooledList<UiDrawList>? _rental;
        public UiDrawData DrawData { get; }
        internal Frame(UiRetainedLayerBatcher owner, ulong generation, UiDrawData data, UiPooledList<UiDrawList>? rental)
            => (_owner, _generation, DrawData, _rental) = (owner, generation, data, rental);
        public void Dispose()
        {
            if (_owner is null || !_owner._active || _owner._frame != _generation) return;
            _rental?.Return();
            _owner._output.Clear();
            _owner._active = false;
        }
    }

    public Frame Prepare(UiDrawData data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_active) throw new InvalidOperationException("Release the previous layer batch frame before preparing another.");
        _active = true;
        _frame++;
        BatchedSourceLists = RebuiltGroups = 0;
        _output.Clear();
        Prune();
        try
        {
            for (var start = 0; start < data.DrawLists.Count;)
            {
                var first = data.DrawLists[start];
                var end = start + 1;
                if (end < data.DrawLists.Count && first.Scene3D.HasValue
                    && data.DrawLists[end].Scene3D == first.Scene3D && CanBatch(first))
                {
                    var bytes = GeometryBytes(first);
                    while (end < data.DrawLists.Count && end - start < MaxMembers)
                    {
                        var next = data.DrawLists[end];
                        if (next.Scene3D != first.Scene3D || bytes + GeometryBytes(next) > MaxGroupBytes || !CanBatch(next)) break;
                        bytes += GeometryBytes(next);
                        end++;
                    }
                }
                if (end - start < 2 || !TryAppendGroup(data.DrawLists, start, end))
                {
                    _output.Add(first);
                    start++;
                }
                else start = end;
            }
            if (BatchedSourceLists == 0) return new(this, _frame, data, null);
            var rental = UiPooledList<UiDrawList>.RentAndCopy(_output);
            return new(this, _frame, data with { DrawLists = rental }, rental);
        }
        catch { _active = false; _output.Clear(); throw; }
    }

    private bool CanBatch(UiDrawList list)
    {
        if (list.OwnsBuffers || list.StaticGeometryKey is not { Length: > 0 } key || !list.Scene3D.HasValue) return false;
        if ((list.GetCommandFeatures() & UiDrawCommandFeatures.Callbacks) != 0) return false;
        var stamp = list.GetStaticGeometryStamp();
        if (_history.TryGetValue(key, out var history))
        {
            var until = history.Stamp == stamp ? history.ExcludeUntil : _frame + 8;
            _history[key] = (stamp, _frame, until);
            return _frame > until;
        }
        // Bound metadata even if an application continuously invents layer IDs.
        if (_history.Count >= 32768) return false;
        _history.Add(key, (stamp, _frame, 0));
        return true;
    }

    private static long GeometryBytes(UiDrawList list)
        => (long)list.Vertices.Count * 20 + (long)list.Indices.Count * 4
            + ((long)(list.RectFilledPrimitives?.Count ?? 0) + (list.CircleFilledPrimitives?.Count ?? 0)) * 32;

    private bool TryAppendGroup(UiPooledList<UiDrawList> lists, int start, int end)
    {
        var key = (lists[start].StaticGeometryKey!, start);
        _entries.TryGetValue(key, out var entry);
        var matches = entry is not null && entry.Sources.Length == end - start;
        for (var i = start; matches && i < end; i++)
            matches = entry!.Sources[i - start] == Describe(lists[i]);
        if (!matches)
        {
            if (entry is null && _entries.Count >= MaxEntries)
            {
                (string Key, int Position)? oldestKey = null;
                var oldest = _frame;
                foreach (var pair in _entries)
                    if (pair.Value.LastFrame < oldest) { oldest = pair.Value.LastFrame; oldestKey = pair.Key; }
                if (oldestKey is null) return false; // All entries are borrowed by this frame.
                _entries[oldestKey.Value].Release();
                _entries.Remove(oldestKey.Value);
            }
            _builder._ResetForNewFrame();
            var vertices = 0; var indices = 0; var commands = 0; var rects = 0; var circles = 0;
            for (var i = start; i < end; i++)
            {
                var list = lists[i];
                vertices += list.Vertices.Count; indices += list.Indices.Count; commands += list.Commands.Count;
                rects += list.RectFilledPrimitives?.Count ?? 0; circles += list.CircleFilledPrimitives?.Count ?? 0;
            }
            _builder.ReserveRetainedGroup(vertices, indices, commands, rects, circles);
            var sources = new Source[end - start];
            for (var i = start; i < end; i++)
            {
                sources[i - start] = Describe(lists[i]);
                _builder.Append(lists[i] with { Scene3D = null });
            }
            var built = _builder.Build();
            var owned = new UiDrawList[built.Count];
            var replay = new UiDrawList[built.Count];
            for (var i = 0; i < built.Count; i++)
            {
                owned[i] = built[i];
                replay[i] = built[i] with
                {
                    OwnsBuffers = false,
                    StaticGeometryKey = "duxel.layer.static:\u001fbatch:" + key.Item1 + ":" + start + ":" + i,
                    StaticGeometryStamp = built[i].GetStaticGeometryStamp(),
                    CommandStateStamp = UiDrawList.ComputeCommandStateStamp(built[i].Commands.AsSpan()),
                };
            }
            built.Return();
            _builder._ClearFreeMemory();
            entry?.Release();
            entry = new(sources, owned, replay);
            _entries[key] = entry;
            RebuiltGroups++;
        }
        entry!.LastFrame = _frame;
        foreach (var list in entry.Replay)
        {
            // Replay wrappers belong only to this exclusive borrowed frame.
            // Source lists and owned geometry templates are never modified.
            list.SetLeasedScene3D(lists[start].Scene3D);
            _output.Add(list);
        }
        BatchedSourceLists += end - start;
        return true;
    }

    private void Prune()
    {
        _expired.Clear();
        foreach (var pair in _entries)
            if (_frame - pair.Value.LastFrame > MaxIdleFrames) _expired.Add(pair.Key);
        foreach (var key in _expired) { _entries[key].Release(); _entries.Remove(key); }
        if (_frame % 60 != 0) return;
        _expiredHistory.Clear();
        foreach (var pair in _history)
            if (_frame - pair.Value.Seen > MaxIdleFrames) _expiredHistory.Add(pair.Key);
        foreach (var key in _expiredHistory) _history.Remove(key);
    }

    public void Dispose()
    {
        if (_active) throw new InvalidOperationException("Release the borrowed layer batch frame before disposing its cache.");
        if (_disposed) return;
        foreach (var entry in _entries.Values) entry.Release();
        _entries.Clear(); _history.Clear(); _output.Clear();
        _builder._ResetForNewFrame();
        _builder._ClearFreeMemory();
        _disposed = true;
    }
}
