namespace Duxel.Core;

internal readonly record struct UiStyledTextPalette(UiColor Foreground, UiColor Syntax, UiColor Link,
    UiColor CodeBackground, UiColor HeaderBackground, UiColor Border, bool Dark = false, bool Forced = false, bool Enabled = true)
{
    internal static UiStyledTextPalette Resolve(bool dark, UiForcedColorPalette? system, UiColor foreground, bool enabled)
    {
        if (system is { } colors)
        {
            var text = enabled ? colors.CanvasText : colors.GrayText;
            return new(text, text, enabled ? colors.LinkText : text, colors.Canvas, colors.Canvas, text, dark, true, enabled);
        }
        return new(foreground, dark ? new(150, 150, 150) : new(105, 105, 105),
            dark ? new(96, 205, 255) : new(0, 103, 192),
            dark ? new(48, 48, 48) : new(242, 242, 242),
            dark ? new(45, 45, 45) : new(245, 245, 245),
            UiFluentDividerPalette.Resolve(dark ? UiFluentColorScheme.Dark : UiFluentColorScheme.Light,
                UiDividerAppearance.Default).Line, dark, false, enabled);
    }

    internal UiColor Text(UiStyledTextStyle style)
        => style.CodeToken != UiCodeToken.None && !Forced && Enabled ? CodeColor(style.CodeToken)
            : (style.Flags & UiStyledTextFlags.Syntax) != 0 ? Syntax
            : (style.Flags & UiStyledTextFlags.Link) != 0 ? Link : Foreground;
    private UiColor CodeColor(UiCodeToken token) => token switch
    {
        UiCodeToken.Keyword => Dark ? new(86, 156, 214) : new(0, 0, 180),
        UiCodeToken.String => Dark ? new(206, 145, 120) : new(163, 21, 21),
        UiCodeToken.Interpolation => Dark ? new(218, 112, 214) : new(145, 32, 140),
        UiCodeToken.Escape => Dark ? new(215, 186, 125) : new(145, 85, 0),
        UiCodeToken.Method => Dark ? new(220, 220, 170) : new(121, 94, 38),
        UiCodeToken.Operator => Dark ? new(180, 180, 180) : new(75, 75, 75),
        UiCodeToken.Comment => Dark ? new(106, 153, 85) : new(0, 110, 0),
        UiCodeToken.Number => Dark ? new(181, 206, 168) : new(9, 134, 88),
        UiCodeToken.Type => Dark ? new(78, 201, 176) : new(38, 127, 153),
        UiCodeToken.Directive => Dark ? new(197, 134, 192) : new(128, 0, 128),
        _ => Foreground
    };

}
