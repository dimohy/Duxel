namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private void DrawChevronIcon(UiRect bounds, float rotationDegrees, float scale, float thickness, UiColor color, float opticalOffsetY = 0f)
    {
        var chevronPad = MathF.Max(3f, bounds.Width * 0.24f);
        var leftX = bounds.X + chevronPad;
        var rightX = bounds.X + bounds.Width - chevronPad;
        var topY = bounds.Y + chevronPad;
        var bottomY = bounds.Y + bounds.Height - chevronPad;
        var center = new UiVector2((leftX + rightX) * 0.5f, ((topY + bottomY) * 0.5f) + opticalOffsetY);

        var iconScale = Math.Clamp(scale, 0.2f, 1f);
        var p1Base = new UiVector2(center.X + ((leftX - center.X) * iconScale), center.Y + ((topY - center.Y) * iconScale));
        var p2Base = new UiVector2(center.X, center.Y + ((bottomY - center.Y) * iconScale));
        var p3Base = new UiVector2(center.X + ((rightX - center.X) * iconScale), center.Y + ((topY - center.Y) * iconScale));

        var rotationRadians = rotationDegrees * (MathF.PI / 180f);
        var sin = MathF.Sin(rotationRadians);
        var cos = MathF.Cos(rotationRadians);

        var p1 = RotatePoint(p1Base, center, sin, cos);
        var p2 = RotatePoint(p2Base, center, sin, cos);
        var p3 = RotatePoint(p3Base, center, sin, cos);

        DrawSmoothLine(p1, p2, color, thickness);
        DrawSmoothLine(p2, p3, color, thickness);
    }

    private void DrawSmoothLine(UiVector2 from, UiVector2 to, UiColor color, float thickness)
    {
        var lineThickness = MathF.Max(0.8f, thickness);
        _builder.AddLine(from, to, color, lineThickness, _whiteTexture);

        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var len = MathF.Sqrt((dx * dx) + (dy * dy));
        if (len <= 0.0001f)
        {
            return;
        }

        var nx = -dy / len;
        var ny = dx / len;
        var feather = 0.5f;
        var edgeColor = ApplyAlpha(color, 0.35f);

        var offsetA1 = new UiVector2(from.X + nx * feather, from.Y + ny * feather);
        var offsetA2 = new UiVector2(to.X + nx * feather, to.Y + ny * feather);
        var offsetB1 = new UiVector2(from.X - nx * feather, from.Y - ny * feather);
        var offsetB2 = new UiVector2(to.X - nx * feather, to.Y - ny * feather);

        _builder.AddLine(offsetA1, offsetA2, edgeColor, 1f, _whiteTexture);
        _builder.AddLine(offsetB1, offsetB2, edgeColor, 1f, _whiteTexture);
    }
}
