using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal readonly struct RadioPaintProbe:IUiRadioGroupBehavior
{
    internal static int Mode;
    public static UiRadioGroupInteraction Process(UiControlContext context,string id,UiRect bounds,ReadOnlySpan<UiRadioOption> items,
        ReadOnlySpan<UiRadioOptionLayout> layouts,string? selected,UiRadioGroupInputState state)
    {
        state.Begin(selected);
        return new(new(false,Mode is 1 or 2,Mode==2){Focused=Mode==3,FocusVisible=Mode==3},Mode is 1 or 2?0:-1,Mode==2?0:-1,selected=="a"?0:2);
    }
}
internal static class RadioGroupRenderingTests
{
    private static readonly UiRadioOption[] Options=[new("a","Alpha option wraps across lines",Description:"More context."),new("b","Beta",false),new("c","Gamma")];
    private static UiWidget Create(int layout,UiBinding<string?> value)=>layout switch
    {
        0=>new RadioPaint0("Choose",value,Options,216),
        1=>new RadioPaint1("Choose",value,Options,216),
        2=>new RadioPaint2("Choose",value,Options,216),
        _=>throw new ArgumentOutOfRangeException(nameof(layout))
    };
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;
        var selected=new UiState<string?>("a");var widgets=Enumerable.Range(0,3).Select(i=>Create(i,selected.Binding)).ToArray();
        using var vertical=new UiRadioGroupContent<UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>(Options);
        using var horizontal=new UiRadioGroupContent<UiFluentRadioGroupPresentation<UiRadioHorizontalLayout>>(Options);
        using var stacked=new UiRadioGroupContent<UiFluentRadioGroupPresentation<UiRadioHorizontalStackedLayout>>(Options);
        try
        {
            foreach(var msaa in new[]{1,4})foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())foreach(var vector in new[]{false,true})
            {
                renderer.SetMsaaSamples(msaa);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                for(var layout=0;layout<3;layout++)for(var mode=0;mode<4;mode++)
                {
                    RadioPaintProbe.Mode=mode;selected.Value=mode==1?"c":"a";
                    byte[] Draw(bool compiled)=>capture($"radio-{layout}-{mode}-{compiled}-{scheme}-{msaa}-{vector}",ui=>
                    {
                        ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                        if(vector)ui.BeginLayer("radio.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                        try
                        {
                            ui.SetCursorScreenPos(new(12,12));
                            if(compiled)widgets[layout].Render(ui);
                            else
                            {
                                string? value=selected.Value;
                                switch(layout)
                                {
                                    case 0:ui.RadioGroup<RadioPaintProbe,UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>("Choose",vertical,ref value,216);break;
                                    case 1:ui.RadioGroup<RadioPaintProbe,UiFluentRadioGroupPresentation<UiRadioHorizontalLayout>>("Choose",horizontal,ref value,216);break;
                                    case 2:ui.RadioGroup<RadioPaintProbe,UiFluentRadioGroupPresentation<UiRadioHorizontalStackedLayout>>("Choose",stacked,ref value,216);break;
                                }
                            }
                        }
                        finally{if(vector)ui.EndLayer();}
                    });
                    var compiled=Draw(true);var immediate=Draw(false);
                    check(compiled.AsSpan().SequenceEqual(immediate),$"RadioGroup generated/direct exact whole image: {layout}/{mode}/{scheme}/{msaa}/{vector}");
                    check(compiled.Where((_,i)=>i%4!=3).Distinct().Count()>3,$"RadioGroup GPU paints actual labelled options and selected indicator: {layout}/{mode}/{scheme}/{msaa}/{vector}");
                }
            }
        }
        finally{foreach(var widget in widgets)widget.Dispose();RadioPaintProbe.Mode=0;setTheme(original);renderer.SetMsaaSamples(samples);}
    }
}

[UiWidgetStyle(typeof(UiRadioGroup<,>),typeof(RadioPaintProbe),typeof(UiFluentRadioGroupPresentation<UiRadioVerticalLayout>))]
internal partial class RadioPaint0;
[UiWidgetStyle(typeof(UiRadioGroup<,>),typeof(RadioPaintProbe),typeof(UiFluentRadioGroupPresentation<UiRadioHorizontalLayout>))]
internal partial class RadioPaint1;
[UiWidgetStyle(typeof(UiRadioGroup<,>),typeof(RadioPaintProbe),typeof(UiFluentRadioGroupPresentation<UiRadioHorizontalStackedLayout>))]
internal partial class RadioPaint2;
