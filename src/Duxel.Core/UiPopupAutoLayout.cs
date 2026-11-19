namespace Duxel.Core;

public static partial class UiPopupPlacement
{
    private readonly record struct FlipObservation(UiPopupPosition Position, double Main, double First, double Second);

    private static UiPopupLayout ArrangeMeasuredCore<TMeasure, TContent, TOffset>(UiControlContext context, TContent content,
        UiRect target, UiRect boundary, UiPopupPositioning options, UiPopupMeasureRequest request)
        where TMeasure : IUiPopupMeasurement<TContent>
        where TOffset : IUiPopupOffset
    {
        var autoWidth = options.AutoSize is UiPopupAutoSize.Width or UiPopupAutoSize.Both;
        var autoHeight = options.AutoSize is UiPopupAutoSize.Height or UiPopupAutoSize.Both;
        var result = context.MeasurePopup<TMeasure, TContent>(content, request);
        var resets = 0;
        if (autoWidth || autoHeight)
        {
            request = request with
            {
                Width = autoWidth ? null : request.Width,
                MaxWidth = autoWidth ? null : request.MaxWidth,
                Height = autoHeight ? null : request.Height,
                MaxHeight = autoHeight ? null : request.MaxHeight
            };
            result = context.MeasurePopup<TMeasure, TContent>(content, request);
            CountReset(ref resets);
        }
        var size = result.Size;
        var position = options.Position;
        var bounds = BaseCoordinates(position, target, size, options.RightToLeft);
        var matched = false;
        var flipIndex = 0;
        Span<FlipObservation> history = stackalloc FlipObservation[52];
        var historyCount = 0;
        var hasOffset = false;
        var offsetPosition = position;
        var offsetData = default(UiVector2);
        var hasArrow = false;
        var arrowAlignmentOffset = 0f;
        var arrowData = default(ArrowData);
        while (true)
        {
            if (options.MatchTargetWidth && !matched && size.X != target.Width)
            {
                matched = true;
                request = request with { Width = request.Width ?? target.Width };
                result = context.MeasurePopup<TMeasure, TContent>(content, request);
                size = result.Size;
                bounds = BaseCoordinates(position, target, size, options.RightToLeft);
                CountReset(ref resets);
                continue;
            }
            var delta = OffsetCoordinates<TOffset>(position, target, size, options);
            if (!hasOffset || offsetPosition != position || arrowAlignmentOffset == 0)
            {
                bounds = bounds with { X = bounds.X + delta.X, Y = bounds.Y + delta.Y };
                offsetData = delta;
                offsetPosition = position;
                hasOffset = true;
            }
            bounds = Cover(bounds, target, position, options);
            if (!options.Pinned && arrowAlignmentOffset == 0)
            {
                var (main, first, second) = Overflow(bounds, target, position, options.FlipBoundary ?? boundary, options.RightToLeft);
                if (main > 0 || first > 0 || second > 0)
                {
                    var observation = new FlipObservation(position, main, first, second);
                    var next = CandidateAt(options, flipIndex + 1);
                    if (next is { } nextPosition)
                    {
                        CountReset(ref resets);
                        history[historyCount++] = observation;
                        flipIndex++;
                        position = nextPosition;
                        bounds = BaseCoordinates(position, target, size, options.RightToLeft);
                        continue;
                    }
                    var best = observation;
                    var bestMain = double.PositiveInfinity;
                    var bestTotal = double.PositiveInfinity;
                    var mainFound = false;
                    for (var i = 0; i <= historyCount; i++)
                    {
                        var entry = i == historyCount ? observation : history[i];
                        var total = Math.Max(0, entry.Main) + Math.Max(0, entry.First) + Math.Max(0, entry.Second);
                        if (!mainFound && total < bestTotal)
                        {
                            bestTotal = total;
                            best = entry;
                        }
                        if (entry.Main <= 0 && entry.First < bestMain)
                        {
                            mainFound = true;
                            bestMain = entry.First;
                            best = entry;
                        }
                    }
                    if (position != best.Position)
                    {
                        CountReset(ref resets);
                        position = best.Position;
                        bounds = BaseCoordinates(position, target, size, options.RightToLeft);
                        continue;
                    }
                }
            }
            bounds = ShiftCore(bounds, target, position, options.OverflowBoundary ?? boundary, options, offsetData);
            if (autoWidth || autoHeight)
            {
                var available = AvailableSize(bounds, position, options.OverflowBoundary ?? boundary, options);
                request = request with
                {
                    MaxWidth = autoWidth && available.X >= 0 ? available.X : request.MaxWidth,
                    Width = autoWidth && available.X >= 0 && size.X > available.X ? available.X : request.Width,
                    MaxHeight = autoHeight && available.Y >= 0 ? available.Y : request.MaxHeight,
                    Height = autoHeight && available.Y >= 0 && size.Y > available.Y ? available.Y : request.Height
                };
                result = context.MeasurePopup<TMeasure, TContent>(content, request);
                if (size != result.Size)
                {
                    CountReset(ref resets);
                    size = result.Size;
                    bounds = BaseCoordinates(position, target, size, options.RightToLeft);
                    continue;
                }
            }
            if (options.ArrowHeight > 0)
            {
                arrowData = ArrowOffset(bounds, target, position, options);
                var adjust = !hasArrow && (int)position % 3 != 0 && arrowData.Offset != arrowData.Desired
                    && arrowData.ReferenceLength * 0.5f - arrowData.Padding - arrowData.Width * 0.5f < 0;
                hasArrow = true;
                if (adjust)
                {
                    arrowAlignmentOffset = arrowData.Desired - arrowData.Offset;
                    bounds = (int)position / 3 < 2 ? bounds with { X = bounds.X + arrowAlignmentOffset }
                        : bounds with { Y = bounds.Y + arrowAlignmentOffset };
                    CountReset(ref resets);
                    continue;
                }
            }
            ValidateRect(bounds);
            return new(bounds, position, hasArrow ? Arrow(bounds, position, options,
                arrowData.Offset + arrowData.Width * 0.5f, arrowData.Width * 0.5f) : null,
                hasArrow ? arrowData.Desired - arrowData.Offset : 0)
            {
                ContentSize = result.ContentSize,
                Measurement = request
            };
        }
    }

    private static void CountReset(ref int resets)
    {
        if (++resets > 51)
        {
            throw new InvalidOperationException("Popup placement did not converge within the 51-reset lifecycle limit.");
        }
    }

    private static UiPopupPosition? CandidateAt(UiPopupPositioning options, int index)
    {
        if (!options.FallbackPositions.IsDefaultOrEmpty)
        {
            return index <= options.FallbackPositions.Length ? options.FallbackPositions[index - 1] : null;
        }
        var original = (int)options.Position;
        var group = original / 3;
        var alignment = original % 3;
        if (alignment == 0)
        {
            return index == 1 ? (UiPopupPosition)((group ^ 1) * 3) : null;
        }
        return index switch
        {
            1 => (UiPopupPosition)(group * 3 + 3 - alignment),
            2 => (UiPopupPosition)((group ^ 1) * 3 + alignment),
            3 => (UiPopupPosition)((group ^ 1) * 3 + 3 - alignment),
            _ => null
        };
    }

    private static UiVector2 AvailableSize(UiRect bounds, UiPopupPosition position, UiRect boundary, UiPopupPositioning options)
    {
        var padding = options.OverflowBoundaryPadding;
        var left = boundary.X + (options.RightToLeft ? padding.End : padding.Start) - bounds.X;
        var right = bounds.X + bounds.Width - boundary.X - boundary.Width + (options.RightToLeft ? padding.Start : padding.End);
        var top = boundary.Y + padding.Top - bounds.Y;
        var bottom = bounds.Y + bounds.Height - boundary.Y - boundary.Height + padding.Bottom;
        var width = bounds.Width - left - right;
        var height = bounds.Height - top - bottom;
        var across = options.TetherMode == UiPopupTetherMode.DisabledAllAxes
            || options.TetherMode == UiPopupTetherMode.Default && options.ShiftToCoverTarget;
        if (!across)
        {
            var group = (int)position / 3;
            if (group < 2)
            {
                height = MathF.Min(height, bounds.Height - (group == 0 ? top : bottom));
            }
            else
            {
                width = MathF.Min(width, bounds.Width - ((group == 2) != options.RightToLeft ? left : right));
            }
        }
        return new(width, height);
    }
}
