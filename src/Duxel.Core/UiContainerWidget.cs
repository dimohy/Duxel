namespace Duxel.Core;

public readonly record struct UiBoxConstraints(float MinWidth,float MaxWidth,float MinHeight,float MaxHeight)
{
    public UiBoxConstraints():this(0,float.PositiveInfinity,0,float.PositiveInfinity){}
    public static UiBoxConstraints Loose(UiVector2 maximum)=>new(0,maximum.X,0,maximum.Y);
    public static UiBoxConstraints Tight(UiVector2 size)=>new(size.X,size.X,size.Y,size.Y);
    public UiBoxConstraints Validate()
    {
        if(!float.IsFinite(MinWidth)||!float.IsFinite(MinHeight)||MinWidth<0||MinHeight<0
            ||float.IsNaN(MaxWidth)||float.IsNaN(MaxHeight)||MaxWidth<MinWidth||MaxHeight<MinHeight)
            throw new ArgumentOutOfRangeException(nameof(UiBoxConstraints));
        return this;
    }
    public UiVector2 Constrain(UiVector2 size)
    {
        Validate();
        if(!float.IsFinite(size.X)||!float.IsFinite(size.Y)||size.X<0||size.Y<0)throw new ArgumentOutOfRangeException(nameof(size));
        return new(Math.Clamp(size.X,MinWidth,MaxWidth),Math.Clamp(size.Y,MinHeight,MaxHeight));
    }
    public UiBoxConstraints Deflate(UiEdgeInsets padding)
    {
        var width=padding.Left+padding.Right;var height=padding.Top+padding.Bottom;
        return new(MathF.Max(0,MinWidth-width),MathF.Max(0,MaxWidth-width),MathF.Max(0,MinHeight-height),MathF.Max(0,MaxHeight-height));
    }
    public UiBoxConstraints Enforce(UiBoxConstraints child)=>new(
        Math.Clamp(child.MinWidth,MinWidth,MaxWidth),Math.Clamp(child.MaxWidth,MinWidth,MaxWidth),
        Math.Clamp(child.MinHeight,MinHeight,MaxHeight),Math.Clamp(child.MaxHeight,MinHeight,MaxHeight));
}

public readonly record struct UiAlignment(float X,float Y)
{
    public static UiAlignment TopLeft=>new(-1,-1);
    public static UiAlignment Center=>default;
    public static UiAlignment BottomRight=>new(1,1);
    public UiAlignment Validate()
    {if(!float.IsFinite(X)||!float.IsFinite(Y)||X< -1||X>1||Y< -1||Y>1)throw new ArgumentOutOfRangeException(nameof(UiAlignment));return this;}
}

public readonly record struct UiContainerOptions(UiEdgeInsets Padding=default,UiEdgeInsets Margin=default,
    UiAlignment? Alignment=null,float? Width=null,float? Height=null,UiBoxConstraints? Constraints=null)
{
    public UiContainerOptions Validate()
    {
        static void Edge(UiEdgeInsets edge)
        {
            if(!float.IsFinite(edge.Left)||!float.IsFinite(edge.Top)||!float.IsFinite(edge.Right)||!float.IsFinite(edge.Bottom)
                ||edge.Left<0||edge.Top<0||edge.Right<0||edge.Bottom<0||!float.IsFinite(edge.Left+edge.Right)||!float.IsFinite(edge.Top+edge.Bottom))
                throw new ArgumentOutOfRangeException(nameof(edge));
        }
        Edge(Padding);Edge(Margin);Alignment?.Validate();Constraints?.Validate();
        if(Width is {} w&&(!float.IsFinite(w)||w<0)||Height is {} h&&(!float.IsFinite(h)||h<0))throw new ArgumentOutOfRangeException(nameof(UiContainerOptions));
        return this;
    }
}

public readonly record struct UiContainerLayoutResult(UiVector2 Size,UiRect Child,UiRect Surface)
{
    public UiContainerLayoutResult Validate()
    {
        static bool Rect(UiRect r)=>float.IsFinite(r.X)&&float.IsFinite(r.Y)&&float.IsFinite(r.Width)&&float.IsFinite(r.Height)&&r.Width>=0&&r.Height>=0;
        if(!float.IsFinite(Size.X)||!float.IsFinite(Size.Y)||Size.X<0||Size.Y<0||!Rect(Child)||!Rect(Surface))throw new InvalidOperationException("Container layout returned invalid geometry.");
        return this;
    }
}
public interface IUiContainerLayout
{
    static virtual bool CanRetainMeasurement=>false;
    static abstract UiContainerLayoutResult Measure(UiControlContext context,UiWidget child,UiContainerOptions options,UiBoxConstraints constraints);
}
public interface IUiContainerPresentation
{
    static abstract void Paint(UiControlContext context,UiRect bounds);
}
public readonly struct UiPlainContainerPresentation:IUiContainerPresentation
{public static void Paint(UiControlContext context,UiRect bounds){} }

public readonly struct UiBoxContainerLayout:IUiContainerLayout
{
    public static bool CanRetainMeasurement=>true;
    public static UiContainerLayoutResult Measure(UiControlContext context,UiWidget child,UiContainerOptions options,UiBoxConstraints constraints)
    {
        options.Validate();constraints.Validate();
        var box=constraints.Deflate(options.Margin);
        if(options.Constraints is {} extra)box=box.Enforce(extra);
        if(options.Width is {} width){var w=Math.Clamp(width,box.MinWidth,box.MaxWidth);box=box with{MinWidth=w,MaxWidth=w};}
        if(options.Height is {} height){var h=Math.Clamp(height,box.MinHeight,box.MaxHeight);box=box with{MinHeight=h,MaxHeight=h};}
        var inside=box.Deflate(options.Padding);
        var desired=context.MeasureConstrained(child,UiBoxConstraints.Loose(new(inside.MaxWidth,inside.MaxHeight)));
        var target=new UiVector2(desired.X+options.Padding.Left+options.Padding.Right,desired.Y+options.Padding.Top+options.Padding.Bottom);
        if(options.Alignment.HasValue)
            target=new(float.IsFinite(box.MaxWidth)?box.MaxWidth:target.X,float.IsFinite(box.MaxHeight)?box.MaxHeight:target.Y);
        var size=box.Constrain(target);var available=new UiVector2(MathF.Max(0,size.X-options.Padding.Left-options.Padding.Right),MathF.Max(0,size.Y-options.Padding.Top-options.Padding.Bottom));
        var alignment=options.Alignment??UiAlignment.TopLeft;
        var childRect=new UiRect(options.Margin.Left+options.Padding.Left+(available.X-desired.X)*(alignment.X+1)*.5f,
            options.Margin.Top+options.Padding.Top+(available.Y-desired.Y)*(alignment.Y+1)*.5f,desired.X,desired.Y);
        var outer=constraints.Constrain(new(size.X+options.Margin.Left+options.Margin.Right,size.Y+options.Margin.Top+options.Margin.Bottom));
        return new(outer,childRect,new(options.Margin.Left,options.Margin.Top,size.X,size.Y));
    }
}

/// <summary>Owns one child and statically selects independent layout and surface implementations.</summary>
public class UiContainerBox<TLayout,TPresentation>:UiSingleChildBox<UiContainerLayoutAdapter<TLayout>,UiContainerOptions,TPresentation>
    where TLayout:IUiContainerLayout where TPresentation:IUiContainerPresentation
{
    public UiContainerBox(UiWidget child,UiContainerOptions options=default,UiWidgetIdentity identity=UiWidgetIdentity.Instance)
        :base(child??throw new ArgumentNullException(nameof(child)),options,identity){}
    public new UiWidget Child=>base.Child!;
}

public readonly struct UiContainerLayoutAdapter<TLayout>:IUiSingleChildLayout<UiContainerOptions> where TLayout:IUiContainerLayout
{
    public static bool CanRetainMeasurement=>TLayout.CanRetainMeasurement;
    public static void Validate(UiContainerOptions options)=>options.Validate();
    public static UiContainerLayoutResult Measure(UiControlContext context,UiWidget? child,UiContainerOptions options,UiBoxConstraints constraints)
        =>TLayout.Measure(context,child??throw new InvalidOperationException("Container requires a child."),options,constraints);
}

[UiWidgetStyle(typeof(UiContainerBox<,>),typeof(UiBoxContainerLayout),typeof(UiPlainContainerPresentation))]
[UiComponent("Container",UiComponentExposure.Infrastructure)]
public partial class UiContainer;

public static partial class Dux
{
    public static UiContainer Container(UiWidget child,UiContainerOptions options=default)=>new(child,options);
}

public sealed partial class UiImmediateContext
{
    internal UiBoxConstraints CurrentLayoutConstraints=>IsMeasuring
        ?_measureConstraints??UiBoxConstraints.Loose(GetContentRegionAvail())
        :_inputSlotBounds is {} slot&&_inputSlotBoundsTier==_popupTierDepth
            ?UiBoxConstraints.Tight(new(slot.Width,slot.Height)):UiBoxConstraints.Loose(GetContentRegionAvail());
    internal void RenderContainer<TPresentation>(UiWidget? child,UiContainerLayoutResult layout)
        where TPresentation:IUiContainerPresentation
    {
        EnsureNotMeasuring();
        var context = new UiControlContext(this);
        var origin = AdvanceCursor(layout.Size);
        var root = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        var surface = layout.Surface with { X = origin.X + layout.Surface.X, Y = origin.Y + layout.Surface.Y };
        var bounds = layout.Child with { X = origin.X + layout.Child.X, Y = origin.Y + layout.Child.Y };
        using var clip = BeginControlClip(root);
        TPresentation.Paint(context, surface);
        if (child is not null)
        {
            RenderAllocatedChild(_builder, child, bounds, surface, constrainLayout : true);
        }
        _lastItemPos = origin;
        _lastItemSize = layout.Size;
        _lastItemId = null;
        _lastItemEdited = false;
    }
    internal UiVector2 AllocateLayoutBox(UiVector2 size)=>AdvanceCursor(size);
    internal void RenderLayoutChild(UiWidget child,UiRect bounds,UiRect root)=>RenderAllocatedChild(_builder,child,bounds,root,constrainLayout:true);
    internal void FinishLayoutBox(UiVector2 origin,UiVector2 size)
    {_lastItemPos=origin;_lastItemSize=size;_lastItemId=null;_lastItemEdited=false;}
}
