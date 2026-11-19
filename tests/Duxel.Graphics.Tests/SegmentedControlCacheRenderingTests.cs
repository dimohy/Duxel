using Duxel.Core;

internal static class SegmentedControlCacheRenderingTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(500, 200));
        foreach (var samples in new[] { 1, 4 })
        {
            fixture.Renderer.SetMsaaSamples(samples);
            var state = new UiState<string?>(null);
            using var widget = new UiSegmentedControlWidget("Cached segments", state.Binding,
                Enumerable.Range(0, 8).Select(i => new UiSegmentOption(i.ToString(), "Segment " + i, Unread : i == 7)).ToArray(),
                new(Width : 220, ReducedMotion : true, IsFixedWidth : false));
            var rear = new UiColor(10, 100, 200);
            var disabled = false;
            fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
            void Draw(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                new UiControlContext(ui).Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), rear, default, 0, 0);
                ui.BeginDisabled(disabled);
                try { widget.Render(ui); }
                finally { ui.EndDisabled(); }
            }
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            UiInputState Wheel(float amount) => idle with { MousePosition = new(100, 15), MouseWheelHorizontal = amount };
            var first = fixture.Capture(Draw);
            var captured = widget.PaintCaptureCount;
            fixture.Capture(Draw);
            check(captured == 1 && widget.PaintCaptureCount == captured && fixture.Renderer.LastFrameMetrics.PaintGroupPasses == 0,
                "msaa" + samples + " idle scrolling segments reuse the owned GPU capture");
            var middle = fixture.Capture(Draw, Wheel(2));
            check(widget.ScrollOffset == 96 && widget.PaintCaptureCount == captured && fixture.Renderer.LastFrameMetrics.PaintGroupPasses == 0,
                "msaa" + samples + " UV scrolling and edge-mask changes do not recapture content");
            int At(byte[] pixels, int x, int y, int c) => pixels[(y * fixture.LastCaptureSize.Width + x) * 4 + c];
            var correct = true;
            foreach (var x in new[] { 8, 100, 218 })
            {
                var opacity = MathF.Min(1, MathF.Min((x + .5f) / 55, (220 - x - .5f) / 55));
                for (var c = 0; c < 3; c++)
                {
                    var backdrop = c == 0 ? 10 : c == 1 ? 100 : 200;
                    var expected = 61 * opacity + backdrop * (1 - opacity);
                    correct &= Math.Abs(At(middle, x, 2, c) - expected) <= 2;
                }
            }
            check(correct, "msaa" + samples + " both physical edges match independent opacity-over-background arithmetic");
            rear = new(220, 80, 30); var changedRear = fixture.Capture(Draw);
            check(widget.PaintCaptureCount == captured && fixture.Renderer.LastFrameMetrics.PaintGroupPasses == 0
                && !middle.AsSpan().SequenceEqual(changedRear),
                "msaa" + samples + " changing the real background recomposites without recapturing segment content");
            rear = new(10, 100, 200); var restored = fixture.Capture(Draw, Wheel(-2));
            check(first.AsSpan().SequenceEqual(restored) && widget.PaintCaptureCount == captured,
                "msaa" + samples + " UV round trip restores exact initial pixels from the same capture");
            state.Value = "7"; fixture.Capture(Draw);
            check(widget.PaintCaptureCount == ++captured && widget.ScrollOffset == widget.MaximumScrollOffset && !widget.Items[7].Unread,
                "msaa" + samples + " accepted selection and unread change replace the capture once");
            widget.Options = widget.Options with { Style = UiSegmentedControlStyle.BrandOverCanvasPill }; fixture.Capture(Draw);
            check(widget.PaintCaptureCount == ++captured, "msaa" + samples + " palette changes invalidate captured pixels");
            disabled = true; fixture.Capture(Draw);
            check(widget.PaintCaptureCount == ++captured, "msaa" + samples + " inherited disabled state invalidates captured ink");
            disabled = false; widget.Options = widget.Options with { RightToLeft = true }; var rtl = fixture.Capture(Draw);
            check(widget.PaintCaptureCount == ++captured && widget.ScrollOffset == 0,
                "msaa" + samples + " RTL rebuilds mirrored source geometry and reveals the selected logical end");
            var design = UiCompiledDesign.Windows11Dark;
            var system = new UiForcedColorPalette(new(8, 8, 8), new(250, 250, 250), new(180, 180, 180),
                new(230, 240, 10), new(0, 0, 0), new(0, 220, 250), new(8, 8, 8), new(250, 250, 250));
            fixture.Context.SetDesign(design with { Theme = system.Apply(design.Theme) });
            state.Value = null;
            var contrast = fixture.Capture(Draw, Wheel(2));
            check(At(contrast, 8, 2, 0) == 8 && At(contrast, 8, 2, 1) == 8 && At(contrast, 8, 2, 2) == 8,
                "msaa" + samples + " forced colors disable edge fading and preserve authoritative system contrast");
            fixture.Context.SetDesign(design);
            ulong iconVersion = 1;
            var iconColor = new UiColor(220, 20, 80);
            UiCompiledIconPainter painter = (paint, rect, ink, filled, interaction) => paint.Frame(rect, iconColor, default, 0, 0);
            var icon = new UiMenuIcon(painter, () => iconVersion);
            widget.SetItems(new UiSegmentOption[] { new("icon", "Versioned icon", Icon : icon), new("text", "A long text segment") });
            widget.Options = widget.Options with { RightToLeft = false, Width = 100 };
            var iconInitial = fixture.Capture(Draw); captured = widget.PaintCaptureCount;
            fixture.Capture(Draw);
            check(widget.PaintCaptureCount == captured, "msaa" + samples + " explicitly versioned custom icons retain cached pixels");
            iconVersion++; iconColor = new(20, 220, 80);
            var iconChanged = fixture.Capture(Draw);
            check(widget.PaintCaptureCount == ++captured && !iconInitial.AsSpan().SequenceEqual(iconChanged),
                "msaa" + samples + " custom icon version changes invalidate the captured content");
            widget.SetItems(new UiSegmentOption[] { new("icon", "Unversioned icon", Icon : new UiMenuIcon(painter)), new("text", "A long text segment") });
            fixture.Capture(Draw); captured = widget.PaintCaptureCount;
            iconColor = new(20, 80, 220); fixture.Capture(Draw);
            check(widget.PaintCaptureCount == captured + 1, "msaa" + samples + " unversioned custom icons never promise a stale cached image");
            widget.SetItems(new UiSegmentOption[] { new("0", "First"), new("1", "Second") });
            widget.Options = widget.Options with { Width = 220, ReducedMotion = true };
            state.Value = "0"; fixture.Capture(Draw);
            widget.Options = widget.Options with { ReducedMotion = false }; fixture.Capture(Draw);
            state.Value = "1"; var start = fixture.Capture(Draw);
            fixture.Context.State.AdvanceTime(.08f); var moving = fixture.Capture(Draw);
            fixture.Context.State.AdvanceTime(.3f); var settled = fixture.Capture(Draw);
            check(!start.AsSpan().SequenceEqual(moving) && !moving.AsSpan().SequenceEqual(settled),
                "msaa" + samples + " cached selection follows the finite animated thumb timeline");
            captured = widget.PaintCaptureCount; fixture.Capture(Draw);
            check(widget.PaintCaptureCount == captured && !fixture.Context.State.HasActiveAnimations,
                "msaa" + samples + " settled selection reuses captured pixels and returns to event wait");
            fixture.WriteCapturePng(Path.Combine(output, "msaa" + samples + "-both-edges.png"), middle);
            fixture.WriteCapturePng(Path.Combine(output, "msaa" + samples + "-rtl.png"), rtl);
        }
    }
}
