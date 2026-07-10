using Duxel.Core;
using System.Collections.Generic;
using System;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private StaticGeometryBuffer EnsureStaticGeometryBuffer(string staticTag, UiDrawList drawList, bool requiresContentHash)
    {
        var hasExisting = TryGetActiveStaticGeometryBuffer(staticTag, out var existing);
        var source = PrepareStaticGeometrySource(
            staticTag,
            drawList,
            requiresContentHash,
            hasExisting,
            in existing);
        if (hasExisting
            && TryApplyStaticGeometryCacheHit(in existing, in source, out var cached))
        {
            return cached;
        }

        var content = CreateStaticGeometryContent(in source);
        var shape = content.Shape;
        var canUpdateSameShape = CanUpdateStaticGeometryBufferInPlace(in existing, in shape);
        if (_staticGeometryRotatingUpdateEnabled
            && canUpdateSameShape
            && TryTakeReusableStaticGeometryBuffer(
                staticTag,
                in shape,
                out var reusable))
        {
            return ApplyStaticGeometryReusableBuffer(
                staticTag,
                in existing,
                in reusable,
                in content,
                in shape);
        }

        if (_staticGeometryInPlaceUpdateEnabled
            && !_staticGeometryRotatingUpdateEnabled
            && canUpdateSameShape)
        {
            return ApplyStaticGeometryInPlaceUpdate(
                staticTag,
                in existing,
                in content,
                in shape);
        }

        if (HasStaticGeometryBufferResources(in existing))
        {
            return ApplyStaticGeometryReplacement(
                staticTag,
                in existing,
                in content,
                in shape,
                canUpdateSameShape);
        }

        return ApplyStaticGeometryCreation(staticTag, in content, in shape);
    }

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

    private readonly ref struct StaticGeometrySource
    {
        public StaticGeometrySource(
            ulong contentHash,
            ReadOnlySpan<UiDrawVertex> vertices,
            ReadOnlySpan<uint> indices,
            ReadOnlySpan<UiRectFilledPrimitive> rects,
            ReadOnlySpan<UiCircleFilledPrimitive> circles,
            int primitiveInstanceBaseCount,
            bool expandPrimitiveGeometry)
        {
            ContentHash = contentHash;
            Vertices = vertices;
            Indices = indices;
            Rects = rects;
            Circles = circles;
            PrimitiveInstanceBaseCount = primitiveInstanceBaseCount;
            ExpandPrimitiveGeometry = expandPrimitiveGeometry;
        }

        public ulong ContentHash { get; }
        public ReadOnlySpan<UiDrawVertex> Vertices { get; }
        public ReadOnlySpan<uint> Indices { get; }
        public ReadOnlySpan<UiRectFilledPrimitive> Rects { get; }
        public ReadOnlySpan<UiCircleFilledPrimitive> Circles { get; }
        public int PrimitiveInstanceBaseCount { get; }
        public bool ExpandPrimitiveGeometry { get; }
    }

    private readonly ref struct StaticGeometryContent
    {
        public StaticGeometryContent(
            ulong contentHash,
            ReadOnlySpan<UiDrawVertex> vertices,
            ReadOnlySpan<uint> indices,
            ReadOnlySpan<UiRectFilledPrimitive> rects,
            ReadOnlySpan<UiCircleFilledPrimitive> circles,
            StaticGeometryShape shape)
        {
            ContentHash = contentHash;
            Vertices = vertices;
            Indices = indices;
            Rects = rects;
            Circles = circles;
            Shape = shape;
        }

        public ulong ContentHash { get; }
        public ReadOnlySpan<UiDrawVertex> Vertices { get; }
        public ReadOnlySpan<uint> Indices { get; }
        public ReadOnlySpan<UiRectFilledPrimitive> Rects { get; }
        public ReadOnlySpan<UiCircleFilledPrimitive> Circles { get; }
        public StaticGeometryShape Shape { get; }
    }

    private StaticGeometrySource PrepareStaticGeometrySource(
        string staticTag,
        UiDrawList drawList,
        bool requiresContentHash,
        bool hasExisting,
        in StaticGeometryBuffer existing)
    {
        var vertices = drawList.Vertices.AsSpan();
        var indices = drawList.Indices.AsSpan();
        var rects = drawList.RectFilledPrimitives is null
            ? ReadOnlySpan<UiRectFilledPrimitive>.Empty
            : drawList.RectFilledPrimitives.AsSpan();
        var circles = drawList.CircleFilledPrimitives is null
            ? ReadOnlySpan<UiCircleFilledPrimitive>.Empty
            : drawList.CircleFilledPrimitives.AsSpan();
        var contentHash = GetStaticGeometryContentHash(drawList, requiresContentHash);
        var primitiveTriangleDecision = GetStaticPrimitiveTriangleDecision(
            rects,
            circles,
            ShouldSuppressStaticPrimitiveTriangleAutoForMutation(
                staticTag,
                hasExisting && existing.ContentHash != contentHash));
        RecordStaticPrimitiveTriangleDecision(primitiveTriangleDecision);

        return new StaticGeometrySource(
            contentHash,
            vertices,
            indices,
            rects,
            circles,
            primitiveInstanceBaseCount: 0,
            primitiveTriangleDecision.Expand);
    }

    private StaticGeometryContent CreateStaticGeometryContent(in StaticGeometrySource source)
    {
        if (source.ExpandPrimitiveGeometry)
        {
            _profileStaticPrimitiveTriangleLayoutMaterializationCount++;
        }

        var primitiveTriangleLayout = source.ExpandPrimitiveGeometry
            ? CreateStaticPrimitiveTriangleLayout(source.Indices.Length, source.Rects, source.Circles)
            : default;
        var shape = CreateStaticGeometryShape(
            source.Vertices.Length,
            source.Indices.Length,
            source.Rects.Length,
            source.Circles.Length,
            source.PrimitiveInstanceBaseCount,
            source.ExpandPrimitiveGeometry,
            in primitiveTriangleLayout);

        return new StaticGeometryContent(
            source.ContentHash,
            source.Vertices,
            source.Indices,
            source.Rects,
            source.Circles,
            shape);
    }

    private ulong GetStaticGeometryContentHash(UiDrawList drawList, bool requiresContentHash)
    {
        if (drawList.StaticGeometryStamp is not 0)
        {
            return drawList.StaticGeometryStamp;
        }

        if (!requiresContentHash)
        {
            return 0UL;
        }

        _profileStaticGeometryHashCount++;
        return drawList.GetStaticGeometryStamp();
    }

    private readonly record struct FrameGeometryCounts(
        int DynamicVertexCount,
        int DynamicIndexCount,
        int RectPrimitiveCount,
        int CirclePrimitiveCount);

    private readonly Dictionary<int, StaticGeometryBuffer> _frameStaticBindings = new();
    private int _profileStaticGeometryHitCount;
    private int _profileStaticGeometryCreateCount;
    private int _profileStaticGeometryReplaceCount;
    private int _profileStaticGeometryUpdateCount;
    private int _profileStaticGeometryReuseCount;
    private int _profileStaticGeometryHashCount;

    private FrameGeometryCounts PrepareStaticGeometryForFrame(
        UiDrawData drawData,
        Dictionary<int, StaticGeometryBuffer> staticBindings)
    {
        var dynamicVertexCount = 0;
        var dynamicIndexCount = 0;
        var rectPrimitiveCount = 0;
        var circlePrimitiveCount = 0;

        ResetStaticGeometryFrameCounters(staticBindings);

        BeginUploadBatch();
        try
        {
            for (var listIndex = 0; listIndex < drawData.DrawLists.Count; listIndex++)
            {
                var drawList = drawData.DrawLists[listIndex];
                var vertexCount = drawList.Vertices.Count;
                var indexCount = drawList.Indices.Count;
                var drawListRectPrimitiveCount = drawList.RectFilledPrimitives?.Count ?? 0;
                var drawListCirclePrimitiveCount = drawList.CircleFilledPrimitives?.Count ?? 0;
                var hasIndexedGeometry = vertexCount > 0 || indexCount > 0;
                var hasPrimitiveGeometry = drawListRectPrimitiveCount > 0 || drawListCirclePrimitiveCount > 0;

                if ((hasIndexedGeometry || hasPrimitiveGeometry)
                    && TryPrepareStaticDrawListBinding(staticBindings, listIndex, drawList))
                {
                    continue;
                }

                if (hasIndexedGeometry)
                {
                    _dynamicDrawListIndices.Add(listIndex);
                    dynamicVertexCount += vertexCount;
                    dynamicIndexCount += indexCount;
                }

                rectPrimitiveCount += drawListRectPrimitiveCount;
                circlePrimitiveCount += drawListCirclePrimitiveCount;
            }
        }
        finally
        {
            EndUploadBatch();
        }

        return new FrameGeometryCounts(
            dynamicVertexCount,
            dynamicIndexCount,
            rectPrimitiveCount,
            circlePrimitiveCount);
    }

    private void ResetStaticGeometryFrameCounters(Dictionary<int, StaticGeometryBuffer> staticBindings)
    {
        staticBindings.Clear();
        _dynamicDrawListIndices.Clear();
        _profileStaticGeometryHitCount = 0;
        _profileStaticGeometryCreateCount = 0;
        _profileStaticGeometryReplaceCount = 0;
        _profileStaticGeometryUpdateCount = 0;
        _profileStaticGeometryReuseCount = 0;
        _profileStaticGeometryHashCount = 0;
        ResetStaticPrimitiveTriangleProfileCounters();
    }

    private bool TryPrepareStaticDrawListBinding(
        Dictionary<int, StaticGeometryBuffer> staticBindings,
        int listIndex,
        UiDrawList drawList)
    {
        if (!TryGetStaticDrawListTag(drawList, out var staticTag, out var requiresContentHash))
        {
            return false;
        }

        MarkStaticGeometrySeen(staticTag, _frameIndex);
        staticBindings[listIndex] = EnsureStaticGeometryBuffer(staticTag, drawList, requiresContentHash);
        return true;
    }

    private bool TryApplyStaticGeometryCacheHit(
        in StaticGeometryBuffer existing,
        in StaticGeometrySource source,
        out StaticGeometryBuffer staticBuffer)
    {
        if (!StaticGeometryCacheEntryMatches(in existing, in source))
        {
            staticBuffer = default;
            return false;
        }

        _profileStaticGeometryHitCount++;
        staticBuffer = existing;
        return true;
    }

    private StaticGeometryBuffer ApplyStaticGeometryReusableBuffer(
        string staticTag,
        in StaticGeometryBuffer existing,
        in StaticGeometryBuffer reusable,
        in StaticGeometryContent content,
        in StaticGeometryShape shape)
    {
        var reused = ReuploadStaticGeometryBufferContent(
            in reusable,
            content.ContentHash,
            content.Vertices,
            content.Indices,
            content.Rects,
            content.Circles,
            in shape);
        RetireStaticGeometryBufferForReuse(existing);
        _profileStaticGeometryReuseCount++;
        SetActiveStaticGeometryBuffer(staticTag, reused);
        return reused;
    }

    private StaticGeometryBuffer ApplyStaticGeometryInPlaceUpdate(
        string staticTag,
        in StaticGeometryBuffer existing,
        in StaticGeometryContent content,
        in StaticGeometryShape shape)
    {
        WaitForAllInFlightFrameFences();

        var updated = ReuploadStaticGeometryBufferContent(
            in existing,
            content.ContentHash,
            content.Vertices,
            content.Indices,
            content.Rects,
            content.Circles,
            in shape);
        _profileStaticGeometryUpdateCount++;
        SetActiveStaticGeometryBuffer(staticTag, updated);
        return updated;
    }

    private StaticGeometryBuffer ApplyStaticGeometryReplacement(
        string staticTag,
        in StaticGeometryBuffer existing,
        in StaticGeometryContent content,
        in StaticGeometryShape shape,
        bool canUpdateSameShape)
    {
        _profileStaticGeometryReplaceCount++;
        if (_staticGeometryRotatingUpdateEnabled && canUpdateSameShape)
        {
            RetireStaticGeometryBufferForReuse(existing);
        }
        else
        {
            QueueStaticGeometryBufferDestroy(existing);
            QueueRetiredStaticGeometryBuffersDestroy(staticTag);
        }

        return MaterializeAndActivateStaticGeometryBuffer(staticTag, in content, in shape);
    }

    private StaticGeometryBuffer ApplyStaticGeometryCreation(
        string staticTag,
        in StaticGeometryContent content,
        in StaticGeometryShape shape)
    {
        _profileStaticGeometryCreateCount++;
        return MaterializeAndActivateStaticGeometryBuffer(staticTag, in content, in shape);
    }

    private StaticGeometryBuffer MaterializeAndActivateStaticGeometryBuffer(
        string staticTag,
        in StaticGeometryContent content,
        in StaticGeometryShape shape)
    {
        var created = CreateStaticGeometryBuffer(
            staticTag,
            content.ContentHash,
            content.Vertices,
            content.Indices,
            content.Rects,
            content.Circles,
            in shape);

        SetActiveStaticGeometryBuffer(staticTag, created);
        return created;
    }

    private readonly record struct StaticGeometryShape(
        int VertexCount,
        int IndexCount,
        int CombinedVertexCount,
        int CombinedIndexCount,
        int PrimitiveCount,
        int PrimitiveInstanceBaseCount,
        int RectPrimitiveCount,
        int CirclePrimitiveCount,
        bool HasExpandedPrimitiveGeometry,
        StaticPrimitiveTriangleLayout PrimitiveTriangleLayout);

    private static StaticGeometryShape CreateStaticGeometryShape(
        int vertexCount,
        int indexCount,
        int rectPrimitiveCount,
        int circlePrimitiveCount,
        int primitiveInstanceBaseCount,
        bool expandPrimitiveGeometry,
        in StaticPrimitiveTriangleLayout primitiveTriangleLayout)
    {
        return new StaticGeometryShape(
            vertexCount,
            indexCount,
            vertexCount + primitiveTriangleLayout.VertexCount,
            indexCount + primitiveTriangleLayout.IndexCount,
            primitiveInstanceBaseCount + (expandPrimitiveGeometry ? 0 : rectPrimitiveCount + circlePrimitiveCount),
            primitiveInstanceBaseCount,
            rectPrimitiveCount,
            circlePrimitiveCount,
            expandPrimitiveGeometry,
            primitiveTriangleLayout);
    }

    private static bool StaticGeometryCacheEntryMatches(
        in StaticGeometryBuffer existing,
        in StaticGeometrySource source)
    {
        return existing.ContentHash == source.ContentHash
            && existing.VertexCount == source.Vertices.Length
            && existing.IndexCount == source.Indices.Length
            && existing.PrimitiveInstanceBaseCount == source.PrimitiveInstanceBaseCount
            && existing.RectPrimitiveCount == source.Rects.Length
            && existing.CirclePrimitiveCount == source.Circles.Length
            && existing.HasExpandedPrimitiveGeometry == source.ExpandPrimitiveGeometry;
    }

    private static bool HasStaticGeometryBufferResources(in StaticGeometryBuffer staticBuffer)
    {
        return staticBuffer.VertexBuffer.Handle is not 0
            || staticBuffer.IndexBuffer.Handle is not 0
            || staticBuffer.PrimitiveBuffer.Handle is not 0;
    }

    private static bool CanUpdateStaticGeometryBufferInPlace(
        in StaticGeometryBuffer existing,
        in StaticGeometryShape shape)
    {
        if (!HasStaticGeometryBufferResources(in existing))
        {
            return false;
        }

        if ((shape.CombinedVertexCount > 0 && existing.VertexBuffer.Handle is 0)
            || (shape.CombinedVertexCount is 0 && existing.VertexBuffer.Handle is not 0)
            || (shape.CombinedIndexCount > 0 && existing.IndexBuffer.Handle is 0)
            || (shape.CombinedIndexCount is 0 && existing.IndexBuffer.Handle is not 0)
            || (shape.PrimitiveCount > 0 && existing.PrimitiveBuffer.Handle is 0)
            || (shape.PrimitiveCount is 0 && existing.PrimitiveBuffer.Handle is not 0))
        {
            return false;
        }

        if (existing.VertexCount != shape.VertexCount
            || existing.IndexCount != shape.IndexCount
            || existing.PrimitiveCount != shape.PrimitiveCount
            || existing.PrimitiveInstanceBaseCount != shape.PrimitiveInstanceBaseCount
            || existing.RectPrimitiveCount != shape.RectPrimitiveCount
            || existing.CirclePrimitiveCount != shape.CirclePrimitiveCount
            || existing.HasExpandedPrimitiveGeometry != shape.HasExpandedPrimitiveGeometry
            || existing.RectExpandedIndexBase != shape.PrimitiveTriangleLayout.RectExpandedIndexBase)
        {
            return false;
        }

        return StaticCircleIndexOffsetsMatch(
            existing.CircleExpandedIndexOffsets,
            shape.PrimitiveTriangleLayout.CircleExpandedIndexOffsets);
    }

    private static bool StaticCircleIndexOffsetsMatch(int[]? existingOffsets, int[]? newOffsets)
    {
        if (ReferenceEquals(existingOffsets, newOffsets))
        {
            return true;
        }

        if (existingOffsets is null || newOffsets is null)
        {
            return false;
        }

        if (existingOffsets.Length != newOffsets.Length)
        {
            return false;
        }

        for (var i = 0; i < existingOffsets.Length; i++)
        {
            if (existingOffsets[i] != newOffsets[i])
            {
                return false;
            }
        }

        return true;
    }
}

