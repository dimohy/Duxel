using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private readonly struct CommandFrameContext(
        CommandBuffer commandBuffer,
        VkBuffer dynamicVertexBuffer,
        VkBuffer dynamicIndexBuffer,
        VkBuffer dynamicPrimitiveBuffer,
        float scaleX,
        float scaleY,
        float translateX,
        float translateY,
        float jitterTranslateX,
        float jitterTranslateY,
        float clipOffsetX,
        float clipOffsetY,
        float clipScaleX,
        float clipScaleY,
        int framebufferWidth,
        int framebufferHeight,
        bool useTemporalJitter,
        bool profileEnabled)
    {
        public CommandBuffer CommandBuffer { get; } = commandBuffer;
        public VkBuffer DynamicVertexBuffer { get; } = dynamicVertexBuffer;
        public VkBuffer DynamicIndexBuffer { get; } = dynamicIndexBuffer;
        public VkBuffer DynamicPrimitiveBuffer { get; } = dynamicPrimitiveBuffer;
        public float ScaleX { get; } = scaleX;
        public float ScaleY { get; } = scaleY;
        public float TranslateX { get; } = translateX;
        public float TranslateY { get; } = translateY;
        public float JitterTranslateX { get; } = jitterTranslateX;
        public float JitterTranslateY { get; } = jitterTranslateY;
        public float ClipOffsetX { get; } = clipOffsetX;
        public float ClipOffsetY { get; } = clipOffsetY;
        public float ClipScaleX { get; } = clipScaleX;
        public float ClipScaleY { get; } = clipScaleY;
        public int FramebufferWidthPixels { get; } = framebufferWidth;
        public int FramebufferHeightPixels { get; } = framebufferHeight;
        public float FramebufferWidth { get; } = framebufferWidth;
        public float FramebufferHeight { get; } = framebufferHeight;
        public bool UseTemporalJitter { get; } = useTemporalJitter;
        public bool ProfileEnabled { get; } = profileEnabled;
    }

    private CommandFrameContext CreateCommandFrameContext(
        CommandBuffer commandBuffer,
        UiDrawData drawData,
        VkBuffer dynamicVertexBuffer,
        VkBuffer dynamicIndexBuffer,
        VkBuffer dynamicPrimitiveBuffer)
    {
        var framebufferWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        var framebufferHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);

        var displaySize = drawData.DisplaySize;
        var displayPos = drawData.DisplayPos;
        var scaleX = 2f / displaySize.X;
        var scaleY = 2f / displaySize.Y;
        var translateX = -1f - displayPos.X * scaleX;
        var translateY = -1f - displayPos.Y * scaleY;

        var clipOffset = drawData.DisplayPos;
        var clipScale = drawData.FramebufferScale;
        const bool useTemporalJitter = false;
        return new CommandFrameContext(
            commandBuffer,
            dynamicVertexBuffer,
            dynamicIndexBuffer,
            dynamicPrimitiveBuffer,
            scaleX,
            scaleY,
            translateX,
            translateY,
            translateX,
            translateY,
            clipOffset.X,
            clipOffset.Y,
            clipScale.X,
            clipScale.Y,
            framebufferWidth,
            framebufferHeight,
            useTemporalJitter,
            _profilingEnabled);
    }

    private readonly struct CommandDrawListContext(
        uint globalIndexOffset,
        uint globalVertexOffset,
        uint globalPrimitiveOffset,
        uint dynamicPrimitiveInstanceBase,
        int drawListRectPrimitiveCount)
    {
        public uint GlobalIndexOffset { get; } = globalIndexOffset;
        public uint GlobalVertexOffset { get; } = globalVertexOffset;
        public uint GlobalPrimitiveOffset { get; } = globalPrimitiveOffset;
        public uint DynamicPrimitiveInstanceBase { get; } = dynamicPrimitiveInstanceBase;
        public int DrawListRectPrimitiveCount { get; } = drawListRectPrimitiveCount;
    }
}
