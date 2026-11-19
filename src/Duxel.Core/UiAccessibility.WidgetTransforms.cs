using System.Numerics;

namespace Duxel.Core;

public sealed partial class UiAccessibilityTree
{
    internal Matrix3x2 WidgetCoordinates { get; set; } = Matrix3x2.Identity;
    internal UiRect? WidgetClip
    {
        get; set;
    }
    internal UiWidgetClipGeometry? WidgetClipGeometry
    {
        get; set;
    }

    private UiAccessibilityNode MapWidgetNode(UiAccessibilityNode node, ref UiRect? revealBounds)
    {
        if (WidgetCoordinates == Matrix3x2.Identity && WidgetClipGeometry is null)
            return node;
        if (!UiWidgetAffineLayout.TryInvert(WidgetCoordinates, out var inverse))
            throw new InvalidOperationException("Accessible widget coordinates must be invertible.");
        var geometry = new UiWidgetPointGeometry(node.Bounds, WidgetCoordinates, inverse, WidgetClipGeometry);
        var visible = geometry.VisibleGeometry(out var clip, out var interior);
        var offscreen = node.Offscreen || !visible;
        var point = UiWidgetAffineLayout.MapPoint(node.ClickablePoint ?? new(node.Bounds.X + node.Bounds.Width / 2,
            node.Bounds.Y + node.Bounds.Height / 2), WidgetCoordinates);
        if (revealBounds is { } reveal)
            revealBounds = UiWidgetAffineLayout.MapBounds(reveal, WidgetCoordinates);
        return node with
        {
            Bounds = clip,
            Offscreen = offscreen,
            ClickablePoint = offscreen ? null : geometry.ContainsPoint(point.X, point.Y) ? point : interior,
            PointGeometry = geometry,
            TextLayout = node.TextLayout?.WithWidgetCoordinates(WidgetCoordinates, WidgetClip, WidgetClipGeometry)
        };
    }

}

public sealed partial class UiAccessibilityTextLayout
{
    private UiAccessibilityTextLayout? _widgetMapped;
    private UiAccessibilityTextLayout? _widgetSource;
    private Matrix3x2 _widgetMapping, _widgetInverse;
    private UiRect? _widgetParentClip;
    private UiWidgetClipGeometry? _widgetClips;

    internal UiAccessibilityTextLayout WithWidgetCoordinates(Matrix3x2 matrix, UiRect? parentClip, UiWidgetClipGeometry? clips)
    {
        if (_widgetMapped is { } previous && previous._widgetMapping == matrix && previous._widgetParentClip == parentClip && previous._widgetClips == clips)
            return previous;
        if (!UiWidgetAffineLayout.TryInvert(matrix, out var inverse))
            throw new InvalidOperationException("Accessible widget coordinates must be invertible.");
        var clip = UiWidgetAffineLayout.MapBounds(ClipBounds, matrix);
        if (parentClip is { } parent)
        {
            var x = MathF.Max(clip.X, parent.X);
            var y = MathF.Max(clip.Y, parent.Y);
            clip = new(x, y, MathF.Max(0, MathF.Min(clip.X + clip.Width, parent.X + parent.Width) - x),
                MathF.Max(0, MathF.Min(clip.Y + clip.Height, parent.Y + parent.Height) - y));
        }
        var characters = new UiRect[_characters.Length];
        var carets = new UiRect[_carets.Length];
        for (var i = 0; i < characters.Length; i++)
            characters[i] = UiWidgetAffineLayout.MapBounds(GetCharacterExtents(i), matrix);
        for (var i = 0; i < carets.Length; i++)
            carets[i] = UiWidgetAffineLayout.MapBounds(AtViewport(_carets[i]), matrix);
        return _widgetMapped = new(Content, MeasurementKey, UiWidgetAffineLayout.MapPoint(Origin, matrix), clip,
            LineHeight * MathF.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12), VisualVersion, characters, carets, _lines)
        {
            VisualKey = VisualKey,
            Composition = Composition,
            _widgetSource = this,
            _widgetMapping = matrix,
            _widgetInverse = inverse,
            _widgetParentClip = parentClip,
            _widgetClips = clips
        };
    }
}
