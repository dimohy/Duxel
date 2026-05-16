// FBA: Duxel 2D 렌더링 성능 측정 — FPS 카운터
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Diagnostics;
using System.IO;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel 2D Performance Test",
        Width = 1200,
        Height = 800,
        VSync = false
    },
    Renderer = new DuxelRendererOptions
    {
        Profile = DuxelPerformanceProfile.Render,
        MsaaSamples = 1
    },
    Frame = new DuxelFrameOptions
    {
        EnableIdleFrameSkip = false
    },
    Debug = Perf2DRenderDiagnostics.CreateDebugOptions(),
    Screen = new Perf2DRenderScreen()
});

public static class Perf2DRenderDiagnostics
{
    public static DuxelDebugOptions CreateDebugOptions()
    {
        var enabled = string.Equals(Environment.GetEnvironmentVariable("DUXEL_PERF_DEBUG"), "1", StringComparison.Ordinal);
        return enabled
            ? new DuxelDebugOptions
            {
                Log = Log,
                LogStartupTimings = true
            }
            : new DuxelDebugOptions();
    }

    public static void Log(string message)
    {
        var logPath = Environment.GetEnvironmentVariable("DUXEL_PERF_LOG_PATH");
        if (string.IsNullOrWhiteSpace(logPath))
        {
            Console.WriteLine(message);
            return;
        }

        var directory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(logPath, "DUXEL_DEBUG " + message + Environment.NewLine);
    }
}

public sealed class Perf2DRenderScreen : UiScreen
{
    private int _frameCount = 0;
    private Stopwatch _sessionTimer = Stopwatch.StartNew();
    private double _currentFps = 0;
    private double _avgFps = 0;
    private long _lastFrameTick = 0;
    private int _testLevel = ParseTestLevel(Environment.GetEnvironmentVariable("DUXEL_PERF_TEST_LEVEL"));
    private bool _showMetrics = true;
    private bool _showTest = true;
    private int _lastLoggedSecond = -1;
    private bool _firstRenderLogged;
    private readonly bool _consoleLogEnabled = !string.Equals(Environment.GetEnvironmentVariable("DUXEL_PERF_CONSOLE"), "0", StringComparison.Ordinal);
    private readonly string? _logPath = Environment.GetEnvironmentVariable("DUXEL_PERF_LOG_PATH");

    public Perf2DRenderScreen()
    {
        if (!string.IsNullOrWhiteSpace(_logPath))
        {
            var directory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_logPath, "DUXEL_PERF_BEGIN" + Environment.NewLine);
        }
    }

    public override void Render(UiImmediateContext ui)
    {
        if (!_firstRenderLogged)
        {
            _firstRenderLogged = true;
            WriteLogLine("DUXEL_PERF_RENDER_FIRST");
        }

        // Update FPS telemetry.
        var tick = Stopwatch.GetTimestamp();
        _frameCount++;
        if (_frameCount > 1)
        {
            var delta = tick - _lastFrameTick;
            _currentFps = Stopwatch.Frequency / (double)delta;
            _avgFps = _frameCount / (_sessionTimer.ElapsedTicks / (double)Stopwatch.Frequency);
            LogMetricsIfNeeded();
        }
        _lastFrameTick = tick;

        if (_showMetrics)
        {
            ui.SetWindowOpen("FPS Metrics", true);
            RenderMetrics(ui);
            _showMetrics = ui.GetWindowOpen("FPS Metrics");
        }

        if (_showTest)
        {
            ui.SetWindowOpen("Test Content", true);
            RenderTestContent(ui);
            _showTest = ui.GetWindowOpen("Test Content");
        }
    }

    private void LogMetricsIfNeeded()
    {
        if (!_consoleLogEnabled)
        {
            return;
        }

        var elapsedSecond = (int)_sessionTimer.Elapsed.TotalSeconds;
        if (elapsedSecond <= 0 || elapsedSecond == _lastLoggedSecond)
        {
            return;
        }

        _lastLoggedSecond = elapsedSecond;
        var frameTimeMs = _sessionTimer.ElapsedMilliseconds > 0
            ? _sessionTimer.ElapsedMilliseconds / (double)_frameCount
            : 0d;
        var line = $"DUXEL_PERF level={_testLevel} second={elapsedSecond} frame={_frameCount} currentFps={_currentFps:F1} avgFps={_avgFps:F1} avgFrameMs={frameTimeMs:F3}";
        Console.WriteLine(line);
        WriteLogLine(line);
    }

    private void WriteLogLine(string line)
    {
        if (!string.IsNullOrWhiteSpace(_logPath))
        {
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
    }

    private static int ParseTestLevel(string? value)
    {
        return int.TryParse(value, out var parsed) && parsed is >= 1 and <= 3 ? parsed : 2;
    }

    private void RenderMetrics(UiImmediateContext ui)
    {
        ui.SetWindowOpen("FPS Metrics", true);
        ui.BeginWindow("FPS Metrics");

        ui.Text($"Frame: {_frameCount}");
        ui.Text($"Current FPS: {_currentFps:F1}");
        ui.Text($"Average FPS: {_avgFps:F1}");
        ui.Text($"Time: {_sessionTimer.Elapsed.TotalSeconds:F1}s");
        ui.Separator();
        if (_sessionTimer.ElapsedMilliseconds > 0)
        {
            var ms = _sessionTimer.ElapsedMilliseconds / (double)_frameCount;
            ui.Text($"Frame Time: {ms:F2}ms");
        }

        ui.EndWindow();
    }

    private void RenderTestContent(UiImmediateContext ui)
    {
        ui.SetWindowOpen("Test Content", true);
        ui.BeginWindow("Test Content");

        if (ui.RadioButton("Light##1", _testLevel == 1))
            _testLevel = 1;
        ui.SameLine();
        if (ui.RadioButton("Normal##2", _testLevel == 2))
            _testLevel = 2;
        ui.SameLine();
        if (ui.RadioButton("Heavy##3", _testLevel == 3))
            _testLevel = 3;
        ui.Separator();

        if (_testLevel >= 1)
        {
            ui.Text("=== Light ===");
            for (int i = 0; i < 10; i++)
            {
                ui.Button($"B{i}", new UiVector2(50, 0));
                if (i < 9) ui.SameLine();
            }
        }

        if (_testLevel >= 2)
        {
            ui.Separator();
            ui.Text("=== Normal ===");
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    ui.Button($"##B{r}x{c}", new UiVector2(60, 25));
                    if (c < 7) ui.SameLine();
                }
            }
        }

        if (_testLevel >= 3)
        {
            ui.Separator();
            ui.Text("=== Heavy ===");
            for (int r = 0; r < 10; r++)
            {
                for (int c = 0; c < 10; c++)
                {
                    ui.Button($"##H{r}x{c}", new UiVector2(40, 20));
                    if (c < 9) ui.SameLine();
                }
            }
        }

        ui.EndWindow();
    }
}
