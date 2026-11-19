using Duxel.Core;

internal static class TabScrollRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, UiAccessibilityTree tree, UiContext context, int width, UiFluentColorScheme scheme)
    {
        var value = new UiState<string?>("0");
        var items = Enumerable.Range(0, 12).Select(i => new UiTabOption(i.ToString(), "Page " + i)).ToArray();
        var tabs = Fluent.TabList("GPU scroll tabs", value.Binding, items,
            new(Size: UiTabListSize.Small)
            {
                Scroll = new(PreviousLabel: "Previous hidden tabs", NextLabel: "Next hidden tabs")
            });
        using var box = Dux.SizedBox(tabs, width: 260, height: 28);
        var idle = new UiInputState(new(100, 12), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            new UiControlContext(ui).Frame(new(0, 0, width, 240), scheme == UiFluentColorScheme.Light ? new UiColor(255, 255, 255) : new(32, 32, 32), default, 0, 0);
            box.Render(ui);
            foreach (var item in items)
                tabs.RenderPanel(ui, item.Value, body => body.Text("Body : " + item.Label));
        }
        List<UiAccessibilityElement> Nodes()
        {
            var nodes = new List<UiAccessibilityElement>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                {
                    nodes.Add(item);
                    Visit(item.FirstChild);
                    id = item.Next;
                }
            }
            Visit(tree.Root.First);
            return nodes;
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => Nodes().Single(e => e.Node.Name == name && e.Node.Role == role);
        bool Has(string name) => Nodes().Any(e => e.Node.Name == name);
        void Invoke(string name, UiAccessibilityRole role)
        {
            var item = Find(name, role);
            check(tree.RequestAction(item.Node.Id, item.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
                "GPU directional tab menu accepts Invoke : " + name);
        }
        setInput(idle);
        capture("idle", Draw);
        var bodyY = Find("Page 0", UiAccessibilityRole.TabPanel).Node.Bounds.Y;
        check(!Has("Previous hidden tabs") && Has("Next hidden tabs") && !tabs.IsScrollBarVisible,
            "GPU initial tab strip has only its trailing ellipsis and no scrollbar");
        setInput(idle with
        {
            MousePosition = new(180, 12),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        capture("press", Draw);
        setInput(idle with
        {
            LeftMouseDown = true
        });
        capture("drag-start", Draw);
        context.State.AdvanceTime(.05f);
        capture("fade-in-mid", Draw);
        check(tabs.ScrollOffset == 80 && Has("Previous hidden tabs") && Has("Next hidden tabs")
            && tabs.ScrollBarOpacity > 0 && tabs.ScrollBarOpacity < 1,
            "GPU dragged headers expose both ellipses with an intermediate fade-in opacity");
        context.State.AdvanceTime(.2f);
        var shown = capture("drag-held", Draw);
        check(tabs.ScrollBarOpacity == 1 && context.State.NextScheduledFrameTimeSeconds is null && !context.State.HasActiveAnimations,
            "GPU stationary held drag keeps the scrollbar visible without periodic frames");
        setInput(idle with
        {
            LeftMouseReleased = true
        });
        capture("release", Draw);
        check(value.Value == "0" && Find("Page 0", UiAccessibilityRole.TabPanel).Node.Bounds.Y == bodyY,
            "GPU drag release preserves the original panel and reserves no scrollbar layout area");
        setInput(idle);
        context.State.AdvanceTime(.39f);
        capture("fade-out-start", Draw);
        context.State.AdvanceTime(.06f);
        var midway = capture("fade-out-mid", Draw);
        check(tabs.ScrollBarOpacity > 0 && tabs.ScrollBarOpacity < 1,
            "GPU scrollbar exit paints an intermediate opacity");
        context.State.AdvanceTime(.2f);
        var hidden = capture("fade-out-end", Draw);
        long Distance(byte[] pixels)
        {
            long result = 0;
            for (var y = 25; y < 28; y++)
                for (var x = 0; x < 260; x++)
                    for (var channel = 0; channel < 3; channel++)
                    {
                        var index = (y * width + x) * 4 + channel;
                        result += Math.Abs(pixels[index] - hidden[index]);
                    }
            return result;
        }
        check(Distance(shown) > Distance(midway) && Distance(midway) > 0,
            "GPU scrollbar pixels visibly fade toward the untouched underlying header");
        check(!tabs.IsScrollBarVisible && context.State.NextScheduledFrameTimeSeconds is null && !context.State.HasActiveAnimations
            && hidden.AsSpan().SequenceEqual(capture("idle-replay", Draw)),
            "GPU settled tab scrollbar returns to exact static replay and event wait");
        setInput(idle with
        {
            MouseWheel = -100
        });
        capture("trailing-end", Draw);
        check(tabs.ScrollOffset == tabs.MaximumScrollOffset && Has("Previous hidden tabs") && !Has("Next hidden tabs"),
            "GPU trailing endpoint displays only its leading ellipsis");
        setInput(idle);
        var offset = tabs.ScrollOffset;
        Invoke("Previous hidden tabs", UiAccessibilityRole.Button);
        capture("leading-menu", Draw);
        check(tabs.ScrollOffset == offset && Find("Page 0", UiAccessibilityRole.MenuItem).Node.Enabled,
            "GPU leading ellipsis opens a context menu without moving the header");
        Invoke("Page 0", UiAccessibilityRole.MenuItem);
        capture("leading-menu-select", Draw);
        capture("leading-menu-settled", Draw);
        check(tabs.ScrollOffset == 0 && value.Value == "0", "GPU leading menu reveals the selected original tab");
        Invoke("Next hidden tabs", UiAccessibilityRole.Button);
        capture("trailing-menu", Draw);
        var choice = Nodes().First(e => e.Node.Role == UiAccessibilityRole.MenuItem && !e.Node.Offscreen && e.Node.Enabled);
        var chosen = items.Single(item => item.Label == choice.Node.Name);
        Invoke(chosen.Label, UiAccessibilityRole.MenuItem);
        capture("trailing-menu-select", Draw);
        capture("trailing-menu-settled", Draw);
        check(value.Value == chosen.Value && !Find(chosen.Label, UiAccessibilityRole.Tab).Node.Offscreen
            && Find(chosen.Label, UiAccessibilityRole.TabPanel).Node.Bounds.Y == bodyY,
            "GPU trailing menu updates the real selection binding and its original panel");
    }
}
