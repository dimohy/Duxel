using System.Collections.Immutable;

// Positioning calculations adapted from Floating UI core 1.6.8 (MIT).
// Copyright (c) 2021-present Floating UI contributors. See Duxel.Core.ThirdPartyNotices.txt.
// Cover/shift composition follows Fluent UI (Microsoft Corporation), licensed in the same notice.

namespace Duxel.Core;

public enum UiPopupPosition
{
    Above,
    AboveStart,
    AboveEnd,
    Below,
    BelowStart,
    BelowEnd,
    Before,
    BeforeTop,
    BeforeBottom,
    After,
    AfterTop,
    AfterBottom
}

public enum UiPopupTetherMode
{
    Default,
    Disabled,
    DisabledAllAxes
}

public enum UiPopupAutoSize
{
    None,
    Width,
    Height,
    Both
}

public readonly record struct UiPopupBoundaryPadding(float Start, float Top, float End, float Bottom)
{
    public UiPopupBoundaryPadding(float all) : this(all, all, all, all) { }

    internal void Validate()
    {
        if (!float.IsFinite(Start) || !float.IsFinite(Top) || !float.IsFinite(End) || !float.IsFinite(Bottom))
        {
            throw new ArgumentOutOfRangeException(nameof(UiPopupBoundaryPadding));
        }
    }
}

/// <summary>Logical placement in the supplied coordinate space; sizes are never silently reduced.</summary>
public readonly record struct UiPopupPositioning
{
    public UiPopupPosition Position
    {
        get; init;
    }
    public bool RightToLeft
    {
        get; init;
    }
    public bool Pinned
    {
        get; init;
    }
    public bool MatchTargetWidth
    {
        get; init;
    }
    public UiPopupAutoSize AutoSize
    {
        get; init;
    }
    public float? Gap
    {
        get; init;
    }
    public float CrossAxisOffset
    {
        get; init;
    }
    public bool CoverTarget
    {
        get; init;
    }
    public bool ShiftToCoverTarget
    {
        get; init;
    }
    public UiPopupTetherMode TetherMode
    {
        get; init;
    }
    public UiPopupBoundaryPadding OverflowBoundaryPadding
    {
        get; init;
    }
    public float ArrowHeight
    {
        get; init;
    }
    public float? ArrowPadding
    {
        get; init;
    }
    public float? ArrowWidth
    {
        get; init;
    }
    public UiRect? FlipBoundary
    {
        get; init;
    }
    public UiRect? OverflowBoundary
    {
        get; init;
    }
    public ImmutableArray<UiPopupPosition> FallbackPositions
    {
        get; init;
    }

    internal void Validate()
    {
        if ((uint)Position > (uint)UiPopupPosition.AfterBottom
            || (uint)AutoSize > (uint)UiPopupAutoSize.Both
            || (uint)TetherMode > (uint)UiPopupTetherMode.DisabledAllAxes
            || !float.IsFinite(Gap ?? 4) || !float.IsFinite(CrossAxisOffset)
            || !float.IsFinite(ArrowHeight) || ArrowHeight < 0
            || !float.IsFinite(ArrowPadding ?? 8) || (ArrowPadding ?? 8) < 0
            || !float.IsFinite(ArrowWidth ?? 2 * ArrowHeight) || (ArrowWidth ?? 2 * ArrowHeight) < 0
            || !float.IsFinite((Gap ?? 4) + ArrowHeight))
        {
            throw new ArgumentOutOfRangeException(nameof(UiPopupPositioning));
        }
        OverflowBoundaryPadding.Validate();
        if (FlipBoundary is { } flip)
        {
            UiPopupPlacement.ValidateRect(flip);
        }
        if (OverflowBoundary is { } overflow)
        {
            UiPopupPlacement.ValidateRect(overflow);
        }
        if (!FallbackPositions.IsDefault)
        {
            foreach (var position in FallbackPositions)
            {
                if ((uint)position > (uint)UiPopupPosition.AfterBottom)
                {
                    throw new ArgumentOutOfRangeException(nameof(FallbackPositions));
                }
            }
        }
    }
}

/// <summary>A custom surface can paint this triangle or use its anchor for another arrow shape.</summary>
public readonly record struct UiPopupArrow(UiVector2 Tip, UiVector2 BaseStart, UiVector2 BaseEnd);

public readonly record struct UiPopupLayout(UiRect Bounds, UiPopupPosition Position, UiPopupArrow? Arrow = null,
    float ArrowCenterOffset = 0)
{
    public UiPopupMeasureRequest Measurement
    {
        get; init;
    }
    public UiVector2 ContentSize
    {
        get; init;
    }
    public UiVector2 ScrollOffset
    {
        get; init;
    }
}

public static partial class UiPopupPlacement
{
    public static UiPopupLayout Arrange(UiRect target, UiVector2 size, UiRect boundary,
        UiPopupPositioning options = default) => Arrange<UiConfiguredPopupOffset>(target, size, boundary, options);

    public static UiPopupLayout Arrange<TOffset>(UiRect target, UiVector2 size, UiRect boundary,
        UiPopupPositioning options = default) where TOffset : IUiPopupOffset
    {
        if (options.MatchTargetWidth || options.AutoSize != UiPopupAutoSize.None)
        {
            throw new InvalidOperationException("Width matching and auto-size require ArrangeMeasured and content remeasurement.");
        }
        return ArrangeCore<TOffset>(target, size, boundary, options);
    }

    private static UiPopupLayout ArrangeCore<TOffset>(UiRect target, UiVector2 size, UiRect boundary,
        UiPopupPositioning options) where TOffset : IUiPopupOffset
    {
        options.Validate();
        ValidateTargetRect(target);
        ValidateRect(boundary);
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0 || size.Y <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }
        var original = (int)options.Position;
        var group = original / 3;
        var alignment = original % 3;
        var opposite = group ^ 1;
        Span<int> candidates = stackalloc int[4];
        candidates[0] = original;
        candidates[1] = alignment == 0 ? opposite * 3 : group * 3 + 3 - alignment;
        candidates[2] = opposite * 3 + alignment;
        candidates[3] = opposite * 3 + (alignment == 0 ? 0 : 3 - alignment);
        var custom = !options.FallbackPositions.IsDefaultOrEmpty;
        var count = options.Pinned ? 1 : custom ? options.FallbackPositions.Length + 1 : alignment == 0 ? 2 : 4;
        var position = options.Position;
        var bounds = Coordinates<TOffset>(position, target, size, options);
        var leastOverflow = double.PositiveInfinity;
        var bestMainAxis = double.PositiveInfinity;
        var mainAxisPosition = position;
        var foundMainAxis = false;
        var flipBoundary = options.FlipBoundary ?? boundary;
        for (var i = 0; i < count; i++)
        {
            var candidate = custom && i > 0 ? options.FallbackPositions[i - 1] : (UiPopupPosition)candidates[i];
            var rect = Coordinates<TOffset>(candidate, target, size, options);
            var (main, firstCross, secondCross) = Overflow(rect, target, candidate, flipBoundary, options.RightToLeft);
            var overflow = Math.Max(0, main) + Math.Max(0, firstCross) + Math.Max(0, secondCross);
            if (overflow < leastOverflow)
            {
                leastOverflow = overflow;
                position = candidate;
                bounds = rect;
            }
            if (main <= 0 && firstCross < bestMainAxis)
            {
                foundMainAxis = true;
                bestMainAxis = firstCross;
                mainAxisPosition = candidate;
            }
            if (main <= 0 && firstCross <= 0 && secondCross <= 0)
            {
                foundMainAxis = true;
                mainAxisPosition = candidate;
                break;
            }
        }
        if (foundMainAxis)
        {
            position = mainAxisPosition;
            bounds = Coordinates<TOffset>(position, target, size, options);
        }
        var shiftBoundary = options.OverflowBoundary ?? boundary;
        bounds = Shift<TOffset>(bounds, target, position, shiftBoundary, options);
        UiPopupArrow? arrow = null;
        var centerOffset = 0f;
        if (options.ArrowHeight > 0)
        {
            var data = ArrowOffset(bounds, target, position, options);
            if ((int)position % 3 != 0 && data.Offset != data.Desired
                && data.ReferenceLength * 0.5f - data.Padding - data.Width * 0.5f < 0)
            {
                var adjustment = data.Desired - data.Offset;
                bounds = (int)position / 3 < 2 ? bounds with
                {
                    X = bounds.X + adjustment
                }
                    : bounds with
                    {
                        Y = bounds.Y + adjustment
                    };
                // A boolean arrow reset preserves coordinates and reruns cover/shift, while offset/flip skip.
                bounds = Cover(bounds, target, position, options);
                bounds = Shift<TOffset>(bounds, target, position, shiftBoundary, options);
                data = ArrowOffset(bounds, target, position, options);
            }
            centerOffset = data.Desired - data.Offset;
            arrow = Arrow(bounds, position, options, data.Offset + data.Width * 0.5f, data.Width * 0.5f);
        }
        ValidateRect(bounds);
        return new(bounds, position, arrow, centerOffset);
    }

    private static UiRect Coordinates<TOffset>(UiPopupPosition position, UiRect target, UiVector2 size,
        UiPopupPositioning options) where TOffset : IUiPopupOffset
    {
        var bounds = BaseCoordinates(position, target, size, options.RightToLeft);
        var delta = OffsetCoordinates<TOffset>(position, target, size, options);
        return Cover(bounds with
        {
            X = bounds.X + delta.X,
            Y = bounds.Y + delta.Y
        }, target, position, options);
    }

    private static UiVector2 OffsetCoordinates<TOffset>(UiPopupPosition position, UiRect target, UiVector2 size,
        UiPopupPositioning options) where TOffset : IUiPopupOffset
    {
        var group = (int)position / 3;
        var offset = ResolveOffset<TOffset>(target, size, position, options);
        var gap = offset.MainAxis + options.ArrowHeight;
        return group < 2 ? new(options.RightToLeft ? -offset.CrossAxis : offset.CrossAxis, group == 0 ? -gap : gap)
            : new((group == 2) != options.RightToLeft ? -gap : gap, offset.CrossAxis);
    }

    private static UiRect BaseCoordinates(UiPopupPosition position, UiRect target, UiVector2 size, bool rtl)
    {
        var group = (int)position / 3;
        var alignment = (int)position % 3;
        if (group < 2)
        {
            if (rtl && alignment != 0)
            {
                alignment = 3 - alignment;
            }
            var x = alignment switch
            {
                1 => target.X,
                2 => target.X + target.Width - size.X,
                _ => target.X + (target.Width - size.X) * 0.5f
            };
            return new(x, group == 0 ? target.Y - size.Y : target.Y + target.Height, size.X, size.Y);
        }
        var before = (group == 2) != rtl;
        var y = alignment switch
        {
            1 => target.Y,
            2 => target.Y + target.Height - size.Y,
            _ => target.Y + (target.Height - size.Y) * 0.5f
        };
        return new(before ? target.X - size.X : target.X + target.Width, y, size.X, size.Y);
    }

    private static (double Main, double FirstCross, double SecondCross) Overflow(UiRect rect, UiRect target,
        UiPopupPosition position, UiRect boundary, bool rtl)
    {
        var top = (double)boundary.Y - rect.Y;
        var bottom = (double)rect.Y + rect.Height - boundary.Y - boundary.Height;
        var left = (double)boundary.X - rect.X;
        var right = (double)rect.X + rect.Width - boundary.X - boundary.Width;
        var group = (int)position / 3;
        var vertical = group < 2;
        var endFirst = vertical ? (int)position % 3 == (rtl ? 2 : 1) : (int)position % 3 == 1;
        if (vertical ? target.Width > rect.Width : target.Height > rect.Height)
        {
            endFirst = !endFirst;
        }
        var main = group switch
        {
            0 => top,
            1 => bottom,
            _ => (group == 2) != rtl ? left : right
        };
        var start = vertical ? left : top;
        var end = vertical ? right : bottom;
        return (main, endFirst ? end : start, endFirst ? start : end);
    }

    private static UiRect Cover(UiRect bounds, UiRect target, UiPopupPosition position, UiPopupPositioning options)
    {
        if (!options.CoverTarget)
        {
            return bounds;
        }
        var group = (int)position / 3;
        return group switch
        {
            0 => bounds with { Y = bounds.Y + target.Height },
            1 => bounds with { Y = bounds.Y - target.Height },
            _ => bounds with { X = bounds.X + ((group == 2) != options.RightToLeft ? target.Width : -target.Width) }
        };
    }

    private static UiRect Shift<TOffset>(UiRect bounds, UiRect target, UiPopupPosition position, UiRect boundary,
        UiPopupPositioning options) where TOffset : IUiPopupOffset
    {
        var offset = OffsetCoordinates<TOffset>(position, target, new(bounds.Width, bounds.Height), options);
        return ShiftCore(bounds, target, position, boundary, options, offset);
    }

    private static UiRect ShiftCore(UiRect bounds, UiRect target, UiPopupPosition position, UiRect boundary,
        UiPopupPositioning options, UiVector2 offsetData)
    {
        var group = (int)position / 3;
        var vertical = group < 2;
        var padding = options.OverflowBoundaryPadding;
        var left = boundary.X + (options.RightToLeft ? padding.End : padding.Start);
        var right = boundary.X + boundary.Width - (options.RightToLeft ? padding.Start : padding.End) - bounds.Width;
        var top = boundary.Y + padding.Top;
        var bottom = boundary.Y + boundary.Height - padding.Bottom - bounds.Height;
        var shiftAcross = options.TetherMode switch
        {
            UiPopupTetherMode.Disabled => false,
            UiPopupTetherMode.DisabledAllAxes => true,
            _ => options.ShiftToCoverTarget
        };
        var x = vertical || shiftAcross ? MathF.Max(left, MathF.Min(bounds.X, right)) : bounds.X;
        var y = !vertical || shiftAcross ? MathF.Max(top, MathF.Min(bounds.Y, bottom)) : bounds.Y;
        var limitAcross = options.TetherMode != UiPopupTetherMode.DisabledAllAxes
            && (options.ShiftToCoverTarget || options.TetherMode == UiPopupTetherMode.Disabled);
        if (limitAcross)
        {
            var origin = group == 0 || group >= 2 && (group == 2) != options.RightToLeft;
            var offset = vertical ? offsetData.Y : offsetData.X;
            var start = vertical ? target.Y : target.X;
            var targetLength = vertical ? target.Height : target.Width;
            var length = vertical ? bounds.Height : bounds.Width;
            var minimum = start - length + (origin ? offset : 0);
            var maximum = start + targetLength + (origin ? 0 : offset);
            var coordinate = vertical ? y : x;
            // limitShift uses ordered comparisons, including when negative offsets invert these bounds.
            if (coordinate < minimum)
            {
                coordinate = minimum;
            }
            else if (coordinate > maximum)
            {
                coordinate = maximum;
            }
            if (vertical)
            {
                y = coordinate;
            }
            else
            {
                x = coordinate;
            }
        }
        return bounds with
        {
            X = x,
            Y = y
        };
    }

    private static UiPopupOffset ResolveOffset<TOffset>(UiRect target, UiVector2 size, UiPopupPosition position,
        UiPopupPositioning options) where TOffset : IUiPopupOffset
    {
        var offset = TOffset.Resolve(new(target, size, position, options));
        if (!float.IsFinite(offset.MainAxis) || !float.IsFinite(offset.CrossAxis)
            || !float.IsFinite(offset.MainAxis + options.ArrowHeight))
        {
            throw new InvalidOperationException("Popup offset policy returned invalid coordinates.");
        }
        return offset;
    }

    private readonly record struct ArrowData(float Desired, float Offset, float Padding, float Width, float ReferenceLength);

    private static ArrowData ArrowOffset(UiRect bounds, UiRect target, UiPopupPosition position, UiPopupPositioning options)
    {
        var vertical = (int)position / 3 < 2;
        var length = vertical ? bounds.Width : bounds.Height;
        var width = options.ArrowWidth ?? options.ArrowHeight * 2;
        var padding = MathF.Min(options.ArrowPadding ?? 8, length * 0.5f - width * 0.5f - 1);
        var referenceLength = vertical ? target.Width : target.Height;
        var desired = vertical ? target.X + referenceLength * 0.5f - bounds.X - width * 0.5f
            : target.Y + referenceLength * 0.5f - bounds.Y - width * 0.5f;
        var offset = MathF.Max(padding, MathF.Min(desired, length - width - padding));
        return new(desired, offset, padding, width, referenceLength);
    }

    private static UiPopupArrow Arrow(UiRect bounds, UiPopupPosition position,
        UiPopupPositioning options, float center, float halfBase)
    {
        var group = (int)position / 3;
        var vertical = group < 2;
        if (vertical)
        {
            var edge = group == 0 ? bounds.Y + bounds.Height : bounds.Y;
            var x = bounds.X + center;
            return new(new(x, edge + (group == 0 ? options.ArrowHeight : -options.ArrowHeight)),
                new(x - halfBase, edge), new(x + halfBase, edge));
        }
        var left = (group == 2) != options.RightToLeft;
        var side = left ? bounds.X + bounds.Width : bounds.X;
        var y = bounds.Y + center;
        return new(new(side + (left ? options.ArrowHeight : -options.ArrowHeight), y),
            new(side, y - halfBase), new(side, y + halfBase));
    }

    private static void ValidateTargetRect(UiRect target)
    {
        if (!float.IsFinite(target.X) || !float.IsFinite(target.Y) || !float.IsFinite(target.Width) || !float.IsFinite(target.Height)
            || target.Width < 0 || target.Height < 0 || !float.IsFinite(target.X + target.Width) || !float.IsFinite(target.Y + target.Height))
            throw new ArgumentOutOfRangeException(nameof(target));
    }

    internal static void ValidateRect(UiRect rect)
    {
        if (!float.IsFinite(rect.X) || !float.IsFinite(rect.Y) || !float.IsFinite(rect.Width)
            || !float.IsFinite(rect.Height) || rect.Width <= 0 || rect.Height <= 0
            || !float.IsFinite(rect.X + rect.Width) || !float.IsFinite(rect.Y + rect.Height))
        {
            throw new ArgumentOutOfRangeException(nameof(rect));
        }
    }
}
