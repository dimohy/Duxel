using Duxel.Core;

internal static class LongMenuRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Func<string, Action<UiImmediateContext>, byte[]> captureGeometry, Action<UiInputState> setInput,
        Action<bool, string> check, UiAccessibilityTree tree)
    {
        var calls = new List<string>();
        using var menu = Fluent.Menu("Long actions",
            [new("apple", "Apple"), .. Enumerable.Range(0, 20).Select(i => new UiMenuOption("item-" + i, "Item " + i)), new("last", "Zulu")],
            calls.Add, new UiMenuOptions(138, 32, 5, ItemSpacing : 2, MaximumWidth : 300) { MaximumHeight = 138 });
        void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 8);
            menu.Render(ui);
        }
        UiInputState Input(UiVector2? pointer = null, bool down = false, bool pressed = false, bool released = false) =>
            new(pointer ?? new(-100, -100), down, false, pressed, released, false, false, 0, 0, [], [], default, default);
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var entry))
                {
                    if (entry.Node.Name == name) result = entry;
                    Visit(entry.FirstChild);
                    id = entry.Next;
                }
            }
            Visit(tree.Root.First);
            return result ?? throw new InvalidOperationException("Missing long-menu item : " + name);
        }
        setInput(Input());
        capture("closed", Render);
        var trigger = Find("Long actions");
        check(tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "GPU long menu opens through its actual queued trigger");
        var first = capture("first", Render);
        check(!Find("Apple").Node.Offscreen && Find("Zulu").Node.Offscreen, "GPU long-menu surface clips its later rows");
        var hidden = Find("Zulu");
        check(tree.RequestAction(hidden.Node.Id, hidden.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Success,
            "GPU hidden MenuItem accepts its shared ScrollItem request");
        capture("reveal-plan", Render);
        var revealed = capture("reveal-applied", Render);
        capture("reveal-settle", Render);
        check(!Find("Zulu").Node.Offscreen && Find("Zulu").RuntimeId == hidden.RuntimeId && !revealed.AsSpan().SequenceEqual(first),
            "GPU actual ScrollItem request reveals and paints the retained last menu row");
        var menuEntry = tree.TryGet(Find("Zulu").Node.ParentId, out var parent) ? parent
            : throw new InvalidOperationException("Long menu scroll parent missing");
        tree.RequestScroll(menuEntry.Node.Id, menuEntry.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 0), wait : false);
        capture("scroll-first", Render);
        setInput(Input() with { CharEvents = new UiCharEvent[] { new('z') } });
        var last = capture("type-z", Render);
        check(Find("Zulu").Node.Focused && !Find("Zulu").Node.Offscreen && Find("Apple").Node.Offscreen
            && !last.AsSpan().SequenceEqual(first), "GPU character navigation reveals and paints the last real menu row");
        setInput(Input());
        var settled = captureGeometry("settled", Render);
        check(captureGeometry("replay", Render).AsSpan().SequenceEqual(settled), "GPU long-menu geometry survives acquired-image reuse at the same animation time");
        var popup = Find("Zulu").Node.Bounds;
        setInput(Input(new(popup.X + 20, popup.Y + 10)) with { MouseWheel = 30 });
        capture("wheel-first", Render);
        check(!Find("Apple").Node.Offscreen && Find("Zulu").Node.Offscreen, "GPU wheel returns the same long menu to its first rows");
        menu.Options = menu.Options with { RightToLeft = true };
        setInput(Input());
        capture("rtl-first", Render);
        setInput(Input() with { KeyEvents = new UiKeyEvent[] { new(UiKey.End, true, 0) } });
        capture("rtl-end", Render);
        check(Find("Zulu").Node.Focused && !Find("Zulu").Node.Offscreen, "GPU RTL long menu preserves keyboard reveal");
        setInput(Input() with { KeyEvents = new UiKeyEvent[] { new(UiKey.Enter, true, 0) } });
        capture("invoke", Render);
        check(!menu.IsOpen && calls.SequenceEqual(["last"]), "GPU revealed menu invocation reaches its exact real callback");
    }
}
