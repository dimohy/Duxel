namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private const string TooltipScrollbarActiveId = "\u001ftooltip-scrollbar";
    private TooltipScrollbarCapture? _tooltipScrollbarCapture;
    private System.Runtime.CompilerServices.ConditionalWeakTable<IUiTooltipTarget, TooltipScrollbarMotions>? _tooltipScrollbarMotions;
    private sealed class TooltipScrollbarMotions
    {
        internal UiScrollbarMotion Horizontal, Vertical;
    }

    private readonly record struct TooltipScrollbarCapture(IUiTooltipTarget Owner, bool Horizontal, float? ThumbOffset);

    private readonly record struct TooltipScrollbar(UiRect Track, UiRect Thumb, float Maximum, bool Horizontal)
    {
        internal float TrackStart => Horizontal ? Track.X : Track.Y;
        internal float ThumbStart => Horizontal ? Thumb.X : Thumb.Y;
        internal float Page => Horizontal ? Track.Width : Track.Height;
        internal float Travel => Page - (Horizontal ? Thumb.Width : Thumb.Height);
    }

    private static float TooltipPhysicalScrollX(float offset, float maximum, bool rightToLeft) =>
        ReverseHorizontalScroll(Math.Clamp(offset, 0, maximum), maximum, rightToLeft);

    private static TooltipScrollbar? GetTooltipScrollbar(UiRect bounds, UiVector2 content, UiVector2 offset, bool horizontal, bool rightToLeft)
    {
        var maximumX = MathF.Max(0, content.X - bounds.Width);
        var maximumY = MathF.Max(0, content.Y - bounds.Height);
        var maximum = horizontal ? maximumX : maximumY;
        if (maximum <= 0)
        {
            return null;
        }
        var thickness = MathF.Min(10, MathF.Min(bounds.Width, bounds.Height) / 2);
        var gutter = maximumY > 0 ? thickness : 0;
        var track = horizontal
            ? new UiRect(bounds.X + (rightToLeft ? gutter : 0), bounds.Y + bounds.Height - thickness, bounds.Width - gutter, thickness)
            : new UiRect(rightToLeft ? bounds.X : bounds.X + bounds.Width - thickness, bounds.Y, thickness, bounds.Height - (maximumX > 0 ? thickness : 0));
        var length = horizontal ? track.Width : track.Height;
        var viewport = horizontal ? bounds.Width : bounds.Height;
        var thumbLength = MathF.Min(length * .8f, MathF.Max(16, length * viewport / (viewport + maximum)));
        var position = Math.Clamp((horizontal ? offset.X : offset.Y) / maximum, 0, 1) * (length - thumbLength);
        var inset = thickness * .2f;
        var thumb = horizontal
            ? new UiRect(track.X + position, track.Y + inset, thumbLength, thickness - 2 * inset)
            : new UiRect(track.X + inset, track.Y + position, thickness - 2 * inset, thumbLength);
        return new(track, thumb, maximum, horizontal);
    }

    private void BeginTooltipScrollbarCapture(IUiTooltipTarget owner, UiRect bounds, UiVector2 content,
        UiTooltipOptions options, ref UiVector2 offset)
    {
        if (!options.ShowScrollbars || !owner.Options.ShowScrollbars || !_leftMousePressed || _state.ActiveId is not null)
        {
            return;
        }
        var rightToLeft = options.Positioning.RightToLeft;
        if (rightToLeft != owner.Options.Positioning.RightToLeft)
            return;

        var physical = offset with
        {
            X = TooltipPhysicalScrollX(offset.X, MathF.Max(0, content.X - bounds.Width), rightToLeft)
        };
        var horizontal = GetTooltipScrollbar(bounds, content, physical, horizontal: true, rightToLeft);
        var vertical = GetTooltipScrollbar(bounds, content, physical, horizontal: false, rightToLeft);
        var hit = horizontal is { } x && IsHovering(IntersectRect(x.Track, _clipRect)) ? x
            : vertical is { } y && IsHovering(IntersectRect(y.Track, _clipRect)) ? y : (TooltipScrollbar?)null;
        if (hit is not { } bar)
        {
            return;
        }
        var pointer = bar.Horizontal ? _mousePosition.X : _mousePosition.Y;
        var thumbOffset = IsHovering(bar.Thumb) ? pointer - bar.ThumbStart : (float?)null;
        if (!thumbOffset.HasValue)
        {
            var axis = bar.Horizontal ? physical.X : physical.Y;
            var page = bar.Horizontal ? bounds.Width : bounds.Height;
            var next = _controlSkins.Scrollbar.TrackClick == UiScrollbarTrackClick.Jump
                ? Math.Clamp((pointer - bar.TrackStart - (bar.Page - bar.Travel) / 2) / bar.Travel, 0, 1) * bar.Maximum
                : Math.Clamp(axis + (pointer < bar.ThumbStart ? -page : page), 0, bar.Maximum);
            offset = bar.Horizontal ? offset with
            {
                X = ReverseHorizontalScroll(next, bar.Maximum, rightToLeft)
            } : offset with
            {
                Y = next
            };
        }
        _leftMousePressed = false;
        if (_leftMouseDown)
        {
            _state.ActiveId = TooltipScrollbarActiveId;
            _tooltipScrollbarCapture = new(owner, bar.Horizontal, thumbOffset);
        }
        else
        {
            _leftMouseReleased = false;
        }
    }

    private bool ContinueTooltipScrollbarCapture()
    {
        if (_tooltipScrollbarCapture is not { } capture)
        {
            return false;
        }
        if (!_leftMouseDown || _leftMouseReleased || _state.ActiveId != TooltipScrollbarActiveId
            || capture.Owner.Visible == false || !capture.Owner.IsOpen || !capture.Owner.Options.ShowScrollbars)
        {
            _leftMouseReleased = false;
            ReleaseTooltipScrollbar();
            return false;
        }
        var controlled = FindControlledTooltip(capture.Owner);
        var bounds = controlled?.SurfaceBounds ?? _tooltipSurfaceBounds;
        var content = controlled?.ContentSize ?? _tooltipContentSize;
        var tier = controlled?.Tier ?? _tooltipOwnerTier;
        var rightToLeft = (controlled?.Options ?? _tooltipOwnerOptions).Positioning.RightToLeft;
        ref var offset = ref (controlled is null ? ref _tooltipScrollOffset : ref controlled.ScrollOffset);
        var physical = offset with
        {
            X = TooltipPhysicalScrollX(offset.X, MathF.Max(0, content.X - bounds.Width), rightToLeft)
        };
        if (rightToLeft != capture.Owner.Options.Positioning.RightToLeft || IsMouseOverHigherTierPopup(tier + 1)
            || GetTooltipScrollbar(bounds, content, physical, capture.Horizontal, rightToLeft) is not { } bar)
        {
            ReleaseTooltipScrollbar();
            return false;
        }
        if (capture.ThumbOffset is { } grab)
        {
            var pointer = capture.Horizontal ? _mousePosition.X : _mousePosition.Y;
            var next = Math.Clamp((pointer - grab - bar.TrackStart) / bar.Travel, 0, 1) * bar.Maximum;
            offset = capture.Horizontal ? offset with
            {
                X = ReverseHorizontalScroll(next, bar.Maximum, rightToLeft)
            } : offset with
            {
                Y = next
            };
        }
        _tooltipSurfaceUnderPointer = capture.Owner;
        return true;
    }

    private void PaintTooltipScrollbars(IUiTooltipTarget owner, UiPopupLayout layout, bool rightToLeft)
    {
        var horizontal = GetTooltipScrollbar(layout.Bounds, layout.ContentSize, layout.ScrollOffset, horizontal: true, rightToLeft);
        var vertical = GetTooltipScrollbar(layout.Bounds, layout.ContentSize, layout.ScrollOffset, horizontal: false, rightToLeft);
        if (_tooltipScrollbarCapture is { } capture && ReferenceEquals(capture.Owner, owner)
            && (capture.Horizontal ? horizontal : vertical) is null)
        {
            ReleaseTooltipScrollbar();
        }
        var clip = IntersectRect(layout.Bounds, CurrentClipRect);
        if (horizontal is { } x)
        {
            PaintTooltipScrollbar(owner, x, clip, layout.ScrollOffset.X);
        }
        if (vertical is { } y)
        {
            PaintTooltipScrollbar(owner, y, clip, layout.ScrollOffset.Y);
        }
    }

    private void PaintTooltipScrollbar(IUiTooltipTarget owner, TooltipScrollbar bar, UiRect clip, float offset)
    {
        if (clip.Width <= 0 || clip.Height <= 0)
            return;
        var active = _tooltipScrollbarCapture is { } capture && ReferenceEquals(capture.Owner, owner)
            && capture.Horizontal == bar.Horizontal;
        var hovered = IsScrollHoverable(bar.Track, clip);
        var motions = (_tooltipScrollbarMotions ??= new()).GetValue(owner, static _ => new());
        ref var motion = ref (bar.Horizontal ? ref motions.Horizontal : ref motions.Vertical);
        var opacity = SampleScrollbarMotion(ref motion, offset, active, hovered);
        PaintScrollbarVisual(bar.Track, bar.Thumb, clip, opacity, active, hovered);
    }

    private void ReleaseTooltipScrollbar(IUiTooltipTarget? owner = null)
    {
        if (owner is not null && !ReferenceEquals(_tooltipScrollbarCapture?.Owner, owner))
        {
            return;
        }
        _tooltipScrollbarCapture = null;
        if (_state.ActiveId == TooltipScrollbarActiveId)
        {
            _state.ActiveId = null;
        }
    }
}
