namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private void BeginCommandBufferRecording(CommandBuffer commandBuffer)
    {
        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        Check(_vk.BeginCommandBuffer(commandBuffer, &beginInfo));
    }

    private void BeginCommandRenderPassFrame(
        CommandBuffer commandBuffer,
        uint imageIndex,
        int framebufferWidth,
        int framebufferHeight,
        QueryPool timestampQueryPool,
        bool writeGpuTimestamps)
    {
        if (writeGpuTimestamps)
        {
            _vk.CmdResetQueryPool(commandBuffer, timestampQueryPool, GpuProfileStartQuery, GpuProfileTimestampQueryCount);
            _vk.CmdWriteTimestamp(commandBuffer, PipelineStageFlags.TopOfPipeBit, timestampQueryPool, GpuProfileStartQuery);
        }

        var clearValue = new ClearValue
        {
            Color = _clearColorValue,
        };

        var renderPassInfo = new RenderPassBeginInfo
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _framebuffers[imageIndex],
            RenderArea = new Rect2D(
                new Offset2D(0, 0),
                new Extent2D((uint)framebufferWidth, (uint)framebufferHeight)),
            ClearValueCount = 1,
            PClearValues = &clearValue,
        };

        _vk.CmdBeginRenderPass(commandBuffer, &renderPassInfo, SubpassContents.Inline);
    }

    private void EndCommandRenderPassFrame(
        CommandBuffer commandBuffer,
        QueryPool timestampQueryPool,
        bool writeGpuTimestamps)
    {
        _vk.CmdEndRenderPass(commandBuffer);

        if (writeGpuTimestamps)
        {
            _vk.CmdWriteTimestamp(commandBuffer, PipelineStageFlags.BottomOfPipeBit, timestampQueryPool, GpuProfileEndQuery);
        }
    }

    private void EndCommandBufferRecording(CommandBuffer commandBuffer)
    {
        Check(_vk.EndCommandBuffer(commandBuffer));
    }
}
