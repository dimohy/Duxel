using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend : IRendererBackend
{
    private readonly Vk _vk = Vk.GetApi();

    public VulkanRendererBackend(IPlatformBackend platform, VulkanRendererOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxFramesInFlight);
        _options = options;
        _fontTextureIdValue = options.FontTextureId.Value is 0 ? 1 : options.FontTextureId.Value;
        _whiteTextureIdValue = options.WhiteTextureId.Value is 0 ? 2 : options.WhiteTextureId.Value;
        _platform = platform;
        _surfaceSource = platform.VulkanSurface ?? throw new InvalidOperationException(
            "Platform backend does not provide a Vulkan surface."
        );
        _minImageCount = Math.Max(2, options.MinImageCount);
        _pipelineCachePath = string.Empty;

        try
        {
            options.DiagnosticsLog?.Invoke("VulkanInit[Begin]");
            CreateInstance();
            options.DiagnosticsLog?.Invoke("VulkanInit[Instance]");
            LoadInstanceExtensions();
            CreateSurface();
            options.DiagnosticsLog?.Invoke("VulkanInit[Surface]");
            PickPhysicalDevice();
            options.DiagnosticsLog?.Invoke("VulkanInit[PhysicalDevice]");
            ResolveDevicePolicySettings();
            ConfigureMsaaSampleCount();
            CreateDevice();
            options.DiagnosticsLog?.Invoke("VulkanInit[Device]");
            LoadDeviceExtensions();
            options.DiagnosticsLog?.Invoke("VulkanInit[DeviceEntryPoints]");
            CreateSwapchainResources();
            options.DiagnosticsLog?.Invoke("VulkanInit[Ready]");
        }
        catch
        {
            if (_swapchain.Handle is not 0)
            {
                DestroySwapchainDependentResources(final: true);
                DestroyDeviceResources();
            }
            DestroyNativeDeviceHandles();
            throw;
        }
    }

    private void ResolveDevicePolicySettings()
    {
        _staticPrimitiveTrianglesEnabled = ResolveStaticPrimitiveTrianglesEnabled(
            _staticPrimitiveTriangleMode,
            _devicePolicy);
        _resolvedStaticGeometryUpdateMode = ResolveStaticGeometryUpdateMode(_staticGeometryUpdateMode, _devicePolicy);
        _staticGeometryInPlaceUpdateEnabled = _resolvedStaticGeometryUpdateMode == VulkanStaticGeometryUpdateMode.InPlace;
        _staticGeometryRotatingUpdateEnabled = _resolvedStaticGeometryUpdateMode == VulkanStaticGeometryUpdateMode.Rotating;
        _gpuProfilingEnabled = _gpuProfilingRequested && _devicePolicy.SupportsGraphicsQueueTimestamps;
    }

    public void Dispose()
    {
        var log = _options.DiagnosticsLog;
        log?.Invoke("VulkanShutdown[Begin]");
        _retainedLayerBatcher.Dispose();
        SavePipelineCache();
        log?.Invoke("VulkanShutdown[PipelineCacheSaved]");
        DestroySwapchainDependentResources(final: true);
        log?.Invoke("VulkanShutdown[SwapchainReleased]");
        DestroyDeviceResources();
        log?.Invoke("VulkanShutdown[DeviceResourcesReleased]");

        DestroyNativeDeviceHandles(log);
        FlushFrameTrace();
    }

    private void DestroyNativeDeviceHandles(Action<string>? log = null)
    {
        if (_device.Handle is not 0)
        {
            _vk.DestroyDevice(_device, null);
            _device = default;
            log?.Invoke("VulkanShutdown[DeviceDestroyed]");
        }

        if (_surface.Handle is not 0)
        {
            _khrSurface.DestroySurface(_instance, _surface, null);
            _surface = default;
            log?.Invoke("VulkanShutdown[SurfaceDestroyed]");
        }

        if (_instance.Handle is not 0)
        {
            _vk.DestroyInstance(_instance, null);
            _instance = default;
            log?.Invoke("VulkanShutdown[InstanceDestroyed]");
        }
    }

    private void DestroyDeviceResources()
    {
        if (_device.Handle is not 0)
        {
            if (!TryWaitForDeviceIdle("DestroyDeviceResources", throwOnFailure: false))
            {
                return;
            }
        }

        FlushPendingTextureDestroysAll();
        FlushPendingBufferDestroysAll();
        DestroyGpuParticleResources();
        DestroyVectorScenes();
        DestroyProjectedGlyphBuffers();

        DestroyCompiledLayerGroups();
        DestroyGeometryBuffers();
        DestroyPaintGroups();
        DestroyTextureResources();
        DestroyStagingBuffer();
        DestroyPipelineCache();

        if (_descriptorPool.Handle is not 0)
        {
            _vk.DestroyDescriptorPool(_device, _descriptorPool, null);
            _descriptorPool = default;
            _bindlessTextureSet = default;
        }

        if (_descriptorSetLayout.Handle is not 0)
        {
            _vk.DestroyDescriptorSetLayout(_device, _descriptorSetLayout, null);
            _descriptorSetLayout = default;
        }

        if (_pipelineLayout.Handle is not 0)
        {
            _vk.DestroyPipelineLayout(_device, _pipelineLayout, null);
            _pipelineLayout = default;
        }

        if (_fontSampler.Handle is not 0)
        {
            _vk.DestroySampler(_device, _fontSampler, null);
            _fontSampler = default;
        }

        if (_imageSampler.Handle is not 0)
        {
            _vk.DestroySampler(_device, _imageSampler, null);
            _imageSampler = default;
        }
    }

    partial void ObserveValidationTextureUpdates(bool begin);

    public void RenderDrawData(UiDrawData drawData)
    {
        _ = TryRenderDrawData(drawData);
    }

    /// <summary>Accepts a presented frame or an unchanged valid native presentation. False requires retry;
    /// PresentationReused distinguishes accepted CPU work from a new QueuePresent.</summary>
    public bool TryRenderDrawData(UiDrawData drawData)
    {
        ObserveValidationFrameAttempt(drawData);
        _frameLayerTransformPushes = _frameCulledLayerLists = 0;
        var profileEnabled = _profilingEnabled;
        // CPU durations are consumed only by profile logs. Counters and GPU
        // timestamps remain enabled on every frame independently of this flag.
        _cpuTimingsThisFrame = profileEnabled && (!_cpuTimingSamplingEnabled || !string.IsNullOrWhiteSpace(_frameTracePath)
            || unchecked(_profilingFrameCounter + 1) % _profilingLogInterval == 0);
        var allocatedBefore = profileEnabled ? GC.GetAllocatedBytesForCurrentThread() : 0;

        if (profileEnabled)
        {
            ResetImageTransitionProfileCounters();
            ResetUploadProfileCounters();
            _profileResizeWaitTicks = _profileResizeCreateTicks = _profileResizeTargetsTicks = _profileResizeSyncTicks = 0;
            _profileDamageSnapshotTicks = _profileDamageRepairTicks = _profileTextureUpdateTicks = 0;
            _profileVectorWorkTicks = _profileVectorImageTicks = _profileVectorRegistrationTicks = 0;
            _profileVectorOutputCount = 0;
        }

        ObserveValidationTextureUpdates(true);
        var textureUpdateStart = BeginFrameProfileTiming(profileEnabled);
        ApplyTextureUpdates(drawData.TextureUpdates.AsSpan());
        _profileTextureUpdateTicks = EndFrameProfileTiming(profileEnabled, textureUpdateStart);
        ObserveValidationTextureUpdates(false);
        var targetStart = BeginFrameProfileTiming(profileEnabled);
        CollectCompletedRenderTargets();
        CollectPresentationFenceResources();

        if (!TryEnsureFrameTarget(drawData))
        {
            return false;
        }
        PrepareGpuParticleResources(drawData);

        _frameUsesDepthLayers = PrepareDepthLayers(drawData);
        using var layerBatchFrame = _retainedLayerBatcher.Prepare(drawData);
        drawData = layerBatchFrame.DrawData;
        var targetTicks = EndFrameProfileTiming(profileEnabled, targetStart);
        PlanPaintGroupsForFrame(drawData);
        using var damagePreparation = new DamagePreparationScope(this);
        var invalidationSequence = ReadPresentationInvalidationSequence();
        if (TryReusePresentation(drawData, invalidationSequence, allocatedBefore))
            return true;
        _presentedDamageValid = false;
        if (!TryBeginRenderFrame(out var frameContext))
        {
            return false;
        }

        var frame = frameContext.FrameSlot;
        var frameData = frameContext.FrameData;
        var frameProfile = CreateFrameProfileState(frameContext.GpuRenderUs);
        frameProfile.TargetTicks = targetTicks;
        frameProfile.BeginTicks = frameContext.BeginTicks;
        frameProfile.FrameFenceTicks = frameContext.FrameFenceTicks;
        frameProfile.AcquireTicks = frameContext.AcquireTicks;
        frameProfile.ImageFenceTicks = frameContext.ImageFenceTicks;
        PreparePaintGroupsForFrame();
        var geometryBuffers = PrepareFrameGeometryForRecording(
            drawData,
            frame,
            frameData,
            frameContext.ImageIndex,
            profileEnabled,
            out frameProfile.UploadTicks);

        var commandBuffer = RecordFrameCommandsForSubmission(
            drawData,
            frameContext.ImageIndex,
            frameData,
            in geometryBuffers,
            profileEnabled,
            ref frameProfile);

        var presented = CompleteRecordedFrame(frameContext, commandBuffer, ref frameProfile);
        CommitPresentationDamage(presented, invalidationSequence);
        TrimIdleStagingBuffer();
        var presentationInterval = 0.0;
        if (presented)
        {
            var timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_presentationTimestampTicks != 0)
                presentationInterval = (timestamp - _presentationTimestampTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
            _presentationTimestampTicks = timestamp;
            _presentationSequence++;
        }
        LastFrameMetrics = new(profileEnabled, _profileDynamicGeometryUploadBytes, _profileUploadStagingBytes,
            frameProfile.RecordStats.DrawCallCount + _framePaintGroupDrawCalls, _profileStaticGeometryHitCount,
            _profileStaticGeometryCreateCount + _profileStaticGeometryReplaceCount,
            _profileStaticGeometryUpdateCount + _profileStaticGeometryReuseCount,
            _depthLayerAllocatedBytes + _pendingDepthTargetBytes, frameProfile.GpuRenderUs)
        {
            PaintGroupPasses = _framePaintGroupPasses,
            RepaintBounds = _repaintContentBounds,
            RepaintHighlightActive = _repaintHighlightActive,
            RepaintHighlightDeadlineTicks = _repaintUntil,
            RepaintDiagnosticRedraw = _repaintDiagnosticRedraw,
            PaintGroupDrawCalls = _framePaintGroupDrawCalls,
            PaintGroupCacheHits = _framePaintGroupCacheHits,
            FramebufferSize = new((int)_frameRenderExtent.Width, (int)_frameRenderExtent.Height),
            VSyncEnabled = _options.EnableVSync,
            MsaaSamples = (int)_msaaSampleCount,
            PresentationMode = _activePresentMode switch
            {
                PresentModeKHR.ImmediateKhr => UiPresentationMode.Immediate,
                PresentModeKHR.MailboxKhr => UiPresentationMode.Mailbox,
                PresentModeKHR.FifoKhr => UiPresentationMode.Fifo,
                PresentModeKHR.FifoRelaxedKhr => UiPresentationMode.FifoRelaxed,
                _ => UiPresentationMode.Other,
            },
            GpuParticleCount = _particleCount,
            GpuParticleDispatches = _particleDispatches,
            GpuParticleInitialUploadBytes = _particleStateUploadBytes,
            PresentationSequence = _presentationSequence,
            CpuTimingCollected = _cpuTimingsThisFrame,
            DrawDataFrameId = drawData.DrawDataFrameId,
            PresentationTimestampTicks = _presentationTimestampTicks,
            PresentationIntervalSeconds = presentationInterval,
            BatchedSourceLists = _retainedLayerBatcher.BatchedSourceLists,
            RebuiltBatchGroups = _retainedLayerBatcher.RebuiltGroups,
            RendererAllocatedBytes = profileEnabled ? GC.GetAllocatedBytesForCurrentThread() - allocatedBefore : null,
            LayerTransformPushes = _frameLayerTransformPushes,
            CulledLayerLists = _frameCulledLayerLists,
            LayerBoundsCommandVisits = _frameLayerBoundsCommandVisits,
            AxisAlignedLayerCulling = _axisAlignedLayerCullingEnabled,
            CompiledLayerDraws = _frameCompiledDraws,
            SharedBasisLayerDraws = _frameSharedBasisDraws,
            SharedCameraLayerDraws = _frameSharedCameraDraws,
            CompiledLayerCommands = _frameCompiledCommands,
            CompiledDescriptorBuilds = _frameCompiledDescriptorBuilds,
            CompiledCommandVisits = _frameCompiledCommandVisits,
            CompiledIndexUploadBytes = _frameCompiledIndexUploadBytes,
            LayerStateUploadBytes = _frameLayerStateUploadBytes,
            CompiledIndexCacheBytes = _compiledCacheBytes,
            CompiledIndexLimit = _compiledIndexLimit,
            RetiredSwapchains = _retiredPresentations.Count,
            PendingRenderTargets = _pendingRenderTargetRetirements,
            PendingRenderTargetBytes = _pendingRenderTargetBytes,
            MsaaAllocatedBytes = _msaaColorAllocationBytes,
        };
        if (profileEnabled)
        {
            if (presented)
                CaptureFrameTrace(drawData.DrawDataFrameId, in frameProfile);
            EmitFrameProfile(drawData.DrawDataFrameId, frameContext.ImageIndex, presented, in frameProfile);
        }
        return presented;
    }

    public UiRendererFrameMetrics LastFrameMetrics
    {
        get; private set;
    }
    private ulong _presentationSequence;
    private long _presentationTimestampTicks;
    private readonly UiRetainedLayerBatcher _retainedLayerBatcher = new();

    private VulkanRendererOptions _options;
    private ClearColorValue _clearColorValue = new(0f, 0f, 0f, 1f);
    private UiColor? _damageClearColor;

    public void SetClearColor(UiColor color)
    {
        if (_damageClearColor != color)
        {
            _damageEpoch++;
            _damageClearColor = color;
        }
        _clearColorValue = ToClearColorValue(color);
    }

    public void CreateDeviceObjects()
    {
        if (_swapchain.Handle is not 0)
        {
            return;
        }

        CreateSwapchainResources();
    }

    public void InvalidateDeviceObjects() => DestroySwapchainDependentResources();

    public void SetMinImageCount(int count)
    {
        var newCount = Math.Max(2, count);
        if (_minImageCount == newCount)
        {
            return;
        }

        _minImageCount = newCount;
        RecreateSwapchainAfterSettingsChange();
    }

    public void SetVSync(bool enable)
    {
        if (_options.EnableVSync == enable)
        {
            return;
        }

        _options = _options with
        {
            EnableVSync = enable
        };
        RecreateSwapchainAfterSettingsChange();
    }

    public void SetMsaaSamples(int samples)
    {
        var normalized = NormalizeRequestedMsaaSamples(samples);

        if (_options.MsaaSamples == normalized)
        {
            return;
        }

        var previousMsaaSampleCount = _msaaSampleCount;
        _options = _options with
        {
            MsaaSamples = normalized
        };
        ConfigureMsaaSampleCount();

        if (_msaaSampleCount == previousMsaaSampleCount)
        {
            return;
        }

        RecreateSwapchainAfterSettingsChange();
    }

    private static int NormalizeRequestedMsaaSamples(int samples) => samples switch
    {
        <= 1 => 1,
        <= 2 => 2,
        <= 4 => 4,
        _ => 8,
    };

    private void RecreateSwapchainAfterSettingsChange()
    {
        if (_swapchain.Handle is 0)
        {
            return;
        }

        _frameIndex = 0;
        Array.Clear(_imagesInFlight, 0, _imagesInFlight.Length);
        _ = TryRecreateSwapchain(rebuildAllResources: true);
    }

    private static bool ParseLegacyClipClampPathEnabled()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_LEGACY_CLIP_CLAMP");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static ClearColorValue ToClearColorValue(UiColor color)
    {
        var rgba = color.Rgba;
        var r = ((rgba >> 16) & 0xFF) / 255f;
        var g = ((rgba >> 8) & 0xFF) / 255f;
        var b = (rgba & 0xFF) / 255f;
        var a = ((rgba >> 24) & 0xFF) / 255f;
        return new ClearColorValue(r, g, b, a);
    }
}

