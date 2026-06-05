using System;

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
        if (_device.Handle is not 0)
        {
            TryWaitForDeviceIdle("CreateSwapchainResources", throwOnFailure: true);
        }

        CreateSwapchain();
        _framesInFlight = Math.Min(_swapchainImages.Length, Math.Max(2, _minImageCount));
        CreateImageViews();
        CreateRenderPass();
        CreatePipelineLayouts();
        CreateDescriptorPool();
        CreatePipelineCache();
        CreateGraphicsPipeline();
        SavePipelineCache();
        CreateMsaaColorImage();
        CreateFramebuffers();
        CreateSyncObjects();
    }

    private void RecreateSwapchain()
    {
        DestroySwapchainDependentResources();
        CreateSwapchainResources();
    }

    private bool TryRecreateSwapchain()
    {
        try
        {
            RecreateSwapchain();
            return true;
        }
        catch (InvalidOperationException ex) when (IsRecoverableSwapchainException(ex))
        {
            return false;
        }
    }

    private unsafe void CreateSwapchain()
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

        var desiredImageCount = Math.Max(_minImageCount, (int)capabilities.MinImageCount);
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
        };

        fixed (SwapchainKHR* swapchain = &_swapchain)
        {
            Check(_khrSwapchain.CreateSwapchain(_device, &createInfo, null, swapchain));
        }

        uint imageCount = 0;
        Check(_khrSwapchain.GetSwapchainImages(_device, _swapchain, &imageCount, null));
        var images = new Image[imageCount];
        fixed (Image* imagePtr = images)
        {
            Check(_khrSwapchain.GetSwapchainImages(_device, _swapchain, &imageCount, imagePtr));
        }

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

        foreach (var framebuffer in _framebuffers)
        {
            _vk.DestroyFramebuffer(_device, framebuffer, null);
        }

        DestroyMsaaColorImage();

        if (_renderPass.Handle is not 0)
        {
            _vk.DestroyRenderPass(_device, _renderPass, null);
            _renderPass = default;
        }

        if (_graphicsPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _graphicsPipeline, null);
            _graphicsPipeline = default;
        }

        if (_graphicsColorPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _graphicsColorPipeline, null);
            _graphicsColorPipeline = default;
        }

        if (_solidColorPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _solidColorPipeline, null);
            _solidColorPipeline = default;
        }

        if (_subpixelPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _subpixelPipeline, null);
            _subpixelPipeline = default;
        }

        if (_primitivePipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _primitivePipeline, null);
            _primitivePipeline = default;
        }

        if (_primitiveColorPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _primitiveColorPipeline, null);
            _primitiveColorPipeline = default;
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

        if (_colorFragmentShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _colorFragmentShaderModule, null);
            _colorFragmentShaderModule = default;
        }

        if (_solidVertexShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _solidVertexShaderModule, null);
            _solidVertexShaderModule = default;
        }

        if (_subpixelFragmentShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _subpixelFragmentShaderModule, null);
            _subpixelFragmentShaderModule = default;
        }

        if (_primitiveVertexShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _primitiveVertexShaderModule, null);
            _primitiveVertexShaderModule = default;
        }

        if (_primitiveFragmentShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _primitiveFragmentShaderModule, null);
            _primitiveFragmentShaderModule = default;
        }

        if (_primitiveColorFragmentShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _primitiveColorFragmentShaderModule, null);
            _primitiveColorFragmentShaderModule = default;
        }

        foreach (var imageView in _swapchainImageViews)
        {
            _vk.DestroyImageView(_device, imageView, null);
        }

        if (_swapchain.Handle is not 0)
        {
            _khrSwapchain.DestroySwapchain(_device, _swapchain, null);
            _swapchain = default;
        }

        _swapchainImages = Array.Empty<Image>();
        _swapchainImageViews = Array.Empty<ImageView>();
        _framebuffers = Array.Empty<Framebuffer>();
        _frames = Array.Empty<FrameResources>();
        _imagesInFlight = Array.Empty<Fence>();
        _framesInFlight = 0;
        _frameSemaphores = Array.Empty<FrameSemaphores>();
        _semaphoreIndex = 0;
    }
}
