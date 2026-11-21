using Duxel.Core;

internal static class ButtonAppearanceInputTests
{
    internal static void Run(Action<bool,string> check)
    {
        for(var appearance=0;appearance<6;appearance++)
        {
            using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
            ControlFontFixture.Configure(context);
            var calls=0;Action activated=()=>calls++;
            using UiWidget? button=appearance switch
            {
                0=>new AppearanceInputPrimary("UI",activated),1=>new AppearanceInputOutline("UI",activated),
                2=>new AppearanceInputSubtle("UI",activated),3=>new AppearanceInputTransparent("UI",activated),
                4=>new InheritedButton(activated),_=>null
            };
            var route=button?.GetType().Name??"ui.Button";
            var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
            var disabled=false;var focused=false;
            void Frame(UiInputState input)
            {
                context.NewFrame(new(1f/60,new(320,240),new(1,1)),input,new(0,0,320,240),UiTextSettings.Default);
                context.Render(new Screen(ui=>
                {
                    ui.SetCursorScreenPos(new(10,10));ui.BeginDisabled(disabled);
                    try
                    {
                        if (button is not null)
                            button.Render(ui);
                        else if (ui.Button("UI"))
                            activated();
                        focused=ui.IsItemFocused();
                    }
                    finally{ui.EndDisabled();}
                }));
                context.GetDrawData().ReleasePooled();
            }
            void Key(UiKey key,bool down=true)=>Frame(idle with {KeyEvents=[new UiKeyEvent(key,down,default)]});
            Frame(idle);
            var press=idle with {MousePosition=new(30,25),LeftMouseDown=true,LeftMousePressed=true};
            Frame(press);Frame(press with {LeftMousePressed=false});Frame(press with {LeftMousePressed=false});
            check(calls==0,"button keeps release behavior while the pointer is held: "+route);
            Frame(press with {LeftMouseDown=false,LeftMousePressed=false,LeftMouseReleased=true});Frame(idle);
            check(calls==1,"button activates once on pointer release: "+route);
            Frame(press);Frame(idle with {LeftMouseReleased=true});Frame(idle);
            check(calls==1,"button cancels a release outside its bounds: "+route);
            Key(UiKey.Tab);check(focused,"button retains keyboard navigation identity: "+route);
            Key(UiKey.Enter);Key(UiKey.Enter,false);
            check(calls==2,"button preserves one Enter activation on key down: "+route);
            Key(UiKey.Space);Frame(idle);Key(UiKey.Space);check(calls==2,"button holds Space without repeat activation: "+route);
            Key(UiKey.Space,false);check(calls==3,"button preserves one Space release activation: "+route);
            disabled=true;Frame(idle);Frame(press);Frame(press with {LeftMouseDown=false,LeftMousePressed=false,LeftMouseReleased=true});
            Key(UiKey.Enter);Key(UiKey.Enter,false);Key(UiKey.Space);Key(UiKey.Space,false);
            check(calls==3,"disabled button rejects pointer and keyboard activation: "+route);
        }
    }
    private sealed class InheritedButton(Action activated):UiButton("UI")
    {
        protected override void OnActivated()=>activated();
    }
    private sealed class Screen(Action<UiImmediateContext> render):UiScreen
    {public override void Render(UiImmediateContext ui)=>render(ui);}
}

[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance>))]
internal partial class AppearanceInputPrimary;
[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentButtonPresentation<UiFluentOutlineButtonAppearance>))]
internal partial class AppearanceInputOutline;
[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentButtonPresentation<UiFluentSubtleButtonAppearance>))]
internal partial class AppearanceInputSubtle;
[UiButtonStyle(typeof(UiReleaseButtonBehavior),typeof(UiFluentButtonPresentation<UiFluentTransparentButtonAppearance>))]
internal partial class AppearanceInputTransparent;
