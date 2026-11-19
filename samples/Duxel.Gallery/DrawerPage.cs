using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class DrawerPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private readonly UiState<string> _memo = new("패널을 닫았다 열어도 편집 내용을 유지합니다.");
    private readonly UiState<bool> _inlineOpen = new(true);
    private readonly UiFluentDrawer _overlay;
    private readonly UiFluentDrawer _inline;
    private readonly UiWidget _root;
    private readonly Action<UiImmediateContext> _renderOverlay;
    private int _saves;
    public string Title => "보조 패널";
    public string Description => "화면 가장자리에서 패널을 열어 보세요. 패널 위치와 모달 여부를 바꾸거나 내용을 스크롤하고, 닫은 뒤 포커스가 돌아오는지 확인할 수 있습니다.";
    internal UiFluentDrawer Overlay => _overlay;
    internal int Saves => _saves;
    public IReadOnlyList<IGallerySample> Samples { get; }
    internal DrawerPage()
    {
        var contentsDefinition = GallerySample.Define<Func<string, UiWidget>>("contents", () => (string input) => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            [Fluent.Input(input, _memo, 4096, 260),
             .. Enumerable.Range(1, 24).Select(index => Fluent.Body1("패널 내용 " + index)).ToArray()]));
        var contents = contentsDefinition.Value;
        var overlayOptions = new UiDrawerOptions(Position : UiDrawerPosition.End, CloseLabel : "보조 패널 닫기",
            Description : "편집 내용을 유지하는 보조 작업 패널");
        var overlayDefinition = GallerySample.Define("_overlay", () =>
        {
            UiFluentDrawer? drawer = null;
            drawer = Fluent.Drawer("보조 작업", contents("패널 메모"),
                [Fluent.Button("패널 저장", () => { _saves++; drawer!.Close(); }), Fluent.Button("패널 닫기", () => drawer!.Close())],
                overlayOptions);
            return drawer;
        });
        _overlay = overlayDefinition.Value;
        var renderDefinition = GallerySample.Define<Action<UiImmediateContext>>("renderOverlays", () => (UiImmediateContext ui) => _overlay.Render(ui));
        _renderOverlay = renderDefinition.Value;
        var overlaySample = GallerySample.Create(() => Fluent.Button("보조 패널 열기", _overlay.Show));
        var overlayFactory = overlaySample.Source;
        overlaySample.BindSource(() => (Memo : _memo.Value, Saves : _saves, _overlay.Options, _overlay.IsOpen), state =>
            $"var _memo = new UiState<string>({GallerySample.String(state.Memo)});\nvar _saves = {state.Saves};\n"
            + $"var overlayOptions = {SourceOptions(state.Options)};\n\n" + contentsDefinition.Source + "\n\n" + overlayDefinition.Source + "\n\n"
            + overlayFactory + (state.IsOpen ? "\n_overlay.Show();" : string.Empty) + "\n\n" + renderDefinition.Source
            + "\n// Invoke renderOverlays(ui) after rendering example in each frame.");
        var inlineOptions = new UiDrawerOptions(Type : UiDrawerType.Inline, InlineCrossExtent : 400, Separator : true, CloseLabel : "인라인 패널 닫기");
        var inlineSample = GallerySample.Create(() =>
        {
            var drawer = Fluent.Drawer("인라인 패널", contents("인라인 메모"),
                [Fluent.Button("인라인 닫기", () => _inlineOpen.Value = false)], inlineOptions);
            drawer.Open = _inlineOpen.Binding;
            return drawer;
        });
        _inline = inlineSample.Preview;
        var inlineFactory = inlineSample.Source;
        inlineSample.BindSource(() => (Memo : _memo.Value, Open : _inlineOpen.Value, _inline.Options), state =>
            $"var _memo = new UiState<string>({GallerySample.String(state.Memo)});\nvar _inlineOpen = new UiState<bool>({GallerySample.Boolean(state.Open)});\n"
            + $"var inlineOptions = {SourceOptions(state.Options)};\n\n" + contentsDefinition.Source + "\n\n" + inlineFactory);
        Samples = [overlaySample, inlineSample];
        _root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1("오버레이는 화면 가장자리에서 열립니다. 인라인 패널은 본문과 나란히 배치할 수 있습니다."),
            Fluent.Subtitle2("오버레이 패널"),
            Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                Fluent.Button("패널 위치 변경", () => _overlay.Options = _overlay.Options with
                    { Position = (UiDrawerPosition)(((int)_overlay.Options.Position + 1) % 3) }),
                Fluent.Button("패널 크기 변경", () => _overlay.Options = _overlay.Options with
                    { Size = (UiDrawerSize)(((int)_overlay.Options.Size + 1) % 4) })),
            Fluent.Switch("패널 모달", new UiBinding<bool>(() => _overlay.Options.Modal,
                value => _overlay.Options = _overlay.Options with { Modal = value })),
            Fluent.Switch("패널 오른쪽에서 왼쪽", new UiBinding<bool>(() => _overlay.Options.RightToLeft,
                value => _overlay.Options = _overlay.Options with { RightToLeft = value })),
            Fluent.Body1(() => "저장 횟수 : " + _saves),
            Fluent.Caption1("화면 높이가 충분하면 제목과 하단 버튼은 고정되고 본문만 스크롤됩니다. 화면이 낮아지면 제목과 버튼을 포함한 전체 내용을 스크롤할 수 있습니다."),
            overlaySample,
            Fluent.Subtitle2("인라인 패널"),
            Fluent.Switch("인라인 패널 표시", _inlineOpen),
            inlineSample));
    }
    private static string SourceOptions(UiDrawerOptions value)
    {
        static string Number(float? number) => number is { } n ? n.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "f" : "null";
        return $"new UiDrawerOptions(Type : UiDrawerType.{value.Type}, Position : UiDrawerPosition.{value.Position}, Size : UiDrawerSize.{value.Size},\n"
            + $"    Extent : {Number(value.Extent)}, InlineCrossExtent : {Number(value.InlineCrossExtent)}, Modal : {GallerySample.Boolean(value.Modal)},\n"
            + $"    LightDismiss : {GallerySample.Boolean(value.LightDismiss)}, ShowCloseButton : {GallerySample.Boolean(value.ShowCloseButton)}, CloseLabel : {GallerySample.String(value.CloseLabel)},\n"
            + $"    Description : {GallerySample.String(value.Description)}, RightToLeft : {GallerySample.Boolean(value.RightToLeft)},\n"
            + $"    StickyHeaderFooter : {GallerySample.Boolean(value.StickyHeaderFooter)}, MotionEnabled : {GallerySample.Boolean(value.MotionEnabled)}, Separator : {GallerySample.Boolean(value.Separator)}, ScrollBody : {GallerySample.Boolean(value.ScrollBody)})";
    }
    UiWidget IObserverNaturalPage.MeasureRoot => _root;
    void IObserverNaturalPage.RenderOverlays(UiImmediateContext ui) => _renderOverlay(ui);
    public void Render(UiImmediateContext ui) { _root.Render(ui); _renderOverlay(ui); }
    public void Dispose() { _overlay.Dispose(); _root.Dispose(); }
}
