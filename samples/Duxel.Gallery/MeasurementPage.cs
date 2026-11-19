using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class MeasurementPage : UiWidget, IGallerySourcePage
{
    private readonly UiState<bool?> _checked = new(null);
    private readonly UiState<float> _value = new(.25f);
    private readonly UiState<string?> _choice = new("a");
    private readonly UiState<string> _formValue = new("편집하세요");
    private readonly UiState<bool> _formChecked = new(false);
    private readonly UiState<string?> _tabValue = new("a");
    private readonly UiState<string> _memo = new("첫 줄\n둘째 줄");
    private readonly MeasurementPrimitives _primitives;
    private readonly MeasurementForm _form;
    private readonly UiWidget _root;
    public IReadOnlyList<IGallerySample> Samples { get; }
    internal int Measurements => _primitives.Measurements;
    internal bool? Checked => _checked.Value;
    internal int ProgressReads => _primitives.ProgressReads;
    internal float LastProgressValue => _primitives.LastProgressValue;
    internal int Navigations => _primitives.Navigations;
    internal UiRect LinkBounds => _primitives.LinkBounds;
    internal string FormValue => _formValue.Value;
    internal int FormMeasurements => _form.Measurements;
    internal string? TabValue => _tabValue.Value;
    internal int TabMeasurements => _form.TabMeasurements;
    internal string MemoValue => _memo.Value;
    internal bool MeasurementTooltipOpen => _form.TooltipOpen;
    internal int DelegatedMeasurements => _form.DelegatedMeasurements;
    internal int OwnedSizingMeasurements => _form.OwnedSizingMeasurements;

    internal MeasurementPage()
    {
        var primitivesDefinition = GallerySample.Define("primitives", () => new MeasurementPrimitives(_checked, _value, _choice));
        var primitives = _primitives = primitivesDefinition.Value;
        var primitiveSample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing : 12, RunSpacing : 12),
                Fluent.Button("프레임 확인", static () => { }),
                Fluent.Button("측정 라벨 변경", primitives.ToggleLabel),
                Fluent.Button("측정 너비 변경", primitives.ToggleWidth),
                Fluent.Button("측정 본문 변경", primitives.ChangeBody)), primitives));
        var primitiveFactory = primitiveSample.Source;
        primitiveSample.BindSource(() => (_checked.Value, Slider : _value.Value, Choice : _choice.Value,
            primitives.CheckboxLabel, primitives.BodyText, primitives.BadgeCount, primitives.SpinnerLabel, primitives.Width, primitives.Navigations), state =>
        {
            var check = state.Value is { } value ? GallerySample.Boolean(value) : "null";
            return $"var _checked = new UiState<bool?>({check});\nvar _value = new UiState<float>({GallerySample.Number(state.Slider)});\n"
                + $"var _choice = new UiState<string?>({GallerySample.String(state.Choice)});\n\n" + primitivesDefinition.Source
                + $"\nprimitives.CheckboxLabel = {GallerySample.String(state.CheckboxLabel)};\nprimitives.BodyText = {GallerySample.String(state.BodyText)};\n"
                + $"primitives.BadgeCount = {state.BadgeCount};\nprimitives.SpinnerLabel = {GallerySample.String(state.SpinnerLabel)};\n"
                + $"primitives.Width = {GallerySample.Number(state.Width)};\nprimitives.Navigations = {state.Navigations};\n\n" + primitiveFactory
                + "\n\n" + GalleryPageSource.Declaration(nameof(MeasurementPrimitives));
        });
        var formDefinition = GallerySample.Define("form", () => new MeasurementForm(_formValue, _formChecked, _tabValue, _memo));
        var form = _form = formDefinition.Value;
        var formSample = GallerySample.Create(() => Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Button("탭 크기 변경", form.ToggleTabSize), form));
        var formFactory = formSample.Source;
        formSample.BindSource(() => (_formValue.Value, Checked : _formChecked.Value, Tab : _tabValue.Value, Memo : _memo.Value, form.TabSize), state =>
            $"var _formValue = new UiState<string>({GallerySample.String(state.Value)});\nvar _formChecked = new UiState<bool>({GallerySample.Boolean(state.Checked)});\n"
            + $"var _tabValue = new UiState<string?>({GallerySample.String(state.Tab)});\nvar _memo = new UiState<string>({GallerySample.String(state.Memo)});\n\n"
            + formDefinition.Source + $"\nform.TabSize = UiTabListSize.{state.TabSize};\n\n" + formFactory
            + "\n\n" + GalleryPageSource.Declaration(nameof(MeasurementForm)));
        Samples = [primitiveSample, formSample];
        _root = Dux.Column(new(Spacing : 16, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Body1("체크·슬라이더·선택·링크를 조작하세요. 자식 측정 수가 그대로여도 화면과 진행률은 바뀝니다."),
            Fluent.Body1("라벨이나 부모 너비를 바꾸면 배치를 다시 계산합니다. 이 수치는 FPS나 GPU 작업량이 아닙니다."),
            Fluent.Body1(() => $"자식 측정 : {Measurements} / 값 : {_value.Value:F2} / 선택 : {_choice.Value} / 링크 : {Navigations}"),
            Fluent.Body1(() => $"폼 부모 측정 : {FormMeasurements} / 입력 : {_formValue.Value} / 메뉴 체크 : {_formChecked.Value}"),
            Fluent.Body1(() => $"탭 자연 측정(프로세스) : {TabMeasurements} / 선택 : {_tabValue.Value}"),
            Fluent.Body1(() => $"위임 부모 측정 : {DelegatedMeasurements} / 메모 길이 : {_memo.Value.Length}"),
            Fluent.Body1(() => $"소유 자식 크기 측정 : {OwnedSizingMeasurements}"),
            Dux.Wrap(new(Spacing : 16, RunSpacing : 16),
                Dux.ConstrainedBox(primitiveSample, new(0, 420, 0, float.PositiveInfinity)),
                Dux.ConstrainedBox(formSample, new(0, 420, 0, float.PositiveInfinity))));
        OwnChild("root", _root);
    }
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
}
