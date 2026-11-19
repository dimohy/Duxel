namespace Duxel.Core;

public readonly record struct UiComboboxOptions(UiDropdownOptions? Field = null, int MaxLength = 4096, bool Freeform = false, string EmptyText = "No matches")
{
    public UiComboboxOptions() : this(null, 4096, false) { }
    internal UiDropdownOptions FieldOptions => Field ?? new();
    internal void Validate()
    {
        FieldOptions.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(EmptyText);
        if (UiAccessibilityTree.ContainsControl(EmptyText, false))
            throw new ArgumentException("Empty-result text must be single-line.", nameof(EmptyText));
        if (MaxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxLength));
    }
}

public interface IUiComboboxPresentation : IUiDropdownPresentation
{
    static abstract UiControlTypographyScope BeginText(UiControlContext context, UiDropdownOptions options);
    static abstract UiInputLayout EditingLayout(UiControlContext context, UiRect bounds, UiDropdownOptions options);
    static abstract void PaintEditor(UiControlContext context, UiInputLayout layout, UiInputEditResult edit, UiDropdownOptions options, bool open);
}

/// <summary>One text editor, canonical selection and filtered popup retain their independent state and ownership.</summary>
public class UiCombobox<TPresentation> : UiControlWidget, IUiLabelTarget where TPresentation : IUiComboboxPresentation
{
    private UiOptionSelection? _selection;
    private UiFilteredOptionSelection? _filtered;
    private UiBinding<string>? _text;
    private UiBinding<bool>? _open;
    private Action<bool>? _openChanged;
    private bool _lastReportedOpen;
    private readonly UiTextInputOwner _editor = new();
    private readonly UiOwnedMenuState _popup = new();
    private readonly UiMenuInputState _logical = new();
    private UiComboboxOptions _options;
    public string Label
    {
        get;
    }
    public bool IsOpen => _popup.Menu.Open;
    /// <summary>Optional authoritative state. Its setter receives requests and may reject them by retaining the previous value.</summary>
    public UiBinding<bool>? Open
    {
        get => _open;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(value, _open))
                return;
            ThrowIfBuilding();
            _open = value;
            RequestRebuild();
        }
    }
    /// <summary>Notifies actual observed transitions once; rejected requests produce no transition notification.</summary>
    public Action<bool>? OpenChanged
    {
        get => _openChanged;
        set
        {
            ThrowIfDisposed();
            _openChanged = value;
        }
    }
    public string SelectedText => _selection?.Text ?? string.Empty;
    public ReadOnlySpan<UiSelectOption> Items => _selection?.Options ?? [];
    public UiBinding<string> Text => _text ?? throw new ObjectDisposedException(GetType().Name);
    public UiComboboxOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (value == _options)
                return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    public UiCombobox(string label, UiBinding<string> text, UiBinding<string?> selection, UiSelectOption[] items,
        UiComboboxOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(text);
        _options = options ?? new();
        _options.Validate();
        Label = label;
        _text = text;
        _selection = new(items, selection);
        _filtered = new(_selection);
        _popup.Menu.CanChangeOpen = RequestOpen;
    }
    public UiCombobox(string label, UiBinding<string> text, UiBinding<string[]> selection, UiSelectOption[] items,
        UiComboboxOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(text);
        _options = options ?? new();
        _options.Validate();
        Label = label;
        _text = text;
        _selection = new(items, selection);
        _filtered = new(_selection);
        _popup.Menu.CanChangeOpen = RequestOpen;
    }
    internal UiComboboxCompletion? Completion { get; set; }
    internal UiCombobox(string label, UiBinding<string> text, UiOptionSelection selection, UiComboboxOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        options.Validate();
        Label = label;
        _text = text;
        _selection = selection;
        _options = options;
        _filtered = new(selection) { ExcludeSelected = true };
        _popup.Menu.CanChangeOpen = RequestOpen;
    }
    private bool RequestOpen(bool value)
    {
        if (_open is not { } binding)
            return true;
        if (binding.Value == value)
            return true;
        binding.Value = value;
        return binding.Value == value;
    }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context)
        => context.LayoutConstraints.Constrain(TPresentation.Measure(context, _options.FieldOptions.WithInheritedDirection(context.RightToLeft)));
    protected override void RenderControl(UiImmediateContext ui)
    {
        _popup.Attach(ui);
        _selection!.Synchronize();
        var text = Text.Value ?? throw new InvalidOperationException("Combobox text cannot be null.");
        if (text.Length > _options.MaxLength || UiAccessibilityTree.ContainsControl(text, false))
            throw new InvalidOperationException("Combobox text exceeds its single-line input contract.");
        if (ui.RenderCombobox<TPresentation>(PrimaryInputId(Label), Label, ref text, _selection, _filtered!,
            _popup.Menu, _logical, _editor, _options, _open, Completion))
            Text.Value = text;
        if (_lastReportedOpen != IsOpen)
        {
            _lastReportedOpen = IsOpen;
            _openChanged?.Invoke(IsOpen);
        }
    }
    private protected override void ReleaseOwnedResources()
    {
        try
        {
            _editor.Dispose();
        }
        finally
        {
            _popup.Menu.CanChangeOpen = null;
            _popup.Dispose();
            _selection = null;
            _filtered = null;
            _text = null;
            _open = null;
            _openChanged = null;
            base.ReleaseOwnedResources();
        }
    }
}

[UiWidgetStyle(typeof(UiCombobox<>), typeof(UiFluentComboboxPresentation))]
[UiComponent("Combobox", UiComponentExposure.Fluent)]
public partial class UiFluentCombobox;
