namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private WeakReference<UiWidget>? _labelFocusTarget;
    private WeakReference<UiLabel>? _labelFocusOwner;

    private static UiControlTypographyScope BeginLabel(UiControlContext context, UiLabelOptions options) =>
        (options.Size, options.Semibold) switch
        {
            (UiLabelSize.Small, true) => context.BeginTypography<UiSemiboldTypeface, UiFluentType200>(),
            (UiLabelSize.Small, false) => context.BeginTypography<UiRegularTypeface, UiFluentType200>(),
            (UiLabelSize.Large, _) => context.BeginTypography<UiSemiboldTypeface, UiFluentType400>(),
            (_, true) => context.BeginTypography<UiSemiboldTypeface, UiFluentType300>(),
            _ => context.BeginTypography<UiRegularTypeface, UiFluentType300>()
        };

    internal static UiLabelLayout MeasureLabel(UiControlContext context, string text, UiLabelOptions options, UiBoxConstraints? available = null)
    {
        using var typography = BeginLabel(context, options);
        var constraints = available ?? context.LayoutConstraints;
        var maximum = constraints.MaxWidth;
        var block = context.MeasureTextBlock(float.IsFinite(maximum) ? context.WrapText(text, MathF.Max(1, maximum)) : text);
        var width = block.Size.X;
        var height = block.Size.Y;
        var required = default(UiRect);
        if (options.Required)
        {
            var star = context.MeasureText("*");
            var x = (block.LineCount > 0 ? block.GetLine(block.LineCount - 1).Size.X : 0) + 4;
            var y = Math.Max(0, block.LineCount - 1) * typography.LineHeight;
            if (x + star.X > maximum) { x = 4; y += typography.LineHeight; }
            required = new(x, y, star.X, typography.LineHeight);
            width = MathF.Max(width, x + star.X);
            height = MathF.Max(height, y + typography.LineHeight);
        }
        return new(block, required, constraints.Constrain(new(width, height)));
    }

    internal void RenderLabel(UiLabel label)
    {
        EnsureNotMeasuring();
        var context = new UiControlContext(this);
        var options = label.Options;
        var layout = label.Layout(context, true);
        var size = ConstrainToLayout(layout.Size);
        var origin = AdvanceCursor(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
        var enabled = context.IsEnabled && !options.Disabled;
        ExposeAccessibilityLabel(label, bounds, enabled);
        var foreground = enabled ? dark ? new UiColor(255, 255, 255) : new(36, 36, 36)
            : dark ? new UiColor(92, 92, 92) : new(189, 189, 189);
        using var clip = context.Clip(bounds);
        using var typography = BeginLabel(context, options);
        context.TypographyTextBlock(layout.Text, bounds, foreground);
        if (options.Required)
            context.TypographyTextBlock(context.MeasureTextBlock("*"), OffsetFieldRect(layout.Required, origin),
                enabled ? dark ? new(227, 125, 128) : new(209, 52, 56) : foreground);
        if (enabled && label.Target is { } target && FieldLabelPressed("\u001flabel", bounds))
        {
            target.ValidateLabelTarget();
            (_labelFocusTarget ??= new(target)).SetTarget(target);
            (_labelFocusOwner ??= new(label)).SetTarget(label);
            _requestFrame?.Invoke();
        }
    }

    private bool HasLabelFocusRequest()
    {
        if (_labelFocusTarget is null) return false;
        if (!_labelFocusTarget.TryGetTarget(out var target) || _labelFocusOwner is null
            || !_labelFocusOwner.TryGetTarget(out var label) || !ReferenceEquals(label.Target, target) || label.Options.Disabled)
        {
            _labelFocusTarget = null;
            _labelFocusOwner = null;
            return false;
        }
        return ReferenceEquals(_identityWidget, target);
    }
}
