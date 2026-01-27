#:property TargetFramework=net10.0
#:project ../../src/Dux.Platform.Glfw/Dux.Platform.Glfw.csproj
#:project ../../src/Dux.Vulkan/Dux.Vulkan.csproj

using System.Threading;
using Dux.Platform.Glfw;
using Dux.Vulkan;

var platformOptions = new GlfwPlatformBackendOptions(
    1280,
    720,
    "Dux Vulkan Init",
    true
);

using var platform = new GlfwPlatformBackend(platformOptions);

var rendererOptions = new VulkanRendererOptions(
    MinImageCount: 2,
    EnableValidationLayers: true
);

using var renderer = new VulkanRendererBackend(platform, rendererOptions);

while (!platform.ShouldClose)
{
    platform.PollEvents();
    Thread.Sleep(1);
}
