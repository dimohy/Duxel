namespace Duxel.Core;

public readonly record struct UiSizeFactors(float? WidthFactor=null,float? HeightFactor=null)
{
    public UiSizeFactors Validate()
    {
        if(WidthFactor is {} w&&(!float.IsFinite(w)||w<0)||HeightFactor is {} h&&(!float.IsFinite(h)||h<0))
            throw new ArgumentOutOfRangeException(nameof(UiSizeFactors));
        return this;
    }
}
public readonly record struct UiAlignOptions(UiAlignment Alignment=default,float? WidthFactor=null,float? HeightFactor=null)
{
    public UiAlignOptions Validate(){Alignment.Validate();new UiSizeFactors(WidthFactor,HeightFactor).Validate();return this;}
}
public readonly record struct UiBoxSize(float? Width=null,float? Height=null)
{
    public UiBoxSize Validate()
    {
        if(Width is {} w&&(float.IsNaN(w)||w<0)||Height is {} h&&(float.IsNaN(h)||h<0))
            throw new ArgumentOutOfRangeException(nameof(UiBoxSize));
        return this;
    }
}

internal static class UiSingleChildLayoutGeometry
{
    internal static UiVector2 Measure(UiControlContext context,UiWidget? child,UiBoxConstraints constraints)
        =>child is null?constraints.Constrain(default):context.MeasureConstrained(child,constraints);
    internal static UiContainerLayoutResult AtOrigin(UiVector2 size)
        =>new(size,new(0,0,size.X,size.Y),new(0,0,size.X,size.Y));
    internal static UiContainerLayoutResult Align(UiControlContext context,UiWidget? child,UiAlignment alignment,UiSizeFactors factors,UiBoxConstraints constraints)
    {
        var desired=Measure(context,child,UiBoxConstraints.Loose(new(constraints.MaxWidth,constraints.MaxHeight)));
        static float Extent(float childSize,float minimum,float maximum,float? factor)
        {
            var desired=factor.HasValue?(double)childSize*factor.Value:float.IsFinite(maximum)?maximum:childSize;
            var result=Math.Clamp(desired,minimum,maximum);
            if(result>float.MaxValue)throw new InvalidOperationException("Alignment size exceeds the supported coordinate range.");
            return (float)result;
        }
        var size=new UiVector2(Extent(desired.X,constraints.MinWidth,constraints.MaxWidth,factors.WidthFactor),Extent(desired.Y,constraints.MinHeight,constraints.MaxHeight,factors.HeightFactor));
        return new(size,new((size.X-desired.X)*(alignment.X+1)*.5f,(size.Y-desired.Y)*(alignment.Y+1)*.5f,desired.X,desired.Y),new(0,0,size.X,size.Y));
    }
}

public readonly struct UiPaddingLayout:IUiSingleChildLayout<UiEdgeInsets>
{
    public static bool CanRetainMeasurement=>true;
    public static void Validate(UiEdgeInsets options)=>new UiContainerOptions(Padding:options).Validate();
    public static UiContainerLayoutResult Measure(UiControlContext context,UiWidget? child,UiEdgeInsets options,UiBoxConstraints constraints)
    {
        var desired=UiSingleChildLayoutGeometry.Measure(context,child,constraints.Deflate(options));
        var size=constraints.Constrain(new(desired.X+options.Left+options.Right,desired.Y+options.Top+options.Bottom));
        return new(size,new(options.Left,options.Top,desired.X,desired.Y),new(0,0,size.X,size.Y));
    }
}
public readonly struct UiAlignLayout:IUiSingleChildLayout<UiAlignOptions>
{
    public static bool CanRetainMeasurement=>true;
    public static void Validate(UiAlignOptions options)=>options.Validate();
    public static UiContainerLayoutResult Measure(UiControlContext context,UiWidget? child,UiAlignOptions options,UiBoxConstraints constraints)
        =>UiSingleChildLayoutGeometry.Align(context,child,options.Alignment,new(options.WidthFactor,options.HeightFactor),constraints);
}
public readonly struct UiCenterLayout:IUiSingleChildLayout<UiSizeFactors>
{
    public static bool CanRetainMeasurement=>true;
    public static void Validate(UiSizeFactors options)=>options.Validate();
    public static UiContainerLayoutResult Measure(UiControlContext context,UiWidget? child,UiSizeFactors options,UiBoxConstraints constraints)
        =>UiSingleChildLayoutGeometry.Align(context,child,UiAlignment.Center,options,constraints);
}
public readonly struct UiConstrainedBoxLayout:IUiSingleChildLayout<UiBoxConstraints>
{
    public static bool CanRetainMeasurement=>true;
    public static void Validate(UiBoxConstraints options)=>options.Validate();
    public static UiContainerLayoutResult Measure(UiControlContext context,UiWidget? child,UiBoxConstraints options,UiBoxConstraints constraints)
        =>UiSingleChildLayoutGeometry.AtOrigin(UiSingleChildLayoutGeometry.Measure(context,child,constraints.Enforce(options)));
}
public readonly struct UiSizedBoxLayout:IUiSingleChildLayout<UiBoxSize>
{
    public static bool CanRetainMeasurement=>true;
    public static void Validate(UiBoxSize options)=>options.Validate();
    public static UiContainerLayoutResult Measure(UiControlContext context,UiWidget? child,UiBoxSize options,UiBoxConstraints constraints)
    {
        var childConstraints=constraints;
        if(options.Width is {} w){w=Math.Clamp(w,constraints.MinWidth,constraints.MaxWidth);childConstraints=childConstraints with{MinWidth=w,MaxWidth=w};}
        if(options.Height is {} h){h=Math.Clamp(h,constraints.MinHeight,constraints.MaxHeight);childConstraints=childConstraints with{MinHeight=h,MaxHeight=h};}
        childConstraints.Validate();
        return UiSingleChildLayoutGeometry.AtOrigin(UiSingleChildLayoutGeometry.Measure(context,child,childConstraints));
    }
}

[UiWidgetStyle(typeof(UiSingleChildBox<,,>),typeof(UiPaddingLayout),typeof(UiEdgeInsets),typeof(UiPlainContainerPresentation))]
[UiComponent("Padding",UiComponentExposure.Infrastructure)]
public partial class UiPadding;
[UiWidgetStyle(typeof(UiSingleChildBox<,,>),typeof(UiAlignLayout),typeof(UiAlignOptions),typeof(UiPlainContainerPresentation))]
[UiComponent("Align",UiComponentExposure.Infrastructure)]
public partial class UiAlign;
[UiWidgetStyle(typeof(UiSingleChildBox<,,>),typeof(UiCenterLayout),typeof(UiSizeFactors),typeof(UiPlainContainerPresentation))]
[UiComponent("Center",UiComponentExposure.Infrastructure)]
public partial class UiCenter;
[UiWidgetStyle(typeof(UiSingleChildBox<,,>),typeof(UiSizedBoxLayout),typeof(UiBoxSize),typeof(UiPlainContainerPresentation))]
[UiComponent("SizedBox",UiComponentExposure.Infrastructure)]
public partial class UiSizedBox;
[UiWidgetStyle(typeof(UiSingleChildBox<,,>),typeof(UiConstrainedBoxLayout),typeof(UiBoxConstraints),typeof(UiPlainContainerPresentation))]
[UiComponent("ConstrainedBox",UiComponentExposure.Infrastructure)]
public partial class UiConstrainedBox;

public static partial class Dux
{
    public static UiPadding Padding(UiWidget? child,UiEdgeInsets padding)=>new(child,padding);
    public static UiAlign Align(UiWidget? child,UiAlignment alignment=default,float? widthFactor=null,float? heightFactor=null)=>new(child,new(alignment,widthFactor,heightFactor));
    public static UiCenter Center(UiWidget? child,float? widthFactor=null,float? heightFactor=null)=>new(child,new(widthFactor,heightFactor));
    public static UiSizedBox SizedBox(UiWidget? child=null,float? width=null,float? height=null)=>new(child,new(width,height));
    public static UiSizedBox Expand(UiWidget? child=null)=>new(child,new(float.PositiveInfinity,float.PositiveInfinity));
    public static UiConstrainedBox ConstrainedBox(UiWidget? child,UiBoxConstraints constraints)=>new(child,constraints);
}
