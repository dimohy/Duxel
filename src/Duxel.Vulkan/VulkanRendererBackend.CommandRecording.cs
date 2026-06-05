using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void RecordCommandBuffer(
        CommandBuffer commandBuffer,
        uint imageIndex,
        UiDrawData drawData,
        VkBuffer vertexBuffer,
        VkBuffer indexBuffer,
        VkBuffer primitiveBuffer,
        Dictionary<int, StaticGeometryBuffer> staticBindings,
        QueryPool timestampQueryPool,
        out long recordTextureLookupTicks,
        out long recordClippingTicks,
        out long recordDescriptorBindTicks,
        out long recordDrawCallTicks,
        out CommandRecordStats recordStats)
    {
        BeginCommandBufferRecording(commandBuffer);
        RecordPendingTextureShaderReadTransitions(commandBuffer);
        var commandFrameContext = CreateCommandFrameContext(
            commandBuffer,
            drawData,
            vertexBuffer,
            indexBuffer,
            primitiveBuffer);
        var writeGpuTimestamps = timestampQueryPool.Handle is not 0;
        var commandState = CreateCommandRecordingState();

        BeginCommandRenderPassFrame(
            commandBuffer,
            imageIndex,
            commandFrameContext.FramebufferWidthPixels,
            commandFrameContext.FramebufferHeightPixels,
            timestampQueryPool,
            writeGpuTimestamps);
        RecordCommandDrawLists(drawData, imageIndex, staticBindings, in commandFrameContext, ref commandState);
        EndCommandRenderPassFrame(commandBuffer, timestampQueryPool, writeGpuTimestamps);

        CompleteCommandRecordingState(
            in commandState,
            out recordTextureLookupTicks,
            out recordClippingTicks,
            out recordDescriptorBindTicks,
            out recordDrawCallTicks,
            out recordStats);
        EndCommandBufferRecording(commandBuffer);
    }
}
