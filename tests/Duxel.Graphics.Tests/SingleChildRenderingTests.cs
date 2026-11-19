using Duxel.Core;

internal static class SingleChildRenderingTests
{
    internal static void Run(Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check)
    {
        var blue=new UiColor(0,120,220);
        void Equal(string name,UiWidget widget,UiRect expected)
        {
            var actual=capture(name,ui=>{Root(ui);widget.Render(ui);});
            var reference=capture(name+"-reference",ui=>{Root(ui);Paint(ui,expected,blue);});
            check(actual.AsSpan().SequenceEqual(reference),name+" matches independent GPU geometry");
        }
        using var padding=Dux.SizedBox(Dux.Padding(new Block(new(20,10)),new(3,5,7,11)),100,60);
        Equal("single-padding-tight",padding,new(3,5,90,44));
        using var align=Dux.SizedBox(Dux.Align(new Block(new(20,10)),UiAlignment.BottomRight),100,60);
        Equal("single-align",align,new(80,50,20,10));
        using var center=Dux.Center(new Block(new(20,10)),2,3);
        Equal("single-center-factors",center,new(10,10,20,10));
        using var constrained=Dux.ConstrainedBox(new Block(new(20,10)),new(50,80,30,60));
        Equal("single-constrained",constrained,new(0,0,50,30));
        using var empty=Dux.SizedBox(width:30,height:20);
        var blank=capture("single-empty-reference",Root);
        var emptyImage=capture("single-empty",ui=>{Root(ui);empty.Render(ui);});
        check(emptyImage.AsSpan().SequenceEqual(blank),"empty SizedBox reserves layout space without emitting geometry");
        using var zero=Dux.Center(new Block(new(20,10)),0,0);
        var zeroImage=capture("single-center-zero-clipped",ui=>{Root(ui);zero.Render(ui);});
        check(zeroImage.AsSpan().SequenceEqual(blank),"zero alignment factors clip the oversized child to the zero-sized surface");

        var inner=Dux.Padding(Dux.Align(Fluent.Button("Wrapped"),UiAlignment.TopLeft),new(8));
        using var button=Dux.SizedBox(inner,180,60);
        var actual=capture("single-fluent-button",ui=>{Root(ui);button.Render(ui);});
        var reference=capture("single-fluent-button-reference",ui=>
        {Root(ui);ui.SetCursorScreenPos(new(8,8));ui.Button<UiReleaseButtonBehavior,UiFluentButtonPresentation>("Wrapped");});
        check(actual.AsSpan().SequenceEqual(reference),"nested single-child wrappers preserve the actual Fluent button glyph and surface image");
        inner.Options=new(12);
        var changed=capture("single-fluent-button-updated",ui=>{Root(ui);button.Render(ui);});
        reference=capture("single-fluent-button-updated-reference",ui=>
        {Root(ui);ui.SetCursorScreenPos(new(12,12));ui.Button<UiReleaseButtonBehavior,UiFluentButtonPresentation>("Wrapped");});
        check(changed.AsSpan().SequenceEqual(reference),"live Padding changes update the nested real Fluent button image");
        var replay=true;for(var i=0;i<4;i++)replay&=reference.AsSpan().SequenceEqual(capture("single-fluent-button-replay-"+i,ui=>{Root(ui);button.Render(ui);}));
        check(replay,"updated single-child geometry remains correct across acquired frame images");
    }
    private static void Root(UiImmediateContext ui){ui.EnableRootViewportContentLayout(contentPadding:0);ui.SetCursorScreenPos(default);}
    private static void Paint(UiImmediateContext ui,UiRect bounds,UiColor color)
    {using var clip=new UiControlContext(ui).Clip(bounds);ui.GetWindowDrawList().AddRectFilled(bounds,color,ui.WhiteTextureId);}
    private sealed class Block(UiVector2 size):UiControlWidget
    {
        protected override bool CanRetainMeasurement=>true;
        protected override UiVector2 MeasureContent(UiControlContext context)=>size;
        protected override void RenderControl(UiImmediateContext ui)
        {var p=ui.GetCursorScreenPos();var allocated=new UiControlContext(ui).ConstrainToLayout(size);Paint(ui,new(p.X,p.Y,allocated.X,allocated.Y),new(0,120,220));ui.Dummy(allocated);}
    }
}
