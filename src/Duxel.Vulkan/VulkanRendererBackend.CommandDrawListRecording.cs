using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void RecordCommandDrawLists(
        UiDrawData drawData,
        uint imageIndex,
        Dictionary<int, StaticGeometryBuffer> staticBindings,
        in CommandFrameContext frameContext,
        ref CommandRecordingState state)
    {
        var viewport = new Viewport(0, 0, frameContext.FramebufferWidth, frameContext.FramebufferHeight, 0, 1);
        _vk.CmdSetViewport(frameContext.CommandBuffer, 0, 1, &viewport);

        uint globalVertexOffset = 0;
        uint globalIndexOffset = 0;
        uint globalPrimitiveOffset = 0;
        var dynamicPrimitiveInstanceBase = ShouldReserveDynamicPrimitiveSentinel()
            ? (uint)DynamicPrimitiveBufferSentinelCount
            : 0u;

        for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
        {
            var drawList = drawData.DrawLists[listIndex];
            var drawListVertexCount = drawList.Vertices.Count;
            var drawListIndexCount = drawList.Indices.Count;
            var drawListRectPrimitiveCount = drawList.RectFilledPrimitives?.Count ?? 0;
            var drawListCirclePrimitiveCount = drawList.CircleFilledPrimitives?.Count ?? 0;
            var hasStaticBinding = staticBindings.TryGetValue(listIndex, out var staticBinding);
            RecordDrawListProfile(frameContext.ProfileEnabled, hasStaticBinding, ref state.Profile);
            var listProfileCommandStart = frameContext.ProfileEnabled ? state.Profile.CommandCount : 0;
            var listProfileDrawStart = frameContext.ProfileEnabled ? state.DrawDispatch.DrawCallCount : 0;
            var listProfilePipelineStart = frameContext.ProfileEnabled ? state.Pipeline.BindCount : 0;
            var listProfileScissorComputeStart = frameContext.ProfileEnabled ? state.Scissor.ScissorComputeCount : 0;
            var listProfileScissorSetStart = frameContext.ProfileEnabled ? state.Scissor.ScissorSetCount : 0;
            var listProfilePushStart = frameContext.ProfileEnabled ? state.PushConstant.PushCount : 0;
            var listProfileGeometryBindStart = frameContext.ProfileEnabled ? state.BufferBinding.GeometryBindCount : 0;
            var listProfilePrimitiveBindStart = frameContext.ProfileEnabled ? state.BufferBinding.PrimitiveBindCount : 0;

            var schedulerStart = BeginCommandSchedulerProfile(frameContext.ProfileEnabled);
            var scheduleResult = TryGetScheduledCommandOrder(drawList, hasStaticBinding, in staticBinding, out var scheduledOrder);
            RecordCommandSchedulerProfile(
                scheduleResult,
                schedulerStart,
                frameContext.ProfileEnabled,
                ref state.Profile);

            var commandIterationState = CreateCommandIterationState(drawList, scheduledOrder);
            var drawListContext = new CommandDrawListContext(
                globalIndexOffset,
                globalVertexOffset,
                globalPrimitiveOffset,
                dynamicPrimitiveInstanceBase,
                drawListRectPrimitiveCount);
            for (var commandOrderIndex = 0;
                TryGetNextCommandIterationStep(
                    drawList,
                    in commandIterationState,
                    commandOrderIndex,
                    out var commandStep);
                commandOrderIndex = commandStep.NextCommandOrderIndex + 1)
            {
                RecordCommandDrawListCommand(
                    drawList,
                    in commandStep,
                    imageIndex,
                    listIndex,
                    hasStaticBinding,
                    in staticBinding,
                    in frameContext,
                    in drawListContext,
                    ref state);
            }

            RecordDrawListWorkProfile(
                frameContext.ProfileEnabled,
                hasStaticBinding,
                state.Profile.CommandCount - listProfileCommandStart,
                state.DrawDispatch.DrawCallCount - listProfileDrawStart,
                state.Pipeline.BindCount - listProfilePipelineStart,
                state.Scissor.ScissorComputeCount - listProfileScissorComputeStart,
                state.Scissor.ScissorSetCount - listProfileScissorSetStart,
                state.PushConstant.PushCount - listProfilePushStart,
                state.BufferBinding.GeometryBindCount - listProfileGeometryBindStart,
                state.BufferBinding.PrimitiveBindCount - listProfilePrimitiveBindStart,
                ref state.Profile);

            RecordStaticSecondaryCandidateProfile(
                frameContext.ProfileEnabled,
                hasStaticBinding,
                state.Profile.CommandCount - listProfileCommandStart,
                state.DrawDispatch.DrawCallCount - listProfileDrawStart,
                _devicePolicy.StaticSecondaryMinDrawCount,
                ref state.Profile);

            if (!hasStaticBinding)
            {
                globalVertexOffset += (uint)drawListVertexCount;
                globalIndexOffset += (uint)drawListIndexCount;

                globalPrimitiveOffset += (uint)(drawListRectPrimitiveCount + drawListCirclePrimitiveCount);
            }
        }
    }

    private void RecordCommandDrawListCommand(
        UiDrawList drawList,
        in CommandIterationStep commandStep,
        uint imageIndex,
        int listIndex,
        bool hasStaticBinding,
        in StaticGeometryBuffer staticBinding,
        in CommandFrameContext frameContext,
        in CommandDrawListContext drawListContext,
        ref CommandRecordingState state)
    {
        var commandIndex = commandStep.CommandIndex;
        var command = commandStep.Command;
        RecordMergedScheduledCommandProfile(
            frameContext.ProfileEnabled,
            commandStep.MergedCommandCount,
            ref state.Profile);

        var classification = ClassifyCommand(in command, hasStaticBinding, in staticBinding);
        EmitCommandDiagnosticIfNeeded(
            in command,
            in classification,
            imageIndex,
            listIndex,
            commandIndex,
            hasStaticBinding,
            ref state.Diagnostic);
        RecordCommandProfile(in command, in classification, frameContext.ProfileEnabled, ref state.Profile);

        var texture = default(TextureResource);
        if (classification.CommandNeedsTexture
            && !TryResolveCommandTexture(
                in command,
                in classification,
                imageIndex,
                listIndex,
                commandIndex,
                frameContext.ProfileEnabled,
                ref state.FontDiagnostic,
                ref state.Texture,
                out texture))
        {
            return;
        }

        AnalyzeAndValidateFontCommandIfNeeded(
            drawList,
            in command,
            in classification,
            imageIndex,
            listIndex,
            commandIndex,
            ref state.FontDiagnostic);

        if (!TryApplyScissorForCommand(
            in command,
            in frameContext,
            ref state.Scissor))
        {
            return;
        }

        RecordCommandDrawPath(
            in command,
            in classification,
            in texture,
            hasStaticBinding,
            in staticBinding,
            in frameContext,
            in drawListContext,
            ref state.Pipeline,
            ref state.Descriptor,
            ref state.BufferBinding,
            ref state.PushConstant,
            ref state.DrawDispatch);
    }
}
