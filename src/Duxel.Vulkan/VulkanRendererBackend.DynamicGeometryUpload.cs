using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private readonly List<int> _dynamicDrawListIndices = new();

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void UploadDynamicGeometry(int frame, UiDrawData drawData, Dictionary<int, StaticGeometryBuffer> staticBindings)
    {
        var frameData = _frames[frame];
        var renderBuffers = frameData.RenderBuffers;

        var vertexDst = (byte*)renderBuffers.VertexMappedPtr;
        var indexDst = (byte*)renderBuffers.IndexMappedPtr;
        var primitiveDst = (PrimitiveInstance*)renderBuffers.PrimitiveMappedPtr;
        if (ShouldReserveDynamicPrimitiveSentinel())
        {
            primitiveDst[0] = CreateSolidTriangleModePrimitiveInstance();
            primitiveDst += DynamicPrimitiveBufferSentinelCount;
        }

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
            if (staticBindings.ContainsKey(listIndex))
            {
                continue;
            }

            var drawList = drawData.DrawLists[listIndex];
            if (drawList.RectFilledPrimitives is not null)
            {
                var primitives = drawList.RectFilledPrimitives.AsSpan();
                for (var i = 0; i < primitives.Length; i++)
                {
                    primitiveDst[i] = CreateRectPrimitiveInstance(in primitives[i]);
                }

                primitiveDst += primitives.Length;
            }

            if (drawList.CircleFilledPrimitives is not null)
            {
                var primitives = drawList.CircleFilledPrimitives.AsSpan();
                for (var i = 0; i < primitives.Length; i++)
                {
                    primitiveDst[i] = CreateCirclePrimitiveInstance(in primitives[i]);
                }

                primitiveDst += primitives.Length;
            }
        }
    }
}
