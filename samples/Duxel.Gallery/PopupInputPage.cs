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
    public string Description => "비모달 팝업과 키보드 입력을 독점하는 팝업의 이동·실행·닫기를 비교합니다.";
    public IReadOnlyList<IGallerySample> Samples { get; }
    UiWidget IObserverNaturalPage.MeasureRoot => _root;
    void IObserverNaturalPage.RenderOverlays(UiImmediateContext ui) => _renderPopup(ui);

    internal PopupInputPage()
    {
        var popup = GallerySample.Define("_popup", () => Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Button("팝업 안 명령", () => InsideCount++),
            Fluent.Button("팝업 안 다음"),
            Fluent.Button("입력 팝업 닫기", () => _requestClose = true)));
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
            Fluent.Body1("비모달 팝업에서는 Tab으로 바깥 위젯까지 이동할 수 있습니다. 키보드 입력을 독점하는 모드에서는 팝업 안에서만 이동합니다."),
            Fluent.Button("팝업 입력 모드 전환", () => Mode = Mode == UiPopupInputMode.NonModal ? UiPopupInputMode.Exclusive : UiPopupInputMode.NonModal),
            Fluent.Button("입력 팝업 열기", () => _requestOpen = true),
            Fluent.Button("팝업 바깥 명령", () => OutsideCount++),
            Fluent.Body1(() => $"팝업 안 실행 : {InsideCount} / 바깥 실행 : {OutsideCount}"),
            Fluent.Body1(() => Mode == UiPopupInputMode.NonModal ? "현재 모드 : 비모달" : "현재 모드 : 키보드 독점"),
            Fluent.Body1("팝업 안을 클릭한 뒤 Tab·Shift+Tab을 확인하세요. 닫기 버튼 또는 바깥 클릭으로 닫습니다. 위젯을 넣은 Fluent Popover 예제는 Popover 메뉴에서 확인하세요.")));
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
