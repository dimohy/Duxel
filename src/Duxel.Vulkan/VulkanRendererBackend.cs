using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Duxel.Core;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using VkBuffer = Silk.NET.Vulkan.Buffer;
using VulkanSemaphore = Silk.NET.Vulkan.Semaphore;

namespace Duxel.Vulkan;

public readonly record struct VulkanRendererOptions(
    int MinImageCount,
    bool EnableValidationLayers,
    bool EnableVSync = true,
    int MsaaSamples = 4,
    bool EnableGlobalStaticGeometryCache = true,
    bool EnableTaaIfSupported = false,
    bool EnableFxaaIfSupported = false,
    bool TaaExcludeFont = true,
    float TaaCurrentFrameWeight = 0.18f,
    bool FontLinearSampling = false,
    UiTextureId FontTextureId = default
);

public sealed unsafe class VulkanRendererBackend : IRendererBackend
{
    private const string StaticLayerGeometryTagPrefix = "duxel.layer.static:";
    private const string StaticGlobalGeometryTagPrefix = "duxel.global.static:";
    private const string TextureBackendTagMarker = ":cbt";
    private const int StaticGeometryLruGraceFrames = 180;
    private const int StaticGeometryPruneIntervalFrames = 32;
    private const int DefaultVertexBufferCapacity = 5_000;
    private const int DefaultIndexBufferCapacity = 10_000;
    private const int VertexBufferGrowthPadding = 5_000;
    private const int IndexBufferGrowthPadding = 10_000;
    private static readonly string[] BaseDeviceExtensions =
    [
        "VK_KHR_swapchain",
    ];

    private static readonly string[] ValidationLayers =
    [
        "VK_LAYER_KHRONOS_validation",
    ];
    private static readonly string? VulkanFailureLogPath = Environment.GetEnvironmentVariable("DUXEL_VK_FAIL_LOG");
    private static readonly object VulkanFailureLogLock = new();

    private readonly Vk _vk = Vk.GetApi();
    private VulkanRendererOptions _options;
    private readonly IVulkanSurfaceSource _surfaceSource;
    private readonly IPlatformBackend _platform;
    private int _minImageCount;
    private KhrSurface _khrSurface = null!;
    private KhrSwapchain _khrSwapchain = null!;
    private ExtDebugUtils? _debugUtils;
    private bool _debugUtilsAvailable;
    private DebugUtilsMessengerEXT _debugMessenger;
    private DebugUtilsMessengerCallbackFunctionEXT _debugCallback = DebugCallback;

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
    private RenderPass _overlayRenderPass;
    private DescriptorSetLayout _descriptorSetLayout;
    private PipelineLayout _pipelineLayout;
    private Sampler _fontSampler;
    private Sampler _imageSampler;
    private Pipeline _graphicsPipeline;
    private Pipeline _taaGraphicsPipeline;
    private Pipeline _fxaaGraphicsPipeline;
    private ShaderModule _vertexShaderModule;
    private ShaderModule _fragmentShaderModule;
    private ShaderModule _fxaaFragmentShaderModule;
    private PipelineCache _pipelineCache;
    private DescriptorPool _descriptorPool;
    private Framebuffer[] _framebuffers = Array.Empty<Framebuffer>();
    private Framebuffer[] _overlayFramebuffers = Array.Empty<Framebuffer>();
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
    private readonly bool _profilingEnabled = string.Equals(Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE"), "1", StringComparison.Ordinal);
    private readonly int _profilingLogInterval = ParseProfileLogInterval();
    private readonly bool _uploadBatchingEnabled = ParseUploadBatchingEnabled();
    private readonly bool _layerTextureBackendEnabled = ParseLayerTextureBackendEnabled();
    private readonly bool _layerTextureComposeEnabled = ParseLayerTextureComposeEnabled();
    private readonly bool _legacyClipClampPath = ParseLegacyClipClampPathEnabled();
    private int _profilingFrameCounter;
    private bool _taaEnabled;
    private bool _fxaaEnabled;
    private bool[] _taaImageHistoryValid = Array.Empty<bool>();
    private Image[] _taaRenderImages = Array.Empty<Image>();
    private DeviceMemory[] _taaRenderMemories = Array.Empty<DeviceMemory>();
    private ImageView[] _taaRenderImageViews = Array.Empty<ImageView>();
    private Image[] _taaHistoryImages = Array.Empty<Image>();
    private DeviceMemory[] _taaHistoryMemories = Array.Empty<DeviceMemory>();
    private ImageView[] _taaHistoryImageViews = Array.Empty<ImageView>();
    private DescriptorSet[] _taaHistoryDescriptorSets = Array.Empty<DescriptorSet>();
    private bool[] _taaHistoryValid = Array.Empty<bool>();

    // MSAA resources
    private SampleCountFlags _msaaSampleCount = SampleCountFlags.Count4Bit;
    private Image _msaaColorImage;
    private DeviceMemory _msaaColorMemory;
    private ImageView _msaaColorImageView;
    private LayerTextureCacheResource _layerTextureCache;
    private bool _layerTextureCacheInitialized;
    private ulong _layerTextureComposeSignature;
    private bool _layerTextureComposeSignatureValid;
    private readonly Dictionary<string, StaticGeometryBuffer> _staticGeometryBuffers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _staticGeometryLastSeenFrame = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ulong> _textureLayerTagHashCache = new(StringComparer.Ordinal);
    private readonly List<string> _staticGeometryStaleTags = new();
    private readonly Dictionary<int, StaticGeometryBuffer> _frameStaticBindings = new();

    private static readonly byte[] VertexShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui.vert.spv");
    private static readonly byte[] FragmentShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui.frag.spv");

    private readonly record struct StaticGeometryBuffer(
        string Tag,
        VkBuffer VertexBuffer,
        DeviceMemory VertexMemory,
        int VertexCount,
        VkBuffer IndexBuffer,
        DeviceMemory IndexMemory,
        int IndexCount);

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
        CreateDebugMessenger();
        CreateSurface();
        PickPhysicalDevice();
        ConfigureTemporalAa();
        ConfigureMsaaSampleCount();
        CreateDevice();
        LoadDeviceExtensions();
        CreateSwapchainResources();
    }

    private void ConfigureTemporalAa()
    {
        _fxaaEnabled = _options.EnableFxaaIfSupported;

        if (!_options.EnableTaaIfSupported)
        {
            _taaEnabled = false;
            if (_fxaaEnabled)
            {
                Console.WriteLine("[Duxel.Vulkan] FXAA enabled (post-process 1-pass).");
            }
            return;
        }

        _taaEnabled = true;
        _fxaaEnabled = false;
        Console.WriteLine("[Duxel.Vulkan] MVP TAA enabled (history blend + jitter, motion vectors 없음).");
    }

    private void ConfigureMsaaSampleCount()
    {
        var requested = (_taaEnabled || _fxaaEnabled) ? 1 : _options.MsaaSamples;
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
        var uploadTicks = 0L;
        var recordTicks = 0L;
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
            RecreateSwapchain();
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

        if (drawData.TotalVertexCount is 0 || drawData.TotalIndexCount is 0)
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
        if (acquireResult == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            acquireResult = _khrSwapchain.AcquireNextImage(_device, _swapchain, ulong.MaxValue, imageAvailable, default, &imageIndex);
            if (acquireResult == Result.ErrorOutOfDateKhr)
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

        var needsLayerComposeQuad = _layerTextureComposeEnabled
            && _layerTextureBackendEnabled
            && !_taaEnabled
            && !_fxaaEnabled
            && _overlayRenderPass.Handle is not 0
            && imageIndex < _overlayFramebuffers.Length
            && _layerTextureCache.DescriptorSet.Handle is not 0;
        var needsTemporalBlendQuad = ((_taaEnabled || _fxaaEnabled) && imageIndex < _taaHistoryDescriptorSets.Length)
            || needsLayerComposeQuad;

        EnsureVertexBufferCapacity(frame, drawData.TotalVertexCount + (needsTemporalBlendQuad ? 4 : 0));
        EnsureIndexBufferCapacity(frame, drawData.TotalIndexCount + (needsTemporalBlendQuad ? 6 : 0));

        var uploadStart = _profilingEnabled ? Stopwatch.GetTimestamp() : 0;
        UploadGeometry(frame, drawData, needsTemporalBlendQuad, _frameStaticBindings);
        if (_profilingEnabled)
        {
            uploadTicks = Stopwatch.GetTimestamp() - uploadStart;
        }

        var commandPool = frameData.CommandPool;
        var commandBuffer = frameData.CommandBuffer;
        Check(_vk.ResetCommandPool(_device, commandPool, 0));
        var vertexBuffer = frameData.RenderBuffers.VertexBuffer;
        var indexBuffer = frameData.RenderBuffers.IndexBuffer;
        var taaNeedsClear = _taaEnabled && (imageIndex >= _taaImageHistoryValid.Length || !_taaImageHistoryValid[imageIndex]);
        var jitter = GetTemporalJitter(_frameIndex);
        var recordStart = _profilingEnabled ? Stopwatch.GetTimestamp() : 0;
        RecordCommandBuffer(
            commandBuffer,
            imageIndex,
            drawData,
            vertexBuffer,
            indexBuffer,
            _frameStaticBindings,
            taaNeedsClear,
            jitter.X,
            jitter.Y,
            out recordTextureLookupTicks,
            out recordClippingTicks,
            out recordDescriptorBindTicks,
            out recordDrawCallTicks);
        if (_profilingEnabled)
        {
            recordTicks = Stopwatch.GetTimestamp() - recordStart;
        }

        var renderFinished = semaphores.RenderFinished;
        submitTicks = SubmitFrame(commandBuffer, imageAvailable, renderFinished, frameData.InFlight);
        var presentResult = PresentFrame(imageIndex, renderFinished, out presentTicks);
        if (_profilingEnabled)
        {
            LogProfileFrame(
                uploadTicks,
                recordTicks,
                recordTextureLookupTicks,
                recordClippingTicks,
                recordDescriptorBindTicks,
                recordDrawCallTicks,
                submitTicks,
                presentTicks);
        }
        if (presentResult == Result.ErrorOutOfDateKhr || IsSuboptimal(presentResult))
        {
            RecreateSwapchain();
            return;
        }

        if (presentResult != Result.Success)
        {
            Check(presentResult);
        }

        if (_taaEnabled && imageIndex < _taaImageHistoryValid.Length)
        {
            _taaImageHistoryValid[imageIndex] = true;
        }

        if (_taaEnabled && imageIndex < _taaHistoryValid.Length)
        {
            _taaHistoryValid[imageIndex] = true;
        }

        _frameIndex++;
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

    private static int ParseProfileLogInterval()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE_EVERY");
        if (int.TryParse(raw, out var value) && value > 0)
        {
            return value;
        }

        return 30;
    }

    private static bool ParseLayerTextureComposeEnabled()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_LAYER_TEXTURE_COMPOSE");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ParseLayerTextureBackendEnabled()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_LAYER_TEXTURE_BACKEND");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase);
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

    private UiVector2 GetTemporalJitter(int frameIndex)
    {
        if (!_taaEnabled)
        {
            return default;
        }

        var index = frameIndex & 7;
        var halton2 = Halton(index + 1, 2);
        var halton3 = Halton(index + 1, 3);
        var jitterX = (halton2 - 0.5f) * 0.66f;
        var jitterY = (halton3 - 0.5f) * 0.66f;
        return new UiVector2(jitterX, jitterY);
    }

    private static float Halton(int index, int @base)
    {
        var result = 0f;
        var f = 1f;
        var i = index;
        while (i > 0)
        {
            f /= @base;
            result += f * (i % @base);
            i /= @base;
        }

        return result;
    }

    private float GetTaaCurrentFrameWeight()
    {
        return Math.Clamp(_options.TaaCurrentFrameWeight, 0.05f, 1f);
    }

    private float GetTaaHistoryBlendWeight()
    {
        var history = 1f - GetTaaCurrentFrameWeight();
        return Math.Clamp(history, 0f, 0.60f);
    }

    private void LogProfileFrame(
        long uploadTicks,
        long recordTicks,
        long recordTextureLookupTicks,
        long recordClippingTicks,
        long recordDescriptorBindTicks,
        long recordDrawCallTicks,
        long submitTicks,
        long presentTicks)
    {
        _profilingFrameCounter++;
        if (_profilingFrameCounter % _profilingLogInterval != 0)
        {
            return;
        }

        var uploadUs = TicksToMicroseconds(uploadTicks);
        var recordUs = TicksToMicroseconds(recordTicks);
        var recordTextureLookupUs = TicksToMicroseconds(recordTextureLookupTicks);
        var recordClippingUs = TicksToMicroseconds(recordClippingTicks);
        var recordDescriptorBindUs = TicksToMicroseconds(recordDescriptorBindTicks);
        var recordDrawCallUs = TicksToMicroseconds(recordDrawCallTicks);
        var submitUs = TicksToMicroseconds(submitTicks);
        var presentUs = TicksToMicroseconds(presentTicks);
        Console.WriteLine(
            $"[duxel-vk-prof] upload={uploadUs:0.000} record={recordUs:0.000}(tex={recordTextureLookupUs:0.000} clip={recordClippingUs:0.000} desc={recordDescriptorBindUs:0.000} draw={recordDrawCallUs:0.000}) submit={submitUs:0.000} present={presentUs:0.000}");
    }

    private static double TicksToMicroseconds(long ticks)
    {
        return ticks * (1000000.0 / Stopwatch.Frequency);
    }

    public byte[] CaptureBackbuffer(out int width, out int height, out Format format)
    {
        if (_swapchainImages.Length is 0)
        {
            throw new InvalidOperationException("Swapchain is not initialized.");
        }

        if (_lastImageIndex < 0 || _lastImageIndex >= _swapchainImages.Length)
        {
            throw new InvalidOperationException("No rendered frame available for capture.");
        }

        TryWaitForDeviceIdle("CaptureBackbuffer", throwOnFailure: true);

        width = (int)_swapchainExtent.Width;
        height = (int)_swapchainExtent.Height;
        format = _swapchainFormat;

        var image = _swapchainImages[_lastImageIndex];
        var byteCount = checked(width * height * 4);
        var bufferSize = (nuint)byteCount;

        CreateBuffer(
            bufferSize,
            BufferUsageFlags.TransferDstBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out var stagingBuffer,
            out var stagingMemory
        );

        try
        {
            var commandBuffer = BeginSingleTimeCommands();
            try
            {
                TransitionImageLayout(commandBuffer, image, ImageLayout.PresentSrcKhr, ImageLayout.TransferSrcOptimal);

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
                    ImageExtent = new Extent3D((uint)width, (uint)height, 1),
                };

                _vk.CmdCopyImageToBuffer(commandBuffer, image, ImageLayout.TransferSrcOptimal, stagingBuffer, 1, &region);
                TransitionImageLayout(commandBuffer, image, ImageLayout.TransferSrcOptimal, ImageLayout.PresentSrcKhr);
            }
            finally
            {
                EndSingleTimeCommands(commandBuffer);
            }

            void* mapped;
            Check(_vk.MapMemory(_device, stagingMemory, 0, bufferSize, 0, &mapped));
            try
            {
                var data = new byte[byteCount];
                new Span<byte>(mapped, data.Length).CopyTo(data);
                return data;
            }
            finally
            {
                _vk.UnmapMemory(_device, stagingMemory);
            }
        }
        finally
        {
            _vk.DestroyBuffer(_device, stagingBuffer, null);
            _vk.FreeMemory(_device, stagingMemory, null);
        }
    }

    public byte[] CaptureBackbufferRgba(out int width, out int height)
    {
        var raw = CaptureBackbuffer(out width, out height, out var format);
        if (format is Format.R8G8B8A8Unorm or Format.R8G8B8A8Srgb)
        {
            return raw;
        }

        if (format is Format.B8G8R8A8Unorm or Format.B8G8R8A8Srgb)
        {
            for (var i = 0; i + 3 < raw.Length; i += 4)
            {
                (raw[i], raw[i + 2]) = (raw[i + 2], raw[i]);
            }

            return raw;
        }

        throw new InvalidOperationException($"Unsupported capture format: {format}.");
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
            RecreateSwapchain();
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
            RecreateSwapchain();
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
            RecreateSwapchain();
        }
    }

    public void SetTaaEnabled(bool enable)
    {
        if (_taaEnabled == enable)
        {
            return;
        }

        var nextFxaa = enable ? false : _fxaaEnabled;
        ApplyTemporalAaSettings(enable, nextFxaa);
    }

    public void SetFxaaEnabled(bool enable)
    {
        if (_fxaaEnabled == enable)
        {
            return;
        }

        var nextTaa = enable ? false : _taaEnabled;
        ApplyTemporalAaSettings(nextTaa, enable);
    }

    public void SetTaaExcludeFont(bool exclude)
    {
        if (_options.TaaExcludeFont == exclude)
        {
            return;
        }

        _options = _options with { TaaExcludeFont = exclude };
    }

    public void SetTaaCurrentFrameWeight(float weight)
    {
        var clamped = Math.Clamp(weight, 0.05f, 1f);
        if (MathF.Abs(_options.TaaCurrentFrameWeight - clamped) <= float.Epsilon)
        {
            return;
        }

        _options = _options with { TaaCurrentFrameWeight = clamped };
    }

    private void ApplyTemporalAaSettings(bool taaEnabled, bool fxaaEnabled)
    {
        if (taaEnabled)
        {
            fxaaEnabled = false;
        }

        if (_taaEnabled == taaEnabled && _fxaaEnabled == fxaaEnabled)
        {
            return;
        }

        _taaEnabled = taaEnabled;
        _fxaaEnabled = fxaaEnabled;
        _options = _options with
        {
            EnableTaaIfSupported = _taaEnabled,
            EnableFxaaIfSupported = _fxaaEnabled
        };

        if (_taaImageHistoryValid.Length > 0)
        {
            Array.Clear(_taaImageHistoryValid, 0, _taaImageHistoryValid.Length);
        }

        if (_taaHistoryValid.Length > 0)
        {
            Array.Clear(_taaHistoryValid, 0, _taaHistoryValid.Length);
        }

        ConfigureMsaaSampleCount();
        if (_swapchain.Handle is not 0)
        {
            _frameIndex = 0;
            Array.Clear(_imagesInFlight, 0, _imagesInFlight.Length);
            RecreateSwapchain();
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

        DestroyDebugMessenger();


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
        CreateLayerTextureCacheScaffold();
        CreateMsaaColorImage();
        CreateTaaRenderTargets();
        CreateFramebuffers();
        CreateSyncObjects();
        _taaImageHistoryValid = new bool[_swapchainImages.Length];
        _taaHistoryValid = new bool[_swapchainImages.Length];
    }

    private void RecreateSwapchain()
    {
        DestroySwapchainDependentResources();
        CreateSwapchainResources();
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
        DestroyLayerTextureCacheScaffold();

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

        foreach (var framebuffer in _overlayFramebuffers)
        {
            _vk.DestroyFramebuffer(_device, framebuffer, null);
        }

        DestroyTaaRenderTargets();
        DestroyMsaaColorImage();

        if (_renderPass.Handle is not 0)
        {
            _vk.DestroyRenderPass(_device, _renderPass, null);
            _renderPass = default;
        }

        if (_overlayRenderPass.Handle is not 0)
        {
            _vk.DestroyRenderPass(_device, _overlayRenderPass, null);
            _overlayRenderPass = default;
        }

        if (_graphicsPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _graphicsPipeline, null);
            _graphicsPipeline = default;
        }

        if (_taaGraphicsPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _taaGraphicsPipeline, null);
            _taaGraphicsPipeline = default;
        }

        if (_fxaaGraphicsPipeline.Handle is not 0)
        {
            _vk.DestroyPipeline(_device, _fxaaGraphicsPipeline, null);
            _fxaaGraphicsPipeline = default;
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

        if (_fxaaFragmentShaderModule.Handle is not 0)
        {
            _vk.DestroyShaderModule(_device, _fxaaFragmentShaderModule, null);
            _fxaaFragmentShaderModule = default;
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
        _overlayFramebuffers = Array.Empty<Framebuffer>();
        _frames = Array.Empty<FrameResources>();
        _imagesInFlight = Array.Empty<Fence>();
        _taaImageHistoryValid = Array.Empty<bool>();
        _taaHistoryValid = Array.Empty<bool>();
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

    private readonly struct TextureResource
    {
        public readonly Image Image;
        public readonly DeviceMemory Memory;
        public readonly ImageView View;
        public readonly DescriptorSet DescriptorSet;
        public readonly int Width;
        public readonly int Height;
        public readonly Format Format;

        public TextureResource(
            Image image,
            DeviceMemory memory,
            ImageView view,
            DescriptorSet descriptorSet,
            int width,
            int height,
            Format format
        )
        {
            Image = image;
            Memory = memory;
            View = view;
            DescriptorSet = descriptorSet;
            Width = width;
            Height = height;
            Format = format;
        }
    }

    private readonly struct LayerTextureCacheResource
    {
        public readonly Image Image;
        public readonly DeviceMemory Memory;
        public readonly ImageView View;
        public readonly DescriptorSet DescriptorSet;
        public readonly int Width;
        public readonly int Height;

        public LayerTextureCacheResource(
            Image image,
            DeviceMemory memory,
            ImageView view,
            DescriptorSet descriptorSet,
            int width,
            int height
        )
        {
            Image = image;
            Memory = memory;
            View = view;
            DescriptorSet = descriptorSet;
            Width = width;
            Height = height;
        }
    }

    private sealed class FrameResources
    {
        public FrameRenderBuffers RenderBuffers = new();
        public CommandPool CommandPool;
        public CommandBuffer CommandBuffer;
        public Fence InFlight;
        public readonly List<PendingTextureDestroy> PendingTextureDestroys = new();
        public readonly List<PendingBufferDestroy> PendingBufferDestroys = new();
    }

    private readonly record struct FrameSemaphores(VulkanSemaphore ImageAvailable, VulkanSemaphore RenderFinished);

    private sealed class FrameRenderBuffers
    {
        public VkBuffer VertexBuffer;
        public DeviceMemory VertexMemory;
        public nuint VertexSize;
        public unsafe void* VertexMappedPtr;
        public VkBuffer IndexBuffer;
        public DeviceMemory IndexMemory;
        public nuint IndexSize;
        public unsafe void* IndexMappedPtr;
    }

    private readonly record struct BufferResource(VkBuffer Buffer, DeviceMemory Memory);
    private readonly record struct PendingBufferDestroy(BufferResource Resource, int DestroyFrame);
    private readonly record struct PendingTextureDestroy(TextureResource Resource, int DestroyFrame);

    private unsafe void CreateInstance()
    {
        var requiredExtensions = _surfaceSource.RequiredInstanceExtensions;
        var extensionList = new List<string>(requiredExtensions.Count + 1);
        for (var i = 0; i < requiredExtensions.Count; i++)
        {
            extensionList.Add(requiredExtensions[i]);
        }

        if (_options.EnableValidationLayers)
        {
            var hasDebugUtils = false;
            for (var i = 0; i < extensionList.Count; i++)
            {
                if (string.Equals(extensionList[i], "VK_EXT_debug_utils", StringComparison.Ordinal))
                {
                    hasDebugUtils = true;
                    break;
                }
            }

            if (!hasDebugUtils)
            {
                extensionList.Add("VK_EXT_debug_utils");
            }
        }

        var extensions = extensionList.ToArray();
        var enabledLayers = _options.EnableValidationLayers ? ValidationLayers : Array.Empty<string>();

        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            ApiVersion = Vk.Version12,
            PApplicationName = (byte*)SilkMarshal.StringToPtr("Duxel"),
            PEngineName = (byte*)SilkMarshal.StringToPtr("Duxel"),
            EngineVersion = new Version32(1, 0, 0),
            ApplicationVersion = new Version32(1, 0, 0),
        };

        var extensionPtr = SilkMarshal.StringArrayToPtr(extensions);
        var layerPtr = SilkMarshal.StringArrayToPtr(enabledLayers);

        try
        {
            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = (uint)extensions.Length,
                PpEnabledExtensionNames = (byte**)extensionPtr,
                EnabledLayerCount = (uint)enabledLayers.Length,
                PpEnabledLayerNames = (byte**)layerPtr,
            };

            fixed (Instance* instance = &_instance)
            {
                Check(_vk.CreateInstance(&createInfo, null, instance));
            }
        }
        finally
        {
            SilkMarshal.Free((nint)appInfo.PApplicationName);
            SilkMarshal.Free((nint)appInfo.PEngineName);
            SilkMarshal.Free(extensionPtr);
            SilkMarshal.Free(layerPtr);
        }
    }

    private void LoadInstanceExtensions()
    {
        if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface))
        {
            throw new InvalidOperationException("Failed to load VK_KHR_surface extension.");
        }

        if (_options.EnableValidationLayers)
        {
            _debugUtilsAvailable = _vk.TryGetInstanceExtension(_instance, out _debugUtils);
        }
    }

    private unsafe void CreateDebugMessenger()
    {
        if (!_options.EnableValidationLayers || !_debugUtilsAvailable)
        {
            return;
        }

        var createInfo = new DebugUtilsMessengerCreateInfoEXT
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt
                              | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                          | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt
                          | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
            PfnUserCallback = _debugCallback,
        };

        fixed (DebugUtilsMessengerEXT* messenger = &_debugMessenger)
        {
            _debugUtils!.CreateDebugUtilsMessenger(_instance, &createInfo, null, messenger);
        }
    }

    private unsafe void DestroyDebugMessenger()
    {
        if (_debugMessenger.Handle is 0 || !_debugUtilsAvailable)
        {
            return;
        }

        _debugUtils!.DestroyDebugUtilsMessenger(_instance, _debugMessenger, null);
        _debugMessenger = default;
    }

    private static uint DebugCallback(
        DebugUtilsMessageSeverityFlagsEXT severity,
        DebugUtilsMessageTypeFlagsEXT types,
        DebugUtilsMessengerCallbackDataEXT* data,
        void* userData
    )
    {
        var message = SilkMarshal.PtrToString((nint)data->PMessage) ?? string.Empty;
        Console.Error.WriteLine($"[Vulkan][{severity}] {message}");
        TraceValidationMessage(severity, types, message);
        return Vk.False;
    }

    private static void TraceValidationMessage(
        DebugUtilsMessageSeverityFlagsEXT severity,
        DebugUtilsMessageTypeFlagsEXT types,
        string message)
    {
        if (string.IsNullOrWhiteSpace(VulkanFailureLogPath))
        {
            return;
        }

        try
        {
            var path = VulkanFailureLogPath!;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var sb = new StringBuilder(768);
            sb.AppendLine("==== VulkanValidation ====");
            sb.Append("Utc: ").AppendLine(DateTime.UtcNow.ToString("O"));
            sb.Append("Severity: ").AppendLine(severity.ToString());
            sb.Append("Types: ").AppendLine(types.ToString());
            sb.Append("Pid: ").AppendLine(Environment.ProcessId.ToString());
            sb.Append("ThreadId: ").AppendLine(Environment.CurrentManagedThreadId.ToString());
            sb.Append("Message: ").AppendLine(message);
            sb.AppendLine("Stack:");
            sb.AppendLine(Environment.StackTrace);

            lock (VulkanFailureLogLock)
            {
                File.AppendAllText(path, sb.ToString());
            }
        }
        catch
        {
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

        var extensionPtr = SilkMarshal.StringArrayToPtr(BaseDeviceExtensions);

        try
        {
            var deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = queueCreateInfoCount,
                PQueueCreateInfos = queueCreateInfos,
                EnabledExtensionCount = (uint)BaseDeviceExtensions.Length,
                PpEnabledExtensionNames = (byte**)extensionPtr,
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
            SilkMarshal.Free(extensionPtr);
        }
    }

    private void LoadDeviceExtensions()
    {
        if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain))
        {
            throw new InvalidOperationException("Failed to load VK_KHR_swapchain extension.");
        }
    }

    private static void Check(Result result)
    {
        if ((int)result >= 0 || IsSuboptimal(result))
        {
            return;
        }

        TraceVulkanFailure(result);

        throw new InvalidOperationException($"Vulkan call failed: {result}");
    }

    private bool TryWaitForDeviceIdle(string phase, bool throwOnFailure)
    {
        if (_device.Handle is 0)
        {
            return true;
        }

        var result = _vk.DeviceWaitIdle(_device);
        if ((int)result >= 0 || IsSuboptimal(result))
        {
            return true;
        }

        TraceVulkanFailure(result);
        if (throwOnFailure)
        {
            throw new InvalidOperationException($"Vulkan DeviceWaitIdle failed during {phase}: {result}");
        }

        return false;
    }

    private static void TraceVulkanFailure(Result result)
    {
        if (string.IsNullOrWhiteSpace(VulkanFailureLogPath))
        {
            return;
        }

        try
        {
            var path = VulkanFailureLogPath!;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var sb = new StringBuilder(768);
            sb.AppendLine("==== VulkanFailure ====");
            sb.Append("Utc: ").AppendLine(DateTime.UtcNow.ToString("O"));
            sb.Append("Result: ").AppendLine(result.ToString());
            sb.Append("Pid: ").AppendLine(Environment.ProcessId.ToString());
            sb.Append("ThreadId: ").AppendLine(Environment.CurrentManagedThreadId.ToString());
            sb.Append("DUXEL_VK_UPLOAD_BATCH: ").AppendLine(Environment.GetEnvironmentVariable("DUXEL_VK_UPLOAD_BATCH") ?? "<null>");
            sb.Append("DUXEL_VK_VALIDATION: ").AppendLine(Environment.GetEnvironmentVariable("DUXEL_VK_VALIDATION") ?? "<null>");
            sb.Append("DUXEL_VK_PROFILE: ").AppendLine(Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE") ?? "<null>");
            sb.AppendLine("Stack:");
            sb.AppendLine(Environment.StackTrace);

            lock (VulkanFailureLogLock)
            {
                File.AppendAllText(path, sb.ToString());
            }
        }
        catch
        {
        }
    }

    private static bool IsSuboptimal(Result result)
    {
        const Result suboptimalKhr = (Result)1000001003;
        if (result == Result.SuboptimalKhr || result == suboptimalKhr)
        {
            return true;
        }

        var name = result.ToString();
        return name.Contains("Suboptimal", StringComparison.OrdinalIgnoreCase);
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
            var finalLayout = (_taaEnabled || _fxaaEnabled) ? ImageLayout.ColorAttachmentOptimal : ImageLayout.PresentSrcKhr;
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

            if (_taaEnabled || _fxaaEnabled || (_layerTextureComposeEnabled && _layerTextureBackendEnabled))
            {
                var overlayAttachment = new AttachmentDescription
                {
                    Format = _swapchainFormat,
                    Samples = SampleCountFlags.Count1Bit,
                    LoadOp = AttachmentLoadOp.Load,
                    StoreOp = AttachmentStoreOp.Store,
                    StencilLoadOp = AttachmentLoadOp.DontCare,
                    StencilStoreOp = AttachmentStoreOp.DontCare,
                    InitialLayout = ImageLayout.PresentSrcKhr,
                    FinalLayout = ImageLayout.PresentSrcKhr,
                };

                var overlayAttachmentRef = new AttachmentReference
                {
                    Attachment = 0,
                    Layout = ImageLayout.ColorAttachmentOptimal,
                };

                var overlaySubpass = new SubpassDescription
                {
                    PipelineBindPoint = PipelineBindPoint.Graphics,
                    ColorAttachmentCount = 1,
                    PColorAttachments = &overlayAttachmentRef,
                    PResolveAttachments = null,
                };

                var overlayDependency = new SubpassDependency
                {
                    SrcSubpass = Vk.SubpassExternal,
                    DstSubpass = 0,
                    SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                    DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                    SrcAccessMask = AccessFlags.MemoryReadBit,
                    DstAccessMask = AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit,
                    DependencyFlags = DependencyFlags.ByRegionBit,
                };

                var overlayRenderPassInfo = new RenderPassCreateInfo
                {
                    SType = StructureType.RenderPassCreateInfo,
                    AttachmentCount = 1,
                    PAttachments = &overlayAttachment,
                    SubpassCount = 1,
                    PSubpasses = &overlaySubpass,
                    DependencyCount = 1,
                    PDependencies = &overlayDependency,
                };

                fixed (RenderPass* overlayRenderPass = &_overlayRenderPass)
                {
                    Check(_vk.CreateRenderPass(_device, &overlayRenderPassInfo, null, overlayRenderPass));
                }
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
        _fxaaFragmentShaderModule = _fxaaEnabled
            ? CreateShaderModule(LoadEmbeddedShader("Duxel.Vulkan.Shaders.fxaa.frag.spv"))
            : default;

        var entryPoint = (byte*)SilkMarshal.StringToPtr("main");

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

            var dynamicStateCount = _taaEnabled ? 3u : 2u;
            var dynamicStates = stackalloc DynamicState[3];
            dynamicStates[0] = DynamicState.Viewport;
            dynamicStates[1] = DynamicState.Scissor;
            if (_taaEnabled)
            {
                dynamicStates[2] = DynamicState.BlendConstants;
            }

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

            if (_taaEnabled)
            {
                var taaBlendAttachment = new PipelineColorBlendAttachmentState
                {
                    BlendEnable = true,
                    SrcColorBlendFactor = BlendFactor.ConstantAlpha,
                    DstColorBlendFactor = BlendFactor.OneMinusConstantAlpha,
                    ColorBlendOp = BlendOp.Add,
                    SrcAlphaBlendFactor = BlendFactor.One,
                    DstAlphaBlendFactor = BlendFactor.Zero,
                    AlphaBlendOp = BlendOp.Add,
                    ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                };

                var taaColorBlending = new PipelineColorBlendStateCreateInfo
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    LogicOpEnable = false,
                    LogicOp = LogicOp.Copy,
                    AttachmentCount = 1,
                    PAttachments = &taaBlendAttachment,
                };

                var taaPipelineInfo = pipelineInfo;
                taaPipelineInfo.PColorBlendState = &taaColorBlending;

                fixed (Pipeline* pipeline = &_taaGraphicsPipeline)
                {
                    Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &taaPipelineInfo, null, pipeline));
                }
            }

            if (_fxaaEnabled)
            {
                var fxaaFragmentStage = new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.FragmentBit,
                    Module = _fxaaFragmentShaderModule,
                    PName = entryPoint,
                };

                var fxaaStages = stackalloc PipelineShaderStageCreateInfo[2];
                fxaaStages[0] = vertexStage;
                fxaaStages[1] = fxaaFragmentStage;

                var fxaaBlendAttachment = new PipelineColorBlendAttachmentState
                {
                    BlendEnable = false,
                    ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                };

                var fxaaColorBlending = new PipelineColorBlendStateCreateInfo
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    LogicOpEnable = false,
                    LogicOp = LogicOp.Copy,
                    AttachmentCount = 1,
                    PAttachments = &fxaaBlendAttachment,
                };

                var fxaaPipelineInfo = pipelineInfo;
                fxaaPipelineInfo.PStages = fxaaStages;
                fxaaPipelineInfo.PColorBlendState = &fxaaColorBlending;
                fxaaPipelineInfo.RenderPass = _overlayRenderPass;

                fixed (Pipeline* pipeline = &_fxaaGraphicsPipeline)
                {
                    Check(_vk.CreateGraphicsPipelines(_device, _pipelineCache, 1, &fxaaPipelineInfo, null, pipeline));
                }
            }
        }
        finally
        {
            SilkMarshal.Free((nint)entryPoint);
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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe void UploadGeometry(int frame, UiDrawData drawData, bool appendTemporalBlendQuad, Dictionary<int, StaticGeometryBuffer> staticBindings)
    {
        BeginUploadBatch();
        try
        {
        var frameData = _frames[frame];
        var renderBuffers = frameData.RenderBuffers;

        var vertexDst = (byte*)renderBuffers.VertexMappedPtr;
        var indexDst = (byte*)renderBuffers.IndexMappedPtr;
        staticBindings.Clear();

        for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
        {
            var drawList = drawData.DrawLists[listIndex];
            var hasStaticTag = TryGetStaticDrawListTag(drawList, out var staticTag);

            if (hasStaticTag)
            {
                _staticGeometryLastSeenFrame[staticTag] = _frameIndex;
                staticBindings[listIndex] = EnsureStaticGeometryBuffer(staticTag, drawList);
                continue;
            }

            var vertices = drawList.Vertices.AsSpan();
            if (!vertices.IsEmpty)
            {
                fixed (UiDrawVertex* vertexSrc = vertices)
                {
                    var vertexBytes = (uint)(vertices.Length * sizeof(UiDrawVertex));
                    Unsafe.CopyBlockUnaligned(vertexDst, vertexSrc, vertexBytes);
                    vertexDst += vertexBytes;
                }
            }

            var indices = drawList.Indices.AsSpan();
            if (!indices.IsEmpty)
            {
                fixed (uint* indexSrc = indices)
                {
                    var indexBytes = (uint)(indices.Length * sizeof(uint));
                    Unsafe.CopyBlockUnaligned(indexDst, indexSrc, indexBytes);
                    indexDst += indexBytes;
                }
            }
        }

        if ((_frameIndex % StaticGeometryPruneIntervalFrames) is 0)
        {
            PruneUnusedStaticGeometryBuffers(_frameIndex);
        }

        if (!appendTemporalBlendQuad)
        {
            return;
        }

        var x0 = drawData.DisplayPos.X;
        var y0 = drawData.DisplayPos.Y;
        var x1 = x0 + drawData.DisplaySize.X;
        var y1 = y0 + drawData.DisplaySize.Y;

        var quadVertices = (UiVertex*)vertexDst;
        quadVertices[0] = new UiVertex { PositionX = x0, PositionY = y0, UVx = 0f, UVy = 0f, Color = 0xFFFFFFFF };
        quadVertices[1] = new UiVertex { PositionX = x1, PositionY = y0, UVx = 1f, UVy = 0f, Color = 0xFFFFFFFF };
        quadVertices[2] = new UiVertex { PositionX = x1, PositionY = y1, UVx = 1f, UVy = 1f, Color = 0xFFFFFFFF };
        quadVertices[3] = new UiVertex { PositionX = x0, PositionY = y1, UVx = 0f, UVy = 1f, Color = 0xFFFFFFFF };

        var quadIndices = (uint*)indexDst;
        quadIndices[0] = 0;
        quadIndices[1] = 1;
        quadIndices[2] = 2;
        quadIndices[3] = 0;
        quadIndices[4] = 2;
        quadIndices[5] = 3;
        }
        finally
        {
            EndUploadBatch();
        }
    }

    private unsafe StaticGeometryBuffer EnsureStaticGeometryBuffer(string staticTag, UiDrawList drawList)
    {
        var vertexCount = drawList.Vertices.Count;
        var indexCount = drawList.Indices.Count;
        if (_staticGeometryBuffers.TryGetValue(staticTag, out var existing)
            && existing.VertexCount == vertexCount
            && existing.IndexCount == indexCount)
        {
            return existing;
        }

        if (existing.VertexBuffer.Handle is not 0)
        {
            QueueBufferDestroy(existing.VertexBuffer, existing.VertexMemory);
            QueueBufferDestroy(existing.IndexBuffer, existing.IndexMemory);
        }

        var vertexSize = (nuint)(Math.Max(1, vertexCount) * sizeof(UiDrawVertex));
        var indexSize = (nuint)(Math.Max(1, indexCount) * sizeof(uint));
        CreateBuffer(
            vertexSize,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit,
            out var vertexBuffer,
            out var vertexMemory
        );

        CreateBuffer(
            indexSize,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit,
            out var indexBuffer,
            out var indexMemory
        );

        var vertices = drawList.Vertices.AsSpan();
        if (!vertices.IsEmpty)
        {
            fixed (UiDrawVertex* vertexSrc = vertices)
            {
                UploadStaticBufferData(vertexBuffer, vertexSize, vertexSrc, (nuint)(vertices.Length * sizeof(UiDrawVertex)));
            }
        }

        var indices = drawList.Indices.AsSpan();
        if (!indices.IsEmpty)
        {
            fixed (uint* indexSrc = indices)
            {
                UploadStaticBufferData(indexBuffer, indexSize, indexSrc, (nuint)(indices.Length * sizeof(uint)));
            }
        }

        var created = new StaticGeometryBuffer(
            staticTag,
            vertexBuffer,
            vertexMemory,
            vertexCount,
            indexBuffer,
            indexMemory,
            indexCount
        );

        _staticGeometryBuffers[staticTag] = created;
        return created;
    }

    private void PruneUnusedStaticGeometryBuffers(int currentFrameIndex)
    {
        if (_staticGeometryBuffers.Count is 0)
        {
            return;
        }

        var staleTags = _staticGeometryStaleTags;
        staleTags.Clear();
        foreach (var pair in _staticGeometryBuffers)
        {
            if (!_staticGeometryLastSeenFrame.TryGetValue(pair.Key, out var lastSeenFrame))
            {
                staleTags.Add(pair.Key);
                continue;
            }

            if ((currentFrameIndex - lastSeenFrame) > StaticGeometryLruGraceFrames)
            {
                staleTags.Add(pair.Key);
            }
        }

        for (var i = 0; i < staleTags.Count; i++)
        {
            var staleTag = staleTags[i];
            if (!_staticGeometryBuffers.Remove(staleTag, out var stale))
            {
                continue;
            }

            _staticGeometryLastSeenFrame.Remove(staleTag);
            _textureLayerTagHashCache.Remove(staleTag);

            QueueBufferDestroy(stale.VertexBuffer, stale.VertexMemory);
            QueueBufferDestroy(stale.IndexBuffer, stale.IndexMemory);
        }

        staleTags.Clear();
    }

    private bool TryGetStaticDrawListTag(UiDrawList drawList, out string tag)
    {
        tag = string.Empty;
        var commandCount = drawList.Commands.Count;
        if (commandCount is 0)
        {
            return false;
        }

        for (var i = 0; i < commandCount; i++)
        {
            ref readonly var cmd = ref drawList.Commands.ItemRef(i);
            if (!IsStaticLayerGeometryTag(cmd.UserData, out var commandTag))
            {
                return false;
            }

            if (i is 0)
            {
                tag = commandTag;
                continue;
            }

            if (!string.Equals(tag, commandTag, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsStaticLayerGeometryTag(object? userData, out string tag)
    {
        if (userData is string value
            && (value.StartsWith(StaticLayerGeometryTagPrefix, StringComparison.Ordinal)
                || (_options.EnableGlobalStaticGeometryCache
                    && value.StartsWith(StaticGlobalGeometryTagPrefix, StringComparison.Ordinal))))
        {
            tag = value;
            return true;
        }

        tag = string.Empty;
        return false;
    }

    private static bool IsTextureLayerGeometryTag(string tag)
    {
        var opacitySuffixIndex = tag.LastIndexOf(":o", StringComparison.Ordinal);
        var coreTag = opacitySuffixIndex > 0
            ? tag.AsSpan(0, opacitySuffixIndex)
            : tag.AsSpan();
        return coreTag.EndsWith(TextureBackendTagMarker.AsSpan(), StringComparison.Ordinal);
    }

    private ulong GetOrComputeTextureLayerTagHash(string tag)
    {
        if (_textureLayerTagHashCache.TryGetValue(tag, out var cachedHash))
        {
            return cachedHash;
        }

        var hash = 1469598103934665603UL;
        unchecked
        {
            hash ^= (uint)tag.Length;
            hash *= 1099511628211UL;
            for (var i = 0; i < tag.Length; i++)
            {
                hash ^= tag[i];
                hash *= 1099511628211UL;
            }
        }

        _textureLayerTagHashCache[tag] = hash;
        return hash;
    }

    private bool TryComputeLayerComposeSignature(UiDrawData drawData, out ulong signature)
    {
        signature = 1469598103934665603UL;

        static void HashByte(ref ulong hash, byte value)
        {
            unchecked
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }
        }

        static void HashUInt32(ref ulong hash, uint value)
        {
            HashByte(ref hash, (byte)(value & 0xFF));
            HashByte(ref hash, (byte)((value >> 8) & 0xFF));
            HashByte(ref hash, (byte)((value >> 16) & 0xFF));
            HashByte(ref hash, (byte)((value >> 24) & 0xFF));
        }

        static void HashInt32(ref ulong hash, int value)
            => HashUInt32(ref hash, unchecked((uint)value));

        static void HashUInt64(ref ulong hash, ulong value)
        {
            HashUInt32(ref hash, (uint)(value & 0xFFFFFFFFUL));
            HashUInt32(ref hash, (uint)(value >> 32));
        }

        var staticListCount = 0;
        var seenNonStaticList = false;
        for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
        {
            var drawList = drawData.DrawLists[listIndex];
            if (!TryGetStaticDrawListTag(drawList, out var staticTag))
            {
                seenNonStaticList = true;
                continue;
            }

            if (!IsTextureLayerGeometryTag(staticTag))
            {
                seenNonStaticList = true;
                continue;
            }

            if (seenNonStaticList)
            {
                signature = 0;
                return false;
            }

            staticListCount++;
            HashInt32(ref signature, listIndex);
            HashUInt64(ref signature, GetOrComputeTextureLayerTagHash(staticTag));
            HashInt32(ref signature, drawList.Commands.Count);

            if (drawList.Commands.Count > 0)
            {
                ref readonly var cmd = ref drawList.Commands.ItemRef(0);
                HashInt32(ref signature, BitConverter.SingleToInt32Bits(cmd.Translation.X));
                HashInt32(ref signature, BitConverter.SingleToInt32Bits(cmd.Translation.Y));
            }
        }

        HashInt32(ref signature, staticListCount);
        return staticListCount > 0;
    }


    private unsafe void UploadStaticBufferData(VkBuffer destinationBuffer, nuint destinationSize, void* sourceData, nuint sourceSize)
    {
        if (sourceData is null || sourceSize is 0)
        {
            return;
        }

        EnsureStagingBuffer(destinationSize);
        Unsafe.CopyBlockUnaligned(_stagingMappedPtr, sourceData, checked((uint)sourceSize));
        CopyBuffer(_stagingBuffer, destinationBuffer, sourceSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryComputeScissorRect(
        in UiDrawCommand cmd,
        float clipOffsetX,
        float clipOffsetY,
        float clipScaleX,
        float clipScaleY,
        float fbWidthF,
        float fbHeightF,
        out int scissorX,
        out int scissorY,
        out uint scissorW,
        out uint scissorH)
    {
        const float epsilon = 0.0001f;

        var cmdTranslationX = cmd.Translation.X;
        var cmdTranslationY = cmd.Translation.Y;

        var clipMinX = (cmd.ClipRect.X + cmdTranslationX - clipOffsetX) * clipScaleX;
        var clipMinY = (cmd.ClipRect.Y + cmdTranslationY - clipOffsetY) * clipScaleY;
        var clipMaxX = (cmd.ClipRect.X + cmd.ClipRect.Width + cmdTranslationX - clipOffsetX) * clipScaleX;
        var clipMaxY = (cmd.ClipRect.Y + cmd.ClipRect.Height + cmdTranslationY - clipOffsetY) * clipScaleY;

        if (clipMaxX <= clipMinX || clipMaxY <= clipMinY)
        {
            scissorX = 0;
            scissorY = 0;
            scissorW = 0;
            scissorH = 0;
            return false;
        }

        if (_legacyClipClampPath)
        {
            clipMinX = Math.Clamp(clipMinX, 0f, fbWidthF);
            clipMinY = Math.Clamp(clipMinY, 0f, fbHeightF);
            clipMaxX = Math.Clamp(clipMaxX, 0f, fbWidthF);
            clipMaxY = Math.Clamp(clipMaxY, 0f, fbHeightF);
        }
        else
        {
            clipMinX = clipMinX < 0f ? 0f : (clipMinX > fbWidthF ? fbWidthF : clipMinX);
            clipMinY = clipMinY < 0f ? 0f : (clipMinY > fbHeightF ? fbHeightF : clipMinY);
            clipMaxX = clipMaxX < 0f ? 0f : (clipMaxX > fbWidthF ? fbWidthF : clipMaxX);
            clipMaxY = clipMaxY < 0f ? 0f : (clipMaxY > fbHeightF ? fbHeightF : clipMaxY);
        }

        if (clipMaxX <= clipMinX || clipMaxY <= clipMinY)
        {
            scissorX = 0;
            scissorY = 0;
            scissorW = 0;
            scissorH = 0;
            return false;
        }

        var minX = (int)MathF.Floor(clipMinX);
        var minY = (int)MathF.Floor(clipMinY);
        var maxX = (int)MathF.Ceiling(clipMaxX - epsilon);
        var maxY = (int)MathF.Ceiling(clipMaxY - epsilon);

        var fbWidthI = (int)fbWidthF;
        var fbHeightI = (int)fbHeightF;
        if (minX < 0)
        {
            minX = 0;
        }

        if (minY < 0)
        {
            minY = 0;
        }

        if (maxX > fbWidthI)
        {
            maxX = fbWidthI;
        }

        if (maxY > fbHeightI)
        {
            maxY = fbHeightI;
        }

        if (maxX <= minX || maxY <= minY)
        {
            scissorX = 0;
            scissorY = 0;
            scissorW = 0;
            scissorH = 0;
            return false;
        }

        scissorX = minX;
        scissorY = minY;
        scissorW = (uint)(maxX - minX);
        scissorH = (uint)(maxY - minY);
        return true;
    }

    private bool TryComputeDynamicCoverageScissor(
        UiDrawData drawData,
        float clipOffsetX,
        float clipOffsetY,
        float clipScaleX,
        float clipScaleY,
        float fbWidth,
        float fbHeight,
        out int scissorX,
        out int scissorY,
        out uint scissorW,
        out uint scissorH)
    {
        var hasCoverage = false;
        var minX = 0f;
        var minY = 0f;
        var maxX = 0f;
        var maxY = 0f;

        for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
        {
            var drawList = drawData.DrawLists[listIndex];
            for (var cmdIndex = 0; cmdIndex < drawList.Commands.Count; cmdIndex++)
            {
                ref readonly var cmd = ref drawList.Commands.ItemRef(cmdIndex);
                if (IsStaticLayerGeometryTag(cmd.UserData, out _))
                {
                    continue;
                }

                var cmdTranslationX = cmd.Translation.X;
                var cmdTranslationY = cmd.Translation.Y;
                var clipMinX = (cmd.ClipRect.X + cmdTranslationX - clipOffsetX) * clipScaleX;
                var clipMinY = (cmd.ClipRect.Y + cmdTranslationY - clipOffsetY) * clipScaleY;
                var clipMaxX = (cmd.ClipRect.X + cmd.ClipRect.Width + cmdTranslationX - clipOffsetX) * clipScaleX;
                var clipMaxY = (cmd.ClipRect.Y + cmd.ClipRect.Height + cmdTranslationY - clipOffsetY) * clipScaleY;

                if (clipMaxX <= 0f || clipMaxY <= 0f || clipMinX >= fbWidth || clipMinY >= fbHeight)
                {
                    continue;
                }

                if (!hasCoverage)
                {
                    minX = clipMinX;
                    minY = clipMinY;
                    maxX = clipMaxX;
                    maxY = clipMaxY;
                    hasCoverage = true;
                }
                else
                {
                    minX = MathF.Min(minX, clipMinX);
                    minY = MathF.Min(minY, clipMinY);
                    maxX = MathF.Max(maxX, clipMaxX);
                    maxY = MathF.Max(maxY, clipMaxY);
                }
            }
        }

        if (!hasCoverage)
        {
            scissorX = 0;
            scissorY = 0;
            scissorW = 0;
            scissorH = 0;
            return false;
        }

        var minXi = (int)MathF.Max(0f, MathF.Floor(minX));
        var minYi = (int)MathF.Max(0f, MathF.Floor(minY));
        var maxXi = (int)MathF.Min(fbWidth, MathF.Ceiling(maxX));
        var maxYi = (int)MathF.Min(fbHeight, MathF.Ceiling(maxY));

        if (maxXi <= minXi || maxYi <= minYi)
        {
            scissorX = 0;
            scissorY = 0;
            scissorW = 0;
            scissorH = 0;
            return false;
        }

        scissorX = minXi;
        scissorY = minYi;
        scissorW = (uint)(maxXi - minXi);
        scissorH = (uint)(maxYi - minYi);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe void RecordCommandBuffer(
        CommandBuffer commandBuffer,
        uint imageIndex,
        UiDrawData drawData,
        VkBuffer vertexBuffer,
        VkBuffer indexBuffer,
        Dictionary<int, StaticGeometryBuffer> staticBindings,
        bool taaNeedsClear,
        float jitterX,
        float jitterY,
        out long recordTextureLookupTicks,
        out long recordClippingTicks,
        out long recordDescriptorBindTicks,
        out long recordDrawCallTicks)
    {
        var profileEnabled = _profilingEnabled;
        var textureLookupTicksLocal = 0L;
        var clippingTicksLocal = 0L;
        var descriptorBindTicksLocal = 0L;
        var drawCallTicksLocal = 0L;

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        Check(_vk.BeginCommandBuffer(commandBuffer, &beginInfo));

        var fbWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        var fbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);

        var clearValue = new ClearValue
        {
            Color = _clearColorValue,
        };

        var renderPassInfo = new RenderPassBeginInfo
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _framebuffers[imageIndex],
            RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)fbWidth, (uint)fbHeight)),
            ClearValueCount = 1,
            PClearValues = &clearValue,
        };

        var displaySize = drawData.DisplaySize;
        var displayPos = drawData.DisplayPos;
        var scaleX = 2f / displaySize.X;
        var scaleY = 2f / displaySize.Y;
        var translateX = -1f - displayPos.X * scaleX;
        var translateY = -1f - displayPos.Y * scaleY;
        var jitterTranslateX = translateX + (2f * jitterX / displaySize.X);
        var jitterTranslateY = translateY + (2f * jitterY / displaySize.Y);
        var normalPushConstants = stackalloc float[4];
        normalPushConstants[0] = scaleX;
        normalPushConstants[1] = scaleY;
        normalPushConstants[2] = translateX;
        normalPushConstants[3] = translateY;
        var temporalPushConstants = stackalloc float[4];
        temporalPushConstants[0] = scaleX;
        temporalPushConstants[1] = scaleY;
        temporalPushConstants[2] = jitterTranslateX;
        temporalPushConstants[3] = jitterTranslateY;


        var clipOffset = drawData.DisplayPos;
        var clipScale = drawData.FramebufferScale;
        var clipOffsetX = clipOffset.X;
        var clipOffsetY = clipOffset.Y;
        var clipScaleX = clipScale.X;
        var clipScaleY = clipScale.Y;
        var fontTextureId = _options.FontTextureId.Value is 0 ? new UiTextureId(1) : _options.FontTextureId;
        var fbWidthF = (float)fbWidth;
        var fbHeightF = (float)fbHeight;

        var postProcessPass = (_taaEnabled || _fxaaEnabled)
                      && _overlayRenderPass.Handle is not 0
                      && imageIndex < _overlayFramebuffers.Length;
        var overlayTextPass = postProcessPass && _options.TaaExcludeFont;
        var layerComposePass = _layerTextureComposeEnabled
                       && _layerTextureBackendEnabled
                       && !postProcessPass
                       && _overlayRenderPass.Handle is not 0
                       && imageIndex < _overlayFramebuffers.Length
                       && _layerTextureCache.DescriptorSet.Handle is not 0;
        var layerComposeSignature = 0UL;
        var hasLayerComposeSignature = layerComposePass && TryComputeLayerComposeSignature(drawData, out layerComposeSignature);
        var useLayerComposePath = layerComposePass && hasLayerComposeSignature;
        var canReuseLayerTexture = useLayerComposePath
                       && _layerTextureCacheInitialized
                       && _layerTextureComposeSignatureValid
                       && _layerTextureComposeSignature == layerComposeSignature;

        void DrawCommandLists(bool renderFontOnly, bool renderNonFontOnly, bool renderStaticOnly, bool renderDynamicOnly)
        {
            var viewport = new Viewport(0, 0, fbWidth, fbHeight, 0, 1);
            _vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);

            uint globalVertexOffset = 0;
            uint globalIndexOffset = 0;
            var hasBoundGeometryBuffers = false;
            var boundVertexBuffer = default(VkBuffer);
            var boundIndexBuffer = default(VkBuffer);
            var hasDescriptorSet = false;
            var lastDescriptorSet = default(DescriptorSet);
            var hasScissor = false;
            var lastScissorX = 0;
            var lastScissorY = 0;
            var lastScissorW = 0u;
            var lastScissorH = 0u;
            var hasLastTexture = false;
            var lastTextureId = default(UiTextureId);
            var lastTextureValid = false;
            var lastTexture = default(TextureResource);
            var currentPipeline = default(Pipeline);
            var hasPushMode = false;
            var currentTemporalPushMode = false;
            var lastPushTranslateX = 0f;
            var lastPushTranslateY = 0f;
            var dynamicPushConstants = stackalloc float[4];
            dynamicPushConstants[0] = scaleX;
            dynamicPushConstants[1] = scaleY;
            

            for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
            {
                var drawList = drawData.DrawLists[listIndex];
                var drawListVertexCount = drawList.Vertices.Count;
                var drawListIndexCount = drawList.Indices.Count;
                var hasStaticBinding = staticBindings.TryGetValue(listIndex, out var staticBinding);
                if (renderStaticOnly && !hasStaticBinding)
                {
                    continue;
                }

                if (renderDynamicOnly && hasStaticBinding)
                {
                    continue;
                }

                var targetVertexBuffer = hasStaticBinding ? staticBinding.VertexBuffer : vertexBuffer;
                var targetIndexBuffer = hasStaticBinding ? staticBinding.IndexBuffer : indexBuffer;
                if (!hasBoundGeometryBuffers
                    || boundVertexBuffer.Handle != targetVertexBuffer.Handle
                    || boundIndexBuffer.Handle != targetIndexBuffer.Handle)
                {
                    ulong vertexOffset = 0;
                    _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &targetVertexBuffer, &vertexOffset);
                    _vk.CmdBindIndexBuffer(commandBuffer, targetIndexBuffer, 0, IndexType.Uint32);
                    boundVertexBuffer = targetVertexBuffer;
                    boundIndexBuffer = targetIndexBuffer;
                    hasBoundGeometryBuffers = true;
                }

                var commandCount = drawList.Commands.Count;
                for (var cmdIndex = 0; cmdIndex < commandCount; cmdIndex++)
                {
                    ref readonly var cmd = ref drawList.Commands.ItemRef(cmdIndex);
                    var isFontCommand = cmd.TextureId.Equals(fontTextureId);
                    if (renderFontOnly && !isFontCommand)
                    {
                        continue;
                    }

                    if (renderNonFontOnly && isFontCommand)
                    {
                        continue;
                    }

                    var textureLookupStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
                    if (!hasLastTexture || !cmd.TextureId.Equals(lastTextureId))
                    {
                        lastTextureId = cmd.TextureId;
                        hasLastTexture = true;
                        if (!_textures.TryGetValue(cmd.TextureId, out lastTexture))
                        {
                            lastTextureValid = false;
                            if (profileEnabled)
                            {
                                textureLookupTicksLocal += Stopwatch.GetTimestamp() - textureLookupStart;
                            }
                            continue;
                        }

                        lastTextureValid = true;
                    }
                    else if (!lastTextureValid)
                    {
                        if (profileEnabled)
                        {
                                textureLookupTicksLocal += Stopwatch.GetTimestamp() - textureLookupStart;
                        }
                        continue;
                    }

                    if (profileEnabled)
                    {
                            textureLookupTicksLocal += Stopwatch.GetTimestamp() - textureLookupStart;
                    }

                    var texture = lastTexture;
                    var cmdTranslationX = cmd.Translation.X;
                    var cmdTranslationY = cmd.Translation.Y;

                    var clippingStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
                    if (!TryComputeScissorRect(
                        cmd,
                        clipOffsetX,
                        clipOffsetY,
                        clipScaleX,
                        clipScaleY,
                        fbWidthF,
                        fbHeightF,
                        out var scissorX,
                        out var scissorY,
                        out var scissorW,
                        out var scissorH))
                    {
                        if (profileEnabled)
                        {
                            clippingTicksLocal += Stopwatch.GetTimestamp() - clippingStart;
                        }

                        continue;
                    }

                    if (!hasScissor
                        || scissorX != lastScissorX
                        || scissorY != lastScissorY
                        || scissorW != lastScissorW
                        || scissorH != lastScissorH)
                    {
                        var scissor = new Rect2D(new Offset2D(scissorX, scissorY), new Extent2D(scissorW, scissorH));
                        _vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);
                        lastScissorX = scissorX;
                        lastScissorY = scissorY;
                        lastScissorW = scissorW;
                        lastScissorH = scissorH;
                        hasScissor = true;
                    }

                    if (profileEnabled)
                    {
                        clippingTicksLocal += Stopwatch.GetTimestamp() - clippingStart;
                    }

                    var descriptorBindStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
                    var useTemporalJitter = _taaEnabled
                                            && !renderFontOnly
                                            && (!_options.TaaExcludeFont || !isFontCommand);
                    var desiredPipeline = _graphicsPipeline;
                    if (currentPipeline.Handle != desiredPipeline.Handle)
                    {
                        _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, desiredPipeline);
                        currentPipeline = desiredPipeline;
                    }

                    var pushTranslateX = useTemporalJitter ? jitterTranslateX : translateX;
                    var pushTranslateY = useTemporalJitter ? jitterTranslateY : translateY;
                    if (cmdTranslationX != 0f)
                    {
                        pushTranslateX += scaleX * cmdTranslationX;
                    }

                    if (cmdTranslationY != 0f)
                    {
                        pushTranslateY += scaleY * cmdTranslationY;
                    }
                    if (!hasPushMode
                        || currentTemporalPushMode != useTemporalJitter
                        || MathF.Abs(lastPushTranslateX - pushTranslateX) > 0.000001f
                        || MathF.Abs(lastPushTranslateY - pushTranslateY) > 0.000001f)
                    {
                        dynamicPushConstants[2] = pushTranslateX;
                        dynamicPushConstants[3] = pushTranslateY;
                        _vk.CmdPushConstants(commandBuffer, _pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)(sizeof(float) * 4), dynamicPushConstants);
                        currentTemporalPushMode = useTemporalJitter;
                        lastPushTranslateX = pushTranslateX;
                        lastPushTranslateY = pushTranslateY;
                        hasPushMode = true;
                    }

                    var descriptorSet = texture.DescriptorSet;
                    if (!hasDescriptorSet || descriptorSet.Handle != lastDescriptorSet.Handle)
                    {
                        _vk.CmdBindDescriptorSets(
                            commandBuffer,
                            PipelineBindPoint.Graphics,
                            _pipelineLayout,
                            0,
                            1,
                            &descriptorSet,
                            0,
                            null
                        );
                        lastDescriptorSet = descriptorSet;
                        hasDescriptorSet = true;
                    }

                    if (profileEnabled)
                    {
                        descriptorBindTicksLocal += Stopwatch.GetTimestamp() - descriptorBindStart;
                    }

                    var firstIndex = hasStaticBinding ? cmd.IndexOffset : cmd.IndexOffset + globalIndexOffset;
                    var vertexOffsetCommand = hasStaticBinding
                        ? (int)cmd.VertexOffset
                        : (int)(cmd.VertexOffset + globalVertexOffset);
                    var drawCallStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
                    _vk.CmdDrawIndexed(commandBuffer, cmd.ElementCount, 1, firstIndex, vertexOffsetCommand, 0);
                    if (profileEnabled)
                    {
                        drawCallTicksLocal += Stopwatch.GetTimestamp() - drawCallStart;
                    }
                }

                if (!hasStaticBinding)
                {
                    globalVertexOffset += (uint)drawListVertexCount;
                    globalIndexOffset += (uint)drawListIndexCount;
                }
            }
        }

        _vk.CmdBeginRenderPass(commandBuffer, &renderPassInfo, SubpassContents.Inline);

        if (_taaEnabled && taaNeedsClear)
        {
            var clearColor = new ClearAttachment
            {
                AspectMask = ImageAspectFlags.ColorBit,
                ColorAttachment = 0,
                ClearValue = new ClearValue { Color = _clearColorValue },
            };
            var clearRect = new ClearRect
            {
                Rect = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)fbWidth, (uint)fbHeight)),
                BaseArrayLayer = 0,
                LayerCount = 1,
            };
            _vk.CmdClearAttachments(commandBuffer, 1, &clearColor, 1, &clearRect);
        }

        if (!useLayerComposePath)
        {
            DrawCommandLists(renderFontOnly: false, renderNonFontOnly: overlayTextPass, renderStaticOnly: false, renderDynamicOnly: false);
        }
        else if (!canReuseLayerTexture)
        {
            DrawCommandLists(renderFontOnly: false, renderNonFontOnly: false, renderStaticOnly: true, renderDynamicOnly: false);
        }
        _vk.CmdEndRenderPass(commandBuffer);

        if (_taaEnabled)
        {
            var taaImage = _taaRenderImages[imageIndex];
            var swapchainImage = _swapchainImages[imageIndex];
            var hasHistoryForBlend = imageIndex < _taaHistoryValid.Length
                                     && _taaHistoryValid[imageIndex]
                                     && imageIndex < _taaHistoryDescriptorSets.Length;

            TransitionImageLayout(commandBuffer, taaImage, ImageLayout.ColorAttachmentOptimal, ImageLayout.TransferSrcOptimal);
            TransitionImageLayout(commandBuffer, swapchainImage, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);

            var copyRegion = new ImageCopy
            {
                SrcSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
                DstSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
                Extent = new Extent3D((uint)fbWidth, (uint)fbHeight, 1),
            };

            _vk.CmdCopyImage(
                commandBuffer,
                taaImage,
                ImageLayout.TransferSrcOptimal,
                swapchainImage,
                ImageLayout.TransferDstOptimal,
                1,
                &copyRegion);

            TransitionImageLayout(commandBuffer, taaImage, ImageLayout.TransferSrcOptimal, ImageLayout.ColorAttachmentOptimal);
            TransitionImageLayout(commandBuffer, swapchainImage, ImageLayout.TransferDstOptimal, ImageLayout.PresentSrcKhr);

            if (postProcessPass)
            {
                var overlayRenderPassInfo = new RenderPassBeginInfo
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _overlayRenderPass,
                    Framebuffer = _overlayFramebuffers[imageIndex],
                    RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)fbWidth, (uint)fbHeight)),
                    ClearValueCount = 0,
                    PClearValues = null,
                };

                _vk.CmdBeginRenderPass(commandBuffer, &overlayRenderPassInfo, SubpassContents.Inline);

                if (hasHistoryForBlend)
                {
                    var viewport = new Viewport(0, 0, fbWidth, fbHeight, 0, 1);
                    _vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);

                    var fullScissor = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)fbWidth, (uint)fbHeight));
                    _vk.CmdSetScissor(commandBuffer, 0, 1, &fullScissor);

                    var localVertexBuffer = vertexBuffer;
                    ulong vertexOffset = 0;
                    _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &localVertexBuffer, &vertexOffset);
                    _vk.CmdBindIndexBuffer(commandBuffer, indexBuffer, 0, IndexType.Uint32);

                    _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _taaGraphicsPipeline);
                    _vk.CmdPushConstants(commandBuffer, _pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)(sizeof(float) * 4), normalPushConstants);

                    var historyWeight = GetTaaHistoryBlendWeight();
                    var blendConstants = stackalloc float[4];
                    blendConstants[0] = historyWeight;
                    blendConstants[1] = historyWeight;
                    blendConstants[2] = historyWeight;
                    blendConstants[3] = historyWeight;
                    _vk.CmdSetBlendConstants(commandBuffer, blendConstants);

                    var historyDescriptorSet = _taaHistoryDescriptorSets[imageIndex];
                    _vk.CmdBindDescriptorSets(
                        commandBuffer,
                        PipelineBindPoint.Graphics,
                        _pipelineLayout,
                        0,
                        1,
                        &historyDescriptorSet,
                        0,
                        null);

                    var temporalFirstIndex = (uint)drawData.TotalIndexCount;
                    var temporalVertexOffset = drawData.TotalVertexCount;
                    _vk.CmdDrawIndexed(commandBuffer, 6, 1, temporalFirstIndex, temporalVertexOffset, 0);
                }

                if (overlayTextPass)
                {
                    DrawCommandLists(renderFontOnly: true, renderNonFontOnly: false, renderStaticOnly: false, renderDynamicOnly: false);
                }
                _vk.CmdEndRenderPass(commandBuffer);
            }

            if (imageIndex < _taaHistoryImages.Length)
            {
                var historyImage = _taaHistoryImages[imageIndex];
                var historyOldLayout = imageIndex < _taaHistoryValid.Length && _taaHistoryValid[imageIndex]
                    ? ImageLayout.ShaderReadOnlyOptimal
                    : ImageLayout.Undefined;

                TransitionImageLayout(commandBuffer, taaImage, ImageLayout.ColorAttachmentOptimal, ImageLayout.TransferSrcOptimal);
                TransitionImageLayout(commandBuffer, historyImage, historyOldLayout, ImageLayout.TransferDstOptimal);

                var historyCopyRegion = new ImageCopy
                {
                    SrcSubresource = new ImageSubresourceLayers
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                    },
                    DstSubresource = new ImageSubresourceLayers
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                    },
                    Extent = new Extent3D((uint)fbWidth, (uint)fbHeight, 1),
                };

                _vk.CmdCopyImage(
                    commandBuffer,
                    taaImage,
                    ImageLayout.TransferSrcOptimal,
                    historyImage,
                    ImageLayout.TransferDstOptimal,
                    1,
                    &historyCopyRegion);

                TransitionImageLayout(commandBuffer, historyImage, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);
                TransitionImageLayout(commandBuffer, taaImage, ImageLayout.TransferSrcOptimal, ImageLayout.ColorAttachmentOptimal);
            }
        }
        else if (_fxaaEnabled)
        {
            var taaImage = _taaRenderImages[imageIndex];

            if (imageIndex < _taaHistoryImages.Length)
            {
                var historyImage = _taaHistoryImages[imageIndex];
                var historyOldLayout = imageIndex < _taaHistoryValid.Length && _taaHistoryValid[imageIndex]
                    ? ImageLayout.ShaderReadOnlyOptimal
                    : ImageLayout.Undefined;

                TransitionImageLayout(commandBuffer, taaImage, ImageLayout.ColorAttachmentOptimal, ImageLayout.TransferSrcOptimal);
                TransitionImageLayout(commandBuffer, historyImage, historyOldLayout, ImageLayout.TransferDstOptimal);

                var historyCopyRegion = new ImageCopy
                {
                    SrcSubresource = new ImageSubresourceLayers
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                    },
                    DstSubresource = new ImageSubresourceLayers
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                    },
                    Extent = new Extent3D((uint)fbWidth, (uint)fbHeight, 1),
                };

                _vk.CmdCopyImage(
                    commandBuffer,
                    taaImage,
                    ImageLayout.TransferSrcOptimal,
                    historyImage,
                    ImageLayout.TransferDstOptimal,
                    1,
                    &historyCopyRegion);

                TransitionImageLayout(commandBuffer, historyImage, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);
                TransitionImageLayout(commandBuffer, taaImage, ImageLayout.TransferSrcOptimal, ImageLayout.ColorAttachmentOptimal);
            }

            if (postProcessPass)
            {
                var overlayRenderPassInfo = new RenderPassBeginInfo
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = _overlayRenderPass,
                    Framebuffer = _overlayFramebuffers[imageIndex],
                    RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)fbWidth, (uint)fbHeight)),
                    ClearValueCount = 0,
                    PClearValues = null,
                };

                _vk.CmdBeginRenderPass(commandBuffer, &overlayRenderPassInfo, SubpassContents.Inline);

                if (imageIndex < _taaHistoryDescriptorSets.Length)
                {
                    var viewport = new Viewport(0, 0, fbWidth, fbHeight, 0, 1);
                    _vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);

                    var fullScissor = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)fbWidth, (uint)fbHeight));
                    _vk.CmdSetScissor(commandBuffer, 0, 1, &fullScissor);

                    var localVertexBuffer = vertexBuffer;
                    ulong vertexOffset = 0;
                    _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &localVertexBuffer, &vertexOffset);
                    _vk.CmdBindIndexBuffer(commandBuffer, indexBuffer, 0, IndexType.Uint32);

                    _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _fxaaGraphicsPipeline);
                    _vk.CmdPushConstants(commandBuffer, _pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)(sizeof(float) * 4), normalPushConstants);

                    var historyDescriptorSet = _taaHistoryDescriptorSets[imageIndex];
                    _vk.CmdBindDescriptorSets(
                        commandBuffer,
                        PipelineBindPoint.Graphics,
                        _pipelineLayout,
                        0,
                        1,
                        &historyDescriptorSet,
                        0,
                        null);

                    var temporalFirstIndex = (uint)drawData.TotalIndexCount;
                    var temporalVertexOffset = drawData.TotalVertexCount;
                    _vk.CmdDrawIndexed(commandBuffer, 6, 1, temporalFirstIndex, temporalVertexOffset, 0);
                }

                if (overlayTextPass)
                {
                    DrawCommandLists(renderFontOnly: true, renderNonFontOnly: false, renderStaticOnly: false, renderDynamicOnly: false);
                }
                _vk.CmdEndRenderPass(commandBuffer);
            }
        }
        else if (useLayerComposePath)
        {
            var swapchainImage = _swapchainImages[imageIndex];
            var layerTextureImage = _layerTextureCache.Image;
            var hasDynamicCoverage = TryComputeDynamicCoverageScissor(
                drawData,
                clipOffsetX,
                clipOffsetY,
                clipScaleX,
                clipScaleY,
                fbWidthF,
                fbHeightF,
                out var dynamicScissorX,
                out var dynamicScissorY,
                out var dynamicScissorW,
                out var dynamicScissorH);

            if (!canReuseLayerTexture)
            {
                TransitionImageLayout(commandBuffer, swapchainImage, ImageLayout.PresentSrcKhr, ImageLayout.TransferSrcOptimal);
                var layerTextureOldLayout = _layerTextureCacheInitialized
                    ? ImageLayout.ShaderReadOnlyOptimal
                    : ImageLayout.Undefined;
                TransitionImageLayout(commandBuffer, layerTextureImage, layerTextureOldLayout, ImageLayout.TransferDstOptimal);

                var copyRegion = new ImageCopy
                {
                    SrcSubresource = new ImageSubresourceLayers
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                    },
                    DstSubresource = new ImageSubresourceLayers
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                    },
                    Extent = new Extent3D((uint)fbWidth, (uint)fbHeight, 1),
                };

                _vk.CmdCopyImage(
                    commandBuffer,
                    swapchainImage,
                    ImageLayout.TransferSrcOptimal,
                    layerTextureImage,
                    ImageLayout.TransferDstOptimal,
                    1,
                    &copyRegion);

                TransitionImageLayout(commandBuffer, layerTextureImage, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);
                TransitionImageLayout(commandBuffer, swapchainImage, ImageLayout.TransferSrcOptimal, ImageLayout.PresentSrcKhr);
                _layerTextureCacheInitialized = true;
                if (hasLayerComposeSignature)
                {
                    _layerTextureComposeSignature = layerComposeSignature;
                    _layerTextureComposeSignatureValid = true;
                }
                else
                {
                    _layerTextureComposeSignature = 0;
                    _layerTextureComposeSignatureValid = false;
                }
            }

            var overlayRenderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = _overlayRenderPass,
                Framebuffer = _overlayFramebuffers[imageIndex],
                RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)fbWidth, (uint)fbHeight)),
                ClearValueCount = 0,
                PClearValues = null,
            };

            if (canReuseLayerTexture || hasDynamicCoverage)
            {
                _vk.CmdBeginRenderPass(commandBuffer, &overlayRenderPassInfo, SubpassContents.Inline);

                var viewport = new Viewport(0, 0, fbWidth, fbHeight, 0, 1);
                _vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);

                if (!canReuseLayerTexture && hasDynamicCoverage)
                {
                    var partialScissor = new Rect2D(
                        new Offset2D(dynamicScissorX, dynamicScissorY),
                        new Extent2D(dynamicScissorW, dynamicScissorH));
                    _vk.CmdSetScissor(commandBuffer, 0, 1, &partialScissor);
                }
                else
                {
                    var fullScissor = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)fbWidth, (uint)fbHeight));
                    _vk.CmdSetScissor(commandBuffer, 0, 1, &fullScissor);
                }

                var localVertexBuffer = vertexBuffer;
                ulong vertexOffset = 0;
                _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &localVertexBuffer, &vertexOffset);
                _vk.CmdBindIndexBuffer(commandBuffer, indexBuffer, 0, IndexType.Uint32);

                _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _graphicsPipeline);
                _vk.CmdPushConstants(commandBuffer, _pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)(sizeof(float) * 4), normalPushConstants);

                var descriptorSet = _layerTextureCache.DescriptorSet;
                _vk.CmdBindDescriptorSets(
                    commandBuffer,
                    PipelineBindPoint.Graphics,
                    _pipelineLayout,
                    0,
                    1,
                    &descriptorSet,
                    0,
                    null);

                var composeFirstIndex = (uint)drawData.TotalIndexCount;
                var composeVertexOffset = drawData.TotalVertexCount;
                _vk.CmdDrawIndexed(commandBuffer, 6, 1, composeFirstIndex, composeVertexOffset, 0);

                DrawCommandLists(renderFontOnly: false, renderNonFontOnly: false, renderStaticOnly: false, renderDynamicOnly: true);

                _vk.CmdEndRenderPass(commandBuffer);
            }
        }

        recordTextureLookupTicks = textureLookupTicksLocal;
        recordClippingTicks = clippingTicksLocal;
        recordDescriptorBindTicks = descriptorBindTicksLocal;
        recordDrawCallTicks = drawCallTicksLocal;

        Check(_vk.EndCommandBuffer(commandBuffer));
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

            frame.RenderBuffers = renderBuffers;

            _frames[i] = frame;
        }
    }

    private void DestroyStaticGeometryBuffers()
    {
        if (_staticGeometryBuffers.Count is 0)
        {
            _staticGeometryLastSeenFrame.Clear();
            _textureLayerTagHashCache.Clear();
            _frameStaticBindings.Clear();
            return;
        }

        foreach (var pair in _staticGeometryBuffers)
        {
            var staticBuffer = pair.Value;
            DestroyBufferResource(new BufferResource(staticBuffer.VertexBuffer, staticBuffer.VertexMemory));
            DestroyBufferResource(new BufferResource(staticBuffer.IndexBuffer, staticBuffer.IndexMemory));
        }

        _staticGeometryBuffers.Clear();
        _staticGeometryLastSeenFrame.Clear();
        _textureLayerTagHashCache.Clear();
        _frameStaticBindings.Clear();
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
        var fontTextureId = _options.FontTextureId.Value is 0 ? new UiTextureId(1) : _options.FontTextureId;
        var isFontTexture = update.TextureId.Equals(fontTextureId);
        var descriptorSet = AllocateTextureDescriptorSet(view, isFontTexture);

        return new TextureResource(image, memory, view, descriptorSet, update.Width, update.Height, format);
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
        var data = update.RgbaPixels.Span;
        var bufferSize = (nuint)data.Length;
        EnsureStagingBuffer(bufferSize);

        data.CopyTo(new Span<byte>(_stagingMappedPtr, data.Length));

        var commandBuffer = BeginSingleTimeCommands();

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

            if ((memProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
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

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct UiVertex
    {
        public float PositionX;
        public float PositionY;
        public float UVx;
        public float UVy;
        public uint Color;
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

    private void CreateLayerTextureCacheScaffold()
    {
        DestroyLayerTextureCacheScaffold();

        if (_swapchainExtent.Width is 0 || _swapchainExtent.Height is 0)
        {
            _layerTextureCache = default;
            return;
        }

        var width = (int)_swapchainExtent.Width;
        var height = (int)_swapchainExtent.Height;
        CreateImage(
            width,
            height,
            _swapchainFormat,
            ImageTiling.Optimal,
            ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            MemoryPropertyFlags.DeviceLocalBit,
            out var image,
            out var memory
        );

        var view = CreateImageView(image, _swapchainFormat);
        var descriptorSet = AllocateTextureDescriptorSet(view, isFontTexture: false);
        _layerTextureCache = new LayerTextureCacheResource(image, memory, view, descriptorSet, width, height);
        _layerTextureCacheInitialized = false;
        _layerTextureComposeSignature = 0;
        _layerTextureComposeSignatureValid = false;
    }

    private unsafe void DestroyLayerTextureCacheScaffold()
    {
        var resource = _layerTextureCache;
        if (resource.DescriptorSet.Handle is not 0 && _descriptorPool.Handle is not 0)
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

        _layerTextureCache = default;
        _layerTextureCacheInitialized = false;
        _layerTextureComposeSignature = 0;
        _layerTextureComposeSignatureValid = false;
    }

    private unsafe void CreateFramebuffers()
    {
        _framebuffers = new Framebuffer[_swapchainImageViews.Length];
        _overlayFramebuffers = Array.Empty<Framebuffer>();

        if (_msaaSampleCount == SampleCountFlags.Count1Bit)
        {
            for (var i = 0; i < _swapchainImageViews.Length; i++)
            {
                var attachment = (_taaEnabled || _fxaaEnabled) ? _taaRenderImageViews[i] : _swapchainImageViews[i];
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

            if (_taaEnabled || _fxaaEnabled || (_layerTextureComposeEnabled && _layerTextureBackendEnabled))
            {
                _overlayFramebuffers = new Framebuffer[_swapchainImageViews.Length];
                for (var i = 0; i < _swapchainImageViews.Length; i++)
                {
                    var overlayAttachment = _swapchainImageViews[i];
                    var overlayFramebufferInfo = new FramebufferCreateInfo
                    {
                        SType = StructureType.FramebufferCreateInfo,
                        RenderPass = _overlayRenderPass,
                        AttachmentCount = 1,
                        PAttachments = &overlayAttachment,
                        Width = _swapchainExtent.Width,
                        Height = _swapchainExtent.Height,
                        Layers = 1,
                    };

                    fixed (Framebuffer* framebuffer = &_overlayFramebuffers[i])
                    {
                        Check(_vk.CreateFramebuffer(_device, &overlayFramebufferInfo, null, framebuffer));
                    }
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

    private void CreateTaaRenderTargets()
    {
        if ((!_taaEnabled && !_fxaaEnabled) || _msaaSampleCount != SampleCountFlags.Count1Bit)
        {
            _taaRenderImages = Array.Empty<Image>();
            _taaRenderMemories = Array.Empty<DeviceMemory>();
            _taaRenderImageViews = Array.Empty<ImageView>();
            _taaHistoryImages = Array.Empty<Image>();
            _taaHistoryMemories = Array.Empty<DeviceMemory>();
            _taaHistoryImageViews = Array.Empty<ImageView>();
            _taaHistoryDescriptorSets = Array.Empty<DescriptorSet>();
            return;
        }

        var count = _swapchainImages.Length;
        _taaRenderImages = new Image[count];
        _taaRenderMemories = new DeviceMemory[count];
        _taaRenderImageViews = new ImageView[count];
        _taaHistoryImages = new Image[count];
        _taaHistoryMemories = new DeviceMemory[count];
        _taaHistoryImageViews = new ImageView[count];
        _taaHistoryDescriptorSets = new DescriptorSet[count];

        for (var i = 0; i < count; i++)
        {
            CreateImage(
                (int)_swapchainExtent.Width,
                (int)_swapchainExtent.Height,
                _swapchainFormat,
                ImageTiling.Optimal,
                ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
                MemoryPropertyFlags.DeviceLocalBit,
                out _taaRenderImages[i],
                out _taaRenderMemories[i]);

            _taaRenderImageViews[i] = CreateImageView(_taaRenderImages[i], _swapchainFormat);

            CreateImage(
                (int)_swapchainExtent.Width,
                (int)_swapchainExtent.Height,
                _swapchainFormat,
                ImageTiling.Optimal,
                ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
                MemoryPropertyFlags.DeviceLocalBit,
                out _taaHistoryImages[i],
                out _taaHistoryMemories[i]);

            _taaHistoryImageViews[i] = CreateImageView(_taaHistoryImages[i], _swapchainFormat);
            _taaHistoryDescriptorSets[i] = AllocateTextureDescriptorSet(_taaHistoryImageViews[i], false);
        }
    }

    private unsafe void DestroyTaaRenderTargets()
    {
        foreach (var view in _taaRenderImageViews)
        {
            if (view.Handle is not 0)
            {
                _vk.DestroyImageView(_device, view, null);
            }
        }

        foreach (var image in _taaRenderImages)
        {
            if (image.Handle is not 0)
            {
                _vk.DestroyImage(_device, image, null);
            }
        }

        foreach (var memory in _taaRenderMemories)
        {
            if (memory.Handle is not 0)
            {
                _vk.FreeMemory(_device, memory, null);
            }
        }

        if (_descriptorPool.Handle is not 0)
        {
            for (var i = 0; i < _taaHistoryDescriptorSets.Length; i++)
            {
                var descriptorSet = _taaHistoryDescriptorSets[i];
                if (descriptorSet.Handle is not 0)
                {
                    _vk.FreeDescriptorSets(_device, _descriptorPool, 1, &descriptorSet);
                }
            }
        }

        foreach (var view in _taaHistoryImageViews)
        {
            if (view.Handle is not 0)
            {
                _vk.DestroyImageView(_device, view, null);
            }
        }

        foreach (var image in _taaHistoryImages)
        {
            if (image.Handle is not 0)
            {
                _vk.DestroyImage(_device, image, null);
            }
        }

        foreach (var memory in _taaHistoryMemories)
        {
            if (memory.Handle is not 0)
            {
                _vk.FreeMemory(_device, memory, null);
            }
        }

        _taaRenderImageViews = Array.Empty<ImageView>();
        _taaRenderImages = Array.Empty<Image>();
        _taaRenderMemories = Array.Empty<DeviceMemory>();
        _taaHistoryDescriptorSets = Array.Empty<DescriptorSet>();
        _taaHistoryImageViews = Array.Empty<ImageView>();
        _taaHistoryImages = Array.Empty<Image>();
        _taaHistoryMemories = Array.Empty<DeviceMemory>();
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
