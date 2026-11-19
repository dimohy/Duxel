using Duxel.Core;

internal static class PopoverRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, UiContext context, Action<UiInputState> setInput)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var text = "정세일";
        var count = 0;
        using var popover = Fluent.Popover("GPU profile", Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Input("GPU name", new UiBinding<string>(() => text, value => text = value), 64, 180),
            Fluent.Button("GPU save", () => count++)), options: new(MotionEnabled: false, Width: 240, DisableAutoFocus: true));
        var bounds = default(UiRect);
        var expected = default(UiColor);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(new(0, 0, 320, 240), new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(24, 100));
            popover.Render(ui);
            if (popover.IsOpen)
            {
                paint.Frame(bounds, new(255, 0, 0), default, 0, 0);
                expected = popover.Options.Appearance == UiPopoverAppearance.Brand
                    ? UiFluentButtonPalette.Primary(paint.FluentColorScheme, UiButtonVisualState.Rest).Background
                    : popover.Options.Appearance == UiPopoverAppearance.Inverted ? new(51, 51, 51)
                    : paint.FluentColorScheme == UiFluentColorScheme.Dark ? new(41, 41, 41) : new(255, 255, 255);
            }
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
        UiAccessibilityElement Find(string name, UiAccessibilityRole? role = null) => Nodes().Single(e => e.Node.Name == name && (role is null || role == e.Node.Role));
        capture("closed", Draw);
        popover.Show();
        foreach (var withArrow in new[] { false, true })
        {
            foreach (var appearance in Enum.GetValues<UiPopoverAppearance>())
            {
                foreach (var size in Enum.GetValues<UiPopoverSize>())
                {
                    popover.Options = popover.Options with
                    {
                        Appearance = appearance,
                        Size = size,
                        WithArrow = withArrow
                    };
                    var name = withArrow + "-" + appearance + "-" + size;
                    capture(name + "-layout", Draw);
                    bounds = Find("GPU profile", UiAccessibilityRole.Group).Node.Bounds;
                    var pixels = capture(name, Draw);
                    check(pixels.AsSpan().SequenceEqual(capture(name + "-replay", Draw)), "GPU Popover identical surface replays : " + name);
                    var offset = ((int)(bounds.Y + bounds.Height - 8) * 320 + (int)(bounds.X + bounds.Width - 8)) * 4;
                    check(pixels[offset] == (byte)expected.Rgba && pixels[offset + 1] == (byte)(expected.Rgba >> 8) && pixels[offset + 2] == (byte)(expected.Rgba >> 16), "GPU Popover palette and portal above later red base geometry : " + name);
                    check(bounds.Width == 240 && bounds.X >= 0 && bounds.Y >= 0 && bounds.Y + bounds.Height <= 240, "GPU Popover measured viewport fit : " + name);
                    var anchor = Find("GPU profile", UiAccessibilityRole.Button).Node.Bounds;
                    var arrowY = bounds.Y > anchor.Y ? bounds.Y - 3 : bounds.Y + bounds.Height + 2;
                    var arrowOffset = ((int)arrowY * 320 + (int)(anchor.X + anchor.Width / 2)) * 4;
                    var arrowFilled = pixels[arrowOffset] == (byte)expected.Rgba
                        && pixels[arrowOffset + 1] == (byte)(expected.Rgba >> 8) && pixels[arrowOffset + 2] == (byte)(expected.Rgba >> 16);
                    check(arrowFilled == withArrow && popover.Options.WithArrow == withArrow, "GPU Popover actual optional arrow pixels follow widget binding : " + name);
                }
            }
        }
        popover.Options = popover.Options with
        {
            WithArrow = true,
            Positioning = new()
            {
                CoverTarget = true,
                Position = UiPopupPosition.BelowStart,
                Pinned = true,
                Gap = 0
            }
        };
        capture("cover-layout", Draw);
        bounds = Find("GPU profile", UiAccessibilityRole.Group).Node.Bounds;
        var covered = capture("cover-without-arrow", Draw);
        var coverAnchor = Find("GPU profile", UiAccessibilityRole.Button).Node.Bounds;
        var coverOffset = ((int)(bounds.Y - 3) * 320 + (int)(coverAnchor.X + coverAnchor.Width / 2)) * 4;
        check(covered[coverOffset] != (byte)expected.Rgba || covered[coverOffset + 1] != (byte)(expected.Rgba >> 8) || covered[coverOffset + 2] != (byte)(expected.Rgba >> 16), "GPU coverTarget suppresses actual arrow pixels despite WithArrow");
        popover.Options = popover.Options with
        {
            Positioning = default
        };
        capture("cover-restored", Draw);
        var save = Find("GPU save");
        check(tree.RequestAction(save.Node.Id, save.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success, "GPU Popover actual child Invoke request");
        capture("invoked", Draw);
        check(count == 1 && popover.IsOpen, "GPU Popover child command executes with surface retained");
        popover.Options = popover.Options with
        {
            TrapFocus = true
        };
        capture("modal", Draw);
        check(Find("GPU profile", UiAccessibilityRole.Dialog).Node.Modal, "GPU Popover actual modal dialog metadata");
        popover.Close();
        capture("closed-again", Draw);
        check(!popover.IsOpen && Nodes().All(e => e.Node.Name != "GPU name"), "GPU Popover closed surface retires actual editor provider");
        check(context.State.NextScheduledFrameTimeSeconds is null, "GPU Popover static surface adds no periodic frames");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        popover.Options = popover.Options with
        {
            TrapFocus = false,
            TriggerMode = UiPopoverTriggerMode.Hover,
            MouseLeaveDelaySeconds = 0
        };
        var hoverAnchor = Find("GPU profile").Node.Bounds;
        setInput(idle with
        {
            MousePosition = new(hoverAnchor.X + hoverAnchor.Width / 2, hoverAnchor.Y + hoverAnchor.Height / 2)
        });
        capture("hover-enter", Draw);
        check(popover.IsOpen && Find("GPU name").Node.Value == "정세일", "GPU actual pointer hover opens retained editor content");
        setInput(idle);
        capture("hover-leave", Draw);
        check(!popover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "GPU zero-delay hover leave closes and schedules no polling loop");
        popover.Options = popover.Options with
        {
            TriggerMode = UiPopoverTriggerMode.Context,
            WithArrow = false,
            Positioning = new()
            {
                Position = UiPopupPosition.BelowStart,
                Gap = 0,
                Pinned = true
            }
        };
        capture("context-ready", Draw);
        var contextPoint = new UiVector2(hoverAnchor.X + 4, hoverAnchor.Y + 4);
        setInput(idle with
        {
            MousePosition = contextPoint,
            RightMouseDown = true,
            RightMousePressed = true
        });
        capture("context-press", Draw);
        setInput(idle with
        {
            MousePosition = contextPoint,
            RightMouseReleased = true
        });
        capture("context-release", Draw);
        var contextBounds = Find("GPU profile", UiAccessibilityRole.Group).Node.Bounds;
        check(popover.IsOpen && contextBounds.X == contextPoint.X && contextBounds.Y == contextPoint.Y, "GPU actual right release uses exact virtual mouse point");
        setInput(idle with
        {
            MouseWheel = 1
        });
        capture("context-scroll-close", Draw);
        check(!popover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "GPU context outside scroll closes and returns to event wait");
        setInput(idle);
        var menuCalls = 0;
        var menu = Fluent.Menu("GPU owned menu", [new("execute", "GPU menu action")], _ => menuCalls++);
        using var owner = Fluent.Popover("GPU menu owner", menu,
            options: new(MotionEnabled: false, Width: 220, DisableAutoFocus: true, TriggerMode: UiPopoverTriggerMode.Hover, MouseLeaveDelaySeconds: 0));
        void DrawMenu(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(24, 100));
            owner.Render(ui);
        }
        owner.Show();
        capture("owned-menu-ready", DrawMenu);
        var menuTrigger = Find("GPU owned menu", UiAccessibilityRole.Button);
        check(tree.RequestAction(menuTrigger.Node.Id, menuTrigger.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "GPU owned menu accepts its real trigger Invoke");
        setInput(idle with
        {
            MousePosition = new(menuTrigger.Node.Bounds.X + 4, menuTrigger.Node.Bounds.Y + 4)
        });
        capture("owned-menu-open", DrawMenu);
        var menuItem = Find("GPU menu action").Node.Bounds;
        var pointer = idle with
        {
            MousePosition = new(menuItem.X + menuItem.Width / 2, menuItem.Y + menuItem.Height - 2)
        };
        setInput(pointer);
        var first = capture("owned-menu-hover", DrawMenu);
        check(owner.IsOpen && menu.IsOpen && context.State.NextScheduledFrameTimeSeconds is null,
            "GPU pointer in owned menu portal keeps zero-delay Hover Popover alive without polling");
        check(first.AsSpan().SequenceEqual(capture("owned-menu-hover-replay", DrawMenu)),
            "GPU stationary owned menu replays identical pixels");
        setInput(pointer with
        {
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        capture("owned-menu-press", DrawMenu);
        setInput(pointer with
        {
            LeftMouseReleased = true
        });
        capture("owned-menu-release", DrawMenu);
        check(menuCalls == 1 && !menu.IsOpen && owner.IsOpen,
            "GPU actual menu pointer press/release invokes once while keeping its owner");
        setInput(idle);
        capture("owned-menu-leave", DrawMenu);
        check(!owner.IsOpen && context.State.NextScheduledFrameTimeSeconds is null,
            "GPU leaving the whole owned popup chain returns to event wait");

        using var outside = Fluent.Button("GPU focus destination");
        using var focusLeave = Fluent.Popover("GPU focus leave", Fluent.Button("GPU focus child"),
            options: new(MotionEnabled: false, Width: 200)
            {
                CloseOnFocusLeave = true
            });
        void DrawFocusLeave(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(new(0, 0, 320, 240), new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(24, 100));
            focusLeave.Render(ui);
            ui.SetCursorScreenPos(new(24, 200));
            outside.Render(ui);
        }
        capture("focus-leave-ready", DrawFocusLeave);
        void FocusDestination()
        {
            var destination = Find("GPU focus destination", UiAccessibilityRole.Button);
            check(tree.RequestAction(destination.Node.Id, destination.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
                "GPU focus-leave destination accepts actual focus action");
        }
        FocusDestination();
        var closedReference = capture("focus-leave-reference", DrawFocusLeave);
        focusLeave.Show();
        var openedPixels = capture("focus-leave-open", DrawFocusLeave);
        var retired = Find("GPU focus child", UiAccessibilityRole.Button);
        check(focusLeave.IsOpen && retired.Node.Focused && !closedReference.AsSpan().SequenceEqual(openedPixels),
            "GPU focus-leave Popover paints actual surface and autofocuses child");
        FocusDestination();
        capture("focus-leave-transfer", DrawFocusLeave);
        var dismissedPixels = capture("focus-leave-dismissed", DrawFocusLeave);
        check(!focusLeave.IsOpen && Find("GPU focus destination", UiAccessibilityRole.Button).Node.Focused
            && tree.RequestAction(retired.Node.Id, retired.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Unavailable,
            "GPU focus-leave dismissal preserves destination and retires real child provider");
        check(closedReference.AsSpan().SequenceEqual(dismissedPixels)
            && dismissedPixels.AsSpan().SequenceEqual(capture("focus-leave-replay", DrawFocusLeave))
            && context.State.NextScheduledFrameTimeSeconds is null,
            "GPU focus-leave dismissal restores exact background pixels and remains in event wait");
    }
}
