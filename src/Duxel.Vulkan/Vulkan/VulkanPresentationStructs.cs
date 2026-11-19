using System.Runtime.InteropServices;

namespace Duxel.Vulkan;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct PhysicalDeviceSurfaceInfo2KHR
{public StructureType SType;public void* PNext;public SurfaceKHR Surface;}
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SurfaceCapabilities2KHR
{public StructureType SType;public void* PNext;public SurfaceCapabilitiesKHR SurfaceCapabilities;}
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SurfacePresentModeEXT
{public StructureType SType;public void* PNext;public PresentModeKHR PresentMode;}
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SurfacePresentScalingCapabilitiesEXT
{public StructureType SType;public void* PNext;public uint SupportedPresentScaling,SupportedPresentGravityX,SupportedPresentGravityY;public Extent2D MinScaledImageExtent,MaxScaledImageExtent;}
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SwapchainPresentScalingCreateInfoEXT
{public StructureType SType;public void* PNext;public uint ScalingBehavior,PresentGravityX,PresentGravityY;}

// Khronos vk.xml: extension276, offsets0 and1. EXT and KHR names are aliases.
[StructLayout(LayoutKind.Sequential)]
public unsafe struct PhysicalDeviceSwapchainMaintenance1FeaturesEXT
{
    public StructureType SType;
    public void* PNext;
    public uint SwapchainMaintenance1;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SwapchainPresentFenceInfoEXT
{
    public StructureType SType;
    public void* PNext;
    public uint SwapchainCount;
    public Fence* PFences;
}
