using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class FrameSchedulingSessionTests
{
    internal static void Run(string mode, Action<bool, string> check, string output)
    {
        if (mode is not ("due" or "cancel" or "exit" or "caret" or "continuous" or "scheduled" or "hidden" or "hidden-continuous" or "hidden-scheduled" or "repaint"))
            throw new ArgumentException("Unknown scheduling session case.");
        var session = new DuxelAppSession();
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640,
            Height = 480,
            Title = "Duxel owned frame scheduling regression",
            UseDuxelTitleBar = false,
            VSync = false,
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new(),
            FrameInvalidated = session.RequestFrame
        });
        using var screen = new ScheduledScreen(mode);
        ExceptionDispatchInfo? failure = null;
        var controller = new Thread(() =>
        {
            try
            {
                void Wait(Func<bool> condition, string name)
                {
                    var clock = Stopwatch.StartNew();
                    while (!condition())
                    {
                        if (clock.Elapsed.TotalSeconds > 10)
                            throw new TimeoutException(name);
                        Thread.Sleep(5);
                    }
                }
                Wait(() => Volatile.Read(ref screen.Frames) > 0, "first scheduled frame");
                Thread.Sleep(300);
                if (mode is "continuous" or "scheduled" or "hidden-continuous" or "hidden-scheduled")
                {
                    var refreshFrame = Volatile.Read(ref screen.Frames);
                    session.RequestFrame();
                    Wait(() => Volatile.Read(ref screen.Frames) > refreshFrame, "refresh submitted renderer metrics");
                    Thread.Sleep(100);
                    using var process = Process.GetCurrentProcess();
                    process.Refresh();
                    var privateBefore = process.PrivateMemorySize64;
                    var workingBefore = process.WorkingSet64;
                    var cpuBefore = process.TotalProcessorTime;
                    var allocatedBefore = GC.GetTotalAllocatedBytes(false);
                    var gen0 = GC.CollectionCount(0);
                    var gen1 = GC.CollectionCount(1);
                    var gen2 = GC.CollectionCount(2);
                    var frameBefore = Volatile.Read(ref screen.Frames);
                    var sample = Stopwatch.StartNew();
                    Thread.Sleep(1000);
                    sample.Stop();
                    var frameAfter = Volatile.Read(ref screen.Frames);
                    var allocatedAfter = GC.GetTotalAllocatedBytes(false);
                    process.Refresh();
                    var measurement = new FrameSchedulingMeasurement(mode, sample.Elapsed.TotalMilliseconds,
                        (process.TotalProcessorTime - cpuBefore).TotalMilliseconds, allocatedAfter - allocatedBefore,
                        frameAfter - frameBefore, privateBefore, process.PrivateMemorySize64, workingBefore, process.WorkingSet64,
                        screen.Metrics.FramebufferSize.Width, screen.Metrics.FramebufferSize.Height,
                        screen.Metrics.MsaaSamples, screen.Metrics.VSyncEnabled,
                        GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2,
                        screen.Metrics.DrawCalls, screen.Metrics.DynamicUploadBytes, screen.Metrics.StagingUploadBytes);
                    File.WriteAllText(Path.Combine(output, "measurement.json"), JsonSerializer.Serialize(measurement, FrameSchedulingJson.Default.FrameSchedulingMeasurement));
                    check(Volatile.Read(ref screen.Visible) == 0, "comparison samples the same hidden Spinner without changing its visual load");
                    return;
                }
                if (mode == "repaint")
                {
                    Thread.Sleep(500);
                    var idleFrames = Volatile.Read(ref screen.Frames);
                    Thread.Sleep(350);
                    check(Volatile.Read(ref screen.Frames) == idleFrames,
                        "real pipelined app erases projected-scene repaint highlights and stops producing idle frames");
                    Volatile.Write(ref screen.Remove, 1);
                    session.RequestFrame();
                    Wait(() => Volatile.Read(ref screen.Frames) > idleFrames, "external repaint change wakes application");
                    check(true, "real pipelined event-wait app accepts a later content change after diagnostic expiry");
                    Thread.Sleep(500);
                    var changedFrames = Volatile.Read(ref screen.Frames);
                    Thread.Sleep(350);
                    check(Volatile.Read(ref screen.Frames) == changedFrames && changedFrames - idleFrames < 8,
                        "later repaint pulse also returns to idle instead of restarting its own diagnostic animation");
                    return;
                }
                var initial = Volatile.Read(ref screen.Frames);
                Thread.Sleep(300);
                check(Volatile.Read(ref screen.Frames) - initial < 5, "pending deadline does not continuously rebuild the real app");
                if (mode == "hidden")
                {
                    var hiddenFrames = Volatile.Read(ref screen.Frames);
                    Thread.Sleep(1000);
                    check(Volatile.Read(ref screen.Frames) == hiddenFrames, "offscreen cached animation waits without producing idle application frames");
                    Volatile.Write(ref screen.Onscreen, 1);
                    session.RequestFrame();
                    Wait(() => Volatile.Read(ref screen.Visible) != 0, "translated layer returns onscreen");
                    var visibleFrames = Volatile.Read(ref screen.Frames);
                    Wait(() => Volatile.Read(ref screen.Frames) > visibleFrames + 2, "returned layer animates");
                    check(true, "external layer movement resumes the retained Spinner animation");
                    Volatile.Write(ref screen.Transparent, 1);
                    session.RequestFrame();
                    Wait(() => Volatile.Read(ref screen.Visible) == 0, "layer becomes transparent");
                    Thread.Sleep(200);
                    var transparentFrames = Volatile.Read(ref screen.Frames);
                    Thread.Sleep(500);
                    check(Volatile.Read(ref screen.Frames) == transparentFrames, "transparent cached animation returns the app to event waiting");
                    Volatile.Write(ref screen.Transparent, 0);
                    session.RequestFrame();
                    Wait(() => Volatile.Read(ref screen.Visible) != 0, "opacity restores the animation");
                    check(true, "restoring opacity wakes and animates the same retained widget");
                }
                else if (mode == "caret")
                {
                    Wait(() => Volatile.Read(ref screen.Active) != 0, "text input is active");
                    var beforeCaret = Volatile.Read(ref screen.Frames);
                    Thread.Sleep(1000);
                    var caretFrames = Volatile.Read(ref screen.Frames) - beforeCaret;
                    check(caretFrames >= 1 && caretFrames <= 5 && Volatile.Read(ref screen.Visible) == 0, "caret blink wakes an active input before the later Spinner deadline without continuous redraw");
                }
                else if (mode == "due")
                {
                    Wait(() => Volatile.Read(ref screen.Visible) != 0, "scheduled Spinner becomes visible without external invalidation");
                    check(screen.FirstVisibleTime >= screen.Deadline, "real app does not render the Spinner before its UI-clock deadline");
                    var visibleFrame = Volatile.Read(ref screen.Frames);
                    Wait(() => Volatile.Read(ref screen.Frames) > visibleFrame + 2, "visible Spinner animates");
                    check(true, "deadline starts continuous visible animation after an idle wait");
                }
                else if (mode == "cancel")
                {
                    Volatile.Write(ref screen.Remove, 1);
                    session.RequestFrame();
                    Wait(() => Volatile.Read(ref screen.Removed) != 0, "external frame request interrupts the deadline wait");
                    check(Volatile.Read(ref screen.Visible) == 0, "external request removes the pending Spinner before it becomes visible");
                    Thread.Sleep(200);
                    var removedFrame = Volatile.Read(ref screen.Frames);
                    Thread.Sleep(2200);
                    check(Volatile.Read(ref screen.Frames) - removedFrame < 5, "removed Spinner does not leave a timer or deadline wake behind");
                }
                else
                    check(Volatile.Read(ref screen.Visible) == 0, "session exits while a future frame is still pending");
            }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            finally { session.Exit(); }
        })
        {
            IsBackground = true,
            Name = "OwnedFrameSchedulingController"
        };
        controller.Start();
        try
        {
            session.RunCore(new()
            {
                Screen = screen,
                Window = new()
                {
                    Width = 640,
                    Height = 480,
                    VSync = false,
                    UseDuxelTitleBar = false
                },
                Font = new()
                {
                    FastStartup = false,
                    SecondaryFontPath = null
                },
                Renderer = new()
                {
                    MaxFramesInFlight = 2,
                    MsaaSamples = 4
                },
                Frame = new()
                {
                    EnableIdleFrameSkip = true,
                    UseEventDrivenPlatformLoop = true,
                    EnableRenderPipeline = true
                }
            }, platform);
        }
        finally { session.Exit(); }
        if (!controller.Join(12000))
            throw new TimeoutException("scheduling controller exit");
        failure?.Throw();
        check(true, "session exit interrupts its wait and joins the render thread");
    }

    private sealed class ScheduledScreen(string mode) : UiScreen, IDisposable
    {
        private readonly UiFluentSpinner _spinner = Fluent.Spinner(options: new(DelaySeconds: mode.StartsWith("hidden", StringComparison.Ordinal) ? 0 : 2));
        private readonly UiFluentInput _input = Fluent.Input("Scheduled caret", new(() => string.Empty, _ => { }), 64, 180);
        internal int Frames, Visible, Remove, Removed, Active, Onscreen, Transparent;
        internal double Deadline, FirstVisibleTime;
        internal UiRendererFrameMetrics Metrics;
        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout();
            if (mode == "repaint")
            {
                ui.SetRepaintHighlights(true);
                ui.BeginLayer("owned-repaint-projected-scene", new UiLayerOptions(false, 1, default)
                {
                    Scene3D = new(System.Numerics.Matrix4x4.CreateRotationZ(.05f), false)
                });
                try
                {
                    var bounds = new UiRect(100, 100, 80, 50);
                    ui.GetWindowDrawList().AddRectFilled(bounds, Volatile.Read(ref Remove) == 0 ? new(255, 0, 0) : new(0, 0, 255), ui.WhiteTextureId, bounds);
                }
                finally { ui.EndLayer(); }
                Metrics = ui.GetRendererFrameMetrics();
                Interlocked.Increment(ref Frames);
                return;
            }
            if (Frames == 0)
                Deadline = ui.GetTime() + 2;
            if (mode.StartsWith("hidden", StringComparison.Ordinal))
            {
                var onscreen = Volatile.Read(ref Onscreen) != 0;
                var transparent = Volatile.Read(ref Transparent) != 0;
                var options = new UiLayerOptions(true, transparent ? 0 : 1, onscreen ? default : new(1000, 0)) { ContentVersion = 1 };
                if (ui.BeginLayer("owned-hidden-spinner", options))
                {
                    ui.SetCursorScreenPos(new(10, 10));
                    _spinner.Render(ui);
                }
                ui.EndLayer();
                if (mode == "hidden-continuous")
                    new UiControlContext(ui).RequestAnimation();
                Volatile.Write(ref Visible, onscreen && !transparent ? 1 : 0);
                Metrics = ui.GetRendererFrameMetrics();
                Interlocked.Increment(ref Frames);
                return;
            }
            if (mode == "caret")
            {
                _input.Render(ui);
                Volatile.Write(ref Active, ui.IsItemActive() ? 1 : 0);
                if (Frames == 0)
                {
                    ui.SetKeyboardFocusHere();
                    ui.RequestFrameAt(ui.GetTime() + .01);
                }
            }
            if (Volatile.Read(ref Remove) == 0)
            {
                _spinner.Render(ui);
                if (mode == "continuous" && ui.GetTime() < Deadline)
                    new UiControlContext(ui).RequestAnimation();
                if (ui.GetItemRectSize().Y > 0 && Volatile.Read(ref Visible) == 0)
                {
                    FirstVisibleTime = ui.GetTime();
                    Volatile.Write(ref Visible, 1);
                }
            }
            else
                Volatile.Write(ref Removed, 1);
            Metrics = ui.GetRendererFrameMetrics();
            Interlocked.Increment(ref Frames);
        }
        public void Dispose()
        {
            _spinner.Dispose();
            _input.Dispose();
        }
    }
}

internal sealed record FrameSchedulingMeasurement(string Mode, double Milliseconds, double CpuMilliseconds, long AllocatedBytes,
    int Frames, long PrivateBefore, long PrivateAfter, long WorkingBefore, long WorkingAfter, int Width, int Height, int Msaa, bool VSync,
    int Gen0, int Gen1, int Gen2, int LastDrawCalls, ulong LastDynamicUploadBytes, ulong LastStagingUploadBytes);
[JsonSerializable(typeof(FrameSchedulingMeasurement))]
internal partial class FrameSchedulingJson : JsonSerializerContext;
