using System;
using System.Collections.Generic;
using Duxel.Core;

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

    private void MarkStaticGeometrySeen(string staticTag, int frameIndex)
    {
        _staticGeometryLastSeenFrame[staticTag] = frameIndex;
    }

    private bool TryGetActiveStaticGeometryBuffer(string staticTag, out StaticGeometryBuffer staticBuffer)
    {
        return _staticGeometryBuffers.TryGetValue(staticTag, out staticBuffer);
    }

    private void SetActiveStaticGeometryBuffer(string staticTag, StaticGeometryBuffer staticBuffer)
    {
        _staticGeometryBuffers[staticTag] = staticBuffer;
    }

    private (int ActiveEntries, int RetiredEntries, ulong ActiveBytes, ulong RetiredBytes) GetStaticGeometryMemoryStats()
    {
        var activeEntries = 0;
        var activeBytes = 0UL;
        foreach (var pair in _staticGeometryBuffers)
        {
            activeEntries++;
            activeBytes += GetStaticGeometryBufferByteSize(pair.Value);
        }

        var retired = GetRetiredStaticGeometryMemoryStats();

        return (activeEntries, retired.Entries, activeBytes, retired.Bytes);
    }

    private static ulong GetStaticGeometryBufferByteSize(StaticGeometryBuffer staticBuffer)
    {
        var vertexCount = staticBuffer.VertexBuffer.Handle is 0
            ? 0
            : staticBuffer.VertexCount + CountExpandedStaticGeometryVertices(staticBuffer);
        var indexCount = staticBuffer.IndexBuffer.Handle is 0
            ? 0
            : staticBuffer.IndexCount + CountExpandedStaticGeometryIndices(staticBuffer);
        var primitiveCount = staticBuffer.PrimitiveBuffer.Handle is 0
            ? 0
            : staticBuffer.PrimitiveCount;
        return checked(
            ((ulong)vertexCount * (ulong)sizeof(UiVertex))
            + ((ulong)indexCount * sizeof(uint))
            + ((ulong)primitiveCount * (ulong)sizeof(PrimitiveInstance)));
    }

    private static int CountExpandedStaticGeometryVertices(StaticGeometryBuffer staticBuffer)
    {
        if (!staticBuffer.HasExpandedPrimitiveGeometry)
        {
            return 0;
        }

        var count = checked(staticBuffer.RectPrimitiveCount * 4);
        var circleOffsets = staticBuffer.CircleExpandedIndexOffsets;
        if (circleOffsets is null || circleOffsets.Length <= 1)
        {
            return count;
        }

        for (var i = 0; i < circleOffsets.Length - 1; i++)
        {
            var indexCount = circleOffsets[i + 1] - circleOffsets[i];
            if (indexCount <= 0)
            {
                continue;
            }

            count = checked(count + (indexCount / 3) + 1);
        }

        return count;
    }

    private static int CountExpandedStaticGeometryIndices(StaticGeometryBuffer staticBuffer)
    {
        if (!staticBuffer.HasExpandedPrimitiveGeometry)
        {
            return 0;
        }

        var count = checked(staticBuffer.RectPrimitiveCount * 6);
        var circleOffsets = staticBuffer.CircleExpandedIndexOffsets;
        if (circleOffsets is null || circleOffsets.Length <= 1)
        {
            return count;
        }

        for (var i = 0; i < circleOffsets.Length - 1; i++)
        {
            var indexCount = circleOffsets[i + 1] - circleOffsets[i];
            if (indexCount > 0)
            {
                count = checked(count + indexCount);
            }
        }

        return count;
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
            QueueStaticGeometryBufferDestroy(stale);
            QueueRetiredStaticGeometryBuffersDestroy(staleTag);
            RemoveStaticPrimitiveTrianglePolicyState(staleTag);
        }

        staleTags.Clear();
    }

    private void PruneStaticGeometryCachesIfNeeded(int currentFrameIndex)
    {
        if ((currentFrameIndex % StaticGeometryPruneIntervalFrames) is not 0)
        {
            return;
        }

        PruneUnusedStaticGeometryBuffers(currentFrameIndex);
        PruneRetiredStaticGeometryBuffers(currentFrameIndex);
    }

    private void QueueStaticGeometryBufferDestroy(StaticGeometryBuffer staticBuffer)
    {
        QueueBufferDestroy(staticBuffer.VertexBuffer, staticBuffer.VertexMemory);
        QueueBufferDestroy(staticBuffer.IndexBuffer, staticBuffer.IndexMemory);
        QueueBufferDestroy(staticBuffer.PrimitiveBuffer, staticBuffer.PrimitiveMemory);
    }

    private void DestroyStaticGeometryBufferImmediate(StaticGeometryBuffer staticBuffer)
    {
        DestroyBufferResource(new BufferResource(staticBuffer.VertexBuffer, staticBuffer.VertexMemory));
        DestroyBufferResource(new BufferResource(staticBuffer.IndexBuffer, staticBuffer.IndexMemory));
        DestroyBufferResource(new BufferResource(staticBuffer.PrimitiveBuffer, staticBuffer.PrimitiveMemory));
    }

    private bool TryGetStaticDrawListTag(UiDrawList drawList, out string tag, out bool requiresContentHash)
    {
        tag = string.Empty;
        requiresContentHash = false;
        if (!string.IsNullOrWhiteSpace(drawList.StaticGeometryKey)
            && IsStaticLayerGeometryTag(drawList.StaticGeometryKey, out tag))
        {
            return true;
        }

        var commandCount = drawList.Commands.Count;
        if (commandCount is 0)
        {
            requiresContentHash = false;
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

        requiresContentHash = true;
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

    private void DestroyStaticGeometryBuffers()
    {
        if (_staticGeometryBuffers.Count is 0 && !HasRetiredStaticGeometryBuffers())
        {
            _staticGeometryLastSeenFrame.Clear();
            ClearRetiredStaticGeometryBuffers();
            ClearStaticPrimitiveTrianglePolicyState();
            _frameStaticBindings.Clear();
            return;
        }

        foreach (var pair in _staticGeometryBuffers)
        {
            DestroyStaticGeometryBufferImmediate(pair.Value);
        }

        DestroyRetiredStaticGeometryBuffersImmediate();

        _staticGeometryBuffers.Clear();
        _staticGeometryLastSeenFrame.Clear();
        _staticGeometryStaleTags.Clear();
        ClearStaticPrimitiveTrianglePolicyState();
        _frameStaticBindings.Clear();
    }
}
