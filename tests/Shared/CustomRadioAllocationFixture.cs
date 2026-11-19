using Duxel.Core;

internal readonly struct CustomRadioPresentation:IUiRadioGroupPresentation
{
    internal static int Paints;
    public static UiRadioGroupLayout Measure(UiControlContext context,string label,ReadOnlySpan<UiRadioOption> items,Span<UiRadioOptionLayout> layouts,float width)
    {
        var size=context.ConstrainToLayout(new(60,32));var row=size.Y/Math.Max(1,items.Length);
        for(var i=0;i<items.Length;i++)layouts[i]=new(new(0,i*row,size.X,row),default,default,default,string.Empty,null);
        return new(size,default,string.Empty);
    }
    public static void PaintLabel(UiControlContext context,UiRadioGroupLayout layout,UiRect bounds){}
    public static void PaintOption(UiControlContext context,UiRadioOptionLayout layout,bool selected,UiButtonInteraction interaction)
    {Paints++;context.Frame(layout.Bounds,context.Theme.Button,context.Theme.Border,4,1);}
}
[UiWidgetStyle(typeof(UiRadioGroup<,>),typeof(UiFluentRadioGroupBehavior),typeof(CustomRadioPresentation))]
internal partial class GeneratedAllocatedRadio;
