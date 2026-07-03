using Duxel.Core;
using System.Collections.Generic;
using System;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private unsafe void CreateImageViews()
    {
        _swapchainImageViews = new ImageView[_swapchainImages.Length];

        for (var i = 0; i < _swapchainImages.Length; i++)
        {
            var createInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainFormat,
                Components = new ComponentMapping(
                    ComponentSwizzle.Identity,
                    ComponentSwizzle.Identity,
                    ComponentSwizzle.Identity,
                    ComponentSwizzle.Identity
                ),
                SubresourceRange = new ImageSubresourceRange(
                    ImageAspectFlags.ColorBit,
                    0,
                    1,
                    0,
                    1
                ),
            };

            fixed (ImageView* view = &_swapchainImageViews[i])
            {
                Check(_vk.CreateImageView(_device, &createInfo, null, view));
            }
        }
    }

    private void QueueBufferDestroy(VkBuffer buffer, DeviceMemory memory)
    {
        if (buffer.Handle is 0 || memory.Handle is 0)
        {
            return;
        }

        if (_frames.Length == 0)
        {
            DestroyBufferResource(new BufferResource(buffer, memory));
            return;
        }

        var frameCount = _frames.Length;
        var frame = _frameIndex % frameCount;
        var destroyFrame = _frameIndex + frameCount;
        _frames[frame].PendingBufferDestroys.Add(new PendingBufferDestroy(new BufferResource(buffer, memory), destroyFrame));
    }

    private void DestroyBufferResource(BufferResource resource)
    {
        if (resource.Buffer.Handle is not 0)
        {
            _vk.DestroyBuffer(_device, resource.Buffer, null);
        }

        if (resource.Memory.Handle is not 0)
        {
            _vk.FreeMemory(_device, resource.Memory, null);
        }
    }

    private void FlushPendingBufferDestroys(int frame)
    {
        if (_frames.Length == 0)
        {
            return;
        }

        var list = _frames[frame].PendingBufferDestroys;
        if (list.Count == 0)
        {
            return;
        }

        var hasDueDestroy = false;
        for (var i = 0; i < list.Count; i++)
        {
            if (_frameIndex >= list[i].DestroyFrame)
            {
                hasDueDestroy = true;
                break;
            }
        }

        if (!hasDueDestroy)
        {
            return;
        }

        WaitForAllInFlightFrameFences();

        var write = 0;
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (_frameIndex >= entry.DestroyFrame)
            {
                DestroyBufferResource(entry.Resource);
                continue;
            }

            list[write++] = entry;
        }

        if (write < list.Count)
        {
            list.RemoveRange(write, list.Count - write);
        }
    }

    private void FlushPendingBufferDestroysAll()
    {
        if (_frames.Length == 0)
        {
            return;
        }

        WaitForAllInFlightFrameFences();

        for (var i = 0; i < _frames.Length; i++)
        {
            var list = _frames[i].PendingBufferDestroys;
            foreach (var entry in list)
            {
                DestroyBufferResource(entry.Resource);
            }
            list.Clear();
        }
    }

    private unsafe void CreateBuffer(
        nuint size,
        BufferUsageFlags usage,
        MemoryPropertyFlags properties,
        out VkBuffer buffer,
        out DeviceMemory memory,
        MemoryPropertyFlags preferredProperties = 0
    )
    {
        var queueFamilies = stackalloc uint[2];
        uint queueFamilyIndexCount = 0;
        uint* queueFamilyIndices = null;
        var sharingMode = SharingMode.Exclusive;
        if (_transferQueueFamily != _graphicsQueueFamily)
        {
            queueFamilies[0] = _graphicsQueueFamily;
            queueFamilies[1] = _transferQueueFamily;
            queueFamilyIndexCount = 2;
            queueFamilyIndices = queueFamilies;
            sharingMode = SharingMode.Concurrent;
        }

        var bufferInfo = stackalloc BufferCreateInfo[1];
        bufferInfo[0] = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = sharingMode,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = queueFamilyIndices,
        };

        var localBuffer = default(VkBuffer);
        Check(_vk.CreateBuffer(_device, bufferInfo, null, &localBuffer));
        buffer = localBuffer;

        _vk.GetBufferMemoryRequirements(_device, buffer, out var memRequirements);

        // Memory type selection is capability policy: when the caller states a
        // preferred set (e.g. BAR memory for shader-pulled dynamic geometry) and
        // the device exposes it, that type is used; otherwise the required set
        // decides. Missing required properties still fail explicitly.
        var memoryTypeIndex = preferredProperties is not 0
            && TryFindMemoryType(memRequirements.MemoryTypeBits, preferredProperties, out var preferredTypeIndex)
            ? preferredTypeIndex
            : FindMemoryType(memRequirements.MemoryTypeBits, properties);

        var allocFlagsInfo = new MemoryAllocateFlagsInfo
        {
            SType = StructureType.MemoryAllocateFlagsInfo,
            Flags = MemoryAllocateFlags.DeviceAddressBit,
        };
        var allocInfo = stackalloc MemoryAllocateInfo[1];
        allocInfo[0] = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            PNext = (usage & BufferUsageFlags.ShaderDeviceAddressBit) is not 0 ? (nint)(&allocFlagsInfo) : 0,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = memoryTypeIndex,
        };

        var localMemory = default(DeviceMemory);
        Check(_vk.AllocateMemory(_device, allocInfo, null, &localMemory));
        memory = localMemory;
        Check(_vk.BindBufferMemory(_device, buffer, localMemory, 0));
    }

    private ulong GetBufferDeviceAddress(VkBuffer buffer)
    {
        var info = new BufferDeviceAddressInfo
        {
            SType = StructureType.BufferDeviceAddressInfo,
            Buffer = buffer,
        };
        return _vk.GetBufferDeviceAddress(_device, &info);
    }

    private unsafe void CreateImage(
        int width,
        int height,
        Format format,
        ImageTiling tiling,
        ImageUsageFlags usage,
        MemoryPropertyFlags properties,
        out Image image,
        out DeviceMemory memory
    )
    {
        var queueFamilies = stackalloc uint[2];
        uint queueFamilyIndexCount = 0;
        uint* queueFamilyIndices = null;
        var sharingMode = SharingMode.Exclusive;
        if (_transferQueueFamily != _graphicsQueueFamily)
        {
            queueFamilies[0] = _graphicsQueueFamily;
            queueFamilies[1] = _transferQueueFamily;
            queueFamilyIndexCount = 2;
            queueFamilyIndices = queueFamilies;
            sharingMode = SharingMode.Concurrent;
        }

        var imageInfo = stackalloc ImageCreateInfo[1];
        imageInfo[0] = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D((uint)width, (uint)height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = tiling,
            InitialLayout = ImageLayout.Undefined,
            Usage = usage,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = sharingMode,
            QueueFamilyIndexCount = queueFamilyIndexCount,
            PQueueFamilyIndices = queueFamilyIndices,
        };

        var localImage = default(Image);
        Check(_vk.CreateImage(_device, imageInfo, null, &localImage));
        image = localImage;

        _vk.GetImageMemoryRequirements(_device, image, out var memRequirements);

        var allocInfo = stackalloc MemoryAllocateInfo[1];
        allocInfo[0] = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties),
        };

        var localMemory = default(DeviceMemory);
        Check(_vk.AllocateMemory(_device, allocInfo, null, &localMemory));
        memory = localMemory;
        Check(_vk.BindImageMemory(_device, image, localMemory, 0));
    }

    private ImageView CreateImageView(Image image, Format format)
    {
        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        ImageView view = default;
        Check(_vk.CreateImageView(_device, &viewInfo, null, &view));
        return view;
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        if (TryFindMemoryType(typeFilter, properties, out var index))
        {
            return index;
        }

        throw new InvalidOperationException("Failed to find a suitable memory type.");
    }

    private bool TryFindMemoryType(uint typeFilter, MemoryPropertyFlags properties, out uint index)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProperties);

        for (var i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << i)) == 0)
            {
                continue;
            }

            if ((memProperties.GetMemoryType((int)i).PropertyFlags & properties) == properties)
            {
                index = (uint)i;
                return true;
            }
        }

        index = 0;
        return false;
    }

    private static Format ToVkFormat(UiTextureFormat format) => format switch
    {
        UiTextureFormat.Rgba8Unorm => Format.R8G8B8A8Unorm,
        UiTextureFormat.Rgba8Srgb => Format.R8G8B8A8Srgb,
        _ => throw new InvalidOperationException($"Unsupported texture format: {format}.")
    };

    private unsafe void CreateMsaaColorImage()
    {
        if (_msaaSampleCount == SampleCountFlags.Count1Bit)
        {
            _msaaColorImage = default;
            _msaaColorMemory = default;
            _msaaColorImageView = default;
            return;
        }

        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(_swapchainExtent.Width, _swapchainExtent.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = _swapchainFormat,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.TransientAttachmentBit | ImageUsageFlags.ColorAttachmentBit,
            Samples = _msaaSampleCount,
            SharingMode = SharingMode.Exclusive,
        };

        fixed (Image* img = &_msaaColorImage)
        {
            Check(_vk.CreateImage(_device, &imageInfo, null, img));
        }

        _vk.GetImageMemoryRequirements(_device, _msaaColorImage, out var memRequirements);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit),
        };

        fixed (DeviceMemory* mem = &_msaaColorMemory)
        {
            Check(_vk.AllocateMemory(_device, &allocInfo, null, mem));
        }

        Check(_vk.BindImageMemory(_device, _msaaColorImage, _msaaColorMemory, 0));

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _msaaColorImage,
            ViewType = ImageViewType.Type2D,
            Format = _swapchainFormat,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        fixed (ImageView* view = &_msaaColorImageView)
        {
            Check(_vk.CreateImageView(_device, &viewInfo, null, view));
        }
    }

    private void DestroyMsaaColorImage()
    {
        if (_msaaColorImageView.Handle is not 0)
        {
            _vk.DestroyImageView(_device, _msaaColorImageView, null);
            _msaaColorImageView = default;
        }

        if (_msaaColorImage.Handle is not 0)
        {
            _vk.DestroyImage(_device, _msaaColorImage, null);
            _msaaColorImage = default;
        }

        if (_msaaColorMemory.Handle is not 0)
        {
            _vk.FreeMemory(_device, _msaaColorMemory, null);
            _msaaColorMemory = default;
        }
    }

    private readonly List<Image> _pendingTextureShaderReadTransitions = new();
    private readonly HashSet<ulong> _pendingTextureShaderReadTransitionImages = new();

    private int _profileImageTransitionCount;
    private int _profileImageTransitionToTransferDstCount;
    private int _profileImageTransitionToShaderReadCount;
    private int _profileImageTransitionPresentCount;
    private int _profileImageTransitionColorAttachmentCount;
    private int _profileImageTransitionTransferStageCompatibleCount;
    private int _profileImageTransitionGraphicsStageRequiredCount;
    private long _profileImageTransitionTicks;

    private readonly record struct ImageLayoutTransitionBarrier(
        AccessFlags SourceAccess,
        AccessFlags DestinationAccess,
        PipelineStageFlags SourceStage,
        PipelineStageFlags DestinationStage,
        bool TransferQueueStageCompatible);

    private void ResetImageTransitionProfileCounters()
    {
        _profileImageTransitionCount = 0;
        _profileImageTransitionToTransferDstCount = 0;
        _profileImageTransitionToShaderReadCount = 0;
        _profileImageTransitionPresentCount = 0;
        _profileImageTransitionColorAttachmentCount = 0;
        _profileImageTransitionTransferStageCompatibleCount = 0;
        _profileImageTransitionGraphicsStageRequiredCount = 0;
        _profileImageTransitionTicks = 0L;
    }

    private ImageLayout PrepareTextureUploadTransferLayout(Image image, ImageLayout initialLayout)
    {
        if (!UsesDedicatedTransferUploadQueue())
        {
            return initialLayout;
        }

        if (IsPendingTextureShaderReadTransition(image))
        {
            return ImageLayout.TransferDstOptimal;
        }

        if (initialLayout != ImageLayout.ShaderReadOnlyOptimal)
        {
            return initialLayout;
        }

        var prepareCommandBuffer = BeginTextureUploadPrepareCommands();
        TransitionImageLayout(
            prepareCommandBuffer,
            image,
            ImageLayout.ShaderReadOnlyOptimal,
            ImageLayout.TransferDstOptimal);
        return ImageLayout.TransferDstOptimal;
    }

    private void CompleteTextureUploadTransferLayout(CommandBuffer commandBuffer, Image image)
    {
        if (!UsesDedicatedTransferUploadQueue())
        {
            TransitionImageLayout(commandBuffer, image, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);
            return;
        }

        QueuePendingTextureShaderReadTransition(image);
    }

    private void RecordPendingTextureShaderReadTransitions(CommandBuffer commandBuffer)
    {
        if (_pendingTextureShaderReadTransitions.Count is 0)
        {
            return;
        }

        for (var i = 0; i < _pendingTextureShaderReadTransitions.Count; i++)
        {
            TransitionImageLayout(
                commandBuffer,
                _pendingTextureShaderReadTransitions[i],
                ImageLayout.TransferDstOptimal,
                ImageLayout.ShaderReadOnlyOptimal);
        }

        _pendingTextureShaderReadTransitions.Clear();
        _pendingTextureShaderReadTransitionImages.Clear();
    }

    private void QueuePendingTextureShaderReadTransition(Image image)
    {
        if (_pendingTextureShaderReadTransitionImages.Add(image.Handle))
        {
            _pendingTextureShaderReadTransitions.Add(image);
        }
    }

    private bool IsPendingTextureShaderReadTransition(Image image)
    {
        return _pendingTextureShaderReadTransitionImages.Contains(image.Handle);
    }

    private void ForgetPendingTextureShaderReadTransition(Image image)
    {
        if (!_pendingTextureShaderReadTransitionImages.Remove(image.Handle))
        {
            return;
        }

        for (var i = 0; i < _pendingTextureShaderReadTransitions.Count; i++)
        {
            if (_pendingTextureShaderReadTransitions[i].Handle != image.Handle)
            {
                continue;
            }

            _pendingTextureShaderReadTransitions.RemoveAt(i);
            return;
        }
    }

    private void TransitionImageLayout(CommandBuffer commandBuffer, Image image, ImageLayout oldLayout, ImageLayout newLayout)
    {
        var transitionStart = BeginFrameProfileTiming(_profilingEnabled);
        var transition = ResolveImageLayoutTransitionBarrier(oldLayout, newLayout);
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcAccessMask = transition.SourceAccess,
            DstAccessMask = transition.DestinationAccess,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        _vk.CmdPipelineBarrier(
            commandBuffer,
            transition.SourceStage,
            transition.DestinationStage,
            0,
            0,
            null,
            0,
            null,
            1,
            &barrier
        );

        RecordImageLayoutTransitionProfile(
            oldLayout,
            newLayout,
            transition.TransferQueueStageCompatible,
            EndFrameProfileTiming(_profilingEnabled, transitionStart));
    }

    private void RecordImageLayoutTransitionProfile(
        ImageLayout oldLayout,
        ImageLayout newLayout,
        bool transferQueueStageCompatible,
        long transitionTicks)
    {
        if (!_profilingEnabled)
        {
            return;
        }

        _profileImageTransitionCount++;
        _profileImageTransitionTicks += transitionTicks;

        if (newLayout == ImageLayout.TransferDstOptimal)
        {
            _profileImageTransitionToTransferDstCount++;
        }

        if (newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            _profileImageTransitionToShaderReadCount++;
        }

        if (oldLayout == ImageLayout.PresentSrcKhr || newLayout == ImageLayout.PresentSrcKhr)
        {
            _profileImageTransitionPresentCount++;
        }

        if (oldLayout == ImageLayout.ColorAttachmentOptimal || newLayout == ImageLayout.ColorAttachmentOptimal)
        {
            _profileImageTransitionColorAttachmentCount++;
        }

        if (transferQueueStageCompatible)
        {
            _profileImageTransitionTransferStageCompatibleCount++;
        }
        else
        {
            _profileImageTransitionGraphicsStageRequiredCount++;
        }
    }

    private static ImageLayoutTransitionBarrier ResolveImageLayoutTransitionBarrier(
        ImageLayout oldLayout,
        ImageLayout newLayout)
    {
        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                0,
                AccessFlags.TransferWriteBit,
                PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.TransferBit);
        }

        if (oldLayout == ImageLayout.ShaderReadOnlyOptimal && newLayout == ImageLayout.TransferDstOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.ShaderReadBit,
                AccessFlags.TransferWriteBit,
                PipelineStageFlags.FragmentShaderBit,
                PipelineStageFlags.TransferBit);
        }

        if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.TransferWriteBit,
                AccessFlags.ShaderReadBit,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.FragmentShaderBit);
        }

        if (oldLayout == ImageLayout.PresentSrcKhr && newLayout == ImageLayout.TransferSrcOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.MemoryReadBit,
                AccessFlags.TransferReadBit,
                PipelineStageFlags.BottomOfPipeBit,
                PipelineStageFlags.TransferBit);
        }

        if (oldLayout == ImageLayout.TransferSrcOptimal && newLayout == ImageLayout.PresentSrcKhr)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.TransferReadBit,
                0,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.BottomOfPipeBit);
        }

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.ColorAttachmentOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                0,
                AccessFlags.ColorAttachmentWriteBit,
                PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.ColorAttachmentOutputBit);
        }

        if (oldLayout == ImageLayout.ColorAttachmentOptimal && newLayout == ImageLayout.TransferSrcOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.ColorAttachmentWriteBit,
                AccessFlags.TransferReadBit,
                PipelineStageFlags.ColorAttachmentOutputBit,
                PipelineStageFlags.TransferBit);
        }

        if (oldLayout == ImageLayout.TransferSrcOptimal && newLayout == ImageLayout.ColorAttachmentOptimal)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.TransferReadBit,
                AccessFlags.ColorAttachmentWriteBit,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.ColorAttachmentOutputBit);
        }

        if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.PresentSrcKhr)
        {
            return CreateImageLayoutTransitionBarrier(
                AccessFlags.TransferWriteBit,
                0,
                PipelineStageFlags.TransferBit,
                PipelineStageFlags.BottomOfPipeBit);
        }

        throw new InvalidOperationException("Unsupported image layout transition.");
    }

    private static ImageLayoutTransitionBarrier CreateImageLayoutTransitionBarrier(
        AccessFlags sourceAccess,
        AccessFlags destinationAccess,
        PipelineStageFlags sourceStage,
        PipelineStageFlags destinationStage)
    {
        return new ImageLayoutTransitionBarrier(
            sourceAccess,
            destinationAccess,
            sourceStage,
            destinationStage,
            IsTransferQueueStageCompatible(sourceStage)
                && IsTransferQueueStageCompatible(destinationStage));
    }

    private static bool IsTransferQueueStageCompatible(PipelineStageFlags stage)
    {
        const PipelineStageFlags transferQueueStages =
            PipelineStageFlags.TopOfPipeBit
            | PipelineStageFlags.TransferBit
            | PipelineStageFlags.BottomOfPipeBit;

        return (stage & ~transferQueueStages) == 0;
    }
}

