using Duxel.Core;

internal static class InputAllocationTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));ControlFontFixture.Configure(context);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        void Frame(Action<UiImmediateContext> draw,UiInputState? input=null)
        {
            context.NewFrame(new(1f/60,new(300,200),new(1,1)),input??idle,new(0,0,300,200),UiTextSettings.Default);
            context.Render(new Screen(ui=>{ui.EnableRootViewportContentLayout(contentPadding:0);ui.SetCursorScreenPos(default);draw(ui);}));using var data=context.GetDrawData();
        }
        var state=new UiState<string>("a");var input=new ObservedInput(state.Binding);using var box=Dux.SizedBox(input,80,64);
        Frame(ui=>{check(box.MeasureConstrained(ui,new())==new UiVector2(80,64),"SizedBox reports the80x64 Input allocation");box.Render(ui);});
        check(input.Bounds==new UiRect(0,0,80,64),"Input actual field and hit bounds honor the parent's80x64 allocation");
        Frame(box.Render,idle with{MousePosition=new(40,54),LeftMouseDown=true,LeftMousePressed=true});
        check(context.State.ActiveId is not null,"the lower allocated Input area focuses the actual text editor");
        Frame(box.Render,idle with{MousePosition=new(40,54),LeftMouseReleased=true});
        Frame(box.Render,idle with{KeyEvents=[new(UiKey.End,true,default)],CharEvents=[new('x')]});
        check(state.Value=="ax","the allocated editor preserves text input and its actual binding");
        var active=context.State.ActiveId;box.Options=new(160,48);Frame(box.Render);
        check(input.Bounds==new UiRect(0,0,160,48)&&context.State.ActiveId==active,"live parent changes preserve the actual input owner and active editor");
        Frame(box.Render,idle with{KeyEvents=[new(UiKey.Z,true,KeyModifiers.Ctrl)],Modifiers=KeyModifiers.Ctrl});
        check(state.Value=="a","parent allocation changes preserve undo history");
        box.Options=new(0,0);Frame(box.Render);check(input.Bounds==new UiRect(0,0,0,0),"zero-sized Input allocations keep valid field geometry without reducing typography");
        box.Options=new(80,10);Frame(box.Render);check(input.Bounds==new UiRect(0,0,80,10),"short Input allocations clip a full-size text line instead of changing font quality");

        var childCalls=0;var prefix=new ChildButton(string.Empty,()=>childCalls++){Size=new(24,24)};
        var suffix=new ChildButton(string.Empty,()=>childCalls++){Size=new(24,24)};
        var slots=Fluent.Input(string.Empty,new UiState<string>("slots").Binding,64,240);
        slots.SetSlots(new("before",prefix,new(24,24)),new("after",suffix,new(24,24)));
        using var slotsBox=Dux.SizedBox(slots,160,64);Frame(slotsBox.Render);
        check(prefix.Bounds==new UiRect(11,20,24,24)&&suffix.Bounds==new UiRect(125,20,24,24),"allocated Input repositions fixed-size prefix and suffix children using the assigned field dimensions");
        Frame(slotsBox.Render,idle with{MousePosition=new(132,28),LeftMouseDown=true,LeftMousePressed=true});
        Frame(slotsBox.Render,idle with{MousePosition=new(132,28),LeftMouseReleased=true});
        check(childCalls==1,"a repositioned suffix child retains exactly one pointer callback");
        slotsBox.Options=new(120,48);Frame(slotsBox.Render);
        check(prefix.Bounds==new UiRect(11,12,24,24)&&suffix.Bounds==new UiRect(85,12,24,24),"resizing a parent preserves slot widgets while updating their real allocations");
        slotsBox.Dispose();check(prefix.Disposed&&suffix.Disposed,"disposing an allocated Input releases its actual owned slot tree");

        using var missing=Dux.SizedBox(new UiInput<UiFluentTextInputBehavior,MissingArrange>(string.Empty,new UiState<string>(string.Empty).Binding,64),80,64);
        var rejected=false;Frame(ui=>{try{missing.MeasureConstrained(ui,new());}catch(NotSupportedException){rejected=true;}});
        check(rejected,"custom Input presentations explicitly reject changed allocations without Arrange support");
        var measured=Fluent.Input(string.Empty,new UiState<string>(string.Empty).Binding,64);using var afterFailure=Dux.SizedBox(measured,100,40);Frame(afterFailure.Render);
        check(context.State.ActiveId is null,"a failed custom Input measurement restores the shared editor context for the next control");
        box.Dispose();var remaining=context.State.TextInputStorage;afterFailure.Dispose();
        check(remaining.Item1>0&&context.State.TextInputStorage==(0,0),"each allocated Input releases its own edit storage while a different live owner keeps its state until disposal");
        using var adorned=new UiInput<UiFluentTextInputBehavior,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentOutlineInput,CountedAdornment,UiInputTextAdornment>>(
            string.Empty,new UiState<string>(string.Empty).Binding,64){Options=new(){ContentBefore="fixed"}};
        using var adornedBox=Dux.SizedBox(adorned,160,64);CountedAdornment.Measures=0;Frame(adornedBox.Render);
        check(CountedAdornment.Measures==2,"Input Arrange reuses measured adornment extents instead of invoking its measurement again in measure and render phases");
        InputKernelTests.Run(check);FluentInputTests.Run(check);InputAdornmentTests.Run(check);InputChildrenTests.Run(check);FieldWidgetTests.Run(check);
    }
    private sealed class ObservedInput(UiBinding<string> value):UiFluentInput(string.Empty,value,64)
    {
        internal UiRect Bounds;
        protected override void RenderControl(UiImmediateContext ui){base.RenderControl(ui);var p=ui.GetItemRectMin();var s=ui.GetItemRectSize();Bounds=new(p.X,p.Y,s.X,s.Y);}
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen{public override void Render(UiImmediateContext ui)=>draw(ui);}
    private readonly struct MissingArrange:IUiInputPresentation
    {
        public static UiInputLayout Measure(UiControlContext context,string label,float width)=>new(new(80,32),new(0,0,80,32),new(4,4,72,20),default,20);
        public static void Paint(UiControlContext context,string label,in UiInputLayout layout,in UiInputEditResult edit){}
    }
    private readonly struct CountedAdornment:IUiInputAdornment
    {
        internal static int Measures;
        public static bool IsPresent(string? content)=>content is not null;
        public static UiVector2 Measure(UiControlContext context,string? content,float iconSize){Measures++;return new(20,12);}
        public static void Paint(UiControlContext context,string? content,UiRect bounds,UiColor color,in UiInputEditResult edit)=>context.Frame(bounds,color,color,0,0);
    }
}
