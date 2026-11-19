namespace Duxel.Core;

/// <summary>A filled regular polygon in source pixels. Rotation is measured in radians.</summary>
public readonly record struct UiRegularPolygon(UiVector2 Center, float Radius, int Sides, float Rotation, UiColor Color);

public sealed partial class UiDrawListBuilder
{
    /// <summary>Appends ordered vector polygons in batches, preserving the active texture, clip and user data.</summary>
    public void AddRegularPolygons(ReadOnlySpan<UiRegularPolygon> polygons)
    {
        // Validate the complete input before mutating any output.
        foreach (var polygon in polygons)
            if (polygon.Sides < 3 || polygon.Sides > MaxVerticesPerList
                || (long)(polygon.Sides - 2) * 3 > MaxIndicesPerList
                || !float.IsFinite(polygon.Radius) || polygon.Radius <= 0
                || !float.IsFinite(polygon.Rotation) || !float.IsFinite(polygon.Center.X) || !float.IsFinite(polygon.Center.Y))
                throw new ArgumentOutOfRangeException(nameof(polygons));

        if (!IsPresentationPass)
            return;
        var position = 0;
        while (position < polygons.Length)
        {
            var end = position;
            var vertices = 0;
            var indices = 0;
            while (end < polygons.Length)
            {
                var shape = polygons[end];
                var nextIndices = (shape.Sides - 2) * 3;
                if (vertices + shape.Sides > MaxVerticesPerList || indices + nextIndices > MaxIndicesPerList)
                    break;
                vertices += shape.Sides;
                indices += nextIndices;
                end++;
            }
            EnsureCapacityFor(vertices, indices);
            var startIndex = (uint)_indices.Count;
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            for (; position < end; position++)
            {
                ref readonly var polygon = ref polygons[position];
                var step = MathF.Tau / polygon.Sides;
                var cosStep = MathF.Cos(step);
                var sinStep = MathF.Sin(step);
                var cos = MathF.Cos(polygon.Rotation);
                var sin = MathF.Sin(polygon.Rotation);
                var startVertex = (uint)_vertices.Count;
                for (var vertex = 0; vertex < polygon.Sides; vertex++)
                {
                    var point = new UiVector2(polygon.Center.X + cos * polygon.Radius, polygon.Center.Y + sin * polygon.Radius);
                    _vertices.Add(new(point, default, polygon.Color));
                    minX = MathF.Min(minX, point.X);
                    minY = MathF.Min(minY, point.Y);
                    maxX = MathF.Max(maxX, point.X);
                    maxY = MathF.Max(maxY, point.Y);
                    var nextCos = cos * cosStep - sin * sinStep;
                    sin = sin * cosStep + cos * sinStep;
                    cos = nextCos;
                }
                for (var triangle = 1; triangle < polygon.Sides - 1; triangle++)
                {
                    _indices.Add(startVertex);
                    _indices.Add(startVertex + (uint)triangle);
                    _indices.Add(startVertex + (uint)triangle + 1);
                }
            }
            AddCommand(new(_currentClipRect, _currentTexture, startIndex, (uint)indices, 0,
                Bounds: new(minX, minY, maxX - minX, maxY - minY), HasBounds: true));
        }
    }
}
