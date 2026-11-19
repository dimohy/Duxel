using Duxel.Core;

internal static class SliderRenderingTests
{
    internal static void Run(Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check)
    {
        var value=25f;var binding=new UiBinding<float>(()=>value,next=>value=next);
        using var slider=Fluent.Slider("GPU Slider",binding);slider.Size=new(200,32);
        void Draw(UiImmediateContext ui){ui.SetCursorScreenPos(new(20,20));slider.Render(ui);}
        var widget=capture("slider-generated",Draw);
        var direct=capture("slider-immediate",ui=>
        {ui.SetCursorScreenPos(new(20,20));var current=value;ui.Slider<UiFluentSliderBehavior,UiFluentSliderPresentation>("GPU Slider",ref current,new(0,100),new(200,32));});
        check(widget.SequenceEqual(direct),"generated and typed immediate Slider produce identical actual GPU pixels");
        value=75;var changed=capture("slider-changed",Draw);
        check(!widget.SequenceEqual(changed),"changing the Slider binding moves its actual GPU thumb and fill");
        slider.Options=new(0,100,10,Size:UiSliderSize.Small);slider.Size=new(200,24);
        var small=capture("slider-small-stepped",Draw);
        check(!small.SequenceEqual(changed),"small stepped Slider changes actual rail, thumb and marker geometry");
        slider.Options=new(0,100,null,Vertical:true);slider.Size=new(32,160);
        var vertical=capture("slider-vertical",Draw);
        check(!vertical.SequenceEqual(small),"vertical Slider uses a distinct actual GPU layout");
    }
}
