using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal readonly struct SlotPaintProbe:IUiTextInputBehavior
{
    internal static UiInputLayout Last;
    public static UiInputEditResult Process(UiControlContext context,string id,ref string value,int maxLength,in UiInputLayout layout)
    {Last=layout;return InputPaintProbe.Process(context,id,ref value,maxLength,layout);}
}
internal static class InputAdornmentRenderingTests
{
    private static UiWidget Create(int style,UiBinding<string> value)=>style switch
    {
        0=>new SlotRender0("Input",value,128,112),
        1=>new SlotRender1("Input",value,128,112),
        2=>new SlotRender2("Input",value,128,112),
        3=>new SlotRender3("Input",value,128,112),
        4=>new SlotRender4("Input",value,128,112),
        5=>new SlotRender5("Input",value,128,112),
        6=>new SlotRender6("Input",value,128,112),
        7=>new SlotRender7("Input",value,128,112),
        8=>new SlotRender8("Input",value,128,112),
        9=>new SlotRender9("Input",value,128,112),
        10=>new SlotRender10("Input",value,128,112),
        11=>new SlotRender11("Input",value,128,112),
        12=>new SlotRender12("Input",value,128,112),
        13=>new SlotRender13("Input",value,128,112),
        14=>new SlotRender14("Input",value,128,112),
        15=>new SlotRender15("Input",value,128,112),
        16=>new SlotRender16("Input",value,128,112),
        17=>new SlotRender17("Input",value,128,112),
        18=>new SlotRender18("Input",value,128,112),
        19=>new SlotRender19("Input",value,128,112),
        20=>new SlotRender20("Input",value,128,112),
        21=>new SlotRender21("Input",value,128,112),
        22=>new SlotRender22("Input",value,128,112),
        23=>new SlotRender23("Input",value,128,112),
        _=>throw new ArgumentOutOfRangeException(nameof(style))
    };
    private static void Configure(UiWidget widget,int style,UiInputOptions options,float width)
    {
        switch(style)
        {
            case 0:((SlotRender0)widget).Options=options;((SlotRender0)widget).Width=width;break;
            case 1:((SlotRender1)widget).Options=options;((SlotRender1)widget).Width=width;break;
            case 2:((SlotRender2)widget).Options=options;((SlotRender2)widget).Width=width;break;
            case 3:((SlotRender3)widget).Options=options;((SlotRender3)widget).Width=width;break;
            case 4:((SlotRender4)widget).Options=options;((SlotRender4)widget).Width=width;break;
            case 5:((SlotRender5)widget).Options=options;((SlotRender5)widget).Width=width;break;
            case 6:((SlotRender6)widget).Options=options;((SlotRender6)widget).Width=width;break;
            case 7:((SlotRender7)widget).Options=options;((SlotRender7)widget).Width=width;break;
            case 8:((SlotRender8)widget).Options=options;((SlotRender8)widget).Width=width;break;
            case 9:((SlotRender9)widget).Options=options;((SlotRender9)widget).Width=width;break;
            case 10:((SlotRender10)widget).Options=options;((SlotRender10)widget).Width=width;break;
            case 11:((SlotRender11)widget).Options=options;((SlotRender11)widget).Width=width;break;
            case 12:((SlotRender12)widget).Options=options;((SlotRender12)widget).Width=width;break;
            case 13:((SlotRender13)widget).Options=options;((SlotRender13)widget).Width=width;break;
            case 14:((SlotRender14)widget).Options=options;((SlotRender14)widget).Width=width;break;
            case 15:((SlotRender15)widget).Options=options;((SlotRender15)widget).Width=width;break;
            case 16:((SlotRender16)widget).Options=options;((SlotRender16)widget).Width=width;break;
            case 17:((SlotRender17)widget).Options=options;((SlotRender17)widget).Width=width;break;
            case 18:((SlotRender18)widget).Options=options;((SlotRender18)widget).Width=width;break;
            case 19:((SlotRender19)widget).Options=options;((SlotRender19)widget).Width=width;break;
            case 20:((SlotRender20)widget).Options=options;((SlotRender20)widget).Width=width;break;
            case 21:((SlotRender21)widget).Options=options;((SlotRender21)widget).Width=width;break;
            case 22:((SlotRender22)widget).Options=options;((SlotRender22)widget).Width=width;break;
            case 23:((SlotRender23)widget).Options=options;((SlotRender23)widget).Width=width;break;
        }
    }
    private static void Direct(UiImmediateContext ui,int style,ref string value,UiInputOptions options,float width,UiTextInputOwner owner)
    {
        switch(style)
        {
            case 0:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentOutlineInput,UiInputTextAdornment,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 1:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentOutlineInput,UiInputIconAdornment<SlotSearch16>,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 2:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentOutlineInput,UiInputTextAdornment,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 3:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentOutlineInput,UiInputIconAdornment<SlotSearch20>,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 4:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentOutlineInput,UiInputTextAdornment,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 5:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentOutlineInput,UiInputIconAdornment<SlotSearch24>,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 6:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentUnderlineInput,UiInputTextAdornment,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 7:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentUnderlineInput,UiInputIconAdornment<SlotSearch16>,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 8:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentUnderlineInput,UiInputTextAdornment,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 9:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentUnderlineInput,UiInputIconAdornment<SlotSearch20>,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 10:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentUnderlineInput,UiInputTextAdornment,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 11:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentUnderlineInput,UiInputIconAdornment<SlotSearch24>,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 12:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentFilledDarkerInput,UiInputTextAdornment,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 13:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentFilledDarkerInput,UiInputIconAdornment<SlotSearch16>,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 14:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledDarkerInput,UiInputTextAdornment,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 15:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledDarkerInput,UiInputIconAdornment<SlotSearch20>,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 16:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentFilledDarkerInput,UiInputTextAdornment,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 17:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentFilledDarkerInput,UiInputIconAdornment<SlotSearch24>,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 18:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentFilledLighterInput,UiInputTextAdornment,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 19:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentFilledLighterInput,UiInputIconAdornment<SlotSearch16>,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 20:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledLighterInput,UiInputTextAdornment,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 21:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledLighterInput,UiInputIconAdornment<SlotSearch20>,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 22:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentFilledLighterInput,UiInputTextAdornment,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
            case 23:ui.InputText<SlotPaintProbe,UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentFilledLighterInput,UiInputIconAdornment<SlotSearch24>,UiInputTextAdornment>>("Input",ref value,128,options,width,owner);break;
        }
    }
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,Func<UiTheme> getTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=getTheme();var samples=renderer.LastFrameMetrics.MsaaSamples;var state=new UiState<string>("");
        var widgets=Enumerable.Range(0,24).Select(i=>Create(i,state.Binding)).ToArray();
        var owners=Enumerable.Range(0,4).Select(_=>new UiTextInputOwner()).ToArray();var layouts=new UiInputLayout[4];
        try
        {
            foreach(var msaa in new[]{1,4})foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())foreach(var vector in new[]{false,true})
            {
                renderer.SetMsaaSamples(msaa);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                for(var style=0;style<24;style++)
                {
                    byte[] Draw(bool generated)=>capture($"input-slot-{style}-{generated}-{scheme}-{msaa}-{vector}",ui=>
                    {
                        ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                        if(vector)ui.BeginLayer("slot.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                        try
                        {
                            for(var mode=0;mode<4;mode++)
                            {
                                InputPaintProbe.Mode=mode==1?2:mode==3?3:0;state.Value=mode==1?"abcdefghijklmnopqrstuvwxyz":"123";
                                var options=new UiInputOptions(){ContentBefore="$",ContentAfter="kg"};var fieldWidth=mode==3?40:112;
                                ui.PushID(mode);ui.SetCursorScreenPos(new(12+(mode%2)*120,12+(mode/2)*64));ui.BeginDisabled(mode==2);
                                try
                                {
                                    if(generated){Configure(widgets[style],style,options,fieldWidth);widgets[style].Render(ui);}
                                    else{var value=state.Value;Direct(ui,style,ref value,options,fieldWidth,owners[mode]);}
                                    layouts[mode]=SlotPaintProbe.Last;
                                }
                                finally{ui.EndDisabled();ui.PopID();}
                            }
                        }
                        finally{if(vector)ui.EndLayer();}
                    });
                    var generated=Draw(true);var direct=Draw(false);
                    bool Same(UiRect a,UiRect b)
                    {
                        if(a.Width!=b.Width||a.Height!=b.Height||a.X<0||a.Y<0||b.X<0||b.Y<0||a.X+a.Width>width||b.X+b.Width>width)return false;
                        for(var y=0;y<(int)a.Height;y++)for(var x=0;x<(int)a.Width;x++)
                        {var ia=((int)(a.Y+y)*width+(int)a.X+x)*4;var ib=((int)(b.Y+y)*width+(int)b.X+x)*4;for(var c=0;c<4;c++)if(generated[ia+c]!=generated[ib+c])return false;}
                        return true;
                    }
                    bool Ink(UiRect area)
                    {
                        var fill=UiFluentInputPalette.Resolve(scheme,UiButtonVisualState.Rest,(UiInputAppearance)(style/6)).Background;var bg=(fill.Rgba>>24)==0?theme.WindowBg.Rgba:fill.Rgba;
                        for(var y=(int)area.Y;y<(int)(area.Y+area.Height);y++)for(var x=(int)area.X;x<(int)(area.X+area.Width);x++)
                        {var i=(y*width+x)*4;if(generated[i]!=(byte)bg||generated[i+1]!=(byte)(bg>>8)||generated[i+2]!=(byte)(bg>>16))return true;}
                        return false;
                    }
                    check(Ink(layouts[0].Before!.Value)&&Ink(layouts[0].After!.Value),$"both input adornments have actual ink: {style}/{scheme}/{msaa}/{vector}");
                    check(layouts[3].After!.Value.X>=layouts[3].Before!.Value.X+layouts[3].Before!.Value.Width,
                        $"adornments preserve order without overlap in a narrow root: {style}/{scheme}/{msaa}/{vector}");
                    check(generated.AsSpan().SequenceEqual(direct),$"Input slots generated/direct exact image: {style}/{scheme}/{msaa}/{vector}");
                    check(Same(layouts[0].Before!.Value,layouts[1].Before!.Value)&&Same(layouts[0].After!.Value,layouts[1].After!.Value),
                        $"long selected body leaves both adornment pixels unchanged: {style}/{scheme}/{msaa}/{vector}");
                    check(layouts.All(l=>l.Field.X>=0&&l.Field.Y>=0&&l.Field.X+l.Field.Width<=width&&l.Field.Y+l.Field.Height<=height),
                        $"all four slotted input states fit the complete capture: {style}/{scheme}/{msaa}/{vector}");
                }
            }
        }
        finally{foreach(var widget in widgets)widget.Dispose();foreach(var owner in owners)owner.Dispose();InputPaintProbe.Mode=0;setTheme(original);renderer.SetMsaaSamples(samples);}
    }
}

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentOutlineInput,UiInputTextAdornment,UiInputTextAdornment>))]
internal partial class SlotRender0;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentOutlineInput,UiInputIconAdornment<SlotSearch16>,UiInputTextAdornment>))]
internal partial class SlotRender1;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentOutlineInput,UiInputTextAdornment,UiInputTextAdornment>))]
internal partial class SlotRender2;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentOutlineInput,UiInputIconAdornment<SlotSearch20>,UiInputTextAdornment>))]
internal partial class SlotRender3;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentOutlineInput,UiInputTextAdornment,UiInputTextAdornment>))]
internal partial class SlotRender4;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentOutlineInput,UiInputIconAdornment<SlotSearch24>,UiInputTextAdornment>))]
internal partial class SlotRender5;

[UiFluentIcon("search",UiFluentIconStyle.Regular,16)]
internal readonly partial struct SlotSearch16;

[UiFluentIcon("search",UiFluentIconStyle.Regular,20)]
internal readonly partial struct SlotSearch20;

[UiFluentIcon("search",UiFluentIconStyle.Regular,24)]
internal readonly partial struct SlotSearch24;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentUnderlineInput,UiInputTextAdornment,UiInputTextAdornment>))]
internal partial class SlotRender6;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentUnderlineInput,UiInputIconAdornment<SlotSearch16>,UiInputTextAdornment>))]
internal partial class SlotRender7;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentUnderlineInput,UiInputTextAdornment,UiInputTextAdornment>))]
internal partial class SlotRender8;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentUnderlineInput,UiInputIconAdornment<SlotSearch20>,UiInputTextAdornment>))]
internal partial class SlotRender9;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentUnderlineInput,UiInputTextAdornment,UiInputTextAdornment>))]
internal partial class SlotRender10;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentUnderlineInput,UiInputIconAdornment<SlotSearch24>,UiInputTextAdornment>))]
internal partial class SlotRender11;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentFilledDarkerInput,UiInputTextAdornment,UiInputTextAdornment>))]
internal partial class SlotRender12;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentFilledDarkerInput,UiInputIconAdornment<SlotSearch16>,UiInputTextAdornment>))]
internal partial class SlotRender13;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledDarkerInput,UiInputTextAdornment,UiInputTextAdornment>))]
internal partial class SlotRender14;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledDarkerInput,UiInputIconAdornment<SlotSearch20>,UiInputTextAdornment>))]
internal partial class SlotRender15;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentFilledDarkerInput,UiInputTextAdornment,UiInputTextAdornment>))]
internal partial class SlotRender16;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentFilledDarkerInput,UiInputIconAdornment<SlotSearch24>,UiInputTextAdornment>))]
internal partial class SlotRender17;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentFilledLighterInput,UiInputTextAdornment,UiInputTextAdornment>))]
internal partial class SlotRender18;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentSmallInputSize,UiFluentFilledLighterInput,UiInputIconAdornment<SlotSearch16>,UiInputTextAdornment>))]
internal partial class SlotRender19;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledLighterInput,UiInputTextAdornment,UiInputTextAdornment>))]
internal partial class SlotRender20;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledLighterInput,UiInputIconAdornment<SlotSearch20>,UiInputTextAdornment>))]
internal partial class SlotRender21;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentFilledLighterInput,UiInputTextAdornment,UiInputTextAdornment>))]
internal partial class SlotRender22;

[UiWidgetStyle(typeof(UiInput<,>),typeof(SlotPaintProbe),typeof(UiFluentInputPresentation<UiFluentLargeInputSize,UiFluentFilledLighterInput,UiInputIconAdornment<SlotSearch24>,UiInputTextAdornment>))]
internal partial class SlotRender23;
