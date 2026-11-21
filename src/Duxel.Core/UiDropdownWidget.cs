namespace Duxel.Core;

public enum UiDropdownSize
{
    Medium, Small, Large
}
public readonly record struct UiDropdownOptions(float Width = 250, UiDropdownSize Size = UiDropdownSize.Medium,
    UiInputAppearance Appearance = UiInputAppearance.Outline, string? Placeholder = null,
    bool RightToLeft = false, float? MaximumPopupHeight = null, bool Invalid = false,
    string SelectedCountFormat = "{0} selected", string ExpandLabel = "Show options")
{
    public UiDropdownOptions() : this(250, UiDropdownSize.Medium, UiInputAppearance.Outline, null, false, null, false, "{0} selected", "Show options") { }
    internal UiDropdownOptions WithInheritedDirection(bool rightToLeft) => rightToLeft
        ? this with { RightToLeft = true } : this;
    internal void Validate()
    {
        if (!float.IsFinite(Width) || Width <= 0 || (uint)Size > 2 || (uint)Appearance > 3
            || MaximumPopupHeight is { } maximumHeight && (!float.IsFinite(maximumHeight) || maximumHeight < 40))
            throw new ArgumentOutOfRangeException(nameof(UiDropdownOptions));
        ArgumentException.ThrowIfNullOrWhiteSpace(SelectedCountFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(ExpandLabel);
        if (Placeholder is { } placeholder && UiAccessibilityTree.ContainsControl(placeholder, false))
            throw new ArgumentException("Dropdown placeholder must be single-line text.", nameof(Placeholder));
        _ = string.Format(System.Globalization.CultureInfo.CurrentCulture, SelectedCountFormat, 0);
    }
}
/// <summary>Selects pointer activation for a selection field and its popup options.</summary>
public interface IUiDropdownBehavior : IUiMenuBehavior { }

public readonly struct UiDefaultDropdownBehavior : IUiDropdownBehavior
{
    public static UiButtonActivation TriggerActivation => UiDefaultMenuBehavior.TriggerActivation;
    public static UiButtonActivation OptionActivation => UiDefaultMenuBehavior.OptionActivation;
}

public interface IUiDropdownPresentation : IUiMenuPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiVector2 Measure(UiControlContext context, UiDropdownOptions options);
    static abstract UiRect ExpandBounds(UiRect bounds, UiDropdownOptions options);
    static abstract void Paint(UiControlContext context, UiRect bounds, string text, bool placeholder,
        UiDropdownOptions options, UiButtonInteraction interaction, bool open, string countText);
}

/// <summary>A bound readonly combobox with persistent logical options and a real selection listbox.</summary>
public class UiDropdown<TBehavior, TPresentation> : UiControlWidget, IUiLabelTarget
    where TBehavior : IUiMenuBehavior
    where TPresentation : IUiDropdownPresentation
{
    private UiOptionSelection? _selection;
    private readonly Func<string> _readCountText;
    private readonly UiOwnedMenuState _input = new();
    private UiDropdownOptions _options;
    private int _count = -1;
    private string _countText = string.Empty;
    private string? _countFormat;
    public string Label
    {
        get;
    }
    public bool IsOpen => _input.Menu.Open;
    public bool Multiple => _selection?.Multiple == true;
    public ReadOnlySpan<UiSelectOption> Items => _selection?.Options ?? [];
    public UiDropdownOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    public UiDropdown(string label, UiBinding<string?> selection, UiSelectOption[] items,
        UiDropdownOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _options = options ?? new();
        _options.Validate();
        _selection = new(items, selection);
        Label = label;
        _readCountText = ReadCountText;
    }
    public UiDropdown(string label, UiBinding<string[]> selection, UiSelectOption[] items,
        UiDropdownOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _options = options ?? new();
        _options.Validate();
        _selection = new(items, selection);
        Label = label;
        _readCountText = ReadCountText;
    }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context)
        => context.LayoutConstraints.Constrain(TPresentation.Measure(context, _options.WithInheritedDirection(context.RightToLeft)));
    protected override void RenderControl(UiImmediateContext ui)
    {
        _input.Attach(ui);
        var selection = _selection!;
        selection.Synchronize();
        ui.RenderDropdown<TBehavior, TPresentation>(PrimaryInputId(Label), Label, selection, _input.Menu, _options, _readCountText);
    }
    private string ReadCountText()
    {
        if (_count != _selection!.Count || _countFormat != _options.SelectedCountFormat)
        {
            _count = _selection.Count;
            _countFormat = _options.SelectedCountFormat;
            _countText = string.Format(System.Globalization.CultureInfo.CurrentCulture, _countFormat, _count);
        }
        return _countText;
    }
    private protected override void ReleaseOwnedResources()
    {
        _input.Dispose();
        _selection = null;
        _countText = string.Empty;
        _countFormat = null;
        base.ReleaseOwnedResources();
    }
}

public class UiDropdown<TPresentation> : UiDropdown<UiDefaultDropdownBehavior, TPresentation>
    where TPresentation : IUiDropdownPresentation
{
    public UiDropdown(string label, UiBinding<string?> selection, UiSelectOption[] items,
        UiDropdownOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(label, selection, items, options, identity) { }

    public UiDropdown(string label, UiBinding<string[]> selection, UiSelectOption[] items,
        UiDropdownOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(label, selection, items, options, identity) { }
}

[UiWidgetStyle(typeof(UiDropdown<>), typeof(UiFluentDropdownPresentation))]
[UiComponent("Dropdown", UiComponentExposure.Fluent)]
public partial class UiFluentDropdown;
