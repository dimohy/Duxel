using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryComputeScissorRect(
        in UiDrawCommand cmd,
        float clipOffsetX,
        float clipOffsetY,
        float clipScaleX,
        float clipScaleY,
        float fbWidthF,
        float fbHeightF,
        out int scissorX,
        out int scissorY,
        out uint scissorW,
        out uint scissorH)
    {
        const float epsilon = 0.0001f;

        var cmdTranslationX = cmd.Translation.X;
        var cmdTranslationY = cmd.Translation.Y;

        var clipMinX = (cmd.ClipRect.X + cmdTranslationX - clipOffsetX) * clipScaleX;
        var clipMinY = (cmd.ClipRect.Y + cmdTranslationY - clipOffsetY) * clipScaleY;
        var clipMaxX = (cmd.ClipRect.X + cmd.ClipRect.Width + cmdTranslationX - clipOffsetX) * clipScaleX;
        var clipMaxY = (cmd.ClipRect.Y + cmd.ClipRect.Height + cmdTranslationY - clipOffsetY) * clipScaleY;

        if (clipMaxX <= clipMinX || clipMaxY <= clipMinY)
        {
            scissorX = 0;
            scissorY = 0;
            scissorW = 0;
            scissorH = 0;
            return false;
        }

        if (_legacyClipClampPath)
        {
            clipMinX = Math.Clamp(clipMinX, 0f, fbWidthF);
            clipMinY = Math.Clamp(clipMinY, 0f, fbHeightF);
            clipMaxX = Math.Clamp(clipMaxX, 0f, fbWidthF);
            clipMaxY = Math.Clamp(clipMaxY, 0f, fbHeightF);
        }
        else
        {
            clipMinX = clipMinX < 0f ? 0f : (clipMinX > fbWidthF ? fbWidthF : clipMinX);
            clipMinY = clipMinY < 0f ? 0f : (clipMinY > fbHeightF ? fbHeightF : clipMinY);
            clipMaxX = clipMaxX < 0f ? 0f : (clipMaxX > fbWidthF ? fbWidthF : clipMaxX);
            clipMaxY = clipMaxY < 0f ? 0f : (clipMaxY > fbHeightF ? fbHeightF : clipMaxY);
        }

        if (clipMaxX <= clipMinX || clipMaxY <= clipMinY)
        {
            scissorX = 0;
            scissorY = 0;
            scissorW = 0;
            scissorH = 0;
            return false;
        }

        var minX = (int)MathF.Floor(clipMinX);
        var minY = (int)MathF.Floor(clipMinY);
        var maxX = (int)MathF.Ceiling(clipMaxX - epsilon);
        var maxY = (int)MathF.Ceiling(clipMaxY - epsilon);

        var fbWidthI = (int)fbWidthF;
        var fbHeightI = (int)fbHeightF;
        if (minX < 0)
        {
            minX = 0;
        }

        if (minY < 0)
        {
            minY = 0;
        }

        if (maxX > fbWidthI)
        {
            maxX = fbWidthI;
        }

        if (maxY > fbHeightI)
        {
            maxY = fbHeightI;
        }

        if (maxX <= minX || maxY <= minY)
        {
            scissorX = 0;
            scissorY = 0;
            scissorW = 0;
            scissorH = 0;
            return false;
        }

        scissorX = minX;
        scissorY = minY;
        scissorW = (uint)(maxX - minX);
        scissorH = (uint)(maxY - minY);
        return true;
    }

    private bool TryComputeDynamicCoverageScissor(
        UiDrawData drawData,
        float clipOffsetX,
        float clipOffsetY,
        float clipScaleX,
        float clipScaleY,
        float fbWidth,
        float fbHeight,
        out int scissorX,
        out int scissorY,
        out uint scissorW,
        out uint scissorH)
    {
        var hasCoverage = false;
        var minX = 0f;
        var minY = 0f;
        var maxX = 0f;
        var maxY = 0f;

        for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
        {
            var drawList = drawData.DrawLists[listIndex];
            for (var cmdIndex = 0; cmdIndex < drawList.Commands.Count; cmdIndex++)
            {
                ref readonly var cmd = ref drawList.Commands.ItemRef(cmdIndex);
                if (IsStaticLayerGeometryTag(cmd.UserData, out _))
                {
                    continue;
                }

                var cmdTranslationX = cmd.Translation.X;
                var cmdTranslationY = cmd.Translation.Y;
                var clipMinX = (cmd.ClipRect.X + cmdTranslationX - clipOffsetX) * clipScaleX;
                var clipMinY = (cmd.ClipRect.Y + cmdTranslationY - clipOffsetY) * clipScaleY;
                var clipMaxX = (cmd.ClipRect.X + cmd.ClipRect.Width + cmdTranslationX - clipOffsetX) * clipScaleX;
                var clipMaxY = (cmd.ClipRect.Y + cmd.ClipRect.Height + cmdTranslationY - clipOffsetY) * clipScaleY;

                if (clipMaxX <= 0f || clipMaxY <= 0f || clipMinX >= fbWidth || clipMinY >= fbHeight)
                {
                    continue;
                }

                if (!hasCoverage)
                {
                    minX = clipMinX;
                    minY = clipMinY;
                    maxX = clipMaxX;
                    maxY = clipMaxY;
                    hasCoverage = true;
                }
                else
                {
                    minX = MathF.Min(minX, clipMinX);
                    minY = MathF.Min(minY, clipMinY);
                    maxX = MathF.Max(maxX, clipMaxX);
                    maxY = MathF.Max(maxY, clipMaxY);
                }
            }
        }

        if (!hasCoverage)
        {
            scissorX = 0;
            scissorY = 0;
            scissorW = 0;
            scissorH = 0;
            return false;
        }

        var minXi = (int)MathF.Max(0f, MathF.Floor(minX));
        var minYi = (int)MathF.Max(0f, MathF.Floor(minY));
        var maxXi = (int)MathF.Min(fbWidth, MathF.Ceiling(maxX));
        var maxYi = (int)MathF.Min(fbHeight, MathF.Ceiling(maxY));

        if (maxXi <= minXi || maxYi <= minYi)
        {
            scissorX = 0;
            scissorY = 0;
            scissorW = 0;
            scissorH = 0;
            return false;
        }

        scissorX = minXi;
        scissorY = minYi;
        scissorW = (uint)(maxXi - minXi);
        scissorH = (uint)(maxYi - minYi);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void RecordCommandBuffer(
        CommandBuffer commandBuffer,
        uint imageIndex,
        UiDrawData drawData,
        VkBuffer vertexBuffer,
        VkBuffer indexBuffer,
        VkBuffer rectPrimitiveBuffer,
        VkBuffer circlePrimitiveBuffer,
        Dictionary<int, StaticGeometryBuffer> staticBindings,
        bool taaNeedsClear,
        float jitterX,
        float jitterY,
        out long recordTextureLookupTicks,
        out long recordClippingTicks,
        out long recordDescriptorBindTicks,
        out long recordDrawCallTicks)
    {
        var profileEnabled = _profilingEnabled;
        var textureLookupTicksLocal = 0L;
        var clippingTicksLocal = 0L;
        var descriptorBindTicksLocal = 0L;
        var drawCallTicksLocal = 0L;

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        Check(_vk.BeginCommandBuffer(commandBuffer, &beginInfo));

        var fbWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        var fbHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);

        var clearValue = new ClearValue
        {
            Color = _clearColorValue,
        };

        var renderPassInfo = new RenderPassBeginInfo
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _framebuffers[imageIndex],
            RenderArea = new Rect2D(new Offset2D(0, 0), new Extent2D((uint)fbWidth, (uint)fbHeight)),
            ClearValueCount = 1,
            PClearValues = &clearValue,
        };

        var displaySize = drawData.DisplaySize;
        var displayPos = drawData.DisplayPos;
        var scaleX = 2f / displaySize.X;
        var scaleY = 2f / displaySize.Y;
        var translateX = -1f - displayPos.X * scaleX;
        var translateY = -1f - displayPos.Y * scaleY;
        var jitterTranslateX = translateX + (2f * jitterX / displaySize.X);
        var jitterTranslateY = translateY + (2f * jitterY / displaySize.Y);
        var normalPushConstants = stackalloc float[4];
        normalPushConstants[0] = scaleX;
        normalPushConstants[1] = scaleY;
        normalPushConstants[2] = translateX;
        normalPushConstants[3] = translateY;
        var temporalPushConstants = stackalloc float[4];
        temporalPushConstants[0] = scaleX;
        temporalPushConstants[1] = scaleY;
        temporalPushConstants[2] = jitterTranslateX;
        temporalPushConstants[3] = jitterTranslateY;


        var clipOffset = drawData.DisplayPos;
        var clipScale = drawData.FramebufferScale;
        var clipOffsetX = clipOffset.X;
        var clipOffsetY = clipOffset.Y;
        var clipScaleX = clipScale.X;
        var clipScaleY = clipScale.Y;
        var fbWidthF = (float)fbWidth;
        var fbHeightF = (float)fbHeight;

        void DrawCommandLists(bool renderFontOnly, bool renderNonFontOnly, bool renderStaticOnly, bool renderDynamicOnly)
        {
            var viewport = new Viewport(0, 0, fbWidth, fbHeight, 0, 1);
            _vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);

            uint globalVertexOffset = 0;
            uint globalIndexOffset = 0;
            uint globalRectPrimitiveOffset = 0;
            uint globalCirclePrimitiveOffset = 0;
            var hasBoundGeometryBuffers = false;
            var boundVertexBuffer = default(VkBuffer);
            var boundIndexBuffer = default(VkBuffer);
            var hasBoundPrimitiveBuffer = false;
            var boundPrimitiveBuffer = default(VkBuffer);
            var hasDescriptorSet = false;
            var lastDescriptorSet = default(DescriptorSet);
            var hasScissor = false;
            var lastScissorX = 0;
            var lastScissorY = 0;
            var lastScissorW = 0u;
            var lastScissorH = 0u;
            var hasLastTexture = false;
            var lastTextureId = default(UiTextureId);
            var lastTextureValid = false;
            var lastTexture = default(TextureResource);
            var currentPipeline = default(Pipeline);
            var hasPushMode = false;
            var currentTemporalPushMode = false;
            var lastPushTranslateX = 0f;
            var lastPushTranslateY = 0f;
            var dynamicPushConstants = stackalloc float[4];
            dynamicPushConstants[0] = scaleX;
            dynamicPushConstants[1] = scaleY;
            var emittedFontCommandDiag = 0;
            const int maxFontCommandDiagPerPass = 256;

            for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
            {
                var drawList = drawData.DrawLists[listIndex];
                var drawListVertexCount = drawList.Vertices.Count;
                var drawListIndexCount = drawList.Indices.Count;
                var drawListRectPrimitiveCount = drawList.RectFilledPrimitives?.Count ?? 0;
                var drawListCirclePrimitiveCount = drawList.CircleFilledPrimitives?.Count ?? 0;
                var hasStaticBinding = staticBindings.TryGetValue(listIndex, out var staticBinding);
                if (renderStaticOnly && !hasStaticBinding)
                {
                    continue;
                }

                if (renderDynamicOnly && hasStaticBinding)
                {
                    continue;
                }
                var commandCount = drawList.Commands.Count;
                for (var cmdIndex = 0; cmdIndex < commandCount; cmdIndex++)
                {
                    ref readonly var cmd = ref drawList.Commands.ItemRef(cmdIndex);
                    var isTriangleCommand = cmd.Kind is UiDrawCommandKind.Triangles;
                    var isFontCommand = IsFontTextureId(cmd.TextureId);
                    if (renderFontOnly && !isFontCommand)
                    {
                        continue;
                    }

                    if (renderNonFontOnly && isFontCommand)
                    {
                        continue;
                    }
                    var textureLookupStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
                    if (!hasLastTexture || !cmd.TextureId.Equals(lastTextureId))
                    {
                        lastTextureId = cmd.TextureId;
                        hasLastTexture = true;
                        if (!_textures.TryGetValue(cmd.TextureId, out lastTexture))
                        {
                            if (_fontCommandDiagEnabled && emittedFontCommandDiag < maxFontCommandDiagPerPass)
                            {
                                EmitFontCommandDiag($"frame={_frameIndex} image={imageIndex} list={listIndex} cmd={cmdIndex} tex={cmd.TextureId.Value} isFont={isFontCommand} missingTexture=1 elem={cmd.ElementCount} idxOff={cmd.IndexOffset} vtxOff={cmd.VertexOffset}");
                                emittedFontCommandDiag++;
                            }

                            lastTextureValid = false;
                            if (profileEnabled)
                            {
                                textureLookupTicksLocal += Stopwatch.GetTimestamp() - textureLookupStart;
                            }
                            continue;
                        }

                        lastTextureValid = true;
                    }
                    else if (!lastTextureValid)
                    {
                        if (profileEnabled)
                        {
                                textureLookupTicksLocal += Stopwatch.GetTimestamp() - textureLookupStart;
                        }
                        continue;
                    }

                    if (profileEnabled)
                    {
                            textureLookupTicksLocal += Stopwatch.GetTimestamp() - textureLookupStart;
                    }

                    if (isTriangleCommand && _fontCommandDiagEnabled && isFontCommand && emittedFontCommandDiag < maxFontCommandDiagPerPass)
                    {
                        EmitFontCommandDiag($"frame={_frameIndex} image={imageIndex} list={listIndex} cmd={cmdIndex} tex={cmd.TextureId.Value} isFont=1 elem={cmd.ElementCount} idxOff={cmd.IndexOffset} vtxOff={cmd.VertexOffset} clip=({cmd.ClipRect.X:0.###},{cmd.ClipRect.Y:0.###},{cmd.ClipRect.Width:0.###},{cmd.ClipRect.Height:0.###}) trans=({cmd.Translation.X:0.###},{cmd.Translation.Y:0.###})");
                        emittedFontCommandDiag++;
                    }

                    if (isTriangleCommand)
                    {
                        AnalyzeAndValidateFontCommandBounds(
                            drawList,
                            in cmd,
                            isFontCommand,
                            imageIndex,
                            listIndex,
                            cmdIndex,
                            ref emittedFontCommandDiag,
                            maxFontCommandDiagPerPass);
                    }

                    var texture = lastTexture;
                    var cmdTranslationX = cmd.Translation.X;
                    var cmdTranslationY = cmd.Translation.Y;

                    var clippingStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
                    if (!TryComputeScissorRect(
                        cmd,
                        clipOffsetX,
                        clipOffsetY,
                        clipScaleX,
                        clipScaleY,
                        fbWidthF,
                        fbHeightF,
                        out var scissorX,
                        out var scissorY,
                        out var scissorW,
                        out var scissorH))
                    {
                        if (profileEnabled)
                        {
                            clippingTicksLocal += Stopwatch.GetTimestamp() - clippingStart;
                        }

                        continue;
                    }

                    if (!hasScissor
                        || scissorX != lastScissorX
                        || scissorY != lastScissorY
                        || scissorW != lastScissorW
                        || scissorH != lastScissorH)
                    {
                        var scissor = new Rect2D(new Offset2D(scissorX, scissorY), new Extent2D(scissorW, scissorH));
                        _vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);
                        lastScissorX = scissorX;
                        lastScissorY = scissorY;
                        lastScissorW = scissorW;
                        lastScissorH = scissorH;
                        hasScissor = true;
                    }

                    if (profileEnabled)
                    {
                        clippingTicksLocal += Stopwatch.GetTimestamp() - clippingStart;
                    }

                    var descriptorBindStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
                    const bool useTemporalJitter = false;
                    var desiredPipeline = cmd.Kind switch
                    {
                        UiDrawCommandKind.Triangles => isFontCommand ? _subpixelPipeline : _graphicsPipeline,
                        UiDrawCommandKind.RectFilledPrimitives => _rectPrimitivePipeline,
                        UiDrawCommandKind.CircleFilledPrimitives => _circlePrimitivePipeline,
                        _ => throw new InvalidOperationException($"Unsupported draw command kind: {cmd.Kind}.")
                    };
                    if (currentPipeline.Handle != desiredPipeline.Handle)
                    {
                        _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, desiredPipeline);
                        currentPipeline = desiredPipeline;
                    }

                    var pushTranslateX = useTemporalJitter ? jitterTranslateX : translateX;
                    var pushTranslateY = useTemporalJitter ? jitterTranslateY : translateY;
                    if (cmdTranslationX != 0f)
                    {
                        pushTranslateX += scaleX * cmdTranslationX;
                    }

                    if (cmdTranslationY != 0f)
                    {
                        pushTranslateY += scaleY * cmdTranslationY;
                    }
                    if (!hasPushMode
                        || currentTemporalPushMode != useTemporalJitter
                        || MathF.Abs(lastPushTranslateX - pushTranslateX) > 0.000001f
                        || MathF.Abs(lastPushTranslateY - pushTranslateY) > 0.000001f)
                    {
                        dynamicPushConstants[2] = pushTranslateX;
                        dynamicPushConstants[3] = pushTranslateY;
                        _vk.CmdPushConstants(commandBuffer, _pipelineLayout, ShaderStageFlags.VertexBit, 0, (uint)(sizeof(float) * 4), dynamicPushConstants);
                        currentTemporalPushMode = useTemporalJitter;
                        lastPushTranslateX = pushTranslateX;
                        lastPushTranslateY = pushTranslateY;
                        hasPushMode = true;
                    }

                    var descriptorSet = texture.DescriptorSet;
                    if (!hasDescriptorSet || descriptorSet.Handle != lastDescriptorSet.Handle)
                    {
                        _vk.CmdBindDescriptorSets(
                            commandBuffer,
                            PipelineBindPoint.Graphics,
                            _pipelineLayout,
                            0,
                            1,
                            &descriptorSet,
                            0,
                            null
                        );
                        lastDescriptorSet = descriptorSet;
                        hasDescriptorSet = true;
                    }

                    if (profileEnabled)
                    {
                        descriptorBindTicksLocal += Stopwatch.GetTimestamp() - descriptorBindStart;
                    }

                    if (isTriangleCommand)
                    {
                        var targetVertexBuffer = hasStaticBinding ? staticBinding.VertexBuffer : vertexBuffer;
                        var targetIndexBuffer = hasStaticBinding ? staticBinding.IndexBuffer : indexBuffer;
                        if (!hasBoundGeometryBuffers
                            || boundVertexBuffer.Handle != targetVertexBuffer.Handle
                            || boundIndexBuffer.Handle != targetIndexBuffer.Handle)
                        {
                            ulong vertexOffset = 0;
                            _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &targetVertexBuffer, &vertexOffset);
                            _vk.CmdBindIndexBuffer(commandBuffer, targetIndexBuffer, 0, IndexType.Uint32);
                            boundVertexBuffer = targetVertexBuffer;
                            boundIndexBuffer = targetIndexBuffer;
                            hasBoundGeometryBuffers = true;
                        }

                        var firstIndex = hasStaticBinding ? cmd.IndexOffset : cmd.IndexOffset + globalIndexOffset;
                        var vertexOffsetCommand = hasStaticBinding
                            ? (int)cmd.VertexOffset
                            : (int)(cmd.VertexOffset + globalVertexOffset);
                        var drawCallStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
                        _vk.CmdDrawIndexed(commandBuffer, cmd.ElementCount, 1, firstIndex, vertexOffsetCommand, 0);
                        if (profileEnabled)
                        {
                            drawCallTicksLocal += Stopwatch.GetTimestamp() - drawCallStart;
                        }

                        continue;
                    }

                    var primitiveBuffer = cmd.Kind is UiDrawCommandKind.RectFilledPrimitives
                        ? rectPrimitiveBuffer
                        : circlePrimitiveBuffer;
                    if (!hasBoundPrimitiveBuffer || boundPrimitiveBuffer.Handle != primitiveBuffer.Handle)
                    {
                        ulong primitiveOffset = 0;
                        _vk.CmdBindVertexBuffers(commandBuffer, 1, 1, &primitiveBuffer, &primitiveOffset);
                        boundPrimitiveBuffer = primitiveBuffer;
                        hasBoundPrimitiveBuffer = true;
                    }

                    var firstInstance = cmd.Kind is UiDrawCommandKind.RectFilledPrimitives
                        ? cmd.IndexOffset + globalRectPrimitiveOffset
                        : cmd.IndexOffset + globalCirclePrimitiveOffset;
                    var vertexCount = cmd.Kind is UiDrawCommandKind.RectFilledPrimitives ? 6u : cmd.VertexOffset * 3u;
                    var primitiveDrawCallStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
                    _vk.CmdDraw(commandBuffer, vertexCount, cmd.ElementCount, 0, firstInstance);
                    if (profileEnabled)
                    {
                        drawCallTicksLocal += Stopwatch.GetTimestamp() - primitiveDrawCallStart;
                    }
                }

                if (!hasStaticBinding)
                {
                    globalVertexOffset += (uint)drawListVertexCount;
                    globalIndexOffset += (uint)drawListIndexCount;
                }

                globalRectPrimitiveOffset += (uint)drawListRectPrimitiveCount;
                globalCirclePrimitiveOffset += (uint)drawListCirclePrimitiveCount;
            }
        }

        _vk.CmdBeginRenderPass(commandBuffer, &renderPassInfo, SubpassContents.Inline);

        DrawCommandLists(renderFontOnly: false, renderNonFontOnly: false, renderStaticOnly: false, renderDynamicOnly: false);
        _vk.CmdEndRenderPass(commandBuffer);

        recordTextureLookupTicks = textureLookupTicksLocal;
        recordClippingTicks = clippingTicksLocal;
        recordDescriptorBindTicks = descriptorBindTicksLocal;
        recordDrawCallTicks = drawCallTicksLocal;

        Check(_vk.EndCommandBuffer(commandBuffer));
    }
}
