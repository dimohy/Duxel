using Duxel.Core;
using System.Numerics;

internal static class BackdropCompositeRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int stride, Func<UiRendererFrameMetrics> metrics, int imageCount, string expectedFamily, bool dualSourceBlend)
    {
        using var lcd = new UiBackdropImage(new(new(1, 1, [128, 0, 0, 0])), new(new(1, 1, [128, 127, 255, 255])));
        // This is a prepared affine reference pair, not runtime widget-group capture.
        using var overlap = new UiBackdropImage(new(new(1, 1, [255, 0, 0, 255])), new(new(1, 1, [255, 0, 0, 255])));
        var bounds = new UiRect(24, 24, 96, 96);
        void Draw(UiImmediateContext ui, UiBackdropImage source, float opacity, bool cutout)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(new(0, 0, stride, 240), new(100, 160, 200), default, 0, 0);
            using (var hole = cutout ? paint.CircleCutout(new(72, 72), 12) : default)
                paint.BackdropImage(source, bounds, opacity);
            paint.Frame(new(140, 24, 40, 40), new(0, 255, 0), default, 0, 0);
        }
        int At(int x, int y) => (y * stride + x) * 4;
        float scalarCoverage = (1f + 128f / 255f) / 3f;
        float ExpectedRed(float opacity)
            => 100f + 28f * opacity * (dualSourceBlend ? 1f : scalarCoverage);
        foreach (var opacity in new[] { 0f, .2f, .8f, 1f })
        {
            var pixels = capture("lcd-" + opacity, ui => Draw(ui, lcd, opacity, false));
            var offset = At(48, 48);
            var expected = dualSourceBlend
                ? new[] { 128 * opacity + 100 * (1 - opacity), 160 * (1 - opacity * 128 / 255), 200f, 255f }
                : new[] { ExpectedRed(opacity), 160 * (1 - opacity * scalarCoverage), 200 * (1 - opacity * scalarCoverage), 255f };
            check(Enumerable.Range(0, 4).All(channel => Math.Abs(pixels[offset + channel] - expected[channel]) <= 2),
                (dualSourceBlend ? "GPU affine composite preserves RGB-specific LCD coverage"
                    : "GPU affine composite normalizes LCD coverage for scalar blending") + " : " + opacity);
            check(pixels[At(150, 40) + 1] == 255 && pixels[At(150, 40)] == 0,
                "GPU normal material pipeline is restored after composite : " + opacity);
        }
        var ordinary = capture("overlap", ui => Draw(ui, overlap, .8f, false));
        check(Math.Abs(ordinary[At(48, 48)] - 224) <= 2 && Math.Abs(ordinary[At(48, 48) + 1] - 32) <= 2,
            "GPU prepared opaque overlap result receives opacity once");
        var masked = capture("masked", ui => Draw(ui, lcd, .8f, true));
        check(masked[At(72, 72)] == 100 && masked[At(72, 72) + 1] == 160
            && Math.Abs(masked[At(48, 48)] - ExpectedRed(.8f)) <= 2, "GPU circular subtraction preserves the rear surface inside composite");
        for (var frame = 0; frame <= imageCount; frame++)
            check(masked.AsSpan().SequenceEqual(capture("replay-" + frame, ui => Draw(ui, lcd, .8f, true))),
                "GPU composite replay retains exact pixels across acquired images : " + frame);
        var idle = metrics();
        check(idle.DrawCalls == 0 && idle.DynamicUploadBytes == 0, "GPU warmed composite does not force rendering or upload");
        var prefix = "backdrop-" + Guid.NewGuid().ToString("N");
        var builds = 0;
        void Depth(UiImmediateContext ui, bool reverse, float cameraX = 0, bool compiled = false)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            void Back()
            {
                if (ui.BeginLayer(prefix + "rear", new(false, 1, default)
                {
                    Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .5f))
                }))
                    new UiControlContext(ui).Frame(new(0, 0, stride, 240), new(100, 160, 200), default, 0, 0);
                ui.EndLayer();
            }
            void Front()
            {
                for (var index = 0; index < (compiled ? 3 : 1); index++)
                {
                    var scene = new UiLayer3D(Matrix4x4.CreateTranslation(cameraX, 0, 0), !compiled);
                    if (compiled)
                        scene = scene with
                        {
                            PlaneModel = new(default, index * .05f)
                        };
                    if (ui.BeginLayer(prefix + "front-" + compiled + "-" + index, new(true, 1, default)
                    {
                        ContentVersion = 1,
                        Scene3D = scene
                    }))
                    {
                        builds++;
                        var paint = new UiControlContext(ui);
                        using var hole = paint.CircleCutout(new(48 + index * 88, 88), 6);
                        paint.BackdropImage(lcd, new(24 + index * 88, 64, 48, 48), .8f);
                    }
                    ui.EndLayer();
                }
            }
            if (reverse)
            {
                Front();
                Back();
            }
            else
            {
                Back();
                Front();
            }
        }
        var depth = capture("depth", ui => Depth(ui, false));
        check(Math.Abs(depth[At(36, 76)] - ExpectedRed(.8f)) <= 2 && depth[At(48, 88)] == 100,
            "GPU backdrop helper splits depth-write parents into read-only composite material");
        check(depth.AsSpan().SequenceEqual(capture("depth-reverse", ui => Depth(ui, true))),
            "GPU translucent composite preserves full pixels when opaque rear submission is reversed");
        var moved = capture("depth-moved", ui => Depth(ui, false, .125f));
        check(builds == 1 && Math.Abs(moved[At(56, 76)] - ExpectedRed(.8f)) <= 2 && moved[At(36, 76)] == 100,
            "GPU retained composite inherits changed parent transform without rebuilding its geometry");
        for (var warm = 0; warm < 3; warm++)
            capture("compiled-warm-" + warm, ui => Depth(ui, false, 0, true));
        var compiledPixels = capture("compiled", ui => Depth(ui, false, 0, true));
        var compiledMetrics = metrics();
        check(compiledMetrics.CompiledLayerDraws > 0 && compiledMetrics.CompiledLayerCommands >= 3,
            "GPU prepared reference pair actually enters compiled descriptor submission");
        check(expectedFamily switch
        {
            "expect-shared-camera" => compiledMetrics.SharedCameraLayerDraws > 0,
            "expect-shared-basis" => compiledMetrics.SharedCameraLayerDraws == 0 && compiledMetrics.SharedBasisLayerDraws > 0,
            "expect-generic" => compiledMetrics.SharedCameraLayerDraws == 0 && compiledMetrics.SharedBasisLayerDraws == 0,
            _ => false
        }, "GPU composite uses the configured compiled family : " + expectedFamily);
        check(compiledPixels.AsSpan().SequenceEqual(capture("compiled-reversed", ui => Depth(ui, true, 0, true))),
            "GPU compiled composite remains exact under reversed opaque rear submission");

    }
}
