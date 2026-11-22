using Duxel.Core;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        const BufferUsageFlags shaderUsage = BufferUsageFlags.TransferDstBit | BufferUsageFlags.ShaderDeviceAddressBit;
        const BufferUsageFlags indexUsage = BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit;
        Span<StaticBufferRange> ranges = stackalloc StaticBufferRange[3];
        ranges.Clear();
        var slice = default(StaticMemorySlice);
        StaticGeometryBuffer created;
        try
        {
            CreateStaticBufferRange(ref ranges[0], (ulong)shape.CombinedVertexCount * (ulong)sizeof(UiVertex), shaderUsage);
            CreateStaticBufferRange(ref ranges[1], StaticIndexBufferBytes(shape.CombinedIndexCount, shape.StaticIndexType), indexUsage);
            CreateStaticBufferRange(ref ranges[2], (ulong)shape.PrimitiveCount * (ulong)sizeof(PrimitiveInstance), shaderUsage);
            BindStaticBufferRanges(ranges, ref slice);

            ref readonly var vertex = ref ranges[0];
            ref readonly var index = ref ranges[1];
            ref readonly var primitive = ref ranges[2];
            created = new StaticGeometryBuffer(
                staticTag,
                contentHash,
                vertex.Buffer,
                vertex.Memory,
                vertex.Buffer.Handle != 0 ? GetBufferDeviceAddress(vertex.Buffer) : 0,
                shape.VertexCount,
                index.Buffer,
                index.Memory,
                shape.IndexCount,
                primitive.Buffer,
                primitive.Memory,
                primitive.Buffer.Handle != 0 ? GetBufferDeviceAddress(primitive.Buffer) : 0,
                shape.PrimitiveCount,
                shape.PrimitiveInstanceBaseCount,
                shape.RectPrimitiveCount,
                shape.CirclePrimitiveCount,
                shape.HasExpandedPrimitiveGeometry,
                shape.PrimitiveTriangleLayout.RectExpandedIndexBase,
                shape.PrimitiveTriangleLayout.CircleExpandedIndexOffsets
            ) { StaticIndexType = shape.StaticIndexType, IndexTopologyHash = HashIndexTopology(indices), StaticMemory = slice };
        }
        catch
        {
            foreach (ref readonly var range in ranges)
                DestroyBufferResource(new BufferResource(range.Buffer, range.Memory));
            if (slice.Page is not null)
                ReleaseStaticMemoryRange(in slice);
            throw;
        }

        try
        {
            UploadStaticGeometryBufferContent(created, vertices, indices, rects, circles, in shape);
            return created;
        }
        catch
        {
            // Earlier copies may already be submitted. Keep their backing alive through retirement.
            QueueStaticGeometryBufferDestroy(created);
            throw;
        }
    }

    private struct StaticBufferRange
    {
        public VkBuffer Buffer;
        public DeviceMemory Memory;
        public MemoryRequirements Requirements;
        public ulong Offset;
        public bool DeviceAddress;
    }

    private void CreateStaticBufferRange(ref StaticBufferRange range, ulong size, BufferUsageFlags usage)
    {
        if (size == 0)
            return;

        range.Buffer = CreateUnboundBuffer(checked((nuint)size), usage);
        _vk.GetBufferMemoryRequirements(_device, range.Buffer, out range.Requirements);
        range.DeviceAddress = (usage & BufferUsageFlags.ShaderDeviceAddressBit) != 0;
    }

    private void BindStaticBufferRanges(Span<StaticBufferRange> ranges, ref StaticMemorySlice slice)
    {
        var size = 0ul;
        var alignment = 1ul;
        var memoryTypes = uint.MaxValue;
        foreach (ref var range in ranges)
        {
            if (range.Buffer.Handle == 0)
                continue;

            ref readonly var requirement = ref range.Requirements;
            alignment = Math.Max(alignment, requirement.Alignment);
            range.Offset = AlignStaticMemory(size, requirement.Alignment);
            size = checked(range.Offset + requirement.Size);
            memoryTypes &= requirement.MemoryTypeBits;
        }

        if (size == 0)
            return;

        // A page can back only buffers with a common device-local memory type.
        if (size <= StaticMemoryPageBytes && TryFindMemoryType(memoryTypes, MemoryPropertyFlags.DeviceLocalBit, out var pageType))
        {
            slice = RentStaticMemoryRange(pageType, size, alignment);
            var memory = slice.Page!.Memory;
            foreach (ref readonly var range in ranges)
            {
                if (range.Buffer.Handle != 0)
                    Check(_vk.BindBufferMemory(_device, range.Buffer, memory, slice.Offset + range.Offset));
            }
            return;
        }

        // Oversized groups and nonintersecting device memory types keep individually owned buffers.
        foreach (ref var range in ranges)
        {
            if (range.Buffer.Handle == 0)
                continue;

            var type = FindMemoryType(range.Requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit);
            range.Memory = AllocateBufferMemory(range.Requirements.Size, type, range.DeviceAddress);
            Check(_vk.BindBufferMemory(_device, range.Buffer, range.Memory, 0));
        }
    }

    private StaticGeometryBuffer ReuploadStaticGeometryBufferContent(
        StaticGeometryBuffer target,
        ulong contentHash,
        ReadOnlySpan<UiDrawVertex> vertices,
        ReadOnlySpan<uint> indices,
        ReadOnlySpan<UiRectFilledPrimitive> rects,
        ReadOnlySpan<UiCircleFilledPrimitive> circles,
        in StaticGeometryShape shape)
    {
        UploadStaticGeometryBufferContent(target, vertices, indices, rects, circles, in shape);
        return target with
        {
            ContentHash = contentHash,
            IndexTopologyHash = HashIndexTopology(indices),
            RectExpandedIndexBase = shape.PrimitiveTriangleLayout.RectExpandedIndexBase,
            CircleExpandedIndexOffsets = shape.PrimitiveTriangleLayout.CircleExpandedIndexOffsets,
        };
    }

    private static ulong HashIndexTopology(ReadOnlySpan<uint> indices)
        => System.IO.Hashing.XxHash3.HashToUInt64(MemoryMarshal.AsBytes(indices));

    private void UploadStaticGeometryBufferContent(
        StaticGeometryBuffer target,
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
                target.StaticIndexType,
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
        var vertexOffset = vertices.Length;
        MemoryMarshal.AsBytes(vertices).CopyTo(new Span<byte>(vertexDst, vertices.Length * sizeof(UiVertex)));

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

            RecordUploadBufferCopy();
            _vk.CmdCopyBuffer(commandBuffer, _stagingBuffer, destinationBuffer, 1, &region);
        }
        finally
        {
            EndSingleTimeCommands(commandBuffer);
        }
    }

    private static ulong StaticIndexBufferBytes(int indexCount, IndexType indexType)
        => indexType == IndexType.Uint16 ? ((ulong)indexCount * sizeof(ushort) + 3) & ~3UL : (ulong)indexCount * sizeof(uint);

    private static void PackStaticIndices(ReadOnlySpan<uint> source, Span<ushort> destination)
    {
        var i = 0;
        var lanes = Vector<uint>.Count;
        if (Vector.IsHardwareAccelerated)
        {
            for (; i <= source.Length - lanes * 2; i += lanes * 2)
            {
                Vector.Narrow(new Vector<uint>(source.Slice(i, lanes)), new Vector<uint>(source.Slice(i + lanes, lanes)))
                    .CopyTo(destination[i..]);
            }
        }
        for (; i < source.Length; i++)
            destination[i] = (ushort)source[i];
        destination[source.Length..].Clear();
    }

    private void UploadStaticIndexBufferData(
        VkBuffer destinationBuffer,
        IndexType indexType,
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

        var sourceSize = checked((nuint)StaticIndexBufferBytes(indexCount, indexType));
        var commandBuffer = BeginStagingUpload(sourceSize, out var stagingOffset);
        var target = (byte*)_stagingMappedPtr + stagingOffset;
        try
        {
            if (indexType == IndexType.Uint16)
            {
                // Shape selection excludes expanded primitives and validates each source index.
                PackStaticIndices(indices, new Span<ushort>(target, checked((int)(sourceSize / sizeof(ushort)))));
            }
            else
            {
                MemoryMarshal.AsBytes(indices).CopyTo(new Span<byte>(target, indices.Length * sizeof(uint)));
                if (includeExpandedPrimitives)
                    WriteExpandedPrimitiveIndices((uint*)target + indices.Length, originalVertexCount, rectPrimitives, circlePrimitives);
            }
            var region = new BufferCopy
            {
                SrcOffset = stagingOffset,
                DstOffset = 0,
                Size = sourceSize,
            };

            RecordUploadBufferCopy();
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

            RecordUploadBufferCopy();
            _vk.CmdCopyBuffer(commandBuffer, _stagingBuffer, destinationBuffer, 1, &region);
        }
        finally
        {
            EndSingleTimeCommands(commandBuffer);
        }
    }

    private const int StaticGeometryRetiredReuseGraceFrames = 120;
    private const ulong MaxExtendedStaticGeometryBufferBytes = 16 * 1024;
    private const ulong MaxExtendedStaticGeometryReuseBytes = 256 * 1024;
    private const int MaxExtendedStaticGeometryReuseCount = 64;

    private readonly Dictionary<string, List<RetiredStaticGeometryBuffer>> _retiredStaticGeometryBuffers = new(StringComparer.Ordinal);
    private readonly List<string> _retiredStaticGeometryStaleTags = new();
    private ulong _extendedStaticGeometryReuseBytes;
    private int _extendedStaticGeometryReuseCount;

    private bool HasRetiredStaticGeometryBuffers()
    {
        return _retiredStaticGeometryBuffers.Count is not 0;
    }

    private bool TryTakeReusableStaticGeometryBuffer(
        string staticTag,
        in StaticGeometryShape shape,
        out StaticGeometryBuffer reusable)
    {
        reusable = StaticGeometryBuffer.Empty;
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
                    retiredBuffer,
                    in shape))
            {
                QueueStaticGeometryBufferDestroy(retiredBuffer);
                ReleaseExtendedStaticGeometryReuse(in retired);
                retiredBuffers.RemoveAt(i);
                if (retiredBuffers.Count is 0)
                {
                    _retiredStaticGeometryBuffers.Remove(staticTag);
                }

                i--;
                continue;
            }

            reusable = retiredBuffer;
            ReleaseExtendedStaticGeometryReuse(in retired);
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

        TrimRetiredStaticGeometryBufferList(retiredBuffers, Math.Max(1, frameCount) - 1);
        var bytes = GetStaticGeometryBufferByteSize(staticBuffer);
        // Keep the frame-based reuse guard. A wall-clock grace prevents frequent
        // presentation from expiring small rotating buffers between updates.
        if (bytes is > 0 and <= MaxExtendedStaticGeometryBufferBytes
            && _extendedStaticGeometryReuseCount < MaxExtendedStaticGeometryReuseCount
            && bytes <= MaxExtendedStaticGeometryReuseBytes - _extendedStaticGeometryReuseBytes)
        {
            retiredBuffers.Add(new(staticBuffer, availableFrame, Stopwatch.GetTimestamp() + Stopwatch.Frequency, bytes));
            _extendedStaticGeometryReuseBytes += bytes;
            _extendedStaticGeometryReuseCount++;
            return;
        }

        retiredBuffers.Add(new(staticBuffer, availableFrame, 0, 0));
    }

    private void ReleaseExtendedStaticGeometryReuse(in RetiredStaticGeometryBuffer retired)
    {
        if (retired.ExtendedReuseBytes == 0)
            return;

        _extendedStaticGeometryReuseBytes -= retired.ExtendedReuseBytes;
        _extendedStaticGeometryReuseCount--;
    }

    private void QueueRetiredStaticGeometryBuffersDestroy(string staticTag)
    {
        if (!_retiredStaticGeometryBuffers.Remove(staticTag, out var retiredBuffers))
        {
            return;
        }

        for (var i = 0; i < retiredBuffers.Count; i++)
        {
            var retired = retiredBuffers[i];
            QueueStaticGeometryBufferDestroy(retired.Buffer);
            ReleaseExtendedStaticGeometryReuse(in retired);
        }

        retiredBuffers.Clear();
    }

    private void TrimRetiredStaticGeometryBufferList(List<RetiredStaticGeometryBuffer> retiredBuffers, int maxRetiredBuffers)
    {
        while (retiredBuffers.Count > maxRetiredBuffers)
        {
            var retired = retiredBuffers[0];
            QueueStaticGeometryBufferDestroy(retired.Buffer);
            ReleaseExtendedStaticGeometryReuse(in retired);
            retiredBuffers.RemoveAt(0);
        }
    }

    private void PruneRetiredStaticGeometryBuffers(int currentFrameIndex, long? timestamp = null)
    {
        if (_retiredStaticGeometryBuffers.Count is 0)
        {
            return;
        }

        var staleTags = _retiredStaticGeometryStaleTags;
        staleTags.Clear();
        var now = timestamp ?? Stopwatch.GetTimestamp();
        foreach (var pair in _retiredStaticGeometryBuffers)
        {
            var retiredBuffers = pair.Value;
            var write = 0;
            for (var i = 0; i < retiredBuffers.Count; i++)
            {
                var retired = retiredBuffers[i];
                if (currentFrameIndex >= retired.AvailableFrame + StaticGeometryRetiredReuseGraceFrames
                    && now >= retired.ReuseUntilTimestamp)
                {
                    QueueStaticGeometryBufferDestroy(retired.Buffer);
                    ReleaseExtendedStaticGeometryReuse(in retired);
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

        ClearRetiredStaticGeometryBuffers();
    }

    private void ClearRetiredStaticGeometryBuffers()
    {
        _retiredStaticGeometryBuffers.Clear();
        _retiredStaticGeometryStaleTags.Clear();
        _extendedStaticGeometryReuseBytes = 0;
        _extendedStaticGeometryReuseCount = 0;
    }
}

