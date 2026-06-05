using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private CommandBuffer RecordFrameCommandsForSubmission(
        UiDrawData drawData,
        uint imageIndex,
        FrameResources frameData,
        in FrameGeometryBuffers geometryBuffers,
        bool profileEnabled,
        ref FrameProfileState profile)
    {
        ResetFrameCommandPool(frameData.CommandPool);

        var commandBuffer = frameData.CommandBuffer;
        var recordStart = BeginFrameProfileTiming(profileEnabled);
        RecordCommandBuffer(
            commandBuffer,
            imageIndex,
            drawData,
            geometryBuffers.VertexBuffer,
            geometryBuffers.IndexBuffer,
            geometryBuffers.PrimitiveBuffer,
            _frameStaticBindings,
            _gpuProfilingEnabled ? frameData.TimestampQueryPool : default,
            out profile.RecordTextureLookupTicks,
            out profile.RecordClippingTicks,
            out profile.RecordDescriptorBindTicks,
            out profile.RecordDrawCallTicks,
            out profile.RecordStats);
        MarkFrameGpuTimestampQueryIssuedIfNeeded(frameData);
        profile.RecordTicks = EndFrameProfileTiming(profileEnabled, recordStart);

        return commandBuffer;
    }

    private void ResetFrameCommandPool(CommandPool commandPool)
    {
        Check(_vk.ResetCommandPool(_device, commandPool, 0));
    }

    private void MarkFrameGpuTimestampQueryIssuedIfNeeded(FrameResources frameData)
    {
        if (_gpuProfilingEnabled && frameData.TimestampQueryPool.Handle is not 0)
        {
            frameData.TimestampQueryIssued = true;
        }
    }
}
