using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    public void RenderDrawData(UiDrawData drawData)
    {
        var profileEnabled = _profilingEnabled;

        if (profileEnabled)
        {
            ResetImageTransitionProfileCounters();
            ResetUploadProfileCounters();
        }

        ApplyTextureUpdates(drawData.TextureUpdates.AsSpan());

        if (!TryEnsureFrameTarget(drawData) || !TryBeginRenderFrame(out var frameContext))
        {
            return;
        }

        var frame = frameContext.FrameSlot;
        var frameData = frameContext.FrameData;
        var frameProfile = CreateFrameProfileState(frameContext.GpuRenderUs);
        var geometryBuffers = PrepareFrameGeometryForRecording(
            drawData,
            frame,
            frameData,
            profileEnabled,
            out frameProfile.UploadTicks);

        var commandBuffer = RecordFrameCommandsForSubmission(
            drawData,
            frameContext.ImageIndex,
            frameData,
            in geometryBuffers,
            profileEnabled,
            ref frameProfile);

        CompleteRecordedFrame(frameContext, commandBuffer, profileEnabled, ref frameProfile);
    }
}
