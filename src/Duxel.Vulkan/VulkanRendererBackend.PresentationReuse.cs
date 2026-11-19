using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    // Keep diagnostic A/B runs on the same executable and native input probe.
    private readonly bool _presentationReuseEnabled = Environment.GetEnvironmentVariable("DUXEL_VK_PRESENT_REUSE") != "0";
    private bool _damageSnapshotPrepared, _damageSnapshotUsable, _presentedDamageValid;
    private ulong _presentedDamageEpoch;
    private long _presentedInvalidationSequence;

    // Current snapshots borrow existing readers until an acquired-image commit.
    // A failed acquire or an accepted unchanged frame must release only new nodes.
    private readonly ref struct DamagePreparationScope(VulkanRendererBackend owner)
    {
        public void Dispose()
        {
            foreach (var command in owner._damageCurrent)
                if (command.Readers == 0)
                    owner._damageFree.Push(command);
            owner._damageCurrent.Clear();
            owner.TrimDamageSnapshotPool();
            owner._damageSnapshotPrepared = owner._damageSnapshotUsable = false;
        }
    }

    partial void OverrideValidationPresentationSequence(ref long? sequence);
    partial void SelectValidationPresentationReuse(ref bool allowed);

    private long? ReadPresentationInvalidationSequence()
    {
        var sequence = (_platform as IPlatformPresentationInvalidationBackend)?.PresentationInvalidationSequence;
        OverrideValidationPresentationSequence(ref sequence);
        return sequence;
    }

    private bool TryReusePresentation(UiDrawData data, long? invalidationSequence, long allocatedBefore)
    {
        if (!_presentationReuseEnabled || invalidationSequence is not { } sequence || !_presentedDamageValid || !_damageEnabled
            || _presentedInvalidationSequence != sequence || _platform.IsInteractingResize
            || HasPendingPresentationWork || data.TextureUpdates.Count != 0
            || _repaintHighlightActive || _repaintMsaaOutline.Width > 0 || HasDirtyPaintGroups())
            return false;
        var allowed = true;
        SelectValidationPresentationReuse(ref allowed);
        if (!allowed)
            return false;
        foreach (var outline in _repaintImageOutlines)
            if (outline.Width > 0)
                return false;
        // Scene-generated/depth work must retain its execution contract even when
        // its CPU command bytes are unchanged.
        foreach (var list in data.DrawLists)
            if (list.Scene3D is { } scene && !SupportsPartialDamage(scene, data.DisplayPos))
                return false;

        PrepareLogicalDamageSnapshot(data);
        if (!_damageSnapshotUsable || _damageEpoch != _presentedDamageEpoch
            || _damageCurrent.Count != _damageLatest.Count)
            return false;
        for (var i = 0; i < _damageCurrent.Count; i++)
            if (!EquivalentDamageSnapshot(_damageCurrent[i], _damageLatest[i]))
                return false;

        // No image or acquire semaphore has been taken. Acknowledge CPU work
        // without inventing a QueuePresent, GPU duration or repaired pixels.
        LastFrameMetrics = new(_profilingEnabled, 0, 0, 0, 0, 0, 0,
            _depthLayerAllocatedBytes + _pendingDepthTargetBytes, double.NaN)
        {
            PresentationReused = true,
            PaintGroupCacheHits = _paintGroupOrder.Count,
            DrawDataFrameId = data.DrawDataFrameId,
            PresentationSequence = _presentationSequence,
            PresentationTimestampTicks = _presentationTimestampTicks,
            FramebufferSize = new((int)_frameRenderExtent.Width, (int)_frameRenderExtent.Height),
            VSyncEnabled = _options.EnableVSync,
            MsaaSamples = (int)_msaaSampleCount,
            PresentationMode = LastFrameMetrics.PresentationMode,
            BatchedSourceLists = _retainedLayerBatcher.BatchedSourceLists,
            RebuiltBatchGroups = _retainedLayerBatcher.RebuiltGroups,
            RendererAllocatedBytes = _profilingEnabled ? GC.GetAllocatedBytesForCurrentThread() - allocatedBefore : null,
            RetiredSwapchains = _retiredPresentations.Count,
            PendingRenderTargets = _pendingRenderTargetRetirements,
            PendingRenderTargetBytes = _pendingRenderTargetBytes,
            MsaaAllocatedBytes = _msaaColorAllocationBytes,
            CompiledIndexCacheBytes = _compiledCacheBytes,
            CompiledIndexLimit = _compiledIndexLimit,
            AxisAlignedLayerCulling = _axisAlignedLayerCullingEnabled
        };
        return true;
    }

    private void CommitPresentationDamage(bool presented, long? invalidationSequence)
    {
        _presentedDamageValid = false;
        if (!presented || !_damageSnapshotUsable || invalidationSequence is not { } sequence)
            return;
        _presentedDamageValid = true;
        _presentedDamageEpoch = _damageEpoch;
        // Preserve the token sampled before rendering, so a concurrent native
        // invalidation cannot be consumed by this presentation accidentally.
        _presentedInvalidationSequence = sequence;
    }
}
