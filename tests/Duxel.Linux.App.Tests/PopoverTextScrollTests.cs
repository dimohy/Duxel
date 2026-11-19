using Duxel.Core;
using Duxel.Platform.Linux;

internal static class PopoverTextScrollTests
{
    internal static void Run(string primary, string secondary, Action<bool, string> check, bool trapFocus = false)
    {
        foreach (var mode in new[] { "single", "multiline", "markdown" })
        {
            using var tree = new UiAccessibilityTree();
            using var wake = new ManualResetEventSlim();
            using var backend = new LinuxPlatformTextBackend();
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            context.SetPlatformTextBackend(backend);
            context.SetDirectTextEnabled(true);
            context.SetDirectTextPrimaryFontPath(primary);
            context.SetDirectTextSecondaryFontPath(secondary);
            context.SetTypographyFonts(new(primary, primary, primary, secondary, secondary, secondary) { Monospace = primary });
            context.SetAccessibilityTree(tree);
            tree.SetRequestFrameCallback(wake.Set);
            using var surfaceOwner = new SurfaceOwner(mode, trapFocus);
            var popover = surfaceOwner.Popover;
            var owner = surfaceOwner.Owner;
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            var screen = new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(10, 10));
                owner.Render(ui);
            });
            void Frame()
            {
                context.NewFrame(new(.1f, new(800, 600), new(1, 1)), idle, new(0, 0, 800, 600), UiTextSettings.Default);
                context.Render(screen);
                context.GetDrawData().ReleasePooled();
            }
            UiAccessibilityElement Text() => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Nested text");
            var surfaceRole = trapFocus ? UiAccessibilityRole.Dialog : UiAccessibilityRole.Group;
            UiRect Surface() => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Popup text" && e.Node.Role == surfaceRole).Node.Bounds;
            Task<UiAccessibilityResult> Begin(UiAccessibilityElement node, UiAccessibilityTextScroll scroll)
            {
                wake.Reset();
                var task = Task.Run(() => tree.RequestTextScroll(node.Node.Id, node.RuntimeId, scroll));
                if (!wake.Wait(1000))
                    throw new InvalidOperationException(task.IsCompleted ? "Popup admission: " + task.Result : "Popup request did not wake its frame.");
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
                    throw new TimeoutException("Popup source-range publication did not complete within its frame budget.");
                return task.Result;
            }
            Frame();
            popover.Show();
            Frame();
            Frame();
            var node = Text();
            var original = node.Node.TextInfo!;
            var runtime = node.RuntimeId;
            var saved = owner.Offset;
            var surface = Surface();
            var target = mode == "single" ? 30 : 8 * 63 + 20;
            check(popover.IsOpen && node.Node.Offscreen && node.Node.CanScrollIntoView && saved.X > 0 && saved.Y > 0,
                "The actual floating popup owns an offscreen source while its logical owner can scroll both axes, " + mode);
            foreach (var placement in Enum.GetValues<UiAccessibilityTextScrollPlacement>())
            {
                var content = Text().Node.TextInfo!.Content;
                var result = Finish(Begin(Text(), new(content, content.GetUtf16Offset(target), content.GetUtf16Offset(target + 1), placement)));
                check(result == UiAccessibilityResult.Success,
                    $"Popup source-range placement completes after its own viewport publication, {mode}/{placement}; result={result}; owner={owner.Offset}; expected={saved}");
                node = Text();
                var cell = node.Node.TextLayout!.GetCharacterExtents(target);
                check(owner.Offset == saved && Surface() == surface && node.RuntimeId == runtime
                    && cell.X >= surface.X && cell.X + cell.Width <= surface.X + surface.Width
                    && cell.Y >= surface.Y && cell.Y + cell.Height <= surface.Y + surface.Height
                    && node.Node.TextLayout.GetOffsetAtPoint(cell.X + cell.Width / 2, cell.Y + cell.Height / 2) == target,
                    $"Floating source movement stays inside the popup without scrolling its logical owner, {mode}/{placement}; owner={owner.Offset}; expected={saved}; cell={cell}; surface={surface}");
            }
            var current = node.Node.TextInfo!.Content;
            var point = new UiVector2(surface.X + 40, surface.Y + 70);
            check(Finish(Begin(node, new(current, current.GetUtf16Offset(target), current.GetUtf16Offset(target + 1), point))) == UiAccessibilityResult.Success,
                "A popup source point uses popup-local horizontal scrolling and its independent vertical viewport, " + mode);
            node = Text();
            var positioned = node.Node.TextLayout!.GetCharacterExtents(target);
            check(owner.Offset == saved && MathF.Abs(positioned.X - point.X) < .05f && MathF.Abs(positioned.Y - point.Y) < .05f,
                $"Popup source point alignment never delegates an axis to the logical owner's scroll view, {mode}; actual={positioned}; expected={point}; owner={owner.Offset}");
            var invalid = Finish(Begin(node, new(current, current.GetUtf16Offset(target), current.GetUtf16Offset(target + 1), new UiVector2(15, 15))));
            check(invalid == UiAccessibilityResult.InvalidArgument && owner.Offset == saved,
                "A point in the logical owner but outside the popup is rejected without moving the owner, " + mode);
            check(node.Node.TextInfo!.Content.Text == original.Content.Text && node.Node.TextInfo.CaretUtf16 == original.CaretUtf16
                && node.Node.TextInfo.AnchorUtf16 == original.AnchorUtf16 && !node.Node.Focused,
                "Popup reveal preserves the committed source, selection and unrelated keyboard focus, " + mode);
            var surfaceNode = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Popup text" && e.Node.Role == surfaceRole);
            var popupViewport = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Scroll view" && e.Node.ParentId == surfaceNode.Node.Id);
            check(tree.RequestScroll(popupViewport.Node.Id, popupViewport.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 0), wait: false) == UiAccessibilityResult.Success,
                "The popup viewport accepts resetting its own vertical scroll before a retirement request, " + mode);
            Frame();
            node = Text();
            var closing = Begin(node, new(current, current.Text.Length, current.Text.Length));
            Frame();
            check(!closing.IsCompleted, "An offscreen popup source request is still pending before the popup closes, " + mode);
            check(popover.Close(), "The owned popup accepts closing after source-range admission, " + mode);
            Frame();
            check(closing.Wait(1000) && closing.Result == UiAccessibilityResult.Unavailable && owner.Offset == saved
                && !tree.CaptureSnapshot().Elements.Any(e => e.Node.Name == "Nested text"),
                "Closing a popup retires its pending source request without scrolling its former logical owner, " + mode);
        }
    }

    internal sealed class SurfaceOwner : IDisposable
    {
        internal UiPopover<UiFluentPopoverPresentation> Popover { get; }
        internal UiScrollView Owner { get; }
        internal SurfaceOwner(string mode, bool trapFocus = false)
        {
            var probe = new AncestorTextScrollTests.Probe(mode, markdownWrap: false);
            Popover = Fluent.Popover("Popup text", Dux.Padding(probe, new(0, 300, 0, 100)),
                new(Width: 280, MaximumHeight: 180, DisableAutoFocus: true, MotionEnabled: false, TrapFocus: trapFocus));
            Owner = new(Dux.Padding(Popover, new(200, 200, 300, 300)), new(UiScrollAxis.Both, 240, 160)) { Offset = new(180, 180) };
        }
        public void Dispose() => Owner.Dispose();
    }

    internal sealed class NativeScreen : UiScreen, IDisposable
    {
        private readonly SurfaceOwner _surface;
        internal ManualResetEventSlim FirstFrame { get; } = new();
        internal NativeScreen(string mode)
        {
            _surface = new(mode);
            _surface.Popover.Show();
        }
        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(10, 10));
            _surface.Owner.Render(ui);
            FirstFrame.Set();
        }
        public void Dispose()
        {
            _surface.Dispose();
            FirstFrame.Dispose();
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
