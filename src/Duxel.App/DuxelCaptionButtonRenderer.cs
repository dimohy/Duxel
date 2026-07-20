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
        var drawList = ui.GetForegroundDrawList();
        if (hovered || pressed)
        {
            var background = kind is DuxelCaptionButtonKind.Close
                ? pressed ? new UiColor(196, 15, 29) : new UiColor(232, 17, 35)
                : pressed ? ui.GetColorU32(UiStyleColor.ButtonActive) : ui.GetColorU32(UiStyleColor.ButtonHovered);
            drawList.AddRectFilledRounded(rect, background, ui.WhiteTextureId, 4f, rect);
        }

        var iconColor = kind is DuxelCaptionButtonKind.Close && hovered
            ? new UiColor(0xFFFFFFFF)
            : ui.GetColorU32(UiStyleColor.WindowTitleText);
        DrawIcon(drawList, rect, kind, isMaximized, iconColor);
    }

    private static void DrawIcon(
        UiDrawListBuilder drawList,
        UiRect rect,
        DuxelCaptionButtonKind kind,
        bool isMaximized,
        UiColor color)
    {
        var center = new UiVector2(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f);
        const float iconSize = 10f;
        const float thickness = 1.5f;

        switch (kind)
        {
            case DuxelCaptionButtonKind.Minimize:
                drawList.AddLine(
                    new UiVector2(center.X - iconSize * 0.5f, center.Y + 3f),
                    new UiVector2(center.X + iconSize * 0.5f, center.Y + 3f),
                    color,
                    thickness);
                break;
            case DuxelCaptionButtonKind.Maximize when isMaximized:
                const float restoreSize = 7f;
                const float restoreOffset = 1.5f;
                var backLeft = center.X - restoreSize * 0.5f + restoreOffset;
                var backTop = center.Y - restoreSize * 0.5f - restoreOffset;
                var backRight = backLeft + restoreSize;
                var backBottom = backTop + restoreSize;
                var frontLeft = center.X - restoreSize * 0.5f - restoreOffset;
                var frontTop = center.Y - restoreSize * 0.5f + restoreOffset;
                var frontRight = frontLeft + restoreSize;

                drawList.AddLine(new UiVector2(backLeft, backTop), new UiVector2(backRight, backTop), color, thickness);
                drawList.AddLine(new UiVector2(backRight, backTop), new UiVector2(backRight, backBottom), color, thickness);
                drawList.AddLine(new UiVector2(backLeft, backTop), new UiVector2(backLeft, frontTop), color, thickness);
                drawList.AddLine(new UiVector2(frontRight, backBottom), new UiVector2(backRight, backBottom), color, thickness);
                drawList.AddRect(
                    new UiRect(frontLeft, frontTop, restoreSize, restoreSize),
                    color,
                    rounding: 0f,
                    thickness: thickness);
                break;
            case DuxelCaptionButtonKind.Maximize:
                drawList.AddRect(
                    new UiRect(center.X - iconSize * 0.5f, center.Y - iconSize * 0.5f, iconSize, iconSize),
                    color,
                    rounding: 1f,
                    thickness: thickness);
                break;
            case DuxelCaptionButtonKind.Close:
                drawList.AddLine(
                    new UiVector2(center.X - iconSize * 0.45f, center.Y - iconSize * 0.45f),
                    new UiVector2(center.X + iconSize * 0.45f, center.Y + iconSize * 0.45f),
                    color,
                    thickness);
                drawList.AddLine(
                    new UiVector2(center.X + iconSize * 0.45f, center.Y - iconSize * 0.45f),
                    new UiVector2(center.X - iconSize * 0.45f, center.Y + iconSize * 0.45f),
                    color,
                    thickness);
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
