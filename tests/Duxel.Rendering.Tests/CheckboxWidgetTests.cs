using Duxel.Core;
using System.Text.Json;

internal static class CheckboxWidgetTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        ControlFontFixture.Configure(context);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        bool? value=null;var writes=0;
        using var widget=new InheritedCheckbox(new(()=>value,next=>{value=next;writes++;}));
        var disabled=false;var size=default(UiVector2);
        void Step(UiInputState? input=null)
        {
            context.NewFrame(new(1f/60,new(320,240),new(1,1)),input??idle,new(0,0,320,240),UiTextSettings.Default);
            context.Render(new Screen(ui=>{ui.SetCursorScreenPos(new(10,10));ui.BeginDisabled(disabled);widget.Render(ui);ui.EndDisabled();size=ui.GetItemRectSize();}));
            using var data=context.GetDrawData();
        }
        Step();check(size.Y==36,"generated large Checkbox reserves36px root with20px indicator and14/20 label");
        Step(idle with{MousePosition=new(20,20),LeftMouseDown=true,LeftMousePressed=true});
        check(value is null&&writes==0,"mixed Checkbox preserves selection until release even when label is before indicator");
        Step(idle with{MousePosition=new(20,20),LeftMouseReleased=true});Step();
        check(value==true&&writes==1&&widget.Changes==1,"label hit target and inherited callback select mixed Checkbox once");
        Step(idle with{KeyEvents=[new(UiKey.Space,true,default)]});
        check(value==true,"Checkbox Space press waits for release");
        Step(idle with{KeyEvents=[new(UiKey.Space,false,default)]});Step();
        check(value==false&&writes==2,"Checkbox Space release toggles focused selection once");
        Step(idle with{KeyEvents=[new(UiKey.Enter,true,default)]});
        Step(idle with{KeyEvents=[new(UiKey.Enter,false,default)]});
        check(value==false&&writes==2,"Checkbox Enter does not inherit button activation");
        disabled=true;
        Step(idle with{MousePosition=new(20,20),LeftMouseDown=true,LeftMousePressed=true});
        Step(idle with{MousePosition=new(20,20),LeftMouseReleased=true});
        check(value==false&&writes==2,"disabled generated Checkbox rejects label clicks");
        foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())
        foreach(var state in Enum.GetValues<UiButtonVisualState>())
        {
            var mixed=UiFluentCheckboxPalette.Resolve(scheme,state,null);
            var selected=UiFluentCheckboxPalette.Resolve(scheme,state,true);
            var off=UiFluentCheckboxPalette.Resolve(scheme,state,false);
            check((mixed.Background.Rgba>>24)==0&&(off.Background.Rgba>>24)==0,"mixed/unchecked Checkbox backgrounds remain transparent: "+scheme+"/"+state);
            check(state==UiButtonVisualState.Disabled?(selected.Background.Rgba>>24)==0:(selected.Background.Rgba>>24)==255,
                "checked Checkbox fills only when enabled: "+scheme+"/"+state);
        }
        var after=UiCheckboxLabelAfter.Measure(16,new(40,20),true);
        var before=UiCheckboxLabelBefore.Measure(16,new(40,20),true);
        check(after.Size==before.Size&&after.LabelOffset.X==36&&before.LabelOffset.X==8&&before.Indicator.X==60,
            "label position implementations preserve overall extent and official independent slot padding");
        using var reference=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","CheckboxPalette.json")));
        foreach(var item in reference.RootElement.EnumerateArray())
        {
            var scheme=Enum.Parse<UiFluentColorScheme>(item.GetProperty("scheme").GetString()!);
            var state=Enum.Parse<UiButtonVisualState>(item.GetProperty("state").GetString()!);
            var selected=item.GetProperty("value").GetString();
            var paint=UiFluentCheckboxPalette.Resolve(scheme,state,selected=="mixed"?null:selected=="true");
            string Hex(UiColor color)=>$"{color.Rgba&255:X2}{(color.Rgba>>8)&255:X2}{(color.Rgba>>16)&255:X2}{color.Rgba>>24:X2}";
            check(Hex(paint.Background)==item.GetProperty("background").GetString()
                &&Hex(paint.Border)==item.GetProperty("border").GetString()
                &&Hex(paint.Indicator)==item.GetProperty("indicator").GetString()
                &&Hex(paint.Label)==item.GetProperty("label").GetString(),
                "Checkbox exact pinned token expressions: "+scheme+"/"+state+"/"+selected);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render):UiScreen
    {public override void Render(UiImmediateContext ui)=>render(ui);}
    private sealed class InheritedCheckbox(UiBinding<bool?> value):CompiledLargeCircleBefore("Checkbox",value)
    {
        internal int Changes;
        protected override void OnChanged(bool? value){Changes++;base.OnChanged(value);}
    }
}

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool?),typeof(UiNullableToggleValue),typeof(UiSpaceButtonBehavior),
    typeof(UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize,UiFluentCircularCheckboxShape,UiCheckboxLabelBefore>))]
internal partial class CompiledLargeCircleBefore;
