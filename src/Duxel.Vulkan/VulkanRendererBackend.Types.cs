using System.Collections.Generic;
using System.Runtime.InteropServices;
using VkBuffer = Duxel.Vulkan.Buffer;
using VulkanSemaphore = Duxel.Vulkan.Semaphore;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private readonly record struct StaticGeometryBuffer(
        string Tag,
        VkBuffer VertexBuffer,
        DeviceMemory VertexMemory,
        int VertexCount,
        VkBuffer IndexBuffer,
        DeviceMemory IndexMemory,
        int IndexCount);

    private readonly struct TextureResource
    {
        public readonly Image Image;
        public readonly DeviceMemory Memory;
        public readonly ImageView View;
        public readonly DescriptorSet DescriptorSet;
        public readonly int Width;
        public readonly int Height;
        public readonly Format Format;

        public TextureResource(
            Image image,
            DeviceMemory memory,
            ImageView view,
            DescriptorSet descriptorSet,
            int width,
            int height,
            Format format
        )
        {
            Image = image;
            Memory = memory;
            View = view;
            DescriptorSet = descriptorSet;
            Width = width;
            Height = height;
            Format = format;
        }
    }

    private sealed class FrameResources
    {
        public FrameRenderBuffers RenderBuffers = new();
        public CommandPool CommandPool;
        public CommandBuffer CommandBuffer;
        public Fence InFlight;
        public readonly List<PendingTextureDestroy> PendingTextureDestroys = new();
        public readonly List<PendingBufferDestroy> PendingBufferDestroys = new();
    }

    private readonly record struct FrameSemaphores(VulkanSemaphore ImageAvailable, VulkanSemaphore RenderFinished);

    private sealed class FrameRenderBuffers
    {
        public VkBuffer VertexBuffer;
        public DeviceMemory VertexMemory;
        public nuint VertexSize;
        public void* VertexMappedPtr;
        public VkBuffer IndexBuffer;
        public DeviceMemory IndexMemory;
        public nuint IndexSize;
        public void* IndexMappedPtr;
        public VkBuffer RectPrimitiveBuffer;
        public DeviceMemory RectPrimitiveMemory;
        public nuint RectPrimitiveSize;
        public void* RectPrimitiveMappedPtr;
        public VkBuffer CirclePrimitiveBuffer;
        public DeviceMemory CirclePrimitiveMemory;
        public nuint CirclePrimitiveSize;
        public void* CirclePrimitiveMappedPtr;
    }

    private readonly record struct BufferResource(VkBuffer Buffer, DeviceMemory Memory);
    private readonly record struct PendingBufferDestroy(BufferResource Resource, int DestroyFrame);
    private readonly record struct PendingTextureDestroy(TextureResource Resource, int DestroyFrame);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct UiVertex
    {
        public float PositionX;
        public float PositionY;
        public float UVx;
        public float UVy;
        public uint Color;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct RectPrimitiveInstance
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public uint Color;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct CirclePrimitiveInstance
    {
        public float CenterX;
        public float CenterY;
        public float Radius;
        public uint Color;
        public uint Segments;
    }
}
