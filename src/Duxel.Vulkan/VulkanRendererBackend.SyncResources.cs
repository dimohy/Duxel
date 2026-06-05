using VulkanSemaphore = Duxel.Vulkan.Semaphore;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private const uint GpuProfileTimestampQueryCount = 2;
    private const uint GpuProfileStartQuery = 0;
    private const uint GpuProfileEndQuery = 1;

    private FrameResources[] _frames = Array.Empty<FrameResources>();
    private Fence[] _imagesInFlight = Array.Empty<Fence>();
    private int _framesInFlight;
    private FrameSemaphores[] _frameSemaphores = Array.Empty<FrameSemaphores>();
    private int _semaphoreIndex;

    private unsafe void CreateSyncObjects()
    {
        var framesInFlight = _framesInFlight;
        _frames = new FrameResources[framesInFlight];
        _imagesInFlight = new Fence[_swapchainImages.Length];
        _frameIndex = 0;
        _semaphoreIndex = 0;

        var semaphoreCount = Math.Max(2, _swapchainImages.Length + 1);
        _frameSemaphores = new FrameSemaphores[semaphoreCount];

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamily,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
        };

        var semaphoreInfo = new SemaphoreCreateInfo
        {
            SType = StructureType.SemaphoreCreateInfo,
        };

        var fenceInfo = new FenceCreateInfo
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit,
        };

        CreateUploadCommandResources(fenceInfo);

        var poolPtr = stackalloc CommandPool[1];
        var commandBufferPtr = stackalloc CommandBuffer[1];
        var imageAvailablePtr = stackalloc VulkanSemaphore[1];
        var renderFinishedPtr = stackalloc VulkanSemaphore[1];
        var fencePtr = stackalloc Fence[1];

        for (var i = 0; i < semaphoreCount; i++)
        {
            Check(_vk.CreateSemaphore(_device, &semaphoreInfo, null, imageAvailablePtr));
            Check(_vk.CreateSemaphore(_device, &semaphoreInfo, null, renderFinishedPtr));
            _frameSemaphores[i] = new FrameSemaphores(imageAvailablePtr[0], renderFinishedPtr[0]);
        }

        for (var i = 0; i < framesInFlight; i++)
        {
            var framePool = default(CommandPool);
            Check(_vk.CreateCommandPool(_device, &poolInfo, null, poolPtr));
            framePool = poolPtr[0];

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = framePool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1,
            };

            var frameBuffer = default(CommandBuffer);
            Check(_vk.AllocateCommandBuffers(_device, &allocInfo, commandBufferPtr));
            frameBuffer = commandBufferPtr[0];

            var frame = new FrameResources
            {
                CommandPool = framePool,
                CommandBuffer = frameBuffer,
            };

            Check(_vk.CreateFence(_device, &fenceInfo, null, fencePtr));
            frame.InFlight = fencePtr[0];

            if (_gpuProfilingEnabled)
            {
                frame.TimestampQueryPool = CreateFrameTimestampQueryPool();
            }

            _frames[i] = frame;
        }
    }

    private unsafe QueryPool CreateFrameTimestampQueryPool()
    {
        var queryPoolInfo = new QueryPoolCreateInfo
        {
            SType = StructureType.QueryPoolCreateInfo,
            QueryType = QueryType.Timestamp,
            QueryCount = GpuProfileTimestampQueryCount,
        };

        var queryPool = default(QueryPool);
        var result = _vk.CreateQueryPool(_device, &queryPoolInfo, null, &queryPool);
        if (result == Result.Success)
        {
            return queryPool;
        }

        TraceVulkanFailure(result);
        _gpuProfilingEnabled = false;
        return default;
    }
}
