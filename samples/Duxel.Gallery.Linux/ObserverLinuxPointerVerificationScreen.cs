using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.App;
using Duxel.Core;
using Duxel.Gallery;
using Duxel.Platform.Linux;

// Sends native X11 events exclusively to the finite probe's own window. Does not
// move the desktop pointer, focus another window or bypass the platform input code.
internal sealed partial class ObserverLinuxPointerVerificationScreen : UiScreen, IDisposable
{
    private static readonly string[] Scenarios = ["MinimalSlider", "ObserverFluent", "ObserverFluentFrozenDiagnostics"];
    private const int Warmup = 96, Samples = 32;
    private readonly ObserverScreen _observer;
    private readonly LinuxX11PlatformBackend _platform;
    private readonly DuxelAppSession _session;
    private readonly string _output;
    private readonly UiState<float> _value = new(.35f);
    private readonly UiFluentSlider _slider;
    private readonly List<LinuxPointerFrame> _frames = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly nint _display;
    private int _scenario, _ordinal;
    private bool _pressed;
    private long _sent;
    private UiVector2 _target;
    private ulong _pendingFrame;
    private double _dispatch, _build;
    private float _observed;
    private int _settleFrames;
    private double _sendMilliseconds;

    internal ObserverLinuxPointerVerificationScreen(ObserverScreen observer, LinuxX11PlatformBackend platform,
        DuxelAppSession session, string output)
    {
        _observer = observer;
        _platform = platform;
        _session = session;
        _output = Path.GetFullPath(output);
        if (Directory.Exists(_output))
            throw new IOException("Specify a fresh pointer verification directory.");
        Directory.CreateDirectory(_output);
        _display = XOpenDisplay(0);
        if (_display == 0)
            throw new InvalidOperationException("Probe XOpenDisplay failed.");
        _slider = Fluent.Slider("진행률", _value.Binding, 0, 1, .01f);
        _slider.Size = new(460, 0);
        _observer.SelectedPage = "Fluent";
        Write(false, null);
    }

    public override void Render(UiImmediateContext ui)
    {
        if (_clock.Elapsed.TotalSeconds > 90)
            throw new TimeoutException("Native pointer probe exceeded90 seconds.");
        var metrics = ui.GetRendererFrameMetrics();
        if (_pendingFrame != 0 && metrics.DrawDataFrameId == _pendingFrame)
        {
            _frames.Add(new(Scenarios[_scenario], _ordinal - Warmup, _target, _observed,
                _dispatch, _build, _sendMilliseconds, Stopwatch.GetElapsedTime(_sent).TotalMilliseconds,
                metrics.PresentationIntervalSeconds * 1000,
                double.IsFinite(metrics.GpuRenderMicroseconds) ? metrics.GpuRenderMicroseconds : null,
                metrics.RepaintBounds));
            _pendingFrame = 0;
            _sent = 0;
            _ordinal++;
            if (_ordinal == Warmup + Samples)
            {
                Send(5, _target, 256);
                _pressed = false;
                Console.WriteLine("PASS native pointer : " + Scenarios[_scenario]);
                _scenario++;
                _ordinal = 0;
                _settleFrames = 0;
            }
        }
        if (_scenario == Scenarios.Length)
        {
            _session.Exit();
            return;
        }
        var received = _sent != 0 && _pendingFrame == 0 && ui.GetMousePos() == _target && ui.IsMouseDown(0);
        if (received)
            _dispatch = Stopwatch.GetElapsedTime(_sent).TotalMilliseconds;
        var start = Stopwatch.GetTimestamp();
        if (_scenario == 0)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 8);
            _slider.Render(ui);
        }
        else
        {
            _observer.FreezeDiagnosticText = _scenario == 2;
            _observer.Render(ui);
        }
        if (received)
        {
            _build = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            _observed = _scenario == 0 ? _value.Value : _observer.ControlsValue;
            _pendingFrame = ui.GetDrawDataFrameId();
        }
        if (!_pressed && !ui.IsMouseDown(0))
            _settleFrames++;
        if (_sent == 0 && (_pressed || _settleFrames >= 3))
        {
            var snapshot = _platform.AccessibilityTree.CaptureSnapshot();
            var sliders = snapshot.Elements.Where(x => x.Node.Role == UiAccessibilityRole.Slider && x.Node.Name == "진행률").ToArray();
            if (sliders.Length == 1)
            {
                var bounds = sliders[0].Node.Bounds;
                // Stay inside the real slider's thumb travel, away from label/hit padding.
                var fraction = .15f + (_ordinal % 24) * .025f;
                _target = new((int)(bounds.X + 20 + (bounds.Width - 40) * fraction), (int)(bounds.Y + bounds.Height / 2));
                _sent = Stopwatch.GetTimestamp();
                Send(_pressed ? 6 : 4, _target, _pressed ? 256u : 0u);
                _sendMilliseconds = Stopwatch.GetElapsedTime(_sent).TotalMilliseconds;
                _pressed = true;
            }
        }
        ui.RequestFrameAt(ui.GetTime() + .002);
    }

    private void Send(int type, UiVector2 point, uint state)
    {
        var value = new NativeEvent
        {
            Type = type,
            SendEvent = 1,
            Display = _display,
            Window = _platform.WindowHandle,
            X = (int)point.X,
            Y = (int)point.Y,
            State = state,
            Detail = type == 6 ? 0u : 1u,
            SameScreen = 1
        };
        var mask = type == 4 ? 4L : type == 5 ? 8L : 64L;
        if (XSendEvent(_display, _platform.WindowHandle, 0, mask, ref value) == 0)
            throw new InvalidOperationException("Probe XSendEvent failed.");
        XFlush(_display);
    }
    internal void Complete()
    {
        if (_frames.Count != Scenarios.Length * (Warmup + Samples))
            throw new InvalidOperationException("Pointer probe incomplete.");
        foreach (var group in _frames.GroupBy(x => x.Scenario))
            if (group.Select(x => x.Value).Distinct().Count() < 20)
                throw new InvalidOperationException("Native drag did not change the real slider binding.");
        Write(true, null);
    }
    internal void Fail(Exception error) => Write(false, error.ToString());
    private void Write(bool complete, string? error) => File.WriteAllText(Path.Combine(_output, "result.json"),
        JsonSerializer.Serialize(new LinuxPointerResult(complete, error,
            "Targeted XSendEvent through X11 poll/input and actual Fluent pointer hit testing; queue-presentation acknowledgement includes2ms probe scheduling. Not physical pointer or compositor/display latency.", _frames.ToArray()), LinuxPointerJson.Default.LinuxPointerResult));
    public void Dispose()
    {
        _slider.Dispose();
        XCloseDisplay(_display);
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct NativeEvent
    {
        [FieldOffset(0)] internal int Type;
        [FieldOffset(16)] internal int SendEvent;
        [FieldOffset(24)] internal nint Display;
        [FieldOffset(32)] internal nuint Window;
        [FieldOffset(64)] internal int X;
        [FieldOffset(68)] internal int Y;
        [FieldOffset(80)] internal uint State;
        [FieldOffset(84)] internal uint Detail;
        [FieldOffset(88)] internal int SameScreen;
    }
    [LibraryImport("libX11.so.6")] private static partial nint XOpenDisplay(nint name);
    [LibraryImport("libX11.so.6")] private static partial int XCloseDisplay(nint display);
    [LibraryImport("libX11.so.6")] private static partial int XFlush(nint display);
    [LibraryImport("libX11.so.6")] private static partial int XSendEvent(nint display, nuint window, int propagate, long mask, ref NativeEvent value);
}
internal sealed record LinuxPointerFrame(string Scenario, int Sample, UiVector2 Target, float Value,
    double DispatchMilliseconds, double BuildMilliseconds, double SendMilliseconds, double QueueAcknowledgedMilliseconds,
    double PresentationIntervalMilliseconds, double? GpuMicroseconds, UiRect RepaintBounds);
internal sealed record LinuxPointerResult(bool Completed, string? Failure, string Scope, LinuxPointerFrame[] Frames);
[JsonSerializable(typeof(LinuxPointerResult))]
internal partial class LinuxPointerJson : JsonSerializerContext;
