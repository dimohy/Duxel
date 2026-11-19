using Duxel.Core;

internal static class FlowContainerTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        void Frame(Action<UiImmediateContext> draw,UiInputState? input=null)
        {
            context.NewFrame(new(1f/60,new(300,180),new(1,1)),input??idle,new(0,0,300,180),UiTextSettings.Default);
            context.Render(new Screen(ui=>{ui.EnableRootViewportContentLayout(contentPadding:0);ui.SetCursorScreenPos(default);draw(ui);}));
            using var data=context.GetDrawData();
        }
        var a=new FlexContainerTests.Probe(new(130,20));var b=new FlexContainerTests.Probe(new(100,40));var c=new FlexContainerTests.Probe(new(80,10));
        using var wrap=Dux.Wrap(new(Spacing:10,RunSpacing:7,CrossAlignment:UiWrapCrossAlignment.End),a,b,c);Frame(wrap.Render);
        check(a.Position==new UiVector2(0,20)&&b.Position==new UiVector2(140,0)&&c.Position==new UiVector2(0,47),"Wrap forms natural-size runs with independent item/run spacing and end cross alignment");
        Frame(ui=>check(wrap.MeasureConstrained(ui,new())==new UiVector2(330,40),"unbounded Wrap main axis forms one natural-width run"));
        var requests=0;context.SetRequestFrameCallback(()=>requests++);
        wrap.Options=wrap.Options with{Alignment=UiMainAxisAlignment.Center,RunAlignment=UiMainAxisAlignment.SpaceBetween};
        check(requests==1,"live Wrap options request a frame");
        using var tightWrap=Dux.Container(wrap,new(Width:300,Height:180));Frame(tightWrap.Render);
        // A Container without alignment keeps natural children; an expanded Stack supplies tight constraints.
        var centerA=new FlexContainerTests.Probe(new(130,20));var centerB=new FlexContainerTests.Probe(new(100,40));var centerC=new FlexContainerTests.Probe(new(80,10));
        using var filled=Dux.Stack(new(Fit:UiStackFit.Expand),new UserWrap([centerA,centerB,centerC],wrap.Options));Frame(filled.Render);
        check(centerA.Position==new UiVector2(30,20)&&centerB.Position==new UiVector2(170,0)&&centerC.Position==new UiVector2(110,170),"generated user Wrap shares centered item and space-between run allocation under tight parent constraints");
        check(a.Disposals==0&&b.Disposals==0&&c.Disposals==0,"Wrap option changes preserve all owned child instances");
        var va=new FlexContainerTests.Probe(new(10,100));var vb=new FlexContainerTests.Probe(new(30,70));var vc=new FlexContainerTests.Probe(new(20,40));
        using var vertical=Dux.VerticalWrap(new(Spacing:10,RunSpacing:5,CrossAlignment:UiWrapCrossAlignment.End),va,vb,vc);Frame(vertical.Render);
        check(va.Position==new UiVector2(20,0)&&vb.Position==new UiVector2(0,110)&&vc.Position==new UiVector2(35,0),"statically selected vertical Wrap forms columns using the same measured run algorithm");
        var ga=new FlexContainerTests.Probe(new(1,1));var gb=new FlexContainerTests.Probe(new(1,1));var gc=new FlexContainerTests.Probe(new(1,1));var gd=new FlexContainerTests.Probe(new(1,1));
        using var grid=Dux.UniformGrid(new(3,CrossAxisSpacing:6,MainAxisSpacing:8,ChildAspectRatio:2),ga,gb,gc,gd);Frame(grid.Render);
        check(ga.Size==new UiVector2(96,48)&&gb.Position==new UiVector2(102,0)&&gc.Position==new UiVector2(204,0)&&gd.Position==new UiVector2(0,56),"Grid allocates equal aspect-ratio cells and a partial final row");
        Frame(ui=>check(grid.MeasureConstrained(ui,new(0,300,0,float.PositiveInfinity))==new UiVector2(300,104),"Grid permits unbounded main-axis measurement with a bounded cross axis"));
        requests=0;grid.Options=new(2,CrossAxisSpacing:10,MainAxisSpacing:8,MainAxisExtent:32);Frame(grid.Render);
        check(requests==1&&ga.Size==new UiVector2(145,32)&&gb.Position==new UiVector2(155,0)&&gd.Position==new UiVector2(155,40)&&ga.Disposals==0,
            "live Grid count/extent changes preserve children while updating exact tight allocations");
        var ha=new FlexContainerTests.Probe(new(1,1));var hb=new FlexContainerTests.Probe(new(1,1));var hc=new FlexContainerTests.Probe(new(1,1));
        using var horizontal=Dux.HorizontalGrid(new(2,CrossAxisSpacing:4,MainAxisSpacing:6,ChildAspectRatio:2),ha,hb,hc);Frame(horizontal.Render);
        check(ha.Size==new UiVector2(44,88)&&hb.Position==new UiVector2(0,92)&&hc.Position==new UiVector2(50,0),"horizontal Grid statically swaps main/cross axes including the aspect ratio");
        var failed=false;Frame(ui=>{try{grid.MeasureConstrained(ui,new());}catch(InvalidOperationException){failed=true;}});
        check(failed,"Grid rejects an unbounded cross axis instead of choosing an arbitrary cell width");
        failed=false;try{grid.Options=new(CrossAxisCount:0);}catch(ArgumentOutOfRangeException){failed=true;}
        check(failed&&grid.Options.CrossAxisCount==2,"invalid Grid options preserve the previous valid layout");
        using var tooMuchSpacing=Dux.UniformGrid(new(2,CrossAxisSpacing:301),new FlexContainerTests.Probe(new(1,1)));failed=false;
        Frame(ui=>{try{tooMuchSpacing.Render(ui);}catch(InvalidOperationException){failed=true;}});
        check(failed,"Grid explicitly rejects cross-axis gaps larger than available space");
        using var wrongFlex=Dux.Wrap(default,Dux.Expanded(new FlexContainerTests.Probe(new(1,1))));failed=false;
        Frame(ui=>{try{wrongFlex.Render(ui);}catch(InvalidOperationException){failed=true;}});
        check(failed,"Wrap does not consume Row/Column-specific parent data");
        using var wrongPosition=Dux.UniformGrid(new(1),Dux.Positioned(new FlexContainerTests.Probe(new(1,1)),new(Left:0)));failed=false;
        Frame(ui=>{try{wrongPosition.Render(ui);}catch(InvalidOperationException){failed=true;}});
        check(failed,"Grid does not consume Stack-specific parent data");
        Frame(grid.Render);check(gd.Position==new UiVector2(155,40),"failed flow layouts restore measurement/allocation scopes");
        var clicks=0;using var buttonGrid=Dux.UniformGrid(new(2,MainAxisExtent:32),Fluent.Button("",()=>clicks++),Fluent.Button(""));
        var point=new UiVector2(140,16);Frame(buttonGrid.Render);
        Frame(buttonGrid.Render,idle with{MousePosition=point,LeftMouseDown=true,LeftMousePressed=true});
        Frame(buttonGrid.Render,idle with{MousePosition=point,LeftMouseReleased=true});
        check(clicks==1,"Grid tight cell allocation reaches real Fluent button pointer bounds");
        var wrappedClicks=0;var firstButton=Fluent.Button("");var secondButton=Fluent.Button("",()=>wrappedClicks++);
        firstButton.Size=secondButton.Size=new(180,32);
        using var wrappedButtons=Dux.Wrap(new(Spacing:12,RunSpacing:12),firstButton,secondButton);
        var secondPoint=new UiVector2(140,60);Frame(wrappedButtons.Render);
        Frame(wrappedButtons.Render,idle with{MousePosition=secondPoint,LeftMouseDown=true,LeftMousePressed=true});
        Frame(wrappedButtons.Render,idle with{MousePosition=secondPoint,LeftMouseReleased=true});
        check(wrappedClicks==1,"Wrap second-run placement preserves actual Fluent button pointer input");
        using var emptyWrap=Dux.Wrap(default);using var emptyGrid=Dux.UniformGrid(new(2));
        Frame(ui=>{check(emptyWrap.MeasureConstrained(ui,UiBoxConstraints.Loose(new(300,180)))==default,"empty Wrap has zero natural extent");
            check(emptyGrid.MeasureConstrained(ui,UiBoxConstraints.Loose(new(300,180)))==new UiVector2(300,0),"empty Grid retains its bounded cross extent without inventing a row");});
        var child=new FlexContainerTests.Probe(new(1,1));failed=false;
        try{using var invalid=new UiGrid([child],new(0));}catch(ArgumentOutOfRangeException){failed=true;}
        using var valid=Dux.Wrap(default,child);Frame(valid.Render);
        check(failed&&child.Disposals==0,"Grid validates options before taking ownership of supplied children");
        check(Dux.Grid(2) is not UiWidget,"legacy declarative Grid overload binding remains unchanged");
        using var badLayout=new InvalidLayout();failed=false;
        Frame(ui=>{try{badLayout.MeasureConstrained(ui,UiBoxConstraints.Tight(new(10,10)));}catch(InvalidOperationException){failed=true;}});
        check(failed,"custom arranged layouts cannot return a size outside parent constraints and silently differ between measure and render");
        check(!Fluent.ComponentNames.Contains("Wrap")&&!Fluent.ComponentNames.Contains("VerticalWrap")&&!Fluent.ComponentNames.Contains("Grid")&&!Fluent.ComponentNames.Contains("HorizontalGrid"),
            "generated flow containers are infrastructure outside the Fluent visual catalog");
        using var manyWrap=new UiWrap(Enumerable.Range(0,10000).Select(_=>(UiWidget)new FlexContainerTests.Probe(new(1,1))).ToArray());
        using var manyGrid=new UiGrid(Enumerable.Range(0,10000).Select(_=>(UiWidget)new FlexContainerTests.Probe(new(1,1))).ToArray(),new(100));
        var constraints=UiBoxConstraints.Loose(new(100,100));
        for(var i=0;i<2;i++)Frame(ui=>{manyWrap.MeasureConstrained(ui,constraints);manyGrid.MeasureConstrained(ui,constraints);});
        long bytes=0;UiVector2 wrapSize=default,gridSize=default;
        Frame(ui=>{var before=GC.GetAllocatedBytesForCurrentThread();wrapSize=manyWrap.MeasureConstrained(ui,constraints);gridSize=manyGrid.MeasureConstrained(ui,constraints);bytes=GC.GetAllocatedBytesForCurrentThread()-before;});
        check(wrapSize==new UiVector2(100,100)&&gridSize==new UiVector2(100,100),"10000-child Wrap and Grid preserve all measured rows and extents");
        check(bytes==0,"10000-child Wrap plus10000-child Grid pure measurement allocates zero bytes after warmup");
        grid.Dispose();check(ga.Disposals==1&&gb.Disposals==1&&gc.Disposals==1&&gd.Disposals==1,"disposing Grid releases every owned child once");
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen
    {public override void Render(UiImmediateContext ui)=>draw(ui);}
    private sealed class InvalidLayout:UiArrangedChildrenWidget
    {
        internal InvalidLayout():base([]) { }
        protected override UiVector2 ArrangeChildren(UiControlContext context,Span<UiRect> bounds)=>default;
    }
}
[UiWidgetStyle(typeof(UiWrapWidget<>),typeof(UiHorizontalAxis))]
internal partial class UserWrap;
