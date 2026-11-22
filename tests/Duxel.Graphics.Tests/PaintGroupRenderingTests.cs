using Duxel.Core;

internal static class PaintGroupRenderingTests
{
    internal static void Run(UiContext context, Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, Func<Action<UiImmediateContext>, UiDrawData> build, Func<UiDrawData, byte[]> render, Func<UiDrawData, bool> submit, Func<int> geometryEntries, int stride, UiTextureId white, Func<UiRendererFrameMetrics> metrics, int imageCount,
        Func<UiTextureId, (ulong Black, ulong White, ulong Msaa)> targetGenerations, bool forceFullReference = false, bool dualSourceBlend = true)
    {
        var black = new UiTextureId(91001);
        var whiteOutput = new UiTextureId(91002);
        var source = new UiTextureId(91003);
        var font = new UiTextureId(1_100_123_457);
        var unrelated = new UiTextureId(91004);
        var clip = new UiRect(0, 0, 80, 80);
        var builder = new UiDrawListBuilder(clip);
        var sourceCutout = builder.CircleCutout(new(56, 8), 3);
        builder.AddRectFilled(new(0, 0, 64, 64), new(255, 0, 0), white, clip);
        builder.AddRectFilled(new(16, 16, 32, 32), new(0, 255, 0), white, clip);
        builder.AddImage(source, new(48, 48), new(64, 64), new(0, 0), new(1, 1), new(255, 255, 255));
        builder.AddImage(font, new(0, 64), new(16, 80), new(0, 0), new(1, 1), new(255, 255, 255));
        sourceCutout.Dispose();
        var lists = builder.Build();
        using var content = new UiPaintGroupContent(clip, new(1, 1), lists);
        foreach (var list in lists)
            list.ReleasePooled();
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, source, UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 255, 0, 255 })
        {
            IsColorImage = true
        });
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, font, UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 64, 0, 0 }));
        context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderPaintGroup, black, UiTextureFormat.Rgba8Unorm,
            content.PixelWidth, content.PixelHeight, ReadOnlyMemory<byte>.Empty)
        {
            PaintGroup = content.Acquire(),
            WhitePaintTexture = whiteOutput,
            IsColorImage = true
        });
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var draw = ui.GetWindowDrawList();
            draw.AddRectFilled(new(0, 0, stride, 240), new(0, 0, 255), white);
            draw.AddBackdropComposite(black, whiteOutput, new(24, 24, 80, 80), .8f);
            draw.AddRectFilled(new(140, 24, 16, 16), new(255, 0, 255), white);
        }
        int At(int x, int y) => (y * stride + x) * 4;
        var pixels = capture("captured-overlap", Draw);
        check(pixels[At(32, 32)] == 204 && pixels[At(32, 32) + 2] == 51,
            "GPU runtime captured first primitive receives group opacity once");
        check(pixels[At(56, 56)] == 0 && pixels[At(56, 56) + 1] == 204 && pixels[At(56, 56) + 2] == 51,
            "GPU runtime overlapping primitives compose before group opacity; no red leaks through overlap");
        check(pixels[At(96, 96) + 2] == 255 && pixels[At(145, 28)] == 255,
            "GPU runtime capture preserves transparent margins and restores ordinary main rendering");
        var textOffset = At(32, 96);
        if (dualSourceBlend)
        {
            check(pixels[textOffset] == 204 && Math.Abs(pixels[textOffset + 1] - 51) <= 1 && pixels[textOffset + 2] == 255,
                "GPU runtime capture preserves independent LCD RGB coverage when glyph alpha is zero");
        }
        else
        {
            check(Math.Abs(pixels[textOffset] - 85) <= 2 && Math.Abs(pixels[textOffset + 1] - 85) <= 2 && pixels[textOffset + 2] == 255,
                "GPU runtime capture uses grayscale coverage when glyph alpha is zero");
        }
        check(pixels[At(80, 32)] == 0 && pixels[At(80, 32) + 2] == 255,
            "GPU runtime source circular subtraction preserves a transparent hole through both capture passes");
        check(metrics().PaintGroupPasses == 2 && metrics().PaintGroupDrawCalls > 0,
            $"GPU capture actually executes two native offscreen rendering passes; actual={metrics().PaintGroupPasses},draws={metrics().PaintGroupDrawCalls}");
        for (var frame = 0; frame <= imageCount; frame++)
            check(pixels.AsSpan().SequenceEqual(capture("cached-" + frame, Draw)),
                "GPU runtime capture cache preserves exact pixels across acquired images : " + frame);
        check(metrics().PaintGroupPasses == 0 && metrics().PaintGroupCacheHits == 1,
            "GPU unchanged runtime capture reuses its offscreen result");
        check(forceFullReference ? metrics().DrawCalls > 0 : metrics().DrawCalls == 0 && metrics().DynamicUploadBytes == 0,
            forceFullReference ? "Forced-full reference actually redraws its warmed main scene"
                : "GPU unchanged runtime capture skips both offscreen passes and warmed main draws/uploads");
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, unrelated, UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 255, 0, 0, 255 })
        {
            IsColorImage = true
        });
        check(pixels.AsSpan().SequenceEqual(capture("unrelated-update", Draw)) && metrics().PaintGroupPasses == 0,
            "GPU unrelated texture upload does not recapture immutable group geometry");
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Update, source, UiTextureFormat.Rgba8Unorm, 1, 1, new byte[] { 0, 255, 255, 255 })
        {
            IsColorImage = true
        });
        var changed = capture("source-texture-only", Draw);
        check(changed[At(80, 80)] == 0 && changed[At(80, 80) + 1] == 204 && changed[At(80, 80) + 2] == 255
            && metrics().PaintGroupPasses == 2,
            "GPU source-texture-only update invalidates capture with unchanged source geometry");
        var localRepair = IsLocalRepair();
        for (var frame = 0; frame <= imageCount; frame++)
        {
            // An unchanged present can reuse an image without warming other swapchain
            // images. Advance a source revision so every attempt exercises the repair path.
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Update, source, UiTextureFormat.Rgba8Unorm,
                1, 1, new byte[] { 0, 255, 255, 255 }) { IsColorImage = true });
            check(changed.AsSpan().SequenceEqual(capture("source-repaired-" + frame, Draw)),
                "GPU changed source repairs each acquired image without stale cached pixels : " + frame);
            localRepair |= IsLocalRepair();
        }
        if (!forceFullReference)
            check(localRepair, "A new paint-group output revision repairs only its consumer bounds on an initialized image");
        bool IsLocalRepair() => metrics().RepaintBounds is { X: 22, Y: 22, Width: 84, Height: 84 };
        var projectedBlack = new UiTextureId(91005);
        var projectedWhite = new UiTextureId(91006);
        using var projectedContent = new UiPaintGroupContent(clip, new(.875f, .875f), content.DrawLists, UiPaintGroupTextMode.Projected);
        context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderPaintGroup, projectedBlack, UiTextureFormat.Rgba8Unorm,
            projectedContent.PixelWidth, projectedContent.PixelHeight, ReadOnlyMemory<byte>.Empty)
        {
            PaintGroup = projectedContent.Acquire(),
            WhitePaintTexture = projectedWhite,
            IsColorImage = true
        });
        var projectedPixels = capture("projected-lcd", ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var draw = ui.GetWindowDrawList();
            draw.AddRectFilled(new(0, 0, stride, 240), new(0, 0, 255), white);
            draw.AddBackdropComposite(projectedBlack, projectedWhite, new(24, 24, 70, 70), .8f);
        });
        var coverage = (255 + 64) / 3f * .8f;
        check(Math.Abs(projectedPixels[At(28, 88)] - coverage) <= 2
            && Math.Abs(projectedPixels[At(28, 88) + 1] - coverage) <= 2 && projectedPixels[At(28, 88) + 2] == 255,
            "GPU flat projected capture uses the existing projected LCD channel-coverage contract");
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, projectedBlack, UiTextureFormat.Rgba8Unorm, 70, 70, ReadOnlyMemory<byte>.Empty));
        PresentationTestPlatform.ConfigureFonts(context);
        using var textGroup = new UiPaintGroup();
        var typography = capture("projected-real-text", ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, stride, 240), new(16, 16, 16), white);
            ui.PaintGroup(textGroup, new(0, 0, 96, 48), 1,
                paint =>
                {
                    using var textStyle = paint.BeginTypography<UiRegularTypeface, UiFluentType300>();
                    paint.Text("한글 Aa", new(2, 2), new(255, 255, 255));
                },
                destination: new(16, 16, 84, 42), rasterScale: .875f, textMode: UiPaintGroupTextMode.Projected);
        });
        var textPixels = 0;
        var neutral = true;
        for (var y = 16; y < 58; y++)
            for (var x = 16; x < 100; x++)
            {
                var at = At(x, y);
                neutral &= Math.Abs(typography[at] - typography[at + 1]) <= 1 && Math.Abs(typography[at + 1] - typography[at + 2]) <= 1;
                if (typography[at] > 32)
                    textPixels++;
            }
        check(neutral && textPixels > 30, "GPU final-resolution projected Korean and Latin glyphs remain visible without RGB fringes");
        textGroup.Dispose();
        using var group = new UiPaintGroup();
        var builds = 0;
        var groupBounds = new UiRect(160, 96, 64, 64);
        var version = 1UL;
        var opacity = .8f;
        var green = false;
        void Group(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, stride, 240), new(0, 0, 255), white);
            ui.PaintGroup(group, groupBounds, version, paint =>
            {
                builds++;
                paint.Frame(groupBounds, green ? new(0, 255, 0) : new(255, 0, 0), default, 0, 0);
            }, opacity);
        }
        var actual = capture("api-initial", Group);
        check(builds == 1 && actual[At(180, 116)] == 204 && metrics().PaintGroupPasses == 2,
            "GPU public pure-paint API captures its real drawing callback");
        groupBounds = groupBounds with
        {
            X = 192
        };
        opacity = .4f;
        var relocated = capture("api-relocated", Group);
        check(builds == 1 && relocated[At(212, 116)] == 102 && relocated[At(180, 116)] == 0
            && metrics().PaintGroupPasses == 0,
            "GPU cached paint moves and changes final opacity without source callback or recapture");
        version++;
        green = true;
        var revised = capture("api-revised", Group);
        check(builds == 2 && revised[At(212, 116)] == 0 && revised[At(212, 116) + 1] == 102
            && metrics().PaintGroupPasses == 2,
            "GPU paint content-version replacement releases old ownership and paints the new immutable snapshot");
        group.Dispose();
        using var parent = new UiPaintGroup();
        using var child = new UiPaintGroup();
        void Nested(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, stride, 240), new(0, 0, 255), white);
            ui.PaintGroup(parent, new(16, 128, 64, 64), 1, paint =>
            {
                paint.Frame(new(16, 128, 64, 64), new(255, 0, 0), default, 0, 0);
                paint.PaintGroup(child, new(32, 144, 32, 32), 1,
                    nested => nested.Frame(new(32, 144, 32, 32), new(0, 255, 0), default, 0, 0), .5f);
            }, .5f);
        }
        var nestedPixels = capture("nested", Nested);
        check(Math.Abs(nestedPixels[At(48, 160)] - 64) <= 1 && Math.Abs(nestedPixels[At(48, 160) + 1] - 64) <= 1
            && Math.Abs(nestedPixels[At(48, 160) + 2] - 128) <= 1 && metrics().PaintGroupPasses == 4,
            "GPU nested paint groups render child before parent with both isolated opacities");
        parent.Dispose();
        child.Dispose();
        using var queuedGroup = new UiPaintGroup();
        var queuedGreen = false;
        var queuedVersion = 1UL;
        void Queued(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, stride, 240), new(0, 0, 255), white);
            ui.PaintGroup(queuedGroup, new(96, 128, 32, 32), queuedVersion,
                paint => paint.Frame(new(96, 128, 32, 32), queuedGreen ? new(0, 255, 0) : new(255, 0, 0), default, 0, 0), .8f);
        }
        using var queuedFirst = build(Queued);
        queuedVersion++;
        queuedGreen = true;
        using var queuedSecond = build(Queued);
        queuedGroup.Dispose();
        var earlierPixels = render(queuedFirst);
        var laterPixels = render(queuedSecond);
        check(earlierPixels[At(112, 144)] == 204 && earlierPixels[At(112, 144) + 1] == 0
            && laterPixels[At(112, 144)] == 0 && laterPixels[At(112, 144) + 1] == 204,
            "GPU queued frames keep independent captured geometry after producer reuse and group-owner disposal");

        var disposedDuringPaint = new UiPaintGroup();
        var sealedPixels = capture("disposed-before-seal", ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, stride, 240), new(0, 0, 255), white);
            ui.PaintGroup(disposedDuringPaint, new(96, 128, 32, 32), 1,
                paint => paint.Frame(new(96, 128, 32, 32), new(255, 0, 0), default, 0, 0), .8f);
            disposedDuringPaint.Dispose();
        });
        check(sealedPixels[At(112, 144)] == 204 && sealedPixels[At(112, 144) + 2] == 51,
            "GPU owner disposal before frame sealing preserves the already recorded paint operation");
        using var asynchronous = new UiPaintGroup();
        var asynchronousVersion = 0UL;
        var asynchronousGreen = false;
        var asynchronousBounds = new UiRect(96, 128, 32, 32);
        void Asynchronous(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, stride, 240), new(0, 0, 255), white);
            ui.PaintGroup(asynchronous, asynchronousBounds, asynchronousVersion,
                paint => paint.Frame(asynchronousBounds, asynchronousGreen ? new(0, 255, 0) : new(255, 0, 0), default, 0, 0), .8f);
        }
        var geometryBeforeUpdates = geometryEntries();
        var geometryRemainsBounded = true;
        var targetsUnchanged = true;
        (ulong Black, ulong White, ulong Msaa) originalTargets = default;
        UiTextureId asynchronousTexture = default;
        for (var frame = 0; frame < 12; frame++)
        {
            asynchronousVersion++;
            asynchronousGreen = (frame & 1) != 0;
            using var submitted = build(Asynchronous);
            asynchronousTexture = submitted.TextureUpdates.Single(update => update.Kind == UiTextureUpdateKind.RenderPaintGroup).TextureId;
            check(submit(submitted), "GPU paint replacement submits through the ordinary path without per-frame readback : " + frame);
            var targets = targetGenerations(asynchronousTexture);
            if (frame == 0)
                originalTargets = targets;
            else
                targetsUnchanged &= targets == originalTargets;
            geometryRemainsBounded &= geometryEntries() <= geometryBeforeUpdates + 1;
        }
        check(targetsUnchanged && originalTargets.Black != 0 && originalTargets.White != 0,
            "GPU same-extent content revisions reuse both matte images and the multisample attachment");
        check(geometryRemainsBounded, "GPU replaced paint geometry retires promptly rather than accumulating for the general cache grace period");
        var asynchronousPixels = capture("asynchronous-final", Asynchronous);
        check(asynchronousPixels[At(112, 144)] == 0 && asynchronousPixels[At(112, 144) + 1] == 204,
            "GPU latest capture pixels survive repeated ordinary submissions and deferred target retirement");
        asynchronousBounds = asynchronousBounds with { Width = 48 };
        asynchronousVersion++;
        var differentExtent = capture("asynchronous-new-extent", Asynchronous);
        // A driver may reuse the same opaque image handle after destroying its previous object.
        var newTargets = targetGenerations(asynchronousTexture);
        check(newTargets.Black > originalTargets.Black && newTargets.White > originalTargets.White
            && (originalTargets.Msaa == 0 ? newTargets.Msaa == 0 : newTargets.Msaa > originalTargets.Msaa)
            && differentExtent[At(140, 144) + 1] == 204,
            "GPU changed paint extent replaces attachments and fills the newly visible area");
        asynchronous.Dispose();
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, black, UiTextureFormat.Rgba8Unorm, 80, 80, ReadOnlyMemory<byte>.Empty));
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, source, UiTextureFormat.Rgba8Unorm, 1, 1, ReadOnlyMemory<byte>.Empty));
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, font, UiTextureFormat.Rgba8Unorm, 1, 1, ReadOnlyMemory<byte>.Empty));
        context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, unrelated, UiTextureFormat.Rgba8Unorm, 1, 1, ReadOnlyMemory<byte>.Empty));
        capture("destroyed", ui => ui.EnableRootViewportContentLayout(contentPadding: 0));
    }
}
