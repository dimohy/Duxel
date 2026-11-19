namespace Duxel.Core;

public sealed partial class UiAccessibilityTree
{
    internal void CaptureTextViewport(string id, UiRect viewport) => GetEntry(id).TextViewport = viewport;

    internal (bool Horizontal, bool Vertical) TextScrollParentAxes(string id)
    {
        if (!_entries.TryGetValue(id, out var entry))
            return default;
        var horizontal = false;
        var vertical = false;
        for (var owner = FindScrollParent(entry); owner is not null; owner = FindScrollParent(owner))
        {
            var range = owner.ScrollViewport!.Value.Range;
            horizontal |= range.HorizontallyScrollable;
            vertical |= range.VerticallyScrollable;
        }
        return (horizontal, vertical);
    }

    internal UiRect TextScrollPointBounds(string id, UiRect local)
    {
        if (!_entries.TryGetValue(id, out var entry))
            return local;
        for (var owner = FindScrollParent(entry); owner is not null; owner = FindScrollParent(owner))
        {
            var viewport = owner.ScrollViewport!.Value;
            if (viewport.Range.HorizontallyScrollable || viewport.Range.VerticallyScrollable)
                local = viewport.Bounds;
            else
                local = UiImmediateContext.IntersectRect(local, viewport.Bounds);
            local = UiImmediateContext.IntersectRect(local, viewport.EnclosingClip);
        }
        return local;
    }

    internal static void CompleteLocalTextScroll(Request request, string text)
    {
        request.ExpectedText = text;
        request.TextScrollApplied = true;
    }

    private static UiAccessibilityResult? ResolveTextScrollBounds(Request request, Entry target, out UiRect bounds)
    {
        bounds = default;
        if (request.TextScroll is not { } scroll || target.Staged.TextInfo is not { } info
            || !ReferenceEquals(scroll.Content, info.Content))
            return UiAccessibilityResult.InvalidArgument;
        if (target.Staged.TextLayout is not { } geometry || target.TextViewport is not { Width: > 0, Height: > 0 } viewport)
            return UiAccessibilityResult.InvalidOperation;
        if (geometry.HasComposition)
            return UiAccessibilityResult.Busy;
        bounds = geometry.GetRangeExtents(info.Content.GetCharacterOffset(scroll.StartUtf16), info.Content.GetCharacterOffset(scroll.EndUtf16));
        // Empty source ranges still reveal an insertion cell through enclosing viewports.
        if (bounds.Width == 0 && bounds.X >= viewport.X && bounds.X <= viewport.X + viewport.Width)
            bounds = bounds with { X = MathF.Min(bounds.X, viewport.X + viewport.Width - 1) };
        bounds = UiImmediateContext.IntersectRect(bounds with { Width = MathF.Max(1, bounds.Width) }, viewport);
        return bounds is { Width: > 0, Height: > 0 } ? null : UiAccessibilityResult.InvalidOperation;
    }
}
