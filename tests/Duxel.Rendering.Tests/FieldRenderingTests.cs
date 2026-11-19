using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class FieldRenderingTests
{
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;
        var value=new UiState<string>("Text");
        try
        {
            foreach(var msaa in new[]{1,4})foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())foreach(var vector in new[]{false,true})
            {
                renderer.SetMsaaSamples(msaa);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                for(var style=0;style<6;style++)for(var state=0;state<5;state++)
                {
                    var horizontal=style>=3;var controlSize=new UiVector2(horizontal?134:200,32);
                    var options=new UiFieldOptions{Label="Work email",Hint="Use your work address",Required=true,Disabled=state==4,
                        ValidationMessage=state is 0 or 4?null:"Check this address",ValidationState=state switch{1=>UiFieldValidationState.Error,2=>UiFieldValidationState.Warning,3=>UiFieldValidationState.Success,_=>UiFieldValidationState.None}};
                    using var child=Fluent.Input("Value",value.Binding,64,controlSize.X);
                    using var field=Create(style,child,controlSize,options);
                    byte[] Draw(bool generated)=>capture($"fluent-field-{style}-{state}-{generated}-{scheme}-{msaa}-{vector}",ui=>
                    {
                        ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                        if(vector)ui.BeginLayer("field.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                        try{ui.SetCursorScreenPos(new(12,12));if(generated)field.Render(ui);else Direct(ui,style,child,controlSize,options);}
                        finally{if(vector)ui.EndLayer();}
                    });
                    var generated=Draw(true);var direct=Draw(false);
                    check(generated.AsSpan().SequenceEqual(direct),$"Field generated/direct exact style={style},state={state},{scheme},MSAA{msaa},vector={vector}");
                    check(HasContent(generated),$"Field produces pixels style={style},state={state},{scheme},MSAA{msaa},vector={vector}");
                }
            }
        }
        finally{setTheme(original);renderer.SetMsaaSamples(samples);}
    }
    private static bool HasContent(byte[] rgba)
    {for(var i=4;i<rgba.Length;i+=4)if(rgba[i]!=rgba[0]||rgba[i+1]!=rgba[1]||rgba[i+2]!=rgba[2])return true;return false;}
    private static UiWidget Create(int style,UiWidget child,UiVector2 size,UiFieldOptions options)=>style switch
    {
        0=>new FieldRender0(child,size,options),1=>new FieldRender1(child,size,options),2=>new FieldRender2(child,size,options),
        3=>new FieldRender3(child,size,options),4=>new FieldRender4(child,size,options),5=>new FieldRender5(child,size,options),_=>throw new ArgumentOutOfRangeException(nameof(style))
    };
    private static void Direct(UiImmediateContext ui,int style,UiWidget child,UiVector2 size,UiFieldOptions options)
    {
        switch(style)
        {
            case 0:ui.Field<UiLabelFieldBehavior,UiFluentFieldPresentation<UiFluentSmallFieldSize,UiVerticalFieldLayout>>(child,size,options);break;
            case 1:ui.Field<UiLabelFieldBehavior,UiFluentFieldPresentation<UiFluentMediumFieldSize,UiVerticalFieldLayout>>(child,size,options);break;
            case 2:ui.Field<UiLabelFieldBehavior,UiFluentFieldPresentation<UiFluentLargeFieldSize,UiVerticalFieldLayout>>(child,size,options);break;
            case 3:ui.Field<UiLabelFieldBehavior,UiFluentFieldPresentation<UiFluentSmallFieldSize,UiHorizontalFieldLayout>>(child,size,options);break;
            case 4:ui.Field<UiLabelFieldBehavior,UiFluentFieldPresentation<UiFluentMediumFieldSize,UiHorizontalFieldLayout>>(child,size,options);break;
            case 5:ui.Field<UiLabelFieldBehavior,UiFluentFieldPresentation<UiFluentLargeFieldSize,UiHorizontalFieldLayout>>(child,size,options);break;
            default:throw new ArgumentOutOfRangeException(nameof(style));
        }
    }
}
[UiWidgetStyle(typeof(UiField<,>),typeof(UiLabelFieldBehavior),typeof(UiFluentFieldPresentation<UiFluentSmallFieldSize,UiVerticalFieldLayout>))] internal partial class FieldRender0;
[UiWidgetStyle(typeof(UiField<,>),typeof(UiLabelFieldBehavior),typeof(UiFluentFieldPresentation<UiFluentMediumFieldSize,UiVerticalFieldLayout>))] internal partial class FieldRender1;
[UiWidgetStyle(typeof(UiField<,>),typeof(UiLabelFieldBehavior),typeof(UiFluentFieldPresentation<UiFluentLargeFieldSize,UiVerticalFieldLayout>))] internal partial class FieldRender2;
[UiWidgetStyle(typeof(UiField<,>),typeof(UiLabelFieldBehavior),typeof(UiFluentFieldPresentation<UiFluentSmallFieldSize,UiHorizontalFieldLayout>))] internal partial class FieldRender3;
[UiWidgetStyle(typeof(UiField<,>),typeof(UiLabelFieldBehavior),typeof(UiFluentFieldPresentation<UiFluentMediumFieldSize,UiHorizontalFieldLayout>))] internal partial class FieldRender4;
[UiWidgetStyle(typeof(UiField<,>),typeof(UiLabelFieldBehavior),typeof(UiFluentFieldPresentation<UiFluentLargeFieldSize,UiHorizontalFieldLayout>))] internal partial class FieldRender5;
