namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private struct CommandRecordingState
    {
        public CommandRecordProfileState Profile;
        public CommandDiagnosticState Diagnostic;
        public CommandFontDiagnosticState FontDiagnostic;
        public CommandPipelineState Pipeline;
        public CommandDescriptorState Descriptor;
        public CommandBufferBindingState BufferBinding;
        public CommandPushConstantState PushConstant;
        public CommandScissorState Scissor;
        public CommandDrawDispatchState DrawDispatch;
        public CommandTextureState Texture;
    }

    private CommandRecordingState CreateCommandRecordingState()
    {
        return new CommandRecordingState
        {
            Diagnostic = CreateCommandDiagnosticState(),
        };
    }

    private static void CompleteCommandRecordingState(
        in CommandRecordingState state,
        out long textureLookupTicks,
        out long clippingTicks,
        out long descriptorBindTicks,
        out long drawCallTicks,
        out CommandRecordStats recordStats)
    {
        descriptorBindTicks = state.Pipeline.BindTicks
            + state.Descriptor.BindTicks
            + state.BufferBinding.BindTicks
            + state.PushConstant.PushTicks;
        textureLookupTicks = state.Texture.LookupTicks;
        clippingTicks = state.Scissor.ClippingTicks;
        drawCallTicks = state.DrawDispatch.DrawCallTicks;
        recordStats = BuildCommandRecordStats(in state);
    }

    private static CommandRecordStats BuildCommandRecordStats(in CommandRecordingState state)
    {
        return new CommandRecordStats(
            state.Profile.StaticDrawListCount,
            state.Profile.DynamicDrawListCount,
            state.Profile.StaticSecondaryCandidateDrawListCount,
            state.Profile.StaticSecondaryCandidateCommandCount,
            state.Profile.StaticSecondaryCandidateDrawCallCount,
            state.Profile.StaticCommandCount,
            state.Profile.DynamicCommandCount,
            state.Profile.StaticDrawCallCount,
            state.Profile.DynamicDrawCallCount,
            state.Profile.StaticPipelineBindCount,
            state.Profile.DynamicPipelineBindCount,
            state.Profile.StaticScissorComputeCount,
            state.Profile.DynamicScissorComputeCount,
            state.Profile.StaticScissorSetCount,
            state.Profile.DynamicScissorSetCount,
            state.Profile.StaticPushConstantCount,
            state.Profile.DynamicPushConstantCount,
            state.Profile.StaticGeometryBindCount,
            state.Profile.DynamicGeometryBindCount,
            state.Profile.StaticPrimitiveBindCount,
            state.Profile.DynamicPrimitiveBindCount,
            state.Profile.CommandCount,
            state.DrawDispatch.DrawCallCount,
            state.Pipeline.BindCount,
            state.Descriptor.BindCount,
            state.Scissor.ScissorSetCount,
            state.Scissor.ScissorComputeCount,
            state.Scissor.ScissorComputeReuseCount,
            state.PushConstant.PushCount,
            state.BufferBinding.GeometryBindCount,
            state.BufferBinding.PrimitiveBindCount,
            state.Pipeline.TriangleBindCount,
            state.Pipeline.FontBindCount,
            state.Pipeline.RectPrimitiveBindCount,
            state.Pipeline.CirclePrimitiveBindCount,
            state.Pipeline.ActualFontBindCount,
            state.Pipeline.ActualTexturedTriangleBindCount,
            state.Pipeline.ActualColorTriangleBindCount,
            state.Pipeline.ActualTexturedPrimitiveBindCount,
            state.Pipeline.ActualColorPrimitiveBindCount,
            state.Pipeline.ActualSolidUnifiedBindCount,
            state.Profile.TriangleToPrimitiveTransitionCount,
            state.Profile.PrimitiveToTriangleTransitionCount,
            state.Profile.RectCircleTransitionCount,
            state.Profile.SchedulerProbeCount,
            state.Profile.SchedulerCacheHitCount,
            state.Profile.SchedulerCacheMissCount,
            state.Profile.SchedulerNoChangeCount,
            state.Profile.SchedulerScheduledListCount,
            state.Profile.SchedulerMergedCommandCount,
            state.Pipeline.BindTicks,
            state.Descriptor.BindTicks,
            state.BufferBinding.BindTicks,
            state.PushConstant.PushTicks,
            state.Scissor.ScissorSetTicks,
            state.Profile.SchedulerTicks);
    }
}
