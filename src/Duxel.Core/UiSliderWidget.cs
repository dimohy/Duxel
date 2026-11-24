namespace Duxel.Core;

public enum UiSliderSize {Small,Medium}
public readonly record struct UiSliderOptions(float Minimum,float Maximum,float? Step=null,
    bool Vertical=false,bool Reversed=false,UiSliderSize Size=UiSliderSize.Medium)
{
    /// <summary>Controls marker painting independently from discrete value snapping.</summary>
    public bool ShowStepMarkers { get; init; } = true;
    /// <summary>Allows an ordered input batch to collapse consecutive moves while this slider
    /// holds pointer capture. Use when bindings only need the latest position, without intermediate value notifications.</summary>
    public bool CoalescePointerMoves { get; init; }
    public UiSliderOptions Validate()
    {
        if(!float.IsFinite(Minimum)||!float.IsFinite(Maximum)||Maximum<=Minimum||!float.IsFinite(Maximum-Minimum))
            throw new ArgumentOutOfRangeException(nameof(Maximum));
        if(Step is {} step&&(!float.IsFinite(step)||step<=0))throw new ArgumentOutOfRangeException(nameof(Step));
        if(Size is not (UiSliderSize.Small or UiSliderSize.Medium))throw new ArgumentOutOfRangeException(nameof(Size));
        return this;
    }
    public float Clamp(float value)
    {
        if(!float.IsFinite(value))throw new ArgumentOutOfRangeException(nameof(value));
        return Math.Clamp(value,Minimum,Maximum);
    }
    public float Snap(double value)
    {
        var step=(double)(Step??1);
        var last=Math.Floor(((double)Maximum-Minimum)/step);
        var index=Math.Clamp(Math.Floor((value-Minimum)/step+.5),0,last);
        return (float)Math.Clamp(Minimum+index*step,Minimum,Maximum);
    }
    public float Fraction(float value)=>(Clamp(value)-Minimum)/(Maximum-Minimum);

    internal bool IsReversedInLayout(bool rightToLeft) => Reversed ^ (!Vertical && rightToLeft);
}

/// <summary>Local geometry supplied by a presentation and consumed by the input policy.</summary>
public readonly record struct UiSliderLayout(UiVector2 Size,UiRect Track,float ThumbDiameter,float InnerThumbRadius);
public readonly record struct UiSliderInteraction(float Value,bool Changed,UiButtonInteraction Control);
public interface IUiSliderBehavior
{
    static abstract UiSliderInteraction Process(UiControlContext context,string id,UiRect bounds,
        UiSliderLayout layout,UiSliderOptions options,float value);
}
public interface IUiSliderPresentation
{
    /// <summary>Measurement depends only on options, requested size, constraints and the current layout environment.</summary>
    static virtual bool CanRetainMeasurement => false;
    static abstract UiSliderLayout Measure(UiControlContext context,UiSliderOptions options,UiVector2 requestedSize);
    static virtual UiSliderLayout Arrange(UiControlContext context,UiSliderLayout layout,UiSliderOptions options,UiVector2 allocatedSize)
        =>throw new NotSupportedException("This Slider presentation must implement Arrange to accept a different parent allocation.");
    static abstract void Paint(UiControlContext context,UiRect bounds,UiSliderLayout layout,
        UiSliderOptions options,UiSliderInteraction interaction);
}
public readonly struct UiFluentSliderBehavior:IUiSliderBehavior
{
    public static UiSliderInteraction Process(UiControlContext context,string id,UiRect bounds,
        UiSliderLayout layout,UiSliderOptions options,float value)=>context.Slide(id,bounds,layout,options,value);
}

public class UiSlider<TBehavior,TPresentation>:UiControlWidget, IUiLabelTarget
    where TBehavior:IUiSliderBehavior where TPresentation:IUiSliderPresentation
{
    private UiSliderOptions _options;
    private string _label;
    private UiBinding<float>? _value;
    private UiVector2 _size;
    public UiSlider(string label,UiBinding<float> value,float minimum=0,float maximum=100,float? step=null,
        UiWidgetIdentity identity=UiWidgetIdentity.Instance):base(identity)
    {
        ArgumentNullException.ThrowIfNull(label);ArgumentNullException.ThrowIfNull(value);
        _label=label;_value=value;_options=new UiSliderOptions(minimum,maximum,step).Validate();
    }
    public string Label
    {
        get=>_label;
        set{ThrowIfDisposed();ArgumentNullException.ThrowIfNull(value);if(string.Equals(_label,value,StringComparison.Ordinal))return;_label=value;RequestRebuild();}
    }
    public UiBinding<float> Value=>_value??throw new ObjectDisposedException(nameof(UiSlider<TBehavior,TPresentation>));
    /// <summary>Whether the most recent traversal held this slider's pointer capture.</summary>
    public bool IsPointerDragging { get; private set; }
    public UiSliderOptions Options
    {
        get=>_options;
        set{ThrowIfDisposed();var options=value.Validate();if(_options==options)return;_options=options;RequestRebuild();}
    }
    public UiVector2 Size
    {
        get=>_size;
        set{ThrowIfDisposed();if(_size==value)return;_size=value;RequestRebuild();}
    }
    protected virtual void OnChanged(float value)=>Value.Value=value;
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context)=>context.MeasureSlider<TPresentation>(Options,Size);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var value = _value!.Value;
        if (ui.Slider<TBehavior, TPresentation>(PrimaryInputId(Label), Label, ref value, Options, Size, constrainLayout : true))
            OnChanged(value);
        IsPointerDragging = ui.IsItemActive() && ui.IsMouseDown((int)UiMouseButton.Left);
    }
    private protected override void ReleaseOwnedResources(){_value=null;_label=string.Empty;base.ReleaseOwnedResources();}
}

public readonly struct UiFluentSliderPresentation:IUiSliderPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiSliderLayout Arrange(UiControlContext context,UiSliderLayout layout,UiSliderOptions options,UiVector2 allocatedSize)
    {
        var main=options.Vertical?allocatedSize.Y:allocatedSize.X;
        var inset=MathF.Min(layout.ThumbDiameter/2,main/2);
        var track=options.Vertical?new UiRect((allocatedSize.X-layout.Track.Width)/2,inset,layout.Track.Width,MathF.Max(0,main-2*inset))
            :new UiRect(inset,(allocatedSize.Y-layout.Track.Height)/2,MathF.Max(0,main-2*inset),layout.Track.Height);
        return layout with{Size=allocatedSize,Track=track};
    }
    public static UiSliderLayout Measure(UiControlContext context,UiSliderOptions options,UiVector2 requestedSize)
    {
        if(!float.IsFinite(requestedSize.X)||!float.IsFinite(requestedSize.Y))throw new ArgumentOutOfRangeException(nameof(requestedSize));
        var small=options.Size==UiSliderSize.Small;
        var thumb=small?16f:20f;var cross=small?24f:32f;var rail=small?2f:4f;
        var size=options.Vertical?new UiVector2(MathF.Max(cross,requestedSize.X),MathF.Max(120,requestedSize.Y))
            :new UiVector2(MathF.Max(120,requestedSize.X>0?requestedSize.X:context.AvailableWidth),MathF.Max(cross,requestedSize.Y));
        var track=options.Vertical?new UiRect((size.X-rail)/2,thumb/2,rail,size.Y-thumb)
            :new UiRect(thumb/2,(size.Y-rail)/2,size.X-thumb,rail);
        return new(size,track,thumb,small?5:6);
    }
    public static void Paint(UiControlContext context,UiRect bounds,UiSliderLayout layout,
        UiSliderOptions options,UiSliderInteraction interaction)
    {
        var track=layout.Track with {X=bounds.X+layout.Track.X,Y=bounds.Y+layout.Track.Y};
        var enabled=context.IsEnabled;
        var state = !enabled ? UiButtonVisualState.Disabled : interaction.Control.Held ? UiButtonVisualState.Pressed
            : interaction.Control.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var paint = UiFluentSliderPalette.Resolve(context.FluentColorScheme, state);
        var forcedColors = context.Theme.ForcedColors;
        var brand = paint.Fill;
        var neutral = paint.Rail;
        if (forcedColors is { } colors)
        {
            brand = enabled ? colors.Highlight : colors.GrayText;
            neutral = enabled ? colors.CanvasText : colors.GrayText;
        }
        var fraction=options.Fraction(interaction.Value);
        var reversed = options.IsReversedInLayout(context.RightToLeft);
        var position = reversed ? 1 - fraction : fraction;
        context.Frame(track,neutral,neutral,4,0);
        var filled=options.Vertical
            ?track with {Y=track.Y+track.Height*(options.Reversed?0:1-fraction),Height=track.Height*fraction}
            :track with {X=track.X+track.Width*(reversed?1-fraction:0),Width=track.Width*fraction};
        if(fraction>0)context.Frame(filled,brand,brand,4,0);
        if(options.ShowStepMarkers && options.Step is {} step)
        {
            var length=options.Vertical?track.Height:track.Width;
            var pitch=(double)length*step/(options.Maximum-options.Minimum);
            var mark = forcedColors?.HighlightText ?? paint.Surface;
            if(pitch<=1)context.Frame(track,mark,mark,4,0);
            else
            {
                // Clip the periodic pattern before emitting geometry, preserving its phase.
                var clip=context.ClipRect;
                var lower=options.Vertical?clip.Y-track.Y:clip.X-track.X;
                var upper=lower+(options.Vertical?clip.Height:clip.Width);
                var reverse = options.Vertical ? !reversed : reversed;
                // Reflect the entire one-unit marker, not just its leading coordinate.
                var markerOrigin = !options.Vertical && context.RightToLeft ? -1 : 0;
                lower -= markerOrigin;
                upper -= markerOrigin;
                if(reverse)(lower,upper)=(length-upper,length-lower);
                var first=Math.Max(1,Math.Ceiling(lower/pitch));
                var last=Math.Min(Math.Ceiling(length/pitch)-1,Math.Floor(upper/pitch));
                var count=checked((int)Math.Max(0,last-first+1));
                for(var index=0;index<count;index++)
                {
                    var offset=(float)((first+index)*pitch);if(reverse)offset=length-offset;
                    context.Frame(options.Vertical?new(track.X,track.Y+offset,track.Width,1)
                        :new(track.X+offset+markerOrigin,track.Y,1,track.Height),mark,mark,0,0);
                }
            }
        }
        var travel=options.Vertical?track.Height:track.Width;
        var inset=MathF.Min(layout.InnerThumbRadius,travel/2);
        var distance=Math.Clamp(position*travel,inset,travel-inset);
        var center=options.Vertical?new UiVector2(track.X+track.Width/2,track.Y+track.Height-distance)
            :new UiVector2(track.X+distance,track.Y+track.Height/2);
        var radius=layout.ThumbDiameter/2;
        context.Circle(center,radius,paint.ThumbBorder);
        context.Circle(center,radius-layout.ThumbDiameter*.05f,paint.Surface);
        context.Circle(center,layout.InnerThumbRadius,brand);
        if (interaction.Control.FocusVisible)
        {
            if (forcedColors is { } systemColors)
            {
                // Fluent's 2px outline starts inside the horizontal root and outside the vertical root.
                var insetX = options.Vertical ? -6 : 2;
                var outline = new UiRect(bounds.X + insetX, bounds.Y, MathF.Max(0, bounds.Width - 2 * insetX), bounds.Height);
                context.Frame(outline, default, systemColors.Highlight, 4, 2);
            }
            else
                context.FocusOutline(bounds, 4);
        }
    }
}
