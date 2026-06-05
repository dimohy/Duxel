using System.Runtime.CompilerServices;
using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private readonly struct CommandPipelineSelection
    {
        public readonly Pipeline DesiredPipeline;
        public readonly bool UsesSolidUnifiedPipeline;
        public readonly VkBuffer TargetVertexBuffer;
        public readonly VkBuffer TargetIndexBuffer;
        public readonly VkBuffer TargetPrimitiveBuffer;

        public CommandPipelineSelection(
            Pipeline desiredPipeline,
            bool usesSolidUnifiedPipeline,
            VkBuffer targetVertexBuffer,
            VkBuffer targetIndexBuffer,
            VkBuffer targetPrimitiveBuffer)
        {
            DesiredPipeline = desiredPipeline;
            UsesSolidUnifiedPipeline = usesSolidUnifiedPipeline;
            TargetVertexBuffer = targetVertexBuffer;
            TargetIndexBuffer = targetIndexBuffer;
            TargetPrimitiveBuffer = targetPrimitiveBuffer;
        }
    }

    private CommandPipelineSelection SelectCommandPipeline(
        in UiDrawCommand command,
        in CommandClassification classification,
        bool hasStaticBinding,
        in StaticGeometryBuffer staticBinding,
        VkBuffer dynamicVertexBuffer,
        VkBuffer dynamicIndexBuffer,
        VkBuffer dynamicPrimitiveBuffer)
    {
        var targetVertexBuffer = hasStaticBinding ? staticBinding.VertexBuffer : dynamicVertexBuffer;
        var targetIndexBuffer = hasStaticBinding ? staticBinding.IndexBuffer : dynamicIndexBuffer;
        var targetPrimitiveBuffer = hasStaticBinding ? staticBinding.PrimitiveBuffer : dynamicPrimitiveBuffer;
        var solidUnifiedAvailable = IsSolidUnifiedPipelineAvailableForCommand(
            hasStaticBinding,
            targetVertexBuffer,
            targetPrimitiveBuffer);
        var triangleUsesUnifiedSolidPipeline = classification.TriangleUsesColorPipeline && solidUnifiedAvailable;
        var primitiveUsesUnifiedSolidPipeline = classification.IsPrimitiveCommand
            && !classification.PrimitiveUsesTexture
            && solidUnifiedAvailable;
        var desiredPipeline = command.Kind switch
        {
            UiDrawCommandKind.Triangles => classification.IsFontCommand
                ? _subpixelPipeline
                : (triangleUsesUnifiedSolidPipeline
                    ? _solidColorPipeline
                    : (classification.TriangleUsesColorPipeline ? _graphicsColorPipeline : _graphicsPipeline)),
            UiDrawCommandKind.RectFilledPrimitives when classification.StaticPrimitiveUsesTriangleGeometry => classification.PrimitiveUsesTexture
                ? _graphicsPipeline
                : (primitiveUsesUnifiedSolidPipeline ? _solidColorPipeline : _graphicsColorPipeline),
            UiDrawCommandKind.RectFilledPrimitives => classification.PrimitiveUsesTexture
                ? _primitivePipeline
                : (primitiveUsesUnifiedSolidPipeline ? _solidColorPipeline : _primitiveColorPipeline),
            UiDrawCommandKind.CircleFilledPrimitives when classification.StaticPrimitiveUsesTriangleGeometry => classification.PrimitiveUsesTexture
                ? _graphicsPipeline
                : (primitiveUsesUnifiedSolidPipeline ? _solidColorPipeline : _graphicsColorPipeline),
            UiDrawCommandKind.CircleFilledPrimitives => classification.PrimitiveUsesTexture
                ? _primitivePipeline
                : (primitiveUsesUnifiedSolidPipeline ? _solidColorPipeline : _primitiveColorPipeline),
            _ => throw new InvalidOperationException($"Unsupported draw command kind: {command.Kind}.")
        };
        var usesSolidUnifiedPipeline = desiredPipeline.Handle == _solidColorPipeline.Handle
            && _solidColorPipeline.Handle is not 0;

        return new CommandPipelineSelection(
            desiredPipeline,
            usesSolidUnifiedPipeline,
            targetVertexBuffer,
            targetIndexBuffer,
            targetPrimitiveBuffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsSolidUnifiedPipelineAvailableForCommand(
        bool hasStaticBinding,
        VkBuffer targetVertexBuffer,
        VkBuffer targetPrimitiveBuffer)
    {
        return _solidUnifiedPipelineEnabled
            && (!hasStaticBinding || _solidUnifiedStaticEnabled)
            && _solidColorPipeline.Handle is not 0
            && targetVertexBuffer.Handle is not 0
            && targetPrimitiveBuffer.Handle is not 0;
    }
}
