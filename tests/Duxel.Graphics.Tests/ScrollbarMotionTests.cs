using Duxel.Core;

internal static class ScrollbarMotionTests
{
    internal static void Run(Action<bool, string> check)
    {
        var motion = new UiScrollbarMotion();
        var initial = motion.Update(0, false, false, 0);
        check(initial.Opacity == .28f && initial.NextFrameTimeSeconds is null,
            "Shared View scrollbar starts faint without requesting idle frames");
        var start = motion.Update(10, false, false, 1);
        check(start.Opacity == .28f && start.Animating && start.NextFrameTimeSeconds is not null,
            "Shared scrollbar movement starts a finite entry timeline");
        var entering = motion.Update(10, false, false, 1.06);
        check(entering.Opacity > 0 && entering.Opacity < 1, "Shared scrollbar entry samples intermediate opacity");
        var held = motion.Update(10, false, false, 1.2);
        check(held.Opacity == 1 && !held.Animating && held.NextFrameTimeSeconds == 1.4,
            "Shared scrollbar hold schedules only the actual hide deadline");
        var leaving = motion.Update(10, false, false, 1.48);
        check(leaving.Opacity > 0 && leaving.Opacity < 1 && leaving.Animating,
            "Shared scrollbar exit catches up from the hold deadline");
        var reversed = motion.Update(20, false, false, 1.48);
        check(reversed.Opacity == leaving.Opacity, "Shared scrollbar reentry reverses continuously without jumping");
        check(motion.Update(20, false, false, 1.54).Opacity > reversed.Opacity,
            "Shared scrollbar reversed entry progresses toward visible");
        var idle = motion.Update(20, false, false, 10);
        check(idle.Opacity == .28f && !idle.Animating && idle.NextFrameTimeSeconds is null,
            "Late View scrollbar samples settle faint without restarting exit animation");
        motion.Reset();
        motion.Update(0, true, false, 0);
        var active = motion.Update(0, true, false, 100);
        check(active.Opacity == 1 && active.NextFrameTimeSeconds is null,
            "A held scrollbar stays visible without periodic frames after entry");
        var released = motion.Update(0, false, false, 101);
        check(released.Opacity == 1 && released.NextFrameTimeSeconds == 101.4,
            "Scrollbar release starts the common hold even after a long stationary drag");
        motion.Reset();
        motion.Update(10_000_000_000_000, false, false, 0);
        motion.Update(10_000_000_000_000, false, false, 10);
        var precise = motion.Update(10_000_000_000_001, false, false, 11);
        check(precise.Animating && motion.TargetVisible, "Shared scrollbar detects one-unit movement at large double offsets");
        motion.Reset();
        var reduced = motion.Update(1, false, false, 0, new(ReducedMotion: true));
        check(reduced.Opacity == 1 && !reduced.Animating, "Reduced motion snaps scrollbar entry while retaining its hold");
        check(motion.Update(1, false, false, 1, new(ReducedMotion: true)).NextFrameTimeSeconds is null,
            "Reduced-motion scrollbar exits without an animation loop");
        motion.Reset();
        var permanent = motion.Update(0, false, false, 0, alwaysVisible: true);
        check(permanent.Opacity == 1 && permanent.NextFrameTimeSeconds is null,
            "AlwaysShowThumb remains an explicit stationary visibility policy");
        VerifyConsumers(check);
        VerifyImmediateRegrab(check);
    }

    private static void VerifyImmediateRegrab(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        const string id = "vertical-regrab";
        var clip = new UiRect(0, 0, 320, 240);
        var track = new UiRect(200, 0, 12, 180);
        var offset = 0f;
        var idle = new UiInputState(new(206, 14), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var screen = new Screen(ui => offset = ui.RenderScrollbarV(id, track, offset, 1000, 1180, clip));

        UiRect Frame(params UiInputState[] inputs)
        {
            context.NewInputBatch(new(.02f, new(320, 240), new(1, 1)), inputs, clip, UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
            return draw.DrawLists.SelectMany(list => list.RectFilledPrimitives?.AsSpan().ToArray() ?? [])
                .Single(rectangle => rectangle.Rect.X == track.X + 2 && rectangle.Rect.Width == track.Width - 4).Rect;
        }

        Frame(idle with { LeftMouseDown = true, LeftMousePressed = true });
        var dragged = idle with { MousePosition = new(206, 100), LeftMouseDown = true };
        var displayedThumb = Frame(dragged);
        check(offset > 0 && displayedThumb.Y > 80,
            "Vertical dragging paints the thumb at the updated scroll offset in the same frame");
        var regrab = idle with { MousePosition = new(206, displayedThumb.Y + displayedThumb.Height / 2) };
        Frame(dragged with { LeftMouseDown = false, LeftMouseReleased = true },
            regrab with { LeftMouseDown = true, LeftMousePressed = true });
        check(context.State.ActiveId == id && context.State.PreciseScrollbarDrag is { Id: id },
            "Release and re-press in one batch can grab the last displayed thumb");
        var grabbedOffset = offset;
        Frame(regrab with { MousePosition = regrab.MousePosition with { Y = regrab.MousePosition.Y + 30 }, LeftMouseDown = true });
        check(offset > grabbedOffset, "A re-grab immediately starts a second scrollbar drag");
    }

    private static void VerifyConsumers(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var offset = 0f;
        var clipped = false;
        void Frame(float delta = .01f)
        {
            context.NewFrame(new(delta, new(320, 240), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                var clip = clipped ? default : new UiRect(0, 0, 320, 240);
                ui.RenderScrollbarV("shared-motion-v", new(200, 0, 12, 180), offset, 1000, 1180, clip);
                ui.RenderScrollbarH("shared-motion-h", new(0, 200, 180, 12), offset, 1000, 1180, clip);
            }));
            using var data = context.GetDrawData();
        }
        Frame();
        check(context.State.ScrollbarMotionCount == 2 && context.State.NextScheduledFrameTimeSeconds is null,
            "Both standard scrollbar axes register hidden motion without idle deadlines");
        offset = 50;
        Frame();
        check(context.State.NextScheduledFrameTimeSeconds is not null && !context.State.HasActiveAnimations,
            "Both standard scrollbar axes consume bounded common motion scheduling");
        Frame(1);
        check(context.State.NextScheduledFrameTimeSeconds is null,
            "Standard scrollbar axes settle after a late frame without continued rendering");
        offset = 60;
        Frame();
        clipped = true;
        Frame();
        check(context.State.ScrollbarMotionCount == 0 && context.State.NextScheduledFrameTimeSeconds is null,
            "Clipped standard scrollbars retire motion and leave no scheduled wake");
        using var owner = Dux.ScrollView(new LargeContent(), new(Axis: UiScrollAxis.Both, Width: 100, Height: 80));
        owner.Offset = new(20, 30);
        context.NewFrame(new(.01f, new(320, 240), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            owner.Render(ui);
        }));
        using (var draw = context.GetDrawData())
        {
        }
        check(context.State.ScrollbarMotionCount == 2 && context.State.NextScheduledFrameTimeSeconds is not null,
            "Real moving ScrollView axes use the shared motion authority");
        owner.Dispose();
        check(context.State.ScrollbarMotionCount == 0, "Real ScrollView disposal releases both owned motion tracks");
        context.NewFrame(new(.01f, new(320, 240), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(_ => { }));
        using var retiredDraw = context.GetDrawData();
        check(context.State.NextScheduledFrameTimeSeconds is null,
            "Retired ScrollView owners cannot renew their old animation deadline");
    }
    private sealed class LargeContent : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(new(500, 600));
        protected override void RenderControl(UiImmediateContext ui) => ui.Dummy(new(500, 600));
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
