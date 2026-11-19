namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private readonly record struct VectorTextCacheKey(DirectTextCacheKey Text, float Tolerance, float IconAscent);
    private readonly record struct VectorTextCacheEntry(PlatformTextMeshResult Mesh, float InkTop, float InkHeight,
        UiVectorGlyphSource? Icon = null)
    {
        internal float Baseline => Mesh.Baseline;
    }
    private readonly Dictionary<VectorTextCacheKey, VectorTextCacheEntry> _vectorTextMeshes = [];
    private readonly Queue<VectorTextCacheKey> _vectorTextMeshOrder = [];
    private bool _fontIconVectorPaint;
    private UiRasterTextureCache<UiGpuVectorScene, UiVectorSceneTextureProvider>? _fontIconTextureCache;
    internal UiRasterTextureStatistics FontIconTextureStatistics => _fontIconTextureCache?.Statistics ?? default;
    private VectorTextCacheKey CreateVectorTextCacheKey(string text, float fontSize)
        => new(CreateDirectTextCacheKey(text, fontSize), _fontIconVectorPaint ? .001f : .001f / WidgetRasterScale,
            _fontIconVectorPaint ? _controlTypographyMetrics.Ascent : 0);
    private UiVectorGlyphSource PrepareIconCoverage(in PlatformTextMeshResult mesh)
        => UiVectorGlyphSource.Create(mesh, 1, new(0, _controlTypographyMetrics.Ascent - mesh.Baseline));

    private bool IsVectorTextLayer()
    {
        if (_fontIconVectorPaint || _widgetTransformDepth > _widgetScreenTransformDepth || _widgetVectorMeasurementDepth > 0)
            return true;
        return HasLayerCameraAboveScreenPortal();
    }

    private bool HasLayerCameraAboveScreenPortal() => _cameraLayerDepth > _screenPortalLayerDepth;

    private VectorTextCacheEntry GetVectorTextMesh(string text, int sizeQuarter, float fontSize)
    {
        var key = CreateVectorTextCacheKey(text, fontSize);
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
        var mesh = backend.CreateTextMesh(new(_directTextPrimaryFontPath!, _directTextSecondaryFontPath, text, fontSize, tolerance));
        if (mesh.TriangleVertices.Length % 3 != 0 || (!mesh.Glyphs.IsEmpty && !mesh.TriangleVertices.IsEmpty))
            throw new InvalidOperationException("Vector text must contain complete triangles.");
        if (_vectorTextMeshes.Count == DirectTextCacheMaxEntries)
            _vectorTextMeshes.Remove(_vectorTextMeshOrder.Dequeue());
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
        var entry = new VectorTextCacheEntry(mesh, float.IsFinite(top) ? top : 0, float.IsFinite(top) ? bottom - top : 0,
            _fontIconVectorPaint ? PrepareIconCoverage(mesh) : null);
        _vectorTextMeshes.Add(key, entry);
        _vectorTextMeshOrder.Enqueue(key);
        return entry;
    }

    private (float Top, float Height, float Baseline) GetVectorTextInk(string text, float fontSize)
    {
        GetVectorTextMesh(text, (int)MathF.Round(fontSize * 4), fontSize);
        var entry = _vectorTextMeshes[CreateVectorTextCacheKey(text, fontSize)];
        return (entry.InkTop, entry.InkHeight, entry.Mesh.Baseline);
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
        => RenderVectorText(draw, new VectorTextCacheEntry(mesh, 0, 0), position, color, clip);
}
