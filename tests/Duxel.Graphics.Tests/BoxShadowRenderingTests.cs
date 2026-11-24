using System.Numerics;
using Duxel.Core;

internal static class BoxShadowRenderingTests
{
    private static long _nextRun;
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int stride, Func<UiRendererFrameMetrics> metrics, int imageCount, UiFluentColorScheme scheme, string expectedFamily)
    {
        var prefix = "shadow-qualification-" + Interlocked.Increment(ref _nextRun) + "-";
        using var shadow = new UiBoxShadow(default);
        var bounds = new UiRect(96.25f, 64.25f, 48, 32);
        var show = false;
        var radius = 0f;
        void Pattern(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            for (var y = 0; y < 240; y += 8)
                for (var x = 0; x < 320; x += 8)
                    paint.Frame(new(x, y, 8, 8), ((x + y) / 8 & 1) == 0 ? new(220, 180, 140) : new(140, 190, 220), default, 0, 0);
            if (show)
                paint.BoxShadow(shadow, bounds, radius);
        }
        int At(int x, int y) => (y * stride + x) * 4;
        var background = capture("pattern-reference", Pattern);
        show = true;
        foreach (var elevation in Enum.GetValues<UiShadowElevation>())
            foreach (var corner in new[] { 0f, 4f, 8f, 16f })
            {
                radius = corner;
                shadow.Options = UiFluentShadow.Resolve(scheme, elevation);
                var pixels = capture(elevation + "-radius-" + corner, Pattern);
                check(pixels.AsSpan(At(120, 80), 4).SequenceEqual(background.AsSpan(At(120, 80), 4))
                    && pixels.AsSpan(At(300, 220), 4).SequenceEqual(background.AsSpan(At(300, 220), 4)),
                    "GPU shadow preserves transparent body and distant patterned background : " + elevation + " " + corner);
                check(pixels[At(120, 99)] < background[At(120, 99)] && pixels[At(120, 99) + 3] == 255,
                    "GPU soft outer shadow darkens rather than replaces the patterned surface : " + elevation + " " + corner);
                check(pixels.AsSpan().SequenceEqual(capture(elevation + "-radius-" + corner + "-replay", Pattern)),
                    "GPU shadow replay retains exact pixels : " + elevation + " " + corner);
            }
        shadow.Options = UiFluentShadow.Resolve(scheme, UiShadowElevation.Shadow16);
        radius = 8;
        var uncut = capture("before-cutout", Pattern);
        var cutout = capture("circular-cutout", ui =>
        {
            show = false;
            Pattern(ui);
            show = true;
            var paint = new UiControlContext(ui);
            using var hole = paint.CircleCutout(new(120, 102), 5);
            paint.BoxShadow(shadow, bounds, radius);
        });
        check(uncut[At(120, 102)] < background[At(120, 102)]
            && cutout.AsSpan(At(120, 102), 4).SequenceEqual(background.AsSpan(At(120, 102), 4)),
            "GPU common circular subtraction applies to the soft shadow texture without a background patch");
        var builds = 0;
        void Depth(UiImmediateContext ui, bool reverse, float cameraX = 0)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            void Back()
            {
                if (ui.BeginLayer(prefix + "shadow-depth-back", new(false, 1, default)
                {
                    Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .5f))
                }))
                    new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(0, 0, 255), default, 0, 0);
                ui.EndLayer();
            }
            void Front()
            {
                if (ui.BeginLayer(prefix + "shadow-depth-front", new(true, 1, default)
                {
                    ContentVersion = 1,
                    Scene3D = new(Matrix4x4.CreateTranslation(cameraX, 0, 0), true)
                }))
                {
                    builds++;
                    var paint = new UiControlContext(ui);
                    paint.BoxShadow(shadow, bounds, radius);
                    paint.Frame(bounds, new(255, 0, 0), default, radius, 0);
                }
                ui.EndLayer();
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
        check(depth[At(120, 100) + 2] is > 0 and < 255 && depth[At(120, 100)] == 0
            && depth[At(120, 80)] == 255,
            "GPU shadow reads rear depth and blends soft coverage without becoming opaque depth-writing material");
        check(depth.AsSpan().SequenceEqual(capture("depth-reversed", ui => Depth(ui, true))),
            "GPU opaque body and translucent shadow preserve exact pixels under depth submission reversal");
        var moved = capture("depth-camera-moved", ui => Depth(ui, false, .125f));
        var followsCamera = true;
        for (var y = 0; y < 240; y++)
            for (var x = 20; x < 300; x++)
                followsCamera &= moved.AsSpan(At(x, y), 4).SequenceEqual(depth.AsSpan(At(x - 20, y), 4));
        check(builds == 1 && followsCamera,
            "GPU retained shadow material inherits changed parent camera while keeping read-only depth and cached geometry");
        var compiledBuilds = 0;
        void Compiled(UiImmediateContext ui, float cameraX = 0)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            if (ui.BeginLayer(prefix + "shadow-compiled-back", new(false, 1, default)
            {
                Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .5f))
            }))
                new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(0, 0, 255), default, 0, 0);
            ui.EndLayer();
            for (var i = 0; i < 3; i++)
            {
                if (ui.BeginLayer(prefix + "shadow-compiled-front-" + i, new(true, 1, default)
                {
                    ContentVersion = 1,
                    Scene3D = new(Matrix4x4.CreateTranslation(cameraX, 0, 0))
                    {
                        PlaneModel = new(default, i * .05f)
                    }
                }))
                {
                    compiledBuilds++;
                    var paint = new UiControlContext(ui);
                    var body = new UiRect(32.25f + i * 88, 64.25f, 48, 32);
                    paint.BoxShadow(shadow, body, radius);
                    paint.Frame(body, new(255, 0, 0), default, radius, 0);
                }
                ui.EndLayer();
            }
        }
        for (var i = 0; i < 3; i++)
            capture("compiled-warm-" + i, ui => Compiled(ui));
        var compiled = capture("compiled-reference", ui => Compiled(ui));
        check(metrics().CompiledLayerDraws > 0 && metrics().CompiledLayerCommands >= 6,
            "GPU three retained shadow casters actually use compiled opaque and translucent descriptors");
        var familyMetrics = metrics();
        check(expectedFamily switch
        {
            "expect-shared-camera" => familyMetrics.SharedCameraLayerDraws > 0,
            "expect-shared-basis" => familyMetrics.SharedCameraLayerDraws == 0 && familyMetrics.SharedBasisLayerDraws > 0,
            "expect-generic" => familyMetrics.SharedCameraLayerDraws == 0 && familyMetrics.SharedBasisLayerDraws == 0,
            _ => throw new ArgumentOutOfRangeException(nameof(expectedFamily))
        }, "GPU shadow compiled pipeline family matches the explicit test expectation : " + expectedFamily);
        check(compiled[At(56, 80)] == 255 && compiled[At(56, 100) + 2] is > 0 and < 255,
            "GPU compiled shadow material preserves both opaque body and soft rear-plane coverage");
        check(compiled.AsSpan().SequenceEqual(capture("compiled-replay", ui => Compiled(ui))),
            "GPU compiled shadow casters replay exact retained pixels");
        var compiledMoved = capture("compiled-camera-moved", ui => Compiled(ui, .125f));
        var compiledFollows = true;
        for (var y = 0; y < 240; y++)
            for (var x = 20; x < 300; x++)
                compiledFollows &= compiledMoved.AsSpan(At(x, y), 4).SequenceEqual(compiled.AsSpan(At(x - 20, y), 4));
        check(compiledBuilds == 3 && compiledFollows && metrics().ResidentUpdates == 0 && metrics().ResidentCreates == 0,
            "GPU compiled shadows inherit parent camera changes without rebuilding or reuploading resident geometry");
        void Cached2D(UiImmediateContext ui)
        {
            show = false;
            Pattern(ui);
            show = true;
            if (ui.BeginLayer(prefix + "shadow-cached-2d", new(true, 1, default)
            {
                ContentVersion = 1
            }))
                new UiControlContext(ui).BoxShadow(shadow, bounds, radius);
            ui.EndLayer();
        }
        byte[]? expected = null;
        for (var i = 0; i <= imageCount; i++)
            expected = capture("cached-warm-" + i, Cached2D);
        var replay = capture("cached-idle", Cached2D);
        check(expected!.AsSpan().SequenceEqual(replay) && metrics().DrawCalls == 0 && metrics().DynamicUploadBytes == 0,
            "GPU warmed shadow replay performs no draw or streamed geometry upload on unchanged acquired images");
    }
}
