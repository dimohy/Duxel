using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend : IRendererBackend
{
    private readonly Vk _vk = Vk.GetApi();

    public VulkanRendererBackend(IPlatformBackend platform, VulkanRendererOptions options)
    {
        _options = options;
        _fontTextureIdValue = options.FontTextureId.Value is 0 ? 1 : options.FontTextureId.Value;
        _whiteTextureIdValue = options.WhiteTextureId.Value is 0 ? 2 : options.WhiteTextureId.Value;
        _platform = platform;
        _surfaceSource = platform.VulkanSurface ?? throw new InvalidOperationException(
            "Platform backend does not provide a Vulkan surface."
        );
        _minImageCount = Math.Max(2, options.MinImageCount);
        _pipelineCachePath = string.Empty;

        CreateInstance();
        LoadInstanceExtensions();
        CreateSurface();
        PickPhysicalDevice();
        ResolveDevicePolicySettings();
        ConfigureMsaaSampleCount();
        CreateDevice();
        LoadDeviceExtensions();
        CreateSwapchainResources();
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
        SavePipelineCache();
        DestroySwapchainDependentResources();
        DestroyDeviceResources();

        if (_device.Handle is not 0)
        {
            _vk.DestroyDevice(_device, null);
        }

        if (_surface.Handle is not 0)
        {
            _khrSurface.DestroySurface(_instance, _surface, null);
        }

        if (_instance.Handle is not 0)
        {
            _vk.DestroyInstance(_instance, null);
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

        DestroyGeometryBuffers();
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

    public void RenderDrawData(UiDrawData drawData)
    {
        _ = TryRenderDrawData(drawData);
    }

    public bool TryRenderDrawData(UiDrawData drawData)
    {
        var profileEnabled = _profilingEnabled;

        if (profileEnabled)
        {
            ResetImageTransitionProfileCounters();
            ResetUploadProfileCounters();
        }

        ApplyTextureUpdates(drawData.TextureUpdates.AsSpan());

        var targetStart = BeginFrameProfileTiming(profileEnabled);
        if (!TryEnsureFrameTarget(drawData))
        {
            return false;
        }

        var targetTicks = EndFrameProfileTiming(profileEnabled, targetStart);
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
        var geometryBuffers = PrepareFrameGeometryForRecording(
            drawData,
            frame,
            frameData,
            profileEnabled,
            out frameProfile.UploadTicks);

        var commandBuffer = RecordFrameCommandsForSubmission(
            drawData,
            frameContext.ImageIndex,
            frameData,
            in geometryBuffers,
            profileEnabled,
            ref frameProfile);

        return CompleteRecordedFrame(frameContext, commandBuffer, profileEnabled, ref frameProfile);
    }

    private VulkanRendererOptions _options;
    private ClearColorValue _clearColorValue = new(0f, 0f, 0f, 1f);

    public void SetClearColor(UiColor color)
    {
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

        _options = _options with { EnableVSync = enable };
        RecreateSwapchainAfterSettingsChange();
    }

    public void SetMsaaSamples(int samples)
    {
        var normalized = samples switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 4 => 4,
            _ => 8,
        };

        if (_options.MsaaSamples == normalized)
        {
            return;
        }

        var previousMsaaSampleCount = _msaaSampleCount;
        _options = _options with { MsaaSamples = normalized };
        ConfigureMsaaSampleCount();

        if (_msaaSampleCount == previousMsaaSampleCount)
        {
            return;
        }

        RecreateSwapchainAfterSettingsChange();
    }

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

