using Duxel.Core;

internal static class LegacyTabAccessibilityTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        using var wake = new ManualResetEventSlim();
        context.SetAccessibilityTree(tree);
        ControlFontFixture.Configure(context);
        var firstCalls = 0;
        var secondCalls = 0;
        var actions = 0;
        var disabled = false;
        var clipped = false;
        var showSecond = true;
        var closeSelection = false;
        var selectionEvents = 0;
        var committedEvents = true;
        tree.SelectionChanged += change =>
        {
            selectionEvents++;
            committedEvents &= tree.TryGet(change.Element.Node.Id, out var current) && current == change.Element;
        };
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var screen = new Screen(ui =>
        {
            ui.SetCursorScreenPos(new(8, clipped ? 800 : 8));
            ui.BeginTabBar("Legacy tabs");
            if (ui.BeginTabItem("First"))
            {
                firstCalls++;
                ui.Button("First child");
                ui.BeginTabBar("Nested tabs");
                if (ui.BeginTabItem("Nested")) { ui.Button("Nested child"); ui.EndTabItem(); }
                ui.EndTabBar();
                ui.EndTabItem();
            }
            if (showSecond)
            {
                ui.BeginDisabled(disabled);
                if (ui.BeginTabItem("Second")) { secondCalls++; ui.Button("Second child"); ui.EndTabItem(); }
                ui.EndDisabled();
            }
            if (closeSelection) ui.SetTabItemClosed("Second");
            if (ui.TabItemButton("Action")) actions++;
            ui.EndTabBar();
            ui.Button("After");
        });
        void Frame()
        {
            firstCalls = secondCalls = 0;
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                {
                    if (item.Node.Name == name && item.Node.Role == role) found = item;
                    Visit(item.FirstChild);
                    id = item.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Legacy semantic node missing : " + name);
        }
        Task<UiAccessibilityResult> Request(string name, UiAccessibilityRole role, UiAccessibilityAction action)
        {
            var item = Find(name, role);
            wake.Reset();
            var task = Task.Run(() => tree.RequestAction(item.Node.Id, item.RuntimeId, action));
            if (WaitHandle.WaitAny([wake.WaitHandle, ((IAsyncResult)task).AsyncWaitHandle], 1000) == WaitHandle.WaitTimeout)
                throw new TimeoutException("Legacy request was neither queued nor rejected.");
            return task;
        }
        UiAccessibilityResult Complete(Task<UiAccessibilityResult> task)
        {
            if (!task.Wait(1000)) throw new TimeoutException("Legacy request did not complete after publication.");
            return task.Result;
        }
        Frame();
        tree.SetRequestFrameCallback(wake.Set);
        var first = Find("First", UiAccessibilityRole.Tab);
        var firstPanel = Find("First", UiAccessibilityRole.TabPanel);
        var root = Find("Legacy tabs", UiAccessibilityRole.TabList);
        check(first.Node.Selected == true && first.Node.ParentId == root.Node.Id && root.Node.SelectionRequired &&
            first.Node.ControlledElementId == firstPanel.Node.Id && firstPanel.Node.LabeledBy == first.Node.Id,
            "legacy tabs publish required single selection and reciprocal active panel relationships");
        check(Find("First child", UiAccessibilityRole.Button).Node.ParentId == firstPanel.Node.Id &&
            Find("Nested tabs", UiAccessibilityRole.TabList).Node.ParentId == firstPanel.Node.Id &&
            Find("After", UiAccessibilityRole.Button).Node.ParentId.Length == 0,
            "legacy nested tab scopes preserve panel descendants and restore the outer accessibility parent");
        var revision = tree.Revision;
        Frame();
        check(tree.Revision == revision && selectionEvents == 0, "unchanged legacy tabs emit no selection change or tree revision");
        var select = Request("Second", UiAccessibilityRole.Tab, UiAccessibilityAction.Select);
        Frame();
        check(!select.IsCompleted && firstCalls == 1 && secondCalls == 0 && Find("First", UiAccessibilityRole.Tab).Node.Selected == true,
            "legacy selection does not acknowledge or publish a second body during the deferred frame");
        check(context.State.NextScheduledFrameTimeSeconds == context.State.TimeSeconds,
            "legacy semantic selection schedules its required next frame in event-wait mode");
        Frame();
        check(Complete(select) == UiAccessibilityResult.Success && firstCalls == 0 && secondCalls == 1 &&
            Find("Second", UiAccessibilityRole.Tab).Node.Selected == true && committedEvents && selectionEvents > 0,
            "legacy selection completes only after the selected body and immutable event state are published");
        var eventCount = selectionEvents;
        wake.Reset(); Frame();
        check(!wake.IsSet && !context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "committed legacy selection returns to event waiting without periodic wakeups or animation deadlines");
        select = Request("Second", UiAccessibilityRole.Tab, UiAccessibilityAction.Select); Frame();
        check(Complete(select) == UiAccessibilityResult.Success && selectionEvents == eventCount,
            "selecting the active legacy tab is idempotent and completes without an extra frame");
        var deselect = Request("Second", UiAccessibilityRole.Tab, UiAccessibilityAction.Deselect); Frame();
        check(Complete(deselect) == UiAccessibilityResult.InvalidOperation,
            "legacy required selection rejects removing its selected tab");
        var action = Request("Action", UiAccessibilityRole.Button, UiAccessibilityAction.Invoke); Frame();
        check(Complete(action) == UiAccessibilityResult.Success && actions == 1 && secondCalls == 1,
            "legacy TabItemButton exposes Invoke without becoming a selectable tab");
        select = Request("First", UiAccessibilityRole.Tab, UiAccessibilityAction.Select); Frame(); Frame();
        check(Complete(select) == UiAccessibilityResult.Success && firstCalls == 1 && secondCalls == 0,
            "legacy semantic navigation works in both header directions with one body per frame");
        select = Request("Second", UiAccessibilityRole.Tab, UiAccessibilityAction.Select); disabled = true; Frame();
        check(Complete(select) == UiAccessibilityResult.Disabled && firstCalls == 1,
            "legacy tab selection revalidates live disabled state before changing selection");
        disabled = false; Frame(); select = Request("Second", UiAccessibilityRole.Tab, UiAccessibilityAction.Select); clipped = true; Frame();
        check(Complete(select) == UiAccessibilityResult.Unavailable && firstCalls == 1,
            "legacy tab selection rejects a header clipped after request acceptance");
        clipped = false; Frame(); select = Request("Second", UiAccessibilityRole.Tab, UiAccessibilityAction.Select); closeSelection = true; Frame();
        closeSelection = false; Frame();
        check(Complete(select) == UiAccessibilityResult.InvalidOperation && firstCalls == 1,
            "a programmatic close before deferred publication cannot report a selection that never committed");
        select = Request("Second", UiAccessibilityRole.Tab, UiAccessibilityAction.Select); Frame(); showSecond = false; Frame();
        check(Complete(select) == UiAccessibilityResult.Unavailable,
            "removing the target before deferred publication rejects the original runtime identity");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
