using Duxel.Core;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void RecordCommandBuffer(
        CommandBuffer commandBuffer,
        uint imageIndex,
        UiDrawData drawData,
        in FrameGeometryBuffers geometryBuffers,
        StaticGeometryFrameBindings staticBindings,
        QueryPool timestampQueryPool,
        out VectorProfileCounts vectorCounts,
        out long recordTextureLookupTicks,
        out long recordClippingTicks,
        out long recordDescriptorBindTicks,
        out long recordDrawCallTicks,
        out CommandRecordStats recordStats)
    {
        var usesDepth = _frameUsesDepthLayers;
        _hasNormalizedPlaneCamera = false;
        PrepareCompiledLayerGroups(drawData, staticBindings);
        BeginCommandBufferRecording(commandBuffer);
        RecordPendingTextureShaderReadTransitions(commandBuffer);
        var commandFrameContext = CreateCommandFrameContext(
            commandBuffer,
            drawData,
            in geometryBuffers);
        var writeGpuTimestamps = timestampQueryPool.Handle is not 0;
        var commandState = CreateCommandRecordingState();
        if (writeGpuTimestamps)
        {
            _vk.CmdResetQueryPool(commandBuffer, timestampQueryPool, GpuProfileStartQuery, GpuProfileTimestampQueryCount);
            _vk.CmdWriteTimestamp(commandBuffer, PipelineStageFlags.TopOfPipeBit, timestampQueryPool, GpuProfileStartQuery);
        }
        RecordGpuParticleSteps(commandBuffer);
        vectorCounts = RecordVectorScenes(commandBuffer, timestampQueryPool);
        RecordPaintGroups(commandBuffer, imageIndex);

        AcquireFrameColorAttachments(commandBuffer, imageIndex);
        var contentArea = _damageArea;
        var contentEmpty = _damageEmpty;
        var contentPartial = _damagePartial;
        if (!contentEmpty || !_repaintDiagnosticRedraw)
            RecordRepairArea();
        foreach (var strip in _repaintBorderRepairs)
        {
            SynchronizeRepairAttachments(commandBuffer);
            _damageArea = RepaintRectToRenderArea(strip);
            _damageEmpty = false;
            _damagePartial = true;
            RecordRepairArea();
        }
        _damageArea = contentArea;
        _damageEmpty = contentEmpty;
        _damagePartial = contentPartial;
        EndCommandRenderPassFrame(commandBuffer, imageIndex, timestampQueryPool, writeGpuTimestamps);

        CompleteCommandRecordingState(
            in commandState,
            out recordTextureLookupTicks,
            out recordClippingTicks,
            out recordDescriptorBindTicks,
            out recordDrawCallTicks,
            out recordStats);
        EndCommandBufferRecording(commandBuffer);

        void RecordRepairArea()
        {
            BeginRenderArea(commandBuffer, imageIndex, usesDepth);
            if (!_damageEmpty)
            {
                if (!commandState.Pipeline.HasViewport)
                {
                    BindGlobalBindlessTextureSet(commandBuffer);
                    _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _graphicsPipeline);
                }
                // The same widget clip can produce different scissors in adjacent repairs.
                commandState.Scissor.HasComputedScissor = false;
                commandState.Pipeline.HasDepthScope = false;
                RecordCommandDrawLists(drawData, imageIndex, staticBindings, in commandFrameContext, ref commandState);
                RecordRepaintHighlight(commandBuffer);
            }
            _khrDynamicRendering.CmdEndRendering(commandBuffer);
        }
    }

    private void BeginCommandBufferRecording(CommandBuffer commandBuffer)
    {
        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        Check(_vk.BeginCommandBuffer(commandBuffer, &beginInfo));
    }

    private void AcquireFrameColorAttachments(CommandBuffer commandBuffer, uint imageIndex)
    {
        // Dynamic rendering has no implicit render-pass layout transitions, so the
        // frame explicitly moves the target images into color-attachment layout.
        var usesMsaa = _msaaSampleCount != SampleCountFlags.Count1Bit;
        var barrierCount = usesMsaa ? 2 : 1;
        var barriers = stackalloc ImageMemoryBarrier[2];
        barriers[0] = CreateColorAttachmentAcquireBarrier(_swapchainImages[imageIndex]);
        if (_damageEnabled)
            barriers[0].DstAccessMask |= AccessFlags.ColorAttachmentReadBit;
        if (_damageEnabled && _damageImages[imageIndex].Initialized)
            barriers[0].OldLayout = ImageLayout.PresentSrcKhr;
        if (usesMsaa)
        {
            barriers[1] = CreateColorAttachmentAcquireBarrier(_msaaColorImage);
            barriers[1].SrcAccessMask = AccessFlags.ColorAttachmentWriteBit;
            if (_damageEnabled)
                barriers[1].DstAccessMask |= AccessFlags.ColorAttachmentReadBit;
            if (_damageEnabled && _damageMsaaInitialized)
            {
                barriers[1].OldLayout = ImageLayout.ColorAttachmentOptimal;
                barriers[1].SrcAccessMask = AccessFlags.ColorAttachmentWriteBit;
            }
        }

        _vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.ColorAttachmentOutputBit,
            0,
            0,
            null,
            0,
            null,
            (uint)barrierCount,
            barriers);
        _damageImages[imageIndex].Initialized = true;
        _damageMsaaInitialized |= usesMsaa;
    }

    private void SynchronizeRepairAttachments(CommandBuffer commandBuffer)
    {
        // Load, store and resolve may access memory outside their render area.
        var barrier = new MemoryBarrier
        {
            SType = StructureType.MemoryBarrier,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstAccessMask = AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit,
        };
        _vk.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.ColorAttachmentOutputBit, 0, 1, &barrier, 0, null, 0, null);
    }

    private void BeginRenderArea(CommandBuffer commandBuffer, uint imageIndex, bool usesDepth)
    {
        var usesMsaa = _msaaSampleCount != SampleCountFlags.Count1Bit;
        var clearValue = new ClearValue
        {
            Color = _clearColorValue,
        };

        var colorAttachment = new RenderingAttachmentInfo
        {
            SType = StructureType.RenderingAttachmentInfo,
            ImageView = usesMsaa ? _msaaColorImageView : _swapchainImageViews[imageIndex],
            ImageLayout = ImageLayout.ColorAttachmentOptimal,
            ResolveMode = usesMsaa ? ResolveModeFlags.AverageBit : ResolveModeFlags.None,
            ResolveImageView = usesMsaa ? _swapchainImageViews[imageIndex] : default,
            ResolveImageLayout = usesMsaa ? ImageLayout.ColorAttachmentOptimal : ImageLayout.Undefined,
            LoadOp = _damageEmpty ? AttachmentLoadOp.Load : AttachmentLoadOp.Clear,
            StoreOp = usesMsaa && !_damageEnabled ? AttachmentStoreOp.DontCare : AttachmentStoreOp.Store,
            ClearValue = clearValue,
        };

        var depthAttachment = usesDepth ? PrepareDepthLayerAttachment(commandBuffer) : default;
        var renderingInfo = new RenderingInfo
        {
            SType = StructureType.RenderingInfo,
            RenderArea = _damageArea,
            LayerCount = 1,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachment,
            PDepthAttachment = usesDepth ? &depthAttachment : null,
        };

        _khrDynamicRendering.CmdBeginRendering(commandBuffer, &renderingInfo);
    }

    private void EndCommandRenderPassFrame(
        CommandBuffer commandBuffer,
        uint imageIndex,
        QueryPool timestampQueryPool,
        bool writeGpuTimestamps)
    {
        RecordValidationReadback(commandBuffer, imageIndex);

        // Explicit transition to present layout replaces the old render-pass
        // finalLayout handling.
        var presentBarrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstAccessMask = 0,
            OldLayout = ImageLayout.ColorAttachmentOptimal,
            NewLayout = ImageLayout.PresentSrcKhr,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = _swapchainImages[imageIndex],
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        _vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.BottomOfPipeBit,
            0,
            0,
            null,
            0,
            null,
            1,
            &presentBarrier);

        if (writeGpuTimestamps)
        {
            _vk.CmdWriteTimestamp(commandBuffer, PipelineStageFlags.BottomOfPipeBit, timestampQueryPool, GpuProfileEndQuery);
        }
    }

    // The call is erased from production builds; the implementation is compiled
    // only by the rendering validation project.
    partial void RecordValidationReadback(CommandBuffer commandBuffer, uint imageIndex);
    partial void ObserveValidationScissor(in UiDrawCommand command, int x, int y, uint width, uint height);

    private static ImageMemoryBarrier CreateColorAttachmentAcquireBarrier(Image image)
    {
        return new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = 0,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
            OldLayout = ImageLayout.Undefined,
            NewLayout = ImageLayout.ColorAttachmentOptimal,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };
    }

    private void EndCommandBufferRecording(CommandBuffer commandBuffer)
    {
        Check(_vk.EndCommandBuffer(commandBuffer));
    }

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
        public int ProjectedGlyphBatchPage;
        public nuint ProjectedGlyphBatchOffset;
        public int ProjectedGlyphPlaneRuns, ProjectedGlyphPlaneFootprints;
        public uint ProjectedGlyphPlaneInstances;
        public UiRect ProjectedGlyphPlaneViewport, ProjectedGlyphPlaneClip, ProjectedGlyphPlaneFractionalViewport;
        public int ProjectedGlyphPlaneDepthScope;
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
            state.Pipeline.ActualTexturedPrimitiveBindCount,
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

    private readonly struct CommandFrameContext(
        CommandBuffer commandBuffer,
        ulong dynamicVertexAddress,
        VkBuffer dynamicIndexBuffer,
        ulong dynamicPrimitiveAddress,
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
        UiVector2 displaySize,
        int framebufferWidth,
        int framebufferHeight,
        bool useTemporalJitter,
        bool profileEnabled)
    {
        public CommandBuffer CommandBuffer { get; } = commandBuffer;
        public ulong DynamicVertexAddress { get; } = dynamicVertexAddress;
        public VkBuffer DynamicIndexBuffer { get; } = dynamicIndexBuffer;
        public ulong DynamicPrimitiveAddress { get; } = dynamicPrimitiveAddress;
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
        public UiVector2 DisplaySize { get; } = displaySize;
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
        in FrameGeometryBuffers geometryBuffers)
    {
        var requestedFramebuffer = GetDrawFramebufferSize(drawData);
        var requestedFramebufferWidth = requestedFramebuffer.Width;
        var requestedFramebufferHeight = requestedFramebuffer.Height;
        var framebufferWidth = _oneToOnePresentation ? requestedFramebufferWidth : (_swapchainExtent.Width > 0 ? (int)_swapchainExtent.Width : requestedFramebufferWidth);
        var framebufferHeight = _oneToOnePresentation ? requestedFramebufferHeight : (_swapchainExtent.Height > 0 ? (int)_swapchainExtent.Height : requestedFramebufferHeight);

        var displaySize = drawData.DisplaySize;
        var displayPos = drawData.DisplayPos;
        var scaleX = 2f / displaySize.X;
        var scaleY = 2f / displaySize.Y;
        var translateX = -1f - displayPos.X * scaleX;
        var translateY = -1f - displayPos.Y * scaleY;

        var clipOffset = drawData.DisplayPos;
        var clipScale = drawData.FramebufferScale;
        var clipScaleX = clipScale.X * framebufferWidth / requestedFramebufferWidth;
        var clipScaleY = clipScale.Y * framebufferHeight / requestedFramebufferHeight;
        const bool useTemporalJitter = false;
        return new CommandFrameContext(
            commandBuffer,
            geometryBuffers.VertexAddress,
            geometryBuffers.IndexBuffer,
            geometryBuffers.PrimitiveAddress,
            scaleX,
            scaleY,
            translateX,
            translateY,
            translateX,
            translateY,
            clipOffset.X,
            clipOffset.Y,
            clipScaleX,
            clipScaleY,
            displaySize,
            framebufferWidth,
            framebufferHeight,
            useTemporalJitter,
            _profilingEnabled);
    }

    private readonly struct CommandDrawListContext(
        uint globalIndexOffset,
        uint globalVertexOffset,
        uint globalPrimitiveOffset,
        int drawListRectPrimitiveCount,
        bool skipLayerFrustumChecks)
    {
        public uint GlobalIndexOffset { get; } = globalIndexOffset;
        public uint GlobalVertexOffset { get; } = globalVertexOffset;
        public uint GlobalPrimitiveOffset { get; } = globalPrimitiveOffset;
        public int DrawListRectPrimitiveCount { get; } = drawListRectPrimitiveCount;
        public bool SkipLayerFrustumChecks { get; } = skipLayerFrustumChecks;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void RecordCommandDrawLists(
        UiDrawData drawData,
        uint imageIndex,
        StaticGeometryFrameBindings staticBindings,
        in CommandFrameContext frameContext,
        ref CommandRecordingState state,
        bool allowCompiled = true)
    {
        var viewport = new Viewport(0, 0, frameContext.FramebufferWidth, frameContext.FramebufferHeight, 0, 1);
        _vk.CmdSetViewport(frameContext.CommandBuffer, 0, 1, &viewport);
        state.Pipeline.HasViewport = true;
        state.Pipeline.ViewportPixels = new(0, 0, frameContext.FramebufferWidth, frameContext.FramebufferHeight);

        uint globalVertexOffset = 0;
        uint globalIndexOffset = 0;
        uint globalPrimitiveOffset = 0;

        for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
        {
            if (allowCompiled && _compiledFrameGroups.TryGetValue(listIndex, out var compiledSpan))
            {
                FlushProjectedGlyphPlanes(in frameContext, ref state);
                foreach (var placement in compiledSpan.Groups)
                    RecordCompiledLayerGroup(placement.Group, drawData, placement.FirstList, staticBindings, imageIndex, in frameContext, ref state);
                listIndex += compiledSpan.ListCount - 1;
                continue;
            }
            var drawList = drawData.DrawLists[listIndex];
            var drawListVertexCount = drawList.Vertices.Count;
            var drawListIndexCount = drawList.Indices.Count;
            var drawListRectPrimitiveCount = drawList.RectFilledPrimitives?.Count ?? 0;
            var drawListCirclePrimitiveCount = drawList.CircleFilledPrimitives?.Count ?? 0;
            var hasStaticBinding = staticBindings.TryGetValue(listIndex, out var staticBinding);
            staticBinding ??= StaticGeometryBuffer.Empty;
            LayerViewportTransform layerView;
            var visibility = LayerVisibility.Intersecting;
            if (drawList.Scene3D is { } listScene && !IsScreenAlignedScene(listScene))
            {
                ref var cachedVisibility = ref GetCachedLayerVisibility(listIndex, drawList, in frameContext);
                layerView = cachedVisibility.View;
                visibility = cachedVisibility.Visibility;
            }
            else
                layerView = GetLayerViewportTransform(null, in frameContext);
            if (visibility == LayerVisibility.Outside)
            {
                if (!hasStaticBinding)
                {
                    globalVertexOffset += (uint)drawListVertexCount;
                    globalIndexOffset += (uint)drawListIndexCount;
                    globalPrimitiveOffset += (uint)(drawListRectPrimitiveCount + drawListCirclePrimitiveCount);
                }
                continue;
            }
            var deferGlyphs = allowCompiled && !_damagePartial && _projectedGlyphPlaneBatchingEnabled
                && TryPrepareProjectedGlyphPlane(drawList, in layerView, in frameContext, ref state);
            if (!deferGlyphs)
                FlushProjectedGlyphPlanes(in frameContext, ref state);
            ApplyDepthLayerState(drawList.Scene3D, in frameContext, in layerView, ref state.Pipeline);
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
            var scheduleResult = TryGetScheduledCommandOrder(drawList, hasStaticBinding, staticBinding, out var scheduledOrder);
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
                drawListRectPrimitiveCount,
                visibility == LayerVisibility.Inside);
            for (var commandOrderIndex = 0;
                TryGetNextCommandIterationStep(
                    drawList,
                    in commandIterationState,
                    commandOrderIndex,
                    out var commandStep);)
            {
                commandOrderIndex = commandStep.NextCommandOrderIndex + 1;
                if (deferGlyphs && commandStep.Command.ProjectedGlyphs is { } glyph)
                {
                    var command = commandStep.Command;
                    if (command.ClipRect.Width > 0 && command.ClipRect.Height > 0
                        && (drawListContext.SkipLayerFrustumChecks || !IsOutsideLayerFrustum(in command, in frameContext, in state.Pipeline)))
                        QueueProjectedGlyphPlane(in command, glyph, in frameContext, ref state);
                    continue;
                }
                if (_projectedGlyphBatchingEnabled && !commandIterationState.HasScheduledOrder
                    && commandStep.Command.ProjectedGlyphs is not null)
                {
                    var end = RecordConsecutiveProjectedGlyphs(drawList, commandStep.CommandIndex, in frameContext,
                        in drawListContext, ref state);
                    if (end > commandOrderIndex)
                    {
                        commandOrderIndex = end;
                        continue;
                    }
                }
                RecordCommandDrawListCommand(
                    drawList,
                    in commandStep,
                    imageIndex,
                    listIndex,
                    hasStaticBinding,
                    staticBinding,
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
        FlushProjectedGlyphPlanes(in frameContext, ref state);
    }

    private void RecordCommandDrawListCommand(
        UiDrawList drawList,
        in CommandIterationStep commandStep,
        uint imageIndex,
        int listIndex,
        bool hasStaticBinding,
        StaticGeometryBuffer staticBinding,
        in CommandFrameContext frameContext,
        in CommandDrawListContext drawListContext,
        ref CommandRecordingState state)
    {
        var commandIndex = commandStep.CommandIndex;
        var command = commandStep.Command;
        if (command.Callback is { } callback)
        {
            if (command.ProjectedGlyphs is { } glyphs)
            {
                if (!IsOutsideFrameDamage(in command, in frameContext, drawList.Scene3D)
                    && (drawListContext.SkipLayerFrustumChecks || !IsOutsideLayerFrustum(in command, in frameContext, in state.Pipeline)))
                    RecordProjectedGlyph(in command, glyphs, in frameContext, ref state);
                return;
            }
            var handled = false;
            RecordValidationProjectedGlyph(in command, in frameContext, ref state, ref handled);
            if (!handled)
                callback(drawList, command);
            return;
        }
        if (IsOutsideFrameDamage(in command, in frameContext, drawList.Scene3D))
            return;
        if (state.Pipeline.Layer3DEnabled && !drawListContext.SkipLayerFrustumChecks
            && IsOutsideLayerFrustum(in command, in frameContext, in state.Pipeline))
        {
            return;
        }
        RecordMergedScheduledCommandProfile(
            frameContext.ProfileEnabled,
            commandStep.MergedCommandCount,
            ref state.Profile);

        var classification = ClassifyCommand(in command, hasStaticBinding, staticBinding);
        EmitCommandDiagnosticIfNeeded(
            in command,
            in classification,
            imageIndex,
            listIndex,
            commandIndex,
            hasStaticBinding,
            ref state.Diagnostic);
        RecordCommandProfile(in command, frameContext.ProfileEnabled, ref state.Profile);

        var texture = default(TextureResource);
        if (!TryResolveCommandTexture(
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

        var scissorCommand = command;
        if (_projectedPaintCapture && !command.IsBackdropComposite && state.Pipeline.LayerLocalClip != command.ClipRect)
        {
            var clip = command.ClipRect;
            _vk.CmdPushConstants(frameContext.CommandBuffer, _pipelineLayout, ShaderStageFlags.FragmentBit, 112, 16, &clip);
            state.Pipeline.LayerLocalClip = clip;
        }
        if (state.Pipeline.Layer3DEnabled)
        {
            // Local clipping follows the transformed plane in the fragment shader.
            // A screen-space scissor must not reject rotated or translated content.
            var clip = command.ClipRect;
            if (state.Pipeline.LayerLocalClip != clip)
            {
                _vk.CmdPushConstants(frameContext.CommandBuffer, _pipelineLayout, ShaderStageFlags.FragmentBit, 112, 16, &clip);
                state.Pipeline.LayerLocalClip = clip;
            }
            scissorCommand = command with
            {
                ClipRect = state.Pipeline.LayerScreenClip,
                Translation = default,
            };
        }
        if (!TryApplyScissorForCommand(
            in scissorCommand,
            in frameContext,
            ref state.Scissor))
        {
            return;
        }

        RecordCommandDrawPath(
            drawList,
            in command,
            in classification,
            in texture,
            hasStaticBinding,
            staticBinding,
            in frameContext,
            in drawListContext,
            ref state.Pipeline,
            ref state.Descriptor,
            ref state.BufferBinding,
            ref state.PushConstant,
            ref state.DrawDispatch);
    }

    partial void RecordValidationProjectedGlyph(in UiDrawCommand command, in CommandFrameContext context,
        ref CommandRecordingState state, ref bool handled);
}
