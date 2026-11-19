using Duxel.Core;

internal static class ToastHostOwnershipTests
{
    internal static void Run(Action<bool, string> check)
    {
        var controller = new UiToastController();
        using var first = new UiToasterWidget(controller);
        controller.SetViewportCapacity(1);
        controller.Dispatch(new("Visible"));
        controller.Dispatch(new("Queued"));
        var visible = controller.Visible;
        var queued = controller.Queued;
        var duplicateRejected = false;
        try { using var duplicate = new UiToasterWidget(controller); }
        catch (InvalidOperationException) { duplicateRejected = true; }
        check(duplicateRejected && ReferenceEquals(visible, controller.Visible) && ReferenceEquals(queued, controller.Queued),
            "duplicate toaster host fails before changing controller snapshots or capacity");
        first.Dispose(); first.Dispose();
        check(controller.Visible.Count == 2 && controller.Queued.Count == 0,
            "idempotent host disposal releases its lease and restores caller-owned capacity");
        using (var replacement = new UiToasterWidget(controller))
        {
            check(controller.Visible.Count == 2, "a disposed host can be replaced without losing notifications");
        }
        var invalidController = new UiToastController();
        var invalidRejected = false;
        try { using var invalid = new UiToasterWidget(invalidController, new(Width : 0)); }
        catch (ArgumentOutOfRangeException) { invalidRejected = true; }
        using (var valid = new UiToasterWidget(invalidController))
            check(invalidRejected, "invalid host options are rejected without leaking a controller lease");

        var callbackController = new UiToastController();
        UiToasterWidget? callbackHost = new(callbackController);
        callbackController.SetViewportCapacity(0);
        var notifications = 0;
        var depth = 0;
        var maximumDepth = 0;
        callbackController.Changed += OnChanged;
        void OnChanged()
        {
            depth++;
            maximumDepth = Math.Max(maximumDepth, depth);
            notifications++;
            if (callbackHost is { } host)
            {
                callbackHost = null;
                host.Dispose();
            }
            depth--;
        }
        callbackController.Dispatch(new("Deferred while hidden"));
        check(notifications == 2 && maximumDepth == 1 && callbackController.Visible.Count == 1 && callbackController.Queued.Count == 0,
            "host disposal during Changed restores capacity after the current notification without recursive callbacks");
        callbackController.Changed -= OnChanged;
        using (var replacement = new UiToasterWidget(callbackController))
            check(callbackController.Visible.Count == 1, "Changed-callback disposal permits a later clean host attachment");

        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark);
        ControlFontFixture.Configure(context);
        var faultController = new UiToastController();
        var faultHost = new UiToasterWidget(faultController, new(ReducedMotion : true));
        faultController.Dispatch(new("Owned surface") { Timeout = null });
        context.NewFrame(new(.016f, new(640, 480), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 640, 480), UiTextSettings.Default);
        context.Render(new Screen(ui => faultHost.Render(ui)));
        using (var draw = context.GetDrawData()) { }
        faultController.SetViewportCapacity(0);
        void ThrowingObserver() => throw new InvalidOperationException("OWNERSHIP_OBSERVER_FAILURE");
        faultController.Changed += ThrowingObserver;
        var observerFailureReported = false;
        try { faultHost.Dispose(); }
        catch (AggregateException error)
        {
            observerFailureReported = error.InnerExceptions.Any(inner => inner is InvalidOperationException
                && inner.Message == "OWNERSHIP_OBSERVER_FAILURE");
        }
        faultController.Changed -= ThrowingObserver;
        check(observerFailureReported && faultHost.Entries.Count == 0,
            "external observer failure stays visible while host disposal still releases owned surface resources");
        using (var replacement = new UiToasterWidget(faultController))
            check(faultController.Visible.Count == 1, "observer failure during host release does not leak its controller lease or notifications");

        var recursiveController = new UiToastController();
        var recursiveRejected = false;
        void AttachDuringNotification()
        {
            try { using var recursive = new UiToasterWidget(recursiveController); }
            catch (InvalidOperationException) { recursiveRejected = true; }
        }
        recursiveController.Changed += AttachDuringNotification;
        recursiveController.Dispatch(new("Notification boundary"));
        recursiveController.Changed -= AttachDuringNotification;
        using (var later = new UiToasterWidget(recursiveController))
            check(recursiveRejected, "host attachment during Changed is rejected atomically and does not reserve a lease");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
