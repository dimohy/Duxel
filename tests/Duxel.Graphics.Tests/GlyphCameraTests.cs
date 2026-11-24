using System.Globalization;
using System.Numerics;
using Duxel.Core;

internal static class GlyphCameraTests
{
    internal static void Run(string output, PresentationReuseTests.Fixture fixture,
        IPlatformVectorTextBackend backend, PlatformTextMeshRequest request, Action<bool, string> check)
    {
        Directory.CreateDirectory(output);
        var scene = UiVectorGlyphSource.Create(backend.CreateTextMesh(request with { OutlineOnly = true }), 1).Scene
            ?? throw new InvalidOperationException("Expected a drawable original-outline scene.");
        var referenceMesh = backend.CreateTextMesh(request);
        // Distinct buffers with equal curves exercise the independently placed reference.
        var independentGlyphs = referenceMesh.Glyphs.ToArray();
        for (var i = 0; i < independentGlyphs.Length; i++)
        {
            var glyph = independentGlyphs[i];
            independentGlyphs[i] = glyph with { Outline = glyph.Outline with { Curves = glyph.Outline.Curves.ToArray() } };
        }
        var reference = UiVectorGlyphSource.Create(referenceMesh with { Glyphs = independentGlyphs }, 1).Scene
            ?? throw new InvalidOperationException("Expected a drawable reference glyph scene.");
        check(scene.CommandCount == reference.CommandCount && scene.Data.Length < reference.Data.Length,
            "Repeated Hangul and Latin glyph instances share their immutable GPU payload");
        var broadData = scene.Data.ToArray();
        var boundedArea = 0f;
        for (var i = 0; i < scene.CommandCount; i++)
        {
            var index = i * 6;
            var bounds = broadData[index];
            boundedArea += (bounds.Z - bounds.X) * (bounds.W - bounds.Y);
            broadData[index] = new(0, 0, scene.Size.X, scene.Size.Y);
        }
        var broadArea = scene.Size.X * scene.Size.Y * scene.CommandCount;
        check(boundedArea < broadArea * .25f, "Individual glyph coverage bounds exclude most of the run area");
        var broadBounds = new UiGpuVectorScene(scene.Size, broadData, scene.CommandCount);
        File.WriteAllLines(Path.Combine(output, "payload.txt"),
            [$"Shared records: {scene.Data.Length}", $"Independent records: {reference.Data.Length}", $"Glyph instances: {scene.CommandCount}",
                FormattableString.Invariant($"Broad area: {broadArea}"), FormattableString.Invariant($"Bounded area: {boundedArea}")]);
        var retainedId = new UiTextureId(2_300_000_021);
        var referenceId = new UiTextureId(2_300_000_022);
        var broadId = new UiTextureId(2_300_000_024);
        var renderer = fixture.Renderer;

        byte[] Read(UiTextureId id, UiGpuVectorScene source, int width, int height, int pixelOrigin = 0)
        {
            using var data = fixture.Build(_ => fixture.Context.QueueTextureUpdate(new(
                UiTextureUpdateKind.RenderVectorScene, id, UiTextureFormat.Rgba8Unorm, width, height, ReadOnlyMemory<byte>.Empty)
            {
                VectorScene = source,
                VectorCamera = new(new(width, height), new(pixelOrigin, 0))
            }));
            return renderer.RenderVectorAndReadback(data, id, width, height);
        }

        var initial = renderer.VectorSceneDiagnostics;
        var smallWidth = (int)MathF.Ceiling(scene.Size.X * .25f);
        var smallHeight = (int)MathF.Ceiling(scene.Size.Y * .25f);
        var fitWidth = (int)MathF.Ceiling(scene.Size.X * .04f);
        var fitHeight = (int)MathF.Ceiling(scene.Size.Y * .04f);
        var largeWidth = (int)MathF.Ceiling(scene.Size.X * 2);
        var largeHeight = (int)MathF.Ceiling(scene.Size.Y * 2);
        var closeWidth = (int)MathF.Ceiling(scene.Size.X * 6);
        var closeHeight = (int)MathF.Ceiling(scene.Size.Y * 6);
        var small = Read(retainedId, scene, smallWidth, smallHeight);
        var fit = Read(retainedId, scene, fitWidth, fitHeight);
        var large = Read(retainedId, scene, largeWidth, largeHeight);
        var close = Read(retainedId, scene, closeWidth, closeHeight);
        var replay = Read(retainedId, scene, smallWidth, smallHeight);
        var zoomed = renderer.VectorSceneDiagnostics;
        check(zoomed.Uploads - initial.Uploads == 1 && zoomed.Dispatches - initial.Dispatches == 5,
            "Vulkan uploads the original glyph scene once and evaluates coverage for each zoom change");
        check(small.AsSpan().SequenceEqual(replay), "Original Hangul coverage returns byte-identically after a zoom round trip");
        foreach (var (pixels, width, height, name) in new[]
                 {
                     (small, smallWidth, smallHeight, "quarter"), (large, largeWidth, largeHeight, "double"),
                     (close, closeWidth, closeHeight, "sixfold"), (fit, fitWidth, fitHeight, "fit")
                 })
        {
            var expected = Read(referenceId, reference, width, height);
            var hasInk = false;
            var hasBackground = false;
            for (var i = 3; i < pixels.Length; i += 4)
            {
                hasInk |= pixels[i] != 0;
                hasBackground |= pixels[i] == 0;
            }
            check(hasInk && (hasBackground || name == "fit") && pixels.AsSpan().SequenceEqual(expected),
                "Original-only mixed-script RGBA matches the existing glyph coverage at " + name + " scale");
            check(pixels.AsSpan().SequenceEqual(Read(broadId, broadBounds, width, height)),
                "Per-glyph bounds preserve broad-bound RGBA at " + name + " scale");
            File.WriteAllBytes(Path.Combine(output, name + ".rgba"), pixels);
        }
        if (Environment.GetEnvironmentVariable("DUXEL_TEST_GLYPH_TIMING") != "1")
            return;
        using var timing = new StreamWriter(Path.Combine(output, "timing.csv"));
        timing.WriteLine("pair,mode,frame,gpuUs");
        const int warmup = 16, measured = 48, pairs = 3;
        for (var pair = 0; pair < pairs; pair++)
        {
            for (var arm = 0; arm < 2; arm++)
            {
                var useTightBounds = (pair + arm) % 2 == 1;
                var id = useTightBounds ? retainedId : broadId;
                var input = useTightBounds ? scene : broadBounds;
                var mode = useTightBounds ? "tight" : "broad";
                var before = renderer.VectorSceneDiagnostics;
                for (var frame = 0; frame < warmup + measured; frame++)
                {
                    Read(id, input, closeWidth, closeHeight, frame % 2);
                    if (frame < warmup)
                        continue;
                    var gpu = renderer.LastFrameMetrics.GpuRenderMicroseconds;
                    if (!double.IsFinite(gpu) || gpu <= 0)
                        throw new InvalidOperationException("Glyph timing requires GPU timestamps.");
                    timing.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{pair},{mode},{frame - warmup},{gpu:R}"));
                }
                check(renderer.VectorSceneDiagnostics.Dispatches - before.Dispatches == warmup + measured
                    && renderer.VectorSceneDiagnostics.Uploads == before.Uploads,
                    $"Every {mode} frame recomputes coverage without a geometry upload in pair {pair + 1}");
            }
        }
    }
}
