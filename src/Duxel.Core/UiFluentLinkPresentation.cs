namespace Duxel.Core;

public interface IUiLinkAppearance
{
    static abstract UiColor Foreground(UiFluentColorScheme scheme, UiButtonVisualState state);
}
public readonly struct UiFluentDefaultLinkAppearance : IUiLinkAppearance
{
    public static UiColor Foreground(UiFluentColorScheme scheme, UiButtonVisualState state) => UiFluentLinkPalette.Resolve(scheme, state, UiLinkAppearance.Default);
}
public readonly struct UiFluentSubtleLinkAppearance : IUiLinkAppearance
{
    public static UiColor Foreground(UiFluentColorScheme scheme, UiButtonVisualState state) => UiFluentLinkPalette.Resolve(scheme, state, UiLinkAppearance.Subtle);
}
public readonly struct UiFluentInvertedLinkAppearance : IUiLinkAppearance
{
    public static UiColor Foreground(UiFluentColorScheme scheme, UiButtonVisualState state) => UiFluentLinkPalette.Resolve(scheme, state, UiLinkAppearance.Inverted);
}
public readonly struct UiFluentOnBrandLinkAppearance : IUiLinkAppearance
{
    public static UiColor Foreground(UiFluentColorScheme scheme, UiButtonVisualState state) => UiFluentLinkPalette.Resolve(scheme, state, UiLinkAppearance.OnBrand);
}
public interface IUiLinkUnderline
{
    static abstract bool Inline
    {
        get;
    }
}
public readonly struct UiStandaloneLinkUnderline : IUiLinkUnderline
{
    public static bool Inline => false;
}
public readonly struct UiInlineLinkUnderline : IUiLinkUnderline
{
    public static bool Inline => true;
}

public readonly struct UiFluentLinkPresentation<TAppearance, TUnderline> : IUiLinkPresentation
    where TAppearance : IUiLinkAppearance where TUnderline : IUiLinkUnderline
{
    public static bool ReuseLayout => true;
    public static UiControlTypographyScope BeginText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    public static UiLinkLayout Measure(UiControlContext context, string text, float width)
    {
        var block = context.MeasureTextBlock(width > 0 ? context.WrapText(text, width) : text);
        return new(block.Size, block);
    }
    public static UiLinkLayout Arrange(UiControlContext context, in UiLinkLayout natural, UiVector2 allocatedSize) =>
        natural with
        {
            Size = allocatedSize
        };
    public static void Paint(UiControlContext context, in UiLinkLayout layout, UiRect bounds, UiButtonInteraction interaction)
    {
        var state = !context.IsEnabled ? UiButtonVisualState.Disabled : interaction.Held ? UiButtonVisualState.Pressed : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var color = TAppearance.Foreground(context.FluentColorScheme, state);
        context.TypographyTextBlock(layout.Text, bounds, color);
        if (!context.IsEnabled || !TUnderline.Inline && !interaction.Hovered && !interaction.Held && !interaction.FocusVisible)
            return;
        var underlineColor = interaction.FocusVisible ? (context.FluentColorScheme == UiFluentColorScheme.Dark ? new UiColor(255, 255, 255) : new UiColor(0, 0, 0)) : color;
        for (var i = 0; i < layout.Text.LineCount; i++)
        {
            var line = layout.Text.GetLine(i);
            if (line.Size.X <= 0)
                continue;
            var y = bounds.Y + i * context.TypographyLineHeight + context.TypographyBaseline + 1;
            context.Line(new(bounds.X, y), new(bounds.X + line.Size.X, y), underlineColor, 1);
            if (interaction.FocusVisible)
                context.Line(new(bounds.X, y + 2), new(bounds.X + line.Size.X, y + 2), underlineColor, 1);
        }
    }
}

public enum UiLinkAppearance
{
    Default, Subtle, Inverted, OnBrand
}
public static class UiFluentLinkPalette
{
    public static UiColor Resolve(UiFluentColorScheme scheme, UiButtonVisualState state, UiLinkAppearance appearance)
    {
        if (scheme is not (UiFluentColorScheme.Light or UiFluentColorScheme.Dark))
            throw new ArgumentOutOfRangeException(nameof(scheme));
        if ((uint)state > 3)
            throw new ArgumentOutOfRangeException(nameof(state));
        if ((uint)appearance > 3)
            throw new ArgumentOutOfRangeException(nameof(appearance));
        var dark = scheme == UiFluentColorScheme.Dark;
        if (state == UiButtonVisualState.Disabled)
            return dark ? new(92, 92, 92) : new(189, 189, 189);
        return appearance switch
        {
            UiLinkAppearance.Subtle => dark ? (state == UiButtonVisualState.Rest ? new(214, 214, 214) : new(255, 255, 255)) :
                state == UiButtonVisualState.Rest ? new(66, 66, 66) : new(36, 36, 36),
            UiLinkAppearance.Inverted => dark ? new(15, 108, 189) : new(71, 158, 245),
            UiLinkAppearance.OnBrand => new(255, 255, 255),
            _ => (dark, state) switch
            {
                (false, UiButtonVisualState.Rest) => new(17, 94, 163),
                (false, UiButtonVisualState.Hover) => new(15, 84, 140),
                (false, _) => new(12, 59, 94),
                (true, UiButtonVisualState.Rest) => new(71, 158, 245),
                (true, UiButtonVisualState.Hover) => new(98, 171, 245),
                _ => new(40, 134, 222)
            }
        };
    }
}
