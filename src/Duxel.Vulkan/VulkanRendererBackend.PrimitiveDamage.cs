using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Vulkan;

public sealed unsafe partial class VulkanRendererBackend
{
    // A draw command can batch a moving thumb with hundreds of stationary marks.
    // Its aggregate bounds are a culling bound, not the bounds of every edit.
    private bool TryGetPrimitiveDamage(DamageCommand before, DamageCommand after, out UiRect damage)
    {
        damage = default;
        var kind = after.Command.Kind;
        if (kind is not (UiDrawCommandKind.RectFilledPrimitives or UiDrawCommandKind.CircleFilledPrimitives)
            || (kind == UiDrawCommandKind.RectFilledPrimitives && after.Command.VertexOffset > (uint)UiRectGeometryVariant.Border)
            || before.Key is not null || after.Key is not null
            || before.WidgetTransform is not null || after.WidgetTransform is not null
            || before.TextureRevision != after.TextureRevision
            || before.WhiteBackdropRevision != after.WhiteBackdropRevision
            || before.GeometryLength != after.GeometryLength
            || before.PaintMaskCount != after.PaintMaskCount
            || !before.PaintMasks.AsSpan(0, before.PaintMaskCount).SequenceEqual(after.PaintMasks.AsSpan(0, after.PaintMaskCount)))
            return false;
        var left = before.Command with
        {
            Bounds = default,
            HasBounds = false
        };
        var right = after.Command with
        {
            Bounds = default,
            HasBounds = false
        };
        if (!EquivalentDamageCommand(left, right, streamed: true))
            return false;

        return kind == UiDrawCommandKind.RectFilledPrimitives
            ? TryGetRectDamage(before, after, out damage)
            : TryGetCircleDamage(before, after, out damage);
    }

    private bool TryGetRectDamage(DamageCommand before, DamageCommand after, out UiRect damage)
    {
        damage = default;
        var oldRecords = MemoryMarshal.Cast<byte, UiRectFilledPrimitive>(before.Geometry.AsSpan(0, before.GeometryLength));
        var newRecords = MemoryMarshal.Cast<byte, UiRectFilledPrimitive>(after.Geometry.AsSpan(0, after.GeometryLength));
        ref readonly var command = ref after.Command;
        if (oldRecords.Length != command.ElementCount)
            return false;
        for (var i = 0; i < oldRecords.Length; i++)
        {
            ref readonly var oldRecord = ref oldRecords[i];
            ref readonly var newRecord = ref newRecords[i];
            if (oldRecord == newRecord)
                continue;
            // Negative records encode affine/mask data rather than rectangles.
            if (oldRecord.BorderThickness < 0 || newRecord.BorderThickness < 0)
                return false;
            if (command.VertexOffset == (uint)UiRectGeometryVariant.Filled
                && TryGetFillEdgeDamage(oldRecord, newRecord, out var edge))
                IncludePrimitiveBounds(ref damage, edge, in command);
            else
            {
                IncludePrimitiveBounds(ref damage, oldRecord.Rect, in command);
                IncludePrimitiveBounds(ref damage, newRecord.Rect, in command);
            }
        }
        return true;
    }

    private bool TryGetCircleDamage(DamageCommand before, DamageCommand after, out UiRect damage)
    {
        damage = default;
        var oldRecords = MemoryMarshal.Cast<byte, UiCircleFilledPrimitive>(before.Geometry.AsSpan(0, before.GeometryLength));
        var newRecords = MemoryMarshal.Cast<byte, UiCircleFilledPrimitive>(after.Geometry.AsSpan(0, after.GeometryLength));
        ref readonly var command = ref after.Command;
        if (oldRecords.Length != command.ElementCount)
            return false;
        for (var i = 0; i < oldRecords.Length; i++)
        {
            if (oldRecords[i] == newRecords[i])
                continue;
            IncludePrimitiveBounds(ref damage, CircleBounds(oldRecords[i]), in command);
            IncludePrimitiveBounds(ref damage, CircleBounds(newRecords[i]), in command);
        }
        return true;

        static UiRect CircleBounds(UiCircleFilledPrimitive circle)
            => new(circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius, circle.Radius * 2, circle.Radius * 2);
    }

    private void IncludePrimitiveBounds(ref UiRect damage, UiRect bounds, in UiDrawCommand command)
    {
        var clip = command.ClipRect;
        var x = Math.Max(bounds.X, clip.X);
        var y = Math.Max(bounds.Y, clip.Y);
        var endX = Math.Min(bounds.X + bounds.Width, clip.X + clip.Width);
        var endY = Math.Min(bounds.Y + bounds.Height, clip.Y + clip.Height);
        if (endX <= x || endY <= y)
            return;
        var pixels = new UiRect((x + command.Translation.X - _damagePixelOffset.X) * _damagePixelScale.X - 2,
            (y + command.Translation.Y - _damagePixelOffset.Y) * _damagePixelScale.Y - 2,
            (endX - x) * _damagePixelScale.X + 4, (endY - y) * _damagePixelScale.Y + 4);
        IncludeDamageBounds(ref damage, pixels);
    }

    private static bool TryGetFillEdgeDamage(UiRectFilledPrimitive before, UiRectFilledPrimitive after, out UiRect damage)
    {
        damage = default;
        if (before.Color != after.Color || before.BorderColor != after.BorderColor
            || before.CornerRadius != after.CornerRadius || before.CornerRadius < 0
            || before.BorderThickness != 0 || after.BorderThickness != 0)
            return false;
        var a = before.Rect;
        var b = after.Rect;
        var radius = before.CornerRadius;
        // Radius clamping changes all corners for very small rectangles.
        if (Math.Min(Math.Min(a.Width, a.Height), Math.Min(b.Width, b.Height)) < radius * 2)
            return false;
        if (a.X == b.X && a.Y == b.Y && a.Height == b.Height && a.Width != b.Width)
        {
            var x = a.X + Math.Min(a.Width, b.Width) - radius;
            damage = new(x, a.Y, Math.Max(a.Width, b.Width) - Math.Min(a.Width, b.Width) + radius, a.Height);
            return true;
        }
        if (a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height != b.Height)
        {
            var y = a.Y + Math.Min(a.Height, b.Height) - radius;
            damage = new(a.X, y, a.Width, Math.Max(a.Height, b.Height) - Math.Min(a.Height, b.Height) + radius);
            return true;
        }
        return false;
    }
}
