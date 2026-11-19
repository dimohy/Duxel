using Duxel.Core;
using Duxel.Platform.Windows;

internal static class MenuScrollTests
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
        var calls = new List<string>();
        var menu = new UiMenu<TPresentation>("Scroll actions",
            [new("first", "First"), .. Enumerable.Range(0, 24).Select(i => new UiMenuOption("item-" + i, "Item " + i)), new("last", "Last")],
            calls.Add, TPresentation.DefaultOptions with { MaximumHeight = 138 });
        using var outer = Dux.ScrollView(Dux.Column(new(Spacing : 0, MainAxisSize : UiMainAxisSize.Min), menu, Dux.SizedBox(width : 200, height : 600)),
            new(Width : 300, Height : 80));
        var scrollChanges = new List<UiAccessibilityScrollChange>();
        tree.ScrollChanged += scrollChanges.Add;
        void Frame()
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 8); outer.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var entry))
                {
                    if (entry.Node.Name == name && entry.Node.Role == role) result = entry;
                    Visit(entry.FirstChild);
                    id = entry.Next;
                }
            }
            Visit(tree.Root.First);
            return result ?? throw new InvalidOperationException("Missing menu scroll entry : " + name);
        }
        Frame();
        var trigger = Find("Scroll actions", UiAccessibilityRole.Button);
        tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame();
        var surface = Find("Scroll actions", UiAccessibilityRole.Menu);
        var retained = native.Find(surface.Node.Id)!;
        var last = Find("Last", UiAccessibilityRole.MenuItem);
        check(surface.Node.Scroll is { VerticallyScrollable : true, HorizontallyScrollable : false }
            && last.Node.CanScrollIntoView && last.Node.Offscreen,
            "actual long menu publishes Scroll and hidden MenuItem ScrollItem capabilities : " + typeof(TPresentation).Name);
        check(tree.RequestScroll(surface.Node.Id, surface.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 100), wait : false) == UiAccessibilityResult.Success,
            "actual menu accepts a queued vertical percentage scroll");
        Frame();
        retained.GetPropertyValue(30055, out var percentage);
        check(Find("Scroll actions", UiAccessibilityRole.Menu).Node.Scroll!.Value.VerticalScrollPercent == 100
            && !Find("Last", UiAccessibilityRole.MenuItem).Node.Offscreen && percentage.Type == 5 && percentage.Data.Real == 100,
            "actual menu publishes its final requested percentage and visible rows atomically");
        var notifications = scrollChanges.Count;
        Frame();
        check(scrollChanges.Count == notifications, "unchanged menu replay suppresses scroll property notifications");
        tree.RequestScroll(surface.Node.Id, surface.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 0), wait : false);
        Frame();
        check(tree.RequestAction(last.Node.Id, last.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Success,
            "an offscreen menu item accepts its real ScrollIntoView request");
        Frame();
        Frame();
        Frame();
        check(!Find("Last", UiAccessibilityRole.MenuItem).Node.Offscreen && outer.Offset.Y == 0
            && Find("Last", UiAccessibilityRole.MenuItem).RuntimeId == last.RuntimeId,
            "floating menu reveal preserves row identity and does not scroll its logical outer owner");
        tree.RequestScroll(surface.Node.Id, surface.RuntimeId,
            UiAccessibilityScrollCommand.ByAmount(UiAccessibilityScrollAmount.NoAmount, UiAccessibilityScrollAmount.SmallDecrement), wait : false);
        Frame();
        check(Find("Scroll actions", UiAccessibilityRole.Menu).Node.Scroll!.Value.VerticalScrollPercent < 100,
            "actual menu applies the shared small-decrement scroll command");
        tree.RequestAction(last.Node.Id, last.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false);
        Frame(); Frame(); Frame();
        tree.RequestAction(last.Node.Id, last.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame(); Frame();
        check(!menu.IsOpen && calls.SequenceEqual(["last"])
            && tree.RequestScroll(surface.Node.Id, surface.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 0), wait : false) == UiAccessibilityResult.Unavailable,
            "invoking the revealed actual row retires the menu and its stale scroll provider");
        check(retained.GetVerticalScrollPercent(out _) == UiaNative.Unavailable,
            "retired native menu provider getters return element unavailable before UIA client projection");
        trigger = Find("Scroll actions", UiAccessibilityRole.Button);
        tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame();
        var fitting = Find("Scroll actions", UiAccessibilityRole.Menu);
        menu.SetItems([new("first", "First"), new("last", "Last")]);
        Frame();
        var fitSurface = Find("Scroll actions", UiAccessibilityRole.Menu);
        var fitProvider = native.Find(fitSurface.Node.Id)!;
        fitProvider.GetPropertyValue(30055, out var fitPercent);
        fitProvider.GetPropertyValue(30056, out var fitView);
        check(fitSurface.RuntimeId == fitting.RuntimeId && fitSurface.Node.Scroll is null
            && !Find("Last", UiAccessibilityRole.MenuItem).Node.CanScrollIntoView,
            "a fitting popup withdraws Scroll and does not inherit ScrollItem from its independent outer owner");
        check(fitPercent.Type == 5 && fitPercent.Data.Real == -1 && fitView.Type == 5 && fitView.Data.Real == 100
            && scrollChanges.Any(change => change.Element.Node.Id == fitSurface.Node.Id && change.Previous is not null && change.Element.Node.Scroll is null),
            "retained fitting menu reports canonical native no-scroll values and a capability withdrawal");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
