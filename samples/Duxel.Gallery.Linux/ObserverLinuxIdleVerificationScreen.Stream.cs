using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal sealed partial class ObserverLinuxIdleVerificationScreen
{
    private readonly bool _streamed;
    private readonly List<string> _nativeObservations = [];
    private readonly ConcurrentQueue<StreamPointerFrame> _streamFrames = new();
    private readonly List<StreamPointerPhase> _streamPhases = [];
    private StreamSeries? _streamSeries;

    private void ObserveStream(UiImmediateContext ui, long started)
    {
        var series = Volatile.Read(ref _streamSeries);
        var point = ui.GetMousePos();
        if (series is null || !ui.IsMouseDown(0) || !series.Sent.TryRemove((int)point.X, out var sent))
            return;
        var metrics = ui.GetRendererFrameMetrics();
        _streamFrames.Enqueue(new(series.Scenario, series.Round, (int)point.X,
            Stopwatch.GetElapsedTime(sent, started).TotalMilliseconds,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            ui.GetDrawDataFrameId(), metrics.DrawDataFrameId,
            metrics.PresentationIntervalSeconds * 1000,
            series.Scenario == 0 ? _value.Value : _observer.ControlsValue,
            metrics.PresentationSequence, metrics.PresentationReused, metrics.RepaintBounds));
    }

    private void DrainNativeObservation(nint display)
    {
        var count = 0;
        while (XPending(display) > 0)
        {
            if (++count > 1024)
                throw new InvalidOperationException("Native observation exceeded its1024-event diagnostic budget.");
            XNextEvent(display, out var observed);
            _nativeObservations.Add($"type={observed.Type},serial={observed.Serial},window={observed.Window}"
                + (observed.Type is 9 or 10 ? $",focusMode={observed.FocusMode},focusDetail={observed.FocusDetail}" : string.Empty));
        }
    }

    private async Task RunStream()
    {
        nint display = 0;
        try
        {
            display = XOpenDisplay(0);
            if (display == 0)
                throw new InvalidOperationException("Stream probe XOpenDisplay failed.");
            // Observe only this owned window's focus/structure notifications.
            // Selection never changes desktop focus or the pointer, and drains
            // occur outside the measured input interval.
            XSelectInput(display, _platform.WindowHandle, (1L << 21) | (1L << 17) | 16 | 32);
            XSync(display, 0);

            using var process = Process.GetCurrentProcess();
            for (var round = 0; round < 3; round++)
                for (var scenario = 0; scenario < 3; scenario++)
                {
                    if (round != 0 || scenario != 0)
                    {
                        _ready = NewReady();
                        Volatile.Write(ref _scenario, scenario);
                        _session.RequestFrame();
                    }
                    var bounds = await _ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    UiVector2 Point(int step, int count) => new(
                        (int)(bounds.X + bounds.Width * (.05f + .9f * step / count)),
                        (int)(bounds.Y + bounds.Height / 2));
                    var press = Point(0, 240);
                    await Send(display, 4, press, 0, true, 0);
                    // Warm the complete producer/renderer before measuring the independent input stream.
                    for (var step = 1; step <= 120; step++)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1d / 120));
                        SendStream(display, 6, Point(step, 120), 256);
                    }
                    await Task.Delay(100);
                    var series = new StreamSeries(scenario, round);
                    Volatile.Write(ref _streamSeries, series);
                    var cpu = process.TotalProcessorTime;
                    var start = Stopwatch.GetTimestamp();
                    var renders = Volatile.Read(ref _renders);
                    var frames = _streamFrames.Count;
                    var sends = new List<double>();
                    for (var step = 0; step < 240; step++)
                    {
                        var deadline = step / 120d;
                        var remaining = deadline - Stopwatch.GetElapsedTime(start).TotalSeconds;
                        if (remaining > 0)
                            await Task.Delay(TimeSpan.FromSeconds(remaining));
                        var point = Point(step, 240);
                        var sent = Stopwatch.GetTimestamp();
                        if (!series.Sent.TryAdd((int)point.X, sent))
                            throw new InvalidOperationException("Stream probe requires unique target coordinates.");
                        SendStream(display, 6, point, 256);
                        sends.Add(Stopwatch.GetElapsedTime(sent).TotalMilliseconds);
                    }
                    var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    var cpuMilliseconds = (process.TotalProcessorTime - cpu).TotalMilliseconds;
                    await Send(display, 6, Point(240, 240), 0, true, 256);
                    Volatile.Write(ref _streamSeries, null);
                    await Send(display, 5, Point(240, 240), 0, false, 256);
                    _streamPhases.Add(new(scenario, round, 240, _streamFrames.Count - frames,
                        Volatile.Read(ref _renders) - renders, elapsed, cpuMilliseconds, sends.ToArray()));
                }
        }
        catch (Exception error)
        {
            _failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error);
        }
        finally
        {
            if (display != 0)
            {
                try
                {
                    DrainNativeObservation(display);
                }
                catch (Exception error)
                {
                    _failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(
                        _failure is null ? error : new AggregateException(_failure.SourceException, error));
                }
                XCloseDisplay(display);
            }
            _session.Exit();
        }
    }

    private void SendStream(nint display, int type, UiVector2 point, uint state)
    {
        var value = new NativeEvent
        {
            Type = type,
            SendEvent = 1,
            Display = display,
            Window = _platform.WindowHandle,
            X = (int)point.X,
            Y = (int)point.Y,
            State = state,
            SameScreen = 1
        };
        if (XSendEvent(display, _platform.WindowHandle, 0, 64, ref value) == 0)
            throw new InvalidOperationException("Stream probe XSendEvent failed.");
        XFlush(display);
    }

    private void CompleteStream()
    {
        if (_failure is null && (_streamPhases.Count != 9 || _streamPhases.Any(phase =>
        {
            var values = _streamFrames.Where(frame => frame.Scenario == phase.Scenario && frame.Round == phase.Round)
                .Select(frame => frame.Value).ToArray();
            return values.Length == 0 || values.Min() >= .2f || values.Max() <= .8f
                || values.Distinct().Count() < 20;
        })))
            _failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(
                new InvalidOperationException("Independent native stream did not traverse the real slider binding in every phase."));

        File.WriteAllText(Path.Combine(_output, "result.json"), JsonSerializer.Serialize(
            new StreamPointerResult(_failure is null && _streamPhases.Count == 9,
                _failure?.SourceException.ToString(),
                "Independent 120Hz XSendEvent stream to owned window; producer acknowledgement and previous actual-presentation/reuse metrics. Not physical pointer/compositor latency.",
                _streamPhases.ToArray(), _streamFrames.ToArray(), _nativeObservations.ToArray()), StreamPointerJson.Default.StreamPointerResult));
        _failure?.Throw();
        if (_streamPhases.Count != 9)
            throw new InvalidOperationException("Stream pointer probe is incomplete.");
    }

    private sealed record StreamSeries(int Scenario, int Round)
    {
        internal ConcurrentDictionary<int, long> Sent { get; } = new();
    }
}

internal sealed record StreamPointerFrame(int Scenario, int Round, int X, double DispatchMilliseconds,
    double BuildMilliseconds, ulong FrameId, ulong PreviousProcessedFrameId, double PreviousPresentationMilliseconds, float Value,
    ulong PreviousPresentationSequence, bool PreviousPresentationReused, UiRect PreviousRepaintBounds);
internal sealed record StreamPointerPhase(int Scenario, int Round, int Sent, int Observed, int Renders,
    double Milliseconds, double CpuMilliseconds, double[] SendMilliseconds);
internal sealed record StreamPointerResult(bool Completed, string? Failure, string Scope,
    StreamPointerPhase[] Phases, StreamPointerFrame[] Frames, string[] NativeObservations);
[JsonSerializable(typeof(StreamPointerResult))]
internal partial class StreamPointerJson : JsonSerializerContext;
