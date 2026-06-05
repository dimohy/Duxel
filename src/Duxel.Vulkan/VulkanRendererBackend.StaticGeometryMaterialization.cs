using System;
using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private StaticGeometryBuffer CreateStaticGeometryBuffer(
        string staticTag,
        ulong contentHash,
        ReadOnlySpan<UiDrawVertex> vertices,
        ReadOnlySpan<uint> indices,
        ReadOnlySpan<UiRectFilledPrimitive> rects,
        ReadOnlySpan<UiCircleFilledPrimitive> circles,
        in StaticGeometryShape shape)
    {
        var vertexBuffer = default(VkBuffer);
        var vertexMemory = default(DeviceMemory);
        if (shape.CombinedVertexCount > 0)
        {
            CreateBuffer(
                (nuint)(shape.CombinedVertexCount * sizeof(UiVertex)),
                BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
                MemoryPropertyFlags.DeviceLocalBit,
                out vertexBuffer,
                out vertexMemory
            );
        }

        var indexBuffer = default(VkBuffer);
        var indexMemory = default(DeviceMemory);
        if (shape.CombinedIndexCount > 0)
        {
            CreateBuffer(
                (nuint)(shape.CombinedIndexCount * sizeof(uint)),
                BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
                MemoryPropertyFlags.DeviceLocalBit,
                out indexBuffer,
                out indexMemory
            );
        }

        var primitiveBuffer = default(VkBuffer);
        var primitiveMemory = default(DeviceMemory);
        if (shape.PrimitiveCount > 0)
        {
            CreateBuffer(
                (nuint)(shape.PrimitiveCount * sizeof(PrimitiveInstance)),
                BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
                MemoryPropertyFlags.DeviceLocalBit,
                out primitiveBuffer,
                out primitiveMemory
            );
        }

        var created = new StaticGeometryBuffer(
            staticTag,
            contentHash,
            vertexBuffer,
            vertexMemory,
            shape.VertexCount,
            indexBuffer,
            indexMemory,
            shape.IndexCount,
            primitiveBuffer,
            primitiveMemory,
            shape.PrimitiveCount,
            shape.PrimitiveInstanceBaseCount,
            shape.RectPrimitiveCount,
            shape.CirclePrimitiveCount,
            shape.HasExpandedPrimitiveGeometry,
            shape.PrimitiveTriangleLayout.RectExpandedIndexBase,
            shape.PrimitiveTriangleLayout.CircleExpandedIndexOffsets
        );

        UploadStaticGeometryBufferContent(in created, vertices, indices, rects, circles, in shape);
        return created;
    }

    private StaticGeometryBuffer ReuploadStaticGeometryBufferContent(
        in StaticGeometryBuffer target,
        ulong contentHash,
        ReadOnlySpan<UiDrawVertex> vertices,
        ReadOnlySpan<uint> indices,
        ReadOnlySpan<UiRectFilledPrimitive> rects,
        ReadOnlySpan<UiCircleFilledPrimitive> circles,
        in StaticGeometryShape shape)
    {
        UploadStaticGeometryBufferContent(in target, vertices, indices, rects, circles, in shape);
        return target with
        {
            ContentHash = contentHash,
            RectExpandedIndexBase = shape.PrimitiveTriangleLayout.RectExpandedIndexBase,
            CircleExpandedIndexOffsets = shape.PrimitiveTriangleLayout.CircleExpandedIndexOffsets,
        };
    }

    private void UploadStaticGeometryBufferContent(
        in StaticGeometryBuffer target,
        ReadOnlySpan<UiDrawVertex> vertices,
        ReadOnlySpan<uint> indices,
        ReadOnlySpan<UiRectFilledPrimitive> rects,
        ReadOnlySpan<UiCircleFilledPrimitive> circles,
        in StaticGeometryShape shape)
    {
        if (shape.CombinedVertexCount > 0)
        {
            UploadStaticVertexBufferData(
                target.VertexBuffer,
                vertices,
                rects,
                circles,
                shape.HasExpandedPrimitiveGeometry);
        }

        if (shape.CombinedIndexCount > 0)
        {
            UploadStaticIndexBufferData(
                target.IndexBuffer,
                indices,
                shape.VertexCount,
                rects,
                circles,
                shape.PrimitiveTriangleLayout,
                shape.HasExpandedPrimitiveGeometry);
        }

        if (shape.PrimitiveCount is 0)
        {
            return;
        }

        var primitiveBufferRects = shape.HasExpandedPrimitiveGeometry
            ? ReadOnlySpan<UiRectFilledPrimitive>.Empty
            : rects;
        var primitiveBufferCircles = shape.HasExpandedPrimitiveGeometry
            ? ReadOnlySpan<UiCircleFilledPrimitive>.Empty
            : circles;
        UploadStaticPrimitiveBufferData(
            target.PrimitiveBuffer,
            shape.PrimitiveInstanceBaseCount > 0,
            primitiveBufferRects,
            primitiveBufferCircles);
    }
}
