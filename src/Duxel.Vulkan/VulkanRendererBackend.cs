using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;
using VulkanSemaphore = Duxel.Vulkan.Semaphore;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend : IRendererBackend
{
    private const int DefaultVertexBufferCapacity = 5_000;
    private const int DefaultIndexBufferCapacity = 10_000;
    private const int DefaultPrimitiveInstanceBufferCapacity = 5_000;
    private const int VertexBufferGrowthPadding = 5_000;
    private const int IndexBufferGrowthPadding = 10_000;
    private const int PrimitiveInstanceBufferGrowthPadding = 5_000;
    private static readonly string[] BaseDeviceExtensions =
    [
        "VK_KHR_swapchain",
    ];

    private readonly Vk _vk = Vk.GetApi();
    private VulkanRendererOptions _options;
    private readonly IVulkanSurfaceSource _surfaceSource;
    private readonly IPlatformBackend _platform;
    private int _minImageCount;
    private KhrSurface _khrSurface = null!;
    private KhrSwapchain _khrSwapchain = null!;
    private Instance _instance;
    private SurfaceKHR _surface;
    private PhysicalDevice _physicalDevice;
    private Device _device;
    private Queue _graphicsQueue;
    private Queue _transferQueue;
    private uint _graphicsQueueFamily;
    private uint _transferQueueFamily;
    private SwapchainKHR _swapchain;
    private Image[] _swapchainImages = Array.Empty<Image>();
    private ImageView[] _swapchainImageViews = Array.Empty<ImageView>();
    private Format _swapchainFormat;
    private Extent2D _swapchainExtent;
    private RenderPass _renderPass;
    private DescriptorSetLayout _descriptorSetLayout;
    private PipelineLayout _pipelineLayout;
    private Sampler _fontSampler;
    private Sampler _imageSampler;
    private Pipeline _graphicsPipeline;
    private Pipeline _subpixelPipeline;
    private Pipeline _rectPrimitivePipeline;
    private Pipeline _circlePrimitivePipeline;
    private ShaderModule _vertexShaderModule;
    private ShaderModule _fragmentShaderModule;
    private ShaderModule _subpixelFragmentShaderModule;
    private ShaderModule _rectPrimitiveVertexShaderModule;
    private ShaderModule _rectPrimitiveFragmentShaderModule;
    private ShaderModule _circlePrimitiveVertexShaderModule;
    private ShaderModule _circlePrimitiveFragmentShaderModule;
    private PipelineCache _pipelineCache;
    private DescriptorPool _descriptorPool;
    private Framebuffer[] _framebuffers = Array.Empty<Framebuffer>();
    private CommandPool _uploadCommandPool;
    private CommandBuffer _uploadCommandBuffer;
    private Fence _uploadFence;
    private bool _hasPendingUploadWork;
    private int _uploadBatchDepth;
    private bool _uploadBatchRecording;
    private FrameResources[] _frames = Array.Empty<FrameResources>();
    private Fence[] _imagesInFlight = Array.Empty<Fence>();
    private int _framesInFlight;
    private FrameSemaphores[] _frameSemaphores = Array.Empty<FrameSemaphores>();
    private int _semaphoreIndex;
    private readonly Dictionary<UiTextureId, TextureResource> _textures = new();
    private int _frameIndex;
    private int _lastImageIndex = -1;
    private ClearColorValue _clearColorValue = new(0f, 0f, 0f, 1f);
    private VkBuffer _stagingBuffer;
    private DeviceMemory _stagingMemory;
    private nuint _stagingBufferSize;
    private unsafe void* _stagingMappedPtr;
    private bool _stagingMapped;
    private readonly string _pipelineCachePath;
    private nuint _pipelineCacheSize;
    private ulong _pipelineCacheHash;
    private readonly bool _uploadBatchingEnabled = ParseUploadBatchingEnabled();
    private readonly bool _legacyClipClampPath = ParseLegacyClipClampPathEnabled();

    // MSAA resources
    private SampleCountFlags _msaaSampleCount = SampleCountFlags.Count4Bit;
    private Image _msaaColorImage;
    private DeviceMemory _msaaColorMemory;
    private ImageView _msaaColorImageView;
    private static readonly byte[] VertexShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui.vert.spv");
    private static readonly byte[] FragmentShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui.frag.spv");
    private static readonly byte[] SubpixelFragmentShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui_subpixel.frag.spv");
    private static readonly byte[] RectPrimitiveVertexShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.rect_primitive.vert.spv");
    private static readonly byte[] RectPrimitiveFragmentShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.rect_primitive.frag.spv");
    private static readonly byte[] CirclePrimitiveVertexShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.circle_primitive.vert.spv");
    private static readonly byte[] CirclePrimitiveFragmentShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.circle_primitive.frag.spv");

    public VulkanRendererBackend(IPlatformBackend platform, VulkanRendererOptions options)
    {
        _options = options;
        _platform = platform;
        _surfaceSource = platform.VulkanSurface ?? throw new InvalidOperationException(
            "Platform backend does not provide a Vulkan surface."
        );
        _minImageCount = Math.Max(2, options.MinImageCount);
        _pipelineCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Duxel", "vulkan_pipeline_cache.bin");

        CreateInstance();
        LoadInstanceExtensions();
        CreateSurface();
        PickPhysicalDevice();
        ConfigureMsaaSampleCount();
        CreateDevice();
        LoadDeviceExtensions();
        CreateSwapchainResources();
    }

    private void ConfigureMsaaSampleCount()
    {
        var requested = _options.MsaaSamples;
        if (requested is not (1 or 2 or 4 or 8))
        {
            requested = 4;
        }

        _vk.GetPhysicalDeviceProperties(_physicalDevice, out var properties);
        var supported = properties.Limits.FramebufferColorSampleCounts;

        _msaaSampleCount = requested switch
        {
            >= 8 when (supported & SampleCountFlags.Count8Bit) != 0 => SampleCountFlags.Count8Bit,
            >= 4 when (supported & SampleCountFlags.Count4Bit) != 0 => SampleCountFlags.Count4Bit,
            >= 2 when (supported & SampleCountFlags.Count2Bit) != 0 => SampleCountFlags.Count2Bit,
            _ => SampleCountFlags.Count1Bit,
        };
    }

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

    public void RenderDrawData(UiDrawData drawData)
    {
        var recordTextureLookupTicks = 0L;
        var recordClippingTicks = 0L;
        var recordDescriptorBindTicks = 0L;
        var recordDrawCallTicks = 0L;
        var submitTicks = 0L;
        var presentTicks = 0L;

        ApplyTextureUpdates(drawData.TextureUpdates.AsSpan());

        if (drawData.DisplaySize.X <= 0 || drawData.DisplaySize.Y <= 0)
        {
            return;
        }

        var expectedFbWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        var expectedFbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
        if (expectedFbWidth != (int)_swapchainExtent.Width || expectedFbHeight != (int)_swapchainExtent.Height)
        {
            if (!TryRecreateSwapchain())
            {
                return;
            }

            if (expectedFbWidth != (int)_swapchainExtent.Width || expectedFbHeight != (int)_swapchainExtent.Height)
            {
                return;
            }
        }

        var fbWidth = (int)_swapchainExtent.Width;
        var fbHeight = (int)_swapchainExtent.Height;
        if (fbWidth <= 0 || fbHeight <= 0)
        {
            return;
        }

        CountPrimitiveInstances(drawData, out var rectPrimitiveCount, out var circlePrimitiveCount);
        var hasIndexedGeometry = drawData.TotalVertexCount > 0 && drawData.TotalIndexCount > 0;
        if (!hasIndexedGeometry && rectPrimitiveCount is 0 && circlePrimitiveCount is 0)
        {
            return;
        }

        var frameCount = _frames.Length;
        var frame = _frameIndex % frameCount;
        var frameData = _frames[frame];

        fixed (Fence* fence = &frameData.InFlight)
        {
            Check(_vk.WaitForFences(_device, 1, fence, true, ulong.MaxValue));
        }

        FlushPendingTextureDestroys(frame);
        FlushPendingBufferDestroys(frame);

        var semaphores = _frameSemaphores[_semaphoreIndex];
        _semaphoreIndex = (_semaphoreIndex + 1) % _frameSemaphores.Length;
        var imageAvailable = semaphores.ImageAvailable;
        uint imageIndex = 0;
        var acquireResult = _khrSwapchain.AcquireNextImage(_device, _swapchain, ulong.MaxValue, imageAvailable, default, &imageIndex);
        if (acquireResult == Result.ErrorOutOfDateKhr || IsSurfaceLost(acquireResult))
        {
            if (!TryRecreateSwapchain())
            {
                return;
            }

            acquireResult = _khrSwapchain.AcquireNextImage(_device, _swapchain, ulong.MaxValue, imageAvailable, default, &imageIndex);
            if (acquireResult == Result.ErrorOutOfDateKhr || IsSurfaceLost(acquireResult))
            {
                return;
            }
        }

        if (acquireResult != Result.Success && !IsSuboptimal(acquireResult))
        {
            Check(acquireResult);
        }

        _lastImageIndex = (int)imageIndex;

        if (_imagesInFlight.Length > 0)
        {
            var imageFence = _imagesInFlight[imageIndex];
            if (imageFence.Handle is not 0 && imageFence.Handle != frameData.InFlight.Handle)
            {
                var fencePtr = stackalloc Fence[1];
                fencePtr[0] = imageFence;
                Check(_vk.WaitForFences(_device, 1, fencePtr, true, ulong.MaxValue));
            }

            _imagesInFlight[imageIndex] = frameData.InFlight;
        }

        if (hasIndexedGeometry)
        {
            EnsureVertexBufferCapacity(frame, drawData.TotalVertexCount);
            EnsureIndexBufferCapacity(frame, drawData.TotalIndexCount);
        }

        EnsureRectPrimitiveBufferCapacity(frame, rectPrimitiveCount);
        EnsureCirclePrimitiveBufferCapacity(frame, circlePrimitiveCount);

        UploadGeometry(frame, drawData, _frameStaticBindings);

        var commandPool = frameData.CommandPool;
        var commandBuffer = frameData.CommandBuffer;
        Check(_vk.ResetCommandPool(_device, commandPool, 0));
        var vertexBuffer = frameData.RenderBuffers.VertexBuffer;
        var indexBuffer = frameData.RenderBuffers.IndexBuffer;
        var rectPrimitiveBuffer = frameData.RenderBuffers.RectPrimitiveBuffer;
        var circlePrimitiveBuffer = frameData.RenderBuffers.CirclePrimitiveBuffer;
        RecordCommandBuffer(
            commandBuffer,
            imageIndex,
            drawData,
            vertexBuffer,
            indexBuffer,
            rectPrimitiveBuffer,
            circlePrimitiveBuffer,
            _frameStaticBindings,
            false,
            0f,
            0f,
            out recordTextureLookupTicks,
            out recordClippingTicks,
            out recordDescriptorBindTicks,
            out recordDrawCallTicks);

        var renderFinished = semaphores.RenderFinished;
        submitTicks = SubmitFrame(commandBuffer, imageAvailable, renderFinished, frameData.InFlight);
        var presentResult = PresentFrame(imageIndex, renderFinished, out presentTicks);
        if (presentResult == Result.ErrorOutOfDateKhr || IsSuboptimal(presentResult) || IsSurfaceLost(presentResult))
        {
            _ = TryRecreateSwapchain();
        }
        else if (presentResult != Result.Success)
        {
            Check(presentResult);
        }

        _frameIndex++;
    }

    private static void CountPrimitiveInstances(UiDrawData drawData, out int rectPrimitiveCount, out int circlePrimitiveCount)
    {
        rectPrimitiveCount = 0;
        circlePrimitiveCount = 0;

        for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
        {
            var drawList = drawData.DrawLists[listIndex];
            rectPrimitiveCount += drawList.RectFilledPrimitives?.Count ?? 0;
            circlePrimitiveCount += drawList.CircleFilledPrimitives?.Count ?? 0;
        }
    }

    private unsafe long SubmitFrame(CommandBuffer commandBuffer, VulkanSemaphore imageAvailable, VulkanSemaphore renderFinished, Fence inFlightFence)
    {
        WaitForPendingUploadWork();

        var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;
        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &imageAvailable,
            PWaitDstStageMask = &waitStage,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &renderFinished,
        };

        Check(_vk.ResetFences(_device, 1, &inFlightFence));
        var submitStart = _profilingEnabled ? Stopwatch.GetTimestamp() : 0;
        Check(_vk.QueueSubmit(_graphicsQueue, 1, &submitInfo, inFlightFence));
        return _profilingEnabled ? Stopwatch.GetTimestamp() - submitStart : 0L;
    }

    private unsafe void WaitForPendingUploadWork()
    {
        if (!_hasPendingUploadWork || _uploadFence.Handle is 0)
        {
            return;
        }

        fixed (Fence* fence = &_uploadFence)
        {
            Check(_vk.WaitForFences(_device, 1, fence, true, ulong.MaxValue));
        }

        _hasPendingUploadWork = false;
    }

    private unsafe Result PresentFrame(uint imageIndex, VulkanSemaphore renderFinished, out long presentTicks)
    {
        var swapchain = _swapchain;
        var presentInfo = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &renderFinished,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &imageIndex,
        };

        var presentStart = _profilingEnabled ? Stopwatch.GetTimestamp() : 0;
        var presentResult = _khrSwapchain.QueuePresent(_graphicsQueue, &presentInfo);
        presentTicks = _profilingEnabled ? Stopwatch.GetTimestamp() - presentStart : 0L;
        return presentResult;
    }

    private static bool ParseUploadBatchingEnabled()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_UPLOAD_BATCH");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (string.Equals(raw, "0", StringComparison.Ordinal)
            || string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase);
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

    public void SetMinImageCount(int count)
    {
        var newCount = Math.Max(2, count);
        if (_minImageCount == newCount)
        {
            return;
        }

        _minImageCount = newCount;
        if (_swapchain.Handle is not 0)
        {
            _frameIndex = 0;
            Array.Clear(_imagesInFlight, 0, _imagesInFlight.Length);
            _ = TryRecreateSwapchain();
        }
    }

    public void SetVSync(bool enable)
    {
        if (_options.EnableVSync == enable)
        {
            return;
        }

        _options = _options with { EnableVSync = enable };
        if (_swapchain.Handle is not 0)
        {
            _frameIndex = 0;
            Array.Clear(_imagesInFlight, 0, _imagesInFlight.Length);
            _ = TryRecreateSwapchain();
        }
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

        if (_swapchain.Handle is not 0)
        {
            _frameIndex = 0;
            Array.Clear(_imagesInFlight, 0, _imagesInFlight.Length);
            _ = TryRecreateSwapchain();
        }
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

        if (_uploadCommandPool.Handle is not 0)
        {
            if (_uploadCommandBuffer.Handle is not 0)
            {
                var buffer = _uploadCommandBuffer;
                _vk.FreeCommandBuffers(_device, _uploadCommandPool, 1, &buffer);
                _uploadCommandBuffer = default;
            }

            if (_uploadFence.Handle is not 0)
            {
                _vk.DestroyFence(_device, _uploadFence, null);
                _uploadFence = default;
                _hasPendingUploadWork = false;
                _uploadBatchDepth = 0;
                _uploadBatchRecording = false;
            }

            _vk.DestroyCommandPool(_device, _uploadCommandPool, null);
            _uploadCommandPool = default;
        }

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

        if (_subpixelPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _subpixelPipeline, null);
            _subpixelPipeline = default;
        }

        if (_rectPrimitivePipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _rectPrimitivePipeline, null);
            _rectPrimitivePipeline = default;
        }

        if (_circlePrimitivePipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _circlePrimitivePipeline, null);
            _circlePrimitivePipeline = default;
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

        if (_subpixelFragmentShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _subpixelFragmentShaderModule, null);
            _subpixelFragmentShaderModule = default;
        }

        if (_rectPrimitiveVertexShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _rectPrimitiveVertexShaderModule, null);
            _rectPrimitiveVertexShaderModule = default;
        }

        if (_rectPrimitiveFragmentShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _rectPrimitiveFragmentShaderModule, null);
            _rectPrimitiveFragmentShaderModule = default;
        }

        if (_circlePrimitiveVertexShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _circlePrimitiveVertexShaderModule, null);
            _circlePrimitiveVertexShaderModule = default;
        }

        if (_circlePrimitiveFragmentShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _circlePrimitiveFragmentShaderModule, null);
            _circlePrimitiveFragmentShaderModule = default;
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

    private unsafe void CreateInstance()
    {
        var requiredExtensions = _surfaceSource.RequiredInstanceExtensions;
        var extensionList = new List<string>(requiredExtensions.Count);
        for (var i = 0; i < requiredExtensions.Count; i++)
        {
            extensionList.Add(requiredExtensions[i]);
        }

        var extensions = extensionList.ToArray();

        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            ApiVersion = Vk.Version12,
            PApplicationName = VulkanMarshaling.StringToPtr("Duxel"),
            PEngineName = VulkanMarshaling.StringToPtr("Duxel"),
            EngineVersion = new Version32(1, 0, 0),
            ApplicationVersion = new Version32(1, 0, 0),
        };

        var extensionPtr = VulkanMarshaling.StringArrayToPtr(extensions);

        try
        {
            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = (uint)extensions.Length,
                PpEnabledExtensionNames = (byte**)extensionPtr,
            };

            fixed (Instance* instance = &_instance)
            {
                Check(_vk.CreateInstance(&createInfo, null, instance));
            }
        }
        finally
        {
            VulkanMarshaling.Free((nint)appInfo.PApplicationName);
            VulkanMarshaling.Free((nint)appInfo.PEngineName);
            VulkanMarshaling.FreeStringArray(extensionPtr, extensions.Length);
        }
    }

    private void LoadInstanceExtensions()
    {
        if (!KhrSurface.TryCreate(_vk, _instance, out _khrSurface))
        {
            throw new InvalidOperationException("Failed to load VK_KHR_surface extension.");
        }
    }

    private void CreateSurface()
    {
        var surfaceHandle = _surfaceSource.CreateSurface((nuint)_instance.Handle);
        _surface = new SurfaceKHR((ulong)surfaceHandle);
    }

    private unsafe void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        Check(_vk.EnumeratePhysicalDevices(_instance, &deviceCount, null));

        if (deviceCount is 0)
        {
            throw new InvalidOperationException("No Vulkan physical devices found.");
        }

        var devices = stackalloc PhysicalDevice[(int)deviceCount];
        Check(_vk.EnumeratePhysicalDevices(_instance, &deviceCount, devices));

        for (var i = 0; i < deviceCount; i++)
        {
            var device = devices[i];
            if (TryFindGraphicsQueue(device, out var queueFamily))
            {
                _physicalDevice = device;
                _graphicsQueueFamily = queueFamily;
                _transferQueueFamily = FindTransferQueueFamily(device, queueFamily);
                return;
            }
        }

        throw new InvalidOperationException("No suitable Vulkan physical device found.");
    }

    private unsafe bool TryFindGraphicsQueue(PhysicalDevice device, out uint queueFamily)
    {
        uint count = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);
        var families = stackalloc QueueFamilyProperties[(int)count];
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, families);

        for (var i = 0u; i < count; i++)
        {
            var props = families[i];
            if ((props.QueueFlags & QueueFlags.GraphicsBit) is 0)
            {
                continue;
            }

            Bool32 supportsPresent;
            Check(_khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, _surface, &supportsPresent));

            if (supportsPresent.Value is 0)
            {
                continue;
            }

            queueFamily = i;
            return true;
        }

        queueFamily = 0;
        return false;
    }

    private unsafe uint FindTransferQueueFamily(PhysicalDevice device, uint graphicsQueueFamily)
    {
        return graphicsQueueFamily;
    }

    private unsafe void CreateDevice()
    {
        var queuePriority = 1.0f;
        var queueCreateInfos = stackalloc DeviceQueueCreateInfo[2];
        var queueCreateInfoCount = 0u;

        queueCreateInfos[queueCreateInfoCount++] = new DeviceQueueCreateInfo
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamily,
            QueueCount = 1,
            PQueuePriorities = &queuePriority,
        };

        if (_transferQueueFamily != _graphicsQueueFamily)
        {
            queueCreateInfos[queueCreateInfoCount++] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = _transferQueueFamily,
                QueueCount = 1,
                PQueuePriorities = &queuePriority,
            };
        }

        var extensionPtr = VulkanMarshaling.StringArrayToPtr(BaseDeviceExtensions);

        try
        {
            var enabledFeatures = new PhysicalDeviceFeatures
            {
                DualSrcBlend = 1,
            };

            var deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = queueCreateInfoCount,
                PQueueCreateInfos = queueCreateInfos,
                EnabledExtensionCount = (uint)BaseDeviceExtensions.Length,
                PpEnabledExtensionNames = (byte**)extensionPtr,
                PEnabledFeatures = &enabledFeatures,
            };

            fixed (Device* device = &_device)
            {
                Check(_vk.CreateDevice(_physicalDevice, &deviceCreateInfo, null, device));
            }

            _vk.GetDeviceQueue(_device, _graphicsQueueFamily, 0, out _graphicsQueue);
            _vk.GetDeviceQueue(_device, _transferQueueFamily, 0, out _transferQueue);
        }
        finally
        {
            VulkanMarshaling.FreeStringArray(extensionPtr, BaseDeviceExtensions.Length);
        }
    }

    private void LoadDeviceExtensions()
    {
        if (!KhrSwapchain.TryCreate(_vk, _instance, _device, out _khrSwapchain))
        {
            throw new InvalidOperationException("Failed to load VK_KHR_swapchain extension.");
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

    private unsafe void CreateImageViews()
    {
        _swapchainImageViews = new ImageView[_swapchainImages.Length];

        for (var i = 0; i < _swapchainImages.Length; i++)
        {
            var createInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainFormat,
                Components = new ComponentMapping(
                    ComponentSwizzle.Identity,
                    ComponentSwizzle.Identity,
                    ComponentSwizzle.Identity,
                    ComponentSwizzle.Identity
                ),
                SubresourceRange = new ImageSubresourceRange(
                    ImageAspectFlags.ColorBit,
                    0,
                    1,
                    0,
                    1
                ),
            };

            fixed (ImageView* view = &_swapchainImageViews[i])
            {
                Check(_vk.CreateImageView(_device, &createInfo, null, view));
            }
        }
    }

    private unsafe void CreateRenderPass()
    {
        if (_msaaSampleCount == SampleCountFlags.Count1Bit)
        {
            var colorLoadOp = AttachmentLoadOp.Clear;
            var initialLayout = ImageLayout.Undefined;
            var finalLayout = ImageLayout.PresentSrcKhr;
            var colorAttachment = new AttachmentDescription
            {
                Format = _swapchainFormat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = colorLoadOp,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = initialLayout,
                FinalLayout = finalLayout,
            };

            var singleColorAttachmentRef = new AttachmentReference
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal,
            };

            var singleSubpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &singleColorAttachmentRef,
                PResolveAttachments = null,
            };

            var singleDependency = new SubpassDependency
            {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                SrcAccessMask = 0,
                DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
                DependencyFlags = DependencyFlags.ByRegionBit,
            };

            var singleRenderPassInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 1,
                PAttachments = &colorAttachment,
                SubpassCount = 1,
                PSubpasses = &singleSubpass,
                DependencyCount = 1,
                PDependencies = &singleDependency,
            };

            fixed (RenderPass* renderPass = &_renderPass)
            {
                Check(_vk.CreateRenderPass(_device, &singleRenderPassInfo, null, renderPass));
            }

            return;
        }

        // Attachment 0: MSAA color attachment (multisampled, not stored directly)
        var msaaColorAttachment = new AttachmentDescription
        {
            Format = _swapchainFormat,
            Samples = _msaaSampleCount,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,  // MSAA image not needed after resolve
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ColorAttachmentOptimal,
        };

        // Attachment 1: Resolve attachment (swapchain image, single-sampled)
        var resolveAttachment = new AttachmentDescription
        {
            Format = _swapchainFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.DontCare,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr,
        };

        var colorAttachmentRef = new AttachmentReference
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal,
        };

        var resolveAttachmentRef = new AttachmentReference
        {
            Attachment = 1,
            Layout = ImageLayout.ColorAttachmentOptimal,
        };

        var subpass = new SubpassDescription
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef,
            PResolveAttachments = &resolveAttachmentRef,
        };

        var dependency = new SubpassDependency
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DependencyFlags = DependencyFlags.ByRegionBit,
        };

        var attachments = stackalloc AttachmentDescription[] { msaaColorAttachment, resolveAttachment };
        var renderPassInfo = new RenderPassCreateInfo
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 2,
            PAttachments = attachments,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency,
        };

        fixed (RenderPass* renderPass = &_renderPass)
        {
            Check(_vk.CreateRenderPass(_device, &renderPassInfo, null, renderPass));
        }
    }

    private unsafe void CreatePipelineLayouts()
    {
        if (_descriptorSetLayout.Handle is not 0 && _pipelineLayout.Handle is not 0 && _fontSampler.Handle is not 0 && _imageSampler.Handle is not 0)
        {
            return;
        }

        var samplerBinding = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            StageFlags = ShaderStageFlags.FragmentBit,
        };

        var descriptorLayoutInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &samplerBinding,
        };

        fixed (DescriptorSetLayout* layout = &_descriptorSetLayout)
        {
            Check(_vk.CreateDescriptorSetLayout(_device, &descriptorLayoutInfo, null, layout));
        }

        var pushConstantRange = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.VertexBit,
            Offset = 0,
            Size = (uint)(sizeof(float) * 4),
        };

        var pipelineLayoutInfo = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushConstantRange,
        };

        var setLayouts = stackalloc DescriptorSetLayout[1];
        setLayouts[0] = _descriptorSetLayout;
        pipelineLayoutInfo.PSetLayouts = setLayouts;

        fixed (PipelineLayout* pipelineLayout = &_pipelineLayout)
        {
            Check(_vk.CreatePipelineLayout(_device, &pipelineLayoutInfo, null, pipelineLayout));
        }

        var fontSamplerInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = _options.FontLinearSampling ? Filter.Linear : Filter.Nearest,
            MinFilter = _options.FontLinearSampling ? Filter.Linear : Filter.Nearest,
            MipmapMode = SamplerMipmapMode.Nearest,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            MinLod = 0,
            MaxLod = 0,
            AnisotropyEnable = false,
            MaxAnisotropy = 1,
        };

        fixed (Sampler* sampler = &_fontSampler)
        {
            Check(_vk.CreateSampler(_device, &fontSamplerInfo, null, sampler));
        }

        var imageSamplerInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            MinLod = 0,
            MaxLod = 0,
            AnisotropyEnable = false,
            MaxAnisotropy = 1,
        };

        fixed (Sampler* sampler = &_imageSampler)
        {
            Check(_vk.CreateSampler(_device, &imageSamplerInfo, null, sampler));
        }
    }

    private unsafe void CreateGraphicsPipeline()
    {
        _vertexShaderModule = CreateShaderModule(VertexShaderSpirv);
        _fragmentShaderModule = CreateShaderModule(FragmentShaderSpirv);
        _rectPrimitiveVertexShaderModule = CreateShaderModule(RectPrimitiveVertexShaderSpirv);
        _rectPrimitiveFragmentShaderModule = CreateShaderModule(RectPrimitiveFragmentShaderSpirv);
        _circlePrimitiveVertexShaderModule = CreateShaderModule(CirclePrimitiveVertexShaderSpirv);
        _circlePrimitiveFragmentShaderModule = CreateShaderModule(CirclePrimitiveFragmentShaderSpirv);

        var entryPoint = VulkanMarshaling.StringToPtr("main");

        try
        {
            var vertexStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = _vertexShaderModule,
                PName = entryPoint,
            };

            var fragmentStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = _fragmentShaderModule,
                PName = entryPoint,
            };

            var stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = vertexStage;
            stages[1] = fragmentStage;

            var bindingDescription = new VertexInputBindingDescription
            {
                Binding = 0,
                Stride = (uint)sizeof(UiVertex),
                InputRate = VertexInputRate.Vertex,
            };

            var attributeDescriptions = stackalloc VertexInputAttributeDescription[3];
            const uint positionOffset = 0;
            const uint uvOffset = 8;
            const uint colorOffset = 16;

            attributeDescriptions[0] = new VertexInputAttributeDescription
            {
                Location = 0,
                Binding = 0,
                Format = Format.R32G32Sfloat,
                Offset = positionOffset,
            };
            attributeDescriptions[1] = new VertexInputAttributeDescription
            {
                Location = 1,
                Binding = 0,
                Format = Format.R32G32Sfloat,
                Offset = uvOffset,
            };
            attributeDescriptions[2] = new VertexInputAttributeDescription
            {
                Location = 2,
                Binding = 0,
                Format = Format.R8G8B8A8Unorm,
                Offset = colorOffset,
            };

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &bindingDescription,
                VertexAttributeDescriptionCount = 3,
                PVertexAttributeDescriptions = attributeDescriptions,
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = false,
            };

            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1,
            };

            var rasterizer = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1.0f,
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.CounterClockwise,
                DepthBiasEnable = false,
                DepthBiasClamp = 0f,
                DepthBiasConstantFactor = 0f,
                DepthBiasSlopeFactor = 0f,
            };

            var multisampling = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = _msaaSampleCount,
                SampleShadingEnable = false,
                MinSampleShading = 1.0f,
                PSampleMask = null,
                AlphaToCoverageEnable = false,
                AlphaToOneEnable = false,
            };

            var depthStencil = new PipelineDepthStencilStateCreateInfo
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = false,
                DepthWriteEnable = false,
                DepthCompareOp = CompareOp.Always,
                DepthBoundsTestEnable = false,
                StencilTestEnable = false,
            };

            var colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                AlphaBlendOp = BlendOp.Add,
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                LogicOp = LogicOp.Copy,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment,
            };

            const uint dynamicStateCount = 2u;
            var dynamicStates = stackalloc DynamicState[2];
            dynamicStates[0] = DynamicState.Viewport;
            dynamicStates[1] = DynamicState.Scissor;

            var dynamicState = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = dynamicStateCount,
                PDynamicStates = dynamicStates,
            };

            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = stages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicState,
                Layout = _pipelineLayout,
                RenderPass = _renderPass,
                Subpass = 0,
            };

            fixed (Pipeline* pipeline = &_graphicsPipeline)
            {
                Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &pipelineInfo, null, pipeline));
            }

            // Create subpixel text pipeline with dual-source blending
            _subpixelFragmentShaderModule = CreateShaderModule(SubpixelFragmentShaderSpirv);

            var subpixelFragmentStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = _subpixelFragmentShaderModule,
                PName = entryPoint,
            };

            var subpixelStages = stackalloc PipelineShaderStageCreateInfo[2];
            subpixelStages[0] = vertexStage;
            subpixelStages[1] = subpixelFragmentStage;

            var subpixelBlendAttachment = new PipelineColorBlendAttachmentState
            {
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.One,
                DstColorBlendFactor = BlendFactor.OneMinusSrc1Color,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.OneMinusSrc1Alpha,
                AlphaBlendOp = BlendOp.Add,
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            };

            var subpixelColorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                LogicOp = LogicOp.Copy,
                AttachmentCount = 1,
                PAttachments = &subpixelBlendAttachment,
            };

            var subpixelPipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = subpixelStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &subpixelColorBlending,
                PDynamicState = &dynamicState,
                Layout = _pipelineLayout,
                RenderPass = _renderPass,
                Subpass = 0,
            };

            fixed (Pipeline* pipeline = &_subpixelPipeline)
            {
                Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &subpixelPipelineInfo, null, pipeline));
            }

            var rectPrimitiveVertexStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = _rectPrimitiveVertexShaderModule,
                PName = entryPoint,
            };

            var rectPrimitiveFragmentStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = _rectPrimitiveFragmentShaderModule,
                PName = entryPoint,
            };

            var rectPrimitiveStages = stackalloc PipelineShaderStageCreateInfo[2];
            rectPrimitiveStages[0] = rectPrimitiveVertexStage;
            rectPrimitiveStages[1] = rectPrimitiveFragmentStage;

            var rectPrimitiveBindingDescription = new VertexInputBindingDescription
            {
                Binding = 1,
                Stride = (uint)sizeof(RectPrimitiveInstance),
                InputRate = VertexInputRate.Instance,
            };

            var rectPrimitiveAttributeDescriptions = stackalloc VertexInputAttributeDescription[2];
            rectPrimitiveAttributeDescriptions[0] = new VertexInputAttributeDescription
            {
                Location = 0,
                Binding = 1,
                Format = Format.R32G32B32A32Sfloat,
                Offset = 0,
            };
            rectPrimitiveAttributeDescriptions[1] = new VertexInputAttributeDescription
            {
                Location = 1,
                Binding = 1,
                Format = Format.R8G8B8A8Unorm,
                Offset = 16,
            };

            var rectPrimitiveVertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &rectPrimitiveBindingDescription,
                VertexAttributeDescriptionCount = 2,
                PVertexAttributeDescriptions = rectPrimitiveAttributeDescriptions,
            };

            var rectPrimitivePipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = rectPrimitiveStages,
                PVertexInputState = &rectPrimitiveVertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicState,
                Layout = _pipelineLayout,
                RenderPass = _renderPass,
                Subpass = 0,
            };

            fixed (Pipeline* pipeline = &_rectPrimitivePipeline)
            {
                Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &rectPrimitivePipelineInfo, null, pipeline));
            }

            var circlePrimitiveVertexStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = _circlePrimitiveVertexShaderModule,
                PName = entryPoint,
            };

            var circlePrimitiveFragmentStage = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = _circlePrimitiveFragmentShaderModule,
                PName = entryPoint,
            };

            var circlePrimitiveStages = stackalloc PipelineShaderStageCreateInfo[2];
            circlePrimitiveStages[0] = circlePrimitiveVertexStage;
            circlePrimitiveStages[1] = circlePrimitiveFragmentStage;

            var circlePrimitiveBindingDescription = new VertexInputBindingDescription
            {
                Binding = 1,
                Stride = (uint)sizeof(CirclePrimitiveInstance),
                InputRate = VertexInputRate.Instance,
            };

            var circlePrimitiveAttributeDescriptions = stackalloc VertexInputAttributeDescription[4];
            circlePrimitiveAttributeDescriptions[0] = new VertexInputAttributeDescription
            {
                Location = 0,
                Binding = 1,
                Format = Format.R32G32Sfloat,
                Offset = 0,
            };
            circlePrimitiveAttributeDescriptions[1] = new VertexInputAttributeDescription
            {
                Location = 1,
                Binding = 1,
                Format = Format.R32Sfloat,
                Offset = 8,
            };
            circlePrimitiveAttributeDescriptions[2] = new VertexInputAttributeDescription
            {
                Location = 2,
                Binding = 1,
                Format = Format.R8G8B8A8Unorm,
                Offset = 12,
            };
            circlePrimitiveAttributeDescriptions[3] = new VertexInputAttributeDescription
            {
                Location = 3,
                Binding = 1,
                Format = Format.R32Uint,
                Offset = 16,
            };

            var circlePrimitiveVertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &circlePrimitiveBindingDescription,
                VertexAttributeDescriptionCount = 4,
                PVertexAttributeDescriptions = circlePrimitiveAttributeDescriptions,
            };

            var circlePrimitivePipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = circlePrimitiveStages,
                PVertexInputState = &circlePrimitiveVertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicState,
                Layout = _pipelineLayout,
                RenderPass = _renderPass,
                Subpass = 0,
            };

            fixed (Pipeline* pipeline = &_circlePrimitivePipeline)
            {
                Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &circlePrimitivePipelineInfo, null, pipeline));
            }
        }
        finally
        {
            VulkanMarshaling.Free((nint)entryPoint);
        }
    }

    private unsafe void CreatePipelineCache()
    {
        if (_pipelineCache.Handle is not 0)
        {
            return;
        }

        byte[]? data = null;
        if (File.Exists(_pipelineCachePath))
        {
            data = File.ReadAllBytes(_pipelineCachePath);
            if (data.Length > 0)
            {
                _pipelineCacheSize = (nuint)data.Length;
                _pipelineCacheHash = ComputeHash(data);
            }
        }

        fixed (byte* dataPtr = data)
        {
            var cacheInfo = new PipelineCacheCreateInfo
            {
                SType = StructureType.PipelineCacheCreateInfo,
                InitialDataSize = (nuint)(data?.Length ?? 0),
                PInitialData = dataPtr,
            };

            fixed (PipelineCache* cache = &_pipelineCache)
            {
                Check(_vk.CreatePipelineCache(_device, &cacheInfo, null, cache));
            }
        }
    }

    private unsafe void SavePipelineCache()
    {
        if (_pipelineCache.Handle is 0)
        {
            return;
        }

        nuint size = 0;
        Check(_vk.GetPipelineCacheData(_device, _pipelineCache, &size, null));
        if (size is 0)
        {
            return;
        }

        var data = new byte[(int)size];
        fixed (byte* dataPtr = data)
        {
            Check(_vk.GetPipelineCacheData(_device, _pipelineCache, &size, dataPtr));
        }

        var hash = ComputeHash(data);
        if (_pipelineCacheSize == size && _pipelineCacheHash == hash)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_pipelineCachePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(_pipelineCachePath, data);
        _pipelineCacheSize = size;
        _pipelineCacheHash = hash;
    }

    private static ulong ComputeHash(ReadOnlySpan<byte> data)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        for (var i = 0; i < data.Length; i++)
        {
            hash ^= data[i];
            hash *= prime;
        }
        return hash;
    }

    private void DestroyPipelineCache()
    {
        if (_pipelineCache.Handle is 0)
        {
            return;
        }

        _vk.DestroyPipelineCache(_device, _pipelineCache, null);
        _pipelineCache = default;
    }

    private unsafe void CreateDescriptorPool()
    {
        if (_descriptorPool.Handle is not 0)
        {
            return;
        }

        const uint poolSize = 1000;
        const int poolSizeCount = 11;
        var poolSizes = stackalloc DescriptorPoolSize[poolSizeCount];
        poolSizes[0] = new DescriptorPoolSize(DescriptorType.Sampler, poolSize);
        poolSizes[1] = new DescriptorPoolSize(DescriptorType.CombinedImageSampler, poolSize);
        poolSizes[2] = new DescriptorPoolSize(DescriptorType.SampledImage, poolSize);
        poolSizes[3] = new DescriptorPoolSize(DescriptorType.StorageImage, poolSize);
        poolSizes[4] = new DescriptorPoolSize(DescriptorType.UniformTexelBuffer, poolSize);
        poolSizes[5] = new DescriptorPoolSize(DescriptorType.StorageTexelBuffer, poolSize);
        poolSizes[6] = new DescriptorPoolSize(DescriptorType.UniformBuffer, poolSize);
        poolSizes[7] = new DescriptorPoolSize(DescriptorType.StorageBuffer, poolSize);
        poolSizes[8] = new DescriptorPoolSize(DescriptorType.UniformBufferDynamic, poolSize);
        poolSizes[9] = new DescriptorPoolSize(DescriptorType.StorageBufferDynamic, poolSize);
        poolSizes[10] = new DescriptorPoolSize(DescriptorType.InputAttachment, poolSize);

        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = (uint)poolSizeCount,
            PPoolSizes = poolSizes,
            MaxSets = poolSize * poolSizeCount,
            Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
        };

        fixed (DescriptorPool* pool = &_descriptorPool)
        {
            Check(_vk.CreateDescriptorPool(_device, &poolInfo, null, pool));
        }
    }

    private void ApplyTextureUpdates(ReadOnlySpan<UiTextureUpdate> updates)
    {
        if (updates.Length is 0)
        {
            return;
        }

        BeginUploadBatch();
        try
        {
            var lastId = default(UiTextureId);
            var hasLast = false;
            var lastKind = UiTextureUpdateKind.Create;
            var lastHasExisting = false;
            var lastExisting = default(TextureResource);
            for (var i = 0; i < updates.Length; i++)
            {
                var update = updates[i];
                var sameId = hasLast && update.TextureId.Equals(lastId);
                if (sameId && update.Kind == lastKind)
                {
                    continue;
                }

                var hasExisting = lastHasExisting;
                var existing = lastExisting;
                if (!sameId)
                {
                    hasExisting = _textures.TryGetValue(update.TextureId, out existing);
                    lastHasExisting = hasExisting;
                    lastExisting = existing;
                }

                hasLast = true;
                lastId = update.TextureId;
                lastKind = update.Kind;
                var expectedFormat = ToVkFormat(update.Format);

                switch (update.Kind)
                {
                    case UiTextureUpdateKind.Create:
                        if (hasExisting)
                        {
                            if (existing.Width != update.Width
                                || existing.Height != update.Height
                                || existing.Format != expectedFormat)
                            {
                                DestroyTexture(update.TextureId);
                                lastHasExisting = false;
                                CreateOrUpdateTexture(update, true);
                                _textures.TryGetValue(update.TextureId, out lastExisting);
                                lastHasExisting = true;
                            }
                            else
                            {
                                CreateOrUpdateTexture(update, false);
                                lastHasExisting = true;
                            }
                        }
                        else
                        {
                            CreateOrUpdateTexture(update, true);
                            _textures.TryGetValue(update.TextureId, out lastExisting);
                            lastHasExisting = true;
                        }
                        break;
                    case UiTextureUpdateKind.Update:
                        if (hasExisting)
                        {
                            if (existing.Width != update.Width
                                || existing.Height != update.Height
                                || existing.Format != expectedFormat)
                            {
                                DestroyTexture(update.TextureId);
                                lastHasExisting = false;
                                CreateOrUpdateTexture(update, true);
                                _textures.TryGetValue(update.TextureId, out lastExisting);
                                lastHasExisting = true;
                            }
                            else
                            {
                                CreateOrUpdateTexture(update, false);
                                lastHasExisting = true;
                            }
                        }
                        else
                        {
                            CreateOrUpdateTexture(update, true);
                            _textures.TryGetValue(update.TextureId, out lastExisting);
                            lastHasExisting = true;
                        }
                        break;
                    case UiTextureUpdateKind.Destroy:
                        DestroyTexture(update.TextureId);
                        lastHasExisting = false;
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported texture update kind: {update.Kind}.");
                }
            }
        }
        finally
        {
            EndUploadBatch();
        }
    }

    private void EnsureVertexBufferCapacity(int frame, int vertexCount)
    {
        var frameData = _frames[frame];
        var renderBuffers = frameData.RenderBuffers;
        var requiredSize = (nuint)(vertexCount * sizeof(UiVertex));
        if (renderBuffers.VertexBuffer.Handle is not 0 && renderBuffers.VertexSize >= requiredSize)
        {
            return;
        }

        if (renderBuffers.VertexBuffer.Handle is not 0)
        {
            if (renderBuffers.VertexMappedPtr is not null)
            {
                _vk.UnmapMemory(_device, renderBuffers.VertexMemory);
                renderBuffers.VertexMappedPtr = null;
            }

            QueueBufferDestroy(renderBuffers.VertexBuffer, renderBuffers.VertexMemory);
            renderBuffers.VertexBuffer = default;
            renderBuffers.VertexMemory = default;
        }

        var newSize = requiredSize;
        if (renderBuffers.VertexSize == 0)
        {
            newSize = Math.Max(newSize, (nuint)(DefaultVertexBufferCapacity * sizeof(UiVertex)));
        }
        else
        {
            var padded = requiredSize + (nuint)(VertexBufferGrowthPadding * sizeof(UiVertex));
            newSize = Math.Max(newSize, padded);
        }

        CreateBuffer(
            newSize,
            BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out renderBuffers.VertexBuffer,
            out renderBuffers.VertexMemory
        );

        void* mapped;
        Check(_vk.MapMemory(_device, renderBuffers.VertexMemory, 0, newSize, 0, &mapped));
        renderBuffers.VertexMappedPtr = mapped;

        renderBuffers.VertexSize = newSize;
        frameData.RenderBuffers = renderBuffers;
        _frames[frame] = frameData;

    }

    private void EnsureIndexBufferCapacity(int frame, int indexCount)
    {
        var frameData = _frames[frame];
        var renderBuffers = frameData.RenderBuffers;
        var requiredSize = (nuint)(indexCount * sizeof(uint));
        if (renderBuffers.IndexBuffer.Handle is not 0 && renderBuffers.IndexSize >= requiredSize)
        {
            return;
        }

        if (renderBuffers.IndexBuffer.Handle is not 0)
        {
            if (renderBuffers.IndexMappedPtr is not null)
            {
                _vk.UnmapMemory(_device, renderBuffers.IndexMemory);
                renderBuffers.IndexMappedPtr = null;
            }

            QueueBufferDestroy(renderBuffers.IndexBuffer, renderBuffers.IndexMemory);
            renderBuffers.IndexBuffer = default;
            renderBuffers.IndexMemory = default;
        }

        var newSize = requiredSize;
        if (renderBuffers.IndexSize == 0)
        {
            newSize = Math.Max(newSize, (nuint)(DefaultIndexBufferCapacity * sizeof(uint)));
        }
        else
        {
            var padded = requiredSize + (nuint)(IndexBufferGrowthPadding * sizeof(uint));
            newSize = Math.Max(newSize, padded);
        }

        CreateBuffer(
            newSize,
            BufferUsageFlags.IndexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out renderBuffers.IndexBuffer,
            out renderBuffers.IndexMemory
        );

        void* mapped;
        Check(_vk.MapMemory(_device, renderBuffers.IndexMemory, 0, newSize, 0, &mapped));
        renderBuffers.IndexMappedPtr = mapped;

        renderBuffers.IndexSize = newSize;
        frameData.RenderBuffers = renderBuffers;
        _frames[frame] = frameData;

    }

    private void EnsureRectPrimitiveBufferCapacity(int frame, int primitiveCount)
    {
        if (primitiveCount is 0)
        {
            return;
        }

        var frameData = _frames[frame];
        var renderBuffers = frameData.RenderBuffers;
        var requiredSize = (nuint)(primitiveCount * sizeof(RectPrimitiveInstance));
        if (renderBuffers.RectPrimitiveBuffer.Handle is not 0 && renderBuffers.RectPrimitiveSize >= requiredSize)
        {
            return;
        }

        if (renderBuffers.RectPrimitiveBuffer.Handle is not 0)
        {
            if (renderBuffers.RectPrimitiveMappedPtr is not null)
            {
                _vk.UnmapMemory(_device, renderBuffers.RectPrimitiveMemory);
                renderBuffers.RectPrimitiveMappedPtr = null;
            }

            QueueBufferDestroy(renderBuffers.RectPrimitiveBuffer, renderBuffers.RectPrimitiveMemory);
            renderBuffers.RectPrimitiveBuffer = default;
            renderBuffers.RectPrimitiveMemory = default;
        }

        var newSize = requiredSize;
        if (renderBuffers.RectPrimitiveSize is 0)
        {
            newSize = Math.Max(newSize, (nuint)(DefaultPrimitiveInstanceBufferCapacity * sizeof(RectPrimitiveInstance)));
        }
        else
        {
            var padded = requiredSize + (nuint)(PrimitiveInstanceBufferGrowthPadding * sizeof(RectPrimitiveInstance));
            newSize = Math.Max(newSize, padded);
        }

        CreateBuffer(
            newSize,
            BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out renderBuffers.RectPrimitiveBuffer,
            out renderBuffers.RectPrimitiveMemory);

        void* mapped;
        Check(_vk.MapMemory(_device, renderBuffers.RectPrimitiveMemory, 0, newSize, 0, &mapped));
        renderBuffers.RectPrimitiveMappedPtr = mapped;
        renderBuffers.RectPrimitiveSize = newSize;
        frameData.RenderBuffers = renderBuffers;
        _frames[frame] = frameData;
    }

    private void EnsureCirclePrimitiveBufferCapacity(int frame, int primitiveCount)
    {
        if (primitiveCount is 0)
        {
            return;
        }

        var frameData = _frames[frame];
        var renderBuffers = frameData.RenderBuffers;
        var requiredSize = (nuint)(primitiveCount * sizeof(CirclePrimitiveInstance));
        if (renderBuffers.CirclePrimitiveBuffer.Handle is not 0 && renderBuffers.CirclePrimitiveSize >= requiredSize)
        {
            return;
        }

        if (renderBuffers.CirclePrimitiveBuffer.Handle is not 0)
        {
            if (renderBuffers.CirclePrimitiveMappedPtr is not null)
            {
                _vk.UnmapMemory(_device, renderBuffers.CirclePrimitiveMemory);
                renderBuffers.CirclePrimitiveMappedPtr = null;
            }

            QueueBufferDestroy(renderBuffers.CirclePrimitiveBuffer, renderBuffers.CirclePrimitiveMemory);
            renderBuffers.CirclePrimitiveBuffer = default;
            renderBuffers.CirclePrimitiveMemory = default;
        }

        var newSize = requiredSize;
        if (renderBuffers.CirclePrimitiveSize is 0)
        {
            newSize = Math.Max(newSize, (nuint)(DefaultPrimitiveInstanceBufferCapacity * sizeof(CirclePrimitiveInstance)));
        }
        else
        {
            var padded = requiredSize + (nuint)(PrimitiveInstanceBufferGrowthPadding * sizeof(CirclePrimitiveInstance));
            newSize = Math.Max(newSize, padded);
        }

        CreateBuffer(
            newSize,
            BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out renderBuffers.CirclePrimitiveBuffer,
            out renderBuffers.CirclePrimitiveMemory);

        void* mapped;
        Check(_vk.MapMemory(_device, renderBuffers.CirclePrimitiveMemory, 0, newSize, 0, &mapped));
        renderBuffers.CirclePrimitiveMappedPtr = mapped;
        renderBuffers.CirclePrimitiveSize = newSize;
        frameData.RenderBuffers = renderBuffers;
        _frames[frame] = frameData;
    }

    private void DestroyGeometryBuffers()
    {
        DestroyStaticGeometryBuffers();

        for (var i = 0; i < _frames.Length; i++)
        {
            var frame = _frames[i];
            if (frame is null)
            {
                continue;
            }

            var renderBuffers = frame.RenderBuffers;

            if (renderBuffers.VertexBuffer.Handle is not 0)
            {
                if (renderBuffers.VertexMappedPtr is not null)
                {
                    _vk.UnmapMemory(_device, renderBuffers.VertexMemory);
                    renderBuffers.VertexMappedPtr = null;
                }

                DestroyBufferResource(new BufferResource(renderBuffers.VertexBuffer, renderBuffers.VertexMemory));
                renderBuffers.VertexBuffer = default;
                renderBuffers.VertexMemory = default;
                renderBuffers.VertexSize = 0;
            }

            if (renderBuffers.IndexBuffer.Handle is not 0)
            {
                if (renderBuffers.IndexMappedPtr is not null)
                {
                    _vk.UnmapMemory(_device, renderBuffers.IndexMemory);
                    renderBuffers.IndexMappedPtr = null;
                }

                DestroyBufferResource(new BufferResource(renderBuffers.IndexBuffer, renderBuffers.IndexMemory));
                renderBuffers.IndexBuffer = default;
                renderBuffers.IndexMemory = default;
                renderBuffers.IndexSize = 0;
            }

            if (renderBuffers.RectPrimitiveBuffer.Handle is not 0)
            {
                if (renderBuffers.RectPrimitiveMappedPtr is not null)
                {
                    _vk.UnmapMemory(_device, renderBuffers.RectPrimitiveMemory);
                    renderBuffers.RectPrimitiveMappedPtr = null;
                }

                DestroyBufferResource(new BufferResource(renderBuffers.RectPrimitiveBuffer, renderBuffers.RectPrimitiveMemory));
                renderBuffers.RectPrimitiveBuffer = default;
                renderBuffers.RectPrimitiveMemory = default;
                renderBuffers.RectPrimitiveSize = 0;
            }

            if (renderBuffers.CirclePrimitiveBuffer.Handle is not 0)
            {
                if (renderBuffers.CirclePrimitiveMappedPtr is not null)
                {
                    _vk.UnmapMemory(_device, renderBuffers.CirclePrimitiveMemory);
                    renderBuffers.CirclePrimitiveMappedPtr = null;
                }

                DestroyBufferResource(new BufferResource(renderBuffers.CirclePrimitiveBuffer, renderBuffers.CirclePrimitiveMemory));
                renderBuffers.CirclePrimitiveBuffer = default;
                renderBuffers.CirclePrimitiveMemory = default;
                renderBuffers.CirclePrimitiveSize = 0;
            }

            frame.RenderBuffers = renderBuffers;

            _frames[i] = frame;
        }
    }

    private void QueueBufferDestroy(VkBuffer buffer, DeviceMemory memory)
    {
        if (buffer.Handle is 0 || memory.Handle is 0)
        {
            return;
        }

        if (_frames.Length == 0)
        {
            DestroyBufferResource(new BufferResource(buffer, memory));
            return;
        }

        var frameCount = _frames.Length;
        var frame = _frameIndex % frameCount;
        var destroyFrame = _frameIndex + frameCount;
        _frames[frame].PendingBufferDestroys.Add(new PendingBufferDestroy(new BufferResource(buffer, memory), destroyFrame));
    }

    private void DestroyBufferResource(BufferResource resource)
    {
        if (resource.Buffer.Handle is not 0)
        {
            _vk.DestroyBuffer(_device, resource.Buffer, null);
        }

        if (resource.Memory.Handle is not 0)
        {
            _vk.FreeMemory(_device, resource.Memory, null);
        }
    }

    private void FlushPendingBufferDestroys(int frame)
    {
        if (_frames.Length == 0)
        {
            return;
        }

        var list = _frames[frame].PendingBufferDestroys;
        if (list.Count == 0)
        {
            return;
        }

        var hasDueDestroy = false;
        for (var i = 0; i < list.Count; i++)
        {
            if (_frameIndex >= list[i].DestroyFrame)
            {
                hasDueDestroy = true;
                break;
            }
        }

        if (!hasDueDestroy)
        {
            return;
        }

        WaitForAllInFlightFrameFences();

        var write = 0;
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (_frameIndex >= entry.DestroyFrame)
            {
                DestroyBufferResource(entry.Resource);
                continue;
            }

            list[write++] = entry;
        }

        if (write < list.Count)
        {
            list.RemoveRange(write, list.Count - write);
        }
    }

    private void FlushPendingBufferDestroysAll()
    {
        if (_frames.Length == 0)
        {
            return;
        }

        WaitForAllInFlightFrameFences();

        for (var i = 0; i < _frames.Length; i++)
        {
            var list = _frames[i].PendingBufferDestroys;
            foreach (var entry in list)
            {
                DestroyBufferResource(entry.Resource);
            }
            list.Clear();
        }
    }

    private void CreateOrUpdateTexture(UiTextureUpdate update, bool isCreate)
    {
        if (update.Width <= 0 || update.Height <= 0)
        {
            throw new InvalidOperationException("Texture size must be positive.");
        }

        var expectedLength = update.Width * update.Height * 4;
        if (update.RgbaPixels.Length != expectedLength)
        {
            throw new InvalidOperationException("Texture RGBA payload size does not match width/height.");
        }

        if (isCreate)
        {
            if (_textures.ContainsKey(update.TextureId))
            {
                throw new InvalidOperationException("Texture already exists.");
            }

            var resource = CreateTextureResource(update, ImageLayout.Undefined);
            _textures[update.TextureId] = resource;
            return;
        }

        if (!_textures.TryGetValue(update.TextureId, out var existing))
        {
            throw new InvalidOperationException("Texture update requested for missing texture.");
        }

        if (existing.Width != update.Width || existing.Height != update.Height || existing.Format != ToVkFormat(update.Format))
        {
            throw new InvalidOperationException("Texture update size/format mismatch.");
        }

        UploadTextureData(existing.Image, update, ImageLayout.ShaderReadOnlyOptimal);
    }

    private TextureResource CreateTextureResource(UiTextureUpdate update, ImageLayout initialLayout)
    {
        var format = ToVkFormat(update.Format);
        CreateImage(
            update.Width,
            update.Height,
            format,
            ImageTiling.Optimal,
            ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            MemoryPropertyFlags.DeviceLocalBit,
            out var image,
            out var memory
        );

        UploadTextureData(image, update, initialLayout);

        var view = CreateImageView(image, format);
        var isFontTexture = IsFontTextureId(update.TextureId);
        var descriptorSet = AllocateTextureDescriptorSet(view, isFontTexture);

        return new TextureResource(image, memory, view, descriptorSet, update.Width, update.Height, format);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsFontTextureId(UiTextureId textureId)
    {
        var baseFontTextureId = _options.FontTextureId.Value is 0 ? new UiTextureId(1) : _options.FontTextureId;
        return textureId.Equals(baseFontTextureId) || textureId.Value >= 1_100_000_000;
    }

    private void DestroyTextureResources()
    {
        if (_textures.Count is 0)
        {
            return;
        }

        var keys = new List<UiTextureId>(_textures.Keys);
        foreach (var key in keys)
        {
            DestroyTextureImmediate(key);
        }

    }

    private void DestroyTexture(UiTextureId textureId)
    {
        if (!_textures.TryGetValue(textureId, out var resource))
        {
            return;
        }

        _textures.Remove(textureId);

        if (_frames.Length == 0)
        {
            DestroyTextureResource(resource);
            return;
        }

        var frameCount = _frames.Length;
        var frame = _frameIndex % frameCount;
        var destroyFrame = _frameIndex + frameCount;
        _frames[frame].PendingTextureDestroys.Add(new PendingTextureDestroy(resource, destroyFrame));
    }

    private void DestroyTextureImmediate(UiTextureId textureId)
    {
        if (!_textures.TryGetValue(textureId, out var resource))
        {
            return;
        }

        _textures.Remove(textureId);
        DestroyTextureResource(resource);
    }

    private void DestroyTextureResource(TextureResource resource)
    {
        if (_descriptorPool.Handle is not 0 && resource.DescriptorSet.Handle is not 0)
        {
            var descriptorSet = resource.DescriptorSet;
            _vk.FreeDescriptorSets(_device, _descriptorPool, 1, &descriptorSet);
        }

        if (resource.View.Handle is not 0)
        {
            _vk.DestroyImageView(_device, resource.View, null);
        }

        if (resource.Image.Handle is not 0)
        {
            _vk.DestroyImage(_device, resource.Image, null);
        }

        if (resource.Memory.Handle is not 0)
        {
            _vk.FreeMemory(_device, resource.Memory, null);
        }
    }

    private void FlushPendingTextureDestroys(int frame)
    {
        if (_frames.Length == 0)
        {
            return;
        }

        var list = _frames[frame].PendingTextureDestroys;
        if (list.Count == 0)
        {
            return;
        }

        var hasDueDestroy = false;
        for (var i = 0; i < list.Count; i++)
        {
            if (_frameIndex >= list[i].DestroyFrame)
            {
                hasDueDestroy = true;
                break;
            }
        }

        if (!hasDueDestroy)
        {
            return;
        }

        WaitForAllInFlightFrameFences();

        var write = 0;
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (_frameIndex >= entry.DestroyFrame)
            {
                DestroyTextureResource(entry.Resource);
                continue;
            }

            list[write++] = entry;
        }

        if (write < list.Count)
        {
            list.RemoveRange(write, list.Count - write);
        }
    }

    private void FlushPendingTextureDestroysAll()
    {
        if (_frames.Length == 0)
        {
            return;
        }

        WaitForAllInFlightFrameFences();

        for (var i = 0; i < _frames.Length; i++)
        {
            var list = _frames[i].PendingTextureDestroys;
            foreach (var entry in list)
            {
                DestroyTextureResource(entry.Resource);
            }
            list.Clear();
        }
    }


    private void UploadTextureData(Image image, UiTextureUpdate update, ImageLayout initialLayout)
    {
        var expectedBytes = GetExpectedTextureDataSize(update.Format, update.Width, update.Height);
        var source = update.RgbaPixels.Span;
        byte[]? normalizedPixels = null;
        ReadOnlySpan<byte> data;

        if ((nuint)source.Length == expectedBytes)
        {
            data = source;
        }
        else
        {
            normalizedPixels = new byte[(int)expectedBytes];
            source[..Math.Min(source.Length, normalizedPixels.Length)].CopyTo(normalizedPixels);
            data = normalizedPixels;
        }

        var bufferSize = expectedBytes;
        EnsureStagingBuffer(bufferSize);

        var commandBuffer = BeginSingleTimeCommands();

        data.CopyTo(new Span<byte>(_stagingMappedPtr, data.Length));

        try
        {
            TransitionImageLayout(commandBuffer, image, initialLayout, ImageLayout.TransferDstOptimal);

            var region = new BufferImageCopy
            {
                BufferOffset = 0,
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
                ImageOffset = new Offset3D(0, 0, 0),
                ImageExtent = new Extent3D((uint)update.Width, (uint)update.Height, 1),
            };

            _vk.CmdCopyBufferToImage(commandBuffer, _stagingBuffer, image, ImageLayout.TransferDstOptimal, 1, &region);
            TransitionImageLayout(commandBuffer, image, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);
        }
        finally
        {
            EndSingleTimeCommands(commandBuffer);
        }
    }

    private static nuint GetExpectedTextureDataSize(UiTextureFormat format, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var bytesPerPixel = format switch
        {
            UiTextureFormat.Rgba8Unorm => 4,
            UiTextureFormat.Rgba8Srgb => 4,
            _ => throw new InvalidOperationException($"Unsupported texture format: {format}.")
        };

        return (nuint)width * (nuint)height * (nuint)bytesPerPixel;
    }

    private unsafe void WaitForAllInFlightFrameFences()
    {
        if (_frames.Length == 0)
        {
            return;
        }

        for (var i = 0; i < _frames.Length; i++)
        {
            var frame = _frames[i];
            if (frame is null || frame.InFlight.Handle is 0)
            {
                continue;
            }

            var fence = frame.InFlight;
            Check(_vk.WaitForFences(_device, 1, &fence, true, ulong.MaxValue));
        }
    }

    private unsafe void CopyBuffer(VkBuffer sourceBuffer, VkBuffer destinationBuffer, nuint size)
    {
        if (size is 0)
        {
            return;
        }

        var commandBuffer = BeginSingleTimeCommands();
        try
        {
            var region = new BufferCopy
            {
                SrcOffset = 0,
                DstOffset = 0,
                Size = size,
            };

            _vk.CmdCopyBuffer(commandBuffer, sourceBuffer, destinationBuffer, 1, &region);
        }
        finally
        {
            EndSingleTimeCommands(commandBuffer);
        }
    }

    private unsafe void EnsureStagingBuffer(nuint size)
    {
        if (_stagingBuffer.Handle is not 0 && _stagingBufferSize >= size)
        {
            return;
        }

        PrepareForStagingBufferRecreate();

        var previousSize = _stagingBufferSize;

        DestroyStagingBuffer();

        var targetSize = size;
        if (previousSize > 0)
        {
            targetSize = Math.Max(targetSize, previousSize * 2);
        }

        CreateBuffer(
            targetSize,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out _stagingBuffer,
            out _stagingMemory
        );
        _stagingBufferSize = targetSize;

        void* mapped;
        Check(_vk.MapMemory(_device, _stagingMemory, 0, targetSize, 0, &mapped));
        _stagingMappedPtr = mapped;
        _stagingMapped = true;
    }

    private unsafe void PrepareForStagingBufferRecreate()
    {
        if (_uploadBatchRecording && _uploadCommandBuffer.Handle is not 0)
        {
            Check(_vk.EndCommandBuffer(_uploadCommandBuffer));

            var commandBuffer = _uploadCommandBuffer;
            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer,
            };

            Check(_vk.QueueSubmit(_transferQueue, 1, &submitInfo, _uploadFence));
            _hasPendingUploadWork = true;
            _uploadBatchRecording = false;
        }

        WaitForPendingUploadWork();

        if (_uploadBatchDepth <= 0 || _uploadCommandBuffer.Handle is 0)
        {
            return;
        }

        fixed (Fence* fence = &_uploadFence)
        {
            Check(_vk.ResetFences(_device, 1, fence));
        }

        Check(_vk.ResetCommandBuffer(_uploadCommandBuffer, 0));

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        Check(_vk.BeginCommandBuffer(_uploadCommandBuffer, &beginInfo));
        _uploadBatchRecording = true;
    }

    private void DestroyStagingBuffer()
    {
        if (_stagingMapped)
        {
            _vk.UnmapMemory(_device, _stagingMemory);
            _stagingMapped = false;
            _stagingMappedPtr = null;
        }

        if (_stagingBuffer.Handle is not 0)
        {
            _vk.DestroyBuffer(_device, _stagingBuffer, null);
            _stagingBuffer = default;
        }

        if (_stagingMemory.Handle is not 0)
        {
            _vk.FreeMemory(_device, _stagingMemory, null);
            _stagingMemory = default;
        }

        _stagingBufferSize = 0;
    }

    private DescriptorSet AllocateTextureDescriptorSet(ImageView view, bool isFontTexture)
    {
        var layout = _descriptorSetLayout;
        var allocInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &layout,
        };

        DescriptorSet descriptorSet = default;
        Check(_vk.AllocateDescriptorSets(_device, &allocInfo, &descriptorSet));

        var imageInfo = new DescriptorImageInfo
        {
            Sampler = isFontTexture ? _fontSampler : _imageSampler,
            ImageView = view,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
        };

        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = descriptorSet,
            DstBinding = 0,
            DstArrayElement = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            PImageInfo = &imageInfo,
        };

        _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);
        return descriptorSet;
    }

    private unsafe void CreateBuffer(
        nuint size,
        BufferUsageFlags usage,
        MemoryPropertyFlags properties,
        out VkBuffer buffer,
        out DeviceMemory memory
    )
    {
        var queueFamilies = stackalloc uint[2];
        uint queueFamilyIndexCount = 0;
        uint* queueFamilyIndices = null;
        var sharingMode = SharingMode.Exclusive;
        if (_transferQueueFamily != _graphicsQueueFamily)
        {
            queueFamilies[0] = _graphicsQueueFamily;
            queueFamilies[1] = _transferQueueFamily;
            queueFamilyIndexCount = 2;
            queueFamilyIndices = queueFamilies;
            sharingMode = SharingMode.Concurrent;
        }

        var bufferInfo = stackalloc BufferCreateInfo[1];
        bufferInfo[0] = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = sharingMode,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = queueFamilyIndices,
        };

        var localBuffer = default(VkBuffer);
        Check(_vk.CreateBuffer(_device, bufferInfo, null, &localBuffer));
        buffer = localBuffer;

        _vk.GetBufferMemoryRequirements(_device, buffer, out var memRequirements);

        var allocInfo = stackalloc MemoryAllocateInfo[1];
        allocInfo[0] = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties),
        };

        var localMemory = default(DeviceMemory);
        Check(_vk.AllocateMemory(_device, allocInfo, null, &localMemory));
        memory = localMemory;
        Check(_vk.BindBufferMemory(_device, buffer, localMemory, 0));
    }

    private unsafe void CreateImage(
        int width,
        int height,
        Format format,
        ImageTiling tiling,
        ImageUsageFlags usage,
        MemoryPropertyFlags properties,
        out Image image,
        out DeviceMemory memory
    )
    {
        var queueFamilies = stackalloc uint[2];
        uint queueFamilyIndexCount = 0;
        uint* queueFamilyIndices = null;
        var sharingMode = SharingMode.Exclusive;
        if (_transferQueueFamily != _graphicsQueueFamily)
        {
            queueFamilies[0] = _graphicsQueueFamily;
            queueFamilies[1] = _transferQueueFamily;
            queueFamilyIndexCount = 2;
            queueFamilyIndices = queueFamilies;
            sharingMode = SharingMode.Concurrent;
        }

        var imageInfo = stackalloc ImageCreateInfo[1];
        imageInfo[0] = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D((uint)width, (uint)height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = tiling,
            InitialLayout = ImageLayout.Undefined,
            Usage = usage,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = sharingMode,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = queueFamilyIndices,
        };

        var localImage = default(Image);
        Check(_vk.CreateImage(_device, imageInfo, null, &localImage));
        image = localImage;

        _vk.GetImageMemoryRequirements(_device, image, out var memRequirements);

        var allocInfo = stackalloc MemoryAllocateInfo[1];
        allocInfo[0] = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties),
        };

        var localMemory = default(DeviceMemory);
        Check(_vk.AllocateMemory(_device, allocInfo, null, &localMemory));
        memory = localMemory;
        Check(_vk.BindImageMemory(_device, image, localMemory, 0));
    }

    private ImageView CreateImageView(Image image, Format format)
    {
        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        ImageView view = default;
        Check(_vk.CreateImageView(_device, &viewInfo, null, &view));
        return view;
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProperties);

        for (var i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << i)) == 0)
            {
                continue;
            }

            if ((memProperties.GetMemoryType((int)i).PropertyFlags & properties) == properties)
            {
                return (uint)i;
            }
        }

        throw new InvalidOperationException("Failed to find a suitable memory type.");
    }

    private void BeginUploadBatch()
    {
        if (!_uploadBatchingEnabled)
        {
            return;
        }

        _uploadBatchDepth++;
    }

    private void EndUploadBatch()
    {
        if (!_uploadBatchingEnabled)
        {
            return;
        }

        if (_uploadBatchDepth <= 0)
        {
            return;
        }

        _uploadBatchDepth--;
        if (_uploadBatchDepth != 0 || !_uploadBatchRecording)
        {
            return;
        }

        Check(_vk.EndCommandBuffer(_uploadCommandBuffer));

        var commandBuffer = _uploadCommandBuffer;
        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };

        Check(_vk.QueueSubmit(_transferQueue, 1, &submitInfo, _uploadFence));
        _hasPendingUploadWork = true;
        _uploadBatchRecording = false;
    }

    private CommandBuffer BeginSingleTimeCommands()
    {
        if (_uploadCommandBuffer.Handle is 0)
        {
            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                Level = CommandBufferLevel.Primary,
                CommandPool = _uploadCommandPool,
                CommandBufferCount = 1,
            };

            fixed (CommandBuffer* uploadBuffer = &_uploadCommandBuffer)
            {
                Check(_vk.AllocateCommandBuffers(_device, &allocInfo, uploadBuffer));
            }
        }

        if (_uploadBatchDepth > 0)
        {
            if (_uploadBatchRecording)
            {
                return _uploadCommandBuffer;
            }

            WaitForPendingUploadWork();

            fixed (Fence* fence = &_uploadFence)
            {
                Check(_vk.ResetFences(_device, 1, fence));
            }

            Check(_vk.ResetCommandBuffer(_uploadCommandBuffer, 0));

            var batchBeginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
            };

            Check(_vk.BeginCommandBuffer(_uploadCommandBuffer, &batchBeginInfo));
            _uploadBatchRecording = true;
            return _uploadCommandBuffer;
        }

        WaitForPendingUploadWork();

        fixed (Fence* fence = &_uploadFence)
        {
            Check(_vk.ResetFences(_device, 1, fence));
        }

        Check(_vk.ResetCommandBuffer(_uploadCommandBuffer, 0));

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        Check(_vk.BeginCommandBuffer(_uploadCommandBuffer, &beginInfo));
        return _uploadCommandBuffer;
    }

    private void EndSingleTimeCommands(CommandBuffer commandBuffer)
    {
        if (_uploadBatchDepth > 0)
        {
            return;
        }

        Check(_vk.EndCommandBuffer(commandBuffer));

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };

        Check(_vk.QueueSubmit(_transferQueue, 1, &submitInfo, _uploadFence));
        _hasPendingUploadWork = true;
    }


    private void TransitionImageLayout(CommandBuffer commandBuffer, Image image, ImageLayout oldLayout, ImageLayout newLayout)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        PipelineStageFlags sourceStage;
        PipelineStageFlags destinationStage;

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;
            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else if (oldLayout == ImageLayout.ShaderReadOnlyOptimal && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.ShaderReadBit;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;
            sourceStage = PipelineStageFlags.FragmentShaderBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;
            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.FragmentShaderBit;
        }
        else if (oldLayout == ImageLayout.PresentSrcKhr && newLayout == ImageLayout.TransferSrcOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.MemoryReadBit;
            barrier.DstAccessMask = AccessFlags.TransferReadBit;
            sourceStage = PipelineStageFlags.BottomOfPipeBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else if (oldLayout == ImageLayout.TransferSrcOptimal && newLayout == ImageLayout.PresentSrcKhr)
        {
            barrier.SrcAccessMask = AccessFlags.TransferReadBit;
            barrier.DstAccessMask = 0;
            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.BottomOfPipeBit;
        }
        else if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.ColorAttachmentOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.ColorAttachmentWriteBit;
            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.ColorAttachmentOutputBit;
        }
        else if (oldLayout == ImageLayout.ColorAttachmentOptimal && newLayout == ImageLayout.TransferSrcOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.ColorAttachmentWriteBit;
            barrier.DstAccessMask = AccessFlags.TransferReadBit;
            sourceStage = PipelineStageFlags.ColorAttachmentOutputBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else if (oldLayout == ImageLayout.TransferSrcOptimal && newLayout == ImageLayout.ColorAttachmentOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.TransferReadBit;
            barrier.DstAccessMask = AccessFlags.ColorAttachmentWriteBit;
            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.ColorAttachmentOutputBit;
        }
        else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.PresentSrcKhr)
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = 0;
            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.BottomOfPipeBit;
        }
        else
        {
            throw new InvalidOperationException("Unsupported image layout transition.");
        }

        _vk.CmdPipelineBarrier(
            commandBuffer,
            sourceStage,
            destinationStage,
            0,
            0,
            null,
            0,
            null,
            1,
            &barrier
        );
    }

    private static Format ToVkFormat(UiTextureFormat format) => format switch
    {
        UiTextureFormat.Rgba8Unorm => Format.R8G8B8A8Unorm,
        UiTextureFormat.Rgba8Srgb => Format.R8G8B8A8Srgb,
        _ => throw new InvalidOperationException($"Unsupported texture format: {format}.")
    };

    private static ClearColorValue ToClearColorValue(UiColor color)
    {
        var rgba = color.Rgba;
        var r = ((rgba >> 16) & 0xFF) / 255f;
        var g = ((rgba >> 8) & 0xFF) / 255f;
        var b = (rgba & 0xFF) / 255f;
        var a = ((rgba >> 24) & 0xFF) / 255f;
        return new ClearColorValue(r, g, b, a);
    }

    private ShaderModule CreateShaderModule(ReadOnlySpan<byte> code)
    {
        unsafe
        {
            fixed (byte* codePtr = code)
            {
                var createInfo = new ShaderModuleCreateInfo
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)code.Length,
                    PCode = (uint*)codePtr,
                };

                ShaderModule module = default;
                Check(_vk.CreateShaderModule(_device, &createInfo, null, &module));
                return module;
            }
        }
    }

    private static byte[] LoadEmbeddedShader(string resourceName)
    {
        var assembly = typeof(VulkanRendererBackend).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded shader not found: {resourceName}.");

        if (stream.Length is > int.MaxValue)
        {
            throw new InvalidOperationException($"Embedded shader too large: {resourceName}.");
        }

        var buffer = new byte[(int)stream.Length];
        stream.ReadExactly(buffer);
        return buffer;
    }

    private unsafe void CreateMsaaColorImage()
    {
        if (_msaaSampleCount == SampleCountFlags.Count1Bit)
        {
            _msaaColorImage = default;
            _msaaColorMemory = default;
            _msaaColorImageView = default;
            return;
        }

        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(_swapchainExtent.Width, _swapchainExtent.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = _swapchainFormat,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.TransientAttachmentBit | ImageUsageFlags.ColorAttachmentBit,
            Samples = _msaaSampleCount,
            SharingMode = SharingMode.Exclusive,
        };

        fixed (Image* img = &_msaaColorImage)
        {
            Check(_vk.CreateImage(_device, &imageInfo, null, img));
        }

        _vk.GetImageMemoryRequirements(_device, _msaaColorImage, out var memRequirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit),
        };

        fixed (DeviceMemory* mem = &_msaaColorMemory)
        {
            Check(_vk.AllocateMemory(_device, &allocInfo, null, mem));
        }

        Check(_vk.BindImageMemory(_device, _msaaColorImage, _msaaColorMemory, 0));

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _msaaColorImage,
            ViewType = ImageViewType.Type2D,
            Format = _swapchainFormat,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        fixed (ImageView* view = &_msaaColorImageView)
        {
            Check(_vk.CreateImageView(_device, &viewInfo, null, view));
        }
    }

    private void DestroyMsaaColorImage()
    {
        if (_msaaColorImageView.Handle is not 0)
        {
            _vk.DestroyImageView(_device, _msaaColorImageView, null);
            _msaaColorImageView = default;
        }

        if (_msaaColorImage.Handle is not 0)
        {
            _vk.DestroyImage(_device, _msaaColorImage, null);
            _msaaColorImage = default;
        }

        if (_msaaColorMemory.Handle is not 0)
        {
            _vk.FreeMemory(_device, _msaaColorMemory, null);
            _msaaColorMemory = default;
        }
    }

    private unsafe void CreateFramebuffers()
    {
        _framebuffers = new Framebuffer[_swapchainImageViews.Length];

        if (_msaaSampleCount == SampleCountFlags.Count1Bit)
        {
            for (var i = 0; i < _swapchainImageViews.Length; i++)
            {
                var attachment = _swapchainImageViews[i];
                var framebufferInfo = new FramebufferCreateInfo
                {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = _renderPass,
                    AttachmentCount = 1,
                    PAttachments = &attachment,
                    Width = _swapchainExtent.Width,
                    Height = _swapchainExtent.Height,
                    Layers = 1,
                };

                fixed (Framebuffer* framebuffer = &_framebuffers[i])
                {
                    Check(_vk.CreateFramebuffer(_device, &framebufferInfo, null, framebuffer));
                }
            }

            return;
        }

        // Allocate outside loop to avoid CA2014 (stackalloc in loop)
        var attachments = stackalloc ImageView[2];
        attachments[0] = _msaaColorImageView;

        for (var i = 0; i < _swapchainImageViews.Length; i++)
        {
            // Attachment 0: MSAA color, Attachment 1: resolve (swapchain)
            attachments[1] = _swapchainImageViews[i];
            var framebufferInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _renderPass,
                AttachmentCount = 2,
                PAttachments = attachments,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                Layers = 1,
            };

            fixed (Framebuffer* framebuffer = &_framebuffers[i])
            {
                Check(_vk.CreateFramebuffer(_device, &framebufferInfo, null, framebuffer));
            }
        }
    }


    private unsafe void CreateSyncObjects()
    {
        var framesInFlight = _framesInFlight;
        _frames = new FrameResources[framesInFlight];
        _imagesInFlight = new Fence[_swapchainImages.Length];
        _frameIndex = 0;
        _semaphoreIndex = 0;

        var semaphoreCount = Math.Max(2, _swapchainImages.Length + 1);
        _frameSemaphores = new FrameSemaphores[semaphoreCount];

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamily,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
        };

        var uploadPoolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _transferQueueFamily,
            Flags = CommandPoolCreateFlags.TransientBit | CommandPoolCreateFlags.ResetCommandBufferBit,
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

        fixed (CommandPool* pool = &_uploadCommandPool)
        {
            Check(_vk.CreateCommandPool(_device, &uploadPoolInfo, null, pool));
        }

        fixed (Fence* uploadFence = &_uploadFence)
        {
            Check(_vk.CreateFence(_device, &fenceInfo, null, uploadFence));
        }
        _hasPendingUploadWork = false;
        _uploadBatchDepth = 0;
        _uploadBatchRecording = false;

        var poolPtr = stackalloc CommandPool[1];
        var commandBufferPtr = stackalloc CommandBuffer[1];
        var imageAvailablePtr = stackalloc VulkanSemaphore[1];
        var renderFinishedPtr = stackalloc VulkanSemaphore[1];
        var fencePtr = stackalloc Fence[1];

        for (var i = 0; i < semaphoreCount; i++)
        {
            Check(_vk.CreateSemaphore(_device, &semaphoreInfo, null, imageAvailablePtr));
            Check(_vk.CreateSemaphore(_device, &semaphoreInfo, null, renderFinishedPtr));
            _frameSemaphores[i] = new FrameSemaphores(imageAvailablePtr[0], renderFinishedPtr[0]);
        }

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

            _frames[i] = frame;
        }
    }

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
        if (!_options.EnableVSync)
        {
            // Immediate: uncapped FPS (may tear)
            for (var i = 0u; i < count; i++)
            {
                if (modes[i] == PresentModeKHR.ImmediateKhr)
                {
                    return PresentModeKHR.ImmediateKhr;
                }
            }

            Console.WriteLine("[Duxel.Vulkan] VSync OFF requested but Immediate present mode is unavailable; falling back to FIFO (refresh-capped).");
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
}
