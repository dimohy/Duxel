namespace Duxel.Core;

/// <summary>Controls discovery in the generated standard component catalog, not user extensibility.</summary>
public enum UiComponentExposure { Fluent, Infrastructure, Legacy }

[AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct|AttributeTargets.Method|AttributeTargets.Property|AttributeTargets.Enum, Inherited = false)]
public sealed class UiComponentAttribute(string name, UiComponentExposure exposure) : Attribute
{
    public string Name { get; } = name;
    public UiComponentExposure Exposure { get; } = exposure;
}

[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentButtonPresentation))]
[UiComponent("Button", UiComponentExposure.Fluent)]
public partial class UiFluentButton;

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiBooleanToggleValue),typeof(UiSpaceButtonBehavior),typeof(UiFluentCheckboxPresentation))]
[UiComponent("Checkbox",UiComponentExposure.Fluent)]
public partial class UiFluentCheckbox;

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool?),typeof(UiNullableToggleValue),typeof(UiSpaceButtonBehavior),typeof(UiFluentCheckboxPresentation))]
[UiComponent("Checkbox",UiComponentExposure.Fluent)]
public partial class UiFluentMixedCheckbox;

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiBooleanToggleValue),typeof(UiSpaceButtonBehavior),typeof(UiFluentSwitchPresentation))]
[UiComponent("Switch",UiComponentExposure.Fluent)]
public partial class UiFluentSwitch;

[UiWidgetStyle(typeof(UiToggle<,,,>),typeof(bool),typeof(UiBooleanToggleValue),typeof(UiReleaseButtonBehavior),typeof(UiFluentToggleButtonPresentation))]
[UiComponent("ToggleButton",UiComponentExposure.Fluent)]
public partial class UiFluentToggleButton;

[UiWidgetStyle(typeof(UiText<>),typeof(UiFluentTextPresentation))]
[UiComponent("Text",UiComponentExposure.Fluent)]
public partial class UiFluentText;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentTextPresentation))]
[UiComponent("Text",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicText;

[UiWidgetStyle(typeof(UiText<>),typeof(UiFluentWrappedTextPresentation))]
public partial class UiFluentWrappedText;

[UiWidgetStyle(typeof(UiText<,,>),typeof(Func<string>),typeof(UiGetterTextSource),typeof(UiFluentWrappedTextPresentation))]
public partial class UiFluentDynamicWrappedText;

[UiWidgetStyle(typeof(UiProgress<,>),typeof(UiFluentProgressBehavior),typeof(UiFluentProgressPresentation))]
[UiComponent("ProgressBar",UiComponentExposure.Fluent)]
public partial class UiFluentProgressBar;

[UiWidgetStyle(typeof(UiProgress<,,,>),typeof(Func<UiProgressValue>),typeof(UiGetterProgressSource),typeof(UiFluentProgressBehavior),typeof(UiFluentProgressPresentation))]
[UiComponent("ProgressBar",UiComponentExposure.Fluent)]
public partial class UiFluentDynamicProgressBar;

[UiWidgetStyle(typeof(UiSlider<,>),typeof(UiFluentSliderBehavior),typeof(UiFluentSliderPresentation))]
[UiComponent("Slider",UiComponentExposure.Fluent)]
public partial class UiFluentSlider;
