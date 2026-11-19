namespace Duxel.Core;

public enum UiPresentationMode
{
    Immediate, Mailbox, Fifo, FifoRelaxed, Other
}

// Counters describe the last submitted frame; GPU timestamps lag by frame slots.
public readonly record struct UiRendererFrameMetrics(
    bool ProfilingEnabled, ulong DynamicUploadBytes, ulong StagingUploadBytes,
    int DrawCalls, int ResidentHits, int ResidentCreates, int ResidentUpdates,
    ulong DepthAllocatedBytes, double GpuRenderMicroseconds)
{
    /// <summary>Framebuffer pixels repaired for scene content, excluding diagnostic cleanup.</summary>
    public UiRect RepaintBounds
    {
        get; init;
    }
    public bool RepaintHighlightActive
    {
        get; init;
    }
    public long RepaintHighlightDeadlineTicks
    {
        get; init;
    }
    /// <summary>Extra repair caused by the diagnostic outline; do not use for performance claims.</summary>
    public bool RepaintDiagnosticRedraw
    {
        get; init;
    }
    public int PaintGroupPasses
    {
        get; init;
    }
    public int PaintGroupDrawCalls
    {
        get; init;
    }
    public int PaintGroupCacheHits
    {
        get; init;
    }

    public PlatformSize FramebufferSize
    {
        get; init;
    }
    public bool VSyncEnabled
    {
        get; init;
    }
    public int MsaaSamples
    {
        get; init;
    }
    public UiPresentationMode PresentationMode
    {
        get; init;
    }
    public int GpuParticleCount
    {
        get; init;
    }
    public int GpuParticleDispatches
    {
        get; init;
    }
    public ulong GpuParticleInitialUploadBytes
    {
        get; init;
    }
    /// <summary>This draw-data frame was accepted because the last successfully presented scene is still valid and unchanged.</summary>
    public bool PresentationReused
    {
        get; init;
    }

    // Successful QueuePresent requests, not physical display refreshes or GPU completion.
    public ulong PresentationSequence
    {
        get; init;
    }
    /// <summary>Detailed CPU durations were collected for this frame; counters/GPU timing have separate lifetimes.</summary>
    public bool CpuTimingCollected
    {
        get; init;
    }
    public ulong DrawDataFrameId
    {
        get; init;
    }
    public long PresentationTimestampTicks
    {
        get; init;
    }
    public double PresentationIntervalSeconds
    {
        get; init;
    }
    public int BatchedSourceLists
    {
        get; init;
    }
    public int RebuiltBatchGroups
    {
        get; init;
    }
    public long? RendererAllocatedBytes
    {
        get; init;
    }
    public int LayerTransformPushes
    {
        get; init;
    }
    public int CulledLayerLists
    {
        get; init;
    }
    public int LayerBoundsCommandVisits
    {
        get; init;
    }
    public bool AxisAlignedLayerCulling
    {
        get; init;
    }
    public int CompiledLayerDraws
    {
        get; init;
    }
    public int SharedBasisLayerDraws
    {
        get; init;
    }
    public int SharedCameraLayerDraws
    {
        get; init;
    }
    public int CompiledLayerCommands
    {
        get; init;
    }
    public int CompiledDescriptorBuilds
    {
        get; init;
    }
    public int CompiledCommandVisits
    {
        get; init;
    }
    public ulong CompiledIndexUploadBytes
    {
        get; init;
    }
    public ulong LayerStateUploadBytes
    {
        get; init;
    }
    public ulong CompiledIndexCacheBytes
    {
        get; init;
    }
    /// <summary>Maximum index entries in one compiled group; the descriptor limit is independent.</summary>
    public int CompiledIndexLimit
    {
        get; init;
    }
    public int RetiredSwapchains
    {
        get; init;
    }
    public int PendingRenderTargets
    {
        get; init;
    }
    public ulong PendingRenderTargetBytes
    {
        get; init;
    }
    public ulong MsaaAllocatedBytes
    {
        get; init;
    }
}
