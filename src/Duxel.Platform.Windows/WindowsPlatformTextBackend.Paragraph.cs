using Duxel.Core;

namespace Duxel.Platform.Windows;

public sealed partial class WindowsPlatformTextBackend : IPlatformTextParagraphBatchBackend
{
    IDisposable IPlatformTextParagraphBatchBackend.BeginParagraphBatch()
        => new WindowsDirectWriteGlyphRasterizer.ParagraphBatch();

    public PlatformTextParagraphLayout CreateParagraph(PlatformTextParagraphRequest request, bool vectorMetrics)
        => WindowsDirectWriteGlyphRasterizer.CreateStyledParagraph(request, vectorMetrics);

    public PlatformTextParagraphRaster RasterizeParagraphRun(PlatformTextParagraphRun run)
        => WindowsDirectWriteGlyphRasterizer.RasterizeStyledParagraphRun(run);

    public PlatformTextMeshResult CreateParagraphRunMesh(PlatformTextParagraphRun run, float flatteningTolerance = .001f)
        => WindowsDirectWriteGlyphRasterizer.CreateStyledParagraphRunMesh(run, flatteningTolerance);
}
