namespace Duxel.Core;

public readonly record struct UiSelectOptions(float Width = 250, UiDropdownSize Size = UiDropdownSize.Medium,
    UiInputAppearance Appearance = UiInputAppearance.Outline, bool RightToLeft = false,
    float? MaximumPopupHeight = null, bool Invalid = false, string ExpandLabel = "Show options")
{
    public UiSelectOptions() : this(250, UiDropdownSize.Medium, UiInputAppearance.Outline, false, null, false, "Show options") { }
    internal UiDropdownOptions Dropdown => new(Width, Size, Appearance, RightToLeft : RightToLeft,
        MaximumPopupHeight : MaximumPopupHeight, Invalid : Invalid, ExpandLabel : ExpandLabel);
    internal void Validate() => Dropdown.Validate();
}

/// <summary>A plain single-choice form control. Rich or multiple options belong to Dropdown.</summary>
public class UiSelect<TBehavior, TPresentation> : UiControlWidget, IUiLabelTarget
    where TBehavior : IUiMenuBehavior
    where TPresentation : IUiDropdownPresentation
{
    private UiOptionSelection? _selection;
    private readonly UiOwnedMenuState _input = new();
    private UiSelectOptions _options;
    public string Label { get; }
    public bool IsOpen => _input.Menu.Open;
    public ReadOnlySpan<UiSelectOption> Items => _selection?.Options ?? [];
    public UiSelectOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed(); value.Validate();
            if (_options == value) return;
            ThrowIfBuilding(); _options = value; RequestRebuild();
        }
    }
    public UiSelect(string label, UiBinding<string?> selection, UiSelectOption[] items,
        UiSelectOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(items);
        _options = options ?? new(); _options.Validate();
        if (items.Any(item => !item.Icon.IsEmpty || item.Description is not null))
            throw new ArgumentException("Select accepts plain text options. Use Dropdown for icons or descriptions.", nameof(items));
        _selection = new(items, selection) { Required = Array.Exists(items, static item => item.Enabled) };
        _selection.Synchronize();
        if (selection.Value is null && Array.FindIndex(items, static item => item.Enabled) is var first && first >= 0)
        {
            // Initialize once; a controlled setter may reject the default without a render-time retry loop.
            selection.Value = items[first].Value;
            _selection.Synchronize();
        }
        Label = label;
    }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context)
        => context.LayoutConstraints.Constrain(TPresentation.Measure(context, _options.Dropdown.WithInheritedDirection(context.RightToLeft)));
    protected override void RenderControl(UiImmediateContext ui)
    {
        _input.Attach(ui);
        var selection = _selection!;
        selection.Synchronize();
        ui.RenderDropdown<TBehavior, TPresentation>(PrimaryInputId(Label), Label, selection, _input.Menu, _options.Dropdown,
            static () => string.Empty, commitClosedSelection : true);
    }
    private protected override void ReleaseOwnedResources()
    {
        _input.Dispose(); _selection = null; base.ReleaseOwnedResources();
    }
}

public class UiSelect<TPresentation>(string label, UiBinding<string?> selection, UiSelectOption[] items,
    UiSelectOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
    : UiSelect<UiDefaultDropdownBehavior, TPresentation>(label, selection, items, options, identity)
    where TPresentation : IUiDropdownPresentation;

[UiWidgetStyle(typeof(UiSelect<>), typeof(UiFluentSelectPresentation))]
[UiComponent("Select", UiComponentExposure.Fluent)]
public partial class UiFluentSelect;
