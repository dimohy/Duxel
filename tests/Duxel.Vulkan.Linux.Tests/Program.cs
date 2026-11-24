using Duxel.Core;
using Duxel.Platform.Linux;

var glyphReplay = args is [_, _, _, _, _, _, _, "glyph-texture-replay", _, _, _];
var outlineProbe = args is [_, _, _, _, _, _, _, "glyph-outline-projected-gpu", _];
if ((!glyphReplay && !outlineProbe && args.Length is not (7 or 8)) || !int.TryParse(args[1], out var slots) || slots is not (1 or 3)
    || args.Length == 8 && args[7] is not ("skeleton" or "text-baseline" or "paragraph-geometry" or "styled-paragraph" or "styled-paragraph-raster" or "inline-paragraph-cache"
        or "host-pixels" or "tree-gpu" or "tree-label-gpu" or "container-pixels" or "virtual-list-pixels" or "virtual-list-host-pixels"
        or "virtual-list-rtl-pixels" or "virtual-grid-pixels" or "virtual-grid-host-pixels" or "clip-host-pixels"
        or "projected-clip-bench" or "projected-image-clip-bench" or "early-init-cleanup" or "svg-replacement-pressure"
        or "radio-parent-pixels" or "viewport-clipping" or "glyph-camera-gpu" or "glyph-residency-gpu" or "text-image-memory"))
    throw new ArgumentException("Specify a fresh output directory, frame slots1/3, Latin regular/bold, Korean regular/bold and monospace font paths.");
if (Directory.Exists(args[0]))
    throw new IOException("Linux GPU validation requires a fresh output directory.");
PresentationTestPlatform.SetFonts(args[2..7]);
// Match the Windows GPU contract host: draw-count assertions require profiling.
// This is a correctness fixture, separate from unprofiled performance runs.
Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE", "1");
Environment.SetEnvironmentVariable("DUXEL_VK_PROFILE_EVERY", "100000");
try
{
    if (args.Length == 8 && args[7] == "text-image-memory")
        TextImageMemoryTests.Run(args[0]);
    else if (glyphReplay)
        GlyphTextureReplayTests.Run(args[0], slots, args[8], args[9], args[10]);
    else if (outlineProbe)
    {
        using var backend = new LinuxPlatformTextBackend();
        ProjectedGlyphMaterialTests.Run(args[0], (passed, message) =>
        {
            if (!passed)
                throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }, backend, new(PresentationTestPlatform.Fonts[2], null, "ㅇㅇ", 32), slots, Path.GetFullPath(args[8]));
    }
    else if (args.Length == 8 && args[7] == "glyph-camera-gpu")
    {
        using var fixture = new PresentationReuseTests.Fixture(slots, false);
        using var backend = new LinuxPlatformTextBackend();
        var fonts = PresentationTestPlatform.Fonts;
        GlyphCameraTests.Run(args[0], fixture, backend,
            new(fonts[0], fonts[2], "한글 Hangul 09 한글 · ₩", 32, .25f), (passed, message) =>
            {
                if (!passed)
                    throw new InvalidOperationException(message);
                Console.WriteLine("PASS " + message);
            });
    }
    else if (args.Length == 8 && args[7] == "glyph-residency-gpu")
    {
        using var backend = new LinuxPlatformTextBackend();
        ProjectedGlyphResidencyTests.Run(args[0], backend, PresentationTestPlatform.Fonts[0], (passed, message) =>
        {
            if (!passed)
                throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        });
    }
    else if (args.Length == 8 && args[7] == "svg-replacement-pressure")
        SvgReplacementPressureTests.Run(args[0], slots);
    else if (args.Length == 8 && args[7] == "radio-parent-pixels")
        RadioAllocationRenderingTests.RunMode(args[0], slots);
    else if (args.Length == 8 && args[7] is "projected-clip-bench" or "projected-image-clip-bench")
    {
        Environment.SetEnvironmentVariable("DUXEL_VK_GPU_PROFILE", "1");
        Directory.CreateDirectory(args[0]);
        using var fixture = new PresentationReuseTests.Fixture(slots, false);
        var size = fixture.FrameInfo.DisplaySize;
        ImageShaderBenchmark.Run(args[0], fixture.Renderer, fixture.Build, new UiTextureId(2), new(0, 0, size.X, size.Y),
            colorImages : args[7] == "projected-image-clip-bench", projectedClip : true);
    }
    else if (args.Length == 8 && args[7] == "styled-paragraph")
        LinuxStyledParagraphTests.Run(args[0]);
    else if (args.Length == 8 && args[7] == "styled-paragraph-raster")
        LinuxStyledParagraphTests.Run(args[0], checkWholeRaster: true);
    else if (args.Length == 8 && args[7] == "inline-paragraph-cache")
        LinuxInlineParagraphCacheTests.Run(args[0], slots);
    else if (args.Length == 8 && args[7] == "paragraph-geometry")
        LinuxParagraphTests.Run(args[0]);
    else if (args.Length == 8 && args[7] == "text-baseline")
        TextBaselineTests.Run(args[0], slots);
    else if (args.Length == 8 && args[7] == "host-pixels")
        PresentationReuseTests.Run(args[0], slots, captureHostPixels: true);
    else if (args.Length == 8 && args[7] == "early-init-cleanup")
        VulkanEarlyInitTests.Run(args[0], slots);
    else if (args.Length == 8 && args[7] == "viewport-clipping")
    {
        Directory.CreateDirectory(args[0]);
        using var fixture = new PresentationReuseTests.Fixture(slots, false, PresentationTestPlatform.Create(320, 240));
        ViewportClippingTests.Run(fixture.Renderer, (_, draw) => fixture.Capture(draw),
            (passed, message) =>
            {
                if (!passed)
                    throw new InvalidOperationException(message);
                Console.WriteLine("PASS " + message);
            }, new(2), 320, 240);
    }
    else if (args.Length == 8 && args[7] is "tree-gpu" or "tree-label-gpu")
        TreeRenderingTests.RunMode(args[0], args[7], slots);
    else if (args.Length == 8 && args[7] is "container-pixels" or "virtual-list-pixels" or "virtual-list-host-pixels" or "virtual-list-rtl-pixels"
        or "virtual-grid-pixels" or "virtual-grid-host-pixels" or "clip-host-pixels")
        ContainerPixelTests.Run(args[0], slots, args[7]);
    else if (args.Length == 8)
        SkeletonRenderingTests.Run(args[0], slots);
    else
        PresentationReuseTests.Run(args[0], slots);
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
