using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class MenuCheckboxTests
{
    internal static void Run(Action<bool, string> check)
    {
        Run<UiThemeMenuPresentation>(check);
        Run<UiFluentMenuPresentation>(check);
    }

    private static void Run<TPresentation>(Action<bool, string> check) where TPresentation : IUiMenuPresentation
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var native = new WindowsAccessibility(0);
        var tree = native.Tree;
        context.SetAccessibilityTree(tree);
        ControlFontFixture.Configure(context);
        var value = false;
        var reads = 0;
        var writes = 0;
        var binding = new UiBinding<bool>(() => { reads++; return value; }, next => { writes++; value = next; });
        var calls = new List<(string Id, bool Value)>();
        using var menu = new UiMenu<TPresentation>("Options", [UiMenuOption.Checkbox("show", "Show", binding),
            UiMenuOption.Checkbox("peer", "Same state", binding), UiMenuOption.Checkbox("disabled", "Disabled", binding, false),
            new("finish", "Finish")], id => calls.Add((id, value)));
        var changes = new List<UiAccessibilityToggleChange>();
        tree.ToggleChanged += change =>
        {
            check(tree.TryGet(change.Element.Node.Id, out var published) && published.Node.Checked == change.Element.Node.Checked,
                "toggle notification observes final committed menu state outside locks");
            changes.Add(change);
        };
        void Frame(UiKeyEvent[]? keys = null, UiVector2? pointer = null, bool down = false, bool pressed = false, bool released = false)
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), new(pointer ?? new(-100, -100), down, false,
                pressed, released, false, false, 0, 0, keys ?? [], [], default, default), new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 8); menu.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role = UiAccessibilityRole.MenuItem)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var entry))
                {
                    if (entry.Node.Name == name && entry.Node.Role == role) found = entry;
                    Visit(entry.FirstChild);
                    id = entry.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Missing checkbox menu element : " + name);
        }
        void Key(UiKey key, bool down = true) => Frame([new(key, down, KeyModifiers.None)]);
        void Request(string name, UiAccessibilityAction action)
        {
            var entry = Find(name);
            check(tree.RequestAction(entry.Node.Id, entry.RuntimeId, action, wait : false) == UiAccessibilityResult.Success,
                "actual menu checkbox accepts " + action);
            Frame();
        }
        Frame();
        check(reads == 0, "closed menu does not read borrowed checkbox bindings");
        Key(UiKey.Tab);
        Key(UiKey.DownArrow);
        check(menu.IsOpen && reads == 1 && writes == 0 && Find("Show").Node.Checked == false && changes.Count == 0,
            "checkbox menu snapshots each shared binding once without initial fabricated changes");
        var entry = Find("Show");
        var provider = native.Find(entry.Node.Id)!;
        provider.GetPatternProvider(10015, out var pattern);
        try
        {
            provider.GetPropertyValue(30041, out var available);
            provider.GetPropertyValue(30086, out var state);
            check(pattern != 0 && available.Data.Boolean != 0 && state.Data.Integer == 0
                && provider.GetToggleState(out var actual) == 0 && actual == 0,
                "actual checkbox menu exposes native Toggle pattern and off state");
        }
        finally { if (pattern != 0) Marshal.Release(pattern); }
        Key(UiKey.Space);
        check(!value && writes == 0, "checkbox Space down does not change state");
        Key(UiKey.Space, false);
        check(menu.IsOpen && value && writes == 1 && calls.SequenceEqual([("show", true)])
            && Find("Show").Node.Checked == true && Find("Same state").Node.Checked == true
            && Find("Disabled").Node.Checked == true && changes.Count == 3,
            "checkbox Space release commits shared peer states before painting and callback, and keeps the menu open");
        var revision = tree.Revision;
        var changeCount = changes.Count;
        var previousReads = reads;
        Frame();
        check(tree.Revision == revision && changes.Count == changeCount && reads == previousReads + 1
            && context.State.NextScheduledFrameTimeSeconds is null && writes == 1,
            "unchanged checkbox replay reads once, publishes no duplicate change and schedules no animation");
        Request("Same state", UiAccessibilityAction.Toggle);
        check(!value && writes == 2 && calls[^1] == ("peer", false) && menu.IsOpen,
            "queued Toggle uses the same bound state and callback as keyboard activation");
        var point = Find("Show").Node.Bounds;
        var center = new UiVector2(point.X + point.Width / 2, point.Y + point.Height / 2);
        Frame(pointer : center, down : true, pressed : true);
        Frame(pointer : center, released : true);
        check(value && writes == 3 && calls.Count == 3 && menu.IsOpen,
            "actual pointer release toggles once and leaves the menu open");
        var disabled = Find("Disabled");
        check(tree.RequestAction(disabled.Node.Id, disabled.RuntimeId, UiAccessibilityAction.Toggle, wait : false) == UiAccessibilityResult.Disabled,
            "disabled checkbox rejects Toggle before queueing");
        var finish = Find("Finish");
        check(tree.RequestAction(finish.Node.Id, finish.RuntimeId, UiAccessibilityAction.Toggle, wait : false) == UiAccessibilityResult.InvalidOperation,
            "ordinary menu action rejects unsupported Toggle");
        value = false;
        Frame();
        check(Find("Show").Node.Checked == false && writes == 3 && calls.Count == 3,
            "external binding changes update the open menu without invoking a callback or setter");
        Request("Show", UiAccessibilityAction.Invoke);
        check(value && writes == 4 && menu.IsOpen, "Invoke and Toggle share checkbox activation semantics");
        var retired = Find("Show");
        Key(UiKey.Escape);
        Frame();
        check(!menu.IsOpen && provider.GetToggleState(out _) == unchecked((int)0x80040201)
            && tree.RequestAction(retired.Node.Id, retired.RuntimeId, UiAccessibilityAction.Toggle, wait : false) == UiAccessibilityResult.Unavailable,
            "closed checkbox menu rejects retained provider and queued mutations");
        Key(UiKey.DownArrow);
        check(Find("Show").Node.Checked == true && Find("Show").RuntimeId != retired.RuntimeId,
            "checkbox binding survives close and reopen with a fresh item runtime identity");
        var reopened = Find("Show");
        tree.RequestAction(reopened.Node.Id, reopened.RuntimeId, UiAccessibilityAction.Toggle, wait : false);
        tree.RequestAction(reopened.Node.Id, reopened.RuntimeId, UiAccessibilityAction.Toggle, wait : false);
        Frame();
        check(!value && writes == 5 && menu.IsOpen, "first queued checkbox activation commits once without consuming the next activation");
        Frame();
        check(value && writes == 6 && calls.Count == 6, "second queued checkbox activation commits on the next requested frame");
        var capabilityProvider = native.Find(reopened.Node.Id)!;
        var beforeCapability = changes.Count;
        menu.SetItems([new("show", "Show"), new("finish", "Finish")]);
        Frame();
        capabilityProvider.GetPatternProvider(10015, out var withdrawn);
        check(Find("Show").RuntimeId == reopened.RuntimeId && Find("Show").Node.Checked is null && withdrawn == 0
            && changes.Count == beforeCapability + 1 && changes[^1].Previous == true && changes[^1].Element.Node.Checked is null
            && capabilityProvider.GetToggleState(out _) == unchecked((int)0x80131509),
            "replacing a checkbox with an ordinary row preserves identity and withdraws Toggle with one capability change");
        Request("Finish", UiAccessibilityAction.Invoke);
        Frame();
        check(!menu.IsOpen && calls[^1].Id == "finish" && writes == 6,
            "ordinary action in the same checkbox menu still invokes and closes");
        menu.Dispose();
        binding.Value = false;
        check(!value && writes == 7, "menu disposal does not take ownership of the caller's binding");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
