using Duxel.Core;

internal static class DrawerRenderingTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        Directory.CreateDirectory(output);
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(640, 640));
        fixture.Context.SetTheme(UiCompiledDesign.Windows11Dark.Theme);
        using var tree = new UiAccessibilityTree();
        fixture.Context.SetAccessibilityTree(tree);
        using var drawer = Fluent.Drawer("Animated details", Fluent.Body1("Retained drawer content"), options : new(Extent : 320));
        var requests = 0;
        fixture.Context.SetRequestFrameCallback(() => requests++);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), new(15, 17, 19), ui.WhiteTextureId);
            drawer.Render(ui);
        }
        byte[] Capture(string name)
        {
            var pixels = fixture.Capture(Draw);
            BrowserPng.Write(Path.Combine(output, name + ".png"), (int)fixture.FrameInfo.DisplaySize.X, (int)fixture.FrameInfo.DisplaySize.Y, pixels);
            return pixels;
        }
        int LastSurfacePixel(byte[] pixels)
        {
            var width = (int)fixture.FrameInfo.DisplaySize.X;
            var last = -1;
            for (var x = 0; x < width; x++)
                if (pixels[(200 * width + x) * 4] > 20) last = x;
            return last;
        }
        Capture("closed");
        drawer.Show();
        Capture("entry-start");
        check(drawer.IsOpen && drawer.Input.LastMotion.Translation.X == -320,
            "Drawer entry starts outside its requested physical edge without scaling content");
        fixture.Context.State.AdvanceTime(.12f);
        var middle = Capture("entry-middle");
        check(drawer.Input.LastMotion.Scale == 1 && drawer.Input.LastMotion.Translation.X is > -320 and < 0
            && LastSurfacePixel(middle) is > 0 and < 319,
            "Drawer GPU entry capture translates an unchanged-size surface into the viewport");
        fixture.Context.State.AdvanceTime(.3f);
        var settled = Capture("entry-settled");
        check(LastSurfacePixel(settled) == 319 && drawer.Input.LastMotion.Translation == default,
            "Drawer GPU settled capture occupies the exact declared edge extent");
        check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "Close" && element.Node.Focused),
            "Drawer entry moves focus to an actual visible control after its initial offscreen pose");
        drawer.Close();
        Capture("exit-start");
        check(!drawer.IsOpen && !tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Dialog),
            "Drawer exit paint retains no interactive or accessibility surface");
        fixture.Context.State.AdvanceTime(.12f);
        var exiting = Capture("exit-middle");
        check(LastSurfacePixel(exiting) is > 0 and < 319,
            "Drawer GPU exit capture replays retained content towards its original edge");
        fixture.Context.State.AdvanceTime(.3f);
        var closed = Capture("exit-complete");
        check(LastSurfacePixel(closed) == -1, "Drawer GPU exit releases its frozen paint at completion");
        Capture("idle-warm");
        requests = 0;
        Capture("idle-settled");
        check(requests == 0, "Drawer idle state requests no continuing animation frames");
        drawer.Options = drawer.Options with { Type = UiDrawerType.Inline, InlineCrossExtent = 400 };
        Capture("inline-closed");
        drawer.Show();
        Capture("inline-entry-start");
        check(drawer.IsOpen && drawer.Input.InlineMotion.Sample(true, true, fixture.Context.State.TimeSeconds) == 0,
            "inline Drawer starts a real allocation transition after its closed state");
        fixture.Context.State.AdvanceTime(.12f);
        var inlineMiddle = Capture("inline-entry-middle");
        var inlineProgress = drawer.Input.InlineMotion.Sample(true, true, fixture.Context.State.TimeSeconds);
        check(inlineProgress is > 0 and < 1 && LastSurfacePixel(inlineMiddle) is > 0 and < 319,
            "inline Drawer GPU capture clips full-size content to its intermediate allocation");
        fixture.Context.State.AdvanceTime(.3f);
        Capture("inline-entry-settled");
        drawer.Close();
        Capture("inline-exit-start");
        check(!tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Group
            && element.Node.Name == "Animated details"), "inline exit paint retires its interactive accessibility surface immediately");
        fixture.Context.State.AdvanceTime(.12f);
        var inlineExit = Capture("inline-exit-middle");
        check(LastSurfacePixel(inlineExit) is > 0 and < 319,
            "inline Drawer GPU exit replays frozen paint while releasing its allocation");
        drawer.Show();
        var closingTimeline = drawer.Input.InlineMotion;
        Capture("inline-reversed");
        check(drawer.Input.InlineMotion.Sample(true, true, fixture.Context.State.TimeSeconds)
            == closingTimeline.Sample(false, true, fixture.Context.State.TimeSeconds)
            && drawer.Input.InlineMotion.Sample(true, true, fixture.Context.State.TimeSeconds) < 1,
            "inline Drawer reverses from its sampled extent without jumping to full size");
        fixture.Context.State.AdvanceTime(.3f);
        Capture("inline-reopened");
        drawer.Close();
        Capture("inline-final-close-start");
        fixture.Context.State.AdvanceTime(.3f);
        var inlineClosed = Capture("inline-final-closed");
        check(LastSurfacePixel(inlineClosed) == -1 && drawer.Input.SurfacePaint is null,
            "inline Drawer completion releases its paint and all visible extent");
        requests = 0;
        Capture("inline-idle");
        check(requests == 0, "inline Drawer settled state requests no perpetual animation frames");
        VerifyScrollSeparators(fixture, tree, output, check);
    }

    private static void VerifyScrollSeparators(PresentationReuseTests.Fixture fixture, UiAccessibilityTree tree,
        string output, Action<bool, string> check)
    {
        using var drawer = Fluent.OverlayDrawer("Scrollable panel",
            Dux.Column(new(MainAxisSize : UiMainAxisSize.Min),
                Enumerable.Range(0, 70).Select(i => Fluent.Body1("Row " + i)).ToArray()),
            [Fluent.Button("Apply")], new(MotionEnabled : false));
        drawer.Show();
        byte[] Capture(string state)
        {
            var pixels = fixture.Capture(ui => drawer.Render(ui));
            BrowserPng.Write(Path.Combine(output, "scroll-separator-" + state + ".png"), 640, 640, pixels);
            return pixels;
        }
        var top = Capture("top");
        var viewport = tree.CaptureSnapshot().Elements.Single(element => element.Node.Scroll is { } scroll
            && scroll.Content.Y > scroll.Viewport.Y).Node.Bounds;
        var x = (int)viewport.X + 2;
        var headerY = (int)viewport.Y - 1;
        var footerY = (int)(viewport.Y + viewport.Height);
        bool Stroke(byte[] pixels, int y)
        {
            var index = (y * 640 + x) * 4;
            return pixels[index] == 102 && pixels[index + 1] == 102 && pixels[index + 2] == 102;
        }
        check(!Stroke(top, headerY) && Stroke(top, footerY), "Drawer draws only the footer separator at the top of the body");
        drawer.Body.Content.Offset = new(0, drawer.Body.Content.Scroll.VerticalScrollRange / 2);
        var middle = Capture("middle");
        check(Stroke(middle, headerY) && Stroke(middle, footerY), "Drawer draws both separators while scrolling the middle of the body");
        drawer.Body.Content.Offset = new(0, drawer.Body.Content.Scroll.VerticalScrollRange);
        var bottom = Capture("bottom");
        check(Stroke(bottom, headerY) && !Stroke(bottom, footerY), "Drawer draws only the header separator at the end of the body");
    }
}
