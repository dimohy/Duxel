using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Duxel.Core;
using VkBuffer = Duxel.Vulkan.Buffer;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private const string StaticLayerGeometryTagPrefix = "duxel.layer.static:";
    private const string StaticGlobalGeometryTagPrefix = "duxel.global.static:";
    private const int StaticGeometryLruGraceFrames = 180;
    private const int StaticGeometryPruneIntervalFrames = 32;

    private readonly Dictionary<string, StaticGeometryBuffer> _staticGeometryBuffers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _staticGeometryLastSeenFrame = new(StringComparer.Ordinal);
    private readonly List<string> _staticGeometryStaleTags = new();
    private readonly List<int> _dynamicDrawListIndices = new();
    private readonly Dictionary<int, StaticGeometryBuffer> _frameStaticBindings = new();

    private void PrepareStaticGeometryAndCountDynamic(
        UiDrawData drawData,
        Dictionary<int, StaticGeometryBuffer> staticBindings,
        out int dynamicVertexCount,
        out int dynamicIndexCount,
        out int rectPrimitiveCount,
        out int circlePrimitiveCount)
    {
        dynamicVertexCount = 0;
        dynamicIndexCount = 0;
        rectPrimitiveCount = 0;
        circlePrimitiveCount = 0;
        staticBindings.Clear();
        _dynamicDrawListIndices.Clear();

        BeginUploadBatch();
        try
        {
            for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
            {
                var drawList = drawData.DrawLists[listIndex];
                rectPrimitiveCount += drawList.RectFilledPrimitives?.Count ?? 0;
                circlePrimitiveCount += drawList.CircleFilledPrimitives?.Count ?? 0;

                if (TryGetStaticDrawListTag(drawList, out var staticTag))
                {
                    _staticGeometryLastSeenFrame[staticTag] = _frameIndex;
                    staticBindings[listIndex] = EnsureStaticGeometryBuffer(staticTag, drawList);
                    continue;
                }

                _dynamicDrawListIndices.Add(listIndex);
                dynamicVertexCount += drawList.Vertices.Count;
                dynamicIndexCount += drawList.Indices.Count;
            }
        }
        finally
        {
            EndUploadBatch();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void UploadGeometry(int frame, UiDrawData drawData)
    {
        var frameData = _frames[frame];
        var renderBuffers = frameData.RenderBuffers;

        var vertexDst = (byte*)renderBuffers.VertexMappedPtr;
        var indexDst = (byte*)renderBuffers.IndexMappedPtr;
        var rectPrimitiveDst = (RectPrimitiveInstance*)renderBuffers.RectPrimitiveMappedPtr;
        var circlePrimitiveDst = (CirclePrimitiveInstance*)renderBuffers.CirclePrimitiveMappedPtr;

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
            var drawList = drawData.DrawLists[listIndex];
            if (drawList.RectFilledPrimitives is not null)
            {
                var primitives = drawList.RectFilledPrimitives.AsSpan();
                for (var i = 0; i < primitives.Length; i++)
                {
                    ref readonly var primitive = ref primitives[i];
                    var rect = primitive.Rect;
                    rectPrimitiveDst[i] = new RectPrimitiveInstance
                    {
                        X = rect.X,
                        Y = rect.Y,
                        Width = rect.Width,
                        Height = rect.Height,
                        Color = primitive.Color.Rgba,
                    };
                }

                rectPrimitiveDst += primitives.Length;
            }

            if (drawList.CircleFilledPrimitives is not null)
            {
                var primitives = drawList.CircleFilledPrimitives.AsSpan();
                for (var i = 0; i < primitives.Length; i++)
                {
                    ref readonly var primitive = ref primitives[i];
                    circlePrimitiveDst[i] = new CirclePrimitiveInstance
                    {
                        CenterX = primitive.Center.X,
                        CenterY = primitive.Center.Y,
                        Radius = primitive.Radius,
                        Color = primitive.Color.Rgba,
                        Segments = (uint)primitive.Segments,
                    };
                }

                circlePrimitiveDst += primitives.Length;
            }
        }

        if ((_frameIndex % StaticGeometryPruneIntervalFrames) is 0)
        {
            PruneUnusedStaticGeometryBuffers(_frameIndex);
        }
    }

    private StaticGeometryBuffer EnsureStaticGeometryBuffer(string staticTag, UiDrawList drawList)
    {
        var vertexCount = drawList.Vertices.Count;
        var indexCount = drawList.Indices.Count;
        if (_staticGeometryBuffers.TryGetValue(staticTag, out var existing)
            && existing.VertexCount == vertexCount
            && existing.IndexCount == indexCount)
        {
            return existing;
        }

        if (existing.VertexBuffer.Handle is not 0)
        {
            QueueBufferDestroy(existing.VertexBuffer, existing.VertexMemory);
            QueueBufferDestroy(existing.IndexBuffer, existing.IndexMemory);
        }

        var vertexSize = (nuint)(Math.Max(1, vertexCount) * sizeof(UiVertex));
        var indexSize = (nuint)(Math.Max(1, indexCount) * sizeof(uint));
        CreateBuffer(
            vertexSize,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit,
            out var vertexBuffer,
            out var vertexMemory
        );

        CreateBuffer(
            indexSize,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit,
            out var indexBuffer,
            out var indexMemory
        );

        var vertices = drawList.Vertices.AsSpan();
        if (!vertices.IsEmpty)
        {
            var convertedVertices = new UiVertex[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                ref readonly var src = ref vertices[i];
                convertedVertices[i] = new UiVertex
                {
                    PositionX = src.Position.X,
                    PositionY = src.Position.Y,
                    UVx = src.UV.X,
                    UVy = src.UV.Y,
                    Color = src.Color.Rgba,
                };
            }

            fixed (UiVertex* vertexSrc = convertedVertices)
            {
                UploadStaticBufferData(vertexBuffer, vertexSize, vertexSrc, (nuint)(convertedVertices.Length * sizeof(UiVertex)));
            }
        }

        var indices = drawList.Indices.AsSpan();
        if (!indices.IsEmpty)
        {
            fixed (uint* indexSrc = indices)
            {
                UploadStaticBufferData(indexBuffer, indexSize, indexSrc, (nuint)(indices.Length * sizeof(uint)));
            }
        }

        var created = new StaticGeometryBuffer(
            staticTag,
            vertexBuffer,
            vertexMemory,
            vertexCount,
            indexBuffer,
            indexMemory,
            indexCount
        );

        _staticGeometryBuffers[staticTag] = created;
        return created;
    }

    private void PruneUnusedStaticGeometryBuffers(int currentFrameIndex)
    {
        if (_staticGeometryBuffers.Count is 0)
        {
            return;
        }

        var staleTags = _staticGeometryStaleTags;
        staleTags.Clear();
        foreach (var pair in _staticGeometryBuffers)
        {
            if (!_staticGeometryLastSeenFrame.TryGetValue(pair.Key, out var lastSeenFrame))
            {
                staleTags.Add(pair.Key);
                continue;
            }

            if ((currentFrameIndex - lastSeenFrame) > StaticGeometryLruGraceFrames)
            {
                staleTags.Add(pair.Key);
            }
        }

        for (var i = 0; i < staleTags.Count; i++)
        {
            var staleTag = staleTags[i];
            if (!_staticGeometryBuffers.Remove(staleTag, out var stale))
            {
                continue;
            }

            _staticGeometryLastSeenFrame.Remove(staleTag);
            QueueBufferDestroy(stale.VertexBuffer, stale.VertexMemory);
            QueueBufferDestroy(stale.IndexBuffer, stale.IndexMemory);
        }

        staleTags.Clear();
    }

    private bool TryGetStaticDrawListTag(UiDrawList drawList, out string tag)
    {
        tag = string.Empty;
        var commandCount = drawList.Commands.Count;
        if (commandCount is 0)
        {
            return false;
        }

        for (var i = 0; i < commandCount; i++)
        {
            ref readonly var cmd = ref drawList.Commands.ItemRef(i);
            if (!IsStaticLayerGeometryTag(cmd.UserData, out var commandTag))
            {
                return false;
            }

            if (i is 0)
            {
                tag = commandTag;
                continue;
            }

            if (!string.Equals(tag, commandTag, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsStaticLayerGeometryTag(object? userData, out string tag)
    {
        if (userData is string value
            && (value.StartsWith(StaticLayerGeometryTagPrefix, StringComparison.Ordinal)
                || value.StartsWith(StaticGlobalGeometryTagPrefix, StringComparison.Ordinal)))
        {
            tag = value;
            return true;
        }

        tag = string.Empty;
        return false;
    }

    private void UploadStaticBufferData(VkBuffer destinationBuffer, nuint destinationSize, void* sourceData, nuint sourceSize)
    {
        if (sourceData is null || sourceSize is 0)
        {
            return;
        }

        EnsureStagingBuffer(destinationSize);
        Unsafe.CopyBlockUnaligned(_stagingMappedPtr, sourceData, checked((uint)sourceSize));
        CopyBuffer(_stagingBuffer, destinationBuffer, sourceSize);
    }

    private void DestroyStaticGeometryBuffers()
    {
        if (_staticGeometryBuffers.Count is 0)
        {
            _staticGeometryLastSeenFrame.Clear();
            _frameStaticBindings.Clear();
            return;
        }

        foreach (var pair in _staticGeometryBuffers)
        {
            var staticBuffer = pair.Value;
            DestroyBufferResource(new BufferResource(staticBuffer.VertexBuffer, staticBuffer.VertexMemory));
            DestroyBufferResource(new BufferResource(staticBuffer.IndexBuffer, staticBuffer.IndexMemory));
        }

        _staticGeometryBuffers.Clear();
        _staticGeometryLastSeenFrame.Clear();
        _frameStaticBindings.Clear();
    }
}
