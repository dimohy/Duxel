namespace Duxel.Core;

[UiFluentIcon("checkmark",UiFluentIconStyle.Filled,12)] public readonly partial struct UiFluentCheckboxCheck12;
[UiFluentIcon("checkmark",UiFluentIconStyle.Filled,16)] public readonly partial struct UiFluentCheckboxCheck16;
[UiFluentIcon("square",UiFluentIconStyle.Filled,12)] public readonly partial struct UiFluentCheckboxSquare12;
[UiFluentIcon("square",UiFluentIconStyle.Filled,16)] public readonly partial struct UiFluentCheckboxSquare16;
[UiFluentIcon("circle",UiFluentIconStyle.Filled,12)] public readonly partial struct UiFluentCheckboxCircle12;
[UiFluentIcon("circle",UiFluentIconStyle.Filled,16)] public readonly partial struct UiFluentCheckboxCircle16;

public interface IUiCheckboxSize
{
    static virtual bool CanRetainMeasurement => false;
    static abstract float IndicatorSize {get;}
    static abstract float GlyphSize {get;}
    static abstract void PaintMark(UiControlContext context,UiRect bounds,UiColor color,UiButtonInteraction interaction,bool mixed,bool circular);
}
public readonly struct UiFluentMediumCheckboxSize:IUiCheckboxSize
{
    public static bool CanRetainMeasurement => true;
    public static float IndicatorSize=>16;
    public static float GlyphSize=>12;
    public static void PaintMark(UiControlContext context,UiRect bounds,UiColor color,UiButtonInteraction interaction,bool mixed,bool circular)
    {
        if(!mixed)UiFluentCheckboxCheck12.Paint(context,bounds,color,interaction);
        else if(circular)UiFluentCheckboxCircle12.Paint(context,bounds,color,interaction);
        else UiFluentCheckboxSquare12.Paint(context,bounds,color,interaction);
    }
}
public readonly struct UiFluentLargeCheckboxSize:IUiCheckboxSize
{
    public static bool CanRetainMeasurement => true;
    public static float IndicatorSize=>20;
    public static float GlyphSize=>16;
    public static void PaintMark(UiControlContext context,UiRect bounds,UiColor color,UiButtonInteraction interaction,bool mixed,bool circular)
    {
        if(!mixed)UiFluentCheckboxCheck16.Paint(context,bounds,color,interaction);
        else if(circular)UiFluentCheckboxCircle16.Paint(context,bounds,color,interaction);
        else UiFluentCheckboxSquare16.Paint(context,bounds,color,interaction);
    }
}
public interface IUiCheckboxShape
{
    static abstract bool Circular {get;}
    static abstract void Paint(UiControlContext context,UiRect bounds,UiColor fill,UiColor border);
}
public readonly struct UiFluentSquareCheckboxShape:IUiCheckboxShape
{
    public static bool Circular=>false;
    public static void Paint(UiControlContext context,UiRect bounds,UiColor fill,UiColor border)=>context.Frame(bounds,fill,border,2,1);
}
public readonly struct UiFluentCircularCheckboxShape:IUiCheckboxShape
{
    public static bool Circular=>true;
    public static void Paint(UiControlContext context,UiRect bounds,UiColor fill,UiColor border)=>context.Frame(bounds,fill,border,bounds.Height/2,1);
}
public interface IUiCheckboxLabelPosition
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiToggleLayout Measure(float indicatorSize,UiVector2 textSize,bool hasLabel);
}
public readonly struct UiCheckboxLabelAfter:IUiCheckboxLabelPosition
{
    public static bool CanRetainMeasurement => true;
    public static UiToggleLayout Measure(float edge,UiVector2 textSize,bool hasLabel)=>
        new(new(edge+16+(hasLabel?textSize.X+12:0),MathF.Max(edge+16,textSize.Y)),new(8,8,edge,edge),new(edge+20,0));
}
public readonly struct UiCheckboxLabelBefore:IUiCheckboxLabelPosition
{
    public static bool CanRetainMeasurement => true;
    public static UiToggleLayout Measure(float edge,UiVector2 textSize,bool hasLabel)=>
        new(new(edge+16+(hasLabel?textSize.X+12:0),MathF.Max(edge+16,textSize.Y)),new(hasLabel?textSize.X+20:8,8,edge,edge),new(8,0));
}

/// <summary>Closed implementations choose measurement, shape and exact glyph exports at compile time.</summary>
public readonly struct UiFluentCheckboxPresentation<TSize,TShape,TLabel>:IUiTogglePresentation
    where TSize:IUiCheckboxSize where TShape:IUiCheckboxShape where TLabel:IUiCheckboxLabelPosition
{
    public static bool CanRetainMeasurement => TSize.CanRetainMeasurement && TLabel.CanRetainMeasurement;
    public static UiToggleLayout Arrange(UiControlContext context,UiToggleLayout layout,UiVector2 allocatedSize)=>layout.CenterContent(allocatedSize);
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>context.BeginTypography<UiRegularTypeface,UiFluentType300>(hasText);
    public static UiToggleLayout Measure(UiControlContext context,UiVector2 textSize,bool hasLabel)=>TLabel.Measure(TSize.IndicatorSize,textSize,hasLabel);
    public static void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,
        UiToggleLayout layout,UiButtonInteraction interaction,bool? value)
    {
        if (context.RightToLeft)
            layout = layout.MirrorHorizontally(textSize.X);
        var state=!context.IsEnabled?UiButtonVisualState.Disabled:interaction.Held?UiButtonVisualState.Pressed:
            interaction.Hovered?UiButtonVisualState.Hover:UiButtonVisualState.Rest;
        var paint = UiFluentCheckboxPalette.Resolve(context, state, value);
        var check = context.SampleStateMotion(value == true);
        var mixed = context.SampleStateMotion(value is null, secondary: true);
        var indicator=layout.Indicator with{X=bounds.X+layout.Indicator.X,Y=bounds.Y+(bounds.Height-layout.Indicator.Height)/2};
        var background = paint.Background;
        if (check != (value == true ? 1 : 0))
        {
            var on = UiFluentCheckboxPalette.Resolve(context, state, true);
            background = UiDisclosureMotion.Blend(default, on.Background, check);
        }
        TShape.Paint(context,indicator,background,paint.Border);
        if (check > 0 || mixed > 0)
        {
            using var clip=context.Clip(indicator);
            if (check > 0) PaintMark(context, indicator, interaction,
                value == true ? paint.Indicator : UiFluentCheckboxPalette.Resolve(context, state, true).Indicator, check, false);
            if (mixed > 0) PaintMark(context, indicator, interaction,
                value is null ? paint.Indicator : UiFluentCheckboxPalette.Resolve(context, state, null).Indicator, mixed, true);
        }
        if(interaction.FocusVisible)context.FocusOutline(bounds,4);
        context.TypographyIndicatorText(text,
            new(bounds.X + layout.LabelOffset.X, bounds.Y, textSize.X, bounds.Height), textSize, paint.Label);
    }

    private static void PaintMark(UiControlContext context, UiRect indicator, UiButtonInteraction interaction,
        UiColor color, float progress, bool mixed)
    {
        var edge = TSize.GlyphSize * (.7f + .3f * progress);
        var inset = (indicator.Height - edge) / 2;
        TSize.PaintMark(context, new(indicator.X + inset, indicator.Y + inset, edge, edge),
            UiDisclosureMotion.Blend(default, color, progress), interaction, mixed, TShape.Circular);
    }
}

public readonly record struct UiCheckboxPaint(UiColor Background,UiColor Border,UiColor Indicator,UiColor Label);
public static partial class UiFluentCheckboxPalette
{
    // Fluent UI8830c10b Checkbox tokens, resolved against the web brand rather than stale comments.
    public static UiCheckboxPaint Resolve(UiFluentColorScheme scheme,UiButtonVisualState state,bool? value)
    {
        if(scheme is not (UiFluentColorScheme.Light or UiFluentColorScheme.Dark))throw new ArgumentOutOfRangeException(nameof(scheme));
        if((uint)state>3)throw new ArgumentOutOfRangeException(nameof(state));
        var dark=scheme==UiFluentColorScheme.Dark;
        var transparent=new UiColor(0,0,0,0);
        if(state==UiButtonVisualState.Disabled)
        {
            var border=dark?new UiColor(66,66,66):new UiColor(224,224,224);
            var foreground=dark?new UiColor(92,92,92):new UiColor(189,189,189);
            return new(transparent,border,foreground,foreground);
        }
        var brand=(dark,state) switch
        {
            (false,UiButtonVisualState.Rest)=>new UiColor(15,108,189),
            (false,UiButtonVisualState.Hover)=>new UiColor(17,94,163),
            (false,_)=>new UiColor(15,84,140),
            (true,UiButtonVisualState.Rest)=>new UiColor(71,158,245),
            (true,UiButtonVisualState.Hover)=>new UiColor(98,171,245),
            _=>new UiColor(40,134,222)
        };
        var foreground1=dark?new UiColor(255,255,255):new UiColor(36,36,36);
        if(value is null)return new(transparent,brand,brand,foreground1);
        if(value==true)return new(brand,brand,dark?new UiColor(36,36,36):new UiColor(255,255,255),foreground1);
        var neutral=(dark,state) switch
        {
            (false,UiButtonVisualState.Rest)=>new UiColor(97,97,97),
            (false,UiButtonVisualState.Hover)=>new UiColor(87,87,87),
            (false,_)=>new UiColor(77,77,77),
            (true,UiButtonVisualState.Rest)=>new UiColor(173,173,173),
            (true,UiButtonVisualState.Hover)=>new UiColor(189,189,189),
            _=>new UiColor(179,179,179)
        };
        var label=state==UiButtonVisualState.Pressed?foreground1:state==UiButtonVisualState.Hover?
            dark?new UiColor(214,214,214):new UiColor(66,66,66):dark?new UiColor(173,173,173):new UiColor(97,97,97);
        return new(transparent,neutral,transparent,label);
    }
}
