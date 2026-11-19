using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class MeasurementForm : UiWidget
{
    private readonly UiWidget _root;
    private readonly Input _input;
    private readonly Divider _divider;
    private readonly Menu _menu;
    private readonly Field _field;
    private readonly UiTabList<UiFluentTabListBehavior, TabPresentation> _tabs;
    private readonly Textarea _textarea;
    private readonly Tooltip _tooltip;
    private readonly Delegated _delegated;
    private readonly FieldInput _fieldInput;
    private readonly TooltipTarget _tooltipTarget;
    internal int Measurements => _input.Measurements + _divider.Measurements + _menu.Measurements + _field.Measurements;
    internal int TabMeasurements => TabPresentation.Measurements;
    internal int DelegatedMeasurements => _textarea.Measurements + _tooltip.Measurements + _delegated.Measurements;
    internal int OwnedSizingMeasurements => _fieldInput.Measurements + _tooltipTarget.Measurements;
    internal bool TooltipOpen => _tooltip.IsOpen;
    internal UiTabListSize TabSize { get => _tabs.Options.Size; set => _tabs.Options = _tabs.Options with { Size = value }; }

    internal MeasurementForm(UiState<string> formValue, UiState<bool> formChecked, UiState<string?> tabValue, UiState<string> memo)
    {
        _input = new(formValue.Binding);
        _divider = new();
        _menu = new(formChecked.Binding);
        _fieldInput = new(formValue.Binding);
        _field = new(_fieldInput);
        _field.Options = new() { Label = "재사용 필드", Hint = "배치 크기를 재사용하더라도 값 변경과 편집 이력은 계속 처리합니다." };
        _tabs = new("재사용 측정 탭", tabValue.Binding,
            new UiTabOption[] { new("a", "측정 탭 Alpha"), new("b", "측정 탭 Beta"), new("c", "측정 탭 Gamma") });
        _textarea = new(memo.Binding);
        _tooltipTarget = new();
        _tooltip = new(_tooltipTarget);
        _delegated = new(_tooltip);
        _root = Dux.SizedBox(Dux.Column(new(Spacing : 12, MainAxisSize : UiMainAxisSize.Min),
            _input, _divider, _menu, _field, _tabs, _textarea, _delegated), 420, 430);
        OwnChild("root", _root);
    }
    internal void ToggleTabSize() => TabSize = TabSize == UiTabListSize.Medium ? UiTabListSize.Small : UiTabListSize.Medium;
    protected override bool CanRetainMeasurement => CanRetainOwnedMeasurement(_root);
    protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
    private sealed class Input(UiBinding<string> value) : UiInput<UiFluentTextInputBehavior, UiFluentInputPresentation<UiFluentMediumInputSize, UiFluentOutlineInput>>("재사용 폼 입력", value, 128, 300)
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class Divider() : UiDivider<UiFluentDividerPresentation>("재사용 구분선")
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class Menu(UiBinding<bool> value) : UiMenu<UiFluentMenuPresentation>("재사용 폼 메뉴", [UiMenuOption.Checkbox("check", "재사용 폼 체크", value)], static _ => { })
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class Field(UiWidget control) : UiField<UiLabelFieldBehavior, UiFluentFieldPresentation<UiFluentMediumFieldSize, UiVerticalFieldLayout>>(control)
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private readonly struct TabPresentation : IUiTabListPresentation
    {
        public static bool CanRetainMeasurement => true;
        internal static int Measurements;
        public static UiVector2 MeasureTab(UiControlContext context, string label, bool selected, UiTabListOptions options)
        {
            Measurements++;
            return UiFluentTabListPresentation.MeasureTab(context, label, selected, options);
        }
        public static void PaintTab(UiControlContext context, string label, UiRect bounds, bool selected, UiButtonInteraction interaction, UiTabListOptions options) =>
            UiFluentTabListPresentation.PaintTab(context, label, bounds, selected, interaction, options);
    }
    private sealed class Textarea(UiBinding<string> value) : UiMultilineInput<UiFluentMultilineInputBehavior,
        UiFluentTextareaPresentation<UiFluentMediumTextareaSize, UiFluentOutlineInput>>("재사용 여러 줄", value, width : 360, height : 64)
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class Tooltip(UiWidget target) : UiTooltip<UiHoverFocusTooltipBehavior, UiFluentTooltipPresentation>(target,
        "대상의 측정 결과를 재사용하더라도, 설정한 지연 시간 뒤에 설명을 표시합니다.", new() { ShowDelaySeconds = .05 })
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class Delegated : UiWidget
    {
        private readonly UiWidget _root;
        internal int Measurements;
        internal Delegated(UiWidget root) { _root = root; OwnChild("root", root); }
        protected override bool CanRetainMeasurement => CanRetainOwnedMeasurement(_root);
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override IUiView Build(UiBuildContext context) => context.Child("root", () => _root);
    }
    private sealed class FieldInput(UiBinding<string> value) : UiFluentInput("재사용 필드 입력", value, 128)
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
    private sealed class TooltipTarget() : UiFluentButton("재사용 도구 설명 대상")
    {
        internal int Measurements;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
    }
}
