namespace Duxel.Core;

public readonly record struct UiMenuPaint(UiColor Background, UiColor Foreground);

/// <summary>Fluent MenuItem authored colors and active system contrast roles.</summary>
public static class UiFluentMenuPalette
{
    public static UiMenuPaint Resolve(UiControlContext context, UiButtonVisualState state)
    {
        var authored = Resolve(context.FluentColorScheme, state);
        if (context.Theme.ForcedColors is not { } colors)
            return authored;
        return state is UiButtonVisualState.Hover or UiButtonVisualState.Pressed
            ? new(colors.Highlight, colors.HighlightText)
            : new(colors.Canvas, state == UiButtonVisualState.Disabled ? colors.GrayText : colors.CanvasText);
    }
    public static UiColor Icon(UiControlContext context, UiButtonVisualState state)
        => context.Theme.ForcedColors.HasValue ? Resolve(context, state).Foreground : Icon(context.FluentColorScheme, state);
    public static UiColor Secondary(UiControlContext context, UiButtonVisualState state, bool hovered)
        => context.Theme.ForcedColors.HasValue ? Resolve(context, state).Foreground
            : Secondary(context.FluentColorScheme, hovered, state != UiButtonVisualState.Disabled);
    public static UiColor Icon(UiFluentColorScheme scheme, UiButtonVisualState state)
    {
        var paint = Resolve(scheme, state);
        return state is UiButtonVisualState.Hover or UiButtonVisualState.Pressed
            ? scheme == UiFluentColorScheme.Dark ? new(71, 158, 245) : new(15, 108, 189) : paint.Foreground;
    }
    public static UiColor Secondary(UiFluentColorScheme scheme, bool hovered, bool enabled)
    {
        var paint = Resolve(scheme, enabled ? UiButtonVisualState.Rest : UiButtonVisualState.Disabled);
        return !enabled ? paint.Foreground : scheme == UiFluentColorScheme.Dark
            ? hovered ? new(214, 214, 214) : new(173, 173, 173)
            : hovered ? new(66, 66, 66) : new(97, 97, 97);
    }
    public static UiMenuPaint Resolve(UiFluentColorScheme scheme, UiButtonVisualState state)
    {
        if (scheme is not (UiFluentColorScheme.Light or UiFluentColorScheme.Dark) || (uint)state > 3)
            throw new ArgumentOutOfRangeException(nameof(state));
        var dark = scheme == UiFluentColorScheme.Dark;
        var neutral = UiFluentButtonPalette.Resolve(scheme, state == UiButtonVisualState.Disabled ? UiButtonVisualState.Rest : state);
        var foreground = state == UiButtonVisualState.Disabled ? dark ? new UiColor(92, 92, 92) : new(189, 189, 189)
            : state == UiButtonVisualState.Rest ? dark ? new UiColor(214, 214, 214) : new(66, 66, 66)
            : dark ? new UiColor(255, 255, 255) : new(36, 36, 36);
        return new(neutral.Background, foreground);
    }
}

/// <summary>Fluent menu surface and items with elevation and system contrast colors.</summary>
public readonly struct UiFluentMenuPresentation : IUiMenuPresentation
{
    public static bool CanRetainTriggerMeasurement => true;
    public static UiMenuOptions DefaultOptions => new(MinimumWidth : 138, Padding : 5, ItemSpacing : 2, MaximumWidth : 300);
    public static UiVector2 MeasureTrigger(UiControlContext context, string label) => UiThemeMenuPresentation.MeasureTrigger(context, label);
    public static void PaintTrigger(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction) =>
        UiThemeMenuPresentation.PaintTrigger(context, label, bounds, interaction);
    public static float MeasureItem(UiControlContext context, string label)
    {
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        return context.MeasureText(label).X + 16;
    }
    public static void PaintSurface(UiControlContext context, UiRect bounds)
    {
        var colors = context.Theme.ForcedColors;
        if (!colors.HasValue)
            context.PaintFluentMenuShadow(bounds);
        var border = colors?.CanvasText ?? (context.FluentColorScheme == UiFluentColorScheme.Dark ? new UiColor(66, 66, 66) : default);
        context.Frame(bounds, UiFluentMenuPalette.Resolve(context, UiButtonVisualState.Rest).Background,
            border, 4, border.Rgba == 0 ? 0 : 1);
    }
    public static void PaintItem(UiControlContext context, string label, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options)
        => PaintItem(context, new UiMenuItemContent(label, null, false), bounds, interaction, options);

    public static float MeasureItem(UiControlContext context, UiMenuItemContent content) =>
        UiMenuContentLayout.Measure(context, content, 8);

    public static void PaintItem(UiControlContext context, UiMenuItemContent content, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options)
    {
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : interaction.Held ? UiButtonVisualState.Pressed
            : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var paint = UiFluentMenuPalette.Resolve(context, state);
        context.Frame(bounds, paint.Background, default, 4, 0);
        UiMenuContentLayout.Paint(context, content, bounds, interaction, options, paint.Foreground,
            UiFluentMenuPalette.Icon(context, state),
            UiFluentMenuPalette.Secondary(context, state, false),
            UiFluentMenuPalette.Secondary(context, state, true), 8, 6);
        if (context.IsEnabled && interaction.FocusVisible && !context.Theme.ForcedColors.HasValue)
            context.FocusOutline(bounds, 4);
    }
    public static void PaintFocus(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, UiMenuOptions options)
    {
        if (context.IsEnabled && interaction.FocusVisible && context.Theme.ForcedColors is { } colors)
            context.Frame(new(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4), default, colors.Highlight, 4, 2);
    }
}

[UiWidgetStyle(typeof(UiMenu<>), typeof(UiFluentMenuPresentation))]
[UiComponent("Menu", UiComponentExposure.Fluent)]
public partial class UiFluentMenu;
