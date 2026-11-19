using Duxel.Core;

internal static class SliderWidgetTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var value=50f;var reads=0;var writes=0;
        var binding=new UiBinding<float>(()=>{reads++;return value;},next=>{writes++;value=next;});
        using var slider=new Probe("Slider",binding,0,100,5){Size=new(200,32)};
        var disabled=false;var hidden=false;var size=default(UiVector2);var deactivated=false;var edited=false;
        UiDrawData Frame(UiInputState? input=null,Action<UiImmediateContext>? action=null)
        {
            context.NewFrame(new(1f/60,new(320,240),new(1,1)),input??idle,new(0,0,320,240),UiTextSettings.Default);
            context.Render(new Screen(action??(ui=>
            {
                if(hidden)return;
                ui.SetCursorScreenPos(new(10,10));ui.BeginDisabled(disabled);slider.Render(ui);ui.EndDisabled();
                size=ui.GetItemRectSize();deactivated=ui.IsItemDeactivatedAfterEdit();edited=ui.IsItemEdited();
            })));
            return context.GetDrawData();
        }
        void Step(UiInputState? input=null){using var data=Frame(input);}
        void Key(UiKey key){Step(idle with {KeyEvents=new[]{new UiKeyEvent(key,true,default)}});}
        using(var data=Frame())
            check(size==new UiVector2(200,32)&&data.DrawLists.Sum(list=>list.RectFilledPrimitives?.Count(item=>item.CornerRadius<0)??0)==3,
                "generated Slider paints an actual medium20px thumb in a32px root");
        check(reads==1&&writes==0,"retained Slider reads its binding once without calling its setter during idle");
        Step(idle with {MousePosition=new(155,26),LeftMousePressed=true,LeftMouseDown=true});
        check(value==75&&writes==1&&edited,"pointer press maps the thumb travel to a stepped value and invokes one callback");
        Step(idle with {MousePosition=new(300,26),LeftMouseDown=true});
        check(value==100&&writes==2,"drag capture continues outside the root and clamps at the maximum");
        Step(idle with {MousePosition=new(300,26),LeftMouseReleased=true});
        check(writes==2&&deactivated,"drag release preserves edited deactivation without another callback");
        Key(UiKey.RightArrow);check(writes==2,"a key at the range boundary does not report a false value change");
        Key(UiKey.LeftArrow);check(value==95&&writes==3,"focused Slider changes one step with the keyboard after pointer release");
        Key(UiKey.PageDown);check(value==85,"PageDown moves by one tenth of the range and snaps to the step grid");
        Key(UiKey.Home);check(value==0,"Home selects the minimum");
        Key(UiKey.End);check(value==100,"End selects the last valid step");
        var before=writes;Key(UiKey.Space);Key(UiKey.Enter);
        check(writes==before&&context.State.ActiveId is null,"Slider does not inherit button Space or Enter activation");
        disabled=true;Key(UiKey.LeftArrow);check(writes==before,"disabled Slider rejects keyboard edits and releases focus");
        disabled=false;Step();Key(UiKey.Tab);Key(UiKey.LeftArrow);
        check(value==95,"Tab navigation makes the enabled Slider keyboard-operable without a pointer press");
        hidden=true;Step();Key(UiKey.LeftArrow);check(value==95,"removing the focused Slider prevents subsequent keyboard edits");
        hidden=false;Step(idle with {MousePosition=new(110,26),LeftMousePressed=true,LeftMouseDown=true});
        hidden=true;before=writes;Step(idle with {LeftMouseDown=true});
        check(context.State.ActiveId is null&&writes==before,"removing a dragged Slider cancels its shared pointer capture without a callback");
        Step(idle with {LeftMouseReleased=true});
        hidden=false;slider.Options=new(0,100,null,Vertical:true);slider.Size=new(32,200);
        Step(idle with {MousePosition=new(26,20),LeftMousePressed=true,LeftMouseDown=true});
        check(value==100,"vertical Slider maps the upper end to the maximum");
        Step(idle with {MousePosition=new(26,200),LeftMouseDown=true});
        check(value==0,"vertical Slider maps the lower end to the minimum");
        Step(idle with {LeftMouseReleased=true});
        slider.Options=new(0,100,null,Reversed:true);slider.Size=new(200,32);
        Step(idle with {MousePosition=new(20,26),LeftMousePressed=true,LeftMouseDown=true});
        check(value==100,"reversed horizontal Slider maps the left end to the maximum");
        Step(idle with {LeftMouseReleased=true});Key(UiKey.RightArrow);
        check(value==99,"reversed keyboard direction matches reversed pointer travel");
        check(slider.Changes==writes,"user inheritance observes exactly the same callback path as the generated widget");
        check(new UiSliderOptions(10,20,3).Snap(20)==19&&new UiSliderOptions(10,20,3).Snap(11.5)==13,
            "step snapping is relative to the minimum and resolves midpoint ties upward");
        foreach(var invalid in new[]{new UiSliderOptions(0,0),new(2,1),new(0,float.NaN),new(0,1,0),new(0,1,float.PositiveInfinity)})
        {
            var rejected=false;try{invalid.Validate();}catch(ArgumentOutOfRangeException){rejected=true;}
            check(rejected,"invalid slider range or step fails before rendering: "+invalid);
        }
        slider.Options=new(0,100,Size:UiSliderSize.Small);slider.Size=new(120,0);
        using(var data=Frame())check(size.Y==24,"small Slider reserves the official24px root");
        using var custom=new UiSlider<UiFluentSliderBehavior,DialPresentation>("Custom",binding);
        using(var data=Frame(action:custom.Render))check(data.DrawLists.Sum(list=>list.RectFilledPrimitives?.Count(item=>item.CornerRadius<0)??0)==1,
            "user presentation replaces Slider layout and drawing while reusing the compiled input behavior");
    }
    private sealed class Screen(Action<UiImmediateContext> render):UiScreen
    {public override void Render(UiImmediateContext ui)=>render(ui);}
    private sealed class Probe(string label,UiBinding<float> binding,float minimum,float maximum,float? step)
        :UiFluentSlider(label,binding,minimum,maximum,step)
    {
        internal int Changes;
        protected override void OnChanged(float value){Changes++;base.OnChanged(value);}
    }
    private readonly struct DialPresentation:IUiSliderPresentation
    {
        public static UiSliderLayout Measure(UiControlContext context,UiSliderOptions options,UiVector2 requestedSize)=>
            new(new(60,60),new(10,29,40,2),10,3);
        public static void Paint(UiControlContext context,UiRect bounds,UiSliderLayout layout,UiSliderOptions options,UiSliderInteraction interaction)=>
            context.Circle(new(bounds.X+30,bounds.Y+30),30,context.Theme.CheckMark);
    }
}
