namespace Duxel.Core;

public enum UiFlowDirection
{
    Inherit, LeftToRight, RightToLeft
}

internal readonly record struct UiControlEnvironment(UiTheme Theme, UiDesignTokens Tokens,
    UiControlFontEnvironment Fonts, bool RightToLeft);

internal readonly struct UiControlThemeScope : IDisposable
{
    private readonly UiImmediateContext _owner;
    private readonly UiControlEnvironment _environment;
    internal UiControlThemeScope(UiImmediateContext owner)
    {
        _owner = owner;
        _environment = owner.ControlEnvironment;
    }
    public void Dispose() => _owner.RestoreControlEnvironment(_environment);
}

public readonly partial struct UiControlContext
{
    public bool RightToLeft => _ui.RightToLeft;
    internal UiControlThemeScope BeginFluentProvider(UiFluentProviderTheme? theme, UiTypographyFonts? fonts = null,
        UiFlowDirection direction = UiFlowDirection.Inherit) => _ui.BeginFluentProvider(theme, fonts, direction);
}

public sealed partial class UiImmediateContext
{
    public bool RightToLeft { get; private set; }
    internal UiControlEnvironment ControlEnvironment => new(_theme, _designTokens, ControlFontEnvironment, RightToLeft);

    internal UiControlThemeScope BeginFluentProvider(UiFluentProviderTheme? theme, UiTypographyFonts? fonts = null,
        UiFlowDirection direction = UiFlowDirection.Inherit)
    {
        var scope = new UiControlThemeScope(this);
        if (theme is not null)
        {
            // System contrast colors remain authoritative inside nested app themes.
            _theme = _theme.ForcedColors is { } forced ? forced.Apply(theme.Palette) : theme.Palette;
            foreach (var disabled in _disabledStack)
                if (disabled) _theme = ApplyThemeAlpha(_theme, .5f);
            _designTokens = theme.Tokens ?? _designTokens;
        }
        if ((fonts ?? theme?.Fonts) is { } selected)
            RestoreControlFonts(new(selected, selected.Regular, selected.SecondaryRegular));
        if (direction != UiFlowDirection.Inherit)
            RightToLeft = direction == UiFlowDirection.RightToLeft;
        return scope;
    }
    internal void RestoreControlEnvironment(UiControlEnvironment environment)
    {
        _theme = environment.Theme;
        _designTokens = environment.Tokens;
        RestoreControlFonts(environment.Fonts);
        RightToLeft = environment.RightToLeft;
    }
}

internal readonly record struct UiControlFontEnvironment(UiTypographyFonts? Fonts, string? Primary, string? Secondary);

public sealed partial class UiImmediateContext
{
    internal UiControlFontEnvironment ControlFontEnvironment => new(_typographyFonts, _directTextPrimaryFontPath, _directTextSecondaryFontPath);
    internal void RestoreControlFonts(UiControlFontEnvironment environment)
    {
        _typographyFonts = environment.Fonts;
        _directTextPrimaryFontPath = environment.Primary;
        _directTextSecondaryFontPath = environment.Secondary;
    }
}
