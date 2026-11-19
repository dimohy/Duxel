using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;

// Actual HWND/provider/event delivery with deterministic UI input; GPU fidelity is qualified separately.
internal static partial class TooltipEventsHost
{
    internal static void RunHostVisibility(Action<bool, string> check, string? coordination = null, bool shutdown = false)
    {
        const int hide = 0;
        const int showWithoutActivation = 4;
        const int minimizeWithoutActivation = 7;
        const int restore = 9;
        var session = new DuxelAppSession();
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 480, Title = "Duxel owned Tooltip visibility fixture",
            UseDuxelTitleBar = false, VSync = false, KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new(), FrameInvalidated = session.RequestFrame
        });
        using var tooltip = Fluent.Tooltip(Fluent.Button("Target"), "Visible help");
        tooltip.Visible = coordination is null;
        var sessionDismiss = !shutdown && coordination is not null
            && Environment.GetEnvironmentVariable("DUXEL_TEST_TOOLTIP_CLOSE_ACTION") == "session-dismiss";
        var begin = 0;
        var dismissVisible = 0;
        var began = false;
        var requests = 0;
        tooltip.VisibleChangeRequested = change =>
        {
            if (change is { Visible : false, Reason : UiTooltipVisibilityReason.HostHidden })
            {
                if (Interlocked.Increment(ref requests) == 2)
                {
                    tooltip.Visible = false;
                }
            }
        };
        var opened = 0;
        var closed = 0;
        var minimizedInputPasses = 0;
        var shownAfterDismissal = 0;
        var firstFramePresented = 0;
        var shutdownMode = Environment.GetEnvironmentVariable("DUXEL_TEST_OBSERVE_CLOSE_BEFORE_TEARDOWN");
        var pumpShutdown = shutdownMode is "worker-pump" or "worker";
        var shutdownObservation = shutdown && coordination is not null
            && (shutdownMode is "1" or "worker-pump")
            ? Path.Combine(coordination, "shutdown-observed")
            : null;
        platform.AccessibilityTree.AutomationEvent += notification =>
        {
            if (notification.Kind == UiAccessibilityEventKind.TooltipOpened)
            {
                Interlocked.Increment(ref opened);
            }
            else if (notification.Kind == UiAccessibilityEventKind.TooltipClosed)
            {
                Interlocked.Increment(ref closed);
                if (shutdownObservation is not null)
                {
                    // This fixture keeps the publication alive while the separate UIA process reads its event cache.
                    var observed = SpinWait.SpinUntil(() => File.Exists(shutdownObservation), TimeSpan.FromSeconds(3));
                    check(observed && IsWindow(platform.WindowHandle) && !platform.AccessibilityTree.IsDisposed,
                        "external client receives the shutdown close while its window and tree remain live");
                }
            }
        };
        var screen = new HostScreen(ui =>
        {
            var extent = platform.FramebufferSize;
            if (platform.HostVisibility.IsHidden && (extent.Width == 0 || extent.Height == 0)
                && ui.RenderPhase == UiRenderPhase.Input)
            {
                Interlocked.Increment(ref minimizedInputPasses);
            }
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(20, 60));
            if (!began && Volatile.Read(ref begin) != 0)
            {
                tooltip.Visible = true;
                began = true;
            }
            if (Interlocked.Exchange(ref dismissVisible, 0) != 0)
                tooltip.Visible = false;
            tooltip.Render(ui);
            if (Volatile.Read(ref requests) == 2 && !platform.HostVisibility.IsHidden)
            {
                Interlocked.Increment(ref shownAfterDismissal);
            }
        });
        ExceptionDispatchInfo? failure = null;
        var controller = new Thread(() =>
        {
            try
            {
                void Wait(Func<bool> predicate, string label)
                {
                    var clock = Stopwatch.StartNew();
                    while (!predicate())
                    {
                        if (clock.Elapsed.TotalSeconds > 8)
                        {
                            throw new TimeoutException(label);
                        }
                        Thread.Sleep(5);
                    }
                }
                void Show(int command)
                {
                    if (!ShowWindowAsync(platform.WindowHandle, command))
                    {
                        throw new InvalidOperationException("Could not queue the owned fixture window transition.");
                    }
                }
                void WaitForClient(string marker)
                {
                    if (coordination is not null)
                    {
                        Wait(() => File.Exists(Path.Combine(coordination, marker)), "UIA client marker " + marker);
                    }
                }
                Wait(() => Volatile.Read(ref firstFramePresented) != 0, "initial application presentation");
                // The runner can supply a hidden STARTUPINFO for the first ShowWindow call.
                Show(showWithoutActivation);
                if (coordination is not null)
                {
                    Wait(() => !platform.HostVisibility.IsHidden && platform.AccessibilityTree.Root.First is not null,
                        "visible fixture is ready for UIA registration");
                    File.WriteAllText(Path.Combine(coordination, "host-ready"), string.Empty);
                    WaitForClient("subscribed");
                    Volatile.Write(ref begin, 1);
                    session.RequestFrame();
                }
                Wait(() => Volatile.Read(ref opened) == 1, "initial visible tooltip publication");
                var baseline = platform.HostVisibility.HideSequence;
                check(!platform.HostVisibility.IsHidden && Volatile.Read(ref requests) == 0,
                    "first visible frame publishes one tooltip without a hide request");
                WaitForClient("first-open");
                if (shutdown)
                {
                    platform.CloseWindow();
                    return;
                }
                if (sessionDismiss)
                {
                    Volatile.Write(ref dismissVisible, 1);
                    session.RequestFrame();
                    Wait(() => Volatile.Read(ref closed) == 1, "application-session tooltip dismissal");
                    WaitForClient("dismissed");
                    check(!platform.HostVisibility.IsHidden && IsWindow(platform.WindowHandle)
                        && Volatile.Read(ref opened) == 1 && Volatile.Read(ref closed) == 1,
                        "ordinary dismissal publishes one close with the application window still visible");
                    return;
                }
                Show(minimizeWithoutActivation);
                Wait(() => Volatile.Read(ref closed) == 1, "minimized tooltip removal");
                check(platform.HostVisibility == new UiHostVisibility(true, baseline + 1)
                    && Volatile.Read(ref requests) == 1 && tooltip.Visible == true,
                    "minimize wakes the idle session and publishes a close after a declined request");
                WaitForClient("minimized");
                Show(restore);
                Wait(() => Volatile.Read(ref opened) == 2, "restored tooltip publication");
                check(platform.HostVisibility == new UiHostVisibility(false, baseline + 1) && Volatile.Read(ref requests) == 1,
                    "restore republishes the retained controlled true value");
                check(Volatile.Read(ref minimizedInputPasses) == 1,
                    "a zero-extent minimize processes one input-only visibility pass before restore");
                WaitForClient("restored");
                Show(hide);
                Wait(() => Volatile.Read(ref closed) == 2, "hidden tooltip removal");
                check(platform.HostVisibility == new UiHostVisibility(true, baseline + 2)
                    && Volatile.Read(ref requests) == 2 && tooltip.Visible == false,
                    "hiding the HWND emits one close and applies the accepted request");
                WaitForClient("hidden");
                Show(showWithoutActivation);
                Wait(() => Volatile.Read(ref shownAfterDismissal) >= 1, "render after accepted dismissal and show");
                session.RequestFrame();
                Wait(() => Volatile.Read(ref shownAfterDismissal) >= 2, "publication after accepted dismissal and show");
                check(Volatile.Read(ref opened) == 2 && Volatile.Read(ref closed) == 2,
                    "showing the HWND again preserves the accepted dismissal");
                if (coordination is not null)
                {
                    File.WriteAllText(Path.Combine(coordination, "host-restored"), string.Empty);
                    WaitForClient("client-done");
                }
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                session.Exit();
            }
        }) { IsBackground = true, Name = "TooltipHostVisibilityFixture" };
        controller.Start();
        try
        {
            session.RunCore(new()
            {
                Screen = screen, Window = new() { Width = 640, Height = 480, VSync = false, UseDuxelTitleBar = false },
                Font = new() { FastStartup = false, SecondaryFontPath = null },
                Frame = new() { EnableIdleFrameSkip = true, UseEventDrivenPlatformLoop = true },
                Debug = new()
                {
                    LogStartupTimings = true,
                    Log = message =>
                    {
                        if (message.StartsWith("StartupTiming[FirstFramePresented]", StringComparison.Ordinal))
                        {
                            Volatile.Write(ref firstFramePresented, 1);
                        }
                    }
                }
            }, platform);
        }
        finally
        {
            session.Exit();
            if (!controller.Join(10000))
            {
                throw new TimeoutException("The visibility test controller did not finish.");
            }
        }
        failure?.Throw();
        if (shutdown && coordination is not null)
        {
            var window = platform.WindowHandle;
            check(platform.ShouldClose && IsWindow(window), "the close request stops the session while its HWND is still valid");
            if (pumpShutdown)
            {
                ExceptionDispatchInfo? shutdownFailure = null;
                var shutdownWorker = new Thread(() =>
                {
                    try
                    {
                        // Keep the HWND owner's message loop available while UIA builds its external event cache.
                        platform.AccessibilityTree.Dispose();
                    }
                    catch (Exception exception)
                    {
                        shutdownFailure = ExceptionDispatchInfo.Capture(exception);
                    }
                }) { IsBackground = true, Name = "TooltipShutdownWorkerFixture" };
                shutdownWorker.Start();
                while (!shutdownWorker.Join(1))
                {
                    platform.PollEvents();
                }
                shutdownFailure?.Throw();
            }
            platform.Dispose();
            check(Volatile.Read(ref opened) == 1 && Volatile.Read(ref closed) == 1 && platform.AccessibilityTree.IsDisposed,
                "closing the window publishes one TooltipClosed before disposing its tree");
            check(!IsWindow(window), "backend disposal destroys the requested HWND");
            File.WriteAllText(Path.Combine(coordination, "host-disposed"), string.Empty);
            var deadline = Environment.TickCount64 + 7000;
            while (!File.Exists(Path.Combine(coordination, "client-done")))
            {
                if (Environment.TickCount64 >= deadline)
                {
                    throw new TimeoutException("The external shutdown observer did not finish.");
                }
                Thread.Sleep(10);
            }
        }
    }

    [LibraryImport("user32.dll")]
    [return : MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindowAsync(nint hwnd, int command);

    [LibraryImport("user32.dll")]
    [return : MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint hwnd);

    internal static void Run(Action<bool, string> check, bool textProperties = false, bool scrollPattern = false)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 480, Title = "Duxel owned tooltip event fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        using var tooltip = TooltipAccessibilityTests.Wrap(Fluent.Button("Tooltip event target"), "Event help");
        var closeAction = Environment.GetEnvironmentVariable("DUXEL_TEST_TOOLTIP_CLOSE_ACTION");
        if (textProperties || scrollPattern || closeAction == "dismiss")
        {
            tooltip.Visible = false;
            tooltip.VisibleChangeRequested = change =>
            {
                if (change.Visible)
                {
                    tooltip.Visible = true;
                }
            };
        }
        if (scrollPattern)
        {
            tooltip.Options = tooltip.Options with { ShowScrollbars = true, ScrollStep = 16, Positioning = new() { RightToLeft = true } };
        }
        using var move = Fluent.Button("Move focus");
        var show = true;
        using var remove = Fluent.Button("Remove target", () => show = false);
        using var dismiss = Fluent.Button("Dismiss tooltip", () => tooltip.Visible = false);
        var finish = false;
        using var done = Fluent.Button("Finish", () => finish = true);
        using var changeText = Fluent.Button("Change tooltip text", () => tooltip.Text = scrollPattern ? "Fit" : "Updated event help");
        var opened = 0;
        var closed = 0;
        platform.AccessibilityTree.AutomationEvent += notification =>
        {
            Console.WriteLine("CoreTooltipEvent : " + notification.Kind);
            if (notification.Kind == UiAccessibilityEventKind.TooltipOpened)
            {
                opened++;
            }
            else
            {
                closed++;
            }
        };
        var screen = new HostScreen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(20, 60));
            if (show)
            {
                tooltip.Render(ui);
            }
            ui.SetCursorScreenPos(new(20, 120));
            move.Render(ui);
            ui.SetCursorScreenPos(new(20, 180));
            remove.Render(ui);
            ui.SetCursorScreenPos(new(20, 240));
            done.Render(ui);
            if (textProperties || scrollPattern)
            {
                ui.SetCursorScreenPos(new(20, 300));
                changeText.Render(ui);
            }
            else if (closeAction == "dismiss")
            {
                ui.SetCursorScreenPos(new(20, 300));
                dismiss.Render(ui);
            }
        });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var separatePublisher = Environment.GetEnvironmentVariable("DUXEL_TEST_TOOLTIP_WORKER") == "1";
        // This fixture does not submit a Vulkan frame, so there is no first-present notification to show its HWND.
        platform.NotifyFirstFramePresented();
        platform.PollEvents();
        // A hidden STARTUPINFO can override the first ShowWindow call.
        const int showWithoutActivation = 4;
        if (platform.HostVisibility.IsHidden && !ShowWindowAsync(platform.WindowHandle, showWithoutActivation))
            throw new InvalidOperationException("Could not show the owned tooltip event fixture.");
        void PublishFrames()
        {
            var clock = Stopwatch.StartNew();
            var previous = clock.Elapsed.TotalSeconds;
            var ready = false;
            Console.WriteLine($"Tooltip publication : {(separatePublisher ? "worker" : "window thread")}");
            while (!finish && clock.Elapsed.TotalSeconds < 20 && !platform.ShouldClose)
            {
                if (!separatePublisher)
                    platform.PollEvents();
                var now = clock.Elapsed.TotalSeconds;
                context.NewFrame(new((float)(now - previous), new(640, 480), new(1, 1)), idle,
                    new(0, 0, 640, 480), UiTextSettings.Default);
                previous = now;
                context.Render(screen);
                using var draw = context.GetDrawData();
                if (!ready && !platform.HostVisibility.IsHidden)
                {
                    ready = true;
                    Console.WriteLine("UIA_TOOLTIP_HOST_READY " + platform.WindowHandle);
                    Console.Out.Flush();
                }
                Thread.Sleep(1);
            }
        }
        if (separatePublisher)
        {
            ExceptionDispatchInfo? publicationFailure = null;
            var publisher = new Thread(() =>
            {
                try
                {
                    PublishFrames();
                }
                catch (Exception exception)
                {
                    publicationFailure = ExceptionDispatchInfo.Capture(exception);
                }
            }) { IsBackground = true, Name = "TooltipFixturePublisher" };
            publisher.Start();
            while (!publisher.Join(1))
                platform.PollEvents();
            publicationFailure?.Throw();
        }
        else
        {
            PublishFrames();
        }
        platform.PollEvents();
        Console.WriteLine($"EventHostCounts : opened = {opened}, closed = {closed}, owner = {tooltip.IsOpen}");
        check(finish, "external client completes the owned tooltip event fixture");
        if (scrollPattern)
        {
            check(opened == 1 && closed == 0 && tooltip.IsOpen && tooltip.Text == "Fit"
                && TooltipAccessibilityTests.Surface.LastScrollOffset == default,
                "content shrink removes overflow without reopening the tooltip");
        }
        else if (textProperties)
        {
            check(opened == 1 && closed == 0 && tooltip.IsOpen && tooltip.Text == "Updated event help",
                "changing visible tooltip text preserves its open lifecycle");
        }
        else
        {
            var cycles = closeAction is null ? 2 : 1;
            check(opened == cycles && closed == cycles, "each requested tooltip lifecycle produces one managed open and close");
            check(!tooltip.IsOpen, "dismissal leaves no visible tooltip owner");
        }
    }
    private sealed class HostScreen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
