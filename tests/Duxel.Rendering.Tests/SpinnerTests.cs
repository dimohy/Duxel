using Duxel.Core;
using Duxel.Platform.Windows;

internal static class SpinnerTests
{
    internal static void Run(Action<bool,string> check)
    {
        var behavior=default(UiFluentSpinnerBehavior);
        var first=UiFluentSpinnerBehavior.Evaluate(ref behavior,default,0);
        var half=UiFluentSpinnerBehavior.Evaluate(ref behavior,default,.75);
        check(first.Visible&&first.Before==0&&first.After==0&&first.TailRotation==-135&&first.Rotation==0,"Spinner starts with the official30-degree tail and no entry fade");
        check(MathF.Abs(half.Before-105)<.0001f&&MathF.Abs(half.After-225)<.0001f&&MathF.Abs(half.Rotation-180)<.0001f,"Spinner expands to255degrees at the official1500ms cycle midpoint");
        var quarter=UiFluentSpinnerBehavior.Evaluate(ref behavior,default,1.875);
        check(MathF.Abs(quarter.Before-52.5f)<.001f&&MathF.Abs(quarter.After-112.5f)<.001f,"Spinner tail uses the symmetric official EasyEase curve");
        behavior=default;var delay=new UiSpinnerOptions(DelaySeconds:2);
        check(!UiFluentSpinnerBehavior.Evaluate(ref behavior,delay,0).Visible&&!UiFluentSpinnerBehavior.Evaluate(ref behavior,delay,1).Visible,"Spinner delay hides spinner and label before its deadline");
        check(UiFluentSpinnerBehavior.IsVisible(behavior,delay,2)&&UiFluentSpinnerBehavior.Evaluate(ref behavior,delay,2).Rotation==0,"Spinner starts its animation when delayed content becomes visible");
        check(UiFluentSpinnerBehavior.Evaluate(ref behavior,delay with{DelaySeconds=20},3).Visible,"a shown Spinner stays shown when delay changes");
        behavior=default;UiFluentSpinnerBehavior.Evaluate(ref behavior,new(DelaySeconds:1),0);
        UiFluentSpinnerBehavior.Evaluate(ref behavior,new(DelaySeconds:2),.5);
        check(!UiFluentSpinnerBehavior.IsVisible(behavior,new(DelaySeconds:2),2)&&UiFluentSpinnerBehavior.IsVisible(behavior,new(DelaySeconds:2),2.5),"changing delay before visibility restarts the pending delay");
        behavior=default;UiFluentSpinnerBehavior.Evaluate(ref behavior,new(ReducedMotion:true),0);
        check(MathF.Abs(UiFluentSpinnerBehavior.Evaluate(ref behavior,new(ReducedMotion:true),.9).Rotation-180)<.0001f,"reduced motion retains the official1800ms single-ring rotation");
        var paused = new UiSpinnerOptions { Paused = true };
        var frozen = UiFluentSpinnerBehavior.Evaluate(ref behavior, paused, 1);
        check(UiFluentSpinnerBehavior.Evaluate(ref behavior, paused, 10) == frozen,
            "explicit pause freezes all Spinner geometry across unrelated frames");
        var resumed = UiFluentSpinnerBehavior.Evaluate(ref behavior, default, 10);
        check(resumed.Rotation == frozen.Rotation && UiFluentSpinnerBehavior.Evaluate(ref behavior, default, 10.25).Rotation != resumed.Rotation,
            "Spinner resumes its paused phase without including paused wall time");
        foreach(var invalid in new[]{new UiSpinnerOptions(Size:(UiSpinnerSize)8),new UiSpinnerOptions(DelaySeconds:-1),new UiSpinnerOptions(DelaySeconds:float.NaN),new UiSpinnerOptions(LabelPosition:(UiSpinnerLabelPosition)4),new UiSpinnerOptions{Width=float.NaN}})
        {var rejected=false;try{invalid.Validate();}catch(ArgumentOutOfRangeException){rejected=true;}check(rejected,"Spinner rejects unsupported options explicitly");}
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));ControlFontFixture.Configure(context);
        using var host=new WindowsAccessibility(0);context.SetAccessibilityTree(host.Tree);
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        void Step(Action<UiImmediateContext> draw)
        {context.NewFrame(new(1f/60,new(640,480),new(1,1)),idle,new(0,0,640,480),UiTextSettings.Default);context.Render(new Screen(draw));using var data=context.GetDrawData();}
        using var spinner=Fluent.Spinner("Connecting to data …");
        spinner.Options = paused;
        Step(spinner.Render);
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "visible paused Spinner has no animation or future frame request");
        spinner.Options = default;
        Step(spinner.Render);
        check(context.State.HasActiveAnimations, "default Spinner retains its animation contract");
        spinner.Options = paused;
        Step(spinner.Render);
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "pausing a playing Spinner clears continuous scheduling in its next frame");
        using var fixedProgress = Fluent.ProgressBar(() => new(.35f, 1));
        Step(fixedProgress.Render);
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "fixed determinate progress does not schedule continuous rendering");
        foreach(var size in Enum.GetValues<UiSpinnerSize>())foreach(var position in Enum.GetValues<UiSpinnerLabelPosition>())
        {
            spinner.Options=new(size,position);Step(ui=>
            {
                var cursor=ui.GetCursorScreenPos();var available=MathF.Max(0,ui.GetContentRegionAvail().X);var measured=spinner.Measure(ui,new(available,400));
                check(spinner.Measure(ui,new(300,400)).X>=300,"Spinner block root fills its supplied width constraint");
                check(cursor==ui.GetCursorScreenPos(),"Spinner measure preserves the caller cursor");
                spinner.Render(ui);var actual=ui.GetItemRectSize();
                check(measured==actual,$"Spinner generated layout matches pure measurement under identical constraints size={size},position={position},measured={measured},actual={actual}");
            });
        }
        var root=host.Tree.Root.First!;host.Tree.TryGet(root,out var semantic);
        check(semantic.Node.Role==UiAccessibilityRole.ProgressBar&&semantic.Node.Name==spinner.Label&&!semantic.Node.Focusable&&!semantic.Node.ValuePattern,"actual Spinner exposes a named indeterminate progress semantic without input patterns");
        var provider=host.Find(root)!;provider.GetPropertyValue(30003,out var type);provider.GetPatternProvider(10002,out var valuePattern);
        check(type.Data.Integer==50012&&valuePattern==0,"native Spinner provider exposes ProgressBar control type without a fabricated determinate value");
        spinner.Label=null;spinner.AccessibleName="Downloading files";Step(spinner.Render);
        host.Tree.TryGet(root,out semantic);check(semantic.Node.Name=="Downloading files","a label-free Spinner accepts an explicit accessible name");
        using var delayed=Fluent.Spinner("Not visible",new(DelaySeconds:10));
        Step(ui=>{var measured=delayed.Measure(ui,new(640,480));delayed.Render(ui);check(measured.Y==0&&ui.GetItemRectSize().Y==0,"delayed Spinner pure measurement agrees with its empty root height");});
        using var custom=new RectSpinner("Same behavior");Step(custom.Render);
        check(Fluent.ComponentNames.Contains("Spinner")&&custom is UiSpinner<UiFluentSpinnerBehavior,RectSpinnerPresentation>,"source-generated Spinner and user presentation consume the same inheritable control contract");
        using var parentA=Fluent.Spinner("First task",identity:UiWidgetIdentity.Parent);using var parentB=Fluent.Spinner("Second task",identity:UiWidgetIdentity.Parent);
        Step(ui=>{parentA.Render(ui);parentB.Render(ui);});
        check(host.Tree.Count==2,"explicit Parent Spinner factories preserve distinct label identities in one caller scope");
        context.NewFrame(new(1f/60,new(640,480),new(1,1)),idle,new(0,0,640,480),UiTextSettings.Default);
        context.Render(new Screen(ui=>ui.GetBackgroundDrawList().AddAnnularSector(new(50,50),22,4,0,MathF.Tau,new(255,0,0),new(255,0,0),new(2),new(0,0,200,200))));
        using var mesh=context.GetDrawData();var vertices=mesh.DrawLists[0].Vertices.AsSpan();
        check(vertices.Length>300&&vertices.Length%4==0&&vertices[0].Color.Rgba>>24==0&&vertices[1].Color.Rgba>>24==255&&vertices[3].Color.Rgba>>24==0,"common annular mesh retains both radial coverage ramps and high geometric precision");
    }
    private sealed class Screen(Action<UiImmediateContext> draw):UiScreen
    {public override void Render(UiImmediateContext ui)=>draw(ui);}
}
[UiWidgetStyle(typeof(UiSpinner<,>),typeof(UiFluentSpinnerBehavior),typeof(RectSpinnerPresentation))]internal partial class RectSpinner;
internal readonly struct RectSpinnerPresentation:IUiSpinnerPresentation
{
    public static UiControlTypographyScope BeginLabel(UiControlContext context,UiSpinnerOptions options,bool hasLabel)=>default;
    public static UiSpinnerLayout Measure(UiControlContext context,string? label,UiSpinnerOptions options)=>new(new(24,12),new(0,0,24,12),default,default);
    public static void Paint(UiControlContext context,UiSpinnerOptions options,in UiSpinnerFrame frame,in UiSpinnerLayout layout,UiVector2 origin)=>
        context.Frame(new(origin.X,origin.Y,24,12),new(255,0,0),new(255,0,0),0,0);
}
