using Duxel.Core;

internal static class WidgetMeasurementTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));ControlFontFixture.Configure(context);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var reads=0;var writes=0;var value="abc";
        using var input=Fluent.Input("Value",new(()=>{reads++;return value;},v=>{value=v;writes++;}),64);
        var toggleReads=0;using var toggle=Fluent.Checkbox("Option",new UiBinding<bool>(()=>{toggleReads++;return false;},_=>writes++));
        var sliderReads=0;using var slider=Fluent.Slider("Range",new UiBinding<float>(()=>{sliderReads++;return 20;},_=>writes++));
        var radioReads=0;using var radio=Fluent.RadioGroup("Choices",new UiBinding<string?>(()=>{radioReads++;return "a";},_=>writes++),new UiRadioOption[]{new("a","First"),new("b","Second line\ncontinues")});
        var clicks=0;using var button=Fluent.Button("Save",()=>clicks++);
        void Step(Action<UiImmediateContext> draw,UiInputState? state=null)
        {context.NewFrame(new(1f/60,new(640,480),new(1,1)),state??idle,new(0,0,640,480),UiTextSettings.Default);context.Render(new Screen(draw));using var data=context.GetDrawData();}
        Step(ui=>
        {
            ui.SetCursorScreenPos(new(10,10));var cursor=ui.GetCursorScreenPos();var id=ui.GetItemID();
            check(input.Measure(ui,new(240,400))==new UiVector2(240,32),"Input natural measurement uses the static presentation and available width");
            check(slider.Measure(ui,new(240,400))==new UiVector2(240,32),"Slider measurement does not need its current value");
            var toggleSize=toggle.Measure(ui,new(240,400));var radioSize=radio.Measure(ui,new(240,400));var buttonSize=button.Measure(ui,new(240,400));
            check(toggleSize.X>20&&toggleSize.Y>0&&radioSize.Y>toggleSize.Y&&buttonSize.X>=96,"Toggle,RadioGroup and Button expose real intrinsic layout");
            check(reads==0&&toggleReads==0&&sliderReads==0&&radioReads==0&&writes==0&&clicks==0,"measurement reads no bindings and invokes no callbacks");
            check(ui.GetCursorScreenPos()==cursor&&ui.GetItemID()==id&&context.State.ActiveId is null&&context.State.FocusedId is null,"measurement preserves caller cursor,item and input state");
            button.Render(ui);check(ui.GetItemRectSize()==buttonSize,"measured button extents equal actual rendered extents");
        });
        using var natural=Fluent.Field(input);natural.Width=220;natural.Options=new(){Label="Name",Hint="Use a display name"};
        Step(ui=>
        {
            var beforeReads=reads;var measured=natural.Measure(ui,new(500,400));
            check(measured==new UiVector2(220,76)&&reads==beforeReads,"Field measures its actual child and descriptions without editing or reading its value");
            ui.SetCursorScreenPos(new(10,10));natural.Render(ui);check(ui.GetItemRectSize()==measured,"automatic Field allocation matches its pure desired size");
        });
        Step(ui=>{ui.SetCursorScreenPos(new(10,10));natural.Measure(ui,new(500,400));natural.Render(ui);},idle with{MousePosition=new(15,15),LeftMouseDown=true,LeftMousePressed=true});
        Step(ui=>{natural.Measure(ui,new(500,400));ui.SetCursorScreenPos(new(10,10));natural.Render(ui);},idle with{CharEvents=[new('z')]});
        check(value=="abcz"&&writes==1,"measure-before-render preserves label focus and exactly one edit callback");
        using var small=new MeasuredSmallInput("Small",new(()=>value,v=>value=v),64);
        using var large=new MeasuredLargeInput("Large",new(()=>value,v=>value=v),64);
        Step(ui=>check(small.Measure(ui,new(240,200)).Y==24&&large.Measure(ui,new(240,200)).Y==40,"source-generated small and large input measurements retain their actual heights"));
        var tall=new MeasuredBody{Height=64};input.BeforeSlot=new("tall",tall,new(24,64));
        Step(ui=>
        {
            var size=natural.Measure(ui,new(500,400));check(size.Y==110&&tall.Renders==0,"owned tall adornments contribute to Input and Field measurement without rendering");
            natural.Render(ui);check(tall.Renders==1&&ui.GetItemRectSize()==size,"tall adornment geometry fits the measured Field height");
        });input.BeforeSlot=null;
        using var compatibility=new UiInput<UiStandardTextInputBehavior,UiContextInputPresentation>("Legacy",new(()=>value,v=>value=v),64);
        Step(ui=>{ui.SetNextItemWidth(77);var cursor=ui.GetCursorScreenPos();compatibility.Measure(ui,new(240,200));check(ui.CalcItemWidth()==77&&ui.GetCursorScreenPos()==cursor,"measurement restores a consumed next-item width for the following real layout");});
        using var body=new MeasuredBody();using var custom=Fluent.Field(body);custom.Width=210;custom.Options=new(){Label="Custom",ValidationMessage="Error"};
        Step(ui=>
        {
            var size=custom.Measure(ui,new(500,400));check(body.Renders==0&&size.Y==100,"a user widget supplies natural size and receives Field validation metadata during measurement");
            custom.Render(ui);check(body.LastBounds.Height==56&&ui.GetItemRectSize()==size,"user widget geometry consumes its measured allocation");
        });
        body.Height=72;custom.Options=custom.Options with{ValidationMessage=null};
        Step(ui=>{var size=custom.Measure(ui,new(500,400));custom.Render(ui);check(body.LastBounds.Height==72&&ui.GetItemRectSize()==size,"child state changes update natural allocation without a stale parent cache");});
        using var horizontal=new MeasuredHorizontalField(Fluent.Input("Horizontal",new(()=>value,v=>value=v),64)){Width=300,Options=new(){Label="Label"}};
        Step(ui=>check(MathF.Abs(horizontal.Measure(ui,new(500,400)).X-300)<.001f,"horizontal Field allocates its33percent label column within requested outer width"));
        Step(ui=>
        {
            var layout=UiFluentFieldPresentation<UiFluentMediumFieldSize,UiHorizontalFieldLayout>.Measure(new(ui),new(){Label="Measured label",Required=true},new(64.32f,32));
            check(layout.LabelClip is {} clip&&clip.X+clip.Width<=layout.Control.X-11.99f,"narrow horizontal Field confines long unbreakable labels to their own column");
        });
        using var nested=Fluent.Field(horizontal);nested.Width=400;nested.Options=new(){Label="Outer"};
        Step(ui=>{var size=nested.Measure(ui,new(500,400));nested.Render(ui);check(ui.GetItemRectSize()==size&&ui.CurrentField is null,"nested naturally measured Fields restore constraints and metadata");});
        using var unsupported=new Unsupported();
        Step(ui=>
        {
            var failed=false;try{unsupported.Measure(ui,new(200,200));}catch(NotSupportedException){failed=true;}
            check(failed&&unsupported.Renders==0,"unsupported measurement fails explicitly and never renders as a substitute");
            failed=false;try{button.Measure(ui,new(float.NaN,200));}catch(ArgumentOutOfRangeException){failed=true;}check(failed,"nonfinite measure constraints are rejected");
        });
        using var fault=new FailingMeasurement();using var invalid=new InvalidMeasurement();using var inputAttempt=new InputDuringMeasure();
        Step(ui=>
        {
            ui.SetCursorScreenPos(new(10,10));var cursor=ui.GetCursorScreenPos();
            var failed=false;try{fault.Measure(ui,new(123,400));}catch(InvalidOperationException e)when(e.Message=="expected measurement failure"){failed=true;}
            check(failed&&ui.GetCursorScreenPos()==cursor&&button.Measure(ui,new(240,200)).Y==32,"failed measurement restores caller layout and allows the next measurement");
            failed=false;try{invalid.Measure(ui,new(240,200));}catch(InvalidOperationException){failed=true;}check(failed,"invalid desired extents fail instead of becoming implicit sizes");
            var active=context.State.ActiveId;failed=false;try{inputAttempt.Measure(ui,new(240,200));}catch(InvalidOperationException){failed=true;}check(failed&&context.State.ActiveId==active,"measurement rejects shared input processing before it changes state");
            var recurse=new Reentrant(ui);try{failed=false;try{recurse.Measure(ui,new(240,200));}catch(InvalidOperationException){failed=true;}check(failed,"recursive measurement is rejected");}finally{recurse.Dispose();}
            using var renderAttempt=new RenderDuringMeasure(ui,button);failed=false;try{renderAttempt.Measure(ui,new(240,200));}catch(InvalidOperationException){failed=true;}check(failed,"measurement rejects rendering another widget before it mutates the frame");
        });
        using(var failedField=Fluent.Field(fault))Step(ui=>{var failed=false;try{failedField.Measure(ui,new(240,200));}catch(InvalidOperationException e)when(e.Message=="expected measurement failure"){failed=true;}check(failed&&ui.CurrentField is null&&!ui.IsMeasuring,"failed natural Field measurement restores metadata and measurement depth");});
        using(var navigable=new NavigableDuringMeasure())Step(ui=>
        {
            var id=ui.GetItemID();var focused=context.State.FocusedId;var hovered=context.State.HoveredId;var failed=false;
            try{navigable.Measure(ui,new(240,200));}catch(InvalidOperationException){failed=true;}
            check(failed&&ui.GetItemID()==id&&context.State.FocusedId==focused&&context.State.HoveredId==hovered,"navigable editing is rejected before item,hover and focus mutation during measurement");
        },idle with{MousePosition=new(5,5),LeftMouseDown=true,LeftMousePressed=true});
        foreach(var kind in new[]{0,1})using(var attempt=new OtherInputDuringMeasure(kind))Step(ui=>
        {
            var id=ui.GetItemID();var focused=context.State.FocusedId;var hovered=context.State.HoveredId;var failed=false;
            try{attempt.Measure(ui,new(240,200));}catch(InvalidOperationException){failed=true;}
            check(failed&&ui.GetItemID()==id&&context.State.FocusedId==focused&&context.State.HoveredId==hovered,$"label/caret input entry rejects measurement before mutation:kind={kind}");
        },idle with{MousePosition=new(5,5),LeftMouseDown=true,LeftMousePressed=true});
        natural.ControlSize=new(220,48);check(!natural.UsesNaturalSize,"explicit ControlSize retains the allocated compatibility mode");natural.Width=220;check(natural.UsesNaturalSize,"setting Width selects natural measurement explicitly");
        natural.Dispose();Step(ui=>{var failed=false;try{natural.Measure(ui,new(200,200));}catch(ObjectDisposedException){failed=true;}check(failed,"disposed widgets cannot retain or regenerate measured resources");});
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen{public override void Render(UiImmediateContext ui)=>draw(ui);}
    private sealed class Unsupported:UiControlWidget{internal int Renders;protected override void RenderControl(UiImmediateContext ui)=>Renders++;}
    private sealed class FailingMeasurement:UiControlWidget
    {protected override UiVector2 MeasureContent(UiControlContext context)=>throw new InvalidOperationException("expected measurement failure");protected override void RenderControl(UiImmediateContext ui){}}
    private sealed class InvalidMeasurement:UiControlWidget
    {protected override UiVector2 MeasureContent(UiControlContext context)=>new(float.NaN,20);protected override void RenderControl(UiImmediateContext ui){}}
    private sealed class InputDuringMeasure:UiControlWidget
    {protected override UiVector2 MeasureContent(UiControlContext context){context.ActivateOnRelease("bad",new(0,0,20,20));return new(20,20);}protected override void RenderControl(UiImmediateContext ui){}}
    private sealed class Reentrant(UiImmediateContext ui):UiControlWidget
    {protected override UiVector2 MeasureContent(UiControlContext context)=>Measure(ui,new(10,10));protected override void RenderControl(UiImmediateContext context){}}
    private sealed class RenderDuringMeasure(UiImmediateContext ui,UiWidget other):UiControlWidget
    {protected override UiVector2 MeasureContent(UiControlContext context){other.Render(ui);return new(20,20);}protected override void RenderControl(UiImmediateContext context){}}
    private sealed class NavigableDuringMeasure:UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context)
        {var value="test";context.EditNavigableSingleLine("bad",ref value,64,new(new(20,20),new(0,0,20,20),new(0,0,20,20),default,16));return new(20,20);}
        protected override void RenderControl(UiImmediateContext context){}
    }
    private sealed class OtherInputDuringMeasure(int kind):UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context)
        {if(kind==0)context.FieldLabelPressed("bad-label",new(0,0,20,20));else context.RestartInputCaretBlink();return new(20,20);}
        protected override void RenderControl(UiImmediateContext context){}
    }
}
internal sealed class MeasuredBody:UiControlWidget
{
    internal float Height=48;internal int Renders;internal UiRect LastBounds;
    protected override UiVector2 MeasureContent(UiControlContext context)=>new(context.AvailableWidth,context.Field is {Invalid:true}?56:Height);
    protected override void RenderControl(UiImmediateContext ui)
    {
        Renders++;var height=ui.CurrentField is {Invalid:true}?56:Height;
        ui.Button<UiReleaseButtonBehavior,UiFluentButtonPresentation>("Custom",new(ui.GetContentRegionAvail().X,height));
        LastBounds=new(ui.GetItemRectMin().X,ui.GetItemRectMin().Y,ui.GetItemRectSize().X,ui.GetItemRectSize().Y);
    }
}
[UiWidgetStyle(typeof(UiInput<,>),typeof(UiFluentTextInputBehavior),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentOutlineInput>))] internal partial class MeasuredSmallInput;
[UiWidgetStyle(typeof(UiInput<,>),typeof(UiFluentTextInputBehavior),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentOutlineInput>))] internal partial class MeasuredLargeInput;
[UiWidgetStyle(typeof(UiField<,>),typeof(UiLabelFieldBehavior),typeof(UiFluentFieldPresentation<UiFluentMediumFieldSize,UiHorizontalFieldLayout>))] internal partial class MeasuredHorizontalField;
