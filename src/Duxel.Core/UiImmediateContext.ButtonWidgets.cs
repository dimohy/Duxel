namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal UiTheme ButtonTheme => _theme;
    internal UiFluentColorScheme ControlFluentColorScheme=>_theme.FluentColorScheme;
    internal UiButtonSkin ButtonLegacySkin => CurrentButtonSkin;
    internal UiVector2 ButtonDefaultPadding => new(ButtonPaddingX, ButtonPaddingY);

    internal UiButtonInteraction ProcessButton(string id, UiRect bounds, UiButtonActivation activation)
        =>ProcessButton<ActionControlKeys>(id,bounds,activation);
    internal UiButtonInteraction ProcessButton<TKeys>(string id,UiRect bounds,UiButtonActivation activation)
        where TKeys:IControlKeys
        => ProcessControlKeyboard<TKeys>(bounds, ProcessControlPointer(id, bounds, activation));
    private UiButtonInteraction ProcessControlPointer(string id, UiRect bounds, UiButtonActivation activation)
    {
        EnsureNotMeasuring();
        var previous=_state.ActiveId;
        var activated = ButtonBehavior(id, bounds, out var hovered, out var held, activation);
        if (IsControlEnabled && _state.ActiveId is { } active && active == _lastItemId)
        {
            _ownedPointerControl = active;
            _ownedPointerControlSeen = true;
        }
        if(previous is not null&&previous!=_state.ActiveId&&_state.HasTextInputStorage(previous))_imeHandler?.SetCompositionOwner(null);
        return new(activated,hovered,held);
    }
    internal void PaintButtonFrame(UiRect bounds, UiColor fill, UiColor border, float radius, float width) =>
        AddDesignedFrame(bounds, fill, border, radius, width);
    internal void PaintButtonCircle(UiVector2 center, float radius, UiColor fill) =>
        _builder.AddCircleFilled(center, radius, fill, _whiteTexture, CurrentClipRect);
    internal void PaintButtonText(string text, UiVector2 position, UiColor color)
    {
        if (text.Length != 0) AddTextInternal(_builder, text, position, color, CurrentClipRect, _textSettings, _lineHeight);
    }

    public bool Button<TBehavior, TPresentation>(string label, UiVector2 size = default)
        where TBehavior : IUiButtonBehavior
        where TPresentation : IUiButtonPresentation
        => Button<TBehavior,TPresentation>(label,label,size);

    public bool Button<TBehavior,TPresentation>(string label,UiVector2 size,bool constrainLayout)
        where TBehavior:IUiButtonBehavior where TPresentation:IUiButtonPresentation
        =>Button<TBehavior,TPresentation>(label,label,size,constrainLayout);

    internal bool Button<TBehavior,TPresentation>(string id,string label,UiVector2 size,bool constrainLayout=false)
        where TBehavior:IUiButtonBehavior
        where TPresentation:IUiButtonPresentation
    {
        label ??= "Button";
        id ??= label;
        var text = GetDisplayLabel(label);
        var context = new UiControlContext(this,text.Length>0);
        using var typography=TPresentation.BeginText(context,text.Length>0);
        var (textSize,measured)=MeasureButtonLocal<TPresentation>(context,text,size,typography.LineHeight);
        if(constrainLayout)measured=ConstrainToLayout(measured);
        var position = AdvanceCursor(measured);
        var bounds = new UiRect(position.X, position.Y, measured.X, measured.Y);
        var interaction = ProcessPlacedButton<TBehavior>(context, id, bounds, out var resolvedId);
        TPresentation.Paint(context, text, textSize, bounds, interaction);
        PublishPlacedButton<TBehavior>(resolvedId, label, bounds);
        return interaction.Activated;
    }

    internal UiButtonInteraction ProcessPlacedButton<TBehavior>(UiControlContext context, string id, UiRect bounds, out string resolvedId)
        where TBehavior : IUiButtonBehavior
    {
        var interaction = TBehavior.Process(context, id, bounds);
        resolvedId = ResolveId(id);
        ClaimAccessibilityField(resolvedId);
        return ApplyAccessibilityInvoke(resolvedId, bounds, interaction);
    }

    internal void PublishPlacedButton<TBehavior>(string resolvedId, string label, UiRect bounds, string? description = null)
        where TBehavior : IUiButtonBehavior =>
        ExposeAccessibility(resolvedId, _accessibilityParent, UiAccessibilityRole.Button, label, bounds,
            focusable : TBehavior.AccessibilityFocus, hint : description);
}
