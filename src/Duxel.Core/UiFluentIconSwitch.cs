namespace Duxel.Core;

/// <summary>Glyph and optional colors for one state of an icon switch.</summary>
public readonly record struct UiSwitchIcon(UiMenuIcon Icon, UiColor? GlyphColor = null, UiColor? ThumbColor = null)
{
    public static UiSwitchIcon Create<TIcon>(UiColor? glyphColor = null, UiColor? thumbColor = null)
        where TIcon : IUiButtonIcon => new(UiMenuIcon.Create<TIcon, TIcon>(), glyphColor, thumbColor);
}

internal sealed record UiSwitchIcons(UiSwitchIcon Off, UiSwitchIcon On);

/// <summary>A compact switch with an icon on its sliding thumb and a text name for assistive technology.</summary>
public sealed class UiFluentIconSwitch : UiToggle<bool, UiBooleanToggleValue, UiSpaceButtonBehavior, UiFluentIconSwitchPresentation>
{
    private readonly UiSwitchIcons _icons;

    public UiFluentIconSwitch(string accessibleName, UiBinding<bool> value, UiSwitchIcon off, UiSwitchIcon on)
        : base(accessibleName, value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleName);
        if (off.Icon.IsEmpty)
            throw new ArgumentException("The off state requires an icon.", nameof(off));
        if (on.Icon.IsEmpty)
            throw new ArgumentException("The on state requires an icon.", nameof(on));
        _icons = new(off, on);
    }

    private protected override UiSwitchIcons? SwitchIcons => _icons;
}

public readonly struct UiFluentIconSwitchPresentation : IUiTogglePresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiAccessibilityRole AccessibilityRole => UiAccessibilityRole.Switch;

    public static UiToggleLayout Measure(UiControlContext context, UiVector2 textSize, bool hasLabel) =>
        new(new(48, 32), new(3, 5, 42, 22), default);

    public static UiToggleLayout Arrange(UiControlContext context, UiToggleLayout layout, UiVector2 allocatedSize) =>
        layout.CenterContent(allocatedSize);

    public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
        UiToggleLayout layout, UiButtonInteraction interaction, bool? value)
    {
        if (value is not bool on || context.SwitchIcons is not { } icons)
            throw new InvalidOperationException("An icon switch requires two icons and a Boolean value.");

        var icon = on ? icons.On : icons.Off;
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled
            : !interaction.Hovered ? UiButtonVisualState.Rest
            : interaction.Held ? UiButtonVisualState.Pressed : UiButtonVisualState.Hover;
        var forced = context.Theme.ForcedColors;
        var paint = forced is { } colors
            ? UiFluentSwitchPalette.ResolveForced(colors, context.IsEnabled, on)
            : UiFluentSwitchPalette.Resolve(context.FluentColorScheme, state, on);
        var useCustomColors = forced is null && context.IsEnabled;
        if (useCustomColors && icon.ThumbColor is { } thumbColor)
            paint = paint with { Thumb = thumbColor };
        var glyphColor = useCustomColors && icon.GlyphColor is { } custom
            ? custom : on ? paint.Background : forced?.Canvas ?? context.Theme.WindowBg;
        var motion = context.SampleSwitchMotion(on, paint);
        paint = motion.Paint;
        var track = layout.Indicator with
        {
            X = bounds.X + (bounds.Width - layout.Indicator.Width) / 2,
            Y = bounds.Y + layout.Indicator.Y
        };
        var travel = context.RightToLeft ? 1 - motion.ThumbPosition : motion.ThumbPosition;
        var radius = track.Height / 2;
        var center = new UiVector2(track.X + radius + (track.Width - track.Height) * travel, track.Y + radius);
        context.Frame(track, paint.Background, paint.Border, radius, 1);
        context.Circle(center, radius - 2, paint.Thumb);
        icon.Icon.Paint(context, new(center.X - 7, center.Y - 7, 14, 14), glyphColor, interaction);
        if (interaction.FocusVisible)
            context.FocusOutline(bounds, 5, forced?.Highlight ?? context.Theme.Text);
    }
}

public static partial class Fluent
{
    public static UiFluentIconSwitch IconSwitch(string accessibleName, UiBinding<bool> value, UiSwitchIcon off, UiSwitchIcon on) =>
        new(accessibleName, value, off, on);
}
