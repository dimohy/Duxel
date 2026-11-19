namespace Duxel.Core;

public sealed partial class UiAccessibilityTree
{
    private sealed record InputTextScrollRetention(UiAccessibilityTextInfo Text, float Width, float Offset,
        UiLayoutMeasurementKey Measurement, bool Active);

    internal bool KeepInputTextScroll(string id, string text, int caret, int anchor, float width, float offset,
        UiLayoutMeasurementKey measurement, bool active, bool inputActivity)
    {
        var entry = GetEntry(id);
        if (!inputActivity && entry.InputTextScroll is { } saved && saved.Text.Content.Text == text
            && saved.Text.CaretUtf16 == caret && saved.Text.AnchorUtf16 == anchor
            && saved.Width == width && saved.Offset == offset && saved.Measurement == measurement && saved.Active == active)
            return true;
        entry.InputTextScroll = null;
        return false;
    }

    internal void RetainInputTextScroll(string id, UiAccessibilityTextInfo text, float width, float offset,
        UiLayoutMeasurementKey measurement, bool active)
        => GetEntry(id).InputTextScroll = new(text, width, offset, measurement, active);

    internal void ClearInputTextScroll(string id) => GetEntry(id).InputTextScroll = null;
}

public sealed partial class UiImmediateContext
{
    private UiInputEditResult ApplyInputAccessibilityScroll(string id, in UiInputLayout layout, in UiInputEditResult edit)
    {
        if (_accessibility is not { } tree || tree.Take(id, UiAccessibilityAction.ScrollText) is not { } request)
            return edit;
        var visible = IntersectRect(CurrentClipRect, layout.Content);
        var axes = tree.TextScrollParentAxes(id);
        var hasParent = axes.Horizontal || axes.Vertical;
        var local = hasParent ? layout.Content : visible;
        var pointBounds = hasParent ? tree.TextScrollPointBounds(id, visible) : visible;
        if (!IsControlEnabled)
            request.Result = UiAccessibilityResult.Disabled;
        else if (local.Width <= 0 || local.Height <= 0 || _popupTierDepth < _blockingKeyboardTier)
            request.Result = UiAccessibilityResult.Unavailable;
        else if (request.TextScroll is not { } scroll || edit.AccessibilityText is not { } info
            || !ReferenceEquals(scroll.Content, info.Content))
            request.Result = UiAccessibilityResult.InvalidArgument;
        else if (!string.IsNullOrEmpty(edit.CompositionText))
            request.Result = UiAccessibilityResult.Busy;
        else if (scroll.Point is { } point && (point.X < pointBounds.X || point.X >= pointBounds.X + pointBounds.Width
            || point.Y < pointBounds.Y || point.Y >= pointBounds.Y + pointBounds.Height))
            request.Result = UiAccessibilityResult.InvalidArgument;
        else
        {
            var geometry = CaptureInputTextLayout(id, info, layout, edit)!;
            var bounds = geometry.GetRangeExtents(info.Content.GetCharacterOffset(scroll.StartUtf16), info.Content.GetCharacterOffset(scroll.EndUtf16));
            var maximum = MathF.Max(0, (edit.LineLayout?.Width ?? MeasureTextWidth(edit.Text, edit.Text.Length)) - layout.Content.Width);
            if (Volatile.Read(ref request.Canceled))
                request.Result = UiAccessibilityResult.Timeout;
            else
            {
                var offset = scroll.Align(bounds, local, new(edit.ScrollX, 0), new(maximum, 0),
                    useHorizontalPoint: !axes.Horizontal, useVerticalPoint: !axes.Vertical);
                _state.SetScrollX(id, offset.X);
                tree.RetainInputTextScroll(id, info, layout.Content.Width, offset.X, LayoutMeasurementKey, edit.Active);
                UiAccessibilityTree.CompleteLocalTextScroll(request, edit.Text);
                return edit with
                {
                    ScrollX = offset.X
                };
            }
        }
        return edit;
    }
}
