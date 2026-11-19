using Duxel.Core;

internal static class PopoverTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var text = "정세일";
        var count = 0;
        var changes = new List<bool>();
        var input = Fluent.Input("Name", new UiBinding<string>(() => text, value => text = value), 64, 180);
        var command = Fluent.Button("Save", () => count++);
        using var popover = Fluent.Popover("Profile", Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min), input, command), options: new(MotionEnabled: false));
        popover.OpenChanged = changes.Add;
        using var before = Fluent.Button("Before");
        using var after = Fluent.Button("After");
        var underArrowClicks = 0;
        UiVector2? underArrowPosition = null;
        using var underArrow = Fluent.Button("Under arrow", () => underArrowClicks++);
        UiWidget current = popover;
        var visible = true;
        var disabled = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? state = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), state ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                before.Render(ui);
                if (visible)
                {
                    ui.BeginDisabled(disabled);
                    try
                    {
                        current.Render(ui);
                    }
                    finally { ui.EndDisabled(); }
                }
                if (underArrowPosition is { } underPosition)
                {
                    ui.SetCursorScreenPos(underPosition);
                    underArrow.Render(ui);
                }
                after.Render(ui);
            }));
            using var data = context.GetDrawData();
        }
        List<UiAccessibilityElement> Nodes()
        {
            var nodes = new List<UiAccessibilityElement>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var e))
                {
                    nodes.Add(e);
                    Visit(e.FirstChild);
                    id = e.Next;
                }
            }
            Visit(tree.Root.First);
            return nodes;
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole? role = null)
            => Nodes().Single(e => e.Node.Name == name && (role is null || e.Node.Role == role));
        void Send(string name, UiAccessibilityAction action, UiAccessibilityRole? role = null)
        {
            var e = Find(name, role);
            check(tree.RequestAction(e.Node.Id, e.RuntimeId, action, wait: false) == UiAccessibilityResult.Success, "Popover queues " + action + " : " + name);
            Frame();
        }
        void Key(UiKey key, KeyModifiers modifiers = default)
        {
            Frame(idle with
            {
                KeyEvents = [new(key, true, modifiers)],
                Modifiers = modifiers
            });
            Frame(idle with
            {
                KeyEvents = [new(key, false, modifiers)],
                Modifiers = modifiers
            });
        }
        Frame();
        var trigger = Find("Profile");
        check(trigger.Node.Expansion == UiAccessibilityExpansionState.Collapsed && !popover.IsOpen && Nodes().All(e => e.Node.Name != "Name"), "Popover starts collapsed and does not publish hidden children");
        Send("Profile", UiAccessibilityAction.Expand);
        var group = Find("Profile", UiAccessibilityRole.Group);
        check(popover.IsOpen && !group.Node.Modal && !group.Node.Focusable && Find("Name").Node.Focused, "Popover defaults to nonmodal group and autofocuses first editor");
        check(Find("Profile", UiAccessibilityRole.Button).Node.ControlledElementId == group.Node.Id && group.Node.Bounds.Width == 280, "Popover trigger controls actual measured surface");
        check(group.Node.Bounds.X >= 0 && group.Node.Bounds.Y >= 0 && group.Node.Bounds.X + group.Node.Bounds.Width <= 640 && group.Node.Bounds.Y + group.Node.Bounds.Height <= 480, "Popover placement stays within viewport");
        check(group.Node.Bounds.Height < 200, "Popover surface follows natural content height instead of filling viewport");
        Frame(idle with
        {
            CharEvents = [new('X')]
        });
        check(text.Contains('X'), "Popover ordinary editor accepts text");
        Send("Save", UiAccessibilityAction.Invoke);
        check(count == 1, "Popover ordinary command executes");
        Send("Save", UiAccessibilityAction.Focus);
        Key(UiKey.Tab);
        check(Find("After").Node.Focused && popover.IsOpen, "Popover nonmodal Tab reaches background without closing");
        Key(UiKey.Tab, KeyModifiers.Shift);
        check(Find("Save").Node.Focused, "Popover reverse Tab returns to surface");
        var retired = Find("Name");
        Key(UiKey.Escape);
        check(!popover.IsOpen && Find("Profile").Node.Focused && changes.SequenceEqual(new[] { true, false }), "Popover Escape closes once and restores trigger focus");
        check(tree.RequestAction(retired.Node.Id, retired.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Unavailable, "Popover closed child provider retires");
        popover.Show();
        Frame();
        check(popover.IsOpen && text.Contains('X'), "Popover reopening retains owned editor model");
        popover.Options = popover.Options with
        {
            WithArrow = true
        };
        Frame();
        var anchorBounds = Find("Profile", UiAccessibilityRole.Button).Node.Bounds;
        var arrowBounds = Find("Profile", UiAccessibilityRole.Group).Node.Bounds;
        var arrowY = arrowBounds.Y > anchorBounds.Y ? arrowBounds.Y - 3 : arrowBounds.Y + arrowBounds.Height + 3;
        underArrowPosition = new(anchorBounds.X + anchorBounds.Width / 2 - 8, arrowY - 8);
        Frame();
        Frame(idle with
        {
            MousePosition = new(anchorBounds.X + anchorBounds.Width / 2, arrowY),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = new(anchorBounds.X + anchorBounds.Width / 2, arrowY),
            LeftMouseReleased = true
        });
        underArrowPosition = null;
        check(popover.IsOpen && underArrowClicks == 0, "Popover arrow pointer remains inside and cannot activate underlying base control");
        Frame(idle with
        {
            MousePosition = new(arrowBounds.X + arrowBounds.Width - 2, arrowY),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame();
        check(!popover.IsOpen, "Popover pointer outside arrow shape dismisses instead of treating its entire horizontal strip as inside");
        popover.Show();
        Frame();
        Frame(idle with
        {
            MousePosition = new(639, 479),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame();
        check(!popover.IsOpen, "Popover outside pointer dismisses");
        popover.Options = popover.Options with
        {
            TrapFocus = true
        };
        popover.Show();
        Frame();
        var dialog = Find("Profile", UiAccessibilityRole.Dialog);
        check(dialog.Node.Modal && !dialog.Node.Focusable && Find("Name").Node.Focused, "Popover trap exposes modal dialog metadata and focuses editor");
        Send("Save", UiAccessibilityAction.Focus);
        Key(UiKey.Tab);
        check(Find("Name").Node.Focused && popover.IsOpen, "Popover trapped Tab cycles surface");
        Key(UiKey.Tab, KeyModifiers.Shift);
        check(Find("Save").Node.Focused, "Popover trapped reverse Tab cycles surface");
        popover.Close();
        Frame();
        check(!popover.IsOpen && Find("After").Node.Enabled, "Popover close releases exclusive keyboard gate next frame");
        var bound = false;
        var rejectOpen = true;
        var rejectClose = false;
        popover.Open = new(() => bound, value => { if (value ? !rejectOpen : !rejectClose) bound = value; });
        popover.Show();
        Frame();
        check(!popover.IsOpen && !bound, "Popover rejected controlled open leaves hidden");
        rejectOpen = false;
        popover.Show();
        Frame();
        check(popover.IsOpen && bound, "Popover accepted controlled open reads authoritative binding");
        rejectClose = true;
        Key(UiKey.Escape);
        check(popover.IsOpen && bound && !popover.Close(), "Popover rejected Escape and imperative close preserve surface");
        rejectClose = false;
        bound = false;
        Frame();
        check(!popover.IsOpen, "Popover external controlled close synchronizes");
        bound = true;
        Frame();
        check(popover.IsOpen, "Popover external controlled open synchronizes");
        visible = false;
        Frame();
        check(!popover.IsOpen && Nodes().All(e => e.Node.Name != "Name"), "Popover hidden owner retires surface despite controlled open");
        visible = true;
        disabled = true;
        Frame();
        check(!popover.IsOpen, "Popover disabled owner cannot reopen controlled surface");
        disabled = false;
        bound = false;
        popover.Options = popover.Options with
        {
            TrapFocus = false,
            DisableAutoFocus = true,
            CloseOnScroll = true
        };
        Send("Before", UiAccessibilityAction.Focus);
        popover.Show();
        Frame();
        check(Find("Before").Node.Focused && popover.IsOpen, "Popover disableAutoFocus preserves existing focus");
        Frame(idle with
        {
            MouseWheel = 1
        });
        check(!popover.IsOpen, "Popover optional outside scroll dismisses");
        using var child = Fluent.Popover("Nested", Fluent.Button("Nested action"), options: new(MotionEnabled: false, TrapFocus: true));
        using var parent = Fluent.Popover("Parent", Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min), Fluent.Button("Parent action"), child), options: new(MotionEnabled: false, TrapFocus: true));
        current = parent;
        parent.Show();
        Frame();
        child.Show();
        Frame();
        Frame();
        check(parent.IsOpen && child.IsOpen && Find("Nested action").Node.Focused, "Popover nested modal child preserves parent and owns focus");
        Key(UiKey.Escape);
        check(parent.IsOpen && !child.IsOpen && Find("Nested").Node.Focused, "Popover nested Escape closes deepest surface and restores child trigger");
        parent.Close();
        Frame();
        check(!parent.IsOpen && !child.IsOpen, "Popover parent close removes nested surfaces");
        using var empty = Fluent.Popover("Information", Fluent.Body1("Read-only information"), options: new(MotionEnabled: false, TrapFocus: true));
        current = empty;
        empty.Show();
        Frame();
        check(Find("Information", UiAccessibilityRole.Dialog).Node.Focused, "Popover empty interactive set focuses information surface");
        Key(UiKey.Tab);
        check(Find("Information", UiAccessibilityRole.Dialog).Node.Focused, "Popover information trap retains a valid keyboard target");
        Key(UiKey.Escape);
        check(!empty.IsOpen && Find("Information").Node.Focused, "Popover information surface remains dismissible by Escape");
        current = popover;
        popover.Open = null;
        popover.Options = popover.Options with
        {
            MaximumHeight = 64
        };
        popover.Show();
        Frame();
        check(Find("Profile", UiAccessibilityRole.Group).Node.Bounds.Height == 64 && Nodes().Any(e => e.Node.Scroll is { VerticallyScrollable: true }), "Popover bounded height exposes actual scrolling viewport");
        using (var other = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2)))
        {
            ControlFontFixture.Configure(other);
            using var otherTree = new UiAccessibilityTree();
            other.SetAccessibilityTree(otherTree);
            popover.Show();
            other.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
            other.Render(new Screen(ui => popover.Render(ui)));
            using var draw = other.GetDrawData();
            check(popover.IsOpen, "Popover context transfer releases old ownership and accepts fresh open request");
            visible = false;
            Frame();
            check(popover.IsOpen && Nodes().All(e => e.Node.Name != "Name"), "Popover old context frame cannot close transferred surface");
        }
        check(!popover.IsOpen, "Popover context disposal detaches live surface");
        visible = true;
        popover.Show();
        Frame();
        popover.Dispose();
        visible = false;
        Frame();
        check(!popover.IsOpen && Nodes().All(e => e.Node.Name != "Name"), "Popover disposal releases surface and owned content");
        check(Fluent.ComponentNames.Contains("Popover") && !Fluent.MissingComponentNames.Contains("Popover"), "Popover connects official catalog");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
