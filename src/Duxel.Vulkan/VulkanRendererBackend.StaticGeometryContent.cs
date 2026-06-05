using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
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

    private StaticGeometryContent CreateStaticGeometryContent(
        string staticTag,
        UiDrawList drawList,
        bool requiresContentHash,
        bool hasExisting,
        in StaticGeometryBuffer existing)
    {
        var vertexCount = drawList.Vertices.Count;
        var indexCount = drawList.Indices.Count;
        var rectPrimitiveCount = drawList.RectFilledPrimitives?.Count ?? 0;
        var circlePrimitiveCount = drawList.CircleFilledPrimitives?.Count ?? 0;
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

        var expandPrimitiveGeometry = primitiveTriangleDecision.Expand;
        var primitiveTriangleLayout = expandPrimitiveGeometry
            ? CreateStaticPrimitiveTriangleLayout(indexCount, rects, circles)
            : default;
        var primitiveInstanceBaseCount = ShouldReserveStaticPrimitiveSentinel(drawList, expandPrimitiveGeometry)
            ? StaticPrimitiveBufferSentinelCount
            : 0;
        var shape = CreateStaticGeometryShape(
            vertexCount,
            indexCount,
            rectPrimitiveCount,
            circlePrimitiveCount,
            primitiveInstanceBaseCount,
            expandPrimitiveGeometry,
            in primitiveTriangleLayout);

        return new StaticGeometryContent(
            contentHash,
            vertices,
            indices,
            rects,
            circles,
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
}
