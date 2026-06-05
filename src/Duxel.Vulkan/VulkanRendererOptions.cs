using Duxel.Core;

namespace Duxel.Vulkan;

public readonly record struct VulkanRendererOptions(
    int MinImageCount,
    bool EnableVSync = true,
    int MsaaSamples = 4,
    bool FontLinearSampling = false,
    UiTextureId FontTextureId = default,
    UiTextureId WhiteTextureId = default
);
