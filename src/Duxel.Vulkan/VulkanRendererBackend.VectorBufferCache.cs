namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private const nuint VectorWorkCacheByteLimit = 64 * 1024 * 1024;
    private const int VectorWorkCacheCountLimit = 256;
    private readonly record struct PendingVectorWork(VectorBuffer Buffer, int AvailableFrame);
    private readonly List<VectorBuffer> _availableVectorWork = [];
    private readonly Dictionary<int, List<PendingVectorWork>> _pendingVectorWork = [];
    private nuint _availableVectorWorkBytes, _peakVectorWorkCacheBytes;
    private long _vectorWorkAllocations, _vectorWorkReuses;
    private int _pendingVectorWorkCount;

    internal (long Allocations, long Reuses, nuint CachedBytes, nuint PeakCachedBytes, int CachedCount, int PendingCount) VectorWorkDiagnostics
        => (_vectorWorkAllocations, _vectorWorkReuses, _availableVectorWorkBytes, _peakVectorWorkCacheBytes,
            _availableVectorWork.Count, _pendingVectorWorkCount);

    private bool TryRentVectorWork(nuint bytes, bool upload, out VectorBuffer buffer)
    {
        var best = -1;
        for (var i = 0; i < _availableVectorWork.Count; i++)
        {
            var candidate = _availableVectorWork[i];
            if (candidate.Upload == upload && candidate.Bytes >= bytes
                && (best < 0 || candidate.Bytes < _availableVectorWork[best].Bytes))
                best = i;
        }
        if (best < 0)
        {
            buffer = default;
            return false;
        }
        buffer = _availableVectorWork[best];
        _availableVectorWork.RemoveAt(best);
        _availableVectorWorkBytes -= buffer.Bytes;
        _vectorWorkReuses++;
        return true;
    }

    private void QueueVectorWorkReuse(VectorBuffer buffer)
    {
        if (_frames.Length == 0)
        {
            DestroyBufferResource(new(buffer.Buffer, buffer.Memory));
            return;
        }
        var slot = _frameIndex % _frames.Length;
        if (!_pendingVectorWork.TryGetValue(slot, out var pending))
            _pendingVectorWork.Add(slot, pending = []);
        pending.Add(new(buffer, _frameIndex + _frames.Length));
        _pendingVectorWorkCount++;
    }

    // ApplyTextureUpdates precedes this slot's fence wait. The q+N deadline also
    // excludes resources retired during the current, not-yet-submitted frame.
    private void RecycleVectorWorkAfterFrameFence(int slot)
    {
        if (!_pendingVectorWork.TryGetValue(slot, out var pending))
            return;
        var write = 0;
        for (var i = 0; i < pending.Count; i++)
        {
            var entry = pending[i];
            if (_frameIndex >= entry.AvailableFrame)
            {
                CacheCompletedVectorWork(entry.Buffer);
                _pendingVectorWorkCount--;
            }
            else
                pending[write++] = entry;
        }
        if (write < pending.Count)
            pending.RemoveRange(write, pending.Count - write);
    }

    private void CacheCompletedVectorWork(VectorBuffer buffer)
    {
        if (_availableVectorWork.Count == VectorWorkCacheCountLimit
            || buffer.Bytes > VectorWorkCacheByteLimit - _availableVectorWorkBytes)
        {
            DestroyBufferResource(new(buffer.Buffer, buffer.Memory));
            return;
        }
        _availableVectorWork.Add(buffer);
        _availableVectorWorkBytes += buffer.Bytes;
        _peakVectorWorkCacheBytes = nuint.Max(_peakVectorWorkCacheBytes, _availableVectorWorkBytes);
    }

    // All frame fences were waited by FlushPendingBufferDestroysAll. Drain before
    // frame indices/slot counts can change during swapchain settings updates.
    private void RecycleAllVectorWorkAfterFrameFences()
    {
        foreach (var pending in _pendingVectorWork.Values)
        {
            foreach (var entry in pending)
                CacheCompletedVectorWork(entry.Buffer);
            pending.Clear();
        }
        _pendingVectorWorkCount = 0;
    }

    private void DestroyVectorWorkCache()
    {
        foreach (var pending in _pendingVectorWork.Values)
            foreach (var entry in pending)
                DestroyBufferResource(new(entry.Buffer.Buffer, entry.Buffer.Memory));
        _pendingVectorWork.Clear();
        _pendingVectorWorkCount = 0;
        foreach (var buffer in _availableVectorWork)
            DestroyBufferResource(new(buffer.Buffer, buffer.Memory));
        _availableVectorWork.Clear();
        _availableVectorWorkBytes = 0;
    }
}
