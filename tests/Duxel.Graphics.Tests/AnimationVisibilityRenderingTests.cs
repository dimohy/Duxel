using Duxel.Core;

internal static class AnimationVisibilityRenderingTests
{
    internal static void Run(Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check)
    {
        using var spinner=new UiSpinner<FixedSpinner,UiFluentSpinnerPresentation>(options:new(){Width=64});
        void Root(UiImmediateContext ui)
        {ui.EnableRootViewportContentLayout();ui.SetCursorScreenPos(new(10,10));spinner.Render(ui);}
        var empty=capture("visibility-empty",ui=>ui.EnableRootViewportContentLayout());
        var reference=capture("visibility-reference",Root);
        check(!reference.AsSpan().SequenceEqual(empty),"visible reference contains actual Spinner GPU pixels");
        var outer=new UiLayerOptions(true,1,default){ContentVersion=1};
        var inner=new UiLayerOptions(true,1,default){ContentVersion=1};
        var nested=false;
        var tightClip=false;
        void Layers(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout();
            if(ui.BeginLayer("visibility-gpu-outer",outer))
            {
                using var clip=new UiControlContext(ui).Clip(tightClip?new(0,0,70,480):new(-2000,-2000,4000,4000));
                if(nested)
                {
                    if(ui.BeginLayer("visibility-gpu-inner",inner))Root(ui);
                    ui.EndLayer();
                }
                else Root(ui);
            }
            ui.EndLayer();
        }
        var pixels=capture("visibility-layer",Layers);
        check(pixels.AsSpan().SequenceEqual(reference),"retained visible animation preserves the entire direct GPU image");
        outer=outer with{Translation=new(1000,0)};pixels=capture("visibility-offscreen",Layers);
        check(pixels.AsSpan().SequenceEqual(empty),"translated offscreen animation produces no visible GPU pixels");
        outer=outer with{Translation=default};pixels=capture("visibility-return",Layers);
        check(pixels.AsSpan().SequenceEqual(reference),"returning a cached animation restores the exact direct GPU image");
        outer=outer with{Opacity=0};pixels=capture("visibility-transparent",Layers);
        check(pixels.AsSpan().SequenceEqual(empty),"transparent cached animation produces no visible GPU pixels");
        outer=outer with{Opacity=1};pixels=capture("visibility-opacity-return",Layers);
        check(pixels.AsSpan().SequenceEqual(reference),"restoring opacity restores the exact cached GPU image");
        nested=true;outer=outer with{Translation=new(1000,0),ContentVersion=2};inner=inner with{Translation=new(-1000,0)};
        pixels=capture("visibility-nested-cancel",Layers);
        check(pixels.AsSpan().SequenceEqual(reference),"opposite nested translations preserve the exact GPU image used by visibility scheduling");
        outer=outer with{Translation=new(40,0),ContentVersion=3};inner=inner with{Translation=new(20,0)};
        pixels=capture("visibility-nested-sum",Layers);
        var shifted=capture("visibility-shifted-reference",ui=>
        {ui.EnableRootViewportContentLayout();ui.SetCursorScreenPos(new(70,10));spinner.Render(ui);});
        check(pixels.AsSpan().SequenceEqual(shifted),"nested translations add without changing retained geometry or visible GPU pixels");
        tightClip=true;outer=outer with{ContentVersion=4};pixels=capture("visibility-nested-clip",Layers);
        var clipped=capture("visibility-clipped-reference",ui=>
        {ui.EnableRootViewportContentLayout();using var clip=new UiControlContext(ui).Clip(new(40,0,70,480));ui.SetCursorScreenPos(new(70,10));spinner.Render(ui);});
        check(!clipped.AsSpan().SequenceEqual(empty)&&!clipped.AsSpan().SequenceEqual(shifted),"clip reference contains a partially visible ring rather than an empty or unclipped image");
        check(pixels.AsSpan().SequenceEqual(clipped),"nested clip intersection uses command-local coordinates after both translations");
        tightClip=false;outer=outer with{Opacity=.5f,ContentVersion=5};inner=inner with{Opacity=.5f};
        pixels=capture("visibility-nested-opacity",Layers);
        var composed=capture("visibility-composed-opacity-reference",ui=>
        {
            ui.EnableRootViewportContentLayout();
            if(ui.BeginLayer("visibility-opacity-reference",new(true,.25f,new(60,0)){ContentVersion=1}))Root(ui);
            ui.EndLayer();
        });
        check(pixels.AsSpan().SequenceEqual(composed),"nested opacity multiplication preserves the exact GPU image");
    }

    private struct FixedSpinner:IUiSpinnerBehavior<FixedSpinner>
    {
        public static bool IsVisible(in FixedSpinner state,UiSpinnerOptions options,double time)=>true;
        public static UiSpinnerFrame Evaluate(ref FixedSpinner state,UiSpinnerOptions options,double time)=>new(true,0,-135,0,0,false,false);
    }
}
