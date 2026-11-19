namespace Duxel.Core;

public static partial class UiFluentCheckboxPalette
{
    /// <summary>Resolves authored or system checkbox colors, including the context's disabled state.</summary>
    public static UiCheckboxPaint Resolve(UiControlContext context, UiButtonVisualState state, bool? value)
    {
        if ((uint)state > 3)
            throw new ArgumentOutOfRangeException(nameof(state));
        if (!context.IsEnabled)
            state = UiButtonVisualState.Disabled;
        if (context.Theme.ForcedColors is not { } colors)
            return Resolve(context.FluentColorScheme, state, value);
        var foreground = state == UiButtonVisualState.Disabled ? colors.GrayText : colors.CanvasText;
        return new(value == true ? colors.Canvas : default, foreground,
            value == false ? default : foreground, foreground);
    }
}
