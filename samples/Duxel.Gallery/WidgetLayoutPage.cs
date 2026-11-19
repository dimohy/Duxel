using System.Globalization;
using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class WidgetLayoutPage : IObserverPage, IObserverNaturalPage, IGallerySourcePage
{
    private Content? _content;
    public string Title => "위젯 배치";
    public string Description => "위젯에 여백을 주거나 회전·확대해 보세요. 변환한 위젯도 입력을 받으며, 팝업은 회전하지 않고 화면 방향에 맞춰 열립니다.";
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new()).Samples;
    internal UiEdgeInsets AppliedMargin => _content?.AppliedMargin ?? default;
    internal int Activations => _content?.Activations ?? 0;
    internal UiWidgetTransform AppliedTransform => _content?.AppliedTransform ?? UiWidgetTransform.Identity;
    UiWidget IObserverNaturalPage.MeasureRoot => (_content ??= new()).Root;
    void IObserverNaturalPage.PrepareContent(UiImmediateContext ui) => (_content ??= new()).Prepare(ui);
    public void Render(UiImmediateContext ui)
    {
        var content = _content ??= new();
        content.Prepare(ui);
        content.Root.Render(ui);
    }
    public void Dispose() => _content?.Dispose();

    private sealed class Content : IDisposable
    {
        private readonly UiState<float> _left = new(8), _top = new(8), _right = new(8), _bottom = new(8);
        private readonly UiState<bool?> _checked = new(false);
        private readonly UiState<float> _value = new(.5f);
        private readonly UiState<float> _rotation = new(0), _scale = new(1);
        private readonly UiState<bool?> _visual = new(false);
        private readonly UiState<string> _text = new("한글 입력");
        private readonly UiState<int> _activations = new(0);
        private int _originIndex = 1;
        private UiWidgetTransform _transform = UiWidgetTransform.Identity;
        private UiVector2 _buttonSize;
        private readonly UiWidget[] _targets;
        private readonly List<IGallerySample> _samples = [];
        internal IReadOnlyList<IGallerySample> Samples => _samples;
        internal UiWidget Root { get; }
        internal int Activations => _activations.Value;
        internal UiEdgeInsets AppliedMargin => _targets[0].Margin;
        internal UiWidgetTransform AppliedTransform => _transform;

        internal Content()
        {
            var button = Record(GallerySample.Create(() =>
            {
                var button = Fluent.Button("변환 버튼 실행", () => _activations.Value++);
                button.Size = new(140, 32);
                return button;
            }), () => _activations.Value, value => $"var _activations = new UiState<int>({value});");
            var checkbox = Record(GallerySample.Create(() => Fluent.Checkbox("여백 체크박스", _checked.Binding)),
                () => _checked.Value, value => $"var _checked = new UiState<bool?>({(value is null ? "null" : GallerySample.Boolean(value.Value))});");
            var slider = Record(GallerySample.Create(() =>
            {
                var slider = Fluent.Slider("여백 예제 값", _value.Binding, 0, 1, .01f);
                slider.Size = new(180, 32);
                slider.Options = slider.Options with { ShowStepMarkers = false };
                return slider;
            }), () => _value.Value, value => $"var _value = new UiState<float>({Number(value)});");
            var input = Record(GallerySample.Create(() => Fluent.Input("변환 입력", _text.Binding, 200, 200)),
                () => _text.Value, value => $"var _text = new UiState<string>({GallerySample.String(value)});");
            var popover = Record(GallerySample.Create(() => Fluent.Popover("변환 팝업",
                Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
                    Fluent.Body1("팝업은 화면 좌표로 열립니다."), Fluent.Button("팝업 안에서 실행", () => _activations.Value++)),
                options : new(Width : 240))), () => _activations.Value, value => $"var _activations = new UiState<int>({value});");
            var menu = Record(GallerySample.Create(() => new UiMenu<UiFluentMenuPresentation>("변환 메뉴",
                [new("run", "메뉴에서 실행")], _ => _activations.Value++)),
                () => _activations.Value, value => $"var _activations = new UiState<int>({value});");
            var tooltip = Record(GallerySample.Create(() => Fluent.Tooltip(Fluent.Button("변환 도구 설명"),
                "회전한 대상 옆에 화면 기준으로 표시합니다.")), () => 0, _ => string.Empty);
            var legacy = Record(GallerySample.Create(() => new LegacyPortalPreview(() => _activations.Value++)),
                () => _activations.Value, value => $"var _activations = new UiState<int>({value});",
                GalleryPageSource.Declaration(nameof(LegacyPortalPreview)));
            _targets = [button.Preview, checkbox.Preview, slider.Preview, input.Preview,
                popover.Preview, menu.Preview, tooltip.Preview, legacy.Preview];
            var controls = Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                Fluent.Body1("위젯의 상하좌우에 여백을 줍니다. 여백을 눌러도 버튼은 실행되지 않습니다."),
                Dux.Wrap(new(Spacing : 16, RunSpacing : 12),
                    Edge("공통 여백 - 왼쪽", _left), Edge("공통 여백 - 위", _top),
                    Edge("공통 여백 - 오른쪽", _right), Edge("공통 여백 - 아래", _bottom)),
                Fluent.Button("공통 여백 초기화", () => _left.Value = _top.Value = _right.Value = _bottom.Value = 0),
                Dux.Wrap(new(Spacing : 16, RunSpacing : 12),
                    Numeric("공통 회전", _rotation, -180, 180, 1), Numeric("공통 확대", _scale, .25f, 3, .05f),
                    Fluent.Checkbox("시각 변환 (배치 크기 유지)", _visual.Binding),
                    Fluent.Button("회전 중심 전환 (시각 변환)", () => _originIndex = (_originIndex + 1) % 3),
                    Fluent.Button("공통 변환 초기화", () => { _rotation.Value = 0; _scale.Value = 1; _visual.Value = false; _originIndex = 1; })));
            Root = Dux.ScrollView(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
                controls,
                Fluent.Body1(() => $"여백 : 왼쪽 {_left.Value:F0} / 위 {_top.Value:F0} / 오른쪽 {_right.Value:F0} / 아래 {_bottom.Value:F0} px"),
                Fluent.Body1(() => $"버튼 배치 : {_buttonSize.X:F0} × {_buttonSize.Y:F0} px"),
                Fluent.Body1(() => $"회전 : {_rotation.Value:F0}° / 확대 : {_scale.Value:F2} / 중심 : {(_originIndex == 0 ? "왼쪽 위" : _originIndex == 1 ? "중앙" : "오른쪽 아래")} / {(_transform.Mode == UiWidgetTransformMode.Layout ? "변환 크기로 배치" : "원래 배치 유지")}"),
                button, checkbox, slider, input, popover, menu, tooltip, legacy,
                Fluent.Body1(() => $"버튼 실행 : {Activations} / 체크 : {(_checked.Value == true ? "선택" : "해제")} / 값 : {_value.Value:F2}"),
                Fluent.Body1(() => $"편집 : {_text.Value}")));
        }

        private GallerySample<T> Record<T, TState>(GallerySample<T> sample, Func<TState> read, Func<TState, string> setup,
            string declaration = "") where T : UiWidget
        {
            var factory = sample.Source;
            sample.BindSource(() => (State : read(), sample.Preview.Margin, sample.Preview.Transform), state =>
            {
                var margin = state.Margin;
                var transform = state.Transform;
                var origin = transform.Origin is { } pivot ? $"new UiVector2({Number(pivot.X)}, {Number(pivot.Y)})" : "null";
                return setup(state.State) + "\n\n" + factory
                    + $"\nexample.Margin = new UiEdgeInsets({Number(margin.Left)}, {Number(margin.Top)}, {Number(margin.Right)}, {Number(margin.Bottom)});"
                    + $"\nexample.Transform = new UiWidgetTransform(Scale : {Number(transform.Scale)}, RotationDegrees : {Number(transform.RotationDegrees)},\n"
                    + $"    Origin : {origin}, Mode : UiWidgetTransformMode.{transform.Mode});"
                    + (declaration.Length == 0 ? string.Empty : "\n\n" + declaration);
            });
            _samples.Add(sample);
            return sample;
        }
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture) + "f";
        private static UiWidget Edge(string label, UiState<float> state) => Numeric(label, state, 0, 40, 1);
        private static UiWidget Numeric(string label, UiState<float> state, float minimum, float maximum, float step)
        {
            var slider = Fluent.Slider(label, state.Binding, minimum, maximum, step);
            slider.Size = new(220, 32);
            slider.Options = slider.Options with { ShowStepMarkers = false };
            return Dux.Column(new(Spacing : 4, MainAxisSize : UiMainAxisSize.Min), Fluent.Caption1(label), slider);
        }

        internal void Prepare(UiImmediateContext ui)
        {
            var margin = new UiEdgeInsets(_left.Value, _top.Value, _right.Value, _bottom.Value);
            var pivot = _originIndex switch
            {
                0 => new UiVector2(0, 0),
                1 => new(.5f, .5f),
                _ => new(1, 1)
            };
            var mode = _visual.Value == true ? UiWidgetTransformMode.Visual : UiWidgetTransformMode.Layout;
            if (_transform.Scale != _scale.Value || _transform.RotationDegrees != _rotation.Value || _transform.Origin != pivot || _transform.Mode != mode)
                _transform = new(_scale.Value, _rotation.Value, pivot, mode);
            foreach (var target in _targets)
            {
                target.Margin = margin;
                target.Transform = _transform;
            }
            _buttonSize = _targets[0].MeasureConstrained(ui, new UiBoxConstraints());
        }
        public void Dispose() => Root.Dispose();
    }
}
