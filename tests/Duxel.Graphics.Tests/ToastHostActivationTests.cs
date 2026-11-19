using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static partial class WindowsImeOwnershipTests
{
    internal static void RunToastHostActivation(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 400, Height = 300, Title = "Duxel Toast activation validation",
            EnableOrderedInput = true, TrayOptions = new(),
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider()
        });
        SendMessageW(platform.WindowHandle, 0x0006, 1, 0);
        var active = AppInputSnapshots.Copy(platform.Input.Snapshot);
        check(!active.IsWindowInactive, "WM_ACTIVATE WA_ACTIVE publishes an active input snapshot");
        SendMessageW(platform.WindowHandle, 0x0006, 0, 0);
        var inactive = AppInputSnapshots.Copy(platform.Input.Snapshot);
        check(inactive.IsWindowInactive, "WM_ACTIVATE WA_INACTIVE publishes an inactive input snapshot");
        var pending = new AppInputSnapshots.PendingInput();
        pending.Add(active);
        pending.Add(inactive);
        check(pending.Drain().IsWindowInactive
            && AppInputSnapshots.Clear(inactive).IsWindowInactive,
            "App snapshot merge and transient clearing preserve latest native activation");
        var idleActive = AppInputSnapshots.Clear(active);
        var idleInactive = idleActive with { IsWindowInactive = true };
        check(AppInputSnapshots.HasInputDrivenInvalidation(idleInactive, idleActive)
            && AppInputSnapshots.HasInputDrivenInvalidation(idleActive, idleInactive),
            "App invalidation wakes for activation-only transitions in both directions");
        check(!AppInputSnapshots.HasInputDrivenInvalidation(idleInactive, idleInactive),
            "unchanged inactive state does not continuously invalidate the idle renderer");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark); ControlFontFixture.Configure(context);
        var clock = new ToastActivationClock(); var controller = new UiToastController(clock : clock);
        using var toaster = new UiToasterWidget(controller, new(ReducedMotion : true));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var observedActive = true;
        void Frame(InputSnapshot snapshot)
        {
            context.NewFrame(new UiFrameInfo(.016f, new(400, 300), new(1, 1))
                { IsWindowInactive = snapshot.IsWindowInactive }, idle, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => { observedActive = ui.IsHostWindowActive; toaster.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        controller.Dispatch(new("Native notification")); Frame(active);
        clock.Step(2); Frame(inactive);
        check(!observedActive && controller.NextTimeout is null,
            "native snapshot reaches UI host state and pauses Toast without changing toaster options");
        clock.Step(30); Frame(inactive);
        check(controller.Visible.Count == 1, "native inactive snapshot keeps notification alive beyond its original deadline");
        SendMessageW(platform.WindowHandle, 0x0006, 0x10002, 0);
        var clicked = AppInputSnapshots.Copy(platform.Input.Snapshot);
        check(!clicked.IsWindowInactive, "WA_CLICKACTIVE uses only the low word of WM_ACTIVATE");
        Frame(clicked);
        check(observedActive && controller.NextTimeout == TimeSpan.FromSeconds(5),
            "native reactivation resumes the precise remaining notification duration");
        clock.Step(5); Frame(clicked); Frame(clicked);
        check(controller.Visible.Count == 0, "reactivated native Toast expires normally after its retained interval");
        toaster.Options = toaster.Options with { ReducedMotion = false };
        controller.Dispatch(new("Native progress") { Timeout = null, Progress = new(null) });
        Frame(active); context.State.AdvanceTime(.4f); Frame(active);
        check(context.State.HasActiveAnimations, "active native host schedules indeterminate Toast progress motion");
        Frame(inactive);
        check(!context.State.HasActiveAnimations && toaster.Entries.Values.Single().Content.AnimationPaused,
            "native deactivation pauses hosted spinner motion and releases animation frames");
        Frame(active);
        check(context.State.HasActiveAnimations && !toaster.Entries.Values.Single().Content.AnimationPaused,
            "native reactivation resumes hosted progress motion");
    }
    private sealed class ToastActivationClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal void Step(int seconds) => _ticks += TimeSpan.FromSeconds(seconds).Ticks;
    }
}
