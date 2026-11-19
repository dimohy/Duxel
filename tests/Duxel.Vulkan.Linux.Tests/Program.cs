using Duxel.Core;

var glyphReplay = args is [_, _, _, _, _, _, _, "glyph-texture-replay", _, _, _];
if ((!glyphReplay && args.Length is not (7 or 8)) || !int.TryParse(args[1], out var slots) || slots is not (1 or 3)
    || args.Length == 8 && args[7] is not ("skeleton" or "text-baseline" or "paragraph-geometry" or "styled-paragraph"
        or "host-pixels" or "tree-gpu" or "tree-label-gpu" or "container-pixels" or "virtual-list-pixels" or "virtual-list-host-pixels"
        or "virtual-list-rtl-pixels" or "virtual-grid-pixels" or "virtual-grid-host-pixels" or "clip-host-pixels"
        or "projected-clip-bench" or "projected-image-clip-bench" or "early-init-cleanup" or "svg-replacement-pressure"
        or "radio-parent-pixels"))
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
    if (glyphReplay)
        GlyphTextureReplayTests.Run(args[0], slots, args[8], args[9], args[10]);
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
    else if (args.Length == 8 && args[7] == "paragraph-geometry")
        LinuxParagraphTests.Run(args[0]);
    else if (args.Length == 8 && args[7] == "text-baseline")
        TextBaselineTests.Run(args[0], slots);
    else if (args.Length == 8 && args[7] == "host-pixels")
        PresentationReuseTests.Run(args[0], slots, captureHostPixels: true);
    else if (args.Length == 8 && args[7] == "early-init-cleanup")
        VulkanEarlyInitTests.Run(args[0], slots);
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
