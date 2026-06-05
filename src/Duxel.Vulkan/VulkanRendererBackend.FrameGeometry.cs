using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private readonly record struct FrameGeometryBuffers(
        VkBuffer VertexBuffer,
        VkBuffer IndexBuffer,
        VkBuffer PrimitiveBuffer);

    private FrameGeometryBuffers PrepareFrameGeometryForRecording(
        UiDrawData drawData,
        int frameSlot,
        FrameResources frameData,
        bool profileEnabled,
        out long uploadTicks)
    {
        var uploadStart = BeginFrameProfileTiming(profileEnabled);
        var geometryCounts = PrepareStaticGeometryForFrame(drawData, _frameStaticBindings);

        EnsureDynamicGeometryCapacity(frameSlot, in geometryCounts);
        UploadDynamicGeometry(frameSlot, drawData, _frameStaticBindings);
        PruneStaticGeometryCachesIfNeeded(_frameIndex);

        uploadTicks = EndFrameProfileTiming(profileEnabled, uploadStart);

        var renderBuffers = frameData.RenderBuffers;
        return new FrameGeometryBuffers(
            renderBuffers.VertexBuffer,
            renderBuffers.IndexBuffer,
            renderBuffers.PrimitiveBuffer);
    }

    private void EnsureDynamicGeometryCapacity(int frameSlot, in FrameGeometryCounts geometryCounts)
    {
        if (geometryCounts.DynamicVertexCount > 0 && geometryCounts.DynamicIndexCount > 0)
        {
            EnsureVertexBufferCapacity(frameSlot, geometryCounts.DynamicVertexCount);
            EnsureIndexBufferCapacity(frameSlot, geometryCounts.DynamicIndexCount);
        }

        var dynamicPrimitiveSentinelCount = ShouldReserveDynamicPrimitiveSentinel()
            ? DynamicPrimitiveBufferSentinelCount
            : 0;
        EnsurePrimitiveBufferCapacity(
            frameSlot,
            geometryCounts.RectPrimitiveCount + geometryCounts.CirclePrimitiveCount + dynamicPrimitiveSentinelCount);
    }
}
