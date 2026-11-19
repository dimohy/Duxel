namespace Duxel.Core;

/// <summary>An immutable palette and optional geometry tokens for a local Fluent subtree.</summary>
public sealed class UiFluentProviderTheme
{
    public UiTheme Palette { get; }
    public UiDesignTokens? Tokens { get; }
    public UiTypographyFonts? Fonts { get; }
    public static UiFluentProviderTheme Light { get; } = new(UiWindows11Design.CreateTheme());
    public static UiFluentProviderTheme Dark { get; } = new(UiWindows11Design.CreateDarkTheme());

    public UiFluentProviderTheme(UiTheme palette, UiDesignTokens? tokens = null, UiTypographyFonts? fonts = null)
    {
        if (!Enum.IsDefined(palette.FluentColorScheme))
            throw new ArgumentOutOfRangeException(nameof(palette));
        if (tokens is { } value)
        {
            static void Validate(float number)
            {
                if (!float.IsFinite(number) || number < 0)
                    throw new ArgumentOutOfRangeException(nameof(tokens));
            }
            Validate(value.WindowCornerRadius);
            Validate(value.ControlCornerRadius);
            Validate(value.ControlBorderWidth);
            if (!float.IsFinite(value.ControlPressedOffsetY))
                throw new ArgumentOutOfRangeException(nameof(tokens));
            Validate(value.InputCornerRadius);
            Validate(value.ToggleCornerRadius);
            Validate(value.ProgressCornerRadius);
            Validate(value.FocusRingThickness);
        }
        if (fonts is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fonts.Regular);
            ArgumentException.ThrowIfNullOrWhiteSpace(fonts.Semibold);
            ArgumentException.ThrowIfNullOrWhiteSpace(fonts.Bold);
        }
        Fonts = fonts;
        Palette = palette;
        Tokens = tokens;
    }
}

/// <summary>Owns a themed subtree without changing the app theme or adding an input focus stop.</summary>
public class UiThemeProvider : UiContainerBox<UiBoxContainerLayout, UiPlainContainerPresentation>
{
    private UiFluentProviderTheme? _theme;
    private bool _paintBackground;
    private UiTypographyFonts? _fonts;
    private UiFlowDirection _direction;
    /// <summary>Sets layout direction for descendants. Inherit uses the enclosing provider's direction.</summary>
    public UiFlowDirection Direction
    {
        get => _direction;
        set
        {
            ThrowIfDisposed();
            if ((uint)value > (uint)UiFlowDirection.RightToLeft)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_direction == value)
                return;
            ThrowIfBuilding();
            _direction = value;
            RequestRebuild();
        }
    }
    /// <summary>Overrides only typography while retaining the inherited palette and geometry.</summary>
    public UiTypographyFonts? Fonts
    {
        get => _fonts;
        set
        {
            ThrowIfDisposed();
            if (_fonts == value) return;
            if (value is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value.Regular);
                ArgumentException.ThrowIfNullOrWhiteSpace(value.Semibold);
                ArgumentException.ThrowIfNullOrWhiteSpace(value.Bold);
            }
            ThrowIfBuilding();
            _fonts = value;
            RequestRebuild();
        }
    }
    public UiFluentProviderTheme? Theme
    {
        get => _theme;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_theme, value)) return;
            ThrowIfBuilding();
            _theme = value;
            RequestRebuild();
        }
    }
    public bool PaintBackground
    {
        get => _paintBackground;
        set
        {
            ThrowIfDisposed();
            if (_paintBackground == value) return;
            ThrowIfBuilding();
            _paintBackground = value;
            RequestRebuild();
        }
    }
    public UiThemeProvider(UiWidget child, UiFluentProviderTheme? theme = null,
        UiContainerOptions options = default, bool paintBackground = true,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(child, options, identity)
    {
        _theme = theme;
        _paintBackground = paintBackground;
    }
    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        using var scope = context.BeginFluentProvider(_theme, _fonts, _direction);
        return base.MeasureContent(context);
    }
    protected override void RenderControl(UiImmediateContext ui)
    {
        using var scope = ui.BeginFluentProvider(_theme, _fonts, _direction);
        var layout = GetLayout(new(ui), render : true);
        if (_paintBackground) ui.RenderContainer<UiFluentProviderPresentation>(Child, layout);
        else ui.RenderContainer<UiPlainContainerPresentation>(Child, layout);
    }
}

public readonly struct UiFluentProviderPresentation : IUiContainerPresentation
{
    public static void Paint(UiControlContext context, UiRect bounds)
        => context.Frame(bounds, context.Theme.WindowBg, default, 0, 0);
}

[UiWidgetStyle(typeof(UiThemeProvider))]
[UiComponent("FluentProvider", UiComponentExposure.Fluent)]
public partial class UiFluentProvider;
