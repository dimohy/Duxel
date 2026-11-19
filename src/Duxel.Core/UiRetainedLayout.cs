namespace Duxel.Core;

internal readonly record struct UiLayoutRetentionAttempt(UiLayoutMeasurementKey Key, UiBoxConstraints Constraints, ulong Revision);

/// <summary>One prepared arrangement shared by layout algorithms; measurement retention is independently optional.</summary>
internal sealed class UiRetainedLayout<TResult> where TResult : struct
{
    private UiLayoutMeasurementKey _key;
    private UiBoxConstraints _constraints;
    private UiVector2 _size;
    private TResult _result;
    private ulong _revision;
    private int _arrangedFrame = int.MinValue;
    private int _preparedFrame = int.MinValue;
    internal bool IsValid { get; private set; }
    internal TResult CachedResult => _result;
    internal void Clear()
    {
        IsValid = false;
        _arrangedFrame = int.MinValue;
        _preparedFrame = int.MinValue;
        _key = default;
        _result = default;
    }
    internal void Invalidate()
    {
        unchecked
        {
            _revision++;
        }
        // An assigned child keeps its parent's prepared geometry only for that frame.
        if (UiWidget.RenderingContext is { } ui && _key == ui.LayoutMeasurementKey)
            _preparedFrame = ui.GetFrameCount();
        IsValid = false;
    }
    internal bool TryGet(UiControlContext context, bool render, out TResult result, bool allowMeasurementReuse = true)
        => TryGet(context, context.LayoutConstraints, render, out result, allowMeasurementReuse, true);
    internal bool TryGet(UiControlContext context, UiBoxConstraints constraints, bool render, out TResult result,
        bool allowMeasurementReuse, bool allowPreparedArrangement)
    {
        var assigned = render && constraints == UiBoxConstraints.Tight(_size);
        if (_key == context.LayoutMeasurementKey
            && (allowMeasurementReuse && IsValid && (_constraints == constraints || assigned && _arrangedFrame == -1)
                || allowPreparedArrangement && assigned && (allowMeasurementReuse && _arrangedFrame == -1 && (IsValid || _preparedFrame == context.LayoutFrame)
                    || _arrangedFrame == context.LayoutFrame)))
        {
            if (!render)
                _preparedFrame = context.LayoutFrame;
            result = _result;
            return true;
        }
        result = default;
        return false;
    }
    internal UiLayoutRetentionAttempt Begin(UiControlContext context)
        => Begin(context, context.LayoutConstraints);
    internal UiLayoutRetentionAttempt Begin(UiControlContext context, UiBoxConstraints constraints)
    {
        Clear();
        return new(context.LayoutMeasurementKey, constraints, _revision);
    }
    internal void Commit(UiControlContext context, in UiLayoutRetentionAttempt attempt, TResult result, UiVector2 size, bool retainMeasurement = true)
    {
        if (_revision != attempt.Revision || context.LayoutMeasurementKey != attempt.Key)
            return;
        _key = attempt.Key;
        _constraints = attempt.Constraints;
        _result = result;
        _size = size;
        _arrangedFrame = retainMeasurement ? -1 : context.LayoutFrame;
        _preparedFrame = context.LayoutFrame;
        IsValid = retainMeasurement;
    }
}
