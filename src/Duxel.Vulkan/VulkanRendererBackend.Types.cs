using System.Collections.Generic;
using System.Runtime.InteropServices;
using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;
using VulkanSemaphore = Duxel.Vulkan.Semaphore;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private sealed record class StaticGeometryBuffer(
        string Tag,
        ulong ContentHash,
        VkBuffer VertexBuffer,
        DeviceMemory VertexMemory,
        ulong VertexAddress,
        int VertexCount,
        VkBuffer IndexBuffer,
        DeviceMemory IndexMemory,
        int IndexCount,
        VkBuffer PrimitiveBuffer,
        DeviceMemory PrimitiveMemory,
        ulong PrimitiveAddress,
        int PrimitiveCount,
        int PrimitiveInstanceBaseCount,
        int RectPrimitiveCount,
        int CirclePrimitiveCount,
        bool HasExpandedPrimitiveGeometry,
        int RectExpandedIndexBase,
        int[]? CircleExpandedIndexOffsets)
    {
        public static StaticGeometryBuffer Empty { get; } = new(
            string.Empty, 0, default, default, 0, 0, default, default, 0,
            default, default, 0, 0, 0, 0, 0, false, 0, null);
        public ulong VertexDataOffset { get; init; }
        public StaticMemorySlice StaticMemory { get; init; }
        public IndexType StaticIndexType { get; init; } = IndexType.Uint32;
        public ulong IndexTopologyHash { get; init; }
        public ulong CommandTopologyHash { get; init; }
        public UiPooledList<UiDrawCommand>? TopologyCommands { get; init; }
    }

    private readonly record struct StaticPrimitiveTriangleLayout(
        int VertexCount,
        int IndexCount,
        int RectExpandedIndexBase,
        int[]? CircleExpandedIndexOffsets);

    private readonly record struct RetiredStaticGeometryBuffer(
        StaticGeometryBuffer Buffer,
        int AvailableFrame,
        long ReuseUntilTimestamp,
        ulong ExtendedReuseBytes);

    private readonly struct TextureResource
    {
        public readonly Image Image;
        public readonly DeviceMemory Memory;
        public readonly ImageView View;
        public readonly uint SlotIndex;
        public readonly int Width;
        public readonly int Height;
        public readonly Format Format;
        public readonly bool ReusableText;
        public readonly ulong AllocationBytes;
        public readonly StaticMemorySlice TextMemory;

        public TextureResource(
            Image image,
            DeviceMemory memory,
            ImageView view,
            uint slotIndex,
            int width,
            int height,
            Format format,
            ulong allocationBytes,
            bool reusableText = false,
            StaticMemorySlice textMemory = default
        )
        {
            Image = image;
            Memory = memory;
            View = view;
            SlotIndex = slotIndex;
            Width = width;
            Height = height;
            Format = format;
            ReusableText = reusableText;
            AllocationBytes = allocationBytes;
            TextMemory = textMemory;
        }
    }

    private sealed class FrameResources
    {
        public FrameRenderBuffers RenderBuffers = new();
        public CommandPool CommandPool;
        public CommandBuffer CommandBuffer;
        public Fence InFlight;
        public QueryPool TimestampQueryPool;
        public bool TimestampQueryIssued;
        public VectorProfileCounts ProfiledVectorCounts;
        public readonly List<PendingTextureDestroy> PendingTextureDestroys = new();
        public readonly List<PendingBufferDestroy> PendingBufferDestroys = new();
        public readonly List<RetiredRenderTargets> PendingRenderTargets=[];
        public List<ProjectedGlyphPage>? ProjectedGlyphBatchPages;
        public ulong SubmittedSequence;
    }

    private struct VectorProfileCounts
    {
        public uint Blurs, Paths, Copies;
    }

    private readonly record struct FrameSemaphores(VulkanSemaphore ImageAvailable, VulkanSemaphore RenderFinished);

    private sealed class FrameRenderBuffers
    {
        public VkBuffer VertexBuffer;
        public DeviceMemory VertexMemory;
        public ulong VertexAddress;
        public nuint VertexSize;
        public void* VertexMappedPtr;
        public VkBuffer IndexBuffer;
        public DeviceMemory IndexMemory;
        public nuint IndexSize;
        public void* IndexMappedPtr;
        public VkBuffer PrimitiveBuffer;
        public DeviceMemory PrimitiveMemory;
        public ulong PrimitiveAddress;
        public nuint PrimitiveSize;
        public void* PrimitiveMappedPtr;
    }

    private readonly record struct BufferResource(VkBuffer Buffer, DeviceMemory Memory);
    private readonly record struct PendingBufferDestroy(BufferResource Resource, int DestroyFrame, StaticMemorySlice StaticMemory);
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
    private struct PrimitiveInstance
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public float Radius;
        public float BorderThickness;
        public uint FillColor;
        public uint BorderColor;
    }
}
