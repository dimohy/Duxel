using Duxel.Core;

internal static class HorizontalOpacityMaskRenderingTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(240, 160));
        using var lcd = new UiBackdropImage(new(new(1, 1, [128, 0, 0, 0])), new(new(1, 1, [128, 127, 255, 255])));
        var bounds = new UiRect(16, 16, 128, 32);
        var mask = new UiHorizontalOpacityMask(.25f, .25f);
        var opacity = 1f;
        var backdrop = new UiColor(100, 160, 200);
        var cutout = false;
        UiColor Background(int x) => x < 80 ? backdrop : new UiColor(60, 20, 180);
        void Rear(UiControlContext paint)
        {
            paint.Frame(new(0, 0, 80, fixture.FrameInfo.DisplaySize.Y), backdrop, default, 0, 0);
            paint.Frame(new(80, 0, fixture.FrameInfo.DisplaySize.X - 80, fixture.FrameInfo.DisplaySize.Y), new(60, 20, 180), default, 0, 0);
        }
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var paint = new UiControlContext(ui);
            Rear(paint);
            using var hole = cutout ? paint.CircleCutout(new(80, 32), 6) : default;
            paint.BackdropImage(lcd, bounds, mask, opacity);
        }
        int At(byte[] pixels, int x, int y, int channel) => pixels[(y * fixture.LastCaptureSize.Width + x) * 4 + channel];
        foreach (var samples in new[] { 1, 4 })
        {
            fixture.Renderer.SetMsaaSamples(samples);
            foreach (var rear in new[] { new UiColor(100, 160, 200), new(20, 20, 20), new(230, 230, 230) })
                foreach (var edges in new[] { default(UiHorizontalOpacityMask), new(.25f, 0), new(0, .25f), new(.25f, .25f) })
                    foreach (var globalOpacity in new[] { 1f, .6f })
                    {
                        backdrop = rear; mask = edges; opacity = globalOpacity;
                        var pixels = fixture.Capture(Draw);
                        var valid = true;
                        foreach (var localX in new[] { 0, 7, 31, 32, 63, 64, 95, 96, 119, 127 })
                        {
                            var x = 16 + localX;
                            var background = Background(x).Rgba;
                            var factor = edges.Evaluate((localX + .5f) / 128) * globalOpacity;
                            float[] expected = [128 * factor + (byte)background * (1 - factor),
                                (byte)(background >> 8) * (1 - 128 / 255f * factor), (byte)(background >> 16), 255];
                            for (var channel = 0; channel < 4; channel++) valid &= Math.Abs(At(pixels, x, 32, channel) - expected[channel]) <= 2;
                        }
                        check(valid, "msaa" + samples + " affine LCD ramp preserves channel coverage over rear=" + rear.Rgba + " edges=" + edges + " opacity=" + opacity);
                    }
            mask = new(.25f, .25f); opacity = .6f; cutout = true;
            var circular = fixture.Capture(Draw);
            check(At(circular, 80, 32, 0) == 60 && At(circular, 80, 32, 1) == 20 && At(circular, 80, 32, 2) == 180,
                "msaa" + samples + " circle subtraction remains transparent inside masked composition");
            cutout = false;
            fixture.WriteCapturePng(Path.Combine(output, "msaa" + samples + "-lcd.png"), fixture.Capture(Draw));
        }
        using var group = new UiPaintGroup();
        var calls = 0;
        var groupBounds = new UiRect(16, 64, 128, 32);
        var uv = UiImageUvTransform.Identity;
        void Content(UiControlContext paint)
        {
            calls++;
            paint.Frame(groupBounds, new(255, 0, 0), default, 0, 0);
            paint.Frame(groupBounds with { X = 48, Width = 64 }, new(0, 255, 0), default, 0, 0);
        }
        void Group(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var paint = new UiControlContext(ui);
            Rear(paint);
            paint.PaintGroup(group, groupBounds, 1, Content, uv, mask, .6f);
        }
        mask = new(.25f, .25f);
        fixture.Capture(Group);
        var first = fixture.Capture(Group);
        check(calls == 1 && fixture.Renderer.LastFrameMetrics.PaintGroupPasses == 0,
            "warmed masked paint group reuses its captured content without repeating the paint callback");
        mask = new(.5f, 0);
        var changed = fixture.Capture(Group);
        check(calls == 1 && fixture.Renderer.LastFrameMetrics.PaintGroupPasses == 0 && !first.AsSpan().SequenceEqual(changed),
            "mask-only changes update composition while keeping the exact cached capture");
        check(Math.Abs(At(changed, 80, 80, 0) - 24) <= 2 && Math.Abs(At(changed, 80, 80, 1) - 161) <= 2,
            "overlapping group content receives masked opacity once after its internal overlap is resolved");
        fixture.WriteCapturePng(Path.Combine(output, "group.png"), changed);
        uv = new(new(.25f, 0), new(.5f, 0), new(0, 1));
        mask = new(.25f, .25f);
        var camera = fixture.Capture(Group);
        var edgeFactor = (8.5f / 128 / .25f) * .6f;
        check(calls == 1 && fixture.Renderer.LastFrameMetrics.PaintGroupPasses == 0
            && Math.Abs(At(camera, 24, 80, 0) - 230 * (1 - edgeFactor)) <= 2
            && Math.Abs(At(camera, 24, 80, 1) - (255 * edgeFactor + 230 * (1 - edgeFactor))) <= 2,
            "UV-only camera crop reuses cached group pixels while keeping the opacity ramp in viewport coordinates");
        fixture.WriteCapturePng(Path.Combine(output, "group-camera.png"), camera);

        CheckSplitFade(fixture, output, check);
    }

    private static void CheckSplitFade(PresentationReuseTests.Fixture fixture, string output, Action<bool, string> check)
    {
        using var combined = new UiPaintGroup();
        using var surface = new UiPaintGroup();
        using var thumb = new UiPaintGroup();
        var bounds = new UiRect(16, 16, 128, 32);
        var mask = new UiHorizontalOpacityMask(.25f, .25f);
        var uv = UiImageUvTransform.Identity;
        void Surface(UiControlContext paint) => paint.Frame(bounds, new(255, 0, 0), default, 0, 0);
        void Thumb(UiControlContext paint) => paint.Frame(new(16, 16, 24, 32), new(0, 255, 0), default, 0, 0);

        foreach (var samples in new[] { 1, 4 })
        {
            fixture.Renderer.SetMsaaSamples(samples);
            byte[] Capture(bool split) => fixture.Capture(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                var paint = new UiControlContext(ui);
                paint.Frame(bounds, new(0, 0, 255), default, 0, 0);
                if (split)
                {
                    paint.PaintGroup(surface, bounds, 1, Surface, uv, mask);
                    paint.PaintGroup(thumb, bounds, 1, Thumb, uv, mask);
                }
                else
                {
                    paint.PaintGroup(combined, bounds, 1, layer =>
                    {
                        Surface(layer);
                        Thumb(layer);
                    }, uv, mask);
                }
            });

            var once = Capture(false);
            var independently = Capture(true);
            var overlap = (32 * fixture.LastCaptureSize.Width + 24) * 4;
            var surfaceOnly = (32 * fixture.LastCaptureSize.Width + 80) * 4;
            var opacity = mask.Evaluate(8.5f / bounds.Width);
            var leakedRed = 255 * opacity * (1 - opacity);
            check(once[overlap] == 0 && Math.Abs(independently[overlap] - leakedRed) <= 2
                && Math.Abs(once[overlap + 1] - independently[overlap + 1]) <= 2
                && once.AsSpan(surfaceOnly, 4).SequenceEqual(independently.AsSpan(surfaceOnly, 4)),
                "msaa" + samples + " independent surface and thumb fades leak the covered surface only where layers overlap");
            fixture.WriteCapturePng(Path.Combine(output, "msaa" + samples + "-combined-fade.png"), once);
            fixture.WriteCapturePng(Path.Combine(output, "msaa" + samples + "-split-fade.png"), independently);
        }
    }
}
