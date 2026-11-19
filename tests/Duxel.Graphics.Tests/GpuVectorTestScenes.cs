using System.Numerics;
using Duxel.Core;

internal static class GpuVectorTestScenes
{
    internal static UiGpuVectorScene Solid(UiVector2 size, float red = 1, float green = 0, float blue = 0) =>
        Rectangles(size, (new(0, 0, size.X, size.Y), new(red, green, blue, 1)));

    internal static UiGpuVectorScene Rectangles(UiVector2 size, params (UiRect Bounds, Vector4 Color)[] rectangles)
    {
        var data = new Vector4[rectangles.Length * 10];
        for (var i = 0; i < rectangles.Length; i++)
        {
            var (bounds, color) = rectangles[i];
            var header = i * 6;
            var edge = rectangles.Length * 6 + i * 4;
            var right = bounds.X + bounds.Width;
            var bottom = bounds.Y + bounds.Height;
            data[header] = new(bounds.X, bounds.Y, right, bottom);
            data[header + 1] = new(edge, 4, 0, 0);
            data[header + 2] = color;
            data[header + 5] = new(0, 0, 1, 0);
            data[edge] = new(bounds.X, bounds.Y, right, bounds.Y);
            data[edge + 1] = new(right, bounds.Y, right, bottom);
            data[edge + 2] = new(right, bottom, bounds.X, bottom);
            data[edge + 3] = new(bounds.X, bottom, bounds.X, bounds.Y);
        }
        return new(size, data, rectangles.Length);
    }
}
