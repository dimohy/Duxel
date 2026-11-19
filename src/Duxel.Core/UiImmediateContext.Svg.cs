namespace Duxel.Core;

/// <summary>Queued upload counts, not completed GPU work or total native memory.</summary>
public readonly record struct UiSvgTextureStatistics(long Uploads, long UploadedBytes, int ResidentTextures, long ResidentBytes, long GpuRenderRequests = 0);

public readonly partial struct UiControlContext
{
    internal void PaintSvg(UiSvg svg, UiRect bounds, UiColor? tint) => _ui.PaintSvg(svg, bounds, tint);
}

public sealed partial class UiImmediateContext
{
    private UiRasterTextureCache<IUiSvgDocument, UiSvgTextureProvider>? _svgTextureCache;
    public UiSvgTextureStatistics SvgTextureStatistics
    {
        get
        {
            var value = _svgTextureCache?.Statistics ?? default;
            return new(value.Uploads, value.UploadedBytes, value.ResidentTextures, value.ResidentBytes, value.GpuRenderRequests);
        }
    }
    internal UiTextureId AllocateOwnedTextureId()
    {
        var id = _nextOwnedTextureId;
        _nextOwnedTextureId = checked(id + 1);
        return new(id);
    }
    internal void ReleaseOwnedImageTextures()
    {
        ReleaseOwnedPaintGroups();
        _svgTextureCache?.Dispose();
        _fontIconTextureCache?.Dispose();
        _imageTextureCache?.Dispose();
        _shadowTextureCache?.Dispose();
        _popupSurfaceTextureCache?.Dispose();
    }
    internal void RenderSvg(UiSvg svg)
    {
        EnsureNotMeasuring();
        var size = ConstrainToLayout(svg.NaturalSize());
        var cursor = AdvanceCursor(size);
        var bounds = new UiRect(cursor.X, cursor.Y, size.X, size.Y);
        PaintSvg(svg, bounds);
        if (svg.AccessibleName is { Length : > 0 } name)
            ExposeAccessibility(ResolveId(svg.AccessibilityInputId(name)), _accessibilityParent,
                UiAccessibilityRole.Image, name, bounds, readOnly : true, focusable : false, valuePattern : false,
                literalLabel : true, imageInfo : new(bounds, name));
    }
    internal void PaintSvg(UiSvg svg, UiRect bounds, UiColor? tint = null)
    {
        EnsureNotMeasuring();
        svg.ObservePaint(this);
        var contentBounds = bounds;
        var clip = IntersectRect(CurrentClipRect, contentBounds);
        if (clip.Width <= 0 || clip.Height <= 0)
            return;
        if (!svg.UseFixedResolutionPreview && _widgetClipGeometry is { } widgetClip
            && (_widgetToScreen.M12 != 0 || _widgetToScreen.M21 != 0))
        {
            if (!UiWidgetPointGeometry.ClipPolygon(clip, System.Numerics.Matrix3x2.Identity, _widgetToScreen,
                    widgetClip, out clip, out _))
                return;
        }
        var baseResolution = ContentScale * svg.RasterScale;
        var viewport = svg.UseFixedResolutionPreview
            ? UiRasterTextureViewport.Create(contentBounds, contentBounds, baseResolution)
            : UiRasterTextureViewport.Create(contentBounds, clip, ContentScale * WidgetRasterScale * svg.RasterScale);
        var cache = _svgTextureCache ??= new(this);
        var texture = svg.Acquire(cache, viewport.Width, viewport.Height, viewport.Camera);
        if (svg.CornerRadius > 0 && viewport.Camera is null)
        {
            _builder.AddImageRounded(texture, contentBounds, UiImageUvTransform.Identity, tint ?? svg.Tint, svg.CornerRadius,
                CurrentClipRect, svg.NaturalSize());
            return;
        }
        _builder.AddImageCamera(texture, contentBounds, viewport.UvMinimum,
            viewport.UvMaximum, tint ?? svg.Tint,
            svg.CornerRadius, svg.NaturalSize(), CurrentClipRect);
    }
    internal int TextureFrame => _state.FrameCount;
    internal bool IsTextureRetained(UiTextureId texture)
    {
        foreach (var binding in _paintGroupBindings)
            if (binding.Content?.TextureDependencies.Contains(texture) == true)
                return true;
        if (_layerCacheStore is null)
            return false;
        foreach (var layer in _layerCacheStore.Layers.Values)
        {
            if (layer.Lists is null)
                continue;
            foreach (var list in layer.Lists)
                foreach (var command in list.LocalCommands)
                    if (command.TextureId == texture || command.WhiteBackdropTextureId == texture)
                        return true;
        }
        return false;
    }
}

