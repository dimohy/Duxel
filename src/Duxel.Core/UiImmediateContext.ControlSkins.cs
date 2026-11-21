namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private UiControlSkins _controlSkins;
    private readonly Stack<UiButtonSkin> _buttonSkinScopes = [];
    private UiTextFieldSkin? _textFieldSkinOverride;
    internal void SetControlSkins(UiControlSkins skins)
    {
        _controlSkins = skins;
        ScrollbarSize = skins.Scrollbar.Width ?? ScrollbarSize;
        _buttonSkinScopes.Clear();
    }
    private UiButtonSkin CurrentButtonSkin => _buttonSkinScopes.Count == 0
        ? _controlSkins.Button : _buttonSkinScopes.Peek();
    internal UiSegmentedSkin SegmentedSkin => _controlSkins.Segmented;
    private UiTextFieldSkin CurrentTextFieldSkin => _textFieldSkinOverride ?? _controlSkins.TextField;

    internal UiTextFieldSkin? SetTextFieldSkinOverride(UiTextFieldSkin? skin)
    {
        var previous = _textFieldSkinOverride;
        _textFieldSkinOverride = skin;
        return previous;
    }

    internal bool SegmentedBehavior(string id, UiVector2 size)
    {
        var cursor = AdvanceCursor(size);
        return ButtonBehavior(id, new(cursor.X, cursor.Y, size.X, size.Y), out _, out _, _controlSkins.Segmented.Activation);
    }

    private UiRect DrawTextFieldFrame(UiRect rect, UiColor border, bool focused, bool multiline = false)
    {
        var skin = CurrentTextFieldSkin;
        if (multiline && skin == default)
        {
            AddRectFilled(rect, border, _whiteTexture);
            var inner = new UiRect(rect.X + 1, rect.Y + 1, MathF.Max(0, rect.Width - 2), MathF.Max(0, rect.Height - 2));
            AddRectFilled(inner, _theme.InputBg, _whiteTexture);
            return inner;
        }
        var radius = skin.CornerRadius ?? (multiline ? 0 : _designTokens.InputCornerRadius);
        var width = focused ? skin.FocusedBorderWidth ?? skin.BorderWidth : skin.BorderWidth;
        var thickness = width ?? _designTokens.ControlBorderWidth;
        if (skin.Appearance == UiTextFieldAppearance.Frame)
            return AddDesignedFrame(rect, _theme.InputBg, border, radius, thickness);
        // Extend below the clip so one analytic primitive has rounded top and square bottom corners.
        var clip = IntersectRect(CurrentClipRect, rect);
        _builder.AddRectFilledRounded(new(rect.X, rect.Y, rect.Width, rect.Height + radius),
            _theme.InputBg, _whiteTexture, radius, clip);
        _builder.AddRectFilled(new(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), border, _whiteTexture, clip);
        return new(rect.X, rect.Y, rect.Width, MathF.Max(0, rect.Height - thickness));
    }

    public bool Toggle(string label, ref bool value)
    {
        if (_controlSkins.Toggle.Appearance == UiToggleAppearance.Checkbox) return Checkbox(label, ref value);
        ArgumentNullException.ThrowIfNull(label);
        return Toggle<bool, UiBooleanToggleValue, UiConfiguredToggleBehavior, UiLegacySwitchPresentation>(label, ref value);
    }

    private void AddButtonFrame(UiRect rect, bool hovered, bool held, in UiButtonSkin skin)
    {
        var color = held ? _theme.ButtonActive : hovered ? _theme.ButtonHovered : _theme.Button;
        var border = held ? _theme.ButtonBorderActive : hovered ? _theme.ButtonBorderHovered : _theme.ButtonBorder;
        var radius = held ? skin.PressedCornerRadius ?? skin.CornerRadius : skin.CornerRadius;
        AddDesignedFrame(rect, color, border, radius ?? _designTokens.ControlCornerRadius,
            skin.BorderWidth ?? _designTokens.ControlBorderWidth);
    }

    public void PushButtonSkin(UiButtonSkin skin) => _buttonSkinScopes.Push(skin);
    public void PopButtonSkin()
    {
        if (_buttonSkinScopes.Count == 0) throw new InvalidOperationException("No button skin scope is active.");
        _buttonSkinScopes.Pop();
    }
}

public static class UiControlSkinExtensions
{
    /// <summary>Applies a text field skin while rendering the view and its legacy input fields.</summary>
    public static IUiView TextFieldStyle(this IUiView view, UiTextFieldSkin skin)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new UiView(ui =>
        {
            var previous = ui.SetTextFieldSkinOverride(skin);
            try { view.Render(ui); }
            finally { ui.SetTextFieldSkinOverride(previous); }
        });
    }

    public static IUiView ControlStyle(this IUiView view, UiButtonSkin skin)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new UiView(ui =>
        {
            ui.PushButtonSkin(skin);
            try { view.Render(ui); }
            finally { ui.PopButtonSkin(); }
        });
    }
}
