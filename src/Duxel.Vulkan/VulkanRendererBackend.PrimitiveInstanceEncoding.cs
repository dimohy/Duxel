using System;
using System.Runtime.CompilerServices;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private const uint PrimitiveRectPayloadFlag = 0x80000000u;
    private const uint SolidTrianglePayloadFlag = 0xFFFFFFFFu;
    private const int StaticPrimitiveBufferSentinelCount = 1;
    private const int DynamicPrimitiveBufferSentinelCount = 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PrimitiveInstance CreateRectPrimitiveInstance(in UiRectFilledPrimitive primitive)
    {
        var rect = primitive.Rect;
        return new PrimitiveInstance
        {
            DataX = rect.X,
            DataY = rect.Y,
            DataZ = rect.Width,
            Payload = PrimitiveRectPayloadFlag | BitConverter.SingleToUInt32Bits(rect.Height),
            Color = primitive.Color.Rgba,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PrimitiveInstance CreateCirclePrimitiveInstance(in UiCircleFilledPrimitive primitive)
    {
        return new PrimitiveInstance
        {
            DataX = primitive.Center.X,
            DataY = primitive.Center.Y,
            DataZ = primitive.Radius,
            Payload = (uint)primitive.Segments,
            Color = primitive.Color.Rgba,
        };
    }

    private static PrimitiveInstance CreateSolidTriangleModePrimitiveInstance()
    {
        return new PrimitiveInstance
        {
            Payload = SolidTrianglePayloadFlag,
            Color = uint.MaxValue,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldReserveDynamicPrimitiveSentinel()
    {
        return _solidUnifiedPipelineEnabled && _triangleColorPipelineEnabled;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldReserveStaticPrimitiveSentinel(UiDrawList drawList, bool expandsPrimitiveGeometry)
    {
        return _solidUnifiedPipelineEnabled
            && _solidUnifiedStaticEnabled
            && _triangleColorPipelineEnabled
            && ((drawList.Vertices.Count > 0 && drawList.Indices.Count > 0)
                || expandsPrimitiveGeometry);
    }
}
