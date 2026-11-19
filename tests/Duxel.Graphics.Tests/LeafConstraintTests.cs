using Duxel.Core;

internal static class LeafConstraintTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        ControlFontFixture.Configure(context);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        void Frame(Action<UiImmediateContext> draw,UiInputState? input=null)
        {
            context.NewFrame(new(1f/60,new(300,200),new(1,1)),input??idle,new(0,0,300,200),UiTextSettings.Default);
            context.Render(new Screen(ui=>{ui.EnableRootViewportContentLayout(contentPadding:0);ui.SetCursorScreenPos(default);draw(ui);}));using var data=context.GetDrawData();
        }
        var state=new UiState<float>(0);var slider=new ObservedSlider(state.Binding);using var box=Dux.SizedBox(slider,80,64);
        Frame(ui=>{check(box.MeasureConstrained(ui,UiBoxConstraints.Loose(new(300,200)))==new UiVector2(80,64),"SizedBox reports the parent's80x64 Slider allocation");box.Render(ui);});
        check(slider.Bounds==new UiRect(0,0,80,64),"Slider paint and hit bounds match its80x64 parent allocation instead of its natural120x32 size");
        Frame(box.Render,idle with{MousePosition=new(70,58),LeftMouseDown=true,LeftMousePressed=true});
        check(state.Value==100,"a short allocated Slider reaches its maximum at the actual rail endpoint across the full hit height");
        Frame(box.Render,idle with{MousePosition=new(70,58),LeftMouseReleased=true});
        Frame(box.Render,idle with{KeyEvents=[new(UiKey.Home,true,default)]});
        check(state.Value==0,"an allocated Slider preserves Home keyboard behavior after pointer capture ends");
        Frame(box.Render,idle with{KeyEvents=[new(UiKey.End,true,default)]});
        check(state.Value==100,"an allocated Slider preserves End keyboard behavior");
        slider.Options=slider.Options with{Vertical=true};box.Options=new(64,80);
        Frame(box.Render,idle with{MousePosition=new(58,10),LeftMouseDown=true,LeftMousePressed=true});
        check(slider.Bounds==new UiRect(0,0,64,80)&&state.Value==100,"a vertical allocated Slider uses the actual cross-axis bounds and top endpoint");
        Frame(box.Render,idle with{MousePosition=new(58,70),LeftMouseDown=true});
        check(state.Value==0,"a vertical allocated Slider reaches the actual bottom endpoint while dragging");
        Frame(box.Render,idle with{MousePosition=new(58,70),LeftMouseReleased=true});
        slider.Options=slider.Options with{Vertical=false};box.Options=new(10,32);state.Value=50;
        Frame(box.Render,idle with{MousePosition=new(5,20),LeftMouseDown=true,LeftMousePressed=true});
        check(state.Value==50&&slider.Bounds==new UiRect(0,0,10,32),"a zero-travel allocated rail keeps a finite thumb and ignores undefined pointer movement");
        Frame(box.Render,idle with{MousePosition=new(5,20),LeftMouseReleased=true});
        Frame(box.Render,idle with{KeyEvents=[new(UiKey.End,true,default)]});
        check(state.Value==100,"zero-travel Sliders remain keyboard-operable without dividing by zero");
        var toggleState=new UiState<bool?>(false);var toggle=new ObservedCheckbox(toggleState.Binding);using var toggleBox=Dux.SizedBox(toggle,180,64);
        Frame(toggleBox.Render);check(toggle.Bounds==new UiRect(0,0,180,64),"Checkbox paint and hit bounds fill the parent's declared allocation");
        Frame(toggleBox.Render,idle with{MousePosition=new(160,50),LeftMouseDown=true,LeftMousePressed=true});
        Frame(toggleBox.Render,idle with{MousePosition=new(160,50),LeftMouseReleased=true});
        check(toggleState.Value==true,"the trailing allocated Checkbox region activates the same bound value once");
        Frame(toggleBox.Render,idle with{KeyEvents=[new(UiKey.Space,true,default)]});
        Frame(toggleBox.Render,idle with{KeyEvents=[new(UiKey.Space,false,default)]});
        check(toggleState.Value==false,"an allocated Checkbox retains Space-release keyboard activation");
        var switchState=new UiState<bool>(false);var toggleSwitch=new ObservedSwitch(switchState.Binding);using var switchBox=Dux.SizedBox(toggleSwitch,180,64);
        Frame(switchBox.Render,idle with{MousePosition=new(160,50),LeftMouseDown=true,LeftMousePressed=true});
        Frame(switchBox.Render,idle with{MousePosition=new(160,50),LeftMouseReleased=true});
        check(switchState.Value&&toggleSwitch.Bounds==new UiRect(0,0,180,64),"an allocated Switch retains its bound value and full trailing hit region");
        using var unsupported=Dux.SizedBox(new UiSlider<UiFluentSliderBehavior,FixedPresentation>(string.Empty,state.Binding),80,64);
        var rejected=false;Frame(ui=>{try{unsupported.MeasureConstrained(ui,new());}catch(NotSupportedException){rejected=true;}});
        check(rejected,"custom presentations must explicitly arrange changed parent allocations instead of silently drawing natural geometry");
        using var bad=Dux.SizedBox(new UiSlider<UiFluentSliderBehavior,BadPresentation>(string.Empty,state.Binding),80,64);
        rejected=false;Frame(ui=>{try{bad.MeasureConstrained(ui,new());}catch(InvalidOperationException){rejected=true;}});
        check(rejected,"a custom Arrange that ignores its assigned size is rejected during measurement");
        using var user=Dux.SizedBox(new GeneratedAdaptiveSlider(string.Empty,state.Binding),80,64);
        AdaptivePresentation.Arrangements=0;Frame(user.Render);
        check(AdaptivePresentation.Arrangements>0,"a generated user presentation consumes its own closed Arrange implementation");
        using var custom=Dux.SizedBox(new CustomControl(),80,64);Frame(custom.Render);
        check(CustomControl.Bounds==new UiVector2(80,64),"a user control can opt into the same public constrained Slider path");
        using var legacy=Dux.SizedBox(new CustomControl(false),80,64);Frame(legacy.Render);
        check(CustomControl.Bounds==new UiVector2(120,32),"existing public immediate Slider calls preserve their natural geometry without explicit layout opt-in");
        SliderWidgetTests.Run(check);CheckboxWidgetTests.Run(check);SwitchWidgetTests.Run(check);
    }
    private sealed class ObservedSlider(UiBinding<float> value):UiFluentSlider(string.Empty,value)
    {
        internal UiRect Bounds;
        protected override void RenderControl(UiImmediateContext ui){base.RenderControl(ui);var p=ui.GetItemRectMin();var s=ui.GetItemRectSize();Bounds=new(p.X,p.Y,s.X,s.Y);}
    }
    private sealed class ObservedCheckbox(UiBinding<bool?> value):UiToggle<bool?,UiNullableToggleValue,UiReleaseButtonBehavior,UiFluentCheckboxPresentation>(string.Empty,value)
    {
        internal UiRect Bounds;
        protected override void RenderControl(UiImmediateContext ui){base.RenderControl(ui);var p=ui.GetItemRectMin();var s=ui.GetItemRectSize();Bounds=new(p.X,p.Y,s.X,s.Y);}
    }
    private sealed class ObservedSwitch(UiBinding<bool> value):UiToggle<bool,UiBooleanToggleValue,UiReleaseButtonBehavior,UiFluentSwitchPresentation>(string.Empty,value)
    {
        internal UiRect Bounds;
        protected override void RenderControl(UiImmediateContext ui){base.RenderControl(ui);var p=ui.GetItemRectMin();var s=ui.GetItemRectSize();Bounds=new(p.X,p.Y,s.X,s.Y);}
    }
    private sealed class CustomControl(bool constrained=true):UiControlWidget
    {
        internal static UiVector2 Bounds;private float _value;
        protected override UiVector2 MeasureContent(UiControlContext context)=>context.MeasureSlider<UiFluentSliderPresentation>(new(0,100),default);
        protected override void RenderControl(UiImmediateContext ui)
        {ui.Slider<UiFluentSliderBehavior,UiFluentSliderPresentation>("custom",ref _value,new(0,100),new(120,32),constrained);Bounds=ui.GetItemRectSize();}
    }
    private readonly struct FixedPresentation:IUiSliderPresentation
    {
        public static UiSliderLayout Measure(UiControlContext context,UiSliderOptions options,UiVector2 requestedSize)=>new(new(60,60),new(10,29,40,2),10,3);
        public static void Paint(UiControlContext context,UiRect bounds,UiSliderLayout layout,UiSliderOptions options,UiSliderInteraction interaction){}
    }
    private readonly struct BadPresentation:IUiSliderPresentation
    {
        public static UiSliderLayout Measure(UiControlContext context,UiSliderOptions options,UiVector2 requestedSize)=>FixedPresentation.Measure(context,options,requestedSize);
        public static UiSliderLayout Arrange(UiControlContext context,UiSliderLayout layout,UiSliderOptions options,UiVector2 allocatedSize)=>layout;
        public static void Paint(UiControlContext context,UiRect bounds,UiSliderLayout layout,UiSliderOptions options,UiSliderInteraction interaction){}
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen{public override void Render(UiImmediateContext ui)=>draw(ui);}
}

internal readonly struct AdaptivePresentation:IUiSliderPresentation
{
    internal static int Arrangements;
    public static UiSliderLayout Measure(UiControlContext context,UiSliderOptions options,UiVector2 requestedSize)=>new(new(60,60),new(10,29,40,2),10,3);
    public static UiSliderLayout Arrange(UiControlContext context,UiSliderLayout layout,UiSliderOptions options,UiVector2 allocatedSize)
    {Arrangements++;return layout with{Size=allocatedSize,Track=new(10,(allocatedSize.Y-2)/2,MathF.Max(0,allocatedSize.X-20),2)};}
    public static void Paint(UiControlContext context,UiRect bounds,UiSliderLayout layout,UiSliderOptions options,UiSliderInteraction interaction)
        =>context.Circle(new(bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2),MathF.Min(bounds.Width,bounds.Height)/2,context.Theme.CheckMark);
}
[UiWidgetStyle(typeof(UiSlider<,>),typeof(UiFluentSliderBehavior),typeof(AdaptivePresentation))]
internal partial class GeneratedAdaptiveSlider;
