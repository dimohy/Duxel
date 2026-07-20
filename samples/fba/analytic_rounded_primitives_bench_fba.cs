// FBA: Rounded rectangle and circle primitive quality/performance gate.
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System.Diagnostics;
using System.Globalization;
using System.IO;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Analytic Rounded Primitive Bench",
        Width = 1280,
        Height = 720,
        VSync = false,
    },
    Renderer = new DuxelRendererOptions
    {
        Profile = DuxelPerformanceProfile.Render,
        MsaaSamples = 1,
    },
    Screen = new AnalyticRoundedPrimitiveBenchScreen(),
});

public sealed class AnalyticRoundedPrimitiveBenchScreen : UiScreen
{
    private readonly string? _outputPath = Environment.GetEnvironmentVariable("DUXEL_ROUNDED_BENCH_OUT");
    private readonly int[] _counts = BenchOptions.ReadIntCsv("DUXEL_ROUNDED_BENCH_COUNTS", [2_000, 6_000, 12_000], minInclusive: 100);
    private readonly double _phaseSeconds = BenchOptions.ReadDouble("DUXEL_ROUNDED_BENCH_PHASE_SECONDS", 1.5d, minExclusive: 0.8d);
    private readonly double _warmupSeconds = BenchOptions.ReadDouble("DUXEL_ROUNDED_BENCH_WARMUP_SECONDS", 0.3d, minExclusive: 0d);
    private readonly List<string> _records = [];
    private readonly BenchFrameRecorder _frameRecorder = new(1_048_576);

    private int _phaseIndex;
    private double _phaseElapsed;
    private double _lastWallTime;

    public AnalyticRoundedPrimitiveBenchScreen()
    {
        ValidatePrimitiveContract();
    }

    public override void Render(UiImmediateContext ui)
    {
        var wallNow = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        var delta = _lastWallTime is 0d ? 0.016d : Math.Max(0.000001d, wallNow - _lastWallTime);
        _lastWallTime = wallNow;

        TickBenchmark(delta);
        DrawScene(ui);
        DuxelApp.RequestFrame();
    }

    private void TickBenchmark(double delta)
    {
        if (string.IsNullOrWhiteSpace(_outputPath) || _phaseIndex >= _counts.Length)
        {
            return;
        }

        var previousElapsed = _phaseElapsed;
        _phaseElapsed += delta;
        if (previousElapsed >= _warmupSeconds)
        {
            _frameRecorder.Record(delta);
        }

        if (_phaseElapsed < _warmupSeconds + _phaseSeconds)
        {
            return;
        }

        var stats = _frameRecorder.Calculate();
        _records.Add(string.Format(
            CultureInfo.InvariantCulture,
            "{{\"phase\":{0},\"primitiveSets\":{1},\"avgFps\":{2:0.###},\"samples\":{3},\"measuredSeconds\":{4:0.######},\"medianFrameMs\":{5:0.######},\"p95FrameMs\":{6:0.######},\"p99FrameMs\":{7:0.######},\"low1PctFps\":{8:0.###}}}",
            _phaseIndex,
            _counts[_phaseIndex],
            stats.AverageFps,
            stats.Samples,
            stats.MeasuredSeconds,
            stats.MedianFrameMs,
            stats.P95FrameMs,
            stats.P99FrameMs,
            stats.Low1PctFps));

        _phaseIndex++;
        _phaseElapsed = 0d;
        _frameRecorder.Reset();

        if (_phaseIndex >= _counts.Length)
        {
            var json = string.Create(
                CultureInfo.InvariantCulture,
                $"{{\"sdk\":\"{Environment.Version}\",\"msaa\":1,\"warmupSeconds\":{_warmupSeconds},\"phaseSeconds\":{_phaseSeconds},\"records\":[{string.Join(',', _records)}]}}");
            File.WriteAllText(_outputPath, json);
            Environment.Exit(0);
        }
    }

    private void DrawScene(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        var canvas = new UiRect(viewport.Pos.X, viewport.Pos.Y, viewport.Size.X, viewport.Size.Y);
        var drawList = ui.GetBackgroundDrawList();
        var white = ui.WhiteTextureId;
        drawList.AddRectFilled(canvas, new UiColor(0xFFF4F6F8), white, canvas);

        if (string.IsNullOrWhiteSpace(_outputPath))
        {
            DrawVisualReference(drawList, white, canvas);
            return;
        }

        var count = _counts[Math.Min(_phaseIndex, _counts.Length - 1)];
        var usableWidth = MathF.Max(1f, canvas.Width - 48f);
        var usableHeight = MathF.Max(1f, canvas.Height - 48f);
        var fill = new UiColor(0xCCFFFFFF);
        var border = new UiColor(0xFF15202B);
        var circle = new UiColor(0xCC1976D2);

        for (var i = 0; i < count; i++)
        {
            var u = (i * 37 % 997) / 997f;
            var v = (i * 53 % 991) / 991f;
            var width = 14f + (i % 11);
            var height = 10f + (i % 7);
            var x = canvas.X + 24f + u * MathF.Max(1f, usableWidth - width);
            var y = canvas.Y + 24f + v * MathF.Max(1f, usableHeight - height);
            var radius = MathF.Min(6f, MathF.Min(width, height) * 0.45f);
            var rect = new UiRect(x, y, width, height);

            drawList.AddRectFilledRounded(rect, fill, border, white, radius, 1f, canvas);

            var circleRadius = 2.5f + (i % 5) * 0.4f;
            drawList.AddCircleFilled(
                new UiVector2(x + width * 0.5f, y + height * 0.5f),
                circleRadius,
                circle,
                white,
                12);
        }
    }

    private static void DrawVisualReference(UiDrawListBuilder drawList, UiTextureId white, UiRect canvas)
    {
        var panel = new UiRect(canvas.X + 72f, canvas.Y + 64f, canvas.Width - 144f, canvas.Height - 128f);
        drawList.AddRectFilledRounded(
            panel,
            new UiColor(0xFFFFFFFF),
            new UiColor(0xFF17212B),
            white,
            18f,
            1f,
            canvas);

        var inset = new UiRect(panel.X + 36f, panel.Y + 36f, panel.Width - 72f, 150f);
        drawList.AddRectFilledRounded(
            inset,
            new UiColor(0xFFF3F7FB),
            new UiColor(0xFF2374AB),
            white,
            12f,
            2f,
            canvas);

        var borderOnly = new UiRect(panel.X + 36f, inset.Y + inset.Height + 32f, panel.Width - 72f, 170f);
        drawList.AddRect(borderOnly, new UiColor(0xFF17212B), 16f, 2f);

        var centerY = borderOnly.Y + borderOnly.Height * 0.5f;
        for (var i = 0; i < 7; i++)
        {
            var radius = 10f + i * 5f;
            drawList.AddCircleFilled(
                new UiVector2(borderOnly.X + 72f + i * 105f, centerY),
                radius,
                new UiColor(0xFF1976D2),
                white,
                3 + i);
        }
    }

    private static void ValidatePrimitiveContract()
    {
        var clip = new UiRect(0f, 0f, 400f, 300f);
        var source = new UiDrawListBuilder(clip);
        source.AddRectFilledRounded(
            new UiRect(10f, 10f, 180f, 100f),
            new UiColor(0xFFFFFFFF),
            new UiColor(0xFF17212B),
            default,
            16f,
            2f,
            clip);
        source.AddRect(new UiRect(20f, 130f, 220f, 120f), new UiColor(0xFF17212B), 18f, 2f);
        source.AddCircleFilled(new UiVector2(280f, 80f), 24f, new UiColor(0xFF1976D2), default, 3);
        source.AddCircleFilled(new UiVector2(340f, 80f), 24f, new UiColor(0xFF1976D2), default, 64);

        var sourceList = source.CloneOutput();
        try
        {
            AssertPrimitiveContract(sourceList);

            var replay = new UiDrawListBuilder(clip);
            replay.AddDrawList(sourceList);
            var replayList = replay.CloneOutput();
            try
            {
                AssertPrimitiveContract(replayList);
            }
            finally
            {
                replayList.ReleasePooled();
            }
        }
        finally
        {
            sourceList.ReleasePooled();
        }
    }

    private static void AssertPrimitiveContract(UiDrawList drawList)
    {
        if (drawList.Vertices.Count is not 0
            || drawList.Indices.Count is not 0
            || drawList.RectFilledPrimitives?.Count is not 2
            || drawList.CircleFilledPrimitives?.Count is not 2
            || drawList.Commands.Count is not 3
            || drawList.Commands[0].VertexOffset is not 0u
            || drawList.Commands[1].VertexOffset is not 1u
            || drawList.Commands[2].ElementCount is not 2u
            || drawList.Commands[2].VertexOffset is not 0u)
        {
            throw new InvalidOperationException("Analytic rounded primitive contract validation failed.");
        }
    }
}
