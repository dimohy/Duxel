using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
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
        _triangleColorPipelineEnabled = ResolveTriangleColorPipelineEnabled(_triangleColorPipelineMode, _devicePolicy);
        _solidUnifiedPipelineEnabled = ResolveSolidUnifiedPipelineEnabled(_solidUnifiedPipelineMode, _devicePolicy);
        _staticPrimitiveTrianglesEnabled = ResolveStaticPrimitiveTrianglesEnabled(
            _staticPrimitiveTriangleMode,
            _devicePolicy,
            _triangleColorPipelineEnabled);
        _resolvedStaticGeometryUpdateMode = ResolveStaticGeometryUpdateMode(_staticGeometryUpdateMode, _devicePolicy);
        _staticGeometryInPlaceUpdateEnabled = _resolvedStaticGeometryUpdateMode == VulkanStaticGeometryUpdateMode.InPlace;
        _staticGeometryRotatingUpdateEnabled = _resolvedStaticGeometryUpdateMode == VulkanStaticGeometryUpdateMode.Rotating;
        _gpuProfilingEnabled = _gpuProfilingRequested && _devicePolicy.SupportsGraphicsQueueTimestamps;
    }
}
