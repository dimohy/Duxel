using Duxel.Core;
using Duxel.Platform.Linux;

internal static class ComponentScrollTests
{
    internal static void Run(string primary, string secondary, Action<bool, string> check)
    {
        foreach (var (outerRtl, innerRtl) in new[] { (false, false), (true, true), (true, false), (false, true) })
        {
            using var tree = new UiAccessibilityTree();
            using var wake = new ManualResetEventSlim();
            using var backend = new LinuxPlatformTextBackend();
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            context.SetPlatformTextBackend(backend);
            context.SetDirectTextEnabled(true);
            context.SetDirectTextPrimaryFontPath(primary);
            context.SetDirectTextSecondaryFontPath(secondary);
            context.SetAccessibilityTree(tree);
            tree.SetRequestFrameCallback(wake.Set);
            using var scene = new Scene(outerRtl, innerRtl);
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            UiAccessibilityElement Frame()
            {
                context.NewFrame(new(.1f, new(500, 400), new(1, 1)), idle, new(0, 0, 500, 400), UiTextSettings.Default);
                context.Render(scene);
                context.GetDrawData().ReleasePooled();
                return tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == Scene.TargetName);
            }
            UiAccessibilityElement Reset()
            {
                scene.Outer.Offset = default;
                scene.Inner.Offset = default;
                return Frame();
            }
            Task<UiAccessibilityResult> Begin(UiAccessibilityElement node, UiAccessibilityScrollAlignment alignment)
            {
                wake.Reset();
                var task = Task.Run(() => tree.RequestScrollIntoView(node.Node.Id, node.RuntimeId, alignment));
                if (!wake.Wait(1000))
                    throw new InvalidOperationException(task.IsCompleted ? "Component admission failed: " + task.Result : "Component request was not queued.");
                return task;
            }
            UiAccessibilityResult Finish(Task<UiAccessibilityResult> task)
            {
                for (var frame = 0; frame < 4 && !task.IsCompleted; frame++)
                {
                    Frame();
                    task.Wait(10);
                }
                if (!task.Wait(1000))
                    throw new TimeoutException("The bounded component publication did not complete.");
                return task.Result;
            }
            UiRect Viewport()
            {
                var outer = tree.CaptureSnapshot().Elements.First(e => e.Node.Scroll is not null).Node;
                var viewport = outer.Scroll!.Value.Viewport;
                return new(outer.Bounds.X + (outerRtl ? outer.Bounds.Width - viewport.X : 0), outer.Bounds.Y, viewport.X, viewport.Y);
            }
            var node = Reset();
            var label = $"outerRTL={outerRtl},innerRTL={innerRtl}";
            check(node.Node.Offscreen && node.Node.CanScrollIntoView, "A real nested button starts offscreen with scroll-item support, " + label);
            foreach (var placement in Enum.GetValues<UiAccessibilityScrollPlacement>())
            {
                node = Reset();
                var runtimeId = node.RuntimeId;
                var task = Begin(node, new(placement));
                Frame();
                check(!task.IsCompleted && scene.Outer.Offset == default && scene.Inner.Offset == default,
                    "A component action waits for the planned movement to be published, " + placement + "/" + label);
                check(Finish(task) == UiAccessibilityResult.Success, "A nested component placement completes, " + placement + "/" + label);
                node = Frame();
                var box = node.Node.Bounds;
                var visible = Viewport();
                check(!node.Node.Offscreen && box.X >= visible.X - .05f && box.Y >= visible.Y - .05f
                    && box.X + box.Width <= visible.X + visible.Width + .05f && box.Y + box.Height <= visible.Y + visible.Height + .05f,
                    $"The complete component is visible, {placement}/{label}; box={box}; viewport={visible}");
                var left = placement is UiAccessibilityScrollPlacement.TopLeft or UiAccessibilityScrollPlacement.LeftEdge;
                var top = placement is UiAccessibilityScrollPlacement.TopLeft or UiAccessibilityScrollPlacement.TopEdge;
                var right = placement is UiAccessibilityScrollPlacement.BottomRight or UiAccessibilityScrollPlacement.RightEdge;
                var bottom = placement is UiAccessibilityScrollPlacement.BottomRight or UiAccessibilityScrollPlacement.BottomEdge;
                check((!left || MathF.Abs(box.X - visible.X) < .05f) && (!top || MathF.Abs(box.Y - visible.Y) < .05f)
                    && (!right || MathF.Abs(box.X + box.Width - visible.X - visible.Width) < .05f)
                    && (!bottom || MathF.Abs(box.Y + box.Height - visible.Y - visible.Height) < .05f),
                    "The requested component edges reach the actual outer viewport, " + placement + "/" + label);
                check(node.RuntimeId == runtimeId && !node.Node.Focused && scene.Activations == 0,
                    "Component reveal preserves runtime identity and does not focus or invoke the button, " + placement + "/" + label);
            }
            node = Reset();
            var point = new UiVector2(80, 70);
            check(Finish(Begin(node, new(point))) == UiAccessibilityResult.Success, "Nested component-to-point completes, " + label);
            node = Frame();
            check(MathF.Abs(node.Node.Bounds.X - point.X) < .05f && MathF.Abs(node.Node.Bounds.Y - point.Y) < .05f,
                $"Component client point remains correct through both scroll owners, {label}; actual={node.Node.Bounds}");
            var oldBounds = node.Node.Bounds;
            var oldOuter = scene.Outer.Offset;
            var oldInner = scene.Inner.Offset;
            check(Finish(Begin(node, new(UiAccessibilityScrollPlacement.Nearest))) == UiAccessibilityResult.Success
                && Frame().Node.Bounds == oldBounds && scene.Outer.Offset == oldOuter && scene.Inner.Offset == oldInner,
                "Repeating nearest reveal leaves visible component geometry unchanged, " + label);
            node = Reset();
            check(Finish(Begin(node, new(new UiVector2(300, 250)))) == UiAccessibilityResult.InvalidArgument
                && scene.Outer.Offset == default && scene.Inner.Offset == default,
                "An out-of-viewport component point fails before any owner moves, " + label);
            check(tree.RequestScrollIntoView(node.Node.Id, node.RuntimeId + 1, new(), wait: false) == UiAccessibilityResult.Unavailable,
                "A stale runtime identity cannot scroll its replacement, " + label);
            scene.Target.Size = new(420, 220);
            node = Reset();
            check(Finish(Begin(node, new(UiAccessibilityScrollPlacement.Nearest))) == UiAccessibilityResult.Success,
                "An oversized component reveals its available viewport slice, " + label);
            node = Frame();
            oldOuter = scene.Outer.Offset;
            oldInner = scene.Inner.Offset;
            check(Finish(Begin(node, new(UiAccessibilityScrollPlacement.Nearest))) == UiAccessibilityResult.Success
                && scene.Outer.Offset == oldOuter && scene.Inner.Offset == oldInner,
                "Repeated nearest placement does not oscillate an oversized component between opposite edges, " + label);
        }
        foreach (var invalid in new[] { new UiVector2(float.NaN, 0), new UiVector2(0, float.PositiveInfinity) })
        {
            var rejected = false;
            try
            {
                _ = new UiAccessibilityScrollAlignment(invalid);
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "Nonfinite component points fail at construction");
        }
        var invalidPlacement = false;
        try
        {
            _ = new UiAccessibilityScrollAlignment((UiAccessibilityScrollPlacement)7);
        }
        catch (ArgumentOutOfRangeException) { invalidPlacement = true; }
        check(invalidPlacement, "An invalid common placement fails at construction");
    }

    internal sealed class Scene : UiScreen, IDisposable
    {
        internal const string TargetName = "Scroll target";
        internal UiScrollView Inner
        {
            get;
        }
        internal UiScrollView Outer
        {
            get;
        }
        internal int Activations;
        internal UiButton Target
        {
            get;
        }
        internal ManualResetEventSlim FirstFrame { get; } = new();

        internal Scene(bool outerRtl = false, bool innerRtl = false)
        {
            Target = new(TargetName, () => Activations++)
            {
                Size = new(120, 32)
            };
            Inner = new(Dux.Padding(Target, new(300, 300, 400, 400)), new(UiScrollAxis.Both, 360, 300)
            {
                RightToLeft = innerRtl
            });
            Outer = new(Inner, new(UiScrollAxis.Both, 240, 160)
            {
                RightToLeft = outerRtl
            });
        }

        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(10, 10));
            Outer.Render(ui);
            FirstFrame.Set();
        }

        public void Dispose()
        {
            Outer.Dispose();
            FirstFrame.Dispose();
        }
    }
}
