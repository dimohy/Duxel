namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private UiMultilineEditResult ApplyMultilineAccessibilityScroll(string id, in UiMultilineInputLayout layout, in UiMultilineEditResult edit)
    {
        if (_accessibility is not { } tree)
            return edit;
        var content = edit.VisualContent ?? layout.ContentBounds;
        var offset = tree.ApplyScrollIntoView(id, new(edit.ScrollX, edit.ScrollY), new(edit.MaximumScrollX, edit.MaximumScrollY));
        ApplyAccessibilityScrollRequest(tree, id, new(content.Width, content.Height), IntersectRect(CurrentClipRect, content),
            new(edit.ContentWidth, edit.ContentHeight), layout.LineHeight * 3, IsControlEnabled, false, ref offset);
        var current = edit with { ScrollX = offset.X, ScrollY = offset.Y };
        if (tree.Take(id, UiAccessibilityAction.ScrollText) is not { } request)
            return current;
        var visible = IntersectRect(CurrentClipRect, content);
        var axes = tree.TextScrollParentAxes(id);
        var hasParent = axes.Horizontal || axes.Vertical;
        var local = hasParent ? content : visible;
        var pointBounds = hasParent ? tree.TextScrollPointBounds(id, visible) : visible;
        if (!IsControlEnabled)
            request.Result = UiAccessibilityResult.Disabled;
        else if (local.Width <= 0 || local.Height <= 0 || _popupTierDepth < _blockingKeyboardTier)
            request.Result = UiAccessibilityResult.Unavailable;
        else if (request.TextScroll is not { } textScroll || current.AccessibilityText is not { } info
            || !ReferenceEquals(textScroll.Content, info.Content))
            request.Result = UiAccessibilityResult.InvalidArgument;
        else if (!string.IsNullOrEmpty(current.CompositionText))
            request.Result = UiAccessibilityResult.Busy;
        else if (textScroll.Point is { } point && (point.X < pointBounds.X || point.X >= pointBounds.X + pointBounds.Width
            || point.Y < pointBounds.Y || point.Y >= pointBounds.Y + pointBounds.Height))
            request.Result = UiAccessibilityResult.InvalidArgument;
        else if (CaptureMultilineTextLayout(id, info, layout, current) is not { } geometry)
            request.Result = UiAccessibilityResult.InvalidOperation;
        else
        {
            var bounds = geometry.GetRangeExtents(info.Content.GetCharacterOffset(textScroll.StartUtf16), info.Content.GetCharacterOffset(textScroll.EndUtf16));
            if (Volatile.Read(ref request.Canceled))
                request.Result = UiAccessibilityResult.Timeout;
            else
            {
                offset = textScroll.Align(bounds, local, offset, new(current.MaximumScrollX, current.MaximumScrollY),
                    useHorizontalPoint: !axes.Horizontal, useVerticalPoint: !axes.Vertical);
                current = current with { ScrollX = offset.X, ScrollY = offset.Y };
                UiAccessibilityTree.CompleteLocalTextScroll(request, current.Text);
            }
        }
        return current;
    }
}
