namespace Duxel.Core;

public enum UiSpinButtonSize
{
    Medium, Small
}
public enum UiSpinButtonChangeReason
{
    Text, Increment, Decrement, PageIncrement, PageDecrement, Minimum, Maximum, Accessibility
}
public readonly record struct UiSpinButtonParseResult(bool Success, double? Value);
public readonly record struct UiSpinButtonChange(string Text, double? PreviousValue, double? RequestedValue,
    double? Value, UiSpinButtonChangeReason Reason, bool Accepted);

public readonly record struct UiSpinButtonOptions(double? Minimum = null, double? Maximum = null,
    double Step = 1, double PageStep = 1, int? Precision = null, float Width = 160,
    UiSpinButtonSize Size = UiSpinButtonSize.Medium, UiInputAppearance Appearance = UiInputAppearance.Outline,
    bool ReadOnly = false, bool Invalid = false, bool RightToLeft = false,
    string IncrementLabel = "Increment value", string DecrementLabel = "Decrement value")
{
    public UiSpinButtonOptions() : this(null, null, 1, 1, null, 160) { }
    internal UiSpinNumberPolicy Validate()
    {
        if (!float.IsFinite(Width) || Width <= 0 || (uint)Size > 1 || (uint)Appearance > 3)
            throw new ArgumentOutOfRangeException(nameof(Width));
        ArgumentException.ThrowIfNullOrWhiteSpace(IncrementLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(DecrementLabel);
        if (UiAccessibilityTree.ContainsControl(IncrementLabel, false) || UiAccessibilityTree.ContainsControl(DecrementLabel, false))
            throw new ArgumentException("SpinButton action labels must be single-line text.");
        return UiSpinNumberPolicy.Create(Minimum, Maximum, Step, PageStep, Precision);
    }
}

public readonly record struct UiSpinButtonLayout(UiInputLayout Input, UiRect Increment, UiRect Decrement);
public interface IUiSpinButtonPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiControlTypographyScope BeginText(UiControlContext context, UiSpinButtonOptions options);
    static abstract UiVector2 Measure(UiControlContext context, UiSpinButtonOptions options);
    static abstract UiSpinButtonLayout Arrange(UiControlContext context, UiRect bounds, UiSpinButtonOptions options);
    static abstract void Paint(UiControlContext context, UiSpinButtonLayout layout, UiInputEditResult edit,
        UiSpinButtonOptions options, UiButtonInteraction increment, UiButtonInteraction decrement, bool canIncrement, bool canDecrement);
}

/// <summary>A nullable numeric editor with one keyboard stop and independently invokable pointer steppers.</summary>
public class UiSpinButton<TPresentation> : UiControlWidget, IUiLabelTarget where TPresentation : IUiSpinButtonPresentation
{
    private UiBinding<double?>? _value;
    private UiSpinButtonOptions _options;
    internal UiSpinNumberPolicy Numbers
    {
        get; private set;
    }
    internal UiSpinInputState Input { get; } = new();
    internal UiTextInputOwner Editor { get; } = new();
    private Func<double?, string>? _formatter;
    private Func<string, UiSpinButtonParseResult>? _parser;
    public string Label
    {
        get;
    }
    public int MaxLength
    {
        get;
    }
    public UiBinding<double?> Value => _value ?? throw new ObjectDisposedException(GetType().Name);
    public Action<UiSpinButtonChange>? Changed
    {
        get; set;
    }
    public string DisplayText => Input.Draft;
    public UiSpinButtonOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            var numbers = value.Validate();
            numbers.ValidateValue(Value.Value);
            if (value == _options)
                return;
            ThrowIfBuilding();
            _options = value;
            Numbers = numbers;
            Input.DisplayDirty = true;
            RequestRebuild();
        }
    }
    public Func<double?, string>? Formatter
    {
        get => _formatter;
        set
        {
            ThrowIfDisposed();
            if (_formatter == value)
                return;
            ThrowIfBuilding();
            _formatter = value;
            Input.DisplayDirty = true;
            RequestRebuild();
        }
    }
    public Func<string, UiSpinButtonParseResult>? Parser
    {
        get => _parser;
        set
        {
            ThrowIfDisposed();
            if (_parser == value)
                return;
            ThrowIfBuilding();
            _parser = value;
            RequestRebuild();
        }
    }
    public UiSpinButton(string label, UiBinding<double?> value, UiSpinButtonOptions? options = null,
        int maxLength = 4096, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(value);
        if (UiAccessibilityTree.ContainsControl(label, false) || maxLength <= 0)
            throw new ArgumentException("SpinButton requires a single-line name and a positive text limit.");
        _options = options ?? new();
        Numbers = _options.Validate();
        Numbers.ValidateValue(value.Value);
        _value = value;
        Editor.FocusLost = OnFocusLost;
        Label = label;
        MaxLength = maxLength;
    }
    internal string Format(double? value)
    {
        Numbers.ValidateValue(value);
        var text = _formatter is { } format ? format(value) : Numbers.Format(value);
        if (text is null || text.Length > MaxLength || UiAccessibilityTree.ContainsControl(text, false))
            throw new InvalidOperationException("Formatted SpinButton text must satisfy its single-line maximum-length contract.");
        return text;
    }
    internal UiSpinButtonParseResult Parse(string text)
    {
        if (_parser is { } parse)
        {
            var result = parse(text);
            if (result.Success && result.Value is { } number)
            {
                if (!double.IsFinite(number))
                    throw new InvalidOperationException("SpinButton parsers cannot accept a nonfinite number.");
                return new(true, Numbers.Normalize(number));
            }
            return result;
        }
        return Numbers.TryCommit(text, out var value) ? new(true, value) : new(false, null);
    }
    internal bool Commit(string text, UiSpinButtonChangeReason reason, double? requested, bool parsed)
    {
        Input.Editing = false;
        var previous = Value.Value;
        if (parsed && requested != previous)
            Value.Value = requested;
        var actual = Value.Value;
        Numbers.ValidateValue(actual);
        var accepted = parsed && actual == requested;
        Input.LastModel = actual;
        Input.Draft = Format(actual);
        Input.Editing = Input.DisplayDirty = false;
        if (!accepted || actual != previous || text != Input.Draft)
            Changed?.Invoke(new(text, previous, requested, actual, reason, accepted));
        if (!Editor.IsDisposed && Value.Value != Input.LastModel)
        {
            Numbers.ValidateValue(Value.Value);
            Input.LastModel = Value.Value;
            Input.Draft = Format(Input.LastModel);
        }
        return accepted;
    }
    private void OnFocusLost(UiImmediateContext context, string id, string? destination)
    {
        if (!Input.Editing || destination == Input.Increment || destination == Input.Decrement)
            return;
        var previous = Input.Draft;
        var parsed = Parse(previous);
        Commit(previous, UiSpinButtonChangeReason.Text, parsed.Value, parsed.Success);
        if (Editor.IsDisposed)
            return;
        var current = Value.Value;
        context.ReconcileNumericFocusCommit(id, previous, Input.Draft,
            current is { } value ? new UiAccessibilityRangeInfo(value, Numbers.Minimum, Numbers.Maximum, Numbers.Step, Numbers.PageStep) : null,
            Input.BaseInvalid);
    }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(TPresentation.Measure(context, Options));
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderSpinButton<TPresentation>(this, PrimaryInputId(Label));
    private protected override void ReleaseOwnedResources()
    {
        try
        {
            Editor.Dispose();
        }
        finally { Input.Dispose(); _value = null; _formatter = null; _parser = null; Changed = null; base.ReleaseOwnedResources(); }
    }
}

[UiWidgetStyle(typeof(UiSpinButton<>), typeof(UiFluentSpinButtonPresentation))]
[UiComponent("SpinButton", UiComponentExposure.Fluent)]
public partial class UiFluentSpinButton;

internal sealed class UiSpinInputState : IDisposable
{
    private WeakReference<UiImmediateContext>? _context;
    internal string Draft = string.Empty;
    internal double? LastModel;
    internal bool Initialized, Editing, DisplayDirty;
    internal bool BaseInvalid;
    internal bool CancelledHold;
    internal double? RepeatAt;
    internal double RepeatStarted;
    internal int Direction, SeenFrame = int.MinValue;
    internal string? Increment, Decrement;
    internal void Bind(UiImmediateContext context, int frame)
    {
        if (_context is not null && _context.TryGetTarget(out var previous) && !ReferenceEquals(context, previous))
        {
            previous.ReleaseSpinCapture(Increment, Decrement);
            Initialized = false;
            Stop();
        }
        if (SeenFrame != frame - 1)
            Stop();
        _context ??= new(context);
        _context.SetTarget(context);
        SeenFrame = frame;
    }
    internal void Stop()
    {
        Direction = 0;
        RepeatAt = null;
    }
    public void Dispose()
    {
        if (_context is not null && _context.TryGetTarget(out var context))
            context.ReleaseSpinCapture(Increment, Decrement);
        _context = null;
        Draft = string.Empty;
        Stop();
    }
}
