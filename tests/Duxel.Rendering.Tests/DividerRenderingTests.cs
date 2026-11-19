using Duxel.Core;
using Duxel.Vulkan;

internal static class DividerRenderingTests
{
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;
        var widgets=new UiFluentDivider[12];
        void Draw(UiImmediateContext ui,bool compiled,bool vertical,UiTheme theme)
        {
            ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
            for(var i=0;i<12;i++)
            {
                ui.SetCursorScreenPos(vertical?new(12+i*24,10):new(12,10+i*17));
                if(compiled)widgets[i].Render(ui);else ui.Divider("A",widgets[i].Options);
            }
            for(var i=0;i<4;i++)
            {ui.SetCursorScreenPos(new(12,220+i*4));ui.Divider(options:new(Appearance:(UiDividerAppearance)i,Length:160));}
        }
        try
        {
            foreach(var msaa in new[]{1,4})
            foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())
            foreach(var vertical in new[]{false,true})
            {
                renderer.SetMsaaSamples(msaa);
                var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;
                setTheme(theme);
                for(var i=0;i<12;i++)
                {
                    widgets[i]?.Dispose();var options=new UiDividerOptions(vertical,(UiDividerAlignment)(i%3),(UiDividerAppearance)(i/3),Length:180);
                    widgets[i]=i%2==0?new UiFluentDivider("A",options):new Derived("A",options);
                }
                var suffix=$"{scheme}-{vertical}-{msaa}";
                var immediate=capture("divider-immediate-"+suffix,ui=>Draw(ui,false,vertical,theme));
                var compiled=capture("divider-generated-"+suffix,ui=>Draw(ui,true,vertical,theme));
                var retained=capture("divider-retained-"+suffix,ui=>Draw(ui,true,vertical,theme));
                check(immediate.SequenceEqual(compiled)&&compiled.SequenceEqual(retained),
                    "all12 Divider alignments/appearances preserve exact GPU pixels across immediate/generated/inherited/retained paths: "+suffix);
                var colors=true;
                for(var i=0;i<4;i++)
                {
                    var offset=((220+i*4)*width+20)*4;var color=UiFluentDividerPalette.Resolve(scheme,(UiDividerAppearance)i).Line.Rgba;
                    colors&=immediate[offset]==(byte)color&&immediate[offset+1]==(byte)(color>>8)&&immediate[offset+2]==(byte)(color>>16)&&immediate[offset+3]==255;
                }
                check(colors,"actual Divider GPU stroke pixels match all four official color tokens: "+suffix);
            }
            using var changing=Fluent.Divider("Before",new(Length:200));
            var before=capture("divider-before",changing.Render);changing.Source="After content";
            var after=capture("divider-after",changing.Render);changing.Options=new(Vertical:true,Appearance:UiDividerAppearance.Brand,Length:180);
            var direction=capture("divider-direction",changing.Render);
            check(!before.SequenceEqual(after)&&!after.SequenceEqual(direction),"changing Divider content and orientation updates actual GPU output");
            var replay=false;
            void Layer(UiImmediateContext ui)
            {
                var options=new UiLayerOptions(true,1,default);replay=ui.TryReplayLayer("divider-static",options);
                if(!replay){ui.BeginLayer("divider-static",options);ui.Divider(options:new(Length:180));ui.EndLayer();}
            }
            capture("divider-static-first",Layer);capture("divider-static-replay",Layer);
            check(replay,"unchanged Divider supports the existing explicit static-layer replay contract");
            var changed=readTheme();changed.FluentColorScheme=changed.FluentColorScheme==UiFluentColorScheme.Light?UiFluentColorScheme.Dark:UiFluentColorScheme.Light;setTheme(changed);
            capture("divider-static-palette-change",Layer);
            check(!replay,"changing the common Fluent scheme invalidates static Divider layers with unchanged legacy colors");
        }
        finally{foreach(var widget in widgets)widget?.Dispose();setTheme(original);renderer.SetMsaaSamples(samples);}
    }
    private sealed class Derived(string text,UiDividerOptions options):UiFluentDivider(text,options);
}
