using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.App;
using Duxel.Core;
using Duxel.Gallery;
using Duxel.Platform.Linux;

// Native events drive rendering; the worker never requests periodic probe frames.
internal sealed partial class ObserverLinuxIdleVerificationScreen : UiScreen, IDisposable
{
    private readonly ObserverScreen _observer;
    private readonly LinuxX11PlatformBackend _platform;
    private readonly DuxelAppSession _session;
    private readonly string _output;
    private readonly UiState<float> _value = new(.35f);
    private readonly UiFluentSlider _slider;
    private readonly UiState<double?> _spinValue = new(0);
    private readonly UiWidget _spin;
    private readonly Task _worker;
    private readonly List<IdlePointerSample> _samples = [];
    private readonly List<IdlePointerActivity> _activities = [];
    private TaskCompletionSource<UiRect> _ready = NewReady();
    private PointerCommand? _pending;
    private ExceptionDispatchInfo? _failure;
    private int _scenario, _renderedScenario = -1, _setupFrames, _renders;

    internal ObserverLinuxIdleVerificationScreen(ObserverScreen observer, LinuxX11PlatformBackend platform,
        DuxelAppSession session, string output, bool streamed = false)
    {
        _observer = observer;
        _platform = platform;
        _session = session;
        _output = Path.GetFullPath(output);
        if (Directory.Exists(_output))
            throw new IOException("Specify a fresh idle pointer result directory.");
        Directory.CreateDirectory(_output);
        _slider = Fluent.Slider("진행률", _value.Binding, 0, 1, .01f);
        _slider.Size = new(460, 0);
        _spin = Fluent.SpinButton("Repeat probe", _spinValue.Binding,
            new(Minimum: 0, Maximum: 100, IncrementLabel: "Probe increment", DecrementLabel: "Probe decrement"));
        _observer.SelectedPage = "Fluent";
        _streamed = streamed;
        _worker = Task.Run(streamed ? RunStream : Run);
    }

    private static TaskCompletionSource<UiRect> NewReady() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override void Render(UiImmediateContext ui)
    {
        var renderStart = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _renders);
        var scenario = Volatile.Read(ref _scenario);
        if (_renderedScenario != scenario)
        {
            _renderedScenario = scenario;
            _setupFrames = 0;
        }
        if (scenario == 0)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 8);
            _slider.Render(ui);
        }
        else if (scenario == 3)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 8);
            _spin.Render(ui);
        }
        else
        {
            _observer.FreezeDiagnosticText = scenario == 2;
            _observer.Render(ui);
        }
        ObserveStream(ui, renderStart);
        if (!_ready.Task.IsCompleted)
        {
            var sliders = _platform.AccessibilityTree.CaptureSnapshot().Elements
                .Where(x => scenario == 3 ? x.Node.Role == UiAccessibilityRole.Button && x.Node.Name == "Probe increment"
                    : x.Node.Role == UiAccessibilityRole.Slider && x.Node.Name == "진행률").ToArray();
            if (++_setupFrames >= 3 && sliders.Length == 1)
                _ready.TrySetResult(sliders[0].Node.Bounds);
            else
                ui.RequestFrameAt(ui.GetTime() + .01);
        }
        var command = Volatile.Read(ref _pending);
        if (command is not null && !command.Accepted.Task.IsCompleted)
            Volatile.Write(ref command.LastObservation, new(ui.GetMousePos(), ui.IsMouseDown(command.Button),
                ui.GetDrawDataFrameId(), _platform.PresentationInvalidationSequence, Volatile.Read(ref _scenario)));

        if (command is not null && ui.GetMousePos() == command.Point
            && ui.IsMouseDown(command.Button) == command.Down)
        {
            var value = scenario == 0 ? _value.Value : scenario == 3 ? (float)_spinValue.Value!.Value : _observer.ControlsValue;
            command.Accepted.TrySetResult(new(Stopwatch.GetElapsedTime(command.Sent).TotalMilliseconds, value));
        }
    }

    private async Task Run()
    {
        nint display = 0;
        try
        {
            display = XOpenDisplay(0);
            if (display == 0)
                throw new InvalidOperationException("Idle probe XOpenDisplay failed.");
            using var process = Process.GetCurrentProcess();
            for (var scenario = 0; scenario < 3; scenario++)
            {
                if (scenario != 0)
                {
                    _ready = NewReady();
                    Volatile.Write(ref _scenario, scenario);
                    _session.RequestFrame();
                }
                var bounds = await _ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var point = new UiVector2((int)(bounds.X + bounds.Width * .35f), (int)(bounds.Y + bounds.Height / 2));
                for (var button = 0; button < 3; button++)
                {
                    var press = await Send(display, 4, point, button, true, 0);
                    // Let finite hover/scrollbar transitions settle before counting idle work.
                    await Task.Delay(350);
                    var before = Volatile.Read(ref _renders);
                    var cpu = process.TotalProcessorTime;
                    var start = Stopwatch.GetTimestamp();
                    await Task.Delay(400);
                    var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    var cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
                    var heldRenders = Volatile.Read(ref _renders) - before;
                    var moved = point with
                    {
                        X = point.X + 60
                    };
                    var move = await Send(display, 6, moved, button, true, 1u << (8 + (int)NativeButton(button) - 1));
                    var release = await Send(display, 5, moved, button, false, 1u << (8 + (int)NativeButton(button) - 1));
                    _samples.Add(new(scenario, button, heldRenders, elapsed, cpuMs,
                        press.Milliseconds, move.Milliseconds, release.Milliseconds, press.Value, move.Value));
                    point = moved;
                }
            }

            async Task<UiRect> Prepare(int scenario)
            {
                _ready = NewReady();
                Volatile.Write(ref _scenario, scenario);
                _session.RequestFrame();
                return await _ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            var sliderBounds = await Prepare(0);
            var dragPoint = new UiVector2((int)(sliderBounds.X + sliderBounds.Width * .2f),
                (int)(sliderBounds.Y + sliderBounds.Height / 2));
            var dragPress = await Send(display, 4, dragPoint, 0, true, 0);
            await Task.Delay(350);
            var dragStart = Stopwatch.GetTimestamp();
            var dragCpu = process.TotalProcessorTime;
            var dragRenders = Volatile.Read(ref _renders);
            var dispatch = new List<double>();
            for (var step = 1; step <= 24; step++)
            {
                await Task.Delay(8);
                dragPoint = dragPoint with
                {
                    X = (int)(sliderBounds.X + sliderBounds.Width * (.2f + .025f * step))
                };
                dispatch.Add((await Send(display, 6, dragPoint, 0, true, 256)).Milliseconds);
            }
            var dragRelease = await Send(display, 5, dragPoint, 0, false, 256);
            _activities.Add(new("paced-drag", Volatile.Read(ref _renders) - dragRenders,
                Stopwatch.GetElapsedTime(dragStart).TotalMilliseconds, (process.TotalProcessorTime - dragCpu).TotalMilliseconds,
                dragPress.Value, dragRelease.Value, dispatch.ToArray()));

            var increment = await Prepare(3);
            var repeatPoint = new UiVector2((int)(increment.X + increment.Width / 2), (int)(increment.Y + increment.Height / 2));
            var repeatPress = await Send(display, 4, repeatPoint, 0, true, 0);
            var repeatStart = Stopwatch.GetTimestamp();
            var repeatCpu = process.TotalProcessorTime;
            var repeatRenders = Volatile.Read(ref _renders);
            await Task.Delay(850);
            var repeatRelease = await Send(display, 5, repeatPoint, 0, false, 256);
            _activities.Add(new("scheduled-spin-repeat", Volatile.Read(ref _renders) - repeatRenders,
                Stopwatch.GetElapsedTime(repeatStart).TotalMilliseconds, (process.TotalProcessorTime - repeatCpu).TotalMilliseconds,
                repeatPress.Value, repeatRelease.Value, [repeatPress.Milliseconds, repeatRelease.Milliseconds]));
            if (dragRelease.Value <= dragPress.Value || repeatRelease.Value < repeatPress.Value + 2)
                throw new InvalidOperationException("Native paced drag or scheduled spin repeat stopped updating its binding.");
        }
        catch (Exception error)
        {
            _failure = ExceptionDispatchInfo.Capture(error);
        }
        finally
        {
            if (display != 0)
                XCloseDisplay(display);
            _session.Exit();
        }
    }

    private async Task<PointerAcceptance> Send(nint display, int type, UiVector2 point, int button, bool down, uint state)
    {
        var command = new PointerCommand(point, button, down, Stopwatch.GetTimestamp());
        Volatile.Write(ref _pending, command);
        var value = new NativeEvent
        {
            Type = type,
            SendEvent = 1,
            Display = display,
            Window = _platform.WindowHandle,
            X = (int)point.X,
            Y = (int)point.Y,
            State = state,
            Detail = type == 6 ? 0 : NativeButton(button),
            SameScreen = 1
        };
        if (XSendEvent(display, _platform.WindowHandle, 0, type == 4 ? 4 : type == 5 ? 8 : 64, ref value) == 0)
            throw new InvalidOperationException("Idle probe XSendEvent failed.");
        XFlush(display);
        try
        {
            return await command.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException error)
        {
            try
            {
                DrainNativeObservation(display);
            }
            catch (Exception diagnosticError)
            {
                throw new AggregateException("Native pointer acknowledgement and native event diagnosis both failed.", error, diagnosticError);
            }
            throw new TimeoutException($"Native pointer acknowledgement timed out: type={type}, point={point}, button={button}, down={down}, "
                + $"scenario={Volatile.Read(ref _scenario)}, observation={Volatile.Read(ref command.LastObservation)}, "
                + $"nativeEvents=[{string.Join("; ", _nativeObservations)}]", error);
        }
    }

    private static uint NativeButton(int button) => button switch { 0 => 1, 1 => 3, 2 => 2, _ => throw new ArgumentOutOfRangeException(nameof(button)) };

    internal void Complete()
    {
        _worker.GetAwaiter().GetResult();
        if (_streamed)
        {
            CompleteStream();
            return;
        }
        File.WriteAllText(Path.Combine(_output, "result.json"), JsonSerializer.Serialize(
            new IdlePointerResult(_failure is null && _samples.Count == 9 && _activities.Count == 2, _failure?.SourceException.ToString(),
                "Owned XSendEvent; no periodic probe frame requests during holds. Native dispatch to UI processing, not physical display latency.",
                _samples.ToArray(), _activities.ToArray()), IdlePointerJson.Default.IdlePointerResult));
        _failure?.Throw();
        if (_samples.Count != 9 || _activities.Count != 2)
            throw new InvalidOperationException("Idle pointer probe is incomplete.");
    }

    public void Dispose()
    {
        _slider.Dispose();
        _spin.Dispose();
    }

    private sealed record PointerCommand(UiVector2 Point, int Button, bool Down, long Sent)
    {
        internal PointerObservation? LastObservation;
        internal TaskCompletionSource<PointerAcceptance> Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed record PointerObservation(UiVector2 Point, bool Down, ulong FrameId, long NativeInvalidationSequence, int Scenario);
    private sealed record PointerAcceptance(double Milliseconds, float Value);
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct NativeEvent
    {
        [FieldOffset(0)] internal int Type;
        [FieldOffset(8)] internal nuint Serial;
        [FieldOffset(40)] internal int FocusMode;
        [FieldOffset(44)] internal int FocusDetail;
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
    [LibraryImport("libX11.so.6")] private static partial int XSelectInput(nint display, nuint window, long mask);
    [LibraryImport("libX11.so.6")] private static partial int XSync(nint display, int discard);
    [LibraryImport("libX11.so.6")] private static partial int XPending(nint display);
    [LibraryImport("libX11.so.6")] private static partial int XNextEvent(nint display, out NativeEvent value);

    [LibraryImport("libX11.so.6")] private static partial int XSendEvent(nint display, nuint window, int propagate, long mask, ref NativeEvent value);
}
internal sealed record IdlePointerSample(int Scenario, int Button, int HeldRenders, double HeldMilliseconds, double CpuMilliseconds,
    double PressMilliseconds, double MoveMilliseconds, double ReleaseMilliseconds, float PressValue, float MoveValue);
internal sealed record IdlePointerActivity(string Name, int Renders, double Milliseconds, double CpuMilliseconds,
    float InitialValue, float FinalValue, double[] DispatchMilliseconds);
internal sealed record IdlePointerResult(bool Completed, string? Failure, string Scope, IdlePointerSample[] Samples, IdlePointerActivity[] Activities);
[JsonSerializable(typeof(IdlePointerResult))]
internal partial class IdlePointerJson : JsonSerializerContext;
