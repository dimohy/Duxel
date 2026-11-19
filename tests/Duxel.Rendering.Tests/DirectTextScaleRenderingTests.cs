using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Vulkan;

internal static class DirectTextScaleRenderingTests
{
    internal static void Run(Action<bool,string> check,int frameSlots,string output)
    {
        using var platform=new WindowsPlatformBackend(new(){Width=1280,Height=720,Title="Duxel owned text-scale validation",VSync=false,
            KeyRepeatSettingsProvider=new WindowsKeyRepeatSettingsProvider(),TrayOptions=new()});
        using var renderer=new VulkanRendererBackend(platform,new(2,EnableVSync:false,MsaaSamples:4,MaxFramesInFlight:frameSlots));
        renderer.CreateDeviceObjects();
        var measuredSize=platform.FramebufferSize;
        var reference=new List<byte[]>();
        var hashes=new List<DirectTextScaleImage>();
        foreach(var lazy in new[]{false,true})
        {
            using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
            context.SetPlatformTextBackend(lazy?new LayoutBackend():new RasterBackend());context.SetDirectTextEnabled(true);
            context.SetDirectTextPrimaryFontPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"segoeui.ttf"));
            context.SetDirectTextBaseFontSize(14);
            ControlFontFixture.Configure(context,backend:false);
            using var screen=new Buttons();
            var size=platform.FramebufferSize;
            for(var frame=0;frame<5;frame++)
            {
                screen.Frame=frame;
                context.NewFrame(new(1f/60,new(size.Width,size.Height),new(1,1)),
                    new(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default),new(0,0,size.Width,size.Height),UiTextSettings.Default);
                context.Render(screen);using var data=context.GetDrawData();
                var pixels=renderer.RenderAndReadback(data);
                hashes.Add(new(lazy,frame,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels))));
                if(!lazy)reference.Add(pixels);
                else check(reference[frame].AsSpan().SequenceEqual(pixels),
                    "10000 DirectText buttons preserve every MSAA4 pixel across lazy layout, idle/scroll/partial/full changes, phase "+frame);
            }
        }
        File.WriteAllText(Path.Combine(output,"directtext-scale-pixels.json"),System.Text.Json.JsonSerializer.Serialize(
            new DirectTextScaleImages(measuredSize.Width,measuredSize.Height,platform.ContentScale,10000,4,frameSlots,"idle,retained idle,scroll,partial text change,full text change; half inherited user buttons",hashes.ToArray()),
            DirectTextScaleJson.Default.DirectTextScaleImages));
    }
    private class RasterBackend:IPlatformTextBackend,IPlatformFontMetricsBackend
    {
        public bool TryGetFontMetrics(string fontPath,float fontSize,out PlatformFontMetrics metrics)=>WindowsPlatformTextBackend.Instance.TryGetFontMetrics(fontPath,fontSize,out metrics);
        public bool TryMeasure(in PlatformTextMeasureRequest request,out PlatformTextMeasureResult result)=>WindowsPlatformTextBackend.Instance.TryMeasure(request,out result);
        public bool TryRasterize(in PlatformTextRasterizeRequest request,out PlatformTextRasterizeResult result)=>WindowsPlatformTextBackend.Instance.TryRasterize(request,out result);
    }
    private sealed class LayoutBackend:RasterBackend,IPlatformTextLayoutBackend
    {
        public ulong TextLayoutVersion => WindowsPlatformTextBackend.Instance.TextLayoutVersion;
        public bool TryLayout(in PlatformTextMeasureRequest request,out PlatformTextLayoutResult result)=>WindowsPlatformTextBackend.Instance.TryLayout(request,out result);
    }
    private sealed class Buttons:UiScreen,IDisposable
    {
        private readonly UiFluentButton[] _buttons=new UiFluentButton[10000];
        private readonly string[] _changed=new string[10000];
        internal int Frame;
        internal Buttons()
        {
            for(var i=0;i<_buttons.Length;i++)
            {
                var label="Button "+i.ToString("D5",System.Globalization.CultureInfo.InvariantCulture);
                _buttons[i]=i%2==0?new InheritedButton(label):Fluent.Button(label);
                _buttons[i].Size=new(110,28);_changed[i]="Changed "+i.ToString("D5",System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        public override void Render(UiImmediateContext ui)
        {
            for(var i=0;i<_buttons.Length;i++)
            {
                if(Frame==4||Frame==3&&i==610)_buttons[i].Label=_changed[i];
                ui.SetCursorScreenPos(new(i%10*120,i/10*32-(Frame>=2?60*32:0)));
                _buttons[i].Render(ui);
            }
        }
        public void Dispose(){foreach(var button in _buttons)button.Dispose();}
    }
    private sealed class InheritedButton(string label):UiFluentButton(label);
}
internal sealed record DirectTextScaleImage(bool Lazy,int Phase,string Sha256);
internal sealed record DirectTextScaleImages(int Width,int Height,float PlatformScale,int Buttons,int MsaaSamples,int FrameSlots,string Scope,DirectTextScaleImage[] Images);
[System.Text.Json.Serialization.JsonSerializable(typeof(DirectTextScaleImages))]
[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented=true)]
internal partial class DirectTextScaleJson:System.Text.Json.Serialization.JsonSerializerContext;
