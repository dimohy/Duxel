namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
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
        ulong contentHash,
        in StaticGeometryShape shape)
    {
        return existing.ContentHash == contentHash
            && existing.VertexCount == shape.VertexCount
            && existing.IndexCount == shape.IndexCount
            && existing.PrimitiveInstanceBaseCount == shape.PrimitiveInstanceBaseCount
            && existing.RectPrimitiveCount == shape.RectPrimitiveCount
            && existing.CirclePrimitiveCount == shape.CirclePrimitiveCount
            && existing.HasExpandedPrimitiveGeometry == shape.HasExpandedPrimitiveGeometry;
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
