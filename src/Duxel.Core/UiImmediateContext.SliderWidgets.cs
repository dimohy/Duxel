namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private string? _editedSliderId;
    internal UiRect ControlClipRect=>CurrentClipRect;
    public bool Slider<TBehavior,TPresentation>(string id,ref float value,UiSliderOptions options,UiVector2 size=default)
        where TBehavior:IUiSliderBehavior where TPresentation:IUiSliderPresentation
        =>Slider<TBehavior,TPresentation>(id,ref value,options,size,false);
    public bool Slider<TBehavior,TPresentation>(string id,ref float value,UiSliderOptions options,UiVector2 size,bool constrainLayout)
        where TBehavior:IUiSliderBehavior where TPresentation:IUiSliderPresentation
        => Slider<TBehavior, TPresentation>(id, id, ref value, options, size, constrainLayout);
    internal bool Slider<TBehavior, TPresentation>(string id, string label, ref float value, UiSliderOptions options, UiVector2 size, bool constrainLayout)
        where TBehavior : IUiSliderBehavior where TPresentation : IUiSliderPresentation
    {
        options.Validate();options.Clamp(value);
        var context=new UiControlContext(this);
        var layout=MeasureSliderLocal<TPresentation>(context,options,size,constrainLayout);
        var cursor=AdvanceCursor(layout.Size);var bounds=new UiRect(cursor.X,cursor.Y,layout.Size.X,layout.Size.Y);
        var result=TBehavior.Process(context,id,bounds,layout,options,value);
        var resolvedId = ResolveId(id);
        ClaimAccessibilityField(resolvedId);
        var next = result.Value;
        if (ApplyAccessibilityRangeValue(resolvedId, bounds, options, ref next))
        {
            result = result with { Value = next, Changed = true };
            _lastItemEdited = true;
            _requestFrame?.Invoke();
        }
        if(result.Changed)value=result.Value;
        var clip=IntersectRect(CurrentClipRect,bounds);
        if(clip.Width>0&&clip.Height>0)TPresentation.Paint(context,bounds,layout,options,result);
        ExposeAccessibility(resolvedId, _accessibilityParent, UiAccessibilityRole.Slider, label, bounds, valuePattern : false,
            range : new UiAccessibilityRangeInfo(result.Value, options.Minimum, options.Maximum, options.Step ?? 1,
                ((double)options.Maximum - options.Minimum) / 10) { Vertical = options.Vertical });
        return result.Changed;
    }
    internal UiSliderInteraction ProcessSlider(string id,UiRect bounds,UiSliderLayout layout,UiSliderOptions options,float value)
    {
        var control=ProcessButton<RangeControlKeys>(id,bounds,UiButtonActivation.Press);
        _lastItemEdited=false;
        var next=options.Clamp(value);
        if(!IsControlEnabled)return new(next,false,control);
        var reversed = options.IsReversedInLayout(RightToLeft);
        if(control.Held&&_leftMouseDown&&(options.Vertical?layout.Track.Height:layout.Track.Width)>0)
        {
            var track=layout.Track;
            var fraction=options.Vertical?1-(_mousePosition.Y-bounds.Y-track.Y)/track.Height
                :(_mousePosition.X-bounds.X-track.X)/track.Width;
            fraction=Math.Clamp(fraction,0,1);if(reversed)fraction=1-fraction;
            next=options.Snap(options.Minimum+(double)fraction*(options.Maximum-options.Minimum));
            if (options.CoalescePointerMoves && _lastItemId is { } captureId)
                _state.PointerMoveCoalescing = (captureId, null);
        }
        if(control.Focused)
        {
            foreach(var key in _keyEvents)
            {
                if(!key.IsDown||(key.Modifiers&(KeyModifiers.Ctrl|KeyModifiers.Alt|KeyModifiers.Super))!=0)continue;
                var reverse=options.Reversed?-1:1;
                var horizontalSign = reversed ? -1 : 1;
                double? candidate=key.Key switch
                {
                    UiKey.LeftArrow=>next-(options.Step??1)*horizontalSign,
                    UiKey.RightArrow=>next+(options.Step??1)*horizontalSign,
                    UiKey.DownArrow=>next-(options.Step??1)*reverse,
                    UiKey.UpArrow=>next+(options.Step??1)*reverse,
                    UiKey.Home=>options.Minimum,
                    UiKey.End=>options.Maximum,
                    UiKey.PageUp=>next+(options.Maximum-options.Minimum)/10d,
                    UiKey.PageDown=>next-(options.Maximum-options.Minimum)/10d,
                    _=>null
                };
                if(candidate is {} amount)next=options.Snap(amount);
            }
        }
        var changed=next!=options.Clamp(value);
        if(changed){_lastItemEdited=true;if(control.Held)_editedSliderId=_lastItemId;_requestFrame?.Invoke();}
        if(!control.Held&&_editedSliderId==_lastItemId&&_state.PreviousActiveId==_lastItemId)_lastItemEdited=true;
        return new(next,changed,control);
    }
}
