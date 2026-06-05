using System.Diagnostics;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private struct CommandBufferBindingState
    {
        public bool HasGeometryBuffers;
        public bool HasBoundIndexBuffer;
        public VkBuffer BoundVertexBuffer;
        public VkBuffer BoundIndexBuffer;
        public bool HasPrimitiveBuffer;
        public VkBuffer BoundPrimitiveBuffer;
        public int GeometryBindCount;
        public int PrimitiveBindCount;
        public long BindTicks;
    }

    private void BindGeometryBuffersIfNeeded(
        CommandBuffer commandBuffer,
        VkBuffer vertexBuffer,
        VkBuffer indexBuffer,
        bool bindIndexBuffer,
        bool profileEnabled,
        ref CommandBufferBindingState state)
    {
        var indexBindingMatches = !bindIndexBuffer
            || (state.HasBoundIndexBuffer && state.BoundIndexBuffer.Handle == indexBuffer.Handle);
        if (state.HasGeometryBuffers
            && state.BoundVertexBuffer.Handle == vertexBuffer.Handle
            && state.BoundIndexBuffer.Handle == indexBuffer.Handle
            && indexBindingMatches)
        {
            return;
        }

        ulong vertexOffset = 0;
        var bufferBindStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &vertexBuffer, &vertexOffset);
        if (bindIndexBuffer)
        {
            _vk.CmdBindIndexBuffer(commandBuffer, indexBuffer, 0, IndexType.Uint32);
        }

        if (profileEnabled)
        {
            state.BindTicks += Stopwatch.GetTimestamp() - bufferBindStart;
            state.GeometryBindCount++;
        }

        state.BoundVertexBuffer = vertexBuffer;
        state.BoundIndexBuffer = indexBuffer;
        state.HasBoundIndexBuffer = bindIndexBuffer;
        state.HasGeometryBuffers = true;
    }

    private void BindPrimitiveBufferIfNeeded(
        CommandBuffer commandBuffer,
        VkBuffer primitiveBuffer,
        bool profileEnabled,
        ref CommandBufferBindingState state)
    {
        if (state.HasPrimitiveBuffer && state.BoundPrimitiveBuffer.Handle == primitiveBuffer.Handle)
        {
            return;
        }

        ulong primitiveOffset = 0;
        var bufferBindStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdBindVertexBuffers(commandBuffer, 1, 1, &primitiveBuffer, &primitiveOffset);
        if (profileEnabled)
        {
            state.BindTicks += Stopwatch.GetTimestamp() - bufferBindStart;
            state.PrimitiveBindCount++;
        }

        state.BoundPrimitiveBuffer = primitiveBuffer;
        state.HasPrimitiveBuffer = true;
    }
}
