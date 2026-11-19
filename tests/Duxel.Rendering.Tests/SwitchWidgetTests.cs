using Duxel.Core;

internal static class SwitchWidgetTests
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(var small in new[]{false,true})foreach(var position in new[]{0,1,2})
        {
            var track=small?new UiVector2(32,16):new UiVector2(40,20);
            var text=new UiVector2(60,small?16:20);
            var layout=position switch{0=>UiSwitchLabelAfter.Measure(track,text,true),1=>UiSwitchLabelBefore.Measure(track,text,true),_=>UiSwitchLabelAbove.Measure(track,text,true)};
            check(layout.Indicator.Width==track.X&&layout.Indicator.Height==track.Y,"Switch exact track dimensions: "+small+"/"+position);
            check(position==2?layout.LabelOffset.Y==4&&layout.Indicator.Y==text.Y+8:
                layout.LabelOffset.Y==8&&layout.Indicator.Y==8,"Switch label and track vertical placement: "+small+"/"+position);
            check(position==2?layout.Size==new UiVector2(76,text.Y+track.Y+16):layout.Size==new UiVector2(track.X+88,track.Y+16),
                "Switch root includes official padding and label arrangement: "+small+"/"+position);
            var empty=position switch{0=>UiSwitchLabelAfter.Measure(track,new(0,999),false),1=>UiSwitchLabelBefore.Measure(track,new(0,999),false),_=>UiSwitchLabelAbove.Measure(track,new(0,999),false)};
            check(empty.Size==new UiVector2(track.X+16,track.Y+16),"unlabelled Switch ignores ambient text height: "+small+"/"+position);
        }
        foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())foreach(var state in Enum.GetValues<UiButtonVisualState>())
        {
            var on=UiFluentSwitchPalette.Resolve(scheme,state,true);var off=UiFluentSwitchPalette.Resolve(scheme,state,false);
            check((on.Border.Rgba>>24)==0&&(off.Background.Rgba>>24)==0,"Switch checked border/unchecked surface stay transparent: "+scheme+"/"+state);
            check((off.Thumb.Rgba>>24)==255&&on.Label==off.Label,"Switch thumb remains visible and label ignores checked state: "+scheme+"/"+state);
            check(on.Thumb!=on.Background,"Switch checked thumb contrasts with track: "+scheme+"/"+state);
        }
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        ControlFontFixture.Configure(context);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var value=false;var writes=0;var disabled=false;
        using var widget=new InheritedSwitch(new(()=>value,next=>{value=next;writes++;}));
        void Step(UiInputState? input=null)
        {
            context.NewFrame(new(1f/60,new(320,240),new(1,1)),input??idle,new(0,0,320,240),UiTextSettings.Default);
            context.Render(new Screen(ui=>{ui.SetCursorScreenPos(new(10,10));ui.BeginDisabled(disabled);widget.Render(ui);ui.EndDisabled();}));
            using var data=context.GetDrawData();
        }
        Step();Step();check(writes==0,"Switch idle frames do not write the binding");
        Step(idle with{MousePosition=new(20,20),LeftMouseDown=true,LeftMousePressed=true});
        check(!value&&writes==0,"Switch above label waits for pointer release");
        Step(idle with{MousePosition=new(20,20),LeftMouseReleased=true});Step();
        check(value&&writes==1&&widget.Changes==1,"Switch above label activates inherited callback exactly once");
        Step(idle with{KeyEvents=[new(UiKey.Space,true,default)]});check(value,"Switch Space press waits for release");
        Step(idle with{KeyEvents=[new(UiKey.Space,false,default)]});Step();check(!value&&writes==2,"Switch Space release changes selection exactly once");
        Step(idle with{KeyEvents=[new(UiKey.Enter,true,default)]});Step(idle with{KeyEvents=[new(UiKey.Enter,false,default)]});
        check(!value&&writes==2,"Switch Enter does not activate checkbox-style input");
        disabled=true;Step(idle with{MousePosition=new(20,20),LeftMouseDown=true,LeftMousePressed=true});Step(idle with{MousePosition=new(20,20),LeftMouseReleased=true});
        check(!value&&writes==2,"disabled Switch label cannot activate");
        using var standard=Fluent.Switch("Setting",new UiState<bool>(false).Binding);
        check(standard is UiFluentSwitch,"standard Switch factory still consumes generated implementation");
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen{public override void Render(UiImmediateContext ui)=>draw(ui);}
    private sealed class InheritedSwitch(UiBinding<bool> value):SwitchAbove("Setting",value)
    {
        internal int Changes;
        protected override void OnChanged(bool value){Changes++;base.OnChanged(value);}
    }
}

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiBooleanToggleValue),typeof(UiSpaceButtonBehavior),typeof(UiFluentSwitchPresentation<UiFluentSmallSwitchSize,UiSwitchLabelAbove>))]
internal partial class SwitchAbove;
