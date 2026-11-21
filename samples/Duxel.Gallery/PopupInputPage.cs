using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class PopupInputPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private readonly UiWidget _root, _popup;
    private readonly Action<UiImmediateContext> _renderPopup;
    private bool _requestOpen, _requestClose;
    internal UiPopupInputMode Mode { get; private set; } = UiPopupInputMode.NonModal;
    internal bool Visible { get; private set; }
    internal int InsideCount { get; private set; }
    internal int OutsideCount { get; private set; }
    public string Title => "팝업 입력";
    public string Description => "Compare navigation, activation, and dismissal in nonmodal popovers and popovers that capture keyboard input.";
    public IReadOnlyList<IGallerySample> Samples { get; }
    UiWidget IObserverNaturalPage.MeasureRoot => _root;
    void IObserverNaturalPage.RenderOverlays(UiImmediateContext ui) => _renderPopup(ui);

    internal PopupInputPage()
    {
        var popup = GallerySample.Define("_popup", () => Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Button(GalleryText.Get("Run command inside pop-up"), () => InsideCount++),
            Fluent.Button(GalleryText.Get("Next control in pop-up")),
            Fluent.Button(GalleryText.Get("Close input pop-up"), () => _requestClose = true)));
        _popup = popup.Value;
        var render = GallerySample.Define<Action<UiImmediateContext>>("renderPopup", () => (UiImmediateContext ui) =>
        {
            if (_requestOpen)
            {
                ui.OpenPopup("observer-input-popup");
                _requestOpen = false;
            }
            ui.SetNextPopupPosition(new(420, 230));
            Visible = ui.BeginPopup("observer-input-popup", Mode);
            if (!Visible)
                return;
            try
            {
                _popup.Render(ui);
                if (_requestClose)
                {
                    ui.CloseCurrentPopup();
                    _requestClose = false;
                }
            }
            finally { ui.EndPopup(); }
        });
        _renderPopup = render.Value;
        var sample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1(GalleryText.Get("Tab can move to controls outside a nonmodal pop-up. Exclusive keyboard mode keeps focus inside the pop-up.")),
            Fluent.Button(GalleryText.Get("Change pop-up input mode"), () => Mode = Mode == UiPopupInputMode.NonModal ? UiPopupInputMode.Exclusive : UiPopupInputMode.NonModal),
            Fluent.Button(GalleryText.Get("Open input pop-up"), () => _requestOpen = true),
            Fluent.Button(GalleryText.Get("Run command outside pop-up"), () => OutsideCount++),
            Fluent.Body1(() => string.Format(GalleryText.Get("Commands inside: {0} / Commands outside: {1}"), InsideCount, OutsideCount)),
            Fluent.Body1(() => Mode == UiPopupInputMode.NonModal ? GalleryText.Get("Current mode: Nonmodal") : GalleryText.Get("Current mode: Exclusive keyboard")),
            Fluent.Body1(GalleryText.Get("Click inside the pop-up and try Tab and Shift+Tab. Use the close button or click outside to dismiss it. See Popover for the Fluent widget example."))));
        var factory = sample.Source;
        sample.BindSource(() => (Mode, InsideCount, OutsideCount, Visible), state =>
            $"var Mode = UiPopupInputMode.{state.Mode};\nvar InsideCount = {state.InsideCount};\nvar OutsideCount = {state.OutsideCount};\n"
            + $"var _requestOpen = {GallerySample.Boolean(state.Visible)};\nvar _requestClose = false;\nvar Visible = false;\n\n"
            + "using " + popup.Source + "\n\n" + render.Source + "\n\n" + factory
            + "\n\n// Render example, then invoke renderPopup(ui) after leaving its content clip.");
        Samples = [sample];
        _root = sample;
    }
    public void Render(UiImmediateContext ui)
    {
        _root.Render(ui);
        _renderPopup(ui);
    }
    public void Dispose()
    {
        _root.Dispose();
        _popup.Dispose();
    }
}
