using System.Collections.Generic;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
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
}
