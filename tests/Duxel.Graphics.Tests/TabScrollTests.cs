using Duxel.Core;

internal static class TabScrollTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var value = new UiState<string?>("0");
        var items = Enumerable.Range(0, 12).Select(i => new UiTabOption(i.ToString(), "Page " + i)).ToArray();
        var tabs = Fluent.TabList("Scrolling tabs", value.Binding, items, new(Size: UiTabListSize.Small)
        {
            Scroll = new(PreviousLabel: "Earlier tabs", NextLabel: "Later tabs")
        });
        using var box = Dux.SizedBox(tabs, width: 260, height: 28);
        using var after = Fluent.Button("After scroll tabs");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null, float elapsed = .01f)
        {
            context.NewFrame(new(elapsed, new(640, 240), new(1, 1)), input ?? idle, new(0, 0, 640, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                box.Render(ui);
                after.Render(ui);
            }));
            using var data = context.GetDrawData();
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
        UiAccessibilityElement Find(string name, UiAccessibilityRole? role = null) => Nodes().Single(e => e.Node.Name == name && (role is null || e.Node.Role == role));
        bool Has(string name) => Nodes().Any(e => e.Node.Name == name);
        void Action(string name, UiAccessibilityAction action, UiAccessibilityRole? role = null)
        {
            var item = Find(name, role);
            if (tree.RequestAction(item.Node.Id, item.RuntimeId, action, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Tab scroll fixture action failed : " + name);
            Frame();
        }
        Frame();
        var bodyY = Find("After scroll tabs").Node.Bounds.Y;
        check(tabs.ScrollOffset == 0 && tabs.MaximumScrollOffset > 0 && !Has("Earlier tabs") && Has("Later tabs"),
            "Scrollable tabs initially show only the right edge indicator");
        check(!tabs.IsScrollBarVisible && context.State.NextScheduledFrameTimeSeconds is null,
            "Stationary scrollable tabs start without overlay or periodic rendering");
        var down = idle with
        {
            MousePosition = new(180, 12),
            LeftMouseDown = true,
            LeftMousePressed = true
        };
        Frame(down);
        Frame(down with
        {
            MousePosition = new(100, 12),
            LeftMousePressed = false
        });
        check(tabs.ScrollOffset == 80 && Has("Earlier tabs") && Has("Later tabs") && tabs.IsScrollBarVisible,
            "Dragging pans tab headers physically and shows both edge indicators and overlay");
        Frame(down with
        {
            MousePosition = new(100, 12),
            LeftMousePressed = false
        }, elapsed: 1);
        check(tabs.IsScrollBarVisible, "Overlay stays visible during a stationary held drag");
        Frame(idle with
        {
            MousePosition = new(100, 12),
            LeftMouseReleased = true
        });
        check(value.Value == "0", "Releasing a tab drag never selects the tab under the pointer");
        check(Find("After scroll tabs").Node.Bounds.Y == bodyY && Find("Scrolling tabs").Node.Bounds.Height == 28,
            "The overlay scrollbar reserves no extra header or body layout space");
        Frame(elapsed: .4f);
        check(tabs.ScrollBarOpacity > .99f && context.State.NextScheduledFrameTimeSeconds is not null,
            "A stopped scrollbar begins its finite fade-out instead of disappearing immediately");
        Frame(elapsed: .08f);
        check(tabs.ScrollBarOpacity > 0 && tabs.ScrollBarOpacity < 1,
            "Scrollbar fade-out has an intermediate opacity");
        Frame(elapsed: .2f);
        check(!tabs.IsScrollBarVisible && context.State.NextScheduledFrameTimeSeconds is null,
            "Overlay hides after movement ends and returns to event wait");
        Frame(idle with
        {
            MousePosition = new(120, 12),
            MouseWheel = -100
        });
        check(tabs.ScrollOffset == tabs.MaximumScrollOffset && Has("Earlier tabs") && !Has("Later tabs"),
            "End of scrolling shows only the left edge indicator");
        var endOffset = tabs.ScrollOffset;
        Action("Earlier tabs", UiAccessibilityAction.Invoke);
        check(tabs.ScrollOffset == endOffset && value.Value == "0" && Find("Page 0", UiAccessibilityRole.MenuItem).Node.Enabled,
            "Left ellipsis opens the hidden leading tab context menu without scrolling or selecting");
        Action("Page 0", UiAccessibilityAction.Invoke, UiAccessibilityRole.MenuItem);
        Frame();
        check(tabs.ScrollOffset == 0 && value.Value == "0", "Choosing the leading menu item reveals its real tab");
        Action("Later tabs", UiAccessibilityAction.Invoke);
        check(tabs.ScrollOffset == 0 && Find("Page 11", UiAccessibilityRole.MenuItem).Node.Enabled,
            "Right ellipsis opens the hidden trailing tab context menu without scrolling");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.End, true, default)]
        });
        Frame(idle with
        {
            KeyEvents = [new(UiKey.End, false, default)]
        });
        check(!Find("Page 11", UiAccessibilityRole.MenuItem).Node.Offscreen,
            "Directional tab menu scrolls its last keyboard-focused option into view");
        Action("Page 11", UiAccessibilityAction.Invoke, UiAccessibilityRole.MenuItem);
        Frame();
        check(value.Value == "11" && !Find("Page 11").Node.Offscreen,
            "Choosing a trailing menu item selects and reveals the original tab");
        value.Value = "11";
        Frame();
        check(!Find("Page 11").Node.Offscreen, "External selection reveals a clipped last tab");
        value.Value = "0";
        Frame();
        check(tabs.ScrollOffset == 0 && !Find("Page 0").Node.Offscreen,
            "External first-tab selection restores the leading edge");
        void ClickTab(UiAccessibilityElement tab)
        {
            var point = new UiVector2(tab.Node.Bounds.X + tab.Node.Bounds.Width / 2, tab.Node.Bounds.Y + tab.Node.Bounds.Height / 2);
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseReleased = true
            });
        }
        var rightmost = Nodes().Where(e => e.Node.Role == UiAccessibilityRole.Tab && !e.Node.Offscreen).MaxBy(e => e.Node.Bounds.X);
        var rightIndex = int.Parse(rightmost.Node.Name[5..]);
        ClickTab(rightmost);
        check(value.Value == rightIndex.ToString() && !Find("Page " + (rightIndex + 1)).Node.Offscreen,
            "Clicking the right edge tab reveals another trailing neighbour for continued navigation");
        var leftmost = Nodes().Where(e => e.Node.Role == UiAccessibilityRole.Tab && !e.Node.Offscreen).MinBy(e => e.Node.Bounds.X);
        var leftIndex = int.Parse(leftmost.Node.Name[5..]);
        ClickTab(leftmost);
        check(value.Value == leftIndex.ToString() && leftIndex > 0 && !Find("Page " + (leftIndex - 1)).Node.Offscreen,
            "Clicking the left edge tab reveals another leading neighbour for continued navigation");
        value.Value = "0";
        Frame();
        Action("Page 0", UiAccessibilityAction.Focus);
        Frame(idle with
        {
            KeyEvents = [new(UiKey.End, true, default)]
        });
        check(!Find("Page 11").Node.Offscreen && Find("Page 11").Node.Focused && value.Value == "0",
            "Keyboard End reveals focused trailing tab without activating it");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Home, true, default)]
        });
        check(tabs.ScrollOffset == 0 && Find("Page 0").Node.Focused,
            "Keyboard Home reveals the first tab again");
        Frame(idle with
        {
            MousePosition = new(20, 12),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = new(20, 12),
            LeftMouseReleased = true
        });
        check(value.Value == "0", "A stationary tab click still activates normally after scrolling");
        tabs.SetItems(new UiTabOption[] { new("0", "Page 0") });
        Frame(elapsed: 1);
        check(tabs.ScrollOffset == 0 && tabs.MaximumScrollOffset == 0 && !Has("Earlier tabs") && !Has("Later tabs") && !tabs.IsScrollBarVisible,
            "Removing overflowing items clamps offset and retires both indicators and overlay");
        tabs.SetItems(items);
        tabs.Options = tabs.Options with
        {
            RightToLeft = true
        };
        value.Value = "11";
        Frame();
        value.Value = "0";
        Frame();
        check(tabs.ScrollOffset == tabs.MaximumScrollOffset && !Find("Page 0").Node.Offscreen,
            "RTL selected first tab reveals its physical trailing edge");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
