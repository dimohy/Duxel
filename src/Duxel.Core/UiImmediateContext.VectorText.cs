namespace Duxel.Core;

internal sealed record UiProjectedGlyphDraw(UiGpuVectorScene Scene, UiVector2 Origin, float Scale, UiColor Color);

public sealed partial class UiImmediateContext
{
    internal bool UseProjectedGlyphText { get; set; }
    private enum ProjectedTextDetail { Detailed, Medium, Coarse }
    private readonly record struct VectorTextCacheKey(DirectTextCacheKey Text, float Tolerance, float IconAscent, bool OutlineOnly, bool UseProjectedLineMetrics);
    private readonly record struct VectorTextCacheEntry(PlatformTextMeshResult Mesh, (float Top, float Height)? Ink = null,
        UiVectorGlyphSource? Icon = null, UiVectorGlyphSource? Outline = null)
    {
        internal float Baseline => Mesh.Baseline;
    }
    private readonly Dictionary<VectorTextCacheKey, VectorTextCacheEntry> _vectorTextMeshes = [];
    private readonly Queue<VectorTextCacheKey> _vectorTextMeshOrder = [];
    private bool _fontIconVectorPaint;
    private IDisposable? _vectorTextParagraphBatch;
    private UiRasterTextureCache<UiGpuVectorScene, UiVectorSceneTextureProvider>? _fontIconTextureCache;
    internal UiRasterTextureStatistics FontIconTextureStatistics => _fontIconTextureCache?.Statistics ?? default;
    private VectorTextCacheKey CreateVectorTextCacheKey(string text, float fontSize, ProjectedTextDetail detail = default, bool outlineOnly = false)
        => new(CreateDirectTextCacheKey(text, fontSize), _fontIconVectorPaint || outlineOnly ? .001f : ProjectedTextTolerance(detail) / WidgetRasterScale,
            _fontIconVectorPaint ? _controlTypographyMetrics.Ascent : 0, outlineOnly,
            !_fontIconVectorPaint && HasLayerCameraAboveScreenPortal());

    private static float ProjectedTextTolerance(ProjectedTextDetail detail) => detail switch
    {
        ProjectedTextDetail.Coarse => .25f,
        ProjectedTextDetail.Medium => .0625f,
        _ => .001f
    };

    private bool UsesProjectedGlyphScope(in UiLayerOptions options)
        => UseProjectedGlyphText && options.Opacity >= 1f
            && options.Scene3D is { WriteDepth: true, IsScreenSpacePresentation: false, WidgetTransform: null };

    private bool CanUseProjectedGlyphs(UiDrawListBuilder draw)
        => !_fontIconVectorPaint && _widgetTransformDepth == 0 && _capturingPaintGroup is null
            && _layerFrames.Count == 1 && ReferenceEquals(draw, _builder) && draw.CanAddProjectedGlyphs
            && _layerFrames.TryPeek(out var frame) && frame.Entry.ProjectedGlyphScope;

    private bool TryRenderProjectedGlyphs(UiDrawListBuilder draw, string text, int sizeQuarter, float fontSize,
        UiVector2 position, UiColor color, UiRect clip, UiTextSettings settings, out UiVector2 size)
    {
        size = default;
        if (color.Rgba >> 24 != 255 || !CanUseProjectedGlyphs(draw))
            return false;
        var entry = GetVectorTextMesh(text, sizeQuarter, fontSize, outlineOnly: true);
        size = MeasureVectorText(entry, settings);
        if (entry.Outline is not { Scene: { } scene } outline)
            return true;
        var scale = 1f / _contentScale;
        var bounds = outline.Bounds;
        var origin = new UiVector2(position.X + bounds.X * scale, position.Y + bounds.Y * scale);
        draw.AddProjectedGlyphs(new(scene, origin, scale, color),
            new(origin.X, origin.Y, bounds.Width * scale, bounds.Height * scale), clip);
        return true;
    }

    private ProjectedTextDetail SelectProjectedTextDetail(in UiLayerOptions options, ProjectedTextDetail previous)
    {
        if (_layerFrames.Count != 0 || _widgetTransformDepth != 0 || _capturingPaintGroup is not null
            || options.Scene3D is not { IsScreenSpacePresentation: false, WidgetTransform: null } scene)
            return ProjectedTextDetail.Detailed;

        var transform = scene.Transform;
        if (transform.M12 != 0f || transform.M21 != 0f || transform.M13 != 0f || transform.M23 != 0f
            || transform.M14 != 0f || transform.M24 != 0f || transform.M44 != 1f)
            return ProjectedTextDetail.Detailed;

        // Each reduced detail stays within a 1/16-pixel projection of its curve tolerance.
        // Separate narrowing and widening limits avoid rebuilding on a small zoom reversal.
        var scale = MathF.Max(MathF.Abs(transform.M11) * _displayFramebufferScale.X,
            MathF.Abs(transform.M22) * _displayFramebufferScale.Y) / _contentScale;
        if (scale > 0f && scale <= (previous == ProjectedTextDetail.Coarse ? .25f : .20f))
            return ProjectedTextDetail.Coarse;
        if (scale > 0f && scale <= (previous == ProjectedTextDetail.Detailed ? .80f : 1f))
            return ProjectedTextDetail.Medium;
        return ProjectedTextDetail.Detailed;
    }

    private ProjectedTextDetail TrackProjectedText(UiDrawListBuilder draw)
    {
        if (_fontIconVectorPaint || _widgetTransformDepth != 0 || _capturingPaintGroup is not null
            || _layerFrames.Count != 1 || !ReferenceEquals(draw, _builder)
            || !_layerFrames.TryPeek(out var frame) || frame.Options.Scene3D is null)
            return ProjectedTextDetail.Detailed;

        frame.Entry.UsesProjectedText = true;
        return frame.Entry.ProjectedTextDetail;
    }
    private UiVectorGlyphSource PrepareIconCoverage(in PlatformTextMeshResult mesh)
        => UiVectorGlyphSource.Create(mesh, 1, new(0, _controlTypographyMetrics.Ascent - mesh.Baseline));

    private bool IsVectorTextLayer()
    {
        if (_fontIconVectorPaint || _widgetTransformDepth > _widgetScreenTransformDepth || _widgetVectorMeasurementDepth > 0)
            return true;
        return HasLayerCameraAboveScreenPortal();
    }

    private bool HasLayerCameraAboveScreenPortal() => _cameraLayerDepth > _screenPortalLayerDepth;

    private VectorTextCacheEntry GetVectorTextMesh(string text, int sizeQuarter, float fontSize, ProjectedTextDetail detail = default, bool outlineOnly = false)
    {
        var key = CreateVectorTextCacheKey(text, fontSize, detail, outlineOnly);
        var tolerance = key.Tolerance;
        if (_vectorTextMeshes.TryGetValue(key, out var cached))
        {
            if (_fontIconVectorPaint && cached.Icon is null)
                _vectorTextMeshes[key] = cached = cached with
                {
                    Icon = PrepareIconCoverage(cached.Mesh)
                };
            return cached;
        }
        if (_platformTextBackend is not IPlatformVectorTextBackend backend)
            throw new NotSupportedException("The selected text backend does not provide vector glyphs required by this drawing scope.");
        // Start only on a cache miss. Retained scenes can render without a font batch.
        _vectorTextParagraphBatch ??= (_platformTextBackend as IPlatformTextParagraphBatchBackend)?.BeginParagraphBatch();
        var mesh = backend.CreateTextMesh(new(_directTextPrimaryFontPath!, _directTextSecondaryFontPath, text, fontSize, tolerance)
        {
            OutlineOnly = outlineOnly,
            UseProjectedLineMetrics = key.UseProjectedLineMetrics,
            RightToLeft = key.Text.RightToLeft
        });
        if (mesh.TriangleVertices.Length % 3 != 0 || (!mesh.Glyphs.IsEmpty && !mesh.TriangleVertices.IsEmpty))
            throw new InvalidOperationException("Vector text must contain complete triangles.");
        if (_vectorTextMeshes.Count == DirectTextCacheMaxEntries)
            _vectorTextMeshes.Remove(_vectorTextMeshOrder.Dequeue());
        var entry = new VectorTextCacheEntry(mesh,
            Icon: _fontIconVectorPaint ? PrepareIconCoverage(mesh) : null,
            Outline: outlineOnly ? UiVectorGlyphSource.Create(mesh, 1) : null);
        _vectorTextMeshes.Add(key, entry);
        _vectorTextMeshOrder.Enqueue(key);
        return entry;
    }

    internal void EndVectorTextParagraphBatch()
    {
        var batch = _vectorTextParagraphBatch;
        _vectorTextParagraphBatch = null;
        batch?.Dispose();
    }

    private (float Top, float Height, float Baseline) GetVectorTextInk(string text, float fontSize)
    {
        var entry = GetVectorTextMesh(text, (int)MathF.Round(fontSize * 4), fontSize);
        if (entry.Ink is not { } ink)
        {
            ink = MeasureVectorTextInk(entry.Mesh);
            _vectorTextMeshes[CreateVectorTextCacheKey(text, fontSize)] = entry with { Ink = ink };
        }
        return (ink.Top, ink.Height, entry.Mesh.Baseline);
    }

    private static (float Top, float Height) MeasureVectorTextInk(in PlatformTextMeshResult mesh)
    {
        var top = float.PositiveInfinity;
        var bottom = float.NegativeInfinity;
        foreach (var point in mesh.TriangleVertices.Span)
        {
            top = MathF.Min(top, point.Y);
            bottom = MathF.Max(bottom, point.Y);
        }
        foreach (var glyph in mesh.Glyphs.Span)
            foreach (var point in glyph.Vertices.Span)
            {
                top = MathF.Min(top, point.Y + glyph.Offset.Y);
                bottom = MathF.Max(bottom, point.Y + glyph.Offset.Y);
            }
        return float.IsFinite(top) ? (top, bottom - top) : (0, 0);
    }

    private UiVector2 MeasureVectorText(in VectorTextCacheEntry entry, UiTextSettings settings)
        => new(entry.Mesh.Width / _contentScale, MathF.Max(entry.Mesh.Height / _contentScale, _lineHeight * settings.LineHeightScale));

    private void RenderVectorText(UiDrawListBuilder draw, in VectorTextCacheEntry entry,
        UiVector2 position, UiColor color, UiRect clip)
    {
        var mesh = entry.Mesh;
        if (_fontIconVectorPaint)
        {
            if (!IsPresentationPass)
                return;
            var icon = entry.Icon ?? throw new InvalidOperationException("Font icon paint requires prepared vector coverage.");
            if (icon.Scene is not { } scene)
                return;
            var bounds = icon.Bounds;
            var start = new UiVector2(position.X + bounds.X / _contentScale, position.Y + bounds.Y / _contentScale);
            var target = new UiRect(start.X, start.Y, bounds.Width / _contentScale, bounds.Height / _contentScale);
            var visible = IntersectRect(target, clip);
            if (visible.Width <= 0 || visible.Height <= 0)
                return;
            var viewport = UiRasterTextureViewport.Create(target, visible, _contentScale * WidgetRasterScale);
            var cache = _fontIconTextureCache ??= new(this, DirectTextCacheMaxEntries);
            var texture = cache.Use(scene, viewport.Width, viewport.Height, viewport.Camera);
            draw.AddTextImage(texture, start, new(start.X + bounds.Width / _contentScale, start.Y + bounds.Height / _contentScale),
                viewport.UvMinimum, viewport.UvMaximum, color, clip);
            return;
        }
        var scale = 1f / _contentScale;
        if (!mesh.Glyphs.IsEmpty)
        {
            foreach (var glyph in mesh.Glyphs.Span)
                draw.AppendVectorGlyph(glyph.Vertices.Span, glyph.Indices.Span,
                    glyph.Offset, position, scale, color, _whiteTexture, clip);
            return;
        }
        var points = mesh.TriangleVertices.Span;
        for (var i = 0; i < points.Length; i += 3)
        {
            var a = new UiVector2(position.X + points[i].X * scale, position.Y + points[i].Y * scale);
            var b = new UiVector2(position.X + points[i + 1].X * scale, position.Y + points[i + 1].Y * scale);
            var c = new UiVector2(position.X + points[i + 2].X * scale, position.Y + points[i + 2].Y * scale);
            draw.AddTriangleFilled(a, b, c, color, _whiteTexture, clip);
        }
    }
    private void RenderVectorText(UiDrawListBuilder draw, in PlatformTextMeshResult mesh,
        UiVector2 position, UiColor color, UiRect clip)
        => RenderVectorText(draw, new VectorTextCacheEntry(mesh), position, color, clip);
}
