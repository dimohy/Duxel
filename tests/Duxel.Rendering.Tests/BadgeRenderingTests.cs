using Duxel.Core;
using Duxel.Vulkan;

internal static class BadgeRenderingTests
{
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();
        var originalSamples=renderer.ValidationRequestedMsaaSamples;
        var widgets=new UiFluentBadge[32];
        for(var i=0;i<widgets.Length;i++)
        {
            var options=new UiFluentBadgeOptions(Appearance:(UiBadgeAppearance)(i/8),Color:(UiBadgeColor)(i%8));
            widgets[i]=i%2==0?new UiFluentBadge("8",options):new InheritedBadge("8",options);
        }
        void Background(UiImmediateContext ui,UiTheme theme)=>ui.GetBackgroundDrawList().AddRectFilled(
            new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
        void Grid(UiImmediateContext ui,bool retained,UiTheme theme)
        {
            Background(ui,theme);
            for(var i=0;i<widgets.Length;i++)
            {
                ui.SetCursorScreenPos(new(12+(i/8)*72,10+(i%8)*28));
                if(retained)widgets[i].Render(ui);
                else ui.Badge("8",widgets[i].Options);
            }
        }
        try
        {
            foreach(var samples in new[]{1,4})
            foreach(var scheme in Enum.GetValues<UiBadgeColorScheme>())
            {
                renderer.SetMsaaSamples(samples);
                var theme=scheme==UiBadgeColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;
                setTheme(theme);
                var background=capture("badge-background-"+scheme+"-"+samples,ui=>Background(ui,theme));
                var direct=capture("badge-immediate-"+scheme+"-"+samples,ui=>Grid(ui,false,theme));
                var generated=capture("badge-generated-"+scheme+"-"+samples,ui=>Grid(ui,true,theme));
                var retained=capture("badge-retained-"+scheme+"-"+samples,ui=>Grid(ui,true,theme));
                check(direct.SequenceEqual(generated)&&generated.SequenceEqual(retained),
                    "all32 Badge appearances/colors preserve exact GPU pixels across immediate,generated,inherited and retained paths in "+scheme+" MSAA"+samples);
                var match=true;
                for(var i=0;i<widgets.Length;i++)
                {
                    var x=16+(i/8)*72;var y=20+(i%8)*28;var offset=(y*width+x)*4;
                    var paint=UiFluentBadgePalette.Resolve(scheme,widgets[i].Options.Appearance,widgets[i].Options.Color);
                    if((paint.Background.Rgba>>24)==0)match&=direct.AsSpan(offset,4).SequenceEqual(background.AsSpan(offset,4));
                    else match&=direct[offset]==(byte)paint.Background.Rgba&&direct[offset+1]==(byte)(paint.Background.Rgba>>8)
                        &&direct[offset+2]==(byte)(paint.Background.Rgba>>16)&&direct[offset+3]==255;
                }
                check(match,"actual GPU Badge interiors match all32 palette fill/transparent contracts in "+scheme+" MSAA"+samples);
            }
            using var counter=Fluent.CounterBadge(0);
            var empty=capture("counter-hidden",counter.Render);
            counter.Count=8;
            var visible=capture("counter-visible",counter.Render);
            counter.Count=108;
            var overflow=capture("counter-overflow",counter.Render);
            counter.Dot=true;
            var dot=capture("counter-dot",counter.Render);
            check(!empty.SequenceEqual(visible)&&!visible.SequenceEqual(overflow)&&!overflow.SequenceEqual(dot),
                "changing count,overflow and dot update actual GPU pixels without rebuilding the widget");
            counter.Count=0;counter.Dot=false;
            var removed=capture("counter-hidden-again",counter.Render);
            check(empty.SequenceEqual(removed),"hiding a previously visible CounterBadge clears every old GPU pixel");
            using var icon=new BadgeValidationIconWidget(""){AccessibleLabel="Completed"};
            icon.Options=new(IconPosition:UiBadgeIconPosition.After,Size:UiBadgeSize.ExtraLarge);
            var iconOnly=capture("badge-icon-only",icon.Render);
            check(!empty.SequenceEqual(iconOnly),"source-generated custom icon-only badge produces actual GPU content");
        }
        finally
        {
            setTheme(original);
            renderer.SetMsaaSamples(originalSamples);
            foreach(var widget in widgets)widget.Dispose();
        }
    }
    private sealed class InheritedBadge(string text,UiFluentBadgeOptions options):UiFluentBadge(text,options);
}
