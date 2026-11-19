using System.Runtime.InteropServices;

namespace Duxel.Vulkan;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct ComputePipelineCreateInfo
{
    public StructureType SType;
    public void* PNext;
    public uint Flags;
    public PipelineShaderStageCreateInfo Stage;
    public PipelineLayout Layout;
    public Pipeline BasePipelineHandle;
    public int BasePipelineIndex;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct MemoryBarrier
{
    public StructureType SType;
    public void* PNext;
    public AccessFlags SrcAccessMask;
    public AccessFlags DstAccessMask;
}

public sealed unsafe partial class Vk
{
    public Result CreateComputePipelines(Device device, PipelineCache cache, uint count, ComputePipelineCreateInfo* infos, AllocationCallbacks* allocator, Pipeline* pipelines)
        => vkCreateComputePipelines(device, cache, count, infos, allocator, pipelines);
    public void CmdDispatch(CommandBuffer commandBuffer, uint x, uint y, uint z) => vkCmdDispatch(commandBuffer, x, y, z);

    [LibraryImport(LibraryName, EntryPoint = "vkCreateComputePipelines")]
    private static partial Result vkCreateComputePipelines(Device device, PipelineCache cache, uint count, ComputePipelineCreateInfo* infos, AllocationCallbacks* allocator, Pipeline* pipelines);
    [LibraryImport(LibraryName, EntryPoint = "vkCmdDispatch")]
    private static partial void vkCmdDispatch(CommandBuffer commandBuffer, uint x, uint y, uint z);
}
