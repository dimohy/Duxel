namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private unsafe void WaitForAllInFlightFrameFences()
    {
        if (_frames.Length == 0)
        {
            return;
        }

        for (var i = 0; i < _frames.Length; i++)
        {
            var frame = _frames[i];
            if (frame is null || frame.InFlight.Handle is 0)
            {
                continue;
            }

            var fence = frame.InFlight;
            Check(_vk.WaitForFences(_device, 1, &fence, true, ulong.MaxValue));
        }
    }
}
