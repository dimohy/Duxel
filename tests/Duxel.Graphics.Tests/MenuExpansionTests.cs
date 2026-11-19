using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class MenuExpansionTests
{
    internal static void Run(Action<bool, string> check)
    {
        Run<UiThemeMenuPresentation>(check);
        Run<UiFluentMenuPresentation>(check);
        using var tree = new UiAccessibilityTree();
        tree.BeginFrame();
        var leaf = new UiAccessibilityNode("leaf", string.Empty, UiAccessibilityRole.Button, "Leaf", new(0, 0, 20, 20), true, false, false)
            { Expansion = UiAccessibilityExpansionState.LeafNode };
        tree.Add(leaf);
        tree.EndFrame();
        tree.TryGet("leaf", out var entry);
        check(tree.RequestAction("leaf", entry.RuntimeId, UiAccessibilityAction.Expand, wait : false) == UiAccessibilityResult.InvalidOperation,
            "leaf expansion state rejects actions rather than fabricating children");
        check(tree.RequestAction("leaf", entry.RuntimeId, (UiAccessibilityAction)999, wait : false) == UiAccessibilityResult.InvalidArgument,
            "action range still rejects undefined values after adding expansion actions");
        tree.BeginFrame();
        var rejected = false;
        try { tree.Add(leaf with { Expansion = (UiAccessibilityExpansionState)99 }); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        tree.AbortFrame();
        check(rejected && tree.TryGet("leaf", out var preserved) && preserved.Node.Expansion == UiAccessibilityExpansionState.LeafNode,
            "invalid expansion capture aborts without corrupting the last committed state");
    }
    private static void Run<TPresentation>(Action<bool, string> check) where TPresentation : IUiMenuPresentation
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var native = new WindowsAccessibility(0);
        var tree = native.Tree;
        context.SetAccessibilityTree(tree);
        ControlFontFixture.Configure(context);
        var calls = new List<string>();
        using var menu = new UiMenu<TPresentation>("Expand actions", [new("one", "One"), new("two", "Two")], calls.Add);
        var events = new List<UiAccessibilityEvent>();
        var changes = new List<UiAccessibilityExpansionChange>();
        tree.ExpansionChanged += change =>
        {
            check(tree.TryGet(change.Element.Node.Id, out var current) && current.Node.Expansion == change.Element.Node.Expansion,
                "expansion notification observes the final published state outside locks");
            changes.Add(change);
        };
        tree.AutomationEvent += notification =>
        {
            check(tree.TryGet(notification.Element.Node.Id, out _), "menu open/close notification retains a queryable source at delivery");
            events.Add(notification);
        };
        var shown = true;
        var enabled = true;
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), input ?? new(new(-100, -100), false, false, false, false,
                false, false, 0, 0, [], [], default, default), new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 8);
                ui.BeginDisabled(!enabled);
                if (shown) menu.Render(ui);
                ui.EndDisabled();
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Trigger()
        {
            tree.TryGet(tree.Root.First!, out var trigger);
            return trigger;
        }
        void Request(UiAccessibilityAction action)
        {
            var trigger = Trigger();
            var result = tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, action, wait : false);
            check(result == UiAccessibilityResult.Success,
                "actual menu trigger accepts " + action);
            if (result != UiAccessibilityResult.Success) throw new InvalidOperationException("Menu expansion request rejected : " + result);
            Frame();
        }
        Frame();
        var provider = native.Find(Trigger().Node.Id)!;
        provider.GetPatternProvider(10005, out var pattern);
        try
        {
            provider.GetPropertyValue(30028, out var available);
            check(pattern != 0 && available.Data.Boolean != 0 && provider.GetExpandCollapseState(out var state) == 0 && state == 0
                && Trigger().Node.Expansion == UiAccessibilityExpansionState.Collapsed && changes.Count == 0,
                "actual menu publishes an initially collapsed ExpandCollapse capability without fabricated initial changes");
        }
        finally { if (pattern != 0) Marshal.Release(pattern); }
        Request(UiAccessibilityAction.Expand);
        check(menu.IsOpen && Trigger().Node.Expansion == UiAccessibilityExpansionState.Expanded
            && events.Count == 1 && events[0].Kind == UiAccessibilityEventKind.MenuOpened && changes.Count == 1,
            "actual queued Expand opens once and commits the expanded state in its first frame");
        Request(UiAccessibilityAction.Expand);
        check(menu.IsOpen && events.Count == 1 && changes.Count == 1 && calls.Count == 0,
            "repeated Expand is idempotent and neither toggles nor invokes the menu");
        Request(UiAccessibilityAction.Collapse);
        check(!menu.IsOpen && Trigger().Node.Expansion == UiAccessibilityExpansionState.Collapsed
            && events.Count == 2 && events[^1].Kind == UiAccessibilityEventKind.MenuClosed && changes.Count == 2,
            "actual queued Collapse closes once and publishes its final collapsed state");
        Request(UiAccessibilityAction.Collapse);
        check(events.Count == 2 && changes.Count == 2, "repeated Collapse emits no duplicate state or menu events");
        Request(UiAccessibilityAction.Expand);
        enabled = false;
        Frame();
        check(!menu.IsOpen && Trigger().Node.Expansion == UiAccessibilityExpansionState.Collapsed
            && tree.RequestAction(Trigger().Node.Id, Trigger().RuntimeId, UiAccessibilityAction.Expand, wait : false) == UiAccessibilityResult.Disabled,
            "disabling an open owner collapses it and rejects expansion without changing the callback model");
        enabled = true;
        Frame();
        Request(UiAccessibilityAction.Expand);
        var retired = Trigger();
        shown = false;
        Frame();
        check(!menu.IsOpen && events[^1].Kind == UiAccessibilityEventKind.MenuClosed
            && tree.RequestAction(retired.Node.Id, retired.RuntimeId, UiAccessibilityAction.Expand, wait : false) == UiAccessibilityResult.Unavailable,
            "removing the owner closes its menu and rejects stale expansion requests");
        shown = true;
        Frame();
        Request(UiAccessibilityAction.Expand);
        var before = events.Count;
        native.Dispose();
        check(tree.IsDisposed && events.Count == before + 1 && events[^1].Kind == UiAccessibilityEventKind.MenuClosed,
            "tree shutdown delivers exactly one menu close before source unpublication");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
