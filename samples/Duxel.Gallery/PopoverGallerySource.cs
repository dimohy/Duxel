using System.Globalization;
using Duxel.Core;

namespace Duxel.Gallery;

internal static class PopoverGallerySource
{
    private static string Number(float? value) => value is { } number ? number.ToString("R", CultureInfo.InvariantCulture) + "f" : "null";
    private static string Rect(UiRect? value) => value is { } rect
        ? $"new UiRect({Number(rect.X)}, {Number(rect.Y)}, {Number(rect.Width)}, {Number(rect.Height)})" : "null";
    internal static string Options(UiPopoverOptions value) => $"new UiPopoverOptions(Width : {Number(value.Width)},\n"
        + $"    Size : UiPopoverSize.{value.Size}, TrapFocus : {GallerySample.Boolean(value.TrapFocus)}, DisableAutoFocus : {GallerySample.Boolean(value.DisableAutoFocus)},\n"
        + $"    CloseOnScroll : {GallerySample.Boolean(value.CloseOnScroll)}, Appearance : UiPopoverAppearance.{value.Appearance}, MaximumHeight : {Number(value.MaximumHeight)},\n"
        + $"    Positioning : {Positioning(value.Positioning)}, WithArrow : {GallerySample.Boolean(value.WithArrow)},\n"
        + $"    TriggerMode : UiPopoverTriggerMode.{value.TriggerMode}, MouseLeaveDelaySeconds : {value.MouseLeaveDelaySeconds.ToString("R", CultureInfo.InvariantCulture)}d,\n"
        + $"    MotionEnabled : {GallerySample.Boolean(value.MotionEnabled)})\n"
        + $"{{ CloseOnFocusLeave = {GallerySample.Boolean(value.CloseOnFocusLeave)}, SurfaceRole = UiAccessibilityRole.{value.SurfaceRole} }}";
    private static string Positioning(UiPopupPositioning value)
    {
        if (value == default) return "new UiPopupPositioning()";
        var padding = value.OverflowBoundaryPadding;
        var fallback = value.FallbackPositions.IsDefault ? "default"
            : value.FallbackPositions.IsEmpty ? "System.Collections.Immutable.ImmutableArray<UiPopupPosition>.Empty"
            : "System.Collections.Immutable.ImmutableArray.Create(" + string.Join(", ", value.FallbackPositions.Select(position => "UiPopupPosition." + position)) + ")";
        return $"new UiPopupPositioning\n    {{ Position = UiPopupPosition.{value.Position}, RightToLeft = {GallerySample.Boolean(value.RightToLeft)},\n"
            + $"      Pinned = {GallerySample.Boolean(value.Pinned)}, MatchTargetWidth = {GallerySample.Boolean(value.MatchTargetWidth)}, AutoSize = UiPopupAutoSize.{value.AutoSize},\n"
            + $"      Gap = {Number(value.Gap)}, CrossAxisOffset = {Number(value.CrossAxisOffset)}, CoverTarget = {GallerySample.Boolean(value.CoverTarget)},\n"
            + $"      ShiftToCoverTarget = {GallerySample.Boolean(value.ShiftToCoverTarget)}, TetherMode = UiPopupTetherMode.{value.TetherMode},\n"
            + $"      OverflowBoundaryPadding = new UiPopupBoundaryPadding({Number(padding.Start)}, {Number(padding.Top)}, {Number(padding.End)}, {Number(padding.Bottom)}),\n"
            + $"      ArrowHeight = {Number(value.ArrowHeight)}, ArrowPadding = {Number(value.ArrowPadding)}, ArrowWidth = {Number(value.ArrowWidth)},\n"
            + $"      FlipBoundary = {Rect(value.FlipBoundary)}, OverflowBoundary = {Rect(value.OverflowBoundary)}, FallbackPositions = {fallback} }}";
    }
}
