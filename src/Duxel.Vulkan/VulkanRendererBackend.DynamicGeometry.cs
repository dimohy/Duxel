using Duxel.Core;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private const int DefaultVertexBufferCapacity = 5_000;
    private const int DefaultIndexBufferCapacity = 10_000;
    private const int DefaultPrimitiveInstanceBufferCapacity = 5_000;
    private const int VertexBufferGrowthPadding = 5_000;
    private const int IndexBufferGrowthPadding = 10_000;
    private const int PrimitiveInstanceBufferGrowthPadding = 5_000;

    private void EnsureVertexBufferCapacity(int frame, int vertexCount)
    {
        var frameData = _frames[frame];
        var renderBuffers = frameData.RenderBuffers;
        var requiredSize = (nuint)(vertexCount * sizeof(UiVertex));
        if (renderBuffers.VertexBuffer.Handle is not 0 && renderBuffers.VertexSize >= requiredSize)
        {
            return;
        }

        if (renderBuffers.VertexBuffer.Handle is not 0)
        {
            if (renderBuffers.VertexMappedPtr is not null)
            {
                _vk.UnmapMemory(_device, renderBuffers.VertexMemory);
                renderBuffers.VertexMappedPtr = null;
            }

            QueueBufferDestroy(renderBuffers.VertexBuffer, renderBuffers.VertexMemory);
            renderBuffers.VertexBuffer = default;
            renderBuffers.VertexMemory = default;
            renderBuffers.VertexAddress = 0;
        }

        var newSize = requiredSize;
        if (renderBuffers.VertexSize == 0)
        {
            newSize = Math.Max(newSize, (nuint)(DefaultVertexBufferCapacity * sizeof(UiVertex)));
        }
        else
        {
            var padded = requiredSize + (nuint)(VertexBufferGrowthPadding * sizeof(UiVertex));
            newSize = Math.Max(newSize, padded);
        }

        CreateBuffer(
            newSize,
            BufferUsageFlags.ShaderDeviceAddressBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out renderBuffers.VertexBuffer,
            out renderBuffers.VertexMemory,
            preferredProperties: MemoryPropertyFlags.DeviceLocalBit | MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        renderBuffers.VertexAddress = GetBufferDeviceAddress(renderBuffers.VertexBuffer);

        void* mapped;
        Check(_vk.MapMemory(_device, renderBuffers.VertexMemory, 0, newSize, 0, &mapped));
        renderBuffers.VertexMappedPtr = mapped;

        renderBuffers.VertexSize = newSize;
        frameData.RenderBuffers = renderBuffers;
        _frames[frame] = frameData;
    }

    private void EnsureIndexBufferCapacity(int frame, int indexCount)
    {
        var frameData = _frames[frame];
        var renderBuffers = frameData.RenderBuffers;
        var requiredSize = (nuint)(indexCount * sizeof(uint));
        if (renderBuffers.IndexBuffer.Handle is not 0 && renderBuffers.IndexSize >= requiredSize)
        {
            return;
        }

        if (renderBuffers.IndexBuffer.Handle is not 0)
        {
            if (renderBuffers.IndexMappedPtr is not null)
            {
                _vk.UnmapMemory(_device, renderBuffers.IndexMemory);
                renderBuffers.IndexMappedPtr = null;
            }

            QueueBufferDestroy(renderBuffers.IndexBuffer, renderBuffers.IndexMemory);
            renderBuffers.IndexBuffer = default;
            renderBuffers.IndexMemory = default;
        }

        var newSize = requiredSize;
        if (renderBuffers.IndexSize == 0)
        {
            newSize = Math.Max(newSize, (nuint)(DefaultIndexBufferCapacity * sizeof(uint)));
        }
        else
        {
            var padded = requiredSize + (nuint)(IndexBufferGrowthPadding * sizeof(uint));
            newSize = Math.Max(newSize, padded);
        }

        CreateBuffer(
            newSize,
            BufferUsageFlags.IndexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out renderBuffers.IndexBuffer,
            out renderBuffers.IndexMemory);

        void* mapped;
        Check(_vk.MapMemory(_device, renderBuffers.IndexMemory, 0, newSize, 0, &mapped));
        renderBuffers.IndexMappedPtr = mapped;

        renderBuffers.IndexSize = newSize;
        frameData.RenderBuffers = renderBuffers;
        _frames[frame] = frameData;
    }

    private void EnsurePrimitiveBufferCapacity(int frame, int primitiveCount)
    {
        if (primitiveCount is 0)
        {
            return;
        }

        var frameData = _frames[frame];
        var renderBuffers = frameData.RenderBuffers;
        var requiredSize = (nuint)(primitiveCount * sizeof(PrimitiveInstance));
        if (renderBuffers.PrimitiveBuffer.Handle is not 0 && renderBuffers.PrimitiveSize >= requiredSize)
        {
            return;
        }

        if (renderBuffers.PrimitiveBuffer.Handle is not 0)
        {
            if (renderBuffers.PrimitiveMappedPtr is not null)
            {
                _vk.UnmapMemory(_device, renderBuffers.PrimitiveMemory);
                renderBuffers.PrimitiveMappedPtr = null;
            }

            QueueBufferDestroy(renderBuffers.PrimitiveBuffer, renderBuffers.PrimitiveMemory);
            renderBuffers.PrimitiveBuffer = default;
            renderBuffers.PrimitiveMemory = default;
            renderBuffers.PrimitiveAddress = 0;
        }

        var newSize = requiredSize;
        if (renderBuffers.PrimitiveSize is 0)
        {
            newSize = Math.Max(newSize, (nuint)(DefaultPrimitiveInstanceBufferCapacity * sizeof(PrimitiveInstance)));
        }
        else
        {
            var padded = requiredSize + (nuint)(PrimitiveInstanceBufferGrowthPadding * sizeof(PrimitiveInstance));
            newSize = Math.Max(newSize, padded);
        }

        CreateBuffer(
            newSize,
            BufferUsageFlags.ShaderDeviceAddressBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out renderBuffers.PrimitiveBuffer,
            out renderBuffers.PrimitiveMemory,
            preferredProperties: MemoryPropertyFlags.DeviceLocalBit | MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        renderBuffers.PrimitiveAddress = GetBufferDeviceAddress(renderBuffers.PrimitiveBuffer);

        void* mapped;
        Check(_vk.MapMemory(_device, renderBuffers.PrimitiveMemory, 0, newSize, 0, &mapped));
        renderBuffers.PrimitiveMappedPtr = mapped;
        renderBuffers.PrimitiveSize = newSize;
        frameData.RenderBuffers = renderBuffers;
        _frames[frame] = frameData;
    }

    private void DestroyGeometryBuffers()
    {
        DestroyStaticGeometryBuffers();

        for (var i = 0; i < _frames.Length; i++)
        {
            var frame = _frames[i];
            if (frame is null)
            {
                continue;
            }

            var renderBuffers = frame.RenderBuffers;

            DestroyFrameVertexBuffer(ref renderBuffers);
            DestroyFrameIndexBuffer(ref renderBuffers);
            DestroyFramePrimitiveBuffer(ref renderBuffers);

            frame.RenderBuffers = renderBuffers;
            _frames[i] = frame;
        }
    }

    private void DestroyFrameVertexBuffer(ref FrameRenderBuffers renderBuffers)
    {
        if (renderBuffers.VertexBuffer.Handle is 0)
        {
            return;
        }

        if (renderBuffers.VertexMappedPtr is not null)
        {
            _vk.UnmapMemory(_device, renderBuffers.VertexMemory);
            renderBuffers.VertexMappedPtr = null;
        }

        DestroyBufferResource(new BufferResource(renderBuffers.VertexBuffer, renderBuffers.VertexMemory));
        renderBuffers.VertexBuffer = default;
        renderBuffers.VertexMemory = default;
        renderBuffers.VertexSize = 0;
    }

    private void DestroyFrameIndexBuffer(ref FrameRenderBuffers renderBuffers)
    {
        if (renderBuffers.IndexBuffer.Handle is 0)
        {
            return;
        }

        if (renderBuffers.IndexMappedPtr is not null)
        {
            _vk.UnmapMemory(_device, renderBuffers.IndexMemory);
            renderBuffers.IndexMappedPtr = null;
        }

        DestroyBufferResource(new BufferResource(renderBuffers.IndexBuffer, renderBuffers.IndexMemory));
        renderBuffers.IndexBuffer = default;
        renderBuffers.IndexMemory = default;
        renderBuffers.IndexSize = 0;
    }

    private void DestroyFramePrimitiveBuffer(ref FrameRenderBuffers renderBuffers)
    {
        if (renderBuffers.PrimitiveBuffer.Handle is 0)
        {
            return;
        }

        if (renderBuffers.PrimitiveMappedPtr is not null)
        {
            _vk.UnmapMemory(_device, renderBuffers.PrimitiveMemory);
            renderBuffers.PrimitiveMappedPtr = null;
        }

        DestroyBufferResource(new BufferResource(renderBuffers.PrimitiveBuffer, renderBuffers.PrimitiveMemory));
        renderBuffers.PrimitiveBuffer = default;
        renderBuffers.PrimitiveMemory = default;
        renderBuffers.PrimitiveSize = 0;
    }

    private readonly record struct FrameGeometryBuffers(
        VkBuffer VertexBuffer,
        ulong VertexAddress,
        VkBuffer IndexBuffer,
        VkBuffer PrimitiveBuffer,
        ulong PrimitiveAddress);

    private FrameGeometryBuffers PrepareFrameGeometryForRecording(
        UiDrawData drawData,
        int frameSlot,
        FrameResources frameData,
        bool profileEnabled,
        out long uploadTicks)
    {
        var uploadStart = BeginFrameProfileTiming(profileEnabled);
        var geometryCounts = PrepareStaticGeometryForFrame(drawData, _frameStaticBindings);

        EnsureDynamicGeometryCapacity(frameSlot, in geometryCounts);
        UploadDynamicGeometry(frameSlot, drawData, _frameStaticBindings);
        PruneStaticGeometryCachesIfNeeded(_frameIndex);

        uploadTicks = EndFrameProfileTiming(profileEnabled, uploadStart);

        var renderBuffers = frameData.RenderBuffers;
        return new FrameGeometryBuffers(
            renderBuffers.VertexBuffer,
            renderBuffers.VertexAddress,
            renderBuffers.IndexBuffer,
            renderBuffers.PrimitiveBuffer,
            renderBuffers.PrimitiveAddress);
    }

    private void EnsureDynamicGeometryCapacity(int frameSlot, in FrameGeometryCounts geometryCounts)
    {
        if (geometryCounts.DynamicVertexCount > 0 && geometryCounts.DynamicIndexCount > 0)
        {
            EnsureVertexBufferCapacity(frameSlot, geometryCounts.DynamicVertexCount);
            EnsureIndexBufferCapacity(frameSlot, geometryCounts.DynamicIndexCount);
        }

        var dynamicPrimitiveSentinelCount = 0;
        EnsurePrimitiveBufferCapacity(
            frameSlot,
            geometryCounts.RectPrimitiveCount + geometryCounts.CirclePrimitiveCount + dynamicPrimitiveSentinelCount);
    }

    private readonly List<int> _dynamicDrawListIndices = new();

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void UploadDynamicGeometry(int frame, UiDrawData drawData, Dictionary<int, StaticGeometryBuffer> staticBindings)
    {
        var frameData = _frames[frame];
        var renderBuffers = frameData.RenderBuffers;

        var vertexDst = (byte*)renderBuffers.VertexMappedPtr;
        var indexDst = (byte*)renderBuffers.IndexMappedPtr;
        var primitiveDst = (PrimitiveInstance*)renderBuffers.PrimitiveMappedPtr;

        for (var dynamicListIndex = 0; dynamicListIndex < _dynamicDrawListIndices.Count; dynamicListIndex++)
        {
            var drawList = drawData.DrawLists[_dynamicDrawListIndices[dynamicListIndex]];
            var vertices = drawList.Vertices.AsSpan();
            if (!vertices.IsEmpty)
            {
                var vertexOut = (UiVertex*)vertexDst;
                for (var i = 0; i < vertices.Length; i++)
                {
                    ref readonly var src = ref vertices[i];
                    vertexOut[i] = new UiVertex
                    {
                        PositionX = src.Position.X,
                        PositionY = src.Position.Y,
                        UVx = src.UV.X,
                        UVy = src.UV.Y,
                        Color = src.Color.Rgba,
                    };
                }

                vertexDst += (uint)(vertices.Length * sizeof(UiVertex));
            }

            var indices = drawList.Indices.AsSpan();
            if (!indices.IsEmpty)
            {
                fixed (uint* indexSrc = indices)
                {
                    var indexBytes = (uint)(indices.Length * sizeof(uint));
                    Unsafe.CopyBlockUnaligned(indexDst, indexSrc, indexBytes);
                    indexDst += indexBytes;
                }
            }
        }

        for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
        {
            if (staticBindings.ContainsKey(listIndex))
            {
                continue;
            }

            var drawList = drawData.DrawLists[listIndex];
            if (drawList.RectFilledPrimitives is not null)
            {
                var primitives = drawList.RectFilledPrimitives.AsSpan();
                for (var i = 0; i < primitives.Length; i++)
                {
                    primitiveDst[i] = CreateRectPrimitiveInstance(in primitives[i]);
                }

                primitiveDst += primitives.Length;
            }

            if (drawList.CircleFilledPrimitives is not null)
            {
                var primitives = drawList.CircleFilledPrimitives.AsSpan();
                for (var i = 0; i < primitives.Length; i++)
                {
                    primitiveDst[i] = CreateCirclePrimitiveInstance(in primitives[i]);
                }

                primitiveDst += primitives.Length;
            }
        }
    }
}

