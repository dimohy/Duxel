using Duxel.Core;

internal static class PopoverMenuTests
{
    internal static void Run(Action<bool, string> check)
    {
        RunCase(check, false, false);
        RunCase(check, true, false);
        RunCase(check, false, true);
        RunCase(check, true, true);
    }

    private static void RunCase(Action<bool, string> check, bool trapFocus, bool motionEnabled)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var calls = 0;
        var menu = Fluent.Menu("Owned menu", [new("execute", "Owned action")], _ => calls++);
        using var popover = Fluent.Popover("Owner trigger", menu, options: new(MotionEnabled: motionEnabled, Width: 240, TrapFocus: trapFocus));
        var visible = true;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null, float elapsed = 1f / 60)
        {
            context.NewFrame(new(elapsed, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(180, 200));
                if (visible)
                {
                    popover.Render(ui);
                }
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                {
                    if (item.Node.Name == name && item.Node.Role != UiAccessibilityRole.Menu)
                    {
                        found = item;
                    }
                    Visit(item.FirstChild);
                    id = item.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Popover menu fixture missing : " + name);
        }
        void Focus(string name)
        {
            var node = Find(name);
            if (tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false) != UiAccessibilityResult.Success)
            {
                throw new InvalidOperationException("Popover menu focus request failed : " + name);
            }
            Frame();
        }
        void Key(UiKey key)
        {
            Frame(idle with
            {
                KeyEvents = [new(key, true, default)]
            });
            Frame(idle with
            {
                KeyEvents = [new(key, false, default)]
            });
        }
        void OpenMenu()
        {
            popover.Show();
            Frame();
            Focus("Owned menu");
            Key(UiKey.DownArrow);
            check(popover.IsOpen && menu.IsOpen, "Owned menu opens above its live Popover");
        }
        OpenMenu();
        Key(UiKey.Escape);
        check(popover.IsOpen && !menu.IsOpen && context.State.FocusedId == Find("Owned menu").Node.Id,
            "Menu item Escape closes only the menu and restores its owned trigger");
        Key(UiKey.Escape);
        check(!popover.IsOpen && context.State.FocusedId == Find("Owner trigger").Node.Id,
            "A subsequent Escape closes the Popover and restores its outer trigger");
        OpenMenu();
        Focus("Owned menu");
        Key(UiKey.Escape);
        check(popover.IsOpen && !menu.IsOpen,
            $"Escape on an open menu trigger is consumed before its Popover (trap : {trapFocus}, parent : {popover.IsOpen}, menu : {menu.IsOpen}, focus : {context.State.FocusedId})");
        OpenMenu();
        Key(UiKey.Enter);
        check(popover.IsOpen && !menu.IsOpen && calls == 1, "Owned menu action executes once without closing its Popover");
        OpenMenu();
        var action = Find("Owned action").Node.Bounds;
        var click = idle with
        {
            MousePosition = new(action.X + action.Width / 2, action.Y + action.Height - 2)
        };
        Frame(click with
        {
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(click with
        {
            LeftMouseReleased = true
        });
        check(popover.IsOpen && !menu.IsOpen && calls == 2,
            "A real click in an owned menu portal invokes once without outside-dismissing its Popover");
        OpenMenu();
        popover.Close();
        Frame();
        check(!popover.IsOpen && !menu.IsOpen, "Programmatic Popover close retires its owned menu");
        OpenMenu();
        visible = false;
        Frame();
        check(!popover.IsOpen && !menu.IsOpen && context.State.NextScheduledFrameTimeSeconds is null,
            "Hidden Popover retires its owned menu without leaving a scheduled wake");
        visible = true;
        popover.Options = popover.Options with
        {
            TriggerMode = UiPopoverTriggerMode.Hover
        };
        OpenMenu();
        var menuTrigger = Find("Owned menu").Node.Bounds;
        Frame(idle with
        {
            MousePosition = new(menuTrigger.X + 4, menuTrigger.Y + 4)
        });
        var item = Find("Owned action").Node.Bounds;
        var menuPointer = idle with
        {
            MousePosition = new(item.X + item.Width / 2, item.Y + item.Height - 2)
        };
        Frame(menuPointer);
        Frame(menuPointer, .6f);
        check(popover.IsOpen && menu.IsOpen && context.State.NextScheduledFrameTimeSeconds is null,
            "Hover Popover retains its owned menu portal beyond the leave delay without polling");
        Frame();
        Frame(elapsed: .6f);
        check(!popover.IsOpen && !menu.IsOpen && context.State.NextScheduledFrameTimeSeconds is null,
            "Leaving both Hover Popover and owned menu closes both once at the deadline");
        OpenMenu();
        popover.Dispose();
        visible = false;
        Frame();
        check(!menu.IsOpen && context.State.NextScheduledFrameTimeSeconds is null,
            "Disposed Popover retires its owned menu without leaving a scheduled wake");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
