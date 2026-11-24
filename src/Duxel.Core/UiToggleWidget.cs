namespace Duxel.Core;

/// <summary>Local indicator and label layout, independent of input and value storage.</summary>
public readonly record struct UiToggleLayout(UiVector2 Size,UiRect Indicator,UiVector2 LabelOffset)
{
    /// <summary>Centers the unchanged indicator/label composition in a new control allocation.</summary>
    public UiToggleLayout CenterContent(UiVector2 allocatedSize)
    {
        var offset=(allocatedSize.Y-Size.Y)/2;
        return this with{Size=allocatedSize,Indicator=Indicator with{Y=Indicator.Y+offset},LabelOffset=LabelOffset with{Y=LabelOffset.Y+offset}};
    }

    internal UiToggleLayout MirrorHorizontally(float labelWidth) => this with
    {
        Indicator = Indicator with { X = Size.X - Indicator.X - Indicator.Width },
        LabelOffset = LabelOffset with { X = Size.X - LabelOffset.X - labelWidth }
    };
}

public interface IUiToggleValue<T>
{
    static abstract bool? Checked(T value);
    static abstract T Activate(T value);
}
public readonly struct UiBooleanToggleValue:IUiToggleValue<bool>
{
    public static bool? Checked(bool value)=>value;
    public static bool Activate(bool value)=>!value;
}
public readonly struct UiNullableToggleValue:IUiToggleValue<bool?>
{
    public static bool? Checked(bool? value)=>value;
    public static bool? Activate(bool? value)=>value!=true;
}
public interface IUiTogglePresentation
{
    /// <summary>Measurement depends only on configured properties, constraints and the current text environment.</summary>
    static virtual bool CanRetainMeasurement => false;
    static virtual UiAccessibilityRole AccessibilityRole => UiAccessibilityRole.Checkbox;
    static virtual UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>default;
    static virtual (string Text, UiVector2 Size) PrepareLabel(UiControlContext context, string text, UiVector2 natural, float maxWidth)
        => (text, natural);
    static abstract UiToggleLayout Measure(UiControlContext context,UiVector2 textSize,bool hasLabel);
    static virtual UiToggleLayout Arrange(UiControlContext context,UiToggleLayout layout,UiVector2 allocatedSize)
        =>throw new NotSupportedException("This Toggle presentation must implement Arrange to accept a different parent allocation.");
    static abstract void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,
        UiToggleLayout layout,UiButtonInteraction interaction,bool? value);
}

public class UiToggle<TValue,TValuePolicy,TBehavior,TPresentation>:UiControlWidget, IUiLabelTarget
    where TValuePolicy:IUiToggleValue<TValue>
    where TBehavior:IUiButtonBehavior
    where TPresentation:IUiTogglePresentation
{
    private string _label;
    private UiBinding<TValue>? _value;
    private bool _disabledFocusable;
    private bool _reducedMotion;
    public UiToggle(string label,UiBinding<TValue> value,UiWidgetIdentity identity=UiWidgetIdentity.Instance):base(identity)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(value);
        _label=label;_value=value;
    }
    public string Label
    {
        get=>_label;
        set{ThrowIfDisposed();ArgumentNullException.ThrowIfNull(value);if(string.Equals(_label,value,StringComparison.Ordinal))return;_label=value;RequestRebuild();}
    }
    public UiBinding<TValue> Value=>_value??throw new ObjectDisposedException(nameof(UiToggle<TValue,TValuePolicy,TBehavior,TPresentation>));

    /// <summary>Disables value changes while keeping the control in the focus order of an enabled parent.</summary>
    public bool DisabledFocusable
    {
        get => _disabledFocusable;
        set
        {
            ThrowIfDisposed();
            if (_disabledFocusable == value)
                return;
            _disabledFocusable = value;
            RequestRebuild();
        }
    }

    /// <summary>Suppresses transitions in toggle presentations that support reduced motion.</summary>
    public bool ReducedMotion
    {
        get => _reducedMotion;
        set
        {
            ThrowIfDisposed();
            if (_reducedMotion == value)
                return;
            _reducedMotion = value;
            RequestRebuild();
        }
    }

    protected virtual void OnChanged(TValue value)=>Value.Value=value;
    private protected virtual UiSwitchIcons? SwitchIcons => null;
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context)=>context.MeasureToggle<TPresentation>(Label);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var value=_value!.Value;
        var focusOnly = DisabledFocusable && ui.IsControlEnabled;
        if (focusOnly)
            ui.PushItemFlag(UiItemFlags.Disabled, true);
        try
        {
            if (ui.Toggle<TValue,TValuePolicy,TBehavior,TPresentation>(PrimaryInputId(Label), Label, ref value,
                constrainLayout: true, focusOnly: focusOnly, reducedMotion: ReducedMotion, switchIcons: SwitchIcons))
                OnChanged(value);
        }
        finally
        {
            if (focusOnly)
                ui.PopItemFlag();
        }
    }
    private protected override void ReleaseOwnedResources(){_value=null;_label=string.Empty;base.ReleaseOwnedResources();}
}

internal readonly struct UiConfiguredToggleBehavior:IUiButtonBehavior
{
    public static UiButtonInteraction Process(UiControlContext context,string id,UiRect bounds)=>context.LegacyToggleSkin.Activation switch
    {
        UiButtonActivation.Release=>context.ActivateOnRelease(id,bounds),
        UiButtonActivation.Press=>context.ActivateOnPress(id,bounds),
        _=>throw new InvalidOperationException("Unknown toggle activation policy.")
    };
}
internal readonly struct UiLegacyCheckboxPresentation:IUiTogglePresentation
{
    public static UiToggleLayout Arrange(UiControlContext context,UiToggleLayout layout,UiVector2 allocatedSize)=>layout.CenterContent(allocatedSize);
    public static UiToggleLayout Measure(UiControlContext context,UiVector2 textSize,bool hasLabel)=>context.MeasureLegacyCheckbox(textSize,hasLabel);
    public static void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,
        UiToggleLayout layout,UiButtonInteraction interaction,bool? value)=>context.PaintLegacyCheckbox(text,textSize,bounds,layout,interaction,value);
}
internal readonly struct UiLegacySwitchPresentation:IUiTogglePresentation
{
    public static UiAccessibilityRole AccessibilityRole => UiAccessibilityRole.Switch;
    public static UiToggleLayout Arrange(UiControlContext context,UiToggleLayout layout,UiVector2 allocatedSize)=>layout.CenterContent(allocatedSize);
    public static UiToggleLayout Measure(UiControlContext context,UiVector2 textSize,bool hasLabel)=>context.MeasureLegacySwitch(textSize,hasLabel);
    public static void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,
        UiToggleLayout layout,UiButtonInteraction interaction,bool? value)=>context.PaintLegacySwitch(text,textSize,bounds,layout,interaction,value);
}

public readonly struct UiFluentCheckboxPresentation:IUiTogglePresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiToggleLayout Arrange(UiControlContext context,UiToggleLayout layout,UiVector2 allocatedSize)=>layout.CenterContent(allocatedSize);
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>
        UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize,UiFluentSquareCheckboxShape,UiCheckboxLabelAfter>.BeginText(context,hasText);
    public static UiToggleLayout Measure(UiControlContext context,UiVector2 textSize,bool hasLabel)=>
        UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize,UiFluentSquareCheckboxShape,UiCheckboxLabelAfter>.Measure(context,textSize,hasLabel);
    public static void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,
        UiToggleLayout layout,UiButtonInteraction interaction,bool? value)=>
        UiFluentCheckboxPresentation<UiFluentMediumCheckboxSize,UiFluentSquareCheckboxShape,UiCheckboxLabelAfter>.Paint(context,text,textSize,bounds,layout,interaction,value);
}
public readonly struct UiFluentSwitchPresentation:IUiTogglePresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiAccessibilityRole AccessibilityRole => UiAccessibilityRole.Switch;
    public static UiToggleLayout Arrange(UiControlContext context,UiToggleLayout layout,UiVector2 allocatedSize)=>layout.CenterContent(allocatedSize);
    public static UiControlTypographyScope BeginText(UiControlContext context,bool hasText)=>
        UiFluentSwitchPresentation<UiFluentMediumSwitchSize,UiSwitchLabelAfter>.BeginText(context,hasText);
    public static UiToggleLayout Measure(UiControlContext context,UiVector2 textSize,bool hasLabel)=>
        UiFluentSwitchPresentation<UiFluentMediumSwitchSize,UiSwitchLabelAfter>.Measure(context,textSize,hasLabel);
    public static void Paint(UiControlContext context,string text,UiVector2 textSize,UiRect bounds,
        UiToggleLayout layout,UiButtonInteraction interaction,bool? value)=>
        UiFluentSwitchPresentation<UiFluentMediumSwitchSize,UiSwitchLabelAfter>.Paint(context,text,textSize,bounds,layout,interaction,value);
}
