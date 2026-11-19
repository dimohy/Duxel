using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class ControlTextBlockRenderingTests
{
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,int width,int height)
    {
        var samples=renderer.ValidationRequestedMsaaSamples;var theme=readTheme();
        try
        {
            foreach(var msaa in new[]{1,4})foreach(var vector in new[]{false,true})foreach(var center in new[]{false,true})
            {
                renderer.SetMsaaSamples(msaa);
                byte[] Draw(bool block)=>capture($"text-block-{block}-{msaa}-{vector}-{center}",ui=>
                {
                    ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                    if(vector)ui.BeginLayer("text-block.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                    try
                    {
                        ui.SetCursorScreenPos(new(12,12));ui.Dummy(new(180,100));
                        var context=new UiControlContext(ui);using var typography=context.BeginTypography<UiRegularTypeface,UiFluentType300>();
                        using var cell=context.Clip(new(20,20,140,60));
                        if(block)
                        {
                            var measured=context.MeasureTextBlock("Alpha\nBeta\nGamma");
                            context.TypographyTextBlock(measured,new(20,20,140,60),theme.Text,center?UiItemHorizontalAlign.Center:UiItemHorizontalAlign.Left);
                        }
                        else
                        {
                            var lines=new[]{"Alpha","Beta","Gamma"};
                            for(var i=0;i<lines.Length;i++)
                            {
                                var size=context.MeasureText(lines[i]) with{Y=20};
                                context.TypographyButtonText(lines[i],new(20,20+i*20,140,20),center?size:size with{X=140},theme.Text);
                            }
                        }
                    }
                    finally{if(vector)ui.EndLayer();}
                });
                var block=Draw(true);var reference=Draw(false);
                check(block.AsSpan().SequenceEqual(reference),$"retained text block matches independent explicit20px line placement: {msaa}/{vector}/{center}");
                for(var line=0;line<3;line++)
                {
                    var ink=false;
                    for(var y=20+line*20;y<40+line*20;y++)for(var x=20;x<160;x++)
                    {
                        var offset=(y*width+x)*4;
                        ink|=block[offset]!=block[0]||block[offset+1]!=block[1]||block[offset+2]!=block[2];
                    }
                    check(ink,$"text block paints actual line{line} without clipping the first or final line: {msaa}/{vector}/{center}");
                }
            }
        }
        finally{renderer.SetMsaaSamples(samples);}
    }
}
