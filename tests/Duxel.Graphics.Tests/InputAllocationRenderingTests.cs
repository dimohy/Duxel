using Duxel.Core;
using BaseInput=Duxel.Core.UiFluentInputPresentation<Duxel.Core.UiFluentMediumInputSize,Duxel.Core.UiFluentOutlineInput>;

internal static class InputAllocationRenderingTests
{
    internal static void Run(Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check)
    {
        var input=Fluent.Input(string.Empty,new UiState<string>("abc").Binding,64);
        using var box=Dux.SizedBox(input,80,64);
        var actual=capture("allocated-input",ui=>{Root(ui);box.Render(ui);});
        var reference=capture("allocated-input-reference",ui=>
        {Root(ui);ManualInput.Size=new(80,64);var value="abc";ui.InputText<UiFluentTextInputBehavior,ManualInput>("##reference",ref value,64);});
        check(actual.AsSpan().SequenceEqual(reference),"allocated Input matches independently assigned80x64 field and14/20 typography geometry");
        box.Options=new(160,48);
        actual=capture("allocated-input-update",ui=>{Root(ui);box.Render(ui);});
        reference=capture("allocated-input-update-reference",ui=>
        {Root(ui);ManualInput.Size=new(160,48);var value="abc";ui.InputText<UiFluentTextInputBehavior,ManualInput>("##reference",ref value,64);});
        check(actual.AsSpan().SequenceEqual(reference),"a live parent width and height change preserves exact Input glyph and frame pixels");
        var replay=true;for(var i=0;i<4;i++)replay&=reference.AsSpan().SequenceEqual(capture("allocated-input-replay-"+i,ui=>{Root(ui);box.Render(ui);}));
        check(replay,"allocated editable geometry stays coherent across acquired frame images");

        using var underline=new UiInput<UiFluentTextInputBehavior,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentUnderlineInput>>(string.Empty,new UiState<string>("abc").Binding,64);
        using var underlineBox=Dux.SizedBox(underline,80,64);
        actual=capture("allocated-input-underline",ui=>{Root(ui);underlineBox.Render(ui);});
        reference=capture("allocated-input-underline-reference",ui=>
        {Root(ui);var value="abc";ui.InputText<UiFluentTextInputBehavior,ManualUnderline>("##reference",ref value,64);});
        check(actual.AsSpan().SequenceEqual(reference),"constrained Underline Input keeps its independent baseline and full-width bottom border");

        var owned=Fluent.Input(string.Empty,new UiState<string>("abc").Binding,64);
        owned.SetSlots(new("before",new ChildButton(string.Empty,null){Size=new(24,24)},new(24,24)),new("after",new ChildButton(string.Empty,null){Size=new(24,24)},new(24,24)));
        using var slots=Dux.SizedBox(owned,160,64);
        actual=capture("allocated-input-slots",ui=>{Root(ui);slots.Render(ui);});
        reference=capture("allocated-input-slots-reference",ui=>
        {
            Root(ui);var value="abc";ui.InputText<UiFluentTextInputBehavior,ManualSlots>("##reference",ref value,64);
            ui.SetCursorScreenPos(new(11,20));ui.Button<UiReleaseButtonBehavior,UiFluentButtonPresentation>(string.Empty,new(24,24));
            ui.SetCursorScreenPos(new(125,20));ui.Button<UiReleaseButtonBehavior,UiFluentButtonPresentation>(string.Empty,new(24,24));
        });
        check(actual.AsSpan().SequenceEqual(reference),"owned Input slots and text match independently placed24px children and editing bounds");
    }
    private static void Root(UiImmediateContext ui){ui.EnableRootViewportContentLayout(contentPadding:0);ui.SetCursorScreenPos(default);}
    private readonly struct ManualInput:IUiInputPresentation
    {
        internal static UiVector2 Size;
        public static UiControlTypographyScope BeginText(UiControlContext context)=>BaseInput.BeginText(context);
        public static UiInputLayout Measure(UiControlContext context,string label,float width)=>new(Size,new(0,0,Size.X,Size.Y),new(13,(Size.Y-20)/2,Size.X-26,20),default,20);
        public static void Paint(UiControlContext context,string label,in UiInputLayout layout,in UiInputEditResult edit)=>BaseInput.Paint(context,label,layout,edit);
    }
    private readonly struct ManualUnderline:IUiInputPresentation
    {
        public static UiControlTypographyScope BeginText(UiControlContext context)=>BaseInput.BeginText(context);
        public static UiInputLayout Measure(UiControlContext context,string label,float width)=>new(new(80,64),new(0,0,80,64),new(12,21.5f,56,20),default,20);
        public static void Paint(UiControlContext context,string label,in UiInputLayout layout,in UiInputEditResult edit)=>UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentUnderlineInput>.Paint(context,label,layout,edit);
    }
    private readonly struct ManualSlots:IUiInputPresentation
    {
        public static UiControlTypographyScope BeginText(UiControlContext context)=>BaseInput.BeginText(context);
        public static UiInputLayout Measure(UiControlContext context,string label,float width)=>new(new(160,64),new(0,0,160,64),new(39,22,82,20),default,20)
            {BeforeChild=true,AfterChild=true,Before=new(11,20,24,24),After=new(125,20,24,24),Editing=new(37,1,86,62)};
        public static void Paint(UiControlContext context,string label,in UiInputLayout layout,in UiInputEditResult edit)=>BaseInput.Paint(context,label,layout,edit);
    }
}
