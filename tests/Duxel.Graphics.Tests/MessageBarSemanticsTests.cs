using Duxel.Core;

internal static class MessageBarSemanticsTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var received = new List<UiAccessibilityAnnouncement>();
        tree.Announcement += received.Add;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw, UiInputState? input = null, float delta = 1f / 60)
        {
            context.NewFrame(new(delta, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); draw(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name) => tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == name);
        using var title = Fluent.MessageBarTitle("한글 제목");
        using var body = Fluent.MessageBarBody("본문", title, Fluent.Link("Learn more", new Uri("https://example.com/"), _ => { }));
        using var bar = Fluent.MessageBar("연결 상태", body,
            Fluent.MessageBarActions([Fluent.Button("재시도")]), Fluent.Button("닫기"), new(Width: 500));
        Frame(ui => bar.Measure(ui, new(500, 400)));
        check(received.Count == 0 && tree.CaptureSnapshot().Elements.Count == 0,
            "MessageBar pure measurement creates neither title relationships nor announcements");
        Frame(bar.Render);
        var root = Find("연결 상태");
        var label = Find("한글 제목");
        check(root.Node.LabeledBy == label.Node.Id && label.Node.ParentId == root.Node.Id && label.Node.Role == UiAccessibilityRole.Label,
            "The named group labels itself through its actual owned title node");
        check(received.Count == 1 && received[0].Message == "한글 제목 본문 Learn more,재시도 닫기"
            && received[0].Priority == UiAnnouncementPriority.Polite,
            "Info mount announces prepared body and action leaf text once with polite priority");
        var activity = received[0].ActivityId;
        var revision = tree.Revision;
        for (var i = 0; i < 8; i++)
            Frame(bar.Render);
        check(received.Count == 1 && tree.Revision == revision && context.State.NextScheduledFrameTimeSeconds is null
            && !context.State.HasActiveAnimations,
            "Stable announcement state neither republishes semantics nor requests recurring frames");
        title.Text = "새 제목";
        body.Text = "바뀐 본문";
        Frame(bar.Render);
        check(received.Count == 1 && Find("연결 상태").Node.LabeledBy == label.Node.Id && Find("새 제목").Node.Id == label.Node.Id,
            "Content edits retain the real title relationship without treating text changes as a new mount");
        bar.Options = bar.Options with
        {
            Intent = UiMessageBarIntent.Error
        };
        Frame(bar.Render);
        check(received.Count == 2 && received[^1].Priority == UiAnnouncementPriority.Assertive
            && received[^1].Message.StartsWith("새 제목 바뀐 본문", StringComparison.Ordinal) && received[^1].ActivityId == activity,
            "A computed politeness change announces the current prepared content under the same activity");
        bar.Options = bar.Options with
        {
            Intent = UiMessageBarIntent.Success
        };
        Frame(bar.Render);
        check(received.Count == 2, "Intent changes with unchanged computed priority do not repeat announcements");
        bar.Options = bar.Options with
        {
            AnnouncementPriority = UiAnnouncementPriority.Polite
        };
        Frame(bar.Render);
        check(received.Count == 3 && received[^1].Priority == UiAnnouncementPriority.Polite,
            "An explicit politeness override uses the common announcement priority contract");
        bar.Options = bar.Options with
        {
            DisableAnnouncement = true
        };
        Frame(bar.Render);
        check(received.Count == 3, "Explicitly disabling automatic announcement does not emit a replacement message");
        bar.Options = bar.Options with
        {
            DisableAnnouncement = false
        };
        Frame(bar.Render);
        check(received.Count == 4, "Re-enabling automatic announcement establishes one new enabled activity");
        bar.Options = bar.Options with
        {
            AnnouncementText = "별도 메시지 🙂"
        };
        Frame(bar.Render);
        check(received.Count == 4, "Changing an override alone does not turn the mounted message into an aria-live region");
        Frame(_ => { });
        Frame(bar.Render);
        check(received.Count == 5 && received[^1].Message == "별도 메시지 🙂",
            "A remount announces the explicit Unicode content once");
        var before = received.Count;
        using var emptyBody = Fluent.MessageBarBody("");
        using var empty = Fluent.MessageBar("Empty", emptyBody);
        Frame(empty.Render);
        emptyBody.Text = "Later content";
        Frame(empty.Render);
        check(received.Count == before, "An empty mount is consumed without inventing fallback text or repeating after later content edits");
        Frame(_ => { });
        Frame(empty.Render);
        check(received.Count == before + 1 && received[^1].Message == "Later content", "A later nonempty remount announces its actual leaf text");
        Frame(_ => { });
        check(tree.AutomaticAnnouncementOwners == 0, "Unmounted announcement owners release their retained activity state");
        using var retry = Fluent.MessageBar("Retry publication", Fluent.MessageBarBody("Retry body", Fluent.MessageBarTitle("Retry title")));
        before = received.Count;
        var rejected = false;
        try
        {
            Frame(ui => { retry.Render(ui); throw new InvalidOperationException("Abort mount"); });
        }
        catch (InvalidOperationException error) when (error.Message == "Abort mount") { rejected = true; }
        check(rejected && received.Count == before && tree.CaptureSnapshot().Elements.Count == 0,
            "An aborted mount neither publishes its title nor consumes or delivers its pending announcement");
        check(tree.AutomaticAnnouncementOwners == 0, "Aborted new mounts leave no retained automatic owner records");
        Frame(retry.Render);
        check(received.Count == before + 1 && received[^1].Message == "Retry title Retry body",
            "The first successful retry delivers the original mount exactly once");
        retry.Options = retry.Options with
        {
            Intent = UiMessageBarIntent.Error
        };
        before = received.Count;
        try
        {
            Frame(ui => { retry.Render(ui); throw new InvalidOperationException("Abort priority"); });
        }
        catch (InvalidOperationException error) when (error.Message == "Abort priority") { }
        Frame(retry.Render);
        check(received.Count == before + 1 && received[^1].Priority == UiAnnouncementPriority.Assertive,
            "An aborted priority change preserves the last committed automatic announcement state");
        foreach (var invalid in new[] { new UiMessageBarOptions { AnnouncementPriority = (UiAnnouncementPriority)0 },
            new UiMessageBarOptions { AnnouncementText = "\0" }, new UiMessageBarOptions { AnnouncementText = " " } })
        {
            rejected = false;
            try
            {
                retry.Options = invalid;
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected && retry.Options.Intent == UiMessageBarIntent.Error, "Invalid announcement options reject atomically before changing the message");
        }
        using var preparedTitle = Fluent.MessageBarTitle("Before");
        using var preparedBody = Fluent.MessageBarBody("Old", preparedTitle);
        using var change = Fluent.Button("Change snapshot", () => { preparedTitle.Text = "After"; preparedBody.Text = "New"; });
        using var prepared = Fluent.MessageBar("Prepared snapshot", preparedBody, Fluent.MessageBarActions([change]));
        Frame(prepared.Render);
        var changeNode = Find("Change snapshot");
        tree.RequestAction(changeNode.Node.Id, changeNode.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        prepared.Options = prepared.Options with
        {
            Intent = UiMessageBarIntent.Error
        };
        Frame(prepared.Render);
        check(preparedTitle.Text == "After" && received[^1].Message == "Before Old,Change snapshot" && Find("Before").Node.Role == UiAccessibilityRole.Label,
            "Action callbacks cannot replace this frame's prepared title/body announcement with later live field values");
        before = received.Count;
        Frame(prepared.Render);
        check(received.Count == before && Find("After").Node.Role == UiAccessibilityRole.Label,
            "The next content frame updates the actual title without repeating its already committed priority announcement");
        using (var disconnected = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2)))
        using (var laterTree = new UiAccessibilityTree())
        using (var laterBar = Fluent.MessageBar("Later accessibility", Fluent.MessageBarBody("Connected later")))
        {
            ControlFontFixture.Configure(disconnected);
            var laterReceived = 0;
            laterTree.Announcement += _ => laterReceived++;
            void OtherFrame()
            {
                disconnected.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
                disconnected.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); laterBar.Render(ui); }));
                using var draw = disconnected.GetDrawData();
            }
            OtherFrame();
            disconnected.SetAccessibilityTree(laterTree);
            OtherFrame();
            OtherFrame();
            check(laterReceived == 1, "Rendering without an accessibility tree does not consume the later connected tree's mount");
        }
        foreach (var key in new[] { UiKey.Enter, UiKey.Space })
        {
            using var dismiss = Fluent.Button("Dismiss actual " + key);
            using var keepAction = Fluent.Button("Keep actual " + key);
            using var first = Fluent.MessageBar("Dismissable", Fluent.MessageBarBody("Remove me", Fluent.MessageBarTitle("First")), containerAction: dismiss);
            using var second = Fluent.MessageBar("Retained", Fluent.MessageBarBody("Keep me"), Fluent.MessageBarActions([keepAction]));
            using var group = Fluent.MessageBarGroup([first, second], keys: ["first", "second"]);
            dismiss.Activated = () => group.SetItems([new("second", second)]);
            Frame(group.Render);
            var button = Find("Dismiss actual " + key);
            tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Focus, wait: false);
            Frame(group.Render);
            Frame(group.Render, idle with
            {
                KeyEvents = [new(key, true, default)]
            });
            Frame(group.Render, idle with
            {
                KeyEvents = [new(key, false, default)]
            });
            Frame(group.Render);
            check(group.Items.Length == 1 && context.State.FocusedId == Find("Keep actual " + key).Node.Id,
                "Actual MessageBar container action dismisses by keyboard and restores surviving action focus : " + key);
            Frame(group.Render, delta: .3f);
            rejected = false;
            try
            {
                _ = first.Body;
            }
            catch (ObjectDisposedException) { rejected = true; }
            check(rejected, "Actual removed MessageBar ends its owned lifetime after the exit fade : " + key);
        }
        using var close = Fluent.Button("Pointer close");
        using var nextAction = Fluent.Button("Next message action");
        using var pointerFirst = Fluent.MessageBar("Pointer-dismissable", Fluent.MessageBarBody("Remove me"), containerAction: close);
        using var pointerSecond = Fluent.MessageBar("Pointer survivor", Fluent.MessageBarBody("Keep me"), Fluent.MessageBarActions([nextAction]));
        using var pointerGroup = Fluent.MessageBarGroup([pointerFirst, pointerSecond]);
        close.Activated = () => pointerGroup.SetVisible(0, false);
        Frame(pointerGroup.Render);
        var closeBounds = Find("Pointer close").Node.Bounds;
        var pointer = idle with { MousePosition = new(closeBounds.X + closeBounds.Width / 2, closeBounds.Y + closeBounds.Height / 2) };
        Frame(pointerGroup.Render, pointer with { LeftMouseDown = true, LeftMousePressed = true });
        Frame(pointerGroup.Render, pointer with { LeftMouseReleased = true });
        Frame(pointerGroup.Render);
        check(!pointerGroup.IsVisible(0) && context.State.FocusedId is null && !context.State.NavCursorVisible,
            "Mouse dismissal clears the removed MessageBar action focus without focusing the next message");
        Frame(pointerGroup.Render, idle with { KeyEvents = [new(UiKey.Tab, true, default)] });
        check(context.State.FocusedId == Find("Next message action").Node.Id && context.State.NavCursorVisible,
            "Tab can focus the surviving MessageBar action after mouse dismissal");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
