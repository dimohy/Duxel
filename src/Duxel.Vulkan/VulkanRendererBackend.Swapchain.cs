using Duxel.Core;
using System;
using VulkanSemaphore = Duxel.Vulkan.Semaphore;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private int _minImageCount;
    internal static int ResolveFrameResourceCount(int imageCount, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        return Math.Min(imageCount, maximum);
    }
    private SwapchainKHR _swapchain;
    private SwapchainKHR _recreationOldSwapchain;
    private Image[] _swapchainImages = Array.Empty<Image>();
    private ImageView[] _swapchainImageViews = Array.Empty<ImageView>();
    private Format _swapchainFormat;
    private Extent2D _swapchainExtent;
    private bool _swapchainRecreationPending;

    private void CreateSwapchainResources()
    {
        CreateSwapchain(_recreationOldSwapchain);
        _options.DiagnosticsLog?.Invoke("VulkanInit[Swapchain]");
        _recreationOldSwapchain = default;
        _framesInFlight = ResolveFrameResourceCount(_swapchainImages.Length, _options.MaxFramesInFlight);
        CreateImageViews();
        CreatePipelineLayouts();
        CreateDescriptorPool();
        CreatePipelineCache();
        CreateGraphicsPipeline();
        _options.DiagnosticsLog?.Invoke("VulkanInit[GraphicsPipeline]");
        SavePipelineCache();
        CreateMsaaColorImage();
        CreateSyncObjects();
    }

    private long BeginResizeProfileTiming() => _profilingEnabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
    private long EndResizeProfileTiming(long start) => _profilingEnabled ? System.Diagnostics.Stopwatch.GetTimestamp() - start : 0;

    private void RecreateSwapchain()
    {
        var beforeWait = _profileResizeWaitTicks;
        var beforeCreate = _profileResizeCreateTicks;
        var beforeTargets = _profileResizeTargetsTicks;
        var beforeSync = _profileResizeSyncTicks;
        var resizeStage = BeginResizeProfileTiming();
        // Poll completion without stalling resize; target generations retire
        // against the last graphics submission that could reference them.
        CollectCompletedRenderTargets();
        _profileResizeWaitTicks += EndResizeProfileTiming(resizeStage);

        ResetDamageImages(0);

        var oldSwapchain = _swapchain;
        var oldImageViews = _swapchainImageViews;
        var oldFormat = _swapchainFormat;

        resizeStage = BeginResizeProfileTiming();
        CreateSwapchain(oldSwapchain);
        _profileResizeCreateTicks += EndResizeProfileTiming(resizeStage);

        resizeStage = BeginResizeProfileTiming();
        var reuseMsaa = _msaaColorImage.Handle != 0 && oldFormat == _swapchainFormat
            && _msaaColorExtent.Width >= _swapchainExtent.Width && _msaaColorExtent.Height >= _swapchainExtent.Height
            && (UsesResizableMsaaCapacity() || MsaaTargetExactlyFits());
        RetireRenderTargets(oldImageViews, includeMsaa: !reuseMsaa);
        RetirePresentation(oldSwapchain, _frameSemaphores);
        _frameSemaphores = [];

        CreateImageViews();
        if (_swapchainFormat != oldFormat)
        {
            WaitForAllInFlightFrameFences();
            CollectCompletedRenderTargets();
            DestroyGraphicsPipelineResources();
            CreateGraphicsPipeline();
        }

        CreateMsaaColorImage();
        _profileResizeTargetsTicks += EndResizeProfileTiming(resizeStage);
        resizeStage = BeginResizeProfileTiming();
        _imagesInFlight = new Fence[_swapchainImages.Length];
        // Old present waits remain with their retired swapchain until the first
        // new presentation has completed, independently of render-device idle.
        EnsureFrameSemaphoreCapacity(Math.Max(_frames.Length, _swapchainImages.Length));
        // Frame resources survive ordinary recreation. Their round-robin slot
        // and the age used by deferred frees/cache eviction must keep advancing.
        _profileResizeSyncTicks += EndResizeProfileTiming(resizeStage);
        if (_profilingEnabled)
            WriteVulkanProfileLine($"[duxel-vk-resize] width={_swapchainExtent.Width} height={_swapchainExtent.Height} msaa={(int)_msaaSampleCount} wait={TicksToMicroseconds(_profileResizeWaitTicks - beforeWait):0.000} create={TicksToMicroseconds(_profileResizeCreateTicks - beforeCreate):0.000} targets={TicksToMicroseconds(_profileResizeTargetsTicks - beforeTargets):0.000} sync={TicksToMicroseconds(_profileResizeSyncTicks - beforeSync):0.000} retired={_retiredPresentations.Count} retiredGpu={_pendingRenderTargetRetirements} retiredGpuBytes={_pendingRenderTargetBytes} presentFences={(_swapchainMaintenanceEnabled ? 1 : 0)} msaaBytes={_msaaColorAllocationBytes} msaaWidth={_msaaColorExtent.Width} msaaHeight={_msaaColorExtent.Height}");
    }

    private bool TryRecreateSwapchain(bool rebuildAllResources = false)
    {
        // Finish graphics use before replacement. Present ownership belongs to
        // deferred swapchain retirement; gating creation on display completion
        // can hold a native size transaction behind the compositor it blocks.
        if (!rebuildAllResources && !AreGraphicsFramesComplete())
        {
            _swapchainRecreationPending = true;
            return false;
        }
        try
        {
            if (rebuildAllResources)
            {
                DestroySwapchainDependentResources();
                CreateSwapchainResources();
            }
            else
            {
                RecreateSwapchain();
            }

            _swapchainRecreationPending = false;
            return true;
        }
        catch (InvalidOperationException ex) when (IsRecoverableSwapchainException(ex))
        {
            return false;
        }
    }

    private unsafe void CreateSwapchain(SwapchainKHR oldSwapchain)
    {
        var capabilities = new SurfaceCapabilitiesKHR();
        ObserveValidationWait("surface-capabilities");
        Check(_khrSurface.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, &capabilities));
        ObserveValidationWait("surface-capabilities-complete");

        uint formatCount = 0;
        ObserveValidationWait("surface-formats");
        Check(_khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, null));
        var formats = stackalloc SurfaceFormatKHR[(int)formatCount];
        Check(_khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, formats));

        uint presentModeCount = 0;
        ObserveValidationWait("present-modes");
        Check(_khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &presentModeCount, null));
        var presentModes = stackalloc PresentModeKHR[(int)presentModeCount];
        Check(_khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &presentModeCount, presentModes));

        var chosenFormat = ChooseSurfaceFormat(formats, formatCount);
        var presentMode = ChoosePresentMode(presentModes, presentModeCount);
        var extent = ChoosePresentationCapacity(ChooseExtent(capabilities), presentMode, out var oneToOne);
        var scalingInfo = new SwapchainPresentScalingCreateInfoEXT
        {
            SType = StructureType.SwapchainPresentScalingCreateInfoEXT,
            ScalingBehavior = PresentOneToOne,
            PresentGravityX = PresentGravityMin,
            PresentGravityY = PresentGravityMin
        };

        var desiredImageCount = Math.Max(_minImageCount, (int)capabilities.MinImageCount + 1);
        if (capabilities.MaxImageCount > 0 && desiredImageCount > capabilities.MaxImageCount)
        {
            desiredImageCount = (int)capabilities.MaxImageCount;
        }

        if (desiredImageCount < 2)
        {
            desiredImageCount = 2;
        }

        var compositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr;
        if ((capabilities.SupportedCompositeAlpha & CompositeAlphaFlagsKHR.OpaqueBitKhr) is 0)
        {
            compositeAlpha = CompositeAlphaFlagsKHR.InheritBitKhr;
            if ((capabilities.SupportedCompositeAlpha & CompositeAlphaFlagsKHR.InheritBitKhr) is 0)
            {
                compositeAlpha = CompositeAlphaFlagsKHR.PreMultipliedBitKhr;
                if ((capabilities.SupportedCompositeAlpha & CompositeAlphaFlagsKHR.PreMultipliedBitKhr) is 0)
                {
                    compositeAlpha = CompositeAlphaFlagsKHR.PostMultipliedBitKhr;
                }
            }
        }

        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            PNext = oneToOne ? &scalingInfo : null,
            Surface = _surface,
            MinImageCount = (uint)desiredImageCount,
            ImageFormat = chosenFormat.Format,
            ImageColorSpace = chosenFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.TransferSrcBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = compositeAlpha,
            PresentMode = presentMode,
            // Retained images are read after reacquisition. WSI clipping may
            // discard obscured pixels, invalidating otherwise unchanged regions.
            Clipped = !_damageEnabled,
            OldSwapchain = oldSwapchain,
        };

        var newSwapchain = default(SwapchainKHR);
        ObserveValidationWait("create-swapchain");
        Check(_khrSwapchain.CreateSwapchain(_device, &createInfo, null, &newSwapchain));
        uint imageCount = 0;
        Image[] images;
        try
        {
            ObserveValidationWait("create-swapchain-complete");
            Check(_khrSwapchain.GetSwapchainImages(_device, newSwapchain, &imageCount, null));
            images = new Image[imageCount];
            fixed (Image* imagePtr = images)
            {
                Check(_khrSwapchain.GetSwapchainImages(_device, newSwapchain, &imageCount, imagePtr));
            }
        }
        catch
        {
            _khrSwapchain.DestroySwapchain(_device, newSwapchain, null);
            throw;
        }

        _swapchain = newSwapchain;
        _activePresentMode = presentMode;
        _swapchainImages = images;
        ResetDamageImages(images.Length);
        _swapchainFormat = chosenFormat.Format;
        _swapchainExtent = extent;
        _oneToOnePresentation = oneToOne;
        if (_profilingEnabled)
        {
            var supported = new ReadOnlySpan<PresentModeKHR>(presentModes, checked((int)presentModeCount));
            WriteVulkanProfileLine($"[duxel-vk-wsi] vsync={BoolToInt(_options.EnableVSync)} selected={presentMode} supported={string.Join(",", supported.ToArray())} images={imageCount} extent={extent.Width}x{extent.Height}");
        }
    }

    private void DestroySwapchainDependentResources(bool final = false)
    {
        if (_device.Handle is not 0)
        {
            if (!TryWaitForDeviceIdle("DestroySwapchainDependentResources", throwOnFailure: false))
            {
                return;
            }
        }

        _completedFrameSequence = _submittedFrameSequence;
        FlushPendingTextureDestroysAll();
        FlushPendingBufferDestroysAll();
        CollectRetiredPresentations(_retirementProofFrame);
        DestroyCompiledLayerGroups();
        DestroyGeometryBuffers();
        foreach (var frame in _frames)
        {
            if (frame is null)
            {
                continue;
            }

            FlushRetiredRenderTargets(frame);

            if (frame.CommandPool.Handle is not 0)
            {
                _vk.DestroyCommandPool(_device, frame.CommandPool, null);
            }

            if (frame.TimestampQueryPool.Handle is not 0)
            {
                _vk.DestroyQueryPool(_device, frame.TimestampQueryPool, null);
                frame.TimestampQueryPool = default;
                frame.TimestampQueryIssued = false;
            }

            if (frame.InFlight.Handle is not 0)
            {
                _vk.DestroyFence(_device, frame.InFlight, null);
            }
        }

        if (final)
        {
            WaitForPresentationFencesAtShutdown();
            DestroyFrameSemaphores();
            DestroyRetiredPresentationsAtShutdown();
            DestroyPresentationFencePool();
            _recreationOldSwapchain = default;
        }
        else if (_swapchain.Handle != 0)
        {
            RetirePresentation(_swapchain, _frameSemaphores);
            _recreationOldSwapchain = _swapchain;
            _swapchain = default;
            _frameSemaphores = [];
        }

        DestroyUploadCommandResources();

        DestroyMsaaColorImage();
        DestroyDepthLayerImages();
        DestroyGraphicsPipelineResources();
        DestroyImageViews(_swapchainImageViews);

        if (_swapchain.Handle is not 0)
        {
            _khrSwapchain.DestroySwapchain(_device, _swapchain, null);
            _swapchain = default;
        }

        _swapchainImages = Array.Empty<Image>();
        _swapchainImageViews = Array.Empty<ImageView>();
        _frames = Array.Empty<FrameResources>();
        _lastSubmittedFrame = null;
        _imagesInFlight = Array.Empty<Fence>();
        _framesInFlight = 0;
        _frameSemaphores = Array.Empty<FrameSemaphores>();
    }

    private void DestroyGraphicsPipelineResources()
    {
        for (var index = 0; index < _projectedCapturePipelines.Length; index++)
        {
            if (_projectedCapturePipelines[index].Handle == 0) continue;
            _vk.DestroyPipeline(_device, _projectedCapturePipelines[index], null);
            _projectedCapturePipelines[index] = default;
        }
        for (var index = 0; index < _backdropPipelines.Length; index++)
        {
            if (_backdropPipelines[index].Handle == 0)
                continue;
            _vk.DestroyPipeline(_device, _backdropPipelines[index], null);
            _backdropPipelines[index] = default;
        }
        for (var index = 0; index < _materialDepthMaskPipelines.Length; index++)
        {
            if (_materialDepthMaskPipelines[index].Handle != 0)
                _vk.DestroyPipeline(_device, _materialDepthMaskPipelines[index], null);
            _materialDepthMaskPipelines[index] = default;
        }
        for (var index = 0; index < _circleCutoutPipelines.Length; index++)
        {
            if (_circleCutoutPipelines[index].Handle == 0)
                continue;
            _vk.DestroyPipeline(_device, _circleCutoutPipelines[index], null);
            _circleCutoutPipelines[index] = default;
        }

        if (_sharedCameraDepthReadPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _sharedCameraDepthReadPipeline, null);
            _sharedCameraDepthReadPipeline = default;
        }
        if (_sharedCameraDepthWritePipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _sharedCameraDepthWritePipeline, null);
            _sharedCameraDepthWritePipeline = default;
        }
        if (_sharedBasisDepthReadPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _sharedBasisDepthReadPipeline, null);
            _sharedBasisDepthReadPipeline = default;
        }
        if (_sharedBasisDepthWritePipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _sharedBasisDepthWritePipeline, null);
            _sharedBasisDepthWritePipeline = default;
        }
        if (_compiledDepthReadPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _compiledDepthReadPipeline, null);
            _compiledDepthReadPipeline = default;
        }
        if (_compiledDepthWritePipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _compiledDepthWritePipeline, null);
            _compiledDepthWritePipeline = default;
        }
        if (_depthReadPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _depthReadPipeline, null);
            _depthReadPipeline = default;
        }
        if (_depthWritePipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _depthWritePipeline, null);
            _depthWritePipeline = default;
        }
        if (_graphicsPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _graphicsPipeline, null);
            _graphicsPipeline = default;
        }

        if (_backdropFragmentShaderModule.Handle != 0)
        {
            _vk.DestroyShaderModule(_device, _backdropFragmentShaderModule, null);
            _backdropFragmentShaderModule = default;
        }
        if (_backdropCutoutFragmentShaderModule.Handle != 0)
        {
            _vk.DestroyShaderModule(_device, _backdropCutoutFragmentShaderModule, null);
            _backdropCutoutFragmentShaderModule = default;
        }
        if (_circleCutoutVertexShaderModule.Handle != 0)
        {
            _vk.DestroyShaderModule(_device, _circleCutoutVertexShaderModule, null);
            _circleCutoutVertexShaderModule = default;
        }
        if (_circleCutoutFragmentShaderModule.Handle != 0)
        {
            _vk.DestroyShaderModule(_device, _circleCutoutFragmentShaderModule, null);
            _circleCutoutFragmentShaderModule = default;
        }

        if (_depthMask4VertexShaderModule.Handle != 0)
        {
            _vk.DestroyShaderModule(_device, _depthMask4VertexShaderModule, null);
            _depthMask4VertexShaderModule = default;
        }
        if (_depthMask4FragmentShaderModule.Handle != 0)
        {
            _vk.DestroyShaderModule(_device, _depthMask4FragmentShaderModule, null);
            _depthMask4FragmentShaderModule = default;
        }
        if (_vertexShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _vertexShaderModule, null);
            _vertexShaderModule = default;
        }

        if (_fragmentShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _fragmentShaderModule, null);
            _fragmentShaderModule = default;
        }
    }

    private void DestroyImageViews(ImageView[] imageViews)
    {
        foreach (var imageView in imageViews)
        {
            if (imageView.Handle is not 0)
                _vk.DestroyImageView(_device, imageView, null);
        }
    }

    private readonly IPlatformBackend _platform;

    private unsafe SurfaceFormatKHR ChooseSurfaceFormat(SurfaceFormatKHR* formats, uint count)
    {
        if (count == 1 && formats[0].Format is Format.Undefined)
        {
            return new SurfaceFormatKHR(Format.B8G8R8A8Unorm, ColorSpaceKHR.PaceSrgbNonlinearKhr);
        }

        var desiredFormats = stackalloc Format[4];
        desiredFormats[0] = Format.B8G8R8A8Unorm;
        desiredFormats[1] = Format.B8G8R8A8Srgb;
        desiredFormats[2] = Format.R8G8B8A8Unorm;
        desiredFormats[3] = Format.R8G8B8A8Srgb;

        for (var j = 0; j < 4; j++)
        {
            for (var i = 0u; i < count; i++)
            {
                var format = formats[i];
                if (format.Format == desiredFormats[j] && format.ColorSpace is ColorSpaceKHR.PaceSrgbNonlinearKhr)
                {
                    return format;
                }
            }
        }

        return formats[0];
    }

    private unsafe PresentModeKHR ChoosePresentMode(PresentModeKHR* modes, uint count)
    {
        var hasImmediate = false;
        var hasMailbox = false;
        for (var i = 0u; i < count; i++)
        {
            hasImmediate |= modes[i] == PresentModeKHR.ImmediateKhr;
            hasMailbox |= modes[i] == PresentModeKHR.MailboxKhr;
        }

        if (!_options.EnableVSync)
        {
            if (hasImmediate)
            {
                return PresentModeKHR.ImmediateKhr;
            }

            if (hasMailbox)
            {
                return PresentModeKHR.MailboxKhr;
            }

            Console.WriteLine("[Duxel.Vulkan] VSync OFF requested but Immediate present mode is unavailable; falling back to FIFO (refresh-capped).");
            return PresentModeKHR.FifoKhr;
        }

        // VSync enabled uses a FIFO queue rather than dropping queued frames in mailbox mode.
        return PresentModeKHR.FifoKhr;
    }

    private Extent2D ChooseExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width is not uint.MaxValue)
        {
            return capabilities.CurrentExtent;
        }

        var framebuffer = _platform.FramebufferSize;
        var width = (uint)Math.Clamp(framebuffer.Width, (int)capabilities.MinImageExtent.Width, (int)capabilities.MaxImageExtent.Width);
        var height = (uint)Math.Clamp(framebuffer.Height, (int)capabilities.MinImageExtent.Height, (int)capabilities.MaxImageExtent.Height);
        return new Extent2D(width, height);
    }

    private SampleCountFlags _msaaSampleCount = SampleCountFlags.Count4Bit;
    private PresentModeKHR _activePresentMode;
    private Image _msaaColorImage;
    private DeviceMemory _msaaColorMemory;
    private ImageView _msaaColorImageView;

    private const uint GpuProfileStartQuery = 0;
    private const uint GpuProfileEndQuery = 1;
    private const uint GpuProfileVectorStartQuery = 2;
    private const uint GpuProfileVectorEndQuery = 3;
    private const uint GpuProfileBlurStartQuery = 4;
    private const uint GpuProfileMaxBlurFilters = 16;
    private const uint GpuProfileMaxVectorStageSamples = 64;
    private const uint GpuProfilePathStartQuery = GpuProfileBlurStartQuery + 2 * GpuProfileMaxBlurFilters;
    private const uint GpuProfileCopyStartQuery = GpuProfilePathStartQuery + 2 * GpuProfileMaxVectorStageSamples;
    private const uint GpuProfileTimestampQueryCount = GpuProfileCopyStartQuery + 2 * GpuProfileMaxVectorStageSamples;

    private FrameResources[] _frames = Array.Empty<FrameResources>();
    private Fence[] _imagesInFlight = Array.Empty<Fence>();
    private int _framesInFlight;
    private FrameSemaphores[] _frameSemaphores = Array.Empty<FrameSemaphores>();

    private unsafe void CreateSyncObjects()
    {
        var framesInFlight = _framesInFlight;
        var frames = new FrameResources[framesInFlight];
        for (var i = 0; i < frames.Length; i++)
            frames[i] = new FrameResources();
        _frames = frames;
        _imagesInFlight = new Fence[_swapchainImages.Length];
        _frameIndex = 0;

        var semaphoreCount = Math.Max(framesInFlight, _swapchainImages.Length);
        _frameSemaphores = [];

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamily,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
        };

        var semaphoreInfo = new SemaphoreCreateInfo
        {
            SType = StructureType.SemaphoreCreateInfo,
        };

        var fenceInfo = new FenceCreateInfo
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit,
        };

        CreateUploadCommandResources(fenceInfo);

        var poolPtr = stackalloc CommandPool[1];
        var commandBufferPtr = stackalloc CommandBuffer[1];
        var fencePtr = stackalloc Fence[1];

        EnsureFrameSemaphoreCapacity(semaphoreCount, semaphoreInfo);

        for (var i = 0; i < framesInFlight; i++)
        {
            var frame = frames[i];
            Check(_vk.CreateCommandPool(_device, &poolInfo, null, poolPtr));
            frame.CommandPool = poolPtr[0];

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = frame.CommandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1,
            };

            Check(_vk.AllocateCommandBuffers(_device, &allocInfo, commandBufferPtr));
            frame.CommandBuffer = commandBufferPtr[0];

            Check(_vk.CreateFence(_device, &fenceInfo, null, fencePtr));
            frame.InFlight = fencePtr[0];

            if (_gpuProfilingEnabled)
            {
                frame.TimestampQueryPool = CreateFrameTimestampQueryPool();
            }
        }
    }

    private void DestroyFrameSemaphores()
    {
        foreach (var semaphores in _frameSemaphores)
        {
            if (semaphores.ImageAvailable.Handle is not 0)
            {
                _vk.DestroySemaphore(_device, semaphores.ImageAvailable, null);
            }

            if (semaphores.RenderFinished.Handle is not 0)
            {
                _vk.DestroySemaphore(_device, semaphores.RenderFinished, null);
            }
        }

        _frameSemaphores = [];
    }

    private unsafe void EnsureFrameSemaphoreCapacity(int requiredCount)
    {
        var semaphoreInfo = new SemaphoreCreateInfo
        {
            SType = StructureType.SemaphoreCreateInfo,
        };
        EnsureFrameSemaphoreCapacity(requiredCount, semaphoreInfo);
    }

    private unsafe void EnsureFrameSemaphoreCapacity(int requiredCount, SemaphoreCreateInfo semaphoreInfo)
    {
        if (_frameSemaphores.Length >= requiredCount)
        {
            return;
        }

        var previousCount = _frameSemaphores.Length;
        Array.Resize(ref _frameSemaphores, requiredCount);
        var imageAvailable = default(VulkanSemaphore);
        var renderFinished = default(VulkanSemaphore);
        for (var i = previousCount; i < requiredCount; i++)
        {
            imageAvailable = default;
            renderFinished = default;
            if (i < _frames.Length)
            {
                Check(_vk.CreateSemaphore(_device, &semaphoreInfo, null, &imageAvailable));
                _frameSemaphores[i] = new FrameSemaphores(imageAvailable, default);
            }
            if (i < _swapchainImages.Length)
                Check(_vk.CreateSemaphore(_device, &semaphoreInfo, null, &renderFinished));
            _frameSemaphores[i] = new FrameSemaphores(imageAvailable, renderFinished);
        }
    }

    private unsafe QueryPool CreateFrameTimestampQueryPool()
    {
        var queryPoolInfo = new QueryPoolCreateInfo
        {
            SType = StructureType.QueryPoolCreateInfo,
            QueryType = QueryType.Timestamp,
            QueryCount = GpuProfileTimestampQueryCount,
        };

        var queryPool = default(QueryPool);
        var result = _vk.CreateQueryPool(_device, &queryPoolInfo, null, &queryPool);
        if (result == Result.Success)
        {
            return queryPool;
        }

        TraceVulkanFailure(result);
        _gpuProfilingEnabled = false;
        return default;
    }
}

