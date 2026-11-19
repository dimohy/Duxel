using Duxel.Core;

internal static class LayoutRetentionContractTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var settings=UiTextSettings.Default;
        void Frame(Action<UiImmediateContext> draw,UiInputState? input=null)
        {
            context.NewFrame(new(1f/60,new(300,180),new(1,1)),input??idle,new(0,0,300,180),settings);
            context.Render(new Screen(ui=>{ui.EnableRootViewportContentLayout(contentPadding:0);ui.SetCursorScreenPos(default);draw(ui);}));
            using var data=context.GetDrawData();
        }
        var a=new StableProbe(new(20,10));var b=new StableProbe(new(30,20));using var wrap=Dux.Wrap(new(Spacing:5),a,b);
        Frame(wrap.Render);var count=a.Measurements+b.Measurements;Frame(wrap.Render);
        check(a.Measurements+b.Measurements==count&&a.Renders==2&&b.Renders==2,"retained Wrap reuses measurement while rendering every child each frame");
        a.Desired=new(40,10);Frame(wrap.Render);
        check(b.Position==new UiVector2(45,0)&&a.Measurements+b.Measurements>count,"a child RequestRebuild invalidates ancestor arrangement without replacing its children");
        var nestedChild=new StableProbe(new(20,10));var sibling=new StableProbe(new(30,10));
        using var nested=Dux.Wrap(default,Dux.Wrap(default,nestedChild),Dux.Wrap(default,sibling));
        Frame(nested.Render);count=sibling.Measurements;var oldNested=nestedChild.Measurements;
        nestedChild.Desired=new(40,10);Frame(nested.Render);
        check(nestedChild.Measurements>oldNested&&sibling.Measurements==count,"ancestor invalidation remeasures the changed branch while retaining an unchanged sibling branch");
        using var volatileChild=new VolatileProbe();using var volatileWrap=Dux.Wrap(default,volatileChild);
        Frame(volatileWrap.Render);count=volatileChild.Measurements;volatileChild.Value=50;Frame(volatileWrap.Render);
        check(volatileChild.Measurements>count&&volatileChild.LastSize.X==50,"ordinary user widgets remain non-retained and may change size without opting into the invalidation contract");
        using var impure=new NonRetainedArrangement();Frame(impure.Render);count=impure.Arrangements;Frame(impure.Render);
        check(impure.Arrangements>count,"custom arranged layouts remain non-retained unless their arrangement explicitly opts in");
        using var derived=new DynamicFluentButton();using var dynamicButtonWrap=Dux.Wrap(default,derived);
        Frame(dynamicButtonWrap.Render);count=derived.Measurements;derived.Width=70;Frame(dynamicButtonWrap.Render);
        check(derived.Measurements>count,"a derived Fluent control can explicitly disable inherited measurement retention for dynamic sizing");
        var flexA=new StableProbe(new(1,10));var flexB=new StableProbe(new(1,10));
        var expandedA=Dux.Expanded(flexA);using var row=Dux.Row(default,expandedA,Dux.Expanded(flexB));
        Frame(row.Render);Frame(row.Render);count=flexA.Measurements+flexB.Measurements;Frame(row.Render);
        check(flexA.Measurements+flexB.Measurements==count&&flexA.LastSize.X==150&&flexB.Position.X==150,
            "retained Row and Expanded reuse child measurement without a render-time wrapper remeasurement");
        expandedA.Flex=3;Frame(row.Render);
        check(flexA.LastSize.X==225&&flexB.Position.X==225&&flexB.LastSize.X==75,
            "live flex weights invalidate retained Row metadata and exact allocations");
        row.Options=row.Options with{CrossAxisAlignment=UiCrossAxisAlignment.Stretch};Frame(row.Render);
        check(flexA.LastSize.Y==180&&flexB.LastSize.Y==180,"cross-axis options invalidate retained Row constraints");
        var columnChild=new StableProbe(new(20,10));
        using var nestedColumn=Dux.Column(new(MainAxisSize:UiMainAxisSize.Min),
            Dux.Container(Dux.Row(default,Dux.Expanded(columnChild)),new(Height:40)));
        Frame(nestedColumn.Render);Frame(nestedColumn.Render);count=columnChild.Measurements;Frame(nestedColumn.Render);
        check(columnChild.Measurements==count&&columnChild.Renders==3,
            "Column Container Row and Expanded share retained measurement while the real child renders every frame");
        columnChild.Desired=new(20,20);Frame(nestedColumn.Render);
        check(columnChild.Measurements>count&&columnChild.LastSize.Y==20,
            "deep child invalidation crosses Container and flex ownership without rebuilding composition");
        var overlayChild=new StableProbe(new(1,1));var position=Dux.Positioned(overlayChild,new(Left:10,Top:5,Width:40,Height:20));
        using var stack=Dux.Stack(new(Fit:UiStackFit.Expand),position);Frame(stack.Render);Frame(stack.Render);
        count=overlayChild.Measurements;Frame(stack.Render);
        check(overlayChild.Measurements==count&&overlayChild.Position==new UiVector2(10,5),
            "Stack and Positioned retain measured geometry without repeating child measurement during paint");
        position.Options=new(Right:5,Bottom:6,Width:40,Height:20);Frame(stack.Render);
        check(overlayChild.Position==new UiVector2(255,154)&&overlayChild.Measurements>count,
            "live Positioned updates invalidate retained Stack geometry");
        var boxChild=new StableProbe(new(20,10));using var box=Dux.Container(boxChild,new(Width:100,Height:60,Alignment:UiAlignment.Center));
        Frame(box.Render);Frame(box.Render);count=boxChild.Measurements;Frame(box.Render);
        check(boxChild.Measurements==count&&boxChild.Position==new UiVector2(40,25),
            "Container retains child and surface geometry in the shared optional result payload");
        box.Options=box.Options with{Padding=new(10),Alignment=UiAlignment.BottomRight};Frame(box.Render);
        check(boxChild.Position==new UiVector2(70,40)&&boxChild.Measurements>count,
            "Container padding and alignment updates invalidate the retained layout result");
        MutableContainerLayout.Width=50;MutableContainerLayout.Calls=0;
        using var customBox=new UiContainerBox<MutableContainerLayout,UiPlainContainerPresentation>(new StableProbe(new(10,10)));
        UiVector2 customSize=default;Frame(ui=>{customBox.Render(ui);customSize=ui.GetItemRectSize();});
        count=MutableContainerLayout.Calls;MutableContainerLayout.Width=80;Frame(ui=>{customBox.Render(ui);customSize=ui.GetItemRectSize();});
        check(MutableContainerLayout.Calls>count&&customSize.X==80,
            "custom container layout implementations remain non-retained unless their static policy opts in");
        MutableAxis.IsHorizontal=true;var axisA=new StableProbe(new(20,10));var axisB=new StableProbe(new(30,10));
        using var customAxis=new UiFlexWidget<MutableAxis>([axisA,axisB]);Frame(customAxis.Render);
        MutableAxis.IsHorizontal=false;Frame(customAxis.Render);
        check(axisB.Position==new UiVector2(0,10),"custom axis implementations default to non-retained measurement and cannot silently freeze a dynamic axis");
        MutableFit.IsTight=true;var fitChild=new StableProbe(new(5,10));
        using var customFit=Dux.Row(default,new UiFlexibleWidget<MutableFit>(fitChild));Frame(customFit.Render);
        var tightWidth=fitChild.LastSize.X;MutableFit.IsTight=false;Frame(customFit.Render);
        check(tightWidth==300&&fitChild.LastSize.X==5,"custom fit implementations default to non-retained measurement and cannot silently freeze a dynamic fit");
        var button=new MeasuredButton();using var buttonGrid=Dux.UniformGrid(new(2,MainAxisExtent:32),button,Fluent.Button(""));
        Frame(buttonGrid.Render);count=button.Measurements;Frame(buttonGrid.Render);
        check(button.Measurements==count,"real Fluent buttons enable the generated Grid retained-measurement path");
        button.Label=string.Empty;button.Size=new(40,32);Frame(buttonGrid.Render);
        check(button.Measurements>count,"button size changes propagate through common ownership to retained Grid");
        var clicks=0;button.Activated=()=>clicks++;var point=new UiVector2(140,16);
        Frame(buttonGrid.Render,idle with{MousePosition=point,LeftMouseDown=true,LeftMousePressed=true});
        Frame(buttonGrid.Render,idle with{MousePosition=point,LeftMouseReleased=true});
        check(clicks==1,"retained Grid continues actual pointer activation and callback delivery");
        count=button.Measurements;settings=settings with{Scale=2};Frame(buttonGrid.Render);
        check(button.Measurements>count,"text scale participates in the retained measurement environment");
        count=button.Measurements;context.SetTypographyFonts(new("regular","semibold","bold"));Frame(buttonGrid.Render);
        check(button.Measurements>count,"typography resource changes invalidate cached layout without retaining a rendering context");
        count=button.Measurements;context.SetContentScale(2);Frame(buttonGrid.Render);
        check(button.Measurements>count,"content scale participates in the retained measurement environment");
        count=button.Measurements;settings=settings with{MissingGlyphObserver=static _=>{}};Frame(buttonGrid.Render);Frame(buttonGrid.Render);
        check(button.Measurements>=count+2,"a glyph observer disables layout reuse so measurement observation is preserved");
        settings=UiTextSettings.Default;context.SetContentScale(1);
        using var composition=new StableComposition();using var composed=Dux.Wrap(default,composition);
        Frame(composed.Render);Frame(composed.Render);var old=composed.Children[0];composition.Width.Value=40;Frame(composed.Render);
        check(composition.Builds==2&&composition.Current!.LastSize.X==40&&composition.Removed!.Disposals==1,"watched Build state invalidates retained ancestors and releases a keyed replacement once");
        var invalidating=new StableProbe(new(10,10)){ChangeDuringMeasure=true};using var invalidatedDuringMeasure=Dux.Wrap(default,invalidating);
        Frame(invalidatedDuringMeasure.Render);count=invalidating.Measurements;Frame(invalidatedDuringMeasure.Render);
        check(invalidating.Measurements>count&&invalidating.LastSize.X==30,"invalidation during measurement prevents publishing a stale retained arrangement");
        var disposable=new StableProbe(new(10,10));using var disposedChild=Dux.Wrap(default,disposable);Frame(disposedChild.Render);disposable.Dispose();
        var failed=false;Frame(ui=>{try{disposedChild.MeasureConstrained(ui,UiBoxConstraints.Loose(new(300,180)));}catch(ObjectDisposedException){failed=true;}});
        check(failed,"disposing an owned child invalidates retained measurement before a later measurement can skip the disposed child");
        var failure=new StableProbe(new(10,10));using var failing=Dux.Wrap(default,failure);Frame(failing.Render);
        failure.Fail=true;failure.RequestRebuild();failed=false;
        Frame(ui=>{try{failing.MeasureConstrained(ui,UiBoxConstraints.Loose(new(300,180)));}catch(InvalidOperationException){failed=true;}});
        failure.Fail=false;failure.Desired=new(25,10);Frame(failing.Render);
        check(failed&&failure.LastSize.X==25,"a failed arrangement cannot leave a valid stale cache and measurement scopes recover");
    }
    private sealed class StableProbe(UiVector2 desired):UiControlWidget
    {
        private UiVector2 _desired=desired;
        internal int Measurements,Renders,Disposals;internal bool ChangeDuringMeasure,Fail;internal UiVector2 Position,LastSize;
        internal UiVector2 Desired {get=>_desired;set{_desired=value;RequestRebuild();}}
        protected override bool CanRetainMeasurement=>true;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;if(Fail)throw new InvalidOperationException("Expected measurement failure.");
            var result=context.LayoutConstraints.Constrain(_desired);
            if(ChangeDuringMeasure){ChangeDuringMeasure=false;Desired=new(30,10);}return result;
        }
        protected override void RenderControl(UiImmediateContext ui)
        {Renders++;Position=ui.GetCursorScreenPos();LastSize=new UiControlContext(ui).ConstrainToLayout(_desired);ui.Dummy(LastSize);}
        protected override void OnDispose()=>Disposals++;
    }
    private sealed class VolatileProbe:UiControlWidget
    {
        internal int Measurements;internal float Value=20;internal UiVector2 LastSize;
        protected override UiVector2 MeasureContent(UiControlContext context){Measurements++;return new(Value,10);}
        protected override void RenderControl(UiImmediateContext ui){LastSize=new UiControlContext(ui).ConstrainToLayout(new(Value,10));ui.Dummy(LastSize);}
    }
    private sealed class NonRetainedArrangement:UiArrangedChildrenWidget
    {
        internal int Arrangements;
        internal NonRetainedArrangement():base([]) { }
        protected override UiVector2 ArrangeChildren(UiControlContext context,Span<UiRect> bounds){Arrangements++;return context.LayoutConstraints.Constrain(default);}
    }
    private sealed class MeasuredButton:UiButton<UiReleaseButtonBehavior,UiFluentButtonPresentation>
    {
        internal int Measurements;
        internal MeasuredButton():base(string.Empty) { }
        protected override UiVector2 MeasureContent(UiControlContext context){Measurements++;return base.MeasureContent(context);}
    }
    private sealed class DynamicFluentButton:UiButton<UiReleaseButtonBehavior,UiFluentButtonPresentation>
    {
        internal int Measurements;internal float Width=20;
        internal DynamicFluentButton():base(string.Empty) { }
        protected override bool CanRetainMeasurement=>false;
        protected override UiVector2 MeasureContent(UiControlContext context){Measurements++;return new(Width,32);}
    }
    private readonly struct MutableContainerLayout:IUiContainerLayout
    {
        internal static float Width;internal static int Calls;
        public static UiContainerLayoutResult Measure(UiControlContext context,UiWidget child,UiContainerOptions options,UiBoxConstraints constraints)
        {Calls++;return UiBoxContainerLayout.Measure(context,child,options with{Width=Width},constraints);}
    }
    private readonly struct MutableAxis:IUiLayoutAxis
    {internal static bool IsHorizontal;public static bool Horizontal=>IsHorizontal;}
    private readonly struct MutableFit:IUiFlexFit
    {internal static bool IsTight;public static bool Tight=>IsTight;}
    private sealed class StableComposition:UiWidget
    {
        internal UiState<int> Width=new(20);internal int Builds;internal StableProbe? Current,Removed;
        protected override bool CanRetainMeasurement=>true;
        protected override IUiView Build(UiBuildContext context)
        {Builds++;var width=context.Watch(Width);Removed=Current;return context.Child("width:"+width,()=>Current=new(new(width,10)));}
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen
    {public override void Render(UiImmediateContext ui)=>draw(ui);}
}
