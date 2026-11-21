using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.App;
using Duxel.Core;
using Duxel.Gallery;

internal sealed class ObserverLinuxSliderVerificationScreen : UiScreen
{
    private const int Warmup = 16, Samples = 32;
    private static readonly string[] Scenarios = ["MinimalSlider", "ObserverFluent", "ObserverFluentFrozenDiagnostics"];
    private readonly ObserverScreen _observer;
    private readonly DuxelAppSession _session;
    private readonly string _output;
    private readonly UiState<float> _value = new(.1f);
    private readonly UiFluentSlider _slider;
    private readonly List<LinuxSliderFrame> _frames = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _scenario, _ordinal;
    private ulong _pendingFrame;
    private float _pendingValue;
    private double _pendingBuild;
    private bool _pageRendered, _revealRequested, _sliderVisible;
    private double _scrollRevealSettlesAt;

    internal ObserverLinuxSliderVerificationScreen(ObserverScreen observer, DuxelAppSession session, string output)
    {
        _observer = observer;
        _session = session;
        _output = Path.GetFullPath(output);
        if (Directory.Exists(_output))
            throw new IOException("Specify a fresh slider verification directory.");
        Directory.CreateDirectory(_output);
        _slider = Fluent.Slider("진행률", _value.Binding, 0, 1, .01f);
        _slider.Size = new(460, 0);
        Write(false, null);
    }

    public override void Render(UiImmediateContext ui)
    {
        if (_clock.Elapsed.TotalSeconds > 90)
            throw new TimeoutException("Linux slider diagnosis exceeded90 seconds.");
        var metrics = ui.GetRendererFrameMetrics();
        if (_pendingFrame != 0 && metrics.DrawDataFrameId == _pendingFrame)
        {
            _frames.Add(new(Scenarios[_scenario], _ordinal - Warmup, _pendingValue, _pendingBuild,
                double.IsFinite(metrics.GpuRenderMicroseconds) ? metrics.GpuRenderMicroseconds : null,
                metrics.PresentationIntervalSeconds * 1000, metrics.RepaintBounds, metrics.DrawCalls,
                metrics.DynamicUploadBytes, metrics.StagingUploadBytes, _pendingFrame));
            _ordinal++;
            if (_ordinal == Warmup + Samples)
            {
                Console.WriteLine("PASS Linux slider diagnosis : " + Scenarios[_scenario]);
                _scenario++;
                _ordinal = 0;
                if (_scenario == 2)
                    _observer.FreezeDiagnosticsForMeasurement();
            }
            _pendingFrame = 0;
        }
        if (_scenario == Scenarios.Length)
        {
            _session.Exit();
            return;
        }
        if (_scenario > 0 && (!_sliderVisible || ui.GetTime() < _scrollRevealSettlesAt))
        {
            _observer.SelectedPage = "Fluent";
            if (_pageRendered && !_sliderVisible)
            {
                _sliderVisible = EnsureGallerySliderVisible(ui);
                if (_sliderVisible && _revealRequested)
                {
                    // ScrollIntoView wakes the default scrollbar. Its hold and fade use
                    // elapsed time, so the two runtimes cannot settle by frame count.
                    var motion = new UiScrollbarMotionOptions();
                    _scrollRevealSettlesAt = ui.GetTime() + motion.HoldSeconds + motion.Exit.DurationSeconds;
                }
            }
            if (!_sliderVisible || ui.GetTime() < _scrollRevealSettlesAt)
            {
                _observer.Render(ui);
                _pageRendered = true;
                ui.RequestFrameAt(ui.GetTime() + .001);
                return;
            }
        }
        var value = .1f + (_ordinal % 24) * .03f;
        var started = Stopwatch.GetTimestamp();
        if (_scenario == 0)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 8);
            _value.Value = value;
            _slider.Render(ui);
        }
        else
        {
            _observer.SelectedPage = "Fluent";
            _observer.ControlsValue = value;
            _observer.Render(ui);
        }
        _pendingBuild = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        _pendingValue = value;
        _pendingFrame = ui.GetDrawDataFrameId();
        ui.RequestFrameAt(ui.GetTime() + .001);
    }

    internal void Complete()
    {
        if (_frames.Count != Scenarios.Length * (Warmup + Samples))
            throw new InvalidOperationException("Slider diagnosis ended before all actual rendered frames.");
        foreach (var scenario in Scenarios)
            if (!_frames.Any(frame => frame.Scenario == scenario && frame.Sample >= 0 && frame.DrawCalls > 0))
                throw new InvalidOperationException("The measured slider never produced drawing commands: " + scenario);
        Write(true, null);
    }

    internal void Fail(Exception error) => Write(false, error.ToString());

    private void Write(bool completed, string? failure) => File.WriteAllText(Path.Combine(_output, "result.json"),
        JsonSerializer.Serialize(new LinuxSliderVerification(RuntimeInformation.FrameworkDescription,
            "Actual visible Fluent slider model sweeps and X11/Vulkan presentation; minimal/full/frozen-diagnostic controls. Not native pointer latency or a pixel equality comparison.",
            completed, failure, Warmup, Samples, _frames.ToArray()), LinuxSliderJson.Default.LinuxSliderVerification));

    private bool EnsureGallerySliderVisible(UiImmediateContext ui)
    {
        var tree = ui.AccessibilityTree ?? throw new InvalidOperationException("Gallery slider verification requires an accessibility tree.");
        var slider = tree.CaptureSnapshot().Elements.Single(element =>
            element.Node.Role == UiAccessibilityRole.Slider && element.Node.Name == "진행률");
        if (!slider.Node.Offscreen && slider.Node.Bounds is { Width: > 0, Height: > 0 })
            return true;
        if (!_revealRequested)
        {
            if (tree.RequestAction(slider.Node.Id, slider.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false)
                != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Could not reveal the Gallery slider before timing it.");
            _revealRequested = true;
        }
        return false;
    }
}

internal sealed record LinuxSliderFrame(string Scenario, int Sample, float Value, double BuildMilliseconds,
    double? GpuMicroseconds, double PresentationIntervalMilliseconds, UiRect RepaintBounds, int DrawCalls,
    ulong DynamicUploadBytes, ulong StagingUploadBytes, ulong DrawDataFrameId);
internal sealed record LinuxSliderVerification(string Framework, string Scope, bool Completed, string? Failure,
    int WarmupFrames, int MeasuredFramesPerScenario, LinuxSliderFrame[] Frames);
[JsonSerializable(typeof(LinuxSliderVerification))]
internal partial class LinuxSliderJson : JsonSerializerContext;
