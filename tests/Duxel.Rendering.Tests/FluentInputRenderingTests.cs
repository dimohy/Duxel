using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal readonly struct InputPaintProbe:IUiTextInputBehavior
{
    internal static int Mode;
    public static UiInputEditResult Process(UiControlContext context,string id,ref string value,int maxLength,in UiInputLayout layout)
    {
        if(Mode is 2 or 3 or 7)context.RestartInputCaretBlink();
        return new(id,value,Mode==2?new(0,value.Length):new(value.Length,value.Length),value.Length,0,Mode==7?"xy":null,Mode==7?context.MeasureText("xy").X:0,Mode is 2 or 3 or 7,Mode is 1 or 2,false){Held=Mode==2};
    }
}
internal static class FluentInputRenderingTests
{
    private static UiWidget Create(int style,UiBinding<string> value)=>style switch
    {
        0=>new InputPaint0("Input",value,64,96),
        1=>new InputPaint1("Input",value,64,96),
        2=>new InputPaint2("Input",value,64,96),
        3=>new InputPaint3("Input",value,64,96),
        4=>new InputPaint4("Input",value,64,96),
        5=>new InputPaint5("Input",value,64,96),
        6=>new InputPaint6("Input",value,64,96),
        7=>new InputPaint7("Input",value,64,96),
        8=>new InputPaint8("Input",value,64,96),
        9=>new InputPaint9("Input",value,64,96),
        10=>new InputPaint10("Input",value,64,96),
        11=>new InputPaint11("Input",value,64,96),
        _=>throw new ArgumentOutOfRangeException(nameof(style))
    };
    private static void Direct(UiImmediateContext ui,int style,ref string value,UiInputOptions options,UiTextInputOwner owner)
    {
        switch(style)
        {
            case 0:ui.InputText<InputPaintProbe,UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentOutlineInput>>("Input",ref value,64,options,96,owner);break;
            case 1:ui.InputText<InputPaintProbe,UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentUnderlineInput>>("Input",ref value,64,options,96,owner);break;
            case 2:ui.InputText<InputPaintProbe,UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentFilledDarkerInput>>("Input",ref value,64,options,96,owner);break;
            case 3:ui.InputText<InputPaintProbe,UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentFilledLighterInput>>("Input",ref value,64,options,96,owner);break;
            case 4:ui.InputText<InputPaintProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentOutlineInput>>("Input",ref value,64,options,96,owner);break;
            case 5:ui.InputText<InputPaintProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentUnderlineInput>>("Input",ref value,64,options,96,owner);break;
            case 6:ui.InputText<InputPaintProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledDarkerInput>>("Input",ref value,64,options,96,owner);break;
            case 7:ui.InputText<InputPaintProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledLighterInput>>("Input",ref value,64,options,96,owner);break;
            case 8:ui.InputText<InputPaintProbe,UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentOutlineInput>>("Input",ref value,64,options,96,owner);break;
            case 9:ui.InputText<InputPaintProbe,UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentUnderlineInput>>("Input",ref value,64,options,96,owner);break;
            case 10:ui.InputText<InputPaintProbe,UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentFilledDarkerInput>>("Input",ref value,64,options,96,owner);break;
            case 11:ui.InputText<InputPaintProbe,UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentFilledLighterInput>>("Input",ref value,64,options,96,owner);break;
            default:throw new ArgumentOutOfRangeException(nameof(style));
        }
    }
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;var state=new UiState<string>("Text");
        var widgets=Enumerable.Range(0,12).Select(i=>Create(i,state.Binding)).ToArray();
        var owners=Enumerable.Range(0,8).Select(_=>new UiTextInputOwner()).ToArray();var regions=new UiRect[8];
        try
        {
            foreach(var msaa in new[]{1,4})foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())foreach(var vector in new[]{false,true})
            {
                renderer.SetMsaaSamples(msaa);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                for(var style=0;style<12;style++)
                {
                    byte[] Draw(bool generated)=>capture($"fluent-input-{style}-{generated}-{scheme}-{msaa}-{vector}",ui=>
                    {
                        ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                        if(vector)ui.BeginLayer("input.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                        try
                        {
                            for(var mode=0;mode<8;mode++)
                            {
                                InputPaintProbe.Mode=mode;state.Value=mode==6?"":"Text";var options=new UiInputOptions(Placeholder:"Search",Invalid:mode==5);
                                ui.PushID(mode);ui.SetCursorScreenPos(new(12+(mode%2)*112,12+(mode/2)*56));ui.BeginDisabled(mode==4);
                                try
                                {
                                    if(generated)
                                    {
                                        switch(style)
                                        {
                                            case 0:((InputPaint0)widgets[style]).Options=options;break;
                                            case 1:((InputPaint1)widgets[style]).Options=options;break;
                                            case 2:((InputPaint2)widgets[style]).Options=options;break;
                                            case 3:((InputPaint3)widgets[style]).Options=options;break;
                                            case 4:((InputPaint4)widgets[style]).Options=options;break;
                                            case 5:((InputPaint5)widgets[style]).Options=options;break;
                                            case 6:((InputPaint6)widgets[style]).Options=options;break;
                                            case 7:((InputPaint7)widgets[style]).Options=options;break;
                                            case 8:((InputPaint8)widgets[style]).Options=options;break;
                                            case 9:((InputPaint9)widgets[style]).Options=options;break;
                                            case 10:((InputPaint10)widgets[style]).Options=options;break;
                                            case 11:((InputPaint11)widgets[style]).Options=options;break;
                                        }
                                        widgets[style].Render(ui);
                                    }
                                    else{var value=state.Value;Direct(ui,style,ref value,options,owners[mode]);}
                                    var min=ui.GetItemRectMin();var max=ui.GetItemRectMax();regions[mode]=new(min.X,min.Y,max.X-min.X,max.Y-min.Y);
                                }
                                finally{ui.EndDisabled();ui.PopID();}
                            }
                        }
                        finally{if(vector)ui.EndLayer();}
                    });
                    var generated=Draw(true);var direct=Draw(false);
                    bool Ink(UiRect area)
                    {
                        if(area.X<0||area.Y<0||area.X+area.Width>width||area.Y+area.Height>height||generated.Length!=width*height*4)return false;
                        for(var y=(int)area.Y;y<(int)(area.Y+area.Height);y++)for(var x=(int)area.X;x<(int)(area.X+area.Width);x++)
                        {var i=(y*width+x)*4;var bg=theme.WindowBg.Rgba;if(generated[i]!=(byte)bg||generated[i+1]!=(byte)(bg>>8)||generated[i+2]!=(byte)(bg>>16))return true;}
                        return false;
                    }
                    check(regions.All(Ink),$"Fluent Input all eight states fit and have actual ink: {style}/{scheme}/{msaa}/{vector}");
                    check(generated.AsSpan().SequenceEqual(direct),$"Fluent Input generated/direct exact whole image: {style}/{scheme}/{msaa}/{vector}");
                }
            }
        }
        finally{foreach(var widget in widgets)widget.Dispose();foreach(var owner in owners)owner.Dispose();InputPaintProbe.Mode=0;setTheme(original);renderer.SetMsaaSamples(samples);}
    }
}

[UiWidgetStyle(typeof(UiInput<,>),typeof(InputPaintProbe),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentOutlineInput>))]
internal partial class InputPaint0;

[UiWidgetStyle(typeof(UiInput<,>),typeof(InputPaintProbe),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentUnderlineInput>))]
internal partial class InputPaint1;

[UiWidgetStyle(typeof(UiInput<,>),typeof(InputPaintProbe),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentFilledDarkerInput>))]
internal partial class InputPaint2;

[UiWidgetStyle(typeof(UiInput<,>),typeof(InputPaintProbe),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentFilledLighterInput>))]
internal partial class InputPaint3;

[UiWidgetStyle(typeof(UiInput<,>),typeof(InputPaintProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentOutlineInput>))]
internal partial class InputPaint4;

[UiWidgetStyle(typeof(UiInput<,>),typeof(InputPaintProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentUnderlineInput>))]
internal partial class InputPaint5;

[UiWidgetStyle(typeof(UiInput<,>),typeof(InputPaintProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledDarkerInput>))]
internal partial class InputPaint6;

[UiWidgetStyle(typeof(UiInput<,>),typeof(InputPaintProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledLighterInput>))]
internal partial class InputPaint7;

[UiWidgetStyle(typeof(UiInput<,>),typeof(InputPaintProbe),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentOutlineInput>))]
internal partial class InputPaint8;

[UiWidgetStyle(typeof(UiInput<,>),typeof(InputPaintProbe),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentUnderlineInput>))]
internal partial class InputPaint9;

[UiWidgetStyle(typeof(UiInput<,>),typeof(InputPaintProbe),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentFilledDarkerInput>))]
internal partial class InputPaint10;

[UiWidgetStyle(typeof(UiInput<,>),typeof(InputPaintProbe),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentFilledLighterInput>))]
internal partial class InputPaint11;
