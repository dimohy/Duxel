using System.Globalization;
using System.Text.Json;
using Duxel.Core;
using Duxel.Vulkan;

internal static class ButtonAppearanceTests
{
    private static readonly string[] Names=["Secondary","Primary","Outline","Subtle","Transparent"];
    private static readonly UiButtonPaint[,,] Expected=ReadReference();
    internal static void ValidateReferences()=>_ = Expected.Length;
    internal static UiButtonPaint ExpectedPaint(UiFluentColorScheme scheme,int appearance,int state)=>Expected[(int)scheme,appearance,state];
    private static UiButtonPaint[,,] ReadReference()
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","ButtonAppearances.json")));
        var result=new UiButtonPaint[2,5,4];var schemes=new[]{"Light","Dark"};var states=new[]{"Rest","Hover","Pressed","Disabled"};
        static UiColor Color(JsonElement row,string field)
        {
            var hex=row.GetProperty(field).GetString()!;
            if(hex.Length is not (7 or 9)||hex[0]!='#')throw new InvalidDataException("Invalid reference color.");
            return new(byte.Parse(hex.AsSpan(1,2),NumberStyles.HexNumber),byte.Parse(hex.AsSpan(3,2),NumberStyles.HexNumber),
                byte.Parse(hex.AsSpan(5,2),NumberStyles.HexNumber),hex.Length==9?byte.Parse(hex.AsSpan(7,2),NumberStyles.HexNumber):(byte)255);
        }
        for(var scheme=0;scheme<2;scheme++)
        {
            var appearances=doc.RootElement.GetProperty(schemes[scheme]);
            if(appearances.EnumerateObject().Count()!=5)throw new InvalidDataException("Button appearance reference cardinality changed.");
            for(var appearance=0;appearance<5;appearance++)
            {
                var rows=appearances.GetProperty(Names[appearance]);if(rows.GetArrayLength()!=4)throw new InvalidDataException("Button state cardinality changed.");
                for(var state=0;state<4;state++)
                {
                    var row=rows[state];if(row.GetProperty("state").GetString()!=states[state])throw new InvalidDataException("Button state order changed.");
                    result[scheme,appearance,state]=new(Color(row,"background"),Color(row,"border"),Color(row,"foreground"),Color(row,"focus"));
                }
            }
        }
        return result;
    }
    private static UiButtonPaint Palette(int appearance,UiFluentColorScheme scheme,UiButtonVisualState state)=>appearance switch
    {
        0=>UiFluentButtonPalette.Resolve(scheme,state),1=>UiFluentButtonPalette.Primary(scheme,state),
        2=>UiFluentButtonPalette.Outline(scheme,state),3=>UiFluentButtonPalette.Subtle(scheme,state),
        4=>UiFluentButtonPalette.Transparent(scheme,state),_=>throw new ArgumentOutOfRangeException(nameof(appearance))
    };
    private static UiWidget Create(int index)=>index switch
    {
        0=>new AppearanceSecondarySmall("UI"),
        1=>new AppearanceSecondaryMedium("UI"),
        2=>new AppearanceSecondaryLarge("UI"),
        3=>new AppearancePrimarySmall("UI"),
        4=>new AppearancePrimaryMedium("UI"),
        5=>new AppearancePrimaryLarge("UI"),
        6=>new AppearanceOutlineSmall("UI"),
        7=>new AppearanceOutlineMedium("UI"),
        8=>new AppearanceOutlineLarge("UI"),
        9=>new AppearanceSubtleSmall("UI"),
        10=>new AppearanceSubtleMedium("UI"),
        11=>new AppearanceSubtleLarge("UI"),
        12=>new AppearanceTransparentSmall("UI"),
        13=>new AppearanceTransparentMedium("UI"),
        14=>new AppearanceTransparentLarge("UI"),
        _=>throw new ArgumentOutOfRangeException(nameof(index))
    };
    private static void Immediate(UiImmediateContext ui,int index)
    {
        switch(index)
        {
            case 0:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentSecondaryButtonAppearance>>("UI");break;
            case 1:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentSecondaryButtonAppearance>>("UI");break;
            case 2:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentSecondaryButtonAppearance>>("UI");break;
            case 3:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentPrimaryButtonAppearance>>("UI");break;
            case 4:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance>>("UI");break;
            case 5:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentPrimaryButtonAppearance>>("UI");break;
            case 6:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentOutlineButtonAppearance>>("UI");break;
            case 7:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentOutlineButtonAppearance>>("UI");break;
            case 8:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentOutlineButtonAppearance>>("UI");break;
            case 9:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentSubtleButtonAppearance>>("UI");break;
            case 10:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentSubtleButtonAppearance>>("UI");break;
            case 11:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentSubtleButtonAppearance>>("UI");break;
            case 12:ui.Button<AppearancePaintProbe,UiFluentSmallButtonPresentation<UiFluentTransparentButtonAppearance>>("UI");break;
            case 13:ui.Button<AppearancePaintProbe,UiFluentButtonPresentation<UiFluentTransparentButtonAppearance>>("UI");break;
            case 14:ui.Button<AppearancePaintProbe,UiFluentLargeButtonPresentation<UiFluentTransparentButtonAppearance>>("UI");break;
            default:throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
        =>RunMatrix(renderer,capture,check,readTheme,setTheme,width,height,Create,Immediate,"appearance-");
    internal static void RunMatrix(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height,
        Func<int,UiWidget> create,Action<UiImmediateContext,int> immediate,string prefix)
    {
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;
        var widgets=Enumerable.Range(0,15).Select(create).ToArray();
        void Grid(UiImmediateContext ui,int appearance,bool compiled,UiTheme theme)
        {
            ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
            for(var state=0;state<4;state++)
            for(var size=0;size<3;size++)
            {
                AppearancePaintProbe.Interaction=new(false,state is 1 or 2,state==2);
                ui.SetCursorScreenPos(new(size==0?12:size==1?84:188,12+state*48));
                ui.BeginDisabled(state==3);
                try{if(compiled)widgets[appearance*3+size].Render(ui);else immediate(ui,appearance*3+size);}
                finally{ui.EndDisabled();}
            }
        }
        try
        {
            foreach(var msaa in new[]{1,4})
            foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())
            {
                renderer.SetMsaaSamples(msaa);
                var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                for(var appearance=0;appearance<5;appearance++)
                {
                    var suffix=Names[appearance]+"-"+scheme+"-"+msaa;
                    var direct=capture(prefix+"immediate-"+suffix,ui=>Grid(ui,appearance,false,theme));
                    var generated=capture(prefix+"generated-"+suffix,ui=>Grid(ui,appearance,true,theme));
                    var retained=capture(prefix+"retained-"+suffix,ui=>Grid(ui,appearance,true,theme));
                    check(direct.SequenceEqual(generated)&&generated.SequenceEqual(retained),
                        "all three Button sizes share exact direct/generated/retained appearance pixels: "+suffix);
                    var colors=true;
                    for(var state=0;state<4;state++)
                    {
                        var expected=Expected[(int)scheme,appearance,state];
                        colors&=expected==Palette(appearance,scheme,(UiButtonVisualState)state);
                        for(var size=0;size<3;size++)
                        {
                            var color=(expected.Background.Rgba>>24)==0?theme.WindowBg:expected.Background;
                            var x=(size==0?12:size==1?84:188)+8;var y=12+state*48+8;var offset=(y*width+x)*4;
                            colors&=direct[offset]==(byte)color.Rgba&&direct[offset+1]==(byte)(color.Rgba>>8)
                                &&direct[offset+2]==(byte)(color.Rgba>>16)&&direct[offset+3]==255;
                        }
                    }
                    check(colors,"actual Button appearance fills and all palette fields match resolved official tokens: "+suffix);
                    void Focus(UiImmediateContext ui,bool focused)
                    {
                        ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                        AppearancePaintProbe.Interaction=new(false,false,false){Focused=focused,FocusVisible=focused};
                        ui.SetCursorScreenPos(new(20,20));widgets[appearance*3+1].Render(ui);
                    }
                    var unfocused=capture(prefix+"focus-rest-"+suffix,ui=>Focus(ui,false));
                    var focused=capture(prefix+"focus-visible-"+suffix,ui=>Focus(ui,true));
                    check(!unfocused.SequenceEqual(focused),"each appearance paints its keyboard focus indicator on the actual GPU: "+suffix);
                }
            }
        }
        finally
        {
            AppearancePaintProbe.Interaction=default;foreach(var widget in widgets)widget.Dispose();
            setTheme(original);renderer.SetMsaaSamples(samples);
        }
    }
}

// Test-only paint-state injection; this is not a pointer or keyboard simulation.
internal readonly struct AppearancePaintProbe:IUiButtonBehavior
{
    internal static UiButtonInteraction Interaction;
    public static UiButtonInteraction Process(UiControlContext context,string id,UiRect bounds)=>Interaction;
}
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentSecondaryButtonAppearance>))]
internal partial class AppearanceSecondarySmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentSecondaryButtonAppearance>))]
internal partial class AppearanceSecondaryMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentSecondaryButtonAppearance>))]
internal partial class AppearanceSecondaryLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentPrimaryButtonAppearance>))]
internal partial class AppearancePrimarySmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentPrimaryButtonAppearance>))]
internal partial class AppearancePrimaryMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentPrimaryButtonAppearance>))]
internal partial class AppearancePrimaryLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentOutlineButtonAppearance>))]
internal partial class AppearanceOutlineSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentOutlineButtonAppearance>))]
internal partial class AppearanceOutlineMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentOutlineButtonAppearance>))]
internal partial class AppearanceOutlineLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentSubtleButtonAppearance>))]
internal partial class AppearanceSubtleSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentSubtleButtonAppearance>))]
internal partial class AppearanceSubtleMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentSubtleButtonAppearance>))]
internal partial class AppearanceSubtleLarge;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentSmallButtonPresentation<UiFluentTransparentButtonAppearance>))]
internal partial class AppearanceTransparentSmall;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentButtonPresentation<UiFluentTransparentButtonAppearance>))]
internal partial class AppearanceTransparentMedium;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentLargeButtonPresentation<UiFluentTransparentButtonAppearance>))]
internal partial class AppearanceTransparentLarge;
