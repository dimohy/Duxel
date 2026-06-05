using System;
using System.Runtime.CompilerServices;
using VkBuffer = Duxel.Vulkan.Buffer;
using VulkanSemaphore = Duxel.Vulkan.Semaphore;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private CommandPool _uploadCommandPool;
    private CommandBuffer _uploadCommandBuffer;
    private CommandPool _graphicsUploadCommandPool;
    private CommandBuffer _graphicsUploadCommandBuffer;
    private VulkanSemaphore _graphicsUploadPreparedSemaphore;
    private Fence _uploadFence;
    private bool _hasPendingUploadWork;
    private int _uploadBatchDepth;
    private bool _uploadBatchRecording;
    private bool _uploadPrepareRecording;
    private nuint _stagingBatchOffset;
    private VkBuffer _stagingBuffer;
    private DeviceMemory _stagingMemory;
    private nuint _stagingBufferSize;
    private unsafe void* _stagingMappedPtr;
    private bool _stagingMapped;
    private readonly bool _uploadBatchingEnabled = ParseUploadBatchingEnabled();
    private int _profileUploadSubmissionCount;
    private int _profileUploadPrepareSubmissionCount;
    private int _profileUploadWaitCount;
    private int _profileUploadBatchFlushCount;
    private int _profileUploadTextureCopyRegionCount;
    private int _profileUploadBufferCopyCount;
    private ulong _profileUploadStagingBytes;
    private long _profileUploadSubmitTicks;
    private long _profileUploadPrepareSubmitTicks;
    private long _profileUploadWaitTicks;

    private static bool ParseUploadBatchingEnabled()
    {
        var raw = Environment.GetEnvironmentVariable("DUXEL_VK_UPLOAD_BATCH");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (string.Equals(raw, "0", StringComparison.Ordinal)
            || string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase);
    }

    private void ResetUploadProfileCounters()
    {
        _profileUploadSubmissionCount = 0;
        _profileUploadPrepareSubmissionCount = 0;
        _profileUploadWaitCount = 0;
        _profileUploadBatchFlushCount = 0;
        _profileUploadTextureCopyRegionCount = 0;
        _profileUploadBufferCopyCount = 0;
        _profileUploadStagingBytes = 0;
        _profileUploadSubmitTicks = 0L;
        _profileUploadPrepareSubmitTicks = 0L;
        _profileUploadWaitTicks = 0L;
    }

    private void RecordUploadStagingBytes(nuint bytes)
    {
        if (_profilingEnabled)
        {
            _profileUploadStagingBytes += bytes;
        }
    }

    private void RecordUploadTextureCopyProfile(int regionCount)
    {
        if (_profilingEnabled)
        {
            _profileUploadTextureCopyRegionCount += regionCount;
        }
    }

    private void RecordUploadBufferCopyProfile()
    {
        if (_profilingEnabled)
        {
            _profileUploadBufferCopyCount++;
        }
    }

    private unsafe void CreateUploadCommandResources(FenceCreateInfo fenceInfo)
    {
        var uploadPoolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _transferQueueFamily,
            Flags = CommandPoolCreateFlags.TransientBit | CommandPoolCreateFlags.ResetCommandBufferBit,
        };

        fixed (CommandPool* pool = &_uploadCommandPool)
        {
            Check(_vk.CreateCommandPool(_device, &uploadPoolInfo, null, pool));
        }

        fixed (Fence* uploadFence = &_uploadFence)
        {
            Check(_vk.CreateFence(_device, &fenceInfo, null, uploadFence));
        }

        CreateGraphicsUploadPrepareResources();

        _hasPendingUploadWork = false;
        _uploadBatchDepth = 0;
        _uploadBatchRecording = false;
        _uploadPrepareRecording = false;
        _stagingBatchOffset = 0;
    }

    private void DestroyUploadCommandResources()
    {
        if (_uploadCommandPool.Handle is 0)
        {
            return;
        }

        if (_uploadCommandBuffer.Handle is not 0)
        {
            var buffer = _uploadCommandBuffer;
            _vk.FreeCommandBuffers(_device, _uploadCommandPool, 1, &buffer);
            _uploadCommandBuffer = default;
        }

        if (_uploadFence.Handle is not 0)
        {
            _vk.DestroyFence(_device, _uploadFence, null);
            _uploadFence = default;
        }

        _hasPendingUploadWork = false;
        _uploadBatchDepth = 0;
        _uploadBatchRecording = false;
        _uploadPrepareRecording = false;
        _stagingBatchOffset = 0;

        _vk.DestroyCommandPool(_device, _uploadCommandPool, null);
        _uploadCommandPool = default;
        DestroyGraphicsUploadPrepareResources();
    }

    private void CreateGraphicsUploadPrepareResources()
    {
        if (!UsesDedicatedTransferUploadQueue())
        {
            return;
        }

        var preparePoolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamily,
            Flags = CommandPoolCreateFlags.TransientBit | CommandPoolCreateFlags.ResetCommandBufferBit,
        };

        fixed (CommandPool* pool = &_graphicsUploadCommandPool)
        {
            Check(_vk.CreateCommandPool(_device, &preparePoolInfo, null, pool));
        }

        var semaphoreInfo = new SemaphoreCreateInfo
        {
            SType = StructureType.SemaphoreCreateInfo,
        };

        fixed (VulkanSemaphore* semaphore = &_graphicsUploadPreparedSemaphore)
        {
            Check(_vk.CreateSemaphore(_device, &semaphoreInfo, null, semaphore));
        }
    }

    private void DestroyGraphicsUploadPrepareResources()
    {
        if (_graphicsUploadCommandBuffer.Handle is not 0 && _graphicsUploadCommandPool.Handle is not 0)
        {
            var buffer = _graphicsUploadCommandBuffer;
            _vk.FreeCommandBuffers(_device, _graphicsUploadCommandPool, 1, &buffer);
            _graphicsUploadCommandBuffer = default;
        }

        if (_graphicsUploadPreparedSemaphore.Handle is not 0)
        {
            _vk.DestroySemaphore(_device, _graphicsUploadPreparedSemaphore, null);
            _graphicsUploadPreparedSemaphore = default;
        }

        if (_graphicsUploadCommandPool.Handle is not 0)
        {
            _vk.DestroyCommandPool(_device, _graphicsUploadCommandPool, null);
            _graphicsUploadCommandPool = default;
        }
    }

    private unsafe void WaitForPendingUploadWork()
    {
        if (!_hasPendingUploadWork || _uploadFence.Handle is 0)
        {
            return;
        }

        fixed (Fence* fence = &_uploadFence)
        {
            var waitStart = BeginFrameProfileTiming(_profilingEnabled);
            Check(_vk.WaitForFences(_device, 1, fence, true, ulong.MaxValue));
            if (_profilingEnabled)
            {
                _profileUploadWaitCount++;
                _profileUploadWaitTicks += EndFrameProfileTiming(true, waitStart);
            }
        }

        _hasPendingUploadWork = false;
    }

    private bool UsesDedicatedTransferUploadQueue()
    {
        return !_devicePolicy.UseGraphicsQueueForUploads
            && _transferQueueFamily != _graphicsQueueFamily;
    }

    private CommandBuffer BeginStagingUpload(nuint size, out nuint stagingOffset)
    {
        RecordUploadStagingBytes(size);
        EnsureStagingBuffer(size);

        var nextOffset = AlignStagingUploadOffset(_stagingBatchOffset);
        if (_uploadBatchingEnabled
            && _uploadBatchDepth > 0
            && _uploadBatchRecording
            && nextOffset + size > _stagingBufferSize)
        {
            FlushUploadBatchRecording();
        }

        var commandBuffer = BeginSingleTimeCommands();
        if (_uploadBatchingEnabled && _uploadBatchDepth > 0)
        {
            nextOffset = AlignStagingUploadOffset(_stagingBatchOffset);
            if (nextOffset + size > _stagingBufferSize)
            {
                throw new InvalidOperationException("Staging upload range exceeds the staging buffer size.");
            }

            stagingOffset = nextOffset;
            _stagingBatchOffset = nextOffset + size;
            return commandBuffer;
        }

        stagingOffset = 0;
        return commandBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint AlignStagingUploadOffset(nuint offset)
    {
        const nuint alignment = 4;
        return (offset + alignment - 1) & ~(alignment - 1);
    }

    private void FlushUploadBatchRecording()
    {
        if (!_uploadBatchRecording || _uploadCommandBuffer.Handle is 0)
        {
            return;
        }

        var commandBuffer = _uploadCommandBuffer;
        if (_profilingEnabled)
        {
            _profileUploadBatchFlushCount++;
        }

        SubmitRecordedUploadCommands(commandBuffer);
        _uploadBatchRecording = false;
    }

    private unsafe void EnsureStagingBuffer(nuint size)
    {
        if (_stagingBuffer.Handle is not 0 && _stagingBufferSize >= size)
        {
            return;
        }

        PrepareForStagingBufferRecreate();

        var previousSize = _stagingBufferSize;

        DestroyStagingBuffer();

        var targetSize = size;
        if (previousSize > 0)
        {
            targetSize = Math.Max(targetSize, previousSize * 2);
        }

        CreateBuffer(
            targetSize,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out _stagingBuffer,
            out _stagingMemory
        );
        _stagingBufferSize = targetSize;

        void* mapped;
        Check(_vk.MapMemory(_device, _stagingMemory, 0, targetSize, 0, &mapped));
        _stagingMappedPtr = mapped;
        _stagingMapped = true;
    }

    private unsafe void PrepareForStagingBufferRecreate()
    {
        FlushUploadBatchRecording();

        WaitForPendingUploadWork();
        _stagingBatchOffset = 0;

        if (_uploadBatchDepth <= 0 || _uploadCommandBuffer.Handle is 0)
        {
            return;
        }

        fixed (Fence* fence = &_uploadFence)
        {
            Check(_vk.ResetFences(_device, 1, fence));
        }

        Check(_vk.ResetCommandBuffer(_uploadCommandBuffer, 0));

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        Check(_vk.BeginCommandBuffer(_uploadCommandBuffer, &beginInfo));
        _uploadBatchRecording = true;
    }

    private void DestroyStagingBuffer()
    {
        if (_stagingMapped)
        {
            _vk.UnmapMemory(_device, _stagingMemory);
            _stagingMapped = false;
            _stagingMappedPtr = null;
        }

        if (_stagingBuffer.Handle is not 0)
        {
            _vk.DestroyBuffer(_device, _stagingBuffer, null);
            _stagingBuffer = default;
        }

        if (_stagingMemory.Handle is not 0)
        {
            _vk.FreeMemory(_device, _stagingMemory, null);
            _stagingMemory = default;
        }

        _stagingBufferSize = 0;
        _stagingBatchOffset = 0;
    }

    private void BeginUploadBatch()
    {
        if (!_uploadBatchingEnabled)
        {
            return;
        }

        _uploadBatchDepth++;
    }

    private void EndUploadBatch()
    {
        if (!_uploadBatchingEnabled)
        {
            return;
        }

        if (_uploadBatchDepth <= 0)
        {
            return;
        }

        _uploadBatchDepth--;
        if (_uploadBatchDepth != 0 || !_uploadBatchRecording)
        {
            return;
        }

        FlushUploadBatchRecording();
    }

    private CommandBuffer BeginSingleTimeCommands()
    {
        if (_uploadCommandBuffer.Handle is 0)
        {
            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                Level = CommandBufferLevel.Primary,
                CommandPool = _uploadCommandPool,
                CommandBufferCount = 1,
            };

            fixed (CommandBuffer* uploadBuffer = &_uploadCommandBuffer)
            {
                Check(_vk.AllocateCommandBuffers(_device, &allocInfo, uploadBuffer));
            }
        }

        if (_uploadBatchDepth > 0)
        {
            if (_uploadBatchRecording)
            {
                return _uploadCommandBuffer;
            }

            WaitForPendingUploadWork();

            fixed (Fence* fence = &_uploadFence)
            {
                Check(_vk.ResetFences(_device, 1, fence));
            }

            Check(_vk.ResetCommandBuffer(_uploadCommandBuffer, 0));
            _stagingBatchOffset = 0;

            var batchBeginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
            };

            Check(_vk.BeginCommandBuffer(_uploadCommandBuffer, &batchBeginInfo));
            _uploadBatchRecording = true;
            return _uploadCommandBuffer;
        }

        WaitForPendingUploadWork();

        fixed (Fence* fence = &_uploadFence)
        {
            Check(_vk.ResetFences(_device, 1, fence));
        }

        Check(_vk.ResetCommandBuffer(_uploadCommandBuffer, 0));
        _stagingBatchOffset = 0;

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        Check(_vk.BeginCommandBuffer(_uploadCommandBuffer, &beginInfo));
        return _uploadCommandBuffer;
    }

    private void EndSingleTimeCommands(CommandBuffer commandBuffer)
    {
        if (_uploadBatchDepth > 0)
        {
            return;
        }

        SubmitRecordedUploadCommands(commandBuffer);
    }

    private CommandBuffer BeginTextureUploadPrepareCommands()
    {
        if (!UsesDedicatedTransferUploadQueue())
        {
            throw new InvalidOperationException("Texture upload prepare commands require a dedicated transfer upload queue.");
        }

        if (_graphicsUploadCommandBuffer.Handle is 0)
        {
            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                Level = CommandBufferLevel.Primary,
                CommandPool = _graphicsUploadCommandPool,
                CommandBufferCount = 1,
            };

            fixed (CommandBuffer* prepareBuffer = &_graphicsUploadCommandBuffer)
            {
                Check(_vk.AllocateCommandBuffers(_device, &allocInfo, prepareBuffer));
            }
        }

        if (_uploadPrepareRecording)
        {
            return _graphicsUploadCommandBuffer;
        }

        Check(_vk.ResetCommandBuffer(_graphicsUploadCommandBuffer, 0));

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        Check(_vk.BeginCommandBuffer(_graphicsUploadCommandBuffer, &beginInfo));
        _uploadPrepareRecording = true;
        return _graphicsUploadCommandBuffer;
    }

    private void SubmitRecordedUploadCommands(CommandBuffer commandBuffer)
    {
        Check(_vk.EndCommandBuffer(commandBuffer));
        var waitsForPrepare = SubmitGraphicsUploadPrepareCommands();

        var waitStage = PipelineStageFlags.TransferBit;
        var waitSemaphore = _graphicsUploadPreparedSemaphore;
        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = waitsForPrepare ? 1u : 0u,
            PWaitSemaphores = waitsForPrepare ? &waitSemaphore : null,
            PWaitDstStageMask = waitsForPrepare ? &waitStage : null,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };

        var submitStart = BeginFrameProfileTiming(_profilingEnabled);
        Check(_vk.QueueSubmit(_transferQueue, 1, &submitInfo, _uploadFence));
        if (_profilingEnabled)
        {
            _profileUploadSubmissionCount++;
            _profileUploadSubmitTicks += EndFrameProfileTiming(true, submitStart);
        }

        _hasPendingUploadWork = true;
    }

    private bool SubmitGraphicsUploadPrepareCommands()
    {
        if (!_uploadPrepareRecording || _graphicsUploadCommandBuffer.Handle is 0)
        {
            return false;
        }

        Check(_vk.EndCommandBuffer(_graphicsUploadCommandBuffer));

        var commandBuffer = _graphicsUploadCommandBuffer;
        var signalSemaphore = _graphicsUploadPreparedSemaphore;
        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &signalSemaphore,
        };

        var submitStart = BeginFrameProfileTiming(_profilingEnabled);
        Check(_vk.QueueSubmit(_graphicsQueue, 1, &submitInfo, default));
        if (_profilingEnabled)
        {
            _profileUploadPrepareSubmissionCount++;
            _profileUploadPrepareSubmitTicks += EndFrameProfileTiming(true, submitStart);
        }

        _uploadPrepareRecording = false;
        return true;
    }
}
