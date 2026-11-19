namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    internal float PrepareInlineFlow(UiInlineFlowTextSource source, UiVisualTextLines lines, float width)
        => _ui.PrepareInlineFlow(source, lines, width);
}

public sealed partial class UiImmediateContext
{
    private UiInlineTextSpan? _inlineSpanTarget;
    private string? _inlineSpanText;
    internal float PrepareInlineFlow(UiInlineFlowTextSource source, UiVisualTextLines lines, float width)
    {
        using var typography = BeginStyledTypography(default);
        lines.Prepare(this, source.Source, width, float.PositiveInfinity, typography.LineHeight, 0, styled: source);
        return typography.LineHeight;
    }
    internal void PaintInlineFlow(UiVisualTextLines lines, UiVector2 origin, UiRect root, ReadOnlySpan<float> lineOffsets)
    {
        using var clip = new UiControlContext(this).Clip(root);
        PaintInlineVisualLines(lines, origin, _theme.Text, CurrentClipRect, lineOffsets);
    }
    internal void RenderInlineSpanChild(UiInlineTextSpan span, string preparedText, UiRect bounds, UiRect root)
    {
        var previous = (_inlineSpanTarget, _inlineSpanText);
        _inlineSpanTarget = span;
        _inlineSpanText = preparedText;
        try
        {
            RenderLayoutChild(span, bounds, root);
        }
        finally { (_inlineSpanTarget, _inlineSpanText) = previous; }
    }
    internal bool TryRenderInlineChild(UiWidget child)
    {
        if (_inlineSpanTarget is not { } target || !ReferenceEquals(target, child))
            return false;
        target.RenderPrepared(this, _inlineSpanText!);
        return true;
    }
    internal string? CaptureInlineSpan(ref string? identity, string text)
    {
        var bounds = _inputSlotBounds ?? throw new InvalidOperationException("An owned inline span requires its prepared allocation.");
        var previous = _allocatedTextPlacements;
        var columns = _columnsActive;
        var table = _tableActive;
        _allocatedTextPlacements = 0;
        _columnsActive = _tableActive = false;
        try
        {
            var layout = PlaceControlText(new(bounds.Width, bounds.Height));
            return ExposePassiveText(ref identity, text, new(layout.Position.X, layout.Position.Y, layout.Size.X, layout.Size.Y));
        }
        finally
        {
            _allocatedTextPlacements = previous;
            _columnsActive = columns;
            _tableActive = table;
        }
    }
}
