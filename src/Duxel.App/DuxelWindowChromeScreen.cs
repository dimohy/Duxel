using Duxel.Core;

namespace Duxel.App;

internal sealed class DuxelWindowChromeScreen(
    UiScreen inner,
    IWindowChromeController chrome,
    float titleBarHeight,
    UiImageTexture? appIcon = null) : UiScreen
{
    private const float ButtonWidth = DuxelCaptionButtonRenderer.ButtonWidth;
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

        DrawAppIcon(ui, drawList);
        DrawLeftAlignedTitle(ui, drawList, width, buttonAreaWidth, textColor);

        var x = width - buttonAreaWidth;
        if (_chrome.CanMinimize)
        {
            DrawChromeButton(ui, "##duxel.titlebar.min", new UiRect(x, 0f, ButtonWidth, _titleBarHeight), DuxelCaptionButtonKind.Minimize, _chrome.MinimizeWindow);
            x += ButtonWidth;
        }

        if (_chrome.CanMaximize)
        {
            DrawChromeButton(
                ui,
                "##duxel.titlebar.max",
                new UiRect(x, 0f, ButtonWidth, _titleBarHeight),
                DuxelCaptionButtonKind.Maximize,
                _chrome.ToggleMaximizeWindow);
            x += ButtonWidth;
        }

        DrawChromeButton(ui, "##duxel.titlebar.close", new UiRect(x, 0f, ButtonWidth, _titleBarHeight), DuxelCaptionButtonKind.Close, _chrome.CloseWindow);
    }

    private void DrawChromeButton(UiImmediateContext ui, string id, UiRect rect, DuxelCaptionButtonKind kind, Action action)
    {
        ui.SetCursorScreenPos(new UiVector2(rect.X, rect.Y));
        if (ui.InvisibleButton(id, new UiVector2(rect.Width, rect.Height)))
        {
            action();
        }

        DuxelCaptionButtonRenderer.Draw(
            ui,
            rect,
            kind,
            _chrome.IsMaximized,
            ui.IsItemHovered(),
            ui.IsItemActive());
    }

    private void DrawAppIcon(UiImmediateContext ui, UiDrawListBuilder drawList)
    {
        if (_appIcon is null)
        {
            return;
        }

        var iconY = MathF.Max(0f, _titleBarHeight - IconSize) * 0.5f;
        var iconRect = new UiRect(IconLeft, iconY, IconSize, IconSize);
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

        drawList.AddImage(
            _appIcon.TextureId,
            new UiVector2(imageRect.X, imageRect.Y),
            new UiVector2(imageRect.X + imageRect.Width, imageRect.Y + imageRect.Height),
            new UiVector2(0f, 0f),
            new UiVector2(1f, 1f),
            new UiColor(0xFFFFFFFF),
            iconRect);
    }

    private void DrawLeftAlignedTitle(UiImmediateContext ui, UiDrawListBuilder drawList, float width, float buttonAreaWidth, UiColor textColor)
    {
        var title = _chrome.WindowTitle;
        if (string.IsNullOrEmpty(title))
        {
            return;
        }

        var titleRectX = _appIcon is null
            ? TitlePaddingX
            : IconLeft + IconSize + TitlePaddingX;
        var titleRectWidth = MathF.Max(0f, width - buttonAreaWidth - titleRectX - TitlePaddingX);

        if (titleRectWidth <= 0f)
        {
            return;
        }

        var titleSize = ui.CalcTextSize(title);
        var titleY = MathF.Max(0f, _titleBarHeight - titleSize.Y) * 0.5f;
        var clipRect = new UiRect(titleRectX, 0f, titleRectWidth, _titleBarHeight);

        drawList.PushClipRect(clipRect, intersectWithCurrentClipRect: true);
        drawList.AddText(new UiVector2(titleRectX, titleY), textColor, title);
        drawList.PopClipRect();
    }
}
