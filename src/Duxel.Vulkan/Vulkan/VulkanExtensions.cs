using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Duxel.Vulkan;

internal unsafe sealed class KhrSurface(Vk vk, Instance instance)
{
    private readonly delegate* unmanaged<Instance, SurfaceKHR, AllocationCallbacks*, void> _destroySurface =
        (delegate* unmanaged<Instance, SurfaceKHR, AllocationCallbacks*, void>)vk.GetInstanceProcAddr(instance, "vkDestroySurfaceKHR");
    private readonly delegate* unmanaged<PhysicalDevice, SurfaceKHR, SurfaceCapabilitiesKHR*, Result> _getSurfaceCapabilities =
        (delegate* unmanaged<PhysicalDevice, SurfaceKHR, SurfaceCapabilitiesKHR*, Result>)vk.GetInstanceProcAddr(instance, "vkGetPhysicalDeviceSurfaceCapabilitiesKHR");
    private readonly delegate* unmanaged<PhysicalDevice, SurfaceKHR, uint*, SurfaceFormatKHR*, Result> _getSurfaceFormats =
        (delegate* unmanaged<PhysicalDevice, SurfaceKHR, uint*, SurfaceFormatKHR*, Result>)vk.GetInstanceProcAddr(instance, "vkGetPhysicalDeviceSurfaceFormatsKHR");
    private readonly delegate* unmanaged<PhysicalDevice, SurfaceKHR, uint*, PresentModeKHR*, Result> _getSurfacePresentModes =
        (delegate* unmanaged<PhysicalDevice, SurfaceKHR, uint*, PresentModeKHR*, Result>)vk.GetInstanceProcAddr(instance, "vkGetPhysicalDeviceSurfacePresentModesKHR");
    private readonly delegate* unmanaged<PhysicalDevice, uint, SurfaceKHR, Bool32*, Result> _getSurfaceSupport =
        (delegate* unmanaged<PhysicalDevice, uint, SurfaceKHR, Bool32*, Result>)vk.GetInstanceProcAddr(instance, "vkGetPhysicalDeviceSurfaceSupportKHR");

    public static bool TryCreate(Vk vk, Instance instance, out KhrSurface extension)
    {
        if (!vk.IsInstanceExtensionPresent("VK_KHR_surface"))
        {
            extension = null!;
            return false;
        }

        extension = new KhrSurface(vk, instance);
        return true;
    }

    public void DestroySurface(Instance targetInstance, SurfaceKHR surface, AllocationCallbacks* allocator) => _destroySurface(targetInstance, surface, allocator);
    public Result GetPhysicalDeviceSurfaceCapabilities(PhysicalDevice physicalDevice, SurfaceKHR surface, SurfaceCapabilitiesKHR* capabilities) => _getSurfaceCapabilities(physicalDevice, surface, capabilities);
    public Result GetPhysicalDeviceSurfaceFormats(PhysicalDevice physicalDevice, SurfaceKHR surface, uint* count, SurfaceFormatKHR* formats) => _getSurfaceFormats(physicalDevice, surface, count, formats);
    public Result GetPhysicalDeviceSurfacePresentModes(PhysicalDevice physicalDevice, SurfaceKHR surface, uint* count, PresentModeKHR* presentModes) => _getSurfacePresentModes(physicalDevice, surface, count, presentModes);
    public Result GetPhysicalDeviceSurfaceSupport(PhysicalDevice physicalDevice, uint queueFamilyIndex, SurfaceKHR surface, Bool32* supported) => _getSurfaceSupport(physicalDevice, queueFamilyIndex, surface, supported);
}

internal unsafe sealed class KhrSwapchain(Vk vk, Device device)
{
    private readonly delegate* unmanaged<Device, SwapchainCreateInfoKHR*, AllocationCallbacks*, SwapchainKHR*, Result> _createSwapchain =
        (delegate* unmanaged<Device, SwapchainCreateInfoKHR*, AllocationCallbacks*, SwapchainKHR*, Result>)vk.GetDeviceProcAddr(device, "vkCreateSwapchainKHR");
    private readonly delegate* unmanaged<Device, SwapchainKHR, AllocationCallbacks*, void> _destroySwapchain =
        (delegate* unmanaged<Device, SwapchainKHR, AllocationCallbacks*, void>)vk.GetDeviceProcAddr(device, "vkDestroySwapchainKHR");
    private readonly delegate* unmanaged<Device, SwapchainKHR, ulong, Semaphore, Fence, uint*, Result> _acquireNextImage =
        (delegate* unmanaged<Device, SwapchainKHR, ulong, Semaphore, Fence, uint*, Result>)vk.GetDeviceProcAddr(device, "vkAcquireNextImageKHR");
    private readonly delegate* unmanaged<Device, SwapchainKHR, uint*, Image*, Result> _getSwapchainImages =
        (delegate* unmanaged<Device, SwapchainKHR, uint*, Image*, Result>)vk.GetDeviceProcAddr(device, "vkGetSwapchainImagesKHR");
    private readonly delegate* unmanaged<Queue, PresentInfoKHR*, Result> _queuePresent =
        (delegate* unmanaged<Queue, PresentInfoKHR*, Result>)vk.GetDeviceProcAddr(device, "vkQueuePresentKHR");

    public static bool TryCreate(Vk vk, Instance instance, Device device, out KhrSwapchain extension)
    {
        if (!vk.IsDeviceExtensionPresent(instance, "VK_KHR_swapchain"))
        {
            extension = null!;
            return false;
        }

        extension = new KhrSwapchain(vk, device);
        return true;
    }

    public Result CreateSwapchain(Device targetDevice, SwapchainCreateInfoKHR* createInfo, AllocationCallbacks* allocator, SwapchainKHR* swapchain) => _createSwapchain(targetDevice, createInfo, allocator, swapchain);
    public void DestroySwapchain(Device targetDevice, SwapchainKHR swapchain, AllocationCallbacks* allocator) => _destroySwapchain(targetDevice, swapchain, allocator);
    public Result AcquireNextImage(Device targetDevice, SwapchainKHR swapchain, ulong timeout, Semaphore semaphore, Fence fence, uint* imageIndex) => _acquireNextImage(targetDevice, swapchain, timeout, semaphore, fence, imageIndex);
    public Result GetSwapchainImages(Device targetDevice, SwapchainKHR swapchain, uint* imageCount, Image* images) => _getSwapchainImages(targetDevice, swapchain, imageCount, images);
    public Result QueuePresent(Queue queue, PresentInfoKHR* presentInfo) => _queuePresent(queue, presentInfo);
}

internal unsafe sealed class ExtDebugUtils(Vk vk, Instance instance)
{
    private readonly delegate* unmanaged<Instance, DebugUtilsMessengerCreateInfoEXT*, AllocationCallbacks*, DebugUtilsMessengerEXT*, Result> _createDebugUtilsMessenger =
        (delegate* unmanaged<Instance, DebugUtilsMessengerCreateInfoEXT*, AllocationCallbacks*, DebugUtilsMessengerEXT*, Result>)vk.GetInstanceProcAddr(instance, "vkCreateDebugUtilsMessengerEXT");
    private readonly delegate* unmanaged<Instance, DebugUtilsMessengerEXT, AllocationCallbacks*, void> _destroyDebugUtilsMessenger =
        (delegate* unmanaged<Instance, DebugUtilsMessengerEXT, AllocationCallbacks*, void>)vk.GetInstanceProcAddr(instance, "vkDestroyDebugUtilsMessengerEXT");

    public static bool TryCreate(Vk vk, Instance instance, out ExtDebugUtils extension)
    {
        if (!vk.IsInstanceExtensionPresent("VK_EXT_debug_utils"))
        {
            extension = null!;
            return false;
        }

        extension = new ExtDebugUtils(vk, instance);
        return true;
    }

    public Result CreateDebugUtilsMessenger(Instance targetInstance, DebugUtilsMessengerCreateInfoEXT* createInfo, AllocationCallbacks* allocator, DebugUtilsMessengerEXT* messenger) => _createDebugUtilsMessenger(targetInstance, createInfo, allocator, messenger);
    public void DestroyDebugUtilsMessenger(Instance targetInstance, DebugUtilsMessengerEXT messenger, AllocationCallbacks* allocator) => _destroyDebugUtilsMessenger(targetInstance, messenger, allocator);
}
