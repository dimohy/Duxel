using Duxel.Core;
using Duxel.Platform.Windows;

internal static class TooltipEventTests
{
    internal static void Run(Action<bool, string> check)
    {
        void Check(bool value, string name) => check(value, "tooltip event : " + name);
        using var tooltip = TooltipAccessibilityTests.Wrap(Fluent.Button("Target"), "Visible help");
        tooltip.Options = new() { ShowDelaySeconds = 0, HideDelaySeconds = 0 };
        using var scene = new TooltipAccessibilityTests.Scene(tooltip);
        var events = new List<UiAccessibilityEvent>();
        var textChanges = new List<UiAccessibilityTextChange>();
        var updatedBeforeNotification = true;
        scene.Host.Tree.TextPropertyChanged += change =>
        {
            updatedBeforeNotification &= scene.Host.Tree.TryGet(change.Element.Node.Id, out var published)
                && published == change.Element;
            textChanges.Add(change);
        };
        var liveInCallbacks = true;
        scene.Host.Tree.AutomationEvent += notification =>
        {
            liveInCallbacks &= scene.Host.Tree.TryGet(notification.Element.Node.Id, out var current)
                && current.RuntimeId == notification.Element.RuntimeId;
            events.Add(notification);
        };
        scene.Frame();
        Check(scene.Host.Tree.Count == 1 && events.Count == 0, "hidden help has no surface node or opened event");
        scene.Frame(0.01f, new(30, 70));
        var target = scene.First;
        scene.Host.Tree.TryGet(target.FirstChild!, out var surface);
        Check(surface.Node.Role == UiAccessibilityRole.Tooltip && surface.Node.ParentId == target.Node.Id
            && surface.Node.Name == "Visible help" && surface.Node.Id == target.Node.Tooltip?.SourceId,
            "visible surface has its own stable identity under the actual primary target");
        Check(!surface.Node.Focusable && !surface.Node.ValuePattern && !surface.Node.Focused && !surface.Node.Offscreen,
            "plain text surface is visible but has no keyboard focus or value pattern");
        var provider = scene.Host.Find(surface.Node.Id)!;
        provider.GetPropertyValue(30003, out var type);
        Check(type.Type == 3 && type.Data.Integer == 50022, "native surface uses the ToolTip control type");
        provider.GetPropertyValue(30017, out var content);
        provider.GetPropertyValue(30016, out var control);
        Check(content.Type == 11 && content.Data.Boolean == 0 && control.Type == 11 && control.Data.Boolean != 0,
            "plain tooltip belongs to control view and not content view");
        provider.GetPatternProvider(10000, out var invoke);
        provider.GetPatternProvider(10002, out var value);
        Check(invoke == 0 && value == 0, "tooltip exposes neither Invoke nor Value patterns");
        Check(events.Count == 1 && events[0].Kind == UiAccessibilityEventKind.TooltipOpened,
            "first visible publication emits one opened event");
        scene.Frame(0.01f, new(30, 70));
        Check(events.Count == 1 && scene.Host.Tree.TryGet(surface.Node.Id, out var unchanged)
            && unchanged.RuntimeId == surface.RuntimeId, "unchanged visible frames preserve identity without duplicate opened events");
        tooltip.Text = "Updated help";
        scene.Frame(0.01f, new(30, 70));
        Check(events.Count == 1 && scene.NativeString(surface.Node.Id, 30005) == "Updated help",
            "content refresh is not reported as another open");
        Check(updatedBeforeNotification && textChanges.Count == 2
            && textChanges[0] is { Property : UiAccessibilityTextProperty.HelpText, OldValue : "Visible help", NewValue : "Updated help" }
            && textChanges[0].Element.RuntimeId == target.RuntimeId
            && textChanges[1] is { Property : UiAccessibilityTextProperty.Name, OldValue : "Visible help", NewValue : "Updated help" }
            && textChanges[1].Element.RuntimeId == surface.RuntimeId,
            "text refresh reports the published target HelpText and the same visible tooltip Name");
        scene.Frame(0.01f, new(30, 70));
        Check(textChanges.Count == 2, "unchanged frames emit no text property notifications");
        scene.Frame();
        Check(events.Count == 2 && events[^1].Kind == UiAccessibilityEventKind.TooltipClosed && liveInCallbacks,
            "close callback can read its source before removal, outside publication locks");
        Check(scene.Host.Tree.Count == 1 && !scene.Host.Tree.TryGet(surface.Node.Id, out _)
            && provider.GetPropertyValue(30005, out _) == UiaNative.Unavailable,
            "closed surface is removed and retained providers become unavailable");
        tooltip.Text = "Hidden update";
        scene.Frame();
        Check(textChanges.Count == 3 && textChanges[^1] is
            { Property : UiAccessibilityTextProperty.HelpText, OldValue : "Updated help", NewValue : "Hidden update" },
            "hidden tooltip text changes only the target HelpText");
        scene.Frame(0.01f, new(30, 70));
        Check(events.Count == 3 && scene.Host.Tree.TryGet(surface.Node.Id, out var recreated)
            && recreated.RuntimeId != surface.RuntimeId, "reopened source receives a new runtime identity");
        scene.Hidden = true;
        scene.Frame();
        Check(events.Count == 4 && events[^1].Kind == UiAccessibilityEventKind.TooltipClosed && scene.Host.Tree.Count == 0,
            "omitting the target closes its surface before both are unpublished");
        scene.Hidden = false;
        tooltip.Options = tooltip.Options with { Relationship = UiTooltipRelationship.Inaccessible };
        scene.Frame(0.01f, new(30, 70));
        Check(tooltip.IsOpen && scene.Host.Tree.Count == 1 && events.Count == 4,
            "inaccessible visual tooltip creates no semantic surface or events");

        using var tree = new UiAccessibilityTree();
        var trace = new List<UiAccessibilityEventKind>();
        var rejectedReentry = false;
        tree.AutomationEvent += notification =>
        {
            trace.Add(notification.Kind);
            try
            {
                tree.BeginFrame();
            }
            catch (InvalidOperationException)
            {
                rejectedReentry = true;
            }
        };
        var node = new UiAccessibilityNode("surface", string.Empty, UiAccessibilityRole.Tooltip, "Manual", new(0, 0, 80, 24),
            true, false, false, Focusable : false, ValuePattern : false);
        tree.BeginFrame();
        tree.Add(node);
        tree.EndFrame();
        Check(rejectedReentry && trace.Count == 1, "automation observers can read but cannot reenter publication");
        tree.BeginFrame();
        tree.AbortFrame();
        Check(trace.Count == 1 && tree.Count == 1, "aborted frames emit no closed notification");
        tree.BeginFrame();
        tree.Add(node with { Offscreen = true });
        tree.EndFrame();
        Check(trace.Count == 2 && trace[^1] == UiAccessibilityEventKind.TooltipClosed,
            "offscreen transition closes a previously visible surface");
        tree.BeginFrame();
        tree.Add(node);
        tree.EndFrame();
        Check(trace.Count == 3 && trace[^1] == UiAccessibilityEventKind.TooltipOpened,
            "returning onscreen emits a new visible transition");

        using var controlled = TooltipAccessibilityTests.Wrap(Fluent.Button("Controlled"), "Controlled help");
        controlled.Visible = true;
        var changes = new List<UiTooltipVisibilityChange>();
        controlled.VisibleChangeRequested = changes.Add;
        using var visibilityScene = new TooltipAccessibilityTests.Scene(controlled) { HostVisibility = new(true, 0) };
        var hostEvents = new List<UiAccessibilityEventKind>();
        visibilityScene.Host.Tree.AutomationEvent += notification => hostEvents.Add(notification.Kind);
        visibilityScene.Frame();
        Check(!controlled.IsOpen && hostEvents.Count == 0 && changes.Count == 0,
            "initially hidden host defers controlled surface publication");
        visibilityScene.HostVisibility = new(false, 0);
        visibilityScene.Frame();
        visibilityScene.HostVisibility = new(true, 1);
        visibilityScene.Frame();
        visibilityScene.Frame();
        Check(!controlled.IsOpen && controlled.Visible == true
            && changes.SequenceEqual([new(false, UiTooltipVisibilityReason.HostHidden)])
            && hostEvents.SequenceEqual([UiAccessibilityEventKind.TooltipOpened, UiAccessibilityEventKind.TooltipClosed]),
            "hidden frames close the presentation and request dismissal exactly once");
        visibilityScene.HostVisibility = new(false, 1);
        visibilityScene.Frame();
        Check(controlled.IsOpen && changes.Count == 1 && hostEvents.Last() == UiAccessibilityEventKind.TooltipOpened,
            "restoring a visible host honors the retained external value");
        visibilityScene.HostVisibility = new(false, 2);
        visibilityScene.Frame();
        Check(controlled.IsOpen && changes.Count == 2 && changes.Last().Reason == UiTooltipVisibilityReason.HostHidden,
            "hide sequence preserves a visibility loss that coalesced with restore");

        using var automatic = TooltipAccessibilityTests.Wrap(Fluent.Button("Automatic"), "Automatic help");
        automatic.Options = new() { ShowDelaySeconds = 0 };
        using var automaticScene = new TooltipAccessibilityTests.Scene(automatic);
        var anchor = new UiVector2(30, 70);
        automaticScene.Frame(mouse : anchor);
        automaticScene.HostVisibility = new(true, 1);
        automaticScene.Frame(mouse : anchor);
        automaticScene.HostVisibility = new(false, 1);
        automaticScene.Frame(mouse : anchor);
        Check(!automatic.IsOpen, "automatic tooltip remains suppressed on restore until trigger exit");
        automaticScene.Frame();
        automaticScene.Frame(mouse : anchor);
        Check(automatic.IsOpen, "new interaction after host restore can open the automatic tooltip");

        using var label = TooltipAccessibilityTests.Wrap(Fluent.Button("##icon"), "More options", UiTooltipRelationship.Label);
        using var labelScene = new TooltipAccessibilityTests.Scene(label);
        var labelChanges = new List<UiAccessibilityTextChange>();
        labelScene.Host.Tree.TextPropertyChanged += labelChanges.Add;
        labelScene.Frame();
        label.Text = "More settings";
        labelScene.Frame();
        Check(labelChanges.Count == 1 && labelChanges[0] is
            { Property : UiAccessibilityTextProperty.Name, OldValue : "More options", NewValue : "More settings" },
            "label relationship changes the effective target Name while its surface is hidden");
        label.Target.AccessibleName = "Explicit name";
        labelScene.Frame();
        label.Text = "Overridden label";
        labelScene.Frame();
        Check(labelChanges.Count == 2 && labelChanges[1] is
            { Property : UiAccessibilityTextProperty.Name, OldValue : "More settings", NewValue : "Explicit name" },
            "overridden tooltip labels do not emit unchanged target names");

        using var ending = new UiAccessibilityTree();
        var ended = 0;
        var ancestryAvailable = false;
        ending.AutomationEvent += notification =>
        {
            if (notification.Kind == UiAccessibilityEventKind.TooltipClosed)
            {
                ended++;
                ancestryAvailable = ending.TryGet(notification.Element.Node.Id, out var source)
                    && source.RuntimeId == notification.Element.RuntimeId
                    && ending.TryGet(notification.Element.Node.ParentId, out _);
            }
        };
        ending.BeginFrame();
        ending.Add(node with { Id = "target", Role = UiAccessibilityRole.Button });
        ending.Add(node with { ParentId = "target" });
        ending.Add(node with { Id = "offscreen", ParentId = "target", Offscreen = true });
        ending.EndFrame();
        ending.Dispose();
        ending.Dispose();
        Check(ended == 1 && ancestryAvailable && ending.IsDisposed && ending.Count == 0,
            "tree shutdown closes only the published visible surface before making its ancestry unavailable");

        using var scroller = TooltipAccessibilityTests.Wrap(Fluent.Button("Scrollable target"), "Two axes");
        scroller.Options = new() { ShowScrollbars = true, ScrollStep = 16 };
        scroller.Visible = true;
        using var scrollScene = new TooltipAccessibilityTests.Scene(scroller);
        scrollScene.Frame();
        var scrollTree = scrollScene.Host.Tree;
        var id = scrollScene.First.FirstChild!;
        scrollTree.TryGet(id, out var scrollNode);
        var runtimeId = scrollNode.RuntimeId;
        Check(scrollNode.Node.Scroll is { HorizontallyScrollable : true, VerticallyScrollable : true,
            HorizontalViewSize : 50, VerticalViewSize : 25 } && scrollScene.First.Node.Scroll is null,
            "scroll metrics belong to the visible tooltip body and derive from its two measured axes");
        var queued = scrollTree.RequestScroll(id, runtimeId, UiAccessibilityScrollCommand.ByPercent(50, 75), wait : false);
        scrollScene.Frame();
        scrollTree.TryGet(id, out var moved);
        Check(queued == UiAccessibilityResult.Success && TooltipAccessibilityTests.Surface.LastScrollOffset == new UiVector2(60, 63)
            && moved.Node.Scroll is { HorizontalScrollPercent : 50, VerticalScrollPercent : 75 },
            "percent action updates the paint offset and publishes the resulting metrics on the same frame");
        queued = scrollTree.RequestScroll(id, runtimeId, UiAccessibilityScrollCommand.ByAmount(
            UiAccessibilityScrollAmount.SmallIncrement, UiAccessibilityScrollAmount.LargeDecrement), wait : false);
        scrollScene.Frame();
        Check(queued == UiAccessibilityResult.Success && TooltipAccessibilityTests.Surface.LastScrollOffset == new UiVector2(76, 35),
            "amount action uses the configured small step and one viewport for the large step");
        Check(scrollTree.RequestScroll(id, runtimeId, UiAccessibilityScrollCommand.ByPercent(double.NaN, 20), wait : false)
            == UiAccessibilityResult.InvalidArgument, "invalid scroll percent is rejected before scheduling");
        scroller.Text = "Vertical";
        scrollScene.Frame();
        scrollTree.TryGet(id, out var vertical);
        Check(vertical.Node.Scroll is { HorizontalScrollPercent : -1, HorizontalViewSize : 100, HorizontallyScrollable : false }
            && scrollTree.RequestScroll(id, runtimeId, UiAccessibilityScrollCommand.ByPercent(50, 0), wait : false)
                == UiAccessibilityResult.InvalidOperation, "non-scrollable axes report no-scroll and reject movement");
        scroller.Visible = false;
        scrollScene.Frame();
        Check(scrollTree.RequestScroll(id, runtimeId, UiAccessibilityScrollCommand.ByPercent(-1, 20), wait : false)
            == UiAccessibilityResult.Unavailable, "retained runtime identity cannot scroll a dismissed tooltip");
        scroller.Options = scroller.Options with { Positioning = new() { RightToLeft = true } };
        scroller.Text = "Two axes";
        scroller.Visible = true;
        scrollScene.Frame();
        scrollTree.TryGet(scrollScene.First.FirstChild!, out var rtl);
        Check(rtl.Node.Scroll is { HorizontalScrollPercent : 0, RightToLeft : true }
            && TooltipAccessibilityTests.Surface.LastScrollOffset == new UiVector2(120, 0),
            "RTL tooltip UIA zero percent matches the painted right edge");
        scrollTree.RequestScroll(rtl.Node.Id, rtl.RuntimeId, UiAccessibilityScrollCommand.ByPercent(100, -1), wait : false);
        scrollScene.Frame();
        Check(scrollTree.TryGet(rtl.Node.Id, out var rtlLeft) && rtlLeft.Node.Scroll is { HorizontalScrollPercent : 100 }
            && TooltipAccessibilityTests.Surface.LastScrollOffset == default,
            "RTL tooltip UIA 100 percent paints the left edge without moving the vertical axis");
    }
}
