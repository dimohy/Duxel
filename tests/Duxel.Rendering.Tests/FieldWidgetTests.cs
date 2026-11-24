using Duxel.Core;
using Duxel.Platform.Windows;
using FieldPresentation=Duxel.Core.UiFluentFieldPresentation<Duxel.Core.UiFluentMediumFieldSize,Duxel.Core.UiVerticalFieldLayout>;
using InputPresentation=Duxel.Core.UiFluentInputPresentation<Duxel.Core.UiFluentMediumInputSize,Duxel.Core.UiFluentOutlineInput>;

internal static class FieldWidgetTests
{
    internal static void Run(Action<bool,string> check)
    {
        var backend=WindowsPlatformTextBackend.Instance;var glyph="\U000F0A11";
        check(backend.TryLayout(new(UiFluentFilledIconFont.Path,null,glyph,12),out var glyphLayout)&&glyphLayout.Advance==12,"UCS4 Fluent diamond glyph has its actual12px advance");
        check(backend.TryRasterize(new(UiFluentFilledIconFont.Path,null,glyph,12),out var glyphPixels)&&glyphPixels.Advance==glyphLayout.Advance&&glyphPixels.Width==glyphLayout.Width&&glyphPixels.Height==glyphLayout.Height,"supplementary glyph layout and raster metrics agree");glyphPixels.PixelOwner?.Dispose();
        var mesh=backend.CreateTextMesh(new(UiFluentFilledIconFont.Path,null,glyph,12));var mixed=backend.CreateTextMesh(new(UiFluentFilledIconFont.Path,null,glyph+"\uE5FA",12));
        check(mesh.Glyphs.Length==1&&mesh.Glyphs.Span[0].Vertices.Length>0&&mixed.Glyphs.Length==2&&mixed.Glyphs.Span[1].Offset.X==12,"a UTF16 surrogate pair creates one actual vector glyph in mixed text");
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));ControlFontFixture.Configure(context);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var text="abc";var calls=0;var input=new FieldInput("Input",new(()=>text,v=>{text=v;calls++;}),64,220);
        var options=new UiFieldOptions{Label="Email",Hint="Use your work address",ValidationMessage="Enter a valid address",Required=true};
        using var field=Fluent.Field(input,new(220,32),options);
        var disabled=false;var scopesRestored=true;UiRect bounds=default;
        void Step(UiInputState? state=null,IUiView? view=null)
        {
            context.NewFrame(new(1f/60,new(600,300),new(1,1)),state??idle,new(0,0,600,300),UiTextSettings.Default);
            context.Render(new Screen(ui=>{ui.SetCursorScreenPos(new(10,10));ui.BeginDisabled(disabled);(view??field).Render(ui);ui.EndDisabled();bounds=new(ui.GetItemRectMin().X,ui.GetItemRectMin().Y,ui.GetItemRectSize().X,ui.GetItemRectSize().Y);scopesRestored&=ui.CurrentField is null;}));using var draw=context.GetDrawData();
        }
        Step();check(Probe.Field==new UiFieldContext("Email","Use your work address","Enter a valid address",true,UiFieldValidationState.Error)&&Probe.Layout.Options.Invalid,"field metadata reaches the actual generated input and sets invalid styling");
        check(input.Options==default,"inherited invalid state does not mutate input options");
        check(Probe.Layout.Field==new UiRect(10,36,220,32)&&bounds==new UiRect(10,10,220,94),"vertical medium field reserves label,control,validation and hint exactly once");
        UiFieldLayout small=default,large=default,horizontal=default;
        Step(view:new View(ui=>
        {
            var c=new UiControlContext(ui);
            small=UiFluentFieldPresentation<UiFluentSmallFieldSize,UiVerticalFieldLayout>.Measure(c,options,new(220,24));
            large=UiFluentFieldPresentation<UiFluentLargeFieldSize,UiVerticalFieldLayout>.Measure(c,options,new(220,40));
            horizontal=UiFluentFieldPresentation<UiFluentMediumFieldSize,UiHorizontalFieldLayout>.Measure(c,options with{Label="Work email"},new(134,32));
        }));
        check(small.Control.Y==22&&large.Control.Y==28,"small and large Field use official label typography and padding");
        check(MathF.Abs(horizontal.Control.X-66)<.001f&&horizontal.Validation.X==horizontal.Control.X+16&&horizontal.Hint.X==horizontal.Control.X,"horizontal Field uses the33percent label column and control-aligned descriptions");
        check(horizontal.RequiredIndicator.Y==horizontal.Label.Y+20,"wrapped labels keep the required indicator after the final text line");
        check(new UiFieldOptions{ValidationMessage=""}.EffectiveValidationState==UiFieldValidationState.None,"empty validation text does not implicitly mark the control invalid");
        Step(view:new View(ui=>
        {
            var c=new UiControlContext(ui);var empty=FieldPresentation.Measure(c,new(){Label="",Required=true,Hint="",ValidationMessage=""},new(220,32));
            check(empty.Control.Y==26&&empty.RequiredIndicator.Y==2&&empty.LabelText.LineCount==0,"empty required labels retain their marker without a fabricated text line");
        }));
        Step(idle with{MousePosition=new(15,18),LeftMouseDown=true,LeftMousePressed=true});
        check(context.State.FocusedId is not null&&context.State.ActiveId==context.State.FocusedId,"label press focuses and activates its editor in the same frame");
        Step(idle with{LeftMouseReleased=true});Step(idle with{CharEvents=[new('d')]});
        check(text=="abcd"&&calls==1,"typing after label activation uses the real editor and exactly one binding callback");
        var prefixCalls=0;input.BeforeSlot=new("prefix",Fluent.Button("?",()=>prefixCalls++),new(24,24));
        Step(idle with{MousePosition=new(15,18),LeftMouseDown=true,LeftMousePressed=true});
        check(context.State.ActiveId==context.State.FocusedId&&context.State.ActiveId is not null&&prefixCalls==0,"label targets the parent editor before its interactive adornments");input.BeforeSlot=null;
        var id=context.State.FocusedId;field.Options=options with{Label="Updated",ValidationMessage=null,Required=false};Step();
        check(context.State.FocusedId==id&&!Probe.Layout.Options.Invalid&&Probe.Field?.Label=="Updated","changing field descriptions preserves editor identity and clears inherited errors");
        field.Options=options with{ValidationState=UiFieldValidationState.Warning};Step();check(!Probe.Layout.Options.Invalid&&Probe.Field?.ValidationState==UiFieldValidationState.Warning,"warning metadata does not mark its editor invalid");
        field.Options=options with{Disabled=true};Step(idle with{MousePosition=new(15,18),LeftMouseDown=true,LeftMousePressed=true});Step(idle with{CharEvents=[new('z')]});
        check(text=="abcd"&&context.State.ActiveId is null,"disabled field control rejects label focus and text edits");
        field.Options=options;disabled=true;Step(idle with{MousePosition=new(15,18),LeftMouseDown=true,LeftMousePressed=true});check(context.State.ActiveId is null,"outer disabled scope also blocks label activation");disabled=false;
        var rejected=false;try{using var other=Fluent.Field(input,new(220,32));}catch(InvalidOperationException){rejected=true;}
        check(rejected,"two fields cannot share one child instance");
        rejected=false;try{field.ControlSize=new(float.NaN,32);}catch(ArgumentOutOfRangeException){rejected=true;}check(rejected&&field.ControlSize==new UiVector2(220,32),"invalid field extents are rejected before mutation");
        rejected=false;try{field.Options=options with{ValidationState=(UiFieldValidationState)99};}catch(ArgumentOutOfRangeException){rejected=true;}check(rejected&&field.Options==options,"invalid field validation states are rejected atomically");
        using(var content=new UiFieldContent<FieldPresentation>())
        {
            var direct=new View(ui=>ui.Field<UiLabelFieldBehavior,FieldPresentation>(input,new(220,32),options,content));Step(view:direct);var count=content.BuildCount;Step(view:direct);
            check(content.BuildCount==count,"unchanged field reuses its owned measured text and layout");
            var narrow=new View(ui=>ui.Field<UiLabelFieldBehavior,FieldPresentation>(input,new(80,32),options,content));Step(view:narrow);check(content.BuildCount==count+1,"control width invalidates wrapped field text");
            var longLabel=new View(ui=>ui.Field<UiLabelFieldBehavior,FieldPresentation>(input,new(80,32),options with{Label=new string('a',1025)},content));Step(view:longLabel);var longCount=content.BuildCount;Step(view:longLabel);
            check(content.BuildCount==longCount+1,"oversized field text is rendered without retaining an unbounded layout");
        }
        var buttonCalls=0;using(var buttonField=Dux.Field(Fluent.Button("Action",()=>buttonCalls++),new(100,32),new(){Label="Run"}))
        {
            Step(idle with{MousePosition=new(15,18),LeftMouseDown=true,LeftMousePressed=true},buttonField);
            check(context.State.FocusedId is not null&&buttonCalls==0,"Dux Field label focuses a real button without activating it");
            Step(idle with{KeyEvents=[new(UiKey.Space,true,default)]},buttonField);Step(idle with{KeyEvents=[new(UiKey.Space,false,default)]},buttonField);
            check(buttonCalls==1,"field preserves its child button Space activation exactly once");
        }
        var nestedValue="nested";using(var inner=Fluent.Field(Fluent.Input("Nested",new(()=>nestedValue,v=>nestedValue=v),64,100),new(100,32),new(){Label="Inner"}))
        using(var outer=Fluent.Field(inner,new(100,58),options))
        {Step(view:outer);check(scopesRestored,"nested fields restore their nearest metadata scope");}
        using(var failing=Fluent.Field(new FailingWidget(),new(100,32),options))
        {
            rejected=false;try{Step(view:failing);}catch(InvalidOperationException error)when(error.Message=="field child failure"){rejected=true;}
            Step();check(rejected&&Probe.Field?.Label=="Email","failed child restores field metadata,layout and disabled scope");
        }
        var owned=new TrackedButton();var unused=Fluent.Field(owned,new(100,32));unused.Dispose();check(owned.Disposed,"field disposal before first Build releases its actual child");
        check(Fluent.ComponentNames.Contains("Field")&&!Fluent.MissingComponentNames.Contains("Field"),"generated standard catalog exposes Field and updates required coverage");
        field.Dispose();check(input.Disposed&&context.State.TextInputStorage==(0,0),"field disposal releases nested editor and editing storage");
        check(scopesRestored,"field metadata scopes are restored after every successful frame");
        VerifySwitchField(check);
    }

    private static void VerifySwitchField(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetAccessibilityTree(tree);
        ControlFontFixture.Configure(context);
        var value = false;
        var writes = 0;
        var control = Fluent.Switch(string.Empty, new UiBinding<bool>(() => value, next => { value = next; writes++; }));
        var options = new UiFieldOptions { Label = "Required setting", Hint = "Enable this setting",
            Required = true, ValidationMessage = "Setting is required" };
        using var field = Fluent.Field(control, new(220, 36), options);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var screen = new Screen(ui =>
        {
            ui.SetCursorScreenPos(new(10, 10));
            field.Render(ui);
        });
        void Step(UiInputState input)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement ReadSwitch()
        {
            for (var id = tree.Root.First; id is not null;)
            {
                if (!tree.TryGet(id, out var entry))
                    break;
                if (entry.Node.Role == UiAccessibilityRole.Switch)
                    return entry;
                id = entry.Next;
            }
            throw new InvalidOperationException("Missing Switch inside Field.");
        }
        void ClickLabel()
        {
            Step(idle with { MousePosition = new(15, 18), LeftMousePressed = true, LeftMouseDown = true });
            Step(idle with { MousePosition = new(15, 18), LeftMouseReleased = true });
        }
        void Space()
        {
            Step(idle with { KeyEvents = [new(UiKey.Space, true, default)] });
            Step(idle with { KeyEvents = [new(UiKey.Space, false, default)] });
        }

        Step(idle);
        var initial = ReadSwitch();
        check(initial.Node is { Name: "Required setting", Required: true, Invalid: true, Hint: "Enable this setting",
            ValidationMessage: "Setting is required" }, "Switch publishes the Field label, required state, hint and validation message");
        control.DisabledFocusable = true;
        ClickLabel();
        check(tree.Root.Focus == initial.Node.Id && !value && writes == 0,
            "Field label focuses a focus-only Switch without changing its value");
        Space();
        check(!value && writes == 0, "A focus-only Switch remains locked after keyboard focus from its Field label");
        field.Options = options with { Disabled = true };
        ClickLabel();
        check(tree.Root.Focus != initial.Node.Id && !ReadSwitch().Node.CanReceiveFocus && writes == 0,
            "An explicitly disabled Field prevents its label from focusing a focus-only Switch");
        field.Options = options with { Label = "Updated setting", Required = false, ValidationMessage = null };
        control.DisabledFocusable = false;
        ClickLabel();
        var updated = ReadSwitch();
        check(updated.RuntimeId == initial.RuntimeId && updated.Node is { Name: "Updated setting", Required: false, Invalid: false }
            && tree.Root.Focus == initial.Node.Id && writes == 0,
            "Field updates keep Switch identity and an ordinary label click only moves focus");
        Space();
        check(value && writes == 1, "Space activates the unlocked Switch exactly once after Field label focus");
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen{public override void Render(UiImmediateContext ui)=>draw(ui);}
    private sealed class View(Action<UiImmediateContext> draw):IUiView{public void Render(UiImmediateContext ui)=>draw(ui);}
    private sealed class FailingWidget:UiControlWidget
    {protected override void RenderControl(UiImmediateContext ui)=>throw new InvalidOperationException("field child failure");}
    private sealed class TrackedButton:UiFluentButton
    {internal bool Disposed;internal TrackedButton():base("Tracked"){}protected override void OnDispose()=>Disposed=true;}
}
internal readonly struct Probe:IUiInputPresentation
{
    public static bool SupportsChildren=>true;
    internal static UiFieldContext? Field;internal static UiInputLayout Layout;
    public static UiControlTypographyScope BeginText(UiControlContext context)=>InputPresentation.BeginText(context);
    public static UiInputLayout Measure(UiControlContext context,string label,float width)=>InputPresentation.Measure(context,label,width);
    public static UiInputLayout MeasureContent(UiControlContext context,in UiInputLayout layout,UiInputOptions options)=>InputPresentation.MeasureContent(context,layout,options);
    public static void Paint(UiControlContext context,string label,in UiInputLayout layout,in UiInputEditResult edit){Field=context.Field;Layout=layout;InputPresentation.Paint(context,label,layout,edit);}
}
[UiWidgetStyle(typeof(UiInput<,>),typeof(UiFluentTextInputBehavior),typeof(Probe))]
internal partial class FieldInput
{internal bool Disposed;protected override void OnDispose()=>Disposed=true;}
