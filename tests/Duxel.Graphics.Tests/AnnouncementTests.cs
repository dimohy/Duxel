using Duxel.Core;

internal static class AnnouncementTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        var received = new List<UiAccessibilityAnnouncement>();
        tree.Announcement += received.Add;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(0, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(render));
            using var data = context.GetDrawData();
        }
        Frame(ui => check(!ui.Announce("연결되었습니다", "connection"),
            "Announcement without an accessibility tree explicitly reports not queued"));
        context.SetAccessibilityTree(tree);
        Frame(ui =>
        {
            check(ui.Announce("연결되었습니다 🙂", "connection"), "Valid Unicode announcement queues in the current frame");
            check(received.Count == 0, "Announcement waits for publication rather than invoking native code during capture");
            ui.PushID("task");
            try
            {
                ui.Announce("연결이 끊겼습니다", "connection", UiAnnouncementPriority.Assertive);
            }
            finally { ui.PopID(); }
        });
        check(received.Count == 2 && received[0].Priority == UiAnnouncementPriority.Polite
            && received[1].Priority == UiAnnouncementPriority.Assertive,
            "Announcements preserve FIFO order and explicit urgency");
        check(received[0].ActivityId == "connection" && received[1].ActivityId == "task/connection",
            "Announcement activities respect the existing caller identity scope");
        check(tree.CaptureSnapshot().Elements.Count == 0 && tree.Revision == 0,
            "Announcements work without inventing nodes, focus or a visual-tree revision");
        check(!context.State.HasActiveAnimations, "Announcements do not request continuing animation frames");
        Frame(_ => { });
        check(received.Count == 2, "A later idle frame does not replay a previous announcement");
        Frame(ui => { ui.Announce("Repeat", "repeat"); ui.Announce("Repeat", "repeat"); });
        check(received.Count == 4, "Explicit repeated announcements are not silently deduplicated");

        foreach (var invalid in new[]
        {
            new UiAccessibilityAnnouncement("", "activity"), new(" ", "activity"),
            new("message", ""), new("message\0", "activity"), new("message", "activity\0"),
            new("\ud800", "activity"), new("message", "\udc00"),
            new("message", "activity", (UiAnnouncementPriority)0), new("message", "activity", (UiAnnouncementPriority)3)
        })
        {
            var rejected = false;
            try
            {
                invalid.Validate();
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "Invalid announcement fails before native delivery: " + invalid.Priority);
        }
        var outsideRejected = false;
        try
        {
            tree.QueueAnnouncement(new("outside", "outside"));
        }
        catch (InvalidOperationException) { outsideRejected = true; }
        check(outsideRejected, "Announcement rejects calls outside the render capture");

        var reentryRejected = 0;
        void Reenter(UiAccessibilityAnnouncement _)
        {
            try
            {
                tree.BeginFrame();
            }
            catch (InvalidOperationException) { reentryRejected++; }
            try
            {
                tree.QueueAnnouncement(new("recursive", "recursive"));
            }
            catch (InvalidOperationException) { reentryRejected++; }
        }
        tree.Announcement += Reenter;
        Frame(ui => ui.Announce("No reentry", "reentry"));
        tree.Announcement -= Reenter;
        check(reentryRejected == 2, "Announcement observers cannot reenter publication or enqueue recursive speech");
        Frame(_ =>
        {
            var rejected = Task.Run(() =>
            {
                try
                {
                    tree.QueueAnnouncement(new("wrong writer", "writer"));
                }
                catch (InvalidOperationException) { return true; }
                return false;
            }).GetAwaiter().GetResult();
            check(rejected, "A different thread cannot append to the frame-local announcement queue");
        });
        var beforeAbort = received.Count;
        tree.BeginFrame();
        tree.QueueAnnouncement(new("aborted", "abort"));
        tree.AbortFrame();
        tree.BeginFrame();
        tree.EndFrame();
        check(received.Count == beforeAbort, "Aborted capture releases announcements without later replay");

        void Fail(UiAccessibilityAnnouncement _) => throw new InvalidOperationException("owned observer failure");
        tree.Announcement += Fail;
        var propagated = false;
        try
        {
            Frame(ui => ui.Announce("failure", "failure"));
        }
        catch (InvalidOperationException error) when (error.Message == "owned observer failure") { propagated = true; }
        tree.Announcement -= Fail;
        var afterFailure = received.Count;
        Frame(_ => { });
        check(propagated && received.Count == afterFailure,
            "Observer failure propagates and clears the completed frame queue without replay");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
