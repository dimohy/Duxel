using Duxel.Core;

internal static class ProgressMotionTests
{
    internal static long ImmediateConstructionBytes,AnimatedConstructionBytes;
    internal static void Run(Action<bool,string> check)
    {
        static double Reference(double x)
        {
            var low=0d;var high=1d;
            for(var i=0;i<48;i++)
            {
                var parameter=(low+high)/2;var inverse=1-parameter;
                var curveX=3*inverse*inverse*parameter*.25+3*inverse*parameter*parameter*.25+parameter*parameter*parameter;
                if(curveX<x)low=parameter;else high=parameter;
            }
            var t=(low+high)/2;var one=1-t;
            return 3*one*one*t*.1+3*one*t*t+t*t*t;
        }
        var error=0d;
        for(var i=0;i<=4096;i++)error=Math.Max(error,Math.Abs(UiFluentProgressBehavior.CssEase(i/4096f)-Reference(i/4096d)));
        check(error<.000001,"CSS ease solver agrees with independent cubic-Bezier inversion across4097 samples");
        var state=default(UiFluentProgressBehavior);
        UiProgressValue Evaluate(float? value,double time,out bool active,float maximum=1)=>
            UiFluentProgressBehavior.Evaluate(ref state,new(value,maximum),time,out active);
        check(Evaluate(0,0,out var active).Fraction==0&&!active,"initial determinate progress has no entry transition");
        var start=Evaluate(1,1,out active);
        var middle=Evaluate(1,1.15,out active);
        var middleFraction=middle.Fraction??throw new InvalidOperationException("Determinate progress lost its display fraction.");
        check(start.Fraction==0&&active&&Math.Abs(middleFraction-Reference(.5))<.000001,
            "progress width follows the official300ms CSS ease curve");
        var retarget=Evaluate(.5f,1.15,out active);
        check(Math.Abs(retarget.Fraction!.Value-middleFraction)<.000001&&active,
            "retargeting progress begins at its current interpolated width without a jump");
        check(Evaluate(.5f,1.45,out active).Fraction==.5f&&!active,
            "a completed transition reaches its exact target and stops requesting frames");
        Evaluate(.8f,2,out active);
        check(Evaluate(.01f,2.01,out active).Fraction==.01f&&!active,
            "reset values at the official threshold skip the transition");
        Evaluate(1,3,out active);
        check(Evaluate(null,3.1,out active).Fraction is null&&!active
            &&Evaluate(.7f,3.2,out active).Fraction==.7f&&!active,
            "indeterminate mode releases the previous determinate transition state");
        var independent=default(UiFluentProgressBehavior);
        check(UiFluentProgressBehavior.Evaluate(ref independent,new(.9f),1.1,out active).Fraction==.9f&&!active,
            "separate widgets do not share progress animation state");
        state=default;Evaluate(0,0,out active);
        check(Evaluate(.5f,1,out active,100).Fraction==0&&active,
            "the reset threshold uses the clamped value rather than its normalized fraction");
        state=default;Evaluate(0,0,out active);Evaluate(1,1.1,out active);
        check(Evaluate(1,1.4,out active).Fraction==1&&!active,
            "deadline rounding does not schedule an extra frame after the displayed target is exact");

        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        using var progress=Fluent.ProgressBar(0);progress.Size=new(200,4);progress.Shape=UiProgressShape.Square;
        var requests=0;context.SetRequestFrameCallback(()=>requests++);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        void Frame(bool hidden=false,bool empty=false)
        {
            context.NewFrame(new(.1f,new(320,240),new(1,1)),idle,new(0,0,320,240),UiTextSettings.Default);
            context.Render(new Screen(ui=>{if(empty)return;ui.SetCursorScreenPos(hidden?new(1000,1000):new(10,10));progress.Render(ui);}));
            context.GetDrawData().ReleasePooled();
        }
        Frame();progress.Value=1;Frame();
        check(context.State.HasActiveAnimations&&progress.Value==1,
            "the generated widget schedules its owned transition while preserving the logical source value");
        requests=0;Frame(hidden:true);
        check(!context.State.HasActiveAnimations&&requests==0,
            "a clipped determinate transition does not keep scheduling frames");
        Frame();Frame();requests=0;Frame();
        check(!context.State.HasActiveAnimations&&requests==0,"a visible completed transition leaves the event-driven loop idle");
        progress.Value=.5f;Frame();requests=0;Frame(empty:true);
        check(!context.State.HasActiveAnimations&&requests==0,
            "removing an animating widget leaves no global animation registration to drain");

        static long Construction(bool animated)
        {
            var views=new UiWidget[1024];
            var before=GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<views.Length;i++)views[i]=animated
                ?Fluent.ProgressBar(.5f,identity:UiWidgetIdentity.Parent)
                :new UiProgress<UiFluentProgressPresentation>(.5f,identity:UiWidgetIdentity.Parent);
            var bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            foreach(var view in views)view.Dispose();
            return bytes;
        }
        Construction(false);Construction(true);
        ImmediateConstructionBytes=Construction(false);AnimatedConstructionBytes=Construction(true);
        check(AnimatedConstructionBytes>ImmediateConstructionBytes,
            "owned transition storage has an explicit measured construction cost rather than a hidden global cache");
        ReleaseProbe.Releases=0;
        var disposable=new UiProgress<ReleaseProbe,UiFluentProgressPresentation>(.5f);
        disposable.Dispose();disposable.Dispose();
        check(ReleaseProbe.Releases==1,"widget disposal releases a statically selected user behavior exactly once");
    }
    private struct ReleaseProbe:IUiProgressBehavior<ReleaseProbe>
    {
        internal static int Releases;
        public static UiProgressValue Evaluate(ref ReleaseProbe state,UiProgressValue target,double timeSeconds,out bool animating)
        {animating=false;return target;}
        public static void Release(ref ReleaseProbe state)=>Releases++;
    }
    private sealed class Screen(Action<UiImmediateContext> render):UiScreen
    {public override void Render(UiImmediateContext ui)=>render(ui);}
}
