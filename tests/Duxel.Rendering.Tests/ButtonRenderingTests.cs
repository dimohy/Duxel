using System.Globalization;
using System.Text.Json;
using Duxel.Core;
using Duxel.Vulkan;

internal static class ButtonRenderingTests
{
    private static readonly UiButtonPaint[,] Expected=ReadReferences();
    internal static void ValidateReferences()=>_ = Expected.Length;
    private static UiButtonPaint[,] ReadReferences()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","ButtonPalette.json")));
        var values=new UiButtonPaint[2,4];var schemes=new[]{"Light","Dark"};var states=new[]{"Rest","Hover","Pressed","Disabled"};
        static UiColor Color(JsonElement row,string name)
        {var hex=row.GetProperty(name).GetString()!;return new(byte.Parse(hex.AsSpan(1,2),NumberStyles.HexNumber),byte.Parse(hex.AsSpan(3,2),NumberStyles.HexNumber),byte.Parse(hex.AsSpan(5,2),NumberStyles.HexNumber));}
        for(var scheme=0;scheme<2;scheme++)
        {
            var rows=doc.RootElement.GetProperty(schemes[scheme]);if(rows.GetArrayLength()!=4)throw new InvalidDataException("Button palette reference cardinality changed.");
            for(var state=0;state<4;state++)
            {var row=rows[state];if(row.GetProperty("state").GetString()!=states[state])throw new InvalidDataException("Button palette reference order changed.");values[scheme,state]=new(Color(row,"background"),Color(row,"border"),Color(row,"foreground"),Color(row,"focus"));}
        }
        return values;
    }
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;
        var widgets=new UiWidget[]
        {
            new ButtonPaintRestSmall("UI"),new ButtonPaintRestMedium("UI"),new ButtonPaintRestLarge("UI"),
            new ButtonPaintHoverSmall("UI"),new ButtonPaintHoverMedium("UI"),new ButtonPaintHoverLarge("UI"),
            new ButtonPaintHeldSmall("UI"),new ButtonPaintHeldMedium("UI"),new ButtonPaintHeldLarge("UI"),
            new ButtonPaintRestSmall("UI"),new ButtonPaintRestMedium("UI"),new ButtonPaintRestLarge("UI")
        };
        void Immediate(UiImmediateContext ui,int index)
        {
            switch(index)
            {
                case 0:case 9:ui.Button<ButtonRestProbe,UiFluentSmallButtonPresentation>("UI");break;
                case 1:case 10:ui.Button<ButtonRestProbe,UiFluentButtonPresentation>("UI");break;
                case 2:case 11:ui.Button<ButtonRestProbe,UiFluentLargeButtonPresentation>("UI");break;
                case 3:ui.Button<ButtonHoverProbe,UiFluentSmallButtonPresentation>("UI");break;
                case 4:ui.Button<ButtonHoverProbe,UiFluentButtonPresentation>("UI");break;
                case 5:ui.Button<ButtonHoverProbe,UiFluentLargeButtonPresentation>("UI");break;
                case 6:ui.Button<ButtonHeldProbe,UiFluentSmallButtonPresentation>("UI");break;
                case 7:ui.Button<ButtonHeldProbe,UiFluentButtonPresentation>("UI");break;
                case 8:ui.Button<ButtonHeldProbe,UiFluentLargeButtonPresentation>("UI");break;
            }
        }
        void Grid(UiImmediateContext ui,bool compiled,UiTheme theme)
        {
            ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
            for(var i=0;i<12;i++)
            {
                var x=i%3==0?12:i%3==1?84:188;ui.SetCursorScreenPos(new(x,12+i/3*48));
                ui.BeginDisabled(i>=9);if(compiled)widgets[i].Render(ui);else Immediate(ui,i);ui.EndDisabled();
            }
        }
        try
        {
            foreach(var msaa in new[]{1,4})
            foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())
            {
                renderer.SetMsaaSamples(msaa);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                var suffix=$"{scheme}-{msaa}";
                var direct=capture("button-immediate-"+suffix,ui=>Grid(ui,false,theme));
                var generated=capture("button-generated-"+suffix,ui=>Grid(ui,true,theme));
                var retained=capture("button-retained-"+suffix,ui=>Grid(ui,true,theme));
                check(direct.SequenceEqual(generated)&&generated.SequenceEqual(retained),
                    "small/medium/large Button paint states preserve exact immediate/generated/retained GPU pixels: "+suffix);
                var colors=true;
                for(var i=0;i<12;i++)
                {
                    var state=i/3;var x=(i%3==0?12:i%3==1?84:188)+8;var y=12+state*48+8;var offset=(y*width+x)*4;
                    var paint=Expected[(int)scheme,state];var actual=UiFluentButtonPalette.Resolve(scheme,(UiButtonVisualState)state);
                    colors&=paint==actual&&direct[offset]==(byte)paint.Background.Rgba&&direct[offset+1]==(byte)(paint.Background.Rgba>>8)
                        &&direct[offset+2]==(byte)(paint.Background.Rgba>>16)&&direct[offset+3]==255;
                }
                check(colors,"Button actual GPU fills and palette fields match independent pinned neutral tokens: "+suffix);
            }
            using var focus=new ButtonPaintFocus("UI");
            var focused=capture("button-focus",focus.Render);var normal=capture("button-rest",widgets[1].Render);
            check(!focused.SequenceEqual(normal),"compiled keyboard-focus paint produces a distinct GPU frame");
        }
        finally{foreach(var widget in widgets)widget.Dispose();setTheme(original);renderer.SetMsaaSamples(samples);}
    }
}
internal readonly struct ButtonRestProbe:IUiButtonBehavior{public static UiButtonInteraction Process(UiControlContext context,string id,UiRect bounds)=>default;}
internal readonly struct ButtonHoverProbe:IUiButtonBehavior{public static UiButtonInteraction Process(UiControlContext context,string id,UiRect bounds)=>new(false,true,false);}
internal readonly struct ButtonHeldProbe:IUiButtonBehavior{public static UiButtonInteraction Process(UiControlContext context,string id,UiRect bounds)=>new(false,true,true);}
internal readonly struct ButtonFocusProbe:IUiButtonBehavior{public static UiButtonInteraction Process(UiControlContext context,string id,UiRect bounds)=>new(false,false,false){Focused=true,FocusVisible=true};}
[UiButtonStyle(typeof(ButtonRestProbe),typeof(UiFluentSmallButtonPresentation))]internal partial class ButtonPaintRestSmall;
[UiButtonStyle(typeof(ButtonRestProbe),typeof(UiFluentButtonPresentation))]internal partial class ButtonPaintRestMedium;
[UiButtonStyle(typeof(ButtonRestProbe),typeof(UiFluentLargeButtonPresentation))]internal partial class ButtonPaintRestLarge;
[UiButtonStyle(typeof(ButtonHoverProbe),typeof(UiFluentSmallButtonPresentation))]internal partial class ButtonPaintHoverSmall;
[UiButtonStyle(typeof(ButtonHoverProbe),typeof(UiFluentButtonPresentation))]internal partial class ButtonPaintHoverMedium;
[UiButtonStyle(typeof(ButtonHoverProbe),typeof(UiFluentLargeButtonPresentation))]internal partial class ButtonPaintHoverLarge;
[UiButtonStyle(typeof(ButtonHeldProbe),typeof(UiFluentSmallButtonPresentation))]internal partial class ButtonPaintHeldSmall;
[UiButtonStyle(typeof(ButtonHeldProbe),typeof(UiFluentButtonPresentation))]internal partial class ButtonPaintHeldMedium;
[UiButtonStyle(typeof(ButtonHeldProbe),typeof(UiFluentLargeButtonPresentation))]internal partial class ButtonPaintHeldLarge;
[UiButtonStyle(typeof(ButtonFocusProbe),typeof(UiFluentButtonPresentation))]internal partial class ButtonPaintFocus;
