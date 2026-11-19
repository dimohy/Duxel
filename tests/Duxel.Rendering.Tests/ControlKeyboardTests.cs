using Duxel.Core;

internal static class ControlKeyboardTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        ControlFontFixture.Configure(context);
        using var first=new ProbeButton(0);using var second=new ProbeButton(1);using var third=new ProbeButton(2);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var hideFirst=false;var disableFirst=false;var firstFocused=false;var thirdFocused=false;
        var firstActive=false;var firstActivated=false;var firstDeactivated=false;
        UiImmediateContext? lastUi=null;
        void Render(UiImmediateContext ui)
        {
            lastUi=ui;
            if(!hideFirst)
            {
                ui.SetCursorScreenPos(new(10,10));ui.BeginDisabled(disableFirst);first.Render(ui);ui.EndDisabled();
                firstFocused=ui.IsItemFocused();
                firstActive=ui.IsItemActive();firstActivated=ui.IsItemActivated();firstDeactivated=ui.IsItemDeactivatedAfterEdit();
            }
            ui.SetCursorScreenPos(new(10,60));ui.BeginDisabled();second.Render(ui);ui.EndDisabled();
            ui.SetCursorScreenPos(new(10,110));third.Render(ui);thirdFocused=ui.IsItemFocused();
        }
        void Frame(UiInputState? input=null,Action<UiImmediateContext>? render=null)
        {
            context.NewFrame(new(1f/60,new(320,240),new(1,1)),input??idle,new(0,0,320,240),UiTextSettings.Default);
            context.Render(new Screen(render??Render));context.GetDrawData().ReleasePooled();
        }
        void Key(UiKey key,bool down=true,KeyModifiers modifiers=default)=>
            Frame(idle with {KeyEvents=new[]{new UiKeyEvent(key,down,modifiers)},Modifiers=modifiers});
        var requests=0;context.SetRequestFrameCallback(()=>requests++);
        Frame();requests=0;Frame();
        check(requests==0,"unchanged controls without keyboard focus do not request another frame");
        Key(UiKey.Tab);
        check(firstFocused&&first.Interaction.FocusVisible,"Tab focuses the first enabled compiled control and exposes its focus visual");
        Key(UiKey.Enter);check(first.Activations==1&&third.Activations==0,"Enter invokes only the focused inherited button");
        Key(UiKey.Enter,false);check(first.Activations==1,"Enter release does not invoke the button twice");
        Key(UiKey.Space);
        check(firstActive&&firstActivated,"keyboard Space exposes the existing item active and activated queries");
        Frame();
        check(first.Interaction.Held&&first.Activations==1,"Space holds the focused button across frames without pointer hover");
        Key(UiKey.Space);Key(UiKey.Space,false);
        check(first.Activations==2&&!first.Interaction.Held&&firstDeactivated,"repeated Space down events produce one activation and an edited deactivation on release");
        Key(UiKey.Tab);check(thirdFocused&&!firstFocused,"Tab skips disabled controls in the generated control sequence");
        Key(UiKey.Tab);check(firstFocused,"forward Tab wraps inside the available control sequence");
        Key(UiKey.Tab,modifiers:KeyModifiers.Shift);check(thirdFocused,"Shift Tab moves backward and wraps");
        Key(UiKey.Tab,modifiers:KeyModifiers.Ctrl);check(thirdFocused,"Ctrl Tab is not consumed as ordinary control navigation");
        Key(UiKey.Tab);Key(UiKey.Space);Key(UiKey.Escape);Key(UiKey.Space);Key(UiKey.Space,false);
        check(first.Activations==2,"Escape cancels an armed Space activation");
        Key(UiKey.Space);disableFirst=true;Key(UiKey.Space,false);
        check(first.Activations==2&&!lastUi!.IsAnyItemFocused(),"disabling a focused control cancels its pending activation and releases owned focus");
        disableFirst=false;Frame();Key(UiKey.Tab);Key(UiKey.Space);hideFirst=true;Frame();Key(UiKey.Space,false);
        check(first.Activations==2&&!lastUi!.IsAnyItemFocused(),"removing the focused control cancels its pending activation without activating a sibling");
        hideFirst=false;Frame();Key(UiKey.Tab);Key(UiKey.Space);context.AddFocusEvent(false);Key(UiKey.Space,false);
        check(first.Activations==2,"native focus loss cancels a pending keyboard activation");
        Key(UiKey.Tab);Key(UiKey.Space);
        Frame(idle with {MousePosition=new(20,120),LeftMouseDown=true,LeftMousePressed=true});
        check(thirdFocused&&!third.Interaction.FocusVisible,"pointer press transfers focus and hides the keyboard focus visual");
        Frame(idle with {MousePosition=new(20,120),LeftMouseReleased=true});
        Key(UiKey.Space,false);
        check(third.Activations==1&&first.Activations==2,"pointer focus changes cannot transfer an armed Space activation");

        var inputValue="";
        void WithTextInput(UiImmediateContext ui)
        {
            Render(ui);ui.SetCursorScreenPos(new(10,170));ui.InputText("##editor",ref inputValue,64);
        }
        Frame(idle with {MousePosition=new(20,180),LeftMouseDown=true,LeftMousePressed=true},WithTextInput);
        Frame(idle with {KeyEvents=new[]{new UiKeyEvent(UiKey.Enter,true,default)}},WithTextInput);
        check(third.Activations==1&&!lastUi!.IsAnyItemFocused(),"clicking a text editor releases button focus before keyboard input can activate it");
        Frame(idle with {KeyEvents=new[]{new UiKeyEvent(UiKey.Tab,true,default)}},WithTextInput);
        Frame(idle with {KeyEvents=new[]{new UiKeyEvent(UiKey.Enter,true,default)}},WithTextInput);
        check(first.Activations==3,"Tab can transfer keyboard control from an active editor to the first registered button");

        var value=new UiState<bool>(false);using var checkbox=Fluent.Checkbox("Checkbox",value.Binding);
        void Checkbox(UiImmediateContext ui){lastUi=ui;checkbox.Render(ui);ui.SetKeyboardFocusHere();}
        Frame(render:Checkbox);Frame(idle with {KeyEvents=new[]{new UiKeyEvent(UiKey.Enter,true,default)}},Checkbox);
        check(!value.Value,"the generated Checkbox selects a Space-only keyboard policy at compile time");
        Frame(idle with {KeyEvents=new[]{new UiKeyEvent(UiKey.Space,true,default)}},Checkbox);
        Frame(idle with {KeyEvents=new[]{new UiKeyEvent(UiKey.Space,false,default)}},Checkbox);
        check(value.Value,"the generated Checkbox uses its existing value transition on Space release");

        var many=Enumerable.Range(0,10000).Select(_=>Fluent.Button("Button")).ToArray();
        try
        {
            Frame(render:ui=>{lastUi=ui;foreach(var button in many){ui.SetCursorScreenPos(new(10,10));button.Render(ui);}});
            check(lastUi!.ControlNavigationStorage.References==10000,"navigation retains only the current ten-thousand-control order");
            for(var i=0;i<121;i++)Frame(render:ui=>lastUi=ui);
            check(lastUi!.ControlNavigationStorage.References==0&&lastUi.ControlNavigationStorage.Capacity<=64,
                "removed navigation identities are released and oversized buffers shrink after120 small frames");
            context.Dispose();check(lastUi.ControlNavigationStorage.Capacity==0,"context disposal releases all navigation buffers");
        }
        finally{foreach(var button in many)button.Dispose();}
    }
    private sealed class ProbeButton:UiButton<UiReleaseButtonBehavior,ProbePresentation>
    {
        private readonly int _index;
        internal int Activations;
        internal UiButtonInteraction Interaction=>ProbePresentation.Last[_index];
        internal ProbeButton(int index):base("Button"){_index=index;}
        protected override void OnActivated()=>Activations++;
    }
    private readonly struct ProbePresentation:IUiButtonPresentation
    {
        internal static readonly UiButtonInteraction[] Last=new UiButtonInteraction[3];
        public static UiVector2 Measure(UiControlContext context,UiVector2 textSize,UiVector2 requestedSize)=>new(96,32);
        public static void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,UiButtonInteraction interaction)=>
            Last[(int)((bounds.Y-10)/50)]=interaction;
    }
    private sealed class Screen(Action<UiImmediateContext> render):UiScreen
    {public override void Render(UiImmediateContext ui)=>render(ui);}
}
