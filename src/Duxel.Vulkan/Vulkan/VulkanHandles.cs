using System.Runtime.InteropServices;

namespace Duxel.Vulkan;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Bool32(uint value)
{
    public uint Value { get; } = value;
    public static implicit operator bool(Bool32 value) => value.Value != 0;
    public static implicit operator Bool32(bool value) => new(value ? 1u : 0u);
}

public readonly struct Version32(uint major, uint minor, uint patch)
{
    public uint Value { get; } = (major << 22) | (minor << 12) | patch;
    public uint Major => Value >> 22;
    public uint Minor => (Value >> 12) & 0x3ff;
    public uint Patch => Value & 0xfff;
    public static implicit operator uint(Version32 version) => version.Value;
}

[StructLayout(LayoutKind.Sequential)] public struct Instance(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct PhysicalDevice(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct Device(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct Queue(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct SurfaceKHR(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct SwapchainKHR(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct CommandPool(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct CommandBuffer(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct Fence(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct Semaphore(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct Buffer(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct DeviceMemory(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct Image(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct ImageView(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct RenderPass(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct DescriptorSetLayout(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct PipelineLayout(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct Sampler(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct Pipeline(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct ShaderModule(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct PipelineCache(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct DescriptorPool(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct Framebuffer(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct DescriptorSet(ulong handle) { public ulong Handle = handle; }
[StructLayout(LayoutKind.Sequential)] public struct DebugUtilsMessengerEXT(ulong handle) { public ulong Handle = handle; }

public unsafe delegate uint DebugUtilsMessengerCallbackFunctionEXT(
    DebugUtilsMessageSeverityFlagsEXT severity,
    DebugUtilsMessageTypeFlagsEXT messageTypes,
    DebugUtilsMessengerCallbackDataEXT* callbackData,
    void* userData);
