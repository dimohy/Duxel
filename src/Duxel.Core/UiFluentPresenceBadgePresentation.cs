namespace Duxel.Core;

public readonly record struct UiPresenceBadgePaint(UiColor Foreground, UiColor Background);

public readonly struct UiFluentPresenceBadgePresentation : IUiPresenceBadgePresentation
{
    public static bool CanRetainMeasurement => true;
    public static int Edge(UiBadgeSize size) => size switch
    {
        UiBadgeSize.Tiny => 6, UiBadgeSize.ExtraSmall => 10, UiBadgeSize.Small => 12,
        UiBadgeSize.Medium => 16, UiBadgeSize.Large => 20, UiBadgeSize.ExtraLarge => 28,
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };
    public static UiPresenceBadgePaint Resolve(UiFluentColorScheme scheme, UiPresenceBadgeOptions options)
    {
        options.Validate();
        if ((uint)scheme > 1) throw new ArgumentOutOfRangeException(nameof(scheme));
        var dark = scheme == UiFluentColorScheme.Dark;
        var color = options.Status switch
        {
            UiPresenceStatus.Available => dark ? new UiColor(0x3d, 0xb8, 0x38) : new(0x13, 0xa1, 0x0e),
            UiPresenceStatus.Busy or UiPresenceStatus.DoNotDisturb or UiPresenceStatus.Blocked => new(0xd1, 0x34, 0x38),
            UiPresenceStatus.Away when !options.OutOfOffice => new(0xea, 0xa3, 0x00),
            UiPresenceStatus.OutOfOffice or UiPresenceStatus.Away => dark ? new UiColor(0xd1, 0x61, 0xc4) : new(0xc2, 0x39, 0xb3),
            UiPresenceStatus.Offline when options.OutOfOffice => dark ? new UiColor(0xd1, 0x61, 0xc4) : new(0xc2, 0x39, 0xb3),
            _ => dark ? new UiColor(0xad, 0xad, 0xad) : new(0x61, 0x61, 0x61)
        };
        return new(color, dark ? new(0x29, 0x29, 0x29) : new(255, 255, 255));
    }
    public static UiVector2 Measure(UiControlContext context, UiPresenceBadgeOptions options) => new(Edge(options.Size), Edge(options.Size));
    public static void Paint(UiControlContext context, UiRect bounds, UiPresenceBadgeContent content, UiPresenceBadgeOptions options)
    {
        var edge = MathF.Min(bounds.Width, bounds.Height);
        if (edge <= 0) return;
        var square = new UiRect(bounds.X + (bounds.Width - edge) * .5f, bounds.Y + (bounds.Height - edge) * .5f, edge, edge);
        var paint = Resolve(context.FluentColorScheme, options);
        var backgroundInset = options.Size == UiBadgeSize.Tiny ? 0 : MathF.Min(1, edge * .5f);
        context.Circle(new(square.X + edge * .5f, square.Y + edge * .5f), edge * .5f - backgroundInset, paint.Background);
        var icon = content.Icon.IsEmpty ? UiPresenceBadgeIcons.Select(options) : content.Icon;
        icon.Paint(context, square, paint.Foreground, default);
    }
}
