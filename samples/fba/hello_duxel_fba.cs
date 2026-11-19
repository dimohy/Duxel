// FBA: 초간단 Hello Duxel 샘플 — Hello는 작게, Duxel!은 크게 출력
#:property TargetFramework=net10.0
#:property OutputType=WinExe
#:property OptimizationPreference=Size
#:property InvariantGlobalization=true
#:property DebuggerSupport=false
#:property EventSourceSupport=false
#:property MetricsSupport=false
#:property MetadataUpdaterSupport=false
#:property StackTraceSupport=false
#:property UseSystemResourceKeys=true
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Hello Duxel",
        Width = 960,
        Height = 540,
        VSync = true
    },
    Screen = new HelloDuxelScreen()
});

public sealed class HelloDuxelScreen : UiScreen
{
    public override void Render(UiImmediateContext ui)
    {
        ui.PushFontSize(18f);
        ui.Text("Hello");
        ui.PopFontSize();

        ui.SameLine(8f, UiItemVerticalAlign.Center);

        ui.PushFontSize(56f);
        ui.Text("Duxel!");
        ui.PopFontSize();

    }
}
