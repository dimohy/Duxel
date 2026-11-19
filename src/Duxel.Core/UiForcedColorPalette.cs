namespace Duxel.Core;

/// <summary>System-selected forced colors. Null on a theme means forced colors are inactive.</summary>
public readonly record struct UiForcedColorPalette(
    UiColor Canvas, UiColor CanvasText, UiColor GrayText, UiColor Highlight,
    UiColor HighlightText, UiColor LinkText, UiColor ButtonFace, UiColor ButtonText)
{
    /// <summary>Replaces shared color roles while preserving layout, typography and widget skins.</summary>
    public UiTheme Apply(UiTheme theme)
    {
        theme.ForcedColors = this;
        theme.Text = CanvasText;
        theme.TextDisabled = GrayText;
        theme.WindowBg = theme.TitleBg = theme.TitleBgActive = theme.MenuBarBg = theme.PopupBg = Canvas;
        theme.Border = theme.Separator = theme.TableBorder = CanvasText;
        theme.FrameBg = theme.FrameBgHovered = theme.FrameBgActive = Canvas;
        theme.Header = theme.HeaderHovered = theme.HeaderActive = Canvas;
        theme.Button = theme.ButtonHovered = theme.ButtonActive = ButtonFace;
        theme.Tab = theme.TabHovered = theme.TabActive = Canvas;
        theme.CheckMark = theme.SliderGrab = theme.SliderGrabActive = Highlight;
        theme.PlotLines = theme.PlotHistogram = CanvasText;
        theme.TableHeaderBg = theme.TableRowBg0 = theme.TableRowBg1 = Canvas;
        theme.TextSelectedBg = Highlight;
        theme.ScrollbarBg = Canvas;
        theme.ScrollbarGrab = theme.ScrollbarGrabHovered = theme.ScrollbarGrabActive = CanvasText;
        theme.InitWidgetDefaults();
        theme.ButtonText = ButtonText;
        theme.ButtonBorder = theme.ButtonBorderHovered = theme.ButtonBorderActive = ButtonText;
        theme.InputBorder = theme.InputBorderHovered = theme.InputBorderActive = CanvasText;
        theme.CheckboxBorder = theme.CheckboxBorderHovered = theme.CheckboxBorderActive = CanvasText;
        theme.RadioButtonBorder = theme.RadioButtonBorderHovered = theme.RadioButtonBorderActive = CanvasText;
        theme.ComboBorder = theme.ComboBorderHovered = theme.ComboBorderActive = CanvasText;
        theme.DragBorder = theme.DragBorderHovered = theme.DragBorderActive = CanvasText;
        return theme;
    }
}
