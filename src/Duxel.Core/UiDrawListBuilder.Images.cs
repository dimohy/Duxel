namespace Duxel.Core;

public enum UiImageFit
{
    Stretch, Contain, Cover
}

/// <summary>Affine texture coordinates across a viewport: origin + x * AxisX + y * AxisY for normalized viewport coordinates.</summary>
public readonly record struct UiImageUvTransform(UiVector2 Origin, UiVector2 AxisX, UiVector2 AxisY)
{
    public static UiImageUvTransform Identity => new(new(0, 0), new(1, 0), new(0, 1));
    public static UiImageUvTransform ForFit(UiVector2 source, UiVector2 viewport, UiImageFit fit)
    {
        if ((uint)fit > 2)
            throw new ArgumentOutOfRangeException(nameof(fit));
        if (!float.IsFinite(source.X) || !float.IsFinite(source.Y) || source.X <= 0 || source.Y <= 0
            || !float.IsFinite(viewport.X) || !float.IsFinite(viewport.Y) || viewport.X <= 0 || viewport.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(source));
        if (fit == UiImageFit.Stretch)
            return Identity;
        var x = (double)viewport.X / source.X;
        var y = (double)viewport.Y / source.Y;
        var scale = fit == UiImageFit.Cover ? Math.Max(x, y) : Math.Min(x, y);
        var u = (float)(x / scale);
        var v = (float)(y / scale);
        return new UiImageUvTransform(new((1 - u) * .5f, (1 - v) * .5f), new(u, 0), new(0, v)).Validate();
    }
    public UiImageUvTransform Validate()
    {
        if (!float.IsFinite(Origin.X) || !float.IsFinite(Origin.Y) || !float.IsFinite(AxisX.X)
            || !float.IsFinite(AxisX.Y) || !float.IsFinite(AxisY.X) || !float.IsFinite(AxisY.Y))
            throw new ArgumentOutOfRangeException(nameof(UiImageUvTransform));
        return this;
    }

    internal bool IsInsideTexture
        => AxisInsideTexture(Origin.X, AxisX.X, AxisY.X) && AxisInsideTexture(Origin.Y, AxisX.Y, AxisY.Y);

    private static bool AxisInsideTexture(float origin, float x, float y)
        => (double)origin + Math.Min(x, 0f) + Math.Min(y, 0f) >= 0
            && (double)origin + Math.Max(x, 0f) + Math.Max(y, 0f) <= 1;
}

public sealed partial class UiDrawListBuilder
{
    internal void AddImageCamera(UiTextureId texture, UiRect bounds, UiVector2 uvMin, UiVector2 uvMax,
        UiColor tint, float radius, UiVector2 sourceSize, UiRect clip)
    {
        if (!IsPresentationPass)
            return;
        EnsureCapacityFor(4, 6);
        uint mask = 0;
        if (radius > 0)
        {
            var parent = HasPaintClip ? GetRoundedClipRecord() : 0;
            var index = _rectFilledPrimitives.Count;
            var relativeParent = parent == 0 ? 0u : unchecked((uint)(checked((int)parent - 1) - index));
            _rectFilledPrimitives.Add(new(new(0, 0, sourceSize.X, sourceSize.Y), new(relativeParent),
                MathF.Min(radius, MathF.Min(sourceSize.X, sourceSize.Y) * .5f), -4));
            var scaleX = sourceSize.X / bounds.Width;
            var scaleY = sourceSize.Y / bounds.Height;
            _rectFilledPrimitives.Add(new(new(scaleX, 0, 0, scaleY), default, -bounds.X * scaleX, -bounds.Y * scaleY));
            mask = checked((uint)index + 1);
        }
        AddImageQuad(texture, new(bounds.X, bounds.Y), new(bounds.X + bounds.Width, bounds.Y),
            new(bounds.X + bounds.Width, bounds.Y + bounds.Height), new(bounds.X, bounds.Y + bounds.Height),
            uvMin, new(uvMax.X, uvMin.Y), uvMax, new(uvMin.X, uvMax.Y), tint, Intersect(clip, _currentClipRect), mask);
    }
    /// <summary>Analytic rounded viewport with affine UVs.</summary>
    public void AddImageRounded(UiTextureId texture, UiRect rect, UiImageUvTransform uv,
        UiColor tint, float radius, UiRect clip) => AddImageRounded(texture, rect, uv, tint, radius, clip, null);

    /// <summary>Optional content size makes the rounded source shape follow the affine UVs.</summary>
    public void AddImageRounded(UiTextureId texture, UiRect rect, UiImageUvTransform uv,
        UiColor tint, float radius, UiRect clip, UiVector2? contentSize)
    {
        uv.Validate();
        if (!float.IsFinite(radius) || radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!float.IsFinite(rect.X) || !float.IsFinite(rect.Y) || !float.IsFinite(rect.Width)
            || !float.IsFinite(rect.Height) || rect.Width < 0 || rect.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(rect));
        if (rect.Width == 0 || rect.Height == 0)
            return;
        var cornerSize = contentSize ?? new UiVector2(rect.Width, rect.Height);
        if (!float.IsFinite(cornerSize.X) || !float.IsFinite(cornerSize.Y) || cornerSize.X <= 0 || cornerSize.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(contentSize));
        if (!IsPresentationPass)
            return;
        var offset = checked((uint)_rectFilledPrimitives.Count);
        _rectFilledPrimitives.Add(new(rect, tint, MathF.Min(radius, MathF.Min(cornerSize.X, cornerSize.Y) * .5f)));
        // Two existing32B slots. Optional source-space dimensions occupy the metadata's two color words.
        _rectFilledPrimitives.Add(new(new(uv.Origin.X, uv.Origin.Y, uv.AxisX.X, uv.AxisX.Y),
            contentSize.HasValue ? new(BitConverter.SingleToUInt32Bits(cornerSize.X)) : default, uv.AxisY.X, uv.AxisY.Y,
            contentSize.HasValue ? new(BitConverter.SingleToUInt32Bits(cornerSize.Y)) : default));
        // When the corners follow source UVs, the source shape already limits UVs
        // to the texture. A viewport-space shape can also have a separate UV edge.
        var opaqueRange = radius > 0f && tint.Rgba >> 24 == 255 && (contentSize.HasValue || uv.IsInsideTexture);
        AddRectFilledPrimitiveCommand(Intersect(clip, _currentClipRect), texture, offset, rect,
            (uint)UiRectGeometryVariant.Image, 2, opaqueRange);
    }
}
