namespace Duxel.Core;

/// <summary>Subtracts one analytic circle from local paint; rectangular clipping still applies.</summary>
public readonly struct UiCircleCutoutScope : IDisposable
{
    private readonly UiDrawListBuilder? _builder;
    internal UiCircleCutoutScope(UiDrawListBuilder builder) => _builder = builder;
    public void Dispose() => _builder?.EndCircleCutout();
}

public sealed partial class UiDrawListBuilder
{
    private UiVector2 _circleCutoutCenter;
    private float _circleCutoutRadius;
    private PooledBuffer<UiRectFilledPrimitive>? _circleCutoutBuffer;
    private uint _circleCutoutRecord;

    /// <summary>Coordinates use the same local space as the geometry. Nested cutouts fail explicitly.</summary>
    public UiCircleCutoutScope CircleCutout(UiVector2 center, float radius)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(radius) || radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (_circleCutoutRadius > 0)
            throw new NotSupportedException("Nested circular cutouts are not supported.");
        _circleCutoutCenter = center;
        _circleCutoutRadius = radius;
        return new(this);
    }

    internal void EndCircleCutout() => _circleCutoutRadius = 0;

    private uint GetCircleCutoutRecord()
    {
        if (_roundedClips.Count != 0)
            return GetRoundedClipRecord();
        if (_circleCutoutRadius == 0)
            return 0;
        var record = new UiRectFilledPrimitive(new(_circleCutoutCenter.X, _circleCutoutCenter.Y, _circleCutoutRadius, 0), default);
        // Check the buffer and contents as flushing/channels may reuse storage and indices.
        if (ReferenceEquals(_circleCutoutBuffer, _rectFilledPrimitives)
            && _circleCutoutRecord > 0 && _circleCutoutRecord <= _rectFilledPrimitives.Count
            && _rectFilledPrimitives[(int)_circleCutoutRecord - 1] == record)
            return _circleCutoutRecord;
        _rectFilledPrimitives.Add(record);
        _circleCutoutBuffer = _rectFilledPrimitives;
        _circleCutoutRecord = checked((uint)_rectFilledPrimitives.Count);
        return _circleCutoutRecord;
    }

    private bool MatchesCircleCutout(UiRect record, UiDrawCommand command) =>
        record.X + command.Translation.X == _circleCutoutCenter.X
        && record.Y + command.Translation.Y == _circleCutoutCenter.Y && record.Width == _circleCutoutRadius;

    private UiDrawList ApplyCircleCutoutToReference(UiDrawList source)
    {
        if (_roundedClips.Count != 0)
            return ApplyRoundedClipsToReference(source);
        var commands = source.Commands.AsSpan().ToArray();
        foreach (var command in commands)
        {
            if (command.Callback is not null)
                throw new NotSupportedException("A circular cutout cannot wrap an external draw callback.");
            if (command.CircleCutoutIndexPlusOne != 0
                && source.RectFilledPrimitives![(int)command.CircleCutoutIndexPlusOne - 1].BorderThickness < 0)
                return ApplyRoundedClipsToReference(source);
            if (command.CircleCutoutIndexPlusOne != 0 && !MatchesCircleCutout(
                source.RectFilledPrimitives![checked((int)command.CircleCutoutIndexPlusOne - 1)].Rect, command))
                throw new NotSupportedException("Nested distinct circular cutouts are not supported.");
        }
        var originalCount = source.RectFilledPrimitives?.Count ?? 0;
        var records = new UiRectFilledPrimitive[checked(originalCount + commands.Length)];
        source.RectFilledPrimitives?.AsSpan().CopyTo(records);
        for (var index = 0; index < commands.Length; index++)
        {
            var command = commands[index];
            if (command.ElementCount == 0 || command.CircleCutoutIndexPlusOne != 0)
                continue;
            records[originalCount + index] = new(new(_circleCutoutCenter.X - command.Translation.X,
                _circleCutoutCenter.Y - command.Translation.Y, _circleCutoutRadius, 0), default);
            commands[index] = command with
            {
                CircleCutoutIndexPlusOne = checked((uint)(originalCount + index + 1))
            };
        }
        // Static-layer keys promise immutable content unless a new explicit stamp is supplied.
        var stamp = source.GetStaticGeometryStamp();
        stamp = (stamp ^ (uint)records.Length) * 1099511628211UL;
        for (var index = originalCount; index < records.Length; index++)
        {
            var record = records[index].Rect;
            stamp = (stamp ^ BitConverter.SingleToUInt32Bits(record.X)) * 1099511628211UL;
            stamp = (stamp ^ BitConverter.SingleToUInt32Bits(record.Y)) * 1099511628211UL;
            stamp = (stamp ^ BitConverter.SingleToUInt32Bits(record.Width)) * 1099511628211UL;
        }
        return source with
        {
            Vertices = UiPooledList<UiDrawVertex>.RentAndCopy(source.Vertices.AsSpan()),
            Indices = UiPooledList<uint>.RentAndCopy(source.Indices.AsSpan()),
            Commands = UiPooledList<UiDrawCommand>.RentAndCopy(commands.AsSpan()),
            RectFilledPrimitives = UiPooledList<UiRectFilledPrimitive>.RentAndCopy(records.AsSpan()),
            CircleFilledPrimitives = source.CircleFilledPrimitives is null ? null
                : UiPooledList<UiCircleFilledPrimitive>.RentAndCopy(source.CircleFilledPrimitives.AsSpan()),
            OwnsBuffers = true,
            StaticGeometryStamp = stamp == 0 ? 1UL : stamp,
            CommandStateStamp = UiDrawList.ComputeCommandStateStamp(commands),
            CommandScheduleStamp = UiDrawList.CommandScheduleStampEnabled ? UiDrawList.ComputeCommandScheduleStamp(commands) : 0
        };
    }

    private uint CopyCircleCutoutRecord(UiDrawList source, UiDrawCommand command)
    {
        if (command.CircleCutoutIndexPlusOne == 0)
            return 0;
        var records = source.RectFilledPrimitives
            ?? throw new InvalidOperationException("A circular cutout requires its primitive record.");
        var root = records[checked((int)command.CircleCutoutIndexPlusOne - 1)];
        if (root.BorderThickness >= 0)
        {
            _rectFilledPrimitives.Add(root);
            return checked((uint)_rectFilledPrimitives.Count);
        }
        var chain = new List<int>();
        for (var index = checked((int)command.CircleCutoutIndexPlusOne - 1); index >= 0; index = MaskParent(records.AsSpan(), index))
            chain.Add(index);
        var head = 0u;
        for (var item = chain.Count - 1; item >= 0; item--)
        {
            var record = records[chain[item]];
            if (record.BorderThickness < 0)
                record = record with
                {
                    Color = new UiColor(head == 0 ? 0u : unchecked((uint)((int)head - 1 - _rectFilledPrimitives.Count)))
                };
            _rectFilledPrimitives.Add(record);
            head = checked((uint)_rectFilledPrimitives.Count);
        }
        return head;
    }
}

public readonly partial struct UiControlContext
{
    public UiCircleCutoutScope CircleCutout(UiVector2 center, float radius) => _ui.BeginCircleCutout(center, radius);
}

public sealed partial class UiImmediateContext
{
    internal UiCircleCutoutScope BeginCircleCutout(UiVector2 center, float radius)
    {
        EnsureNotMeasuring();
        return _builder.CircleCutout(center, radius);
    }
}
