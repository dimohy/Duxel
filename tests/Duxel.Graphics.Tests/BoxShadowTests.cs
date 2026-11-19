using System.Numerics;
using Duxel.Core;

internal static class BoxShadowTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
            foreach (var elevation in Enum.GetValues<UiShadowElevation>())
            {
                var style = UiFluentShadow.Resolve(scheme, elevation);
                var blur = new[] { 2, 4, 8, 16, 28, 64 }[(int)elevation];
                check(style.First.BlurRadius == (blur >= 28 ? 8 : 2) && style.Second.BlurRadius == blur
                    && style.Second.Offset == new UiVector2(0, blur / 2f)
                    && style.First.Opacity == (scheme == UiFluentColorScheme.Dark ? .24f : .12f)
                    && style.Second.Opacity == (scheme == UiFluentColorScheme.Dark ? .28f : .14f),
                    "shadow tokens match pinned CSS geometry and theme opacity : " + scheme + " " + elevation);
            }
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var shadow = new UiBoxShadow(new(new(new(0, 2), 4, new(0, 0, 0), .7f)));
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        var bounds = new UiRect(32, 32, 16, 16);
        UiRasterTextureStatistics statistics = default;
        UiDrawData Frame(UiContext? target = null, UiRect? rectangle = null, float radius = 8)
        {
            var owner = target ?? context;
            owner.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 320, 240), UiTextSettings.Default);
            owner.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                new UiControlContext(ui).BoxShadow(shadow, rectangle ?? bounds, radius);
                statistics = ui.ShadowTextureStatistics;
            }));
            return owner.GetDrawData();
        }
        using var first = Frame();
        var upload = first.TextureUpdates.Single(update => update.IsColorImage);
        var saved = upload.RgbaPixels.ToArray();
        var paint = shadow.Options.PaintBounds(bounds);
        check(upload.Width == 34 && upload.Height == 34 && statistics.Uploads == 1,
            "shadow first paint prepares one cropped Gaussian raster within its explicit support");
        byte Alpha(int x, int y) => saved[(y * upload.Width + x) * 4 + 3];
        check(Alpha(17, 15) == 0 && Alpha(0, 0) == 0 && saved.Where((_, index) => index % 4 == 3).Any(a => a > 0),
            "outer shadow preserves transparent body and distant background while producing real soft pixels");
        foreach (var (x, y) in new[] { (5, 15), (7, 11), (9, 7), (17, 25), (27, 15), (17, 29) })
        {
            var localX = paint.X - bounds.X + x + .5;
            var localY = paint.Y - bounds.Y + y + .5;
            var expected = CircleReference(localX, localY - 2, 16, 2) * .7 * 255;
            check(Math.Abs(Alpha(x, y) - expected) <= 1,
                "analytic shadow convolution agrees within one alpha byte with independent dense circle integration : " + x + "," + y);
        }
        var stableRequests = requests;
        using (var repeat = Frame())
            check(repeat.TextureUpdates.Count == 0 && statistics.Uploads == 1,
            "unchanged shadow reuses its texture without raster generation or another upload");
        check(requests == stableRequests && context.State.NextScheduledFrameTimeSeconds is null,
            "shadow paint never requests periodic frames or animation");
        using (var translated = Frame(rectangle: bounds with
        {
            X = 90,
            Y = 70
        }))
            check(translated.TextureUpdates.Count == 0, "shadow translation reuses its position-independent raster");
        shadow.Options = UiFluentShadow.Resolve(UiFluentColorScheme.Dark, UiShadowElevation.Shadow8);
        using (var changed = Frame())
            check(statistics.Uploads == 2 && changed.TextureUpdates.Count == 1,
            "shadow color and blur mutation prepares a new immutable texture");
        check(upload.RgbaPixels.Span.SequenceEqual(saved), "later shadow mutations preserve queued source pixels");
        var colored = new UiShadowLayer(new(0, 2), 4, new(255, 0, 0), .5f);
        shadow.Options = new(colored, colored with
        {
            Color = new(0, 0, 255)
        });
        using (var colors = Frame())
        {
            var raster = colors.TextureUpdates.Single().RgbaPixels.Span;
            var at = (15 * 34 + 5) * 4;
            var coverage = CircleReference(-3.5, 6.5, 16, 2) * .5;
            var total = coverage + coverage * (1 - coverage);
            check(Math.Abs(raster[at] - Math.Round(255 * coverage / total)) <= 1
                && raster[at + 1] == 0
                && Math.Abs(raster[at + 2] - Math.Round(255 * coverage * (1 - coverage) / total)) <= 1,
                "colored shadows composite in front-to-back order and upload straight-alpha RGB exactly once");
        }
        shadow.Options = UiFluentShadow.Resolve(UiFluentColorScheme.Dark, UiShadowElevation.Shadow8);
        context.SetContentScale(2);
        using (var dpi = Frame())
            check(dpi.TextureUpdates.Single().Width == 100 && statistics.Uploads == 4,
            "shadow DPI mutation prepares physical pixel coverage rather than reusing a blurry lower-resolution image");
        using (var other = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2)))
        using (var frame = Frame(other))
            check(statistics.Uploads == 1,
            "shadow leases use independent per-context GPU texture identities");
        using (var returned = Frame())
            check(returned.TextureUpdates.Count == 0,
            "shadow returning from a different context resolution reuses its existing context-specific geometry");
        UiDrawData Ordered(Action<UiImmediateContext> draw)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(draw));
            return context.GetDrawData();
        }
        void Layer(UiImmediateContext ui, string id, bool write, int scope = 0, bool callback = false)
        {
            if (ui.BeginLayer(id, new(false, 1, default)
            {
                Scene3D = new(Matrix4x4.Identity, write)
                {
                    DepthScopeId = scope
                }
            }))
            {
                new UiControlContext(ui).Frame(new(20, 20, 30, 30), new(255, 0, 0), default, 0, 0);
                if (callback)
                    ui.GetWindowDrawList().AddCallback(static (_, _) => { });
            }
            ui.EndLayer();
        }
        using (var ordered = Ordered(ui => { Layer(ui, "read", false); Layer(ui, "write", true); }))
            check(ordered.DrawLists.Select(list => list.Scene3D!.Value.WriteDepth).SequenceEqual(new[] { true, false }),
                "common depth scope stably places opaque writes before translucent reads");
        using (var barriers = Ordered(ui =>
        {
            Layer(ui, "read-before-2d", false);
            new UiControlContext(ui).Frame(new(0, 0, 10, 10), new(0, 255, 0), default, 0, 0);
            Layer(ui, "write-after-2d", true);
        }))
            check(barriers.DrawLists[0].Scene3D is { WriteDepth: false }
            && barriers.DrawLists[1].Scene3D is null && barriers.DrawLists[2].Scene3D is { WriteDepth: true },
            "common material ordering never crosses a 2D painter surface");
        using (var scopes = Ordered(ui =>
        {
            Layer(ui, "read-scope0", false);
            Layer(ui, "write-scope0", true);
            Layer(ui, "read-scope1", false, 1);
            Layer(ui, "write-scope1", true, 1);
        }))
            check(scopes.DrawLists.Select(list => (list.Scene3D!.Value.DepthScopeId, list.Scene3D.Value.WriteDepth))
            .SequenceEqual(new[] { (0, true), (0, false), (1, true), (1, false) }),
            "common depth material ordering preserves independent scope boundaries");
        using (var callbacks = Ordered(ui =>
        {
            Layer(ui, "read-before-callback", false);
            Layer(ui, "callback-barrier", false, callback: true);
            Layer(ui, "write-after-callback", true);
        }))
            check(callbacks.DrawLists[0].Scene3D is { WriteDepth: false }
            && callbacks.DrawLists[1].Commands.Any(command => command.Callback is not null)
            && callbacks.DrawLists[2].Scene3D is { WriteDepth: true },
            "common material ordering retains external callback barriers and submission semantics");
        static bool Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (T) { return true; }
        }
        check(Throws<ArgumentOutOfRangeException>(() => new UiBoxShadow(new(new(default, -1, new(0, 0, 0))))),
            "negative shadow blur fails explicitly");
        check(Throws<ArgumentOutOfRangeException>(() => new UiBoxShadow(new(new(default, 2, new(0, 0, 0), float.NaN)))),
            "nonfinite shadow opacity fails before raster work");
        check(Throws<InvalidOperationException>(() => { using var oversized = Frame(rectangle: new(0, 0, 3000, 3000)); }),
            "oversized shadow fails its pixel budget before generation or upload");
        shadow.Dispose();
        check(Throws<ObjectDisposedException>(() => { using var disposed = Frame(); }), "disposed shadow owner rejects painting");
    }

    private static double CircleReference(double x, double y, double edge, double sigma)
    {
        const double step = .125;
        var sum = 0d;
        for (var row = step * .5; row < edge; row += step)
            for (var column = step * .5; column < edge; column += step)
            {
                var dx = column - edge * .5;
                var dy = row - edge * .5;
                if (dx * dx + dy * dy > edge * edge * .25)
                    continue;
                var xx = (column - x) / sigma;
                var yy = (row - y) / sigma;
                sum += Math.Exp(-.5 * (xx * xx + yy * yy));
            }
        return sum * step * step / (2 * Math.PI * sigma * sigma);
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
