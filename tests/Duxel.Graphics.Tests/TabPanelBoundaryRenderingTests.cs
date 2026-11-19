using Duxel.Core;

internal static class TabPanelBoundaryRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, UiAccessibilityTree tree, UiState state, int width, int height)
    {
        var selection = new UiState<string?>("one");
        using var tabs = Fluent.TabList("Panel boundary", selection.Binding, new UiTabOption[] { new("one", "One") });
        var tall = false;
        void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 8);
            tabs.Render(ui);
            tabs.RenderPanel(ui, "one", body => body.Dummy(new(60, tall ? height * 2 : 60)));
        }
        UiAccessibilityElement Panel()
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var entry))
                {
                    if (entry.Node.Role == UiAccessibilityRole.TabPanel) found = entry;
                    Visit(entry.FirstChild);
                    id = entry.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Missing tab panel boundary");
        }
        setInput(new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default));
        foreach (var overflowing in new[] { false, true })
        {
            tall = overflowing;
            capture("layout-" + tall, Render);
            var panel = Panel();
            check(panel.Node.Bounds.X == 8 && panel.Node.Bounds.X + panel.Node.Bounds.Width == width - 8
                && panel.Node.Bounds.Y + panel.Node.Bounds.Height <= height - 8,
                "panel semantic bounds honor caller padding on right and bottom : " + tall);
            check(tree.RequestAction(panel.Node.Id, panel.RuntimeId, UiAccessibilityAction.Focus, wait : false) == UiAccessibilityResult.Success,
                "panel boundary test queues its actual focus : " + tall);
            var pixels = capture("focused-" + tall, Render);
            var rect = Panel().Node.Bounds;
            bool Ink(int x, int y)
            {
                var offset = (y * width + x) * 4;
                return pixels[offset + 2] > pixels[offset] + 20 && pixels[offset + 1] > pixels[offset] + 20;
            }
            var left = (int)rect.X;
            var right = (int)(rect.X + rect.Width) - 1;
            var top = (int)rect.Y;
            var bottom = (int)(rect.Y + rect.Height) - 1;
            check(Ink(left, (top + bottom) / 2) && Ink(right, (top + bottom) / 2)
                && Ink((left + right) / 2, top) && Ink((left + right) / 2, bottom),
                "actual GPU focus ring paints all four edges inside the visible panel : " + tall);
            check(!Ink(width - 1, (top + bottom) / 2) && !Ink((left + right) / 2, height - 1),
                "panel ring does not extend to the outer viewport edge : " + tall);
        }
        using var navigation = Fluent.Nav("Scrolled navigation", selection, new UiNavGroup[]
        {
            new("group", "Group", new UiNavItem[] { new("one", "One") })
        });
        foreach (var kind in new[] { "legacy", "fluent", "navigation" })
        {
            var viewportName = "Scrolled viewport " + kind;
            var role = kind == "navigation" ? UiAccessibilityRole.Pane : UiAccessibilityRole.TabPanel;
            void Body(UiImmediateContext ui)
            {
                ui.Dummy(new(40, 800));
                ui.Button("Visible scrolled action");
                ui.Dummy(new(40, 200));
            }
            void Scrolled(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 8);
                ui.SetCursorScreenPos(new(16, 16));
                ui.BeginChild(viewportName, new(300, 220));
                if (kind == "legacy")
                {
                    ui.BeginTabBar("Scrolled legacy tabs");
                    if (ui.BeginTabItem("One")) { Body(ui); ui.EndTabItem(); }
                    ui.EndTabBar();
                }
                else if (kind == "fluent")
                {
                    tabs.Render(ui);
                    tabs.RenderPanel(ui, "one", Body);
                }
                else navigation.RenderPage(ui, "one", Body, navigationVisible : false);
                ui.EndChild();
            }
            UiAccessibilityNode Node(string name, UiAccessibilityRole expectedRole) => tree.CaptureSnapshot().Elements
                .Single(element => element.Node.Name == name && element.Node.Role == expectedRole).Node;
            capture("scrolled-" + kind + "-initial", Scrolled);
            var viewport = Node(viewportName, UiAccessibilityRole.Pane);
            state.SetScrollY(viewport.Id, 800);
            capture("scrolled-" + kind + "-settle", Scrolled);
            capture("scrolled-" + kind, Scrolled);
            var button = Node("Visible scrolled action", UiAccessibilityRole.Button);
            var panel = Node("One", role);
            var rect = panel.Bounds;
            check(!button.Offscreen && !panel.Offscreen && rect.Width > 0 && rect.Height > 0,
                "scrolled region remains visible with its actual descendant : " + kind);
            check(rect.X <= button.Bounds.X && rect.Y <= button.Bounds.Y
                && rect.X + rect.Width >= button.Bounds.X + button.Bounds.Width
                && rect.Y + rect.Height >= button.Bounds.Y + button.Bounds.Height,
                "scrolled region contains all four edges of its visible descendant : " + kind);
            viewport = Node(viewportName, UiAccessibilityRole.Pane);
            check(rect.X >= viewport.Bounds.X && rect.Y >= viewport.Bounds.Y
                && rect.X + rect.Width <= viewport.Bounds.X + viewport.Bounds.Width
                && rect.Y + rect.Height <= viewport.Bounds.Y + viewport.Bounds.Height,
                "scrolled region stays clipped to its parent viewport : " + kind);
            if (kind != "legacy")
            {
                var element = tree.CaptureSnapshot().Elements.Single(entry => entry.Node.Id == panel.Id);
                check(tree.RequestAction(panel.Id, element.RuntimeId, UiAccessibilityAction.Focus, wait : false) == UiAccessibilityResult.Success,
                    "scrolled region accepts keyboard focus : " + kind);
                var pixels = capture("scrolled-" + kind + "-focused", Scrolled);
                rect = Node("One", role).Bounds;
                bool Ink(int x, int y)
                {
                    var at = (y * width + x) * 4;
                    return pixels[at + 2] > pixels[at] + 20 && pixels[at + 1] > pixels[at] + 20;
                }
                var left = (int)rect.X;
                var right = (int)(rect.X + rect.Width) - 1;
                var top = (int)rect.Y;
                var bottom = (int)(rect.Y + rect.Height) - 1;
                check(Ink(left, (top + bottom) / 2) && Ink(right, (top + bottom) / 2)
                    && Ink((left + right) / 2, top) && Ink((left + right) / 2, bottom),
                    "scrolled GPU focus ring retains all four visible edges : " + kind);
            }
            state.SetScrollY(viewport.Id, 0);
        }
    }
}
