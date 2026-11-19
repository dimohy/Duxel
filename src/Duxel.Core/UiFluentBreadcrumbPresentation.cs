// Breadcrumb metrics follow Fluent UI (MIT, Microsoft Corporation); see Duxel.Core.ThirdPartyNotices.txt.
namespace Duxel.Core;

[UiFluentIcon("chevron_right")]
internal readonly partial struct UiBreadcrumbChevronRight;
[UiFluentIcon("chevron_left")]
internal readonly partial struct UiBreadcrumbChevronLeft;
[UiFluentIcon("more_horizontal")]
internal readonly partial struct UiBreadcrumbMore;

public readonly struct UiFluentBreadcrumbPresentation : IUiBreadcrumbPresentation
{
    private static readonly UiMenuIcon Right = UiMenuIcon.Create<UiBreadcrumbChevronRight, UiBreadcrumbChevronRight>();
    private static readonly UiMenuIcon Left = UiMenuIcon.Create<UiBreadcrumbChevronLeft, UiBreadcrumbChevronLeft>();
    internal static readonly UiMenuIcon More = UiMenuIcon.Create<UiBreadcrumbMore, UiBreadcrumbMore>();
    public static bool CanRetainMeasurement => true;
    public static float Height(UiBreadcrumbOptions options) => options.Size switch
    {
        UiBreadcrumbSize.Small => 24,
        UiBreadcrumbSize.Medium => 32,
        UiBreadcrumbSize.Large => 40,
        _ => throw new ArgumentOutOfRangeException(nameof(options))
    };
    public static float DividerWidth(UiBreadcrumbOptions options) => options.Size switch
    {
        UiBreadcrumbSize.Small => 12,
        UiBreadcrumbSize.Medium => 16,
        UiBreadcrumbSize.Large => 20,
        _ => throw new ArgumentOutOfRangeException(nameof(options))
    };
    private static float Padding(UiBreadcrumbOptions options) => options.Size == UiBreadcrumbSize.Large ? 8 : 6;
    private static UiControlTypographyScope Typography(UiControlContext context, UiBreadcrumbOptions options, bool current)
        => (options.Size, current) switch
        {
            (UiBreadcrumbSize.Small, false) => context.BeginTypography<UiRegularTypeface, UiFluentType200>(),
            (UiBreadcrumbSize.Small, true) => context.BeginTypography<UiSemiboldTypeface, UiFluentType200>(),
            (UiBreadcrumbSize.Medium, false) => context.BeginTypography<UiRegularTypeface, UiFluentType300>(),
            (UiBreadcrumbSize.Medium, true) => context.BeginTypography<UiSemiboldTypeface, UiFluentType300>(),
            (UiBreadcrumbSize.Large, false) => context.BeginTypography<UiRegularTypeface, UiFluentType400>(),
            (UiBreadcrumbSize.Large, true) => context.BeginTypography<UiSemiboldTypeface, UiFluentType400>(),
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
    public static UiVector2 MeasureItem(UiControlContext context, UiBreadcrumbItemContent item, UiBreadcrumbOptions options)
    {
        using var typography = Typography(context, options, item.Current);
        return new(context.MeasureText(item.Text).X + Padding(options) * 2 + (item.Icon.IsEmpty ? 0 : DividerWidth(options) + 4), Height(options));
    }
    public static void PaintItem(UiControlContext context, UiBreadcrumbItemContent item, UiRect bounds,
        UiButtonInteraction interaction, UiBreadcrumbOptions options)
    {
        using var typography = Typography(context, options, item.Current);
        var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
        var color = !context.IsEnabled ? dark ? new UiColor(92, 92, 92) : new(189, 189, 189)
            : interaction.Hovered && !item.Current ? dark ? new(255, 255, 255) : new(36, 36, 36)
            : dark ? new(214, 214, 214) : new(66, 66, 66);
        if (!item.Current && context.IsEnabled && (interaction.Hovered || interaction.Held))
            context.Frame(bounds, dark ? interaction.Held ? new(46, 46, 46) : new UiColor(56, 56, 56)
                : interaction.Held ? new(224, 224, 224) : new(245, 245, 245), default, 4, 0);
        var text = context.MeasureTextBlock(item.Text);
        var padding = Padding(options);
        var icon = DividerWidth(options);
        if (!item.Icon.IsEmpty)
        {
            var iconX = options.RightToLeft ? bounds.X + bounds.Width - padding - icon : bounds.X + padding;
            item.Icon.Paint(context, new(iconX, bounds.Y + (bounds.Height - icon) / 2, icon, icon), color, interaction);
        }
        var textX = options.RightToLeft ? bounds.X + bounds.Width - padding - text.Size.X - (item.Icon.IsEmpty ? 0 : icon + 4)
            : bounds.X + padding + (item.Icon.IsEmpty ? 0 : icon + 4);
        context.TypographyTextBlock(text,
            new(textX, bounds.Y + (bounds.Height - text.Size.Y) / 2, text.Size.X, text.Size.Y), color);
        if (interaction.FocusVisible)
            context.FocusOutline(bounds, 4);
    }
    public static void PaintDivider(UiControlContext context, UiRect bounds, UiBreadcrumbOptions options)
    {
        var icon = options.RightToLeft ? Left : Right;
        var edge = DividerWidth(options);
        icon.Paint(context, new(bounds.X + (bounds.Width - edge) / 2, bounds.Y + (bounds.Height - edge) / 2, edge, edge),
            context.FluentColorScheme == UiFluentColorScheme.Dark ? new(214, 214, 214) : new(66, 66, 66), default);
    }
}
