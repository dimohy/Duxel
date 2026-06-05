namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private void CompleteRecordedFrame(
        ActiveFrameContext frameContext,
        CommandBuffer commandBuffer,
        bool profileEnabled,
        ref FrameProfileState profile)
    {
        var semaphores = frameContext.Semaphores;
        var renderFinished = semaphores.RenderFinished;
        profile.SubmitTicks = SubmitFrame(
            commandBuffer,
            semaphores.ImageAvailable,
            renderFinished,
            frameContext.FrameData.InFlight);

        var presentResult = PresentFrame(frameContext.ImageIndex, renderFinished, out profile.PresentTicks);
        HandleFramePresentResult(presentResult);
        EmitFrameProfileIfEnabled(profileEnabled, in profile);

        _frameIndex++;
    }
}
