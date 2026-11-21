using Duxel.Core;
using Duxel.Gallery;

internal static class MessageBarPageInteractionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new MessageBarPage();
        var announcements = new List<UiAccessibilityAnnouncement>();
        tree.Announcement += announcements.Add;
        void Frame()
        {
            context.NewFrame(new(.1f, new(1280, 900), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 1280, 900), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); page.Render(ui); }));
            using var data = context.GetDrawData();
        }
        void Settle()
        {
            for (var i = 0; i < 16; i++)
                Frame();
        }
        UiAccessibilityElement Find(string name, string? parent = null) => tree.CaptureSnapshot().Elements
            .Single(x => x.Node.Name == name && (parent is null || x.Node.ParentId == parent));
        void Act(string name, UiAccessibilityAction action, string? parent = null)
        {
            var node = Find(name, parent);
            if (node.Node.Offscreen)
            {
                if (tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("The actual MessageBar page could not reveal its control : " + name);
                Settle();
                node = Find(name, parent);
            }
            if (node.Node.Offscreen)
                throw new InvalidOperationException("The actual MessageBar control remains outside its scroll viewport : " + name);
            if (tree.RequestAction(node.Node.Id, node.RuntimeId, action, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Actual Observer control rejected its action : " + name);
            Settle();
        }
        void Toggle(string name) => Act(name, UiAccessibilityAction.Toggle);
        void Invoke(string name, string? parent = null) => Act(name, UiAccessibilityAction.Invoke, parent);
        void Width(double width)
        {
            var node = Find("MessageBar 너비");
            if (tree.RequestRangeValue(node.Node.Id, node.RuntimeId, width, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Actual Observer width slider rejected its numeric action.");
            Settle();
        }
        UiFluentMessageBar[] AllMessages() => page.FixedMessages.Children.ToArray().Concat(page.DynamicMessages.Children.ToArray())
            .Cast<UiFluentMessageBar>().ToArray();
        string[] fixedNames = ["메시지 정보", "메시지 성공", "메시지 경고", "메시지 오류"];
        Settle();
        check(page.FixedMessages.Children.Length == 4 && announcements.Count == 4
            && announcements.Count(x => x.Priority == UiAnnouncementPriority.Polite) == 1
            && fixedNames.All(name => Find(name).Node.LabeledBy is not null),
            "Actual Observer exposes all four owned intents with real title relations and initial priority announcements");
        Toggle("동작 줄이기");
        check(Find("동작 줄이기").Node.Checked == true && page.FixedMessages.Options.ReducedMotion
            && page.DynamicMessages.Options.ReducedMotion, "Actual reduced-motion switch updates both real groups");
        Width(300);
        check(Find("MessageBar 너비").Node.Range?.Value == 300
            && AllMessages().All(x => x.Options.Width == 300 && x.LastLayout.Size.X == 300),
            "Actual numeric slider input updates fixed-message options and allocated width");
        Width(800);
        Toggle("여러 행 배치");
        Toggle("각진 모서리");
        Toggle("오른쪽에서 왼쪽 배치");
        Invoke("메시지 추가");
        Invoke("메시지 추가");
        check(page.RequestedDynamicCount == 2 && page.PhysicalDynamicCount == 2
            && AllMessages().All(x => x.Options.Layout == UiMessageBarLayoutMode.MultiLine && x.LastLayout.Multiline
                && x.Options.Shape == UiMessageBarShape.Square && x.Options.RightToLeft && x.Options.Width == 800),
            "Current layout,shape,direction and width apply equally to fixed and newly inserted actual Observer messages");
        var before = page.DynamicMessages.Items.ToArray();
        Invoke("순서 뒤집기");
        check(page.DynamicMessages.Items[0].Key == before[1].Key
            && ReferenceEquals(page.DynamicMessages.Items[0].Widget, before[1].Widget)
            && ReferenceEquals(page.DynamicMessages.Items[1].Widget, before[0].Widget),
            "Actual reverse button preserves owned message identities while changing key order");
        Invoke("재시도", Find("메시지 정보").Node.Id);
        check(tree.CaptureSnapshot().Elements.Any(x => x.Node.Name == "너비 : 800 / 액션 실행 : 1"),
            "Actual retry button executes its callback and updates the visible action count");
        var count = announcements.Count;
        Invoke("닫기", Find("메시지 정보").Node.Id);
        check(tree.CaptureSnapshot().Elements.All(x => x.Node.Name != "메시지 정보")
            && page.RequestedDynamicCount == 2, "Actual close retires only its fixed message without removing dynamic items");
        Invoke("모두 다시 열기");
        check(Find("메시지 정보").Node.LabeledBy is not null && announcements.Count == count + 1,
            "Actual reopen restores the owned message and announces only that new mount");
        Toggle("표시할 때 자동 공지");
        count = announcements.Count;
        Invoke("닫기", Find("메시지 성공").Node.Id);
        Invoke("모두 다시 열기");
        check(announcements.Count == count && AllMessages().All(x => x.Options.DisableAnnouncement),
            "Actual announcement disable switch suppresses a close/reopen across fixed and dynamic messages");
        Toggle("표시할 때 자동 공지");
        check(announcements.Count == count + 6, "Actual re-enable switch announces each of the six mounted messages once");
        count = announcements.Count;
        Toggle("차분한 공지로 고정");
        check(announcements.Count == count + 3 && announcements.TakeLast(3).All(x => x.Priority == UiAnnouncementPriority.Polite)
            && AllMessages().All(x => x.Options.AnnouncementPriority == UiAnnouncementPriority.Polite),
            "Actual polite switch changes only the three formerly urgent mounted priorities");
        count = announcements.Count;
        Toggle("차분한 공지로 고정");
        check(announcements.Count == count + 3 && announcements.TakeLast(3).All(x => x.Priority == UiAnnouncementPriority.Assertive)
            && AllMessages().All(x => x.Options.AnnouncementPriority is null),
            "Actual polite override can return to each message's intent-derived native priority");
        Toggle("오른쪽에서 왼쪽 배치");
        Toggle("각진 모서리");
        Toggle("여러 행 배치");
        check(AllMessages().All(x => !x.Options.RightToLeft && x.Options.Shape == UiMessageBarShape.Rounded
            && x.Options.Layout == UiMessageBarLayoutMode.Auto), "Actual option switches restore both fixed and dynamic message defaults");
        var originalFixed = page.FixedMessages.Children.ToArray();
        foreach (var name in fixedNames)
            Invoke("닫기", Find(name).Node.Id);
        check(tree.CaptureSnapshot().Elements.All(x => x.Node.Role != UiAccessibilityRole.Group
            || !x.Node.Name.StartsWith("메시지 ", StringComparison.Ordinal)),
            "Each actual intent-specific close callback retires its own message");
        Invoke("모두 다시 열기");
        check(fixedNames.All(name => Find(name).Node.LabeledBy is not null)
            && page.FixedMessages.Children.ToArray().SequenceEqual(originalFixed),
            "Actual reopen restores all four fixed owned instances and title identities");
        Toggle("동작 줄이기");
        check(!page.FixedMessages.Options.ReducedMotion && !page.DynamicMessages.Options.ReducedMotion,
            "Actual reduced-motion switch can restore normal motion in both groups");
        var removed = page.DynamicMessages.Items[0].Key;
        var removedRoot = tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == removed && x.Node.Role == UiAccessibilityRole.Group);
        Invoke("삭제", removedRoot.Node.Id);
        check(page.RequestedDynamicCount == 1 && page.PhysicalDynamicCount == 1
            && page.DynamicMessages.Items[0].Key != removed, "Actual per-message delete removes its exact key and owned lifetime");
        Invoke("첫 메시지 삭제");
        check(page.RequestedDynamicCount == 0 && page.PhysicalDynamicCount == 0,
            "Actual first-message delete settles the remaining dynamic collection");
        Settle();
        count = announcements.Count;
        var revision = tree.Revision;
        Settle();
        check(announcements.Count == count && tree.Revision == revision && !context.State.HasActiveAnimations
            && context.State.NextScheduledFrameTimeSeconds is null,
            "Settled actual Observer interactions leave no repeated announcement,semantic revision or recurring frame deadline");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
