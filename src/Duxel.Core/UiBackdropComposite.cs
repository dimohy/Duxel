namespace Duxel.Core;

public sealed partial class UiDrawListBuilder
{
    private UiTextureId? _backdropWhiteTexture;
    private float _backdropOpacity = 1;

    /// <summary>
    /// Composites two equal-sized renders of the same content over transparent black and opaque white.
    /// The producer owns both textures. These are affine render results, not ordinary straight-alpha images.
    /// </summary>
    public void AddBackdropComposite(UiTextureId blackTexture, UiTextureId whiteTexture, UiRect bounds,
        float opacity = 1)
        => AddBackdropCompositeCore(blackTexture, whiteTexture, bounds, opacity, null);

    /// <summary>Multiplies both premultiplied source and independent channel coverage by the same edge mask.</summary>
    public void AddBackdropComposite(UiTextureId blackTexture, UiTextureId whiteTexture, UiRect bounds,
        UiHorizontalOpacityMask mask, float opacity = 1)
        => AddBackdropCompositeCore(blackTexture, whiteTexture, bounds, opacity, mask);

    public void AddBackdropComposite(UiTextureId blackTexture, UiTextureId whiteTexture, UiRect bounds,
        UiImageUvTransform uv, UiHorizontalOpacityMask mask, float opacity = 1)
        => AddBackdropCompositeCore(blackTexture, whiteTexture, bounds, opacity, mask, uv);

    private void AddBackdropCompositeCore(UiTextureId blackTexture, UiTextureId whiteTexture, UiRect bounds,
        float opacity, UiHorizontalOpacityMask? mask, UiImageUvTransform? uv = null)
    {
        ValidateBackdropBounds(bounds, opacity);
        mask?.Validate();
        uv?.Validate();
        if (!IsPresentationPass)
            return;
        if (bounds.Width == 0 || bounds.Height == 0 || opacity == 0)
            return;
        var previousScene = _scene3D;
        var readMaterial = previousScene is { WriteDepth: true };
        if (readMaterial)
        {
            Flush();
            _scene3D = previousScene!.Value with
            {
                WriteDepth = false
            };
        }
        _backdropWhiteTexture = whiteTexture;
        _backdropOpacity = opacity;
        try
        {
            if (mask is { IsIdentity: false } edges)
            {
                var source = uv ?? UiImageUvTransform.Identity;
                AddMaskedBackdropSlice(blackTexture, bounds, source, 0, edges.LeftFadeFraction, 0, 255);
                AddMaskedBackdropSlice(blackTexture, bounds, source, edges.LeftFadeFraction, 1 - edges.RightFadeFraction, 255, 255);
                AddMaskedBackdropSlice(blackTexture, bounds, source, 1 - edges.RightFadeFraction, 1, 255, 0);
            }
            else if (uv is { } source && source != UiImageUvTransform.Identity)
                AddMaskedBackdropSlice(blackTexture, bounds, source, 0, 1, 255, 255);
            else
                AddImage(blackTexture, new(bounds.X, bounds.Y), new(bounds.X + bounds.Width, bounds.Y + bounds.Height),
                    new(0, 0), new(1, 1), new(255, 255, 255));
            if (readMaterial)
                Flush();
        }
        finally
        {
            _backdropWhiteTexture = null;
            _backdropOpacity = 1;
            _scene3D = previousScene;
        }
    }
    private void AddMaskedBackdropSlice(UiTextureId texture, UiRect bounds, UiImageUvTransform uv,
        float start, float end, byte leftAlpha, byte rightAlpha)
    {
        if (end <= start) return;
        var left = bounds.X + bounds.Width * start;
        var right = bounds.X + bounds.Width * end;
        var bottom = bounds.Y + bounds.Height;
        var first = new UiColor(255, 255, 255, leftAlpha);
        var last = new UiColor(255, 255, 255, rightAlpha);
        UiVector2 At(float u, float v) => new(uv.Origin.X + uv.AxisX.X * u + uv.AxisY.X * v,
            uv.Origin.Y + uv.AxisX.Y * u + uv.AxisY.Y * v);
        AddImageQuad(texture, new(left, bounds.Y), new(right, bounds.Y), new(right, bottom), new(left, bottom),
            At(start, 0), At(end, 0), At(end, 1), At(start, 1),
            first, last, last, first, _currentClipRect);
    }
    internal static void ValidateBackdropBounds(UiRect bounds, float opacity)
    {
        if (!float.IsFinite(bounds.X) || !float.IsFinite(bounds.Y) || !float.IsFinite(bounds.Width)
            || !float.IsFinite(bounds.Height) || bounds.Width < 0 || bounds.Height < 0
            || !float.IsFinite(opacity) || opacity < 0 || opacity > 1)
            throw new ArgumentOutOfRangeException(nameof(bounds));
    }

}

public readonly partial struct UiControlContext
{
    /// <summary>Paint-only affine composite. Both source textures must remain alive for queued frames.</summary>
    public void BackdropComposite(UiTextureId blackTexture, UiTextureId whiteTexture, UiRect bounds,
        float opacity = 1) => _ui.GetWindowDrawList().AddBackdropComposite(blackTexture, whiteTexture, bounds, opacity);

    public void BackdropComposite(UiTextureId blackTexture, UiTextureId whiteTexture, UiRect bounds,
        UiHorizontalOpacityMask mask, float opacity = 1) => _ui.GetWindowDrawList().AddBackdropComposite(blackTexture, whiteTexture, bounds, mask, opacity);
    public void BackdropComposite(UiTextureId blackTexture, UiTextureId whiteTexture, UiRect bounds,
        UiImageUvTransform uv, UiHorizontalOpacityMask mask, float opacity = 1) => _ui.GetWindowDrawList().AddBackdropComposite(blackTexture, whiteTexture, bounds, uv, mask, opacity);
}
