using Duxel.Core;

namespace Duxel.App;

internal sealed class ExtendedContentWindowChromeScreen(
    UiScreen inner,
    IWindowChromeController chrome,
    IWindowTitleBarPlatform titleBar) : UiScreen
{
    private readonly UiScreen _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IWindowChromeController _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
    private readonly IWindowTitleBarPlatform _titleBar = titleBar ?? throw new ArgumentNullException(nameof(titleBar));

    public override void Render(UiImmediateContext ui)
    {
        ArgumentNullException.ThrowIfNull(ui);

        _inner.Render(ui);
        DrawCaptionButtons(ui);
    }

    private void DrawCaptionButtons(UiImmediateContext ui)
    {
        if (!_titleBar.TryGetCaptionButtonBounds(out var bounds)
            || bounds.Width <= 0f
            || bounds.Height <= 0f)
        {
            return;
        }

        var buttonWidth = DuxelCaptionButtonRenderer.ButtonWidth;
        var state = _titleBar.CaptionButtonVisualState;
        var x = bounds.X;

        if (_chrome.CanMinimize)
        {
            DrawCaptionButton(ui, new UiRect(x, bounds.Y, buttonWidth, bounds.Height), DuxelCaptionButtonKind.Minimize, UiCaptionButtonKind.Minimize, state);
            x += buttonWidth;
        }

        if (_chrome.CanMaximize)
        {
            DrawCaptionButton(ui, new UiRect(x, bounds.Y, buttonWidth, bounds.Height), DuxelCaptionButtonKind.Maximize, UiCaptionButtonKind.Maximize, state);
            x += buttonWidth;
        }

        DrawCaptionButton(ui, new UiRect(x, bounds.Y, buttonWidth, bounds.Height), DuxelCaptionButtonKind.Close, UiCaptionButtonKind.Close, state);
    }

    private void DrawCaptionButton(
        UiImmediateContext ui,
        UiRect rect,
        DuxelCaptionButtonKind kind,
        UiCaptionButtonKind platformKind,
        UiCaptionButtonVisualState state)
    {
        DuxelCaptionButtonRenderer.Draw(
            ui,
            rect,
            kind,
            _chrome.IsMaximized,
            state.Hovered == platformKind,
            state.Pressed == platformKind);
    }
}
