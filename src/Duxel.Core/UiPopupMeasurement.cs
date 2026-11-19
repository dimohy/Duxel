namespace Duxel.Core;

public enum UiPopupSide
{
    Top,
    Bottom,
    Left,
    Right
}

public readonly record struct UiPopupOffset(float MainAxis, float CrossAxis);

public readonly record struct UiPopupOffsetContext(UiRect Target, UiVector2 Size, UiPopupPosition Position,
    UiPopupPositioning Options)
{
    public UiPopupSide Side => ((int)Position / 3) switch
    {
        0 => UiPopupSide.Top,
        1 => UiPopupSide.Bottom,
        2 => Options.RightToLeft ? UiPopupSide.Right : UiPopupSide.Left,
        _ => Options.RightToLeft ? UiPopupSide.Left : UiPopupSide.Right
    };
}

/// <summary>A pure offset policy evaluated with each candidate's physical side and measured dimensions.</summary>
public interface IUiPopupOffset
{
    static abstract UiPopupOffset Resolve(UiPopupOffsetContext context);
}

public readonly struct UiConfiguredPopupOffset : IUiPopupOffset
{
    public static UiPopupOffset Resolve(UiPopupOffsetContext context) =>
        new(context.Options.Gap ?? 4, context.Options.CrossAxisOffset);
}

public readonly record struct UiPopupMeasureRequest(float? Width = null, float? Height = null,
    float? MaxWidth = null, float? MaxHeight = null);

public readonly record struct UiPopupContentMeasure(UiVector2 Size, UiVector2 ContentSize)
{
    public UiPopupContentMeasure(UiVector2 size) : this(size, size)
    {
    }
}

/// <summary>Pure content measurement; an explicit width must be applied before measuring wrapped height.</summary>
public interface IUiPopupMeasurement<TContent>
{
    static abstract UiPopupContentMeasure Measure(UiControlContext context, TContent content, UiPopupMeasureRequest request);
}

public static partial class UiPopupPlacement
{
    public static UiPopupLayout ArrangeMeasured<TMeasure, TContent>(UiControlContext context, TContent content,
        UiRect target, UiRect boundary, UiPopupPositioning options = default, UiPopupMeasureRequest request = default)
        where TMeasure : IUiPopupMeasurement<TContent> =>
        ArrangeMeasured<TMeasure, TContent, UiConfiguredPopupOffset>(context, content, target, boundary, options, request);

    public static UiPopupLayout ArrangeMeasured<TMeasure, TContent, TOffset>(UiControlContext context, TContent content,
        UiRect target, UiRect boundary, UiPopupPositioning options = default, UiPopupMeasureRequest request = default)
        where TMeasure : IUiPopupMeasurement<TContent>
        where TOffset : IUiPopupOffset
    {
        options.Validate();
        ValidateTargetRect(target);
        ValidateRect(boundary);
        ValidateDimension(request.Width);
        ValidateDimension(request.Height);
        ValidateDimension(request.MaxWidth);
        ValidateDimension(request.MaxHeight);
        return ArrangeMeasuredCore<TMeasure, TContent, TOffset>(context, content, target, boundary, options, request);
    }

    private static void ValidateDimension(float? value)
    {
        if (value is { } dimension && (!float.IsFinite(dimension) || dimension < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}

public readonly partial struct UiControlContext
{
    internal UiPopupContentMeasure MeasurePopup<TMeasure, TContent>(TContent content, UiPopupMeasureRequest request)
        where TMeasure : IUiPopupMeasurement<TContent> => _ui.MeasurePopup<TMeasure, TContent>(content, request);
}

public sealed partial class UiImmediateContext
{
    internal UiPopupContentMeasure MeasurePopup<TMeasure, TContent>(TContent content, UiPopupMeasureRequest request)
        where TMeasure : IUiPopupMeasurement<TContent>
    {
        var previousConstraints = _measureConstraints;
        var previousBounds = _inputSlotBounds;
        var previousTier = _inputSlotBoundsTier;
        var depth = _layouts.Count;
        var next = (_nextItemWidth, _nextItemVerticalAlign, _hasNextItemVerticalAlign);
        var width = MathF.Min(request.Width ?? float.PositiveInfinity, request.MaxWidth ?? float.PositiveInfinity);
        var height = MathF.Min(request.Height ?? float.PositiveInfinity, request.MaxHeight ?? float.PositiveInfinity);
        _measureConstraints = new(request.Width.HasValue ? width : 0, width, request.Height.HasValue ? height : 0, height);
        _inputSlotBounds = new(0, 0, width, height);
        _inputSlotBoundsTier = _popupTierDepth;
        _layouts.Push(new(default, false, 0, 0));
        _measurementDepth++;
        var completed = false;
        try
        {
            var result = TMeasure.Measure(new(this), content, request);
            var size = result.Size;
            if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0 || size.Y <= 0)
            {
                throw new InvalidOperationException("Popup measurement returned invalid extents.");
            }
            if (!float.IsFinite(result.ContentSize.X) || !float.IsFinite(result.ContentSize.Y)
                || result.ContentSize.X < 0 || result.ContentSize.Y < 0)
            {
                throw new InvalidOperationException("Popup measurement returned invalid content extents.");
            }
            completed = true;
            return result;
        }
        finally
        {
            var balanced = _layouts.Count == depth + 1;
            while (_layouts.Count > depth)
            {
                _layouts.Pop();
            }
            _measurementDepth--;
            _inputSlotBounds = previousBounds;
            _inputSlotBoundsTier = previousTier;
            _measureConstraints = previousConstraints;
            (_nextItemWidth, _nextItemVerticalAlign, _hasNextItemVerticalAlign) = next;
            if (completed && !balanced)
            {
                throw new InvalidOperationException("Popup measurement left an unbalanced layout scope.");
            }
        }
    }
}
