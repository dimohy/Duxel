using System.Diagnostics;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private struct CommandPipelineState
    {
        public Pipeline CurrentPipeline;
        public int BindCount;
        public int TriangleBindCount;
        public int FontBindCount;
        public int RectPrimitiveBindCount;
        public int CirclePrimitiveBindCount;
        public int ActualFontBindCount;
        public int ActualTexturedTriangleBindCount;
        public int ActualColorTriangleBindCount;
        public int ActualTexturedPrimitiveBindCount;
        public int ActualColorPrimitiveBindCount;
        public int ActualSolidUnifiedBindCount;
        public long BindTicks;
    }

    private void BindPipelineIfNeeded(
        CommandBuffer commandBuffer,
        Pipeline desiredPipeline,
        UiDrawCommandKind commandKind,
        bool isFontCommand,
        bool profileEnabled,
        ref CommandPipelineState state)
    {
        if (state.CurrentPipeline.Handle == desiredPipeline.Handle)
        {
            return;
        }

        var pipelineBindStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, desiredPipeline);
        if (profileEnabled)
        {
            state.BindTicks += Stopwatch.GetTimestamp() - pipelineBindStart;
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

            if (desiredPipeline.Handle == _subpixelPipeline.Handle)
            {
                state.ActualFontBindCount++;
            }
            else if (desiredPipeline.Handle == _graphicsPipeline.Handle)
            {
                state.ActualTexturedTriangleBindCount++;
            }
            else if (desiredPipeline.Handle == _graphicsColorPipeline.Handle)
            {
                state.ActualColorTriangleBindCount++;
            }
            else if (desiredPipeline.Handle == _primitivePipeline.Handle)
            {
                state.ActualTexturedPrimitiveBindCount++;
            }
            else if (desiredPipeline.Handle == _primitiveColorPipeline.Handle)
            {
                state.ActualColorPrimitiveBindCount++;
            }
            else if (desiredPipeline.Handle == _solidColorPipeline.Handle)
            {
                state.ActualSolidUnifiedBindCount++;
            }
        }

        state.CurrentPipeline = desiredPipeline;
    }
}
