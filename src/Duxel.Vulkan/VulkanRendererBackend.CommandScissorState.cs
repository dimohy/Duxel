using System.Diagnostics;
using System.Runtime.CompilerServices;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
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
