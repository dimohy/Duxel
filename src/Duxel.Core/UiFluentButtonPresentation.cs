namespace Duxel.Core;

/// <summary>Fluent 2 medium button presentation, independent of numeric legacy skin selection.</summary>
public readonly struct UiFluentButtonPresentation : IUiButtonPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>context.BeginTypography<UiSemiboldTypeface,UiFluentType300>(hasText);
    public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) =>
        new(requestedSize.X > 0 ? requestedSize.X : MathF.Max(96, textSize.X + 26),
            requestedSize.Y > 0 ? requestedSize.Y : MathF.Max(32, textSize.Y + 12));

    public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds, UiButtonInteraction interaction)
    {
        UiFluentButtonPresentation<UiFluentSecondaryButtonAppearance>.Paint(context,text,textSize,bounds,interaction);
    }
}

public readonly struct UiFluentSmallButtonPresentation:IUiButtonPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>context.BeginTypography<UiRegularTypeface,UiFluentType200>(hasText);
    public static UiVector2 Measure(UiControlContext context,UiVector2 textSize,UiVector2 requestedSize)=>
        new(requestedSize.X>0?requestedSize.X:MathF.Max(64,textSize.X+18),requestedSize.Y>0?requestedSize.Y:MathF.Max(24,textSize.Y+8));
    public static void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentButtonPresentation.Paint(context,text,textSize,bounds,interaction);
}
public readonly struct UiFluentLargeButtonPresentation:IUiButtonPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>context.BeginTypography<UiSemiboldTypeface,UiFluentType400>(hasText);
    public static UiVector2 Measure(UiControlContext context,UiVector2 textSize,UiVector2 requestedSize)=>
        new(requestedSize.X>0?requestedSize.X:MathF.Max(96,textSize.X+34),requestedSize.Y>0?requestedSize.Y:MathF.Max(40,textSize.Y+18));
    public static void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,UiButtonInteraction interaction)=>
        UiFluentButtonPresentation.Paint(context,text,textSize,bounds,interaction);
}
