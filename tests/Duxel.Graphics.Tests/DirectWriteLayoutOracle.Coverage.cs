using System.Runtime.InteropServices;

internal static unsafe partial class DirectWriteLayoutOracle
{
    internal const int CoverageWidth = 640, CoverageHeight = 128, CoverageLeft = 20, CoverageBaseline = 32;
    [StructLayout(LayoutKind.Sequential)]
    private struct PixelBounds
    {
        internal int Left, Top, Right, Bottom;
    }

    private static void CaptureCoverage(Capture capture, float x, float yOrigin, GlyphRun* run)
    {
        if (run->Count == 0)
            return;
        nint analysis = 0;
        try
        {
            // Analyze the original native run, direction and fractional origin directly, without product glyph extraction.
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, GlyphRun*, nint, int, int, int, int, float, float, nint*, int>)Method(capture.AnalysisFactory, 30))
                (capture.AnalysisFactory, run, 0, run->Size >= 16 ? 5 : 4, 0, run->Size >= 16 ? 1 : 2, 0, x, yOrigin, &analysis));
            PixelBounds bounds = default;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, PixelBounds*, int>)Method(analysis, 3))(analysis, 1, &bounds));
            var width = bounds.Right - bounds.Left;
            var height = bounds.Bottom - bounds.Top;
            if (width == 0 || height == 0)
                return;
            var samples = new byte[checked(width * height * 3)];
            fixed (byte* pixels = samples)
                Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, PixelBounds*, byte*, uint, int>)Method(analysis, 4))
                    (analysis, 1, &bounds, pixels, (uint)samples.Length));
            if (capture.Rasters is { } rasters)
            {
                var rgba = new byte[checked(width * height * 4)];
                for (var pixel = 0; pixel < width * height; pixel++)
                {
                    var red = samples[pixel * 3];
                    var green = samples[pixel * 3 + 1];
                    var blue = samples[pixel * 3 + 2];
                    rgba[pixel * 4] = red;
                    rgba[pixel * 4 + 1] = green;
                    rgba[pixel * 4 + 2] = blue;
                    rgba[pixel * 4 + 3] = (byte)((red + green + blue) / 3);
                }
                rasters.Add(new(bounds.Left, bounds.Top, width, height, yOrigin, rgba));
            }
            if (capture.Coverage is null)
                return;
            var target = capture.Coverage!;
            for (var y = 0; y < height; y++)
                for (var column = 0; column < width; column++)
                {
                    var targetX = CoverageLeft + bounds.Left + column;
                    var targetY = CoverageBaseline + bounds.Top + y;
                    if ((uint)targetX >= CoverageWidth || (uint)targetY >= CoverageHeight)
                        throw new InvalidOperationException("The independent paragraph coverage canvas is too small.");
                    for (var channel = 0; channel < 3; channel++)
                    {
                        var index = (targetY * CoverageWidth + targetX) * 3 + channel;
                        var source = samples[(y * width + column) * 3 + channel];
                        target[index] = (byte)(source + (target[index] * (255 - source) + 127) / 255);
                    }
                }
        }
        finally
        {
            if (analysis != 0)
                Marshal.Release(analysis);
        }
    }
}
