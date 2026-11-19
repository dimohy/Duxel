using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class RepaintExperiment : UiControlWidget
{
    private readonly UiState<bool> _running = new(false);
    private readonly UiState<bool> _profile = new(false);
    private readonly UiState<string> _text = new("빠르게 입력해 보세요. 한글과 English");
    private readonly UiViewHost _host;
    private readonly UiWidget _controls;
    private readonly UiWidget _textView;
    private readonly UiWidget _fixedView;
    private long _revision;
    private long _staticBuilds, _textBuilds;
    private long _dynamicVersion, _staticVersion, _drawnRevision = -1;
    private string? _drawnText;
    private UiRect _dynamicBounds, _staticBounds;
    private double _nextUpdate;
    private string _snapshot = "자동 변경을 멈춘 뒤 ‘측정값 확인’을 누르세요. 강조를 끄면 진단용 추가 그리기가 사라집니다.";
    private string _cpuSnapshot = "CPU 구간 측정을 켠 뒤 ‘측정값 확인’을 누르세요.";
    internal string CpuSnapshot => _cpuSnapshot;
    private UiRendererFrameMetrics _lastMetrics;
    private UiInputProcessingMetrics _lastInputMetrics;
    private double _lastBuildMilliseconds;
    internal bool Running => _running.Value;
    internal long StaticBuilds => _staticBuilds;
    internal long TextBuilds => _textBuilds;
    internal bool Profile { get => _profile.Value; set => _profile.Value = value; }
    internal bool AutoChange { get => _running.Value; set => _running.Value = value; }

    internal RepaintExperiment()
    {
        _controls = Dux.Column(new(Spacing : 10, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1("직접 입력하거나 텍스트를 자동으로 바꾸면서 아래의 고정된 패널과 비교해 보세요."),
            Fluent.Body1("강조 테두리는 0.2초 뒤 사라집니다. 테두리를 지우느라 다시 그린 영역은 내용이 바뀐 영역과 따로 기록합니다."),
            Fluent.Body1("오른쪽 상단의 갱신 영역 강조는 모든 탭에 적용됩니다. 강조 중에는 상단 수치 표시를 고정합니다."),
            Dux.Wrap(new(Spacing : 16, RunSpacing : 8),
                Fluent.Switch("텍스트 자동 변경 (60Hz)", _running.Binding),
                Fluent.Switch("CPU 구간 측정", _profile.Binding),
                Fluent.Button("한 번 변경", Advance),
                Fluent.Button("측정값 확인", Snapshot)),
            Fluent.Input("갱신 관찰 입력", _text.Binding, 512),
            Fluent.Caption1(() => _snapshot),
            Fluent.Caption1(() => _cpuSnapshot));
        _textView = Fluent.Body1(() => $"변경 횟수 : {_revision:D8} / {_text.Value}");
        _fixedView = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Title3("변하지 않는 패널"),
            Fluent.Body1("텍스트가 바뀌어도 이 패널의 구성은 재사용합니다."),
            Fluent.Body1("이 패널을 GPU가 다시 그렸는지는 초록 테두리로 확인하세요."));
        _host = new(new UiView(RenderContents)) { AccessibleName = "갱신 관찰 실험" };
        OwnChild("host", _host);
        OwnChild("controls", _controls);
        OwnChild("dynamic", _textView);
        OwnChild("static", _fixedView);
    }

    private void Advance() => _revision++;

    private void Snapshot()
    {
        var m = _lastMetrics;
        _snapshot = $"직전 내용 구성 : {_lastBuildMilliseconds:F3} ms / Draw : {m.DrawCalls} / Upload : {m.DynamicUploadBytes + m.StagingUploadBytes} B / "
            + $"재사용 : {m.ResidentHits} / 내용 갱신 : {m.RepaintBounds.Width:F0}×{m.RepaintBounds.Height:F0} px / 진단 추가 갱신 : {m.RepaintDiagnosticRedraw} / "
            + $"정적 구성 : {_staticBuilds} / 텍스트 구성 : {_textBuilds}";
        var input = _lastInputMetrics;
        _cpuSnapshot = input.ProfilingEnabled
            ? $"직전 Render : 입력 {input.CompletedSteps}/{input.RequestedSteps} / 그리기 구성 {input.BuiltDrawings}"
                + $"\n준비 : {input.PreparationMilliseconds:F3} ms / UI : {input.CompositionMilliseconds:F3} ms / 마무리 : {input.FinalizationMilliseconds:F3} ms"
                + $"\n현재 렌더 스레드 할당 : {input.AllocatedBytesOnRenderThread} B"
            : "CPU 구간 측정 꺼짐";
    }

    protected override UiVector2 MeasureContent(UiControlContext context)
    {
        var constraints = context.LayoutConstraints;
        var controls = context.MeasureConstrained(_controls, new(0, constraints.MaxWidth, 0, float.PositiveInfinity));
        return constraints.Constrain(new(constraints.MaxWidth, controls.Y + 252));
    }
    protected override void RenderControl(UiImmediateContext ui) => _host.Render(ui);
    private void RenderContents(UiImmediateContext ui)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        _lastMetrics = ui.GetRendererFrameMetrics();
        _lastInputMetrics = ui.GetInputProcessingMetrics();
        var bodyOrigin = ui.GetCursorScreenPos();
        _controls.Render(ui);
        var controlsSize = ui.GetItemRectSize();
        ui.SetCursorScreenPos(new(bodyOrigin.X, bodyOrigin.Y + controlsSize.Y + 8));
        ui.SetInputProfilingEnabled(_profile.Value);
        var now = ui.GetTime();
        if (_running.Value)
        {
            if (now >= _nextUpdate)
            {
                Advance();
                _nextUpdate = now + 1d / 60;
            }
            ui.RequestFrameAt(_nextUpdate);
        }
        var origin = ui.GetCursorScreenPos();
        var width = MathF.Max(1, MathF.Min(920, ui.GetContentRegionAvail().X));
        var dynamicBounds = new UiRect(origin.X, origin.Y, width, 64);
        var staticBounds = new UiRect(origin.X, origin.Y + 96, width, 140);
        if (_drawnRevision != _revision || _drawnText != _text.Value || _dynamicBounds != dynamicBounds)
        {
            _dynamicVersion++;
            _drawnRevision = _revision;
            _drawnText = _text.Value;
            _dynamicBounds = dynamicBounds;
        }
        if (_staticBounds != staticBounds)
        {
            _staticVersion++;
            _staticBounds = staticBounds;
        }
        DrawPanel("repaint-text", dynamicBounds, _dynamicVersion, _textView, ref _textBuilds);
        DrawPanel("repaint-fixed", staticBounds, _staticVersion, _fixedView, ref _staticBuilds);
        ui.SetCursorScreenPos(new(origin.X, staticBounds.Y + staticBounds.Height + 8));
        ui.Dummy(new(width, 0));
        _lastBuildMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        void DrawPanel(string id, UiRect bounds, long version, UiWidget content, ref long builds)
        {
            if (ui.BeginLayer(id, new(false, 1, default)
            {
                ContentVersion = version
            }))
            {
                builds++;
                ui.PushClipRect(bounds, true);
                ui.GetWindowDrawList().AddRectFilled(bounds, new(35, 40, 48), ui.WhiteTextureId, bounds);
                ui.SetCursorScreenPos(new(bounds.X + 12, bounds.Y + 12));
                content.Render(ui);
                ui.PopClipRect();
            }
            ui.EndLayer();
        }
    }
}
