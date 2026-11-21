using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class RepaintExperiment : UiControlWidget
{
    private readonly UiState<bool> _running = new(false);
    private readonly UiState<bool> _profile = new(false);
    private readonly UiState<string> _text = new(GalleryText.Get("Try typing quickly. Text and English"));
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
    private string _snapshot = GalleryText.Get("Stop automatic updates and select Read measurements. Turn off repaint highlighting to remove its extra drawing.");
    private string _cpuSnapshot = GalleryText.Get("Enable CPU profiling and select Read measurements.");
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
            Fluent.Body1(GalleryText.Get("Type or enable automatic text updates and compare the result with the fixed panel below.")),
            Fluent.Body1(GalleryText.Get("The highlight fades after 0.2 seconds. Redrawing to clear it is counted separately from repainting changed content.")),
            Fluent.Body1(GalleryText.Get("The repaint highlight in the top toolbar applies to all pages. The toolbar statistics freeze while highlighting is active.")),
            Dux.Wrap(new(Spacing : 16, RunSpacing : 8),
                Fluent.Switch(GalleryText.Get("Update text automatically (60 Hz)"), _running.Binding),
                Fluent.Switch(GalleryText.Get("CPU profiling"), _profile.Binding),
                Fluent.Button(GalleryText.Get("Update once"), Advance),
                Fluent.Button(GalleryText.Get("Read measurements"), Snapshot)),
            Fluent.Input(GalleryText.Get("Repaint observer input"), _text.Binding, 512),
            Fluent.Caption1(() => _snapshot),
            Fluent.Caption1(() => _cpuSnapshot));
        _textView = Fluent.Body1(() => string.Format(GalleryText.Get("Updates: {0:D8} / {1}"), _revision, _text.Value));
        _fixedView = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Title3(GalleryText.Get("Fixed panel")),
            Fluent.Body1(GalleryText.Get("This panel reuses its content when the text changes.")),
            Fluent.Body1(GalleryText.Get("A green border shows when the GPU repaints this panel.")));
        _host = new(new UiView(RenderContents)) { AccessibleName = GalleryText.Get("Repaint experiment") };
        OwnChild("host", _host);
        OwnChild("controls", _controls);
        OwnChild("dynamic", _textView);
        OwnChild("static", _fixedView);
    }

    private void Advance() => _revision++;

    private void Snapshot()
    {
        var m = _lastMetrics;
        _snapshot = string.Format(GalleryText.Get("Previous composition: {0:F3} ms / Draws: {1} / Upload: {2} B / Reuse: {3} / Content repaint: {4:F0}×{5:F0} px / Diagnostic redraw: {6} / Static builds: {7} / Text builds: {8}"),
            _lastBuildMilliseconds, m.DrawCalls, m.DynamicUploadBytes + m.StagingUploadBytes, m.ResidentHits,
            m.RepaintBounds.Width, m.RepaintBounds.Height, m.RepaintDiagnosticRedraw, _staticBuilds, _textBuilds);
        var input = _lastInputMetrics;
        _cpuSnapshot = input.ProfilingEnabled
            ? string.Format(GalleryText.Get("Previous render: input {0}/{1} / Drawings built: {2}"), input.CompletedSteps, input.RequestedSteps, input.BuiltDrawings)
                + "\n" + string.Format(GalleryText.Get("Preparation: {0:F3} ms / UI: {1:F3} ms / Finalization: {2:F3} ms"),
                    input.PreparationMilliseconds, input.CompositionMilliseconds, input.FinalizationMilliseconds)
                + "\n" + string.Format(GalleryText.Get("Render thread allocations: {0} B"), input.AllocatedBytesOnRenderThread)
            : GalleryText.Get("CPU profiling is off");
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
