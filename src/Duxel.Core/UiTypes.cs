using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Duxel.Core;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct UiVector2(float X, float Y);

public readonly record struct UiVector4(float X, float Y, float Z, float W);

public readonly record struct UiRect(float X, float Y, float Width, float Height);

[StructLayout(LayoutKind.Sequential)]
public readonly record struct UiColor(uint Rgba)
{
    /// <summary>
    /// Creates a UiColor from individual RGBA byte components.
    /// </summary>
    public UiColor(byte r, byte g, byte b, byte a = 255)
        : this((uint)a << 24 | (uint)b << 16 | (uint)g << 8 | r) { }
}

public readonly record struct UiLayerOptions(
    bool StaticCache,
    float Opacity,
    UiVector2 Translation
);

public readonly record struct UiLayerCardInteraction(
    bool Clicked,
    bool Held,
    bool Released,
    bool Hovered,
    UiVector2 MousePosition
);

public readonly record struct UiFontResource(UiFontAtlas FontAtlas, UiTextureId TextureId, ulong CodepointSignature = 0UL);

[InlineArray(StyleColorCount)]
public struct UiThemeColors
{
    private UiColor _element0;
    public const int StyleColorCount = 110;
}

public struct UiTheme
{
    private UiThemeColors _colors;

    public UiColor this[UiStyleColor color]
    {
        readonly get => _colors[(int)color];
        set => _colors[(int)color] = value;
    }

    // Named property wrappers — backward compatibility with _theme.Button etc.
    public UiColor Text { readonly get => _colors[(int)UiStyleColor.Text]; set => _colors[(int)UiStyleColor.Text] = value; }
    public UiColor TextDisabled { readonly get => _colors[(int)UiStyleColor.TextDisabled]; set => _colors[(int)UiStyleColor.TextDisabled] = value; }
    public UiColor WindowBg { readonly get => _colors[(int)UiStyleColor.WindowBg]; set => _colors[(int)UiStyleColor.WindowBg] = value; }
    public UiColor TitleBg { readonly get => _colors[(int)UiStyleColor.TitleBg]; set => _colors[(int)UiStyleColor.TitleBg] = value; }
    public UiColor TitleBgActive { readonly get => _colors[(int)UiStyleColor.TitleBgActive]; set => _colors[(int)UiStyleColor.TitleBgActive] = value; }
    public UiColor MenuBarBg { readonly get => _colors[(int)UiStyleColor.MenuBarBg]; set => _colors[(int)UiStyleColor.MenuBarBg] = value; }
    public UiColor PopupBg { readonly get => _colors[(int)UiStyleColor.PopupBg]; set => _colors[(int)UiStyleColor.PopupBg] = value; }
    public UiColor Border { readonly get => _colors[(int)UiStyleColor.Border]; set => _colors[(int)UiStyleColor.Border] = value; }
    public UiColor FrameBg { readonly get => _colors[(int)UiStyleColor.FrameBg]; set => _colors[(int)UiStyleColor.FrameBg] = value; }
    public UiColor FrameBgHovered { readonly get => _colors[(int)UiStyleColor.FrameBgHovered]; set => _colors[(int)UiStyleColor.FrameBgHovered] = value; }
    public UiColor FrameBgActive { readonly get => _colors[(int)UiStyleColor.FrameBgActive]; set => _colors[(int)UiStyleColor.FrameBgActive] = value; }
    public UiColor Header { readonly get => _colors[(int)UiStyleColor.Header]; set => _colors[(int)UiStyleColor.Header] = value; }
    public UiColor HeaderHovered { readonly get => _colors[(int)UiStyleColor.HeaderHovered]; set => _colors[(int)UiStyleColor.HeaderHovered] = value; }
    public UiColor HeaderActive { readonly get => _colors[(int)UiStyleColor.HeaderActive]; set => _colors[(int)UiStyleColor.HeaderActive] = value; }
    public UiColor Button { readonly get => _colors[(int)UiStyleColor.Button]; set => _colors[(int)UiStyleColor.Button] = value; }
    public UiColor ButtonHovered { readonly get => _colors[(int)UiStyleColor.ButtonHovered]; set => _colors[(int)UiStyleColor.ButtonHovered] = value; }
    public UiColor ButtonActive { readonly get => _colors[(int)UiStyleColor.ButtonActive]; set => _colors[(int)UiStyleColor.ButtonActive] = value; }
    public UiColor Tab { readonly get => _colors[(int)UiStyleColor.Tab]; set => _colors[(int)UiStyleColor.Tab] = value; }
    public UiColor TabHovered { readonly get => _colors[(int)UiStyleColor.TabHovered]; set => _colors[(int)UiStyleColor.TabHovered] = value; }
    public UiColor TabActive { readonly get => _colors[(int)UiStyleColor.TabActive]; set => _colors[(int)UiStyleColor.TabActive] = value; }
    public UiColor CheckMark { readonly get => _colors[(int)UiStyleColor.CheckMark]; set => _colors[(int)UiStyleColor.CheckMark] = value; }
    public UiColor SliderGrab { readonly get => _colors[(int)UiStyleColor.SliderGrab]; set => _colors[(int)UiStyleColor.SliderGrab] = value; }
    public UiColor SliderGrabActive { readonly get => _colors[(int)UiStyleColor.SliderGrabActive]; set => _colors[(int)UiStyleColor.SliderGrabActive] = value; }
    public UiColor PlotLines { readonly get => _colors[(int)UiStyleColor.PlotLines]; set => _colors[(int)UiStyleColor.PlotLines] = value; }
    public UiColor PlotHistogram { readonly get => _colors[(int)UiStyleColor.PlotHistogram]; set => _colors[(int)UiStyleColor.PlotHistogram] = value; }
    public UiColor Separator { readonly get => _colors[(int)UiStyleColor.Separator]; set => _colors[(int)UiStyleColor.Separator] = value; }
    public UiColor TableHeaderBg { readonly get => _colors[(int)UiStyleColor.TableHeaderBg]; set => _colors[(int)UiStyleColor.TableHeaderBg] = value; }
    public UiColor TableRowBg0 { readonly get => _colors[(int)UiStyleColor.TableRowBg0]; set => _colors[(int)UiStyleColor.TableRowBg0] = value; }
    public UiColor TableRowBg1 { readonly get => _colors[(int)UiStyleColor.TableRowBg1]; set => _colors[(int)UiStyleColor.TableRowBg1] = value; }
    public UiColor TableBorder { readonly get => _colors[(int)UiStyleColor.TableBorder]; set => _colors[(int)UiStyleColor.TableBorder] = value; }
    public UiColor TextSelectedBg { readonly get => _colors[(int)UiStyleColor.TextSelectedBg]; set => _colors[(int)UiStyleColor.TextSelectedBg] = value; }
    public UiColor ScrollbarBg { readonly get => _colors[(int)UiStyleColor.ScrollbarBg]; set => _colors[(int)UiStyleColor.ScrollbarBg] = value; }
    public UiColor ScrollbarGrab { readonly get => _colors[(int)UiStyleColor.ScrollbarGrab]; set => _colors[(int)UiStyleColor.ScrollbarGrab] = value; }
    public UiColor ScrollbarGrabHovered { readonly get => _colors[(int)UiStyleColor.ScrollbarGrabHovered]; set => _colors[(int)UiStyleColor.ScrollbarGrabHovered] = value; }
    public UiColor ScrollbarGrabActive { readonly get => _colors[(int)UiStyleColor.ScrollbarGrabActive]; set => _colors[(int)UiStyleColor.ScrollbarGrabActive] = value; }

    // === Widget-Specific Token Properties (Layer 2) ===

    // Button
    public UiColor ButtonText { readonly get => _colors[(int)UiStyleColor.ButtonText]; set => _colors[(int)UiStyleColor.ButtonText] = value; }
    public UiColor ButtonBorder { readonly get => _colors[(int)UiStyleColor.ButtonBorder]; set => _colors[(int)UiStyleColor.ButtonBorder] = value; }
    public UiColor ButtonBorderHovered { readonly get => _colors[(int)UiStyleColor.ButtonBorderHovered]; set => _colors[(int)UiStyleColor.ButtonBorderHovered] = value; }
    public UiColor ButtonBorderActive { readonly get => _colors[(int)UiStyleColor.ButtonBorderActive]; set => _colors[(int)UiStyleColor.ButtonBorderActive] = value; }

    // Checkbox
    public UiColor CheckboxText { readonly get => _colors[(int)UiStyleColor.CheckboxText]; set => _colors[(int)UiStyleColor.CheckboxText] = value; }
    public UiColor CheckboxBg { readonly get => _colors[(int)UiStyleColor.CheckboxBg]; set => _colors[(int)UiStyleColor.CheckboxBg] = value; }
    public UiColor CheckboxBgHovered { readonly get => _colors[(int)UiStyleColor.CheckboxBgHovered]; set => _colors[(int)UiStyleColor.CheckboxBgHovered] = value; }
    public UiColor CheckboxBgActive { readonly get => _colors[(int)UiStyleColor.CheckboxBgActive]; set => _colors[(int)UiStyleColor.CheckboxBgActive] = value; }
    public UiColor CheckboxBorder { readonly get => _colors[(int)UiStyleColor.CheckboxBorder]; set => _colors[(int)UiStyleColor.CheckboxBorder] = value; }
    public UiColor CheckboxBorderHovered { readonly get => _colors[(int)UiStyleColor.CheckboxBorderHovered]; set => _colors[(int)UiStyleColor.CheckboxBorderHovered] = value; }
    public UiColor CheckboxBorderActive { readonly get => _colors[(int)UiStyleColor.CheckboxBorderActive]; set => _colors[(int)UiStyleColor.CheckboxBorderActive] = value; }

    // RadioButton
    public UiColor RadioButtonText { readonly get => _colors[(int)UiStyleColor.RadioButtonText]; set => _colors[(int)UiStyleColor.RadioButtonText] = value; }
    public UiColor RadioButtonBg { readonly get => _colors[(int)UiStyleColor.RadioButtonBg]; set => _colors[(int)UiStyleColor.RadioButtonBg] = value; }
    public UiColor RadioButtonBgHovered { readonly get => _colors[(int)UiStyleColor.RadioButtonBgHovered]; set => _colors[(int)UiStyleColor.RadioButtonBgHovered] = value; }
    public UiColor RadioButtonBgActive { readonly get => _colors[(int)UiStyleColor.RadioButtonBgActive]; set => _colors[(int)UiStyleColor.RadioButtonBgActive] = value; }
    public UiColor RadioButtonBorder { readonly get => _colors[(int)UiStyleColor.RadioButtonBorder]; set => _colors[(int)UiStyleColor.RadioButtonBorder] = value; }
    public UiColor RadioButtonBorderHovered { readonly get => _colors[(int)UiStyleColor.RadioButtonBorderHovered]; set => _colors[(int)UiStyleColor.RadioButtonBorderHovered] = value; }
    public UiColor RadioButtonBorderActive { readonly get => _colors[(int)UiStyleColor.RadioButtonBorderActive]; set => _colors[(int)UiStyleColor.RadioButtonBorderActive] = value; }

    // Input
    public UiColor InputText { readonly get => _colors[(int)UiStyleColor.InputText]; set => _colors[(int)UiStyleColor.InputText] = value; }
    public UiColor InputBg { readonly get => _colors[(int)UiStyleColor.InputBg]; set => _colors[(int)UiStyleColor.InputBg] = value; }
    public UiColor InputBgHovered { readonly get => _colors[(int)UiStyleColor.InputBgHovered]; set => _colors[(int)UiStyleColor.InputBgHovered] = value; }
    public UiColor InputBgActive { readonly get => _colors[(int)UiStyleColor.InputBgActive]; set => _colors[(int)UiStyleColor.InputBgActive] = value; }
    public UiColor InputSelectionBg { readonly get => _colors[(int)UiStyleColor.InputSelectionBg]; set => _colors[(int)UiStyleColor.InputSelectionBg] = value; }
    public UiColor InputBorder { readonly get => _colors[(int)UiStyleColor.InputBorder]; set => _colors[(int)UiStyleColor.InputBorder] = value; }
    public UiColor InputBorderHovered { readonly get => _colors[(int)UiStyleColor.InputBorderHovered]; set => _colors[(int)UiStyleColor.InputBorderHovered] = value; }
    public UiColor InputBorderActive { readonly get => _colors[(int)UiStyleColor.InputBorderActive]; set => _colors[(int)UiStyleColor.InputBorderActive] = value; }

    // Slider
    public UiColor SliderText { readonly get => _colors[(int)UiStyleColor.SliderText]; set => _colors[(int)UiStyleColor.SliderText] = value; }
    public UiColor SliderBg { readonly get => _colors[(int)UiStyleColor.SliderBg]; set => _colors[(int)UiStyleColor.SliderBg] = value; }
    public UiColor SliderBgHovered { readonly get => _colors[(int)UiStyleColor.SliderBgHovered]; set => _colors[(int)UiStyleColor.SliderBgHovered] = value; }
    public UiColor SliderBorder { readonly get => _colors[(int)UiStyleColor.SliderBorder]; set => _colors[(int)UiStyleColor.SliderBorder] = value; }

    // Drag
    public UiColor DragText { readonly get => _colors[(int)UiStyleColor.DragText]; set => _colors[(int)UiStyleColor.DragText] = value; }
    public UiColor DragBg { readonly get => _colors[(int)UiStyleColor.DragBg]; set => _colors[(int)UiStyleColor.DragBg] = value; }
    public UiColor DragBgHovered { readonly get => _colors[(int)UiStyleColor.DragBgHovered]; set => _colors[(int)UiStyleColor.DragBgHovered] = value; }
    public UiColor DragBgActive { readonly get => _colors[(int)UiStyleColor.DragBgActive]; set => _colors[(int)UiStyleColor.DragBgActive] = value; }
    public UiColor DragBorder { readonly get => _colors[(int)UiStyleColor.DragBorder]; set => _colors[(int)UiStyleColor.DragBorder] = value; }
    public UiColor DragBorderHovered { readonly get => _colors[(int)UiStyleColor.DragBorderHovered]; set => _colors[(int)UiStyleColor.DragBorderHovered] = value; }
    public UiColor DragBorderActive { readonly get => _colors[(int)UiStyleColor.DragBorderActive]; set => _colors[(int)UiStyleColor.DragBorderActive] = value; }

    // Combo
    public UiColor ComboText { readonly get => _colors[(int)UiStyleColor.ComboText]; set => _colors[(int)UiStyleColor.ComboText] = value; }
    public UiColor ComboBg { readonly get => _colors[(int)UiStyleColor.ComboBg]; set => _colors[(int)UiStyleColor.ComboBg] = value; }
    public UiColor ComboBgHovered { readonly get => _colors[(int)UiStyleColor.ComboBgHovered]; set => _colors[(int)UiStyleColor.ComboBgHovered] = value; }
    public UiColor ComboBgActive { readonly get => _colors[(int)UiStyleColor.ComboBgActive]; set => _colors[(int)UiStyleColor.ComboBgActive] = value; }
    public UiColor ComboPopupBg { readonly get => _colors[(int)UiStyleColor.ComboPopupBg]; set => _colors[(int)UiStyleColor.ComboPopupBg] = value; }
    public UiColor ComboBorder { readonly get => _colors[(int)UiStyleColor.ComboBorder]; set => _colors[(int)UiStyleColor.ComboBorder] = value; }
    public UiColor ComboBorderHovered { readonly get => _colors[(int)UiStyleColor.ComboBorderHovered]; set => _colors[(int)UiStyleColor.ComboBorderHovered] = value; }
    public UiColor ComboBorderActive { readonly get => _colors[(int)UiStyleColor.ComboBorderActive]; set => _colors[(int)UiStyleColor.ComboBorderActive] = value; }

    // Selectable
    public UiColor SelectableText { readonly get => _colors[(int)UiStyleColor.SelectableText]; set => _colors[(int)UiStyleColor.SelectableText] = value; }
    public UiColor SelectableBgHovered { readonly get => _colors[(int)UiStyleColor.SelectableBgHovered]; set => _colors[(int)UiStyleColor.SelectableBgHovered] = value; }
    public UiColor SelectableBgActive { readonly get => _colors[(int)UiStyleColor.SelectableBgActive]; set => _colors[(int)UiStyleColor.SelectableBgActive] = value; }

    // MenuItem
    public UiColor MenuItemText { readonly get => _colors[(int)UiStyleColor.MenuItemText]; set => _colors[(int)UiStyleColor.MenuItemText] = value; }
    public UiColor MenuItemTextDisabled { readonly get => _colors[(int)UiStyleColor.MenuItemTextDisabled]; set => _colors[(int)UiStyleColor.MenuItemTextDisabled] = value; }
    public UiColor MenuItemBgHovered { readonly get => _colors[(int)UiStyleColor.MenuItemBgHovered]; set => _colors[(int)UiStyleColor.MenuItemBgHovered] = value; }
    public UiColor MenuItemBgActive { readonly get => _colors[(int)UiStyleColor.MenuItemBgActive]; set => _colors[(int)UiStyleColor.MenuItemBgActive] = value; }

    // Tab
    public UiColor TabText { readonly get => _colors[(int)UiStyleColor.TabText]; set => _colors[(int)UiStyleColor.TabText] = value; }
    public UiColor TabBorder { readonly get => _colors[(int)UiStyleColor.TabBorder]; set => _colors[(int)UiStyleColor.TabBorder] = value; }

    // TreeNode
    public UiColor TreeNodeText { readonly get => _colors[(int)UiStyleColor.TreeNodeText]; set => _colors[(int)UiStyleColor.TreeNodeText] = value; }
    public UiColor TreeNodeBg { readonly get => _colors[(int)UiStyleColor.TreeNodeBg]; set => _colors[(int)UiStyleColor.TreeNodeBg] = value; }
    public UiColor TreeNodeBgHovered { readonly get => _colors[(int)UiStyleColor.TreeNodeBgHovered]; set => _colors[(int)UiStyleColor.TreeNodeBgHovered] = value; }
    public UiColor TreeNodeBgActive { readonly get => _colors[(int)UiStyleColor.TreeNodeBgActive]; set => _colors[(int)UiStyleColor.TreeNodeBgActive] = value; }

    // Table
    public UiColor TableHeaderText { readonly get => _colors[(int)UiStyleColor.TableHeaderText]; set => _colors[(int)UiStyleColor.TableHeaderText] = value; }
    public UiColor TableCellText { readonly get => _colors[(int)UiStyleColor.TableCellText]; set => _colors[(int)UiStyleColor.TableCellText] = value; }

    // Tooltip
    public UiColor TooltipText { readonly get => _colors[(int)UiStyleColor.TooltipText]; set => _colors[(int)UiStyleColor.TooltipText] = value; }
    public UiColor TooltipBg { readonly get => _colors[(int)UiStyleColor.TooltipBg]; set => _colors[(int)UiStyleColor.TooltipBg] = value; }
    public UiColor TooltipBorder { readonly get => _colors[(int)UiStyleColor.TooltipBorder]; set => _colors[(int)UiStyleColor.TooltipBorder] = value; }

    // Window
    public UiColor WindowTitleText { readonly get => _colors[(int)UiStyleColor.WindowTitleText]; set => _colors[(int)UiStyleColor.WindowTitleText] = value; }

    // ListBox
    public UiColor ListBoxBg { readonly get => _colors[(int)UiStyleColor.ListBoxBg]; set => _colors[(int)UiStyleColor.ListBoxBg] = value; }
    public UiColor ListBoxItemText { readonly get => _colors[(int)UiStyleColor.ListBoxItemText]; set => _colors[(int)UiStyleColor.ListBoxItemText] = value; }
    public UiColor ListBoxItemBgHovered { readonly get => _colors[(int)UiStyleColor.ListBoxItemBgHovered]; set => _colors[(int)UiStyleColor.ListBoxItemBgHovered] = value; }
    public UiColor ListBoxItemBgActive { readonly get => _colors[(int)UiStyleColor.ListBoxItemBgActive]; set => _colors[(int)UiStyleColor.ListBoxItemBgActive] = value; }
    public UiColor ListBoxBorder { readonly get => _colors[(int)UiStyleColor.ListBoxBorder]; set => _colors[(int)UiStyleColor.ListBoxBorder] = value; }

    // ProgressBar
    public UiColor ProgressBarBg { readonly get => _colors[(int)UiStyleColor.ProgressBarBg]; set => _colors[(int)UiStyleColor.ProgressBarBg] = value; }
    public UiColor ProgressBarFill { readonly get => _colors[(int)UiStyleColor.ProgressBarFill]; set => _colors[(int)UiStyleColor.ProgressBarFill] = value; }
    public UiColor ProgressBarBorder { readonly get => _colors[(int)UiStyleColor.ProgressBarBorder]; set => _colors[(int)UiStyleColor.ProgressBarBorder] = value; }

    // Separator
    public UiColor SeparatorLabelText { readonly get => _colors[(int)UiStyleColor.SeparatorLabelText]; set => _colors[(int)UiStyleColor.SeparatorLabelText] = value; }

    /// <summary>
    /// Copies global token values to widget-specific tokens.
    /// Call after setting global tokens to populate defaults.
    /// </summary>
    public void InitWidgetDefaults()
    {
        // Button
        ButtonText = Text;
        ButtonBorder = Border;
        ButtonBorderHovered = FrameBgHovered;
        ButtonBorderActive = FrameBgActive;

        // Checkbox
        CheckboxText = Text;
        CheckboxBg = FrameBg;
        CheckboxBgHovered = FrameBgHovered;
        CheckboxBgActive = FrameBgActive;
        CheckboxBorder = Border;
        CheckboxBorderHovered = FrameBgHovered;
        CheckboxBorderActive = FrameBgActive;

        // RadioButton
        RadioButtonText = Text;
        RadioButtonBg = FrameBg;
        RadioButtonBgHovered = FrameBgHovered;
        RadioButtonBgActive = FrameBgActive;
        RadioButtonBorder = Border;
        RadioButtonBorderHovered = FrameBgHovered;
        RadioButtonBorderActive = FrameBgActive;

        // Input
        InputText = Text;
        InputBg = FrameBg;
        InputBgHovered = FrameBgHovered;
        InputBgActive = FrameBgActive;
        InputSelectionBg = TextSelectedBg;
        InputBorder = Border;
        InputBorderHovered = FrameBgHovered;
        InputBorderActive = CheckMark;

        // Slider
        SliderText = Text;
        SliderBg = FrameBg;
        SliderBgHovered = FrameBgHovered;
        SliderBorder = Border;

        // Drag
        DragText = Text;
        DragBg = FrameBg;
        DragBgHovered = FrameBgHovered;
        DragBgActive = FrameBgActive;
        DragBorder = Border;
        DragBorderHovered = FrameBgHovered;
        DragBorderActive = FrameBgActive;

        // Combo
        ComboText = Text;
        ComboBg = FrameBg;
        ComboBgHovered = FrameBgHovered;
        ComboBgActive = FrameBgActive;
        ComboPopupBg = PopupBg;
        ComboBorder = Border;
        ComboBorderHovered = FrameBgHovered;
        ComboBorderActive = FrameBgActive;

        // Selectable
        SelectableText = Text;
        SelectableBgHovered = HeaderHovered;
        SelectableBgActive = HeaderActive;

        // MenuItem
        MenuItemText = Text;
        MenuItemTextDisabled = TextDisabled;
        MenuItemBgHovered = HeaderHovered;
        MenuItemBgActive = HeaderActive;

        // Tab
        TabText = Text;
        TabBorder = Border;

        // TreeNode
        TreeNodeText = Text;
        TreeNodeBg = Header;
        TreeNodeBgHovered = HeaderHovered;
        TreeNodeBgActive = HeaderActive;

        // Table
        TableHeaderText = Text;
        TableCellText = Text;

        // Tooltip
        TooltipText = Text;
        TooltipBg = PopupBg;
        TooltipBorder = Border;

        // Window
        WindowTitleText = Text;

        // ListBox
        ListBoxBg = FrameBg;
        ListBoxItemText = Text;
        ListBoxItemBgHovered = HeaderHovered;
        ListBoxItemBgActive = HeaderActive;
        ListBoxBorder = Border;

        // ProgressBar
        ProgressBarBg = FrameBg;
        ProgressBarFill = PlotHistogram;
        ProgressBarBorder = Border;

        // Separator
        SeparatorLabelText = Text;
    }

    public static UiTheme ImGuiDark
    {
        get
        {
            var t = new UiTheme
            {
                Text = new UiColor(0xFFE6E8EA),
                TextDisabled = new UiColor(0xFF8A9099),
                WindowBg = new UiColor(0xFF1E1F22),
                TitleBg = new UiColor(0xFF1B1C1F),
                TitleBgActive = new UiColor(0xFF2A2D32),
                MenuBarBg = new UiColor(0xFF202225),
                PopupBg = new UiColor(0xEE1F2124),
                Border = new UiColor(0xFF2C2F36),
                FrameBg = new UiColor(0xFF2B2D31),
                FrameBgHovered = new UiColor(0xFF3A3F47),
                FrameBgActive = new UiColor(0xFF4A515C),
                Header = new UiColor(0xFF2F3136),
                HeaderHovered = new UiColor(0xFF3C424D),
                HeaderActive = new UiColor(0xFF4C5462),
                Button = new UiColor(0xFF2F3136),
                ButtonHovered = new UiColor(0xFF3C424D),
                ButtonActive = new UiColor(0xFF4C5462),
                Tab = new UiColor(0xFF26292E),
                TabHovered = new UiColor(0xFF3A3F47),
                TabActive = new UiColor(0xFF4A515C),
                CheckMark = new UiColor(0xFF58A6FF),
                SliderGrab = new UiColor(0xFF58A6FF),
                SliderGrabActive = new UiColor(0xFF79B8FF),
                PlotLines = new UiColor(0xFFB0B6BE),
                PlotHistogram = new UiColor(0xFFB58A42),
                Separator = new UiColor(0xFF2C2F36),
                TableHeaderBg = new UiColor(0xFF26292E),
                TableRowBg0 = new UiColor(0xFF1E1F22),
                TableRowBg1 = new UiColor(0xFF22252A),
                TableBorder = new UiColor(0xFF2C2F36),
                TextSelectedBg = new UiColor(0x88406AA3),
                ScrollbarBg = new UiColor(0x87020202),
                ScrollbarGrab = new UiColor(0xFF4F4F4F),
                ScrollbarGrabHovered = new UiColor(0xFF696969),
                ScrollbarGrabActive = new UiColor(0xFF828282),
            };
            t.InitWidgetDefaults();
            return t;
        }
    }

    public static UiTheme ImGuiLight
    {
        get
        {
            var t = new UiTheme
            {
                Text = new UiColor(0xFF000000),
                TextDisabled = new UiColor(0xFF808080),
                WindowBg = new UiColor(0xFFF0F0F0),
                TitleBg = new UiColor(0xFFE5E5E5),
                TitleBgActive = new UiColor(0xFFD0D0D0),
                MenuBarBg = new UiColor(0xFFE0E0E0),
                PopupBg = new UiColor(0xFFF8F8F8),
                Border = new UiColor(0xFFB0B0B0),
                FrameBg = new UiColor(0xFFE0E0E0),
                FrameBgHovered = new UiColor(0xFFD0D0D0),
                FrameBgActive = new UiColor(0xFFC0C0C0),
                Header = new UiColor(0xFFD8D8D8),
                HeaderHovered = new UiColor(0xFFC8C8C8),
                HeaderActive = new UiColor(0xFFB8B8B8),
                Button = new UiColor(0xFFD8D8D8),
                ButtonHovered = new UiColor(0xFFC8C8C8),
                ButtonActive = new UiColor(0xFFB8B8B8),
                Tab = new UiColor(0xFFD0D0D0),
                TabHovered = new UiColor(0xFFC0C0C0),
                TabActive = new UiColor(0xFFB0B0B0),
                CheckMark = new UiColor(0xFF1E90FF),
                SliderGrab = new UiColor(0xFF1E90FF),
                SliderGrabActive = new UiColor(0xFF3CA0FF),
                PlotLines = new UiColor(0xFF505050),
                PlotHistogram = new UiColor(0xFFB08040),
                Separator = new UiColor(0xFFB0B0B0),
                TableHeaderBg = new UiColor(0xFFE0E0E0),
                TableRowBg0 = new UiColor(0xFFF4F4F4),
                TableRowBg1 = new UiColor(0xFFEFEFEF),
                TableBorder = new UiColor(0xFFB0B0B0),
                TextSelectedBg = new UiColor(0x804A78B0),
                ScrollbarBg = new UiColor(0x33000000),
                ScrollbarGrab = new UiColor(0xFFA0A0A0),
                ScrollbarGrabHovered = new UiColor(0xFFB0B0B0),
                ScrollbarGrabActive = new UiColor(0xFFC0C0C0),
            };
            t.InitWidgetDefaults();
            return t;
        }
    }

    public static UiTheme ImGuiClassic
    {
        get
        {
            var t = new UiTheme
            {
                Text = new UiColor(0xFF000000),
                TextDisabled = new UiColor(0xFF808080),
                WindowBg = new UiColor(0xFFEFEFEF),
                TitleBg = new UiColor(0xFFD5D5D5),
                TitleBgActive = new UiColor(0xFFBEBEBE),
                MenuBarBg = new UiColor(0xFFDCDCDC),
                PopupBg = new UiColor(0xFFF8F8F8),
                Border = new UiColor(0xFF8C8C8C),
                FrameBg = new UiColor(0xFFDCDCDC),
                FrameBgHovered = new UiColor(0xFFCFCFCF),
                FrameBgActive = new UiColor(0xFFBFBFBF),
                Header = new UiColor(0xFFD0D0D0),
                HeaderHovered = new UiColor(0xFFC0C0C0),
                HeaderActive = new UiColor(0xFFB0B0B0),
                Button = new UiColor(0xFFD0D0D0),
                ButtonHovered = new UiColor(0xFFC0C0C0),
                ButtonActive = new UiColor(0xFFB0B0B0),
                Tab = new UiColor(0xFFD0D0D0),
                TabHovered = new UiColor(0xFFC0C0C0),
                TabActive = new UiColor(0xFFB0B0B0),
                CheckMark = new UiColor(0xFF1E90FF),
                SliderGrab = new UiColor(0xFF6D8ACF),
                SliderGrabActive = new UiColor(0xFF4D74C9),
                PlotLines = new UiColor(0xFF444444),
                PlotHistogram = new UiColor(0xFFBB7A2A),
                Separator = new UiColor(0xFF9A9A9A),
                TableHeaderBg = new UiColor(0xFFD8D8D8),
                TableRowBg0 = new UiColor(0xFFF4F4F4),
                TableRowBg1 = new UiColor(0xFFEFEFEF),
                TableBorder = new UiColor(0xFF9A9A9A),
                TextSelectedBg = new UiColor(0x803A7BD5),
                ScrollbarBg = new UiColor(0x33000000),
                ScrollbarGrab = new UiColor(0xFF9A9A9A),
                ScrollbarGrabHovered = new UiColor(0xFFAEAEAE),
                ScrollbarGrabActive = new UiColor(0xFFC0C0C0),
            };
            t.InitWidgetDefaults();
            return t;
        }
    }

    // ── Extended Preset Themes ──────────────────────────────

    public static UiTheme Nord
    {
        get
        {
            var t = new UiTheme
            {
                Text = new UiColor(0xFFD8DEE9),
                TextDisabled = new UiColor(0xFF6D7A8D),
                WindowBg = new UiColor(0xFF2E3440),
                TitleBg = new UiColor(0xFF2B303B),
                TitleBgActive = new UiColor(0xFF3B4252),
                MenuBarBg = new UiColor(0xFF2E3440),
                PopupBg = new UiColor(0xEE2E3440),
                Border = new UiColor(0xFF3B4252),
                FrameBg = new UiColor(0xFF3B4252),
                FrameBgHovered = new UiColor(0xFF434C5E),
                FrameBgActive = new UiColor(0xFF4C566A),
                Header = new UiColor(0xFF3B4252),
                HeaderHovered = new UiColor(0xFF434C5E),
                HeaderActive = new UiColor(0xFF4C566A),
                Button = new UiColor(0xFF434C5E),
                ButtonHovered = new UiColor(0xFF4C566A),
                ButtonActive = new UiColor(0xFF5E81AC),
                Tab = new UiColor(0xFF3B4252),
                TabHovered = new UiColor(0xFF434C5E),
                TabActive = new UiColor(0xFF4C566A),
                CheckMark = new UiColor(0xFF88C0D0),
                SliderGrab = new UiColor(0xFF81A1C1),
                SliderGrabActive = new UiColor(0xFF88C0D0),
                PlotLines = new UiColor(0xFFA3BE8C),
                PlotHistogram = new UiColor(0xFFEBCB8B),
                Separator = new UiColor(0xFF3B4252),
                TableHeaderBg = new UiColor(0xFF3B4252),
                TableRowBg0 = new UiColor(0xFF2E3440),
                TableRowBg1 = new UiColor(0xFF333945),
                TableBorder = new UiColor(0xFF3B4252),
                TextSelectedBg = new UiColor(0x885E81AC),
                ScrollbarBg = new UiColor(0x872E3440),
                ScrollbarGrab = new UiColor(0xFF4C566A),
                ScrollbarGrabHovered = new UiColor(0xFF5E6D83),
                ScrollbarGrabActive = new UiColor(0xFF81A1C1),
            };
            t.InitWidgetDefaults();
            return t;
        }
    }

    public static UiTheme SolarizedDark
    {
        get
        {
            var t = new UiTheme
            {
                Text = new UiColor(0xFF839496),
                TextDisabled = new UiColor(0xFF586E75),
                WindowBg = new UiColor(0xFF002B36),
                TitleBg = new UiColor(0xFF002229),
                TitleBgActive = new UiColor(0xFF073642),
                MenuBarBg = new UiColor(0xFF002B36),
                PopupBg = new UiColor(0xEE002B36),
                Border = new UiColor(0xFF073642),
                FrameBg = new UiColor(0xFF073642),
                FrameBgHovered = new UiColor(0xFF0A4050),
                FrameBgActive = new UiColor(0xFF0D4E60),
                Header = new UiColor(0xFF073642),
                HeaderHovered = new UiColor(0xFF0A4050),
                HeaderActive = new UiColor(0xFF0D4E60),
                Button = new UiColor(0xFF073642),
                ButtonHovered = new UiColor(0xFF0A4050),
                ButtonActive = new UiColor(0xFF268BD2),
                Tab = new UiColor(0xFF073642),
                TabHovered = new UiColor(0xFF0A4050),
                TabActive = new UiColor(0xFF0D4E60),
                CheckMark = new UiColor(0xFF268BD2),
                SliderGrab = new UiColor(0xFF268BD2),
                SliderGrabActive = new UiColor(0xFF2AA198),
                PlotLines = new UiColor(0xFF859900),
                PlotHistogram = new UiColor(0xFFCB4B16),
                Separator = new UiColor(0xFF073642),
                TableHeaderBg = new UiColor(0xFF073642),
                TableRowBg0 = new UiColor(0xFF002B36),
                TableRowBg1 = new UiColor(0xFF003340),
                TableBorder = new UiColor(0xFF073642),
                TextSelectedBg = new UiColor(0x88268BD2),
                ScrollbarBg = new UiColor(0x87002B36),
                ScrollbarGrab = new UiColor(0xFF586E75),
                ScrollbarGrabHovered = new UiColor(0xFF657B83),
                ScrollbarGrabActive = new UiColor(0xFF839496),
            };
            t.InitWidgetDefaults();
            return t;
        }
    }

    public static UiTheme SolarizedLight
    {
        get
        {
            var t = new UiTheme
            {
                Text = new UiColor(0xFF657B83),
                TextDisabled = new UiColor(0xFF93A1A1),
                WindowBg = new UiColor(0xFFFDF6E3),
                TitleBg = new UiColor(0xFFEEE8D5),
                TitleBgActive = new UiColor(0xFFDDD5C0),
                MenuBarBg = new UiColor(0xFFEEE8D5),
                PopupBg = new UiColor(0xFFFDF6E3),
                Border = new UiColor(0xFFD3CBB8),
                FrameBg = new UiColor(0xFFEEE8D5),
                FrameBgHovered = new UiColor(0xFFE0DACB),
                FrameBgActive = new UiColor(0xFFD3CBB8),
                Header = new UiColor(0xFFEEE8D5),
                HeaderHovered = new UiColor(0xFFE0DACB),
                HeaderActive = new UiColor(0xFFD3CBB8),
                Button = new UiColor(0xFFEEE8D5),
                ButtonHovered = new UiColor(0xFFE0DACB),
                ButtonActive = new UiColor(0xFF268BD2),
                Tab = new UiColor(0xFFEEE8D5),
                TabHovered = new UiColor(0xFFE0DACB),
                TabActive = new UiColor(0xFFD3CBB8),
                CheckMark = new UiColor(0xFF268BD2),
                SliderGrab = new UiColor(0xFF268BD2),
                SliderGrabActive = new UiColor(0xFF2AA198),
                PlotLines = new UiColor(0xFF586E75),
                PlotHistogram = new UiColor(0xFFCB4B16),
                Separator = new UiColor(0xFFD3CBB8),
                TableHeaderBg = new UiColor(0xFFEEE8D5),
                TableRowBg0 = new UiColor(0xFFFDF6E3),
                TableRowBg1 = new UiColor(0xFFF5EFDC),
                TableBorder = new UiColor(0xFFD3CBB8),
                TextSelectedBg = new UiColor(0x80268BD2),
                ScrollbarBg = new UiColor(0x33657B83),
                ScrollbarGrab = new UiColor(0xFF93A1A1),
                ScrollbarGrabHovered = new UiColor(0xFFA8B4B4),
                ScrollbarGrabActive = new UiColor(0xFFBCC6C6),
            };
            t.InitWidgetDefaults();
            return t;
        }
    }

    public static UiTheme Dracula
    {
        get
        {
            var t = new UiTheme
            {
                Text = new UiColor(0xFFF8F8F2),
                TextDisabled = new UiColor(0xFF6272A4),
                WindowBg = new UiColor(0xFF282A36),
                TitleBg = new UiColor(0xFF22232E),
                TitleBgActive = new UiColor(0xFF343746),
                MenuBarBg = new UiColor(0xFF282A36),
                PopupBg = new UiColor(0xEE282A36),
                Border = new UiColor(0xFF44475A),
                FrameBg = new UiColor(0xFF44475A),
                FrameBgHovered = new UiColor(0xFF515570),
                FrameBgActive = new UiColor(0xFF6272A4),
                Header = new UiColor(0xFF44475A),
                HeaderHovered = new UiColor(0xFF515570),
                HeaderActive = new UiColor(0xFF6272A4),
                Button = new UiColor(0xFF44475A),
                ButtonHovered = new UiColor(0xFF6272A4),
                ButtonActive = new UiColor(0xFFBD93F9),
                Tab = new UiColor(0xFF343746),
                TabHovered = new UiColor(0xFF44475A),
                TabActive = new UiColor(0xFF6272A4),
                CheckMark = new UiColor(0xFFBD93F9),
                SliderGrab = new UiColor(0xFFBD93F9),
                SliderGrabActive = new UiColor(0xFFFF79C6),
                PlotLines = new UiColor(0xFF50FA7B),
                PlotHistogram = new UiColor(0xFFFFB86C),
                Separator = new UiColor(0xFF44475A),
                TableHeaderBg = new UiColor(0xFF343746),
                TableRowBg0 = new UiColor(0xFF282A36),
                TableRowBg1 = new UiColor(0xFF2D2F3D),
                TableBorder = new UiColor(0xFF44475A),
                TextSelectedBg = new UiColor(0x88BD93F9),
                ScrollbarBg = new UiColor(0x87282A36),
                ScrollbarGrab = new UiColor(0xFF44475A),
                ScrollbarGrabHovered = new UiColor(0xFF6272A4),
                ScrollbarGrabActive = new UiColor(0xFFBD93F9),
            };
            t.InitWidgetDefaults();
            return t;
        }
    }

    public static UiTheme Monokai
    {
        get
        {
            var t = new UiTheme
            {
                Text = new UiColor(0xFFF8F8F2),
                TextDisabled = new UiColor(0xFF75715E),
                WindowBg = new UiColor(0xFF272822),
                TitleBg = new UiColor(0xFF1E1F1A),
                TitleBgActive = new UiColor(0xFF3E3D32),
                MenuBarBg = new UiColor(0xFF272822),
                PopupBg = new UiColor(0xEE272822),
                Border = new UiColor(0xFF3E3D32),
                FrameBg = new UiColor(0xFF3E3D32),
                FrameBgHovered = new UiColor(0xFF49483E),
                FrameBgActive = new UiColor(0xFF595844),
                Header = new UiColor(0xFF3E3D32),
                HeaderHovered = new UiColor(0xFF49483E),
                HeaderActive = new UiColor(0xFF595844),
                Button = new UiColor(0xFF49483E),
                ButtonHovered = new UiColor(0xFF595844),
                ButtonActive = new UiColor(0xFFA6E22E),
                Tab = new UiColor(0xFF3E3D32),
                TabHovered = new UiColor(0xFF49483E),
                TabActive = new UiColor(0xFF595844),
                CheckMark = new UiColor(0xFFA6E22E),
                SliderGrab = new UiColor(0xFF66D9EF),
                SliderGrabActive = new UiColor(0xFFA6E22E),
                PlotLines = new UiColor(0xFFF92672),
                PlotHistogram = new UiColor(0xFFFD971F),
                Separator = new UiColor(0xFF3E3D32),
                TableHeaderBg = new UiColor(0xFF3E3D32),
                TableRowBg0 = new UiColor(0xFF272822),
                TableRowBg1 = new UiColor(0xFF2D2E28),
                TableBorder = new UiColor(0xFF3E3D32),
                TextSelectedBg = new UiColor(0x88A6E22E),
                ScrollbarBg = new UiColor(0x87272822),
                ScrollbarGrab = new UiColor(0xFF49483E),
                ScrollbarGrabHovered = new UiColor(0xFF75715E),
                ScrollbarGrabActive = new UiColor(0xFFA6E22E),
            };
            t.InitWidgetDefaults();
            return t;
        }
    }

    public static UiTheme CatppuccinMocha
    {
        get
        {
            var t = new UiTheme
            {
                Text = new UiColor(0xFFCDD6F4),
                TextDisabled = new UiColor(0xFF6C7086),
                WindowBg = new UiColor(0xFF1E1E2E),
                TitleBg = new UiColor(0xFF181825),
                TitleBgActive = new UiColor(0xFF313244),
                MenuBarBg = new UiColor(0xFF1E1E2E),
                PopupBg = new UiColor(0xEE1E1E2E),
                Border = new UiColor(0xFF313244),
                FrameBg = new UiColor(0xFF313244),
                FrameBgHovered = new UiColor(0xFF45475A),
                FrameBgActive = new UiColor(0xFF585B70),
                Header = new UiColor(0xFF313244),
                HeaderHovered = new UiColor(0xFF45475A),
                HeaderActive = new UiColor(0xFF585B70),
                Button = new UiColor(0xFF45475A),
                ButtonHovered = new UiColor(0xFF585B70),
                ButtonActive = new UiColor(0xFF89B4FA),
                Tab = new UiColor(0xFF313244),
                TabHovered = new UiColor(0xFF45475A),
                TabActive = new UiColor(0xFF585B70),
                CheckMark = new UiColor(0xFF89B4FA),
                SliderGrab = new UiColor(0xFF89B4FA),
                SliderGrabActive = new UiColor(0xFFCBA6F7),
                PlotLines = new UiColor(0xFFA6E3A1),
                PlotHistogram = new UiColor(0xFFFAB387),
                Separator = new UiColor(0xFF313244),
                TableHeaderBg = new UiColor(0xFF313244),
                TableRowBg0 = new UiColor(0xFF1E1E2E),
                TableRowBg1 = new UiColor(0xFF232336),
                TableBorder = new UiColor(0xFF313244),
                TextSelectedBg = new UiColor(0x8889B4FA),
                ScrollbarBg = new UiColor(0x871E1E2E),
                ScrollbarGrab = new UiColor(0xFF45475A),
                ScrollbarGrabHovered = new UiColor(0xFF585B70),
                ScrollbarGrabActive = new UiColor(0xFF89B4FA),
            };
            t.InitWidgetDefaults();
            return t;
        }
    }

    public static UiTheme GitHubDark
    {
        get
        {
            var t = new UiTheme
            {
                Text = new UiColor(0xFFC9D1D9),
                TextDisabled = new UiColor(0xFF484F58),
                WindowBg = new UiColor(0xFF0D1117),
                TitleBg = new UiColor(0xFF010409),
                TitleBgActive = new UiColor(0xFF161B22),
                MenuBarBg = new UiColor(0xFF0D1117),
                PopupBg = new UiColor(0xEE161B22),
                Border = new UiColor(0xFF30363D),
                FrameBg = new UiColor(0xFF161B22),
                FrameBgHovered = new UiColor(0xFF21262D),
                FrameBgActive = new UiColor(0xFF30363D),
                Header = new UiColor(0xFF161B22),
                HeaderHovered = new UiColor(0xFF21262D),
                HeaderActive = new UiColor(0xFF30363D),
                Button = new UiColor(0xFF21262D),
                ButtonHovered = new UiColor(0xFF30363D),
                ButtonActive = new UiColor(0xFF58A6FF),
                Tab = new UiColor(0xFF161B22),
                TabHovered = new UiColor(0xFF21262D),
                TabActive = new UiColor(0xFF30363D),
                CheckMark = new UiColor(0xFF58A6FF),
                SliderGrab = new UiColor(0xFF58A6FF),
                SliderGrabActive = new UiColor(0xFF79C0FF),
                PlotLines = new UiColor(0xFF3FB950),
                PlotHistogram = new UiColor(0xFFD29922),
                Separator = new UiColor(0xFF21262D),
                TableHeaderBg = new UiColor(0xFF161B22),
                TableRowBg0 = new UiColor(0xFF0D1117),
                TableRowBg1 = new UiColor(0xFF111820),
                TableBorder = new UiColor(0xFF30363D),
                TextSelectedBg = new UiColor(0x8858A6FF),
                ScrollbarBg = new UiColor(0x870D1117),
                ScrollbarGrab = new UiColor(0xFF30363D),
                ScrollbarGrabHovered = new UiColor(0xFF484F58),
                ScrollbarGrabActive = new UiColor(0xFF58A6FF),
            };
            t.InitWidgetDefaults();
            return t;
        }
    }
}

public sealed record class UiStyle(
    UiVector2 WindowPadding,
    UiVector2 ItemSpacing,
    UiVector2 FramePadding,
    UiVector2 ButtonPadding,
    float RowSpacing,
    float CheckboxSpacing,
    float InputWidth,
    float SliderWidth,
    float TreeIndent,
    float ScrollbarSize
)
{
    public static UiStyle Default => new(
        new UiVector2(10f, 10f),
        new UiVector2(10f, 6f),
        new UiVector2(6f, 4f),
        new UiVector2(6f, 4f),
        6f,
        6f,
        240f,
        240f,
        18f,
        14f
    );

    public UiStyle ScaleAllSizes(float scale)
    {
        var s = MathF.Max(0.1f, scale);
        return this with
        {
            WindowPadding = Scale(WindowPadding, s),
            ItemSpacing = Scale(ItemSpacing, s),
            FramePadding = Scale(FramePadding, s),
            ButtonPadding = Scale(ButtonPadding, s),
            RowSpacing = RowSpacing * s,
            CheckboxSpacing = CheckboxSpacing * s,
            InputWidth = InputWidth * s,
            SliderWidth = SliderWidth * s,
            TreeIndent = TreeIndent * s,
            ScrollbarSize = ScrollbarSize * s,
        };
    }

    private static UiVector2 Scale(UiVector2 value, float scale) => new(value.X * scale, value.Y * scale);
}

public enum UiStyleColor
{
    Text,
    TextDisabled,
    WindowBg,
    TitleBg,
    TitleBgActive,
    MenuBarBg,
    PopupBg,
    Border,
    FrameBg,
    FrameBgHovered,
    FrameBgActive,
    Header,
    HeaderHovered,
    HeaderActive,
    Button,
    ButtonHovered,
    ButtonActive,
    Tab,
    TabHovered,
    TabActive,
    CheckMark,
    SliderGrab,
    SliderGrabActive,
    PlotLines,
    PlotHistogram,
    Separator,
    TableHeaderBg,
    TableRowBg0,
    TableRowBg1,
    TableBorder,
    TextSelectedBg,
    ScrollbarBg,
    ScrollbarGrab,
    ScrollbarGrabHovered,
    ScrollbarGrabActive,

    // === Widget-Specific Tokens (Layer 2) ===

    // Button
    ButtonText,
    ButtonBorder,
    ButtonBorderHovered,
    ButtonBorderActive,

    // Checkbox
    CheckboxText,
    CheckboxBg,
    CheckboxBgHovered,
    CheckboxBgActive,
    CheckboxBorder,
    CheckboxBorderHovered,
    CheckboxBorderActive,

    // RadioButton
    RadioButtonText,
    RadioButtonBg,
    RadioButtonBgHovered,
    RadioButtonBgActive,
    RadioButtonBorder,
    RadioButtonBorderHovered,
    RadioButtonBorderActive,

    // Input
    InputText,
    InputBg,
    InputBgHovered,
    InputBgActive,
    InputSelectionBg,
    InputBorder,
    InputBorderHovered,
    InputBorderActive,

    // Slider
    SliderText,
    SliderBg,
    SliderBgHovered,
    SliderBorder,

    // Drag
    DragText,
    DragBg,
    DragBgHovered,
    DragBgActive,
    DragBorder,
    DragBorderHovered,
    DragBorderActive,

    // Combo
    ComboText,
    ComboBg,
    ComboBgHovered,
    ComboBgActive,
    ComboPopupBg,
    ComboBorder,
    ComboBorderHovered,
    ComboBorderActive,

    // Selectable
    SelectableText,
    SelectableBgHovered,
    SelectableBgActive,

    // MenuItem
    MenuItemText,
    MenuItemTextDisabled,
    MenuItemBgHovered,
    MenuItemBgActive,

    // Tab
    TabText,
    TabBorder,

    // TreeNode
    TreeNodeText,
    TreeNodeBg,
    TreeNodeBgHovered,
    TreeNodeBgActive,

    // Table
    TableHeaderText,
    TableCellText,

    // Tooltip
    TooltipText,
    TooltipBg,
    TooltipBorder,

    // Window
    WindowTitleText,

    // ListBox
    ListBoxBg,
    ListBoxItemText,
    ListBoxItemBgHovered,
    ListBoxItemBgActive,
    ListBoxBorder,

    // ProgressBar
    ProgressBarBg,
    ProgressBarFill,
    ProgressBarBorder,

    // Separator
    SeparatorLabelText,
}

public enum UiStyleVar
{
    WindowPadding,
    ItemSpacing,
    FramePadding,
    IndentSpacing,
}

public enum UiAnimationEasing
{
    Linear,
    InOutSine,
    OutCubic,
}

[StructLayout(LayoutKind.Sequential)]
public readonly record struct UiDrawVertex(UiVector2 Position, UiVector2 UV, UiColor Color);

public readonly record struct UiTextureId(nuint Value);

[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1 << 0,
    Shift = 1 << 1,
    Alt = 1 << 2,
    Super = 1 << 3,
}

public enum UiKey
{
    None = 0,
    Tab,
    LeftArrow,
    RightArrow,
    UpArrow,
    DownArrow,
    PageUp,
    PageDown,
    Home,
    End,
    Insert,
    Delete,
    Backspace,
    Space,
    Enter,
    Escape,
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,
}

public readonly record struct UiKeyEvent(UiKey Key, bool IsDown, KeyModifiers Modifiers);

public readonly record struct UiCharEvent(uint CodePoint);

public readonly record struct UiKeyRepeatSettings(
    double InitialDelaySeconds,
    double RepeatIntervalSeconds
);

public enum UiTextureFormat
{
    Rgba8Unorm,
    Rgba8Srgb,
}

public enum UiTextureUpdateKind
{
    Create,
    Update,
    Destroy,
}

public enum UiDir
{
    Left,
    Right,
    Up,
    Down,
}

public enum UiItemVerticalAlign
{
    Top,
    Center,
    Bottom,
}

public enum UiItemHorizontalAlign
{
    Left,
    Center,
    Right,
}

public enum UiMouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2,
}

public enum UiMouseCursor
{
    Arrow,
    TextInput,
    ResizeAll,
    ResizeNS,
    ResizeEW,
    ResizeNESW,
    ResizeNWSE,
    Hand,
    NotAllowed,
}

[Flags]
public enum UiItemFlags
{
    None = 0,
    AllowOverlap = 1 << 0,
    Disabled = 1 << 1,
}

[Flags]
public enum UiSelectableFlags
{
    None = 0,
}

[Flags]
public enum UiMultiSelectFlags
{
    None = 0,
}

[Flags]
public enum UiDragDropFlags
{
    None = 0,
    SourceNoPreviewTooltip = 1 << 0,
    SourceNoDisableHover = 1 << 1,
    SourceAllowNullID = 1 << 2,
    SourceExtern = 1 << 3,
    AcceptBeforeDelivery = 1 << 10,
    AcceptNoDrawDefaultRect = 1 << 11,
    AcceptNoPreviewTooltip = 1 << 12,
}

[Flags]
public enum UiTreeNodeFlags
{
    None = 0,
    DefaultOpen = 1 << 0,
}

[Flags]
public enum UiTabBarFlags
{
    None = 0,
}

[Flags]
public enum UiTabItemFlags
{
    None = 0,
}

[Flags]
public enum UiInputTextFlags
{
    None = 0,
}

[Flags]
public enum UiTableFlags
{
    None = 0,
    Borders = 1 << 0,
    RowBg = 1 << 1,
    Sortable = 1 << 2,
}

[Flags]
public enum UiTableColumnFlags
{
    None = 0,
}

[Flags]
public enum UiTableRowFlags
{
    None = 0,
}

public enum UiTableBgTarget
{
    RowBg0,
    RowBg1,
    CellBg,
}

public readonly record struct UiTextureUpdate(
    UiTextureUpdateKind Kind,
    UiTextureId TextureId,
    UiTextureFormat Format,
    int Width,
    int Height,
    ReadOnlyMemory<byte> RgbaPixels
);

public readonly record struct UiDrawCommand(
    UiRect ClipRect,
    UiTextureId TextureId,
    uint IndexOffset,
    uint ElementCount,
    uint VertexOffset,
    UiDrawCallback? Callback = null,
    object? UserData = null,
    UiVector2 Translation = default
);

public delegate void UiDrawCallback(UiDrawList drawList, UiDrawCommand command);

/// <summary>
/// ArrayPool-backed buffer with zero-copy transfer to <see cref="UiPooledList{T}"/>.
/// Replaces List&lt;T&gt; for vertex/index/command buffers in draw list building.
/// </summary>
internal sealed class PooledBuffer<T>
{
    private T[] _array;
    private int _count;

    public PooledBuffer(int initialCapacity = 1024)
    {
        _array = ArrayPool<T>.Shared.Rent(Math.Max(initialCapacity, 16));
        _count = 0;
    }

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    public int Capacity => _array.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        if (capacity > _array.Length)
        {
            Grow(capacity);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int capacity)
    {
        var nextCapacity = _array.Length * 2;
        if (nextCapacity < capacity)
        {
            nextCapacity = capacity;
        }

        var newArray = ArrayPool<T>.Shared.Rent(nextCapacity);
        _array.AsSpan(0, _count).CopyTo(newArray);
        ArrayPool<T>.Shared.Return(_array, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        _array = newArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        var array = _array;
        var count = _count;
        if ((uint)count < (uint)array.Length)
        {
            array[count] = item;
            _count = count + 1;
        }
        else
        {
            AddWithResize(item);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddWithResize(T item)
    {
        Grow(_count + 1);
        _array[_count] = item;
        _count++;
    }

    public void AddRange(IReadOnlyList<T> source)
    {
        var sourceCount = source.Count;
        EnsureCapacity(_count + sourceCount);
        if (source is UiPooledList<T> pooled)
        {
            pooled.AsSpan().CopyTo(_array.AsSpan(_count));
        }
        else
        {
            for (var i = 0; i < sourceCount; i++)
            {
                _array[_count + i] = source[i];
            }
        }
        _count += sourceCount;
    }

    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _array[index];
    }

    public ref T this[Index index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _array[index.GetOffset(_count)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan() => _array.AsSpan(0, _count);

    public void SetCount(int count)
    {
        if (count > _array.Length)
        {
            Grow(count);
        }
        _count = count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _count = 0;

    public void RemoveAt(int index)
    {
        _count--;
        if (index < _count)
        {
            Array.Copy(_array, index + 1, _array, index, _count - index);
        }
    }

    public void RemoveRange(int start, int count)
    {
        if (start + count < _count)
        {
            Array.Copy(_array, start + count, _array, start, _count - start - count);
        }
        _count -= count;
    }

    public void TrimExcess()
    {
        // For pooled buffers, return old and rent a minimal one
        if (_count == 0 && _array.Length > 64)
        {
            ArrayPool<T>.Shared.Return(_array, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            _array = ArrayPool<T>.Shared.Rent(16);
        }
    }

    /// <summary>
    /// Zero-copy transfer: hands off the backing array to a <see cref="UiPooledList{T}"/> and rents a fresh buffer.
    /// </summary>
    public UiPooledList<T> TransferToPooledList()
    {
        if (_count == 0)
        {
            return new UiPooledList<T>(Array.Empty<T>(), 0, pooled: false);
        }
        var result = new UiPooledList<T>(_array, _count, pooled: true);
        _array = ArrayPool<T>.Shared.Rent(1024);
        _count = 0;
        return result;
    }
}

public sealed class UiPooledList<T> : IReadOnlyList<T>
{
    private T[]? _buffer;
    private int _count;
    private readonly bool _pooled;

    public UiPooledList(T[] buffer, int count, bool pooled)
    {
        _buffer = buffer;
        _count = count;
        _pooled = pooled;
    }

    public int Count => _count;

    public T this[int index]
    {
        get
        {
            if (_buffer is null)
            {
                throw new ObjectDisposedException(nameof(UiPooledList<T>));
            }

            return _buffer[index];
        }
    }

    public static UiPooledList<T> RentAndCopy(List<T> source)
    {
        if (source.Count == 0)
        {
            return new UiPooledList<T>(Array.Empty<T>(), 0, pooled: false);
        }

        var buffer = ArrayPool<T>.Shared.Rent(source.Count);
        source.CopyTo(buffer);
        return new UiPooledList<T>(buffer, source.Count, pooled: true);
    }

    public static UiPooledList<T> RentAndCopy(ReadOnlySpan<T> source)
    {
        if (source.Length == 0)
        {
            return new UiPooledList<T>(Array.Empty<T>(), 0, pooled: false);
        }

        var buffer = ArrayPool<T>.Shared.Rent(source.Length);
        source.CopyTo(buffer);
        return new UiPooledList<T>(buffer, source.Length, pooled: true);
    }

    public static UiPooledList<T> RentAndCopy(IReadOnlyList<T> source)
    {
        var count = source.Count;
        if (count == 0)
        {
            return new UiPooledList<T>(Array.Empty<T>(), 0, pooled: false);
        }

        var buffer = ArrayPool<T>.Shared.Rent(count);
        for (var i = 0; i < count; i++)
        {
            buffer[i] = source[i];
        }

        return new UiPooledList<T>(buffer, count, pooled: true);
    }

    public static UiPooledList<T> FromArray(T[] array)
    {
        return new UiPooledList<T>(array, array.Length, pooled: false);
    }

    public static UiPooledList<T> FromArray(T[] array, int count)
    {
        return new UiPooledList<T>(array, count, pooled: false);
    }

    public void Return()
    {
        if (!_pooled || _buffer is null)
        {
            _buffer = null;
            _count = 0;
            return;
        }

        ArrayPool<T>.Shared.Return(_buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        _buffer = null;
        _count = 0;
    }

    public ReadOnlySpan<T> AsSpan()
    {
        if (_buffer is null)
        {
            throw new ObjectDisposedException(nameof(UiPooledList<T>));
        }

        return new ReadOnlySpan<T>(_buffer, 0, _count);
    }

    public void CopyTo(Span<T> destination)
    {
        AsSpan().CopyTo(destination);
    }

    public ref readonly T ItemRef(int index)
    {
        if (_buffer is null)
        {
            throw new ObjectDisposedException(nameof(UiPooledList<T>));
        }

        return ref _buffer[index];
    }

    public IEnumerator<T> GetEnumerator()
    {
        if (_buffer is null)
        {
            yield break;
        }

        for (var i = 0; i < _count; i++)
        {
            yield return _buffer[i];
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed record class UiDrawList(
    UiPooledList<UiDrawVertex> Vertices,
    UiPooledList<uint> Indices,
    UiPooledList<UiDrawCommand> Commands
)
{
    public void ReleasePooled()
    {
        Vertices.Return();
        Indices.Return();
        Commands.Return();
    }

    public UiDrawList DeIndexAllBuffers()
    {
        if (Indices.Count == 0 || Vertices.Count == 0)
        {
            return this;
        }

        var newVertices = new List<UiDrawVertex>(Indices.Count);
        for (var i = 0; i < Indices.Count; i++)
        {
            var index = Indices[i];
            newVertices.Add(Vertices[(int)index]);
        }

        var newIndices = new List<uint>(Indices.Count);
        for (var i = 0; i < newVertices.Count; i++)
        {
            newIndices.Add((uint)i);
        }

        var newCommands = new List<UiDrawCommand>(Commands.Count);
        var offset = 0u;
        for (var i = 0; i < Commands.Count; i++)
        {
            ref readonly var cmd = ref Commands.ItemRef(i);
            newCommands.Add(cmd with { IndexOffset = offset });
            offset += cmd.ElementCount;
        }

        return new UiDrawList(
            UiPooledList<UiDrawVertex>.RentAndCopy(newVertices),
            UiPooledList<uint>.RentAndCopy(newIndices),
            UiPooledList<UiDrawCommand>.RentAndCopy(newCommands)
        );
    }

    public UiDrawList ScaleClipRects(UiVector2 scale)
    {
        if (Commands.Count == 0)
        {
            return this;
        }

        var scaled = new List<UiDrawCommand>(Commands.Count);
        for (var i = 0; i < Commands.Count; i++)
        {
            ref readonly var cmd = ref Commands.ItemRef(i);
            var rect = cmd.ClipRect;
            var scaledRect = new UiRect(rect.X * scale.X, rect.Y * scale.Y, rect.Width * scale.X, rect.Height * scale.Y);
            scaled.Add(cmd with { ClipRect = scaledRect });
        }

        return new UiDrawList(
            UiPooledList<UiDrawVertex>.RentAndCopy(Vertices.AsSpan()),
            UiPooledList<uint>.RentAndCopy(Indices.AsSpan()),
            UiPooledList<UiDrawCommand>.RentAndCopy(scaled)
        );
    }
}

public sealed record class UiDrawData(
    UiVector2 DisplaySize,
    UiVector2 DisplayPos,
    UiVector2 FramebufferScale,
    int TotalVertexCount,
    int TotalIndexCount,
    UiPooledList<UiDrawList> DrawLists,
    UiPooledList<UiTextureUpdate> TextureUpdates,
    UiRect DynamicDirtyRect = default,
    bool HasDynamicDirtyRect = false
)
{
    public void ReleasePooled()
    {
        for (var i = 0; i < DrawLists.Count; i++)
        {
            DrawLists[i].ReleasePooled();
        }

        DrawLists.Return();
        TextureUpdates.Return();
    }

    public UiDrawData ScaleClipRects(UiVector2 scale)
    {
        if (DrawLists.Count == 0)
        {
            return this;
        }

        var scaled = new List<UiDrawList>(DrawLists.Count);
        for (var i = 0; i < DrawLists.Count; i++)
        {
            scaled.Add(DrawLists[i].ScaleClipRects(scale));
        }

        var scaledDirtyRect = DynamicDirtyRect;
        if (HasDynamicDirtyRect)
        {
            scaledDirtyRect = new UiRect(
                DynamicDirtyRect.X * scale.X,
                DynamicDirtyRect.Y * scale.Y,
                DynamicDirtyRect.Width * scale.X,
                DynamicDirtyRect.Height * scale.Y);
        }

        return this with
        {
            DrawLists = UiPooledList<UiDrawList>.RentAndCopy(scaled),
            DynamicDirtyRect = scaledDirtyRect,
        };
    }
}

public readonly record struct UiViewport(
    UiVector2 Pos,
    UiVector2 Size,
    UiVector2 WorkPos,
    UiVector2 WorkSize
);

public sealed class UiDrawListSharedData
{
    public UiFontAtlas FontAtlas { get; set; }
    public UiTextSettings TextSettings { get; set; }
    public float LineHeight { get; set; }

    public UiDrawListSharedData(UiFontAtlas fontAtlas, UiTextSettings textSettings, float lineHeight)
    {
        FontAtlas = fontAtlas;
        TextSettings = textSettings;
        LineHeight = lineHeight;
    }
}

public sealed class UiDragDropPayload
{
    public string DataType { get; internal set; } = string.Empty;
    public ReadOnlyMemory<byte> Data { get; internal set; }
    public int DataSize => Data.Length;
    public bool Preview { get; internal set; }
    public bool Delivery { get; internal set; }
    public string? SourceId { get; internal set; }
    internal int DataFrameCount { get; set; } = -1;

    public bool IsDataType(string type) => string.Equals(DataType, type, StringComparison.Ordinal);
}

public sealed class UiStateStorage
{
    private sealed class Box<T>	where T : struct
    {
        public T Value;
        public Box(T value) => Value = value;
    }

    private readonly Dictionary<string, Box<int>> _ints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Box<float>> _floats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Box<bool>> _bools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Box<nint>> _voidPtrs = new(StringComparer.Ordinal);

    public void Clear()
    {
        _ints.Clear();
        _floats.Clear();
        _bools.Clear();
        _voidPtrs.Clear();
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _ints.TryGetValue(key, out var box) ? box.Value : defaultValue;
    }

    public void SetInt(string key, int value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_ints.TryGetValue(key, out var box))
        {
            box.Value = value;
            return;
        }

        _ints[key] = new Box<int>(value);
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _floats.TryGetValue(key, out var box) ? box.Value : defaultValue;
    }

    public void SetFloat(string key, float value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_floats.TryGetValue(key, out var box))
        {
            box.Value = value;
            return;
        }

        _floats[key] = new Box<float>(value);
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _bools.TryGetValue(key, out var box) ? box.Value : defaultValue;
    }

    public void SetBool(string key, bool value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_bools.TryGetValue(key, out var box))
        {
            box.Value = value;
            return;
        }

        _bools[key] = new Box<bool>(value);
    }

    public nint GetVoidPtr(string key, nint defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _voidPtrs.TryGetValue(key, out var box) ? box.Value : defaultValue;
    }

    public void SetVoidPtr(string key, nint value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_voidPtrs.TryGetValue(key, out var box))
        {
            box.Value = value;
            return;
        }

        _voidPtrs[key] = new Box<nint>(value);
    }

    public ref int GetIntRef(string key, int defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_ints.TryGetValue(key, out var box))
        {
            box = new Box<int>(defaultValue);
            _ints[key] = box;
        }

        return ref box.Value;
    }

    public ref bool GetBoolRef(string key, bool defaultValue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_bools.TryGetValue(key, out var box))
        {
            box = new Box<bool>(defaultValue);
            _bools[key] = box;
        }

        return ref box.Value;
    }

    public ref float GetFloatRef(string key, float defaultValue = 0f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_floats.TryGetValue(key, out var box))
        {
            box = new Box<float>(defaultValue);
            _floats[key] = box;
        }

        return ref box.Value;
    }

    public ref nint GetVoidPtrRef(string key, nint defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_voidPtrs.TryGetValue(key, out var box))
        {
            box = new Box<nint>(defaultValue);
            _voidPtrs[key] = box;
        }

        return ref box.Value;
    }

    public void BuildSortByKey()
    {
        RebuildSorted(_ints);
        RebuildSorted(_floats);
        RebuildSorted(_bools);
        RebuildSorted(_voidPtrs);
    }

    public void SetAllInt(int value)
    {
        foreach (var entry in _ints.Values)
        {
            entry.Value = value;
        }
    }

    private static void RebuildSorted<T>(Dictionary<string, Box<T>> dict) where T : struct
    {
        if (dict.Count <= 1)
        {
            return;
        }

        var ordered = new List<KeyValuePair<string, Box<T>>>(dict.Count);
        foreach (var entry in dict)
        {
            ordered.Add(entry);
        }

        ordered.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
        dict.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            dict[entry.Key] = entry.Value;
        }
    }
}

public delegate byte[] UiMemAlloc(int size);

public delegate void UiMemFree(byte[] block);

public interface IUiContext : IDisposable
{
    void NewFrame(UiFrameInfo frameInfo);
    void Render();
    UiDrawData GetDrawData();
}

public interface IUiImeHandler
{
    void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth);
    string? GetCompositionText();
    void SetCompositionOwner(string? inputId);
    string? ConsumeCommittedText(string inputId);
    string? ConsumeRecentCommittedText();
}

public readonly record struct UiFrameInfo(
    float DeltaTime,
    UiVector2 DisplaySize,
    UiVector2 DisplayFramebufferScale
);

public interface IRendererBackend : IDisposable
{
    void CreateDeviceObjects();
    void InvalidateDeviceObjects();
    void RenderDrawData(UiDrawData drawData);
    void SetMinImageCount(int count);
    void SetVSync(bool enable);
    void SetMsaaSamples(int samples);
}
