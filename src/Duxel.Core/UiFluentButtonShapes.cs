namespace Duxel.Core;

/// <summary>A statically bound frame drawing algorithm, including inset focus strokes.</summary>
public interface IUiButtonShape
{
    static abstract void Paint(UiControlContext context,UiRect bounds,UiColor fill,UiColor border,float inset);
}

public readonly struct UiFluentRoundedButtonShape:IUiButtonShape
{
    public static void Paint(UiControlContext context,UiRect bounds,UiColor fill,UiColor border,float inset)=>
        context.Frame(UiFluentButtonShape.Inset(bounds,inset),fill,border,MathF.Max(0,4-inset),1);
}
public readonly struct UiFluentSquareButtonShape:IUiButtonShape
{
    public static void Paint(UiControlContext context,UiRect bounds,UiColor fill,UiColor border,float inset)=>
        context.Frame(UiFluentButtonShape.Inset(bounds,inset),fill,border,0,1);
}
public readonly struct UiFluentCircularButtonShape:IUiButtonShape
{
    public static void Paint(UiControlContext context,UiRect bounds,UiColor fill,UiColor border,float inset)
    {
        var inner=UiFluentButtonShape.Inset(bounds,inset);
        context.Frame(inner,fill,border,MathF.Min(MathF.Max(0,10000-inset),MathF.Min(inner.Width,inner.Height)/2),1);
    }
}

internal static class UiFluentButtonShape
{
    internal static UiRect Inset(UiRect bounds,float inset)=>
        new(bounds.X+inset,bounds.Y+inset,MathF.Max(0,bounds.Width-2*inset),MathF.Max(0,bounds.Height-2*inset));
}
