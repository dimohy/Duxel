using Duxel.Core;
using Duxel.Platform.Linux;

internal static class AncestorTextScrollTests
{
    internal static void Run(string primary, string secondary, Action<bool, string> check,
        bool outerRightToLeft = false, bool innerRightToLeft = false)
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
            var probe = new Probe(mode);
            var padded = Dux.Padding(probe, new(300, 300, 100, 100));
            var inner = new UiScrollView(padded, new(UiScrollAxis.Both, 360, 300) { RightToLeft = innerRightToLeft });
            using var outer = new UiScrollView(inner, new(UiScrollAxis.Both, 240, 160) { RightToLeft = outerRightToLeft });
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            var screen = new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(10, 10));
                outer.Render(ui);
            });
            UiAccessibilityElement Frame()
            {
                context.NewFrame(new(.1f, new(500, 400), new(1, 1)), idle, new(0, 0, 500, 400), UiTextSettings.Default);
                context.Render(screen);
                context.GetDrawData().ReleasePooled();
                return tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Nested text");
            }
            UiAccessibilityElement Reset()
            {
                outer.Offset = default;
                inner.Offset = default;
                return Frame();
            }
            Task<UiAccessibilityResult> Begin(UiAccessibilityElement node, UiAccessibilityTextScroll scroll)
            {
                wake.Reset();
                var task = Task.Run(() => tree.RequestTextScroll(node.Node.Id, node.RuntimeId, scroll));
                if (!wake.Wait(1000))
                    throw new InvalidOperationException(task.IsCompleted ? "Nested text admission failed: " + task.Result
                        : "The owned nested text request did not enter its render queue.");
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
                    throw new TimeoutException("The bounded nested text publication did not complete.");
                return task.Result;
            }
            var node = Reset();
            var original = probe.Value;
            var caret = node.Node.TextInfo!.CaretUtf16;
            var anchor = node.Node.TextInfo.AnchorUtf16;
            var target = mode == "single" ? 30 : 8 * 63 + 20;
            check(node.Node.Offscreen && node.Node.CanScrollIntoView && node.Node.TextLayout is not null,
                "The real nested fixture publishes an offscreen source input with scrollable ancestors, " + mode);
            foreach (var placement in Enum.GetValues<UiAccessibilityTextScrollPlacement>())
            {
                node = Reset();
                var content = node.Node.TextInfo!.Content;
                var task = Begin(node, new(content, content.GetUtf16Offset(target), content.GetUtf16Offset(target + 1), placement));
                Frame();
                check(!task.IsCompleted && outer.Offset == default && inner.Offset == default,
                    "A source-range request waits for its planned ancestor movement to be painted, " + mode + "/" + placement);
                var result = Finish(task);
                node = Frame();
                var cell = node.Node.TextLayout!.GetCharacterExtents(target);
                check(result == UiAccessibilityResult.Success && !node.Node.Offscreen
                    && cell.X >= 10 - .05f && cell.Y >= 10 - .05f && cell.X + cell.Width <= 250.05f && cell.Y + cell.Height <= 170.05f
                    && node.Node.TextLayout.GetOffsetAtPoint(cell.X + cell.Width / 2, cell.Y + cell.Height / 2) == target,
                    "Published nested source glyphs are actually visible and hit-test to their original scalar, " + mode + "/" + placement);
                check(probe.Value == original && node.Node.TextInfo!.CaretUtf16 == caret && node.Node.TextInfo.AnchorUtf16 == anchor && !node.Node.Focused,
                    "Nested source reveal preserves exact text, caret, anchor and keyboard focus, " + mode + "/" + placement);
            }
            node = Reset();
            var current = node.Node.TextInfo!.Content;
            var point = new UiVector2(80, 70);
            var pointTask = Begin(node, new(current, current.GetUtf16Offset(target), current.GetUtf16Offset(target + 1), point));
            check(Finish(pointTask) == UiAccessibilityResult.Success, "Nested source-to-point completes after ancestor publication, " + mode);
            node = Frame();
            var aligned = node.Node.TextLayout!.GetCharacterExtents(target);
            check(MathF.Abs(aligned.X - point.X) < .05f && MathF.Abs(aligned.Y - point.Y) < .05f,
                $"Nested point alignment reaches its client position, {mode}; actual={aligned}; target={point}; inner={inner.Offset}; outer={outer.Offset}; clip={node.Node.TextLayout.ClipBounds}");
            node = Reset();
            current = node.Node.TextInfo!.Content;
            var invalidPoint = Begin(node, new(current, current.GetUtf16Offset(target), current.GetUtf16Offset(target + 1), new UiVector2(300, 250)));
            check(Finish(invalidPoint) == UiAccessibilityResult.InvalidArgument && outer.Offset == default && inner.Offset == default,
                "A nested target outside the outer visible viewport fails before ancestor movement, " + mode);
            node = Reset();
            current = node.Node.TextInfo!.Content;
            var stale = Begin(node, new(current, current.GetUtf16Offset(target), current.GetUtf16Offset(target + 1)));
            Frame();
            probe.Value = "다른 " + original;
            check(Finish(stale) == UiAccessibilityResult.InvalidArgument && probe.Value == "다른 " + original,
                "A changed source between local planning and ancestor publication is refused without rewriting the source, " + mode);
            node = Reset();
            current = node.Node.TextInfo!.Content;
            var endCaret = node.Node.TextInfo.CaretUtf16;
            var endTask = Begin(node, new(current, current.Text.Length, current.Text.Length));
            check(Finish(endTask) == UiAccessibilityResult.Success,
                "An empty source range at end of document reveals its insertion geometry through both ancestors, " + mode);
            node = Frame();
            var insertion = node.Node.TextLayout!.GetRangeExtents(current.CharacterCount, current.CharacterCount);
            var endClip = node.Node.TextLayout.ClipBounds;
            check(insertion.Width == 0 && insertion.X >= endClip.X - .05f && insertion.X <= endClip.X + endClip.Width + .05f
                && insertion.Y >= endClip.Y - .05f && insertion.Y + insertion.Height <= endClip.Y + endClip.Height + .05f
                && node.Node.TextInfo!.CaretUtf16 == endCaret,
                "The published end-insertion rectangle is visibly bounded without moving the real caret, " + mode);
            if (mode == "single")
            {
                node = Reset();
                current = node.Node.TextInfo!.Content;
                var canceled = Begin(node, new(current, current.GetUtf16Offset(target), current.GetUtf16Offset(target + 1)));
                Frame();
                check(canceled.Wait(2600) && canceled.Result == UiAccessibilityResult.Timeout,
                    "A staged ancestor request uses the real two-second production timeout when no following frame is rendered");
                Frame();
                check(outer.Offset == default && inner.Offset == default,
                    "A canceled ancestor plan cannot apply its delayed viewport offsets on a later frame");
                node = Reset();
                current = node.Node.TextInfo!.Content;
                var removed = Begin(node, new(current, current.GetUtf16Offset(target), current.GetUtf16Offset(target + 1)));
                Frame();
                context.NewFrame(new(.1f, new(500, 400), new(1, 1)), idle, new(0, 0, 500, 400), UiTextSettings.Default);
                context.Render(new Screen(_ => { }));
                context.GetDrawData().ReleasePooled();
                check(removed.Wait(1000) && removed.Result == UiAccessibilityResult.Unavailable,
                    "Removing a target after local planning completes the waiting request as unavailable rather than succeeding or hanging");
            }
        }
    }

    internal sealed class Probe : UiControlWidget
    {
        private readonly string _mode;
        private readonly UiMarkdownEditor _markdown;
        internal string Value;
        internal Probe(string mode, bool markdownWrap = true)
        {
            _mode = mode;
            Value = mode == "single" ? "정😀" + new string('W', 80)
                : string.Join('\n', Enumerable.Repeat("정😀" + new string('W', 60), 15));
            _markdown = new("Nested text", new(() => Value, value => Value = value), new(Width: 220, Height: 210, MaxLength: 4096, Wrap: markdownWrap));
        }
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(new(220, _mode == "single" ? 24 : 210));
        protected override void RenderControl(UiImmediateContext ui)
        {
            if (_mode == "single")
                ui.InputText<UiStandardTextInputBehavior, UiFluentInputPresentation<UiFluentSmallInputSize, UiFluentOutlineInput>>("Nested text", ref Value, 4096, 220);
            else if (_mode == "multiline")
                ui.InputTextMultiline<UiStandardMultilineInputBehavior, UiFluentTextareaPresentation<UiFluentSmallTextareaSize, UiFluentOutlineInput>>(
                    "Nested text", ref Value, 4096, new() { Wrap = false }, 220, 210);
            else
                _markdown.Render(ui);
        }
        protected override void OnDispose() => _markdown.Dispose();
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
