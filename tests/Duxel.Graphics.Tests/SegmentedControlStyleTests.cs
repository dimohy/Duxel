using Duxel.Core;

internal static class SegmentedControlStyleTests
{
    internal static void Run(Action<bool, string> check)
    {
        // Literal reference rows transcribed from the pinned iOS token and alias sources.
        uint[][] expected =
        [
            [0xF0F0F0, 0x0F6CBD, 0x616161, 0xFFFFFF, 0x0F6CBD],
            [0xF0F0F0, 0x0F6CBD, 0xBDBDBD, 0x2886DE, 0xBDBDBD],
            [0x3D3D3D, 0x479EF5, 0xD6D6D6, 0x000000, 0xFFFFFF],
            [0x3D3D3D, 0x479EF5, 0x5C5C5C, 0x2886DE, 0x5C5C5C],
            [0xFFFFFF, 0x0F6CBD, 0x616161, 0xFFFFFF, 0x0F6CBD],
            [0xFFFFFF, 0x0F6CBD, 0xBDBDBD, 0x2886DE, 0xBDBDBD],
            [0x292929, 0x479EF5, 0xD6D6D6, 0x000000, 0xFFFFFF],
            [0x292929, 0x479EF5, 0x5C5C5C, 0x2886DE, 0x5C5C5C],
            [0x115EA3, 0xFFFFFF, 0xFFFFFF, 0x0F6CBD, 0xFFFFFF],
            [0x115EA3, 0xFFFFFF, 0x2886DE, 0xB4D6FA, 0x2886DE],
            [0x3D3D3D, 0x616161, 0xD6D6D6, 0xFFFFFF, 0xFFFFFF],
            [0x3D3D3D, 0x616161, 0x5C5C5C, 0x2E2E2E, 0x5C5C5C],
            [0xFFFFFF, 0x0F6CBD, 0x0F6CBD, 0xFFFFFF, 0x0F6CBD],
            [0xFFFFFF, 0x0F548C, 0xB4D6FA, 0x2886DE, 0xB4D6FA],
            [0x292929, 0x4D4D4D, 0xD6D6D6, 0xFFFFFF, 0xFFFFFF],
            [0x292929, 0x4D4D4D, 0x5C5C5C, 0x2E2E2E, 0x5C5C5C]
        ];
        var row = 0;
        foreach (var style in Enum.GetValues<UiSegmentedControlStyle>())
            foreach (var scheme in new[] { UiFluentColorScheme.Light, UiFluentColorScheme.Dark })
                foreach (var enabled in new[] { true, false })
                {
                    var paint = UiSegmentedControlPalette.Resolve(style, scheme, enabled);
                    var source = expected[row++];
                    check(paint == new UiSegmentedControlPaint(Color(source[0]), Color(source[1]), Color(source[2]), Color(source[3]), Color(source[4])),
                        style + " " + scheme + " enabled=" + enabled + " matches all five official color roles");
                }
        var timing = UiSegmentedControlMotion.Options(false);
        check(timing.DurationSeconds == .2 && timing.Curve == new UiCubicBezier(0, 0, .58, 1)
            && Math.Abs(timing.Curve.Value.Evaluate(.5) - .6846431874) < .00001,
            "selection movement uses the verified200ms ease-out timing curve rather than an approximate easing enum");
        check(UiSegmentedControlTypography.FontSize == 15 && UiSegmentedControlTypography.LineHeight == 20,
            "segmented typography retains the official15-unit body2 size in the shared line box");
        try { UiSegmentedControlPalette.Resolve((UiSegmentedControlStyle)99, UiFluentColorScheme.Light); check(false, "invalid segmented style is rejected"); }
        catch (ArgumentOutOfRangeException) { check(true, "invalid segmented style is rejected"); }
        try { UiSegmentedControlPalette.Resolve(default, (UiFluentColorScheme)99); check(false, "invalid segmented scheme is rejected"); }
        catch (ArgumentOutOfRangeException) { check(true, "invalid segmented scheme is rejected"); }
    }
    private static UiColor Color(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
