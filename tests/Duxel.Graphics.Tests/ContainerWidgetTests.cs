using Duxel.Core;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class ContainerWidgetTests
{
    internal static void MeasureBuildContextStorage(string output)
    {
        const int count=10000;
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var idle=new UiInputState(default,false,false,false,false,false,false,0,0,[],[],default,default);
        void Frame(Action<UiImmediateContext> draw)
        {context.NewFrame(new(1f/60,new(200,100),new(1,1)),idle,new(0,0,200,100),UiTextSettings.Default);context.Render(new Screen(draw));using var data=context.GetDrawData();}
        for(var i=0;i<96;i++)using(var warm=new UiContainer(new Probe(new(1,1),UiWidgetIdentity.Parent),default,UiWidgetIdentity.Parent)){}
        for(var i=0;i<96;i++)Frame(ui=>{using var warm=new EmptyBuild();warm.Render(ui);});
        var children=new Probe[count];var owned=new UiContainer[count];
        var before=GC.GetAllocatedBytesForCurrentThread();
        for(var i=0;i<count;i++)owned[i]=new UiContainer(children[i]=new(new(1,1),UiWidgetIdentity.Parent),default,UiWidgetIdentity.Parent);
        var ownedBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        foreach(var item in owned)item.Dispose();
        if(children.Any(child=>child.Disposals!=1))throw new InvalidOperationException("Ownership probe did not dispose each child exactly once.");
        var views=Enumerable.Range(0,count).Select(_=>new EmptyBuild()).ToArray();
        long firstBytes=0,steadyBytes=0;
        Frame(ui=>{var start=GC.GetAllocatedBytesForCurrentThread();foreach(var view in views)view.Render(ui);firstBytes=GC.GetAllocatedBytesForCurrentThread()-start;});
        Frame(ui=>{var start=GC.GetAllocatedBytesForCurrentThread();foreach(var view in views)view.Render(ui);steadyBytes=GC.GetAllocatedBytesForCurrentThread()-start;});
        var builds=views.Sum(view=>view.Builds);foreach(var view in views)view.Dispose();
        if(builds!=count||steadyBytes!=0)throw new InvalidOperationException($"Build reuse contract failed: builds={builds};steady={steadyBytes}.");
        var result=new BuildContextMemoryResult(RuntimeInformation.FrameworkDescription,count,ownedBytes,firstBytes,steadyBytes,builds,
            "Current-thread allocation for actual generated Container ownership and first retained Build; no forced GC; excludes preallocated arrays and widget objects in first-Build phase; not Private or FPS.");
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(result,BuildContextMemoryJson.Default.BuildContextMemoryResult));
        Console.WriteLine($"Owned={ownedBytes}B;firstBuild={firstBytes}B;steady={steadyBytes}B;builds={builds}");
    }
    private sealed class EmptyBuild:UiWidget
    {
        internal int Builds;
        internal EmptyBuild():base(UiWidgetIdentity.Parent){}
        protected override IUiView Build(UiBuildContext context){Builds++;return EmptyView.Instance;}
    }
    private sealed class EmptyView:IUiView
    {internal static readonly EmptyView Instance=new();public void Render(UiImmediateContext ui){}}
    internal static void Run(Action<bool,string> check)
    {
        FlexContainerTests.Run(check);
        StackContainerTests.Run(check);
        FlowContainerTests.Run(check);
        LayoutRetentionContractTests.Run(check);
        SingleChildLayoutTests.Run(check);
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        void Frame(Action<UiImmediateContext> draw,UiInputState? input=null)
        {
            context.NewFrame(new(1f/60,new(200,100),new(1,1)),input??idle,new(0,0,200,100),UiTextSettings.Default);
            context.Render(new Screen(ui=>{ui.EnableRootViewportContentLayout(contentPadding:0);ui.SetCursorScreenPos(default);draw(ui);}));
            using var data=context.GetDrawData();
        }
        var probe=new Probe(new(20,10));
        using var box=Dux.Container(probe,new(Padding:new(10),Margin:new(5),Alignment:UiAlignment.Center,Width:100,Height:60));
        Frame(ui=>
        {
            check(box.MeasureConstrained(ui,UiBoxConstraints.Loose(new(200,100)))==new UiVector2(110,70),"Container measures padding and margin under parent constraints without rendering its child");
            check(probe.Renders==0,"container measurement does not run child input or rendering");
            box.Render(ui);
        });
        check(probe.Position==new UiVector2(45,30)&&probe.Available==new UiVector2(20,10),"Container assigns the measured child position and isolated local allocation");
        Frame(ui=>check(box.MeasureConstrained(ui,UiBoxConstraints.Tight(new(90,50)))==new UiVector2(90,50),"tight parent constraints override a larger requested container size"));
        var natural=new Probe(new(20,10));using var unbounded=new UiContainer(natural,new(Padding:new(2),Alignment:UiAlignment.Center));
        Frame(ui=>check(unbounded.MeasureConstrained(ui,new())==new UiVector2(24,14),"an aligned container shrink-wraps a naturally measurable child under unbounded maxima"));
        var invalid=false;try{new UiBoxConstraints(10,5,0,20).Validate();}catch(ArgumentOutOfRangeException){invalid=true;}
        check(invalid,"invalid minimum/maximum constraints are rejected explicitly");
        var sharing=false;try{_=new UiContainer(probe);}catch(InvalidOperationException){sharing=true;}
        check(sharing,"a child cannot be adopted by two containers");
        var a=new Probe(new(20,10));var b=new Probe(new(30,20));
        using var row=new UiRow([a,b],new(Spacing:5,MainAxisSize:UiMainAxisSize.Min,CrossAxisAlignment:UiCrossAxisAlignment.End));
        Frame(ui=>{check(row.MeasureConstrained(ui,UiBoxConstraints.Loose(new(200,100)))==new UiVector2(55,20),"generated Row measures main-axis spacing and cross-axis extent");row.Render(ui);});
        check(a.Position==new UiVector2(0,10)&&b.Position==new UiVector2(25,0),"Row aligns and places owned children on the horizontal axis");
        var c=new Probe(new(20,10));var d=new Probe(new(30,20));
        using var column=Dux.Column(new(Spacing:5,MainAxisSize:UiMainAxisSize.Min,CrossAxisAlignment:UiCrossAxisAlignment.Center),c,d);
        Frame(ui=>{check(column.MeasureConstrained(ui,UiBoxConstraints.Loose(new(200,100)))==new UiVector2(30,35),"generated Column measures vertical children");column.Render(ui);});
        check(c.Position==new UiVector2(5,0)&&d.Position==new UiVector2(0,15),"Column aligns and places children on the vertical axis");
        var duplicate=new Probe(new(1,1));var duplicateRejected=false;
        try{_=new UiRow([duplicate,duplicate]);}catch(ArgumentException){duplicateRejected=true;}
        using(var afterRejected=new UiContainer(duplicate))check(duplicateRejected,"duplicate-child rejection happens before ownership mutation");
        var overflow=false;Frame(ui=>{try{_=row.MeasureConstrained(ui,UiBoxConstraints.Loose(new(40,100)));}catch(InvalidOperationException){overflow=true;}});
        check(overflow,"main-axis overflow is explicit rather than silently overlapping children");
        using var custom=new TestGeneratedContainer(new Probe(new(12,8)),new(Padding:new(2)));
        Frame(custom.Render);
        check(custom.Child is Probe {Renders:1},"a user container consumes its source-generated closed layout/presentation implementation");
        check(!Fluent.ComponentNames.Contains("Container")&&!Fluent.ComponentNames.Contains("Row")&&!Fluent.ComponentNames.Contains("Column"),"layout infrastructure does not masquerade as extra Fluent visual components");
        var clicks=0;var button=new ObservedButton(()=>clicks++);button.Size=new(60,32);
        using var interactive=new UiContainer(button,new(Width:100,Height:60,Alignment:UiAlignment.Center));
        Frame(interactive.Render);var point=new UiVector2(button.Bounds.X+button.Bounds.Width*.5f,button.Bounds.Y+button.Bounds.Height*.5f);
        Frame(interactive.Render,idle with{MousePosition=point,LeftMouseDown=true,LeftMousePressed=true});
        Frame(interactive.Render,idle with{MousePosition=point,LeftMouseReleased=true});
        check(clicks==1,"an inherited generated Fluent button inside a container retains its real pointer callback exactly once");
        row.Dispose();check(a.Disposals==1&&b.Disposals==1,"disposing a layout container releases owned child lifetimes exactly once");
        using var composition=new Composed();
        Frame(ui=>{check(composition.MeasureConstrained(ui,UiBoxConstraints.Loose(new(200,100)))==new UiVector2(24,14),"a user Build-only widget participates in constrained container measurement");composition.Render(ui);});
        check(composition.Builds==1&&composition.Content.Renders==1,"measurement prepares a keyed Build once while actual rendering still executes");
        composition.Dispose();check(composition.Content.Disposals==1,"a keyed composed container releases its entire owned subtree");
        var oldRequests=0;var newRequests=0;
        context.SetRequestFrameCallback(()=>oldRequests++);
        using var lateOwner=new LateOwner();Frame(lateOwner.Render);
        var lateChild=new Probe(new(1,1));lateOwner.Take(lateChild);
        lateOwner.RequestRebuild();check(oldRequests==1,"a rendered direct control preserves its frame callback when it first acquires child ownership");
        context.SetRequestFrameCallback(()=>newRequests++);Frame(lateOwner.Render);lateOwner.RequestRebuild();
        check(newRequests==1&&oldRequests==1,"an existing Build context accepts the latest frame callback without retaining the previous callback");
        lateOwner.Dispose();check(lateChild.Disposals==1,"late ownership still disposes its child exactly once");
        RetainedArrangementInvalidationTests.Run(check);
        TabLayoutTests.Run(check);
        PanelRootClipTests.Run(check);
    }
    private sealed class LateOwner:UiControlWidget
    {
        private UiWidget? _child;
        internal void Take(UiWidget child){OwnChild("late",child);_child=child;}
        protected override void RenderControl(UiImmediateContext ui)=>_child?.Render(ui);
    }
    private sealed class Probe(UiVector2 desired,UiWidgetIdentity identity=UiWidgetIdentity.Instance):UiControlWidget(identity)
    {
        internal int Renders,Disposals;internal UiVector2 Position,Available;
        protected override UiVector2 MeasureContent(UiControlContext context)=>desired;
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;Position=ui.GetCursorScreenPos();Available=ui.GetContentRegionAvail();
            ui.GetWindowDrawList().AddRectFilled(new(Position.X,Position.Y,desired.X,desired.Y),new(0,120,220),ui.WhiteTextureId);
            ui.Dummy(desired);
        }
        protected override void OnDispose()=>Disposals++;
    }
    private sealed class ObservedButton:UiFluentButton
    {
        internal UiRect Bounds;
        internal ObservedButton(Action action):base(string.Empty,action){}
        protected override void RenderControl(UiImmediateContext ui)
        {base.RenderControl(ui);var p=ui.GetItemRectMin();var size=ui.GetItemRectSize();Bounds=new(p.X,p.Y,size.X,size.Y);}
    }
    private sealed class Composed:UiWidget
    {
        internal int Builds;internal Probe Content=new(new(20,10));
        protected override IUiView Build(UiBuildContext context)
        {Builds++;return context.Child("root",()=>new UiContainer(Content,new(Padding:new(2))));}
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen
    {public override void Render(UiImmediateContext ui)=>draw(ui);}
}

internal readonly struct TestContainerPaint:IUiContainerPresentation
{public static void Paint(UiControlContext context,UiRect bounds)=>context.Frame(bounds,new(20,30,40),new(20,30,40),0,0);}
[UiWidgetStyle(typeof(UiContainerBox<,>),typeof(UiBoxContainerLayout),typeof(TestContainerPaint))]
internal partial class TestGeneratedContainer;

internal sealed record BuildContextMemoryResult(string Framework,int Widgets,long OwnedConstructionBytes,long FirstBuildBytes,long SteadyBuildBytes,int Builds,string Scope);
[JsonSerializable(typeof(BuildContextMemoryResult))]
[JsonSourceGenerationOptions(WriteIndented=true)]
internal partial class BuildContextMemoryJson:JsonSerializerContext;
