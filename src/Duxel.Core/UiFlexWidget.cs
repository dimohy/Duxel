namespace Duxel.Core;

public enum UiMainAxisSize {Max,Min}
public enum UiMainAxisAlignment {Start,Center,End,SpaceBetween,SpaceAround,SpaceEvenly}
public enum UiCrossAxisAlignment {Start,Center,End,Stretch}
public readonly record struct UiFlexOptions(float Spacing=0,UiMainAxisSize MainAxisSize=UiMainAxisSize.Max,
    UiMainAxisAlignment MainAxisAlignment=UiMainAxisAlignment.Start,UiCrossAxisAlignment CrossAxisAlignment=UiCrossAxisAlignment.Start)
{
    public UiFlexOptions Validate()
    {if(!float.IsFinite(Spacing)||Spacing<0||(uint)MainAxisSize>1||(uint)MainAxisAlignment>5||(uint)CrossAxisAlignment>3)throw new ArgumentOutOfRangeException(nameof(UiFlexOptions));return this;}
}
public interface IUiLayoutAxis {static abstract bool Horizontal {get;} static virtual bool CanRetainMeasurement=>false;}
public readonly struct UiHorizontalAxis:IUiLayoutAxis {public static bool Horizontal=>true;public static bool CanRetainMeasurement=>true;}
public readonly struct UiVerticalAxis:IUiLayoutAxis {public static bool Horizontal=>false;public static bool CanRetainMeasurement=>true;}

/// <summary>Measures owned children and assigns positions using a statically selected layout axis.</summary>
public class UiFlexWidget<TAxis>:UiMultiChildWidget where TAxis:IUiLayoutAxis
{
    private UiVector2[] _sizes;
    private UiFlexParentData[]? _flexData;
    private UiFlexOptions _options;
    private UiRetainedLayout<UiVector2>? _retained;
    protected override bool CanRetainArrangement=>TAxis.CanRetainMeasurement;
    protected override bool CanRetainMeasurement=>_retained is {IsValid:true};
    internal override void InvalidateRetainedMeasurement()=>_retained?.Invalidate();
    public UiFlexWidget(UiWidget[] children,UiFlexOptions options=default,UiWidgetIdentity identity=UiWidgetIdentity.Instance):base(ValidateOptions(children,options),identity)
    {
        _options=options;_sizes=new UiVector2[children.Length];
    }
    private static UiWidget[] ValidateOptions(UiWidget[] children,UiFlexOptions options){options.Validate();return children;}
    public UiFlexOptions Options {get=>_options;set{ThrowIfDisposed();value.Validate();if(_options==value)return;ThrowIfBuilding();_options=value;RequestRebuild();}}
    private UiVector2 MeasureLayout(UiControlContext context, bool render = false)
    {
        var allowed = CanRetainArrangement && context.CanRetainLayoutMeasurement;
        if (_retained is { } previous && previous.TryGet(context, render, out var size, allowed))
            return size;
        _retained?.Clear();
        var retain = allowed && ChildrenRetainMeasurement;
        var cache = _retained ??= new();
        var attempt = cache.Begin(context);
        var result = MeasureUncachedLayout(context);
        cache.Commit(context, attempt, result, result, retain);
        return result;
    }
    private UiVector2 MeasureUncachedLayout(UiControlContext context)
    {
        var constraints=context.LayoutConstraints.Validate();var horizontal=TAxis.Horizontal;
        var max=horizontal?constraints.MaxWidth:constraints.MaxHeight;
        var crossMax=horizontal?constraints.MaxHeight:constraints.MaxWidth;
        var stretch=_options.CrossAxisAlignment==UiCrossAxisAlignment.Stretch;
        if(stretch&&!float.IsFinite(crossMax))throw new InvalidOperationException("Flex cross-axis stretch requires a bounded cross axis.");
        var crossMin=stretch?crossMax:0;
        UiBoxConstraints ChildConstraints(float minimum,float maximum)=>horizontal
            ?new(minimum,maximum,crossMin,crossMax):new(crossMin,crossMax,minimum,maximum);
        var bounded=float.IsFinite(max);long totalFlex=0;
        var main=(double)_options.Spacing*Math.Max(0,Children.Length-1);var cross=0f;
        if(main>float.MaxValue)throw new InvalidOperationException("Flex spacing exceeds finite layout capacity.");
        for(var i=0;i<Children.Length;i++)
        {
            var data=context.ResolveFlexParentData(Children[i]);
            if(data.HasValue)_flexData??=new UiFlexParentData[Children.Length];
            if(_flexData is not null)_flexData[i]=data??default;
            if(data is {Flex:>0} flex)
            {
                if(!bounded&&(_options.MainAxisSize==UiMainAxisSize.Max||flex.Tight))
                    throw new InvalidOperationException("A flex child cannot fill an unbounded main axis. Use bounded constraints or loose Flexible with MainAxisSize.Min.");
                if(bounded){totalFlex+=flex.Flex;continue;}
            }
            var size=context.MeasureFlexChild(Children[i],data??default,ChildConstraints(0,float.PositiveInfinity));_sizes[i]=size;
            main+=horizontal?size.X:size.Y;cross=MathF.Max(cross,horizontal?size.Y:size.X);
        }
        if (bounded && (float)main > max)
            throw new InvalidOperationException($"Non-flex children and spacing overflow the {(horizontal ? "horizontal" : "vertical")} main axis: children = {main}, maximum = {max}, constraints = {constraints}.");
        if(totalFlex>0)
        {
            var remaining=Math.Max(0,max-main);long distributedFlex=0;var boundary=0f;
            for(var i=0;i<Children.Length;i++)
            {
                var data=_flexData![i];if(data.Flex==0)continue;
                distributedFlex+=data.Flex;
                var next=(float)(remaining*distributedFlex/totalFlex);
                var allocation=MathF.Max(0,next-boundary);boundary=next;
                var size=context.MeasureFlexChild(Children[i],data,ChildConstraints(data.Tight?allocation:0,allocation));_sizes[i]=size;
                main+=horizontal?size.X:size.Y;cross=MathF.Max(cross,horizontal?size.Y:size.X);
            }
        }
        if(main>float.MaxValue)throw new InvalidOperationException("Flex children exceed finite layout capacity.");
        var desiredMain=_options.MainAxisSize==UiMainAxisSize.Max&&bounded?max:(float)main;
        var result=constraints.Constrain(horizontal?new(desiredMain,cross):new(cross,desiredMain));
        var available=horizontal?result.X:result.Y;
        if((float)main>available)throw new InvalidOperationException($"Flex children overflow the {(horizontal?"horizontal":"vertical")} constraint: children={main}, available={available}, constraints={constraints}.");
        return result;
    }
    protected override UiVector2 MeasureContent(UiControlContext context)=>MeasureLayout(context);
    private protected override void ReleaseOwnedResources(){_sizes=[];_flexData=null;_retained=null;base.ReleaseOwnedResources();}
    protected override void RenderControl(UiImmediateContext ui)
    {
        var context=new UiControlContext(ui);var size=MeasureLayout(context,render:true);var origin=ui.AllocateLayoutBox(size);
        var horizontal=TAxis.Horizontal;var total=_options.Spacing*Math.Max(0,Children.Length-1);
        foreach(var child in _sizes)total+=horizontal?child.X:child.Y;
        var free=MathF.Max(0,(horizontal?size.X:size.Y)-total);var start=0f;var gap=_options.Spacing;
        switch(_options.MainAxisAlignment)
        {
            case UiMainAxisAlignment.Center:start=free*.5f;break;
            case UiMainAxisAlignment.End:start=free;break;
            case UiMainAxisAlignment.SpaceBetween:if(Children.Length>1)gap+=free/(Children.Length-1);break;
            case UiMainAxisAlignment.SpaceAround:if(Children.Length>0){var extra=free/Children.Length;gap+=extra;start=extra*.5f;}break;
            case UiMainAxisAlignment.SpaceEvenly:var even=free/(Children.Length+1);gap+=even;start=even;break;
        }
        var root=new UiRect(origin.X,origin.Y,size.X,size.Y);
        for(var i=0;i<Children.Length;i++)
        {
            var child=_sizes[i];var crossFree=(horizontal?size.Y-child.Y:size.X-child.X);
            var cross=_options.CrossAxisAlignment switch{UiCrossAxisAlignment.Center=>crossFree*.5f,UiCrossAxisAlignment.End=>crossFree,_=>0};
            var bounds=new UiRect(origin.X+(horizontal?start:cross),origin.Y+(horizontal?cross:start),child.X,child.Y);
            ui.RenderFlexChild(Children[i],_flexData?[i]??default,bounds,root);start+=(horizontal?child.X:child.Y)+gap;
        }
        ui.FinishLayoutBox(origin,size);
    }
}

[UiWidgetStyle(typeof(UiFlexWidget<>),typeof(UiHorizontalAxis))]
[UiComponent("Row",UiComponentExposure.Infrastructure)]
public partial class UiRow;
[UiWidgetStyle(typeof(UiFlexWidget<>),typeof(UiVerticalAxis))]
[UiComponent("Column",UiComponentExposure.Infrastructure)]
public partial class UiColumn;

public static partial class Dux
{
    public static UiRow Row(UiFlexOptions options,params UiWidget[] children)=>new(children,options);
    public static UiColumn Column(UiFlexOptions options,params UiWidget[] children)=>new(children,options);
}
