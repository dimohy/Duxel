namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RatingDisplay<TPresentation>(string id, UiRatingDisplayContent content, UiRatingDisplayOptions options,
        UiRatingDisplayLayout layout, ReadOnlySpan<UiRect> items) where TPresentation : IUiRatingDisplayPresentation
    {
        var position = AdvanceCursor(layout.Size);
        var bounds = new UiRect(position.X, position.Y, layout.Size.X, layout.Size.Y);
        ExposeAccessibility(ResolveId(id), _accessibilityParent, UiAccessibilityRole.Image, content.AccessibleLabel,
            bounds, focusable: false, valuePattern: false, literalLabel: true,
            imageInfo: bounds.Width > 0 && bounds.Height > 0 ? new(bounds, content.AccessibleLabel) : null);
        if (!IsPresentationPass)
            return;
        var context = new UiControlContext(this);
        using var clip = context.Clip(bounds);
        TPresentation.Paint(context, bounds, content, options, layout, items);
    }
}
