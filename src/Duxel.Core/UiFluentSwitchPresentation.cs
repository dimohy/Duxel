namespace Duxel.Core;

[UiFluentIcon("circle", UiFluentIconStyle.Filled, 20)]
public readonly partial struct UiFluentSwitchThumb;

public interface IUiSwitchSize
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiVector2 TrackSize
    {
        get;
    }
    static abstract UiControlTypographyScope BeginText(UiControlContext context, bool hasText);
}
public readonly struct UiFluentMediumSwitchSize : IUiSwitchSize
{
    public static bool CanRetainMeasurement => true;
    public static UiVector2 TrackSize => new(40, 20);
    public static UiControlTypographyScope BeginText(UiControlContext context, bool hasText) => context.BeginTypography<UiRegularTypeface, UiFluentType300>(hasText);
}
public readonly struct UiFluentSmallSwitchSize : IUiSwitchSize
{
    public static bool CanRetainMeasurement => true;
    public static UiVector2 TrackSize => new(32, 16);
    public static UiControlTypographyScope BeginText(UiControlContext context, bool hasText) => context.BeginTypography<UiRegularTypeface, UiFluentType200>(hasText);
}
public interface IUiSwitchLabelPosition
{
    static virtual bool CanRetainMeasurement => false;
    static virtual bool AlignToFirstLine => false;
    static virtual float LabelWidth(float availableWidth, UiVector2 track) => availableWidth - track.X - 28;
    static abstract UiToggleLayout Measure(UiVector2 track, UiVector2 text, bool hasLabel);
}
public readonly struct UiSwitchLabelAfter : IUiSwitchLabelPosition
{
    public static bool CanRetainMeasurement => true;
    public static bool AlignToFirstLine => true;
    public static UiToggleLayout Measure(UiVector2 track, UiVector2 text, bool hasLabel)
    {
        var height = MathF.Max(track.Y, hasLabel ? text.Y : 0);
        return new(new(track.X + 16 + (hasLabel ? text.X + 12 : 0), height + 16),
            new(8, 8 + (height - track.Y) / 2, track.X, track.Y), new(track.X + 20, 8 + (height - text.Y) / 2));
    }
}
public readonly struct UiSwitchLabelBefore : IUiSwitchLabelPosition
{
    public static bool CanRetainMeasurement => true;
    public static bool AlignToFirstLine => true;
    public static UiToggleLayout Measure(UiVector2 track, UiVector2 text, bool hasLabel)
    {
        var height = MathF.Max(track.Y, hasLabel ? text.Y : 0);
        return new(new(track.X + 16 + (hasLabel ? text.X + 12 : 0), height + 16),
            new(hasLabel ? text.X + 20 : 8, 8 + (height - track.Y) / 2, track.X, track.Y), new(8, 8 + (height - text.Y) / 2));
    }
}
public readonly struct UiSwitchLabelAbove : IUiSwitchLabelPosition
{
    public static bool CanRetainMeasurement => true;
    public static float LabelWidth(float availableWidth, UiVector2 track) => availableWidth - 16;
    public static UiToggleLayout Measure(UiVector2 track, UiVector2 text, bool hasLabel) => hasLabel ?
        new(new(MathF.Max(track.X, text.X) + 16, text.Y + track.Y + 16), new(8, text.Y + 8, track.X, track.Y), new(8, 4)) :
        UiSwitchLabelAfter.Measure(track, text, false);
}

/// <summary>Size and label composition are statically selected implementations.</summary>
public readonly struct UiFluentSwitchPresentation<TSize, TLabel> : IUiTogglePresentation
    where TSize : IUiSwitchSize where TLabel : IUiSwitchLabelPosition
{
    public static bool CanRetainMeasurement => TSize.CanRetainMeasurement && TLabel.CanRetainMeasurement;
    public static UiAccessibilityRole AccessibilityRole => UiAccessibilityRole.Switch;
    public static UiToggleLayout Arrange(UiControlContext context, UiToggleLayout layout, UiVector2 allocatedSize) => layout.CenterContent(allocatedSize);
    public static UiControlTypographyScope BeginText(UiControlContext context, bool hasText) => TSize.BeginText(context, hasText);
    public static (string Text, UiVector2 Size) PrepareLabel(UiControlContext context, string text, UiVector2 natural, float maxWidth)
    {
        if (text.Length == 0)
            return (text, natural);
        var wrappedBeforeWidth = 0f;
        if (float.IsFinite(maxWidth))
        {
            var width = MathF.Max(1, TLabel.LabelWidth(maxWidth, TSize.TrackSize));
            if (natural.X > width)
            {
                var wrapped = context.WrapText(text, width);
                // A flex-shrunk label before the track keeps its allocated width after wrapping.
                if (typeof(TLabel) == typeof(UiSwitchLabelBefore) && !ReferenceEquals(wrapped, text))
                    wrappedBeforeWidth = width;
                text = wrapped;
            }
        }
        if (text.AsSpan().IndexOfAny('\r', '\n') < 0)
            return (text, natural);
        var size = context.MeasureTextBlock(text).Size;
        return (text, size with { X = MathF.Max(size.X, wrappedBeforeWidth) });
    }

    public static UiToggleLayout Measure(UiControlContext context, UiVector2 textSize, bool hasLabel)
    {
        var layout = TLabel.Measure(TSize.TrackSize, textSize, hasLabel);
        var lineHeight = context.TypographyLineHeight;
        if (!TLabel.AlignToFirstLine || !hasLabel || textSize.Y <= lineHeight)
            return layout;
        var firstLine = TLabel.Measure(TSize.TrackSize, new(textSize.X, lineHeight), true);
        return layout with { Indicator = firstLine.Indicator, LabelOffset = firstLine.LabelOffset };
    }
    public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
        UiToggleLayout layout, UiButtonInteraction interaction, bool? value)
    {
        if (value is null)
            throw new ArgumentException("Switch requires a two-state value.", nameof(value));
        var rightToLeft = context.RightToLeft;
        if (rightToLeft)
            layout = layout.MirrorHorizontally(textSize.X);
        var track = layout.Indicator with { X = bounds.X + layout.Indicator.X, Y = bounds.Y + layout.Indicator.Y };
        var inputHovered = interaction.Hovered && context.IsHovered(TrackInputBounds(bounds, track, text.Length != 0));
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled
            : !inputHovered ? UiButtonVisualState.Rest
            : interaction.Held ? UiButtonVisualState.Pressed : UiButtonVisualState.Hover;
        var forcedColors = context.Theme.ForcedColors;
        var paint = forcedColors is { } colors
            ? UiFluentSwitchPalette.ResolveForced(colors, context.IsEnabled, value.Value)
            : UiFluentSwitchPalette.Resolve(context.FluentColorScheme, state, value.Value);
        var motion = context.SampleSwitchMotion(value.Value, paint);
        paint = motion.Paint;
        context.Frame(track, paint.Background, paint.Border, track.Height / 2, 1);
        var thumb = track.Height - 2;
        var travel = rightToLeft ? 1 - motion.ThumbPosition : motion.ThumbPosition;
        using (context.Clip(track))
            UiFluentSwitchThumb.Paint(context,
                new(track.X + 1 + (track.Width - thumb - 2) * travel, track.Y + 1, thumb, thumb), paint.Thumb, interaction);
        if (interaction.FocusVisible)
            context.FocusOutline(bounds, 4, forcedColors?.Highlight ?? context.Theme.Text);
        var labelBounds = new UiRect(bounds.X + layout.LabelOffset.X, bounds.Y + layout.LabelOffset.Y, textSize.X, textSize.Y);
        if (text.AsSpan().IndexOfAny('\r', '\n') >= 0)
        {
            var block = context.MeasureTextBlock(text);
            if (TLabel.AlignToFirstLine)
                labelBounds = labelBounds with { Y = context.IndicatorBlockY(block, track.Y + track.Height / 2, labelBounds.Y) };
            context.TypographyTextBlock(block, labelBounds, paint.Label,
                rightToLeft ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left);
        }
        else
            context.TypographyIndicatorText(text, labelBounds, textSize, paint.Label);
    }

    private static UiRect TrackInputBounds(UiRect bounds, UiRect track, bool hasLabel)
    {
        const float padding = 8;
        // The React input styles cover the track plus its spacing, separately from the label.
        if (hasLabel && typeof(TLabel) == typeof(UiSwitchLabelAbove))
        {
            var height = UiFluentMediumSwitchSize.TrackSize.Y + padding;
            return new(bounds.X, track.Y + track.Height + padding - height, bounds.Width, height);
        }
        return new(track.X - padding, bounds.Y, track.Width + padding * 2, bounds.Height);
    }

}

public readonly record struct UiSwitchPaint(UiColor Background, UiColor Border, UiColor Thumb, UiColor Label);
public static class UiFluentSwitchPalette
{
    internal static UiSwitchPaint ResolveForced(UiForcedColorPalette colors, bool enabled, bool value)
    {
        if (!enabled)
            return new(value ? colors.Canvas : default, colors.GrayText, colors.GrayText, colors.GrayText);
        return value
            ? new(colors.Highlight, colors.Canvas, colors.Canvas, colors.CanvasText)
            : new(default, colors.CanvasText, colors.CanvasText, colors.CanvasText);
    }
    public static UiSwitchPaint Resolve(UiFluentColorScheme scheme, UiButtonVisualState state, bool value)
    {
        var checkbox = UiFluentCheckboxPalette.Resolve(scheme, state, value);
        var transparent = new UiColor(0, 0, 0, 0);
        var dark = scheme == UiFluentColorScheme.Dark;
        if (state == UiButtonVisualState.Disabled)
            return new(value ? (dark ? new(20, 20, 20) : new(240, 240, 240)) : transparent,
            value ? transparent : checkbox.Border, checkbox.Indicator, checkbox.Label);
        return new(checkbox.Background, value ? transparent : checkbox.Border, value ? checkbox.Indicator : checkbox.Border,
            dark ? new(255, 255, 255) : new(36, 36, 36));
    }
}
