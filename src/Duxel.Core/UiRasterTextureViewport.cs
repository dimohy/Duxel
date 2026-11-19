namespace Duxel.Core;

/// <summary>One visible pixel window shared by SVG and glyph vector textures.</summary>
internal readonly record struct UiRasterTextureViewport(int Width, int Height, UiGpuVectorCamera? Camera,
    UiVector2 UvMinimum, UiVector2 UvMaximum)
{
    internal static UiRasterTextureViewport Create(UiRect bounds, UiRect clip, float resolution)
    {
        if (!float.IsFinite(resolution) || resolution <= 0 || bounds.Width <= 0 || bounds.Height <= 0 || clip.Width <= 0 || clip.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(resolution), "A raster viewport requires positive bounds, clip and resolution.");
        var fullWidth = MathF.Max(1, MathF.Ceiling(bounds.Width * resolution));
        var fullHeight = MathF.Max(1, MathF.Ceiling(bounds.Height * resolution));
        var left = MathF.Max(0, MathF.Floor((clip.X - bounds.X) * resolution) - 1);
        var top = MathF.Max(0, MathF.Floor((clip.Y - bounds.Y) * resolution) - 1);
        var right = MathF.Min(fullWidth, MathF.Ceiling((clip.X + clip.Width - bounds.X) * resolution) + 1);
        var bottom = MathF.Min(fullHeight, MathF.Ceiling((clip.Y + clip.Height - bounds.Y) * resolution) + 1);
        var width = Math.Max(1, checked((int)(right - left)));
        var height = Math.Max(1, checked((int)(bottom - top)));
        UiGpuVectorCamera? camera = left == 0 && top == 0 && right == fullWidth && bottom == fullHeight
            ? null : new(new(fullWidth, fullHeight), new(left, top));
        camera?.Validate();
        return new(width, height, camera, new(-left / width, -top / height), new((fullWidth - left) / width, (fullHeight - top) / height));
    }
}
