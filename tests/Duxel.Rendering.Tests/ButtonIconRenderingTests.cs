using Duxel.Core;
using Duxel.Vulkan;

internal static class ButtonIconRenderingTests
{
    private static UiWidget Create(int index,string text)=>index switch
    {
        0=>new IconPaintBeforeSecondarySmall(text),
        1=>new IconPaintBeforeSecondaryMedium(text),
        2=>new IconPaintBeforeSecondaryLarge(text),
        3=>new IconPaintBeforePrimarySmall(text),
        4=>new IconPaintBeforePrimaryMedium(text),
        5=>new IconPaintBeforePrimaryLarge(text),
        6=>new IconPaintBeforeOutlineSmall(text),
        7=>new IconPaintBeforeOutlineMedium(text),
        8=>new IconPaintBeforeOutlineLarge(text),
        9=>new IconPaintBeforeSubtleSmall(text),
        10=>new IconPaintBeforeSubtleMedium(text),
        11=>new IconPaintBeforeSubtleLarge(text),
        12=>new IconPaintBeforeTransparentSmall(text),
        13=>new IconPaintBeforeTransparentMedium(text),
        14=>new IconPaintBeforeTransparentLarge(text),
        15=>new IconPaintAfterSecondarySmall(text),
        16=>new IconPaintAfterSecondaryMedium(text),
        17=>new IconPaintAfterSecondaryLarge(text),
        18=>new IconPaintAfterPrimarySmall(text),
        19=>new IconPaintAfterPrimaryMedium(text),
        20=>new IconPaintAfterPrimaryLarge(text),
        21=>new IconPaintAfterOutlineSmall(text),
        22=>new IconPaintAfterOutlineMedium(text),
        23=>new IconPaintAfterOutlineLarge(text),
        24=>new IconPaintAfterSubtleSmall(text),
        25=>new IconPaintAfterSubtleMedium(text),
        26=>new IconPaintAfterSubtleLarge(text),
        27=>new IconPaintAfterTransparentSmall(text),
        28=>new IconPaintAfterTransparentMedium(text),
        29=>new IconPaintAfterTransparentLarge(text),
        _=>throw new ArgumentOutOfRangeException(nameof(index))
    };
    private static void Immediate(UiImmediateContext ui,int index,string text)
    {
        switch(index)
        {
            case 0:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentSecondaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 1:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentSecondaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 2:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentSecondaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 3:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 4:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 5:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 6:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentOutlineButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 7:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentOutlineButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 8:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentOutlineButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 9:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentSubtleButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 10:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentSubtleButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 11:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentSubtleButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 12:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentTransparentButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 13:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentTransparentButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 14:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentTransparentButtonAppearance,IconLayoutProbe,UiButtonIconBefore>>(text);break;
            case 15:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentSecondaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 16:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentSecondaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 17:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentSecondaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 18:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 19:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 20:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 21:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentOutlineButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 22:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentOutlineButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 23:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentOutlineButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 24:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentSubtleButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 25:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentSubtleButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 26:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentSubtleButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 27:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentTransparentButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 28:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentTransparentButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            case 29:ui.Button<AppearancePaintProbe,UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentTransparentButtonAppearance,IconLayoutProbe,UiButtonIconAfter>>(text);break;
            default:throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        for(var position=0;position<2;position++)
        for(var mode=0;mode<2;mode++)
        {
            var offset=position*15;var text=mode==0?"UI":"";
            ButtonAppearanceTests.RunMatrix(renderer,capture,check,readTheme,setTheme,width,height,
                index=>Create(offset+index,text),(ui,index)=>Immediate(ui,offset+index,text),
                "icon-"+(position==0?"before-":"after-")+(mode==0?"caption-":"only-"));
        }
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;
        try
        {
            foreach(var msaa in new[]{1,4})
            foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())
            {
                renderer.SetMsaaSamples(msaa);
                var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                for(var appearance=0;appearance<5;appearance++)
                {
                    using var widget=Create(appearance*3+1,"");
                    for(var state=0;state<4;state++)
                    {
                        var image=capture("icon-tint-"+appearance+"-"+state+"-"+scheme+"-"+msaa,ui=>
                        {
                            ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                            AppearancePaintProbe.Interaction=new(false,state is 1 or 2,state==2);
                            ui.SetCursorScreenPos(new(20,20));ui.BeginDisabled(state==3);
                            try{widget.Render(ui);}finally{ui.EndDisabled();}
                        });
                        var expected=ButtonAppearanceTests.ExpectedPaint(scheme,appearance==3?4:appearance,state).Foreground;
                        var rect=IconLayoutProbe.Bounds;var x=(int)(rect.X+rect.Width/2);var y=(int)(rect.Y+rect.Height/2);var i=(y*width+x)*4;
                        check(IconLayoutProbe.Color==expected&&image[i]==(byte)expected.Rgba&&image[i+1]==(byte)(expected.Rgba>>8)&&image[i+2]==(byte)(expected.Rgba>>16),
                            "actual icon pixels match independent foreground tokens, including subtle brand interaction overrides: "+appearance+"-"+state+"-"+scheme+"-"+msaa);
                    }
                }
            }
        }
        finally{AppearancePaintProbe.Interaction=default;setTheme(original);renderer.SetMsaaSamples(samples);}
    }
}
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentSecondaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforeSecondarySmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentSecondaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforeSecondaryMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentSecondaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforeSecondaryLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforePrimarySmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforePrimaryMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforePrimaryLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentOutlineButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforeOutlineSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentOutlineButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforeOutlineMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentOutlineButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforeOutlineLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentSubtleButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforeSubtleSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentSubtleButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforeSubtleMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentSubtleButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforeSubtleLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentTransparentButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforeTransparentSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentTransparentButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforeTransparentMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentTransparentButtonAppearance,IconLayoutProbe,UiButtonIconBefore>))]
internal partial class IconPaintBeforeTransparentLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentSecondaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterSecondarySmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentSecondaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterSecondaryMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentSecondaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterSecondaryLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterPrimarySmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterPrimaryMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentPrimaryButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterPrimaryLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentOutlineButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterOutlineSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentOutlineButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterOutlineMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentOutlineButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterOutlineLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentSubtleButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterSubtleSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentSubtleButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterSubtleMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentSubtleButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterSubtleLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentSmallButtonIconSize,UiFluentTransparentButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterTransparentSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentTransparentButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterTransparentMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentTransparentButtonAppearance,IconLayoutProbe,UiButtonIconAfter>))]
internal partial class IconPaintAfterTransparentLarge;
