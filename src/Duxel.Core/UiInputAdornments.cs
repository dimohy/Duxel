namespace Duxel.Core;

/// <summary>Statically selected non-interactive content inside an input's root.</summary>
public interface IUiInputAdornment
{
    static virtual bool CanRetainMeasurement => false;
    static abstract bool IsPresent(string? content);
    static abstract UiVector2 Measure(UiControlContext context,string? content,float iconSize);
    static abstract void Paint(UiControlContext context,string? content,UiRect bounds,UiColor color,in UiInputEditResult edit);
}
public readonly struct UiInputTextAdornment:IUiInputAdornment
{
    public static bool CanRetainMeasurement => true;
    public static bool IsPresent(string? content)=>content is not null;
    public static UiVector2 Measure(UiControlContext context,string? content,float iconSize)
    {
        if(content!.AsSpan().IndexOfAny('\r','\n')>=0)throw new ArgumentException("Input text adornments require a single line.",nameof(content));
        return context.MeasureText(content!) with{Y=context.TypographyLineHeight};
    }
    public static void Paint(UiControlContext context,string? content,UiRect bounds,UiColor color,in UiInputEditResult edit)
    {var block=new UiControlTextBlock(content!,new(bounds.Width,bounds.Height),0);context.TypographyTextBlock(block,bounds,color);}
}
public readonly struct UiInputIconAdornment<TIcon>:IUiInputAdornment where TIcon:IUiButtonIcon
{
    public static bool CanRetainMeasurement => true;
    public static bool IsPresent(string? content)=>true;
    public static UiVector2 Measure(UiControlContext context,string? content,float iconSize)=>new(iconSize,iconSize);
    public static void Paint(UiControlContext context,string? content,UiRect bounds,UiColor color,in UiInputEditResult edit)=>
        TIcon.Paint(context,bounds,color,new(false,edit.Hovered,edit.Held){Focused=edit.Active});
}
