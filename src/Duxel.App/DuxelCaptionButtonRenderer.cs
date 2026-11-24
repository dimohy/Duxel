using Duxel.Core;

namespace Duxel.App;

internal static class DuxelCaptionButtonRenderer
{
    public const float ButtonWidth = 48f;

    public static void Draw(
        UiImmediateContext ui,
        UiRect rect,
        DuxelCaptionButtonKind kind,
        bool isMaximized,
        bool hovered,
        bool pressed)
    {
        var drawList = ui.GetCaptionButtonDrawList((int)kind);
        var theme = ui.GetStyle();
        var iconColor = theme.WindowTitleText;
        if (hovered)
        {
            var close = kind == DuxelCaptionButtonKind.Close;
            var background = close
                ? pressed ? new UiColor(177, 14, 28) : new UiColor(232, 17, 35)
                : pressed ? theme.ButtonActive : theme.ButtonHovered;
            if (theme.ForcedColors is { } system)
            {
                background = system.Highlight;
                iconColor = system.HighlightText;
            }
            else if (close)
                iconColor = new UiColor(255, 255, 255);

            drawList.AddRectFilled(rect, background, ui.WhiteTextureId, rect);
            if (pressed && theme.ForcedColors is { } contrast)
                drawList.AddRect(new(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4),
                    contrast.HighlightText, rounding : 0, thickness : 1 / ui.ContentScale);
        }

        // Draw command scheduling must not move another caption button into this one's damage.
        drawList.PushClipRect(rect);
        try
        {
            DrawIcon(drawList, rect, kind, isMaximized, iconColor, ui.ContentScale);
        }
        finally
        {
            drawList.PopClipRect();
        }
    }

    private static void DrawIcon(
        UiDrawListBuilder drawList,
        UiRect rect,
        DuxelCaptionButtonKind kind,
        bool isMaximized,
        UiColor color,
        float scale)
    {
        var center = new UiVector2(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f);
        const float half = 5f;
        var pixels = MathF.Max(1, MathF.Round(scale));
        var thickness = pixels / scale;
        var phase = (pixels % 2) * .5f;
        float Snap(float value) => (MathF.Round(value * scale - phase) + phase) / scale;
        UiVector2 Point(float x, float y) => new(Snap(center.X + x), Snap(center.Y + y));
        void Line(float left, float top, float right, float bottom) => drawList.AddLine(Point(left, top), Point(right, bottom), color, thickness);
        void Frame(float left, float top, float right, float bottom)
        {
            var start = Point(left, top);
            var end = Point(right, bottom);
            drawList.AddRect(new(start.X, start.Y, end.X - start.X, end.Y - start.Y), color, rounding : 0, thickness : thickness);
        }

        switch (kind)
        {
            case DuxelCaptionButtonKind.Minimize:
                Line(-half, 2, half, 2);
                break;
            case DuxelCaptionButtonKind.Maximize when isMaximized:
                Line(-3, -half, half, -half);
                Line(half, -half, half, 3);
                Line(-3, -half, -3, -3);
                Line(3, 3, half, 3);
                Frame(-half, -3, 3, half);
                break;
            case DuxelCaptionButtonKind.Maximize:
                Frame(-half, -half, half, half);
                break;
            case DuxelCaptionButtonKind.Close:
                Line(-half, -half, half, half);
                Line(half, -half, -half, half);
                break;
        }
    }
}

internal enum DuxelCaptionButtonKind
{
    Minimize,
    Maximize,
    Close,
}
