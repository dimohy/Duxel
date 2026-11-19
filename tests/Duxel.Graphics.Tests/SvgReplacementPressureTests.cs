using System.Diagnostics;
using System.Globalization;
using Duxel.Core;

internal static class SvgReplacementPressureTests
{
    internal static void Run(string output, int slots)
    {
        const int small = 3000, large = 3200;
        Directory.CreateDirectory(output);
        using var log = File.CreateText(Path.Combine(output, "frames.tsv"));
        log.WriteLine("phase\tframe\tlogical_bytes\tactive\tactive_bytes\tpending\tpending_bytes\tui_ms\tsubmit_ms");
        using var fixture = new PresentationReuseTests.Fixture(slots, reuse: false);
        using var svg = new UiSvg(new Document()) { Size = new(small, small), UseFixedResolutionPreview = true };
        var logicalBytes = 0L;
        void Draw(UiImmediateContext ui)
        {
            ui.SetCursorScreenPos(new(16, 16));
            svg.Render(ui);
            logicalBytes = ui.SvgTextureStatistics.ResidentBytes;
        }
        void CheckPixel(byte[] raster)
        {
            const int x = 40, y = 40;
            var offset = checked((y * fixture.LastCaptureSize.Width + x) * 4);
            if (!raster.AsSpan(offset, 4).SequenceEqual(new byte[] { 220, 30, 40, 255 }))
                throw new InvalidOperationException("The actual SVG texture did not paint its authored color.");
        }
        CheckPixel(fixture.Capture(Draw));
        for (var frame = 0; frame < 32; frame++)
        {
            var extent = frame % 2 == 0 ? large : small;
            svg.Size = new(extent, extent);
            var start = Stopwatch.GetTimestamp();
            using var data = fixture.Build(Draw);
            var composed = Stopwatch.GetTimestamp();
            if (logicalBytes != (long)extent * extent * 4
                || data.TextureUpdates.Count != 2 || data.TextureUpdates[0].Kind != UiTextureUpdateKind.Destroy
                || data.TextureUpdates[1].Kind != UiTextureUpdateKind.RenderVectorScene
                || data.TextureUpdates[1].Width != extent || data.TextureUpdates[1].Height != extent)
                throw new InvalidOperationException("Each change must replace exactly one SVG texture at native resolution.");
            if (!fixture.RenderSubmit(data))
                throw new InvalidOperationException("Vulkan rejected the SVG replacement frame.");
            var submitted = Stopwatch.GetTimestamp();
            var textures = fixture.Renderer.ValidationSquareTextureAllocations(small, large);
            if (textures.Active != 1 || textures.Pending > slots || textures.ActiveBytes < (ulong)logicalBytes
                || textures.PendingBytes < (ulong)textures.Pending * small * small * 4)
                throw new InvalidOperationException($"SVG replacement exceeded its resource lifetime bound: {textures}.");
            log.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"replace\t{frame}\t{logicalBytes}\t{textures.Active}\t{textures.ActiveBytes}\t{textures.Pending}\t{textures.PendingBytes}\t{Stopwatch.GetElapsedTime(start, composed).TotalMilliseconds:F4}\t{Stopwatch.GetElapsedTime(composed, submitted).TotalMilliseconds:F4}"));
        }
        for (var frame = 0; frame < slots; frame++)
        {
            if (!fixture.Submit(Draw))
                throw new InvalidOperationException("Vulkan rejected a drain frame.");
            var textures = fixture.Renderer.ValidationSquareTextureAllocations(small, large);
            log.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"drain\t{frame}\t{logicalBytes}\t{textures.Active}\t{textures.ActiveBytes}\t{textures.Pending}\t{textures.PendingBytes}\t\t"));
        }
        var drained = fixture.Renderer.ValidationSquareTextureAllocations(small, large);
        if (drained is not { Active: 1, Pending: 0 })
            throw new InvalidOperationException("Previous SVG allocations remain after all frame slots were consumed.");
        CheckPixel(fixture.Capture(Draw));
        Console.WriteLine($"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}: 32 native SVG replacements and {slots} drain frames passed.");
    }

    private sealed class Document : IUiGpuSvgDocument
    {
        public UiGpuVectorScene GpuScene { get; } = GpuVectorTestScenes.Solid(new(1, 1), 220f / 255, 30f / 255, 40f / 255);
        public UiVector2 IntrinsicSize => new(1, 1);
        public long Revision => 0;
        public event Action? Changed { add { } remove { } }
        public UiSvgRaster Rasterize(int width, int height) => throw new InvalidOperationException("The GPU fixture cannot rasterize on the CPU.");
    }
}
