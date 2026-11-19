using Duxel.Core;
using Duxel.Svg;
using Duxel.Vulkan;

internal static class GpuVectorSceneTests
{
    internal static void Run(string output, UiContext context, VulkanRendererBackend renderer,
        Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int width)
    {
        GpuVectorDashContractTests.Run(output, check);
        GpuVectorConicContractTests.Run(output, check);
        GpuVectorRectangleContractTests.Run(check);
        GpuVectorRingContractTests.Run(check);
        var source = Path.Combine(output, "gpu-vector.svg");
        File.WriteAllText(source, """
            <svg width="64" height="48">
              <defs><linearGradient id="g"><stop stop-color="red"/><stop offset="1" stop-color="blue"/></linearGradient></defs>
              <rect width="64" height="48" fill="url(#g)"/>
              <g opacity=".5"><rect x="24" y="12" width="24" height="24" fill="lime"/></g>
              <path d="M4 4H20V20H4Z M8 8H16V16H8Z" fill="white" fill-rule="evenodd"/>
            </svg>
            """);
        using var store = new SvgStore();
        var scene = store.Load(source).GpuScene;
        GpuVectorOpacityContractTests.Run(scene, check);
        check(ReferenceEquals(scene, store.Load(source).GpuScene), "GPU SVG compilation retains one immutable scene");
        var id = new UiTextureId(2_300_000_001);
        byte[] Draw(int pixelWidth, int pixelHeight, bool prepare)
        {
            return capture($"gpu-vector-{pixelWidth}-{pixelHeight}-{prepare}", ui =>
            {
                if (prepare)
                    context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderVectorScene, id, UiTextureFormat.Rgba8Unorm,
                        pixelWidth, pixelHeight, ReadOnlyMemory<byte>.Empty)
                    {
                        VectorScene = scene,
                        IsColorImage = true
                    });
                ui.GetWindowDrawList().AddImage(id, new(16, 16), new(144, 112), default, new(1, 1), new(255, 255, 255));
            });
        }
        var first = Draw(128, 96, true);
        int At(int x, int y) => (y * width + x) * 4;
        check(first[At(20, 70)] > 230 && first[At(138, 70) + 2] > 230,
            "Vulkan computes SVG gradients from vector data without CPU pixels");
        check(first[At(28, 28)] == 255 && first[At(28, 28) + 1] == 255
            && first[At(40, 40) + 1] < 8,
            "GPU path winding preserves even-odd holes");
        check(first[At(80, 60) + 1] is >= 126 and <= 129,
            "GPU group opacity composites its children once");
        var initial = renderer.VectorSceneDiagnostics;
        for (var frame = 0; frame < 8; frame++)
            check(first.AsSpan().SequenceEqual(Draw(128, 96, false)), "GPU vector output survives acquired-image replay : " + frame);
        check(renderer.VectorSceneDiagnostics == initial, "Unchanged SVG frames perform no vector upload or compute dispatch");
        foreach (var scale in new[] { .25f, .5f, 1f, 1.01f, 2f, 4f })
        {
            var pixels = Draw((int)MathF.Ceiling(128 * scale), (int)MathF.Ceiling(96 * scale), true);
            check(pixels[At(20, 70)] > 200 && pixels[At(138, 70) + 2] > 200, "GPU-only zoom preserves vector paint : " + scale);
        }
        check(renderer.VectorSceneDiagnostics.Uploads == initial.Uploads,
            "Zoom reuses the device-resident SVG scene instead of uploading new paths");
        check(store.Statistics is { XmlParses: 1, SceneCompiles: 1, Rasterizations: 0, CachedRasterBytes: 0 },
            "GPU SVG output never invokes the CPU rasterizer or creates a CPU image cache");
        var emptyStrokePath = Path.Combine(output, "zero-width-stroke.svg");
        File.WriteAllText(emptyStrokePath, "<svg width='64' height='48'><rect x='8' y='8' width='48' height='32' fill='none' stroke='blue' stroke-width='0'/></svg>");
        scene = store.Load(emptyStrokePath).GpuScene;
        var zeroWidth = Draw(128, 96, true);
        var noStrokeWork = scene.Strokes.Count == 0;
        var emptyPath = Path.Combine(output, "empty.svg");
        File.WriteAllText(emptyPath, "<svg width='64' height='48'/>");
        scene = store.Load(emptyPath).GpuScene;
        check(noStrokeWork && zeroWidth.AsSpan().SequenceEqual(Draw(128, 96, true)),
            "A zero-width SVG stroke matches an empty document and creates no GPU stroke work");
        scene = GpuVectorTestScenes.Solid(new(64, 48), .5f / 255, 2.5f / 255, 4.5f / 255);
        var halfChannels = Draw(128, 96, true);
        check(halfChannels[At(20, 70)] == 0 && halfChannels[At(20, 70) + 1] == 2 && halfChannels[At(20, 70) + 2] == 4,
            "GPU RGBA8 half-channel ties match the shared nearest-even color contract");
        var effects = Path.Combine(output, "gpu-vector-effects.svg");
        File.WriteAllText(effects, """
            <svg width="64" height="48">
              <defs>
                <pattern id="p" patternUnits="userSpaceOnUse" width="8" height="8"><rect width="4" height="8" fill="red"/></pattern>
                <pattern id="q" patternUnits="userSpaceOnUse" width="16" height="8"><rect width="16" height="8" fill="url(#p)"/></pattern>
                <clipPath id="clip"><rect width="16" height="16"/></clipPath>
                <mask id="mask" mask-type="alpha"><rect x="32" width="16" height="24" fill="white" opacity=".5"/></mask>
              </defs>
              <rect y="24" width="64" height="24" fill="url(#q)"/>
              <rect width="32" height="24" fill="blue" clip-path="url(#clip)"/>
              <rect x="32" width="32" height="24" fill="lime" mask="url(#mask)"/>
            </svg>
            """);
        scene = store.Load(effects).GpuScene;
        var painted = Draw(128, 96, true);
        check(painted[At(24, 24) + 2] == 255 && painted[At(56, 24) + 2] == 0,
            "GPU vector clips intersect the source without a CPU mask");
        check(painted[At(88, 24) + 1] is >= 126 and <= 129 && painted[At(120, 24) + 1] == 0,
            "GPU alpha masks multiply the composed source coverage");
        check(painted[At(20, 80)] == 255 && painted[At(28, 80)] == 0 && painted[At(36, 80)] == 255,
            "Nested GPU pattern surfaces repeat authored geometry without a CPU raster tile");
        check(store.Statistics.Rasterizations == 0, "GPU masks and patterns never call the CPU rasterizer");
        var blurPath = Path.Combine(output, "gpu-vector-blur.svg");
        File.WriteAllText(blurPath, """
            <svg width="64" height="48"><defs><filter id="blur" filterUnits="userSpaceOnUse" x="0" y="0" width="64" height="48" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="2"/></filter></defs>
            <rect x="16" y="16" width="32" height="16" fill="red" filter="url(#blur)"/></svg>
            """);
        var blurredAsset = store.Load(blurPath);
        scene = blurredAsset.GpuScene;
        var blurred = Draw(128, 96, true);
        check(blurred[At(80, 64)] > 245 && blurred[At(44, 64)] is > 0 and < 120,
            "GPU Gaussian passes preserve the interior and create the authored blur halo");
        var filterUploads = renderer.VectorSceneDiagnostics.Uploads;
        Draw(256, 192, true);
        check(renderer.VectorSceneDiagnostics.Uploads == filterUploads && store.Statistics.Rasterizations == 0,
            "GPU blur zoom reuses both root and dependency scene buffers without CPU rasterization");
        blurred = Draw(128, 96, true);
        // This explicit reference call belongs only to the test, after the zero-raster assertion.
        var reference = blurredAsset.Rasterize(128, 96);
        var referenceId = new UiTextureId(2_300_000_002);
        var referencePixels = capture("gpu-blur-scalar-reference", ui =>
        {
            context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, referenceId, UiTextureFormat.Rgba8Unorm,
                reference.Width, reference.Height, reference.Pixels)
            {
                IsColorImage = true
            });
            ui.GetWindowDrawList().AddImage(referenceId, new(16, 16), new(144, 112), default, new(1, 1), new(255, 255, 255));
        });
        var maximumDifference = blurred.Zip(referencePixels).Max(pair => Math.Abs(pair.First - pair.Second));
        check(maximumDifference <= 2, "GPU blur matches the independent scalar image within RGBA8 roundoff : " + maximumDifference);
        var cropped = capture("gpu-vector-camera-crop", ui =>
        {
            context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderVectorScene, id, UiTextureFormat.Rgba8Unorm,
                64, 48, ReadOnlyMemory<byte>.Empty)
            {
                VectorScene = scene,
                VectorCamera = new(new(128, 96), new(32, 16)),
                IsColorImage = true
            });
            ui.GetWindowDrawList().AddImage(id, new(16, 16), new(80, 64), default, new(1, 1), new(255, 255, 255));
        });
        var cropDifference = 0;
        for (var y = 0; y < 48; y++)
            for (var x = 0; x < 64; x++)
                for (var channel = 0; channel < 4; channel++)
                    cropDifference = Math.Max(cropDifference, Math.Abs(cropped[At(16 + x, 16 + y) + channel] - blurred[At(48 + x, 32 + y) + channel]));
        check(cropDifference <= 2, "GPU camera window and blur halo match the same full-scene pixels : " + cropDifference);
        check(renderer.VectorSceneDiagnostics.Uploads == filterUploads,
            "GPU camera changes preserve the root and filter device buffers");
        capture("gpu-vector-destroy", _ => context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, id,
            UiTextureFormat.Rgba8Unorm, 0, 0, ReadOnlyMemory<byte>.Empty)));
        check(renderer.VectorSceneDiagnostics is { Scenes: 0, Outputs: 0 },
            "Destroying the last SVG output retires its shared GPU scene");
    }
}
