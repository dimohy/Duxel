using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class DirectTextBench
{
    internal static void Run(string output,string variant)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("This benchmark measures the real Windows text backend.");
        if(variant is not ("eager" or "lazy"))throw new ArgumentException("Expected eager or lazy backend capability.");
        var directory=Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var regular=Path.Combine(directory,"segoeui.ttf");var semibold=Path.Combine(directory,"seguisb.ttf");var bold=Path.Combine(directory,"segoeuib.ttf");
        foreach(var path in new[]{regular,semibold,bold})
            if(!File.Exists(path))throw new FileNotFoundException("Fixed benchmark font is unavailable",path);
        if(File.Exists(output))throw new InvalidOperationException("Preserve previous measurement output.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        ObservedBackend backend=variant=="lazy"?new LayoutBackend():new ObservedBackend();
        using var context=new UiContext(new(1,1,UiTextureFormat.Rgba8Unorm,[255,255,255,255]),new(1),new(2));
        context.SetPlatformTextBackend(backend);context.SetDirectTextEnabled(true);context.SetDirectTextPrimaryFontPath(regular);context.SetDirectTextBaseFontSize(14);
        context.SetTypographyFonts(new(regular,semibold,bold));
        using var screen=new Buttons();
        var input=new UiInputState(new(-100,-100),false,false,false,false,false,false,0,0,[],[],default,default);
        var commands=0;
        void Frame()
        {
            context.NewFrame(new(1f/60,new(1280,720),new(1,1)),input,new(0,0,1280,720),UiTextSettings.Default);
            context.Render(screen);using var data=context.GetDrawData();commands=0;
            foreach(var list in data.DrawLists)commands+=list.Commands.Count;
        }
        var timer=Stopwatch.StartNew();var warmupFrames=0;
        do{Frame();warmupFrames++;if(timer.Elapsed.TotalSeconds>30)throw new TimeoutException("DirectText warmup exceeded30 seconds.");}
        while(timer.Elapsed.TotalSeconds<.5||warmupFrames<3);
        var warmupSeconds=timer.Elapsed.TotalSeconds;
        var recorder=new BenchFrameRecorder(4096);using var process=Process.GetCurrentProcess();process.Refresh();
        var privateBefore=process.PrivateMemorySize64;var workingBefore=process.WorkingSet64;
        var rasters=backend.Rasters;var layouts=backend.Layouts;var metrics=backend.Metrics;
        var allocatedBefore=GC.GetAllocatedBytesForCurrentThread();timer.Restart();var previous=timer.ElapsedTicks;
        do
        {
            Frame();var now=timer.ElapsedTicks;recorder.Record((double)(now-previous)/Stopwatch.Frequency);previous=now;
            if(timer.Elapsed.TotalSeconds>30)throw new TimeoutException("DirectText measurement exceeded30 seconds.");
        }while((timer.Elapsed.TotalSeconds<1||recorder.FrameSeconds.Length<12)&&recorder.FrameSeconds.Length<4096);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;process.Refresh();
        var stats=recorder.Calculate();var raw=output+".csv";
        using(var writer=new StreamWriter(raw)){writer.WriteLine("frameSeconds");foreach(var seconds in recorder.FrameSeconds)writer.WriteLine(seconds.ToString("R",CultureInfo.InvariantCulture));}
        var result=new WidgetBenchResult("idle-unique-directtext",10000,(double)allocated/stats.Samples,privateBefore,process.PrivateMemorySize64,
            workingBefore,process.WorkingSet64,commands,raw,stats);
        var report=new DirectTextBenchReport("CPU construction of10000 unchanged real DirectText buttons, same1280x720 clip/input/quality; no GPU or app FPS; eager adapter preserves raster-first behavior",RuntimeInformation.FrameworkDescription,
            variant,semibold,new(regular,semibold,bold,UiFluentType300.FontSize,UiFluentType300.LineHeight,nameof(UiFluentButtonPresentation)),
            warmupFrames,warmupSeconds,backend.Layouts-layouts,backend.Rasters-rasters,backend.Metrics-metrics,false,result);
        File.WriteAllText(output,JsonSerializer.Serialize(report,BenchJsonContext.Default.DirectTextBenchReport));
        Console.WriteLine($"{variant}: samples={stats.Samples},layout={backend.Layouts-layouts},rasters={backend.Rasters-rasters},allocated/frame={result.allocatedBytesPerFrame:0}");
    }
    private class ObservedBackend:IPlatformTextBackend,IPlatformFontMetricsBackend
    {
        internal long Rasters,Layouts,Metrics;
        public bool TryGetFontMetrics(string fontPath,float fontSize,out PlatformFontMetrics metrics)
        {Metrics++;return WindowsPlatformTextBackend.Instance.TryGetFontMetrics(fontPath,fontSize,out metrics);}
        public bool TryMeasure(in PlatformTextMeasureRequest request,out PlatformTextMeasureResult result)=>WindowsPlatformTextBackend.Instance.TryMeasure(request,out result);
        public bool TryRasterize(in PlatformTextRasterizeRequest request,out PlatformTextRasterizeResult result)
        {Rasters++;return WindowsPlatformTextBackend.Instance.TryRasterize(request,out result);}
    }
    private sealed class LayoutBackend:ObservedBackend,IPlatformTextLayoutBackend
    {
        public ulong TextLayoutVersion => WindowsPlatformTextBackend.Instance.TextLayoutVersion;
        public bool TryLayout(in PlatformTextMeasureRequest request,out PlatformTextLayoutResult result)
        {Layouts++;return WindowsPlatformTextBackend.Instance.TryLayout(request,out result);}
    }
    private sealed class Buttons:UiScreen,IDisposable
    {
        private readonly UiFluentButton[] _buttons=new UiFluentButton[10000];
        internal Buttons(){for(var i=0;i<_buttons.Length;i++){_buttons[i]=Fluent.Button("Button "+i.ToString("D5",CultureInfo.InvariantCulture));_buttons[i].Size=new(110,28);}}
        public override void Render(UiImmediateContext ui)
        {for(var i=0;i<_buttons.Length;i++){ui.SetCursorScreenPos(new(i%10*120,i/10*32));_buttons[i].Render(ui);}}
        public void Dispose(){foreach(var button in _buttons)button.Dispose();}
    }
}
internal sealed record DirectTextBenchFontProfile(string regular,string semibold,string bold,float controlFontSize,float controlLineHeight,string presentation);
internal sealed record DirectTextBenchReport(string scope,string framework,string variant,string fontPath,DirectTextBenchFontProfile fontProfile,int warmupFrames,double warmupSeconds,
    long layoutCalls,long rasterCalls,long fontMetricCalls,bool forcedGc,WidgetBenchResult result);
