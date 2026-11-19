using System.Numerics;
using Duxel.Core;

internal static class RoundedClipRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int stride, UiTextureId white, Func<UiRendererFrameMetrics> metrics, int imageCount, string expectedFamily)
    {
        var viewport = new UiRect(0, 0, stride, 240);
        var bounds = new UiRect(24.25f, 24.25f, 96, 96);
        using var image = new UiImage(new(new(1, 1, [255, 64, 32, 255]))) { Size = new(128, 128), CornerRadius = 3 };
        using var text = Fluent.Body1("MMMMMMMMMMMM");
        using var group = new UiPaintGroup();
        int At(int x, int y) => (y * stride + x) * 4;
        void Background(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(viewport, new(0, 32, 180), white);
        }
        var rear = capture("rounded-background", Background);
        void Draw(UiImmediateContext ui, int kind, bool masked)
        {
            Background(ui);
            var list = ui.GetWindowDrawList();
            using var scope = masked ? list.RoundedClip(bounds, 24) : default;
            if (kind == 0) list.AddRectFilled(new(16, 16, 128, 128), new(255, 0, 0), white);
            else if (kind == 1) list.AddImageRounded(white, new(16, 16, 128, 128), UiImageUvTransform.Identity, new(255, 0, 0), 2, viewport);
            else if (kind == 2)
            {
                ui.SetCursorScreenPos(new(16, 16)); image.Render(ui);
            }
            else if (kind == 3)
            {
                ui.SetCursorScreenPos(new(16, 24)); text.Render(ui);
            }
            else if (kind == 4)
                list.AddRectFilledGradient(new(16, 16, 128, 128), new(new(16, 16), new(144, 144),
                    new(255, 0, 0), new(255, 255, 0), new(255, 255, 255)), white, viewport, 2);
            else new UiControlContext(ui).PaintGroup(group, new(16, 16, 128, 128), 1,
                paint => paint.Frame(new(16, 16, 128, 128), new(255, 0, 0), default, 0, 0));
        }
        static float Distance(float x, float y, UiRect rect, float radius)
        {
            var q = Vector2.Abs(new Vector2(x - rect.X - rect.Width / 2, y - rect.Y - rect.Height / 2))
                - new Vector2(rect.Width / 2 - radius, rect.Height / 2 - radius);
            return Vector2.Max(q, Vector2.Zero).Length() + MathF.Min(MathF.Max(q.X, q.Y), 0) - radius;
        }
        for (var kind = 0; kind < 6; kind++)
        {
            var original = capture("rounded-original-" + kind, ui => Draw(ui, kind, false));
            var clipped = capture("rounded-clipped-" + kind, ui => Draw(ui, kind, true));
            var outside = true; var inside = true; var removed = 0; var fractional = 0;
            for (var y = 12; y < 150; y++)
            for (var x = 12; x < 150; x++)
            {
                var d = Distance(x + .5f, y + .5f, bounds, 24);
                var at = At(x, y);
                if (d > 2)
                {
                    outside &= clipped.AsSpan(at, 4).SequenceEqual(rear.AsSpan(at, 4));
                    if (!original.AsSpan(at, 4).SequenceEqual(rear.AsSpan(at, 4))) removed++;
                }
                if (d < -2) inside &= clipped.AsSpan(at, 4).SequenceEqual(original.AsSpan(at, 4));
                if (kind == 0 && MathF.Abs(d) < 1.5f && !clipped.AsSpan(at, 4).SequenceEqual(rear.AsSpan(at, 4))
                    && !clipped.AsSpan(at, 4).SequenceEqual(original.AsSpan(at, 4))) fractional++;
            }
            check(outside && removed > 3, "GPU rounded clip removes corner/outside content over unchanged rear pixels : " + kind);
            check(inside, "GPU rounded clip preserves interior color and text coverage : " + kind);
            if (kind == 0) check(fractional > 0, "GPU rounded clip has analytic fractional edge coverage at one sample");
            check(clipped.AsSpan().SequenceEqual(capture("rounded-replay-" + kind, ui => Draw(ui, kind, true))),
                "GPU rounded clipped content replays exact acquired-image pixels : " + kind);
        }
        void Nested(UiImmediateContext ui, float radius)
        {
            Background(ui);
            var list = ui.GetWindowDrawList();
            using var outer = list.RoundedClip(new(20, 20, 120, 96), radius);
            using var inner = list.RoundedClip(new(16, 16, 144, 128), 8);
            list.AddRectFilled(viewport, new(255, 0, 0), white);
        }
        var rounded = capture("rounded-parent-radius40", ui => Nested(ui, 40));
        for (var frame = 0; frame <= imageCount; frame++) capture("rounded-parent-warm-" + frame, ui => Nested(ui, 40));
        var square = capture("rounded-parent-radius4", ui => Nested(ui, 4));
        check(rounded.AsSpan(At(22, 22), 4).SequenceEqual(rear.AsSpan(At(22, 22), 4)) && square[At(22, 22)] == 255,
            "GPU nested ancestor-radius-only mutation changes pixels while root mask bounds stay unchanged");
        for (var frame = 0; frame <= imageCount; frame++)
            check(square.AsSpan().SequenceEqual(capture("rounded-parent-repair-" + frame, ui => Nested(ui, 4))),
                "GPU every warmed acquired image repairs an ancestor-only clip change : " + frame);
        check(metrics().DrawCalls == 0 && metrics().DynamicUploadBytes == 0,
            "GPU warmed unchanged nested clip avoids drawing and streamed uploads");
        void Circle(UiImmediateContext ui, bool circleFirst)
        {
            Background(ui); var list = ui.GetWindowDrawList();
            if (circleFirst)
            {
                using var cut = list.CircleCutout(new(72, 72), 16);
                using var clip = list.RoundedClip(bounds, 24);
                list.AddRectFilled(viewport, new(255, 0, 0), white);
            }
            else
            {
                using var clip = list.RoundedClip(bounds, 24);
                using var cut = list.CircleCutout(new(72, 72), 16);
                list.AddRectFilled(viewport, new(255, 0, 0), white);
            }
        }
        var circle = capture("rounded-circle-first", ui => Circle(ui, true));
        check(circle.AsSpan().SequenceEqual(capture("rounded-circle-last", ui => Circle(ui, false)))
            && circle.AsSpan(At(72, 72), 4).SequenceEqual(rear.AsSpan(At(72, 72), 4)) && circle[At(48, 72)] == 255,
            "GPU rounded intersection and analytic circular subtraction compose in either scope order");
        void Depth(UiImmediateContext ui, bool reverse, float opacity)
        {
            Background(ui);
            void Back()
            {
                if (ui.BeginLayer("rounded-depth-back", new(false, 1, default)
                    { Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .5f)) }))
                    ui.GetWindowDrawList().AddRectFilled(new(16, 16, 128, 128), new(0, 0, 255), white);
                ui.EndLayer();
            }
            void Front()
            {
                if (ui.BeginLayer("rounded-depth-front", new(false, opacity, default)
                    { Scene3D = new(Matrix4x4.Identity, opacity == 1) }))
                {
                    var list = ui.GetWindowDrawList();
                    using var roundedClip = list.RoundedClip(bounds, 24);
                    list.AddRectFilled(new(16, 16, 128, 128), new(255, 0, 0), white);
                }
                ui.EndLayer();
            }
            if (reverse) { Front(); Back(); } else { Back(); Front(); }
        }
        var depth = capture("rounded-depth", ui => Depth(ui, false, 1));
        check(depth.AsSpan().SequenceEqual(capture("rounded-depth-reversed", ui => Depth(ui, true, 1)))
            && depth[At(26, 26) + 2] == 255 && depth[At(72, 72)] == 255,
            "GPU rounded clip preserves rear samples and opaque depth under submission reversal");
        var alpha = capture("rounded-depth-alpha", ui => Depth(ui, false, .5f));
        check(alpha[At(26, 26) + 2] == 255 && alpha[At(72, 72)] is >= 126 and <= 129
            && alpha[At(72, 72) + 2] is >= 126 and <= 129,
            "GPU rounded clip keeps material opacity separate from boundary sample coverage");
        void Cached(UiImmediateContext ui, bool parent, float radius, bool compiled)
        {
            Background(ui);
            using var scope = parent ? ui.GetWindowDrawList().RoundedClip(new(bounds.X + 8, bounds.Y + 9, 96, 96), radius) : default;
            for (var index = 0; index < 3; index++)
            {
                if (ui.BeginLayer("rounded-cached-" + parent + "-" + compiled + "-" + index, new(true, 1, new(8, 9))
                    { ContentVersion = 1, Scene3D = compiled ? new(Matrix4x4.Identity) { PlaneModel = new(default, index * .05f) } : null }))
                {
                    var list = ui.GetWindowDrawList();
                    using var local = parent ? default : list.RoundedClip(bounds, 24);
                    list.AddImageRounded(white, new(16 + index * 32, 16, 32, 128), UiImageUvTransform.Identity, new(255, 0, 0), 1, viewport);
                }
                ui.EndLayer();
            }
        }
        foreach (var compiled in new[] { false, true })
        {
            var child = capture("rounded-child-" + compiled, ui => Cached(ui, false, 24, compiled));
            var parent = capture("rounded-parent-" + compiled, ui => Cached(ui, true, 24, compiled));
            if (!child.AsSpan().SequenceEqual(parent))
            {
                var differing = Enumerable.Range(0, child.Length).Where(i => child[i] != parent[i]).Take(8);
                Console.WriteLine("ROUNDED_PARENT_DIFF " + string.Join(", ", differing.Select(i => $"({i / 4 % stride},{i / 4 / stride},c{i % 4})={child[i]}/{parent[i]}")));
            }
            check(child.AsSpan().SequenceEqual(parent), "GPU translated retained outer rounded clip matches local capture : " + compiled);
            if (compiled) check(metrics().CompiledLayerDraws > 0 && metrics().CompiledLayerCommands >= 3,
                "GPU rounded clip actually uses compiled descriptors and rebased mask addresses");
            if (compiled)
            {
                var family = metrics();
                check(expectedFamily switch
                {
                    "expect-shared-camera" => family.SharedCameraLayerDraws > 0,
                    "expect-shared-basis" => family.SharedCameraLayerDraws == 0 && family.SharedBasisLayerDraws > 0,
                    "expect-generic" => family.SharedCameraLayerDraws == 0 && family.SharedBasisLayerDraws == 0,
                    _ => throw new ArgumentOutOfRangeException(nameof(expectedFamily))
                }, "GPU rounded clip executes the explicitly expected compiled pipeline family : " + expectedFamily);
            }
            var changed = capture("rounded-cached-radius-" + compiled, ui => Cached(ui, true, 4, compiled));
            check(!changed.AsSpan().SequenceEqual(parent) && changed.AsSpan().SequenceEqual(capture("rounded-cached-replay-" + compiled,
                ui => Cached(ui, true, 4, compiled))), "GPU outer rounded radius invalidates unchanged retained content : " + compiled);
        }
    }
}
