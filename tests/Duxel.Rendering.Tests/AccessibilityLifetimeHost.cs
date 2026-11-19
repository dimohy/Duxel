using Duxel.Core;
using Duxel.Platform.Windows;

// Native HWND and actual CPU widget input paths; this fixture makes no GPU/visual assertion.
internal static class AccessibilityLifetimeHost
{
    internal static void Run(Action<bool,string> check)
    {
        using var platform=new WindowsPlatformBackend(new(){Width=640,Height=480,Title="Duxel owned UIA lifetime fixture",KeyRepeatSettingsProvider=new WindowsKeyRepeatSettingsProvider(),TrayOptions=new()});
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        ControlFontFixture.Configure(context);context.SetAccessibilityTree(platform.AccessibilityTree);
        var show=true;var finish=false;
        using var transient=Fluent.Button("Transient");
        using var remove=Fluent.Button("Remove",()=>show=false);
        using var restore=Fluent.Button("Restore",()=>show=true);
        using var done=Fluent.Button("Finish",()=>finish=true);
        var screen=new HostScreen(ui=>{if(show)transient.Render(ui);remove.Render(ui);restore.Render(ui);done.Render(ui);});
        var idle=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var deadline=Environment.TickCount64+20000;var first=true;
        while(!finish&&Environment.TickCount64<deadline&&!platform.ShouldClose)
        {
            platform.PollEvents();
            context.NewFrame(new(1f/60,new(640,480),new(1,1)),idle,new(0,0,640,480),UiTextSettings.Default);context.Render(screen);using var draw=context.GetDrawData();
            if(first){first=false;Console.WriteLine("UIA_HOST_READY "+platform.WindowHandle);Console.Out.Flush();}
            Thread.Sleep(1);
        }
        platform.PollEvents();var storage=platform.AccessibilityStorage;
        check(finish,"external client completes the owned UIA lifetime fixture");
        check(storage.Retired==0&&storage.Disconnected+storage.AlreadyUnavailable>=1,"removed provider is drained through a queued Windows message");
        check(storage.Cached<=4,"provider cache contains only current live controls after removal and recreation");
        Console.WriteLine($"UIA storage cached={storage.Cached} retired={storage.Retired} disconnected={storage.Disconnected} alreadyUnavailable={storage.AlreadyUnavailable}");
    }
    private sealed class HostScreen(Action<UiImmediateContext> draw):UiScreen
    {public override void Render(UiImmediateContext ui)=>draw(ui);}
}
