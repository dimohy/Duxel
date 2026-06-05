using System.Diagnostics;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private struct CommandDrawDispatchState
    {
        public int DrawCallCount;
        public long DrawCallTicks;
    }

    private void DrawTriangleCommand(
        CommandBuffer commandBuffer,
        in UiDrawCommand command,
        bool hasStaticBinding,
        uint globalIndexOffset,
        uint globalVertexOffset,
        bool profileEnabled,
        ref CommandDrawDispatchState state)
    {
        var firstIndex = hasStaticBinding
            ? command.IndexOffset
            : command.IndexOffset + globalIndexOffset;
        var vertexOffsetCommand = hasStaticBinding
            ? (int)command.VertexOffset
            : (int)(command.VertexOffset + globalVertexOffset);
        var drawCallStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdDrawIndexed(commandBuffer, command.ElementCount, 1, firstIndex, vertexOffsetCommand, 0);
        RecordDrawCallProfile(profileEnabled, drawCallStart, ref state);
    }

    private void DrawExpandedStaticPrimitiveCommand(
        CommandBuffer commandBuffer,
        in UiDrawCommand command,
        in StaticGeometryBuffer staticBinding,
        bool profileEnabled,
        ref CommandDrawDispatchState state)
    {
        uint firstIndex;
        uint indexCount;
        if (command.Kind is UiDrawCommandKind.RectFilledPrimitives)
        {
            firstIndex = checked((uint)(staticBinding.RectExpandedIndexBase + (int)command.IndexOffset * 6));
            indexCount = checked(command.ElementCount * 6u);
        }
        else
        {
            var circleOffsets = staticBinding.CircleExpandedIndexOffsets
                ?? throw new InvalidOperationException("Static circle primitive command has no expanded index table.");
            var start = checked((int)command.IndexOffset);
            var end = checked(start + (int)command.ElementCount);
            if ((uint)start >= (uint)circleOffsets.Length || (uint)end >= (uint)circleOffsets.Length)
            {
                throw new InvalidOperationException("Static circle primitive command is outside the expanded index table.");
            }

            firstIndex = checked((uint)circleOffsets[start]);
            indexCount = checked((uint)(circleOffsets[end] - circleOffsets[start]));
        }

        var drawCallStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdDrawIndexed(commandBuffer, indexCount, 1, firstIndex, 0, 0);
        RecordDrawCallProfile(profileEnabled, drawCallStart, ref state);
    }

    private void DrawPrimitiveInstanceCommand(
        CommandBuffer commandBuffer,
        in UiDrawCommand command,
        bool hasStaticBinding,
        in StaticGeometryBuffer staticBinding,
        uint globalPrimitiveOffset,
        int drawListRectPrimitiveCount,
        uint dynamicPrimitiveInstanceBase,
        bool profileEnabled,
        ref CommandDrawDispatchState state)
    {
        var firstInstance = command.Kind is UiDrawCommandKind.RectFilledPrimitives
            ? (hasStaticBinding
                ? command.IndexOffset + (uint)staticBinding.PrimitiveInstanceBaseCount
                : command.IndexOffset + globalPrimitiveOffset + dynamicPrimitiveInstanceBase)
            : (hasStaticBinding
                ? command.IndexOffset + (uint)(staticBinding.PrimitiveInstanceBaseCount + staticBinding.RectPrimitiveCount)
                : command.IndexOffset + globalPrimitiveOffset + (uint)drawListRectPrimitiveCount + dynamicPrimitiveInstanceBase);
        var vertexCount = command.Kind is UiDrawCommandKind.RectFilledPrimitives
            ? 6u
            : command.VertexOffset * 3u;
        var drawCallStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdDraw(commandBuffer, vertexCount, command.ElementCount, 0, firstInstance);
        RecordDrawCallProfile(profileEnabled, drawCallStart, ref state);
    }

    private static void RecordDrawCallProfile(
        bool profileEnabled,
        long drawCallStart,
        ref CommandDrawDispatchState state)
    {
        if (!profileEnabled)
        {
            return;
        }

        state.DrawCallTicks += Stopwatch.GetTimestamp() - drawCallStart;
        state.DrawCallCount++;
    }
}
