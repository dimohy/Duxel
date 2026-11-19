using Duxel.Core;

internal static class RadioAllocationTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));ControlFontFixture.Configure(context);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        void Frame(Action<UiImmediateContext> draw,UiInputState? input=null)
        {
            context.NewFrame(new(1f/60,new(300,240),new(1,1)),input??idle,new(0,0,300,240),UiTextSettings.Default);
            context.Render(new Screen(ui=>{ui.EnableRootViewportContentLayout(contentPadding:0);ui.SetCursorScreenPos(default);draw(ui);}));using var data=context.GetDrawData();
        }
        UiRadioOption[] options=[new("a","A"),new("b","B")];
        var state=new UiState<string?>("a");var group=new ObservedGroup(state.Binding,options);using var box=Dux.SizedBox(group,80,120);
        Frame(ui=>{check(box.MeasureConstrained(ui,new())==new UiVector2(80,120),"SizedBox reports the80x120 RadioGroup allocation");box.Render(ui);});
        check(group.Bounds==new UiRect(0,0,80,120),"RadioGroup root bounds honor the actual parent allocation");
        Frame(box.Render,idle with{MousePosition=new(15,48),LeftMouseDown=true,LeftMousePressed=true});
        Frame(box.Render,idle with{MousePosition=new(15,48),LeftMouseReleased=true});
        check(state.Value=="b","allocated RadioGroup preserves real option pointer selection");
        Frame(box.Render,idle with{MousePosition=new(79,16),LeftMouseDown=true,LeftMousePressed=true});
        Frame(box.Render,idle with{MousePosition=new(79,16),LeftMouseReleased=true});
        check(state.Value=="b","extra root width keeps Fluent start-aligned option hit areas rather than selecting blank space");
        Frame(box.Render,idle with{KeyEvents=[new(UiKey.UpArrow,true,default)]});
        check(state.Value=="a","allocated RadioGroup preserves focused arrow navigation");
        box.Options=new(120,32);Frame(box.Render);
        Frame(box.Render,idle with{MousePosition=new(15,48),LeftMouseDown=true,LeftMousePressed=true});
        Frame(box.Render,idle with{MousePosition=new(15,48),LeftMouseReleased=true});
        check(state.Value=="a"&&group.Bounds==new UiRect(0,0,120,32),"a short parent clips option input to the actual root allocation");

        UiRadioOption[] wrapped=[new("a","Long option label that must wrap at the real parent width",Description:"Additional descriptive text belongs to this option.")];
        using var content=new UiRadioGroupContent<UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>(wrapped);
        using var cacheControl=new CacheControl(content);using var cacheBox=Dux.SizedBox(cacheControl,100,160);
        Frame(cacheBox.Render);Frame(cacheBox.Render);
        check(content.Layouts[0].Label.Contains('\n'),"parent width constrains RadioGroup label and description wrapping even with an explicit wider request");
        var builds=content.LayoutBuildCount;var canonical=content.Layout;var canonicalBounds=content.Layouts[0].Bounds;
        cacheBox.Options=cacheBox.Options with{Height=200};Frame(cacheBox.Render);
        check(content.LayoutBuildCount==builds&&content.Layout==canonical&&content.Layouts[0].Bounds==canonicalBounds,
            "height-only RadioGroup arrangement preserves the canonical measured root and option cache without rebuilding text");
        Frame(ui=>{string? value="a";ui.RadioGroup<UiFluentRadioGroupBehavior,UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>("##natural",content,ref value,240);});
        check(content.LayoutBuildCount==builds+1&&content.Layout.Size.Y<canonical.Size.Y,"an unconstrained direct call rebuilds the correct natural-width layout after a boxed use");
        Frame(cacheBox.Render);
        check(content.LayoutBuildCount==builds+2&&content.Layout==canonical,"returning to boxed width restores its canonical layout instead of reusing arranged or direct geometry");
        content.Dispose();check(content.Layouts.Length==0&&content.DerivedTextCharacters==0,"disposing RadioGroup content releases measured rows and derived text after arrangement");

        using var unsupported=Dux.SizedBox(new UiRadioGroup<UiFluentRadioGroupBehavior,MissingArrange>(string.Empty,new UiState<string?>(null).Binding,Array.Empty<UiRadioOption>()),80,120);
        var rejected=false;Frame(ui=>{try{unsupported.MeasureConstrained(ui,new());}catch(NotSupportedException){rejected=true;}});
        check(rejected,"unsupported custom RadioGroup allocation fails during measurement before processing input");
        using var generated=Dux.SizedBox(new GeneratedAllocatedRadio(string.Empty,new UiState<string?>("a").Binding,options),80,120);
        CustomRadioPresentation.Paints=0;Frame(generated.Render);
        check(CustomRadioPresentation.Paints==2,"a user-generated RadioGroup can measure and paint its own allocation-dependent geometry through the same behavior path");
        RadioGroupWidgetTests.Run(check);
    }
    private sealed class ObservedGroup(UiBinding<string?> value,ReadOnlyMemory<UiRadioOption> items):UiFluentRadioGroup(string.Empty,value,items,240)
    {
        internal UiRect Bounds;
        protected override void RenderControl(UiImmediateContext ui){base.RenderControl(ui);var p=ui.GetItemRectMin();var s=ui.GetItemRectSize();Bounds=new(p.X,p.Y,s.X,s.Y);}
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen{public override void Render(UiImmediateContext ui)=>draw(ui);}
    private sealed class CacheControl(UiRadioGroupContent<UiFluentRadioGroupPresentation<UiRadioVerticalLayout>> content):UiControlWidget
    {
        private string? _value="a";
        protected override UiVector2 MeasureContent(UiControlContext context)=>context.MeasureRadioGroup(string.Empty,240,content);
        protected override void RenderControl(UiImmediateContext ui)=>ui.RadioGroup<UiFluentRadioGroupBehavior,UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>("##cache",content,ref _value,240,true);
    }
    private readonly struct MissingArrange:IUiRadioGroupPresentation
    {
        public static UiRadioGroupLayout Measure(UiControlContext context,string label,ReadOnlySpan<UiRadioOption> items,Span<UiRadioOptionLayout> layouts,float width)=>new(new(60,32),default,string.Empty);
        public static void PaintLabel(UiControlContext context,UiRadioGroupLayout layout,UiRect bounds){}
        public static void PaintOption(UiControlContext context,UiRadioOptionLayout layout,bool selected,UiButtonInteraction interaction){}
    }
}
