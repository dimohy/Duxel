using System.Numerics;
using Duxel.Core;

internal static class CircleCutoutRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int stride, UiTextureId white, Func<UiRendererFrameMetrics> statistics, int imageCount)
    {
        var clip = new UiRect(0, 0, stride, 240);
        var bounds = new UiRect(24.25f, 24.25f, 96, 96);
        var center = new UiVector2(74.25f, 68.25f);
        const float radius = 18.5f;
        using var image = new UiImage(new(new(1, 1, [255, 64, 32, 255]))) { Size = new(96, 96), CornerRadius = 8 };
        using var text = Fluent.Body1("MMMMMMMMM");
        using var svg = new UiSvg(new SolidDocument()) { Size = new(96, 96), CornerRadius = 6 };
        int At(int x, int y) => (y * stride + x) * 4;
        void Background(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var list = ui.GetWindowDrawList();
            list.AddRectFilled(clip, new(0, 32, 180), white, clip);
            list.AddRectFilled(new(64, 0, 64, 240), new(16, 160, 96), white, clip);
        }
        var background = capture("background", Background);
        void Draw(UiImmediateContext ui, int kind, bool masked, float shift = 0)
        {
            Background(ui);
            var list = ui.GetWindowDrawList();
            using var cutout = masked ? list.CircleCutout(new(center.X + shift, center.Y), radius) : default;
            if (kind == 0) list.AddRectFilled(bounds, new(255, 0, 0), white, clip);
            else if (kind == 1) list.AddRectFilledRounded(bounds, new(255, 0, 0), white, 12, clip);
            else if (kind == 2) list.AddCircleFilled(new(72.25f, 72.25f), 48, new(255, 0, 0), white, clip);
            else if (kind == 3)
            {
                list.AddRectFilled(new(225, 215, 2, 2), new(255, 0, 0), white, clip);
                ui.SetCursorScreenPos(new(bounds.X, bounds.Y));
                image.Render(ui);
            }
            else if (kind == 4)
            {
                list.AddRectFilled(new(225, 215, 2, 2), new(255, 0, 0), white, clip);
                ui.SetCursorScreenPos(new(bounds.X, bounds.Y));
                svg.Render(ui);
            }
            else if (kind == 5)
            {
                ui.SetCursorScreenPos(new(28.25f, 60.25f));
                text.Render(ui);
            }
            else list.AddRectFilledGradient(bounds, new(new(24, 24), new(120, 120),
                new(255, 0, 0), new(255, 255, 0), new(255, 255, 255)), white, clip, 8);
        }
        for (var kind = 0; kind < 7; kind++)
        {
            var original = capture("original-" + kind, ui => Draw(ui, kind, false));
            var masked = capture("masked-" + kind, ui => Draw(ui, kind, true));
            var insideEqual = true;
            var outsideEqual = true;
            var removed = 0;
            var fractional = 0;
            for (var y = 20; y < 126; y++)
            for (var x = 20; x < 126; x++)
            {
                var distance = Vector2.Distance(new(x + .5f, y + .5f), new(center.X, center.Y));
                var offset = At(x, y);
                if (distance < radius - 2)
                {
                    insideEqual &= masked.AsSpan(offset, 4).SequenceEqual(background.AsSpan(offset, 4));
                    if (!original.AsSpan(offset, 4).SequenceEqual(background.AsSpan(offset, 4))) removed++;
                }
                if (distance > radius + 2) outsideEqual &= masked.AsSpan(offset, 4).SequenceEqual(original.AsSpan(offset, 4));
                if (kind == 0 && Math.Abs(distance - radius) < 2
                    && !masked.AsSpan(offset, 4).SequenceEqual(original.AsSpan(offset, 4))
                    && !masked.AsSpan(offset, 4).SequenceEqual(background.AsSpan(offset, 4))) fractional++;
            }
            check(insideEqual && removed > 3, "GPU circle removes actual content and preserves patterned rear pixels : " + kind);
            check(outsideEqual, "GPU circle preserves all distant source pixels including font coverage : " + kind);
            if (kind == 0) check(fractional > 0, "GPU fractional circle edge has analytic antialiasing even with one sample");
            check(masked.AsSpan().SequenceEqual(capture("replay-" + kind, ui => Draw(ui, kind, true))),
                "GPU unchanged circular cutout replays exact pixels : " + kind);
        }
        var first = capture("circle-initial", ui => Draw(ui, 3, true));
        for (var frame = 0; frame <= imageCount; frame++) capture("old-circle-warm-" + frame, ui => Draw(ui, 3, true));
        var changed = capture("circle-moved", ui => Draw(ui, 3, true, 24));
        check(!first.AsSpan().SequenceEqual(changed), "GPU circle-only mutation invalidates the acquired image while source geometry stays fixed");
        var repaired = true;
        for (var frame = 0; frame <= imageCount; frame++)
        {
            var repeated = capture("idle-" + frame, ui => Draw(ui, 3, true, 24));
            repaired &= repeated.AsSpan().SequenceEqual(changed)
                && repeated[At(74, 68)] == 255 && repeated[At(74, 68) + 1] == 64
                && repeated.AsSpan(At(98, 68), 4).SequenceEqual(background.AsSpan(At(98, 68), 4));
        }
        check(repaired, "GPU mask-only movement repairs every already-warmed acquired image without stale holes");
        var idle = statistics();
        check(idle.DrawCalls == 0 && idle.DynamicUploadBytes == 0, "GPU warmed unchanged circular cutout avoids draw and streamed upload");

        void Layers(UiImmediateContext ui, bool reverse)
        {
            Background(ui);
            for (var order = 0; order < 3; order++)
            {
                var index = reverse ? 2 - order : order;
                var origin = new UiVector2(20 + index * 88, 140);
                if (ui.BeginLayer("cutout-compiled-" + index, new(true, 1, default)
                    { ContentVersion = 1, Scene3D = new(Matrix4x4.Identity) }))
                {
                    var list = ui.GetWindowDrawList();
                    using var mask = list.CircleCutout(new(origin.X + 32, origin.Y + 32), 12);
                    list.AddRectFilled(new(origin.X, origin.Y, 2, 2), new(255, 0, 0), white, clip);
                    list.AddImageRounded(white, new(origin.X, origin.Y, 64, 64), UiImageUvTransform.Identity, new(255, 0, 0), 4, clip);
                }
                ui.EndLayer();
            }
        }
        var compiled = capture("compiled", ui => Layers(ui, false));
        var metrics = statistics();
        check(metrics.CompiledLayerDraws > 0 && metrics.CompiledLayerCommands >= 3,
            "GPU circle commands actually use compiled layer descriptors");
        check(compiled.AsSpan().SequenceEqual(capture("compiled-reversed", ui => Layers(ui, true))),
            "GPU compiled circular exclusions preserve all pixels under nonoverlapping submission reversal");
        for (var index = 0; index < 3; index++)
        {
            var offset = At(52 + index * 88, 172);
            check(compiled.AsSpan(offset, 4).SequenceEqual(background.AsSpan(offset, 4))
                && compiled[At(26 + index * 88, 146)] == 255,
                "GPU compiled image uses each rebased circular mask record : " + index);
        }

        void Depth(UiImmediateContext ui, bool reverse, float opacity)
        {
            Background(ui);
            void Back()
            {
                if (ui.BeginLayer("cutout-depth-back", new(false, 1, default)
                    { Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .5f)) }))
                    ui.GetWindowDrawList().AddRectFilled(bounds, new(0, 0, 255), white, clip);
                ui.EndLayer();
            }
            void Front()
            {
                if (ui.BeginLayer("cutout-depth-front", new(false, opacity, default)
                    { Scene3D = new(Matrix4x4.Identity, opacity == 1) }))
                {
                    var list = ui.GetWindowDrawList();
                    using var mask = list.CircleCutout(center, radius);
                    list.AddRectFilledRounded(bounds, new(255, 0, 0), white, 8, clip);
                }
                ui.EndLayer();
            }
            if (reverse) { Front(); Back(); } else { Back(); Front(); }
        }
        var depth = capture("depth", ui => Depth(ui, false, 1));
        check(depth.AsSpan().SequenceEqual(capture("depth-reversed", ui => Depth(ui, true, 1))),
            "GPU circular hole preserves every opaque depth sample under overlapping submission reversal");
        check(depth[At(74, 68) + 2] == 255 && depth[At(40, 40)] == 255,
            "GPU circular hole exposes the rear depth plane while opaque body writes front depth");
        var alpha = capture("depth-alpha", ui => Depth(ui, false, .5f));
        check(alpha[At(74, 68) + 2] == 255 && alpha[At(40, 40)] is >= 126 and <= 129
            && alpha[At(40, 40) + 2] is >= 126 and <= 129,
            "GPU circular subtraction preserves material opacity independently of hole coverage");

        void ScopedLayer(UiImmediateContext ui, bool outside, float shift = 0)
        {
            Background(ui);
            using var parentMask = outside ? ui.GetWindowDrawList().CircleCutout(new(center.X + 12 + shift, center.Y + 7), radius) : default;
            if (ui.BeginLayer(outside ? "cutout-parent-scope" : "cutout-child-scope", new(true, 1, new(12, 7))
                { ContentVersion = 1, Scene3D = new(Matrix4x4.Identity) }))
            {
                var list = ui.GetWindowDrawList();
                using var childMask = outside ? default : list.CircleCutout(center, radius);
                list.AddImageRounded(white, bounds, UiImageUvTransform.Identity, new(255, 0, 0), 8, clip);
            }
            ui.EndLayer();
        }
        var childScope = capture("child-scope", ui => ScopedLayer(ui, false));
        var parentScope = capture("parent-scope", ui => ScopedLayer(ui, true));
        check(childScope[At(50, 50)] == 255 && childScope.AsSpan().SequenceEqual(parentScope),
            "GPU outer cutout of a translated retained layer matches a mask captured inside its geometry");
        check(parentScope.AsSpan().SequenceEqual(capture("parent-scope-replay", ui => ScopedLayer(ui, true))),
            "GPU cached translated layer replays its scoped circular exclusion without mutating its source");
        var changedParent = capture("parent-scope-moved", ui => ScopedLayer(ui, true, 24));
        check(!parentScope.AsSpan().SequenceEqual(changedParent),
            "GPU outer mask mutation updates an unchanged cached translated layer");
        check(changedParent.AsSpan().SequenceEqual(capture("parent-scope-moved-replay", ui => ScopedLayer(ui, true, 24))),
            "GPU changed outer mask then replays exact cached-layer pixels");

        void CachedOuter(UiImmediateContext ui, float shift, bool depth = true)
        {
            Background(ui);
            using var mask = ui.GetWindowDrawList().CircleCutout(new(144 + shift, 172), 42);
            for (var index = 0; index < 3; index++)
            {
                if (ui.BeginLayer("cutout-cached-outer-" + depth + "-" + index, new(true, 1, default)
                    { ContentVersion = 1, Scene3D = depth ? new(Matrix4x4.Identity) : null }))
                    ui.GetWindowDrawList().AddImageRounded(white, new(20 + index * 88, 140, 64, 64),
                        UiImageUvTransform.Identity, new(255, 0, 0), 4, clip);
                ui.EndLayer();
            }
        }
        var cachedOuter = capture("cached-outer", ui => CachedOuter(ui, 0));
        var cachedMetrics = statistics();
        check(cachedMetrics.CompiledLayerDraws > 0 && cachedMetrics.CompiledLayerCommands >= 3
            && cachedOuter[At(32, 148)] == 255 && cachedOuter[At(220, 148)] == 255,
            "GPU externally masked cached layers actually use compiled descriptors");
        var cachedChanged = capture("cached-outer-moved", ui => CachedOuter(ui, 40));
        check(!cachedOuter.AsSpan().SequenceEqual(cachedChanged),
            "GPU external mask-only mutation invalidates compiled cached layers with fixed source versions");
        var cachedExact = true;
        for (var frame = 0; frame <= imageCount; frame++)
            cachedExact &= cachedChanged.AsSpan().SequenceEqual(capture("cached-outer-idle-" + frame, ui => CachedOuter(ui, 40)));
        var retainedMetrics = statistics();
        check(cachedExact && retainedMetrics.ResidentUpdates == 0 && retainedMetrics.ResidentCreates == 0,
            $"GPU changed compiled depth-layer mask replays exact pixels without recreating or updating resident geometry; exact={cachedExact}, updates={retainedMetrics.ResidentUpdates}, creates={retainedMetrics.ResidentCreates}, totalDynamic={retainedMetrics.DynamicUploadBytes}");
        var flatOld = capture("flat-cached-outer", ui => CachedOuter(ui, 0, false));
        for (var frame = 0; frame <= imageCount; frame++) capture("flat-old-warm-" + frame, ui => CachedOuter(ui, 0, false));
        var flatChanged = capture("flat-cached-outer-moved", ui => CachedOuter(ui, 40, false));
        check(!flatOld.AsSpan().SequenceEqual(flatChanged), "GPU external circle mutation updates ordinary2D cached layers");
        var flatExact = true;
        for (var frame = 0; frame <= imageCount; frame++)
            flatExact &= flatChanged.AsSpan().SequenceEqual(capture("flat-cached-outer-idle-" + frame, ui => CachedOuter(ui, 40, false)));
        check(flatExact && statistics().DrawCalls == 0 && statistics().DynamicUploadBytes == 0,
            "GPU ordinary2D cached-layer masks repair every acquired image then avoid draw and streamed upload");

        var deferredChild = new UiDrawListBuilder(clip);
        void Deferred(UiImmediateContext ui)
        {
            Background(ui);
            deferredChild._ResetForNewFrame();
            deferredChild.AddImageRounded(white, bounds, UiImageUvTransform.Identity, new(255, 0, 0), 8, clip);
            var parent = ui.GetWindowDrawList();
            using var mask = parent.CircleCutout(center, radius);
            parent.AppendDeferredFrom(deferredChild);
        }
        var deferredPixels = capture("deferred-child", Deferred);
        check(deferredPixels[At(40, 40)] == 255
            && deferredPixels.AsSpan(At(74, 68), 4).SequenceEqual(background.AsSpan(At(74, 68), 4)),
            "GPU deferred child painting preserves its visible body and parent circular hole");
        check(deferredPixels.AsSpan().SequenceEqual(capture("deferred-child-replay", Deferred)),
            "GPU deferred child circular subtraction reproduces exact replay pixels");
    }
    private sealed class SolidDocument : IUiGpuSvgDocument
    {
        public UiGpuVectorScene GpuScene { get; } = GpuVectorTestScenes.Solid(new(96, 96));
        public UiVector2 IntrinsicSize => new(96, 96);
        public long Revision => 0;
        public event Action? Changed { add { } remove { } }
        public UiSvgRaster Rasterize(int width, int height)
        {
            var pixels = new byte[checked(width * height * 4)];
            for (var index = 0; index < pixels.Length; index += 4)
            {
                pixels[index] = 255;
                pixels[index + 3] = 255;
            }
            return new(width, height, pixels);
        }
    }
}
