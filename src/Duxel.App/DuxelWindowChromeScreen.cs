using Duxel.Core;

namespace Duxel.App;

internal sealed class DuxelWindowChromeScreen(
    UiScreen inner,
    IWindowChromeController chrome,
    float titleBarHeight,
    UiImageTexture? appIcon = null) : UiScreen
{
    private const float ButtonWidth = 48f;
    private const float IconLeft = 16f;
    private const float IconSize = 20f;
    private const float TitlePaddingX = 16f;
    private readonly UiScreen _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IWindowChromeController _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
    private readonly UiImageTexture? _appIcon = appIcon;
    private readonly float _titleBarHeight = MathF.Max(32f, titleBarHeight);

    public override void Render(UiImmediateContext ui)
    {
        ArgumentNullException.ThrowIfNull(ui);

        ui.SetViewportTopInset(_titleBarHeight);
        ui.EnableRootViewportContentLayout();
        _inner.Render(ui);
        DrawTitleBar(ui);
    }

    private void DrawTitleBar(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        var width = viewport.Size.X;
        var rect = new UiRect(0f, 0f, width, _titleBarHeight);
        var drawList = ui.GetForegroundDrawList();
        var titleBg = ui.GetColorU32(UiStyleColor.TitleBgActive);
        var textColor = ui.GetColorU32(UiStyleColor.WindowTitleText);
        var borderColor = ui.GetColorU32(UiStyleColor.Border);

        drawList.AddRectFilled(rect, titleBg, ui.WhiteTextureId, rect);
        drawList.AddRectFilled(new UiRect(0f, _titleBarHeight - 1f, width, 1f), borderColor, ui.WhiteTextureId, rect);

        var buttonCount = 1 + (_chrome.CanMaximize ? 1 : 0) + (_chrome.CanMinimize ? 1 : 0);
        var buttonAreaWidth = buttonCount * ButtonWidth;

        DrawAppIcon(ui, drawList, titleBg, textColor);
        DrawCenteredTitle(ui, drawList, width, buttonAreaWidth, textColor);

        var x = width - buttonAreaWidth;
        if (_chrome.CanMinimize)
        {
            DrawChromeButton(ui, "##duxel.titlebar.min", new UiRect(x, 0f, ButtonWidth, _titleBarHeight), DuxelChromeButtonKind.Minimize, _chrome.MinimizeWindow);
            x += ButtonWidth;
        }

        if (_chrome.CanMaximize)
        {
            DrawChromeButton(ui, "##duxel.titlebar.max", new UiRect(x, 0f, ButtonWidth, _titleBarHeight), DuxelChromeButtonKind.Maximize, _chrome.ToggleMaximizeWindow);
            x += ButtonWidth;
        }

        DrawChromeButton(ui, "##duxel.titlebar.close", new UiRect(x, 0f, ButtonWidth, _titleBarHeight), DuxelChromeButtonKind.Close, _chrome.CloseWindow, close: true);
    }

    private static void DrawChromeButton(UiImmediateContext ui, string id, UiRect rect, DuxelChromeButtonKind kind, Action action, bool close = false)
    {
        ui.SetCursorScreenPos(new UiVector2(rect.X, rect.Y));
        if (ui.InvisibleButton(id, new UiVector2(rect.Width, rect.Height)))
        {
            action();
        }

        var hovered = ui.IsItemHovered();
        var bg = close && hovered
            ? new UiColor(232, 17, 35)
            : hovered
                ? ui.GetColorU32(UiStyleColor.ButtonHovered)
                : new UiColor(0x00FFFFFF);
        if (hovered)
        {
            ui.GetForegroundDrawList().AddRectFilledRounded(rect, bg, ui.WhiteTextureId, 4f, rect);
        }

        var iconColor = close && hovered
            ? new UiColor(0xFFFFFFFF)
            : ui.GetColorU32(UiStyleColor.WindowTitleText);
        DrawChromeIcon(ui, rect, kind, iconColor);
    }

    private static void DrawChromeIcon(UiImmediateContext ui, UiRect rect, DuxelChromeButtonKind kind, UiColor color)
    {
        var drawList = ui.GetForegroundDrawList();
        var center = new UiVector2(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f);
        const float iconSize = 10f;
        const float thickness = 1.5f;

        switch (kind)
        {
            case DuxelChromeButtonKind.Minimize:
                drawList.AddLine(
                    new UiVector2(center.X - iconSize * 0.5f, center.Y + 3f),
                    new UiVector2(center.X + iconSize * 0.5f, center.Y + 3f),
                    color,
                    thickness);
                break;
            case DuxelChromeButtonKind.Maximize:
                drawList.AddRect(
                    new UiRect(center.X - iconSize * 0.5f, center.Y - iconSize * 0.5f, iconSize, iconSize),
                    color,
                    rounding: 1f,
                    thickness: thickness);
                break;
            case DuxelChromeButtonKind.Close:
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

    private void DrawAppIcon(UiImmediateContext ui, UiDrawListBuilder drawList, UiColor titleBg, UiColor textColor)
    {
        var iconY = MathF.Max(0f, _titleBarHeight - IconSize) * 0.5f;
        var iconRect = new UiRect(IconLeft, iconY, IconSize, IconSize);
        if (_appIcon is not null)
        {
            _appIcon.Prepare(ui, UiImageEffects.Default);
            var widthScale = IconSize / MathF.Max(1f, _appIcon.Width);
            var heightScale = IconSize / MathF.Max(1f, _appIcon.Height);
            var scale = MathF.Min(widthScale, heightScale);
            var iconWidth = MathF.Max(1f, _appIcon.Width * scale);
            var iconHeight = MathF.Max(1f, _appIcon.Height * scale);
            var imageRect = new UiRect(
                IconLeft + (IconSize - iconWidth) * 0.5f,
                iconY + (IconSize - iconHeight) * 0.5f,
                iconWidth,
                iconHeight);

            drawList.AddRectFilled(imageRect, new UiColor(0xFFFFFFFF), _appIcon.TextureId, iconRect);
            return;
        }

        DrawDefaultAppIcon(drawList, ui.WhiteTextureId, iconRect, titleBg, textColor);
    }

    private static void DrawDefaultAppIcon(UiDrawListBuilder drawList, UiTextureId whiteTexture, UiRect rect, UiColor titleBg, UiColor textColor)
    {
        var accent = IsDark(titleBg)
            ? new UiColor(96, 205, 255)
            : new UiColor(0, 95, 184);
        var shadow = IsDark(titleBg)
            ? new UiColor(255, 255, 255, 36)
            : new UiColor(0, 0, 0, 24);
        var bgRect = new UiRect(rect.X, rect.Y, rect.Width, rect.Height);
        drawList.AddRectFilledRounded(bgRect, accent, whiteTexture, 5f, rect);
        drawList.AddRect(bgRect, shadow, rounding: 5f, thickness: 1f);

        var inset = 5f;
        var left = rect.X + inset;
        var top = rect.Y + 4f;
        var bottom = rect.Y + rect.Height - 4f;
        var right = rect.X + rect.Width - inset;
        var midY = rect.Y + rect.Height * 0.5f;
        var letterColor = IsDark(accent) ? new UiColor(0xFFFFFFFF) : textColor;

        drawList.AddLine(new UiVector2(left, top), new UiVector2(left, bottom), letterColor, 1.8f);
        drawList.AddLine(new UiVector2(left, top), new UiVector2(right - 2f, top + 2f), letterColor, 1.8f);
        drawList.AddLine(new UiVector2(right - 2f, top + 2f), new UiVector2(right, midY), letterColor, 1.8f);
        drawList.AddLine(new UiVector2(right, midY), new UiVector2(right - 2f, bottom - 2f), letterColor, 1.8f);
        drawList.AddLine(new UiVector2(right - 2f, bottom - 2f), new UiVector2(left, bottom), letterColor, 1.8f);
    }

    private static bool IsDark(UiColor color)
    {
        var r = (int)(color.Rgba & 0xFF);
        var g = (int)((color.Rgba >> 8) & 0xFF);
        var b = (int)((color.Rgba >> 16) & 0xFF);
        return ((r * 299) + (g * 587) + (b * 114)) < 128000;
    }

    private void DrawCenteredTitle(UiImmediateContext ui, UiDrawListBuilder drawList, float width, float buttonAreaWidth, UiColor textColor)
    {
        var title = _chrome.WindowTitle;
        if (string.IsNullOrEmpty(title))
        {
            return;
        }

        var titleRectX = MathF.Max(TitlePaddingX, buttonAreaWidth);
        var titleRectWidth = MathF.Max(0f, width - (titleRectX * 2f));
        if (titleRectWidth <= 0f)
        {
            titleRectX = TitlePaddingX;
            titleRectWidth = MathF.Max(0f, width - buttonAreaWidth - (TitlePaddingX * 2f));
        }

        if (titleRectWidth <= 0f)
        {
            return;
        }

        var titleSize = ui.CalcTextSize(title);
        var titleX = titleRectX + MathF.Max(0f, titleRectWidth - titleSize.X) * 0.5f;
        var titleY = MathF.Max(0f, _titleBarHeight - titleSize.Y) * 0.5f;
        var clipRect = new UiRect(titleRectX, 0f, titleRectWidth, _titleBarHeight);

        drawList.PushClipRect(clipRect, intersectWithCurrentClipRect: true);
        drawList.AddText(new UiVector2(titleX, titleY), textColor, title);
        drawList.PopClipRect();
    }

    private enum DuxelChromeButtonKind
    {
        Minimize,
        Maximize,
        Close,
    }
}
