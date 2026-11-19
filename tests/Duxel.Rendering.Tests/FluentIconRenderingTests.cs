using System.Numerics;
using System.Security.Cryptography;
using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Vulkan;

internal static class FluentIconRenderingTests
{
    internal static void ValidateAssets()
    {
        foreach(var pair in new[]{(UiFluentRegularIconFont.Path,"AF1B71AB7BB475AE30B8C87172551503F8DE6CF733548FE7DDBDA15FC08DEA34"),
            (UiFluentFilledIconFont.Path,"CD88FF3FF72C93A575D39B5504A422E67C6C28278642E7946B76A7B6EF35F46F")})
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pair.Item1)))!=pair.Item2)
                throw new InvalidDataException("Bundled Fluent icon font does not match its pinned source: "+pair.Item1);
    }
    private static UiWidget Create(int index)=>index switch
    {
        0=>new FontIconRegular20(""),1=>new FontIconFilled20(""),2=>new FontIconInteractive20(""),
        3=>new FontIconRegular24(""),4=>new FontIconFilled24(""),5=>new FontIconInteractive24(""),
        _=>throw new ArgumentOutOfRangeException(nameof(index))
    };
    internal static void Run(VulkanRendererBackend renderer,Func<string,Action<UiImmediateContext>,byte[]> capture,Action<bool,string> check,
        Func<UiTheme> readTheme,Action<UiTheme> setTheme,int width,int height)
    {
        var original=readTheme();var samples=renderer.ValidationRequestedMsaaSamples;
        var widgets=Enumerable.Range(0,6).Select(Create).ToArray();
        var distinct=new bool[2,2];
        try
        {
            var backend=(IPlatformVectorTextBackend)WindowsPlatformTextBackend.Instance;
            var regularMesh=backend.CreateTextMesh(new(UiFluentRegularIconFont.Path,null,"\uF10A",24));
            var filledMesh=backend.CreateTextMesh(new(UiFluentFilledIconFont.Path,null,"\uF10A",24));
            check(regularMesh.Glyphs.Length==1&&filledMesh.Glyphs.Length==1
                &&!regularMesh.Glyphs.Span[0].Vertices.Span.SequenceEqual(filledMesh.Glyphs.Span[0].Vertices.Span),
                "the two official24px font faces produce different actual vector geometry before sample quantization");
            foreach(var msaa in new[]{1,4})
            foreach(var scheme in Enum.GetValues<UiFluentColorScheme>())
            foreach(var vector in new[]{false,true})
            {
                renderer.SetMsaaSamples(msaa);var theme=scheme==UiFluentColorScheme.Light?UiCompiledDesign.Windows11.Theme:UiCompiledDesign.Windows11Dark.Theme;setTheme(theme);
                for(var size=0;size<2;size++)
                {
                    byte[] Draw(int style,bool hover)
                    {
                        return capture("font-icon-"+size+"-"+style+"-"+hover+"-"+scheme+"-"+msaa+"-"+vector,ui=>
                        {
                            ui.GetBackgroundDrawList().AddRectFilled(new(0,0,width,height),theme.WindowBg,ui.WhiteTextureId,new(0,0,width,height));
                            AppearancePaintProbe.Interaction=new(false,hover,false);
                            if(vector)ui.BeginLayer("official-font-icon",new(false,1,default){Scene3D=new(Matrix4x4.Identity,false)});
                            try{ui.SetCursorScreenPos(new(20,20));widgets[size*3+style].Render(ui);}finally{if(vector)ui.EndLayer();}
                        });
                    }
                    var regular=Draw(0,false);var filled=Draw(1,false);var interactive=Draw(2,false);
                    distinct[size,vector?1:0]|=!regular.SequenceEqual(filled);
                    check(regular.SequenceEqual(interactive),
                        "interactive rest matches the official regular font exactly: "+size+"-"+scheme+"-"+msaa+"-"+vector);
                    var regularHover=Draw(0,true);var filledHover=Draw(1,true);var interactiveHover=Draw(2,true);
                    check(filledHover.SequenceEqual(interactiveHover),
                        "compiled interactive icon selects the official filled font on hover: "+size+"-"+scheme+"-"+msaa+"-"+vector);
                    var edge=size==0?32:40;var center=20+edge/2;var i=(center*width+center)*4;
                    var background=UiFluentButtonPalette.Primary(scheme,UiButtonVisualState.Rest).Background;
                    check(regular[i]!=(byte)background.Rgba||regular[i+1]!=(byte)(background.Rgba>>8)||regular[i+2]!=(byte)(background.Rgba>>16),
                        "the actual official Add glyph paints its center rather than leaving only the empty button frame: "+size+"-"+scheme+"-"+msaa+"-"+vector);
                }
            }
            for(var size=0;size<2;size++)for(var mode=0;mode<2;mode++)
                check(distinct[size,mode],"official regular and filled icons remain visibly distinguishable across the qualified sample conditions: "+size+"-"+mode);
        }
        finally{AppearancePaintProbe.Interaction=default;foreach(var widget in widgets)widget.Dispose();setTheme(original);renderer.SetMsaaSamples(samples);}
    }
}

[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentPrimaryButtonAppearance,UiFluentAddIcon,UiButtonIconBefore>))]
internal partial class FontIconRegular20;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentPrimaryButtonAppearance,UiFluentAddFilledIcon,UiButtonIconBefore>))]
internal partial class FontIconFilled20;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize,UiFluentPrimaryButtonAppearance,UiFluentAddInteractiveIcon,UiButtonIconBefore>))]
internal partial class FontIconInteractive20;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentPrimaryButtonAppearance,UiFluentAddIcon,UiButtonIconBefore>))]
internal partial class FontIconRegular24;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentPrimaryButtonAppearance,UiFluentAddFilledIcon,UiButtonIconBefore>))]
internal partial class FontIconFilled24;
[UiButtonStyle(typeof(AppearancePaintProbe),typeof(UiFluentIconButtonPresentation<UiFluentLargeButtonIconSize,UiFluentPrimaryButtonAppearance,UiFluentAddInteractiveIcon,UiButtonIconBefore>))]
internal partial class FontIconInteractive24;
