using Duxel.Core;
using Duxel.Platform.Windows;

internal static class FocusEventTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        var events = new List<UiAccessibilityFocusChange>();
        var published = true;
        tree.FocusChanged += change =>
        {
            events.Add(change);
            published &= tree.Root.Focus == change.Current?.Node.Id;
            if (change.Current is { } current)
                published &= tree.TryGet(current.Node.Id, out var live) && current == live;
        };
        void Frame(bool one = false, bool two = false, bool includeTwo = true, bool enabled = true,
            bool offscreen = false, bool focusable = true, float x = 0)
        {
            tree.BeginFrame();
            tree.Add(new("group", "", UiAccessibilityRole.Pane, "Group", new(0, 0, 300, 100), true, false, true,
                Focusable : false, ValuePattern : false));
            tree.Add(new("one", "group", UiAccessibilityRole.Button, "One", new(x, 0, 100, 30), enabled, offscreen, one,
                Focusable : focusable, ValuePattern : false));
            if (includeTwo) tree.Add(new("two", "group", UiAccessibilityRole.Button, "Two", new(100, 0, 100, 30), true, false, two, ValuePattern : false));
            tree.EndFrame();
        }
        Frame();
        check(events.Count == 0 && tree.Root.Focus is null, "a nonfocusable grouping flag does not invent an eligible focus target");
        Frame(one : true);
        check(events.Count == 1 && events[0].Previous is null && events[0].Current?.Node.Id == "one",
            "the first eligible focused element emits a committed acquisition");
        events.Clear();
        Frame(one : true, x : 20);
        check(events.Count == 0, "moving focused geometry does not repeat a focus-change event");
        Frame(two : true);
        check(events.Count == 1 && events[0].Previous?.Node.Id == "one" && events[0].Current?.Node.Id == "two" && published,
            "focus transfer carries immutable previous/current elements after publication");
        events.Clear();
        Frame(one : true, enabled : false);
        check(events.Count == 1 && events[0].Current is null && tree.Root.Focus is null,
            "disabled logical focus cannot become the eligible published focus");
        events.Clear();
        Frame(one : true, offscreen : true);
        Frame(one : true, focusable : false);
        check(events.Count == 0 && tree.Root.Focus is null, "offscreen and nonfocusable flags cannot fabricate a focus acquisition");
        var revision = tree.Revision;
        var duplicateRejected = false;
        try { Frame(one : true, two : true); }
        catch (InvalidOperationException) { duplicateRejected = true; tree.AbortFrame(); }
        check(duplicateRejected && tree.Revision == revision && events.Count == 0,
            "two eligible focus targets fail before publication or notifications");
        Frame(two : true);
        var oldTwo = events[^1].Current!.Value;
        events.Clear();
        Frame(includeTwo : false);
        check(events.Count == 1 && events[0].Previous == oldTwo && events[0].Current is null && !tree.TryGet("two", out _),
            "removing focused content reports logical focus loss without requiring a live old element");
        events.Clear();
        Frame(two : true);
        check(events.Count == 1 && events[0].Current!.Value.RuntimeId != oldTwo.RuntimeId,
            "a restored focus key has a new runtime identity");
        var reentry = false;
        var disposal = false;
        void Reenter(UiAccessibilityFocusChange change)
        {
            try { tree.BeginFrame(); }
            catch (InvalidOperationException) { reentry = true; }
            try { tree.Dispose(); }
            catch (InvalidOperationException) { disposal = true; }
        }
        tree.FocusChanged += Reenter;
        Frame(one : true);
        tree.FocusChanged -= Reenter;
        check(reentry && disposal && !tree.IsDisposed, "focus callbacks reject publication and disposal reentry");
        events.Clear();
        tree.BeginFrame();
        tree.Add(new("aborted", "", UiAccessibilityRole.Button, "Aborted", new(0, 0, 10, 10), true, false, true));
        tree.AbortFrame();
        Frame(one : true);
        check(events.Count == 0, "aborted staging cannot leak a pending focus acquisition");

        using var quiet = new UiAccessibilityTree();
        var count = 0;
        quiet.FocusChanged += _ => count++;
        var stable = new UiAccessibilityNode("stable", "", UiAccessibilityRole.Button, "Stable", new(0, 0, 20, 20), true, false, true);
        void Quiet() { quiet.BeginFrame(); quiet.Add(stable); quiet.EndFrame(); }
        for (var i = 0; i < 32; i++) Quiet();
        var initial = count;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 32; i++) Quiet();
        check(count == initial && GC.GetAllocatedBytesForCurrentThread() == before,
            "unchanged focused publication allocates zero bytes after warmup with a focus observer");

        using var host = new WindowsAccessibility(0);
        host.Tree.BeginFrame(); host.Tree.Add(stable); host.Tree.EndFrame();
        host.Find("stable")!.GetPropertyValue(30008, out var value);
        check(value.Type == 11 && value.Data.Boolean == 0, "headless logical focus does not claim native keyboard focus");
        host.Root.GetFocus(out var pointer);
        check(pointer == 0, "an unfocused native fragment returns no global focus target");
        CheckImmediate(check);
    }

    private static void CheckImmediate(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var selection = new UiState<string?>("alpha");
        using var tabs = Fluent.TabList("Tabs", selection.Binding, new UiTabOption[] { new("alpha", "Alpha"), new("beta", "Beta") });
        using var after = Fluent.Button("After");
        var screen = new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); tabs.Render(ui); after.Render(ui); });
        void Frame()
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        Frame();
        tree.TryGet(tree.Root.First!, out var group);
        tree.TryGet(group.FirstChild!, out var alpha);
        tree.TryGet(group.Next!, out var button);
        void Focus(UiAccessibilityElement element)
        {
            tree.RequestAction(element.Node.Id, element.RuntimeId, UiAccessibilityAction.Focus, wait : false);
            Frame();
        }
        Focus(alpha);
        Focus(button);
        tree.TryGet(alpha.Node.Id, out var old);
        tree.TryGet(button.Node.Id, out var current);
        check(tree.Root.Focus == current.Node.Id && current.Node.Focused && !old.Node.Focused,
            "a later control's focus request clears the tab captured earlier in the same frame");
        Focus(alpha);
        tree.TryGet(button.Node.Id, out var previous);
        check(tree.Root.Focus == alpha.Node.Id && !previous.Node.Focused,
            "returning to the composite resolves its actual tab after final frame focus is known");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
