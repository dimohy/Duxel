using Duxel.Core;

internal static class FloatTransitionTests
{
    internal static void Run(Action<bool, string> check)
    {
        var transition = new UiFloatTransition(0);
        var linear = new UiTransitionOptions(1, UiAnimationEasing.Linear);
        var first = transition.Update(1, 0, linear);
        check(first.Value == 0 && first.Running && first.NextFrameTimeSeconds == 1d / 60,
            "Owned transition starts at its explicit value with one finite frame deadline");
        var half = transition.Update(1, .5, linear);
        check(half.Value == .5f && half.Running, "Owned transition samples its configured linear midpoint");
        var reversed = transition.Update(0, .5, linear);
        check(reversed.Value == half.Value, "Retargeting reverses continuously from the current sampled value");
        check(transition.Update(0, 1, linear).Value == .25f, "Reversed transition follows its new timeline");
        var end = transition.Update(0, 1.5, linear);
        check(end.Value == 0 && !end.Running && end.NextFrameTimeSeconds is null,
            "A settled owned transition has no render deadline");
        check(transition.Update(0, 20, linear).NextFrameTimeSeconds is null,
            "Repeated stationary transition samples do not restart animation");
        var snap = transition.Update(1, 20, linear with
        {
            ReducedMotion = true
        });
        check(snap.Value == 1 && !snap.Running && snap.NextFrameTimeSeconds is null,
            "Reduced-motion transition resolves immediately without waking the render loop");
        transition.Reset(0);
        var restarted = transition.Update(1, 0, linear);
        check(restarted.Value == 0 && restarted.Running, "Reset releases the old clock and preserves the explicit initial state");
        var invalid = false;
        try
        {
            transition.Update(1, -.1, linear);
        }
        catch (ArgumentOutOfRangeException) { invalid = true; }
        check(invalid, "Owned transition rejects invalid clocks before scheduling");
        var wide = new UiFloatTransition(-float.MaxValue);
        wide.Update(float.MaxValue, 0, linear);
        check(wide.Update(float.MaxValue, .5, linear).Value == 0,
            "Finite transition endpoints interpolate without overflowing the float difference");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var target = 0f;
        var owned = new UiFloatTransition(0);
        float sampled = 0;
        void Frame(float delta)
        {
            context.NewFrame(new(delta, new(100, 100), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 100, 100), UiTextSettings.Default);
            context.Render(new Screen(ui => sampled = ui.AnimateFloat(ref owned, target, new(.1, UiAnimationEasing.Linear))));
            using var data = context.GetDrawData();
        }
        Frame(.01f);
        check(context.State.NextScheduledFrameTimeSeconds is null && !context.State.HasActiveAnimations,
            "Common transition API leaves an unchanged frame in event wait");
        target = 1;
        Frame(.01f);
        check(sampled == 0 && context.State.NextScheduledFrameTimeSeconds is not null && !context.State.HasActiveAnimations,
            "Common transition API uses bounded scheduling instead of a continuous animation flag");
        Frame(.05f);
        check(sampled > 0 && sampled < 1, "Common transition API paints intermediate values");
        Frame(.1f);
        check(sampled == 1 && context.State.NextScheduledFrameTimeSeconds is null && !context.State.HasActiveAnimations,
            "Common transition API returns to event wait at the exact endpoint");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
