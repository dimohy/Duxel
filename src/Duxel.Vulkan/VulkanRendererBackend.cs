using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    bool EnableVSync = true
);

public sealed unsafe class VulkanRendererBackend : IRendererBackend
{
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
    private uint _graphicsQueueFamily;
    private SwapchainKHR _swapchain;
    private Image[] _swapchainImages = Array.Empty<Image>();
    private ImageView[] _swapchainImageViews = Array.Empty<ImageView>();
    private Format _swapchainFormat;
    private Extent2D _swapchainExtent;
    private RenderPass _renderPass;
    private DescriptorSetLayout _descriptorSetLayout;
    private PipelineLayout _pipelineLayout;
    private Sampler _fontSampler;
    private Pipeline _graphicsPipeline;
    private ShaderModule _vertexShaderModule;
    private ShaderModule _fragmentShaderModule;
    private PipelineCache _pipelineCache;
    private DescriptorPool _descriptorPool;
    private Framebuffer[] _framebuffers = Array.Empty<Framebuffer>();
    private CommandPool _uploadCommandPool;
    private CommandBuffer _uploadCommandBuffer;
    private Fence _uploadFence;
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

    // MSAA resources
    private const SampleCountFlags MsaaSampleCount = SampleCountFlags.Count4Bit;
    private Image _msaaColorImage;
    private DeviceMemory _msaaColorMemory;
    private ImageView _msaaColorImageView;

    private static readonly byte[] VertexShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui.vert.spv");
    private static readonly byte[] FragmentShaderSpirv = LoadEmbeddedShader("Duxel.Vulkan.Shaders.imgui.frag.spv");

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
        CreateDevice();
        LoadDeviceExtensions();
        CreateSwapchainResources();
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

        EnsureVertexBufferCapacity(frame, drawData.TotalVertexCount);
        EnsureIndexBufferCapacity(frame, drawData.TotalIndexCount);

        UploadGeometry(frame, drawData);

        var commandPool = frameData.CommandPool;
        var commandBuffer = frameData.CommandBuffer;
        Check(_vk.ResetCommandPool(_device, commandPool, 0));
        var vertexBuffer = frameData.RenderBuffers.VertexBuffer;
        var indexBuffer = frameData.RenderBuffers.IndexBuffer;
        RecordCommandBuffer(commandBuffer, imageIndex, drawData, vertexBuffer, indexBuffer);

        var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;
        var renderFinished = semaphores.RenderFinished;

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

        fixed (Fence* resetFence = &frameData.InFlight)
        {
            Check(_vk.ResetFences(_device, 1, resetFence));
        }
        fixed (Fence* fence = &frameData.InFlight)
        {
            Check(_vk.QueueSubmit(_graphicsQueue, 1, &submitInfo, *fence));
        }

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

        var presentResult = _khrSwapchain.QueuePresent(_graphicsQueue, &presentInfo);
        if (presentResult == Result.ErrorOutOfDateKhr || IsSuboptimal(presentResult))
        {
            RecreateSwapchain();
            return;
        }

        if (presentResult != Result.Success)
        {
            Check(presentResult);
        }
        _frameIndex++;
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

        _vk.DeviceWaitIdle(_device);

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
            _vk.DeviceWaitIdle(_device);
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

    private void DestroySwapchainDependentResources()
    {
        if (_device.Handle is not 0)
        {
            _vk.DeviceWaitIdle(_device);
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
            _vk.DeviceWaitIdle(_device);
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
        return Vk.False;
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

    private unsafe void CreateDevice()
    {
        var queuePriority = 1.0f;
        var queueCreateInfo = new DeviceQueueCreateInfo
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamily,
            QueueCount = 1,
            PQueuePriorities = &queuePriority,
        };

        var extensionPtr = SilkMarshal.StringArrayToPtr(BaseDeviceExtensions);

        try
        {
            var deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = &queueCreateInfo,
                EnabledExtensionCount = (uint)BaseDeviceExtensions.Length,
                PpEnabledExtensionNames = (byte**)extensionPtr,
            };

            fixed (Device* device = &_device)
            {
                Check(_vk.CreateDevice(_physicalDevice, &deviceCreateInfo, null, device));
            }

            _vk.GetDeviceQueue(_device, _graphicsQueueFamily, 0, out _graphicsQueue);
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

        throw new InvalidOperationException($"Vulkan call failed: {result}");
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
        // Attachment 0: MSAA color attachment (multisampled, not stored directly)
        var msaaColorAttachment = new AttachmentDescription
        {
            Format = _swapchainFormat,
            Samples = MsaaSampleCount,
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
        if (_descriptorSetLayout.Handle is not 0 && _pipelineLayout.Handle is not 0 && _fontSampler.Handle is not 0)
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

        var samplerInfo = new SamplerCreateInfo
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

        fixed (Sampler* sampler = &_fontSampler)
        {
            Check(_vk.CreateSampler(_device, &samplerInfo, null, sampler));
        }
    }

    private unsafe void CreateGraphicsPipeline()
    {
        _vertexShaderModule = CreateShaderModule(VertexShaderSpirv);
        _fragmentShaderModule = CreateShaderModule(FragmentShaderSpirv);

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
                RasterizationSamples = MsaaSampleCount,
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

            var dynamicStates = stackalloc DynamicState[2];
            dynamicStates[0] = DynamicState.Viewport;
            dynamicStates[1] = DynamicState.Scissor;

            var dynamicState = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
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

    private unsafe void UploadGeometry(int frame, UiDrawData drawData)
    {
        var frameData = _frames[frame];
        var renderBuffers = frameData.RenderBuffers;

        var vertexDst = (byte*)renderBuffers.VertexMappedPtr;
        var indexDst = (byte*)renderBuffers.IndexMappedPtr;

        for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
        {
            var drawList = drawData.DrawLists[listIndex];

            var vertexSrc = MemoryMarshal.AsBytes(drawList.Vertices.AsSpan());
            vertexSrc.CopyTo(new Span<byte>(vertexDst, vertexSrc.Length));
            vertexDst += vertexSrc.Length;

            var indexSrc = MemoryMarshal.AsBytes(drawList.Indices.AsSpan());
            indexSrc.CopyTo(new Span<byte>(indexDst, indexSrc.Length));
            indexDst += indexSrc.Length;
        }
    }

    private unsafe void RecordCommandBuffer(CommandBuffer commandBuffer, uint imageIndex, UiDrawData drawData, VkBuffer vertexBuffer, VkBuffer indexBuffer)
    {
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

        _vk.CmdBeginRenderPass(commandBuffer, &renderPassInfo, SubpassContents.Inline);
        _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _graphicsPipeline);

        var viewport = new Viewport(0, 0, fbWidth, fbHeight, 0, 1);
        _vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);

        var displaySize = drawData.DisplaySize;
        var displayPos = drawData.DisplayPos;
        var scaleX = 2f / displaySize.X;
        var scaleY = 2f / displaySize.Y;
        var translateX = -1f - displayPos.X * scaleX;
        var translateY = -1f - displayPos.Y * scaleY;

        var pushConstants = stackalloc float[4];
        pushConstants[0] = scaleX;
        pushConstants[1] = scaleY;
        pushConstants[2] = translateX;
        pushConstants[3] = translateY;
        _vk.CmdPushConstants(commandBuffer, _pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)(sizeof(float) * 4), pushConstants);

        ulong vertexOffset = 0;
        _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &vertexBuffer, &vertexOffset);
        _vk.CmdBindIndexBuffer(commandBuffer, indexBuffer, 0, IndexType.Uint32);

        var clipOffset = drawData.DisplayPos;
        var clipScale = drawData.FramebufferScale;
        var clipOffsetX = clipOffset.X;
        var clipOffsetY = clipOffset.Y;
        var clipScaleX = clipScale.X;
        var clipScaleY = clipScale.Y;

        uint globalVertexOffset = 0;
        uint globalIndexOffset = 0;
        var hasDescriptorSet = false;
        var lastDescriptorSet = default(DescriptorSet);
        var hasScissor = false;
        var lastScissor = default(Rect2D);
        var hasLastTexture = false;
        var lastTextureId = default(UiTextureId);
        var lastTextureValid = false;
        var lastTexture = default(TextureResource);
        var fbWidthF = (float)fbWidth;
        var fbHeightF = (float)fbHeight;

        for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
        {
            var drawList = drawData.DrawLists[listIndex];
            var drawListVertexCount = drawList.Vertices.Count;
            var drawListIndexCount = drawList.Indices.Count;
            var commandCount = drawList.Commands.Count;
            for (var cmdIndex = 0; cmdIndex < commandCount; cmdIndex++)
            {
                ref readonly var cmd = ref drawList.Commands.ItemRef(cmdIndex);
                if (!hasLastTexture || !cmd.TextureId.Equals(lastTextureId))
                {
                    lastTextureId = cmd.TextureId;
                    hasLastTexture = true;
                    if (!_textures.TryGetValue(cmd.TextureId, out lastTexture))
                    {
                        lastTextureValid = false;
                        continue;
                    }

                    lastTextureValid = true;
                }
                else if (!lastTextureValid)
                {
                    continue;
                }

                var texture = lastTexture;

                var clipMinX = (cmd.ClipRect.X - clipOffsetX) * clipScaleX;
                var clipMinY = (cmd.ClipRect.Y - clipOffsetY) * clipScaleY;
                var clipMaxX = (cmd.ClipRect.X + cmd.ClipRect.Width - clipOffsetX) * clipScaleX;
                var clipMaxY = (cmd.ClipRect.Y + cmd.ClipRect.Height - clipOffsetY) * clipScaleY;

                if (clipMaxX <= clipMinX || clipMaxY <= clipMinY)
                {
                    continue;
                }

                clipMinX = Math.Clamp(clipMinX, 0f, fbWidthF);
                clipMinY = Math.Clamp(clipMinY, 0f, fbHeightF);
                clipMaxX = Math.Clamp(clipMaxX, 0f, fbWidthF);
                clipMaxY = Math.Clamp(clipMaxY, 0f, fbHeightF);

                var scissor = new Rect2D(
                    new Offset2D((int)clipMinX, (int)clipMinY),
                    new Extent2D((uint)(clipMaxX - clipMinX), (uint)(clipMaxY - clipMinY))
                );

                if (!hasScissor
                    || scissor.Offset.X != lastScissor.Offset.X
                    || scissor.Offset.Y != lastScissor.Offset.Y
                    || scissor.Extent.Width != lastScissor.Extent.Width
                    || scissor.Extent.Height != lastScissor.Extent.Height)
                {
                    _vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);
                    lastScissor = scissor;
                    hasScissor = true;
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

                var firstIndex = cmd.IndexOffset + globalIndexOffset;
                var vertexOffsetCommand = (int)(cmd.VertexOffset + globalVertexOffset);
                _vk.CmdDrawIndexed(commandBuffer, cmd.ElementCount, 1, firstIndex, vertexOffsetCommand, 0);
            }

            globalVertexOffset += (uint)drawListVertexCount;
            globalIndexOffset += (uint)drawListIndexCount;
        }

        _vk.CmdEndRenderPass(commandBuffer);
        Check(_vk.EndCommandBuffer(commandBuffer));
    }

    private void DestroyGeometryBuffers()
    {
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
        var descriptorSet = AllocateTextureDescriptorSet(view);

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

    private unsafe void EnsureStagingBuffer(nuint size)
    {
        if (_stagingBuffer.Handle is not 0 && _stagingBufferSize >= size)
        {
            return;
        }

        DestroyStagingBuffer();
        CreateBuffer(
            size,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out _stagingBuffer,
            out _stagingMemory
        );
        _stagingBufferSize = size;

        void* mapped;
        Check(_vk.MapMemory(_device, _stagingMemory, 0, size, 0, &mapped));
        _stagingMappedPtr = mapped;
        _stagingMapped = true;
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

    private DescriptorSet AllocateTextureDescriptorSet(ImageView view)
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
            Sampler = _fontSampler,
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
        var bufferInfo = stackalloc BufferCreateInfo[1];
        bufferInfo[0] = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
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
            SharingMode = SharingMode.Exclusive,
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

        fixed (Fence* fence = &_uploadFence)
        {
            Check(_vk.WaitForFences(_device, 1, fence, true, ulong.MaxValue));
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
        Check(_vk.EndCommandBuffer(commandBuffer));

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };

        Check(_vk.QueueSubmit(_graphicsQueue, 1, &submitInfo, _uploadFence));
        fixed (Fence* fence = &_uploadFence)
        {
            Check(_vk.WaitForFences(_device, 1, fence, true, ulong.MaxValue));
        }
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
            Samples = MsaaSampleCount,
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
            QueueFamilyIndex = _graphicsQueueFamily,
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

            // Mailbox: capped at monitor refresh but no tearing
            for (var i = 0u; i < count; i++)
            {
                if (modes[i] == PresentModeKHR.MailboxKhr)
                {
                    return PresentModeKHR.MailboxKhr;
                }
            }
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
