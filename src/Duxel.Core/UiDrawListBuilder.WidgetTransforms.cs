using System.Buffers;
using System.Numerics;

namespace Duxel.Core;

internal sealed record UiWidgetDrawTransform(Matrix3x2 ToParent, UiLayer3D? SourceScene, UiRect ParentClip);

public sealed partial class UiDrawListBuilder
{
    internal void AppendTransformedWidget(UiDrawListBuilder child, Matrix3x2 matrix, UiVector2 display, UiRect parentClip)
    {
        if (!IsPresentationPass)
        {
            child.DiscardDeferred();
            return;
        }
        Flush();
        child.Flush();
        while (child._drawLists.Count != 0)
        {
            var original = child._drawLists[0];
            var prior = original.Scene3D?.WidgetTransform;
            var combined = (prior?.ToParent ?? Matrix3x2.Identity) * matrix;
            var basis = (prior is null ? original.Scene3D : prior.SourceScene) ?? _scene3D;
            var viewport = basis?.Viewport ?? new UiRect(0, 0, display.X, display.Y);
            var clipMatrix = UiWidgetAffineLayout.ToClipMatrix(combined, viewport);
            var scene = (basis ?? new UiLayer3D(Matrix4x4.Identity, false)) with
            {
                Transform = clipMatrix * (basis?.GetEffectiveTransform(new(0, 0, display.X, display.Y)) ?? Matrix4x4.Identity),
                PlaneModel = null,
                IsScreenSpacePresentation = false,
                WidgetTransform = new(combined, basis, parentClip)
            };
            var transformed = ApplyWidgetParentClips(original, combined, parentClip) with
            {
                Scene3D = scene
            };
            child._drawLists.RemoveAt(0);
            _drawLists.Add(transformed);
        }
    }

    private UiDrawList ApplyWidgetParentClips(UiDrawList source, Matrix3x2 matrix, UiRect parentClip)
    {
        // Every affine mask is a tagged shape record followed by six matrix coefficients.
        // Existing geometry and command ownership transfer without copying frame-owned vertices.
        var capacity = checked((source.RectFilledPrimitives?.Count ?? 0)
            + source.Commands.Count * 2 * (1 + _roundedClips.Count + (_circleCutoutRadius > 0 ? 1 : 0)));
        var records = ArrayPool<UiRectFilledPrimitive>.Shared.Rent(Math.Max(1, capacity));
        var count = source.RectFilledPrimitives?.Count ?? 0;
        source.RectFilledPrimitives?.AsSpan().CopyTo(records);
        var ownsCommands = source.OwnsBuffers;
        var commands = ownsCommands ? source.Commands : UiPooledList<UiDrawCommand>.RentAndCopy(source.Commands.AsSpan());
        var complete = false;
        try
        {
            uint Add(uint head, UiRect bounds, float radius, float kind, Matrix3x2 toParent)
            {
                var index = count;
                var parent = head == 0 ? 0u : unchecked((uint)(checked((int)head - 1) - index));
                records[count++] = new(bounds, new(parent), radius, kind);
                records[count++] = new(new(toParent.M11, toParent.M12, toParent.M21, toParent.M22),
                    default, toParent.M31, toParent.M32);
                return checked((uint)index + 1);
            }
            var previousHead = uint.MaxValue;
            var previousTranslation = new UiVector2(float.NaN, float.NaN);
            var cachedHead = 0u;
            var writable = commands.AsWritableSpan();
            for (var index = 0; index < writable.Length; index++)
            {
                ref var command = ref writable[index];
                if (command.Callback is not null)
                    throw new NotSupportedException("Widget transforms cannot wrap external draw callbacks.");
                if (command.ElementCount == 0)
                    continue;
                var originalHead = command.CircleCutoutIndexPlusOne;
                if (originalHead != previousHead || command.Translation != previousTranslation)
                {
                    var mapping = Matrix3x2.CreateTranslation(command.Translation.X, command.Translation.Y) * matrix;
                    cachedHead = Add(originalHead, parentClip, 0, -3, mapping);
                    foreach (var (bounds, radius) in _roundedClips)
                        cachedHead = Add(cachedHead, bounds, radius, -4, mapping);
                    if (_circleCutoutRadius > 0)
                        cachedHead = Add(cachedHead, new(_circleCutoutCenter.X, _circleCutoutCenter.Y, _circleCutoutRadius, 0),
                            _circleCutoutRadius, -5, mapping);
                    previousHead = originalHead;
                    previousTranslation = command.Translation;
                }
                command = command with
                {
                    CircleCutoutIndexPlusOne = cachedHead
                };
            }
            var result = source with
            {
                Vertices = source.OwnsBuffers ? source.Vertices : UiPooledList<UiDrawVertex>.RentAndCopy(source.Vertices.AsSpan()),
                Indices = source.OwnsBuffers ? source.Indices : UiPooledList<uint>.RentAndCopy(source.Indices.AsSpan()),
                CircleFilledPrimitives = source.OwnsBuffers || source.CircleFilledPrimitives is null ? source.CircleFilledPrimitives
                    : UiPooledList<UiCircleFilledPrimitive>.RentAndCopy(source.CircleFilledPrimitives.AsSpan()),
                Commands = commands,
                RectFilledPrimitives = new UiPooledList<UiRectFilledPrimitive>(records, count, true),
                OwnsBuffers = true,
                StaticGeometryStamp = 0,
                CommandStateStamp = UiDrawList.ComputeCommandStateStamp(writable),
                CommandScheduleStamp = UiDrawList.CommandScheduleStampEnabled ? UiDrawList.ComputeCommandScheduleStamp(writable) : 0
            };
            if (source.OwnsBuffers)
                source.RectFilledPrimitives?.Return();
            complete = true;
            return result;
        }
        finally
        {
            if (!complete)
            {
                ArrayPool<UiRectFilledPrimitive>.Shared.Return(records);
                if (!ownsCommands)
                    commands.Return();
            }
        }
    }
}
