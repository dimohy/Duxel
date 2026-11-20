using System.Numerics;
using Duxel.Core;
using Duxel.Vulkan;

internal static class ViewportClippingTests
{
    private static readonly UiVector2[] SingleSample = [new(.5f, .5f)];
    private static readonly UiVector2[] FourSamples =
        [new(.375f, .125f), new(.875f, .375f), new(.125f, .625f), new(.625f, .875f)];

    internal static void Run(VulkanRendererBackend renderer, Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, UiTextureId white, int width, int height)
    {
        if (width < 233 || height < 178)
        {
            throw new InvalidOperationException("Viewport clipping test requires at least a 233×178 framebuffer.");
        }

        var integerOnly = Environment.GetEnvironmentVariable("DUXEL_TEST_INTEGER_VIEWPORT") == "1";
        var fractional = renderer.ValidationViewportClipPlanes && !integerOnly;
        var first = fractional ? new UiRect(38.25f, 25.5f, 130, 103) : new UiRect(38, 25, 130, 103);
        var second = fractional ? new UiRect(107.5f, 68.25f, 125, 109) : new UiRect(107, 68, 125, 109);
        var clip = new UiRect(0, 0, width, height);

        foreach (var sampleCount in new[] { 1, 4 })
        {
            renderer.SetMsaaSamples(sampleCount);
            foreach (var reverse in new[] { false, true })
            {
                var pixels = capture($"viewport-clip-{sampleCount}-{reverse}", ui =>
                {
                    void Draw(string key, UiRect viewport, UiColor color, float depth, bool rounded)
                    {
                        var scene = new UiLayer3D(Matrix4x4.CreateTranslation(0, 0, depth)) { Viewport = viewport };
                        if (ui.BeginLayer(key, new(false, 1, default) { Scene3D = scene }))
                        {
                            var rect = new UiRect(viewport.X - 18, viewport.Y - 18, viewport.Width + 36, viewport.Height + 36);
                            var builder = ui.GetWindowDrawList();
                            if (rounded)
                                builder.AddRectFilledRounded(rect, color, white, 12, clip);
                            else
                                builder.AddRectFilled(rect, color, white, clip);
                        }
                        ui.EndLayer();
                    }

                    if (reverse)
                    {
                        Draw("viewport-clip-blue", second, new(0, 0, 255), .8f, false);
                        Draw("viewport-clip-red", first, new(255, 0, 0), .1f, true);
                    }
                    else
                    {
                        Draw("viewport-clip-red", first, new(255, 0, 0), .1f, true);
                        Draw("viewport-clip-blue", second, new(0, 0, 255), .8f, false);
                    }
                });
                var actualSamples = renderer.LastFrameMetrics.MsaaSamples;
                if (actualSamples > 1 && !renderer.ValidationStandardSampleLocations)
                    continue;

                var samples = actualSamples == 4 ? FourSamples : SingleSample;
                var mismatch = FindMismatch(pixels, width, height, first, second, reverse, samples);
                check(mismatch is null,
                    $"viewport clipping and isolated depth at MSAA {actualSamples}, fractional {fractional}, reverse {reverse}, first mismatch {mismatch}");
            }
        }
        CheckRoundedDepthMask(renderer, capture, check, white, width, height, first);
        if (integerOnly && !renderer.ValidationViewportClipPlanes)
        {
            var explicitUses = 0;
            renderer.ValidationDepthMask4 = (_, selected) => explicitUses += selected ? 1 : 0;
            try
            {
                capture("fractional-without-clip", ui =>
                {
                    var viewport = new UiRect(38.25f, 25.5f, 130, 103);
                    if (ui.BeginLayer("fractional-control", new(false, 1, default)
                        { Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .1f)) { Viewport = viewport } }))
                        ui.GetWindowDrawList().AddRectFilledRounded(new(50, 35, 70, 60), new(255, 0, 0), white, 12, clip);
                    ui.EndLayer();
                });
                check(explicitUses == 0, "fractional viewport without clip distances keeps the ordinary route");
            }
            finally
            {
                renderer.ValidationDepthMask4 = null;
            }
        }
        renderer.SetMsaaSamples(1);
    }

    private static void CheckRoundedDepthMask(VulkanRendererBackend renderer,
        Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiTextureId white, int width, int height, UiRect viewport)
    {
        var clip = new UiRect(0, 0, width, height);
        var shape = new UiRect(viewport.X + 12.3f, viewport.Y + 7.2f, 68.4f, 61.3f);
        const float radius = 17.4f;
        var texture = new UiTextureId(0x7800);
        var opaque = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 };
        var halfAlpha = new byte[] { 255, 255, 255, 64 };
        var selected = new List<int>();
        renderer.ValidationDepthMask4 = (mode, active) =>
        {
            if (active) selected.Add(mode);
        };
        try
        {
            byte[] Capture(string name, UiTextureUpdate? update = null, byte alpha = 255, bool retained = false,
                UiImageUvTransform? uv = null, UiVector2 translation = default, bool translateGeometry = false,
                float layerOpacity = 1f)
            {
                selected.Clear();
                return capture(name, ui =>
                {
                    if (update is { } upload)
                        ui.QueueTextureUpdate(upload);
                    var drawShape = translateGeometry
                        ? shape with { X = shape.X + translation.X, Y = shape.Y + translation.Y }
                        : shape;
                    for (var layer = 0; layer < (retained ? 2 : 1); layer++)
                    {
                        if (ui.BeginLayer("depth-mask-front-" + layer, new(retained, layerOpacity, translateGeometry ? default : translation)
                            { Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .1f)) { Viewport = viewport } }))
                        {
                            var draw = ui.GetWindowDrawList();
                            if (uv is { } imageUv)
                                draw.AddImageRounded(texture, drawShape, imageUv, new(255, 0, 0, alpha), radius, clip);
                            else
                                draw.AddRectFilledRounded(drawShape, new(255, 0, 0, alpha), texture, radius, clip);
                        }
                        ui.EndLayer();
                    }
                    if (ui.BeginLayer("depth-mask-back", new(false, 1, default)
                        { Scene3D = new(Matrix4x4.CreateTranslation(0, 0, .8f)) { Viewport = viewport } }))
                    {
                        ui.GetWindowDrawList().AddRectFilled(viewport, new(0, 0, 255), white, clip);
                    }
                    ui.EndLayer();
                });
            }

            var firstFrame = Capture("rounded-depth-mask-opaque",
                new(UiTextureUpdateKind.Create, texture, UiTextureFormat.Rgba8Unorm, 2, 1, opaque));
            var supported = renderer.ValidationDepthMask4Supported;
            check(selected.Contains(2) == supported, $"qualified rounded fill selects its 4× pipeline when supported ({supported})");
            if (supported)
            {
                string? mismatch = null;
                for (var y = (int)shape.Y; y < MathF.Ceiling(shape.Y + shape.Height) && mismatch is null; y++)
                {
                    for (var x = (int)shape.X; x < MathF.Ceiling(shape.X + shape.Width); x++)
                    {
                        var count = 0;
                        foreach (var sample in FourSamples)
                        {
                            var dx = Math.Max(0, Math.Abs(x + (double)sample.X - (shape.X + shape.Width / 2.0)) - (shape.Width / 2.0 - radius));
                            var dy = Math.Max(0, Math.Abs(y + (double)sample.Y - (shape.Y + shape.Height / 2.0)) - (shape.Height / 2.0 - radius));
                            if (dx * dx + dy * dy <= radius * (double)radius)
                                count++;
                        }
                        var index = (y * width + x) * 4;
                        if (Math.Abs(firstFrame[index] - 255 * count / 4.0) <= 1
                            && Math.Abs(firstFrame[index + 2] - 255 * (4 - count) / 4.0) <= 1)
                            continue;
                        mismatch = $"({x}, {y})";
                        break;
                    }
                }
                check(mismatch is null, $"rounded coverage masks color and depth at the four standard positions, first mismatch {mismatch}");
            }

            var partial = new UiTextureUpdate(UiTextureUpdateKind.Update, texture, UiTextureFormat.Rgba8Unorm, 2, 1, halfAlpha)
                { RegionWidth = 1, RegionHeight = 1 };
            var transparentFrame = Capture("rounded-depth-mask-region-alpha", partial);
            check(selected.Count == 0 && !transparentFrame.AsSpan().SequenceEqual(firstFrame),
                "partial texture upload invalidates opaque coverage and changes the composited output");
            var restored = Capture("rounded-depth-mask-restored", new(UiTextureUpdateKind.Update, texture, UiTextureFormat.Rgba8Unorm, 2, 1, opaque));
            check((selected.Contains(2) == supported) && restored.AsSpan().SequenceEqual(firstFrame),
                "full opaque texture replacement restores the prior coverage");
            var tinted = Capture("rounded-depth-mask-alpha-tint", alpha: 128);
            check(selected.Count == 0 && !tinted.AsSpan().SequenceEqual(firstFrame),
                "a translucent fill tint preserves the ordinary coverage route");
            var translation = new UiVector2(10, 9);
            var translatedGeometry = Capture("rounded-depth-mask-translated-reference", translation: translation, translateGeometry: true);
            var translatedCommand = Capture("rounded-depth-mask-translated-command", translation: translation);
            check(selected.Contains(2) == supported && (!supported || translatedCommand.AsSpan().SequenceEqual(translatedGeometry)),
                "command translation preserves the authored opacity proof and the translated geometry pixels");
            var translucentLayer = Capture("rounded-depth-mask-alpha-layer", translation: translation, layerOpacity: .5f);
            check(selected.Count == 0 && !translucentLayer.AsSpan().SequenceEqual(translatedCommand),
                "layer opacity preserves the ordinary route after command translation");
            var stretched = Capture("rounded-depth-mask-image", uv: UiImageUvTransform.Identity);
            check(selected.Contains(2) == supported && (!supported || stretched.AsSpan().SequenceEqual(firstFrame)),
                "opaque rounded image with bounded UV shares the rounded fill sample coverage");
            var letterboxed = Capture("rounded-depth-mask-image-uv-edge", uv: new UiImageUvTransform(new(-.5f, 0), new(2, 0), new(0, 1)));
            check(selected.Count == 0 && !letterboxed.AsSpan().SequenceEqual(stretched),
                "an independent UV edge keeps the original image clip");
            Capture("rounded-depth-mask-retained-warm", retained: true);
            var retainedFrame = Capture("rounded-depth-mask-retained", retained: true);
            check(renderer.LastFrameMetrics.CompiledLayerDraws > 0
                && selected.Any(mode => mode is 4 or 6 or 8) == supported
                && retainedFrame.AsSpan().SequenceEqual(firstFrame),
                "compiled opaque geometry uses the qualified route and preserves direct output");
            Capture("rounded-depth-mask-release", new(UiTextureUpdateKind.Destroy, texture, UiTextureFormat.Rgba8Unorm, 0, 0, default));
            check(selected.Count == 0, "texture destruction invalidates opaque coverage");
        }
        finally
        {
            renderer.ValidationDepthMask4 = null;
        }
    }

    private static string? FindMismatch(byte[] pixels, int width, int height, UiRect first, UiRect second,
        bool reverse, UiVector2[] samples)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var reds = 0;
                var blues = 0;
                foreach (var offset in samples)
                {
                    var point = new UiVector2(x + offset.X, y + offset.Y);
                    var red = Contains(first, point);
                    var blue = Contains(second, point);
                    if (red && (reverse || !blue))
                        reds++;
                    else if (blue)
                        blues++;
                }
                var index = (y * width + x) * 4;
                var expectedRed = 255f * reds / samples.Length;
                var expectedBlue = 255f * blues / samples.Length;
                if (MathF.Abs(pixels[index] - expectedRed) > 1f || pixels[index + 1] > 1
                    || MathF.Abs(pixels[index + 2] - expectedBlue) > 1f)
                    return $"({x}, {y})";
            }
        }
        return null;
    }

    private static bool Contains(UiRect rect, UiVector2 point)
        => point.X >= rect.X && point.Y >= rect.Y && point.X < rect.X + rect.Width && point.Y < rect.Y + rect.Height;
}
