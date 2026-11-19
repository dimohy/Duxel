namespace Duxel.Core;

public sealed partial class UiDrawListBuilder
{
    /// <summary>Annular sector with linear angular color and one-pixel radial coverage ramps.</summary>
    public void AddAnnularSector(UiVector2 center, float radius, float stroke, float start, float sweep,
        UiColor first, UiColor last, UiTextureId texture, UiRect clip)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(radius) || !float.IsFinite(stroke)
            || !float.IsFinite(start) || !float.IsFinite(sweep) || stroke < 1 || radius <= stroke + 1 || MathF.Abs(sweep) > MathF.Tau + .00001f)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (!IsPresentationPass)
            return;
        if (sweep == 0)
            return;
        clip = Intersect(clip, _currentClipRect);
        if (clip.Width <= 0 || clip.Height <= 0)
            return;
        // Maximum chord error is0.01 logical pixel; large sectors split without lowering quality.
        var step = 2 * Math.Acos(Math.Max(-1, 1 - .01 / radius));
        var count = checked((int)Math.Ceiling(Math.Abs(sweep) / step));
        var bounds = new UiRect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
        for (var begin = 0; begin < count;)
        {
            var length = Math.Min(2048, count - begin);
            EnsureCapacityFor((length + 1) * 4, length * 18);
            var vertex = (uint)_vertices.Count;
            var index = (uint)_indices.Count;
            for (var j = 0; j <= length; j++)
            {
                var fraction = (float)(begin + j) / count;
                var angle = start + sweep * fraction;
                var direction = new UiVector2(MathF.Sin(angle), -MathF.Cos(angle));
                var color = LerpRingColor(first, last, fraction);
                var transparent = new UiColor(color.Rgba & 0x00ffffff);
                void Point(float distance, UiColor paint) => _vertices.Add(new(new(center.X + direction.X * distance, center.Y + direction.Y * distance), default, paint));
                Point(radius - stroke - 1, transparent);
                Point(radius - stroke, color);
                Point(radius - 1, color);
                Point(radius, transparent);
                if (j == length)
                    continue;
                for (var radial = 0u; radial < 3; radial++)
                {
                    var a = vertex + (uint)(j * 4) + radial;
                    _indices.Add(a);
                    _indices.Add(a + 4);
                    _indices.Add(a + 1);
                    _indices.Add(a + 1);
                    _indices.Add(a + 4);
                    _indices.Add(a + 5);
                }
            }
            AddCommand(new(clip, texture, index, (uint)(length * 18), 0, Bounds: bounds, HasBounds: true));
            begin += length;
        }
    }
    private static UiColor LerpRingColor(UiColor a, UiColor b, float t)
    {
        uint result = 0;
        for (var channel = 0; channel < 32; channel += 8)
        {
            var x = (a.Rgba >> channel) & 255;
            var y = (b.Rgba >> channel) & 255;
            result |= (uint)MathF.Round(x + (y - (float)x) * t) << channel;
        }
        return new(result);
    }
}
