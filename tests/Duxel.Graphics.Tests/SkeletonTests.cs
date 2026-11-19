using Duxel.Core;

internal static class SkeletonTests
{
    internal static void Run(Action<bool, string> check)
    {
        var defaults = UiSkeletonStyle.Default;
        check(defaults.Size == 16 && defaults.Animation == UiSkeletonAnimation.Wave
            && defaults.Appearance == UiSkeletonAppearance.Opaque && defaults.Shape == UiSkeletonShape.Rectangle,
            "Skeleton defaults match the pinned official context");
        foreach (var size in new[] { 8, 12, 14, 16, 20, 22, 24, 28, 32, 36, 40, 48, 52, 56, 64, 72, 92, 96, 120, 128 })
            check(new UiSkeletonOptions(Size: size).Resolve(defaults).Size == size, "Skeleton supports official size " + size);
        foreach (var bad in new[] { new UiSkeletonOptions(Size: 13), new UiSkeletonOptions(Size: 0),
            new UiSkeletonOptions(Width: float.NaN), new UiSkeletonOptions(Animation: (UiSkeletonAnimation)5),
            new UiSkeletonOptions(Shape: (UiSkeletonShape)9) })
        {
            var rejected = false;
            try
            {
                bad.Validate();
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "Invalid Skeleton options fail before layout: " + bad);
        }
        var wave = UiSkeletonMotion.Sample(defaults, 1.5);
        check(MathF.Abs(wave.WaveOffset) < .00001f && wave.Animated,
            "Skeleton three-second wave reaches its midpoint at1.5seconds");
        var pulse = defaults with
        {
            Animation = UiSkeletonAnimation.Pulse
        };
        check(UiSkeletonMotion.Sample(pulse, 0).Opacity == 1
            && MathF.Abs(UiSkeletonMotion.Sample(pulse, .5).Opacity - .4f) < .00001f
            && UiSkeletonMotion.Sample(pulse, 1).Opacity == 1,
            "Skeleton one-second pulse preserves official endpoint opacity");
        check(!UiSkeletonMotion.Sample(defaults with
        {
            ReducedMotion = true
        }, 4).Animated
            && !UiSkeletonMotion.Sample(defaults with
            {
                Paused = true
            }, 4).Animated,
            "Reduced and paused Skeleton frames do not animate");

        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiDrawData Frame(Action<UiImmediateContext> render, float delta = 0)
        {
            context.NewFrame(new(delta, new(400, 240), new(1, 1)), idle, new(0, 0, 400, 240), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); render(ui); }));
            return context.GetDrawData();
        }
        using var child = Fluent.SkeletonItem();
        using var root = Fluent.Skeleton(child, new(Size: 24, Shape: UiSkeletonShape.Square, Paused: true));
        root.AccessibleName = "Profile loading";
        var sizeResult = default(UiVector2);
        using (var data = Frame(ui => { root.Render(ui); sizeResult = ui.GetItemRectSize(); }))
        {
        }
        check(sizeResult == new UiVector2(24, 24), "Skeleton context changes the real child measurement");
        root.Options = root.Options with
        {
            Size = 48
        };
        using (var data = Frame(ui => { root.Render(ui); sizeResult = ui.GetItemRectSize(); }))
        {
        }
        check(sizeResult == new UiVector2(48, 48), "Inherited size invalidates retained child arrangement");
        child.Options = new(Size: 12, Shape: UiSkeletonShape.Rectangle, Width: 120);
        using (var data = Frame(ui => { root.Render(ui); sizeResult = ui.GetItemRectSize(); }))
        {
        }
        check(sizeResult == new UiVector2(120, 12), "Local SkeletonItem overrides take precedence over inherited defaults");
        var node = tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == "Profile loading").Node;
        check(node.Role == UiAccessibilityRole.ProgressBar && node.Busy && !node.Focusable && !node.ValuePattern
            && node.Bounds.Width == 120 && node.Bounds.Height == 12,
            "Skeleton exposes one named nonfocusable indeterminate progress root");
        check(!context.State.HasActiveAnimations, "Paused Skeleton does not sustain the event loop");
        root.Options = root.Options with
        {
            Paused = false
        };
        using (var data = Frame(root.Render, .25f))
        {
        }
        check(context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds > context.State.TimeSeconds,
            "Visible Skeleton uses the shared paced animation deadline");
        using (var data = Frame(ui => { ui.SetCursorScreenPos(new(1000, 1000)); root.Render(ui); }))
        {
        }
        check(!context.State.HasActiveAnimations, "Offscreen Skeleton schedules no animation");
        root.Options = root.Options with
        {
            ReducedMotion = true
        };
        using (var data = Frame(root.Render))
        {
        }
        check(!context.State.HasActiveAnimations, "Reduced-motion Skeleton remains static");
        using var standalone = Fluent.SkeletonItem(new(Shape: UiSkeletonShape.Circle, Size: 32, Paused: true));
        using (var data = Frame(ui => { standalone.Render(ui); sizeResult = ui.GetItemRectSize(); }))
        {
        }
        check(sizeResult == new UiVector2(32, 32), "Standalone SkeletonItem restores defaults after leaving a parent scope");
        using var innerItem = Fluent.SkeletonItem();
        using var inner = Fluent.Skeleton(innerItem, new(Shape: UiSkeletonShape.Circle));
        using var outer = Fluent.Skeleton(inner, new(Size: 28, Paused: true));
        using (var data = Frame(ui => { outer.Render(ui); sizeResult = ui.GetItemRectSize(); }))
        {
        }
        check(sizeResult == new UiVector2(28, 28), "Nested Skeleton contexts inherit size and override only shape");
        outer.Options = outer.Options with
        {
            Size = 64
        };
        using (var data = Frame(ui => { outer.Render(ui); sizeResult = ui.GetItemRectSize(); }))
        {
        }
        check(sizeResult == new UiVector2(64, 64), "Outer context changes invalidate nested retained environments");
        using var ellipse = Fluent.SkeletonItem(new(Shape: UiSkeletonShape.Circle, Size: 20, Width: 80, Paused: true));
        using (var data = Frame(ellipse.Render))
            check(data.DrawLists.Any(list => list.Scene3D is not null),
                "Nonsquare circle allocation consumes the common affine analytic path");
        using var failing = Fluent.Skeleton(new ThrowingMeasure(), new(Size: 128));
        using var unconfigured = Fluent.SkeletonItem(new(Shape: UiSkeletonShape.Circle, Paused: true));
        var restored = false;
        using (var data = Frame(ui =>
        {
            try
            {
                failing.Measure(ui, new(100, 100));
            }
            catch (NotSupportedException) { restored = true; }
            sizeResult = unconfigured.Measure(ui, new(100, 100));
        }))
        {
        }
        check(restored && sizeResult == new UiVector2(16, 16), "Failed child measurement restores the inherited environment");
        check(Fluent.ComponentNames.Contains("Skeleton") && Fluent.ComponentNames.Contains("SkeletonItem")
            && !Fluent.RequiredComponentNames.Contains("SkeletonItem") && !Fluent.MissingComponentNames.Contains("Skeleton"),
            $"Compiled Skeleton catalog keeps variants under their parent: factories={Fluent.ComponentNames.Length}, parents={Fluent.RequiredComponentNames.Length}, missing={Fluent.MissingComponentNames.Length}");
    }

    private sealed class ThrowingMeasure : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => throw new NotSupportedException("Fixture measurement failure.");
        protected override void RenderControl(UiImmediateContext ui) => throw new InvalidOperationException("Not rendered.");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
