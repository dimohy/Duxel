namespace Duxel.Core;

/// <summary>A statically selected Button frame algorithm returning its text color.</summary>
public interface IUiButtonAppearance
{
    static abstract UiColor Paint(UiControlContext context,UiRect bounds,UiButtonInteraction interaction);
    static virtual UiColor IconColor(UiControlContext context,UiButtonInteraction interaction,UiColor foreground)=>foreground;
}

public readonly struct UiFluentSecondaryButtonAppearance:IUiButtonAppearance
{
    public static UiColor Paint(UiControlContext context,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentSecondaryButtonAppearance<UiFluentRoundedButtonShape>.Paint(context,bounds,interaction);
}
public readonly struct UiFluentOutlineButtonAppearance:IUiButtonAppearance
{
    public static UiColor Paint(UiControlContext context,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentOutlineButtonAppearance<UiFluentRoundedButtonShape>.Paint(context,bounds,interaction);
}
public readonly struct UiFluentSubtleButtonAppearance:IUiButtonAppearance
{
    public static UiColor IconColor(UiControlContext context,UiButtonInteraction interaction,UiColor foreground)=>
        UiFluentSubtleButtonAppearance<UiFluentRoundedButtonShape>.IconColor(context,interaction,foreground);
    public static UiColor Paint(UiControlContext context,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentSubtleButtonAppearance<UiFluentRoundedButtonShape>.Paint(context,bounds,interaction);
}
public readonly struct UiFluentTransparentButtonAppearance:IUiButtonAppearance
{
    public static UiColor Paint(UiControlContext context,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentTransparentButtonAppearance<UiFluentRoundedButtonShape>.Paint(context,bounds,interaction);
}
public readonly struct UiFluentPrimaryButtonAppearance:IUiButtonAppearance
{
    public static UiColor Paint(UiControlContext context,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentPrimaryButtonAppearance<UiFluentRoundedButtonShape>.Paint(context,bounds,interaction);
}

public readonly struct UiFluentSecondaryButtonAppearance<TShape>:IUiButtonAppearance where TShape:IUiButtonShape
{
    public static UiColor Paint(UiControlContext context,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentButtonFrame.Paint<TShape>(context,bounds,interaction,UiFluentButtonPalette.Resolve(context.FluentColorScheme,UiFluentButtonFrame.State(context,interaction)));
}
public readonly struct UiFluentOutlineButtonAppearance<TShape>:IUiButtonAppearance where TShape:IUiButtonShape
{
    public static UiColor Paint(UiControlContext context,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentButtonFrame.Paint<TShape>(context,bounds,interaction,UiFluentButtonPalette.Outline(context.FluentColorScheme,UiFluentButtonFrame.State(context,interaction)));
}
public readonly struct UiFluentSubtleButtonAppearance<TShape>:IUiButtonAppearance where TShape:IUiButtonShape
{
    public static UiColor IconColor(UiControlContext context,UiButtonInteraction interaction,UiColor foreground)=>
        UiFluentButtonPalette.Transparent(context.FluentColorScheme,UiFluentButtonFrame.State(context,interaction)).Foreground;
    public static UiColor Paint(UiControlContext context,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentButtonFrame.Paint<TShape>(context,bounds,interaction,UiFluentButtonPalette.Subtle(context.FluentColorScheme,UiFluentButtonFrame.State(context,interaction)));
}
public readonly struct UiFluentTransparentButtonAppearance<TShape>:IUiButtonAppearance where TShape:IUiButtonShape
{
    public static UiColor Paint(UiControlContext context,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentButtonFrame.Paint<TShape>(context,bounds,interaction,UiFluentButtonPalette.Transparent(context.FluentColorScheme,UiFluentButtonFrame.State(context,interaction)));
}
public readonly struct UiFluentPrimaryButtonAppearance<TShape>:IUiButtonAppearance where TShape:IUiButtonShape
{
    public static UiColor Paint(UiControlContext context,UiRect bounds,UiButtonInteraction interaction)
    {
        var paint=UiFluentButtonPalette.Primary(context.FluentColorScheme,UiFluentButtonFrame.State(context,interaction));
        var color=UiFluentButtonFrame.Paint<TShape>(context,bounds,interaction,paint);
        if(context.IsEnabled&&interaction.FocusVisible&&!interaction.Hovered)
            TShape.Paint(context,bounds,default,new(255,255,255),2);
        return color;
    }
}

internal static class UiFluentButtonFrame
{
    internal static UiButtonVisualState State(UiControlContext context,UiButtonInteraction interaction)=>
        !context.IsEnabled?UiButtonVisualState.Disabled:interaction.Held?UiButtonVisualState.Pressed:
            interaction.Hovered?UiButtonVisualState.Hover:UiButtonVisualState.Rest;
    internal static UiColor Paint<TShape>(UiControlContext context,UiRect bounds,UiButtonInteraction interaction,UiButtonPaint paint) where TShape:IUiButtonShape
    {
        var focused=context.IsEnabled&&interaction.FocusVisible;
        TShape.Paint(context,bounds,paint.Background,focused?paint.Focus:paint.Border,0);
        if(focused)TShape.Paint(context,bounds,default,paint.Focus,1);
        return paint.Foreground;
    }
}

/// <summary>Medium Button with an independently replaceable compile-time frame implementation.</summary>
public readonly struct UiFluentButtonPresentation<TAppearance>:IUiButtonPresentation where TAppearance:IUiButtonAppearance
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>UiFluentButtonPresentation.BeginText(context,hasText);
    public static UiVector2 Measure(UiControlContext context,UiVector2 textSize,UiVector2 requestedSize)=>UiFluentButtonPresentation.Measure(context,textSize,requestedSize);
    public static void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,UiButtonInteraction interaction)=>
        context.TypographyButtonText(text,bounds,textSize,TAppearance.Paint(context,bounds,interaction));
}
public readonly struct UiFluentSmallButtonPresentation<TAppearance>:IUiButtonPresentation where TAppearance:IUiButtonAppearance
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>UiFluentSmallButtonPresentation.BeginText(context,hasText);
    public static UiVector2 Measure(UiControlContext context,UiVector2 textSize,UiVector2 requestedSize)=>UiFluentSmallButtonPresentation.Measure(context,textSize,requestedSize);
    public static void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentButtonPresentation<TAppearance>.Paint(context,text,textSize,bounds,interaction);
}
public readonly struct UiFluentLargeButtonPresentation<TAppearance>:IUiButtonPresentation where TAppearance:IUiButtonAppearance
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>UiFluentLargeButtonPresentation.BeginText(context,hasText);
    public static UiVector2 Measure(UiControlContext context,UiVector2 textSize,UiVector2 requestedSize)=>UiFluentLargeButtonPresentation.Measure(context,textSize,requestedSize);
    public static void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentButtonPresentation<TAppearance>.Paint(context,text,textSize,bounds,interaction);
}
