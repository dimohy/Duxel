using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.Core;

internal static class WidgetScaleBench
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        var results = new List<WidgetBenchResult>();
        foreach (var scenario in new[] { "idle", "scroll", "partial", "full" })
        {
            var glyphs = Enumerable.Range(32, 95).ToDictionary(c => c,
                c => new UiGlyphInfo(7, 0, -10, c == 32 ? 0 : 6, 12, new(0, 0, 1, 1)));
            var font = new UiFontAtlas(1, 1, UiTextureFormat.Rgba8Unorm, [255,255,255,255],
                glyphs, new Dictionary<uint,float>(), 12, -3, 1, -1);
            using var context = new UiContext(font, new(1), new(2));
            using var screen = new Buttons(scenario);
            var input = new UiInputState(new(-100,-100), false,false,false,false,false,false,0,0,[],[],default,default);
            var bounds = new UiRect(0,0,1600,1000);
            var commands = 0;
            void Frame()
            {
                context.NewFrame(new(1f/60,new(1600,1000),new(1,1)),input,bounds,UiTextSettings.Default);
                context.Render(screen);
                using var data = context.GetDrawData();
                commands = 0;
                foreach(var list in data.DrawLists) commands += list.Commands.Count;
            }
            var timer = Stopwatch.StartNew();
            do { Frame(); } while(timer.Elapsed.TotalSeconds < .5);
            var recorder = new BenchFrameRecorder(262144);
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            var privateBefore = process.PrivateMemorySize64;
            var workingSetBefore = process.WorkingSet64;
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            timer.Restart();
            var last = timer.ElapsedTicks;
            do
            {
                Frame();
                var now = timer.ElapsedTicks;
                recorder.Record((double)(now-last)/Stopwatch.Frequency);
                last = now;
            } while(timer.Elapsed.TotalSeconds < 1 && recorder.FrameSeconds.Length < 262144);
            var allocated = GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;
            process.Refresh();
            var privateAfter = process.PrivateMemorySize64;
            var workingSetAfter = process.WorkingSet64;
            var stats = recorder.Calculate();
            var raw = output+"."+scenario+".csv";
            using(var writer = new StreamWriter(raw))
            {
                writer.WriteLine("frameSeconds");
                foreach(var seconds in recorder.FrameSeconds) writer.WriteLine(seconds.ToString("R",CultureInfo.InvariantCulture));
            }
            results.Add(new(scenario,10000,(double)allocated/stats.Samples,privateBefore,privateAfter,workingSetBefore,workingSetAfter,commands,raw,stats));
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new WidgetBenchReport("CPU widget generation only; atlas text fixture; no GPU/presentation or application FPS",
            RuntimeInformation.FrameworkDescription,"1600x1000",.5,1,false,results),BenchJsonContext.Default.WidgetBenchReport));
    }

    private sealed class Buttons : UiScreen, IDisposable
    {
        private readonly string _scenario;
        private readonly UiFluentButton[] _buttons = new UiFluentButton[10000];
        private readonly string[] _captions = Enumerable.Range(0,32).Select(i=>"Action "+i.ToString("D2",CultureInfo.InvariantCulture)).ToArray();
        private int _frame;
        internal Buttons(string scenario)
        {
            _scenario = scenario;
            for(var i=0;i<_buttons.Length;i++) _buttons[i]=Fluent.Button("Action 00");
        }
        public override void Render(UiImmediateContext ui)
        {
            var frame = _frame++;
            var caption = _captions[frame%_captions.Length];
            var scroll = _scenario=="scroll" ? frame%100*36 : 0;
            for(var i=0;i<_buttons.Length;i++)
            {
                if(_scenario=="full" || _scenario=="partial" && i%100==0) _buttons[i].Label=caption;
                ui.SetCursorScreenPos(new(i%10*150,i/10*36-scroll));
                _buttons[i].Render(ui);
            }
        }
        public void Dispose(){foreach(var button in _buttons) button.Dispose();}
    }
}
