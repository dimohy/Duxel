namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal bool IsControlEnabled=>(_currentItemFlags&UiItemFlags.Disabled)==0;
    internal UiToggleSkin ControlToggleSkin=>_controlSkins.Toggle;
    internal void PaintControlLine(UiVector2 start,UiVector2 end,UiColor color,float width)
    {
        _builder.PushClipRect(CurrentClipRect);
        try{_builder.AddLine(start,end,color,width,_whiteTexture);}
        finally{_builder.PopClipRect();}
    }
    public bool Toggle<TValue,TValuePolicy,TBehavior,TPresentation>(string label,ref TValue value)
        where TValuePolicy:IUiToggleValue<TValue>
        where TBehavior:IUiButtonBehavior
        where TPresentation:IUiTogglePresentation
        => Toggle<TValue,TValuePolicy,TBehavior,TPresentation>(label,label,ref value);

    public bool ToggleButton(string label,ref bool value) =>
        Toggle<bool,UiBooleanToggleValue,UiReleaseButtonBehavior,UiFluentToggleButtonPresentation>(label,ref value);

    public bool Toggle<TValue,TValuePolicy,TBehavior,TPresentation>(string label,ref TValue value,bool constrainLayout)
        where TValuePolicy:IUiToggleValue<TValue>
        where TBehavior:IUiButtonBehavior
        where TPresentation:IUiTogglePresentation
        =>Toggle<TValue,TValuePolicy,TBehavior,TPresentation>(label,label,ref value,constrainLayout);

    internal bool Toggle<TValue,TValuePolicy,TBehavior,TPresentation>(string id,string label,ref TValue value,bool constrainLayout=false)
        where TValuePolicy:IUiToggleValue<TValue>
        where TBehavior:IUiButtonBehavior
        where TPresentation:IUiTogglePresentation
    {
        label??="Checkbox";
        id??=label;
        var text=GetDisplayLabel(label);
        var context=new UiControlContext(this);
        using var typography=TPresentation.BeginText(context,text.Length!=0);
        var (textSize,layout)=MeasureToggleLocal<TPresentation>(context,text,typography.LineHeight,constrainLayout);
        var cursor=AdvanceCursor(layout.Size);
        var bounds=new UiRect(cursor.X,cursor.Y,layout.Size.X,layout.Size.Y);
        var interaction=TBehavior.Process(context,id,bounds);
        var resolvedId = ResolveId(id);
        ClaimAccessibilityField(resolvedId);
        if (!interaction.Activated && ApplyAccessibilityToggle(resolvedId, bounds))
            interaction = interaction with { Activated = true };
        if(interaction.Activated)value=TValuePolicy.Activate(value);
        var checkedValue = TValuePolicy.Checked(value);
        TPresentation.Paint(context with { MotionKey = resolvedId }, text, textSize, bounds, layout, interaction, checkedValue);
        var state = checkedValue switch
        {
            true => UiAccessibilityToggleState.On,
            false => UiAccessibilityToggleState.Off,
            null => UiAccessibilityToggleState.Indeterminate
        };
        ExposeAccessibility(resolvedId, _accessibilityParent, TPresentation.AccessibilityRole, label, bounds,
            focusable : TBehavior.AccessibilityFocus, valuePattern : false, toggleState : state);
        return interaction.Activated;
    }

    private bool ApplyAccessibilityToggle(string id, UiRect bounds)
    {
        var request = _accessibility?.Take(id, UiAccessibilityAction.Toggle);
        if (request is null) return false;
        if (!IsControlEnabled)
        {
            request.Result = UiAccessibilityResult.Disabled;
            return false;
        }
        var clip = IntersectRect(CurrentClipRect, bounds);
        if (clip.Width <= 0 || clip.Height <= 0 || _popupTierDepth < _blockingKeyboardTier)
        {
            request.Result = UiAccessibilityResult.Unavailable;
            return false;
        }
        request.Result = UiAccessibilityResult.Success;
        _lastItemEdited = true;
        _requestFrame?.Invoke();
        return true;
    }

    internal UiToggleLayout MeasureLegacyCheckbox(UiVector2 textSize,bool hasLabel)
    {
        var compact=MathF.Max(12,(GetTextLineHeight()*.78f)+2);
        var size=_controlSkins.Toggle.CheckboxSize??MathF.Min(MathF.Max(12,GetFrameHeight()-2),compact);
        if(_textSettings.PixelSnap)size=MathF.Round(size);
        var height=MathF.Max(size,textSize.Y);
        return new(new(size+(hasLabel?CheckboxSpacing+textSize.X:0),height),new(0,(height-size)*.5f,size,size),new(size+CheckboxSpacing,0));
    }
    internal UiToggleLayout MeasureLegacySwitch(UiVector2 textSize,bool hasLabel)
    {
        var width=_controlSkins.Toggle.SwitchWidth??52;
        var height=_controlSkins.Toggle.SwitchHeight??32;
        var totalHeight=MathF.Max(height,textSize.Y);
        return new(new(width+(hasLabel?CheckboxSpacing+textSize.X:0),totalHeight),
            new(0,(totalHeight-height)/2,width,height),new(width+CheckboxSpacing,0));
    }
    internal void PaintLegacySwitch(string text,UiVector2 textSize,UiRect bounds,UiToggleLayout layout,UiButtonInteraction interaction,bool value,float progress)
    {
        var skin=_controlSkins.Toggle;
        var rect=layout.Indicator with{X=bounds.X+layout.Indicator.X,Y=bounds.Y+layout.Indicator.Y};
        var fill=value?_theme.CheckMark:interaction.Held?_theme.CheckboxBgActive:interaction.Hovered?_theme.CheckboxBgHovered:_theme.CheckboxBg;
        var border=value?_theme.CheckMark:_theme.CheckboxBorder;
        AddDesignedFrame(rect,fill,border,rect.Height/2,skin.BorderWidth??2);
        var offDiameter = skin.OffThumbSize ?? 16;
        var onDiameter = skin.OnThumbSize ?? 24;
        var diameter = interaction.Held ? skin.PressedThumbSize ?? 28 : offDiameter + (onDiameter - offDiameter) * progress;
        var center = new UiVector2(rect.X + rect.Height / 2 + (rect.Width - rect.Height) * progress, rect.Y + rect.Height / 2);
        _builder.AddCircleFilled(center,diameter/2,value?_theme.ButtonText:_theme.CheckboxText,_whiteTexture,CurrentClipRect);
        PaintButtonText(text,new(bounds.X+layout.LabelOffset.X,bounds.Y+(bounds.Height-textSize.Y)/2),_theme.CheckboxText);
    }
    internal void PaintLegacyCheckbox(string text,UiVector2 textSize,UiRect bounds,UiToggleLayout layout,UiButtonInteraction interaction,bool value)
    {
        var skin=_controlSkins.Toggle;
        var x=bounds.X+layout.Indicator.X;var y=bounds.Y+layout.Indicator.Y;
        if(_textSettings.PixelSnap){x=MathF.Round(x);y=MathF.Round(y);}
        var box=new UiRect(x,y,layout.Indicator.Width,layout.Indicator.Height);
        var fill=interaction.Held?_theme.CheckboxBgActive:interaction.Hovered?_theme.CheckboxBgHovered:_theme.CheckboxBg;
        var border=interaction.Held?_theme.CheckboxBorderActive:interaction.Hovered?_theme.CheckboxBorderHovered:_theme.CheckboxBorder;
        var radius=skin.CornerRadius??_designTokens.ToggleCornerRadius;
        var inner=AddDesignedFrame(box,fill,border,radius,skin.BorderWidth??_designTokens.ControlBorderWidth);
        if(value)
        {
            var inset=MathF.Max(2,MathF.Round(box.Width/5.75f));
            var size=MathF.Max(0,MathF.Min(inner.Width,inner.Height)-inset*2);
            var checkX=inner.X+(inner.Width-size)*.5f;var checkY=inner.Y+(inner.Height-size)*.5f;
            if(_textSettings.PixelSnap){checkX=MathF.Round(checkX);checkY=MathF.Round(checkY);size=MathF.Round(size);}
            AddRectFilledRounded(new(checkX,checkY,size,size),_theme.CheckMark,_whiteTexture,MathF.Max(0,radius-1));
        }
        if(text.Length!=0)
        {
            var textRect=new UiRect(bounds.X+layout.LabelOffset.X,bounds.Y,textSize.X,bounds.Height);
            PaintButtonText(text,AlignRect(textRect,textSize,UiItemHorizontalAlign.Left,UiItemVerticalAlign.Center),_theme.CheckboxText);
        }
    }
}
