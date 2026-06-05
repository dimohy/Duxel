using System.Diagnostics;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private struct CommandDescriptorState
    {
        public bool HasDescriptorSet;
        public DescriptorSet LastDescriptorSet;
        public int BindCount;
        public long BindTicks;
    }

    private void BindDescriptorSetIfNeeded(
        CommandBuffer commandBuffer,
        DescriptorSet descriptorSet,
        bool profileEnabled,
        ref CommandDescriptorState state)
    {
        if (state.HasDescriptorSet && descriptorSet.Handle == state.LastDescriptorSet.Handle)
        {
            return;
        }

        var descriptorSetBindStart = profileEnabled ? Stopwatch.GetTimestamp() : 0;
        _vk.CmdBindDescriptorSets(
            commandBuffer,
            PipelineBindPoint.Graphics,
            _pipelineLayout,
            0,
            1,
            &descriptorSet,
            0,
            null);
        if (profileEnabled)
        {
            state.BindTicks += Stopwatch.GetTimestamp() - descriptorSetBindStart;
            state.BindCount++;
        }

        state.LastDescriptorSet = descriptorSet;
        state.HasDescriptorSet = true;
    }
}
