using Duxel.Core;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private struct CommandPipelineState
    {
        public bool HasDrawMode;
        public uint CurrentDrawMode;
        public int BindCount;
        public int TriangleBindCount;
        public int FontBindCount;
        public int RectPrimitiveBindCount;
        public int CirclePrimitiveBindCount;
        public int ActualFontBindCount;
        public int ActualTexturedTriangleBindCount;
        public int ActualTexturedPrimitiveBindCount;
        public long BindTicks;
    }

    private const uint DrawModePushOffset = 20;

    /// <summary>
    /// The single GPU-driven pipeline is bound once per frame; per-draw geometry
    /// interpretation switches through a small vertex push constant instead of
    /// pipeline binds. Profile counters keep their historical "bind" naming so
    /// existing benchmark tooling stays comparable.
    /// </summary>
    private void PushDrawModeIfNeeded(
        CommandBuffer commandBuffer,
        uint drawMode,
        UiDrawCommandKind commandKind,
        bool isFontCommand,
        bool profileEnabled,
        ref CommandPipelineState state)
    {
        if (state.HasDrawMode && state.CurrentDrawMode == drawMode)
        {
            return;
        }

        var pushStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdPushConstants(
            commandBuffer,
            _pipelineLayout,
            ShaderStageFlags.VertexBit,
            DrawModePushOffset,
            sizeof(uint),
            &drawMode);
        if (profileEnabled)
        {
            state.BindTicks += Stopwatch.GetTimestamp() - pushStart;
            state.BindCount++;
            switch (commandKind)
            {
                case UiDrawCommandKind.Triangles when isFontCommand:
                    state.FontBindCount++;
                    break;
                case UiDrawCommandKind.Triangles:
                    state.TriangleBindCount++;
                    break;
                case UiDrawCommandKind.RectFilledPrimitives:
                    state.RectPrimitiveBindCount++;
                    break;
                case UiDrawCommandKind.CircleFilledPrimitives:
                    state.CirclePrimitiveBindCount++;
                    break;
            }

            if (drawMode is 0u)
            {
                if (isFontCommand)
                {
                    state.ActualFontBindCount++;
                }
                else
                {
                    state.ActualTexturedTriangleBindCount++;
                }
            }
            else
            {
                state.ActualTexturedPrimitiveBindCount++;
            }
        }

        state.CurrentDrawMode = drawMode;
        state.HasDrawMode = true;
    }

    /// <summary>
    /// High bit of the fragment push-constant texture index selects the ClearType
    /// subpixel coverage output mode in the unified fragment shader.
    /// </summary>
    private const uint SubpixelCoverageModeBit = 0x80000000u;

    private struct CommandDescriptorState
    {
        public bool HasTextureIndex;
        public uint LastTextureIndex;
        public int BindCount;
        public long BindTicks;
    }

    private void BindGlobalBindlessTextureSet(CommandBuffer commandBuffer)
    {
        var descriptorSet = _bindlessTextureSet;
        _vk.CmdBindDescriptorSets(
            commandBuffer,
            PipelineBindPoint.Graphics,
            _pipelineLayout,
            0,
            1,
            &descriptorSet,
            0,
            null);
    }

    private void PushTextureIndexIfNeeded(
        CommandBuffer commandBuffer,
        uint textureIndex,
        bool profileEnabled,
        ref CommandDescriptorState state)
    {
        if (state.HasTextureIndex && textureIndex == state.LastTextureIndex)
        {
            return;
        }

        var pushStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdPushConstants(
            commandBuffer,
            _pipelineLayout,
            ShaderStageFlags.FragmentBit,
            40,
            sizeof(uint),
            &textureIndex);
        if (profileEnabled)
        {
            state.BindTicks += Stopwatch.GetTimestamp() - pushStart;
            state.BindCount++;
        }

        state.LastTextureIndex = textureIndex;
        state.HasTextureIndex = true;
    }

    private struct CommandBufferBindingState
    {
        public bool HasVertexAddress;
        public ulong CurrentVertexAddress;
        public bool HasBoundIndexBuffer;
        public VkBuffer BoundIndexBuffer;
        public bool HasPrimitiveAddress;
        public ulong CurrentPrimitiveAddress;
        public int GeometryBindCount;
        public int PrimitiveBindCount;
        public long BindTicks;
    }

    private const uint VertexAddressPushOffset = 24;
    private const uint PrimitiveAddressPushOffset = 32;

    private void BindTriangleGeometryIfNeeded(
        CommandBuffer commandBuffer,
        ulong vertexAddress,
        VkBuffer indexBuffer,
        bool profileEnabled,
        ref CommandBufferBindingState state)
    {
        var vertexMatches = state.HasVertexAddress && state.CurrentVertexAddress == vertexAddress;
        var indexMatches = state.HasBoundIndexBuffer && state.BoundIndexBuffer.Handle == indexBuffer.Handle;
        if (vertexMatches && indexMatches)
        {
            return;
        }

        var bufferBindStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        if (!vertexMatches)
        {
            _vk.CmdPushConstants(
                commandBuffer,
                _pipelineLayout,
                ShaderStageFlags.VertexBit,
                VertexAddressPushOffset,
                sizeof(ulong),
                &vertexAddress);
            state.CurrentVertexAddress = vertexAddress;
            state.HasVertexAddress = true;
        }

        if (!indexMatches)
        {
            _vk.CmdBindIndexBuffer(commandBuffer, indexBuffer, 0, IndexType.Uint32);
            state.BoundIndexBuffer = indexBuffer;
            state.HasBoundIndexBuffer = true;
        }

        if (profileEnabled)
        {
            state.BindTicks += Stopwatch.GetTimestamp() - bufferBindStart;
            state.GeometryBindCount++;
        }
    }

    private void PushPrimitiveAddressIfNeeded(
        CommandBuffer commandBuffer,
        ulong primitiveAddress,
        bool profileEnabled,
        ref CommandBufferBindingState state)
    {
        if (state.HasPrimitiveAddress && state.CurrentPrimitiveAddress == primitiveAddress)
        {
            return;
        }

        var bufferBindStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdPushConstants(
            commandBuffer,
            _pipelineLayout,
            ShaderStageFlags.VertexBit,
            PrimitiveAddressPushOffset,
            sizeof(ulong),
            &primitiveAddress);
        if (profileEnabled)
        {
            state.BindTicks += Stopwatch.GetTimestamp() - bufferBindStart;
            state.PrimitiveBindCount++;
        }

        state.CurrentPrimitiveAddress = primitiveAddress;
        state.HasPrimitiveAddress = true;
    }

    private struct CommandPushConstantState
    {
        public bool HasPushMode;
        public bool CurrentTemporalPushMode;
        public float LastTranslateX;
        public float LastTranslateY;
        public float LastOpacity;
        public int PushCount;
        public long PushTicks;
    }

    private void ApplyPushConstantsIfNeeded(
        in CommandFrameContext context,
        float commandTranslationX,
        float commandTranslationY,
        float commandOpacity,
        ref CommandPushConstantState state)
    {
        var pushTranslateX = context.UseTemporalJitter ? context.JitterTranslateX : context.TranslateX;
        var pushTranslateY = context.UseTemporalJitter ? context.JitterTranslateY : context.TranslateY;
        if (commandTranslationX != 0f)
        {
            pushTranslateX += context.ScaleX * commandTranslationX;
        }

        if (commandTranslationY != 0f)
        {
            pushTranslateY += context.ScaleY * commandTranslationY;
        }

        var pushTransformChanged = !state.HasPushMode
            || state.CurrentTemporalPushMode != context.UseTemporalJitter
            || MathF.Abs(state.LastTranslateX - pushTranslateX) > 0.000001f
            || MathF.Abs(state.LastTranslateY - pushTranslateY) > 0.000001f;
        var pushOpacityChanged = !state.HasPushMode
            || MathF.Abs(state.LastOpacity - commandOpacity) > 0.000001f;
        if (pushTransformChanged)
        {
            var pushConstants = stackalloc float[5];
            pushConstants[0] = context.ScaleX;
            pushConstants[1] = context.ScaleY;
            pushConstants[2] = pushTranslateX;
            pushConstants[3] = pushTranslateY;
            if (pushOpacityChanged)
            {
                pushConstants[4] = commandOpacity;
                var pushConstantStart = context.ProfileEnabled ? Stopwatch.GetTimestamp() : 0;
                _vk.CmdPushConstants(
                    context.CommandBuffer,
                    _pipelineLayout,
                    ShaderStageFlags.VertexBit,
                    0,
                    (uint)(sizeof(float) * 5),
                    pushConstants);
                if (context.ProfileEnabled)
                {
                    state.PushTicks += Stopwatch.GetTimestamp() - pushConstantStart;
                }

                state.LastOpacity = commandOpacity;
            }
            else
            {
                var pushConstantStart = context.ProfileEnabled ? Stopwatch.GetTimestamp() : 0;
                _vk.CmdPushConstants(
                    context.CommandBuffer,
                    _pipelineLayout,
                    ShaderStageFlags.VertexBit,
                    0,
                    (uint)(sizeof(float) * 4),
                    pushConstants);
                if (context.ProfileEnabled)
                {
                    state.PushTicks += Stopwatch.GetTimestamp() - pushConstantStart;
                }
            }

            state.CurrentTemporalPushMode = context.UseTemporalJitter;
            state.LastTranslateX = pushTranslateX;
            state.LastTranslateY = pushTranslateY;
            state.HasPushMode = true;
            if (context.ProfileEnabled)
            {
                state.PushCount++;
            }
        }
        else if (pushOpacityChanged)
        {
            var opacityPushConstant = commandOpacity;
            var pushConstantStart = context.ProfileEnabled ? Stopwatch.GetTimestamp() : 0;
            _vk.CmdPushConstants(
                context.CommandBuffer,
                _pipelineLayout,
                ShaderStageFlags.VertexBit,
                (uint)(sizeof(float) * 4),
                (uint)sizeof(float),
                &opacityPushConstant);
            if (context.ProfileEnabled)
            {
                state.PushTicks += Stopwatch.GetTimestamp() - pushConstantStart;
            }

            state.LastOpacity = commandOpacity;
            if (context.ProfileEnabled)
            {
                state.PushCount++;
            }
        }
    }

    private readonly bool _legacyClipClampPath = ParseLegacyClipClampPathEnabled();

    private struct CommandScissorState
    {
        public bool HasScissor;
        public int LastScissorX;
        public int LastScissorY;
        public uint LastScissorW;
        public uint LastScissorH;
        public bool HasComputedScissor;
        public UiRect LastComputedClipRect;
        public float LastComputedTranslationX;
        public float LastComputedTranslationY;
        public bool LastComputedScissorVisible;
        public int LastComputedScissorX;
        public int LastComputedScissorY;
        public uint LastComputedScissorW;
        public uint LastComputedScissorH;
        public int ScissorSetCount;
        public int ScissorComputeCount;
        public int ScissorComputeReuseCount;
        public long ClippingTicks;
        public long ScissorSetTicks;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryComputeScissorRect(
        in UiDrawCommand command,
        in CommandFrameContext context,
        out int scissorX,
        out int scissorY,
        out uint scissorW,
        out uint scissorH)
    {
        const float epsilon = 0.0001f;

        var commandTranslationX = command.Translation.X;
        var commandTranslationY = command.Translation.Y;

        var clipMinX = (command.ClipRect.X + commandTranslationX - context.ClipOffsetX) * context.ClipScaleX;
        var clipMinY = (command.ClipRect.Y + commandTranslationY - context.ClipOffsetY) * context.ClipScaleY;
        var clipMaxX = (command.ClipRect.X + command.ClipRect.Width + commandTranslationX - context.ClipOffsetX) * context.ClipScaleX;
        var clipMaxY = (command.ClipRect.Y + command.ClipRect.Height + commandTranslationY - context.ClipOffsetY) * context.ClipScaleY;

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
            clipMinX = Math.Clamp(clipMinX, 0f, context.FramebufferWidth);
            clipMinY = Math.Clamp(clipMinY, 0f, context.FramebufferHeight);
            clipMaxX = Math.Clamp(clipMaxX, 0f, context.FramebufferWidth);
            clipMaxY = Math.Clamp(clipMaxY, 0f, context.FramebufferHeight);
        }
        else
        {
            clipMinX = clipMinX < 0f ? 0f : (clipMinX > context.FramebufferWidth ? context.FramebufferWidth : clipMinX);
            clipMinY = clipMinY < 0f ? 0f : (clipMinY > context.FramebufferHeight ? context.FramebufferHeight : clipMinY);
            clipMaxX = clipMaxX < 0f ? 0f : (clipMaxX > context.FramebufferWidth ? context.FramebufferWidth : clipMaxX);
            clipMaxY = clipMaxY < 0f ? 0f : (clipMaxY > context.FramebufferHeight ? context.FramebufferHeight : clipMaxY);
        }

        if (clipMaxX <= clipMinX || clipMaxY <= clipMinY)
        {
            scissorX = 0;
            scissorY = 0;
            scissorW = 0;
            scissorH = 0;
            return false;
        }

        var minX = (int)clipMinX;
        var minY = (int)clipMinY;
        var maxXF = clipMaxX - epsilon;
        var maxYF = clipMaxY - epsilon;
        var maxX = (int)maxXF;
        var maxY = (int)maxYF;
        if (maxXF > maxX)
        {
            maxX++;
        }

        if (maxYF > maxY)
        {
            maxY++;
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

    private bool TryApplyScissorForCommand(
        in UiDrawCommand command,
        in CommandFrameContext context,
        ref CommandScissorState state)
    {
        var clippingStart = context.ProfileEnabled ? Stopwatch.GetTimestamp() : 0;
        var commandTranslationX = command.Translation.X;
        var commandTranslationY = command.Translation.Y;
        int scissorX;
        int scissorY;
        uint scissorW;
        uint scissorH;
        var clipRect = command.ClipRect;
        var reusedComputedScissor = state.HasComputedScissor
            && clipRect == state.LastComputedClipRect
            && commandTranslationX == state.LastComputedTranslationX
            && commandTranslationY == state.LastComputedTranslationY;
        if (reusedComputedScissor)
        {
            if (context.ProfileEnabled)
            {
                state.ScissorComputeReuseCount++;
            }

            if (!state.LastComputedScissorVisible)
            {
                if (context.ProfileEnabled)
                {
                    state.ClippingTicks += Stopwatch.GetTimestamp() - clippingStart;
                }

                return false;
            }

            scissorX = state.LastComputedScissorX;
            scissorY = state.LastComputedScissorY;
            scissorW = state.LastComputedScissorW;
            scissorH = state.LastComputedScissorH;
        }
        else
        {
            if (context.ProfileEnabled)
            {
                state.ScissorComputeCount++;
            }

            var computedScissorVisible = TryComputeScissorRect(
                command,
                in context,
                out scissorX,
                out scissorY,
                out scissorW,
                out scissorH);
            state.LastComputedClipRect = clipRect;
            state.LastComputedTranslationX = commandTranslationX;
            state.LastComputedTranslationY = commandTranslationY;
            state.LastComputedScissorVisible = computedScissorVisible;
            state.LastComputedScissorX = scissorX;
            state.LastComputedScissorY = scissorY;
            state.LastComputedScissorW = scissorW;
            state.LastComputedScissorH = scissorH;
            state.HasComputedScissor = true;
            if (!computedScissorVisible)
            {
                if (context.ProfileEnabled)
                {
                    state.ClippingTicks += Stopwatch.GetTimestamp() - clippingStart;
                }

                return false;
            }
        }

        if (!state.HasScissor
            || scissorX != state.LastScissorX
            || scissorY != state.LastScissorY
            || scissorW != state.LastScissorW
            || scissorH != state.LastScissorH)
        {
            var scissor = new Rect2D(new Offset2D(scissorX, scissorY), new Extent2D(scissorW, scissorH));
            var scissorSetStart = context.ProfileEnabled ? Stopwatch.GetTimestamp() : 0;
            _vk.CmdSetScissor(context.CommandBuffer, 0, 1, &scissor);
            if (context.ProfileEnabled)
            {
                state.ScissorSetTicks += Stopwatch.GetTimestamp() - scissorSetStart;
            }

            state.LastScissorX = scissorX;
            state.LastScissorY = scissorY;
            state.LastScissorW = scissorW;
            state.LastScissorH = scissorH;
            state.HasScissor = true;
            if (context.ProfileEnabled)
            {
                state.ScissorSetCount++;
            }
        }

        if (context.ProfileEnabled)
        {
            state.ClippingTicks += Stopwatch.GetTimestamp() - clippingStart;
        }

        return true;
    }
}

