using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class SwitchRenderingTests
{
    private static UiWidget Create(int style,UiBinding<bool> value)=>style switch
    {
        0=>new SwitchPaint0("UI",value),
        1=>new SwitchPaint1("UI",value),
        2=>new SwitchPaint2("UI",value),
        3=>new SwitchPaint3("UI",value),
        4=>new SwitchPaint4("UI",value),
        5=>new SwitchPaint5("UI",value),
        _=>throw new ArgumentOutOfRangeException(nameof(style))
    };
    private static void Immediate(UiImmediateContext ui,int style,ref bool value)
    {
        switch(style)
        {
            case 0:ui.Toggle<bool,UiBooleanToggleValue,AppearancePaintProbe,UiFluentSwitchPresentation<UiFluentMediumSwitchSize,UiSwitchLabelAfter>>("UI",ref value);break;
            case 1:ui.Toggle<bool,UiBooleanToggleValue,AppearancePaintProbe,UiFluentSwitchPresentation<UiFluentMediumSwitchSize,UiSwitchLabelBefore>>("UI",ref value);break;
            case 2:ui.Toggle<bool,UiBooleanToggleValue,AppearancePaintProbe,UiFluentSwitchPresentation<UiFluentMediumSwitchSize,UiSwitchLabelAbove>>("UI",ref value);break;
            case 3:ui.Toggle<bool,UiBooleanToggleValue,AppearancePaintProbe,UiFluentSwitchPresentation<UiFluentSmallSwitchSize,UiSwitchLabelAfter>>("UI",ref value);break;
            case 4:ui.Toggle<bool,UiBooleanToggleValue,AppearancePaintProbe,UiFluentSwitchPresentation<UiFluentSmallSwitchSize,UiSwitchLabelBefore>>("UI",ref value);break;
            case 5:ui.Toggle<bool,UiBooleanToggleValue,AppearancePaintProbe,UiFluentSwitchPresentation<UiFluentSmallSwitchSize,UiSwitchLabelAbove>>("UI",ref value);break;
            default:throw new ArgumentOutOfRangeException(nameof(style));
        }
    }
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;
        var regions=new UiRect[5];
        var states=new[]{new UiState<bool>(false),new UiState<bool>(true)};
        var widgets=Enumerable.Range(0,6).Select(i=>states.Select(v=>Create(i,v.Binding)).ToArray()).ToArray();
        try
        {
            var directionTheme = UiCompiledDesign.Windows11Dark.Theme;
            setTheme(directionTheme);
            foreach (var msaa in new[] { 1, 4 })
            foreach (var value in new[] { false, true })
            {
                renderer.SetMsaaSamples(msaa);
                byte[] DrawDirection(UiFlowDirection direction) => capture($"switch-direction-{direction}-{value}-{msaa}", ui =>
                {
                    using var scope = ui.BeginFluentProvider(null, direction : direction);
                    ui.SetCursorScreenPos(new(12, 12));
                    ui.PushID(value ? 1 : 0);
                    try
                    {
                        var state = value;
                        ui.Toggle<bool, UiBooleanToggleValue, AppearancePaintProbe,
                            UiFluentSwitchPresentation<UiFluentMediumSwitchSize, UiSwitchLabelAfter>>("##direction", ref state);
                    }
                    finally { ui.PopID(); }
                });
                var ltr = DrawDirection(UiFlowDirection.LeftToRight);
                var rtl = DrawDirection(UiFlowDirection.RightToLeft);
                var left = (30 * width + 30) * 4;
                var right = (30 * width + 50) * 4;
                check(!ltr.AsSpan(left, 3).SequenceEqual(ltr.AsSpan(right, 3))
                    && ltr.AsSpan(left, 3).SequenceEqual(rtl.AsSpan(right, 3))
                    && ltr.AsSpan(right, 3).SequenceEqual(rtl.AsSpan(left, 3)),
                    $"Switch moves the visible thumb to the opposite track end in RTL ({value}, MSAA {msaa})");
            }
            foreach(var msaa in new[]{1,4})foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())foreach(var vector in new[]{false,true})
            {
                renderer.SetMsaaSamples(msaa);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                for(var style=0;style<6;style++)for(var value=0;value<2;value++)
                {
                    byte[] Draw(bool compiled)=>capture($"switch-{style}-{value}-{compiled}-{scheme}-{msaa}-{vector}",ui=>
                    {
                        ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                        if(vector)ui.BeginLayer("switch.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                        // Each case has a fixed value and must own its own toggle motion history.
                        ui.PushID($"switch.paint.{style}.{value}");
                        try
                        {
                            for(var state=0;state<5;state++)
                            {
                                AppearancePaintProbe.Interaction=new(false,state==1||state==2,state==2){FocusVisible=state==4,Focused=state==4};
                                ui.PushID(state);ui.SetCursorScreenPos(new(12+(state%2)*98,12+(state/2)*70));ui.BeginDisabled(state==3);
                                try
                                {
                                    if(compiled)widgets[style][value].Render(ui);else{var current=states[value].Value;Immediate(ui,style,ref current);}
                                    var min=ui.GetItemRectMin();var max=ui.GetItemRectMax();regions[state]=new(min.X,min.Y,max.X-min.X,max.Y-min.Y);
                                }
                                finally{ui.EndDisabled();ui.PopID();}
                            }
                        }
                        finally
                        {
                            ui.PopID();
                            if (vector) ui.EndLayer();
                        }
                    });
                    var compiled=Draw(true);var immediate=Draw(false);
                    bool HasVisibleInk(UiRect area)
                    {
                        if(area.X<0||area.Y<0||area.X+area.Width>width||area.Y+area.Height>height||compiled.Length!=width*height*4)return false;
                        for(var y=(int)area.Y;y<(int)(area.Y+area.Height);y++)for(var x=(int)area.X;x<(int)(area.X+area.Width);x++)
                        {
                            var offset=(y*width+x)*4;var background=theme.WindowBg.Rgba;
                            if(compiled[offset]!=(byte)background||compiled[offset+1]!=(byte)(background>>8)||compiled[offset+2]!=(byte)(background>>16))return true;
                        }
                        return false;
                    }
                    check(regions.All(HasVisibleInk),$"Switch five state regions are wholly in the readback and contain actual ink: {style}/{value}/{scheme}/{msaa}/{vector}");
                    check(compiled.AsSpan().SequenceEqual(immediate),$"Switch generated/direct exact image, five states: {style}/{value}/{scheme}/{msaa}/{vector}");
                    check(compiled.Where((_,i)=>i%4!=3).Distinct().Count()>3,$"Switch actual image contains track/thumb/label: {style}/{value}/{scheme}/{msaa}/{vector}");
                }
            }
        }
        finally{foreach(var group in widgets)foreach(var widget in group)widget.Dispose();AppearancePaintProbe.Interaction=default;setTheme(original);renderer.SetMsaaSamples(samples);}
    }
}

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiBooleanToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentSwitchPresentation<UiFluentMediumSwitchSize,UiSwitchLabelAfter>))]
internal partial class SwitchPaint0;

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiBooleanToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentSwitchPresentation<UiFluentMediumSwitchSize,UiSwitchLabelBefore>))]
internal partial class SwitchPaint1;

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiBooleanToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentSwitchPresentation<UiFluentMediumSwitchSize,UiSwitchLabelAbove>))]
internal partial class SwitchPaint2;

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiBooleanToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentSwitchPresentation<UiFluentSmallSwitchSize,UiSwitchLabelAfter>))]
internal partial class SwitchPaint3;

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiBooleanToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentSwitchPresentation<UiFluentSmallSwitchSize,UiSwitchLabelBefore>))]
internal partial class SwitchPaint4;

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiBooleanToggleValue),typeof(AppearancePaintProbe),typeof(UiFluentSwitchPresentation<UiFluentSmallSwitchSize,UiSwitchLabelAbove>))]
internal partial class SwitchPaint5;
