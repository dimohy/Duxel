using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class InputChildrenRenderingTests
{
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<UiInputState> setInput,UiState inputState,Action<bool,string> check,Func<UiTheme> getTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=getTheme();var samples=renderer.LastFrameMetrics.MsaaSamples;var value=new UiState<string>("abc");
        try
        {
            Appearance<UiFluentOutlineInput>(new ChildRenderInput0("Input",value.Binding,64,200),0);
            Appearance<UiFluentUnderlineInput>(new ChildRenderInput1("Input",value.Binding,64,200),1);
            Appearance<UiFluentFilledDarkerInput>(new ChildRenderInput2("Input",value.Binding,64,200),2);
            Appearance<UiFluentFilledLighterInput>(new ChildRenderInput3("Input",value.Binding,64,200),3);
        }
        finally{setInput(Idle());setTheme(original);renderer.SetMsaaSamples(samples);ChildReferenceBehavior.Mode=0;}
        void Appearance<TAppearance>(UiInput<UiFluentTextInputBehavior,UiFluentInputPresentation<UiFluentMediumInputSize,TAppearance>> actual,int appearance) where TAppearance:IUiInputAppearance
        {
            using(actual)
            using(var referenceBefore=new ChildButton("?",null){Size=new(24,24)})
            using(var referenceAfter=new ChildButton("x",null){Size=new(24,24)})
            using(var owner=new UiTextInputOwner())
            {
                actual.SetSlots(new("help",new ChildButton("?",null){Size=new(24,24)},new(24,24)),new("clear",new ChildButton("x",null){Size=new(24,24)},new(24,24)));
                foreach(var msaa in new[]{1,4})foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())foreach(var vector in new[]{false,true})
                {
                    renderer.SetMsaaSamples(msaa);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                    for(var mode=0;mode<4;mode++)
                    {
                        var options=new UiInputOptions(Invalid:mode==2);actual.Options=options;ChildReferenceBehavior.Mode=mode;
                        var input=mode is 1 or 2?Idle() with{MousePosition=mode==1?new(36,30):new(190,30),LeftMouseDown=true,LeftMousePressed=true}:Idle();
                        byte[] Draw(bool owned)
                        {
                            inputState.ActiveId=null;inputState.FocusedId=null;inputState.NavCursorVisible=false;setInput(input);
                            return capture($"input-child-{appearance}-{owned}-{mode}-{scheme}-{msaa}-{vector}",ui=>
                            {
                                ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                                if(vector)ui.BeginLayer("child.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                                try
                                {
                                    ui.SetCursorScreenPos(new(20,20));ui.BeginDisabled(mode==3);
                                    try
                                    {
                                        if(owned)actual.Render(ui);
                                        else
                                        {
                                            var text=value.Value;
                                            ui.InputText<ChildReferenceBehavior,ChildReferencePresentation<TAppearance>>("Reference",ref text,64,options,200,owner);
                                            var layout=ChildReferencePresentation<TAppearance>.Last;
                                            void Child(ChildButton button,UiRect bounds)
                                            {ui.SetCursorScreenPos(new(bounds.X,bounds.Y));ui.PushClipRect(bounds,true);try{button.Render(ui);}finally{ui.PopClipRect();}}
                                            Child(referenceBefore,layout.Before!.Value);Child(referenceAfter,layout.After!.Value);
                                        }
                                    }
                                    finally{ui.EndDisabled();}
                                }
                                finally{if(vector)ui.EndLayer();}
                            });
                        }
                        var generated=Draw(true);var reference=Draw(false);
                        check(generated.AsSpan().SequenceEqual(reference),$"owned interactive input matches independently painted root and real buttons: {appearance}/{mode}/{scheme}/{msaa}/{vector}");
                    }
                }
            }
        }
    }
    private static UiInputState Idle()=>new(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
}
internal readonly struct ChildReferenceBehavior:IUiTextInputBehavior
{
    internal static int Mode;
    public static UiInputEditResult Process(UiControlContext context,string id,ref string value,int maxLength,in UiInputLayout layout)
    {var edit=UiFluentTextInputBehavior.Process(context,id,ref value,maxLength,layout);return edit with{FocusWithin=Mode is 1 or 2,Held=Mode is 1 or 2};}
}
internal readonly struct ChildReferencePresentation<TAppearance>:IUiInputPresentation where TAppearance:IUiInputAppearance
{
    internal static UiInputLayout Last;
    public static UiControlTypographyScope BeginText(UiControlContext context)=>UiFluentInputPresentation<UiFluentMediumInputSize,TAppearance>.BeginText(context);
    public static UiInputLayout Measure(UiControlContext context,string label,float width)=>UiFluentInputPresentation<UiFluentMediumInputSize,TAppearance>.Measure(context,label,width);
    public static UiInputLayout MeasureContent(UiControlContext context,in UiInputLayout local,UiInputOptions options)=>
        UiFluentInputPresentation<UiFluentMediumInputSize,TAppearance>.MeasureContent(context,local with{BeforeChild=true,AfterChild=true,Before=new(0,0,24,24),After=new(0,0,24,24)},options);
    public static void Paint(UiControlContext context,string label,in UiInputLayout layout,in UiInputEditResult edit)
    {Last=layout;UiFluentInputPresentation<UiFluentMediumInputSize,TAppearance>.Paint(context,label,layout,edit);}
}

[UiWidgetStyle(typeof(UiInput<,>),typeof(UiFluentTextInputBehavior),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentOutlineInput>))]
internal partial class ChildRenderInput0;

[UiWidgetStyle(typeof(UiInput<,>),typeof(UiFluentTextInputBehavior),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentUnderlineInput>))]
internal partial class ChildRenderInput1;

[UiWidgetStyle(typeof(UiInput<,>),typeof(UiFluentTextInputBehavior),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledDarkerInput>))]
internal partial class ChildRenderInput2;

[UiWidgetStyle(typeof(UiInput<,>),typeof(UiFluentTextInputBehavior),typeof(UiFluentInputPresentation<UiFluentMediumInputSize,UiFluentFilledLighterInput>))]
internal partial class ChildRenderInput3;
