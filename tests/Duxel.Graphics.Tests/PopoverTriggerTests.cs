using Duxel.Core;

internal static class PopoverTriggerTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var hover = Fluent.Popover("Hover target", Fluent.Button("Hover child"), options: new(MotionEnabled: false, Width: 200, DisableAutoFocus: true, TriggerMode: UiPopoverTriggerMode.Hover));
        UiWidget current = hover;
        var visible = true;
        Action? afterRender = null;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        long uploads = 0;
        void Frame(UiInputState? input = null, float elapsed = .01f)
        {
            context.NewFrame(new(elapsed, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(220, 230));
                if (visible)
                    current.Render(ui);
                afterRender?.Invoke();
                uploads = ui.PopupSurfaceTextureStatistics.Uploads;
            }));
            using var draw = context.GetDrawData();
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
        UiAccessibilityElement Find(string name, UiAccessibilityRole? role = null) => Nodes().Single(e => e.Node.Name == name && (role is null || e.Node.Role == role));
        void Focus(string name, UiInputState? input = null)
        {
            var e = Find(name, UiAccessibilityRole.Button);
            check(tree.RequestAction(e.Node.Id, e.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success, "Popover trigger queues native focus : " + name);
            Frame(input);
        }
        void Key(UiKey key, KeyModifiers modifiers = default, UiInputState? pointer = null)
        {
            Frame((pointer ?? idle) with
            {
                KeyEvents = [new(key, true, modifiers)],
                Modifiers = modifiers
            });
            Frame((pointer ?? idle) with
            {
                KeyEvents = [new(key, false, modifiers)],
                Modifiers = modifiers
            });
        }
        Frame();
        var anchor = Find("Hover target").Node.Bounds;
        var hp = idle with
        {
            MousePosition = new(anchor.X + anchor.Width / 2, anchor.Y + anchor.Height / 2)
        };
        check(!hover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "Hover trigger idle waits without scheduling");
        Frame(hp);
        check(hover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "Hover entry opens immediately without a polling deadline");
        var firstUpload = uploads;
        Frame(hp, .1f);
        check(hover.IsOpen && uploads == firstUpload && context.State.NextScheduledFrameTimeSeconds is null, "Stationary hover reuses surface and schedules no periodic work");
        Frame();
        var deadline = context.State.NextScheduledFrameTimeSeconds!.Value;
        check(hover.IsOpen && Math.Abs(deadline - context.State.TimeSeconds - .5) < 1e-8, "Hover leave schedules source default500ms close once");
        Frame(elapsed: .2f);
        check(hover.IsOpen && context.State.NextScheduledFrameTimeSeconds == deadline, "Hover outside frames preserve absolute deadline instead of restarting it");
        var body = Find("Hover target", UiAccessibilityRole.Group).Node.Bounds;
        var surfacePointer = idle with
        {
            MousePosition = new(body.X + 12, body.Y + 12)
        };
        Frame(surfacePointer);
        check(hover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "Entering owned surface cancels pending hover close");
        Frame();
        Frame(elapsed: .49f);
        check(hover.IsOpen, "Hover remains visible before its deadline");
        Frame(elapsed: .02f);
        check(!hover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "Hover deadline closes once and leaves event wait unscheduled");
        hover.Options = hover.Options with
        {
            MouseLeaveDelaySeconds = 0
        };
        Frame(hp);
        Frame();
        check(!hover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "Zero hover leave delay closes immediately without a timer loop");
        hover.Options = hover.Options with
        {
            MouseLeaveDelaySeconds = .5
        };
        Frame(hp);
        Focus("Hover target", hp);
        Key(UiKey.Escape, pointer: hp);
        Frame(hp);
        check(!hover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "Hover Escape from trigger does not reopen under stationary pointer");
        Frame();
        Frame(hp);
        Frame();
        check(context.State.NextScheduledFrameTimeSeconds is not null, "Hover reentry permits another leave deadline");
        hover.Options = hover.Options with
        {
            TriggerMode = UiPopoverTriggerMode.Click
        };
        Frame();
        Frame(elapsed: .6f);
        check(hover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "Changing trigger mode cancels obsolete hover deadline");
        hover.Close();
        hover.Options = hover.Options with
        {
            TriggerMode = UiPopoverTriggerMode.Hover
        };
        var model = false;
        var denyOpen = true;
        var denyClose = false;
        var requests = 0;
        hover.Open = new(() => model, value => { requests++; if (value ? !denyOpen : !denyClose) model = value; });
        Frame();
        Frame(hp);
        Frame(hp, .6f);
        check(!hover.IsOpen && requests == 1 && context.State.NextScheduledFrameTimeSeconds is null, "Rejected hover open is requested once per entry");
        Frame();
        denyOpen = false;
        Frame(hp);
        denyClose = true;
        Frame();
        Frame(elapsed: .6f);
        var rejectedRequests = requests;
        Frame(elapsed: .6f);
        check(hover.IsOpen && requests == rejectedRequests && context.State.NextScheduledFrameTimeSeconds is null, "Rejected delayed close does not reschedule a past-deadline loop");
        denyClose = false;
        hover.Close();
        Frame(hp);
        Frame();
        visible = false;
        Frame();
        check(!hover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "Hidden hover owner cancels its pending deadline");
        visible = true;
        hover.Show();
        Frame(hp);
        Frame();
        hover.Dispose();
        visible = false;
        Frame();
        check(context.State.NextScheduledFrameTimeSeconds is null, "Disposed hover owner leaves no scheduled wake");
        using var contextual = Fluent.Popover("Context target", Fluent.Button("Context action"), options: new(MotionEnabled: false, Width: 180, DisableAutoFocus: true,
            TriggerMode: UiPopoverTriggerMode.Context, Positioning: new()
            {
                Position = UiPopupPosition.BelowStart,
                Gap = 0,
                Pinned = true
            }));
        current = contextual;
        visible = true;
        Frame();
        var contextAnchor = Find("Context target").Node.Bounds;
        var cp = new UiVector2(contextAnchor.X + 5, contextAnchor.Y + 5);
        Frame(idle with
        {
            MousePosition = cp,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = cp,
            LeftMouseReleased = true
        });
        check(!contextual.IsOpen, "Context trigger ignores ordinary left click");
        Frame(idle with
        {
            MousePosition = cp,
            RightMouseDown = true,
            RightMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = cp,
            RightMouseReleased = true
        });
        var contextBounds = Find("Context target", UiAccessibilityRole.Group).Node.Bounds;
        check(contextual.IsOpen && contextBounds.X == cp.X && contextBounds.Y == cp.Y, "Context right release anchors a true zero-size mouse point");
        var originalUploads = uploads;
        var cp2 = new UiVector2(contextAnchor.X + 2, contextAnchor.Y + 2);
        Frame(idle with
        {
            MousePosition = cp2,
            RightMouseDown = true,
            RightMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = cp2,
            RightMouseReleased = true
        });
        contextBounds = Find("Context target", UiAccessibilityRole.Group).Node.Bounds;
        check(contextBounds.X == cp2.X && contextBounds.Y == cp2.Y && uploads == originalUploads, "Repeated context gesture repositions and reuses integral surface raster");
        Frame(idle with
        {
            MouseWheel = 1
        });
        check(!contextual.IsOpen, "Context mode dismisses on outside scroll without CloseOnScroll opt-in");
        Focus("Context target");
        Key(UiKey.ContextMenu);
        check(contextual.IsOpen, "Context Menu key opens the focused trigger");
        Key(UiKey.Escape);
        check(!contextual.IsOpen && Find("Context target").Node.Focused, "Context Escape restores trigger focus");
        Key(UiKey.F10, KeyModifiers.Shift);
        check(contextual.IsOpen, "Context Shift+F10 opens the focused trigger");
        contextual.Close();
        Frame();
        var triggerNode = Find("Context target");
        tree.RequestAction(triggerNode.Node.Id, triggerNode.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        Frame();
        check(contextual.IsOpen, "Context native Invoke opens at trigger center while physical click remains disabled");
        contextual.Close();
        Frame();
        contextual.Options = contextual.Options with
        {
            WithArrow = true,
            Positioning = contextual.Options.Positioning with
            {
                CoverTarget = true
            }
        };
        contextual.Show();
        Frame();
        check(contextual.IsOpen, "Context cover-target placement remains valid and suppresses the optional arrow");
        using var child = Fluent.Popover("Nested context", Fluent.Button("Nested action"), options: new(MotionEnabled: false, DisableAutoFocus: true, TriggerMode: UiPopoverTriggerMode.Context));
        using var parent = Fluent.Popover("Parent", child, options: new(MotionEnabled: false, DisableAutoFocus: true));
        current = parent;
        parent.Show();
        Frame();
        child.Show();
        Frame();
        Focus("Nested context");
        Key(UiKey.Escape);
        check(parent.IsOpen && !child.IsOpen, "Escape on nested trigger closes deepest owner rather than its parent");
        parent.Close();
        using var firstHover = Fluent.Popover("First hover", Fluent.Button("First content"), options: new(MotionEnabled: false, DisableAutoFocus: true, TriggerMode: UiPopoverTriggerMode.Hover));
        using var secondHover = Fluent.Popover("Second hover", Fluent.Button("Second content"), options: new(MotionEnabled: false, DisableAutoFocus: true, TriggerMode: UiPopoverTriggerMode.Hover, MouseLeaveDelaySeconds: 1));
        using var hoverPair = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min), firstHover, secondHover);
        current = hoverPair;
        Frame();
        var firstAnchor = Find("First hover").Node.Bounds;
        Frame(idle with
        {
            MousePosition = new(firstAnchor.X + firstAnchor.Width / 2, firstAnchor.Y + firstAnchor.Height / 2)
        });
        var secondAnchor = Find("Second hover").Node.Bounds;
        Frame(idle with
        {
            MousePosition = new(secondAnchor.X + secondAnchor.Width / 2, secondAnchor.Y + secondAnchor.Height / 2)
        });
        check(firstHover.IsOpen && secondHover.IsOpen, "Independent hover owners can overlap delayed lifetimes");
        Frame();
        var secondDeadline = context.State.TimeSeconds + 1;
        check(context.State.NextScheduledFrameTimeSeconds < secondDeadline, "Multiple hover owners declare the earliest pending deadline");
        afterRender = () => firstHover.Close();
        Frame();
        afterRender = null;
        check(!firstHover.IsOpen && secondHover.IsOpen && context.State.NextScheduledFrameTimeSeconds == secondDeadline,
            "Later same-frame close cancels one deadline while preserving another owner");
        Frame(elapsed: 1.1f);
        check(!secondHover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "Last hover deadline closes and returns aggregate scheduler to event wait");
        using var invalidContent = Fluent.Body1("Unused");
        var rejected = 0;
        foreach (var invalid in new[] { new UiPopoverOptions(MouseLeaveDelaySeconds: -1), new UiPopoverOptions(MouseLeaveDelaySeconds: double.NaN), new UiPopoverOptions(TriggerMode: (UiPopoverTriggerMode)99) })
        {
            try
            {
                using var invalidPopover = Fluent.Popover("Invalid", invalidContent, options: invalid);
            }
            catch (ArgumentOutOfRangeException) { rejected++; }
        }
        check(rejected == 3, "Popover trigger options reject invalid delay and mode before adopting content");
        var point = UiPopupPlacement.Arrange(new(100, 100, 0, 0), new(20, 20), new(0, 0, 640, 480), new()
        {
            Position = UiPopupPosition.BelowStart,
            Gap = 0,
            Pinned = true
        });
        check(point.Bounds.X == 100 && point.Bounds.Y == 100, "Shared placement accepts an exact virtual point without an invented pixel target");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
