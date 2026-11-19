using System.Diagnostics;
using System.Runtime.CompilerServices;
using Duxel.Core;

internal static class ControlOwnershipTests
{
    internal static void Run(Action<bool,string> check)
    {
        var glyphs=Enumerable.Range(32,95).ToDictionary(c=>c,c=>new UiGlyphInfo(7,0,-10,c==32?0:6,12,new(0,0,1,1)));
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255],glyphs,new Dictionary<uint,float>(),12,-3,1,-1),new(1),new(2));
        ControlFontFixture.Configure(context);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        void Frame(IUiView view)
        {
            context.NewFrame(new(1f/60,new(320,240),new(1,1)),idle,new(0,0,320,240),UiTextSettings.Default);
            context.Render(new Screen(view));context.GetDrawData().ReleasePooled();
        }
        static bool Disposed(Action action){try{action();return false;}catch(ObjectDisposedException){return true;}}
        static bool Invalid(Action action){try{action();return false;}catch(ArgumentException){return true;}}
        var requests=0;context.SetRequestFrameCallback(()=>requests++);
        using var progress=Fluent.ProgressBar(.2f);Frame(progress);
        var observed=0f;context.SetRequestFrameCallback(()=>{requests++;observed=progress.Value??-1;});Frame(progress);
        var before=requests;progress.Value=.6f;progress.Value=.6f;progress.Size=new(200,4);progress.Size=new(200,4);
        check(requests-before==2&&observed==.6f,"Progress source/size publish before waking idle once per change");
        before=requests;
        check(Invalid(()=>progress.Maximum=0)&&progress.Maximum==1&&requests==before,
            "invalid Progress source updates preserve existing state and do not request frames");
        var sourceReads=0;using var dynamicProgress=Fluent.ProgressBar(()=>{sourceReads++;return new(.25f);});Frame(dynamicProgress);
        var replacementReads=0;Func<UiProgressValue> replacement=()=>{replacementReads++;return new(.75f);};
        before=requests;dynamicProgress.Source=replacement;dynamicProgress.Source=replacement;
        check(requests-before==1&&replacementReads==0,"replacing a Progress getter wakes once without evaluating runtime data");
        Frame(dynamicProgress);check(sourceReads==1&&replacementReads==1,"Progress reads the selected getter exactly once per rendered frame");
        using var slider=Fluent.Slider("Slider",new UiState<float>(25).Binding);Frame(slider);
        before=requests;slider.Label="Updated";slider.Label="Updated";slider.Options=slider.Options with {Step=5};
        slider.Options=slider.Options;slider.Size=new(220,32);slider.Size=new(220,32);
        check(requests-before==3,"Slider label/options/size notify an idle host once per actual change");
        var options=slider.Options;before=requests;
        check(Invalid(()=>slider.Options=options with {Maximum=options.Minimum})&&slider.Options==options&&requests==before,
            "invalid Slider options preserve state and scheduling");
        check(Invalid(()=>slider.Label=null!)&&slider.Label=="Updated"&&requests==before,
            "Slider rejects a null label consistently with construction");
        using var toggle=Fluent.Checkbox("Toggle",new UiState<bool>(false).Binding);Frame(toggle);
        before=requests;toggle.Label="Updated toggle";toggle.Label="Updated toggle";
        check(requests-before==1,"Toggle labels use common idle invalidation without replacing their input identity");
        check(Invalid(()=>toggle.Label=null!)&&toggle.Label=="Updated toggle","Toggle rejects a null label without replacing valid content");
        before = requests;
        var warmStart = Stopwatch.GetTimestamp();
        var warmCalls = 0;
        do
        {
            MeasureIdenticalAssignments(progress, slider, toggle, options);
            warmCalls++;
        } while (Stopwatch.GetElapsedTime(warmStart).TotalSeconds < 0.25);
        var bytes = MeasureIdenticalAssignments(progress, slider, toggle, options);
        Console.WriteLine($"Control assignment allocation : {bytes}; warm calls : {warmCalls}; requests : {requests - before}");
        if (bytes != 0 || requests != before)
            throw new InvalidOperationException($"Control unchanged assignments : allocatedBytes = {bytes}, requests = {requests - before}");
        check(true,"ten thousand unchanged Progress/Slider/Toggle assignments allocate zero bytes and request no frames");
        dynamicProgress.Dispose();progress.Dispose();slider.Dispose();toggle.Dispose();
        check(dynamicProgress.Source is null&&slider.Label.Length==0&&toggle.Label.Length==0,
            "control disposal clears Progress getter and Slider/Toggle labels");
        check(Disposed(()=>dynamicProgress.Source=replacement)&&Disposed(()=>progress.Value=.8f)&&Disposed(()=>progress.Size=new(1,1))
            &&Disposed(()=>slider.Label="After")&&Disposed(()=>slider.Options=options)&&Disposed(()=>slider.Size=new(1,1))
            &&Disposed(()=>toggle.Label="After"),"disposed controls reject new source/layout ownership");
        check(Disposed(()=>_ = slider.Value)&&Disposed(()=>_ = toggle.Value),"disposed Slider/Toggle getters cannot expose a released binding");
        var state=new UiState<bool>(true);using var skipped=new SkippingToggle("Skip",state.Binding);skipped.Dispose();
        state.Value=false;
        check(Disposed(()=>_ = skipped.Value)&&!state.Value,"a skipped base disposal hook releases Toggle binding without disposing caller-owned state");
        using var failedSlider=new FailingSlider("Failure",new UiState<float>(10).Binding);var failed=false;
        try{failedSlider.Dispose();}catch(AggregateException){failed=true;}
        check(failed&&Disposed(()=>_ = failedSlider.Value),"a failing user disposal hook still releases Slider binding");
        FailingProgressBehavior.Releases=0;
        using var failedProgress=new FailingProgress(static()=>new(.5f));failed=false;
        try{failedProgress.Dispose();}catch(AggregateException error){failed=error.InnerExceptions.Count==2;}
        check(failed&&failedProgress.Source is null&&FailingProgressBehavior.Releases==1,
            "user and behavior disposal failures remain observable while Progress source release still runs exactly once");
        var releasedSlider=ReleasedSlider();var releasedToggle=ReleasedToggle();
        // Lifetime collection checks are separate from all allocation/performance measurements.
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        check(!releasedSlider.State.TryGetTarget(out _)&&!releasedToggle.State.TryGetTarget(out _),
            "retaining disposed Slider/Toggle objects does not retain otherwise-unowned bound state graphs");
        GC.KeepAlive(releasedSlider.Widget);GC.KeepAlive(releasedToggle.Widget);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureIdenticalAssignments(UiFluentProgressBar progress, UiFluentSlider slider,
        UiFluentCheckbox toggle, UiSliderOptions options)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++)
        {
            progress.Value = .6f;
            slider.Label = "Updated";
            slider.Options = options;
            toggle.Label = "Updated toggle";
        }
        return GC.GetAllocatedBytesForCurrentThread() - allocated;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (UiFluentSlider Widget,WeakReference<UiState<float>> State) ReleasedSlider()
    {var state=new UiState<float>(10);var widget=Fluent.Slider("Release",state.Binding);widget.Dispose();return(widget,new(state));}
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (UiFluentSwitch Widget,WeakReference<UiState<bool>> State) ReleasedToggle()
    {var state=new UiState<bool>(true);var widget=Fluent.Switch("Release",state.Binding);widget.Dispose();return(widget,new(state));}
    private sealed class Screen(IUiView view):UiScreen{public override void Render(UiImmediateContext ui)=>view.Render(ui);}
    private sealed class SkippingToggle(string label,UiBinding<bool> value):UiFluentCheckbox(label,value)
    {protected override void OnDispose(){}}
    private sealed class FailingSlider(string label,UiBinding<float> value):UiFluentSlider(label,value)
    {protected override void OnDispose()=>throw new InvalidOperationException("Declared user cleanup failure.");}
    private sealed class FailingProgress(Func<UiProgressValue> source):UiProgress<Func<UiProgressValue>,UiGetterProgressSource,FailingProgressBehavior,UiFluentProgressPresentation>(source)
    {protected override void OnDispose()=>throw new InvalidOperationException("Declared user cleanup failure.");}
    private struct FailingProgressBehavior:IUiProgressBehavior<FailingProgressBehavior>
    {
        internal static int Releases;
        public static UiProgressValue Evaluate(ref FailingProgressBehavior state,UiProgressValue value,double time,out bool animating){animating=false;return value;}
        public static void Release(ref FailingProgressBehavior state){Releases++;throw new InvalidOperationException("Declared behavior cleanup failure.");}
    }
}
