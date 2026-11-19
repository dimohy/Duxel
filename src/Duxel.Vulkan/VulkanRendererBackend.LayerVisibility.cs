using System;
using System.Numerics;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    private int _frameLayerTransformPushes, _frameCulledLayerLists;
    private int _frameLayerBoundsCommandVisits;
    private readonly bool _layerBoundsReuseEnabled = Environment.GetEnvironmentVariable("DUXEL_VK_LAYER_BOUNDS_REUSE") != "0";
    private readonly bool _axisAlignedLayerCullingEnabled = ParseAxisAlignedLayerCulling();
    private static bool ParseAxisAlignedLayerCulling()
        => Environment.GetEnvironmentVariable("DUXEL_VK_AXIS_ALIGNED_CULLING") switch
        {
            null or "" or "1" => true,
            "0" => false,
            _ => throw new ArgumentException("DUXEL_VK_AXIS_ALIGNED_CULLING must be 0 or 1."),
        };
    private enum LayerVisibility { Intersecting, Inside, Outside }

    private LayerVisibilityEntry[] _layerVisibility = [];
    private uint _layerVisibilityEpoch;
    private struct LayerVisibilityEntry
    {
        public uint Epoch;
        public LayerVisibility Visibility;
        public LayerViewportTransform View;
        public ulong BoundsStamp;
        public UiRect Bounds;
        public bool HasBounds;
    }

    private ref LayerVisibilityEntry GetCachedLayerVisibility(int index, UiDrawList list, in CommandFrameContext context)
    {
        ref var entry = ref _layerVisibility[index];
        if (entry.Epoch != _layerVisibilityEpoch)
        {
            entry.Epoch = _layerVisibilityEpoch;
            entry.View = GetLayerViewportTransform(list.Scene3D, in context);
            var stamp = _layerBoundsReuseEnabled ? list.CommandStateStamp : 0;
            if (stamp == 0 || entry.BoundsStamp != stamp)
            {
                entry.HasBounds = TryGetLayerBounds(list, out entry.Bounds);
                entry.BoundsStamp = stamp;
            }
            var model = entry.View.PlaneModel;
            entry.Visibility = entry.HasBounds
                ? ClassifyLayerBounds(entry.Bounds, model?.Translation ?? default, in context,
                    entry.View.Transform, entry.View.PixelViewport, depthOffset: model?.DepthOffset ?? 0,
                    axisAlignedCulling: _axisAlignedLayerCullingEnabled)
                : LayerVisibility.Intersecting;
            if (entry.Visibility == LayerVisibility.Outside) _frameCulledLayerLists++;
        }
        return ref entry;
    }

    private void PrepareLayerVisibilityFrame(int count)
    {
        _frameLayerBoundsCommandVisits = 0;
        if (_layerVisibility.Length != count) _layerVisibility = new LayerVisibilityEntry[count];
        _layerVisibilityEpoch++;
        if (_layerVisibilityEpoch == 0)
        {
            Array.Clear(_layerVisibility);
            _layerVisibilityEpoch = 1;
        }
    }

    private bool TryGetLayerBounds(UiDrawList list, out UiRect combined)
    {
        combined = default;
        var minX = float.PositiveInfinity; var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity; var maxY = float.NegativeInfinity;
        if (list.Commands.Count == 0) return false;
        foreach (ref readonly var command in list.Commands.AsSpan())
        {
            _frameLayerBoundsCommandVisits++;
            if (command.Callback is not null) return false;
            if (command.ElementCount == 0) continue;
            var bounds = command.Bounds;
            if (!command.HasBounds || bounds.Width < 0 || bounds.Height < 0)
                return false;
            var x = bounds.X + command.Translation.X; var y = bounds.Y + command.Translation.Y;
            var right = x + bounds.Width; var bottom = y + bounds.Height;
            if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(right) || !float.IsFinite(bottom))
                return false;
            minX = MathF.Min(minX, x); minY = MathF.Min(minY, y);
            maxX = MathF.Max(maxX, right); maxY = MathF.Max(maxY, bottom);
        }
        combined = new(minX, minY, maxX - minX, maxY - minY);
        return float.IsFinite(combined.Width) && float.IsFinite(combined.Height);
    }

    // Reject only when every corner lies beyond the same homogeneous clip plane.
    // This remains conservative for perspective, rotation and near-plane crossings.
    // Geometry residency/upload is independent of visibility so returning to a
    // previously hidden layer does not recreate its buffers.
    private bool IsOutsideLayerFrustum(
        in UiDrawCommand command, in CommandFrameContext context, in CommandPipelineState pipeline)
    {
        if (!command.HasBounds || command.Callback is not null) return false;
        var bounds = command.Bounds;
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y)
            || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height)
            || bounds.Width < 0 || bounds.Height < 0) return false;

        var translation = new UiVector2(command.Translation.X + pipeline.LayerModelTranslation.X,
            command.Translation.Y + pipeline.LayerModelTranslation.Y);
        return ClassifyLayerBounds(bounds, translation, in context, in pipeline.LayerTransform, pipeline.ViewportPixels,
            classifyInside: false, depthOffset: pipeline.LayerModelDepth,
            axisAlignedCulling: _axisAlignedLayerCullingEnabled) == LayerVisibility.Outside;
    }

    private static LayerVisibility ClassifyLayerBounds(UiRect bounds, UiVector2 translation,
        in CommandFrameContext context, in Matrix4x4 transform, UiRect viewportPixels, bool classifyInside = true,
        float depthOffset = 0, bool axisAlignedCulling = true)
    {
        if (!float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height)) return LayerVisibility.Intersecting;
        var x = (bounds.X + translation.X) * context.ScaleX + context.TranslateX;
        var y = (bounds.Y + translation.Y) * context.ScaleY + context.TranslateY;
        var right = x + bounds.Width * context.ScaleX;
        var bottom = y + bounds.Height * context.ScaleY;
        // Keep a two-pixel guard band for antialiasing and floating-point error.
        var guardX = 4f / MathF.Max(1f, viewportPixels.Width);
        var guardY = 4f / MathF.Max(1f, viewportPixels.Height);
        if (axisAlignedCulling && transform.M12 == 0 && transform.M13 == 0 && transform.M14 == 0
            && transform.M21 == 0 && transform.M23 == 0 && transform.M24 == 0
            && transform.M31 == 0 && transform.M32 == 0 && transform.M33 == 1 && transform.M34 == 0
            && transform.M44 == 1)
        {
            // An axis-aligned affine plane has constant clip W/Z. Its extrema
            // lie at two opposite endpoints. Keep Transform's floating-point
            // evaluation so clip-edge decisions match the general path.
            var first = Vector4.Transform(new Vector4(x, y, 0, 1), transform);
            var last = Vector4.Transform(new Vector4(right, bottom, 0, 1), transform);
            var x0 = first.X; var x1 = last.X;
            var y0 = first.Y; var y1 = last.Y;
            var z = first.Z + depthOffset;
            if (!float.IsFinite(x0) || !float.IsFinite(x1) || !float.IsFinite(y0)
                || !float.IsFinite(y1) || !float.IsFinite(z)) return LayerVisibility.Intersecting;
            var minimumX = MathF.Min(x0, x1); var maximumX = MathF.Max(x0, x1);
            var minimumY = MathF.Min(y0, y1); var maximumY = MathF.Max(y0, y1);
            var marginX = guardX + 0.00001f; var marginY = guardY + 0.00001f;
            if (maximumX < -1f - marginX || minimumX > 1f + marginX
                || maximumY < -1f - marginY || minimumY > 1f + marginY
                || z < -0.00001f || z > 1f + 0.00001f) return LayerVisibility.Outside;
            return classifyInside && minimumX >= -1f - marginX && maximumX <= 1f + marginX
                && minimumY >= -1f - marginY && maximumY <= 1f + marginY
                ? LayerVisibility.Inside : LayerVisibility.Intersecting;
        }
        var outside = 63;
        var anyOutside = 0;
        for (var corner = 0; corner < 4; corner++)
        {
            var clip = Vector4.Transform(new Vector4(
                (corner & 1) == 0 ? x : right, (corner & 2) == 0 ? y : bottom, 0, 1),
                transform);
            clip.Z += depthOffset * clip.W;
            if (!float.IsFinite(clip.X) || !float.IsFinite(clip.Y)
                || !float.IsFinite(clip.Z) || !float.IsFinite(clip.W)) return LayerVisibility.Intersecting;
            var marginX = MathF.Abs(clip.W) * guardX + 0.00001f;
            var marginY = MathF.Abs(clip.W) * guardY + 0.00001f;
            var planes = (clip.X < -clip.W - marginX ? 1 : 0)
                | (clip.X > clip.W + marginX ? 2 : 0)
                | (clip.Y < -clip.W - marginY ? 4 : 0)
                | (clip.Y > clip.W + marginY ? 8 : 0)
                | (clip.Z < -0.00001f ? 16 : 0)
                | (clip.Z > clip.W + 0.00001f ? 32 : 0);
            outside &= planes;
            anyOutside |= planes;
            if (!classifyInside && outside == 0) return LayerVisibility.Intersecting;
        }
        return outside != 0 ? LayerVisibility.Outside : anyOutside == 0 ? LayerVisibility.Inside : LayerVisibility.Intersecting;
    }
}
