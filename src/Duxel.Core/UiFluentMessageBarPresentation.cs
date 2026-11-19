// Token and icon mapping: Fluent UI f9720c35 (MIT, Microsoft Corporation).
namespace Duxel.Core;

public readonly record struct UiMessageBarPalette(UiColor Background, UiColor Border, UiColor Foreground, UiColor Icon);

public readonly struct UiFluentMessageBarPresentation : IUiMessageBarPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiMessageBarMetrics Metrics => new(1, 12, 20, 8, 36, 10, 8, 10, 4);
    public static float SingleLineHeight(UiControlContext context)
        => context.MeasureTypography<UiRegularTypeface, UiFluentType300>(string.Empty).Y;
    private static UiColor Rgb(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    public static UiMessageBarPalette ResolvePalette(UiFluentColorScheme scheme, UiMessageBarIntent intent)
    {
        var text = scheme switch
        {
            UiFluentColorScheme.Light => Rgb(0x242424),
            UiFluentColorScheme.Dark => Rgb(0xffffff),
            _ => throw new ArgumentOutOfRangeException(nameof(scheme))
        };
        var (background, border, icon) = (scheme, intent) switch
        {
            (UiFluentColorScheme.Light, UiMessageBarIntent.Info) => (0xf5f5f5u, 0xd1d1d1u, 0x616161u),
            (UiFluentColorScheme.Light, UiMessageBarIntent.Success) => (0xf1faf1u, 0x9fd89fu, 0x0e700eu),
            (UiFluentColorScheme.Light, UiMessageBarIntent.Warning) => (0xfff9f5u, 0xfdcfb4u, 0xbc4b09u),
            (UiFluentColorScheme.Light, UiMessageBarIntent.Error) => (0xfdf3f4u, 0xeeacb2u, 0xb10e1cu),
            (UiFluentColorScheme.Dark, UiMessageBarIntent.Info) => (0x141414u, 0x666666u, 0xadadadu),
            (UiFluentColorScheme.Dark, UiMessageBarIntent.Success) => (0x052505u, 0x107c10u, 0x54b054u),
            (UiFluentColorScheme.Dark, UiMessageBarIntent.Warning) => (0x4a1e04u, 0xf7630cu, 0xf98845u),
            (UiFluentColorScheme.Dark, UiMessageBarIntent.Error) => (0x3b0509u, 0xc50f1fu, 0xdc626du),
            _ => throw new ArgumentOutOfRangeException(nameof(intent))
        };
        return new(Rgb(background), Rgb(border), text, Rgb(icon));
    }
    public static UiColor? Foreground(UiControlContext context, UiMessageBarOptions options)
        => ResolvePalette(context.FluentColorScheme, options.Intent).Foreground;
    public static void Paint(UiControlContext context, UiRect bounds, UiRect icon, UiMessageBarOptions options)
    {
        var palette = ResolvePalette(context.FluentColorScheme, options.Intent);
        context.Frame(bounds, palette.Background, palette.Border, options.Shape == UiMessageBarShape.Square ? 0 : Metrics.CornerRadius, Metrics.Border);
        if (options.HideIcon || icon.Width <= 0 || icon.Height <= 0)
            return;
        switch (options.Intent)
        {
            case UiMessageBarIntent.Info:
                UiMessageBarInfoIcon.Paint(context, icon, palette.Icon, default);
                break;
            case UiMessageBarIntent.Success:
                UiMessageBarSuccessIcon.Paint(context, icon, palette.Icon, default);
                break;
            case UiMessageBarIntent.Warning:
                UiMessageBarWarningIcon.Paint(context, icon, palette.Icon, default);
                break;
            case UiMessageBarIntent.Error:
                UiMessageBarErrorIcon.Paint(context, icon, palette.Icon, default);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(options));
        }
    }
}

[UiFluentIcon("info", UiFluentIconStyle.Filled, 20)] internal readonly partial struct UiMessageBarInfoIcon;
[UiFluentIcon("checkmark_circle", UiFluentIconStyle.Filled, 20)] internal readonly partial struct UiMessageBarSuccessIcon;
[UiFluentIcon("warning", UiFluentIconStyle.Filled, 20)] internal readonly partial struct UiMessageBarWarningIcon;
[UiFluentIcon("diamond_dismiss", UiFluentIconStyle.Filled, 20)] internal readonly partial struct UiMessageBarErrorIcon;
