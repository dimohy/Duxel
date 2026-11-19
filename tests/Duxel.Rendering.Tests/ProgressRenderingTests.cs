using Duxel.Core;

internal static class ProgressRenderingTests
{
    internal static void Run(Func<string,Action<UiImmediateContext>,byte[]> capture,Func<Action<UiImmediateContext>,UiDrawData> build,
        Action<bool,string> check,int width,int height)
    {
        using var progress=Fluent.ProgressBar(.35f);progress.Size=new(200,4);
        var widget=capture("progress-generated",ui=>{ui.SetCursorScreenPos(new(20,20));progress.Render(ui);});
        var direct=capture("progress-immediate",ui=>
        {
            ui.SetCursorScreenPos(new(20,20));
            ui.ProgressBar<UiFluentProgressPresentation>(new(.35f),new(200,4));
        });
        check(widget.SequenceEqual(direct),"generated and typed-immediate ProgressBar produce identical GPU pixels");
        progress.Value=1;
        var beginning=capture("progress-transition-start",ui=>{ui.SetCursorScreenPos(new(20,20));progress.Render(ui);});
        check(beginning.SequenceEqual(widget),"a progress transition starts at the currently displayed GPU width");
        for(var frame=0;frame<20;frame++)using(var data=build(progress.Render)){}
        var complete=capture("progress-complete",ui=>{ui.SetCursorScreenPos(new(20,20));progress.Render(ui);});
        check(!complete.SequenceEqual(widget),"changing progress value updates actual GPU fill pixels");
        progress.Color=UiProgressColor.Error;
        var error=capture("progress-error",ui=>{ui.SetCursorScreenPos(new(20,20));progress.Render(ui);});
        check(!error.SequenceEqual(complete),"progress status color changes the actual GPU surface");
        var empty=capture("progress-empty",static _=>{});
        progress.Value=null;progress.Shape=UiProgressShape.Square;
        var animated=capture("progress-gradient",ui=>{ui.SetCursorScreenPos(new(20,20));progress.Render(ui);});
        var escaped=0;
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
        {
            if(x>=20&&x<220&&y>=20&&y<24)continue;
            var index=(y*width+x)*4;
            if(!animated.AsSpan(index,4).SequenceEqual(empty.AsSpan(index,4)))escaped++;
        }
        check(escaped==0,"moving progress gradient cannot paint outside its root bounds on the GPU");
        progress.ReducedMotion=true;
        var reduced=capture("progress-reduced-motion",ui=>{ui.SetCursorScreenPos(new(20,20));progress.Render(ui);});
        check(!reduced.SequenceEqual(animated),"reduced-motion opacity pulse produces a distinct actual GPU frame");
    }
}
