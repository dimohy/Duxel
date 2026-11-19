using Duxel.Core;

internal static class ToastControllerTests
{
    internal static void Run(Action<bool, string> check)
    {
        var clock = new Clock();
        var controller = new UiToastController(clock : clock);
        var changes = 0;
        controller.Changed += () => changes++;
        var first = controller.Dispatch(new("Saved"), "first");
        check(controller.Visible.Count == 1 && controller.Visible[0].Status == UiToastStatus.Entering && controller.NextTimeout is null,
            "toast enters without starting its dismissal clock before the entry motion finishes");
        clock.Step(30); controller.Advance(); controller.MarkVisible(first);
        check(controller.NextTimeout == TimeSpan.FromSeconds(7), "default toast deadline starts seven seconds after visible completion");
        var stable = controller.Visible; var before = changes;
        controller.Advance(); controller.Advance();
        check(ReferenceEquals(stable, controller.Visible) && changes == before, "idle lifecycle polling neither rebuilds snapshots nor publishes changes");
        clock.Step(2); controller.SetPaused(first, UiToastPauseReason.Hover, true);
        check(controller.NextTimeout is null, "hover pause removes the dismissal deadline");
        controller.SetPaused(first, UiToastPauseReason.Focus, true); clock.Step(100);
        controller.SetPaused(first, UiToastPauseReason.Hover, false);
        check(controller.NextTimeout is null, "removing hover pause preserves a concurrent keyboard-focus pause");
        controller.SetPaused(first, UiToastPauseReason.WindowInactive, true);
        controller.SetPaused(first, UiToastPauseReason.Focus, false);
        check(controller.NextTimeout is null, "window inactivity remains independent from focus pause");
        controller.SetPaused(first, UiToastPauseReason.WindowInactive, false);
        check(controller.NextTimeout == TimeSpan.FromSeconds(5), "resuming preserves exactly the unused duration");
        clock.Step(4); controller.Update(first, new("Saved again"));
        check(controller.NextTimeout == TimeSpan.FromSeconds(1) && controller.Visible[0].Revision == 1,
            "content updates advance announcement revision without silently restarting the timer");
        controller.Update(first, new("Saved again"), restartTimer : true);
        check(controller.NextTimeout == TimeSpan.FromSeconds(7), "explicit restart renews the full configured duration");
        var retained = controller.Visible;
        clock.Step(7); check(controller.Advance() && controller.Visible[0].Status == UiToastStatus.Exiting && controller.NextTimeout is null,
            "expiry begins exit and stops its timer without prematurely dropping the paint owner");
        check(retained[0].Status == UiToastStatus.Visible, "previous immutable snapshots preserve their original lifecycle state");
        check(!controller.Dismiss(first) && controller.Remove(first) && controller.Visible.Count == 0,
            "dismissal is idempotent and exit completion removes the notification");
        var persistent = controller.Dispatch(new("Working") { Timeout = null }); controller.MarkVisible(persistent);
        clock.Step(1000); check(!controller.Advance() && controller.NextTimeout is null, "persistent progress notifications have no expiry or periodic wakeup");
        controller.Update(persistent, new("Finished") { Timeout = TimeSpan.FromSeconds(2) });
        check(controller.NextTimeout == TimeSpan.FromSeconds(2), "changing persistence to a timed confirmation arms a new deadline");
        check(new UiToastOptions("Information").AnnouncementPriority == UiAnnouncementPriority.Polite
            && new UiToastOptions("Warning", Intent : UiToastIntent.Warning).AnnouncementPriority == UiAnnouncementPriority.Assertive,
            "toast intent maps to the existing polite or assertive announcement contract");
        var queueClock = new Clock(); var queue = new UiToastController(2, queueClock);
        queue.Dispatch(new("A"), "a"); queue.Dispatch(new("B"), "b");
        queue.Dispatch(new("C"), "c"); queue.Dispatch(new("Urgent") { Priority = 10 }, "urgent");
        queue.Dispatch(new("D"), "d");
        check(queue.Visible.Select(e => e.Id).SequenceEqual(new[] { "b", "a" }) && queue.Queued.Select(e => e.Id).SequenceEqual(new[] { "urgent", "c", "d" }),
            "visible notifications are newest-first and waiting notifications use priority then FIFO");
        queue.Dismiss("a");
        check(queue.Visible.Count == 2 && queue.Queued.Count == 3, "an exiting toast retains its slot until exit paint finishes");
        queueClock.Step(100); queue.Remove("a");
        check(queue.Visible.Any(e => e.Id == "urgent" && e.Status == UiToastStatus.Entering) && queue.NextTimeout is null,
            "freeing a slot promotes the highest-priority waiter without charging its queue time");
        queue.MarkVisible("urgent");
        check(queue.NextTimeout == TimeSpan.FromSeconds(7), "a promoted toast receives its complete visible lifetime");
        queue.Dismiss("c");
        check(!queue.Queued.Any(e => e.Id == "c"), "dismissing a queued notification removes it without a phantom exit");
        queue.DismissAll();
        check(queue.Queued.Count == 0 && queue.Visible.All(e => e.Status == UiToastStatus.Exiting) && queue.NextTimeout is null,
            "dismiss-all clears waiting entries and begins finite exits for visible entries");
        var invalid = new UiToastController(clock : new Clock());
        invalid.Dispatch(new("Valid"), "same"); var original = invalid.Visible;
        void Invalid(Action action, string name)
        {
            try { action(); check(false, name); }
            catch (ArgumentException) { check(ReferenceEquals(original, invalid.Visible), name); }
        }
        Invalid(() => invalid.Dispatch(new("Duplicate"), "same"), "duplicate IDs fail before notification state changes");
        Invalid(() => invalid.Dispatch(new(" ")), "blank titles fail before notification state changes");
        Invalid(() => invalid.Dispatch(new("Invalid") { Timeout = TimeSpan.Zero }), "zero duration is rejected rather than treated as an implicit persistent toast");
        Invalid(() => invalid.Dispatch(new("Invalid", Intent : (UiToastIntent)99)), "unknown intent values fail before notification state changes");
        Invalid(() => invalid.SetPaused("same", (UiToastPauseReason)32, true), "unknown pause reasons fail before notification state changes");
        var optedOut = new UiToastController(clock : new Clock());
        var id = optedOut.Dispatch(new("No hover pause") { PauseOnHover = false, PauseOnWindowBlur = false }); optedOut.MarkVisible(id);
        check(!optedOut.SetPaused(id, UiToastPauseReason.Hover | UiToastPauseReason.WindowInactive, true)
            && optedOut.NextTimeout == TimeSpan.FromSeconds(7), "disabled environmental pause options leave the timer running");
        optedOut.SetPaused(id, UiToastPauseReason.Explicit, true);
        check(optedOut.NextTimeout is null, "explicit pause remains available independently of environmental settings");
        var viewportClock = new Clock(); var viewport = new UiToastController(clock : viewportClock);
        viewport.Dispatch(new("Older"), "older"); viewport.Dispatch(new("Newer"), "newer");
        viewport.MarkVisible("older"); viewport.MarkVisible("newer"); viewportClock.Step(2);
        viewport.SetPaused("older", UiToastPauseReason.Explicit, true);
        viewport.SetPaused("newer", UiToastPauseReason.Hover | UiToastPauseReason.Focus, true);
        viewport.SetViewportCapacity(1, "older");
        check(viewport.Visible.Single().Id == "older" && viewport.Queued.Single().Id == "newer"
            && viewport.Queued[0].PauseReasons == UiToastPauseReason.None,
            "viewport capacity preserves the focused older Toast and clears obsolete pointer/focus pause on deferred content");
        var unchangedViewport = viewport.Visible;
        check(!viewport.SetViewportCapacity(1) && ReferenceEquals(unchangedViewport, viewport.Visible),
            "unchanged viewport capacity preserves snapshots without repeated change notifications");
        viewportClock.Step(30); viewport.SetViewportCapacity(2); viewport.MarkVisible("newer");
        check(viewport.NextTimeout == TimeSpan.FromSeconds(5),
            "viewport-deferred Toast resumes its exact unused lifetime without charging queue time");
        viewport.SetViewportCapacity(0); viewportClock.Step(30);
        check(viewport.Visible.Count == 0 && viewport.Queued.Count == 2 && viewport.NextTimeout is null,
            "a viewport with no usable slots queues content without hidden expiry deadlines");
        using (var owner = new UiToasterWidget(viewport)) { }
        check(viewport.Visible.Count == 2 && viewport.Queued.Count == 0,
            "disposing a host restores its caller-owned controller capacity");
        viewport.Dismiss("older"); viewport.SetViewportCapacity(0);
        check(viewport.Visible.Single().Status == UiToastStatus.Exiting,
            "viewport contraction keeps an exiting slot until its existing exit completes");
        viewport.Remove("older");
        check(viewport.Visible.Count == 0 && viewport.Queued.Count == 1,
            "exit completion respects zero viewport capacity rather than immediately promoting hidden work");
        var reverseClock = new Clock(); var reverse = new UiToastController(clock : reverseClock);
        reverseClock.Step(2); reverse.Dispatch(new("Monotonic")); reverseClock.Step(-1);
        try { reverse.Advance(); check(false, "a clock moving backwards is an explicit contract failure"); }
        catch (InvalidOperationException) { check(true, "a clock moving backwards is an explicit contract failure"); }
        var reentrant = new UiToastController(clock : new Clock()); var rejected = false;
        reentrant.Changed += () => { try { reentrant.Dispatch(new("Nested")); } catch (InvalidOperationException) { rejected = true; } };
        reentrant.Dispatch(new("Outer"));
        check(rejected && reentrant.Visible.Count == 1, "change notification rejects recursive mutation before state changes");
    }
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal void Step(int seconds) => _ticks += TimeSpan.FromSeconds(seconds).Ticks;
    }
}
