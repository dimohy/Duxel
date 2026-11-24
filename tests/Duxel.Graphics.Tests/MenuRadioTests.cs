using System.Runtime.InteropServices;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class MenuRadioTests
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
        string? sorting = "one";
        var reads = 0;
        var writes = 0;
        var binding = new UiBinding<string?>(() => { reads++; return sorting; }, value => { writes++; sorting = value; });
        var sort = new UiMenuRadioGroup("Sort", binding);
        var viewState = new UiState<string?>("compact");
        var view = new UiMenuRadioGroup("View", viewState);
        var calls = new List<(string Id, string? Value)>();
        UiMenuOption[] items = [UiMenuOption.Radio("one", "One", sort), UiMenuOption.Radio("two", "Two", sort),
            UiMenuOption.Radio("disabled", "Disabled", sort, false), UiMenuOption.Radio("compact", "Compact", view),
            UiMenuOption.Radio("wide", "Wide", view), new("finish", "Finish")];
        using var menu = new UiMenu<TPresentation>("Radio options", items, id => calls.Add((id, sorting)));
        var changes = new List<UiAccessibilitySelectionChange>();
        tree.SelectionChanged += change =>
        {
            check(tree.TryGet(change.Element.Node.Id, out var committed) && committed == change.Element,
                "radio notification observes the final committed selection snapshot");
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
            return found ?? throw new InvalidOperationException("Missing radio menu element : " + name);
        }
        void Key(UiKey key, bool down = true) => Frame([new(key, down, KeyModifiers.None)]);
        void Open()
        {
            Frame();
            var trigger = Find("Radio options", UiAccessibilityRole.Button);
            tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Expand, wait : false);
            Frame();
        }
        void Request(string name, UiAccessibilityAction action)
        {
            var item = Find(name);
            check(tree.RequestAction(item.Node.Id, item.RuntimeId, action, wait : false) == UiAccessibilityResult.Success,
                "radio menu accepts " + action);
            Frame();
        }
        Frame();
        check(reads == 0, "closed radio menu does not read selection bindings");
        Open();
        var one = Find("One");
        var two = Find("Two");
        var group = Find("Sort", UiAccessibilityRole.MenuGroup);
        check(reads == 1 && writes == 0 && one.Node.Selected == true && two.Node.Selected == false
            && Find("Compact").Node.Selected == true && one.Node.ParentId == group.Node.Id
            && Find("Compact").Node.ParentId != group.Node.Id && changes.Count == 0,
            "radio groups snapshot once and expose independent named single-selection containers");
        var owner = native.Find(group.Node.Id)!;
        var oneProvider = native.Find(one.Node.Id)!;
        var twoProvider = native.Find(two.Node.Id)!;
        owner.GetPatternProvider(10001, out var selectionPattern);
        twoProvider.GetPatternProvider(10010, out var itemPattern);
        try
        {
            owner.GetCanSelectMultiple(out var multiple);
            owner.GetIsSelectionRequired(out var required);
            twoProvider.GetPatternProvider(10015, out var toggle);
            check(selectionPattern != 0 && itemPattern != 0 && multiple == 0 && required == 1 && toggle == 0
                && twoProvider.AddToSelection() == unchecked((int)0x80131509),
                "native radio groups expose Selection/SelectionItem without Toggle and reject adding a second exclusive selection");
        }
        finally { if (selectionPattern != 0) Marshal.Release(selectionPattern); if (itemPattern != 0) Marshal.Release(itemPattern); }
        check(tree.RequestAction(one.Node.Id, one.RuntimeId, UiAccessibilityAction.Deselect, wait : false) == UiAccessibilityResult.InvalidOperation,
            "selected radio item cannot be cleared through Deselect");
        Request("Two", UiAccessibilityAction.Deselect);
        check(menu.IsOpen && sorting == "one" && writes == 0 && calls.Count == 0, "deselecting an unselected radio is a no-op");
        Request("Two", UiAccessibilityAction.Select);
        check(!menu.IsOpen && sorting == "two" && writes == 1 && calls.SequenceEqual([("two", (string?)"two")])
            && Find("One").Node.Selected == false && Find("Two").Node.Selected == true && Find("Compact").Node.Selected == true,
            "Select replaces only its own group before callback and final publication, then closes the menu");
        check(changes.Count == 2 && changes.TrueForAll(change => change.DismissingMenu)
            && changes.Count(change => change.Event == UiAccessibilitySelectionEventKind.Selected) == 1,
            "exclusive replacement emits both changed properties and one final selected event");
        Frame();
        check(oneProvider.GetIsSelected(out _) == unchecked((int)0x80040201), "closed radio item provider becomes unavailable");
        Open();
        Request("Two", UiAccessibilityAction.Invoke);
        check(!menu.IsOpen && writes == 1 && calls.Count == 2,
            "invoking an already selected radio closes and calls back without a redundant binding write");
        Open();
        var wide = Find("Wide").Node.Bounds;
        var center = new UiVector2(wide.X + wide.Width / 2, wide.Y + wide.Height / 2);
        Frame(pointer : center, down : true, pressed : true);
        Frame(pointer : center, released : true);
        check(!menu.IsOpen && viewState.Value == "wide" && sorting == "two", "radio pointer release replaces the independent second group and closes");
        Open();
        Key(UiKey.Home);
        Key(UiKey.Space);
        check(menu.IsOpen && writes == 1, "radio Space down does not invoke");
        Key(UiKey.Space, false);
        check(!menu.IsOpen && sorting == "one" && writes == 2, "radio Space release changes selection and dismisses");
        Open();
        var beforeBindingChange = changes.Count;
        sorting = null;
        Frame();
        check(Find("One").Node.Selected == false && Find("Two").Node.Selected == false
            && !Find("Sort", UiAccessibilityRole.MenuGroup).Node.SelectionRequired && writes == 2,
            "external empty state clears marks and updates the group policy without setter calls");
        check(changes.Skip(beforeBindingChange).Any() && changes.Skip(beforeBindingChange).All(change => !change.DismissingMenu),
            "binding changes in an open menu do not defer native selection events");
        var before = changes.Count;
        Frame();
        check(changes.Count == before && context.State.NextScheduledFrameTimeSeconds is null,
            "unchanged radio replay publishes no duplicate notifications or animation deadline");
        var disabled = Find("Disabled");
        check(tree.RequestAction(disabled.Node.Id, disabled.RuntimeId, UiAccessibilityAction.Select, wait : false) == UiAccessibilityResult.Disabled,
            "disabled radio rejects selection before queueing");
        var rejected = 0;
        void Reject(UiMenuOption[] invalid)
        {
            try { menu.SetItems(invalid); }
            catch (ArgumentException) { rejected++; }
        }
        Reject([items[0], items[5], items[1]]);
        Reject([items[0], UiMenuOption.Radio("other", "Other", new("Sort", new UiState<string?>(null)))]);
        Reject([items[0], UiMenuOption.Radio("other", "Other", new("Other", binding))]);
        check(rejected == 3 && menu.Items.Length == 6, "noncontiguous, duplicate-name and shared-binding radio groups fail before replacing valid items");
        Request("One", UiAccessibilityAction.Select);
        Open();
        var beforeRemoval = changes.Count;
        menu.SetItems([items[1], items[2], items[5]]);
        Frame();
        check(!Find("Sort", UiAccessibilityRole.MenuGroup).Node.SelectionRequired
            && changes.Skip(beforeRemoval).Any(change => change.Event == UiAccessibilitySelectionEventKind.Invalidated),
            "removing the selected radio invalidates the surviving group and updates its required-selection policy");
        menu.SetItems([items[0], .. Enumerable.Range(0, 12).Select(index => UiMenuOption.Radio("long-" + index, "Long " + index, sort))]);
        menu.Options = menu.Options with { MaximumHeight = 138 };
        Frame();
        var hidden = Find("Long 11");
        check(hidden.Node.Offscreen && hidden.Node.CanScrollIntoView
            && tree.RequestAction(hidden.Node.Id, hidden.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Success,
            "radio items retain ScrollItem through their named selection-group ancestor");
        Frame(); Frame();
        check(!Find("Long 11").Node.Offscreen && Find("Long 11").RuntimeId == hidden.RuntimeId && menu.IsOpen,
            "radio ScrollItem reveals the retained row without selecting or dismissing the menu");
        Request("Long 11", UiAccessibilityAction.Select);
        check(sorting == "long-11" && !menu.IsOpen, "revealed radio row uses the same exclusive selection and dismissal contract");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
