using Duxel.Core;

internal static class LeafConstraintRenderingTests
{
    internal static void Run(Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check)
    {
        void Slider(string name,UiVector2 size,UiSliderOptions options,UiRect track)
        {
            using var widget=Fluent.Slider(string.Empty,new UiState<float>(50).Binding);widget.Options=options;
            using var box=Dux.SizedBox(widget,size.X,size.Y);
            var actual=capture(name,ui=>{Root(ui);box.Render(ui);});
            var reference=capture(name+"-reference",ui=>
            {Root(ui);var context=new UiControlContext(ui);using var clip=context.Clip(new(0,0,size.X,size.Y));UiFluentSliderPresentation.Paint(context,new(0,0,size.X,size.Y),new(size,track,20,6),options,new(50,false,default));});
            check(actual.AsSpan().SequenceEqual(reference),name+" matches an independently assigned rail and unchanged20px thumb");
        }
        Slider("leaf-slider-short",new(80,64),new(0,100),new(10,30,60,4));
        Slider("leaf-slider-vertical",new(64,80),new(0,100,Vertical:true),new(30,10,4,60));
        Slider("leaf-slider-zero-travel",new(10,32),new(0,100),new(5,14,0,4));

        using var checkbox=new UiToggle<bool?,UiNullableToggleValue,UiReleaseButtonBehavior,UiFluentCheckboxPresentation>(string.Empty,new UiState<bool?>(false).Binding);
        using var checkboxBox=Dux.SizedBox(checkbox,180,64);
        var actual=capture("leaf-checkbox",ui=>{Root(ui);checkboxBox.Render(ui);});
        var reference=capture("leaf-checkbox-reference",ui=>
        {Root(ui);UiFluentCheckboxPresentation.Paint(new(ui),string.Empty,default,new(0,0,180,64),new(new(180,64),new(8,24,16,16),new(36,0)),default,false);});
        check(actual.AsSpan().SequenceEqual(reference),"allocated Checkbox keeps the same16px indicator centered in the full64px hit surface");

        using var toggleSwitch=Fluent.Switch(string.Empty,new UiState<bool>(false).Binding);var inner=Dux.Padding(toggleSwitch,new(4));using var switchBox=Dux.SizedBox(inner,188,72);
        actual=capture("leaf-switch",ui=>{Root(ui);switchBox.Render(ui);});
        reference=capture("leaf-switch-reference",ui=>
        {Root(ui);UiFluentSwitchPresentation.Paint(new(ui),string.Empty,default,new(4,4,180,64),new(new(180,64),new(8,22,40,20),new(60,22)),default,false);});
        check(actual.AsSpan().SequenceEqual(reference),"nested allocated Switch preserves the40x20 track and centers its composition");
        inner.Options=new(8);
        actual=capture("leaf-switch-updated",ui=>{Root(ui);switchBox.Render(ui);});
        reference=capture("leaf-switch-updated-reference",ui=>
        {Root(ui);UiFluentSwitchPresentation.Paint(new(ui),string.Empty,default,new(8,8,172,56),new(new(172,56),new(8,18,40,20),new(60,18)),default,false);});
        check(actual.AsSpan().SequenceEqual(reference),"live parent allocation changes preserve exact nested Switch pixels");
        var replay=true;for(var i=0;i<4;i++)replay&=reference.AsSpan().SequenceEqual(capture("leaf-switch-replay-"+i,ui=>{Root(ui);switchBox.Render(ui);}));
        check(replay,"updated constrained Toggle geometry stays coherent across acquired frame images");
    }
    private static void Root(UiImmediateContext ui){ui.EnableRootViewportContentLayout(contentPadding:0);ui.SetCursorScreenPos(default);}
}
