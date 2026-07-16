using Duxel.Core;
using System;
using VulkanSemaphore = Duxel.Vulkan.Semaphore;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private int _minImageCount;
    private SwapchainKHR _swapchain;
    private Image[] _swapchainImages = Array.Empty<Image>();
    private ImageView[] _swapchainImageViews = Array.Empty<ImageView>();
    private Format _swapchainFormat;
    private Extent2D _swapchainExtent;

    private void CreateSwapchainResources()
    {
        CreateSwapchain(default);
        _framesInFlight = Math.Min(_swapchainImages.Length, Math.Max(2, _minImageCount));
        CreateImageViews();
        CreatePipelineLayouts();
        CreateDescriptorPool();
        CreatePipelineCache();
        CreateGraphicsPipeline();
        SavePipelineCache();
        CreateMsaaColorImage();
        CreateSyncObjects();
    }

    private void RecreateSwapchain()
    {
        _ = TryWaitForDeviceIdle("RecreateSwapchain", throwOnFailure: true);

        FlushPendingTextureDestroysAll();
        FlushPendingBufferDestroysAll();

        var oldSwapchain = _swapchain;
        var oldImageViews = _swapchainImageViews;
        var oldFormat = _swapchainFormat;

        CreateSwapchain(oldSwapchain);

        DestroyMsaaColorImage();
        DestroyImageViews(oldImageViews);
        if (oldSwapchain.Handle is not 0)
        {
            _khrSwapchain.DestroySwapchain(_device, oldSwapchain, null);
        }

        CreateImageViews();
        if (_swapchainFormat != oldFormat)
        {
            DestroyGraphicsPipelineResources();
            CreateGraphicsPipeline();
        }

        CreateMsaaColorImage();
        _imagesInFlight = new Fence[_swapchainImages.Length];
        EnsureFrameSemaphoreCapacity(Math.Max(2, _swapchainImages.Length + 1));
        _frameIndex = 0;
        _semaphoreIndex = 0;
    }

    private bool TryRecreateSwapchain(bool rebuildAllResources = false)
    {
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
        Check(_khrSurface.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, &capabilities));

        uint formatCount = 0;
        Check(_khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, null));
        var formats = stackalloc SurfaceFormatKHR[(int)formatCount];
        Check(_khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, formats));

        uint presentModeCount = 0;
        Check(_khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &presentModeCount, null));
        var presentModes = stackalloc PresentModeKHR[(int)presentModeCount];
        Check(_khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &presentModeCount, presentModes));

        var chosenFormat = ChooseSurfaceFormat(formats, formatCount);
        var presentMode = ChoosePresentMode(presentModes, presentModeCount);
        var extent = ChooseExtent(capabilities);

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
            Clipped = true,
            OldSwapchain = oldSwapchain,
        };

        var newSwapchain = default(SwapchainKHR);
        Check(_khrSwapchain.CreateSwapchain(_device, &createInfo, null, &newSwapchain));

        uint imageCount = 0;
        Check(_khrSwapchain.GetSwapchainImages(_device, newSwapchain, &imageCount, null));
        var images = new Image[imageCount];
        fixed (Image* imagePtr = images)
        {
            Check(_khrSwapchain.GetSwapchainImages(_device, newSwapchain, &imageCount, imagePtr));
        }

        _swapchain = newSwapchain;
        _swapchainImages = images;
        _swapchainFormat = chosenFormat.Format;
        _swapchainExtent = extent;
    }

    private void DestroySwapchainDependentResources()
    {
        if (_device.Handle is not 0)
        {
            if (!TryWaitForDeviceIdle("DestroySwapchainDependentResources", throwOnFailure: false))
            {
                return;
            }
        }

        FlushPendingTextureDestroysAll();
        FlushPendingBufferDestroysAll();
        DestroyGeometryBuffers();
        foreach (var frame in _frames)
        {
            if (frame is null)
            {
                continue;
            }

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

        DestroyUploadCommandResources();

        DestroyMsaaColorImage();
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
        _imagesInFlight = Array.Empty<Fence>();
        _framesInFlight = 0;
        _frameSemaphores = Array.Empty<FrameSemaphores>();
        _semaphoreIndex = 0;
    }

    private void DestroyGraphicsPipelineResources()
    {
        if (_graphicsPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _graphicsPipeline, null);
            _graphicsPipeline = default;
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

        if (hasMailbox)
        {
            return PresentModeKHR.MailboxKhr;
        }

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
    private Image _msaaColorImage;
    private DeviceMemory _msaaColorMemory;
    private ImageView _msaaColorImageView;

    private const uint GpuProfileTimestampQueryCount = 2;
    private const uint GpuProfileStartQuery = 0;
    private const uint GpuProfileEndQuery = 1;

    private FrameResources[] _frames = Array.Empty<FrameResources>();
    private Fence[] _imagesInFlight = Array.Empty<Fence>();
    private int _framesInFlight;
    private FrameSemaphores[] _frameSemaphores = Array.Empty<FrameSemaphores>();
    private int _semaphoreIndex;

    private unsafe void CreateSyncObjects()
    {
        var framesInFlight = _framesInFlight;
        _frames = new FrameResources[framesInFlight];
        _imagesInFlight = new Fence[_swapchainImages.Length];
        _frameIndex = 0;
        _semaphoreIndex = 0;

        var semaphoreCount = Math.Max(2, _swapchainImages.Length + 1);
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
            var framePool = default(CommandPool);
            Check(_vk.CreateCommandPool(_device, &poolInfo, null, poolPtr));
            framePool = poolPtr[0];

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = framePool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1,
            };

            var frameBuffer = default(CommandBuffer);
            Check(_vk.AllocateCommandBuffers(_device, &allocInfo, commandBufferPtr));
            frameBuffer = commandBufferPtr[0];

            var frame = new FrameResources
            {
                CommandPool = framePool,
                CommandBuffer = frameBuffer,
            };

            Check(_vk.CreateFence(_device, &fenceInfo, null, fencePtr));
            frame.InFlight = fencePtr[0];

            if (_gpuProfilingEnabled)
            {
                frame.TimestampQueryPool = CreateFrameTimestampQueryPool();
            }

            _frames[i] = frame;
        }
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
            Check(_vk.CreateSemaphore(_device, &semaphoreInfo, null, &imageAvailable));
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

