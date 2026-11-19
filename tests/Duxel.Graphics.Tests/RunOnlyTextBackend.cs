using Duxel.Core;
using Duxel.Platform.Windows;

// Explicitly select run-based painting without advertising paragraph or line geometry.
internal sealed class RunOnlyTextBackend : IPlatformTextBackend, IPlatformFontMetricsBackend
{
    public bool TryMeasure(in PlatformTextMeasureRequest request, out PlatformTextMeasureResult result)
        => WindowsPlatformTextBackend.Instance.TryMeasure(request, out result);
    public bool TryRasterize(in PlatformTextRasterizeRequest request, out PlatformTextRasterizeResult result)
        => WindowsPlatformTextBackend.Instance.TryRasterize(request, out result);
    public bool TryGetFontMetrics(string fontPath, float fontSize, out PlatformFontMetrics metrics)
        => WindowsPlatformTextBackend.Instance.TryGetFontMetrics(fontPath, fontSize, out metrics);
}
