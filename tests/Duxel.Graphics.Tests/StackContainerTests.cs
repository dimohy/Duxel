using Duxel.Core;

internal static class StackContainerTests
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
        var baseChild=new FlexContainerTests.Probe(new(100,60));var edge=new FlexContainerTests.Probe(new(5,5));
        var aligned=new FlexContainerTests.Probe(new(20,10));
        var position=Dux.Positioned(edge,new(Left:10,Right:20,Top:5,Bottom:15));
        using var stack=Dux.Stack(new(Alignment:UiAlignment.Center),baseChild,position,aligned);
        Frame(stack.Render);
        check(edge.Position==new UiVector2(10,5)&&edge.Size==new UiVector2(70,40),"Stack stretches edge-anchored children after ordinary children establish100x60 extent");
        check(aligned.Position==new UiVector2(40,25),"Stack alignment applies independently to ordinary children");
        var requests=0;context.SetRequestFrameCallback(()=>requests++);
        position.Options=new(Right:7,Bottom:9,Width:30,Height:20);Frame(stack.Render);
        check(requests==1&&edge.Position==new UiVector2(63,31)&&edge.Size==new UiVector2(30,20)&&edge.Disposals==0,"live Positioned options request a frame and preserve child state");
        var failed=false;try{position.Options=new(Left:0,Right:0,Width:10);}catch(ArgumentOutOfRangeException){failed=true;}
        check(failed&&position.Options.Right==7,"over-constrained Positioned updates reject without changing valid options");
        var emptyOptionsChild=new FlexContainerTests.Probe(new(20,10));
        using var ordinary=Dux.Stack(default,Dux.Positioned(emptyOptionsChild));Frame(ordinary.Render);
        check(emptyOptionsChild.Size==new UiVector2(20,10),"Positioned with all-null options participates in ordinary Stack sizing");
        var full=new FlexContainerTests.Probe(new(1,1));using var expanded=Dux.Stack(new(Fit:UiStackFit.Expand),full);Frame(expanded.Render);
        check(full.Size==new UiVector2(300,180),"Stack Expand passes tight bounded extents to ordinary children");
        using var boundedOnly=Dux.Stack(default,Dux.Positioned(new FlexContainerTests.Probe(new(10,10)),new(Left:0)));
        failed=false;Frame(ui=>{try{boundedOnly.MeasureConstrained(ui,new());}catch(InvalidOperationException){failed=true;}});
        check(failed,"all-positioned Stack rejects unbounded self-sizing explicitly");
        using var outside=Dux.Positioned(new FlexContainerTests.Probe(new(1,1)),new(Left:0));failed=false;
        Frame(ui=>{try{outside.MeasureConstrained(ui,UiBoxConstraints.Loose(new(100,100)));}catch(InvalidOperationException){failed=true;}});
        check(failed,"Positioned cannot silently lose parent data outside Stack");
        using var barrier=Dux.Stack(default,Dux.Container(Dux.Positioned(new FlexContainerTests.Probe(new(1,1)),new(Left:0))));failed=false;
        Frame(ui=>{try{barrier.Render(ui);}catch(InvalidOperationException){failed=true;}});
        check(failed,"a physical Container is a parent-data boundary between Positioned and Stack");
        using var wrongFlex=Dux.Stack(default,Dux.Expanded(new FlexContainerTests.Probe(new(1,1))));failed=false;
        Frame(ui=>{try{wrongFlex.Render(ui);}catch(InvalidOperationException){failed=true;}});
        check(failed,"Stack rejects flex parent data instead of interpreting it as positioning");
        using var wrongPosition=Dux.Row(default,Dux.Positioned(new FlexContainerTests.Probe(new(1,1)),new(Left:0)));failed=false;
        Frame(ui=>{try{wrongPosition.Render(ui);}catch(InvalidOperationException){failed=true;}});
        check(failed,"Row rejects Positioned parent data instead of interpreting it as flex");
        using var composed=new Composition();using var composedStack=Dux.Stack(default,composed);Frame(composedStack.Render);
        check(composed.Builds==1&&composed.Current!.Position==new UiVector2(8,9),"Build-only user widgets forward Positioned data through the shared measurement guard");
        var previous=composed.Current!;composed.Key=2;composed.RequestRebuild();Frame(composedStack.Render);
        check(composed.Builds==2&&previous.Disposals==1&&composed.Current!.Position==new UiVector2(16,9),"keyed Positioned replacement refreshes parent data and releases the old subtree once");
        Frame(stack.Render);check(edge.Position==new UiVector2(63,31),"failed parent-data scopes restore subsequent layout frames");
        var clicks=0;using var buttonStack=Dux.Stack(default,Dux.Positioned(Fluent.Button("",()=>clicks++),new(Left:10,Top:10,Width:80,Height:32)));
        var point=new UiVector2(85,25);Frame(buttonStack.Render);
        Frame(buttonStack.Render,idle with{MousePosition=point,LeftMouseDown=true,LeftMousePressed=true});
        Frame(buttonStack.Render,idle with{MousePosition=point,LeftMouseReleased=true});
        check(clicks==1,"Positioned applies the allocated width to real Fluent button pointer bounds");
        var lowerClicks=0;var upperClicks=0;
        using var overlap=Dux.Stack(default,
            Dux.Positioned(Fluent.Button("",()=>lowerClicks++),new(Left:10,Top:10,Width:80,Height:32)),
            Dux.Positioned(Fluent.Button("",()=>upperClicks++),new(Left:10,Top:10,Width:80,Height:32)));
        Frame(overlap.Render);Frame(overlap.Render,idle with{MousePosition=point,LeftMouseDown=true,LeftMousePressed=true});
        Frame(overlap.Render,idle with{MousePosition=point,LeftMouseReleased=true});
        check(lowerClicks==0&&upperClicks==1,"overlapping Stack buttons activate only the last painted topmost child");
        var lowerPress=0;var upperPress=0;
        using var pressStack=Dux.Stack(default,
            Dux.Positioned(new UiButton<UiPressButtonBehavior,UiFluentButtonPresentation>("",()=>lowerPress++),new(Left:10,Top:10,Width:80,Height:32)),
            Dux.Positioned(new UiButton<UiPressButtonBehavior,UiFluentButtonPresentation>("",()=>upperPress++),new(Left:10,Top:10,Width:80,Height:32)));
        Frame(pressStack.Render);Frame(pressStack.Render,idle with{MousePosition=point,LeftMouseDown=true,LeftMousePressed=true});
        Frame(pressStack.Render,idle with{MousePosition=point,LeftMouseReleased=true});
        check(lowerPress==0&&upperPress==1,"topmost Stack pointer routing also prevents duplicate press-activation callbacks");
        var clippedClicks=0;
        using var clippedStack=Dux.Stack(default,new FlexContainerTests.Probe(new(100,60)),
            Dux.Positioned(Fluent.Button("",()=>clippedClicks++),new(Left:90,Top:10,Width:80,Height:32)));
        var outsidePoint=new UiVector2(140,25);Frame(clippedStack.Render);
        Frame(clippedStack.Render,idle with{MousePosition=outsidePoint,LeftMouseDown=true,LeftMousePressed=true});
        Frame(clippedStack.Render,idle with{MousePosition=outsidePoint,LeftMouseReleased=true});
        check(clippedClicks==0,"Stack overflow outside the visible root does not receive button pointer input");
        var shared=new FlexContainerTests.Probe(new(1,1));failed=false;
        try{using var invalid=new UiStack([shared,shared]);}catch(ArgumentException){failed=true;}
        using var recovered=new UiStack([shared]);Frame(recovered.Render);
        check(failed&&shared.Disposals==0,"multi-child ownership validates every child before adoption and leaves failed inputs reusable");
        check(!Fluent.ComponentNames.Contains("Stack")&&!Fluent.ComponentNames.Contains("Positioned"),"source-generated Stack and Positioned remain outside the Fluent visual catalog");
        using var many=new UiStack(Enumerable.Range(0,10_000).Select(_=>(UiWidget)Dux.Positioned(new FlexContainerTests.Probe(new(1,1)),new(Left:0,Top:0))).ToArray());
        var constraints=UiBoxConstraints.Loose(new(300,180));Frame(ui=>many.MeasureConstrained(ui,constraints));Frame(ui=>many.MeasureConstrained(ui,constraints));
        long bytes=0;Frame(ui=>{var before=GC.GetAllocatedBytesForCurrentThread();many.MeasureConstrained(ui,constraints);bytes=GC.GetAllocatedBytesForCurrentThread()-before;});
        check(bytes==0,"10000-child positioned measurement allocates zero current-thread bytes after warmup");
        composedStack.Dispose();check(composed.Current!.Disposals==1,"disposing Stack releases the current composed Positioned child once");
    }
    private sealed class Composition:UiWidget
    {
        internal int Key=1,Builds;internal FlexContainerTests.Probe? Current;
        protected override IUiView Build(UiBuildContext context)
        {Builds++;return context.Child("position:"+Key,()=>Dux.Positioned(Current=new(new(10,10)),new(Left:8*Key,Top:9)));}
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen
    {public override void Render(UiImmediateContext ui)=>draw(ui);}
}
