namespace Duxel.Core;

/// <summary>Intersects local paint with an analytic rounded rectangle. Scopes must be disposed in reverse order.</summary>
public readonly struct UiRoundedClipScope : IDisposable
{
    private readonly UiDrawListBuilder? _builder;
    private readonly int _depth;
    internal UiRoundedClipScope(UiDrawListBuilder builder, int depth)
    {
        _builder = builder;
        _depth = depth;
    }
    public void Dispose() => _builder?.EndRoundedClip(_depth);
}

public sealed partial class UiDrawListBuilder
{
    // Auxiliary records use the existing 32-byte primitive stream. Negative thickness tags a mask;
    // Color stores a signed relative parent index, preserving links across whole-list rebasing.
    private readonly List<(UiRect Bounds, float Radius)> _roundedClips = [];
    private PooledBuffer<UiRectFilledPrimitive>? _roundedClipBuffer;
    private uint _roundedClipRecord;
    private bool HasPaintClip => _circleCutoutRadius > 0 || _roundedClips.Count != 0;

    public UiRoundedClipScope RoundedClip(UiRect bounds, float radius)
    {
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(bounds.Width)
            || !float.IsFinite(bounds.Height) || !float.IsFinite(bounds.X + bounds.Width)
            || !float.IsFinite(bounds.Y + bounds.Height) || bounds.Width < 0 || bounds.Height < 0
            || !float.IsFinite(radius) || radius < 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        _roundedClips.Add((bounds, MathF.Min(radius, MathF.Min(bounds.Width, bounds.Height) / 2)));
        PushClipRect(bounds, intersectWithCurrentClipRect: true);
        return new(this, _roundedClips.Count);
    }

    internal void EndRoundedClip(int depth)
    {
        if (_roundedClips.Count != depth)
            throw new InvalidOperationException("Rounded clip scopes must close in reverse order.");
        _roundedClips.RemoveAt(depth - 1);
        PopClipRect();
        _roundedClipRecord = 0;
    }

    private static int MaskParent(ReadOnlySpan<UiRectFilledPrimitive> records, int index)
    {
        var record = records[index];
        if (record.BorderThickness >= 0 || record.Color.Rgba == 0)
            return -1;
        var delta = unchecked((int)record.Color.Rgba);
        var parent = checked(index + delta);
        if (delta >= 0 || parent < 0)
            throw new InvalidOperationException("Paint mask parent must precede its child.");
        return parent;
    }

    private static bool ContainsMask(ReadOnlySpan<UiRectFilledPrimitive> records, uint head, UiRect bounds, float radius, bool circle)
    {
        for (var index = checked((int)head - 1); index >= 0; index = MaskParent(records, index))
        {
            var item = records[index];
            if (circle ? item.BorderThickness != -1 && item.Rect.X == bounds.X && item.Rect.Y == bounds.Y && item.Rect.Width == radius
                : item.BorderThickness == -1 && item.Rect == bounds && item.CornerRadius == radius)
                return true;
        }
        return false;
    }

    private UiDrawCommand IntersectRoundedCommandClip(UiDrawCommand command)
    {
        var clip = command.ClipRect;
        foreach (var (bounds, _) in _roundedClips)
            clip = Intersect(clip, bounds with
            {
                X = bounds.X - command.Translation.X,
                Y = bounds.Y - command.Translation.Y
            });
        return command with
        {
            ClipRect = clip
        };
    }

    private uint ComposePaintClips(PooledBuffer<UiRectFilledPrimitive> records, UiDrawCommand command)
    {
        var head = command.CircleCutoutIndexPlusOne;
        uint Add(UiRect bounds, float radius, bool circle)
        {
            bounds = bounds with
            {
                X = bounds.X - command.Translation.X,
                Y = bounds.Y - command.Translation.Y
            };
            if (ContainsMask(records.AsSpan(), head, bounds, radius, circle))
                return head;
            var next = records.Count;
            var parent = head == 0 ? 0u : unchecked((uint)(checked((int)head - 1) - next));
            records.Add(new(bounds, new UiColor(parent), radius, circle ? -2 : -1));
            return checked((uint)records.Count);
        }
        if (_circleCutoutRadius > 0)
            head = Add(new(_circleCutoutCenter.X, _circleCutoutCenter.Y, _circleCutoutRadius, 0), _circleCutoutRadius, true);
        foreach (var (bounds, radius) in _roundedClips)
            head = Add(bounds, radius, false);
        return head;
    }

    private uint GetRoundedClipRecord()
    {
        if (ReferenceEquals(_roundedClipBuffer, _rectFilledPrimitives) && _roundedClipRecord > 0
            && _roundedClipRecord <= _rectFilledPrimitives.Count)
        {
            var span = _rectFilledPrimitives.AsSpan();
            var matches = _circleCutoutRadius == 0 || ContainsMask(span, _roundedClipRecord,
                new(_circleCutoutCenter.X, _circleCutoutCenter.Y, _circleCutoutRadius, 0), _circleCutoutRadius, true);
            for (var index = (int)_roundedClipRecord - 1; index >= 0; index = MaskParent(span, index))
            {
                var record = span[index];
                var allowed = record.BorderThickness != -1 && _circleCutoutRadius > 0
                    && record.Rect == new UiRect(_circleCutoutCenter.X, _circleCutoutCenter.Y, _circleCutoutRadius, 0);
                foreach (var (bounds, radius) in _roundedClips)
                    allowed |= record.BorderThickness == -1 && record.Rect == bounds && record.CornerRadius == radius;
                matches &= allowed;
            }
            foreach (var (bounds, radius) in _roundedClips)
                matches &= ContainsMask(span, _roundedClipRecord, bounds, radius, false);
            if (matches)
                return _roundedClipRecord;
        }
        _roundedClipRecord = ComposePaintClips(_rectFilledPrimitives, default);
        _roundedClipBuffer = _rectFilledPrimitives;
        return _roundedClipRecord;
    }

    private UiDrawList ApplyRoundedClipsToReference(UiDrawList source)
    {
        var commands = source.Commands.AsSpan().ToArray();
        var records = new PooledBuffer<UiRectFilledPrimitive>((source.RectFilledPrimitives?.Count ?? 0) + commands.Length, pooled: false);
        if (source.RectFilledPrimitives is { } original)
            records.AddRange(original);
        for (var index = 0; index < commands.Length; index++)
        {
            var command = IntersectRoundedCommandClip(commands[index]);
            if (command.Callback is not null)
                throw new NotSupportedException("Paint clips cannot wrap an external draw callback.");
            if (command.ElementCount != 0)
                commands[index] = command with
                {
                    CircleCutoutIndexPlusOne = ComposePaintClips(records, command)
                };
        }
        var result = source with
        {
            Vertices = UiPooledList<UiDrawVertex>.RentAndCopy(source.Vertices.AsSpan()),
            Indices = UiPooledList<uint>.RentAndCopy(source.Indices.AsSpan()),
            Commands = UiPooledList<UiDrawCommand>.RentAndCopy(commands.AsSpan()),
            RectFilledPrimitives = UiPooledList<UiRectFilledPrimitive>.RentAndCopy(records.AsSpan()),
            CircleFilledPrimitives = source.CircleFilledPrimitives is null ? null
                : UiPooledList<UiCircleFilledPrimitive>.RentAndCopy(source.CircleFilledPrimitives.AsSpan()),
            OwnsBuffers = true,
            StaticGeometryStamp = 0,
            CommandStateStamp = UiDrawList.ComputeCommandStateStamp(commands),
            CommandScheduleStamp = UiDrawList.CommandScheduleStampEnabled ? UiDrawList.ComputeCommandScheduleStamp(commands) : 0
        };
        return result with
        {
            StaticGeometryStamp = result.GetStaticGeometryStamp()
        };
    }
}

public readonly partial struct UiControlContext
{
    /// <summary>Clips paint only; interactive controls must also respect their layout allocation.</summary>
    public UiRoundedClipScope RoundedClip(UiRect bounds, float radius) => _ui.BeginRoundedClip(bounds, radius);
}

public sealed partial class UiImmediateContext
{
    internal UiRoundedClipScope BeginRoundedClip(UiRect bounds, float radius)
    {
        EnsureNotMeasuring();
        return _builder.RoundedClip(bounds, radius);
    }
}
