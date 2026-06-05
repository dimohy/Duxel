using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private const ulong StaticPrimitiveTriangleAutoMaxByteExpansionRatio = 32UL;
    private const int StaticPrimitiveTriangleAutoMutationSuppressFrames = 30;

    private readonly Dictionary<string, int> _staticPrimitiveTriangleAutoSuppressUntilFrame = new(StringComparer.Ordinal);
    private int _profileStaticPrimitiveTriangleExpandedListCount;
    private int _profileStaticPrimitiveTriangleExpandedPrimitiveCount;
    private int _profileStaticPrimitiveTriangleForcedListCount;
    private int _profileStaticPrimitiveTriangleAutoSkippedListCount;
    private int _profileStaticPrimitiveTriangleAutoSkippedPrimitiveCount;
    private int _profileStaticPrimitiveTriangleAutoSkippedMutationListCount;
    private ulong _profileStaticPrimitiveTriangleExpandedBytes;
    private ulong _profileStaticPrimitiveTriangleAutoSkippedBytes;

    private readonly record struct StaticPrimitiveTriangleDecision(
        bool Expand,
        bool Forced,
        bool AutoSkippedByByteCost,
        bool AutoSkippedByMutation,
        int PrimitiveCount,
        ulong ExpandedBytes);

    private void ResetStaticPrimitiveTriangleProfileCounters()
    {
        _profileStaticPrimitiveTriangleExpandedListCount = 0;
        _profileStaticPrimitiveTriangleExpandedPrimitiveCount = 0;
        _profileStaticPrimitiveTriangleForcedListCount = 0;
        _profileStaticPrimitiveTriangleAutoSkippedListCount = 0;
        _profileStaticPrimitiveTriangleAutoSkippedPrimitiveCount = 0;
        _profileStaticPrimitiveTriangleAutoSkippedMutationListCount = 0;
        _profileStaticPrimitiveTriangleExpandedBytes = 0UL;
        _profileStaticPrimitiveTriangleAutoSkippedBytes = 0UL;
    }

    private void RemoveStaticPrimitiveTrianglePolicyState(string staticTag)
    {
        _staticPrimitiveTriangleAutoSuppressUntilFrame.Remove(staticTag);
    }

    private void ClearStaticPrimitiveTrianglePolicyState()
    {
        _staticPrimitiveTriangleAutoSuppressUntilFrame.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldExpandStaticPrimitiveGeometry(UiDrawList drawList)
    {
        var rects = drawList.RectFilledPrimitives is null
            ? ReadOnlySpan<UiRectFilledPrimitive>.Empty
            : drawList.RectFilledPrimitives.AsSpan();
        var circles = drawList.CircleFilledPrimitives is null
            ? ReadOnlySpan<UiCircleFilledPrimitive>.Empty
            : drawList.CircleFilledPrimitives.AsSpan();

        return GetStaticPrimitiveTriangleDecision(rects, circles, suppressForMutation: false).Expand;
    }

    private StaticPrimitiveTriangleDecision GetStaticPrimitiveTriangleDecision(
        ReadOnlySpan<UiRectFilledPrimitive> rectPrimitives,
        ReadOnlySpan<UiCircleFilledPrimitive> circlePrimitives,
        bool suppressForMutation)
    {
        if (!_staticPrimitiveTrianglesEnabled || !_triangleColorPipelineEnabled)
        {
            return default;
        }

        var primitiveCount = checked(rectPrimitives.Length + circlePrimitives.Length);
        if (primitiveCount is 0)
        {
            return default;
        }

        if (_staticPrimitiveTriangleMode == VulkanStaticPrimitiveTriangleMode.Enabled)
        {
            var forcedExpandedBytes = _profilingEnabled
                ? EstimateExpandedStaticPrimitiveGeometryBytes(rectPrimitives, circlePrimitives)
                : 0UL;
            return new StaticPrimitiveTriangleDecision(
                Expand: true,
                Forced: true,
                AutoSkippedByByteCost: false,
                AutoSkippedByMutation: false,
                PrimitiveCount: primitiveCount,
                ExpandedBytes: forcedExpandedBytes);
        }

        var primitiveBytes = checked((ulong)primitiveCount * (ulong)sizeof(PrimitiveInstance));
        var expandedBytes = EstimateExpandedStaticPrimitiveGeometryBytes(rectPrimitives, circlePrimitives);
        var maxAutoExpandedBytes = primitiveBytes > ulong.MaxValue / StaticPrimitiveTriangleAutoMaxByteExpansionRatio
            ? ulong.MaxValue
            : primitiveBytes * StaticPrimitiveTriangleAutoMaxByteExpansionRatio;
        var skipByByteCost = expandedBytes > maxAutoExpandedBytes;
        var expand = !skipByByteCost && !suppressForMutation;
        return new StaticPrimitiveTriangleDecision(
            Expand: expand,
            Forced: false,
            AutoSkippedByByteCost: skipByByteCost,
            AutoSkippedByMutation: !skipByByteCost && suppressForMutation,
            PrimitiveCount: primitiveCount,
            ExpandedBytes: expandedBytes);
    }

    private bool ShouldSuppressStaticPrimitiveTriangleAutoForMutation(string staticTag, bool contentChanging)
    {
        if (_staticPrimitiveTriangleMode != VulkanStaticPrimitiveTriangleMode.Auto)
        {
            return false;
        }

        if (contentChanging)
        {
            _staticPrimitiveTriangleAutoSuppressUntilFrame[staticTag] = checked(_frameIndex + StaticPrimitiveTriangleAutoMutationSuppressFrames);
            return true;
        }

        return _staticPrimitiveTriangleAutoSuppressUntilFrame.TryGetValue(staticTag, out var suppressUntilFrame)
            && suppressUntilFrame >= _frameIndex;
    }

    private void RecordStaticPrimitiveTriangleDecision(in StaticPrimitiveTriangleDecision decision)
    {
        if (decision.PrimitiveCount is 0)
        {
            return;
        }

        if (decision.Expand)
        {
            _profileStaticPrimitiveTriangleExpandedListCount++;
            _profileStaticPrimitiveTriangleExpandedPrimitiveCount = checked(_profileStaticPrimitiveTriangleExpandedPrimitiveCount + decision.PrimitiveCount);
            _profileStaticPrimitiveTriangleExpandedBytes = checked(_profileStaticPrimitiveTriangleExpandedBytes + decision.ExpandedBytes);
            if (decision.Forced)
            {
                _profileStaticPrimitiveTriangleForcedListCount++;
            }
        }
        else if (decision.AutoSkippedByByteCost)
        {
            _profileStaticPrimitiveTriangleAutoSkippedListCount++;
            _profileStaticPrimitiveTriangleAutoSkippedPrimitiveCount = checked(_profileStaticPrimitiveTriangleAutoSkippedPrimitiveCount + decision.PrimitiveCount);
            _profileStaticPrimitiveTriangleAutoSkippedBytes = checked(_profileStaticPrimitiveTriangleAutoSkippedBytes + decision.ExpandedBytes);
        }
        else if (decision.AutoSkippedByMutation)
        {
            _profileStaticPrimitiveTriangleAutoSkippedListCount++;
            _profileStaticPrimitiveTriangleAutoSkippedPrimitiveCount = checked(_profileStaticPrimitiveTriangleAutoSkippedPrimitiveCount + decision.PrimitiveCount);
            _profileStaticPrimitiveTriangleAutoSkippedMutationListCount++;
            _profileStaticPrimitiveTriangleAutoSkippedBytes = checked(_profileStaticPrimitiveTriangleAutoSkippedBytes + decision.ExpandedBytes);
        }
    }

    private static ulong EstimateExpandedStaticPrimitiveGeometryBytes(
        ReadOnlySpan<UiRectFilledPrimitive> rectPrimitives,
        ReadOnlySpan<UiCircleFilledPrimitive> circlePrimitives)
    {
        var vertexCount = checked((ulong)rectPrimitives.Length * 4UL);
        var indexCount = checked((ulong)rectPrimitives.Length * 6UL);
        for (var i = 0; i < circlePrimitives.Length; i++)
        {
            var segmentCount = (ulong)Math.Max(3, circlePrimitives[i].Segments);
            vertexCount = checked(vertexCount + segmentCount + 1UL);
            indexCount = checked(indexCount + segmentCount * 3UL);
        }

        return checked(
            vertexCount * (ulong)sizeof(UiVertex)
            + indexCount * sizeof(uint));
    }
}
