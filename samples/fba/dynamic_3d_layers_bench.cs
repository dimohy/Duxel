// FBA: dynamic geometry on intersecting 3D planes; no static cache or offscreen layers.
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new() { Title = "Duxel dynamic 3D layers", Width = 1280, Height = 800, VSync = false },
    Renderer = new() { MsaaSamples = 1 },
    Screen = new Dynamic3DLayersScreen(),
});

public sealed class Dynamic3DLayersScreen : UiScreen
{
    private readonly record struct Phase(int Layers, int Shapes, float Overlap, bool Scene3D);
    private readonly Phase[] _phases;
    private readonly string[] _ids = Enumerable.Range(0, 8).Select(i => "dynamic-plane-" + i).ToArray();
    private readonly string? _output = Environment.GetEnvironmentVariable("DUXEL_3D_BENCH_OUT");
    private readonly double _seconds = BenchOptions.ReadDouble("DUXEL_3D_BENCH_SECONDS", 1.5, .8, 30);
    private readonly BenchFrameRecorder _frames = new(1_048_576);
    private readonly double[] _rawFrames = new double[1_048_576];
    private readonly List<string> _records = [];
    private readonly UiFpsCounter _fps = new(.25);
    private int _phaseIndex;
    private int _frame;
    private double _lastTime;
    private double _elapsed;
    private double _sceneMs;
    private double _sceneAllocated;
    private double _renderMs;
    private double _submitMs;
    private double _lastSceneMs;
    private long _lastSceneAllocated;
    private bool _done;

    public Dynamic3DLayersScreen()
    {
        _phases =
        [
            new(1, 1000, .2f, false), new(1, 1000, .2f, true),
            new(8, 1000, .2f, false), new(8, 1000, .2f, true),
            new(8, 4000, .8f, false), new(8, 4000, .8f, true),
        ];
        if (BenchOptions.ReadBool("DUXEL_3D_BENCH_REVERSE")) Array.Reverse(_phases);
        MarkProfilePhase();
    }

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var delta = _lastTime == 0 ? 0 : now - _lastTime;
        _lastTime = now;
        var fps = _fps.Tick(delta).Fps;
        if (!_done && _output is not null && delta > 0)
        {
            if (_elapsed >= .25)
            {
                _rawFrames[_frames.Count] = delta;
                _frames.Record(delta);
                _sceneMs += _lastSceneMs;
                _sceneAllocated += _lastSceneAllocated;
                _renderMs += ui.GetRenderTimeMs();
                _submitMs += ui.GetSubmitTimeMs();
            }
            _elapsed += delta;
            if (_elapsed >= _seconds + .25) FinishPhase();
        }
        if (_done) { DuxelApp.Exit(); return; }

        var phase = _output is null ? new Phase(8, 2000, .7f, true) : _phases[_phaseIndex];
        var started = Stopwatch.GetTimestamp();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var viewport = ui.GetMainViewport();
        var width = viewport.Size.X;
        var height = viewport.Size.Y;
        var camera = Matrix4x4.CreateLookAt(new(0, 0, 2.8f), Vector3.Zero, Vector3.UnitY)
            * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, width / height, .1f, 20);
        _frame++;
        for (var layer = 0; layer < phase.Layers; layer++)
        {
            var shift = (layer - (phase.Layers - 1) * .5f) * (1 - phase.Overlap) * .12f;
            var model = Matrix4x4.CreateRotationY(MathF.Sin((float)now * .6f + layer) * .55f)
                * Matrix4x4.CreateRotationZ(MathF.Sin((float)now * .3f + layer) * .12f)
                * Matrix4x4.CreateTranslation(shift, 0, (layer - 3) * .035f);
            var options = new UiLayerOptions(false, 1, default)
            {
                Scene3D = phase.Scene3D ? new UiLayer3D(model * camera) : null,
            };
            if (!ui.BeginLayer(_ids[layer], options)) throw new InvalidOperationException("Dynamic layer was cached.");
            var draw = ui.GetWindowDrawList();
            // Group primitive kinds for batching; every position and color changes.
            for (var i = 0; i < phase.Shapes; i++)
            {
                var x = width * .12f + ((i * 17 + _frame) % 997) / 997f * width * .76f;
                var y = height * .12f + ((i * 31 + _frame) % 991) / 991f * height * .76f;
                var color = new UiColor((uint)(0xFF000000 | (((layer + 1) * 0x28417B + i * 19 + _frame) & 0xFFFFFF)));
                draw.AddRectFilled(new(x, y, 2 + phase.Overlap * 6, 2 + phase.Overlap * 6), color, ui.WhiteTextureId);
            }
            for (var i = 0; i < phase.Shapes / 16; i++)
            {
                var x = width * .15f + ((i * 23 + _frame) % 997) / 997f * width * .7f;
                var y = height * .15f + ((i * 41 + _frame) % 991) / 991f * height * .7f;
                draw.AddTriangleFilled(new(x, y), new(x + 5, y), new(x, y + 5), new UiColor(0xFFFFFFFF));
            }
            ui.EndLayer();
        }
        _lastSceneMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        _lastSceneAllocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        const string statusTitle = "Dynamic planes / perspective camera";
        const float statusPadding = 8f;
        const float statusSpacing = 2f;
        ui.PushStyleVar(UiStyleVar.WindowPadding, new UiVector2(statusPadding, statusPadding));
        ui.PushStyleVar(UiStyleVar.ItemSpacing, new UiVector2(8f, statusSpacing));
        // One multiline layout uses the font's actual line spacing instead of
        // adding a separate widget line box between each short status line.
        var statusText = string.Create(CultureInfo.InvariantCulture,
            $"{phase.Layers} layers, {phase.Shapes} shapes/layer, {fps:0.0} FPS\n3D: {phase.Scene3D} | cache: off | overlap: {phase.Overlap:0.0}\nGeometry updates every frame.");
        var textSize = ui.CalcTextSize(statusText);
        var titleHeight = ui.GetFrameHeight() + statusPadding;
        var statusWidth = MathF.Max(textSize.X + statusPadding * 2,
            ui.CalcTextSize(statusTitle).X + titleHeight * 2 + statusPadding * 3);
        ui.SetNextWindowPos(new(viewport.WorkPos.X + 12, viewport.WorkPos.Y + 12));
        ui.SetNextWindowSize(new(MathF.Ceiling(statusWidth), MathF.Ceiling(titleHeight + statusSpacing + textSize.Y + statusPadding)));
        ui.BeginPanel(statusTitle);
        ui.Text(statusText);
        ui.EndPanel();
        ui.PopStyleVar(2);
        DuxelApp.RequestFrame();
    }

    private void FinishPhase()
    {
        var stats = _frames.Calculate();
        var phase = _phases[_phaseIndex];
        _records.Add(string.Create(CultureInfo.InvariantCulture,
            $"{{\"phase\":{_phaseIndex},\"layers\":{phase.Layers},\"shapes\":{phase.Shapes},\"overlap\":{phase.Overlap},\"scene3D\":{(phase.Scene3D ? "true" : "false")},\"avgFps\":{stats.AverageFps},\"samples\":{stats.Samples},\"medianFrameMs\":{stats.MedianFrameMs},\"p95FrameMs\":{stats.P95FrameMs},\"p99FrameMs\":{stats.P99FrameMs},\"low1PctFps\":{stats.Low1PctFps},\"sceneBuildMs\":{_sceneMs / stats.Samples},\"sceneBuildAllocatedBytes\":{_sceneAllocated / stats.Samples},\"uiRenderMs\":{_renderMs / stats.Samples},\"rendererSubmitMs\":{_submitMs / stats.Samples}}}"));
        var rawPath = _output! + $".phase-{_phaseIndex}.frames.csv";
        using (var writer = new StreamWriter(rawPath))
        {
            writer.WriteLine("frameSeconds");
            for (var i = 0; i < stats.Samples; i++) writer.WriteLine(_rawFrames[i].ToString("R", CultureInfo.InvariantCulture));
        }
        _phaseIndex++;
        _elapsed = _sceneMs = _sceneAllocated = _renderMs = _submitMs = 0;
        _frames.Reset();
        if (_phaseIndex == _phases.Length)
        {
            File.WriteAllText(_output!, $"{{\"schemaVersion\":1,\"framework\":\"{RuntimeInformation.FrameworkDescription}\",\"nativeAot\":{(!RuntimeFeature.IsDynamicCodeSupported ? "true" : "false")},\"staticCache\":false,\"warmupSeconds\":0.25,\"records\":[{string.Join(',', _records)}]}}");
            _done = true;
        }
        else MarkProfilePhase();
    }

    private void MarkProfilePhase()
    {
        var path = Environment.GetEnvironmentVariable("DUXEL_VK_PROFILE_OUT");
        if (string.IsNullOrWhiteSpace(path)) return;
        var phase = _phases[_phaseIndex];
        File.AppendAllText(path, string.Create(CultureInfo.InvariantCulture,
            $"[bench-phase] index={_phaseIndex} layers={phase.Layers} shapes={phase.Shapes} overlap={phase.Overlap} scene3D={phase.Scene3D}\n"));
    }
}
