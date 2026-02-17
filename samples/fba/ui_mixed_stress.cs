// FBA: 균형형 UI 스트레스 샘플 — 다중 창/텍스트/테이블/리스트/입력/팝업/드로우를 동시에 부하
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Duxel.App;
using Duxel.Core;

DuxelApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Duxel Mixed UI Stress (FBA)",
        Width = 1600,
        Height = 1000,
        VSync = false
    },
    Screen = new MixedUiStressScreen()
});

public sealed class MixedUiStressScreen : UiScreen
{
    public static readonly IReadOnlyList<string> GlyphStrings = new[]
    {
        "Mixed UI Stress",
        "Controls",
        "Summary",
        "Tables",
        "Lists",
        "Forms",
        "Canvas",
        "Rows",
        "Columns",
        "Renderers",
        "Workers",
        "Search",
        "Filter",
        "Status",
        "Paused",
        "Reset",
        "FPS",
        "Count"
    };

    private readonly double _benchDurationSeconds = ReadBenchDurationSeconds();
    private readonly string? _benchOutputPath = Environment.GetEnvironmentVariable("DUXEL_PERF_BENCH_OUT");

    private bool _paused;
    private double _lastTime;
    private float _fps;
    private int _fpsFrames;
    private double _fpsTime;

    private int _windowCount = 6;
    private int _tableRows = 220;
    private int _tableCols = 5;
    private int _listItems = 1800;
    private int _textLines = 380;
    private int _canvasShapes = 1200;

    private int _selectedListIndex;
    private bool _showPopup;
    private bool _showToolWindow = true;
    private bool _showMetricsWindow = true;
    private bool _showCanvasWindow = true;
    private bool _showBaselineProbe = true;

    private string _search = "stress";
    private string _filter = "all";

    private readonly string[] _statusNames = ["Idle", "Warmup", "Busy", "Waiting", "I/O"];
    private readonly bool[] _rowFlags = new bool[4096];

    private double _benchElapsedSeconds;
    private double _benchFpsSum;
    private int _benchFpsSamples;
    private bool _benchCompleted;

    public override void Render(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        var delta = _lastTime == 0 ? 0.016 : Math.Clamp(now - _lastTime, 0.0, 0.05);
        _lastTime = now;

        UpdateFps(delta);
        TickBenchMode(ui, delta);

        var viewport = ui.GetMainViewport();
        var bounds = new UiRect(viewport.Pos.X, viewport.Pos.Y, viewport.Size.X, viewport.Size.Y);

        DrawControlWindow(ui, bounds);

        if (_showToolWindow)
        {
            DrawSummaryWindow(ui, bounds);
            DrawFormWindow(ui, bounds);
            DrawListWindow(ui, bounds);
        }

        if (_showMetricsWindow)
        {
            DrawTableWindow(ui, bounds);
            DrawTextWindow(ui, bounds);
        }

        if (_showCanvasWindow)
        {
            DrawCanvasWindow(ui, bounds);
        }

        DrawPopupAndContext(ui);
    }

    private static double ReadBenchDurationSeconds()
    {
        var value = Environment.GetEnvironmentVariable("DUXEL_PERF_BENCH_SECONDS");
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0d;
        }

        return double.TryParse(value, out var seconds) && seconds > 0d ? seconds : 0d;
    }

    private void TickBenchMode(UiImmediateContext ui, double delta)
    {
        if (_benchCompleted || _benchDurationSeconds <= 0d)
        {
            return;
        }

        _benchElapsedSeconds += delta;
        if (_fps > 0f)
        {
            _benchFpsSum += _fps;
            _benchFpsSamples++;
        }

        if (_benchElapsedSeconds < _benchDurationSeconds)
        {
            return;
        }

        _benchCompleted = true;
        var avgFps = _benchFpsSamples > 0 ? _benchFpsSum / _benchFpsSamples : 0d;
        var vsync = ui.GetVSync();

        if (!string.IsNullOrWhiteSpace(_benchOutputPath))
        {
            var json = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"avgFps\":{0:0.###},\"samples\":{1},\"elapsedSeconds\":{2:0.###},\"vsync\":{3},\"windows\":{4},\"rows\":{5},\"listItems\":{6},\"canvas\":{7}}}",
                avgFps,
                _benchFpsSamples,
                _benchElapsedSeconds,
                vsync.ToString().ToLowerInvariant(),
                _windowCount,
                _tableRows,
                _listItems,
                _canvasShapes
            );
            File.WriteAllText(_benchOutputPath!, json);
        }

        Environment.Exit(0);
    }

    private void UpdateFps(double delta)
    {
        if (_paused)
        {
            return;
        }

        _fpsFrames++;
        _fpsTime += delta;
        if (_fpsTime >= 0.5)
        {
            _fps = (float)(_fpsFrames / _fpsTime);
            _fpsFrames = 0;
            _fpsTime = 0;
        }
    }

    private void DrawControlWindow(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 16f, bounds.Y + 16f));
        ui.SetNextWindowSize(new UiVector2(360f, 420f));
        ui.BeginWindow("Controls");

        ui.TextV("FPS: {0:0.0}", _fps);
        ui.TextV("Windows: {0}", _windowCount);

        var vsync = ui.GetVSync();
        if (ui.Checkbox("VSync", ref vsync))
        {
            ui.SetVSync(vsync);
        }

        ui.Checkbox("Paused", ref _paused);
        ui.Checkbox("Show Tool Windows", ref _showToolWindow);
        ui.Checkbox("Show Metrics Windows", ref _showMetricsWindow);
        ui.Checkbox("Show Canvas Window", ref _showCanvasWindow);
        ui.Checkbox("Show Baseline Probe", ref _showBaselineProbe);

        ui.SeparatorText("Workload");
        ui.SliderInt("UI Windows", ref _windowCount, 3, 10);
        ui.SliderInt("Table Rows", ref _tableRows, 40, 800);
        ui.SliderInt("Table Cols", ref _tableCols, 3, 8);
        ui.SliderInt("List Items", ref _listItems, 200, 4000);
        ui.SliderInt("Text Lines", ref _textLines, 80, 1200);
        ui.SliderInt("Canvas Shapes", ref _canvasShapes, 100, 3000);

        if (ui.Button("Reset Workload"))
        {
            _windowCount = 6;
            _tableRows = 220;
            _tableCols = 5;
            _listItems = 1800;
            _textLines = 380;
            _canvasShapes = 1200;
        }

        ui.EndWindow();
    }

    private void DrawSummaryWindow(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 390f, bounds.Y + 16f));
        ui.SetNextWindowSize(new UiVector2(380f, 260f));
        ui.BeginWindow("Summary");

        ui.Text("Mixed stress scenario:");
        ui.BulletText("many windows");
        ui.BulletText("heavy text layout");
        ui.BulletText("table row iteration");
        ui.BulletText("list selectable updates");
        ui.BulletText("dynamic canvas draws");

        ui.SeparatorText("Quick Fields");
        ui.InputText("Search", ref _search, 64);
        ui.InputText("Filter", ref _filter, 64);

        if (_showBaselineProbe)
        {
            ui.SeparatorText("Baseline Probe");
            ui.Text("gyjpq • gyjpq • gyjpq");
            ui.Text("Iil1 | O0o | .,;:!?");
            ui.Text("한글 테스트: 가각간갇갈값");
        }

        if (ui.Button("Open Popup"))
        {
            _showPopup = true;
            ui.OpenPopup("stress_popup");
        }

        ui.EndWindow();
    }

    private void DrawFormWindow(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 390f, bounds.Y + 286f));
        ui.SetNextWindowSize(new UiVector2(380f, 350f));
        ui.BeginWindow("Forms");

        for (var i = 0; i < 48; i++)
        {
            var value = _rowFlags[i];
            if (ui.Checkbox($"Feature {i}", ref value))
            {
                _rowFlags[i] = value;
            }

            if ((i % 3) != 2)
            {
                ui.SameLine();
            }
        }

        ui.SeparatorText("Buttons");
        for (var i = 0; i < 24; i++)
        {
            ui.Button($"Action {i}");
            if ((i % 4) != 3)
            {
                ui.SameLine();
            }
        }

        ui.EndWindow();
    }

    private void DrawListWindow(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 780f, bounds.Y + 16f));
        ui.SetNextWindowSize(new UiVector2(350f, 620f));
        ui.BeginWindow("Lists");

        if (ui.BeginListBox("Rows", new UiVector2(0f, 560f)))
        {
            for (var i = 0; i < _listItems; i++)
            {
                var selected = i == _selectedListIndex;
                if (ui.Selectable($"Item {i:0000}  |  {_statusNames[i % _statusNames.Length]}", selected))
                {
                    _selectedListIndex = i;
                }
            }
            ui.EndListBox();
        }

        ui.EndWindow();
    }

    private void DrawTableWindow(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 1140f, bounds.Y + 16f));
        ui.SetNextWindowSize(new UiVector2(430f, 450f));
        ui.BeginWindow("Tables");

        if (ui.BeginTable("stress_table", _tableCols, UiTableFlags.Borders | UiTableFlags.RowBg))
        {
            for (var c = 0; c < _tableCols; c++)
            {
                ui.TableSetupColumn($"Col {c}");
            }
            ui.TableHeadersRow();

            for (var row = 0; row < _tableRows; row++)
            {
                ui.TableNextRow();
                for (var col = 0; col < _tableCols; col++)
                {
                    ui.TableSetColumnIndex(col);
                    ui.TextV("R{0} C{1} | {2}", row, col, _statusNames[(row + col) % _statusNames.Length]);
                }
            }

            ui.EndTable();
        }

        ui.EndWindow();
    }

    private void DrawTextWindow(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 1140f, bounds.Y + 476f));
        ui.SetNextWindowSize(new UiVector2(430f, 320f));
        ui.BeginWindow("Renderers");

        for (var i = 0; i < _textLines; i++)
        {
            ui.TextV("Line {0:0000} :: alpha beta gamma delta epsilon zeta eta theta", i);
        }

        ui.EndWindow();
    }

    private void DrawCanvasWindow(UiImmediateContext ui, UiRect bounds)
    {
        ui.SetNextWindowPos(new UiVector2(bounds.X + 390f, bounds.Y + 646f));
        ui.SetNextWindowSize(new UiVector2(740f, 300f));
        ui.BeginWindow("Canvas");

        var drawList = ui.GetWindowDrawList();
        var cursor = ui.GetCursorScreenPos();
        var region = ui.GetContentRegionAvail();

        var clip = new UiRect(cursor.X, cursor.Y, MathF.Max(1f, region.X), MathF.Max(1f, region.Y));
        var white = ui.WhiteTextureId;

        var columns = Math.Max(1, (int)(clip.Width / 20f));
        for (var i = 0; i < _canvasShapes; i++)
        {
            var x = i % columns;
            var y = i / columns;
            var p = new UiVector2(clip.X + 6f + x * 20f, clip.Y + 6f + y * 14f);
            var p2 = new UiVector2(p.X + 10f, p.Y + 8f);
            var color = new UiColor((uint)(0xFF000000u | ((uint)((i * 37) & 0xFF) << 16) | ((uint)((i * 17) & 0xFF) << 8) | (uint)((i * 53) & 0xFF)));
            drawList.AddRectFilled(new UiRect(p.X, p.Y, p2.X - p.X, p2.Y - p.Y), color, white, clip);
        }

        ui.Dummy(new UiVector2(MathF.Max(1f, region.X), MathF.Max(1f, region.Y)));
        ui.EndWindow();
    }

    private void DrawPopupAndContext(UiImmediateContext ui)
    {
        if (_showPopup && ui.BeginPopup("stress_popup"))
        {
            ui.Text("Stress popup opened.");
            ui.TextV("Selected item: {0}", _selectedListIndex);
            if (ui.Button("Close"))
            {
                _showPopup = false;
                ui.CloseCurrentPopup();
            }
            ui.EndPopup();
        }
    }
}
