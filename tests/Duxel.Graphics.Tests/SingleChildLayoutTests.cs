using Duxel.Core;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class SingleChildLayoutTests
{
    internal static void MeasureMemory(string output)
    {
        const int count=10000;
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var input=new UiInputState(default,false,false,false,false,false,false,0,0,[],[],default,default);
        void Frame(Action<UiImmediateContext> draw)
        {context.NewFrame(new(1f/60,new(200,100),new(1,1)),input,new(0,0,200,100),UiTextSettings.Default);context.Render(new Screen(draw));using var data=context.GetDrawData();}
        for(var i=0;i<96;i++)
        {
            using var warm=new UiPadding(new Probe(new(1,1),UiWidgetIdentity.Parent),new(1),UiWidgetIdentity.Parent);
            Frame(ui=>{warm.MeasureConstrained(ui,new());warm.MeasureConstrained(ui,new());});
        }
        var roots=new UiPadding[count];var children=new Probe[count];
        var start=GC.GetAllocatedBytesForCurrentThread();
        for(var i=0;i<count;i++)roots[i]=new(children[i]=new(new(1,1),UiWidgetIdentity.Parent),new(1),UiWidgetIdentity.Parent);
        var construction=GC.GetAllocatedBytesForCurrentThread()-start;
        long first=0,steady=0,invalidated=0;
        Frame(ui=>{var before=GC.GetAllocatedBytesForCurrentThread();foreach(var root in roots)root.MeasureConstrained(ui,new());first=GC.GetAllocatedBytesForCurrentThread()-before;});
        Frame(ui=>{var before=GC.GetAllocatedBytesForCurrentThread();foreach(var root in roots)root.MeasureConstrained(ui,new());steady=GC.GetAllocatedBytesForCurrentThread()-before;});
        foreach(var child in children)child.Change(new(2,2));
        Frame(ui=>{var before=GC.GetAllocatedBytesForCurrentThread();foreach(var root in roots)root.MeasureConstrained(ui,new());invalidated=GC.GetAllocatedBytesForCurrentThread()-before;});
        foreach(var root in roots)root.Dispose();
        var disposals=children.Sum(child=>child.Disposals);
        var result=new SingleChildMemoryResult(RuntimeInformation.FrameworkDescription,count,construction,first,steady,invalidated,disposals,
            "Current-thread generated Padding+child construction and separate first retained-layout acquisition,steady measurement,and invalidation reuse;arrays/context/frame/disposal excluded;no forced GC;not application Private/FPS or a before-after comparison.");
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(result,SingleChildMemoryJson.Default.SingleChildMemoryResult));
        if(steady!=0||invalidated!=0||disposals!=count)throw new InvalidOperationException($"Single-child storage contract failed: steady={steady};invalidated={invalidated};disposals={disposals}.");
    }
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        void Frame(Action<UiImmediateContext> draw,UiInputState? input=null)
        {
            context.NewFrame(new(1f/60,new(200,100),new(1,1)),input??idle,new(0,0,200,100),UiTextSettings.Default);
            context.Render(new Screen(ui=>{ui.EnableRootViewportContentLayout(contentPadding:0);ui.SetCursorScreenPos(default);draw(ui);}));
            using var data=context.GetDrawData();
        }
        var probe=new Probe(new(20,10));using var padding=Dux.Padding(probe,new(3,5,7,11));
        Frame(ui=>
        {
            check(padding.MeasureConstrained(ui,UiBoxConstraints.Loose(new(100,60)))==new UiVector2(30,26),"Padding adds independent horizontal and vertical insets to the child's measured size");
            check(probe.Renders==0,"single-child wrapper measurement runs no rendering or input");
            check(padding.MeasureConstrained(ui,UiBoxConstraints.Tight(new(100,60)))==new UiVector2(100,60)
                &&probe.Constraints==UiBoxConstraints.Tight(new(90,44)),"Padding passes deflated tight parent constraints down to its child");
        });
        using var paddedSize=Dux.SizedBox(padding,100,60);
        Frame(paddedSize.Render);
        check(probe.Position==new UiVector2(3,5)&&probe.Available==new UiVector2(90,44),"nested SizedBox and Padding render the child at the constrained inset allocation");

        var aligned=new Probe(new(20,10));using var align=Dux.Align(aligned,UiAlignment.BottomRight);
        Frame(ui=>{check(align.MeasureConstrained(ui,UiBoxConstraints.Loose(new(100,60)))==new UiVector2(100,60),"Align fills finite axes when no size factor is specified");});
        using var alignSize=Dux.SizedBox(align,100,60);Frame(alignSize.Render);
        check(aligned.Position==new UiVector2(80,50)&&aligned.Available==new UiVector2(20,10),"Align loosens child constraints and positions the natural child at bottom right");
        using var center=Dux.Center(new Probe(new(20,10)),2,3);
        Frame(ui=>check(center.MeasureConstrained(ui,new())==new UiVector2(40,30),"Center size factors multiply child dimensions under unbounded axes"));
        using var naturalAlign=Dux.Align(new Probe(new(20,10)));
        Frame(ui=>check(naturalAlign.MeasureConstrained(ui,new())==new UiVector2(20,10),"Align without factors shrink-wraps unbounded axes"));
        using var mixed=Dux.Align(new Probe(new(20,10)),widthFactor:2);
        Frame(ui=>check(mixed.MeasureConstrained(ui,UiBoxConstraints.Loose(new(100,60)))==new UiVector2(40,60),"Align factors and bounded expansion are independent per axis"));
        using var zero=Dux.Center(new Probe(new(20,10)),0,0);
        Frame(ui=>check(zero.MeasureConstrained(ui,new())==default,"zero alignment factors produce a zero-sized clipped parent without changing child measurement"));
        using var huge=Dux.Center(new Probe(new(20,10)),float.MaxValue,float.MaxValue);
        Frame(ui=>check(huge.MeasureConstrained(ui,UiBoxConstraints.Loose(new(100,60)))==new UiVector2(100,60),"large finite alignment factors clamp to bounded parents without intermediate float overflow"));

        var sizedChild=new Probe(new(20,10));using var sized=Dux.SizedBox(sizedChild,100,40);
        Frame(ui=>
        {
            check(sized.MeasureConstrained(ui,UiBoxConstraints.Loose(new(200,100)))==new UiVector2(100,40)
                &&sizedChild.Constraints==UiBoxConstraints.Tight(new(100,40)),"SizedBox passes its requested tight dimensions to the child");
            check(sized.MeasureConstrained(ui,UiBoxConstraints.Tight(new(60,50)))==new UiVector2(60,50)
                &&sizedChild.Constraints==UiBoxConstraints.Tight(new(60,50)),"parent tight dimensions override conflicting SizedBox dimensions");
        });
        using var oneAxis=Dux.SizedBox(new Probe(new(20,10)),width:40);
        using var empty=Dux.SizedBox(width:30,height:20);using var expand=Dux.Expand();
        Frame(ui=>
        {
            check(oneAxis.MeasureConstrained(ui,new())==new UiVector2(40,10),"unspecified SizedBox axes follow the child while explicit axes remain tight");
            check(empty.MeasureConstrained(ui,new())==new UiVector2(30,20)&&empty.Child is null,"SizedBox supports empty layout space without allocating an artificial child widget");
            check(expand.MeasureConstrained(ui,UiBoxConstraints.Loose(new(200,100)))==new UiVector2(200,100),"SizedBox Expand fills bounded parent dimensions");
            var rejected=false;try{expand.MeasureConstrained(ui,new());}catch(ArgumentOutOfRangeException){rejected=true;}
            check(rejected,"expanding an unbounded axis fails explicitly instead of producing invalid geometry");
        });
        var constrainedChild=new Probe(new(20,10));using var constrained=Dux.ConstrainedBox(constrainedChild,new(50,80,30,60));
        Frame(ui=>
        {
            check(constrained.MeasureConstrained(ui,UiBoxConstraints.Loose(new(200,100)))==new UiVector2(50,30),"ConstrainedBox enforces additional minimum extents on the child");
            check(constrained.MeasureConstrained(ui,UiBoxConstraints.Tight(new(20,10)))==new UiVector2(20,10)
                &&constrainedChild.Constraints==UiBoxConstraints.Tight(new(20,10)),"ConstrainedBox preserves the authority of tighter parent constraints");
        });
        var unowned=new Probe(new(1,1));var invalid=false;
        try{_=Dux.Padding(unowned,new(-1));}catch(ArgumentOutOfRangeException){invalid=true;}
        using(var accepted=Dux.Center(unowned))check(invalid,"invalid wrapper options fail before adopting a child");
        var unchanged=center.Options;invalid=false;
        try{center.Options=new(float.NaN,1);}catch(ArgumentOutOfRangeException){invalid=true;}
        check(invalid&&center.Options==unchanged,"invalid live factor changes preserve valid prior options");
        var sharing=false;try{_=Dux.Center(probe);}catch(InvalidOperationException){sharing=true;}
        check(sharing,"single-child wrappers reject ownership sharing with an existing container");
        using var user=new GeneratedUserSingleChild(new Probe(new(20,10)),new(UiAlignment.BottomRight));
        Frame(ui=>check(user.MeasureConstrained(ui,UiBoxConstraints.Loose(new(100,60)))==new UiVector2(100,60),"user-attributed wrappers consume the same generated closed single-child implementation"));
        check(new[]{"Padding","Align","Center","SizedBox","ConstrainedBox"}.All(name=>!Fluent.ComponentNames.Contains(name)),"single-child infrastructure does not expand the official Fluent visual catalog");

        var retainedChild=new Probe(new(20,10));using var retained=Dux.Padding(retainedChild,new(2));
        Frame(ui=>{retained.MeasureConstrained(ui,new());retained.MeasureConstrained(ui,new());});var measures=retainedChild.Measures;
        Frame(ui=>retained.MeasureConstrained(ui,new()));
        check(retainedChild.Measures==measures,"unchanged single-child wrappers retain eligible child measurement");
        retainedChild.Change(new(30,15));
        Frame(ui=>check(retained.MeasureConstrained(ui,new())==new UiVector2(34,19),"child invalidation propagates through the shared retained single-child layout"));
        var requests=0;context.SetRequestFrameCallback(()=>requests++);Frame(retained.Render);retained.Options=new(4);
        check(requests==1&&ReferenceEquals(retained.Child,retainedChild),"live wrapper options request one frame while preserving the owned child");
        Frame(ui=>check(retained.MeasureConstrained(ui,new())==new UiVector2(38,23),"changed wrapper options invalidate the previous measured layout"));
        using var dynamicLayout=new UiSingleChildBox<DynamicLayout,UiBoxSize,UiPlainContainerPresentation>(new Probe(new(1,1)),default);
        DynamicLayout.Measures=0;Frame(ui=>{dynamicLayout.MeasureConstrained(ui,new());dynamicLayout.MeasureConstrained(ui,new());});
        check(DynamicLayout.Measures==2,"custom single-child algorithms remain uncached unless they explicitly opt in");

        var customChild = new Probe(new(10, 10));
        using var customSize = new UiSingleChildBox<UnconstrainedLayout, UiBoxSize, UiPlainContainerPresentation>(
            customChild, new(201, 100));
        Frame(ui =>
        {
            var constraints = new UiBoxConstraints(50, 200, 40, 100);
            bool RejectsSize(Action use)
            {
                try
                {
                    use();
                    return false;
                }
                catch (InvalidOperationException error) when (error.Message.Contains("outside its parent constraints", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            check(RejectsSize(() => customSize.MeasureConstrained(ui, constraints)),
                "custom single-child size above the parent's maximum fails before clamping");
            customSize.Options = new(50, 39);
            check(RejectsSize(() => customSize.MeasureConstrained(ui, constraints)),
                "custom single-child size below the parent's minimum is rejected");
            customSize.Options = new(201, 100);
            var cursor = ui.GetCursorScreenPos();
            check(RejectsSize(() => customSize.Render(ui)) && customChild.Renders == 0 && ui.GetCursorScreenPos() == cursor,
                "invalid custom geometry is rejected before placing or painting its child");
            customSize.Options = new(50, 40);
            check(customSize.MeasureConstrained(ui, constraints) == new UiVector2(50, 40),
                "a corrected custom layout can measure after another result was rejected");
            customSize.Render(ui);
            check(ui.GetItemRectSize() == new UiVector2(50, 40) && customChild.Renders == 1,
                "the valid custom layout gives the same size to measurement and rendering");
        });

        var clicks=0;var button=new ObservedButton(()=>clicks++){Size=new(60,32)};
        var buttonPadding=Dux.Padding(Dux.Align(button,UiAlignment.TopLeft),new(8));using var buttonBox=Dux.SizedBox(buttonPadding,100,60);
        Frame(buttonBox.Render);
        check(button.Bounds==new UiRect(8,8,60,32),"nested generated wrappers preserve Fluent button paint and hit bounds");
        var point=new UiVector2(20,20);
        Frame(buttonBox.Render,idle with{MousePosition=point,LeftMouseDown=true,LeftMousePressed=true});
        Frame(buttonBox.Render,idle with{MousePosition=point,LeftMouseReleased=true});
        check(clicks==1,"nested single-child wrappers preserve exactly one real Fluent pointer callback");
        buttonPadding.Options=new(12);Frame(buttonBox.Render);
        check(button.Bounds==new UiRect(12,12,60,32)&&ReferenceEquals(buttonPadding.Child!.GetType(),typeof(UiAlign)),"live wrapper changes update actual button geometry without replacing the child tree");
        buttonBox.Dispose();check(button.Disposals==1,"nested wrapper disposal releases its actual Fluent descendant exactly once");
        var disposed=false;try{buttonPadding.Options=new(0);}catch(ObjectDisposedException){disposed=true;}
        check(disposed,"disposed wrappers reject subsequent mutation");

        const int count=10000;var many=new UiPadding[count];
        for(var i=0;i<count;i++)many[i]=new(new Probe(new(1,1),UiWidgetIdentity.Parent),new(1),UiWidgetIdentity.Parent);
        Frame(ui=>{foreach(var item in many)item.MeasureConstrained(ui,UiBoxConstraints.Loose(new(20,20)));});
        long acquired=0,steady=0;
        Frame(ui=>{var start=GC.GetAllocatedBytesForCurrentThread();foreach(var item in many)item.MeasureConstrained(ui,UiBoxConstraints.Loose(new(20,20)));acquired=GC.GetAllocatedBytesForCurrentThread()-start;});
        Frame(ui=>{var start=GC.GetAllocatedBytesForCurrentThread();foreach(var item in many)item.MeasureConstrained(ui,UiBoxConstraints.Loose(new(20,20)));steady=GC.GetAllocatedBytesForCurrentThread()-start;});
        check(acquired==0&&steady==0,"10000 retained Padding measurements allocate zero current-thread bytes after initial payload acquisition");
        foreach(var item in many)item.Dispose();
        paddedSize.Dispose();check(probe.Disposals==1,"SizedBox and Padding ownership dispose a shared subtree only once");
    }
    private sealed class Probe(UiVector2 desired,UiWidgetIdentity identity=UiWidgetIdentity.Instance):UiControlWidget(identity)
    {
        internal int Renders,Measures,Disposals;internal UiBoxConstraints Constraints;internal UiVector2 Position,Available;
        internal void Change(UiVector2 value){desired=value;RequestRebuild();}
        protected override bool CanRetainMeasurement=>true;
        protected override UiVector2 MeasureContent(UiControlContext context){Measures++;Constraints=context.LayoutConstraints;return desired;}
        protected override void RenderControl(UiImmediateContext ui){Renders++;Position=ui.GetCursorScreenPos();Available=ui.GetContentRegionAvail();ui.Dummy(new UiControlContext(ui).ConstrainToLayout(desired));}
        protected override void OnDispose()=>Disposals++;
    }
    private sealed class ObservedButton(Action activated):UiFluentButton(string.Empty,activated)
    {
        internal UiRect Bounds;internal int Disposals;
        protected override void RenderControl(UiImmediateContext ui){base.RenderControl(ui);var p=ui.GetItemRectMin();var s=ui.GetItemRectSize();Bounds=new(p.X,p.Y,s.X,s.Y);}
        protected override void OnDispose()=>Disposals++;
    }
    private readonly struct DynamicLayout:IUiSingleChildLayout<UiBoxSize>
    {
        internal static int Measures;
        public static void Validate(UiBoxSize options)=>options.Validate();
        public static UiContainerLayoutResult Measure(UiControlContext context,UiWidget? child,UiBoxSize options,UiBoxConstraints constraints)
        {Measures++;return new(new(1,1),new(0,0,1,1),new(0,0,1,1));}
    }
    private readonly struct UnconstrainedLayout : IUiSingleChildLayout<UiBoxSize>
    {
        public static void Validate(UiBoxSize options) => options.Validate();

        public static UiContainerLayoutResult Measure(UiControlContext context, UiWidget? child,
            UiBoxSize options, UiBoxConstraints constraints)
        {
            var size = new UiVector2(options.Width.GetValueOrDefault(), options.Height.GetValueOrDefault());
            var full = new UiRect(0, 0, size.X, size.Y);
            return new(size, full, full);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen{public override void Render(UiImmediateContext ui)=>draw(ui);}
}
[UiWidgetStyle(typeof(UiSingleChildBox<,,>),typeof(UiAlignLayout),typeof(UiAlignOptions),typeof(UiPlainContainerPresentation))]
internal partial class GeneratedUserSingleChild;

internal sealed record SingleChildMemoryResult(string Runtime,int Widgets,long ConstructionBytes,long FirstLayoutBytes,long SteadyLayoutBytes,long InvalidatedLayoutBytes,int ChildDisposals,string Scope);
[JsonSerializable(typeof(SingleChildMemoryResult))]
[JsonSourceGenerationOptions(WriteIndented=true)]
internal partial class SingleChildMemoryJson:JsonSerializerContext;
