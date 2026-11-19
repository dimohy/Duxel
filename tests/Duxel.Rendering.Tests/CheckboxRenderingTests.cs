using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class CheckboxRenderingTests
{
    private static UiWidget Create(int style,UiBinding<bool?> value)=>style switch
    {
        0=>new CheckboxPaint0("UI",value),
        1=>new CheckboxPaint1("UI",value),
        2=>new CheckboxPaint2("UI",value),
        3=>new CheckboxPaint3("UI",value),
        4=>new CheckboxPaint4("UI",value),
        5=>new CheckboxPaint5("UI",value),
        6=>new CheckboxPaint6("UI",value),
        7=>new CheckboxPaint7("UI",value),
        _=>throw new ArgumentOutOfRangeException(nameof(style))
    };
    private static void Immediate(UiImmediateContext ui,int style,ref bool? value)
    {
        switch(style)
        {
            case 0:ui.Toggle<bool?,UiNullableToggleValue,AppearancePaintProbe,UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize,UiFluentSquareCheckboxShape,UiCheckboxLabelAfter>>("UI",ref value);break;
            case 1:ui.Toggle<bool?,UiNullableToggleValue,AppearancePaintProbe,UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize,UiFluentSquareCheckboxShape,UiCheckboxLabelBefore>>("UI",ref value);break;
            case 2:ui.Toggle<bool?,UiNullableToggleValue,AppearancePaintProbe,UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize,UiFluentCircularCheckboxShape,UiCheckboxLabelAfter>>("UI",ref value);break;
            case 3:ui.Toggle<bool?,UiNullableToggleValue,AppearancePaintProbe,UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize,UiFluentCircularCheckboxShape,UiCheckboxLabelBefore>>("UI",ref value);break;
            case 4:ui.Toggle<bool?,UiNullableToggleValue,AppearancePaintProbe,UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize,UiFluentSquareCheckboxShape,UiCheckboxLabelAfter>>("UI",ref value);break;
            case 5:ui.Toggle<bool?,UiNullableToggleValue,AppearancePaintProbe,UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize,UiFluentSquareCheckboxShape,UiCheckboxLabelBefore>>("UI",ref value);break;
            case 6:ui.Toggle<bool?,UiNullableToggleValue,AppearancePaintProbe,UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize,UiFluentCircularCheckboxShape,UiCheckboxLabelAfter>>("UI",ref value);break;
            case 7:ui.Toggle<bool?,UiNullableToggleValue,AppearancePaintProbe,UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize,UiFluentCircularCheckboxShape,UiCheckboxLabelBefore>>("UI",ref value);break;
            default:throw new ArgumentOutOfRangeException(nameof(style));
        }
    }
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;
        var states=new[]{new UiState<bool?>(false),new UiState<bool?>(true),new UiState<bool?>(null)};
        var widgets=Enumerable.Range(0,8).Select(i=>states.Select(v=>Create(i,v.Binding)).ToArray()).ToArray();
        try
        {
            foreach(var msaa in new[]{1,4})
            foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())
            foreach(var vector in new[]{false,true})
            {
                renderer.SetMsaaSamples(msaa);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                for(var style=0;style<8;style++)
                {
                    byte[] Draw(bool compiled)=>capture($"checkbox-{style}-{compiled}-{scheme}-{msaa}-{vector}",ui=>
                    {
                        ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                        if(vector)ui.BeginLayer("checkbox.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                        try
                        {
                            for(var state=0;state<5;state++)for(var value=0;value<3;value++)
                            {
                                AppearancePaintProbe.Interaction=new(false,state==1,state==2){FocusVisible=state==4,Focused=state==4};
                                ui.PushID(state*3+value);ui.SetCursorScreenPos(new(12+value*72,12+state*44));ui.BeginDisabled(state==3);
                                try
                                {
                                    if(compiled)widgets[style][value].Render(ui);
                                    else{var current=states[value].Value;Immediate(ui,style,ref current);}
                                }
                                finally{ui.EndDisabled();ui.PopID();}
                            }
                        }
                        finally{if(vector)ui.EndLayer();}
                    });
                    var compiled=Draw(true);var immediate=Draw(false);
                    check(compiled.AsSpan().SequenceEqual(immediate),$"Checkbox generated/direct exact whole image, three values/five states: {style}/{scheme}/{msaa}/{vector}");
                    check(compiled.Where((_,i)=>i%4!=3).Distinct().Count()>3,$"Checkbox actual GPU image contains controls and glyphs: {style}/{scheme}/{msaa}/{vector}");
                }
                if(msaa==1&&scheme==UiFluentColorScheme.Dark&&!vector)
                {
                    void Mark<TSize,TShape>() where TSize:IUiCheckboxSize where TShape:IUiCheckboxShape
                    {
                        foreach(var mixed in new[]{false,true})
                        {
                            var tag=typeof(TSize).Name+"-"+typeof(TShape).Name+"-"+mixed;
                            byte[] Draw(bool omit)=>capture("checkbox-mark-"+tag+"-"+omit,ui=>
                            {
                                ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                                ui.SetCursorScreenPos(new(20,20));AppearancePaintProbe.Interaction=default;bool? selected=mixed?null:true;
                                if(omit)ui.Toggle<bool?,UiNullableToggleValue,AppearancePaintProbe,UiFluentCheckboxPresentation<EmptyMark<TSize>,TShape,UiCheckboxLabelAfter>>("##glyph",ref selected);
                                else ui.Toggle<bool?,UiNullableToggleValue,AppearancePaintProbe,UiFluentCheckboxPresentation<TSize,TShape,UiCheckboxLabelAfter>>("##glyph",ref selected);
                            });
                            check(!Draw(false).AsSpan().SequenceEqual(Draw(true)),"actual Checkbox mark survives the no-label/no-glyph negative control: "+tag);
                        }
                    }
                    Mark<UiFluentMediumCheckboxSize,UiFluentSquareCheckboxShape>();
                    Mark<UiFluentMediumCheckboxSize,UiFluentCircularCheckboxShape>();
                    Mark<UiFluentLargeCheckboxSize,UiFluentSquareCheckboxShape>();
                    Mark<UiFluentLargeCheckboxSize,UiFluentCircularCheckboxShape>();
                }
            }
        }
        finally{foreach(var group in widgets)foreach(var widget in group)widget.Dispose();AppearancePaintProbe.Interaction=default;setTheme(original);renderer.SetMsaaSamples(samples);}
    }
    private readonly struct EmptyMark<TSize>:IUiCheckboxSize where TSize:IUiCheckboxSize
    {
        public static float IndicatorSize=>TSize.IndicatorSize;
        public static float GlyphSize=>TSize.GlyphSize;
        public static void PaintMark(UiControlContext context,UiRect bounds,UiColor color,UiButtonInteraction interaction,bool mixed,bool circular){}
    }
}

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool?),typeof(UiNullableToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize,UiFluentSquareCheckboxShape,UiCheckboxLabelAfter>))]
internal partial class CheckboxPaint0;
[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool?),typeof(UiNullableToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize,UiFluentSquareCheckboxShape,UiCheckboxLabelBefore>))]
internal partial class CheckboxPaint1;
[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool?),typeof(UiNullableToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize,UiFluentCircularCheckboxShape,UiCheckboxLabelAfter>))]
internal partial class CheckboxPaint2;
[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool?),typeof(UiNullableToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize,UiFluentCircularCheckboxShape,UiCheckboxLabelBefore>))]
internal partial class CheckboxPaint3;
[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool?),typeof(UiNullableToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize,UiFluentSquareCheckboxShape,UiCheckboxLabelAfter>))]
internal partial class CheckboxPaint4;
[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool?),typeof(UiNullableToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize,UiFluentSquareCheckboxShape,UiCheckboxLabelBefore>))]
internal partial class CheckboxPaint5;
[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool?),typeof(UiNullableToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize,UiFluentCircularCheckboxShape,UiCheckboxLabelAfter>))]
internal partial class CheckboxPaint6;
[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool?),typeof(UiNullableToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize,UiFluentCircularCheckboxShape,UiCheckboxLabelBefore>))]
internal partial class CheckboxPaint7;
