using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class LinkRenderingTests
{
    private static readonly Uri Destination=new("/guide",UriKind.Relative);
    private static readonly Action<Uri> Navigate=static _=>{};
    private static UiWidget Create(int style)=>style switch
    {
        0=>new LinkPaint0("Read the guide",Destination,Navigate,82),
        1=>new LinkPaint1("Read the guide",Destination,Navigate,82),
        2=>new LinkPaint2("Read the guide",Destination,Navigate,82),
        3=>new LinkPaint3("Read the guide",Destination,Navigate,82),
        4=>new LinkPaint4("Read the guide",Destination,Navigate,82),
        5=>new LinkPaint5("Read the guide",Destination,Navigate,82),
        6=>new LinkPaint6("Read the guide",Destination,Navigate,82),
        7=>new LinkPaint7("Read the guide",Destination,Navigate,82),
        _=>throw new ArgumentOutOfRangeException(nameof(style))
    };
    private static void Immediate(UiImmediateContext ui,int style,object content)
    {
        switch(style)
        {
            case 0:ui.Link<AppearancePaintProbe,UiFluentLinkPresentation<UiFluentDefaultLinkAppearance,UiStandaloneLinkUnderline>>("Read the guide",Destination,Navigate,(UiLinkContent<UiFluentLinkPresentation<UiFluentDefaultLinkAppearance,UiStandaloneLinkUnderline>>)content,82);break;
            case 1:ui.Link<AppearancePaintProbe,UiFluentLinkPresentation<UiFluentDefaultLinkAppearance,UiInlineLinkUnderline>>("Read the guide",Destination,Navigate,(UiLinkContent<UiFluentLinkPresentation<UiFluentDefaultLinkAppearance,UiInlineLinkUnderline>>)content,82);break;
            case 2:ui.Link<AppearancePaintProbe,UiFluentLinkPresentation<UiFluentSubtleLinkAppearance,UiStandaloneLinkUnderline>>("Read the guide",Destination,Navigate,(UiLinkContent<UiFluentLinkPresentation<UiFluentSubtleLinkAppearance,UiStandaloneLinkUnderline>>)content,82);break;
            case 3:ui.Link<AppearancePaintProbe,UiFluentLinkPresentation<UiFluentSubtleLinkAppearance,UiInlineLinkUnderline>>("Read the guide",Destination,Navigate,(UiLinkContent<UiFluentLinkPresentation<UiFluentSubtleLinkAppearance,UiInlineLinkUnderline>>)content,82);break;
            case 4:ui.Link<AppearancePaintProbe,UiFluentLinkPresentation<UiFluentInvertedLinkAppearance,UiStandaloneLinkUnderline>>("Read the guide",Destination,Navigate,(UiLinkContent<UiFluentLinkPresentation<UiFluentInvertedLinkAppearance,UiStandaloneLinkUnderline>>)content,82);break;
            case 5:ui.Link<AppearancePaintProbe,UiFluentLinkPresentation<UiFluentInvertedLinkAppearance,UiInlineLinkUnderline>>("Read the guide",Destination,Navigate,(UiLinkContent<UiFluentLinkPresentation<UiFluentInvertedLinkAppearance,UiInlineLinkUnderline>>)content,82);break;
            case 6:ui.Link<AppearancePaintProbe,UiFluentLinkPresentation<UiFluentOnBrandLinkAppearance,UiStandaloneLinkUnderline>>("Read the guide",Destination,Navigate,(UiLinkContent<UiFluentLinkPresentation<UiFluentOnBrandLinkAppearance,UiStandaloneLinkUnderline>>)content,82);break;
            case 7:ui.Link<AppearancePaintProbe,UiFluentLinkPresentation<UiFluentOnBrandLinkAppearance,UiInlineLinkUnderline>>("Read the guide",Destination,Navigate,(UiLinkContent<UiFluentLinkPresentation<UiFluentOnBrandLinkAppearance,UiInlineLinkUnderline>>)content,82);break;
            default:throw new ArgumentOutOfRangeException(nameof(style));
        }
    }
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;
        var widgets=Enumerable.Range(0,8).Select(Create).ToArray();var regions=new UiRect[5];
        object[] contents=[
            new UiLinkContent<UiFluentLinkPresentation<UiFluentDefaultLinkAppearance,UiStandaloneLinkUnderline>>(),
            new UiLinkContent<UiFluentLinkPresentation<UiFluentDefaultLinkAppearance,UiInlineLinkUnderline>>(),
            new UiLinkContent<UiFluentLinkPresentation<UiFluentSubtleLinkAppearance,UiStandaloneLinkUnderline>>(),
            new UiLinkContent<UiFluentLinkPresentation<UiFluentSubtleLinkAppearance,UiInlineLinkUnderline>>(),
            new UiLinkContent<UiFluentLinkPresentation<UiFluentInvertedLinkAppearance,UiStandaloneLinkUnderline>>(),
            new UiLinkContent<UiFluentLinkPresentation<UiFluentInvertedLinkAppearance,UiInlineLinkUnderline>>(),
            new UiLinkContent<UiFluentLinkPresentation<UiFluentOnBrandLinkAppearance,UiStandaloneLinkUnderline>>(),
            new UiLinkContent<UiFluentLinkPresentation<UiFluentOnBrandLinkAppearance,UiInlineLinkUnderline>>(),
        ];
        try
        {
            foreach(var msaa in new[]{1,4})foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())foreach(var vector in new[]{false,true})
            {
                renderer.SetMsaaSamples(msaa);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                byte[]? standalone=null;
                for(var style=0;style<8;style++)
                {
                    var background=style>=4?new UiColor(15,70,110):theme.WindowBg;
                    byte[] Draw(bool compiled)=>capture($"link-{style}-{compiled}-{scheme}-{msaa}-{vector}",ui=>
                    {
                        ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),background,ui.WhiteTextureId,new(0,0,width,height));
                        if(vector)ui.BeginLayer("link.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                        try
                        {
                            for(var state=0;state<5;state++)
                            {
                                AppearancePaintProbe.Interaction=new(false,state==1||state==2,state==2){FocusVisible=state==4,Focused=state==4};
                                ui.PushID(state);ui.SetCursorScreenPos(new(12+(state%2)*98,12+(state/2)*70));ui.BeginDisabled(state==3);
                                try
                                {
                                    if(compiled)widgets[style].Render(ui);else Immediate(ui,style,contents[style]);
                                    var min=ui.GetItemRectMin();var max=ui.GetItemRectMax();regions[state]=new(min.X,min.Y,max.X-min.X,max.Y-min.Y);
                                }
                                finally{ui.EndDisabled();ui.PopID();}
                            }
                        }
                        finally{if(vector)ui.EndLayer();}
                    });
                    var compiled=Draw(true);var direct=Draw(false);
                    bool HasInk(UiRect area)
                    {
                        if(area.X<0||area.Y<0||area.X+area.Width>width||area.Y+area.Height>height||compiled.Length!=width*height*4)return false;
                        for(var y=(int)area.Y;y<(int)(area.Y+area.Height);y++)for(var x=(int)area.X;x<(int)(area.X+area.Width);x++)
                        {
                            var offset=(y*width+x)*4;var rgb=background.Rgba;
                            if(compiled[offset]!=(byte)rgb||compiled[offset+1]!=(byte)(rgb>>8)||compiled[offset+2]!=(byte)(rgb>>16))return true;
                        }
                        return false;
                    }
                    check(regions.All(HasInk),$"Link five state regions fit readback and contain actual text: {style}/{scheme}/{msaa}/{vector}");
                    check(compiled.AsSpan().SequenceEqual(direct),$"Link generated/direct exact whole image: {style}/{scheme}/{msaa}/{vector}");
                    if(style%2==0)standalone=compiled;
                    else
                    {
                        bool EqualRegion(UiRect area)
                        {
                            for(var y=(int)area.Y;y<(int)(area.Y+area.Height);y++)for(var x=(int)area.X;x<(int)(area.X+area.Width);x++)
                            {var offset=(y*width+x)*4;if(!compiled.AsSpan(offset,4).SequenceEqual(standalone!.AsSpan(offset,4)))return false;}
                            return true;
                        }
                        check(!EqualRegion(regions[0])&&EqualRegion(regions[1])&&EqualRegion(regions[3])&&EqualRegion(regions[4]),
                            $"Link inline rest underline changes pixels while hover/disabled/double-focus remain exact: {style}/{scheme}/{msaa}/{vector}");
                    }
                }
            }
        }
        finally{foreach(var widget in widgets)widget.Dispose();foreach(IDisposable content in contents)content.Dispose();AppearancePaintProbe.Interaction=default;setTheme(original);renderer.SetMsaaSamples(samples);}
    }
}

[UiWidgetStyle(typeof(UiLink<,>),typeof(AppearancePaintProbe),typeof(UiFluentLinkPresentation<UiFluentDefaultLinkAppearance,UiStandaloneLinkUnderline>))]
internal partial class LinkPaint0;

[UiWidgetStyle(typeof(UiLink<,>),typeof(AppearancePaintProbe),typeof(UiFluentLinkPresentation<UiFluentDefaultLinkAppearance,UiInlineLinkUnderline>))]
internal partial class LinkPaint1;

[UiWidgetStyle(typeof(UiLink<,>),typeof(AppearancePaintProbe),typeof(UiFluentLinkPresentation<UiFluentSubtleLinkAppearance,UiStandaloneLinkUnderline>))]
internal partial class LinkPaint2;

[UiWidgetStyle(typeof(UiLink<,>),typeof(AppearancePaintProbe),typeof(UiFluentLinkPresentation<UiFluentSubtleLinkAppearance,UiInlineLinkUnderline>))]
internal partial class LinkPaint3;

[UiWidgetStyle(typeof(UiLink<,>),typeof(AppearancePaintProbe),typeof(UiFluentLinkPresentation<UiFluentInvertedLinkAppearance,UiStandaloneLinkUnderline>))]
internal partial class LinkPaint4;

[UiWidgetStyle(typeof(UiLink<,>),typeof(AppearancePaintProbe),typeof(UiFluentLinkPresentation<UiFluentInvertedLinkAppearance,UiInlineLinkUnderline>))]
internal partial class LinkPaint5;

[UiWidgetStyle(typeof(UiLink<,>),typeof(AppearancePaintProbe),typeof(UiFluentLinkPresentation<UiFluentOnBrandLinkAppearance,UiStandaloneLinkUnderline>))]
internal partial class LinkPaint6;

[UiWidgetStyle(typeof(UiLink<,>),typeof(AppearancePaintProbe),typeof(UiFluentLinkPresentation<UiFluentOnBrandLinkAppearance,UiInlineLinkUnderline>))]
internal partial class LinkPaint7;
