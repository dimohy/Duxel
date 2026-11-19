using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class WidgetMeasurementRenderingTests
{
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;var value=new UiState<string>("Text");
        try
        {
            foreach(var msaa in new[]{1,4})foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())foreach(var vector in new[]{false,true})
            {
                renderer.SetMsaaSamples(msaa);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                for(var kind=0;kind<9;kind++)foreach(var outerWidth in new[]{96f,220f})foreach(var horizontal in new[]{false,true})
                {
                    var controlWidth=horizontal?outerWidth*.67f:outerWidth;
                    using var probe=new GeometryProbe(Create(kind,value));
                    var options=new UiFieldOptions{Label="Measured label",Hint="Helper text",ValidationMessage="Review this value",ValidationState=UiFieldValidationState.Warning,Required=true};
                    using UiWidget field=horizontal?new MeasuredHorizontalField(probe){Width=outerWidth,Options=options}:new UiFluentField(probe){Width=outerWidth,Options=options};
                    var prefix=$"widget-measure-{kind}-{outerWidth}-{horizontal}-{scheme}-{msaa}-{vector}";
                    byte[] Draw(int mode,float controlHeight)=>capture(prefix+"-"+mode,ui=>
                    {
                        ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                        if(vector)ui.BeginLayer("measure.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                        try
                        {
                            ui.SetCursorScreenPos(new(12,12));
                            if(mode==1)field.Render(ui);
                            else if(horizontal)ui.Field<UiLabelFieldBehavior,UiFluentFieldPresentation<UiFluentMediumFieldSize,UiHorizontalFieldLayout>>(probe,new UiVector2(controlWidth,controlHeight),mode==3?options with{Label=null}:options);
                            else ui.Field<UiLabelFieldBehavior,UiFluentFieldPresentation<UiFluentMediumFieldSize,UiVerticalFieldLayout>>(probe,new UiVector2(controlWidth,controlHeight),options);
                        }
                        finally{if(vector)ui.EndLayer();}
                    });
                    // The oracle renders the control in an explicit roomy allocation. Its actual
                    // item bounds, not Measure, determine the independent reference height.
                    Draw(0,240);var actual=probe.LastSize;
                    var reference=Draw(2,actual.Y);var automatic=Draw(1,0);
                    check(reference.AsSpan().SequenceEqual(automatic),$"Natural Field matches actual rendered control height kind={kind},width={outerWidth},horizontal={horizontal},{scheme},MSAA{msaa},vector={vector}");
                    check(probe.LastSize==actual,$"Natural allocation preserves actual child geometry kind={kind},width={outerWidth},horizontal={horizontal},{scheme},MSAA{msaa},vector={vector}");
                    if(kind==1&&horizontal&&outerWidth==96)
                    {
                        var noLabel=Draw(3,actual.Y);var fromX=(int)MathF.Ceiling(12+controlWidth*33/67);var equal=true;
                        for(var y=0;y<height&&equal;y++)equal=automatic.AsSpan((y*width+fromX)*4,(width-fromX)*4).SequenceEqual(noLabel.AsSpan((y*width+fromX)*4,(width-fromX)*4));
                        check(equal,$"a narrow label never paints over its neighboring control and descriptions:{scheme},MSAA{msaa},vector={vector}");
                    }
                }
            }
        }
        finally{setTheme(original);renderer.SetMsaaSamples(samples);}
    }
    private static UiWidget Create(int kind,UiState<string> value)=>kind switch
    {
        0=>new MeasuredSmallInput("Small",value.Binding,64),1=>Fluent.Input("Medium",value.Binding,64),2=>new MeasuredLargeInput("Large",value.Binding,64),
        3=>Fluent.Checkbox("Option",new UiState<bool>(true).Binding),4=>Fluent.Switch("Option",new UiState<bool>(true).Binding),
        5=>Fluent.Slider("Range",new UiState<float>(30).Binding),
        6=>Fluent.RadioGroup("Group",new UiState<string?>("a").Binding,new UiRadioOption[]{new("a","First option"),new("b","Second option that wraps")}),
        7=>new MeasuredBody{Height=64},8=>TallInput(value),_=>throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static UiWidget TallInput(UiState<string> value)
    {var input=Fluent.Input("Tall",value.Binding,64);input.AfterSlot=new("tall",new MeasuredBody{Height=48},new(24,48));return input;}
    private sealed class GeometryProbe:UiControlWidget
    {
        private readonly UiWidget _content;internal UiVector2 LastSize;
        internal GeometryProbe(UiWidget content){OwnChild("content",content);_content=content;}
        protected override UiVector2 MeasureContent(UiControlContext context)=>context.Measure(_content,context.AvailableSize);
        protected override void RenderControl(UiImmediateContext ui){_content.Render(ui);LastSize=ui.GetItemRectSize();}
    }
}
