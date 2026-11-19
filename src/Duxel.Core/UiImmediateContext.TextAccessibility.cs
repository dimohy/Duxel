namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private int _textAccessibilityRoots = -1;
    private UiRect _textAccessibilityBounds;

    internal void RenderTextWidget<TPresentation>(ref string? identity, string text, UiColor? color)
        where TPresentation : IUiTextPresentation
    {
        if (_accessibility is null)
        {
            Text<TPresentation>(text, color, constrainLayout: true);
            return;
        }
        var previousRoots = _textAccessibilityRoots;
        var previousBounds = _textAccessibilityBounds;
        _textAccessibilityRoots = 0;
        _textAccessibilityBounds = default;
        try
        {
            Text<TPresentation>(text, color, constrainLayout: true);
            if (_textAccessibilityRoots == 0)
                throw new NotSupportedException("An accessible text presentation must place a common text root.");
            ExposePassiveText(ref identity, text, _textAccessibilityBounds);
        }
        finally
        {
            _textAccessibilityRoots = previousRoots;
            _textAccessibilityBounds = previousBounds;
        }
    }

    private string? ExposePassiveText(ref string? identity, string text, UiRect bounds)
    {
        if (_accessibility is null)
            return null;
        identity ??= UiWidget.NewPassiveAccessibilityIdentity();
        var id = ResolveId(identity);
        ExposeAccessibility(id, _accessibilityParent, UiAccessibilityRole.Label,
            text, bounds, readOnly: true, focusable: false, valuePattern: false, literalLabel: true);
        return id;
    }

    private void CaptureTextAccessibilityRoot(UiRect bounds)
    {
        if (_textAccessibilityRoots < 0)
            return;
        if (_textAccessibilityRoots++ == 0)
        {
            _textAccessibilityBounds = bounds;
            return;
        }
        var left = MathF.Min(_textAccessibilityBounds.X, bounds.X);
        var top = MathF.Min(_textAccessibilityBounds.Y, bounds.Y);
        var right = MathF.Max(_textAccessibilityBounds.X + _textAccessibilityBounds.Width, bounds.X + bounds.Width);
        var bottom = MathF.Max(_textAccessibilityBounds.Y + _textAccessibilityBounds.Height, bounds.Y + bounds.Height);
        _textAccessibilityBounds = new(left, top, right - left, bottom - top);
    }
}
