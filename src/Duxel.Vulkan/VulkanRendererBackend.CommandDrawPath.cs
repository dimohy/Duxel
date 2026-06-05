using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private void RecordCommandDrawPath(
        in UiDrawCommand command,
        in CommandClassification classification,
        in TextureResource texture,
        bool hasStaticBinding,
        in StaticGeometryBuffer staticBinding,
        in CommandFrameContext frameContext,
        in CommandDrawListContext drawListContext,
        ref CommandPipelineState pipelineState,
        ref CommandDescriptorState descriptorState,
        ref CommandBufferBindingState bufferBindingState,
        ref CommandPushConstantState pushConstantState,
        ref CommandDrawDispatchState drawDispatchState)
    {
        var pipelineSelection = SelectCommandPipeline(
            in command,
            in classification,
            hasStaticBinding,
            in staticBinding,
            frameContext.DynamicVertexBuffer,
            frameContext.DynamicIndexBuffer,
            frameContext.DynamicPrimitiveBuffer);

        BindPipelineIfNeeded(
            frameContext.CommandBuffer,
            pipelineSelection.DesiredPipeline,
            command.Kind,
            classification.IsFontCommand,
            frameContext.ProfileEnabled,
            ref pipelineState);

        ApplyPushConstantsIfNeeded(
            in frameContext,
            command.Translation.X,
            command.Translation.Y,
            command.Opacity,
            ref pushConstantState);

        if (classification.CommandNeedsTexture)
        {
            BindDescriptorSetIfNeeded(
                frameContext.CommandBuffer,
                texture.DescriptorSet,
                frameContext.ProfileEnabled,
                ref descriptorState);
        }

        if (classification.IsTriangleCommand)
        {
            RecordTriangleDrawPath(
                in command,
                hasStaticBinding,
                in frameContext,
                in drawListContext,
                in pipelineSelection,
                ref bufferBindingState,
                ref drawDispatchState);
            return;
        }

        if (classification.StaticPrimitiveUsesTriangleGeometry)
        {
            RecordExpandedStaticPrimitiveDrawPath(
                in command,
                in staticBinding,
                in frameContext,
                in pipelineSelection,
                ref bufferBindingState,
                ref drawDispatchState);
            return;
        }

        RecordPrimitiveInstanceDrawPath(
            in command,
            hasStaticBinding,
            in staticBinding,
            in frameContext,
            in drawListContext,
            in pipelineSelection,
            ref bufferBindingState,
            ref drawDispatchState);
    }

    private void RecordTriangleDrawPath(
        in UiDrawCommand command,
        bool hasStaticBinding,
        in CommandFrameContext frameContext,
        in CommandDrawListContext drawListContext,
        in CommandPipelineSelection pipelineSelection,
        ref CommandBufferBindingState bufferBindingState,
        ref CommandDrawDispatchState drawDispatchState)
    {
        BindGeometryBuffersIfNeeded(
            frameContext.CommandBuffer,
            pipelineSelection.TargetVertexBuffer,
            pipelineSelection.TargetIndexBuffer,
            bindIndexBuffer: true,
            frameContext.ProfileEnabled,
            ref bufferBindingState);

        if (pipelineSelection.UsesSolidUnifiedPipeline)
        {
            BindPrimitiveBufferIfNeeded(
                frameContext.CommandBuffer,
                pipelineSelection.TargetPrimitiveBuffer,
                frameContext.ProfileEnabled,
                ref bufferBindingState);
        }

        DrawTriangleCommand(
            frameContext.CommandBuffer,
            in command,
            hasStaticBinding,
            drawListContext.GlobalIndexOffset,
            drawListContext.GlobalVertexOffset,
            frameContext.ProfileEnabled,
            ref drawDispatchState);
    }

    private void RecordExpandedStaticPrimitiveDrawPath(
        in UiDrawCommand command,
        in StaticGeometryBuffer staticBinding,
        in CommandFrameContext frameContext,
        in CommandPipelineSelection pipelineSelection,
        ref CommandBufferBindingState bufferBindingState,
        ref CommandDrawDispatchState drawDispatchState)
    {
        BindGeometryBuffersIfNeeded(
            frameContext.CommandBuffer,
            pipelineSelection.TargetVertexBuffer,
            pipelineSelection.TargetIndexBuffer,
            bindIndexBuffer: true,
            frameContext.ProfileEnabled,
            ref bufferBindingState);

        if (pipelineSelection.UsesSolidUnifiedPipeline)
        {
            BindPrimitiveBufferIfNeeded(
                frameContext.CommandBuffer,
                pipelineSelection.TargetPrimitiveBuffer,
                frameContext.ProfileEnabled,
                ref bufferBindingState);
        }

        DrawExpandedStaticPrimitiveCommand(
            frameContext.CommandBuffer,
            in command,
            in staticBinding,
            frameContext.ProfileEnabled,
            ref drawDispatchState);
    }

    private void RecordPrimitiveInstanceDrawPath(
        in UiDrawCommand command,
        bool hasStaticBinding,
        in StaticGeometryBuffer staticBinding,
        in CommandFrameContext frameContext,
        in CommandDrawListContext drawListContext,
        in CommandPipelineSelection pipelineSelection,
        ref CommandBufferBindingState bufferBindingState,
        ref CommandDrawDispatchState drawDispatchState)
    {
        if (pipelineSelection.TargetPrimitiveBuffer.Handle is 0)
        {
            throw new InvalidOperationException($"Primitive draw command has no bound instance buffer: {command.Kind}.");
        }

        if (pipelineSelection.UsesSolidUnifiedPipeline
            && pipelineSelection.TargetVertexBuffer.Handle is not 0)
        {
            BindGeometryBuffersIfNeeded(
                frameContext.CommandBuffer,
                pipelineSelection.TargetVertexBuffer,
                pipelineSelection.TargetIndexBuffer,
                bindIndexBuffer: pipelineSelection.TargetIndexBuffer.Handle is not 0,
                frameContext.ProfileEnabled,
                ref bufferBindingState);
        }

        BindPrimitiveBufferIfNeeded(
            frameContext.CommandBuffer,
            pipelineSelection.TargetPrimitiveBuffer,
            frameContext.ProfileEnabled,
            ref bufferBindingState);

        DrawPrimitiveInstanceCommand(
            frameContext.CommandBuffer,
            in command,
            hasStaticBinding,
            in staticBinding,
            drawListContext.GlobalPrimitiveOffset,
            drawListContext.DrawListRectPrimitiveCount,
            drawListContext.DynamicPrimitiveInstanceBase,
            frameContext.ProfileEnabled,
            ref drawDispatchState);
    }
}
