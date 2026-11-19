using Duxel.Core;

internal static class PopoverMotionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var calls = 0;
        UiFluentPopover? owner = null;
        using var popover = owner = Fluent.Popover("Motion owner", Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Button("Motion action", () => calls++), Fluent.Button("Motion close", () => owner!.Close())),
            options: new(Width: 200, DisableAutoFocus: true, WithArrow: true,
                Positioning: new()
                {
                    Position = UiPopupPosition.BelowStart,
                    Pinned = true
                }));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var visible = true;
        var origin = new UiVector2(180, 180);
        long uploads = 0;
        int groupUpdates = 0;
        UiTextureId groupTexture = default;
        UiDrawData Frame(float elapsed = 0, UiInputState? input = null)
        {
            context.NewFrame(new(elapsed, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(origin);
                if (visible)
                    popover.Render(ui);
                uploads = ui.PopupSurfaceTextureStatistics.Uploads;
            }));
            var draw = context.GetDrawData();
            groupUpdates = draw.TextureUpdates.Count(x => x.Kind == UiTextureUpdateKind.RenderPaintGroup);
            if (groupUpdates > 0)
                groupTexture = draw.TextureUpdates.First(x => x.Kind == UiTextureUpdateKind.RenderPaintGroup).TextureId;
            return draw;
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role = UiAccessibilityRole.Button)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var e))
                {
                    if (e.Node.Name == name && e.Node.Role == role)
                        found = e;
                    Visit(e.FirstChild);
                    id = e.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Motion fixture missing : " + name);
        }
        float CompositeOpacity(UiDrawData draw) => draw.DrawLists.SelectMany(x => x.Commands)
            .Single(x => x.WhiteBackdropTextureId is not null).Opacity;
        using (Frame())
        {
        }
        check(popover.Options.MotionEnabled, "Popover official entry motion is enabled by default");
        popover.Show();
        using (var enter = Frame())
        {
            check(popover.IsOpen && groupUpdates == 1 && context.State.NextScheduledFrameTimeSeconds is { } next
                && next > context.State.TimeSeconds, "Entry captures live content once and schedules a finite next frame");
            check(enter.TextureUpdates.Single(x => x.Kind == UiTextureUpdateKind.RenderPaintGroup).PaintGroup!.Content.TextMode == UiPaintGroupTextMode.Projected,
                "Moving content reuses group compositing with projected text");
        }
        var initial = Find("Motion owner", UiAccessibilityRole.Group).Node.Bounds;
        var sourceUploads = uploads;
        using (var mid = Frame(.05f))
        {
            var actual = Find("Motion owner", UiAccessibilityRole.Group).Node.Bounds;
            check(MathF.Abs(actual.Y - initial.Y - 5) < .001f && MathF.Abs(CompositeOpacity(mid) - .5f) < .001f,
                "At50ms official400ms cubic-bezier yields group opacity0.5 and5px remaining below slide");
            check(uploads == sourceUploads, "Fractional motion reuses the canonical body-arrow raster");
        }
        using (Frame())
            check(groupUpdates == 0, "Unchanged same-time content reuses its complete group snapshot");
        var unrelated = new UiTextureId(999999);
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, unrelated, UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 255, 255, 255 }));
        using (Frame())
            check(groupUpdates == 0, "Unrelated texture updates do not invalidate unchanged interactive content");
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Update, new(2), UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 255, 255, 255 }));
        using (Frame())
            check(groupUpdates == 1, "An external update to a referenced texture invalidates unchanged draw geometry");
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, unrelated, UiTextureFormat.Rgba8Unorm, 0, 0, ReadOnlyMemory<byte>.Empty));
        var action = Find("Motion action").Node.Bounds;
        var pointer = idle with
        {
            MousePosition = new(action.X + action.Width / 2, action.Y + action.Height / 2)
        };
        using (Frame(input: pointer with
        {
            LeftMouseDown = true,
            LeftMousePressed = true
        }))
        {
        }
        using (Frame(input: pointer with
        {
            LeftMouseReleased = true
        }))
        {
        }
        check(calls == 1 && popover.IsOpen, "Entry accepts one actual pointer action at its displayed accessibility bounds");
        using (var settled = Frame(.36f))
        {
            check(context.State.NextScheduledFrameTimeSeconds is null && groupUpdates == 0,
                "Entry completion returns to event wait without another group render");
        }
        using (var retired = Frame())
            check(retired.TextureUpdates.Any(x => x.Kind == UiTextureUpdateKind.Destroy && x.TextureId == groupTexture),
                "Settled entry releases its transient composite textures at the next normal frame boundary");
        popover.Close();
        popover.Show();
        using (Frame())
        {
        }
        var close = Find("Motion close");
        check(tree.RequestAction(close.Node.Id, close.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "Entry child queues a normal close Invoke");
        using (var closed = Frame(.05f))
            check(!popover.IsOpen && groupUpdates == 0 && context.State.NextScheduledFrameTimeSeconds is null,
                "Closing from captured live content is immediate and safely releases the active capture");
        popover.Show();
        using (Frame())
        {
        }
        visible = false;
        using (Frame(.05f))
            check(!popover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "Hidden animated owner leaves no motion wake");
        visible = true;
        popover.Options = popover.Options with
        {
            MotionEnabled = false
        };
        popover.Show();
        using (Frame())
            check(groupUpdates == 0 && context.State.NextScheduledFrameTimeSeconds is null, "Motion opt-out paints normally without animation targets or wakes");
        popover.Close();
        origin = new(280, 220);
        foreach (var rtl in new[] { false, true })
        {
            foreach (var position in new[] { UiPopupPosition.Above, UiPopupPosition.Below, UiPopupPosition.Before, UiPopupPosition.After })
            {
                popover.Options = popover.Options with
                {
                    MotionEnabled = true,
                    Positioning = new()
                    {
                        Position = position,
                        RightToLeft = rtl,
                        Pinned = true
                    }
                };
                popover.Show();
                using (Frame())
                {
                }
                var first = Find("Motion owner", UiAccessibilityRole.Group).Node.Bounds;
                using (Frame(.05f))
                {
                }
                var middle = Find("Motion owner", UiAccessibilityRole.Group).Node.Bounds;
                using (Frame(.36f))
                {
                }
                var last = Find("Motion owner", UiAccessibilityRole.Group).Node.Bounds;
                var expected = position switch
                {
                    UiPopupPosition.Above => new UiVector2(0, 10),
                    UiPopupPosition.Below => new UiVector2(0, -10),
                    UiPopupPosition.Before => new UiVector2(rtl ? -10 : 10, 0),
                    _ => new UiVector2(rtl ? 10 : -10, 0)
                };
                check(MathF.Abs(first.X - last.X - expected.X) < .001f && MathF.Abs(first.Y - last.Y - expected.Y) < .001f
                    && MathF.Abs(middle.X - last.X - expected.X / 2) < .001f && MathF.Abs(middle.Y - last.Y - expected.Y / 2) < .001f,
                    $"Entry follows resolved physical direction with RTL : {position} / {rtl}");
                popover.Close();
            }
        }
        popover.Show();
        using (Frame())
        {
        }
        popover.Options = popover.Options with
        {
            MotionEnabled = false
        };
        using (Frame())
            check(context.State.NextScheduledFrameTimeSeconds is null && groupUpdates == 0,
                "Disabling an active entry immediately retires its motion target and wake");
        popover.Close();
        popover.Options = popover.Options with
        {
            MotionEnabled = true
        };
        popover.Show();
        using (Frame())
        {
        }
        popover.Dispose();
        visible = false;
        using (Frame())
            check(context.State.NextScheduledFrameTimeSeconds is null, "Disposed animated owner leaves no scheduled wake");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
