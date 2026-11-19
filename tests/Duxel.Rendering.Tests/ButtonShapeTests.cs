using Duxel.Core;
using Duxel.Vulkan;

internal static class ButtonShapeTests
{
    private static UiWidget Create(int index)=>index switch
    {
        0=>new ShapeSquareSecondarySmall("UI"),
        1=>new ShapeSquareSecondaryMedium("UI"),
        2=>new ShapeSquareSecondaryLarge("UI"),
        3=>new ShapeSquarePrimarySmall("UI"),
        4=>new ShapeSquarePrimaryMedium("UI"),
        5=>new ShapeSquarePrimaryLarge("UI"),
        6=>new ShapeSquareOutlineSmall("UI"),
        7=>new ShapeSquareOutlineMedium("UI"),
        8=>new ShapeSquareOutlineLarge("UI"),
        9=>new ShapeSquareSubtleSmall("UI"),
        10=>new ShapeSquareSubtleMedium("UI"),
        11=>new ShapeSquareSubtleLarge("UI"),
        12=>new ShapeSquareTransparentSmall("UI"),
        13=>new ShapeSquareTransparentMedium("UI"),
        14=>new ShapeSquareTransparentLarge("UI"),
        15=>new ShapeCircularSecondarySmall("UI"),
        16=>new ShapeCircularSecondaryMedium("UI"),
        17=>new ShapeCircularSecondaryLarge("UI"),
        18=>new ShapeCircularPrimarySmall("UI"),
        19=>new ShapeCircularPrimaryMedium("UI"),
        20=>new ShapeCircularPrimaryLarge("UI"),
        21=>new ShapeCircularOutlineSmall("UI"),
        22=>new ShapeCircularOutlineMedium("UI"),
        23=>new ShapeCircularOutlineLarge("UI"),
        24=>new ShapeCircularSubtleSmall("UI"),
        25=>new ShapeCircularSubtleMedium("UI"),
        26=>new ShapeCircularSubtleLarge("UI"),
        27=>new ShapeCircularTransparentSmall("UI"),
        28=>new ShapeCircularTransparentMedium("UI"),
        29=>new ShapeCircularTransparentLarge("UI"),
        _=>throw new ArgumentOutOfRangeException(nameof(index))
    };
    private static void Immediate(UiImmediateContext ui,int index)
    {
        switch(index)
        {
            case 0:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 1:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 2:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 3:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 4:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 5:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 6:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentOutlineButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 7:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentOutlineButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 8:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentOutlineButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 9:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentSubtleButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 10:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentSubtleButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 11:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentSubtleButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 12:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentTransparentButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 13:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentTransparentButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 14:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentTransparentButtonAppearance<UiFluentSquareButtonShape>>>("UI");break;
            case 15:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 16:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 17:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 18:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 19:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 20:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 21:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentOutlineButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 22:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentOutlineButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 23:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentOutlineButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 24:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentSubtleButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 25:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentSubtleButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 26:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentSubtleButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 27:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentTransparentButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 28:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentTransparentButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            case 29:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentTransparentButtonAppearance<UiFluentCircularButtonShape>>>("UI");break;
            default:throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        for(var shape=0;shape<2;shape++)
        {
            var offset=shape*15;
            ButtonAppearanceTests.RunMatrix(renderer,capture,check,readTheme,setTheme,width,height,
                index=>Create(offset+index),(ui,index)=>Immediate(ui,offset+index),"shape-"+(shape==0?"square-":"circular-"));
        }
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;
        try
        {
            foreach(var msaa in new[]{1,4})
            foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())
            {
                renderer.SetMsaaSamples(msaa);
                var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                for(var shape=0;shape<2;shape++)
                {
                    using var widget=Create(shape*15+4);
                    var image=capture("shape-corner-"+shape+"-"+scheme+"-"+msaa,ui=>
                    {
                        ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                        AppearancePaintProbe.Interaction=default;ui.SetCursorScreenPos(new(20,20));widget.Render(ui);
                    });
                    var expected=shape==0?UiFluentButtonPalette.Primary(scheme,UiButtonVisualState.Rest).Background:theme.WindowBg;
                    var i=(22*width+22)*4;
                    check(image[i]==(byte)expected.Rgba&&image[i+1]==(byte)(expected.Rgba>>8)&&image[i+2]==(byte)(expected.Rgba>>16),
                        "square corners fill while circular capsule corners preserve the underlying surface: "+shape+"-"+scheme+"-"+msaa);
                }
            }
            using var custom=new ShapeCustomButton("");
            var customImage=capture("shape-custom-algorithm",ui=>
            {
                var theme=readTheme();
                ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                ui.SetCursorScreenPos(new(20,20));custom.Render(ui);
            });
            var bg=readTheme().WindowBg;var middle=(30*width+68)*4;var left=(30*width+23)*4;
            check(customImage[middle]==(byte)bg.Rgba&&customImage[middle+1]==(byte)(bg.Rgba>>8)
                &&!customImage.AsSpan(middle,4).SequenceEqual(customImage.AsSpan(left,4)),
                "a user shape replaces the frame algorithm with two disjoint surfaces through the same generated widget");
            using var huge=new ShapeCircularPrimaryMedium(""){Size=new(30000,30000)};
            var hugeImage=capture("shape-circular-token-limit",ui=>
            {
                var theme=readTheme();
                ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                ui.SetCursorScreenPos(new(-26500,-26500));huge.Render(ui);
            });
            var hugeFill=UiFluentButtonPalette.Primary(readTheme().FluentColorScheme,UiButtonVisualState.Rest).Background;
            check(hugeImage[middle]==(byte)hugeFill.Rgba&&hugeImage[middle+1]==(byte)(hugeFill.Rgba>>8)
                &&hugeImage[middle+2]==(byte)(hugeFill.Rgba>>16),
                "circular Button keeps the official10000px radius limit for oversized clipped bounds instead of becoming a15000px circle");
        }
        finally{AppearancePaintProbe.Interaction=default;setTheme(original);renderer.SetMsaaSamples(samples);}
    }
}

internal readonly struct SplitSurfaceShape:IUiButtonShape
{
    public static void Paint(UiControlContext context,UiRect bounds,UiColor fill,UiColor border,float inset)
    {
        context.Frame(new(bounds.X,bounds.Y,8,bounds.Height),fill,border,0,1);
        context.Frame(new(bounds.X+bounds.Width-8,bounds.Y,8,bounds.Height),fill,border,0,1);
    }
}
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance<SplitSurfaceShape>>))]
internal partial class ShapeCustomButton;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquareSecondarySmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquareSecondaryMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquareSecondaryLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquarePrimarySmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquarePrimaryMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquarePrimaryLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentOutlineButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquareOutlineSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentOutlineButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquareOutlineMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentOutlineButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquareOutlineLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentSubtleButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquareSubtleSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentSubtleButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquareSubtleMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentSubtleButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquareSubtleLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentTransparentButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquareTransparentSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentTransparentButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquareTransparentMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentTransparentButtonAppearance<UiFluentSquareButtonShape>>))]
internal partial class ShapeSquareTransparentLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularSecondarySmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularSecondaryMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentSecondaryButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularSecondaryLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularPrimarySmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularPrimaryMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularPrimaryLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentOutlineButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularOutlineSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentOutlineButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularOutlineMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentOutlineButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularOutlineLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentSubtleButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularSubtleSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentSubtleButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularSubtleMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentSubtleButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularSubtleLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentTransparentButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularTransparentSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentTransparentButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularTransparentMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentTransparentButtonAppearance<UiFluentCircularButtonShape>>))]
internal partial class ShapeCircularTransparentLarge;
