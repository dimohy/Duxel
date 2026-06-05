using System;
using System.Diagnostics;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
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
}
