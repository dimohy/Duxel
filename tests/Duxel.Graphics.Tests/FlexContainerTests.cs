using Duxel.Core;

internal static class FlexContainerTests
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
        var fixedChild=new Probe(new(40,10));var a=new Probe(new(5,20));var b=new Probe(new(5,30));
        using var row=Dux.Row(new(Spacing:10),fixedChild,Dux.Expanded(a),Dux.Expanded(b,2));
        Frame(row.Render);
        check(a.Position==new UiVector2(50,0)&&a.Size==new UiVector2(80,20)&&b.Position==new UiVector2(140,0)&&b.Size==new UiVector2(160,30),
            "Expanded divides remaining row space1:2 after non-flex content and spacing");
        row.Options=row.Options with{CrossAxisAlignment=UiCrossAxisAlignment.Stretch};Frame(row.Render);
        check(fixedChild.Size.Y==180&&a.Size.Y==180&&b.Size.Y==180,"cross-axis stretch passes tight finite constraints to all row children");
        var requests=0;context.SetRequestFrameCallback(()=>requests++);Frame(row.Render);
        var expanded=(UiExpanded)row.Children[1];expanded.Flex=2;
        check(requests==1,"changing a live flex weight requests a new frame");Frame(row.Render);
        check(a.Size.X==120&&b.Size.X==120&&a.Disposals==0&&b.Disposals==0,"a flex weight change preserves owned child instances and their state");
        expanded.Flex=1;Frame(row.Render);
        var loose=new Probe(new(20,10));var tight=new Probe(new(5,10));
        using var mixed=new UiRow([new Probe(new(40,10)),new UserFlexible(loose),Dux.Expanded(tight)],new(Spacing:10,MainAxisSize:UiMainAxisSize.Min));
        Frame(ui=>{check(mixed.MeasureConstrained(ui,UiBoxConstraints.Loose(new(300,180)))==new UiVector2(200,10),"loose Flexible may shrink below its share while tight Expanded fills its share");mixed.Render(ui);});
        check(loose.Size.X==20&&tight.Size.X==120,"a user-generated fit implementation shares the built-in weighted layout path");
        var left=new Probe(new(1,10));var right=new Probe(new(1,10));
        using var spaced=Dux.Row(default,Dux.Expanded(left),Dux.FlexSpacer(),Dux.Expanded(right));Frame(spaced.Render);
        check(left.Size.X==100&&right.Position.X==200&&right.Size.X==100,"generated Spacer consumes weighted space without a painted child");
        var spacer=(UiSpacer)spaced.Children[1];spacer.Flex=2;Frame(spaced.Render);
        check(left.Size.X==75&&right.Position.X==225&&right.Size.X==75,"a live Spacer weight update changes only the allocated gap");
        var rejected=false;try{spacer.Flex=0;}catch(ArgumentOutOfRangeException){rejected=true;}
        check(rejected&&spacer.Flex==2,"invalid Spacer updates preserve the previous valid weight");
        var top=new Probe(new(10,1));var bottom=new Probe(new(10,1));
        using var column=Dux.Column(default,Dux.Expanded(top),Dux.Expanded(bottom,2));Frame(column.Render);
        check(top.Size.Y==60&&bottom.Position.Y==60&&bottom.Size.Y==120,"the statically selected vertical axis applies the same weighted allocation");
        using var unboundedLoose=new UiColumn([Dux.Flexible(new Probe(new(20,10)))],new(MainAxisSize:UiMainAxisSize.Min));
        Frame(ui=>check(unboundedLoose.MeasureConstrained(ui,new())==new UiVector2(20,10),"loose Flexible shrink-wraps an unbounded main axis with MainAxisSize.Min"));
        using var unboundedTight=new UiColumn([Dux.Expanded(new Probe(new(20,10)))],new(MainAxisSize:UiMainAxisSize.Min));
        var failed=false;Frame(ui=>{try{unboundedTight.MeasureConstrained(ui,new());}catch(InvalidOperationException){failed=true;}});
        check(failed,"tight flex rejects an unbounded main axis explicitly");
        using var outside=Dux.Expanded(new Probe(new(1,1)));failed=false;
        Frame(ui=>{try{outside.MeasureConstrained(ui,UiBoxConstraints.Loose(new(100,100)));}catch(InvalidOperationException){failed=true;}});
        check(failed,"flex parent data cannot be silently ignored outside a Row or Column");
        Frame(row.Render);check(a.Size.X==80,"a rejected allocation restores measurement and flex scopes for the next frame");
        var blocked=false;
        using var impureRow=Dux.Row(default,new ImpureBuild());
        Frame(ui=>
        {
            ImpureBuild.Ui=ui;
            try{impureRow.Render(ui);}catch(InvalidOperationException error){blocked=error.Message=="Measurement cannot render or process input.";}
            finally{ImpureBuild.Ui=null;}
        });
        check(blocked,"flex metadata discovery runs user Build inside the common measurement guard before any input or paint");
        Frame(row.Render);check(a.Size.X==80,"failed parent-data discovery restores common measurement state");
        using var composition=new Composition();var sibling=new Probe(new(1,10));
        using var composedRow=Dux.Row(default,composition,Dux.Expanded(sibling));Frame(composedRow.Render);
        check(composition.Builds==1&&composition.Current!.Size.X==150&&sibling.Size.X==150,"Build-only user composition forwards flex parent data without adding a layout barrier");
        var old=composition.Current!;composition.Weight=3;composition.RequestRebuild();Frame(composedRow.Render);
        check(composition.Builds==2&&old.Disposals==1&&composition.Current!.Size.X==225&&sibling.Size.X==75,
            "a keyed Build replacement refreshes flex metadata and releases the removed subtree once");
        var clicks=0;var button=new ObservedButton(()=>clicks++){Size=new(20,32)};
        using var buttonRow=Dux.Row(default,Dux.Expanded(button));Frame(buttonRow.Render);
        var point=new UiVector2(280,16);
        Frame(buttonRow.Render,idle with{MousePosition=point,LeftMouseDown=true,LeftMousePressed=true});
        Frame(buttonRow.Render,idle with{MousePosition=point,LeftMouseReleased=true});
        check(button.Bounds==new UiRect(0,0,300,32)&&clicks==1,"Expanded stretches real Fluent button paint and pointer bounds beyond its requested20px width");
        check(!Fluent.ComponentNames.Contains("Expanded")&&!Fluent.ComponentNames.Contains("Flexible")&&!Fluent.ComponentNames.Contains("Spacer"),
            "generated layout helpers remain infrastructure outside the Fluent visual catalog");
        check(Dux.Spacer() is not UiSpacer&&Dux.Spacer(3) is not UiSpacer,"legacy fixed-height Spacer overloads retain their existing meaning");
        var manual=new ManualControls();using var manualRow=Dux.Row(default,Dux.Expanded(manual));Frame(manualRow.Render);
        check(manual.ButtonSize==new UiVector2(20,32),"an allocated custom control can still lay out immediate buttons at explicitly requested sizes");
        var zero=new Probe(new(20,10));using var zeroFlex=Dux.Row(default,Dux.Expanded(zero,0));Frame(zeroFlex.Render);
        check(zero.Size.X==20,"zero flex remains an ordinary natural-size child");
        using var many=new UiRow(Enumerable.Range(0,10_000).Select(_=>(UiWidget)Dux.Expanded(new Probe(new(1,1)))).ToArray());
        var constraints=UiBoxConstraints.Loose(new(10_000,10));
        Frame(ui=>many.MeasureConstrained(ui,constraints));Frame(ui=>many.MeasureConstrained(ui,constraints));
        long allocation=0;Frame(ui=>{var start=GC.GetAllocatedBytesForCurrentThread();var size=many.MeasureConstrained(ui,constraints);allocation=GC.GetAllocatedBytesForCurrentThread()-start;check(size==new UiVector2(10_000,1),"10000 generated flex children preserve the complete measured extent");});
        check(allocation==0,"10000-child weighted measurement allocates zero current-thread bytes after warmup");
        composedRow.Dispose();check(composition.Current!.Disposals==1,"disposing the flex owner releases the current composed child once");
    }

    internal sealed class Probe(UiVector2 desired):UiControlWidget
    {
        internal UiVector2 Position,Size;internal int Disposals;
        protected override UiVector2 MeasureContent(UiControlContext context)=>context.LayoutConstraints.Constrain(desired);
        protected override void RenderControl(UiImmediateContext ui)
        {
            Position=ui.GetCursorScreenPos();Size=new UiControlContext(ui).ConstrainToLayout(desired);
            ui.GetWindowDrawList().AddRectFilled(new(Position.X,Position.Y,Size.X,Size.Y),new(0,120,220),ui.WhiteTextureId);ui.Dummy(Size);
        }
        protected override void OnDispose()=>Disposals++;
    }
    private sealed class Composition:UiWidget
    {
        internal int Weight=1,Builds;internal Probe? Current;
        protected override IUiView Build(UiBuildContext context)
        {Builds++;return context.Child("weight:"+Weight,()=>Dux.Expanded(Current=new(new(1,10)),Weight));}
    }
    private sealed class ManualControls:UiControlWidget
    {
        internal UiVector2 ButtonSize;
        protected override UiVector2 MeasureContent(UiControlContext context)=>new(40,32);
        protected override void RenderControl(UiImmediateContext ui)
        {ui.Button<UiReleaseButtonBehavior,UiDefaultButtonPresentation>(string.Empty,new(20,32));ButtonSize=ui.GetItemRectSize();}
    }
    private sealed class ImpureBuild:UiWidget
    {
        internal static UiImmediateContext? Ui;
        protected override IUiView Build(UiBuildContext context)
        {Ui!.Button<UiReleaseButtonBehavior,UiDefaultButtonPresentation>(string.Empty,new(20,20));return context.Child("body",()=>Dux.FlexSpacer());}
    }
    private sealed class ObservedButton(Action action):UiFluentButton(string.Empty,action)
    {
        internal UiRect Bounds;
        protected override void RenderControl(UiImmediateContext ui)
        {base.RenderControl(ui);var p=ui.GetItemRectMin();var size=ui.GetItemRectSize();Bounds=new(p.X,p.Y,size.X,size.Y);}
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen {public override void Render(UiImmediateContext ui)=>draw(ui);}
}

[UiWidgetStyle(typeof(UiFlexibleWidget<>),typeof(UiLooseFlexFit))]
internal partial class UserFlexible;
