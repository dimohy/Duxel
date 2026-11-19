namespace Duxel.Core;

/// <summary>A user-supplied icon can select regular/filled geometry from the current input state.</summary>
public interface IUiButtonIcon
{
    static abstract void Paint(UiControlContext context,UiRect bounds,UiColor color,UiButtonInteraction interaction);
}
public readonly record struct UiButtonIconMetrics(float Edge,float Gap,float MinimumWidth,float Height,float HorizontalPadding);
public interface IUiButtonIconSize
{
    static abstract UiControlTypographyScope BeginText(UiControlContext context,bool hasText);
    static abstract UiButtonIconMetrics Metrics {get;}
}
public readonly struct UiFluentSmallButtonIconSize:IUiButtonIconSize
{
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>UiFluentSmallButtonPresentation.BeginText(context,hasText);
    public static UiButtonIconMetrics Metrics=>new(20,4,64,24,8);
}
public readonly struct UiFluentMediumButtonIconSize:IUiButtonIconSize
{
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>UiFluentButtonPresentation.BeginText(context,hasText);
    public static UiButtonIconMetrics Metrics=>new(20,6,96,32,12);
}
public readonly struct UiFluentLargeButtonIconSize:IUiButtonIconSize
{
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>UiFluentLargeButtonPresentation.BeginText(context,hasText);
    public static UiButtonIconMetrics Metrics=>new(24,6,96,40,16);
}
public interface IUiButtonIconPosition {static abstract bool After {get;}}
public readonly struct UiButtonIconBefore:IUiButtonIconPosition {public static bool After=>false;}
public readonly struct UiButtonIconAfter:IUiButtonIconPosition {public static bool After=>true;}

/// <summary>Compiled icon/text composition; the same kernel handles icon-only content.</summary>
public readonly struct UiFluentIconButtonPresentation<TSize,TAppearance,TIcon,TPosition>:IUiButtonPresentation
    where TSize:IUiButtonIconSize where TAppearance:IUiButtonAppearance where TIcon:IUiButtonIcon where TPosition:IUiButtonIconPosition
{
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>TSize.BeginText(context,hasText);
    public static UiVector2 Measure(UiControlContext context,UiVector2 textSize,UiVector2 requestedSize)
    {
        var hasText=context.ButtonHasText;
        var metrics=TSize.Metrics;
        var height=hasText?MathF.Max(metrics.Height,textSize.Y+metrics.Height-metrics.Edge):metrics.Height;
        return new(requestedSize.X>0?requestedSize.X:hasText?
            MathF.Max(metrics.MinimumWidth,textSize.X+metrics.Edge+metrics.Gap+2*metrics.HorizontalPadding+2):metrics.Height,
            requestedSize.Y>0?requestedSize.Y:height);
    }
    public static void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,UiButtonInteraction interaction)
    {
        using var clip=context.Clip(bounds);
        var foreground=TAppearance.Paint(context,bounds,interaction);
        var metrics=TSize.Metrics;var hasText=text.Length>0;
        var width=metrics.Edge+(hasText?metrics.Gap+textSize.X:0);
        var start=bounds.X+(bounds.Width-width)/2;
        var iconX=hasText&&TPosition.After?start+textSize.X+metrics.Gap:start;
        TIcon.Paint(context,new(iconX,bounds.Y+(bounds.Height-metrics.Edge)/2,metrics.Edge,metrics.Edge),
            TAppearance.IconColor(context,interaction,foreground),interaction);
        if(hasText)
        {
            var textX=TPosition.After?start:start+metrics.Edge+metrics.Gap;
            context.TypographyButtonText(text,new(textX,bounds.Y,textSize.X,bounds.Height),textSize,foreground);
        }
    }
}
