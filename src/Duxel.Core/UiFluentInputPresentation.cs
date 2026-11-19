namespace Duxel.Core;

public readonly struct UiFluentTextInputBehavior:IUiTextInputBehavior
{
    public static bool AccessibilityValue=>true;
    public static bool AccessibilityFocus=>true;
    public static UiInputEditResult Process(UiControlContext context,string resolvedId,ref string value,int maxLength,in UiInputLayout layout)=>
        context.EditNavigableSingleLine(resolvedId,ref value,maxLength,layout);
}
public interface IUiInputSize
{
    static virtual bool CanRetainMeasurement => false;
    static abstract float Height {get;}
    static abstract float Padding {get;}
    static virtual float InputPadding=>2;
    static virtual float Gap=>2;
    static virtual float IconSize=>20;
    static abstract UiControlTypographyScope BeginText(UiControlContext context);
}
public readonly struct UiFluentSmallInputSize:IUiInputSize
{
    public static bool CanRetainMeasurement => true;
    public static float Height=>24;public static float Padding=>8;public static float IconSize=>16;
    public static UiControlTypographyScope BeginText(UiControlContext context)=>context.BeginTypography<UiRegularTypeface,UiFluentType200>();
}
public readonly struct UiFluentMediumInputSize:IUiInputSize
{
    public static bool CanRetainMeasurement => true;
    public static float Height=>32;public static float Padding=>12;
    public static UiControlTypographyScope BeginText(UiControlContext context)=>context.BeginTypography<UiRegularTypeface,UiFluentType300>();
}
public readonly struct UiFluentLargeInputSize:IUiInputSize
{
    public static bool CanRetainMeasurement => true;
    public static float Height=>40;public static float Padding=>18;public static float InputPadding=>6;public static float Gap=>6;public static float IconSize=>24;
    public static UiControlTypographyScope BeginText(UiControlContext context)=>context.BeginTypography<UiRegularTypeface,UiFluentType400>();
}
public interface IUiInputAppearance
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiInputAppearance Kind {get;}
    static abstract void Paint(UiControlContext context,UiRect bounds,UiFluentInputPaint paint,bool focused);
}
public readonly struct UiFluentOutlineInput:IUiInputAppearance
{
    public static bool CanRetainMeasurement => true;
    public static UiInputAppearance Kind=>UiInputAppearance.Outline;
    public static void Paint(UiControlContext context,UiRect bounds,UiFluentInputPaint paint,bool focused)
    {
        context.Frame(bounds,paint.Background,paint.Border,4,1);
        UiFluentInputDrawing.Bottom(context,bounds,paint.Bottom,1,4);
        if(focused)UiFluentInputDrawing.Bottom(context,bounds,paint.Focus,2,4);
    }
}
public readonly struct UiFluentUnderlineInput:IUiInputAppearance
{
    public static bool CanRetainMeasurement => true;
    public static UiInputAppearance Kind=>UiInputAppearance.Underline;
    public static void Paint(UiControlContext context,UiRect bounds,UiFluentInputPaint paint,bool focused)=>
        UiFluentInputDrawing.Bottom(context,bounds,focused?paint.Focus:paint.Bottom,focused?2:1,0);
}
public readonly struct UiFluentFilledDarkerInput:IUiInputAppearance
{
    public static bool CanRetainMeasurement => true;
    public static UiInputAppearance Kind=>UiInputAppearance.FilledDarker;
    public static void Paint(UiControlContext context,UiRect bounds,UiFluentInputPaint paint,bool focused)
    {context.Frame(bounds,paint.Background,paint.Border,4,1);if(focused)UiFluentInputDrawing.Bottom(context,bounds,paint.Focus,2,4);}
}
public readonly struct UiFluentFilledLighterInput:IUiInputAppearance
{
    public static bool CanRetainMeasurement => true;
    public static UiInputAppearance Kind=>UiInputAppearance.FilledLighter;
    public static void Paint(UiControlContext context,UiRect bounds,UiFluentInputPaint paint,bool focused)=>UiFluentFilledDarkerInput.Paint(context,bounds,paint,focused);
}
internal static class UiFluentInputDrawing
{
    internal static void Bottom(UiControlContext context,UiRect bounds,UiColor color,float thickness,float radius)
    {
        using var clip=context.Clip(new(bounds.X,bounds.Y+bounds.Height-thickness,bounds.Width,thickness));
        context.Frame(bounds,new(0,0,0,0),color,radius,thickness);
    }
}
public readonly struct UiFluentInputPresentation<TSize,TAppearance>:IUiInputPresentation
    where TSize:IUiInputSize where TAppearance:IUiInputAppearance
{
    public static bool CanRetainMeasurement => TSize.CanRetainMeasurement && TAppearance.CanRetainMeasurement;
    public static bool SupportsChildren=>true;
    public static UiControlTypographyScope BeginText(UiControlContext context)=>TSize.BeginText(context);
    public static UiInputLayout Measure(UiControlContext context,string label,float width)=>UiFluentInputPresentation<TSize,TAppearance,UiInputTextAdornment,UiInputTextAdornment>.Measure(context,label,width);
    public static UiInputLayout MeasureContent(UiControlContext context,in UiInputLayout local,UiInputOptions options)=>UiFluentInputPresentation<TSize,TAppearance,UiInputTextAdornment,UiInputTextAdornment>.MeasureContent(context,local,options);
    public static UiInputLayout Arrange(UiControlContext context,in UiInputLayout local,UiInputOptions options,UiVector2 allocatedSize)=>UiFluentInputPresentation<TSize,TAppearance,UiInputTextAdornment,UiInputTextAdornment>.Arrange(context,local,options,allocatedSize);
    public static void Paint(UiControlContext context,string label,in UiInputLayout layout,in UiInputEditResult edit)=>UiFluentInputPresentation<TSize,TAppearance,UiInputTextAdornment,UiInputTextAdornment>.Paint(context,label,layout,edit);
}
public readonly struct UiFluentInputPresentation<TSize,TAppearance,TBefore,TAfter>:IUiInputPresentation
    where TSize:IUiInputSize where TAppearance:IUiInputAppearance where TBefore:IUiInputAdornment where TAfter:IUiInputAdornment
{
    public static bool CanRetainMeasurement => TSize.CanRetainMeasurement && TAppearance.CanRetainMeasurement
        && TBefore.CanRetainMeasurement && TAfter.CanRetainMeasurement;
    public static bool SupportsChildren=>true;
    public static UiControlTypographyScope BeginText(UiControlContext context)=>TSize.BeginText(context);
    public static UiInputLayout Measure(UiControlContext context,string label,float width)
    {
        var w=MathF.Max(1,width>0?width:context.AvailableWidth);var line=context.TypographyLineHeight;var edge=TAppearance.Kind==UiInputAppearance.Underline?0:1;var height=MathF.Max(TSize.Height,line+2);
        return new(new(w,height),new(0,0,w,height),new(TSize.Padding+edge,(height-(edge==0?1:0)-line)/2,w-2*(TSize.Padding+edge),line),default,line);
    }
    public static UiInputLayout MeasureContent(UiControlContext context,in UiInputLayout local,UiInputOptions options)
    {
        var hasBefore=local.BeforeChild||TBefore.IsPresent(options.ContentBefore);var hasAfter=local.AfterChild||TAfter.IsPresent(options.ContentAfter);
        if(!hasBefore&&!hasAfter)return local;
        var before=local.BeforeChild?new UiVector2(local.Before!.Value.Width,local.Before.Value.Height):hasBefore?TBefore.Measure(context,options.ContentBefore,TSize.IconSize):default;
        var after=local.AfterChild?new UiVector2(local.After!.Value.Width,local.After.Value.Height):hasAfter?TAfter.Measure(context,options.ContentAfter,TSize.IconSize):default;
        static void Validate(UiVector2 size)
        {if(!float.IsFinite(size.X)||!float.IsFinite(size.Y)||size.X<0||size.Y<0)throw new InvalidOperationException("Input adornment returned invalid size.");}
        Validate(before);Validate(after);
        var edge=TAppearance.Kind==UiInputAppearance.Underline?0:1;var bottom=edge==0?1:edge;
        var size=local.Size with{Y=MathF.Max(local.Size.Y,MathF.Max(before.Y,after.Y)+edge+bottom)};
        return ArrangeContent(local,size,hasBefore?before:null,hasAfter?after:null);
    }
    public static UiInputLayout Arrange(UiControlContext context,in UiInputLayout local,UiInputOptions options,UiVector2 allocatedSize)
    {
        static UiVector2? Extent(UiRect? rect)=>rect is {} value?new(value.Width,value.Height):null;
        return ArrangeContent(local,allocatedSize,Extent(local.Before),Extent(local.After));
    }
    private static UiInputLayout ArrangeContent(in UiInputLayout local,UiVector2 size,UiVector2? beforeSize,UiVector2? afterSize)
    {
        var hasBefore=beforeSize.HasValue;var hasAfter=afterSize.HasValue;
        var before=beforeSize.GetValueOrDefault();var after=afterSize.GetValueOrDefault();
        var edge=TAppearance.Kind==UiInputAppearance.Underline?0:1;var bottom=edge==0?1:edge;
        var height=size.Y;var width=size.X;var rootPadding=TSize.Padding-TSize.InputPadding;
        if(!hasBefore&&!hasAfter)
            return local with{Size=size,Field=new(0,0,width,height),Content=new(TSize.Padding+edge,(height-(edge==0?1:0)-local.LineHeight)/2,MathF.Max(0,width-2*(TSize.Padding+edge)),local.LineHeight),Editing=null,Before=null,After=null};
        var left=(float)edge;var right=width-edge;UiRect? beforeRect=null,afterRect=null;
        if(hasBefore){beforeRect=new(edge+rootPadding,edge+(height-edge-bottom-before.Y)/2,before.X,before.Y);left=edge+rootPadding+before.X+TSize.Gap;}
        if(hasAfter)
        {
            var minimum=left+(hasBefore?TSize.InputPadding:TSize.Padding)+TSize.InputPadding+TSize.Gap;
            var afterX=MathF.Max(minimum,width-edge-rootPadding-after.X);
            afterRect=new(afterX,edge+(height-edge-bottom-after.Y)/2,after.X,after.Y);right=afterX-TSize.Gap;
        }
        // Flex content shrinks to zero; an oversized adornment remains clipped by its root.
        left=Math.Clamp(left,edge,MathF.Max(edge,width-edge));right=Math.Clamp(right,left,MathF.Max(left,width-edge));
        var contentLeft=left+(hasBefore?TSize.InputPadding:TSize.Padding);
        var contentRight=right-(hasAfter?TSize.InputPadding:TSize.Padding);
        return local with{Size=size,Field=new(0,0,width,height),Content=new(contentLeft,(height-(edge==0?1:0)-local.LineHeight)/2,MathF.Max(0,contentRight-contentLeft),local.LineHeight),Editing=new(left,MathF.Min(edge,height),right-left,MathF.Max(0,height-edge-bottom)),Before=beforeRect,After=afterRect};
    }
    public static void Paint(UiControlContext context,string label,in UiInputLayout layout,in UiInputEditResult edit)
    {
        var state=!context.IsEnabled?UiButtonVisualState.Disabled:(edit.Active||edit.FocusWithin)?UiButtonVisualState.Pressed:edit.Hovered?UiButtonVisualState.Hover:UiButtonVisualState.Rest;
        var paint=UiFluentInputPalette.Resolve(context,state,TAppearance.Kind,layout.Options.Invalid,edit.Hovered&&edit.Held);
        TAppearance.Paint(context,layout.Field,paint,(edit.Active||edit.FocusWithin)&&context.IsEnabled);
        context.PaintEditableText(layout,edit,new(paint.Text,context.Theme.InputSelectionBg,paint.Text){Typography=true});
        var adornment=UiFluentInputPalette.Adornment(context);
        using var rootClip=context.Clip(layout.Field);
        if(!layout.BeforeChild&&layout.Before is {} before)TBefore.Paint(context,layout.Options.ContentBefore,before,adornment,edit);
        if(!layout.AfterChild&&layout.After is {} after)TAfter.Paint(context,layout.Options.ContentAfter,after,adornment,edit);
        if(edit.Text.Length==0&&string.IsNullOrEmpty(edit.CompositionText)&&!string.IsNullOrEmpty(layout.Options.Placeholder))
        {
            using var clip=context.Clip(layout.EditingBounds);
            var block=context.MeasureTextBlock(layout.Options.Placeholder);
            context.TypographyTextBlock(block,layout.Content,paint.Placeholder);
        }
    }
}
public enum UiInputAppearance {Outline,Underline,FilledDarker,FilledLighter}
public readonly record struct UiFluentInputPaint(UiColor Background,UiColor Border,UiColor Bottom,UiColor Focus,UiColor Text,UiColor Placeholder);
public static class UiFluentInputPalette
{
    /// <summary>Resolves authored input colors or the active system contrast roles.</summary>
    public static UiFluentInputPaint Resolve(UiControlContext context, UiButtonVisualState state,
        UiInputAppearance appearance, bool invalid = false, bool held = false)
    {
        var authored = Resolve(context.FluentColorScheme, state, appearance, invalid, held);
        if (context.Theme.ForcedColors is not { } forced)
            return authored;
        var disabled = state == UiButtonVisualState.Disabled;
        var foreground = disabled ? forced.GrayText : forced.CanvasText;
        return new(disabled || appearance == UiInputAppearance.Underline ? default : forced.Canvas,
            foreground, foreground, disabled ? default : forced.Highlight, foreground, foreground);
    }

    internal static UiColor Adornment(UiControlContext context)
    {
        if (context.Theme.ForcedColors is { } forced)
            return context.IsEnabled ? forced.CanvasText : forced.GrayText;
        return context.IsEnabled
            ? context.FluentColorScheme == UiFluentColorScheme.Dark ? new(173, 173, 173) : new(97, 97, 97)
            : Resolve(context.FluentColorScheme, UiButtonVisualState.Disabled, UiInputAppearance.Outline).Text;
    }

    public static UiFluentInputPaint Resolve(UiFluentColorScheme scheme,UiButtonVisualState state,UiInputAppearance appearance,bool invalid=false,bool held=false)
    {
        if(scheme is not(UiFluentColorScheme.Light or UiFluentColorScheme.Dark)||(uint)state>3||(uint)appearance>3)throw new ArgumentOutOfRangeException(nameof(state));
        var dark=scheme==UiFluentColorScheme.Dark;var transparent=new UiColor(0,0,0,0);
        var background=appearance==UiInputAppearance.Underline?transparent:appearance==UiInputAppearance.FilledDarker?(dark?new(20,20,20):new(245,245,245)):dark?new(41,41,41):new(255,255,255);
        if(state==UiButtonVisualState.Disabled)return new(transparent,dark?new(66,66,66):new(224,224,224),dark?new(66,66,66):new(224,224,224),transparent,dark?new(92,92,92):new(189,189,189),dark?new(92,92,92):new(189,189,189));
        var border=UiFluentButtonPalette.Resolve(scheme,state).Border;
        var bottom=UiFluentCheckboxPalette.Resolve(scheme,state,false).Border;
        if(appearance is UiInputAppearance.FilledDarker or UiInputAppearance.FilledLighter)border=bottom=transparent;
        if(invalid&&state!=UiButtonVisualState.Pressed)border=bottom=dark?new(227,125,128):new(209,52,56);
        return new(background,border,bottom,held?UiFluentCheckboxPalette.Resolve(scheme,UiButtonVisualState.Pressed,true).Background:dark?new(71,158,245):new(15,108,189),dark?new(255,255,255):new(36,36,36),dark?new(153,153,153):new(112,112,112));
    }
}

[UiWidgetStyle(typeof(UiInput<,>),typeof(UiFluentTextInputBehavior),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentOutlineInput>))]
[UiComponent("Input",UiComponentExposure.Fluent)]
public partial class UiFluentInput;
