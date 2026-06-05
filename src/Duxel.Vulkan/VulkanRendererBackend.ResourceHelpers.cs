using System;
using Duxel.Core;
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
        Check(_vk.BindBufferMemory(_device, buffer, localMemory, 0));
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
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProperties);

        for (var i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << i)) == 0)
            {
                continue;
            }

            if ((memProperties.GetMemoryType((int)i).PropertyFlags & properties) == properties)
            {
                return (uint)i;
            }
        }

        throw new InvalidOperationException("Failed to find a suitable memory type.");
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
}
