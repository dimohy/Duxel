using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class SmallFontBenchmark
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var font = UiFluentFilledIconFont.Path;
        if (!File.Exists(font)) throw new FileNotFoundException("The pinned Fluent filled font is required.", font);
        var backend = WindowsPlatformTextBackend.Instance;
        using var writer = new StreamWriter(Path.Combine(output, "samples.csv"));
        writer.WriteLine("size,codepoint,sample,microseconds");
        using var cases = new StreamWriter(Path.Combine(output, "cases.csv"));
        cases.WriteLine("size,codepoint,width,height,advance,managedBytesPerCall,pixelSha256");
        foreach (var size in new[] { 6, 8, 10, 16 })
        foreach (var codepoint in new[] { 63014, 63017 })
        {
            var request = new PlatformTextRasterizeRequest(font, null, char.ConvertFromUtf32(codepoint), size);
            for (var index = 0; index < 32; index++)
            {
                if (!backend.TryRasterize(request, out var warmup)) throw new InvalidOperationException("Small-font warmup failed.");
                warmup.PixelOwner?.Dispose();
            }
            var samples = new double[256];
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var width = 0;
            var height = 0;
            var advance = 0f;
            var hash = string.Empty;
            for (var index = 0; index < samples.Length; index++)
            {
                var start = Stopwatch.GetTimestamp();
                if (!backend.TryRasterize(request, out var result)) throw new InvalidOperationException("Small-font rasterization failed.");
                samples[index] = Stopwatch.GetElapsedTime(start).TotalMicroseconds;
                try
                {
                    if (index == samples.Length - 1)
                    {
                        width = result.Width;
                        height = result.Height;
                        advance = result.Advance;
                        hash = Convert.ToHexString(SHA256.HashData(result.RgbaPixels.Span));
                    }
                }
                finally { result.PixelOwner?.Dispose(); }
            }
            var bytesPerCall = (double)(GC.GetAllocatedBytesForCurrentThread() - allocated) / samples.Length;
            for (var index = 0; index < samples.Length; index++)
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{size},{codepoint},{index},{samples[index]:R}"));
            cases.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{size},{codepoint},{width},{height},{advance:R},{bytesPerCall:R},{hash}"));
        }
        File.WriteAllLines(Path.Combine(output, "scope.txt"),
        [
            RuntimeInformation.FrameworkDescription,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(font))),
            "32 warmup/256 measured native raster calls per glyph/size; font face warm, no UI glyph-cache hits, no GPU.",
            "Managed bytes include the final hash; native allocations, private/working set and app FPS are not measured."
        ]);
    }
}
