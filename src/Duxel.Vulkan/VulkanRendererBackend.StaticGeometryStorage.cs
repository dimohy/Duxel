using Duxel.Core;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
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
        var vertexAddress = 0ul;
        if (shape.CombinedVertexCount > 0)
        {
            CreateBuffer(
                (nuint)(shape.CombinedVertexCount * sizeof(UiVertex)),
                BufferUsageFlags.TransferDstBit | BufferUsageFlags.ShaderDeviceAddressBit,
                MemoryPropertyFlags.DeviceLocalBit,
                out vertexBuffer,
                out vertexMemory
            );
            vertexAddress = GetBufferDeviceAddress(vertexBuffer);
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
        var primitiveAddress = 0ul;
        if (shape.PrimitiveCount > 0)
        {
            CreateBuffer(
                (nuint)(shape.PrimitiveCount * sizeof(PrimitiveInstance)),
                BufferUsageFlags.TransferDstBit | BufferUsageFlags.ShaderDeviceAddressBit,
                MemoryPropertyFlags.DeviceLocalBit,
                out primitiveBuffer,
                out primitiveMemory
            );
            primitiveAddress = GetBufferDeviceAddress(primitiveBuffer);
        }

        var created = new StaticGeometryBuffer(
            staticTag,
            contentHash,
            vertexBuffer,
            vertexMemory,
            vertexAddress,
            shape.VertexCount,
            indexBuffer,
            indexMemory,
            shape.IndexCount,
            primitiveBuffer,
            primitiveMemory,
            primitiveAddress,
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
            primitiveBufferRects,
            primitiveBufferCircles);
    }

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
        ReadOnlySpan<UiRectFilledPrimitive> rectPrimitives,
        ReadOnlySpan<UiCircleFilledPrimitive> circlePrimitives)
    {
        var primitiveCount = rectPrimitives.Length + circlePrimitives.Length;
        var sourcePrimitiveCount = primitiveCount;
        if (sourcePrimitiveCount is 0)
        {
            return;
        }

        var sourceSize = (nuint)(sourcePrimitiveCount * sizeof(PrimitiveInstance));
        var commandBuffer = BeginStagingUpload(sourceSize, out var stagingOffset);
        var primitiveDst = (PrimitiveInstance*)((byte*)_stagingMappedPtr + stagingOffset);

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

    private const int StaticGeometryRetiredReuseGraceFrames = 120;

    private readonly Dictionary<string, List<RetiredStaticGeometryBuffer>> _retiredStaticGeometryBuffers = new(StringComparer.Ordinal);
    private readonly List<string> _retiredStaticGeometryStaleTags = new();

    private bool HasRetiredStaticGeometryBuffers()
    {
        return _retiredStaticGeometryBuffers.Count is not 0;
    }

    private bool TryTakeReusableStaticGeometryBuffer(
        string staticTag,
        in StaticGeometryShape shape,
        out StaticGeometryBuffer reusable)
    {
        reusable = default;
        if (!_retiredStaticGeometryBuffers.TryGetValue(staticTag, out var retiredBuffers)
            || retiredBuffers.Count is 0)
        {
            return false;
        }

        for (var i = 0; i < retiredBuffers.Count; i++)
        {
            var retired = retiredBuffers[i];
            var retiredBuffer = retired.Buffer;
            if (_frameIndex < retired.AvailableFrame)
            {
                continue;
            }

            if (!CanUpdateStaticGeometryBufferInPlace(
                    in retiredBuffer,
                    in shape))
            {
                QueueStaticGeometryBufferDestroy(retiredBuffer);
                retiredBuffers.RemoveAt(i);
                if (retiredBuffers.Count is 0)
                {
                    _retiredStaticGeometryBuffers.Remove(staticTag);
                }

                i--;
                continue;
            }

            reusable = retiredBuffer;
            retiredBuffers.RemoveAt(i);
            if (retiredBuffers.Count is 0)
            {
                _retiredStaticGeometryBuffers.Remove(staticTag);
            }

            return true;
        }

        return false;
    }

    private void RetireStaticGeometryBufferForReuse(StaticGeometryBuffer staticBuffer)
    {
        if (staticBuffer.VertexBuffer.Handle is 0
            && staticBuffer.IndexBuffer.Handle is 0
            && staticBuffer.PrimitiveBuffer.Handle is 0)
        {
            return;
        }

        var frameCount = _frames.Length;
        var availableFrame = _frameIndex + Math.Max(1, frameCount);
        if (!_retiredStaticGeometryBuffers.TryGetValue(staticBuffer.Tag, out var retiredBuffers))
        {
            retiredBuffers = new List<RetiredStaticGeometryBuffer>();
            _retiredStaticGeometryBuffers[staticBuffer.Tag] = retiredBuffers;
        }

        retiredBuffers.Add(new RetiredStaticGeometryBuffer(staticBuffer, availableFrame));
        TrimRetiredStaticGeometryBufferList(retiredBuffers, Math.Max(1, frameCount));
    }

    private void QueueRetiredStaticGeometryBuffersDestroy(string staticTag)
    {
        if (!_retiredStaticGeometryBuffers.Remove(staticTag, out var retiredBuffers))
        {
            return;
        }

        for (var i = 0; i < retiredBuffers.Count; i++)
        {
            QueueStaticGeometryBufferDestroy(retiredBuffers[i].Buffer);
        }

        retiredBuffers.Clear();
    }

    private void TrimRetiredStaticGeometryBufferList(List<RetiredStaticGeometryBuffer> retiredBuffers, int maxRetiredBuffers)
    {
        while (retiredBuffers.Count > maxRetiredBuffers)
        {
            var retired = retiredBuffers[0];
            QueueStaticGeometryBufferDestroy(retired.Buffer);
            retiredBuffers.RemoveAt(0);
        }
    }

    private void PruneRetiredStaticGeometryBuffers(int currentFrameIndex)
    {
        if (_retiredStaticGeometryBuffers.Count is 0)
        {
            return;
        }

        var staleTags = _retiredStaticGeometryStaleTags;
        staleTags.Clear();
        foreach (var pair in _retiredStaticGeometryBuffers)
        {
            var retiredBuffers = pair.Value;
            var write = 0;
            for (var i = 0; i < retiredBuffers.Count; i++)
            {
                var retired = retiredBuffers[i];
                if (currentFrameIndex >= retired.AvailableFrame + StaticGeometryRetiredReuseGraceFrames)
                {
                    QueueStaticGeometryBufferDestroy(retired.Buffer);
                    continue;
                }

                retiredBuffers[write++] = retired;
            }

            if (write < retiredBuffers.Count)
            {
                retiredBuffers.RemoveRange(write, retiredBuffers.Count - write);
            }

            if (retiredBuffers.Count is 0)
            {
                staleTags.Add(pair.Key);
            }
        }

        for (var i = 0; i < staleTags.Count; i++)
        {
            _retiredStaticGeometryBuffers.Remove(staleTags[i]);
        }

        staleTags.Clear();
    }

    private (int Entries, ulong Bytes) GetRetiredStaticGeometryMemoryStats()
    {
        var retiredEntries = 0;
        var retiredBytes = 0UL;
        foreach (var pair in _retiredStaticGeometryBuffers)
        {
            var retiredBuffers = pair.Value;
            for (var i = 0; i < retiredBuffers.Count; i++)
            {
                retiredEntries++;
                retiredBytes += GetStaticGeometryBufferByteSize(retiredBuffers[i].Buffer);
            }
        }

        return (retiredEntries, retiredBytes);
    }

    private void DestroyRetiredStaticGeometryBuffersImmediate()
    {
        foreach (var pair in _retiredStaticGeometryBuffers)
        {
            var retiredBuffers = pair.Value;
            for (var i = 0; i < retiredBuffers.Count; i++)
            {
                DestroyStaticGeometryBufferImmediate(retiredBuffers[i].Buffer);
            }

            retiredBuffers.Clear();
        }

        _retiredStaticGeometryBuffers.Clear();
        _retiredStaticGeometryStaleTags.Clear();
    }

    private void ClearRetiredStaticGeometryBuffers()
    {
        _retiredStaticGeometryBuffers.Clear();
        _retiredStaticGeometryStaleTags.Clear();
    }
}

