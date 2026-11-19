using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class DialogPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    internal UiFluentDialog? Preview => _content?.Dialog;
    internal float MotionValue => _content?.Value ?? 0;
    public string Title => "대화 상자";
    public string Description => "모달 창과 비모달 창, 경고 창을 비교해 보세요. 내용을 입력하고 창을 닫으면 원래 버튼으로 포커스가 돌아갑니다.";
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    void IObserverNaturalPage.PrepareContent(UiImmediateContext ui) => (_content ??= new()).Prepare(ui);
    void IObserverNaturalPage.RenderOverlays(UiImmediateContext ui) => _content!.RenderOverlays(ui);
    public void Render(UiImmediateContext ui)
    {
        var content = _content ??= new();
        content.Prepare(ui);
        content.Root.Render(ui);
        content.RenderOverlays(ui);
    }
    public void Dispose() => _content?.Dispose();

    private sealed class Content : IDisposable
    {
        private UiFloatTransition _transition = new(0);
        private UiCubicBezier _curve = new(0, 0, 0, 1);
        private float _target, _value;
        private readonly Action<UiImmediateContext> _prepare, _renderOverlays;
        internal float Value => _value;
        internal UiFluentDialog Dialog { get; }
        internal IReadOnlyList<IGallerySample> Samples { get; }
        internal void Prepare(UiImmediateContext ui) => _prepare(ui);
        internal void RenderOverlays(UiImmediateContext ui) => _renderOverlays(ui);
        internal UiWidget Root
        {
            get;
        }
        internal Content()
        {
            var name = new UiState<string>("");
            var message = new UiState<string>("이름을 입력한 뒤 저장하세요.");
            var open = new UiState<bool>(false);
            var dialogOptions = new UiDialogOptions(Description : "이름을 입력하고 저장하거나 취소합니다.");
            var dialogDefinition = GallerySample.Define("_dialog", () =>
            {
                UiFluentDialog? dialog = null;
                var input = Fluent.Input("이름", name.Binding, 100, 300);
                var save = Fluent.Button("저장", () =>
                {
                    if (string.IsNullOrWhiteSpace(name.Value))
                    {
                        message.Value = "이름을 입력해야 저장할 수 있습니다.";
                        return;
                    }
                    message.Value = $"저장됨 : {name.Value}";
                    dialog!.Close();
                });
                var cancel = Fluent.Button("취소", () => dialog!.Close());
                dialog = Fluent.Dialog("사용자 정보", Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Body1("탭으로 이동하고 한글을 입력해 보세요. 창을 다시 열어도 입력은 유지됩니다."),
                    Fluent.Label("이름", input), input, Fluent.Body1(() => message.Value)), [save, cancel], dialogOptions);
                dialog.Open = open.Binding;
                return dialog;
            });
            var _dialog = Dialog = dialogDefinition.Value;
            var showDefinition = GallerySample.Define<Action<UiDialogType>>("show", () => (UiDialogType type) =>
            {
                _dialog.Options = _dialog.Options with
                {
                    Type = type
                };
                _dialog.Show();
            });
            var show = showDefinition.Value;
            var renderDefinition = GallerySample.Define<Action<UiImmediateContext>>("renderOverlays", () => (UiImmediateContext ui) => _dialog.Render(ui));
            _renderOverlays = renderDefinition.Value;
            var dialogSample = GallerySample.Create(() => Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                Fluent.Button("모달 열기", () => show(UiDialogType.Modal)),
                Fluent.Button("비모달 열기", () => show(UiDialogType.NonModal)),
                Fluent.Button("경고 열기", () => show(UiDialogType.Alert))));
            var dialogFactory = dialogSample.Source;
            dialogSample.BindSource(() => (Name : name.Value, Message : message.Value, Open : open.Value, _dialog.Options), state =>
                $"var name = new UiState<string>({GallerySample.String(state.Name)});\nvar message = new UiState<string>({GallerySample.String(state.Message)});\n"
                + $"var open = new UiState<bool>({GallerySample.Boolean(state.Open)});\nvar dialogOptions = {SourceOptions(state.Options)};\n\n"
                + dialogDefinition.Source + "\n\n" + showDefinition.Source + "\n\n" + dialogFactory + "\n\n" + renderDefinition.Source
                + "\n// Invoke renderOverlays(ui) after rendering example in each frame.");

            var prepareDefinition = GallerySample.Define<Action<UiImmediateContext>>("prepareMotion", () => (UiImmediateContext ui) =>
            {
                _value = ui.AnimateFloat(ref _transition, _target, new(.25) { Curve = _curve });
            });
            _prepare = prepareDefinition.Value;
            UiWidget? motionPreview = null;
            var curveDefinition = GallerySample.Define<Action<UiCubicBezier>>("setCurve", () => (UiCubicBezier curve) =>
            {
                _curve = curve;
                _target = _target == 0 ? 1 : 0;
                motionPreview!.RequestRebuild();
            });
            var setCurve = curveDefinition.Value;
            var motionSample = GallerySample.Create(() =>
            {
                var progress = Fluent.ProgressBar(() => new(_value, 1, ReducedMotion : true));
                progress.Size = new(460, 4);
                motionPreview = progress;
                return Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                    Dux.Wrap(new(Spacing : 8, RunSpacing : 8),
                        Fluent.Button("등장 곡선", () => setCurve(new(0, 0, 0, 1))),
                        Fluent.Button("퇴장 곡선", () => setCurve(new(.8, 0, .78, 1))),
                        Fluent.Button("배경 곡선", () => setCurve(new(.33, 0, .67, 1)))), progress);
            });
            var motionFactory = motionSample.Source;
            motionSample.BindSource(() => (_curve, _target), state =>
                $"var _transition = new UiFloatTransition(0);\nvar _target = {Number(state._target)}f;\nvar _value = 0f;\n"
                + $"var _curve = new UiCubicBezier({Number(state._curve.X1)}d, {Number(state._curve.Y1)}d, {Number(state._curve.X2)}d, {Number(state._curve.Y2)}d);\n"
                + "UiWidget? motionPreview = null;\n\n" + prepareDefinition.Source + "\n\n" + curveDefinition.Source + "\n\n" + motionFactory
                + "\n// Invoke prepareMotion(ui) before rendering example in each frame.");
            Samples = [dialogSample, motionSample];
            Root = Dux.ScrollView(Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1("모달은 배경 입력을 막고, 비모달은 배경과 함께 조작할 수 있습니다. 경고 창은 배경을 눌러도 닫히지 않습니다."),
                Fluent.Body1(() => $"열림 : {open.Value} / 이름 : {name.Value}"),
                Fluent.Caption1("Escape·Tab·Shift+Tab으로 조작하고 저장할 때 입력값을 검사하는지 확인하세요. 창을 닫으면 원래 버튼으로 포커스가 돌아갑니다."),
                dialogSample,
                Fluent.Subtitle2("모션 곡선 미리 보기"),
                Fluent.Body1("버튼을 누르면 250ms 동안 전환합니다. 도중에 방향을 바꾸면 현재 위치에서 이어지며, 전환이 끝나면 더 이상 프레임을 요청하지 않습니다."),
                Fluent.Caption1("여러 위젯이 함께 사용하는 애니메이션 곡선의 예제입니다. 대화 상자는 크기와 투명도가 바뀌며 나타나고, 닫힐 때는 저장해 둔 화면으로 애니메이션을 이어갑니다. 사라지는 도중에 다시 열어도 현재 상태에서 자연스럽게 이어집니다."),
                motionSample));
        }
        private static string Number(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        private static string SourceOptions(UiDialogOptions value) => $"new UiDialogOptions(Type : UiDialogType.{value.Type}, Width : {Number(value.Width)}f,\n"
            + $"    MaximumHeight : {(value.MaximumHeight is { } height ? Number(height) + "f" : "null")}, LightDismiss : {GallerySample.Boolean(value.LightDismiss)},\n"
            + $"    ShowCloseButton : {(value.ShowCloseButton is { } close ? GallerySample.Boolean(close) : "null")}, CloseLabel : {GallerySample.String(value.CloseLabel)},\n"
            + $"    Description : {GallerySample.String(value.Description)}, MotionEnabled : {GallerySample.Boolean(value.MotionEnabled)})";
        public void Dispose() { Dialog.Dispose(); Root.Dispose(); }
    }
}
