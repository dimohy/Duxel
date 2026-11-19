using System.Numerics;

namespace Duxel.Svg.Managed;

internal sealed partial class SvgManagedScene
{
    private static Node FoldSinglePaintOpacity(Node node, float opacity)
    {
        if (opacity is 0 or 1 || !TryFold(node, out var folded))
            return node;
        return folded with
        {
            Opacity = 1
        };

        bool TryFold(Node current, out Node result)
        {
            result = current;
            // A geometric clip preserves a single paint's identity; masks and filters consume stored colors.
            if (current.Mask is not null || current.Filter is not null)
                return false;
            if (current.Fill is null && current.Stroke is null && current.Children.Length == 1)
            {
                var child = current.Children[0];
                // A transformed child retains the group's intermediate color-storage boundary.
                if (child.Opacity != 1 || child.Transform != Matrix3x2.Identity || !TryFold(child, out var foldedChild))
                    return false;
                result = current with
                {
                    Children = [foldedChild]
                };
                return true;
            }
            if (current.Children.Length != 0 || (current.Fill is null) == (current.Stroke is null))
                return false;
            var paint = current.Fill is not null ? current.FillColor : current.StrokeColor;
            // A nested element opacity is a storage boundary, not another scalar to multiply away.
            ISvgPaint Fold(ISvgPaint source) => new SvgElementOpacityPaint(source, opacity);
            var replacement = paint switch
            {
                SvgOrderedDither dither when dither.Source is not SvgElementOpacityPaint => dither with { Source = Fold(dither.Source) },
                SvgSolidPaint => Fold(paint),
                _ => null
            };
            if (replacement is null)
                return false;
            result = current.Fill is not null ? current with
            {
                FillColor = replacement
            } : current with
            {
                StrokeColor = replacement
            };
            return true;
        }
    }
}
