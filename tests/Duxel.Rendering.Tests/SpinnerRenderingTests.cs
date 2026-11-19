using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class SpinnerRenderingTests
{
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();var originalSamples=renderer.ValidationRequestedMsaaSamples;
        try
        {
            foreach(var samples in new[]{1,4})foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())foreach(var vector in new[]{false,true})
            {
                renderer.SetMsaaSamples(samples);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                foreach(var size in Enum.GetValues<UiSpinnerSize>())foreach(var appearance in Enum.GetValues<UiSpinnerAppearance>())foreach(var position in Enum.GetValues<UiSpinnerLabelPosition>())
                {
                    var options=new UiSpinnerOptions(size,position,appearance);using var widget=Fluent.Spinner("Connecting …",options);
                    byte[] Draw(bool generated)=>capture($"spinner-{size}-{appearance}-{position}-{scheme}-{samples}-{vector}-{generated}",ui=>
                    {
                        ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                        if(vector)ui.BeginLayer("spinner.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                        try
                        {
                            ui.SetCursorScreenPos(new(10,10));
                            if(generated)widget.Render(ui);
                            else{var behavior=default(UiFluentSpinnerBehavior);ui.Spinner<UiFluentSpinnerBehavior,UiFluentSpinnerPresentation>("reference",ref behavior,"Connecting …",options);}
                        }
                        finally{if(vector)ui.EndLayer();}
                    });
                    var actual=Draw(true);var reference=Draw(false);
                    check(actual.AsSpan().SequenceEqual(reference),$"Spinner generated/direct pixels match {size},{appearance},{position},{scheme},MSAA{samples},vector={vector}");
                    check(HasContent(actual),$"Spinner renders ring and label {size},{appearance},{position},{scheme},MSAA{samples},vector={vector}");
                }
            }
            foreach(var samples in new[]{1,4})foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())
            {
                renderer.SetMsaaSamples(samples);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                var frames=new byte[4][];
                for(var phase=0;phase<4;phase++)
                {
                    var state=default(UiFluentSpinnerBehavior);UiFluentSpinnerBehavior.Evaluate(ref state,default,0);
                    var frame=UiFluentSpinnerBehavior.Evaluate(ref state,default,phase*.375);
                    frames[phase]=capture($"spinner-motion-{scheme}-{samples}-{phase}",ui=>
                    {
                        ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                        var context=new UiControlContext(ui);var options=new UiSpinnerOptions(UiSpinnerSize.Huge);
                        var layout=UiFluentSpinnerPresentation.Measure(context,null,options);
                        UiFluentSpinnerPresentation.Paint(context,options,frame,layout,new(10,10));
                    });
                }
                check(!frames[0].AsSpan().SequenceEqual(frames[1])&&!frames[1].AsSpan().SequenceEqual(frames[2]),$"Spinner real GPU pixels change across official motion phases {scheme},MSAA{samples}");
                var background=scheme==UiFluentColorScheme.Light?new UiColor(180,214,250):new UiColor(14,71,117);
                var foreground=scheme==UiFluentColorScheme.Light?new UiColor(15,108,189):new UiColor(71,158,245);
                check(Pixel(frames[0],width,32,32)==theme.WindowBg,$"Spinner ring preserves the center hole {scheme},MSAA{samples}");
                check(Pixel(frames[0],width,27,13)==foreground&&Pixel(frames[0],width,32,51)==background,$"Spinner independent interior samples match official brand tokens {scheme},MSAA{samples}");
                foreach(var reduced in new[]{false,true})foreach(var rtl in new[]{false,true})foreach(var appearance in Enum.GetValues<UiSpinnerAppearance>())
                {
                    var options=new UiSpinnerOptions(UiSpinnerSize.Huge,Appearance:appearance,ReducedMotion:reduced,RightToLeft:rtl);
                    var previous=Array.Empty<byte>();
                    for(var phase=0;phase<3;phase++)
                    {
                        var behavior=default(UiFluentSpinnerBehavior);UiFluentSpinnerBehavior.Evaluate(ref behavior,options,0);
                        var frame=UiFluentSpinnerBehavior.Evaluate(ref behavior,options,phase*(reduced?1.8:1.5)/4);
                        var pixels=capture($"spinner-variant-motion-{scheme}-{samples}-{reduced}-{rtl}-{appearance}-{phase}",ui=>
                        {
                            ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                            var context=new UiControlContext(ui);var layout=UiFluentSpinnerPresentation.Measure(context,null,options);
                            UiFluentSpinnerPresentation.Paint(context,options,frame,layout,new(10,10));
                        });
                        check(HasContent(pixels),$"Spinner variant produces GPU pixels {scheme},MSAA{samples},reduced={reduced},RTL={rtl},{appearance},phase={phase}");
                        if(phase>0)check(!pixels.AsSpan().SequenceEqual(previous),$"Spinner variant advances real pixels {scheme},MSAA{samples},reduced={reduced},RTL={rtl},{appearance},phase={phase}");
                        previous=pixels;
                    }
                }
            }
        }
        finally{setTheme(original);renderer.SetMsaaSamples(originalSamples);}
    }
    private static UiColor Pixel(byte[] data,int width,int x,int y)
    {var offset=(y*width+x)*4;return new(data[offset],data[offset+1],data[offset+2],data[offset+3]);}
    private static bool HasContent(byte[] pixels)
    {for(var i=4;i<pixels.Length;i+=4)if(pixels[i]!=pixels[0]||pixels[i+1]!=pixels[1]||pixels[i+2]!=pixels[2])return true;return false;}
}
