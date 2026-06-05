namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    public void Dispose()
    {
        SavePipelineCache();
        DestroySwapchainDependentResources();
        DestroyDeviceResources();

        if (_device.Handle is not 0)
        {
            _vk.DestroyDevice(_device, null);
        }

        if (_surface.Handle is not 0)
        {
            _khrSurface.DestroySurface(_instance, _surface, null);
        }

        if (_instance.Handle is not 0)
        {
            _vk.DestroyInstance(_instance, null);
        }
    }

    private void DestroyDeviceResources()
    {
        if (_device.Handle is not 0)
        {
            if (!TryWaitForDeviceIdle("DestroyDeviceResources", throwOnFailure: false))
            {
                return;
            }
        }

        FlushPendingTextureDestroysAll();
        FlushPendingBufferDestroysAll();

        DestroyGeometryBuffers();
        DestroyTextureResources();
        DestroyStagingBuffer();
        DestroyPipelineCache();

        if (_descriptorPool.Handle is not 0)
        {
            _vk.DestroyDescriptorPool(_device, _descriptorPool, null);
            _descriptorPool = default;
        }

        if (_descriptorSetLayout.Handle is not 0)
        {
            _vk.DestroyDescriptorSetLayout(_device, _descriptorSetLayout, null);
            _descriptorSetLayout = default;
        }

        if (_pipelineLayout.Handle is not 0)
        {
            _vk.DestroyPipelineLayout(_device, _pipelineLayout, null);
            _pipelineLayout = default;
        }

        if (_fontSampler.Handle is not 0)
        {
            _vk.DestroySampler(_device, _fontSampler, null);
            _fontSampler = default;
        }

        if (_imageSampler.Handle is not 0)
        {
            _vk.DestroySampler(_device, _imageSampler, null);
            _imageSampler = default;
        }
    }
}
