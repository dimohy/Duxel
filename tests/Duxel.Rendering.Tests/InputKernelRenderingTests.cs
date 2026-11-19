using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class InputKernelRenderingTests
{
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<UiInputState> setInput,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        try
        {
            foreach(var msaa in new[]{1,4})foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())foreach(var vector in new[]{false,true})
            {
                renderer.SetMsaaSamples(msaa);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                for(var mode=0;mode<7;mode++)
                {
                    byte[] Draw(bool reference)
                    {
                        var input=mode switch
                        {
                            1=>idle with{MousePosition=new(20,20)},
                            2=>idle with{MousePosition=new(20,20),LeftMouseDown=true,LeftMousePressed=true},
                            3=>idle with{MousePosition=new(20,20),LeftMouseDown=true,LeftMousePressed=true,KeyEvents=[new(UiKey.A,true,KeyModifiers.Ctrl)]},
                            4=>idle with{MousePosition=new(20,20),LeftMouseDown=true,LeftMousePressed=true,KeyEvents=[new(UiKey.End,true,default)]},
                            5=>idle with{MousePosition=new(20,20),LeftMouseDown=true,LeftMousePressed=true,CharEvents=[new('Z')]},
                            _=>idle
                        };
                        setInput(input with{KeyEvents=[new(UiKey.A,false,default),new(UiKey.End,false,default),..input.KeyEvents]});
                        return capture($"input-reference-{reference}-{mode}-{scheme}-{msaa}-{vector}",ui=>
                        {
                            ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                            if(vector)ui.BeginLayer("input.reference.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                            try
                            {
                                ui.PushID(reference?"before":"after");ui.PushID(mode);ui.SetCursorScreenPos(new(12,12));ui.PushItemWidth(140);
                                try
                                {
                                    var value=mode==4?"A long input value that scrolls to the caret":"alpha beta";
                                    var label=mode==6?"Value":"##input";
                                    if(reference)ui.InputTextLegacyReference(label,ref value,256);else ui.InputText(label,ref value,256);
                                }
                                finally{ui.PopItemWidth();ui.PopID();ui.PopID();}
                            }
                            finally{if(vector)ui.EndLayer();}
                        });
                    }
                    var before=Draw(true);var after=Draw(false);
                    check(before.Where((_,i)=>i%4!=3).Distinct().Count()>3,$"legacy input reference contains actual frame and text pixels: mode{mode}/{scheme}/{msaa}/{vector}");
                    check(before.AsSpan().SequenceEqual(after),$"extracted input equals frozen legacy pixels: mode{mode}/{scheme}/{msaa}/{vector}");
                }
            }
            setInput(idle);setTheme(original);
            check(readTheme().Text==original.Text&&readTheme().WindowBg==original.WindowBg,"alternate input fixture uses matching label theme and background");
            foreach(var vector in new[]{false,true})
            {
                using var widget=new CompiledTestInput("Above label",new UiState<string>("Typed value").Binding,128,180);
                using var owner=new UiTextInputOwner();
                byte[] Draw(bool generated)=>capture($"input-custom-{generated}-{vector}",ui=>
                {
                    ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),original.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                    if(vector)ui.BeginLayer("input.custom.vector",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                    try
                    {
                        ui.SetCursorScreenPos(new(12,12));
                        if(generated)widget.Render(ui);else{var value="Typed value";ui.InputText<UiStandardTextInputBehavior,AlternateInputPresentation>("Above label",ref value,128,180,owner);}
                    }
                    finally{if(vector)ui.EndLayer();}
                });
                var generated=Draw(true);var direct=Draw(false);
                check(generated.Where((_,i)=>i%4!=3).Distinct().Count()>3,"alternate input composition has actual frame and text pixels: vector"+vector);
                check(generated.AsSpan().SequenceEqual(direct),"generated inherited input and direct alternate composition share actual pixels: vector"+vector);
            }
        }
        finally{setInput(idle);setTheme(original);renderer.SetMsaaSamples(samples);}
    }
}
