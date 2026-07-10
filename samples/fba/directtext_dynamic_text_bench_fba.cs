// FBA: DirectText stable-cache versus changing-string churn benchmark
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

var options = BenchOptions.FromEnvironment("DUXEL_DIRECTTEXT_BENCH_");

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel DirectText Dynamic Text Bench",
        Width = 960,
        Height = 640,
        VSync = false,
    },
    Renderer = new DuxelRendererOptions
    {
        Profile = DuxelPerformanceProfile.Render,
        MsaaSamples = 1,
        TextRendering = DuxelTextRenderingMode.DirectText,
    },
    Frame = new DuxelFrameOptions
    {
        EnableIdleFrameSkip = true,
    },
    Screen = new DirectTextDynamicTextBenchScreen(options),
});

public sealed class DirectTextDynamicTextBenchScreen : UiScreen
{
    private const int PhaseCount = 2;
    private const int FrameCapacity = 1_048_576;
    private const int DigitPermutationCount = 3_628_800;

    private readonly string _outputPath;
    private readonly double _phaseSeconds;
    private readonly int _warmupFrames;
    private readonly int _rows;
    private readonly int _corpusFrames;
    private readonly string[] _stableTexts;
    private readonly string[] _changingTexts;
    private readonly BenchFrameRecorder _frameRecorder = new(FrameCapacity);
    private readonly BenchFrameRecorder _textWorkRecorder = new(FrameCapacity);
    private readonly PhaseResult[] _results = new PhaseResult[PhaseCount];

    private int _phaseIndex;
    private int _warmupFramesRemaining;
    private int _changingFrameIndex;
    private long _lastFrameTimestamp;
    private bool _recordPreviousFrame;
    private bool _measurementStarted;
    private long _allocatedBytesSum;
    private int _gen0Start;
    private int _gen1Start;
    private int _gen2Start;

    public DirectTextDynamicTextBenchScreen(BenchOptionsReader options)
    {
        _outputPath = options.String("OUT");
        if (string.IsNullOrWhiteSpace(_outputPath))
        {
            throw new InvalidOperationException("DUXEL_DIRECTTEXT_BENCH_OUT is required.");
        }

        _phaseSeconds = options.Double("PHASE_SECONDS", 3d, minExclusive: 0.5d, maxInclusive: 30d);
        _warmupFrames = options.Int("WARMUP_FRAMES", 96, minInclusive: 1, maxInclusive: 10_000);
        _rows = options.Int("ROWS", 8, minInclusive: 1, maxInclusive: 24);
        _corpusFrames = options.Int("CORPUS_FRAMES", 256, minInclusive: 1, maxInclusive: 4096);

        var corpusEntries = checked(_rows * _corpusFrames);
        if (corpusEntries <= 512 || corpusEntries >= DigitPermutationCount)
        {
            throw new InvalidOperationException("The changing-text corpus must contain between 513 and 3,628,799 unique entries.");
        }

        _stableTexts = new string[_rows];
        for (var row = 0; row < _rows; row++)
        {
            _stableTexts[row] = CreatePermutationText(3_000_000 + row);
        }

        _changingTexts = new string[corpusEntries];
        for (var i = 0; i < _changingTexts.Length; i++)
        {
            _changingTexts[i] = CreatePermutationText(i);
        }

        _warmupFramesRemaining = _warmupFrames;
    }

    public override void Render(UiImmediateContext ui)
    {
        var frameTimestamp = Stopwatch.GetTimestamp();
        if (_lastFrameTimestamp is not 0 && _recordPreviousFrame)
        {
            _frameRecorder.Record((frameTimestamp - _lastFrameTimestamp) / (double)Stopwatch.Frequency);
            if (_frameRecorder.MeasuredSeconds >= _phaseSeconds)
            {
                CompletePhase();
                if (_phaseIndex >= PhaseCount)
                {
                    WriteResults();
                    Environment.Exit(0);
                    return;
                }
            }
        }

        _lastFrameTimestamp = frameTimestamp;

        var viewport = ui.GetMainViewport();
        ui.SetNextWindowPos(new UiVector2(viewport.Pos.X + 40f, viewport.Pos.Y + 40f));
        ui.SetNextWindowSize(new UiVector2(viewport.Size.X - 80f, viewport.Size.Y - 80f));
        ui.BeginWindow("DirectText Dynamic Text Workload");

        if (_warmupFramesRemaining > 0)
        {
            DrawRows(ui);
            _warmupFramesRemaining--;
            _recordPreviousFrame = false;
        }
        else
        {
            StartMeasurementIfNeeded();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var textStart = Stopwatch.GetTimestamp();
            DrawRows(ui);
            var textTicks = Stopwatch.GetTimestamp() - textStart;
            _textWorkRecorder.Record(textTicks / (double)Stopwatch.Frequency);
            _allocatedBytesSum += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            _recordPreviousFrame = true;
        }

        ui.EndWindow();
        DuxelApp.RequestFrame();
    }

    private void DrawRows(UiImmediateContext ui)
    {
        if (_phaseIndex is 0)
        {
            for (var row = 0; row < _stableTexts.Length; row++)
            {
                ui.Text(_stableTexts[row]);
            }

            return;
        }

        var frameOffset = (_changingFrameIndex++ % _corpusFrames) * _rows;
        for (var row = 0; row < _rows; row++)
        {
            ui.Text(_changingTexts[frameOffset + row]);
        }
    }

    private void StartMeasurementIfNeeded()
    {
        if (_measurementStarted)
        {
            return;
        }

        _measurementStarted = true;
        _gen0Start = GC.CollectionCount(0);
        _gen1Start = GC.CollectionCount(1);
        _gen2Start = GC.CollectionCount(2);
    }

    private void CompletePhase()
    {
        var frameStats = _frameRecorder.Calculate();
        var textStats = _textWorkRecorder.Calculate();
        if (frameStats.Samples != textStats.Samples)
        {
            throw new InvalidOperationException("Frame and text-work sample counts diverged.");
        }

        _results[_phaseIndex] = new PhaseResult(
            _phaseIndex is 0 ? "stable-cache-hit" : "changing-cache-miss",
            frameStats,
            textStats,
            _allocatedBytesSum / frameStats.Samples,
            GC.CollectionCount(0) - _gen0Start,
            GC.CollectionCount(1) - _gen1Start,
            GC.CollectionCount(2) - _gen2Start);

        _phaseIndex++;
        _warmupFramesRemaining = _warmupFrames;
        _frameRecorder.Reset();
        _textWorkRecorder.Reset();
        _recordPreviousFrame = false;
        _measurementStarted = false;
        _allocatedBytesSum = 0;
    }

    private void WriteResults()
    {
        var textRendering = BenchOptions.ReadString("DUXEL_TEXT_RENDERING", "direct");
        var directTextPage = BenchOptions.ReadBool("DUXEL_DIRECT_TEXT_PAGE");
        var stable = _results[0];
        var changing = _results[1];

        var sb = new StringBuilder(2048);
        sb.Append("{\"schemaVersion\":1,\"benchmark\":\"directtext-dynamic-text\",\"phaseSeconds\":")
            .Append(_phaseSeconds.ToString(CultureInfo.InvariantCulture))
            .Append(",\"warmupFrames\":").Append(_warmupFrames)
            .Append(",\"rows\":").Append(_rows)
            .Append(",\"corpusFrames\":").Append(_corpusFrames)
            .Append(",\"corpusEntries\":").Append(_changingTexts.Length)
            .Append(",\"script\":\"ascii\",\"textRendering\":\"").Append(textRendering)
            .Append("\",\"directTextPage\":").Append(directTextPage ? "true" : "false")
            .Append(",\"records\":[");
        AppendResult(sb, 0, stable, expectedNewStringsPerFrame: 0);
        sb.Append(',');
        AppendResult(sb, 1, changing, expectedNewStringsPerFrame: _rows);
        sb.Append("],\"comparison\":{\"changingVsStableFpsPercent\":")
            .Append(PercentChange(stable.Frame.AverageFps, changing.Frame.AverageFps).ToString("0.###", CultureInfo.InvariantCulture))
            .Append(",\"changingVsStableTextWorkPercent\":")
            .Append(PercentChange(AverageTextWorkMicroseconds(stable), AverageTextWorkMicroseconds(changing)).ToString("0.###", CultureInfo.InvariantCulture))
            .Append(",\"changingVsStableAllocatedBytes\":")
            .Append(changing.AverageAllocatedBytes - stable.AverageAllocatedBytes)
            .Append("}}");

        File.WriteAllText(_outputPath, sb.ToString());
    }

    private static void AppendResult(StringBuilder sb, int phase, in PhaseResult result, int expectedNewStringsPerFrame)
    {
        sb.Append("{\"phase\":").Append(phase)
            .Append(",\"name\":\"").Append(result.Name)
            .Append("\",\"expectedNewStringsPerFrame\":").Append(expectedNewStringsPerFrame)
            .Append(",\"samples\":").Append(result.Frame.Samples)
            .Append(",\"measuredSeconds\":").Append(result.Frame.MeasuredSeconds.ToString("0.######", CultureInfo.InvariantCulture))
            .Append(",\"avgFps\":").Append(result.Frame.AverageFps.ToString("0.###", CultureInfo.InvariantCulture))
            .Append(",\"medianFrameMs\":").Append(result.Frame.MedianFrameMs.ToString("0.######", CultureInfo.InvariantCulture))
            .Append(",\"p95FrameMs\":").Append(result.Frame.P95FrameMs.ToString("0.######", CultureInfo.InvariantCulture))
            .Append(",\"p99FrameMs\":").Append(result.Frame.P99FrameMs.ToString("0.######", CultureInfo.InvariantCulture))
            .Append(",\"low1PctFps\":").Append(result.Frame.Low1PctFps.ToString("0.###", CultureInfo.InvariantCulture))
            .Append(",\"avgTextWorkUs\":").Append(AverageTextWorkMicroseconds(result).ToString("0.###", CultureInfo.InvariantCulture))
            .Append(",\"medianTextWorkUs\":").Append((result.TextWork.MedianFrameMs * 1000d).ToString("0.###", CultureInfo.InvariantCulture))
            .Append(",\"p95TextWorkUs\":").Append((result.TextWork.P95FrameMs * 1000d).ToString("0.###", CultureInfo.InvariantCulture))
            .Append(",\"p99TextWorkUs\":").Append((result.TextWork.P99FrameMs * 1000d).ToString("0.###", CultureInfo.InvariantCulture))
            .Append(",\"avgAllocatedBytes\":").Append(result.AverageAllocatedBytes)
            .Append(",\"gen0Collections\":").Append(result.Gen0Collections)
            .Append(",\"gen1Collections\":").Append(result.Gen1Collections)
            .Append(",\"gen2Collections\":").Append(result.Gen2Collections)
            .Append('}');
    }

    private static double AverageTextWorkMicroseconds(in PhaseResult result) =>
        result.TextWork.MeasuredSeconds / result.TextWork.Samples * 1_000_000d;

    private static double PercentChange(double baseline, double candidate) =>
        ((candidate / baseline) - 1d) * 100d;

    private static string CreatePermutationText(int index)
    {
        Span<char> available = stackalloc char[10];
        "0123456789".AsSpan().CopyTo(available);
        Span<char> permutation = stackalloc char[10];
        ReadOnlySpan<int> factorials = [1, 1, 2, 6, 24, 120, 720, 5_040, 40_320, 362_880];

        var remainder = index % DigitPermutationCount;
        var availableCount = available.Length;
        for (var position = 0; position < permutation.Length; position++)
        {
            var divisor = factorials[availableCount - 1];
            var selectedIndex = remainder / divisor;
            remainder %= divisor;
            permutation[position] = available[selectedIndex];
            for (var i = selectedIndex; i < availableCount - 1; i++)
            {
                available[i] = available[i + 1];
            }

            availableCount--;
        }

        return "Metrics " + new string(permutation);
    }

    private readonly record struct PhaseResult(
        string Name,
        BenchFrameStatistics Frame,
        BenchFrameStatistics TextWork,
        long AverageAllocatedBytes,
        int Gen0Collections,
        int Gen1Collections,
        int Gen2Collections);
}
