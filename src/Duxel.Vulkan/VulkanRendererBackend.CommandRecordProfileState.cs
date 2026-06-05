using System.Diagnostics;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private struct CommandRecordProfileState
    {
        public int StaticDrawListCount;
        public int DynamicDrawListCount;
        public int StaticSecondaryCandidateDrawListCount;
        public int StaticSecondaryCandidateCommandCount;
        public int StaticSecondaryCandidateDrawCallCount;
        public int StaticCommandCount;
        public int DynamicCommandCount;
        public int StaticDrawCallCount;
        public int DynamicDrawCallCount;
        public int StaticPipelineBindCount;
        public int DynamicPipelineBindCount;
        public int StaticScissorComputeCount;
        public int DynamicScissorComputeCount;
        public int StaticScissorSetCount;
        public int DynamicScissorSetCount;
        public int StaticPushConstantCount;
        public int DynamicPushConstantCount;
        public int StaticGeometryBindCount;
        public int DynamicGeometryBindCount;
        public int StaticPrimitiveBindCount;
        public int DynamicPrimitiveBindCount;
        public int CommandCount;
        public int TriangleToPrimitiveTransitionCount;
        public int PrimitiveToTriangleTransitionCount;
        public int RectCircleTransitionCount;
        public int SchedulerProbeCount;
        public int SchedulerCacheHitCount;
        public int SchedulerCacheMissCount;
        public int SchedulerNoChangeCount;
        public int SchedulerScheduledListCount;
        public int SchedulerMergedCommandCount;
        public long SchedulerTicks;
        public UiDrawCommandKind LastRecordedKind;
        public bool HasLastRecordedKind;
    }

    private static void RecordDrawListProfile(
        bool profileEnabled,
        bool hasStaticBinding,
        ref CommandRecordProfileState state)
    {
        if (!profileEnabled)
        {
            return;
        }

        if (hasStaticBinding)
        {
            state.StaticDrawListCount++;
        }
        else
        {
            state.DynamicDrawListCount++;
        }
    }

    private static void RecordDrawListWorkProfile(
        bool profileEnabled,
        bool hasStaticBinding,
        int commandCount,
        int drawCallCount,
        int pipelineBindCount,
        int scissorComputeCount,
        int scissorSetCount,
        int pushConstantCount,
        int geometryBindCount,
        int primitiveBindCount,
        ref CommandRecordProfileState state)
    {
        if (!profileEnabled)
        {
            return;
        }

        if (hasStaticBinding)
        {
            state.StaticCommandCount += commandCount;
            state.StaticDrawCallCount += drawCallCount;
            state.StaticPipelineBindCount += pipelineBindCount;
            state.StaticScissorComputeCount += scissorComputeCount;
            state.StaticScissorSetCount += scissorSetCount;
            state.StaticPushConstantCount += pushConstantCount;
            state.StaticGeometryBindCount += geometryBindCount;
            state.StaticPrimitiveBindCount += primitiveBindCount;
        }
        else
        {
            state.DynamicCommandCount += commandCount;
            state.DynamicDrawCallCount += drawCallCount;
            state.DynamicPipelineBindCount += pipelineBindCount;
            state.DynamicScissorComputeCount += scissorComputeCount;
            state.DynamicScissorSetCount += scissorSetCount;
            state.DynamicPushConstantCount += pushConstantCount;
            state.DynamicGeometryBindCount += geometryBindCount;
            state.DynamicPrimitiveBindCount += primitiveBindCount;
        }
    }

    private static void RecordStaticSecondaryCandidateProfile(
        bool profileEnabled,
        bool hasStaticBinding,
        int recordedCommandCount,
        int recordedDrawCallCount,
        int minCommandCount,
        ref CommandRecordProfileState state)
    {
        if (!profileEnabled
            || !hasStaticBinding
            || recordedCommandCount < minCommandCount)
        {
            return;
        }

        state.StaticSecondaryCandidateDrawListCount++;
        state.StaticSecondaryCandidateCommandCount += recordedCommandCount;
        state.StaticSecondaryCandidateDrawCallCount += recordedDrawCallCount;
    }

    private static long BeginCommandSchedulerProfile(bool profileEnabled)
    {
        return profileEnabled ? Stopwatch.GetTimestamp() : 0;
    }

    private static void RecordCommandSchedulerProfile(
        CommandScheduleResult scheduleResult,
        long schedulerStart,
        bool profileEnabled,
        ref CommandRecordProfileState state)
    {
        if (!profileEnabled)
        {
            return;
        }

        state.SchedulerTicks += Stopwatch.GetTimestamp() - schedulerStart;
        switch (scheduleResult)
        {
            case CommandScheduleResult.CacheHit:
                state.SchedulerProbeCount++;
                state.SchedulerCacheHitCount++;
                state.SchedulerScheduledListCount++;
                break;
            case CommandScheduleResult.CacheMiss:
                state.SchedulerProbeCount++;
                state.SchedulerCacheMissCount++;
                state.SchedulerScheduledListCount++;
                break;
            case CommandScheduleResult.NoChange:
                state.SchedulerProbeCount++;
                state.SchedulerNoChangeCount++;
                break;
        }
    }

    private static void RecordMergedScheduledCommandProfile(
        bool profileEnabled,
        int mergedCommandCount,
        ref CommandRecordProfileState state)
    {
        if (profileEnabled && mergedCommandCount > 0)
        {
            state.SchedulerMergedCommandCount += mergedCommandCount;
        }
    }

    private static void RecordCommandProfile(
        in UiDrawCommand command,
        in CommandClassification classification,
        bool profileEnabled,
        ref CommandRecordProfileState state)
    {
        if (!profileEnabled)
        {
            return;
        }

        state.CommandCount++;
        if (state.HasLastRecordedKind)
        {
            var lastWasTriangle = state.LastRecordedKind is UiDrawCommandKind.Triangles;
            if (lastWasTriangle && classification.IsPrimitiveCommand)
            {
                state.TriangleToPrimitiveTransitionCount++;
            }
            else if (!lastWasTriangle && classification.IsTriangleCommand)
            {
                state.PrimitiveToTriangleTransitionCount++;
            }
            else if (!lastWasTriangle
                && classification.IsPrimitiveCommand
                && state.LastRecordedKind != command.Kind)
            {
                state.RectCircleTransitionCount++;
            }
        }

        state.LastRecordedKind = command.Kind;
        state.HasLastRecordedKind = true;
    }

}
