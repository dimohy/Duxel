using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;
using System.Runtime.ExceptionServices;

internal static class EventLoopTests
{
    internal static void Run(Action<bool, string> check, string output)
    {
        var unsupportedRejected = false;
        try
        {
            new DuxelAppSession().RunCore(new()
            {
                Screen = new LoopScreen(new()),
                Frame = new() { UseEventDrivenPlatformLoop = true },
            }, new UnsupportedPlatform());
        }
        catch (NotSupportedException ex) when (ex.Message.Contains("requires IPlatformEventWakeBackend", StringComparison.Ordinal))
        { unsupportedRejected = true; }
        check(unsupportedRejected, "event-driven mode rejects an unsupported backend before reading platform state or creating GPU resources");
        RunSession(check, failSubmit: false, output);
        RunSession(check, failSubmit: true, output);
    }

    private static void RunSession(Action<bool, string> check, bool failSubmit, string output)
    {
        var profilePath = Path.Combine(output, failSubmit ? "event-loop-memory-off.log" : "event-loop-memory-on.log");
        string[] names = ["DUXEL_APP_PROFILE", "DUXEL_APP_PROFILE_EVERY", "DUXEL_APP_PROFILE_OUT", "DUXEL_APP_PROFILE_MEMORY"];
        var saved = names.Select(Environment.GetEnvironmentVariable).ToArray();
        Environment.SetEnvironmentVariable(names[0], "1");
        Environment.SetEnvironmentVariable(names[1], "1");
        Environment.SetEnvironmentVariable(names[2], profilePath);
        Environment.SetEnvironmentVariable(names[3], failSubmit ? "0" : "1");
        try
        {
            // WM_QUIT belongs to an owner thread's queue. Each real application
            // validation owns its window and queue independently of the GPU fixture.
            ExceptionDispatchInfo? error = null;
            var owner = new Thread(() =>
            {
                try { RunSessionOnOwner(check, failSubmit); }
                catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); }
            }) { IsBackground = true, Name = "DuxelEventLoopTestOwner" };
            owner.Start();
            if (!owner.Join(TimeSpan.FromSeconds(15))) throw new TimeoutException("Event-loop validation owner did not terminate.");
            error?.Throw();
            var lines = File.ReadAllLines(profilePath);
            check(lines.Length > 0 && lines.All(line => line.Contains(" gcHeap=", StringComparison.Ordinal) != failSubmit),
                failSubmit ? "disabled GC diagnostics leave application profile records unchanged"
                    : "enabled GC diagnostics are emitted by the actual application render worker");
            if (!failSubmit)
            {
                string[] fields = ["gcHeap", "gcCommitted", "gcLastHeap", "gcGen0", "gcGen1", "gcGen2", "gcAllocated"];
                check(lines.All(line => fields.All(field =>
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"\b" + field + @"=(\d+)");
                    return match.Success && long.TryParse(match.Groups[1].Value, out var value) && value >= 0;
                })), "application GC records contain nonnegative heap,commit,allocation and collection snapshots");
            }
        }
        finally
        {
            for (var i = 0; i < names.Length; i++) Environment.SetEnvironmentVariable(names[i], saved[i]);
        }
    }

    private static void RunSessionOnOwner(Action<bool, string> check, bool failSubmit)
    {
        var session = new DuxelAppSession();
        using var native = new WindowsPlatformBackend(new()
        {
            Width = 320, Height = 240, MinWidth = 100, MinHeight = 100,
            Title = "Duxel event-loop validation", TitleBarMode = DuxelTitleBarMode.System,
            VSync = false, Resizable = true, TrayOptions = new(),
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            FrameInvalidated = session.RequestFrame,
        });
        using var observed = new ObservedPlatform(native);
        using var finished = new ManualResetEventSlim();
        var state = new LoopState();
        var logs = new List<string>();
        var watchdogTriggered = false;
        var updatedFramePresented = false;
        var submitFailurePropagated = false;
        var controller = Task.Run(() =>
        {
            if (!observed.WaitAfterFirstPresent.Wait(TimeSpan.FromSeconds(5)))
            {
                watchdogTriggered = true;
                session.Exit();
                native.WakeEvents();
                return;
            }
            Volatile.Write(ref state.Mode, failSubmit ? 2 : 1);
            session.RequestFrame();
            if (!failSubmit)
            {
                var cursorApplied = observed.HandCursorApplied.Wait(TimeSpan.FromSeconds(2));
                updatedFramePresented = cursorApplied && SpinWait.SpinUntil(() =>
                {
                    var source = Volatile.Read(ref state.ChangedSourceId);
                    return source > 0 && state.Reader!.Latest.DrawDataFrameId >= (ulong)source;
                }, TimeSpan.FromSeconds(2));
                if (!updatedFramePresented) watchdogTriggered = true;
                session.Exit();
            }
            // This is test-only bounded cleanup. Any watchdog wake fails the test;
            // production event waits are interrupted only by messages or signals.
            if (!finished.Wait(TimeSpan.FromSeconds(3)))
            {
                watchdogTriggered = true;
                session.Exit();
                native.WakeEvents();
            }
        });
        try
        {
            session.RunCore(new()
            {
                Screen = new LoopScreen(state),
                Window = new() { Width = 320, Height = 240, TitleBarMode = DuxelTitleBarMode.System, VSync = false },
                Renderer = new() { MsaaSamples = 1, TextRendering = DuxelTextRenderingMode.Atlas },
                Font = new() { FastStartup = false, Oversample = 1, AtlasWidth = 256, AtlasHeight = 256 },
                Frame = new() { EnableRenderPipeline = true, UseEventDrivenPlatformLoop = true },
                Debug = new() { Log = logs.Add },
            }, observed);
        }
        catch (InvalidOperationException ex) when (failSubmit && ReferenceEquals(ex, state.SubmitFailure))
        {
            submitFailurePropagated = true;
        }
        finally { finished.Set(); }
        if (!controller.Wait(TimeSpan.FromSeconds(6))) throw new TimeoutException("Event-loop validation controller did not finish.");
        check(!watchdogTriggered && observed.WakeCount > 0 && observed.FirstPresented,
            failSubmit ? "renderer failure wakes and terminates an event-waiting application without watchdog intervention"
                : "explicit Exit interrupts the platform event wait and joins the application without another OS input message");
        if (failSubmit)
            check(submitFailurePropagated && logs.Any(line => line.Contains(state.SubmitFailure.Message, StringComparison.Ordinal)),
                "the original renderer callback failure propagates through the application error path");
        else
            check(updatedFramePresented && observed.HandCursorApplied.IsSet,
                "on-demand UI work presents while the platform sleeps and cursor changes wake its owner thread");
    }

    private sealed class LoopState
    {
        internal int Mode;
        internal long ChangedSourceId;
        internal IUiPresentationReader? Reader;
        internal readonly InvalidOperationException SubmitFailure = new("intentional event-loop submit failure");
    }

    private sealed class LoopScreen(LoopState state) : UiScreen
    {
        public override void Render(UiImmediateContext ui)
        {
            state.Reader ??= ui.GetPresentationReader();
            var mode = Volatile.Read(ref state.Mode);
            if (mode == 1)
            {
                ui.SetMouseCursor(UiMouseCursor.Hand);
                Interlocked.CompareExchange(ref state.ChangedSourceId, checked((long)ui.GetDrawDataFrameId()), 0);
            }
            if (mode == 2)
                ui.GetWindowDrawList().AddCallback((_, _) => throw state.SubmitFailure);
        }
    }

    private sealed class ObservedPlatform(WindowsPlatformBackend inner) : IPlatformBackend, IPlatformEventWakeBackend, IDisposable
    {
        internal readonly ManualResetEventSlim WaitAfterFirstPresent = new();
        internal readonly ManualResetEventSlim HandCursorApplied = new();
        private int _firstPresented;
        internal int WakeCount;
        internal bool FirstPresented => Volatile.Read(ref _firstPresented) != 0;
        public PlatformSize WindowSize => inner.WindowSize;
        public PlatformSize FramebufferSize => inner.FramebufferSize;
        public float ContentScale => inner.ContentScale;
        public bool IsInteractingResize => inner.IsInteractingResize;
        public bool ShouldClose => inner.ShouldClose;
        public double TimeSeconds => inner.TimeSeconds;
        public IInputBackend Input => inner.Input;
        public IVulkanSurfaceSource? VulkanSurface => inner.VulkanSurface;
        public IPlatformTextBackend? TextBackend => inner.TextBackend;
        public IUiImeHandler? ImeHandler => inner.ImeHandler;
        public long InteractiveResizeSequence => inner.InteractiveResizeSequence;
        public void PollEvents() => inner.PollEvents();
        public void WaitEvents(int timeoutMilliseconds)
        {
            if (FirstPresented) WaitAfterFirstPresent.Set();
            inner.WaitEvents(timeoutMilliseconds);
        }
        public void WakeEvents() { Interlocked.Increment(ref WakeCount); inner.WakeEvents(); }
        public void SetMouseCursor(UiMouseCursor cursor)
        {
            inner.SetMouseCursor(cursor);
            if (cursor == UiMouseCursor.Hand) HandCursorApplied.Set();
        }
        public void NotifyFirstFramePresented()
        {
            Volatile.Write(ref _firstPresented, 1);
            inner.NotifyFirstFramePresented();
        }
        public void NotifyFramePresented(long sequence) => inner.NotifyFramePresented(sequence);
        public void CancelInteractiveResizeWait() => inner.CancelInteractiveResizeWait();
        public void Dispose() { WaitAfterFirstPresent.Dispose(); HandCursorApplied.Dispose(); }
    }

    private sealed class UnsupportedPlatform : IPlatformBackend
    {
        private static T Unexpected<T>() => throw new InvalidOperationException("Unsupported platform must not be accessed.");
        public PlatformSize WindowSize => Unexpected<PlatformSize>();
        public PlatformSize FramebufferSize => Unexpected<PlatformSize>();
        public bool IsInteractingResize => Unexpected<bool>();
        public bool ShouldClose => Unexpected<bool>();
        public double TimeSeconds => Unexpected<double>();
        public IInputBackend Input => Unexpected<IInputBackend>();
        public IVulkanSurfaceSource? VulkanSurface => Unexpected<IVulkanSurfaceSource>();
        public IPlatformTextBackend? TextBackend => Unexpected<IPlatformTextBackend>();
        public void PollEvents() => throw new InvalidOperationException();
        public void WaitEvents(int timeoutMilliseconds) => throw new InvalidOperationException();
        public void SetMouseCursor(UiMouseCursor cursor) => throw new InvalidOperationException();
        public void Dispose() { }
    }
}
