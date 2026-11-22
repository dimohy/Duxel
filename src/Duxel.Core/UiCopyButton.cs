namespace Duxel.Core;

/// <summary>Copies current text and shows a timed confirmation without taking over the clipboard lifetime.</summary>
public sealed class UiCopyButton : UiWidget
{
    private readonly UiFluentTooltip _hint;
    private readonly string _copyLabel;
    private double? _until;
    public UiCopyButton(Func<string> getText, string copyLabel, string copiedLabel, bool showHoverHint = false,
        UiPopupPosition position = UiPopupPosition.AboveEnd)
    {
        ArgumentNullException.ThrowIfNull(getText);
        _copyLabel = copyLabel;
        var button = new UiButton<UiReleaseButtonBehavior, CopyPresentation>(_copyLabel, () =>
        {
            var ui = RenderingContext!;
            ui.SetClipboardText(getText());
            _until = ui.GetTime() + 1.2;
            _hint!.Text = copiedLabel;
            _hint.Visible = true;
            ui.RequestFrameAt(ui.GetTime());
        });
        _hint = Fluent.Tooltip(button, _copyLabel, new()
        {
            Positioning = new() { Position = position },
            ShowDelaySeconds = showHoverHint ? .65 : 0,
            HideDelaySeconds = 0
        });
        _hint.Visible = false;
        if (showHoverHint)
            _hint.VisibleChangeRequested = change =>
            {
                if (_until is null) _hint.Visible = change.Visible;
            };
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("hint", () => _hint);
    protected override void RenderContent(UiImmediateContext ui)
    {
        var now = ui.GetTime();
        if (_until is { } until)
        {
            _hint.Visible = now < until;
            if (now < until) ui.RequestFrameAt(until);
            else { _until = null; _hint.Text = _copyLabel; }
        }
        base.RenderContent(ui);
    }

    private readonly struct CopyPresentation : IUiButtonPresentation
    {
        public static bool CanRetainMeasurement => true;
        public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) => new(32, 32);
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds, UiButtonInteraction interaction)
        {
            var foreground = context.Theme.ForcedColors?.CanvasText ?? context.Theme.Text;
            var active = interaction.Hovered || interaction.Focused;
            var icon = active || context.Theme.ForcedColors is not null ? foreground : new UiColor((foreground.Rgba & 0x00ffffffu) | 0x99000000u);
            context.Frame(bounds, active ? context.Theme.ButtonHovered : default, default, 4, 0);
            context.Frame(new(bounds.X + 13, bounds.Y + 9, 10, 10), default, icon, 2, 1.5f);
            context.Frame(new(bounds.X + 9, bounds.Y + 13, 10, 10), context.Theme.WindowBg, icon, 2, 1.5f);
            if (interaction.FocusVisible) context.FocusOutline(bounds, 4);
        }
    }
}
