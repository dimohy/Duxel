using Duxel.Core;

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
            BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out renderBuffers.VertexBuffer,
            out renderBuffers.VertexMemory);

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
            BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out renderBuffers.PrimitiveBuffer,
            out renderBuffers.PrimitiveMemory);

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
}
