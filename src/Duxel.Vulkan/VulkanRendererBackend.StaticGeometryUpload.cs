using System;
using System.Runtime.CompilerServices;
using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private void UploadStaticVertexBufferData(
        VkBuffer destinationBuffer,
        ReadOnlySpan<UiDrawVertex> vertices,
        ReadOnlySpan<UiRectFilledPrimitive> rectPrimitives,
        ReadOnlySpan<UiCircleFilledPrimitive> circlePrimitives,
        bool includeExpandedPrimitives)
    {
        var expandedVertexCount = includeExpandedPrimitives
            ? CountExpandedPrimitiveVertices(rectPrimitives, circlePrimitives)
            : 0;
        var vertexCount = vertices.Length + expandedVertexCount;
        if (vertexCount is 0)
        {
            return;
        }

        var sourceSize = (nuint)(vertexCount * sizeof(UiVertex));
        var commandBuffer = BeginStagingUpload(sourceSize, out var stagingOffset);
        var vertexDst = (UiVertex*)((byte*)_stagingMappedPtr + stagingOffset);
        var vertexOffset = 0;

        for (var i = 0; i < vertices.Length; i++)
        {
            ref readonly var src = ref vertices[i];
            vertexDst[vertexOffset++] = new UiVertex
            {
                PositionX = src.Position.X,
                PositionY = src.Position.Y,
                UVx = src.UV.X,
                UVy = src.UV.Y,
                Color = src.Color.Rgba,
            };
        }

        if (includeExpandedPrimitives)
        {
            WriteExpandedPrimitiveVertices(vertexDst + vertexOffset, rectPrimitives, circlePrimitives);
        }

        try
        {
            var region = new BufferCopy
            {
                SrcOffset = stagingOffset,
                DstOffset = 0,
                Size = sourceSize,
            };

            RecordUploadBufferCopyProfile();
            _vk.CmdCopyBuffer(commandBuffer, _stagingBuffer, destinationBuffer, 1, &region);
        }
        finally
        {
            EndSingleTimeCommands(commandBuffer);
        }
    }

    private void UploadStaticIndexBufferData(
        VkBuffer destinationBuffer,
        ReadOnlySpan<uint> indices,
        int originalVertexCount,
        ReadOnlySpan<UiRectFilledPrimitive> rectPrimitives,
        ReadOnlySpan<UiCircleFilledPrimitive> circlePrimitives,
        in StaticPrimitiveTriangleLayout primitiveTriangleLayout,
        bool includeExpandedPrimitives)
    {
        var expandedIndexCount = includeExpandedPrimitives ? primitiveTriangleLayout.IndexCount : 0;
        var indexCount = indices.Length + expandedIndexCount;
        if (indexCount is 0)
        {
            return;
        }

        var sourceSize = (nuint)(indexCount * sizeof(uint));
        var commandBuffer = BeginStagingUpload(sourceSize, out var stagingOffset);
        var indexDst = (uint*)((byte*)_stagingMappedPtr + stagingOffset);

        if (!indices.IsEmpty)
        {
            fixed (uint* indexSrc = indices)
            {
                Unsafe.CopyBlockUnaligned(indexDst, indexSrc, (uint)(indices.Length * sizeof(uint)));
            }
        }

        if (includeExpandedPrimitives)
        {
            WriteExpandedPrimitiveIndices(
                indexDst + indices.Length,
                originalVertexCount,
                rectPrimitives,
                circlePrimitives);
        }

        try
        {
            var region = new BufferCopy
            {
                SrcOffset = stagingOffset,
                DstOffset = 0,
                Size = sourceSize,
            };

            RecordUploadBufferCopyProfile();
            _vk.CmdCopyBuffer(commandBuffer, _stagingBuffer, destinationBuffer, 1, &region);
        }
        finally
        {
            EndSingleTimeCommands(commandBuffer);
        }
    }

    private static StaticPrimitiveTriangleLayout CreateStaticPrimitiveTriangleLayout(
        int originalIndexCount,
        ReadOnlySpan<UiRectFilledPrimitive> rectPrimitives,
        ReadOnlySpan<UiCircleFilledPrimitive> circlePrimitives)
    {
        var rectVertexCount = checked(rectPrimitives.Length * 4);
        var rectIndexCount = checked(rectPrimitives.Length * 6);
        var circleVertexCount = 0;
        var circleIndexCount = 0;
        int[]? circleIndexOffsets = null;
        if (!circlePrimitives.IsEmpty)
        {
            circleIndexOffsets = new int[circlePrimitives.Length + 1];
            var indexOffset = checked(originalIndexCount + rectIndexCount);
            for (var i = 0; i < circlePrimitives.Length; i++)
            {
                var segmentCount = Math.Max(3, circlePrimitives[i].Segments);
                circleIndexOffsets[i] = indexOffset;
                circleVertexCount = checked(circleVertexCount + segmentCount + 1);
                var indexCount = checked(segmentCount * 3);
                circleIndexCount = checked(circleIndexCount + indexCount);
                indexOffset = checked(indexOffset + indexCount);
            }

            circleIndexOffsets[^1] = indexOffset;
        }

        return new StaticPrimitiveTriangleLayout(
            checked(rectVertexCount + circleVertexCount),
            checked(rectIndexCount + circleIndexCount),
            originalIndexCount,
            circleIndexOffsets);
    }

    private static int CountExpandedPrimitiveVertices(
        ReadOnlySpan<UiRectFilledPrimitive> rectPrimitives,
        ReadOnlySpan<UiCircleFilledPrimitive> circlePrimitives)
    {
        var count = checked(rectPrimitives.Length * 4);
        for (var i = 0; i < circlePrimitives.Length; i++)
        {
            count = checked(count + Math.Max(3, circlePrimitives[i].Segments) + 1);
        }

        return count;
    }

    private static void WriteExpandedPrimitiveVertices(
        UiVertex* vertexDst,
        ReadOnlySpan<UiRectFilledPrimitive> rectPrimitives,
        ReadOnlySpan<UiCircleFilledPrimitive> circlePrimitives)
    {
        var writeIndex = 0;
        for (var i = 0; i < rectPrimitives.Length; i++)
        {
            var primitive = rectPrimitives[i];
            var rect = primitive.Rect;
            var x0 = rect.X;
            var y0 = rect.Y;
            var x1 = rect.X + rect.Width;
            var y1 = rect.Y + rect.Height;
            var color = primitive.Color.Rgba;
            vertexDst[writeIndex++] = CreateExpandedPrimitiveVertex(x0, y0, color);
            vertexDst[writeIndex++] = CreateExpandedPrimitiveVertex(x1, y0, color);
            vertexDst[writeIndex++] = CreateExpandedPrimitiveVertex(x1, y1, color);
            vertexDst[writeIndex++] = CreateExpandedPrimitiveVertex(x0, y1, color);
        }

        for (var i = 0; i < circlePrimitives.Length; i++)
        {
            var primitive = circlePrimitives[i];
            var segmentCount = Math.Max(3, primitive.Segments);
            var color = primitive.Color.Rgba;
            vertexDst[writeIndex++] = CreateExpandedPrimitiveVertex(primitive.Center.X, primitive.Center.Y, color);
            for (var segment = 0; segment < segmentCount; segment++)
            {
                var angle = MathF.Tau * segment / segmentCount;
                var x = primitive.Center.X + MathF.Cos(angle) * primitive.Radius;
                var y = primitive.Center.Y + MathF.Sin(angle) * primitive.Radius;
                vertexDst[writeIndex++] = CreateExpandedPrimitiveVertex(x, y, color);
            }
        }
    }

    private static void WriteExpandedPrimitiveIndices(
        uint* indexDst,
        int originalVertexCount,
        ReadOnlySpan<UiRectFilledPrimitive> rectPrimitives,
        ReadOnlySpan<UiCircleFilledPrimitive> circlePrimitives)
    {
        var writeIndex = 0;
        var vertexBase = checked((uint)originalVertexCount);
        for (var i = 0; i < rectPrimitives.Length; i++)
        {
            indexDst[writeIndex++] = vertexBase;
            indexDst[writeIndex++] = vertexBase + 1;
            indexDst[writeIndex++] = vertexBase + 2;
            indexDst[writeIndex++] = vertexBase;
            indexDst[writeIndex++] = vertexBase + 2;
            indexDst[writeIndex++] = vertexBase + 3;
            vertexBase += 4;
        }

        for (var i = 0; i < circlePrimitives.Length; i++)
        {
            var segmentCount = Math.Max(3, circlePrimitives[i].Segments);
            var centerIndex = vertexBase;
            var ringIndex = vertexBase + 1;
            for (var segment = 0; segment < segmentCount; segment++)
            {
                indexDst[writeIndex++] = centerIndex;
                indexDst[writeIndex++] = ringIndex + (uint)segment;
                indexDst[writeIndex++] = ringIndex + (uint)((segment + 1) % segmentCount);
            }

            vertexBase += checked((uint)segmentCount + 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UiVertex CreateExpandedPrimitiveVertex(float x, float y, uint color)
    {
        return new UiVertex
        {
            PositionX = x,
            PositionY = y,
            UVx = 0.5f,
            UVy = 0.5f,
            Color = color,
        };
    }

    private void UploadStaticPrimitiveBufferData(
        VkBuffer destinationBuffer,
        bool includeSolidTriangleSentinel,
        ReadOnlySpan<UiRectFilledPrimitive> rectPrimitives,
        ReadOnlySpan<UiCircleFilledPrimitive> circlePrimitives)
    {
        var primitiveCount = rectPrimitives.Length + circlePrimitives.Length;
        var sourcePrimitiveCount = primitiveCount + (includeSolidTriangleSentinel ? StaticPrimitiveBufferSentinelCount : 0);
        if (sourcePrimitiveCount is 0)
        {
            return;
        }

        var sourceSize = (nuint)(sourcePrimitiveCount * sizeof(PrimitiveInstance));
        var commandBuffer = BeginStagingUpload(sourceSize, out var stagingOffset);
        var primitiveDst = (PrimitiveInstance*)((byte*)_stagingMappedPtr + stagingOffset);
        if (includeSolidTriangleSentinel)
        {
            primitiveDst[0] = CreateSolidTriangleModePrimitiveInstance();
            primitiveDst += StaticPrimitiveBufferSentinelCount;
        }

        for (var i = 0; i < rectPrimitives.Length; i++)
        {
            primitiveDst[i] = CreateRectPrimitiveInstance(in rectPrimitives[i]);
        }

        primitiveDst += rectPrimitives.Length;
        for (var i = 0; i < circlePrimitives.Length; i++)
        {
            primitiveDst[i] = CreateCirclePrimitiveInstance(in circlePrimitives[i]);
        }

        try
        {
            var region = new BufferCopy
            {
                SrcOffset = stagingOffset,
                DstOffset = 0,
                Size = sourceSize,
            };

            RecordUploadBufferCopyProfile();
            _vk.CmdCopyBuffer(commandBuffer, _stagingBuffer, destinationBuffer, 1, &region);
        }
        finally
        {
            EndSingleTimeCommands(commandBuffer);
        }
    }

}
