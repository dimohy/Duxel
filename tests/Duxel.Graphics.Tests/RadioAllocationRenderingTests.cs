using Duxel.Core;

internal static partial class RadioAllocationRenderingTests
{
    private static readonly UiRadioOption[] Options=[new("a","Alpha option wraps with words",Description:"More context belongs to this choice."),new("b","Beta")];
    internal static void Run(Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check)
    {
        Compare<UiRadioVerticalLayout>("vertical",capture,check);
        Compare<UiRadioHorizontalLayout>("horizontal",capture,check);
        Compare<UiRadioHorizontalStackedLayout>("stacked",capture,check);
        using var custom=Dux.SizedBox(new GeneratedAllocatedRadio(string.Empty,new UiState<string?>("a").Binding,Options),80,120);
        var actual=capture("radio-custom-allocation",ui=>{Root(ui);custom.Render(ui);});
        var reference=capture("radio-custom-allocation-reference",ui=>
        {
            Root(ui);
            var clip = new UiRect(0, 0, 80, 120);
            var theme = ui.GetStyle();
            var draw = ui.GetWindowDrawList();
            draw.AddRectFilledRounded(new(0, 0, 80, 60), theme.Button, theme.Border, ui.WhiteTextureId, 4, 1, clip);
            draw.AddRectFilledRounded(new(0, 60, 80, 60), theme.Button, theme.Border, ui.WhiteTextureId, 4, 1, clip);
        });
        check(actual.AsSpan().SequenceEqual(reference),"a generated user RadioGroup paints independently assigned allocation-dependent rows through the same renderer");
    }
    private static void Compare<TLayout>(string name,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check) where TLayout:IUiRadioGroupLayout
    {
        var group=new UiRadioGroup<UiFluentRadioGroupBehavior,UiFluentRadioGroupPresentation<TLayout>>(string.Empty,new UiState<string?>("a").Binding,Options,300);
        using var box=Dux.SizedBox(group,160,64);
        using var natural=new UiRadioGroupContent<UiFluentRadioGroupPresentation<TLayout>>(Options);
        byte[] Reference(UiVector2 size,string suffix)=>capture("radio-"+name+suffix+"-reference",ui=>
        {
            Root(ui);
            ui.PushClipRect(new(0, 0, size.X, size.Y), intersect : true);
            try
            {
                string? value = "a";
                ui.RadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<TLayout>>("##reference", natural, ref value, size.X);
            }
            finally { ui.PopClipRect(); }
        });
        var actual=capture("radio-"+name,ui=>{Root(ui);box.Render(ui);});var expected=Reference(new(160,64),string.Empty);
        check(actual.AsSpan().SequenceEqual(expected),name+" RadioGroup parent width drives natural wrapped labels and description with exact independent root clipping");
        box.Options=new(240,180);actual=capture("radio-"+name+"-updated",ui=>{Root(ui);box.Render(ui);});expected=Reference(new(240,180),"-updated");
        check(actual.AsSpan().SequenceEqual(expected),name+" RadioGroup live parent dimensions preserve exact label and indicator pixels");
        var replay=true;for(var i=0;i<4;i++)replay&=expected.AsSpan().SequenceEqual(capture("radio-"+name+"-replay-"+i,ui=>{Root(ui);box.Render(ui);}));
        check(replay,name+" RadioGroup arrangement remains correct across acquired frame images without corrupting canonical geometry");
    }
    private static void Root(UiImmediateContext ui){ui.EnableRootViewportContentLayout(contentPadding:0);ui.SetCursorScreenPos(default);}
}
