// Ultra-simple FPS benchmark
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Diagnostics;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "FPS Benchmark",
        Width = 1280,
        Height = 720,
        VSync = false
    },
    Screen = new BenchScreen()
});

public sealed class BenchScreen : UiScreen
{
    private int _frameCount = 0;
    private Stopwatch _sw = Stopwatch.StartNew();
    private long _lastReportTime = 0;
    
    public override void Render(UiImmediateContext ui)
    {
        _frameCount++;
        
        var now = _sw.ElapsedMilliseconds;
        if ((now - _lastReportTime) >= 1000 || _lastReportTime == 0)
        {
            Console.WriteLine($"[FPS] Frame={_frameCount} Time={now}ms");
            _lastReportTime = now;
        }
        
        ui.SetNextWindowPos(new UiVector2(10, 10));
        ui.SetNextWindowSize(new UiVector2(400, 150));
        
        if (ui.Begin("Benchmark Info"))
        {
            ui.Text($"Frame: {_frameCount}");
            ui.Text($"Time: {now}ms");
            ui.Text("Rendering...");
        }
        
        ui.End();
        
        // Auto-exit after 600 frames
        if (_frameCount >= 600)
        {
            Environment.Exit(0);
        }
    }
}
