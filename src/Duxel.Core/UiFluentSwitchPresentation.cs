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
    static abstract UiToggleLayout Measure(UiVector2 track, UiVector2 text, bool hasLabel);
}
public readonly struct UiSwitchLabelAfter : IUiSwitchLabelPosition
{
    public static bool CanRetainMeasurement => true;
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
    public static UiToggleLayout Measure(UiControlContext context, UiVector2 textSize, bool hasLabel) => TLabel.Measure(TSize.TrackSize, textSize, hasLabel);
    public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
        UiToggleLayout layout, UiButtonInteraction interaction, bool? value)
    {
        if (value is null)
            throw new ArgumentException("Switch requires a two-state value.", nameof(value));
        var rightToLeft = context.RightToLeft;
        if (rightToLeft)
            layout = layout.MirrorHorizontally(textSize.X);
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : interaction.Held && interaction.Hovered ? UiButtonVisualState.Pressed :
            interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var paint = UiFluentSwitchPalette.Resolve(context.FluentColorScheme, state, value.Value);
        var progress = context.SampleStateMotion(value.Value);
        if (progress != (value.Value ? 1 : 0))
        {
            var off = UiFluentSwitchPalette.Resolve(context.FluentColorScheme, state, false);
            var on = UiFluentSwitchPalette.Resolve(context.FluentColorScheme, state, true);
            paint = new(UiDisclosureMotion.Blend(off.Background, on.Background, progress),
                UiDisclosureMotion.Blend(off.Border, on.Border, progress),
                UiDisclosureMotion.Blend(off.Thumb, on.Thumb, progress), paint.Label);
        }
        var track = layout.Indicator with
        {
            X = bounds.X + layout.Indicator.X,
            Y = bounds.Y + layout.Indicator.Y
        };
        context.Frame(track, paint.Background, paint.Border, track.Height / 2, 1);
        var thumb = track.Height - 2;
        var travel = rightToLeft ? 1 - progress : progress;
        using (context.Clip(track))
            UiFluentSwitchThumb.Paint(context,
                new(track.X + 1 + (track.Width - thumb - 2) * travel, track.Y + 1, thumb, thumb), paint.Thumb, interaction);
        if (interaction.FocusVisible)
            context.FocusOutline(bounds, 4);
        context.TypographyIndicatorText(text, new(bounds.X + layout.LabelOffset.X, bounds.Y + layout.LabelOffset.Y, textSize.X, textSize.Y), textSize, paint.Label);
    }
}

public readonly record struct UiSwitchPaint(UiColor Background, UiColor Border, UiColor Thumb, UiColor Label);
public static class UiFluentSwitchPalette
{
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
