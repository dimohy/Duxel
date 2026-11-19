namespace Duxel.Core;

public enum UiSegmentedControlStyle
{
    NeutralOverNavBarPill, NeutralOverCanvasPill, BrandOverNavBarPill, BrandOverCanvasPill
}

public readonly record struct UiSegmentedControlPaint(UiColor Surface, UiColor SelectedSurface,
    UiColor Foreground, UiColor SelectedForeground, UiColor Unread);

/// <summary>Fluent Apple standard light/dark tokens; forced colors remain owned by the platform.</summary>
public static class UiSegmentedControlPalette
{
    public static UiSegmentedControlPaint Resolve(UiSegmentedControlStyle style, UiFluentColorScheme scheme, bool enabled = true)
    {
        if ((uint)style > 3) throw new ArgumentOutOfRangeException(nameof(style));
        if (scheme is not (UiFluentColorScheme.Light or UiFluentColorScheme.Dark)) throw new ArgumentOutOfRangeException(nameof(scheme));
        var dark = scheme == UiFluentColorScheme.Dark;
        UiColor Adaptive(uint light, uint darkValue) => Rgb(dark ? darkValue : light);
        var surface = style switch
        {
            UiSegmentedControlStyle.NeutralOverNavBarPill => Adaptive(0xF0F0F0, 0x3D3D3D),
            UiSegmentedControlStyle.NeutralOverCanvasPill or UiSegmentedControlStyle.BrandOverCanvasPill => Adaptive(0xFFFFFF, 0x292929),
            UiSegmentedControlStyle.BrandOverNavBarPill => Adaptive(0x115EA3, 0x3D3D3D),
            _ => throw new ArgumentOutOfRangeException(nameof(style))
        };
        var selected = style switch
        {
            UiSegmentedControlStyle.NeutralOverNavBarPill or UiSegmentedControlStyle.NeutralOverCanvasPill => Adaptive(0x0F6CBD, 0x479EF5),
            UiSegmentedControlStyle.BrandOverNavBarPill => Adaptive(0xFFFFFF, 0x616161),
            UiSegmentedControlStyle.BrandOverCanvasPill => Adaptive(enabled ? 0x0F6CBDu : 0x0F548Cu, 0x4D4D4D),
            _ => throw new ArgumentOutOfRangeException(nameof(style))
        };
        var foreground = style switch
        {
            UiSegmentedControlStyle.NeutralOverNavBarPill or UiSegmentedControlStyle.NeutralOverCanvasPill =>
                enabled ? Adaptive(0x616161, 0xD6D6D6) : Adaptive(0xBDBDBD, 0x5C5C5C),
            UiSegmentedControlStyle.BrandOverNavBarPill => enabled ? Adaptive(0xFFFFFF, 0xD6D6D6) : Adaptive(0x2886DE, 0x5C5C5C),
            UiSegmentedControlStyle.BrandOverCanvasPill => enabled ? Adaptive(0x0F6CBD, 0xD6D6D6) : Adaptive(0xB4D6FA, 0x5C5C5C),
            _ => throw new ArgumentOutOfRangeException(nameof(style))
        };
        var selectedForeground = style switch
        {
            UiSegmentedControlStyle.NeutralOverNavBarPill or UiSegmentedControlStyle.NeutralOverCanvasPill =>
                enabled ? Adaptive(0xFFFFFF, 0x000000) : Rgb(0x2886DE),
            UiSegmentedControlStyle.BrandOverNavBarPill => enabled ? Adaptive(0x0F6CBD, 0xFFFFFF) : Adaptive(0xB4D6FA, 0x2E2E2E),
            UiSegmentedControlStyle.BrandOverCanvasPill => enabled ? Rgb(0xFFFFFF) : Adaptive(0x2886DE, 0x2E2E2E),
            _ => throw new ArgumentOutOfRangeException(nameof(style))
        };
        var unread = enabled ? Adaptive(style == UiSegmentedControlStyle.BrandOverNavBarPill ? 0xFFFFFFu : 0x0F6CBDu, 0xFFFFFF) : foreground;
        return new(surface, selected, foreground, selectedForeground, unread);
    }
    private static UiColor Rgb(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
